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

    private static Product ReadSample() => Json.ReadProduct("product-model-v1.json");

    private static void AssertInvalid(Action<Product> change, string expectedMessage)
    {
        Product product = ReadSample();
        change(product);

        Assert.Contains(expectedMessage, Assert.Single(Validator.Validate(product)));
    }
}
