using AmongUs.Data;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Text.RegularExpressions;
using Innersloth.Assets;
using Nebula.Behaviour;
using PowerTools;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using UnityEngine.AddressableAssets;
using Virial.Game;
using Virial.Runtime;

namespace Nebula.Modules;

public class CustomItemGrouped
{
    public CustomItemBundle MyBundle = null!;
}

public abstract class CustomCosmicItem : CustomItemGrouped
{
    [JsonSerializableField]
    public string Name = "Undefined";
    [JsonSerializableField]
    public string Author = "Unknown";
    [JsonSerializableField]
    public string Package = "None";
    [JsonSerializableField(true)]
    public string? TranslationKey = null;

    [JsonSerializableField(true)]
    public string[]? Tags = null;

    public string UnescapedName => Regex.Unescape(Name).Replace('_', ' ');
    public string UnescapedAuthor => Regex.Unescape(Author).Replace('_', ' ');
    public static string GetEscapedString(string text) => Regex.Escape(text.Replace(' ', '_'));

    public virtual string Category { get => "Undefined"; }
    public bool IsValid { get; private set; } = true;
    public bool IsActive { get; private set; } = false;

    public IEnumerable<CosmicImage> AllImage() {

        foreach(var f in this.GetType().GetFields())
        {
            if (!f.FieldType.Equals(typeof(CosmicImage))) continue;
            var image = (CosmicImage?)f.GetValue(this);
            if (image != null) yield return image;
        }
    }

    public string SubholderPath => Author.ToByteString() + "/" + Name.ToByteString();

    public bool HasAnimation => AllImage().Any(image => image.GetLength() > 1);

    public async Task Preactivate()
    {
        string holder = SubholderPath;

        async Task CheckAndDownload(string? hash, string address)
        {
            var stream = MyBundle.OpenStream(Category + "/" + holder + "/" + address);
            string? existingHash = null;
            if (stream != null)
            {
                existingHash = CosmicImage.ComputeImageHash(stream);
                stream.Close();
            }
            if (MyBundle.RelatedRemoteAddress != null && (existingHash == null || !(hash?.Equals(existingHash) ?? true)))
            {
                //更新を要する場合
                await MyBundle.DownloadAsset(Category, holder, address);
            }
        }

        foreach (var image in AllImage())
        {
            if(image.Address != null) await CheckAndDownload(image.Hash, image.Address);
            if(image.ExAddress != null) await CheckAndDownload(image.ExHash, image.ExAddress);
        }
    }

    public virtual IEnumerator Activate(bool addToMoreCosmic = true)
    {
        string holder = SubholderPath;

        (UnloadTextureLoader.AsyncLoader loader, Action<Exception> exHandler) GetLoader(string? address) => 
            address == null ? (null!, _ => { }) :
            (MyBundle.GetTextureLoader(Category, SubholderPath, address), (ex) =>
            {
                string? message = null;
                if (ex is DirectoryNotFoundException)
                    message = "Missed Directory (" + ex.ToString() + ")";
                if (ex is FileNotFoundException)
                    message = "Missed File (" + ex.ToString() + ")";
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Warning, NebulaLog.LogCategory.MoreCosmic, "Failed to load images. ( \"" + address + "\" in \"" + Name + "\" )\nReason: " + (message ?? "Others (" + ex.ToString() + ")"));
            }
        );

        foreach (var image in AllImage())
        {
            var loader = GetLoader(image.Address);
            yield return loader.loader.LoadAsync(loader.exHandler);

            var exLoader = GetLoader(image.ExAddress);
            yield return exLoader.loader?.LoadAsync(exLoader.exHandler);

            try
            {
                if (!image.TryLoadImage(loader.loader?.Result!, exLoader.loader?.Result))
                {
                    IsValid = false;
                    break;
                }
            }
            catch 
            {
                IsValid = false;
                break;
            }
        }
        yield break;
    }


    public void Abandom()
    {

    }

    abstract public Sprite? PreviewSprite { get; }
    virtual public Sprite? PreviewAdditionalSprite { get => null; }
    virtual public bool PreviewAdditionalInFront { get => false; }
}

public class CosmicImage
{
    [JsonSerializableField(true)]
    public string? Hash = null;
    [JsonSerializableField]
    public string Address = "";

    [JsonSerializableField(true)]
    public string? ExHash = null;
    [JsonSerializableField(true)]
    public string? ExAddress = null;
    [JsonSerializableField]
    public bool ExIsFront = false;

    [JsonSerializableField(true)]
    public int? Length = null;

    [JsonSerializableField(true)]
    public int? X = null;
    [JsonSerializableField(true)]
    public int? Y = null;

    public int GetLength() => (X ?? Length ?? 1) * (Y ?? 1);

    public float PixelsPerUnit = 100f;
    public Vector2 Pivot = new Vector2(0.5f, 0.5f);

    public bool RequirePlayFirstState = false;
    private IDividedSpriteLoader? spriteLoader { get; set; }
    private IDividedSpriteLoader? exSpriteLoader { get; set; }

    public bool HasExImage => ExAddress != null;

    public bool TryLoadImage(ITextureLoader textureLoader, ITextureLoader? exTextureLoader)
    {
        int length = GetLength();
        this.spriteLoader = new DividedSpriteLoader(textureLoader, PixelsPerUnit, X ?? Length ?? 1, Y ?? 1) { Pivot = Pivot };
        for (int i = 0; i < length; i++) if (!spriteLoader.GetSprite(i)) return false;

        if (ExAddress != null)
        {
            this.exSpriteLoader = new DividedSpriteLoader(exTextureLoader!, PixelsPerUnit, X ?? Length ?? 1, Y ?? 1) { Pivot = Pivot };
            for (int i = 0; i < length; i++) if (!exSpriteLoader!.GetSprite(i)) return false;
        }

        return true;
    }

    public Sprite? GetSprite(int index)
    {
        return spriteLoader?.GetSprite(index) ?? null;
    }

    public Sprite? GetExSprite(int index)
    {
        return exSpriteLoader?.GetSprite(index) ?? null;
    }

