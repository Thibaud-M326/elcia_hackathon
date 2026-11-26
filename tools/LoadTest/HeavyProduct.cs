using System.Text.Json;
using Engine;

// Produit volontairement lourd pour le test de charge : beaucoup de paramètres, de règles et une
// grande grille, pour que chaque evaluate fasse un vrai travail.
//   2 dimensions + 20 listes de 10 choix + 40 options (prix fixe / pourcentage / par unité)
//   40 disponibilités (2 conditions), 18 implications en 2 chaînes de 10, 10 incompatibilités,
//   40 plus-values (2 conditions), grille 20 x 20
// Il reste publiable : les implications et les incompatibilités portent sur des options
// différentes, aucune option ne peut en entraîner deux incompatibles.
static class HeavyProduct
{
    public const string Id = "bench-lourd";
    public const int ChoiceParams = 20;
    public const int ChoicesPerParam = 10;
    public const int Options = 40;

    static JsonElement J(object value) => JsonSerializer.SerializeToElement(value);

    static string ChoiceId(int i) => $"liste{i}";
    static string Choice(int k) => $"choix-{k}";
    public static string OptionId(int i) => $"option{i}";

    public static Product Build()
    {
        var parameters = new List<Parameter>
        {
            new() { Id = "largeur", Label = "Largeur (mm)", Type = ParamType.Number, Min = 600, Max = 2400, Default = J(1200) },
            new() { Id = "hauteur", Label = "Hauteur (mm)", Type = ParamType.Number, Min = 600, Max = 2100, Default = J(1200) },
        };

        for (int i = 1; i <= ChoiceParams; i++)
        {
            parameters.Add(new Parameter
            {
                Id = ChoiceId(i),
                Label = $"Liste {i}",
                Type = ParamType.Choice,
                Choices = Enumerable.Range(1, ChoicesPerParam).Select(Choice).ToList(),
                Default = J(Choice(1)),
            });
        }

        for (int i = 1; i <= Options; i++)
        {
            Price price = (i % 3) switch
            {
                0 => new Price { Type = PriceType.Fixed, Amount = 10 + i },
                1 => new Price { Type = PriceType.Percent, Rate = 1 + i % 10 },
                _ => new Price { Type = PriceType.PerUnit, Rate = 0.01m * (1 + i % 5), Param = i % 2 == 0 ? "largeur" : "hauteur" },
            };
            parameters.Add(new Parameter { Id = OptionId(i), Label = $"Option {i}", Type = ParamType.Option, Default = J(false), Price = price });
        }

        var rules = new List<Rule>();

        // Disponibilités : chaque option dépend d'une liste (7 choix sur 10) et de la largeur
        for (int i = 1; i <= Options; i++)
        {
            string list = ChoiceId(1 + i % ChoiceParams);
            rules.Add(new Rule
            {
                Id = $"dispo-{i}",
                Type = RuleType.AvailableIf,
                Option = OptionId(i),
                When =
                {
                    new Condition { Param = list, Op = Op.In, Value = J(Enumerable.Range(1, 7).Select(Choice).ToArray()) },
                    new Condition { Param = "largeur", Op = Op.Lte, Value = J(2300) },
                },
                Message = $"Option {i} impossible en {{{list}}} ou au-delà de 2300 mm (largeur {{largeur}}).",
            });
        }

        // Implications : deux chaînes option1 -> ... -> option10 et option11 -> ... -> option20 (cascade)
        foreach (int start in new[] { 1, 11 })
        {
            for (int i = start; i < start + 9; i++)
                rules.Add(new Rule { Id = $"implique-{i}", Type = RuleType.Implies, Option = OptionId(i), Other = OptionId(i + 1) });
        }

        // Incompatibilités entre options 21 à 40, par paires
        for (int i = 21; i < 40; i += 2)
        {
            rules.Add(new Rule
            {
                Id = $"incompatible-{i}",
                Type = RuleType.Incompatible,
                Option = OptionId(i),
                Other = OptionId(i + 1),
                Message = $"Option {i} et option {i + 1} sont incompatibles.",
            });
        }

        // Plus-values : un choix d'une liste ET une largeur minimale
        for (int i = 1; i <= 40; i++)
        {
            rules.Add(new Rule
            {
                Id = $"plus-value-{i}",
                Type = RuleType.Surcharge,
                Label = $"Plus-value {i}",
                When =
                {
                    new Condition { Param = ChoiceId(1 + i % ChoiceParams), Op = Op.Neq, Value = J(Choice(1 + i % ChoicesPerParam)) },
                    new Condition { Param = "largeur", Op = Op.Gte, Value = J(800 + i * 20) },
                },
                Price = i % 2 == 0 ? new Price { Type = PriceType.Fixed, Amount = 5 + i } : new Price { Type = PriceType.Percent, Rate = 1 + i % 5 },
            });
        }

        // Grille 20 x 20, prix croissants
        var widths = Enumerable.Range(0, 20).Select(k => 600m + k * 95).ToList();
        widths[^1] = 2400;
        var heights = Enumerable.Range(0, 20).Select(k => 600m + k * 79).ToList();
        heights[^1] = 2100;
        var prices = heights.Select((_, r) => widths.Select((_, c) => (decimal?)(250 + r * 21 + c * 17)).ToList()).ToList();

        return new Product
        {
            Id = Id,
            Label = "Produit de test de charge",
            Manufacturer = "Benchmark",
            Parameters = parameters,
            Rules = rules,
            Grid = new Grid { Label = "Prix de base", Widths = widths, Heights = heights, Prices = prices },
        };
    }

    // Configuration aléatoire complète : toutes les saisies renseignées, environ la moitié des
    // options cochées. Une partie sera invalide (incompatibilités, options indisponibles) : c'est
    // voulu, le moteur fait alors tout le travail de vérification.
    public static Dictionary<string, object> RandomConfig(Random random)
    {
        var config = new Dictionary<string, object>
        {
            ["largeur"] = random.Next(600, 2401),
            ["hauteur"] = random.Next(600, 2101),
        };
        for (int i = 1; i <= ChoiceParams; i++)
            config[ChoiceId(i)] = Choice(random.Next(1, ChoicesPerParam + 1));
        for (int i = 1; i <= Options; i++)
            config[OptionId(i)] = random.Next(2) == 1;
        return config;
    }
}
