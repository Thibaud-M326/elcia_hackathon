using System.Text.Json;
using System.Text.RegularExpressions;

namespace Engine;

public static class Validator
{
    public static List<string> Validate(Product p)
    {
        List<string> errors = new();
        HashSet<string> ids = new(StringComparer.Ordinal);

        for (int index = 0; index < p.Parameters.Count; index++)
        {
            Parameter parameter = p.Parameters[index];
            string context = $"Paramètre {index + 1} ('{parameter.Id}')";

            if (string.IsNullOrWhiteSpace(parameter.Id))
                errors.Add($"{context} : identifiant vide.");
            else if (!ids.Add(parameter.Id))
                errors.Add($"{context} : identifiant en double.");

            if (parameter.Id is string id && (id.Any(char.IsWhiteSpace) || id.Contains('.')))
                errors.Add($"{context} : l'identifiant ne peut contenir ni espace ni point.");

            if (string.IsNullOrWhiteSpace(parameter.Label))
                errors.Add($"{context} : libellé vide.");

            switch (parameter.Type)
            {
                case ParamType.Number:
                    ValidateNumber(parameter, context, errors);
                    break;
                case ParamType.Choice:
                    ValidateChoice(parameter, context, errors);
                    break;
                case ParamType.Option:
                    ValidatePrice(parameter.Price, p.Parameters, $"Option '{parameter.Id}'", errors);
                    break;
            }
        }

        foreach (string requiredId in new[] { "largeur", "hauteur" })
        {
            if (!ids.Contains(requiredId))
                errors.Add($"Paramètre obligatoire manquant : '{requiredId}'.");
        }

        ValidateGrid(p.Grid, errors);

        ValidateRules(p, ids, errors);
        ValidateOptionCompatibility(p, errors);
        return errors;
    }

    private static void ValidateOptionCompatibility(Product product, List<string> errors)
    {
        Dictionary<string, List<string>> implications = new(StringComparer.Ordinal);
        foreach (Parameter parameter in product.Parameters)
        {
            if (parameter.Type == ParamType.Option && !string.IsNullOrWhiteSpace(parameter.Id))
                implications.TryAdd(parameter.Id, new List<string>());
        }

        List<(string first, string second)> incompatiblePairs = new();
        foreach (Rule rule in product.Rules)
        {
            if (rule.Option == null || rule.Other == null || rule.Option == rule.Other)
                continue;
            if (!implications.ContainsKey(rule.Option) || !implications.ContainsKey(rule.Other))
                continue;

            if (rule.Type == RuleType.Implies)
                implications[rule.Option].Add(rule.Other);
            else if (rule.Type == RuleType.Incompatible)
                incompatiblePairs.Add((rule.Option, rule.Other));
        }

        foreach (string startingOption in implications.Keys)
        {
            HashSet<string> selected = FindImpliedOptions(startingOption, implications);
            foreach (var pair in incompatiblePairs)
            {
                if (selected.Contains(pair.first) && selected.Contains(pair.second))
                    errors.Add($"L'option '{startingOption}' ne peut pas être vendue : les options '{pair.first}' et '{pair.second}' seraient sélectionnées ensemble et sont incompatibles.");
            }
        }
    }

    private static HashSet<string> FindImpliedOptions(string startingOption, Dictionary<string, List<string>> implications)
    {
        HashSet<string> selected = new(StringComparer.Ordinal);
        Queue<string> pending = new();
        selected.Add(startingOption);
        pending.Enqueue(startingOption);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue();
            foreach (string impliedOption in implications[current])
            {
                if (selected.Add(impliedOption))
                    pending.Enqueue(impliedOption);
            }
        }