    public static string ComputeImageHash(Stream stream)
    {
        return System.BitConverter.ToString(CustomItemBundle.MD5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}

public class CosmicHat : CustomCosmicItem
{
    [JsonSerializableField(true)]
    public CosmicImage? Main;
    [JsonSerializableField(true)]
    public CosmicImage? Flip;
    [JsonSerializableField(true)]
    public CosmicImage? Back;
    [JsonSerializableField(true)]
    public CosmicImage? BackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? Move;
    [JsonSerializableField(true)]
    public CosmicImage? MoveFlip;
    [JsonSerializableField(true)]
    public CosmicImage? MoveBack;
    [JsonSerializableField(true)]
    public CosmicImage? MoveBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? Climb;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbDown;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbDownFlip;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVent;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVent;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentFlip;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentBack;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentBack;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? Preview;
    [JsonSerializableField(true)]
    public bool Bounce = false;
    [JsonSerializableField(true)]
    public bool Adaptive = false;
    [JsonSerializableField(true)]
    public bool HideHands = false;
    [JsonSerializableField(true)]
    public bool IsSkinny = false;
    [JSFieldAmbiguous]
    public int FPS = 1;

    public HatData MyHat { get; private set; } = null!;
    public HatViewData MyView { get; private set; } = null!;
    public override IEnumerator Activate(bool addToMoreCosmic)
    {
        foreach (var image in AllImage()) {
            image.Pivot = new Vector2(0.53f, 0.575f);
            image.PixelsPerUnit = 112.875f;
        }

        yield return base.Activate(addToMoreCosmic);

        if (!IsValid) yield break;

        MyHat ??= ScriptableObject.CreateInstance<HatData>();
        MyHat.MarkDontUnload();
        if (MyView == null)
        {
            MyView = ScriptableObject.CreateInstance<HatViewData>();
            var assetRef = new AssetReference(MyView.Pointer);
            MyHat.ViewDataRef = assetRef;
            MyView.MarkDontUnload();
        }

        MyHat.name = UnescapedName + "\n<size=1.6>by " + UnescapedAuthor + "</size>";
        MyHat.displayOrder = 99;
        MyHat.ProductId = "noshat_" + Author + "_" + Name;
        MyHat.InFront = true;
        MyHat.NoBounce = !Bounce;
        MyHat.ChipOffset = new Vector2(0f, 0.2f);
        MyHat.Free = true;
        MyHat.PreviewCrewmateColor = Adaptive;

        MyView.MatchPlayerColor = Adaptive;

        MyHat.CreateAddressableAsset();

        if (EnterVent != null) EnterVent.RequirePlayFirstState = true;
        if (EnterVentBack != null) EnterVentBack.RequirePlayFirstState = true;
        if (EnterVentFlip != null) EnterVentFlip.RequirePlayFirstState = true;
        if (EnterVentBackFlip != null) EnterVentBackFlip.RequirePlayFirstState = true;
        if (ExitVent != null) ExitVent.RequirePlayFirstState = true;
        if (ExitVentBack != null) ExitVentBack.RequirePlayFirstState = true;
        if (ExitVentFlip != null) ExitVentFlip.RequirePlayFirstState = true;
        if (ExitVentBackFlip != null) ExitVentBackFlip.RequirePlayFirstState = true;
        if (Climb != null) Climb.RequirePlayFirstState = true;
        if (ClimbFlip != null) ClimbFlip.RequirePlayFirstState = true;
        if (ClimbDown != null) ClimbDown.RequirePlayFirstState = true;
        if (ClimbDownFlip != null) ClimbDownFlip.RequirePlayFirstState = true;

        if(addToMoreCosmic) MoreCosmic.AllHats.Add(MyHat.ProductId, this);        
    }

    public override string Category { get => "hats"; }

    public override Sprite? PreviewSprite => Preview?.GetSprite(0) ?? Main?.GetSprite(0) ?? Back?.GetSprite(0) ?? Move?.GetSprite(0);
    public override Sprite? PreviewAdditionalSprite => Preview?.GetExSprite(0);
    public override bool PreviewAdditionalInFront => Preview?.ExIsFront ?? false;
}

public class CosmicVisor : CustomCosmicItem
{
    [JsonSerializableField(true)]
    public CosmicImage? Main;
    [JsonSerializableField(true)]
    public CosmicImage? Flip;
    [JsonSerializableField(true)]
    public CosmicImage? Back;
    [JsonSerializableField(true)]
    public CosmicImage? BackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? Move;
    [JsonSerializableField(true)]
    public CosmicImage? MoveFlip;
    [JsonSerializableField(true)]
    public CosmicImage? MoveBack;
    [JsonSerializableField(true)]
    public CosmicImage? MoveBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVent;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentFlip;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentBack;
    [JsonSerializableField(true)]
    public CosmicImage? EnterVentBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVent;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentBack;
    [JsonSerializableField(true)]
    public CosmicImage? ExitVentBackFlip;
    [JsonSerializableField(true)]
    public CosmicImage? Preview;
    [JsonSerializableField(true)]
    public CosmicImage? Climb;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbFlip;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbDown;
    [JsonSerializableField(true)]
    public CosmicImage? ClimbDownFlip;
    [JsonSerializableField(true)]
    public bool Adaptive = false;
    [JsonSerializableField(true)]
    public bool BehindHat = false;
    [JsonSerializableField(true)]
    public bool BackmostBack = false;
    [JSFieldAmbiguous]
    public int FPS = 1;

    public VisorData MyVisor { get; private set; } = null!;
    public VisorViewData MyView { get; private set; } = null!;
    public bool HasClimbUpImage => Climb != null;
    public bool HasClimbDownImage => (ClimbDown ?? Climb) != null;

    public override IEnumerator Activate(bool addToMoreCosmic)
    {
        foreach (var image in AllImage())
        {
            image.Pivot = new Vector2(0.53f, 0.575f);
            image.PixelsPerUnit = 112.875f;
        }

        yield return base.Activate(addToMoreCosmic);

        if (!IsValid) yield break;

        MyVisor = ScriptableObject.CreateInstance<VisorData>();
        MyVisor.MarkDontUnload();
        if (MyView == null)
        {
            MyView = ScriptableObject.CreateInstance<VisorViewData>();
            MyView.MarkDontUnload();
            var assetRef = new AssetReference(MyView.Pointer);
            MyVisor.ViewDataRef = assetRef;
        }

        MyVisor.name = UnescapedName + "\n<size=1.6>by " + UnescapedAuthor + "</size>";
        MyVisor.displayOrder = 99;
        MyVisor.ProductId = "nosvisor_" + Author + "_" + Name;
        MyVisor.ChipOffset = new Vector2(0f, 0.2f);
        MyVisor.Free = true;
        MyVisor.PreviewCrewmateColor = Adaptive;
        //MyVisor.SpritePreview = Preview?.GetSprite(0) ?? Main?.GetSprite(0);

        //if (Adaptive) MyView.AltShader = MoreCosmic.AdaptiveShader;
        MyView.MatchPlayerColor = Adaptive;

        MyVisor.CreateAddressableAsset();

        if (EnterVent != null) EnterVent.RequirePlayFirstState = true;
        if (EnterVentFlip != null) EnterVentFlip.RequirePlayFirstState = true;
        if (EnterVentBack != null) EnterVentBack.RequirePlayFirstState = true;
        if (EnterVentBackFlip != null) EnterVentBackFlip.RequirePlayFirstState = true;
        if (ExitVent != null) ExitVent.RequirePlayFirstState = true;
        if (ExitVentFlip != null) ExitVentFlip.RequirePlayFirstState = true;
        if (ExitVentBack != null) ExitVentBack.RequirePlayFirstState = true;
        if (ExitVentBackFlip != null) ExitVentBackFlip.RequirePlayFirstState = true;
        if (Climb != null) Climb.RequirePlayFirstState = true;
        if (ClimbFlip != null) ClimbFlip.RequirePlayFirstState = true;
        if (ClimbDown != null) ClimbDown.RequirePlayFirstState = true;
        if (ClimbDownFlip != null) ClimbDownFlip.RequirePlayFirstState = true;

        if (addToMoreCosmic) MoreCosmic.AllVisors.Add(MyVisor.ProductId, this);
    }
    public override string Category { get => "visors"; }

    public override Sprite? PreviewSprite => Preview?.GetSprite(0) ?? Main?.GetSprite(0);
    public override Sprite? PreviewAdditionalSprite => Preview?.GetExSprite(0);
    public override bool PreviewAdditionalInFront => Preview?.ExIsFront ?? false;
}

public class CosmicNameplate : CustomCosmicItem
{
    [JsonSerializableField(true)]
    public CosmicImage? Plate;
    [JsonSerializableField(true)]
    public CosmicImage? Adaptive;
    [JsonSerializableField(true)]
    public bool AdaptiveInFront = true;

    public NamePlateData MyPlate { get; private set; } = null!;
    public NamePlateViewData MyView { get; private set; } = null!;
    public override IEnumerator Activate(bool addToMoreCosmic)
    {
        yield return base.Activate(addToMoreCosmic);
        if (!IsValid) yield break;

        MyPlate = ScriptableObject.CreateInstance<NamePlateData>();
        MyPlate.MarkDontUnload();
        if (MyView == null)
        {
            MyView = ScriptableObject.CreateInstance<NamePlateViewData>();
            MyView.MarkDontUnload(); 
            var assetRef = new AssetReference(MyView.Pointer);
            MyPlate.ViewDataRef = assetRef;
        }

        MyView.Image = Plate?.GetSprite(0);

        MyPlate.name = UnescapedName + "\n<size=1.6>by " + UnescapedAuthor + "</size>";
        MyPlate.displayOrder = 99;
        MyPlate.ProductId = "nosplate_" + Author + "_" + Name;
        MyPlate.ChipOffset = new Vector2(0f, 0.2f);
        MyPlate.Free = true;
        //MyPlate.SpritePreview = Plate?.GetSprite(0);

        MyPlate.CreateAddressableAsset();

        if (addToMoreCosmic) MoreCosmic.AllNameplates.Add(MyPlate.ProductId, this);
    }
    public override string Category { get => "nameplates"; }

    public override Sprite? PreviewSprite => Plate?.GetSprite(0);
}

public class CosmicPackage : CustomItemGrouped
{
    [JsonSerializableField]
    public string Package = "None";
    [JsonSerializableField]
    public string Format = "Custom Package";
    [JsonSerializableField(true)]
    public string? TranslationKey = null;
    [JsonSerializableField]
    public int Priority = 1;

    public string DisplayName => Language.Find(TranslationKey) ?? Format;
}

public class CustomItemBundle
{
    static public MD5 MD5 = MD5.Create();

    static Dictionary<string, CustomItemBundle> AllBundles = new();

    [JSFieldAmbiguous]
    public string? BundleName = null;

    [JSFieldAmbiguous]
    public List<CosmicHat> Hats = new();
    [JSFieldAmbiguous]
    public List<CosmicVisor> Visors = new();
    [JSFieldAmbiguous]
    public List<CosmicNameplate> Nameplates = new();
    [JSFieldAmbiguous]
    public List<CosmicPackage> Packages = new();
    
    public string? RelatedLocalAddress { get; set; } = null;
    public string? RelatedRemoteAddress { get; set; } = null;
    public ZipArchive? RelatedZip { get; private set; } = null;

    public bool IsActive { get;private set; } = false;

    private IEnumerable<CustomCosmicItem> AllCosmicItem()
    {
        foreach (var item in Hats) yield return item;
        foreach (var item in Visors) yield return item;
        foreach (var item in Nameplates) yield return item;
    }

    private IEnumerable<CustomItemGrouped> AllContents()
    {
        foreach (var item in AllCosmicItem()) yield return item;
        foreach (var item in Packages) yield return item;
    }
    public async Task Load()
    {
        if (IsActive) return;

        if (RelatedLocalAddress != null && !RelatedLocalAddress.EndsWith("/")) RelatedLocalAddress += "/";
        if (RelatedRemoteAddress != null && !RelatedRemoteAddress.EndsWith("/")) RelatedRemoteAddress += "/";

        foreach (var item in AllContents()) item.MyBundle = this;
        foreach (var item in AllCosmicItem()) await item.Preactivate();

        if (AllBundles.ContainsKey(BundleName!)) throw new Exception("Duplicated Bundle Error");
    }

    public IEnumerator Activate(bool addToMoreCosmic)
    {
        IsActive = true;
        if(addToMoreCosmic) AllBundles[BundleName!] = this;

        foreach (var item in AllCosmicItem())
        {
            item.MyBundle = this;

            yield return item.Activate(addToMoreCosmic);
        }

        if (addToMoreCosmic)
        {
            foreach (var package in Packages) MoreCosmic.AllPackages.Add(package.Package, package);

            var hatList = HatManager.Instance.allHats.ToList();
            foreach (var item in Hats) if (item.IsValid) hatList.Add(item.MyHat);
            HatManager.Instance.allHats = hatList.ToArray();

            var visorList = HatManager.Instance.allVisors.ToList();
            foreach (var item in Visors) if (item.IsValid) visorList.Add(item.MyVisor);
            HatManager.Instance.allVisors = visorList.ToArray();

            var nameplateList = HatManager.Instance.allNamePlates.ToList();
            foreach (var item in Nameplates) if (item.IsValid) nameplateList.Add(item.MyPlate);
            HatManager.Instance.allNamePlates = nameplateList.ToArray();
        }
    }

    public Stream? OpenStream(string path)
    {
        if (RelatedZip != null && RelatedLocalAddress != null)
        {
            string address = RelatedLocalAddress + path;
            return RelatedZip.GetEntry(address)?.Open();
        }
        if (RelatedLocalAddress != null)
        {
            string address = RelatedLocalAddress + path;
            if (!File.Exists(address)) return null;
            return File.OpenRead(address);
        }
        return null;
    }

    public async Task DownloadAsset(string category,string localHolder,string address)
    {
        //リモートリポジトリやローカルの配置先が無い場合はダウンロードできない
        if (RelatedRemoteAddress == null || RelatedLocalAddress == null) return;

        var hatFileResponse = await NebulaPlugin.HttpClient.GetAsync(RelatedRemoteAddress + category + "/" + address, HttpCompletionOption.ResponseContentRead);
        if (hatFileResponse.StatusCode != HttpStatusCode.OK) return;

        var responseStream = await hatFileResponse.Content.ReadAsByteArrayAsync();
        //サブディレクトリまでを作っておく
        string localPath = RelatedLocalAddress + category + "/" + localHolder + "/" + address;

        var dir = Path.GetDirectoryName(localPath);
        if(dir!=null)Directory.CreateDirectory(dir);

        using var fileStream = File.Create(localPath);

        await fileStream.WriteAsync(responseStream);
        
        if (ClientOption.AllOptions[ClientOption.ClientOptionType.OutputCosmicHash].Value == 1)
        {
            string hash = System.BitConverter.ToString(CustomItemBundle.MD5.ComputeHash(responseStream)).Replace("-", "").ToLowerInvariant();

            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic,$"Hash: {hash} ({category}/{address})");
        }

    }

    public UnloadTextureLoader.AsyncLoader GetTextureLoader(string category,string subholder,string address)
    {
        if (RelatedZip != null)
            return new UnloadTextureLoader.AsyncLoader(() => RelatedZip.GetEntry(RelatedLocalAddress + category + "/" + address)?.Open());
        else if (RelatedRemoteAddress == null)
            return new UnloadTextureLoader.AsyncLoader(() => {
                var data = File.ReadAllBytes(RelatedLocalAddress + category + "/" + address);
                return new MemoryStream(data);
            }, true);
        else
            return new UnloadTextureLoader.AsyncLoader(() => File.OpenRead(RelatedLocalAddress + category + "/" + subholder + "/" + address));
    }

    static public async Task<CustomItemBundle?> LoadOnline(string url)
    {
        NebulaPlugin.HttpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        var response = await NebulaPlugin.HttpClient.GetAsync(new System.Uri($"{url}/Contents.json"), HttpCompletionOption.ResponseContentRead);
        if (response.StatusCode != HttpStatusCode.OK) return null;

        using StreamReader stream = new(await response.Content.ReadAsStreamAsync(),Encoding.UTF8);
        string json = stream.ReadToEnd();
        CustomItemBundle? bundle = (CustomItemBundle?)JsonStructure.Deserialize(json, typeof(CustomItemBundle));
        
        if (bundle == null) return null;

        bundle.RelatedRemoteAddress = url;
        bundle.RelatedLocalAddress = "MoreCosmic/";
        if (bundle.BundleName == null) bundle.BundleName = url;
        await bundle.Load();

        return bundle;
    }

    static public async Task<CustomItemBundle?> LoadOffline(NebulaAddon addon)
    {
        using var stream = addon.OpenStream("MoreCosmic/Contents.json");

        if (stream == null) return null;

        string json = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        CustomItemBundle? bundle = (CustomItemBundle?)JsonStructure.Deserialize(json, typeof(CustomItemBundle));

        if (bundle == null) return null;

        bundle.RelatedRemoteAddress = null;
        bundle.RelatedLocalAddress = addon.InZipPath + "MoreCosmic/";
        bundle.RelatedZip = addon.Archive;
        if (bundle.BundleName == null) bundle.BundleName = addon.AddonName;

        await bundle.Load();

        return bundle;
    }
}

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
public static class MoreCosmic
{
    public static Dictionary<string, CosmicHat> AllHats = new();
    public static Dictionary<string, CosmicVisor> AllVisors = new();
    public static Dictionary<string, CosmicNameplate> AllNameplates = new();
    public static Dictionary<string, CosmicPackage> AllPackages = new();

