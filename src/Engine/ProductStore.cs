using System.Collections.Concurrent;
using System.Text.Json;

namespace Engine;

public record ProductSummary(string Id, string Label, string Manufacturer, int? PublishedVersion);

public class ProductStore
{
    private readonly string dataDir;
    private readonly object writeLock = new();
    private readonly Dictionary<string, Product> drafts = new();
    private readonly ConcurrentDictionary<(string Id, int Version), Product> published = new();
    private readonly ConcurrentDictionary<string, int> latestVersions = new();

    public ProductStore(string dataDir)
    {
        this.dataDir = Path.GetFullPath(dataDir);
        Directory.CreateDirectory(this.dataDir);

        if (!Directory.EnumerateFileSystemEntries(this.dataDir).Any())
        {
            ImportSamples();
            return;
        }

        foreach (string directory in Directory.GetDirectories(this.dataDir))
            LoadProductDirectory(directory);
    }

    public void SaveDraft(Product product)
    {
        CheckId(product.Id);
        lock (writeLock)
        {
            string directory = Path.Combine(dataDir, product.Id);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "draft.json");
            Product snapshot = CopyProduct(product);
            WriteProduct(path, snapshot, overwrite: true);
            drafts[snapshot.Id] = snapshot;
        }
    }

    public Product? GetDraft(string id)
    {
        CheckId(id);
        lock (writeLock)
        {
            if (!drafts.TryGetValue(id, out Product? draft))
                return null;

            return CopyProduct(draft);
        }
    }

    public List<ProductSummary> List()
    {
        lock (writeLock)
        {
            List<ProductSummary> result = new();
            var ids = drafts.Keys.Union(latestVersions.Keys).Order(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                int? version = null;
                if (latestVersions.TryGetValue(id, out int latest))
                    version = latest;

                if (!drafts.TryGetValue(id, out Product? product))
                    product = published[(id, latest)];

                result.Add(new ProductSummary(id, product.Label, product.Manufacturer, version));
            }
            return result;
        }
    }

    public List<string> Publish(string id)
    {
        CheckId(id);
        lock (writeLock)
        {
            if (!drafts.TryGetValue(id, out Product? draft))
                return new List<string> { $"Brouillon introuvable : '{id}'." };

            List<string> errors = Validator.Validate(draft);
            if (errors.Count > 0)
                return errors;

            int version = 1;
            if (latestVersions.TryGetValue(id, out int latest))
                version = checked(latest + 1);

            string path = Path.Combine(dataDir, id, $"v{version}.json");
            if (File.Exists(path))
                throw new IOException($"La version {version} de '{id}' existe déjà.");

            WriteProduct(path, draft, overwrite: false);
            published[(id, version)] = draft;
            latestVersions[id] = version;
            return errors;
        }
    }

    public Product? Get(string id, int? version = null)
    {
        CheckId(id);
        if (version == null)
        {
            if (!latestVersions.TryGetValue(id, out int latest))
                return null;
            version = latest;
        }

        if (!published.TryGetValue((id, version.Value), out Product? product))
            return null;
        return CopyProduct(product);
    }

    private void LoadProductDirectory(string directory)
    {
        string id = Path.GetFileName(directory);
        CheckId(id);
        string draftPath = Path.Combine(directory, "draft.json");
        if (File.Exists(draftPath))
            drafts[id] = ReadStoredProduct(draftPath, id);

        foreach (string path in Directory.GetFiles(directory, "v*.json"))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (!int.TryParse(name.Substring(1), out int version) || version < 1)
                continue;
            if (name != $"v{version}")
                continue;

            published[(id, version)] = ReadStoredProduct(path, id);
            if (!latestVersions.TryGetValue(id, out int latest) || version > latest)
                latestVersions[id] = version;
        }
    }

    private void ImportSamples()
    {
        string samplesDir = Path.Combine(AppContext.BaseDirectory, "samples");
        foreach (string path in Directory.GetFiles(samplesDir, "*.json"))
        {
            Product sample = Json.ReadProduct(path);
            SaveDraft(sample);
            List<string> errors = Publish(sample.Id);
            if (errors.Count > 0)
                throw new InvalidDataException($"Exemple invalide '{path}' : {string.Join(", ", errors)}");
        }
    }

    private static Product ReadStoredProduct(string path, string id)
    {
        Product product = Json.ReadProduct(path);
        if (product.Id != id)
            throw new InvalidDataException($"Le produit dans '{path}' doit avoir l'identifiant '{id}'.");
        return product;
    }

    private static void CheckId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id == "." || id.Contains("..") ||
            id.Contains('/') || id.Contains('\\') || Path.IsPathRooted(id))
            throw new ArgumentException("Identifiant de produit invalide.", nameof(id));
    }

    private static Product CopyProduct(Product product)
    {
        JsonElement snapshot = JsonSerializer.SerializeToElement(product, Json.Options);
        return snapshot.Deserialize<Product>(Json.Options)!;
    }

    private static void WriteProduct(string path, Product product, bool overwrite)
    {
        string temporaryPath = path + "." + Guid.NewGuid() + ".tmp";
        try
        {
            using (FileStream file = File.Create(temporaryPath))
                JsonSerializer.Serialize(file, product, Json.Options);

            File.Move(temporaryPath, path, overwrite);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
