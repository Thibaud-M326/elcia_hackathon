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
        AssertInvalid(p => p.Parameters[2].Id = "", "identifiant vide");
    }

    [Fact]
    public void Validate_RejectsDuplicateId()
    {
        AssertInvalid(p => p.Parameters[2].Id = "largeur", "identifiant en double");
    }

    [Theory]
    [InlineData("dormant interieur")]
    [InlineData("dormant.interieur")]
    public void Validate_RejectsInvalidIdCharacters(string id)
    {
        AssertInvalid(p => p.Parameters[2].Id = id, "ni espace ni point");
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
        AssertInvalid(p =>
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
        AssertInvalid(p => p.Parameters.RemoveAll(parameter => parameter.Id == id),
            $"Paramètre obligatoire manquant : '{id}'");
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

    private static Product ReadSample() => Json.ReadProduct("product-model-v1.json");

    private static void AssertInvalid(Action<Product> change, string expectedMessage)
    {
        Product product = ReadSample();
        change(product);

        Assert.Contains(expectedMessage, Assert.Single(Validator.Validate(product)));
    }
}
