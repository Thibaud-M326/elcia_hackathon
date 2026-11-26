namespace Engine;

public class EvaluationResult
{
    public bool Valid { get; set; }
    public decimal? Total { get; set; }
    public int Version { get; set; }
    public List<PriceLine> PriceLines { get; set; } = new();
    public List<OptionState> Options { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Values { get; set; } = new();
}

public class PriceLine
{
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
}

public class OptionState
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Selected { get; set; }
    public bool Available { get; set; }
    public bool AutoAdded { get; set; }
    public string? AutoAddedBy { get; set; }
    public List<string> Reasons { get; set; } = new();
}
