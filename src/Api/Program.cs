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

app.MapGet("/health", () => "ok");

app.Run();
