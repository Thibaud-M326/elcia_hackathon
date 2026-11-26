using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine;

// Test de charge de POST /products/{id}/evaluate, à débit constant.
//
//   dotnet run -c Release --project tools/LoadTest -- [options]
//     --url http://localhost:5080   adresse de l'API
//     --rate 5000                   requêtes par minute (plusieurs paliers : --rate 5000,20000,50000)
//     --duration 60                 durée de chaque palier, en secondes
//     --warmup 10                   chauffe avant de mesurer, en secondes (non comptée)
//     --product bench-lourd         produit chiffré ; bench-lourd est créé et publié s'il le faut
//     --max                         débit maximal au lieu d'un débit constant (voir plus bas)
//     --concurrency 64              avec --max : nombre de clients en parallèle (plusieurs : 16,64,256)
//
// Mode --max ("closed model") : N clients envoient chacun une requête, attendent la réponse, puis
// renvoient aussitôt, pendant --duration secondes. On compte combien de requêtes passent : c'est la
// capacité de l'API. La latence monte forcément, le serveur étant saturé.
//
// Débit constant ("open model") : chaque requête part à son heure prévue, même si les précédentes
// n'ont pas encore répondu. La latence est comptée depuis l'heure prévue : un serveur qui ralentit
// ne peut pas cacher son retard en recevant moins de requêtes.
//
// Chaque latence est découpée grâce à l'en-tête Server-Timing de l'API :
//   moteur  = Evaluator.Evaluate seul
//   serveur = de l'arrivée de la requête au début de la réponse (moteur compris)
//   réseau  = total - serveur (transport, TLS, file d'attente, lecture de la réponse)

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

string url = Arg("--url", "http://localhost:5080").TrimEnd('/');
int[] rates = Arg("--rate", "5000").Split(',').Select(int.Parse).ToArray();
int duration = int.Parse(Arg("--duration", "60"));
int warmup = int.Parse(Arg("--warmup", "10"));
bool max = args.Contains("--max");
int[] concurrencies = Arg("--concurrency", "64").Split(',').Select(int.Parse).ToArray();
string productId = Arg("--product", HeavyProduct.Id);

var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
};

using var http = new HttpClient(new SocketsHttpHandler
{
    MaxConnectionsPerServer = 512,
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    AutomaticDecompression = DecompressionMethods.All,
})
{
    BaseAddress = new Uri(url),
    Timeout = TimeSpan.FromSeconds(60),
};

Console.WriteLine($"Cible   : {url}");
await PrepareProduct();

var random = new Random(42);
var configs = Enumerable.Range(0, 2000).Select(_ => HeavyProduct.RandomConfig(random)).ToArray();
string evaluatePath = $"/products/{Uri.EscapeDataString(productId)}/evaluate";

// Première requête seule, avec un long délai : réveille un hébergement endormi (Render gratuit)
var wake = Stopwatch.StartNew();
await Send(0, 0);
Console.WriteLine($"Réveil  : première requête en {wake.Elapsed.TotalMilliseconds:0} ms");

if (max)
{
    foreach (int concurrency in concurrencies)
    {
        if (warmup > 0)
        {
            Console.WriteLine($"Chauffe : {warmup} s, {concurrency} clients...");
            await RunMax(concurrency, warmup);
        }
        Console.WriteLine($"Mesure  : débit max pendant {duration} s, {concurrency} clients en parallèle...");
        var sw = Stopwatch.StartNew();
        List<Sample> samples = await RunMax(concurrency, duration);
        Report($"Débit max, {concurrency} clients, pendant {duration} s", samples, sw.Elapsed);
    }
    return;
}

foreach (int ratePerMinute in rates)
{
    if (warmup > 0)
    {
        Console.WriteLine($"Chauffe : {warmup} s à {ratePerMinute} req/min...");
        await RunStep(ratePerMinute, warmup);
    }
    var sw = Stopwatch.StartNew();
    Console.WriteLine($"Mesure  : {duration} s à {ratePerMinute} req/min ({ratePerMinute / 60.0:0.#} req/s)...");
    List<Sample> samples = await RunStep(ratePerMinute, duration);
    Report($"{ratePerMinute} req/min pendant {duration} s", samples, sw.Elapsed);
}

// ---------------------------------------------------------------------------------------------

async Task PrepareProduct()
{
    if (productId != HeavyProduct.Id)
        return;

    Product product = HeavyProduct.Build();
    List<string> errors = Validator.Validate(product);
    if (errors.Count > 0)
        throw new InvalidOperationException("Produit lourd invalide : " + string.Join(" | ", errors));

    var put = await http.PutAsJsonAsync($"/products/{HeavyProduct.Id}/draft", product, json);
    put.EnsureSuccessStatusCode();
    var publish = await http.PostAsync($"/products/{HeavyProduct.Id}/publish", null);
    string body = await publish.Content.ReadAsStringAsync();
    if (!publish.IsSuccessStatusCode)
        throw new InvalidOperationException($"Publication refusée ({(int)publish.StatusCode}) : {body}");

    Console.WriteLine($"Produit : {HeavyProduct.Id} publié ({body}) : {product.Parameters.Count} paramètres, "
        + $"{product.Rules.Count} règles, grille {product.Grid.Widths.Count} x {product.Grid.Heights.Count}");
}

