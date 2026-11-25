using System.Text.Json;

namespace Engine;

public static class Evaluator
{
    public static EvaluationResult Evaluate(Product p, int version, IDictionary<string, object?> input)
    {
        EvaluationResult evalRes = new();

        Dictionary<string, object> values = new();

        evalRes.Version = version;

        foreach (Parameter param in p.Parameters)
        {
            if (!input.TryGetValue(param.Id, out object? raw))
                raw = DefaultOf(param);

            if (raw == null)
            {
                evalRes.Errors.Add($"{param.Label} : valeur manquante.");
                continue;
            }

            object? value = Values.Normalize(raw, param.Type);

            if (value == null)
            {
                string expectedRes = param.Type switch
                {
                    ParamType.Number => "un nombre",
                    ParamType.Option => "oui ou non",
                    ParamType.Choice => "un texte",
                    _ => "une valeur"
                };
                evalRes.Errors.Add($"{param.Label} : {expectedRes} est attendu.");
                continue;
            }

            if (param.Type == ParamType.Number)
            {
                decimal d = (decimal)value;

                if (param.Min != null && d < param.Min)
                {
                    evalRes.Errors.Add($"{param.Label} : minimum {param.Min} (saisi : {value}).");
                    continue;
                }

                if (param.Max != null && d > param.Max)
                {
                    evalRes.Errors.Add($"{param.Label} : maximum {param.Max} (saisi : {value}).");
                    continue;
                }
            }

            if (param.Type == ParamType.Choice)
            {
                string str = (string)value;

                if (param.Choices == null || !param.Choices.Contains(str))
                {
                    evalRes.Errors.Add($"{param.Label} : '{str}' n'est pas un choix possible.");
                    continue;
                }
                
            }

            values[param.Id] = value;
        }

        foreach (string key in input.Keys)
        {
            if (!p.Parameters.Any(param => param.Id == key))
            {
                evalRes.Errors.Add($"Paramètre inconnu : {key}.");
            }
        }

        if (evalRes.Errors.Count > 0)
            return evalRes;

        HashSet<string> autoAdded = Cascade(p, values);
        AddOptionStates(evalRes, p, values, autoAdded);

        decimal width = (decimal)values["largeur"];
        decimal height = (decimal)values["hauteur"];

        int row = Step(p.Grid.Heights, height);
        int col = Step(p.Grid.Widths, width);

        decimal? basePrice = row >= 0 && col >= 0 ? p.Grid.Prices[row][col] : null;

        evalRes.Values = values;

        if (basePrice == null)
        {
            evalRes.Errors.Add($"Dimensions {width} × {height} non fabricables.");
            return evalRes;
        }

        evalRes.PriceLines.Add(new PriceLine { Label = "Prix de base", Amount = basePrice.Value });
        
        var selectedOptions = p.Parameters.Where(x =>
            x.Type == ParamType.Option && x.Price != null && (bool)values[x.Id]);

        foreach (Parameter option in selectedOptions)
        {
            decimal amount = Amount(option.Price!, basePrice.Value, values);
            evalRes.PriceLines.Add(new PriceLine { Label = option.Label, Amount = amount });
        }

        evalRes.Valid = evalRes.Errors.Count == 0;
        return evalRes;
    }

    private static object? DefaultOf(Parameter param)
    {
        if (param.Default != null)
            return param.Default.Value;

        return param.Type switch
        {
            ParamType.Choice => param.Choices?.FirstOrDefault(),
            ParamType.Option => false,
            _ => null,
        };
    }

    private static int Step(List<decimal> steps, decimal value)
    {
        for (int i = 0; i < steps.Count; i++)
        {
            if (value <= steps[i])
                return i;
        }

        return -1;
    }

    internal static bool Matches(Dictionary<string, object> values, List<Condition> when)
        => when.All(c => Holds(values, c));

    internal static bool Holds(Dictionary<string, object> values, Condition c)
    {
        if (!values.TryGetValue(c.Param, out object? actual))
            return false;

        ParamType? type = actual switch
        {
            decimal => ParamType.Number,
            string => ParamType.Choice,
            bool => ParamType.Option,
            _ => null,
        };

        if (type == null)
            return false;

        return c.Op switch
        {
            Op.Eq => Equals(actual, Values.Normalize(c.Value, type.Value)),
            Op.Neq => !Equals(actual, Values.Normalize(c.Value, type.Value)),
            Op.Gte => actual is decimal a && Values.Normalize(c.Value, type.Value) is decimal b && a >= b,
            Op.Lte => actual is decimal a && Values.Normalize(c.Value, type.Value) is decimal b && a <= b,
            Op.In => IsInList(actual, c.Value, type.Value),
            _ => false,
        };
    }

    private static bool IsInList(object actual, JsonElement list, ParamType type)
    {
        if (list.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement element in list.EnumerateArray())
        {
            if (Equals(actual, Values.Normalize(element, type)))
                return true;
        }

        return false;
    }

    private static HashSet<string> Cascade(Product p, Dictionary<string, object> values)
    {
        HashSet<string> autoAdded = new();
        bool changed = true;

        for (int round = 0; changed && round < 1000; round++)
        {
            changed = false;

            foreach (Rule rule in p.Rules)
            {
                if (rule.Type != RuleType.Implies)
                    continue;

                if (IsSelected(values, rule.Option) && !IsSelected(values, rule.Other))
                {
                    values[rule.Other!] = true;
                    autoAdded.Add(rule.Other!);
                    changed = true;
                }
            }
        }

        return autoAdded;
    }

    private static void AddOptionStates(EvaluationResult evalRes, Product p,
        Dictionary<string, object> values, HashSet<string> autoAdded)
    {
        foreach (Parameter param in p.Parameters)
        {
            if (param.Type != ParamType.Option)
                continue;

            evalRes.Options.Add(new OptionState
            {
                Id = param.Id,
                Label = param.Label,
                Selected = IsSelected(values, param.Id),
                AutoAdded = autoAdded.Contains(param.Id),
                Available = true,
            });
        }
    }

    private static bool IsSelected(Dictionary<string, object> values, string? id)
        => id != null && values.TryGetValue(id, out object? value) && value is true;

    private static decimal Amount(Price price, decimal basePrice, Dictionary<string, object> values)
    {
        decimal amount = price.Type switch
        {
            PriceType.Fixed => price.Amount,
            PriceType.Percent => basePrice * price.Rate / 100,
            PriceType.PerUnit => price.Rate * (decimal)values[price.Param!],
            _ => throw new InvalidOperationException($"Type de prix inconnu : {price.Type}."),
        };

        return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}
