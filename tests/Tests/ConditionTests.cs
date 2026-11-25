using System.Text.Json;
using Engine;

namespace Tests;

public class ConditionTests
{
    // Valeurs telles qu'A1 les range : decimal, string, bool
    private static Dictionary<string, object> Values(decimal dormant = 70, string couleur = "blanc", bool oscillo = false)
        => new() { ["dormant"] = dormant, ["couleur"] = couleur, ["oscillo"] = oscillo };

    // Une condition comme dans le JSON : la valeur est écrite en JSON brut
    private static Condition C(string param, Op op, string jsonValue)
        => new() { Param = param, Op = op, Value = JsonSerializer.Deserialize<JsonElement>(jsonValue) };

    [Fact]
    public void Gte_is_true_on_the_limit()
        => Assert.True(Evaluator.Holds(Values(dormant: 60), C("dormant", Op.Gte, "60")));

    [Fact]
    public void Gte_is_false_below_the_limit()
        => Assert.False(Evaluator.Holds(Values(dormant: 59), C("dormant", Op.Gte, "60")));

    [Fact]
    public void Lte_is_true_below_the_limit()
        => Assert.True(Evaluator.Holds(Values(dormant: 50), C("dormant", Op.Lte, "60")));

    [Fact]
    public void In_is_false_when_value_is_not_in_the_list()
        => Assert.False(Evaluator.Holds(Values(couleur: "chene"), C("couleur", Op.In, """["blanc", "gris"]""")));

    [Fact]
    public void In_is_true_when_value_is_in_the_list()
        => Assert.True(Evaluator.Holds(Values(couleur: "gris"), C("couleur", Op.In, """["blanc", "gris"]""")));

    [Fact]
    public void Eq_on_an_option_is_false_when_not_selected()
        => Assert.False(Evaluator.Holds(Values(oscillo: false), C("oscillo", Op.Eq, "true")));

    [Fact]
    public void Eq_on_an_option_is_true_when_selected()
        => Assert.True(Evaluator.Holds(Values(oscillo: true), C("oscillo", Op.Eq, "true")));

    [Fact]
    public void Neq_on_a_choice()
        => Assert.True(Evaluator.Holds(Values(couleur: "gris"), C("couleur", Op.Neq, "\"blanc\"")));

    [Fact]
    public void Unknown_parameter_is_false()
        => Assert.False(Evaluator.Holds(Values(), C("inconnu", Op.Eq, "1")));

    [Fact]
    public void Empty_list_matches()
        => Assert.True(Evaluator.Matches(Values(), new()));

    [Fact]
    public void Matches_needs_all_conditions()
    {
        var both = new List<Condition> { C("dormant", Op.Gte, "60"), C("oscillo", Op.Eq, "true") };

        Assert.True(Evaluator.Matches(Values(dormant: 70, oscillo: true), both));
        Assert.False(Evaluator.Matches(Values(dormant: 70, oscillo: false), both));
    }
}
