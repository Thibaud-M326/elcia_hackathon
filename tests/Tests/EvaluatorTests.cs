using System.Globalization;
using System.Text.Json;
using Engine;

namespace Tests;

public class EvaluatorTests
{
    private static readonly Product P = Json.ReadProduct("product-model-v1.json");

    private static EvaluationResult Eval(Dictionary<string, object?> input)
        => Evaluator.Evaluate(P, 1, input);

    // Le dormant est une liste de choix ; le volet n'est possible qu'à partir de 100 mm,
    // et le dormant par défaut fait 60 mm : par défaut, le volet est indisponible.
    private const string Dormant60 = "Dormant de 60mm";
    private const string Dormant100 = "Dormant de 100mm";

    // Produit de référence + une règle ajoutée pour le test (fonctionnalité absente du produit)
    private static Product With(params Rule[] rules)
    {
        Product product = Json.ReadProduct("product-model-v1.json");
        product.Rules.AddRange(rules);
        return product;
    }

    private static EvaluationResult Eval(Product product, Dictionary<string, object?> input)
        => Evaluator.Evaluate(product, 1, input);

    [Fact]
    public void Empty_input_is_valid_and_uses_defaults()
    {
        var r = Eval(new());

        Assert.Empty(r.Errors);
        Assert.True(r.Valid);
        Assert.Equal(1200m, r.Values["largeur"]);
        Assert.Equal(1200m, r.Values["hauteur"]);
        Assert.Equal("blanc", r.Values["couleur"]);
        Assert.Equal(Dormant60, r.Values["dormant"]);
        Assert.Equal("Poignée en titane", r.Values["poignee"]);
        Assert.Equal(false, r.Values["volet"]);
    }

    [Fact]
    public void Input_value_replaces_default()
    {
        var r = Eval(new() { ["largeur"] = 1500, ["volet"] = true, ["dormant"] = Dormant100 });

        Assert.Empty(r.Errors);
        Assert.Equal(1500m, r.Values["largeur"]);
        Assert.Equal(true, r.Values["volet"]);
        Assert.Equal(Dormant100, r.Values["dormant"]);
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
        Assert.Contains("Dimensions 2400 x 2100 non fabricables.", r.Errors);
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
        Assert.Contains("Dimensions 1500 x 1200 non fabricables.", exact.Errors);
        Assert.False(nextStep.Valid);
        Assert.Contains("Dimensions 1400 x 1100 non fabricables.", nextStep.Errors);
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
        var r = Eval(new() { ["volet"] = true, ["dormant"] = Dormant100 });   // 0,18 € × 1200 mm

        Assert.Empty(r.Errors);
        Assert.Equal(216m, LineAmount(r, "Volet roulant"));
    }

    [Fact]
    public void Per_unit_price_follows_the_parameter_value()
    {
        var r = Eval(new() { ["volet"] = true, ["dormant"] = Dormant100, ["largeur"] = 1500 });   // 0,18 € × 1500 mm

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

        Assert.Equal(4, r.Options.Count);   // oscillo, feuillete, volet, motorisation
        Assert.All(r.Options, o => Assert.False(o.Selected));
        Assert.All(r.Options, o => Assert.False(o.AutoAdded));
        Assert.All(r.Options, o => Assert.Null(o.AutoAddedBy));
        Assert.Equal("Volet roulant", Option(r, "volet").Label);
    }

    [Fact]
    public void Motor_adds_the_shutter_automatically()
    {
        var r = Eval(new() { ["motorisation"] = true, ["dormant"] = Dormant100 });

        Assert.Empty(r.Errors);
        Assert.True(Option(r, "volet").Selected);
        Assert.True(Option(r, "volet").AutoAdded);
        Assert.Equal("motorisation", Option(r, "volet").AutoAddedBy);
        Assert.Equal(216m, LineAmount(r, "Volet roulant"));        // 0,18 € × 1200 mm
        Assert.Equal(180m, LineAmount(r, "Motorisation du volet"));
    }

    [Fact]
    public void Option_chosen_by_the_salesperson_is_not_auto_added()
    {
        var r = Eval(new() { ["volet"] = true, ["dormant"] = Dormant100 });

        Assert.True(Option(r, "volet").Selected);
        Assert.False(Option(r, "volet").AutoAdded);
        Assert.Null(Option(r, "volet").AutoAddedBy);
    }

