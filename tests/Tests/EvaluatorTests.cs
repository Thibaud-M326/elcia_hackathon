using System.Text.Json;
using Engine;

namespace Tests;

public class EvaluatorTests
{
    private static readonly Product P = Json.ReadProduct("product-model-v1.json");

    private static EvaluationResult Eval(Dictionary<string, object?> input)
        => Evaluator.Evaluate(P, 1, input);

    [Fact]
    public void Empty_input_is_valid_and_uses_defaults()
    {
        var r = Eval(new());

        Assert.Empty(r.Errors);
        Assert.True(r.Valid);
        Assert.Equal(1200m, r.Values["largeur"]);
        Assert.Equal(1200m, r.Values["hauteur"]);
        Assert.Equal("blanc", r.Values["couleur"]);
        Assert.Equal(false, r.Values["volet"]);
    }

    [Fact]
    public void Input_value_replaces_default()
    {
        var r = Eval(new() { ["largeur"] = 1500, ["volet"] = true });

        Assert.Empty(r.Errors);
        Assert.Equal(1500m, r.Values["largeur"]);
        Assert.Equal(true, r.Values["volet"]);
    }

    [Fact]
    public void Json_input_is_accepted()
    {
        var r = Eval(new() { ["largeur"] = JsonSerializer.Deserialize<JsonElement>("1500") });

        Assert.Empty(r.Errors);
        Assert.Equal(1500m, r.Values["largeur"]);
    }

    [Fact]
    public void Width_above_max_is_an_error()
    {
        var r = Eval(new() { ["largeur"] = 5000 });

        Assert.False(r.Valid);
        Assert.Null(r.Total);
        Assert.Contains("Largeur (mm) : maximum 2400 (saisi : 5000).", r.Errors);
    }

    [Fact]
    public void Width_below_min_is_an_error()
    {
        var r = Eval(new() { ["largeur"] = 100 });

        Assert.False(r.Valid);
        Assert.Contains("Largeur (mm) : minimum 600 (saisi : 100).", r.Errors);
    }

    [Fact]
    public void Text_instead_of_number_is_an_error()
    {
        var r = Eval(new() { ["largeur"] = "abc" });

        Assert.False(r.Valid);
        Assert.Contains("Largeur (mm) : un nombre est attendu.", r.Errors);
    }

    [Fact]
    public void Unknown_choice_is_an_error()
    {
        var r = Eval(new() { ["couleur"] = "rouge" });

        Assert.False(r.Valid);
        Assert.Contains("Couleur : 'rouge' n'est pas un choix possible.", r.Errors);
    }

    [Fact]
    public void Unknown_parameter_is_an_error()
    {
        var r = Eval(new() { ["inconnu"] = 1 });

        Assert.False(r.Valid);
        Assert.Contains("Paramètre inconnu : inconnu.", r.Errors);
    }

    [Fact]
    public void All_errors_are_returned_at_once()
    {
        var r = Eval(new() { ["largeur"] = 5000, ["couleur"] = "rouge" });

        Assert.Equal(2, r.Errors.Count);
    }
}
