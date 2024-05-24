using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace Nebula.Modules;

[NebulaPreprocessForNoS(PreprocessPhaseForNoS.LoadAddons)]
public class NebulaAddon : VariableResourceAllocator, IDisposable, IResourceAllocator
{
    public class AddonMeta
    {
        [JsonSerializableField]
        public string? Id = null;
        [JsonSerializableField]
        public string Name = "Undefined";
        [JsonSerializableField]
        public string Author = "Unknown";
        [JsonSerializableField]
        public string Description = "";
        [JsonSerializableField]
        public string Version = "";
        [JsonSerializableField]
        public int Build = 0;
        [JsonSerializableField]
        public List<string> Dependency = [];

        [JsonSerializableField]
        public bool Hidden = false;
    }

    static private Dictionary<string, NebulaAddon> allAddons = new();
    static private List<NebulaAddon> allOrderedAddons = new();
    static public IEnumerable<NebulaAddon> AllAddons => allOrderedAddons;
    static public NebulaAddon? GetAddon(string id)
    {
        if (allAddons.TryGetValue(id, out var addon)) return addon;
        return null;
    }

    static public IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Addons");

        Directory.CreateDirectory("Addons");

        var md5 = MD5.Create();

        //ローカルなアドオンを更新
        foreach (var dir in Directory.GetDirectories("Addons", "*"))
        {
            string id = Path.GetFileName(dir);
            string filePath = dir + "/" + id + ".zip";
            if (File.Exists(filePath)) File.Move(filePath, "Addons/" + id + ".zip", true);
        }


        //組込アドオンの読み込み
        Assembly assembly = Assembly.GetExecutingAssembly();
        string prefix = "Nebula.Resources.Addons.";
        foreach (var file in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix) && name.EndsWith(".zip")))
        {
            try
            {
                var stream = assembly.GetManifestResourceStream(file);
                if (stream == null) continue;
                var zip = new ZipArchive(stream);

                var addon = new NebulaAddon(zip, file.Substring(prefix.Length)) { IsBuiltIn = true };
                allAddons.Add(addon.Id, addon);

                addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(assembly.GetManifestResourceStream(file)!)).ComputeConstantHash();
            }
            catch
            {
            }
        }

        //外部アドオンの読み込み
        foreach (var file in Directory.GetFiles("Addons"))
        {
            var ext = Path.GetExtension(file);
            if (ext == null) continue;
            if (!ext.Equals(".zip")) continue;

            var zip = ZipFile.OpenRead(file);

            try
            {
                var addon = new NebulaAddon(zip, file);
                allAddons.Add(addon.Id, addon);

                addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(File.OpenRead(file))).ComputeConstantHash();
            }
            catch
            {
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Addon, "Failed to load addon \"" + Path.GetFileName(file) + "\".");
            }
        }

        allOrderedAddons = new();

        List<NebulaAddon> leftAddons = new(allAddons.Values);
        void ResolveOrder(NebulaAddon a)
        {
            leftAddons.Remove(a);
            leftAddons.Do(l => l.UnsolvedDependency.Remove(a.Id));
            allOrderedAddons.Add(a);
        }

        //組み込みアドオン
        leftAddons.Where(a => a.IsBuiltIn).ToArray().Do(ResolveOrder);

        //依存関係が解決したアドオンから順に追加する
        int left = leftAddons.Count;
        while (left > 0)
        {
            leftAddons.Where(a => a.UnsolvedDependency.Count == 0).ToArray().Do(ResolveOrder);
            if (leftAddons.Count == left) break;
            left = leftAddons.Count;
        }
        
        //未解決のアドオン
        foreach(var l in leftAddons)
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Could not resolve dependencies. Excluded addon: \"" + l.AddonName + " (" + l.Id + ")\"");
        }

        AllAddons.Do(a => a.Dependency = a.IdDependencyCache.Select(id => GetAddon(id)).ToArray()!);
    }

    static private string MetaFileName = "addon.meta";
    private NebulaAddon(ZipArchive zip, string path)
    {
        foreach (var entry in zip.Entries)
        {
            if (entry.Name != MetaFileName) continue;

            using var metaFile = entry.Open();

            AddonMeta? meta = (AddonMeta?)JsonStructure.Deserialize(metaFile, typeof(AddonMeta));
            if (meta == null) throw new Exception();

            Id = meta.Id ?? Path.GetFileNameWithoutExtension(path);
            AddonName = meta.Name;
            Author = meta.Author;
            Build = Mathf.Max(0, meta.Build);
            Description = meta.Description;
            Version = meta.Version;
            IdDependencyCache = meta.Dependency.ToArray();
            UnsolvedDependency = new(meta.Dependency);
            IsHidden = meta.Hidden;

            InZipPath = entry.FullName.Substring(0, entry.FullName.Length - MetaFileName.Length);

            NebulaResourceManager.RegisterNamespace(Id ,this);

            break;
        }

        using var iconEntry = zip.GetEntry(InZipPath + "icon.png")?.Open();
        if (iconEntry != null)
        {
            var texture = GraphicsHelper.LoadTextureFromStream(iconEntry);
            texture.MarkDontUnload();

            Icon = texture.ToSprite(Mathf.Max(texture.width, texture.height));
            Icon.MarkDontUnload();
        }

        Archive = zip;
    }

    public Stream? OpenStream(string path)
    {
        return Archive.GetEntry(InZipPath + path.Replace('\\', '/'))?.Open();
    }

    public void Dispose()
    {
        Archive.Dispose();
    }

    public Stream? OpenRead(string innerAddress)
    {
        innerAddress = (InZipPath + innerAddress).Replace('/', '.');
        
        foreach (var entry in Archive.Entries)
        {
            if (entry.FullName.Replace('/', '.').ToLower() == innerAddress.ToLower()) return entry.Open();
        }
        return null;
    }

    public string Id { get; private set; } = "";
    public string InZipPath { get; private set; } = "";
    public string Author { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Version { get; private set; } = "";
    public int Build { get; private set; } = 0;
    public string AddonName { get; private set; } = "";
    public bool IsBuiltIn { get; private set; } = false;
    public string[] IdDependencyCache { get; private set; } = [];
    public NebulaAddon[] Dependency { get; private set; } = [];
    public HashSet<string> UnsolvedDependency { get; private set; } = [];
    public bool IsHidden { get; private set; } = false;
    public Sprite? Icon { get; private set; } = null;
    public ZipArchive Archive { get; private set; }

    //互換性チェックが必要なアドオン
    public bool NeedHandshake { get; set; } = false;

    public int HandshakeHash { get; private set; } = 0;

    public void MarkAsNeedingHandshake() { NeedHandshake = true; }

    static public int AddonHandshakeHash
    {
        get
        {
            int val = 0;
            foreach(var addon in allOrderedAddons)
            {
                if(addon.NeedHandshake) val ^= addon.HandshakeHash;
            }
            return val;
        }
    }

    INebulaResource? IResourceAllocator.GetResource(IReadOnlyArray<string> namespaceArray, string name)
    {
        var baseResult = base.GetResource(namespaceArray, name);
        if (baseResult != null) return baseResult;

        if (namespaceArray.Count > 0) return null;
        if (name.Length == 0) return null;

        return new StreamResource(() => OpenRead("Resources." + name));
    }
}