    [Fact]
    public void Option_chosen_by_the_salesperson_and_implied_is_not_auto_added()
    {
        var r = Eval(new() { ["motorisation"] = true, ["volet"] = true, ["dormant"] = Dormant100 });

        Assert.True(Option(r, "volet").Selected);
        Assert.False(Option(r, "volet").AutoAdded);
        Assert.Null(Option(r, "volet").AutoAddedBy);
    }

    [Fact]
    public void Implies_cascades()
    {
        // oscillo → motorisation → volet
        var r = Eval(With(Implies("oscillo", "motorisation")), new() { ["oscillo"] = true });

        Assert.True(Option(r, "motorisation").AutoAdded);
        Assert.True(Option(r, "volet").AutoAdded);
        Assert.Equal("oscillo", Option(r, "motorisation").AutoAddedBy);
        Assert.Equal("oscillo", Option(r, "volet").AutoAddedBy);
        Assert.False(Option(r, "oscillo").AutoAdded);
    }

    [Fact]
    public void Implies_loop_does_not_hang()
    {
        // motorisation → volet (déjà dans le produit) et volet → motorisation
        var r = Eval(With(Implies("volet", "motorisation")), new() { ["volet"] = true });

        Assert.True(Option(r, "motorisation").Selected);
        Assert.True(Option(r, "motorisation").AutoAdded);
    }

    // ---------- A6 : disponibilité ----------

    private const string VoletDormant60 = "Le dormant doit être d'au moins 100mm (actuel: Dormant de 60mm)";
    private const string OscilloFeuillete = "L'oscillo-battant est incompatible avec le vitrage feuilleté.";

    // Le produit n'a pas d'incompatibilité : on en ajoute une pour tester le moteur
    private static readonly Product WithIncompatibility = With(new Rule
    {
        Id = "oscillo-feuillete",
        Type = RuleType.Incompatible,
        Option = "oscillo",
        Other = "feuillete",
        Message = OscilloFeuillete,
    });

    [Fact]
    public void Default_frame_makes_only_the_shutter_unavailable()
    {
        var r = Eval(new());

        Assert.False(Option(r, "volet").Available);
        Assert.Equal(VoletDormant60, Assert.Single(Option(r, "volet").Reasons));
        Assert.All(r.Options.Where(o => o.Id != "volet"), o => Assert.True(o.Available));
        Assert.All(r.Options.Where(o => o.Id != "volet"), o => Assert.Empty(o.Reasons));
    }

    [Theory]
    [InlineData("Dormant de 100mm")]
    [InlineData("Dormant de 200mm")]
    public void Thick_frame_makes_every_option_available(string dormant)
    {
        var r = Eval(new() { ["dormant"] = dormant });

        Assert.All(r.Options, o => Assert.True(o.Available));
        Assert.All(r.Options, o => Assert.Empty(o.Reasons));
    }

    [Fact]
    public void Unselected_unavailable_option_is_greyed_without_error()
    {
        var r = Eval(new());

        Assert.False(Option(r, "volet").Available);
        Assert.Empty(r.Errors);
        Assert.True(r.Valid);
    }

    [Fact]
    public void Selected_unavailable_option_is_an_error()
    {
        var r = Eval(new() { ["volet"] = true });

        Assert.False(r.Valid);
        Assert.Contains(VoletDormant60, r.Errors);
    }

    [Fact]
    public void Auto_added_unavailable_option_is_an_error()
    {
        // La motorisation ajoute le volet (A5), mais le dormant de 60 mm l'interdit
        var r = Eval(new() { ["motorisation"] = true });

        Assert.True(Option(r, "volet").AutoAdded);
        Assert.Equal("motorisation", Option(r, "volet").AutoAddedBy);
        Assert.False(Option(r, "volet").Available);
        Assert.False(r.Valid);
        Assert.Contains(VoletDormant60, r.Errors);
    }

    [Fact]
    public void Message_is_filled_with_the_current_value()
    {
        var r = Eval(new() { ["dormant"] = "Dormant de 60mm" });

        Assert.Contains("Le dormant doit être d'au moins 100mm (actuel: Dormant de 60mm)", Option(r, "volet").Reasons);
    }

