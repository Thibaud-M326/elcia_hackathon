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
        AssertValidationError(p => Param(p, "couleur").Id = "", "identifiant vide");
    }

    [Fact]
    public void Validate_RejectsDuplicateId()
    {
        AssertValidationError(p => Param(p, "couleur").Id = "largeur", "identifiant en double");
    }

    [Theory]
    [InlineData("dormant interieur")]
    [InlineData("dormant.interieur")]
    public void Validate_RejectsInvalidIdCharacters(string id)
    {
        AssertValidationError(p => Param(p, "dormant").Id = id, "ni espace ni point");
    }

    [Fact]
    public void Validate_RejectsEmptyLabel()
    {
        AssertInvalid(p => Param(p, "couleur").Label = " ", "libellé vide");
    }

    [Fact]
    public void Validate_RejectsInvertedBounds()
    {
        AssertInvalid(p =>
        {
            Parameter parameter = Param(p, "largeur");
            parameter.Min = 2401;
            parameter.Default = null;
        }, "min est supérieur à max");
    }

    [Theory]
    [InlineData(599)]
    [InlineData(2401)]
    public void Validate_RejectsNumberDefaultOutsideBounds(int value)
    {
        AssertInvalid(p => Param(p, "largeur").Default = JsonSerializer.SerializeToElement(value),
            "valeur par défaut est hors des bornes");
    }

    [Fact]
    public void Validate_RejectsUnrepresentableNumberDefault()
    {
        AssertInvalid(p => Param(p, "largeur").Default = JsonSerializer.SerializeToElement(1e100),
            "valeur par défaut doit être un nombre décimal");
    }

    [Fact]
    public void Validate_RejectsEmptyChoices()
    {
        AssertValidationError(p =>
        {
            Parameter parameter = Param(p, "couleur");
            parameter.Choices!.Clear();
            parameter.Default = null;
        }, "au moins un choix est requis");
    }

    [Fact]
    public void Validate_RejectsDuplicateChoices()
    {
        AssertInvalid(p => Param(p, "couleur").Choices!.Add("blanc"), "choix en double");
    }

    [Fact]
    public void Validate_RejectsUnknownDefaultChoice()
    {
        AssertInvalid(p => Param(p, "couleur").Default = JsonSerializer.SerializeToElement("rouge"),
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
        Param(product, "hauteur").Label = "";
        Param(product, "couleur").Choices!.Add("blanc");

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
        AssertInvalid(p => RuleById(p, "couleur-hors-blanc").Price!.Rate = rate,
            "Surcharge 'couleur-hors-blanc' : le taux doit être compris entre 0 et 100");
    }

    [Theory]
    [InlineData(null, "le prix par unité doit référencer un paramètre")]
    [InlineData("inconnu", "le paramètre 'inconnu' est introuvable")]
    [InlineData("couleur", "le paramètre 'couleur' doit être numérique")]
    public void Validate_RejectsInvalidPerUnitPriceParameter(string? parameterId, string message)
    {
        AssertInvalid(p => Param(p, "volet").Price!.Param = parameterId,
            $"Option 'volet' : {message}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_AcceptsPriceBoundaries(int rate)
    {
        Product product = ReadSample();
        Param(product, "oscillo").Price!.Amount = 0;
        RuleById(product, "couleur-hors-blanc").Price!.Rate = rate;

        Assert.Empty(Validator.Validate(product));
    }

    [Fact]
    public void Validate_CollectsOptionAndSurchargePriceErrors()
    {
        Product product = ReadSample();
        Param(product, "oscillo").Price!.Amount = -1;
        RuleById(product, "grande-largeur").Price!.Amount = -1;

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
        Rule rule = RuleById(product, "motorisation-volet");
        rule.Option = "inconnue";
        rule.Other = "largeur";

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("'option' référence le paramètre introuvable", error),
            error => Assert.Contains("'other' doit référencer une option", error));
    }

    [Fact]
    public void Validate_RejectsRuleThatReferencesItself()
    {
        AssertInvalid(p => RuleById(p, "motorisation-volet").Other = "motorisation", "ne peut pas porter sur elle-même");
    }

    [Fact]
    public void Validate_ReportsMissingRequiredRuleFields()
    {
        Product product = ReadSample();
        Rule availability = RuleById(product, "volet-dormant");
        availability.When.Clear();
        availability.Message = " ";
        Rule surcharge = RuleById(product, "couleur-hors-blanc");
        surcharge.When.Clear();
        surcharge.Label = null;
        surcharge.Price = null;

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
        RuleById(product, "volet-dormant").When[0].Param = "inconnu";
        RuleById(product, "motorisation-volet").Message = "Option {volet} avec {inconnue}.";

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("condition 1 : paramètre 'inconnu' introuvable", error),
            error => Assert.Contains("paramètre de message 'inconnue' introuvable", error));
    }

    [Theory]
    [InlineData(Op.Gte)]
    [InlineData(Op.Lte)]
    public void Validate_RejectsNumericComparisonsOnChoices(Op operation)
    {
        AssertInvalid(p => RuleById(p, "couleur-hors-blanc").When[0].Op = operation, "exigent un paramètre numérique");
    }

    [Fact]
    public void Validate_RejectsNonNumericAndNonBooleanConditionValues()
    {
        Product product = ReadSample();
        Rule rule = RuleById(product, "grande-largeur");
        rule.When[0].Value = JsonSerializer.SerializeToElement("1800");
        rule.When[1].Value = JsonSerializer.SerializeToElement("true");

        Assert.Collection(Validator.Validate(product),
            error => Assert.Contains("la valeur doit être un nombre décimal", error),
            error => Assert.Contains("la valeur doit être un booléen", error));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("\"Dormant de 100mm\"")]
    public void Validate_RequiresNonEmptyListForIn(string json)
    {
        AssertInvalid(p => RuleById(p, "volet-dormant").When[0].Value = JsonSerializer.Deserialize<JsonElement>(json),
            "in exige une liste non vide");
    }

    [Fact]
    public void Validate_ChecksEachChoiceInList()
    {
        Product product = ReadSample();
        RuleById(product, "volet-dormant").When[0].Value =
            JsonSerializer.Deserialize<JsonElement>("[\"Dormant de 100mm\", \"chêne\", 42]");

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

    // Par identifiant plutôt que par position : les tests ne cassent pas si l'ordre du produit change
    private static Parameter Param(Product product, string id) => product.Parameters.Single(p => p.Id == id);

    private static Rule RuleById(Product product, string id) => product.Rules.Single(r => r.Id == id);

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
        // Le produit n'a pas d'incompatibilité : on en ajoute une entre volet et feuilleté
        AddOptionRule(product, RuleType.Incompatible, "volet", "feuillete");
        AddOptionRule(product, RuleType.Implies, "oscillo", "volet");
        AddOptionRule(product, RuleType.Implies, "oscillo", "feuillete");

        string error = Assert.Single(Validator.Validate(product));
        Assert.Contains("L'option 'oscillo' ne peut pas être vendue", error);
        Assert.Contains("'volet' et 'feuillete'", error);
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
