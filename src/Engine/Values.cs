using System.Text.Json;

namespace Engine;

public static class Values
{
    public static object? Normalize(object? raw, ParamType type) => type switch
    {
        ParamType.Number => ToDecimal(raw),
        ParamType.Choice => ToText(raw),
        ParamType.Option => ToBool(raw),
        _ => null,
    };

    private static decimal? ToDecimal(object? raw) => raw switch
    {
        int i => i,
        long l => l,
        double d => (decimal)d,
        decimal m => m,
        JsonElement e when e.ValueKind == JsonValueKind.Number => e.GetDecimal(),
        _ => null,
    };

    private static string? ToText(object? raw) => raw switch
    {
        string s => s,
        JsonElement e when e.ValueKind == JsonValueKind.String => e.GetString(),
        _ => null,
    };

    private static bool? ToBool(object? raw) => raw switch
    {
        bool b => b,
        JsonElement e when e.ValueKind is JsonValueKind.True or JsonValueKind.False => e.GetBoolean(),
        _ => null,
    };
}