    [Fact]
    public void Both_incompatible_options_selected_is_an_error()
    {
        var r = Eval(WithIncompatibility, new() { ["oscillo"] = true, ["feuillete"] = true });

        Assert.False(r.Valid);
        Assert.Contains(OscilloFeuillete, r.Errors);
    }

    [Fact]
    public void Selected_option_greys_out_the_incompatible_one()
    {
        var r = Eval(WithIncompatibility, new() { ["oscillo"] = true });

        Assert.Empty(r.Errors);
        Assert.True(Option(r, "oscillo").Available);
        Assert.False(Option(r, "feuillete").Available);
        Assert.Contains(OscilloFeuillete, Option(r, "feuillete").Reasons);
    }

    [Fact]
    public void Incompatibility_works_both_ways()
    {
        var r = Eval(WithIncompatibility, new() { ["feuillete"] = true });

        Assert.Empty(r.Errors);
        Assert.False(Option(r, "oscillo").Available);
        Assert.Contains(OscilloFeuillete, Option(r, "oscillo").Reasons);
    }

    [Fact]
    public void Option_can_have_several_reasons()
    {
        // Volet : dormant trop fin (availableIf) ET incompatible avec le feuilleté (règle ajoutée)
        Product product = With(new Rule
        {
            Id = "volet-feuillete",
            Type = RuleType.Incompatible,
            Option = "volet",
            Other = "feuillete",
            Message = "Pas de volet avec le vitrage feuilleté.",
        });

        var r = Eval(product, new() { ["feuillete"] = true });

        Assert.Equal(2, Option(r, "volet").Reasons.Count);
        Assert.Empty(r.Errors);   // le volet n'est pas coché
    }

    // ---------- A7 : surcharges ----------

    private static bool HasLine(EvaluationResult r, string label)
        => r.PriceLines.Any(l => l.Label == label);

    [Fact]
    public void Colour_surcharge_is_a_percent_of_the_base_price()
    {
        var r = Eval(new() { ["couleur"] = "gris" });   // 12 % de 419 €

        Assert.Empty(r.Errors);
        Assert.Equal(50.28m, LineAmount(r, "Plus-value couleur"));
    }

    [Fact]
    public void Colour_surcharge_follows_the_base_price()
    {
        var r = Eval(new() { ["couleur"] = "gris", ["largeur"] = 1500 });   // 12 % de 459 €

        Assert.Equal(55.08m, LineAmount(r, "Plus-value couleur"));
    }

    [Fact]
    public void Colour_surcharge_ignores_option_prices()
    {
        // 12 % du prix de base (419 €), pas du total avec l'oscillo-battant (474 €)
        var r = Eval(new() { ["couleur"] = "gris", ["oscillo"] = true });

        Assert.Equal(50.28m, LineAmount(r, "Plus-value couleur"));
    }

    [Fact]
    public void White_has_no_colour_surcharge()
    {
        var r = Eval(new() { ["couleur"] = "blanc" });

        Assert.False(HasLine(r, "Plus-value couleur"));
    }

    [Theory]
    [InlineData("Poignée en laiton", "Plus value poignée en laiton", 15)]
    [InlineData("Poignée moderne inox", "Plus value poignée moderne inox", 50)]
    public void Handle_choice_adds_its_surcharge(string poignee, string label, int amount)
    {
        var r = Eval(new() { ["poignee"] = poignee });

        Assert.Empty(r.Errors);
        Assert.Equal(amount, LineAmount(r, label));
        Assert.Equal(419m + amount, r.Total);
    }

    [Fact]
    public void Default_handle_has_no_surcharge()
    {
        var r = Eval(new() { ["poignee"] = "Poignée en titane" });

        Assert.False(HasLine(r, "Plus value poignée en laiton"));
        Assert.False(HasLine(r, "Plus value poignée moderne inox"));
    }

    [Fact]
    public void Large_window_with_tilt_and_turn_gets_reinforcement()
    {
        var r = Eval(new() { ["largeur"] = 1800, ["oscillo"] = true });

        Assert.Empty(r.Errors);
        Assert.Equal(40m, LineAmount(r, "Renfort grande largeur"));
    }

    [Fact]
    public void Large_window_without_tilt_and_turn_has_no_reinforcement()
    {
        var r = Eval(new() { ["largeur"] = 1800 });

        Assert.False(HasLine(r, "Renfort grande largeur"));
    }

