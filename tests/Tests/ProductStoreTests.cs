using Engine;

namespace Tests;

public class ProductStoreTests : IDisposable
{
    private const string SampleId = "fenetre-pvc-1v";
    private readonly string testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string dataDir;

    public ProductStoreTests()
    {
        dataDir = Path.Combine(testDirectory, "data");
        Directory.CreateDirectory(dataDir);
    }

    [Fact]
    public void PublishingTwicePreservesFirstVersionFile()
    {
        ProductStore store = new(dataDir);
        Product product = store.GetDraft(SampleId)!;
        product.Id = "fenetre-test";
        product.Label = "Première version";
        store.SaveDraft(product);
        Assert.Empty(store.Publish(product.Id));

        string directory = Path.Combine(dataDir, product.Id);
        string firstPath = Path.Combine(directory, "v1.json");
        byte[] originalBytes = File.ReadAllBytes(firstPath);

        product.Label = "Deuxième version";
        store.SaveDraft(product);
        Assert.Empty(store.Publish(product.Id));

        Assert.Equal(originalBytes, File.ReadAllBytes(firstPath));
        string secondPath = Path.Combine(directory, "v2.json");
        Assert.Equal("Deuxième version", Json.ReadProduct(secondPath).Label);
    }

    [Fact]
    public void EmptyStoreImportsSampleAndRestoresVersionsAndDraft()
    {
        ProductStore store = new(dataDir);
        Product original = store.Get(SampleId, 1)!;
        Product draft = store.GetDraft(SampleId)!;
        draft.Label = "Deuxième version";
        store.SaveDraft(draft);
        Assert.Empty(store.Publish(SampleId));

        draft.Label = "Travail en cours";
        store.SaveDraft(draft);
        ProductStore restarted = new(dataDir);

        Assert.Equal(original.Label, restarted.Get(SampleId, 1)!.Label);
        Assert.Equal("Deuxième version", restarted.Get(SampleId)!.Label);
        Assert.Equal("Travail en cours", restarted.GetDraft(SampleId)!.Label);
        ProductSummary summary = Assert.Single(restarted.List());
        Assert.Equal(new ProductSummary(SampleId, draft.Label, draft.Manufacturer, 2), summary);
    }

    [Fact]
    public void InvalidDraftReturnsAllErrorsAndDoesNotConsumeVersion()
    {
        ProductStore store = new(dataDir);
        Product draft = store.GetDraft(SampleId)!;
        string publishedLabel = draft.Label;
        draft.Label = "Brouillon invalide";
        draft.Parameters[0].Label = "";
        draft.Parameters[1].Label = "";
        store.SaveDraft(draft);

        Assert.Equal(2, store.Publish(SampleId).Count);
        Assert.Null(store.Get(SampleId, 2));
        string directory = Path.Combine(dataDir, SampleId);
        Assert.Equal(Path.Combine(directory, "v1.json"), Assert.Single(Directory.GetFiles(directory, "v*.json")));
        Assert.Equal(publishedLabel, store.Get(SampleId)!.Label);
        Assert.Equal(1, Assert.Single(store.List()).PublishedVersion);

        store.SaveDraft(store.Get(SampleId)!);
        Assert.Empty(store.Publish(SampleId));
        Assert.NotNull(store.Get(SampleId, 2));
    }

    [Fact]
    public async Task ConcurrentPublicationsCreateDistinctVersions()
    {
        ProductStore store = new(dataDir);
        Task<List<string>> first = Task.Run(() => store.Publish(SampleId));
        Task<List<string>> second = Task.Run(() => store.Publish(SampleId));
        foreach (List<string> errors in await Task.WhenAll(first, second))
            Assert.Empty(errors);

        Assert.Equal(3, Assert.Single(store.List()).PublishedVersion);
        Assert.NotNull(store.Get(SampleId, 2));
        Assert.NotNull(store.Get(SampleId, 3));
        Assert.True(File.Exists(Path.Combine(dataDir, SampleId, "v2.json")));
        Assert.True(File.Exists(Path.Combine(dataDir, SampleId, "v3.json")));
    }

    [Fact]
    public void ExistingVersionIsPreservedAndCacheIsUnchanged()
    {
        ProductStore store = new(dataDir);
        string path = Path.Combine(dataDir, SampleId, "v2.json");
        File.WriteAllText(path, "fichier existant");

        Assert.Throws<IOException>(() => store.Publish(SampleId));
        Assert.Equal("fichier existant", File.ReadAllText(path));
        Assert.Null(store.Get(SampleId, 2));
        Assert.Equal(1, Assert.Single(store.List()).PublishedVersion);
    }

    [Fact]
    public void PublishedReadsUseOnlyMemoryAndReturnIndependentProducts()
    {
        ProductStore store = new(dataDir);
        File.Delete(Path.Combine(dataDir, SampleId, "v1.json"));
        Product product = store.Get(SampleId)!;
        string originalLabel = product.Parameters[0].Label;
        product.Parameters[0].Label = "Modifié";

        Assert.Equal(originalLabel, store.Get(SampleId, 1)!.Parameters[0].Label);
    }

    [Fact]
    public void DraftOnlyProductHasNoPublishedVersion()
    {
        ProductStore store = new(dataDir);
        Product product = store.Get(SampleId)!;
        product.Id = "nouveau";
        product.Rules[0].When[0].Value = default;
        store.SaveDraft(product);
        product.Label = "Modification après enregistrement";

        ProductStore restarted = new(dataDir);
        Assert.NotEqual(product.Label, restarted.GetDraft("nouveau")!.Label);
        Assert.Null(restarted.Get("nouveau"));
        Assert.Null(restarted.List().Single(item => item.Id == "nouveau").PublishedVersion);
        Assert.NotEmpty(restarted.Publish("nouveau"));
        Assert.Null(restarted.GetDraft("inconnu"));
    }

    [Theory]
    [InlineData("../pirate")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a..b")]
    public void UnsafeIdsAreRejected(string id)
    {
        ProductStore store = new(dataDir);
        Product product = store.Get(SampleId)!;
        product.Id = id;

        Assert.Throws<ArgumentException>(() => store.SaveDraft(product));
        Assert.Throws<ArgumentException>(() => store.Publish(id));
        Assert.Throws<ArgumentException>(() => store.GetDraft(id));
        Assert.Throws<ArgumentException>(() => store.Get(id));
        Assert.False(Directory.Exists(Path.Combine(testDirectory, "pirate")));
    }

    public void Dispose()
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}
