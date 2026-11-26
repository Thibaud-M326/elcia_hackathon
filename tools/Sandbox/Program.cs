using System.Globalization;
using Engine;

// Bac à sable : évaluer une configuration sans l'API.
//
//   dotnet run --project tools/Sandbox                              -> scénarios de référence
//   dotnet run --project tools/Sandbox -- couleur=gris motorisation=true dormant=50

string path = Path.Combine(AppContext.BaseDirectory, "product-model-v1.json");
Product product = Json.ReadProduct(path);

if (args.Length > 0)
{
    Show(ParseArgs(args));
}
else
{
    Show(new());
    Show(new() { ["couleur"] = "gris", ["motorisation"] = true });
    Show(new() { ["couleur"] = "gris", ["motorisation"] = true, ["dormant"] = 50m });
    Show(new() { ["largeur"] = 2400m, ["hauteur"] = 2100m });
}

void Show(Dictionary<string, object?> input)
{
    EvaluationResult r = Evaluator.Evaluate(product, 1, input);

    Console.WriteLine("==================================================");
    Console.WriteLine($"Configuration : {Describe(input)}");
    Console.WriteLine("--------------------------------------------------");

    foreach (PriceLine line in r.PriceLines)
        Console.WriteLine($"  {line.Label,-28}{line.Amount,12:N2} €");

    string total = r.Total == null ? "-" : $"{r.Total:N2} €";
    Console.WriteLine($"  {"TOTAL",-28}{total,14}   {(r.Valid ? "VALIDE" : "INVALIDE")}");

    if (r.Options.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Options :");
        foreach (OptionState o in r.Options)
        {
            string box = o.Selected ? "[x]" : "[ ]";
            string tags = (o.AutoAdded ? " [Auto]" : "") + (o.Available ? "" : " [Indisponible]");
            Console.WriteLine($"    {box} {o.Label}{tags}");
            foreach (string reason in o.Reasons)
                Console.WriteLine($"          raison : {reason}");
        }
    }

    if (r.Errors.Count > 0)
    {
        Console.WriteLine();
        Console.WriteLine("  Erreurs :");
        foreach (string error in r.Errors)
            Console.WriteLine($"    - {error}");
    }

    Console.WriteLine();
}

// "largeur=1500" -> nombre, "volet=true" -> booléen, "couleur=gris" -> texte
static Dictionary<string, object?> ParseArgs(string[] args)
{
    Dictionary<string, object?> input = new();

    foreach (string arg in args)
    {
        string[] parts = arg.Split('=', 2);
        if (parts.Length != 2)
        {
            Console.WriteLine($"Argument ignoré (format attendu clé=valeur) : {arg}");
            continue;
        }

        string text = parts[1];
        if (bool.TryParse(text, out bool b))
            input[parts[0]] = b;
        else if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d))
            input[parts[0]] = d;
        else
            input[parts[0]] = text;
    }

    return input;
}

static string Describe(Dictionary<string, object?> input)
{
    if (input.Count == 0)
        return "(défauts)";

    List<string> parts = new();
    foreach (KeyValuePair<string, object?> entry in input)
        parts.Add($"{entry.Key}={entry.Value}");
    return string.Join(", ", parts);
}