    internal static string DebugProductId = "NEBULA_DEBUG";
    public static void RegisterDebugHat(CosmicHat hat)
    {
        hat.MyHat.ProductId = DebugProductId;
        AllHats[DebugProductId] = hat;
    }

    public static void RegisterDebugVisor(CosmicVisor visor)
    {
        visor.MyVisor.ProductId = DebugProductId;
        AllVisors[DebugProductId] = visor;
    }

    private static Material? adaptiveShader = null;
    public static Material AdaptiveShader { get {
            if (adaptiveShader == null) adaptiveShader = HatManager.Instance.PlayerMaterial;
            return adaptiveShader;
        } }

    private static bool isLoaded = false;
    private static List<CustomItemBundle?> loadedBundles = new();

    private static async Task LoadLocal()
    {
        foreach(var addon in NebulaAddon.AllAddons)
        {
            var bundle = await CustomItemBundle.LoadOffline(addon);

            lock (loadedBundles)
            {
                loadedBundles.Add(bundle);
            }
        }
    }

    private static List<string> allRepos = new();
    private static async Task LoadOnline()
    {
        var response = await NebulaPlugin.HttpClient.GetAsync(new System.Uri("https://raw.githubusercontent.com/Dolly1016/MoreCosmic/master/UserCosmics.dat"), HttpCompletionOption.ResponseContentRead);
        if (response.StatusCode != HttpStatusCode.OK) return;

        string repos = await response.Content.ReadAsStringAsync();

        while (!HatManager.InstanceExists) await Task.Delay(1000);

        allRepos.AddRange(repos.Split("\n").Concat(MarketplaceData.Data?.OwningCostumes.Select(c => c.ToCostumeUrl) ?? []).Where(url => url.Length > 3));
        foreach (string repo in allRepos.ToArray())
        {
            try
            {
                var result = await Modules.CustomItemBundle.LoadOnline(repo);

                lock (loadedBundles)
                {
                    loadedBundles.Add(result);
                }
            }
            catch { }
        }
    }

    public static async Task LoadOnlineExtra(string url)
    {
        if (allRepos.Contains(url)) return;
        allRepos.Add(url);

        try
        {
            var result = await Modules.CustomItemBundle.LoadOnline(url);

            lock (loadedBundles)
            {
                loadedBundles.Add(result);
            }
        }
        catch (Exception e){
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.MoreCosmic, "Error is occurred while loading costumes.\n" + e.ToString());
        }
    }



    static private Queue<StackfullCoroutine> ActivateQueue = new Queue<StackfullCoroutine>();
    
    public static void Update()
    {
        if (!HatManager.InstanceExists) return;
        if (!EOSManager.InstanceExists) return;
        if (!EOSManager.Instance.HasFinishedLoginFlow()) return;

        lock (loadedBundles)
        {
            if (loadedBundles.Count > 0)
            {
                foreach (var bundle in loadedBundles) if(bundle != null) ActivateQueue.Enqueue(new(bundle!.Activate(true)));
                loadedBundles.Clear();
            }
        }

        if (ActivateQueue.Count > 0)
        {
            var current = ActivateQueue.Peek();
            if (!(current?.MoveNext() ?? false)) ActivateQueue.Dequeue();
        }
    }

    private static async Task LoadAll()
    {
        await LoadLocal();
        await LoadOnline();
    }

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        if (isLoaded) return;

        var detached = LoadAll();

