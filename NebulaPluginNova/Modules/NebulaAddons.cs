using Nebula.Behavior;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;
using static Nebula.Modules.ModUpdater;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.LoadAddons)]
public class NebulaAddon : VariableResourceAllocator, IDisposable, IResourceAllocator
{
    static private MD5 md5 = MD5.Create();

    public class MarketplaceAddonMeta
    {
        [JsonSerializableField]
        public int Id = -1;
        [JsonSerializableField]
        public long Release = 0;
        [JsonSerializableField]
        public int Hash = 0;

    }

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

    public class GitHubReleaseContent
    {
        public class GitHubReleaseAsset
        {
            [JsonSerializableField]
            public long id = 0;

            [JsonSerializableField]
            public string browser_download_url = "";

            [JsonSerializableField]
            public string name = "";
        }

        [JsonSerializableField]
        public List<GitHubReleaseAsset>? assets = null!;
    }


    static private Dictionary<string, NebulaAddon> allAddons = [];
    static private List<NebulaAddon> allOrderedAddons = [];
    static public IEnumerable<NebulaAddon> AllAddons => allOrderedAddons;
    static public NebulaAddon? GetAddon(string id)
    {
        if (allAddons.TryGetValue(id, out var addon)) return addon;
        return null;
    }

    static private IEnumerable<string> ExternalAddons()
    {
        foreach (var file in Directory.GetFiles("Addons"))
        {
            var ext = Path.GetExtension(file);
            if (ext == null) continue;
            if (!ext.Equals(".zip") && !ext.Equals(".addon")) continue;
            yield return file;
        }
    }

    private static async Task FetchAndDownloadAddon(string url, long? lastId, int entryId, Action? onDownload = null)
    {
        var response = await NebulaPlugin.HttpClient.GetAsync(url);
        if (response.StatusCode != HttpStatusCode.OK) return;
        string json = await response.Content.ReadAsStringAsync();

        var assets = JsonStructure.Deserialize<GitHubReleaseContent>(json);

        var addonAsset = assets?.assets?.FirstOrDefault(a => a.name.EndsWith(".zip") || a.name.EndsWith(".addon"));
        if (addonAsset == null) return;

        if(!lastId.HasValue || lastId.Value != addonAsset.id)
        {
            onDownload?.Invoke();
            response = await NebulaPlugin.HttpClient.GetAsync(addonAsset.browser_download_url);
            if (response.StatusCode != HttpStatusCode.OK) return;
            var dllStream = await response.Content.ReadAsStreamAsync();

            try
            {
                //ダウンロードしたファイルを配置
                string path = "Addons" + Path.DirectorySeparatorChar + "[M" + entryId + "]" + addonAsset.name;
                using (var fileStream = File.Create(path))
                {
                    dllStream.CopyTo(fileStream);
                    fileStream.Flush();
                }

                //元のハッシュ値を計算
                int hash;
                using (var file = File.Open(path, FileMode.Open))
                {
                    hash = System.BitConverter.ToString(md5.ComputeHash(file)).ComputeConstantHash();
                }

                //メタ情報を付加
                using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
                var entry = zip.CreateEntry(".marketplace");
                using var writer = new StreamWriter(entry.Open());

                writer.Write(JsonStructure.Serialize(new MarketplaceAddonMeta() { Id = entryId, Release = addonAsset.id, Hash = hash }));
            }
            catch (Exception ex)
            {
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, ex.GetType().Name + ex.Message + ex.StackTrace);
            }
        }
    }

    static public IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Directory.CreateDirectory("Addons");

        yield return preprocessor.SetLoadingText("Fetching Addons");

        //ローカルなアドオンを更新
        foreach (var dir in Directory.GetDirectories("Addons", "*"))
        {
            string id = Path.GetFileName(dir);
            string filePath = dir + "/" + id + ".zip";
            if (File.Exists(filePath)) File.Move(filePath, "Addons/" + id + ".zip", true);
        }

        List<int> existingEntry = [];
        foreach(var path in ExternalAddons())
        {
            var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry(".marketplace");
            if(entry == null) continue;

            using var stream = entry.Open();
            var meta = JsonStructure.Deserialize<MarketplaceAddonMeta>(stream)!;
            zip.Dispose();

            if (meta == null) continue;

            var marketplaceData = MarketplaceData.Data?.OwningAddons.Find(a => a.EntryId == meta.Id);


            if (marketplaceData == null)
            {
                //削除済みのEntryIdのアドオンは削除する
                File.Delete(path);
            }
            else if (marketplaceData.AutoUpdate)
            {
                //自動アップデートが必要な場合
                yield return FetchAndDownloadAddon(marketplaceData.ToAddonUrl, meta.Release, marketplaceData.EntryId, preprocessor.SetLoadingText("Update Addon - " + marketplaceData.Title).Do).WaitAsCoroutine();
            }

            existingEntry.Add(meta.Id);
        }

        

        foreach (var addon in MarketplaceData.Data!.OwningAddons.Where(a => !existingEntry.Contains(a.EntryId)))
        {
            yield return FetchAndDownloadAddon(addon.ToAddonUrl, null, addon.EntryId, preprocessor.SetLoadingText("Download Addon - " + addon.Title).Do).WaitAsCoroutine();
        }


        yield return preprocessor.SetLoadingText("Loading Addons");

        //組込アドオンの読み込み
        Assembly assembly = Assembly.GetExecutingAssembly();
        string prefix = "Nebula.Resources.Addons.";
        foreach (var file in assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix) && name.EndsWith(".zip")))
        {
            ZipArchive? zip = null;
            try
            {
                var stream = assembly.GetManifestResourceStream(file);
                if (stream == null) continue;
                zip = new ZipArchive(stream);

                var addon = new NebulaAddon(zip, file.Substring(prefix.Length)) { IsBuiltIn = true };
                allAddons.Add(addon.Id, addon);

                addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(assembly.GetManifestResourceStream(file)!)).ComputeConstantHash();
            }
            catch
            {
                zip?.Dispose();
            }
        }

        //外部アドオンの読み込み
        foreach (var file in Directory.GetFiles("Addons"))
        {
            var ext = Path.GetExtension(file);
            if (ext == null) continue;
            if (!ext.Equals(".zip") && !ext.Equals(".addon")) continue;

            var zip = ZipFile.OpenRead(file);

            try
            {
                var addon = new NebulaAddon(zip, file);
                allAddons.Add(addon.Id, addon);

                var marketplaceMeta = zip.GetEntry(".marketplace");
                if (marketplaceMeta == null)
                    addon.HandshakeHash = addon.HandshakeHash = System.BitConverter.ToString(md5.ComputeHash(File.OpenRead(file))).ComputeConstantHash();
                else
                {
                    using var mmStream = marketplaceMeta.Open();
                    var mMeta = JsonStructure.Deserialize<MarketplaceAddonMeta>(mmStream);
                    addon.HandshakeHash = mMeta?.Hash ?? 0;
                }
            }
            catch
            {
                zip.Dispose();
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.Addon, "Failed to load addon \"" + Path.GetFileName(file) + "\".");
            }
        }

        allOrderedAddons = [];

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

    private const string MetaFileName = "addon.meta";
    private NebulaAddon(ZipArchive zip, string path)
    {
        bool foundMetaFile = false;
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

            foundMetaFile = true;
            break;
        }
        if (!foundMetaFile) throw new Exception();
        
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
        innerAddress = (InZipPath + innerAddress).Replace('/', '.').ToLower();
        
        foreach (var entry in Archive.Entries)
        {
            if (entry.FullName.Replace('/', '.').ToLower() == innerAddress) return entry.Open();
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

