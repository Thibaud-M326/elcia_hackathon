using Engine;

namespace Tests;

public class JsonTests
{
    [Fact]
    public void ReadProduct_LitLeModeleV1()
    {
        Product p = Json.ReadProduct("product-model-v1.json");

        Assert.Equal(9, p.Parameters.Count);
        Assert.Equal(6, p.Rules.Count);
        Assert.Equal(6, p.Grid.Prices.Count);
        Assert.Equal(7, p.Grid.Prices[0].Count);

        // Les enums sont lus depuis les textes du JSON
        Assert.Equal(ParamType.Number, p.Parameters[0].Type);
        Assert.Equal(RuleType.AvailableIf, p.Rules[0].Type);
        Assert.Equal(PriceType.PerUnit, p.Parameters[5].Price!.Type);

        // Les prix sont lus, y compris la case vide
        Assert.Equal(419m, p.Grid.Prices[2][2]);
        Assert.Null(p.Grid.Prices[5][6]);
    }
}