async Task<List<Sample>> RunStep(int ratePerMinute, int seconds)
{
    int count = ratePerMinute * seconds / 60;
    double intervalMs = 60_000.0 / ratePerMinute;
    var pending = new List<Task<Sample>>(count);
    var clock = Stopwatch.StartNew();

    for (int i = 0; i < count; i++)
    {
        double due = i * intervalMs;
        double wait = due - clock.Elapsed.TotalMilliseconds;
        if (wait > 1)
            await Task.Delay(TimeSpan.FromMilliseconds(wait));
        else
            while (clock.Elapsed.TotalMilliseconds < due) Thread.SpinWait(50);

        long scheduled = clock.ElapsedTicks;
        pending.Add(Send(i, scheduled, clock));
    }

    return (await Task.WhenAll(pending)).ToList();
}

// Chaque client enchaîne : envoyer, attendre la réponse, recommencer, jusqu'à la fin du temps
async Task<List<Sample>> RunMax(int concurrency, int seconds)
{
    var samples = new ConcurrentBag<Sample>();
    var clock = Stopwatch.StartNew();
    long end = Stopwatch.Frequency * seconds;
    int next = 0;

    async Task Client()
    {
        while (clock.ElapsedTicks < end)
            samples.Add(await Send(Interlocked.Increment(ref next), clock.ElapsedTicks, clock));
    }

    await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => Task.Run(Client)));
    return samples.ToList();
}

async Task<Sample> Send(int index, long scheduledTicks, Stopwatch? clock = null)
{
    clock ??= Stopwatch.StartNew();
    var content = JsonContent.Create(configs[index % configs.Length], options: json);
    try
    {
        using var response = await http.PostAsync(evaluatePath, content);
        await response.Content.ReadAsByteArrayAsync();
        double total = (clock.ElapsedTicks - scheduledTicks) * 1000.0 / Stopwatch.Frequency;

        if (!response.IsSuccessStatusCode)
            return Sample.Failed($"HTTP {(int)response.StatusCode}", total);

        (double? engine, double? server) = ParseServerTiming(response);
        return new Sample(true, null, total, engine, server);
    }
    catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
    {
        double total = (clock.ElapsedTicks - scheduledTicks) * 1000.0 / Stopwatch.Frequency;
        return Sample.Failed(e is TaskCanceledException ? "délai dépassé" : e.Message, total);
    }
}

static (double? engine, double? server) ParseServerTiming(HttpResponseMessage response)
{
    if (!response.Headers.TryGetValues("Server-Timing", out var values))
        return (null, null);

    double? engine = null, server = null;
    foreach (string metric in string.Join(",", values).Split(','))
    {
        string[] parts = metric.Trim().Split(";dur=");
        if (parts.Length != 2 || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ms))
            continue;
        if (parts[0] == "engine") engine = ms;
        if (parts[0] == "server") server = ms;
    }
    return (engine, server);
}

void Report(string title, List<Sample> samples, TimeSpan elapsed)
{
    var ok = samples.Where(s => s.Ok).ToList();
    var failed = samples.Where(s => !s.Ok).ToList();

    Console.WriteLine();
    double perSecond = ok.Count / elapsed.TotalSeconds;
    Console.WriteLine($"=== {title} ===");
    Console.WriteLine($"Envoyées {samples.Count}   réussies {ok.Count}   erreurs {failed.Count}   "
        + $"débit réel {perSecond:0.0} req/s ({perSecond * 60:N0} req/min)");
    foreach (var group in failed.GroupBy(f => f.Error).OrderByDescending(g => g.Count()).Take(5))
        Console.WriteLine($"  erreur x{group.Count()} : {group.Key}");

    Console.WriteLine($"{"",-10}{"p50",10}{"p95",10}{"p99",10}{"max",10}");
    Line("moteur", ok.Where(s => s.Engine.HasValue).Select(s => s.Engine!.Value));
    Line("serveur", ok.Where(s => s.Server.HasValue).Select(s => s.Server!.Value));
    Line("réseau", ok.Where(s => s.Server.HasValue).Select(s => Math.Max(0, s.Total - s.Server!.Value)));
    Line("total", ok.Select(s => s.Total));
    Console.WriteLine("(en millisecondes ; total = du départ prévu de la requête à la fin de la réponse)");
    Console.WriteLine();
}

static void Line(string label, IEnumerable<double> values)
{
    double[] sorted = values.OrderBy(v => v).ToArray();
    if (sorted.Length == 0)
    {
        Console.WriteLine($"{label,-10}{"-",10}{"-",10}{"-",10}{"-",10}");
        return;
    }
    Console.WriteLine($"{label,-10}{P(sorted, 50),10:0.000}{P(sorted, 95),10:0.000}{P(sorted, 99),10:0.000}{sorted[^1],10:0.000}");
}

static double P(double[] sorted, double percentile)
    => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(percentile / 100 * sorted.Length) - 1)];

string Arg(string name, string fallback)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

record Sample(bool Ok, string? Error, double Total, double? Engine, double? Server)
{
    public static Sample Failed(string error, double total) => new(false, error, total, null, null);
}