        isLoaded = true;
    }
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public static class WrappedAddressableAssetLoader
{
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        try
        {
            ClassInjector.RegisterTypeInIl2Cpp<WrappedHatAsset>();
            ClassInjector.RegisterTypeInIl2Cpp<WrappedVisorAsset>();
            ClassInjector.RegisterTypeInIl2Cpp<WrappedNamePlateAsset>();
        }catch(Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }
}

public class WrappedHatAsset : AddressableAsset<HatViewData> {
    public HatViewData? viewData = null;
    public WrappedHatAsset(System.IntPtr ptr) : base(ptr) { }
    public WrappedHatAsset() : base(ClassInjector.DerivedConstructorPointer<WrappedHatAsset>())
    { ClassInjector.DerivedConstructorBody(this); }
    public override HatViewData GetAsset() => viewData!;
    public override void LoadAsync(Il2CppSystem.Action? onSuccessCb = null, Il2CppSystem.Action? onErrorcb = null, Il2CppSystem.Action? onFinishedcb = null)
    {
        if (onSuccessCb != null) onSuccessCb.Invoke();
        if (onFinishedcb != null) onFinishedcb.Invoke();
    }
    public override void Unload() { }
    public override void Destroy() { }
    public override AssetLoadState GetState() => AssetLoadState.Success;
}
public class WrappedVisorAsset : AddressableAsset<VisorViewData> {
    public VisorViewData viewData = null!;
    public WrappedVisorAsset(System.IntPtr ptr) : base(ptr) { }
    public WrappedVisorAsset() : base(ClassInjector.DerivedConstructorPointer<WrappedVisorAsset>())
    { ClassInjector.DerivedConstructorBody(this); }
    public override VisorViewData GetAsset() => viewData;
    public override void LoadAsync(Il2CppSystem.Action? onSuccessCb = null, Il2CppSystem.Action? onErrorcb = null, Il2CppSystem.Action? onFinishedcb = null)
    {
        if (onSuccessCb != null) onSuccessCb.Invoke();
        if (onFinishedcb != null) onFinishedcb.Invoke();
    }
    public override void Unload() { }
    public override void Destroy() { }
    public override AssetLoadState GetState() => AssetLoadState.Success;
}
public class WrappedNamePlateAsset : AddressableAsset<NamePlateViewData> {
    public NamePlateViewData viewData = null!;
    public WrappedNamePlateAsset(System.IntPtr ptr) : base(ptr) { }
    public WrappedNamePlateAsset() : base(ClassInjector.DerivedConstructorPointer<WrappedNamePlateAsset>())
    { ClassInjector.DerivedConstructorBody(this); }
    public override NamePlateViewData GetAsset() => viewData;
    public override void LoadAsync(Il2CppSystem.Action? onSuccessCb = null, Il2CppSystem.Action? onErrorcb = null, Il2CppSystem.Action? onFinishedcb = null)
    {
        if (onSuccessCb != null) onSuccessCb.Invoke();
        if (onFinishedcb != null) onFinishedcb.Invoke();
    }
    public override void Unload() { }
    public override void Destroy() { }
    public override AssetLoadState GetState() => AssetLoadState.Success;
}


[HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetHat))]
public class CosmeticsCacheGetHatPatch
{
    public static bool Prefix(string id, ref HatViewData __result)
    {
        if (MoreCosmic.AllHats.TryGetValue(id,out var hat))
        {
            __result = hat.MyView;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetVisor))]
public class CosmeticsCacheGetVisorPatch
{
    public static bool Prefix(string id, ref VisorViewData __result)
    {
        if (MoreCosmic.AllVisors.TryGetValue(id, out var visor))
        {
            __result = visor.MyView;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.GetNameplate))]
public class CosmeticsCacheGetNameplatePatch
{
    public static bool Prefix(string id, ref NamePlateViewData __result)
    {
        if (MoreCosmic.AllNameplates.TryGetValue(id, out var plate))
        {
            __result = plate.MyView;
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(CosmeticData), nameof(CosmeticData.GetItemName))]
public class ItemNamePatch
{
    public static bool Prefix(CosmeticData __instance, ref string __result)
    {
        CustomCosmicItem? item = null;
        if (__instance.TryCast<HatData>())
        {
            if (!MoreCosmic.AllHats.TryGetValue(__instance.ProductId, out var val)) return true;
            item = val;
        }
        else if (__instance.TryCast<VisorData>())
        {
            if (!MoreCosmic.AllVisors.TryGetValue(__instance.ProductId, out var val)) return true;
            item = val;
        }
        else if (__instance.TryCast<NamePlateData>())
        {
            if (!MoreCosmic.AllNameplates.TryGetValue(__instance.ProductId, out var val)) return true;
            item = val;
        }
        else
        {
            return true;
        }

        if (item == null) return true;

        string? name = Language.Find(item.TranslationKey);
        if (name == null) return true;

        __result = name + "\n<size=1.6>by " + item.UnescapedAuthor + "</size>";
        return false;
    }
}

[HarmonyPatch(typeof(HatData), nameof(HatData.CreateAddressableAsset))]
public class HatAssetPatch
{
    public static bool Prefix(HatData __instance, ref AddressableAsset<HatViewData> __result)
    {
        if (!MoreCosmic.AllHats.TryGetValue(__instance.ProductId, out var value)) return true;
        var asset =  new WrappedHatAsset(); 
        asset.viewData = value.MyView;
        __result = asset.Cast<AddressableAsset<HatViewData>>();
        return false;
    }
}

[HarmonyPatch(typeof(VisorData), nameof(VisorData.CreateAddressableAsset))]
public class VisorAssetPatch
{
    public static bool Prefix(VisorData __instance, ref AddressableAsset<VisorViewData> __result)
    {
        if (!MoreCosmic.AllVisors.TryGetValue(__instance.ProductId, out var value)) return true;
        var asset = new WrappedVisorAsset();
        asset.viewData = value.MyView;
        __result = asset.Cast<AddressableAsset<VisorViewData>>();
        return false;
    }
}

[HarmonyPatch(typeof(NamePlateData), nameof(NamePlateData.CreateAddressableAsset))]
public class NameplateAssetPatch
{
    public static bool Prefix(NamePlateData __instance, ref AddressableAsset<NamePlateViewData> __result)
    {
        if (!MoreCosmic.AllNameplates.TryGetValue(__instance.ProductId, out var value)) return true;
        var asset = new WrappedNamePlateAsset();
        asset.viewData = value.MyView;
        __result = asset.Cast<AddressableAsset<NamePlateViewData>>();
        return false;
    }
}

[HarmonyPatch(typeof(HatParent), nameof(HatParent.LateUpdate))]
public class HatLateUpdatePatch
{
    public static bool Prefix(HatParent __instance)
    {
        try
        {
            if (!__instance.Hat) return true;
            return !MoreCosmic.AllHats.ContainsKey(__instance.Hat.ProductId);
        }catch { return true; }
    }
}

[HarmonyPatch(typeof(HatParent), nameof(HatParent.SetHat),  typeof(int) )]
public class SetHatPatch
{
    public static bool Prefix(HatParent __instance, [HarmonyArgument(0)] int color)
    {
        if (!__instance.Hat || !MoreCosmic.AllHats.TryGetValue(__instance.Hat.ProductId, out var value)) return true;

        __instance.SetMaterialColor(color);
        __instance.UnloadAsset();

        var asset = new WrappedHatAsset();
        asset.viewData = value.MyView;

        __instance.viewAsset = asset;
        __instance.LoadAssetAsync(__instance.viewAsset, (Il2CppSystem.Action)(() =>
        {
            __instance.PopulateFromViewData();
        }), null);
        return false;
    }
}


[HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetVisor), new Type[] { typeof(VisorData), typeof(int) })]
public class SetVisorPatch
{
    public static bool Prefix(VisorLayer __instance,[HarmonyArgument(0)] VisorData data,[HarmonyArgument(1)] int color)
    {
        if (!MoreCosmic.AllVisors.TryGetValue(data.ProductId, out var value)) return true;

        if (data == null || data != __instance.visorData)__instance.Image.sprite = null;

        __instance.visorData = data;
        __instance.SetMaterialColor(color);
        __instance.UnloadAsset();
        var asset = new WrappedVisorAsset();
        asset.viewData = value.MyView;

        __instance.viewAsset = asset;
        __instance.LoadAssetAsync(__instance.viewAsset, (Il2CppSystem.Action)(()=>
        {
            __instance.PopulateFromViewData();
        }), null);
        return false;
    }
}

[HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.UpdateMaterial))]
public class SetVisorMaterialPatch
{
    public static void Postfix(VisorLayer __instance)
    {
        if (__instance.transform.parent.TryGetComponent<NebulaCosmeticsLayer>(out var layer))
        {
            try
            {
                PlayerMaterial.SetColors(__instance.matProperties.ColorId, layer.VisorBackRenderer);
                if (__instance.matProperties.MaskLayer <= 0)
                    PlayerMaterial.SetMaskLayerBasedOnLocalPlayer(layer.VisorBackRenderer, __instance.matProperties.IsLocalPlayer);
            }catch(Exception e) { }
        }
    }
}


