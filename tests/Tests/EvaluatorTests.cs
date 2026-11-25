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

    // ---------- Test de prix ------------

    private static decimal BasePrice(EvaluationResult r)
        => r.PriceLines.Single(l => l.Label == "Prix de base").Amount;

    [Fact]
    public void Base_price_on_exact_steps()
    {
        var r = Eval(new() { ["largeur"] = 1200, ["hauteur"] = 1200 });

        Assert.Empty(r.Errors);
        Assert.Equal(419m, BasePrice(r));
    }

    [Fact]
    public void Base_price_uses_next_step_up()
    {
        var r = Eval(new() { ["largeur"] = 1100, ["hauteur"] = 1000 });

        Assert.Empty(r.Errors);
        Assert.Equal(419m, BasePrice(r));
    }

    [Fact]
    public void Base_price_on_first_step()
    {
        var r = Eval(new() { ["largeur"] = 600, ["hauteur"] = 600 });

        Assert.Empty(r.Errors);
        Assert.Equal(277m, BasePrice(r));
    }

    [Fact]
    public void Empty_grid_cell_is_not_manufacturable()
    {
        var r = Eval(new() { ["largeur"] = 2400, ["hauteur"] = 2100 });

        Assert.False(r.Valid);
        Assert.Null(r.Total);
        Assert.Contains("Dimensions 2400 × 2100 non fabricables.", r.Errors);
        Assert.Empty(r.PriceLines);
    }

    [Fact]
    public void Empty_cell_in_the_middle_is_not_manufacturable()
    {
        Product product = Json.ReadProduct("product-model-v1.json");
        product.Grid.Prices[2][3] = null; 

        var exact = Evaluator.Evaluate(product, 1,
            new Dictionary<string, object?> { ["largeur"] = 1500, ["hauteur"] = 1200 });
        var nextStep = Evaluator.Evaluate(product, 1,
            new Dictionary<string, object?> { ["largeur"] = 1400, ["hauteur"] = 1100 });

        Assert.False(exact.Valid);
        Assert.Contains("Dimensions 1500 × 1200 non fabricables.", exact.Errors);
        Assert.False(nextStep.Valid);
        Assert.Contains("Dimensions 1400 × 1100 non fabricables.", nextStep.Errors);
    }

    // ---------- A3 : prix des options ----------

    private static decimal LineAmount(EvaluationResult r, string label)
        => r.PriceLines.Single(l => l.Label == label).Amount;

    [Fact]
    public void Fixed_price_option()
    {
        var r = Eval(new() { ["oscillo"] = true });

        Assert.Empty(r.Errors);
        Assert.Equal(55m, LineAmount(r, "Oscillo-battant"));
    }

    [Fact]
    public void Percent_price_option_uses_base_price()
    {
        var r = Eval(new() { ["feuillete"] = true });   // 15 % de 419 €

        Assert.Empty(r.Errors);
        Assert.Equal(62.85m, LineAmount(r, "Vitrage feuilleté"));
    }

    [Fact]
    public void Per_unit_price_option_uses_another_parameter()
    {
        var r = Eval(new() { ["croisillons"] = true });   // 0,05 € × 1200 mm

        Assert.Empty(r.Errors);
        Assert.Equal(60m, LineAmount(r, "Croisillons"));
    }

    [Fact]
    public void Per_unit_price_follows_the_parameter_value()
    {
        var r = Eval(new() { ["volet"] = true, ["largeur"] = 1500 });   // 0,18 € × 1500 mm

        Assert.Empty(r.Errors);
        Assert.Equal(270m, LineAmount(r, "Volet roulant"));
    }

    [Fact]
    public void Several_options_give_one_line_each()
    {
        var r = Eval(new() { ["oscillo"] = true, ["feuillete"] = true });

        Assert.Equal(3, r.PriceLines.Count);   // prix de base + 2 options
        Assert.Equal(55m, LineAmount(r, "Oscillo-battant"));
        Assert.Equal(62.85m, LineAmount(r, "Vitrage feuilleté"));
    }

    [Fact]
    public void Unselected_options_add_no_line()
    {
        var r = Eval(new());

        var line = Assert.Single(r.PriceLines);
        Assert.Equal("Prix de base", line.Label);
    }

    [Fact]
    public void No_option_line_when_not_manufacturable()
    {
        var r = Eval(new() { ["largeur"] = 2400, ["hauteur"] = 2100, ["oscillo"] = true });

        Assert.False(r.Valid);
        Assert.Empty(r.PriceLines);
    }

    // ---------- A5 : cascade implies ----------

    private static OptionState Option(EvaluationResult r, string id)
        => r.Options.Single(o => o.Id == id);

    private static Rule Implies(string option, string other)
        => new() { Id = $"{option}-{other}", Type = RuleType.Implies, Option = option, Other = other };

    [Fact]
    public void Every_option_has_a_state()
    {
        var r = Eval(new());

        Assert.Equal(5, r.Options.Count);   // oscillo, croisillons, feuillete, volet, motorisation
        Assert.All(r.Options, o => Assert.False(o.Selected));
        Assert.All(r.Options, o => Assert.False(o.AutoAdded));
        Assert.Equal("Volet roulant", Option(r, "volet").Label);
    }

    [Fact]
    public void Motor_adds_the_shutter_automatically()
    {
        var r = Eval(new() { ["motorisation"] = true });

        Assert.Empty(r.Errors);
        Assert.True(Option(r, "volet").Selected);
        Assert.True(Option(r, "volet").AutoAdded);
        Assert.Equal(216m, LineAmount(r, "Volet roulant"));        // 0,18 € × 1200 mm
        Assert.Equal(180m, LineAmount(r, "Motorisation du volet"));
    }

    [Fact]
    public void Option_chosen_by_the_salesperson_is_not_auto_added()
    {
        var r = Eval(new() { ["volet"] = true });

        Assert.True(Option(r, "volet").Selected);
        Assert.False(Option(r, "volet").AutoAdded);
    }

    [Fact]
    public void Option_chosen_by_the_salesperson_and_implied_is_not_auto_added()
    {
        var r = Eval(new() { ["motorisation"] = true, ["volet"] = true });

        Assert.True(Option(r, "volet").Selected);
        Assert.False(Option(r, "volet").AutoAdded);
    }

    [Fact]
    public void Implies_cascades()
    {
        // oscillo → motorisation → volet
        Product product = Json.ReadProduct("product-model-v1.json");
        product.Rules.Add(Implies("oscillo", "motorisation"));

        var r = Evaluator.Evaluate(product, 1, new Dictionary<string, object?> { ["oscillo"] = true });

        Assert.True(Option(r, "motorisation").AutoAdded);
        Assert.True(Option(r, "volet").AutoAdded);
        Assert.False(Option(r, "oscillo").AutoAdded);
    }

    [Fact]
    public void Implies_loop_does_not_hang()
    {
        // motorisation → volet (déjà dans le produit) et volet → motorisation
        Product product = Json.ReadProduct("product-model-v1.json");
        product.Rules.Add(Implies("volet", "motorisation"));

        var r = Evaluator.Evaluate(product, 1, new Dictionary<string, object?> { ["volet"] = true });

        Assert.True(Option(r, "motorisation").Selected);
        Assert.True(Option(r, "motorisation").AutoAdded);
    }

    // ---------- A6 : disponibilité ----------

    private const string VoletDormant50 = "Dormant de 50 mm : il faut au moins 60 mm pour un volet roulant.";
    private const string CroisillonsFeuillete = "Les croisillons sont incompatibles avec le vitrage feuilleté.";

    [Fact]
    public void Default_configuration_has_every_option_available()
    {
        var r = Eval(new());

        Assert.All(r.Options, o => Assert.True(o.Available));
        Assert.All(r.Options, o => Assert.Empty(o.Reasons));
    }

    [Fact]
    public void Unselected_unavailable_option_is_greyed_without_error()
    {
        var r = Eval(new() { ["dormant"] = 50 });

        Assert.False(Option(r, "volet").Available);
        Assert.Contains(VoletDormant50, Option(r, "volet").Reasons);
        Assert.Empty(r.Errors);
        Assert.True(r.Valid);
    }

    [Fact]
    public void Selected_unavailable_option_is_an_error()
    {
        var r = Eval(new() { ["dormant"] = 50, ["volet"] = true });

        Assert.False(r.Valid);
        Assert.Contains(VoletDormant50, r.Errors);
    }

    [Fact]
    public void Auto_added_unavailable_option_is_an_error()
    {
        // La motorisation ajoute le volet (A5), mais le dormant de 50 mm l'interdit
        var r = Eval(new() { ["dormant"] = 50, ["motorisation"] = true });

        Assert.True(Option(r, "volet").AutoAdded);
        Assert.False(Option(r, "volet").Available);
        Assert.False(r.Valid);
        Assert.Contains(VoletDormant50, r.Errors);
    }

    [Fact]
    public void Message_is_filled_with_the_current_value()
    {
        var r = Eval(new() { ["couleur"] = "chene" });

        Assert.False(Option(r, "croisillons").Available);
        Assert.Contains("Les croisillons n'existent pas en chene.", Option(r, "croisillons").Reasons);
    }

    [Fact]
    public void Both_incompatible_options_selected_is_an_error()
    {
        var r = Eval(new() { ["croisillons"] = true, ["feuillete"] = true });

        Assert.False(r.Valid);
        Assert.Contains(CroisillonsFeuillete, r.Errors);
    }

    [Fact]
    public void Selected_option_greys_out_the_incompatible_one()
    {
        var r = Eval(new() { ["croisillons"] = true });

        Assert.Empty(r.Errors);
        Assert.True(Option(r, "croisillons").Available);
        Assert.False(Option(r, "feuillete").Available);
        Assert.Contains(CroisillonsFeuillete, Option(r, "feuillete").Reasons);
    }

    [Fact]
    public void Incompatibility_works_both_ways()
    {
        var r = Eval(new() { ["feuillete"] = true });

        Assert.Empty(r.Errors);
        Assert.False(Option(r, "croisillons").Available);
        Assert.Contains(CroisillonsFeuillete, Option(r, "croisillons").Reasons);
    }

    [Fact]
    public void Option_can_have_several_reasons()
    {
        // Croisillons : pas en chêne (availableIf) ET incompatibles avec le feuilleté
        var r = Eval(new() { ["couleur"] = "chene", ["feuillete"] = true });

        Assert.Equal(2, Option(r, "croisillons").Reasons.Count);
        Assert.Empty(r.Errors);   // les croisillons ne sont pas cochés
    }
}
