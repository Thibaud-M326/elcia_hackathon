using System.Text.Json;
using Engine;

namespace Tests;

public class ValidatorTests
{
    [Fact]
    public void Validate_AcceptsSampleProduct()
    {
        Assert.Empty(Validator.Validate(ReadSample()));
    }

    [Fact]
    public void Validate_RejectsEmptyId()
    {
        AssertValidationError(p => p.Parameters[2].Id = "", "identifiant vide");
    }

    [Fact]
    public void Validate_RejectsDuplicateId()
    {
        AssertValidationError(p => p.Parameters[2].Id = "largeur", "identifiant en double");
    }

    [Theory]
    [InlineData("dormant interieur")]
    [InlineData("dormant.interieur")]
    public void Validate_RejectsInvalidIdCharacters(string id)
    {
        AssertValidationError(p => p.Parameters[2].Id = id, "ni espace ni point");
    }

    [Fact]
    public void Validate_RejectsEmptyLabel()
    {
        AssertInvalid(p => p.Parameters[2].Label = " ", "libellé vide");
    }

    [Fact]
    public void Validate_RejectsInvertedBounds()
    {
        AssertInvalid(p =>
        {
            Parameter parameter = p.Parameters[2];
            parameter.Min = 121;
            parameter.Default = null;
        }, "min est supérieur à max");
    }

    [Theory]
    [InlineData(39)]
    [InlineData(121)]
    public void Validate_RejectsNumberDefaultOutsideBounds(int value)
    {
        AssertInvalid(p => p.Parameters[2].Default = JsonSerializer.SerializeToElement(value),
            "valeur par défaut est hors des bornes");
    }

    [Fact]
    public void Validate_RejectsUnrepresentableNumberDefault()
    {
        AssertInvalid(p => p.Parameters[2].Default = JsonSerializer.SerializeToElement(1e100),
            "valeur par défaut doit être un nombre décimal");
    }

    [Fact]
    public void Validate_RejectsEmptyChoices()
    {
        AssertValidationError(p =>
        {
            Parameter parameter = p.Parameters[3];
            parameter.Choices!.Clear();
            parameter.Default = null;
        }, "au moins un choix est requis");
    }

    [Fact]
    public void Validate_RejectsDuplicateChoices()
    {
        AssertInvalid(p => p.Parameters[3].Choices!.Add("blanc"), "choix en double");
    }

    [Fact]
    public void Validate_RejectsUnknownDefaultChoice()
    {
        AssertInvalid(p => p.Parameters[3].Default = JsonSerializer.SerializeToElement("rouge"),
            "valeur par défaut doit figurer parmi les choix");
    }

    [Theory]
    [InlineData("largeur")]
    [InlineData("hauteur")]
    public void Validate_RequiresDimension(string id)
    {
        Product product = ReadSample();
        product.Parameters.RemoveAll(parameter => parameter.Id == id);

        Assert.Contains($"Paramètre obligatoire manquant : '{id}'.", Validator.Validate(product));
    }

