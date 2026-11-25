using System.Reflection.Emit;
using System.Text.Json;

namespace Engine;

public enum ParamType { Number, Choice, Option }
public enum PriceType { Fixed, Percent, PerUnit }
public enum RuleType  { AvailableIf, Incompatible, Implies, Surcharge }
public enum Op        { Eq, Neq, Gte, Lte, In }

public class Product
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public List<Parameter> Parameters { get; set; } = new();
    public Grid Grid { get; set; } = new();
    public List<Rule> Rules { get; set; } = new();
}

public class Parameter
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public ParamType Type { get; set; }

    public decimal? Min { get; set; }
    public decimal? Max { get; set; }

    public List<string>? Choices { get; set; }

    public JsonElement? Default { get; set; }

    public Price? Price { get; set; }
}

public class Price
{
    public PriceType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal Rate { get; set; }
    public string? Param { get; set; }
}

public class Grid
{
    public string Label { get; set; } = "";
    public List<decimal> Widths { get; set; } = new();
    public List<decimal> Heights { get; set; } = new();

    public List<List<decimal?>> Prices { get; set; } = new();
}

public class Rule
{
    public string Id { get; set; } = "";
    public RuleType Type { get; set; }
    public string? Option { get; set; }
    public string? Other { get; set; }
    public List<Condition> When { get; set; } = new();
    public string? Message { get; set; }
    public string? Label { get; set; }
    public Price? Price { get; set; }
}

public class Condition
{
    public string Param { get; set; } = "";
    public Op Op { get; set; }

    public JsonElement Value { get; set; }
}
