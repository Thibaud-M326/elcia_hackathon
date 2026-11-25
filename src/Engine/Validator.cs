using System.Text.Json;

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

        foreach (Rule rule in p.Rules)
        {
            if (rule.Type == RuleType.Surcharge)
                ValidatePrice(rule.Price, p.Parameters, $"Surcharge '{rule.Id}'", errors);
        }

        return errors;
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
