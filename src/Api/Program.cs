using System.Text.Json;
using System.Text.Json.Serialization;
using Engine;

var builder = WebApplication.CreateBuilder(args);

string repoRoot = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".."));
string dataDir = Path.Combine(repoRoot, builder.Configuration.GetValue<string>("DataDir") ?? "data");

builder.Services.AddSingleton(new ProductStore(dataDir));

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));


builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

app.UseCors();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (ArgumentException e) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { errors = new[] { e.Message } });
    }
});

app.MapGet("/health", () => "ok");

var products = app.MapGroup("/products");

products.MapGet("/", (ProductStore store) => store.List());

products.MapGet("/{id}/draft", (string id, ProductStore store) =>
    store.GetDraft(id) is Product draft ? Results.Ok(draft) : Results.NotFound());

products.MapPut("/{id}/draft", (string id, Product body, ProductStore store) =>
{
    if (body.Id != id)
        return Results.BadRequest(new { errors = new[] { $"L'identifiant du corps ('{body.Id}') ne correspond pas a l'URL ('{id}')." } });

    store.SaveDraft(body);
    return Results.NoContent();
});

products.MapPost("/{id}/publish", (string id, ProductStore store) =>
{
    if (store.GetDraft(id) == null)
        return Results.NotFound();

    List<string> errors = store.Publish(id);
    if (errors.Count > 0)
        return Results.UnprocessableEntity(new { errors });

    return Results.Ok(new { version = LatestVersion(store, id) });
});

products.MapGet("/{id}/published", (string id, int? version, ProductStore store) =>
    store.Get(id, version) is Product product ? Results.Ok(product) : Results.NotFound());

products.MapPost("/{id}/evaluate", (string id, int? version, Dictionary<string, JsonElement>? body, ProductStore store) =>
{
    int? resolved = version ?? LatestVersion(store, id);
    if (resolved is not int v || store.Get(id, v) is not Product product)
        return Results.NotFound();

    Dictionary<string, object?> input = (body ?? new()).ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
    return Results.Ok(Evaluator.Evaluate(product, v, input));
});

app.Run();

static int? LatestVersion(ProductStore store, string id) =>
    store.List().FirstOrDefault(p => p.Id == id)?.PublishedVersion;