        return selected;
    }

    private static void ValidateRules(Product product, HashSet<string> parameterIds, List<string> errors)
    {
        HashSet<string> ruleIds = new(StringComparer.Ordinal);
        for (int index = 0; index < product.Rules.Count; index++)
        {
            Rule rule = product.Rules[index];
            string context = $"Règle {index + 1} ('{rule.Id}')";

            if (string.IsNullOrWhiteSpace(rule.Id))
                errors.Add($"{context} : identifiant de règle vide.");
            else if (!ruleIds.Add(rule.Id))
                errors.Add($"{context} : identifiant de règle en double.");

            ValidateRuleFields(rule, product.Parameters, context, errors);
            ValidateRuleConditions(rule, product.Parameters, context, errors);
            ValidateRuleMessage(rule.Message, parameterIds, context, errors);
        }
    }

    private static void ValidateRuleFields(Rule rule, List<Parameter> parameters, string context, List<string> errors)
    {
        switch (rule.Type)
        {
            case RuleType.AvailableIf:
                ValidateRuleOption(rule.Option, "option", parameters, context, errors);
                RequireRuleConditions(rule, context, errors);
                RequireRuleMessage(rule, context, errors);
                break;

            case RuleType.Incompatible:
                ValidateRuleOptions(rule, parameters, context, errors);
                RequireRuleMessage(rule, context, errors);
                break;

            case RuleType.Implies:
                ValidateRuleOptions(rule, parameters, context, errors);
                break;

            case RuleType.Surcharge:
                RequireRuleConditions(rule, context, errors);
                if (string.IsNullOrWhiteSpace(rule.Label))
                    errors.Add($"{context} : libellé de surcharge obligatoire.");
                if (rule.Price == null)
                    errors.Add($"{context} : prix de surcharge obligatoire.");
                else
                    ValidatePrice(rule.Price, parameters, $"Surcharge '{rule.Id}'", errors);
                break;
        }
    }

    private static void ValidateRuleOptions(Rule rule, List<Parameter> parameters, string context, List<string> errors)
    {
        ValidateRuleOption(rule.Option, "option", parameters, context, errors);
        ValidateRuleOption(rule.Other, "other", parameters, context, errors);

        if (string.IsNullOrWhiteSpace(rule.Option))
            return;

        if (rule.Option == rule.Other)
            errors.Add($"{context} : une règle ne peut pas porter sur elle-même.");
    }

    private static void RequireRuleConditions(Rule rule, string context, List<string> errors)
    {
        if (rule.When == null || rule.When.Count == 0)
            errors.Add($"{context} : au moins une condition est requise.");
    }

    private static void RequireRuleMessage(Rule rule, string context, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rule.Message))
            errors.Add($"{context} : message obligatoire.");
    }

    private static void ValidateRuleConditions(Rule rule, List<Parameter> parameters, string context, List<string> errors)
    {
        if (rule.When == null)
            return;

        for (int index = 0; index < rule.When.Count; index++)
        {
            string conditionContext = $"{context}, condition {index + 1}";
            ValidateCondition(rule.When[index], parameters, conditionContext, errors);
        }
    }

    private static void ValidateRuleMessage(string? message, HashSet<string> parameterIds, string context, List<string> errors)
    {
        if (message == null)
            return;

        // Le groupe entre parenthèses récupère le texte à l'intérieur des accolades.
        foreach (Match placeholder in Regex.Matches(message, @"\{([^{}]+)\}"))
        {
            string id = placeholder.Groups[1].Value;
            if (!parameterIds.Contains(id))
                errors.Add($"{context} : paramètre de message '{id}' introuvable.");
        }
    }

    private static void ValidateRuleOption(string? id, string field, List<Parameter> parameters, string context, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            errors.Add($"{context} : le champ '{field}' doit référencer une option.");
            return;
        }

        Parameter? parameter = parameters.Find(item => item.Id == id);
        if (parameter == null)
            errors.Add($"{context} : le champ '{field}' référence le paramètre introuvable '{id}'.");
        else if (parameter.Type != ParamType.Option)
            errors.Add($"{context} : le champ '{field}' doit référencer une option, mais '{id}' n'est pas une option.");
    }

    private static void ValidateCondition(Condition condition, List<Parameter> parameters, string context, List<string> errors)
    {
        Parameter? parameter = null;
        if (!string.IsNullOrWhiteSpace(condition.Param))
            parameter = parameters.Find(item => item.Id == condition.Param);

        if (parameter == null)
            errors.Add($"{context} : paramètre '{condition.Param}' introuvable.");
        else
        {
            bool comparesNumbers = condition.Op == Op.Gte || condition.Op == Op.Lte;
            if (comparesNumbers && parameter.Type != ParamType.Number)
                errors.Add($"{context} : gte et lte exigent un paramètre numérique.");
        }

        if (condition.Op == Op.In)
            ValidateConditionList(condition.Value, parameter, context, errors);
        else if (parameter != null)
            ValidateConditionValue(condition.Value, parameter, context, errors);
    }

    private static void ValidateConditionList(JsonElement list, Parameter? parameter, string context, List<string> errors)
    {
        if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
        {
            errors.Add($"{context} : in exige une liste non vide.");
            return;
        }

        if (parameter == null)
            return;

        int itemNumber = 1;
        foreach (JsonElement value in list.EnumerateArray())
        {
            ValidateConditionValue(value, parameter, $"{context}, élément {itemNumber}", errors);
            itemNumber++;
        }
    }

    private static void ValidateConditionValue(JsonElement value, Parameter parameter, string context, List<string> errors)
    {
        switch (parameter.Type)
        {
            case ParamType.Number:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out _))
                    errors.Add($"{context} : la valeur doit être un nombre décimal.");
                break;

            case ParamType.Choice:
                if (value.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{context} : la valeur doit être du texte.");
                    break;
                }

                string? choice = value.GetString();
                if (choice == null || parameter.Choices == null || !parameter.Choices.Contains(choice))
                    errors.Add($"{context} : le choix '{choice}' est absent de '{parameter.Id}'.");
                break;

            case ParamType.Option:
                if (value.ValueKind != JsonValueKind.True && value.ValueKind != JsonValueKind.False)
                    errors.Add($"{context} : la valeur doit être un booléen.");
                break;
        }
    }

    private static void ValidatePrice(Price? price, List<Parameter> parameters, string context, List<string> errors)
    {
        if (price == null)
            return;

        switch (price.Type)
        {
            case PriceType.Fixed:
                if (price.Amount < 0)
                    errors.Add($"{context} : le montant fixe doit être positif ou nul.");
                break;

            case PriceType.Percent:
                if (price.Rate < 0 || price.Rate > 100)
                    errors.Add($"{context} : le taux doit être compris entre 0 et 100.");
                break;

            case PriceType.PerUnit:
                ValidatePriceParameter(price.Param, parameters, context, errors);
                break;
        }
    }

    private static void ValidatePriceParameter(string? parameterId, List<Parameter> parameters, string context, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            errors.Add($"{context} : le prix par unité doit référencer un paramètre.");
            return;
        }

        Parameter? parameter = parameters.Find(item => item.Id == parameterId);
        if (parameter == null)
            errors.Add($"{context} : le paramètre '{parameterId}' est introuvable.");
        else if (parameter.Type != ParamType.Number)
            errors.Add($"{context} : le paramètre '{parameterId}' doit être numérique.");
    }

    private static void ValidateGrid(Grid grid, List<string> errors)
    {
        ValidateTiers(grid.Widths, "largeur", errors);
        ValidateTiers(grid.Heights, "hauteur", errors);

        int expectedRows = grid.Heights.Count;
        if (grid.Prices.Count != expectedRows)
            errors.Add($"Grille : {expectedRows} lignes de prix attendues, {grid.Prices.Count} trouvées.");

        int expectedColumns = grid.Widths.Count;
        for (int rowIndex = 0; rowIndex < grid.Prices.Count; rowIndex++)
        {
            List<decimal?> row = grid.Prices[rowIndex];
            int rowNumber = rowIndex + 1;
            if (row.Count != expectedColumns)
                errors.Add($"Grille, ligne {rowNumber} : {expectedColumns} cases attendues, {row.Count} trouvées.");

            for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
            {
                decimal? price = row[columnIndex];
                if (price < 0)
                    errors.Add($"Grille, ligne {rowNumber}, colonne {columnIndex + 1} : prix négatif.");
            }
        }
    }

    private static void ValidateTiers(List<decimal> tiers, string dimension, List<string> errors)
    {
        if (tiers.Count == 0)
            errors.Add($"Grille : au moins un palier de {dimension} est requis.");

        for (int index = 1; index < tiers.Count; index++)
        {
            decimal previousTier = tiers[index - 1];
            decimal currentTier = tiers[index];
            if (currentTier <= previousTier)
                errors.Add($"Grille : le palier de {dimension} {index + 1} doit être supérieur au précédent.");
        }
    }

    private static void ValidateNumber(Parameter parameter, string context, List<string> errors)
    {
        if (parameter.Min > parameter.Max)
            errors.Add($"{context} : min est supérieur à max.");

        if (parameter.Default is not JsonElement value)
            return;

        // TryGetDecimal also handles JSON numbers outside the decimal range.
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out decimal number))
            errors.Add($"{context} : la valeur par défaut doit être un nombre décimal.");
        else if (number < parameter.Min || number > parameter.Max)
            errors.Add($"{context} : la valeur par défaut est hors des bornes.");
    }

    private static void ValidateChoice(Parameter parameter, string context, List<string> errors)
    {
        List<string>? configuredChoices = parameter.Choices;
        if (configuredChoices == null)
            configuredChoices = new List<string>();

        HashSet<string> choices = new(configuredChoices, StringComparer.Ordinal);

        if (choices.Count == 0)
            errors.Add($"{context} : au moins un choix est requis.");
        if (choices.Count < configuredChoices.Count)
            errors.Add($"{context} : la liste contient des choix en double.");

        if (!parameter.Default.HasValue)
            return;

        JsonElement defaultValue = parameter.Default.Value;
        if (defaultValue.ValueKind == JsonValueKind.String)
        {
            string? defaultChoice = defaultValue.GetString();
            if (defaultChoice != null && choices.Contains(defaultChoice))
                return;
        }

        errors.Add($"{context} : la valeur par défaut doit figurer parmi les choix.");
    }
}