[HarmonyPatch(typeof(VisorData), nameof(VisorData.CoLoadIcon))]
public class CoLoadIconPatch
{
    public static bool Prefix(VisorData __instance, ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)] Il2CppSystem.Action<Sprite, AddressableAsset> onLoaded)
    {
        if (!MoreCosmic.AllVisors.TryGetValue(__instance.ProductId, out var value)) return true;

        IEnumerator GetEnumerator()
        {
            var asset = new WrappedVisorAsset();
            asset.viewData = value.MyView;
            yield return asset.CoLoadAsync(null);
            VisorViewData viewData = asset.GetAsset();
            Sprite? sprite = ((viewData != null) ? viewData.IdleFrame : null);
            onLoaded.Invoke(sprite!, asset);
            yield break;
        }
        __result = GetEnumerator().WrapToIl2Cpp();
        return false;
    }
}

[HarmonyPatch(typeof(CosmeticsCache), nameof(CosmeticsCache.CoAddVisor))]
public class CoAddVisorPatch
{
    public static bool Prefix(CosmeticsCache __instance, ref Il2CppSystem.Collections.IEnumerator __result,[HarmonyArgument(0)] string visorId)
    {
        if (!MoreCosmic.AllVisors.TryGetValue(visorId, out var value)) return true;

        IEnumerator GetEnumerator()
        {
            var asset = new WrappedVisorAsset();
            asset.viewData = value.MyView;
            __instance.allCachedAssets.Add(asset.Cast<AddressableAsset<VisorViewData>>());
            yield return asset.CoLoadAsync(null);
            __instance.visors[visorId] = asset;

            asset = null;

            yield break;
        }
        __result = GetEnumerator().WrapToIl2Cpp();
        return false;
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.StartClimb))]
public class ClimbVisorPatch
{
    private static void Postfix(PlayerPhysics __instance, [HarmonyArgument(0)] bool down)
    {
        
        try
        {
            if (!MoreCosmic.AllVisors.TryGetValue(__instance.myPlayer.cosmetics.visor.visorData.ProductId, out var value)) return;

            if(down ? value.HasClimbDownImage : value.HasClimbUpImage) __instance.myPlayer.cosmetics.ToggleVisor(true);
        }
        catch { }
        
        __instance.myPlayer.cosmetics.ToggleVisor(true);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ResetAnimState))]
public static class HatFixPatch
{
    public static void Postfix(PlayerPhysics __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted) return;

        try
        {
            __instance.myPlayer.cosmetics.SetHatAndVisorIdle(__instance.myPlayer.GetModInfo()!.Unbox().CurrentOutfit.ColorId);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(AirshipExileController), nameof(AirshipExileController.PlayerFall))]
public static class ExiledPlayerHideHandsPatch
{
    public static void Postfix(AirshipExileController __instance)
    {
        HatParent hp = __instance.Player.cosmetics.hat;
        if (hp.Hat == null) return;

        if (MoreCosmic.AllHats.TryGetValue(hp.Hat.ProductId, out var modHat))
            if (modHat.HideHands) __instance.Player.gameObject.transform.FindChild("HandSlot").gameObject.SetActive(false);
    }
}

[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.EnsureInitialized))]
public class NebulaCosmeticsLayerPatch
{
    private static void Postfix(CosmeticsLayer __instance)
    {
        if (!__instance.gameObject.TryGetComponent<NebulaCosmeticsLayer>(out var c))__instance.gameObject.AddComponent<NebulaCosmeticsLayer>();
    }
}

public enum PlayerAnimState
{
    Idle,
    Run,
    ClimbUp,
    ClimbDown,
    EnterVent,
    ExitVent
}

public class NebulaNameplate : MonoBehaviour
{
    public SpriteRenderer AdaptiveRenderer;

    static NebulaNameplate()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaNameplate>();
    }

    public void Awake()
    {
        var voteArea = GetComponent<PlayerVoteArea>();
        AdaptiveRenderer = GameObject.Instantiate(voteArea.Background, voteArea.Background.transform);
        if (MeetingHud.Instance)
        {
            //ゲーム内だとマスク不要
            AdaptiveRenderer.material = HatManager.Instance.PlayerMaterial;
            AdaptiveRenderer.maskInteraction = SpriteMaskInteraction.None;
        }
        else
        {
            //ゲーム外だとマスク
            AdaptiveRenderer.material = HatManager.Instance.MaskedPlayerMaterial;
            AdaptiveRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
        AdaptiveRenderer.sprite = null;

        UpdateColor();
    }

    public void UpdateColor()
    {
        var voteArea = GetComponent<PlayerVoteArea>();
        PlayerMaterial.SetColors(voteArea.TargetPlayerId, AdaptiveRenderer);
    }
}
public class NebulaCosmeticsLayer : MonoBehaviour
{
    public CosmeticsLayer MyLayer = null!;
    public PlayerAnimations? MyAnimations = null!;

    public HatData? CurrentHat;
    public VisorData? CurrentVisor;
    public CosmicHat? CurrentModHat;
    public CosmicVisor? CurrentModVisor;

    public float HatTimer = 0f;
    public int HatFrontIndex = 0;
    public int HatBackIndex = 0;

    public float VisorTimer = 0f;
    public int VisorIndex = 0;
    public int VisorBackIndex = 0;

    private CosmicImage? lastHatFrontImage = null;
    private CosmicImage? lastHatBackImage = null;
    private CosmicImage? lastVisorImage = null;
    private CosmicImage? lastVisorBackImage = null;

    private SpriteRenderer hatFrontExRenderer;
    private SpriteRenderer hatBackExRenderer;
    private SpriteRenderer visorFrontExRenderer;
    private SpriteRenderer visorBackRenderer;
    private SpriteRenderer visorBackExRenderer;
    public SpriteRenderer VisorBackRenderer => visorBackRenderer;
    public IEnumerable<SpriteRenderer> AdditionalRenderers()
    {
        foreach (var r in AdditionalHatRenderers()) yield return r;
        foreach (var r in AdditionalVisorRenderers()) yield return r;
    }
    public IEnumerable<SpriteRenderer> AdditionalHatRenderers()
    {
        yield return hatFrontExRenderer;
        yield return hatBackExRenderer;
    }
    public IEnumerable<SpriteRenderer> AdditionalVisorRenderers()
    {
        yield return visorBackRenderer;
        yield return visorFrontExRenderer;
        yield return visorBackExRenderer;
    }
    private bool useDefaultShader = true;//追加したRendererのマテリアルを変更する必要があるか否か調べるために使用
    private bool usePlayerShaderOnVisor = false;//追加したRendererのマテリアルを変更する必要があるか否か調べるために使用

