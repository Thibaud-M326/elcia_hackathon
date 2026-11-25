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

        evalRes.Values = values;
        evalRes.Valid = true;
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
}