    [Fact]
    public void Validate_CollectsBothErrors()
    {
        Product product = ReadSample();
        product.Parameters[2].Label = "";
        product.Parameters[3].Choices!.Add("blanc");

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("libellé vide", error),
            error => Assert.Contains("choix en double", error));
    }

    [Theory]
    [InlineData(true, "largeur")]
    [InlineData(false, "hauteur")]
    public void Validate_RejectsEmptyTiers(bool useWidths, string dimension)
    {
        Product product = ReadSample();
        if (useWidths)
            product.Grid.Widths.Clear();
        else
            product.Grid.Heights.Clear();

        Assert.Contains(Validator.Validate(product),
            error => error.Contains($"au moins un palier de {dimension}"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RejectsTiersThatDoNotIncrease(int difference)
    {
        AssertInvalid(p => p.Grid.Widths[1] = p.Grid.Widths[0] + difference,
            "palier de largeur 2");
        AssertInvalid(p => p.Grid.Heights[1] = p.Grid.Heights[0] + difference,
            "palier de hauteur 2");
    }

    [Fact]
    public void Validate_RejectsWrongPriceRowCount()
    {
        AssertInvalid(p => p.Grid.Prices.RemoveAt(0), "lignes de prix attendues");
        AssertInvalid(p => p.Grid.Prices.Add(new List<decimal?>(p.Grid.Prices[0])),
            "lignes de prix attendues");
    }

    [Fact]
    public void Validate_RejectsWrongPriceColumnCount()
    {
        AssertInvalid(p => p.Grid.Prices[0].RemoveAt(0), "Grille, ligne 1");
        AssertInvalid(p => p.Grid.Prices[0].Add(0), "Grille, ligne 1");
    }

    [Fact]
    public void Validate_AcceptsZeroAndMissingPrices()
    {
        Product product = ReadSample();
        product.Grid.Prices[0][0] = 0;
        product.Grid.Prices[0][1] = null;

        Assert.Empty(Validator.Validate(product));
    }

    [Fact]
    public void Validate_CollectsParameterAndGridErrors()
    {
        Product product = ReadSample();
        product.Parameters[0].Label = "";
        product.Grid.Prices[0].RemoveAt(0);
        product.Grid.Prices[0][0] = -1;

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("libellé vide", error),
            error => Assert.Contains("cases attendues", error),
            error => Assert.Contains("ligne 1, colonne 1 : prix négatif", error));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Validate_RejectsPercentRatesOutsideBounds(int rate)
    {
        AssertInvalid(p => p.Rules[4].Price!.Rate = rate,
            "Surcharge 'couleur-hors-blanc' : le taux doit être compris entre 0 et 100");
    }

    [Theory]
    [InlineData(null, "le prix par unité doit référencer un paramètre")]
    [InlineData("inconnu", "le paramètre 'inconnu' est introuvable")]
    [InlineData("couleur", "le paramètre 'couleur' doit être numérique")]
    public void Validate_RejectsInvalidPerUnitPriceParameter(string? parameterId, string message)
    {
        AssertInvalid(p => p.Parameters[5].Price!.Param = parameterId,
            $"Option 'croisillons' : {message}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_AcceptsPriceBoundaries(int rate)
    {
        Product product = ReadSample();
        product.Parameters[4].Price!.Amount = 0;
        product.Rules[4].Price!.Rate = rate;

        Assert.Empty(Validator.Validate(product));
    }

    [Fact]
    public void Validate_CollectsOptionAndSurchargePriceErrors()
    {
        Product product = ReadSample();
        product.Parameters[4].Price!.Amount = -1;
        product.Rules[5].Price!.Amount = -1;

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("Option 'oscillo' : le montant fixe", error),
            error => Assert.Contains("Surcharge 'grande-largeur' : le montant fixe", error));
    }

    [Fact]
    public void Validate_ReportsEmptyAndDuplicateRuleIds()
    {
        Product product = ReadSample();
        product.Rules[0].Id = " ";
        product.Rules[2].Id = product.Rules[1].Id;

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("identifiant de règle vide", error),
            error => Assert.Contains("identifiant de règle en double", error));
    }

    [Fact]
    public void Validate_ChecksBothOptionReferences()
    {
        Product product = ReadSample();
        Rule rule = product.Rules[2];
        rule.Option = "inconnue";
        rule.Other = "largeur";

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("'option' référence le paramètre introuvable", error),
            error => Assert.Contains("'other' doit référencer une option", error));
    }

    [Fact]
    public void Validate_RejectsRuleThatReferencesItself()
    {
        AssertInvalid(p => p.Rules[3].Other = "motorisation", "ne peut pas porter sur elle-même");
    }

    [Fact]
    public void Validate_ReportsMissingRequiredRuleFields()
    {
        Product product = ReadSample();
        product.Rules[0].When.Clear();
        product.Rules[0].Message = " ";
        product.Rules[4].When.Clear();
        product.Rules[4].Label = null;
        product.Rules[4].Price = null;

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("au moins une condition", error),
            error => Assert.Contains("message obligatoire", error),
            error => Assert.Contains("au moins une condition", error),
            error => Assert.Contains("libellé de surcharge obligatoire", error),
            error => Assert.Contains("prix de surcharge obligatoire", error));
    }

    [Fact]
    public void Validate_ReportsUnknownConditionAndMessageParameters()
    {
        Product product = ReadSample();
        product.Rules[0].When[0].Param = "inconnu";
        product.Rules[3].Message = "Option {volet} avec {inconnue}.";

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("condition 1 : paramètre 'inconnu' introuvable", error),
            error => Assert.Contains("paramètre de message 'inconnue' introuvable", error));
    }

    [Theory]
    [InlineData(Op.Gte)]
    [InlineData(Op.Lte)]
    public void Validate_RejectsNumericComparisonsOnChoices(Op operation)
    {
        AssertInvalid(p => p.Rules[4].When[0].Op = operation, "exigent un paramètre numérique");
    }

    [Fact]
    public void Validate_RejectsNonNumericAndNonBooleanConditionValues()
    {
        Product product = ReadSample();
        product.Rules[0].When[0].Value = JsonSerializer.SerializeToElement("60");
        product.Rules[5].When[1].Value = JsonSerializer.SerializeToElement("true");

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("la valeur doit être un nombre décimal", error),
            error => Assert.Contains("la valeur doit être un booléen", error));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"blanc\"")]
    public void Validate_RequiresNonEmptyListForIn(string json)
    {
        AssertInvalid(p => p.Rules[1].When[0].Value = JsonSerializer.Deserialize<JsonElement>(json),
            "in exige une liste non vide");
    }

    [Fact]
    public void Validate_ChecksEachChoiceInList()
    {
        Product product = ReadSample();
        product.Rules[1].When[0].Value =
            JsonSerializer.Deserialize<JsonElement>("[\"blanc\", \"chêne\", 42]");

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("élément 2 : le choix 'chêne'", error),
            error => Assert.Contains("élément 3 : la valeur doit être du texte", error));
    }

    private static void AssertValidationError(Action<Product> change, string expectedMessage)
    {
        Product product = ReadSample();
        change(product);

        Assert.Contains(Validator.Validate(product), error => error.Contains(expectedMessage));
    }

    private static Product ReadSample() => Json.ReadProduct("product-model-v1.json");

    [Fact]
    public void Validate_RejectsMotorisationWhenIncompatibleWithVolet()
    {
        Product product = ReadSample();
        AddOptionRule(product, RuleType.Incompatible, "volet", "motorisation");

        string error = Assert.Single(Validator.Validate(product));
        Assert.Contains("L'option 'motorisation' ne peut pas être vendue", error);
        Assert.Contains("'volet' et 'motorisation'", error);
    }

    [Fact]
    public void Validate_RejectsTwoIncompatibleImpliedOptions()
    {
        Product product = ReadSample();
        AddOptionRule(product, RuleType.Implies, "oscillo", "croisillons");
        AddOptionRule(product, RuleType.Implies, "oscillo", "feuillete");

        string error = Assert.Single(Validator.Validate(product));
        Assert.Contains("L'option 'oscillo' ne peut pas être vendue", error);
        Assert.Contains("'croisillons' et 'feuillete'", error);
    }

    [Fact]
    public void Validate_FollowsIndirectImplications()
    {
        Product product = ReadSample();
        // Le produit contient déjà motorisation -> volet.
        AddOptionRule(product, RuleType.Implies, "volet", "oscillo");
        AddOptionRule(product, RuleType.Incompatible, "motorisation", "oscillo");

        string error = Assert.Single(Validator.Validate(product));
        Assert.Contains("L'option 'motorisation' ne peut pas être vendue", error);
        Assert.Contains("'motorisation' et 'oscillo'", error);
    }

    [Fact]
    public void Validate_AcceptsImplicationCycleWithoutConflict()
    {
        Product product = ReadSample();
        AddOptionRule(product, RuleType.Implies, "volet", "motorisation");

        Assert.Empty(Validator.Validate(product));
    }

    private static void AddOptionRule(Product product, RuleType type, string option, string other)
    {
        product.Rules.Add(new Rule
        {
            Id = $"{type}-{option}-{other}",
            Type = type,
            Option = option,
            Other = other,
            Message = $"Relation entre {option} et {other}."
        });
    }

    private static void AssertInvalid(Action<Product> change, string expectedMessage)
    {
        Product product = ReadSample();
        change(product);

        Assert.Contains(expectedMessage, Assert.Single(Validator.Validate(product)));
    }
}