    static NebulaCosmeticsLayer()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaCosmeticsLayer>();
    }

    public void Awake()
    {
        MyLayer = gameObject.GetComponent<CosmeticsLayer>();

        //PoolablePlayer相手には取得できない
        try
        {
            MyAnimations = transform.parent.GetComponentInChildren<PlayerAnimations>();

            hatFrontExRenderer = GameObject.Instantiate(MyLayer.hat.FrontLayer, MyLayer.hat.FrontLayer.transform);
            hatFrontExRenderer.transform.localPosition = new(0f, 0f, 0f);
            hatFrontExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

            hatBackExRenderer = GameObject.Instantiate(MyLayer.hat.BackLayer, MyLayer.hat.BackLayer.transform);
            hatBackExRenderer.transform.localPosition = new(0f, 0f, 0f);
            hatBackExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

            visorBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("Back", MyLayer.visor.Image.transform, new(0f, 0f, 1f));
            visorBackRenderer.size = MyLayer.visor.Image.size;
            visorBackRenderer.transform.localPosition = new(0f, 0f, 0f);
            visorBackRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

            visorFrontExRenderer = GameObject.Instantiate(visorBackRenderer, MyLayer.visor.Image.transform);
            visorFrontExRenderer.transform.localPosition = new(0f, 0f, 0f);
            visorFrontExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

            visorBackExRenderer = GameObject.Instantiate(visorBackRenderer, visorBackRenderer.transform);
            visorBackExRenderer.transform.localPosition = new(0f, 0f, 0f);
            visorBackExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

            useDefaultShader = true;
            usePlayerShaderOnVisor = false;
        }
        catch { }
    }

    public void LateUpdate()
    {
        try
        {

            if (MyLayer.hat != null && CurrentHat != MyLayer.hat.Hat)
            {
                CurrentHat = MyLayer.hat.Hat;
                MoreCosmic.AllHats.TryGetValue(MyLayer.hat.Hat.ProductId, out CurrentModHat);
                HatFrontIndex = HatBackIndex = 0;
            }
            if (MyLayer.visor != null && CurrentVisor != MyLayer.visor.visorData)
            {
                CurrentVisor = MyLayer.visor.visorData;
                MoreCosmic.AllVisors.TryGetValue(MyLayer.visor.visorData.ProductId, out CurrentModVisor);
                VisorIndex = VisorBackIndex = 0;
            }

            PlayerAnimState animState = PlayerAnimState.Idle;
            bool flip = MyLayer.FlipX;

            if (MyAnimations)
            {
                var current = MyAnimations!.Animator.m_currAnim;
                if (current == MyAnimations!.group.ClimbUpAnim)
                    animState = PlayerAnimState.ClimbUp;
                else if (current == MyAnimations!.group.ClimbDownAnim)
                    animState = PlayerAnimState.ClimbDown;
                else if (current == MyAnimations!.group.RunAnim)
                    animState = PlayerAnimState.Run;
                else if (current == MyAnimations!.group.EnterVentAnim)
                    animState = PlayerAnimState.EnterVent;
                else if (current == MyAnimations!.group.ExitVentAnim)
                    animState = PlayerAnimState.ExitVent;
            }

            void SetImage(ref CosmicImage? current, CosmicImage? normal, CosmicImage? flipped)
            {
                current = (flip ? flipped : normal) ?? normal ?? flipped ?? current;
            }

            if (CurrentModHat != null)
            {
                //タイマーの更新
                HatTimer -= Time.deltaTime;
                if (HatTimer < 0f)
                {
                    HatTimer = 1f / (float)CurrentModHat.FPS;
                    HatBackIndex++;
                    HatFrontIndex++;
                }

                //表示する画像の選定
                CosmicImage? frontImage = null;
                CosmicImage? backImage = null;
                if (animState is not PlayerAnimState.ClimbUp and not PlayerAnimState.ClimbDown)
                {
                    SetImage(ref frontImage, CurrentModHat.Main, CurrentModHat.Flip);
                    SetImage(ref backImage, CurrentModHat.Back, CurrentModHat.BackFlip);
                }

                switch (animState)
                {
                    case PlayerAnimState.Run:
                        SetImage(ref frontImage, CurrentModHat.Move, CurrentModHat.MoveFlip);
                        SetImage(ref backImage, CurrentModHat.MoveBack, CurrentModHat.MoveBackFlip);
                        break;
                    case PlayerAnimState.ClimbUp:
                        SetImage(ref frontImage, CurrentModHat.Climb, CurrentModHat.ClimbFlip);
                        backImage = null;
                        break;
                    case PlayerAnimState.ClimbDown:
                        SetImage(ref frontImage, CurrentModHat.Climb, CurrentModHat.ClimbFlip);
                        SetImage(ref frontImage, CurrentModHat.ClimbDown, CurrentModHat.ClimbDownFlip);
                        
                        SpriteAnimNodeSync? spriteAnimNodeSync = MyLayer.hat?.SpriteSyncNode ?? MyLayer.hat?.GetComponent<SpriteAnimNodeSync>();
                        if (spriteAnimNodeSync) spriteAnimNodeSync!.NodeId = 0;
                        
                        backImage = null;
                        break;
                    case PlayerAnimState.EnterVent:
                        SetImage(ref frontImage, CurrentModHat.EnterVent, CurrentModHat.EnterVentFlip);
                        SetImage(ref backImage, CurrentModHat.EnterVentBack, CurrentModHat.EnterVentBackFlip);
                        break;
                    case PlayerAnimState.ExitVent:
                        SetImage(ref frontImage, CurrentModHat.ExitVent, CurrentModHat.ExitVentFlip);
                        SetImage(ref backImage, CurrentModHat.ExitVentBack, CurrentModHat.ExitVentBackFlip);
                        break;
                }

                //インデックスの調整
                HatFrontIndex %= frontImage?.GetLength() ?? 1;
                HatBackIndex %= backImage?.GetLength() ?? 1;
                if (lastHatFrontImage != frontImage && (frontImage?.RequirePlayFirstState ?? true)) HatFrontIndex = 0;
                if (lastHatBackImage != backImage && (backImage?.RequirePlayFirstState ?? true)) HatBackIndex = 0;
                lastHatFrontImage = frontImage;
                lastHatBackImage = backImage;

                MyLayer.hat!.FrontLayer.sprite = frontImage?.GetSprite(HatFrontIndex) ?? null;
                MyLayer.hat!.BackLayer.sprite = backImage?.GetSprite(HatBackIndex) ?? null;

                MyLayer.hat!.FrontLayer.enabled = true;
                MyLayer.hat!.BackLayer.enabled = true;

                MyLayer.hat!.FrontLayer.transform.SetLocalZ(MyLayer.zIndexSpacing * (CurrentModHat.IsSkinny ? -1f : -3f));

                AdditionalRenderers().Do(r => r.flipX = MyLayer.FlipX);

                //追加レイヤー
                var frontHasExImage = frontImage?.HasExImage ?? false;
                hatFrontExRenderer.gameObject.SetActive(frontHasExImage);
                if(frontHasExImage) hatFrontExRenderer.sprite = frontImage?.GetExSprite(HatFrontIndex) ?? null;

                var backHasExImage = backImage?.HasExImage ?? false;
                hatBackExRenderer.gameObject.SetActive(backImage?.HasExImage ?? false);
                if (backHasExImage) hatBackExRenderer.sprite = backImage?.GetExSprite(HatBackIndex) ?? null;

                hatFrontExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * ((frontImage?.ExIsFront ?? false) ? -0.125f : 0.125f));
                hatBackExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * ((backImage?.ExIsFront ?? false) ? -0.125f : 0.125f));
            }
            else
            {
                MyLayer.hat!.FrontLayer.transform.SetLocalZ(MyLayer.zIndexSpacing * -3f);

                hatFrontExRenderer.gameObject.SetActive(false);
                hatBackExRenderer.gameObject.SetActive(false);
            }
            if (CurrentModVisor != null)
            {
                //タイマーの更新
                VisorTimer -= Time.deltaTime;
                if (VisorTimer < 0f)
                {
                    VisorTimer = 1f / (float)CurrentModVisor.FPS;
                    VisorIndex++;
                    VisorBackIndex++;
                }

                //表示する画像の選定
                CosmicImage? image = null;
                CosmicImage? backImage = null;

                if (animState is not PlayerAnimState.ClimbUp and not PlayerAnimState.ClimbDown)
                {
                    SetImage(ref image, CurrentModVisor.Main, CurrentModVisor.Flip);
                    SetImage(ref backImage, CurrentModVisor.Back, CurrentModVisor.BackFlip);
                }
                switch (animState)
                {
                    case PlayerAnimState.Run:
                        SetImage(ref image, CurrentModVisor.Move, CurrentModVisor.MoveFlip);
                        SetImage(ref backImage, CurrentModVisor.MoveBack, CurrentModVisor.MoveBackFlip);
                        break;
                    case PlayerAnimState.EnterVent:
                        SetImage(ref image, CurrentModVisor.EnterVent, CurrentModVisor.EnterVentFlip);
                        SetImage(ref backImage, CurrentModVisor.EnterVentBack, CurrentModVisor.EnterVentBackFlip);
                        break;
                    case PlayerAnimState.ExitVent:
                        SetImage(ref image, CurrentModVisor.ExitVent, CurrentModVisor.ExitVentFlip);
                        SetImage(ref backImage, CurrentModVisor.ExitVentBack, CurrentModVisor.ExitVentBackFlip);
                        break;
                    case PlayerAnimState.ClimbUp:
                        SetImage(ref image, CurrentModVisor.Climb, CurrentModVisor.ClimbFlip);
                        break;
                    case PlayerAnimState.ClimbDown:
                        SetImage(ref image, CurrentModVisor.Climb, CurrentModVisor.ClimbFlip);
                        SetImage(ref image, CurrentModVisor.ClimbDown, CurrentModVisor.ClimbDownFlip);
                        break;
                }

                //インデックスの調整
                VisorIndex %= image?.GetLength() ?? 1;
                VisorBackIndex %= backImage?.GetLength() ?? 1;
                if (lastVisorImage != image && (image?.RequirePlayFirstState ?? true)) VisorIndex = 0;
                if (lastVisorBackImage != backImage && (backImage?.RequirePlayFirstState ?? true)) VisorBackIndex = 0;
                lastVisorImage = image;
                lastVisorBackImage = backImage;

                MyLayer.visor!.Image.sprite = image?.GetSprite(VisorIndex) ?? null;
                visorBackRenderer.gameObject.SetActive(true);
                visorBackRenderer.sprite = backImage?.GetSprite(VisorBackIndex) ?? null;

                float frontZ = MyLayer.zIndexSpacing * (CurrentModVisor.BehindHat ? -2f : -4f);
                MyLayer.visor!.Image.transform.SetLocalZ(frontZ);
                visorBackRenderer.transform.SetLocalZ(-frontZ + MyLayer.zIndexSpacing * (CurrentModVisor.BackmostBack ? 1.5f : 0.5f)); //背景は前面の子なので、位置関係の計算に注意

                //追加レイヤー
                var frontHasExImage = image?.HasExImage ?? false;
                visorFrontExRenderer.gameObject.SetActive(frontHasExImage);
                visorFrontExRenderer.sprite = image?.GetExSprite(VisorIndex) ?? null;

                var backHasExImage = backImage?.HasExImage ?? false;
                visorBackExRenderer.gameObject.SetActive(backImage?.HasExImage ?? false);
                visorBackExRenderer.sprite = backImage?.GetExSprite(VisorBackIndex) ?? null;

                visorFrontExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * ((image?.ExIsFront ?? false) ? -0.125f : 0.125f));
                visorBackExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * ((backImage?.ExIsFront ?? false) ? -0.125f : 0.125f));
            }
            else
            {
                MyLayer.visor!.Image.transform.SetLocalZ(MyLayer.zIndexSpacing * (MyLayer.visor.visorData.behindHats ? -2f : -4f));

                visorBackRenderer.gameObject.SetActive(false);
                visorFrontExRenderer.gameObject.SetActive(false);
                visorBackExRenderer.gameObject.SetActive(false);
            }

            var shouldUseDefault = !(MyLayer.bodyMatProperties.MaskType is PlayerMaterial.MaskType.ComplexUI or PlayerMaterial.MaskType.SimpleUI);
            var shouldUsePlayerVisor = CurrentModVisor?.Adaptive ?? false;
            if (shouldUseDefault != useDefaultShader || shouldUsePlayerVisor != usePlayerShaderOnVisor)
            {
                useDefaultShader = shouldUseDefault;
                usePlayerShaderOnVisor = shouldUsePlayerVisor;

                Material exShader = shouldUseDefault ? HatManager.Instance.DefaultShader : HatManager.Instance.MaskedMaterial;
                Material visorShader = shouldUsePlayerVisor ? (shouldUseDefault ? HatManager.Instance.PlayerMaterial : HatManager.Instance.MaskedPlayerMaterial) : exShader;

                hatFrontExRenderer.sharedMaterial = exShader;
                hatBackExRenderer.sharedMaterial = exShader;
                visorFrontExRenderer.sharedMaterial = exShader;
                visorBackExRenderer.sharedMaterial = exShader;
                visorBackRenderer.sharedMaterial = visorShader;

                PlayerMaterial.SetColors(MyLayer.ColorId, visorBackRenderer);
            }

            AdditionalRenderers().Do(r => r.enabled = MyLayer.visible);
        }
        catch { }
    }
}