    [Fact]
    public void Several_surcharges_can_apply()
    {
        var r = Eval(new() { ["couleur"] = "gris", ["largeur"] = 1800, ["oscillo"] = true, ["poignee"] = "Poignée en laiton" });

        Assert.True(HasLine(r, "Plus-value couleur"));
        Assert.True(HasLine(r, "Renfort grande largeur"));
        Assert.True(HasLine(r, "Plus value poignée en laiton"));
    }

    [Fact]
    public void No_surcharge_line_when_not_manufacturable()
    {
        var r = Eval(new() { ["largeur"] = 2400, ["hauteur"] = 2100, ["couleur"] = "gris" });

        Assert.False(r.Valid);
        Assert.Empty(r.PriceLines);
    }

    // ---------- A8 : total et validité (tests de référence, à garder pour toujours) ----------

    [Fact]
    public void Reference_default_configuration()
    {
        var r = Eval(new());

        Assert.True(r.Valid);
        Assert.Equal(419.00m, r.Total);
    }

    [Fact]
    public void Reference_grey_with_motor()
    {
        // 419 (base) + 216 (volet ajouté) + 180 (motorisation) + 50,28 (plus-value couleur)
        var r = Eval(new() { ["couleur"] = "gris", ["motorisation"] = true, ["dormant"] = Dormant100 });

        Assert.True(r.Valid);
        Assert.Equal(865.28m, r.Total);
        Assert.True(Option(r, "volet").AutoAdded);
    }

    [Fact]
    public void Reference_grey_with_motor_and_thin_frame_is_invalid()
    {
        // Même prix, mais le dormant par défaut (60 mm) interdit le volet ajouté par la motorisation
        var r = Eval(new() { ["couleur"] = "gris", ["motorisation"] = true });

        Assert.False(r.Valid);
        Assert.Equal(865.28m, r.Total);
        Assert.Contains(VoletDormant60, r.Errors);
    }

    [Fact]
    public void Reference_not_manufacturable_has_no_total()
    {
        var r = Eval(new() { ["largeur"] = 2400, ["hauteur"] = 2100 });

        Assert.False(r.Valid);
        Assert.Null(r.Total);
    }

    [Fact]
    public void Input_error_has_no_total()
    {
        var r = Eval(new() { ["largeur"] = 5000 });

        Assert.False(r.Valid);
        Assert.Null(r.Total);
    }

    [Fact]
    public void Total_is_the_sum_of_all_lines()
    {
        var r = Eval(new()
        {
            ["couleur"] = "gris",
            ["largeur"] = 1800,
            ["oscillo"] = true,
            ["feuillete"] = true,
            ["poignee"] = "Poignée moderne inox",
        });

        decimal sum = 0;
        foreach (PriceLine line in r.PriceLines)
            sum += line.Amount;

        Assert.Equal(sum, r.Total);
    }

    [Fact]
    public void Version_is_returned_in_every_case()
    {
        Assert.Equal(3, Evaluator.Evaluate(P, 3, new Dictionary<string, object?>()).Version);
        Assert.Equal(3, Evaluator.Evaluate(P, 3, new Dictionary<string, object?> { ["largeur"] = 5000 }).Version);
    }

    [Fact]
    public void Values_are_returned_on_input_error()
    {
        var r = Eval(new() { ["largeur"] = 5000 });

        Assert.False(r.Valid);
        Assert.Null(r.Total);
        Assert.False(r.Values.ContainsKey("largeur"));
        Assert.Equal(1200m, r.Values["hauteur"]);
        Assert.Equal("blanc", r.Values["couleur"]);
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void Message_values_do_not_depend_on_culture(string culture)
    {
        // Le produit n'a pas de message avec un nombre : on ajoute une règle qui en affiche un
        Product product = With(new Rule
        {
            Id = "oscillo-largeur",
            Type = RuleType.AvailableIf,
            Option = "oscillo",
            When = { new Condition { Param = "largeur", Op = Op.Lte, Value = JsonSerializer.SerializeToElement(2000) } },
            Message = "Largeur de {largeur} mm : trop large pour l'oscillo-battant.",
        });

        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);

            var r = Eval(product, new() { ["oscillo"] = true, ["largeur"] = 2100.5m });

            Assert.Contains("Largeur de 2100.5 mm : trop large pour l'oscillo-battant.", r.Errors);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
