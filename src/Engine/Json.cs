using System.Text.Json;
using System.Text.Json.Serialization;

namespace Engine;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = true,
    };

    public static Product ReadProduct(string path)
    {
        string text = File.ReadAllText(path);
        Product? product = JsonSerializer.Deserialize<Product>(text, Options);

        if (product == null)
            throw new InvalidOperationException($"Le fichier {path} ne contient pas de produit.");

        return product;
    }
}