[HarmonyPatch]
public static class TabEnablePatch
{
    public static TMPro.TMP_Text textTemplate = null!;

    private static float headerSize = 0.8f;
    private static float headerX = 0.85f;
    private static float inventoryZ = -2f;

    private static List<TMPro.TMP_Text> customTexts = new List<TMPro.TMP_Text>();

    private static SpriteLoader animationSprite = SpriteLoader.FromResource("Nebula.Resources.HasGraphicIcon.png", 100f);

    public static void SetUpTab<ItemTab,VanillaItem,ModItem>(ItemTab __instance,VanillaItem emptyItem,(VanillaItem,ModItem?)[] items,Func<VanillaItem> defaultProvider,Action<VanillaItem> selector,Action<VanillaItem, ColorChip>? chipSetter = null) where ItemTab : InventoryTab where ModItem : CustomCosmicItem where VanillaItem : CosmeticData
    {
        textTemplate = __instance.transform.FindChild("Text").gameObject.GetComponent<TMPro.TMP_Text>();

        var groups = items.GroupBy((tuple) => tuple.Item2?.Package ?? "InnerSloth").OrderBy(group => MoreCosmic.AllPackages.TryGetValue(group.Key, out var package) ? package.Priority : 10000);

        foreach (var text in customTexts) if (text) GameObject.Destroy(text.gameObject);
        foreach (var chip in __instance.ColorChips) if (chip) GameObject.Destroy(chip.gameObject);
        customTexts.Clear();
        __instance.ColorChips.Clear();

        float y = __instance.YStart;

        if (__instance.ColorTabPrefab.Inner != null)
        {
            __instance.ColorTabPrefab.Inner.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            __instance.ColorTabPrefab.Inner.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }
        __instance.ColorTabPrefab.PlayerEquippedForeground.GetComponentsInChildren<SpriteRenderer>().Do(renderer => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask);
        __instance.ColorTabPrefab.SelectionHighlight.transform.parent.GetComponentsInChildren<SpriteRenderer>().Do(renderer => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask);

        foreach (var group in groups)
        {
            TMPro.TMP_Text title = UnityEngine.Object.Instantiate<TMPro.TMP_Text>(textTemplate, __instance.scroller.Inner);
            title.GetComponent<TextTranslatorTMP>().enabled = false;
            var mat = title.GetComponent<MeshRenderer>().material;
            mat.SetFloat("_StencilComp", 4f);
            mat.SetFloat("_Stencil", 1f);

            title.transform.parent = __instance.scroller.Inner;
            title.transform.localPosition = new Vector3(headerX, y, inventoryZ);
            title.text = MoreCosmic.AllPackages.TryGetValue(group.Key, out var package) ? package.DisplayName : group.Key;
            title.alignment = TMPro.TextAlignmentOptions.Center;
            title.fontSize = 5f;
            title.fontWeight = TMPro.FontWeight.Thin;
            title.enableAutoSizing = false;
            title.autoSizeTextContainer = true;
            y -= headerSize * __instance.YOffset;
            customTexts.Add(title);


            int index = 0;
            float yInOffset = 0;
            foreach (var item in group.Prepend((emptyItem,null)))
            {
                VanillaItem vanillaItem = item.Item1;
                ModItem? modItem = item.Item2;
                if (index != 0 && vanillaItem == emptyItem) continue;

                yInOffset = (float)(index / __instance.NumPerRow) * __instance.YOffset;
                float itemX = __instance.XRange.Lerp((float)(index % __instance.NumPerRow) / ((float)__instance.NumPerRow - 1f));
                float itemY = y - yInOffset;

                ColorChip colorChip = GameObject.Instantiate<ColorChip>(__instance.ColorTabPrefab, __instance.scroller.Inner);
                colorChip.transform.localPosition = new Vector3(itemX, itemY, -1f);

                colorChip.Button.OnMouseOver.AddListener(()=>selector.Invoke(vanillaItem));
                colorChip.Button.OnMouseOut.AddListener(() => selector.Invoke(defaultProvider.Invoke()));
                colorChip.Button.OnClick.AddListener(__instance.ClickEquip);

                colorChip.Button.ClickMask = __instance.scroller.Hitbox;

                colorChip.ProductId = vanillaItem.ProductId;

                if(chipSetter == null)
                {
                    colorChip.Inner.SetMaskType(PlayerMaterial.MaskType.ScrollingUI);
                    __instance.UpdateMaterials(colorChip.Inner.FrontLayer, vanillaItem);
                    int colorId = __instance.HasLocalPlayer() ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId : ((int)DataManager.Player.Customization.Color);
                    vanillaItem.SetPreview(colorChip.Inner.FrontLayer, colorId);

                    var previewAdditionalSprite = modItem?.PreviewAdditionalSprite;
                    if(previewAdditionalSprite != null)
                    {
                        var ex = GameObject.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                        ex.sharedMaterial = HatManager.Instance.DefaultShader;
                        ex.sprite = previewAdditionalSprite;
                        ex.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f, 0f, modItem!.PreviewAdditionalInFront ? -0.1f : 0.1f);
                    }

                    if (modItem is CosmicHat mHat && mHat.Preview == null && mHat.Main != null)
                    {
                        if (mHat.Main.HasExImage)
                        {
                            var exFront = GameObject.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                            exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                            exFront.sprite = mHat.Main.GetExSprite(0);
                            exFront.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f,0f,mHat.Main.ExIsFront ? -0.1f : 0.1f);
                        }


                        if (mHat.Back != null)
                        {
                            __instance.UpdateMaterials(colorChip.Inner.BackLayer, vanillaItem);
                            colorChip.Inner.BackLayer.sprite = mHat.Back.GetSprite(0)!;
                            if (Application.isPlaying) PlayerMaterial.SetColors(colorId, colorChip.Inner.BackLayer);

                            if (mHat.Back.HasExImage)
                            {
                                var exFront = GameObject.Instantiate(colorChip.Inner.BackLayer, colorChip.Inner.BackLayer.transform.parent);
                                exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                                exFront.sprite = mHat.Back.GetExSprite(0);
                                exFront.transform.localPosition = colorChip.Inner.BackLayer.transform.localPosition + new Vector3(0f, 0f, mHat.Back.ExIsFront ? -0.1f : 0.1f);
                            }
                        }
                    }
                    else if(modItem is CosmicVisor mVisor && mVisor.Preview == null && mVisor.Main != null)
                    {
                        if (mVisor.Main.HasExImage)
                        {
                            var exFront = GameObject.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                            exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                            exFront.sprite = mVisor.Main.GetExSprite(0);
                            exFront.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f, 0f, mVisor.Main.ExIsFront ? -0.1f : 0.1f);
                        }


                        if (mVisor.Back != null)
                        {
                            __instance.UpdateMaterials(colorChip.Inner.BackLayer, vanillaItem);
                            colorChip.Inner.BackLayer.sprite = mVisor.Back.GetSprite(0)!;
                            if (Application.isPlaying) PlayerMaterial.SetColors(colorId, colorChip.Inner.BackLayer);

                            if (mVisor.Back.HasExImage)
                            {
                                var exFront = GameObject.Instantiate(colorChip.Inner.BackLayer, colorChip.Inner.BackLayer.transform.parent);
                                exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                                exFront.sprite = mVisor.Back.GetExSprite(0);
                                exFront.transform.localPosition = colorChip.Inner.BackLayer.transform.localPosition + new Vector3(0f, 0f, mVisor.Back.ExIsFront ? -0.1f : 0.1f);
                            }
                        }
                    }
                }
                else
                {
                    chipSetter.Invoke(vanillaItem,colorChip);
                }

                if (modItem?.HasAnimation ?? false)
                {
                    GameObject obj = new GameObject("AnimationMark");
                    obj.transform.SetParent(colorChip.transform);
                    obj.layer = colorChip.gameObject.layer;
                    obj.transform.localPosition = new Vector3(-0.38f, 0.39f, -10f);
                    SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                    renderer.sprite = animationSprite.GetSprite();
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }

                colorChip.Tag = vanillaItem;
                colorChip.SelectionHighlight.gameObject.SetActive(false);
                __instance.ColorChips.Add(colorChip);

                try
                {
                    colorChip.Inner.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    colorChip.Inner.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }catch(Exception e) { }

                index++;
            }

            y -= yInOffset + __instance.YOffset;
        }

        __instance.scroller.ContentYBounds.max = -(y - __instance.YStart) - 3.5f;
        __instance.scroller.UpdateScrollBars();
    }

    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.OnEnable))]
    public class HatsTabOnEnablePatch
    {
        public static bool Prefix(HatsTab __instance)
        {
            (HatData, CosmicHat?)[] unlockedHats = DestroyableSingleton<HatManager>.Instance.GetUnlockedHats().Select(hat => MoreCosmic.AllHats.TryGetValue(hat.ProductId, out var modHat) ? (hat, modHat) : (hat, null)).ToArray();
            __instance.currentHat = DestroyableSingleton<HatManager>.Instance.GetHatById(DataManager.Player.Customization.Hat);

            SetUpTab(__instance, HatManager.Instance.allHats.First(h => h.IsEmpty), unlockedHats, 
                () => HatManager.Instance.GetHatById(DataManager.Player.Customization.Hat),
                (hat) => __instance.SelectHat(hat)
            );

            return false;
        }
    }

    [HarmonyPatch(typeof(VisorsTab), nameof(VisorsTab.OnEnable))]
    public class VisorsTabOnEnablePatch
    {
        public static bool Prefix(VisorsTab __instance)
        {
            (VisorData, CosmicVisor?)[] unlockedVisors = DestroyableSingleton<HatManager>.Instance.GetUnlockedVisors().Select(visor => MoreCosmic.AllVisors.TryGetValue(visor.ProductId, out var modVisor) ? (visor, modVisor) : (visor, null)).ToArray();

            SetUpTab(__instance, HatManager.Instance.allVisors.First(v => v.IsEmpty), unlockedVisors,
                () => HatManager.Instance.GetVisorById(DataManager.Player.Customization.Visor),
                (visor) => __instance.SelectVisor(visor)
            );

            return false;
        }
    }

    [HarmonyPatch(typeof(NameplatesTab), nameof(NameplatesTab.OnEnable))]
    public class NameplatesTabOnEnablePatch
    {
        public static bool Prefix(NameplatesTab __instance)
        {
            (NamePlateData, CosmicNameplate?)[] unlockedNamePlates = DestroyableSingleton<HatManager>.Instance.GetUnlockedNamePlates().Select(nameplate => MoreCosmic.AllNameplates.TryGetValue(nameplate.ProductId, out var modNameplate) ? (nameplate, modNameplate) : (nameplate, null)).ToArray();

            __instance.previewArea.TargetPlayerId = NebulaPlayerTab.PreviewColorId;
            SetUpTab(__instance, HatManager.Instance.allNamePlates.First(v => v.IsEmpty), unlockedNamePlates,
                () => HatManager.Instance.GetNamePlateById(DataManager.Player.Customization.NamePlate),
                (nameplate) => __instance.SelectNameplate(nameplate),
                (item, chip) => {
                    __instance.StartCoroutine(AddressableAssetExtensions.CoLoadAssetAsync(__instance, item.Cast<IAddressableAssetProvider<NamePlateViewData>>(), (Il2CppSystem.Action<NamePlateViewData>)((viewData) =>
                    {
                        var plateChip = chip.CastFast<NameplateChip>().image;
                        plateChip.sprite = ((viewData != null) ? viewData.Image : null);

                        if (MoreCosmic.AllNameplates.TryGetValue(item.ProductId, out var mPlate)) {
                            var adaptiveChip = GameObject.Instantiate(plateChip, plateChip.transform.parent);
                            GameObject.Destroy(adaptiveChip.GetComponent<PassiveButton>());
                            GameObject.Destroy(adaptiveChip.GetComponent<CircleCollider2D>());
                            adaptiveChip.sprite = mPlate.Adaptive?.GetSprite(0);
                            adaptiveChip.transform.localPosition = plateChip.transform.localPosition + new Vector3(0f, 0f, mPlate.AdaptiveInFront ? -0.1f : 0.1f) ;
                            adaptiveChip.transform.localScale = plateChip.transform.localScale;
                            adaptiveChip.material = HatManager.Instance.PlayerMaterial;
                            PlayerMaterial.SetColors(NebulaPlayerTab.PreviewColorId, adaptiveChip);
                        }
                    })));
                }
            );

            return false;
        }
    }

    [HarmonyPatch(typeof(CosmeticData), nameof(CosmeticData.SetPreview))]
    public class SetPreviewPatch
    {
        public static bool Prefix(CosmeticData __instance, [HarmonyArgument(0)] SpriteRenderer renderer, [HarmonyArgument(1)] int color)
        {
            if (renderer != null)
            {
                if (__instance.TryCast<HatData>() != null && MoreCosmic.AllHats.TryGetValue(__instance.ProductId, out var hat))
                {
                    renderer.sprite = hat.PreviewSprite;
                    PlayerMaterial.SetColors(color, renderer);
                    return false;
                }
                if (__instance.TryCast<VisorData>() != null && MoreCosmic.AllVisors.TryGetValue(__instance.ProductId, out var visor))
                {
                    renderer.sprite = visor.PreviewSprite;
                    PlayerMaterial.SetColors(color, renderer);
                    return false;
                }
                if (__instance.TryCast<NamePlateData>() != null && MoreCosmic.AllNameplates.TryGetValue(__instance.ProductId, out var nameplate))
                {
                    renderer.sprite = nameplate.PreviewSprite;
                    PlayerMaterial.SetColors(color, renderer);
                    return false;
                }
                return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.PreviewNameplate))]
    public class PreviewNameplatePatch
    {
        public static void Postfix(PlayerVoteArea __instance, [HarmonyArgument(0)] string plateID)
        {
            if(!__instance.gameObject.TryGetComponent<NebulaNameplate>(out var nebulaPlate))
                nebulaPlate = __instance.gameObject.AddComponent<NebulaNameplate>();
            else nebulaPlate.UpdateColor();

            var plate = HatManager.Instance.GetNamePlateById(plateID);
            if (plate != null && MoreCosmic.AllNameplates.TryGetValue(plate.ProductId, out var mPlate))
            {
                nebulaPlate.AdaptiveRenderer.sprite = mPlate.Adaptive?.GetSprite(0);
                nebulaPlate.AdaptiveRenderer.transform.localPosition = new Vector3(0, 0, mPlate.AdaptiveInFront ? -0.1f : 0.1f);
                PlayerMaterial.SetColors(__instance.TargetPlayerId, nebulaPlate.AdaptiveRenderer);

            }
            else
            {
                nebulaPlate.AdaptiveRenderer.sprite = null;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerVoteArea), nameof(PlayerVoteArea.SetCosmetics))]
    public class NameplateSetCosmeticsPatch
    {
        public static void Postfix(PlayerVoteArea __instance, [HarmonyArgument(0)] NetworkedPlayerInfo playerInfo)
        {

            if (!__instance.gameObject.TryGetComponent<NebulaNameplate>(out var nebulaPlate))
                nebulaPlate = __instance.gameObject.AddComponent<NebulaNameplate>();

            var plate = HatManager.Instance.GetNamePlateById(playerInfo.DefaultOutfit.NamePlateId);
            if (plate != null && MoreCosmic.AllNameplates.TryGetValue(plate.ProductId, out var mPlate))
            {
                nebulaPlate.AdaptiveRenderer.sprite = mPlate.Adaptive?.GetSprite(0);
                nebulaPlate.AdaptiveRenderer.transform.localPosition = new Vector3(0, 0, mPlate.AdaptiveInFront ? -0.1f : 0.1f);
                PlayerMaterial.SetColors(playerInfo.PlayerId, nebulaPlate.AdaptiveRenderer);
            }
            else
            {
                nebulaPlate.AdaptiveRenderer.sprite = null;
            }
            
        }
    }
}