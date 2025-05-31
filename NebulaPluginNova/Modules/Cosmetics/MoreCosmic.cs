using AmongUs.Data;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Text.RegularExpressions;
using Innersloth.Assets;
using Nebula.Behavior;
using Nebula.Utilities;
using PowerTools;
using Rewired.UI.ControlMapper;
using Sentry;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Virial;
using Virial.Game;
using Virial.Media;
using Virial.Runtime;
using Virial.Utilities;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;
using static Nebula.Modules.ClientOption;
using static PlayerMaterial;

namespace Nebula.Modules.Cosmetics;

public class CostumePermissionHolder
{
    [JsonSerializableField(true)]
    public bool IsLocalizable = true;

    virtual public bool CanLocalize => IsLocalizable;
}
public class CustomItemGrouped : CostumePermissionHolder
{
    public CustomItemBundle MyBundle = null!;
    public LocalMarketplaceItem? RelatedMarketplaceItem => MyBundle?.RelatedMarketplaceItem;

    public override bool CanLocalize => base.CanLocalize && MyBundle.CanLocalize;
}

public class CostumeAction
{
    static CostumeAction() => JsonStructure.Register<CostumeAction>(text => new CostumeAction(text));
    public CostumeAction(string costume)
    {
        var ary = costume.Split("_", 2);
        Author = ary.Get(0, "");
        Name = ary.Get(1, "");
    }
    public CostumeAction(string author, string name)
    {
        Author = author;
        Name = name;
    }
    public CostumeAction() { }

    [JsonSerializableField]
    public string Name = "Undefined";
    [JsonSerializableField]
    public string Author = "Undefined";
    public string Costume => Author + "_" + Name;
    [JsonSerializableField(true)]
    public string? Condition = null;
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
    public List<string>? Tags = null;

    [JsonSerializableField(true)]
    public bool? IsHidden = null;

    [JsonSerializableField(true)]
    public CostumeAction? OnButton = null;
    [JsonSerializableField(true)]
    public CostumeAction? SabotageAlternative = null;
    [JsonSerializableField(true)]
    public CostumeAction? CommSabAlternative = null;
    [JsonSerializableField(true)]
    public CostumeAction? GhostAlternative = null;

    [JsonSerializableField(true)]
    public bool IsUnlockable = false;

    public bool ShowOnWardrobe => !(IsHidden ?? false);

    public string Id => Author + "_" + Name;
    abstract public string ProductId { get; }

    public string UnescapedName => Regex.Unescape(Name).Replace('_', ' ');
    public string UnescapedAuthor => Regex.Unescape(Author).Replace('_', ' ');
    public static string GetEscapedString(string text) => Regex.Escape(text.Replace(' ', '_'));

    public virtual string Category { get => "Undefined"; }
    public bool IsValid { get; private set; } = true;
    public bool IsActive { get; private set; } = false;


    public IEnumerable<CosmicImage> AllImage()
    {

        foreach (var f in GetType().GetFields())
        {
            if (!f.FieldType.Equals(typeof(CosmicImage))) continue;
            var image = (CosmicImage?)f.GetValue(this);
            if (image != null)
            {
                image.MyImageTag = f.Name;
                yield return image;
            }
        }
    }

    public string SubholderPath => Author.ToByteString() + "/" + Name.ToByteString();

    private bool? hasAnimationCache = null;
    public bool HasAnimation
    {
        get
        {
            hasAnimationCache ??= AllImage().Any(image => image.GetLength() > 1);
            return hasAnimationCache!.Value;
        }
    }

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
            if (image.Address != null) await CheckAndDownload(image.Hash, image.Address);
            if (image.ExAddress != null) await CheckAndDownload(image.ExHash, image.ExAddress);

            image.MyItem = this;
        }
    }

    public virtual IEnumerator Activate(bool addToMoreCosmic = true)
    {
        string holder = SubholderPath;

        /*
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
        */

        ITextureLoader GetLoader(string? address) => address == null ? null! : MyBundle.GetTextureLoader(Category, SubholderPath, address);

        AllImage().Do(image =>
        {
            var loader = GetLoader(image.Address);
            var exLoader = GetLoader(image.ExAddress);

            try
            {
                if (!image.SetTextureLoader(loader, exLoader))
                {
                    IsValid = false;
                }
            }
            catch
            {
                IsValid = false;
            }
        });

        yield break;
    }

    abstract public Sprite? PreviewSprite { get; }
    virtual public Sprite? PreviewAdditionalSprite { get => null; }
    virtual public bool PreviewAdditionalInFront { get => false; }

    abstract public IEnumerator LoadForGame(Action? onLoad);
    abstract public IEnumerator LoadForPreview(Action? onLoad);
    abstract public void UnloadForGame(Action? onUnload);
    abstract public void UnloadForPreview(Action? onUnload);
}

public abstract class CustomCosmicAnimationItem : CustomCosmicItem
{
    [JSFieldAmbiguous]
    public int FPS = 1;
    public float GetFPS(int index, CosmicImage? image)
    {
        if (image?.FPSCurve != null) return image.FPSCurve.Get(index, FPS);
        return FPS;
    }
}

public class CosmicImage
{
    public CustomCosmicItem? MyItem { get; set; } = null;
    public string MyImageTag { get; set; } = "Undefined";

    public bool CanLocalize => MyItem?.CanLocalize ?? true;
    private Versioning.Timestamp localizeCheckVersion = Language.LanguageVersion.GetTimestamp();

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

    [JsonSerializableField(true)]
    public List<float>? FPSCurve = null;

    public void MarkAsUnloadAsset()
    {
        spriteLoader?.MarkAsUnloadAsset();
        exSpriteLoader?.MarkAsUnloadAsset();
    }

    public int GetLength() => X.HasValue && Y.HasValue ? Length.HasValue ? Math.Min(Length.Value, X.Value * Y.Value) : X.Value * Y.Value : Length ?? 1;

    public float PixelsPerUnit = 100f;
    public Vector2 Pivot = new Vector2(0.5f, 0.5f);

    public bool RequirePlayFirstState = false;
    private MultiImage? spriteLoader = null;
    private MultiImage? exSpriteLoader = null;
    private MultiImage? localizedSpriteLoader = null;
    private MultiImage? localizedExSpriteLoader = null;
    private void CheckLocalizedSprite()
    {
        localizedSpriteLoader = Hash != null ? MoreCosmic.TryGetLocalizedImage(Hash, PixelsPerUnit, X ?? 1, Y ?? 1) : null;
        localizedExSpriteLoader = ExHash != null ? MoreCosmic.TryGetLocalizedImage(ExHash, PixelsPerUnit, X ?? 1, Y ?? 1) : null;
    }
    public MultiImage? MainSpriteLoader
    {
        get
        {
            if (CanLocalize)
            {
                if (localizeCheckVersion.Check()) CheckLocalizedSprite();
                return localizedSpriteLoader ?? spriteLoader;
            }
            else
            {
                return spriteLoader;
            }
        }
    }
    public MultiImage? ExSpriteLoader
    {
        get
        {
            if (CanLocalize)
            {
                if (localizeCheckVersion.Check()) CheckLocalizedSprite();
                return localizedExSpriteLoader ?? exSpriteLoader;
            }
            else
            {
                return exSpriteLoader;
            }
        }
    }

    public bool HasExImage => ExAddress != null;

    public bool SetTextureLoader(ITextureLoader textureLoader, ITextureLoader? exTextureLoader)
    {
        spriteLoader = new DividedSpriteLoader(textureLoader, PixelsPerUnit, X ?? Length ?? 1, Y ?? 1) { Pivot = Pivot };
        //for (int i = 0; i < length; i++) if (!spriteLoader.GetSprite(i)) return false;

        if (ExAddress != null)
        {
            exSpriteLoader = new DividedSpriteLoader(exTextureLoader!, PixelsPerUnit, X ?? Length ?? 1, Y ?? 1) { Pivot = Pivot };
            //for (int i = 0; i < length; i++) if (!exSpriteLoader!.GetSprite(i)) return false;
        }

        return true;
    }

    public Sprite? GetSprite(int index) => MainSpriteLoader?.GetSprite(index) ?? null;
    public Sprite? GetExSprite(int index) => ExSpriteLoader?.GetSprite(index) ?? null;

    public static string ComputeImageHash(Stream stream)
    {
        return BitConverter.ToString(CustomItemBundle.MD5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    public IEnumerator LoadGraphic()
    {
        yield return spriteLoader?.LoadAsset();
        yield return exSpriteLoader?.LoadAsset();
        yield return localizedSpriteLoader?.LoadAsset();
        yield return localizedExSpriteLoader?.LoadAsset();
    }
    public void UnloadGraphic()
    {
        spriteLoader?.UnloadAsset();
        exSpriteLoader?.UnloadAsset();
        localizedSpriteLoader?.UnloadAsset();
        localizedExSpriteLoader?.UnloadAsset();
    }
}

public class CosmicHat : CustomCosmicAnimationItem
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
    [JsonSerializableField(true)]
    public bool SeekerCostume = true;
    [JsonSerializableField(true)]
    public CostumeAction? SeekerAlternative = null;
    [JsonSerializableField(true)]
    public bool? DoAnimationIfDead = null;

    override public string ProductId => IdToProductId(Id);
    public static string IdToProductId(string id) => "noshat_" + id;

    public HatData MyHat { get; private set; } = null!;
    public HatViewData MyView { get; private set; } = null!;
    public override IEnumerator Activate(bool addToMoreCosmic)
    {
        foreach (var image in AllImage())
        {
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
        MyHat.ProductId = ProductId;
        MyHat.InFront = true;
        MyHat.NoBounce = !Bounce;
        MyHat.ChipOffset = new Vector2(0f, 0.2f);
        MyHat.Free = ShowOnWardrobe;
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

        if (addToMoreCosmic) MoreCosmic.AllHats[MyHat.ProductId] = this;
    }

    public override string Category { get => "hats"; }

    private CosmicImage? PreviewImage => Preview ?? Main ?? Back ?? Move;
    public override Sprite? PreviewSprite => PreviewImage?.GetSprite(0);
    public override Sprite? PreviewAdditionalSprite => Preview?.GetExSprite(0);
    public override bool PreviewAdditionalInFront => Preview?.ExIsFront ?? false;

    public override IEnumerator LoadForGame(Action? onLoad)
    {
        foreach (var image in AllImage())
        {
            if (image == null) continue;
            yield return image.LoadGraphic();
        }
        onLoad?.Invoke();
    }

    public override IEnumerator LoadForPreview(Action? onLoad)
    {
        yield return PreviewImage?.LoadGraphic();
        onLoad?.Invoke();
    }

    public override void UnloadForGame(Action? onUnload)
    {
        foreach (var image in AllImage())
        {
            image?.UnloadGraphic();
        }
        onUnload?.Invoke();
    }

    public override void UnloadForPreview(Action? onUnload)
    {
        Preview?.UnloadGraphic();
        onUnload?.Invoke();
    }
}

public class CosmicVisor : CustomCosmicAnimationItem
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
    [JsonSerializableField(true)]
    public bool Fixed = false;
    [JsonSerializableField(true)]
    public bool? DoAnimationIfDead = null;

    override public string ProductId => IdToProductId(Id);
    public static string IdToProductId(string id) => "nosvisor_" + id;
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
        MyVisor.ProductId = ProductId;
        MyVisor.ChipOffset = new Vector2(0f, 0.2f);
        MyVisor.Free = ShowOnWardrobe;
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

        if (addToMoreCosmic) MoreCosmic.AllVisors[MyVisor.ProductId] = this;
    }
    public override string Category { get => "visors"; }

    private CosmicImage? PreviewImage => Preview ?? Main ?? Back;
    public override Sprite? PreviewSprite => Preview?.GetSprite(0) ?? Main?.GetSprite(0) ?? Back?.GetSprite(0);
    public override Sprite? PreviewAdditionalSprite => Preview?.GetExSprite(0);
    public override bool PreviewAdditionalInFront => Preview?.ExIsFront ?? false;

    public override IEnumerator LoadForGame(Action? onLoad)
    {
        foreach (var image in AllImage())
        {
            if (image == null) continue;
            yield return image.LoadGraphic();
        }
        onLoad?.Invoke();
    }

    public override IEnumerator LoadForPreview(Action? onLoad)
    {
        yield return PreviewImage?.LoadGraphic();
        onLoad?.Invoke();
    }

    public override void UnloadForGame(Action? onUnload)
    {
        foreach (var image in AllImage())
        {
            image?.UnloadGraphic();
        }
        onUnload?.Invoke();
    }

    public override void UnloadForPreview(Action? onUnload)
    {
        Preview?.UnloadGraphic();
        onUnload?.Invoke();
    }
}

public class CosmicNameplate : CustomCosmicItem
{
    [JsonSerializableField(true)]
    public CosmicImage? Plate;
    [JsonSerializableField(true)]
    public CosmicImage? Adaptive;
    [JsonSerializableField(true)]
    public bool AdaptiveInFront = true;
    override public string ProductId => IdToProductId(Id);
    public static string IdToProductId(string id) => "nosplate_" + id;
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
        MyPlate.ProductId = ProductId;
        MyPlate.ChipOffset = new Vector2(0f, 0.2f);
        MyPlate.Free = ShowOnWardrobe;
        //MyPlate.SpritePreview = Plate?.GetSprite(0);

        MyPlate.CreateAddressableAsset();

        if (addToMoreCosmic) MoreCosmic.AllNameplates[MyPlate.ProductId] = this;
    }
    public override string Category { get => "nameplates"; }

    public override Sprite? PreviewSprite => Plate?.GetSprite(0);

    public override IEnumerator LoadForGame(Action? onLoad)
    {
        foreach (var image in AllImage())
        {
            if (image == null) continue;
            yield return image.LoadGraphic();
        }

        onLoad?.Invoke();
    }

    public override IEnumerator LoadForPreview(Action? onLoad) => LoadForGame(onLoad);

    public override void UnloadForGame(Action? onUnload)
    {
        foreach (var image in AllImage()) image?.UnloadGraphic();
        onUnload?.Invoke();
    }

    public override void UnloadForPreview(Action? onUnload)
    {
        onUnload?.Invoke();
    }
}

public class CosmicStamp : CustomCosmicAnimationItem
{
    [JsonSerializableField(true)]
    public CosmicImage? Image;
    [JsonSerializableField(true)]
    public bool Adaptive = false;

    override public string ProductId => IdToProductId(Id);
    public static string IdToProductId(string id) => "stamp_" + id;
    public override IEnumerator Activate(bool addToMoreCosmic)
    {
        yield return base.Activate(addToMoreCosmic);
        if (!IsValid) yield break;

        Image?.MarkAsUnloadAsset();

        if (addToMoreCosmic) MoreCosmic.AllStamps[ProductId] = this;
    }
    public override string Category { get => "stamps"; }

    //スタンプのプレビューは使用しない
    public override Sprite? PreviewSprite => null;

    public override IEnumerator LoadForGame(Action? onLoad)
    {
        onLoad?.Invoke();
        yield break;
    }

    public override IEnumerator LoadForPreview(Action? onLoad) => LoadForGame(onLoad);

    public override void UnloadForGame(Action? onUnload) => onUnload?.Invoke();


    public override void UnloadForPreview(Action? onUnload) => onUnload?.Invoke();

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

public class CustomItemLocalizationEntry
{
    [JsonSerializableField]
    public string Hash = "Undefined";
    [JsonSerializableField]
    public string Address = "Undefined";
}
public class CustomItemLocalization
{
    [JsonSerializableField]
    public string Language = "English";
    [JsonSerializableField]
    public List<CustomItemLocalizationEntry> Entries = [];
}

public class CustomItemBundle : CostumePermissionHolder
{
    static public readonly MD5 MD5 = MD5.Create();

    static readonly Dictionary<string, CustomItemBundle> AllBundles = [];

    public LocalMarketplaceItem? RelatedMarketplaceItem = null;

    [JSFieldAmbiguous]
    public string? BundleName = null;

    [JSFieldAmbiguous]
    public List<CosmicHat> Hats = [];
    [JSFieldAmbiguous]
    public List<CosmicVisor> Visors = [];
    [JSFieldAmbiguous]
    public List<CosmicNameplate> Nameplates = [];
    [JSFieldAmbiguous]
    public List<CosmicStamp> Stamps = [];
    [JSFieldAmbiguous]
    public List<CosmicPackage> Packages = [];

    public string? RelatedLocalAddress { get; set; } = null;
    public string? RelatedRemoteAddress { get; set; } = null;
    public ZipArchive? RelatedZip { get; private set; } = null;

    public bool IsActive { get; private set; } = false;

    private IEnumerable<CustomCosmicItem> AllCosmicItem()
    {
        foreach (var item in Hats) yield return item;
        foreach (var item in Visors) yield return item;
        foreach (var item in Nameplates) yield return item;
        foreach (var item in Stamps) yield return item;
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
        if (addToMoreCosmic) AllBundles[BundleName!] = this;

        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, $"Start to load costume bundle. (Bundle: {BundleName}, Contents: {Hats.Count + Visors.Count + Nameplates.Count})");

        foreach (var item in AllCosmicItem())
        {
            item.MyBundle = this;

            yield return item.Activate(addToMoreCosmic);
        }

        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, $"Finish to load costume bundle! (Bundle: {BundleName})");

        if (addToMoreCosmic)
        {
            foreach (var package in Packages) MoreCosmic.AllPackages[package.Package] = package;

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

        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, $"Finish to flush costume bundle! (Bundle: {BundleName})");
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

    public async Task DownloadAsset(string category, string localHolder, string address)
    {
        //リモートリポジトリやローカルの配置先が無い場合はダウンロードできない
        if (RelatedRemoteAddress == null || RelatedLocalAddress == null) return;

        var hatFileResponse = await NebulaPlugin.HttpClient.GetAsync(RelatedRemoteAddress + category + "/" + address, HttpCompletionOption.ResponseContentRead);
        if (hatFileResponse.StatusCode != HttpStatusCode.OK) return;

        var responseStream = await hatFileResponse.Content.ReadAsByteArrayAsync();
        //サブディレクトリまでを作っておく
        string localPath = RelatedLocalAddress + category + "/" + localHolder + "/" + address;

        var dir = Path.GetDirectoryName(localPath);
        if (dir != null) Directory.CreateDirectory(dir);

        using var fileStream = File.Create(localPath);

        await fileStream.WriteAsync(responseStream);

        if (AllOptions[ClientOptionType.OutputCosmicHash].Value == 1)
        {
            string hash = BitConverter.ToString(MD5.ComputeHash(responseStream)).Replace("-", "").ToLowerInvariant();

            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, $"Hash: {hash} ({category}/{address})");
        }

    }


    public ITextureLoader GetTextureLoader(string category, string subholder, string address)
    {
        if (RelatedZip != null)
        {
            return new StreamTextureLoader(() => RelatedZip.GetEntry(RelatedLocalAddress + category + "/" + address)?.Open()!);
        }
        else if (RelatedRemoteAddress == null)
        {
            return new DiskTextureLoader(RelatedLocalAddress + category + "/" + address);
        }
        else
        {
            return new DiskTextureLoader(RelatedLocalAddress + category + "/" + subholder + "/" + address);
        }
    }


    static public async Task<CustomItemBundle?> LoadOnline(LocalMarketplaceItem? item, string url)
    {
        NebulaPlugin.HttpClient.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        var response = await NebulaPlugin.HttpClient.GetAsync(new Uri($"{url}/Contents.json"), HttpCompletionOption.ResponseContentRead);
        if (response.StatusCode != HttpStatusCode.OK) return null;

        using StreamReader stream = new(await response.Content.ReadAsStreamAsync(), Encoding.UTF8);
        string json = stream.ReadToEnd();

        CustomItemBundle? bundle = null;
        try
        {
            bundle = (CustomItemBundle?)JsonStructure.Deserialize(json, typeof(CustomItemBundle));
        }
        catch (Exception ex)
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, $"Error occurred in costume deserialize (URL: {url})\n" + ex.ToString());
            return null;
        }

        if (bundle == null) return null;

        bundle.RelatedMarketplaceItem = item;
        bundle.RelatedRemoteAddress = url;
        bundle.RelatedLocalAddress = "MoreCosmic/";
        bundle.BundleName ??= url;
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
        bundle.BundleName ??= addon.AddonName;

        await bundle.Load();

        return bundle;
    }
}

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public static class MoreCosmic
{
    public static readonly Dictionary<string, CosmicHat> AllHats = [];
    public static readonly Dictionary<string, CosmicVisor> AllVisors = [];
    public static readonly Dictionary<string, CosmicNameplate> AllNameplates = [];
    public static readonly Dictionary<string, CosmicStamp> AllStamps = [];
    public static readonly Dictionary<string, CosmicPackage> AllPackages = [];
    public static Dictionary<string, HashSet<string>> VanillaTags = [];

    public record LocalizedSpriteLoader(string Address, IResourceAllocator Allocator)
    {
        public MultiImage? GetSpriteLoader(float pixelsPerUnit, int x, int y) => Allocator.GetResource(Address)?.AsMultiImage(x, y, pixelsPerUnit);
    }
    public static readonly Dictionary<string, Dictionary<string, LocalizedSpriteLoader>> AllLocalizations = [];
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
    public static Material AdaptiveShader
    {
        get
        {
            if (adaptiveShader == null) adaptiveShader = HatManager.Instance.PlayerMaterial;
            return adaptiveShader;
        }
    }

    private static bool isLoaded = false;
    private static readonly List<CustomItemBundle?> loadedBundles = [];

    private static void LoadLocalizationLocal(NebulaAddon addon)
    {
        //アドオンはローカライズの実装を含む
        var localizeStream = addon.OpenStream("MoreCosmic/Localization.json");
        if (localizeStream == null) return;
        var deserialized = JsonStructure.Deserialize<List<CustomItemLocalization>>(localizeStream);
        if (deserialized == null) return;

        foreach (var entry in deserialized)
        {
            if (!AllLocalizations.TryGetValue(entry.Language, out var localization))
            {
                localization = [];
                AllLocalizations[entry.Language] = localization;
            }

            int num = 0;
            foreach (var pair in entry.Entries)
            {
                if (pair == null) continue;
                localization![pair.Hash] = new(pair.Address, addon);
                num++;
            }

            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.MoreCosmic, num + "Localization entries have been registered! (Language: " + entry.Language + ")");
        }
    }
    public static MultiImage? TryGetLocalizedImage(string hash, float pixelsPerUnit, int x, int y)
    {
        if (AllLocalizations.TryGetValue(Language.GetCurrentLanguage(), out var localization))
        {
            if (localization.TryGetValue(hash, out var loader))
            {
                return loader.GetSpriteLoader(pixelsPerUnit, x, y);
            }
        }
        return null;
    }

    private static async Task LoadLocal()
    {
        foreach (var addon in NebulaAddon.AllAddons)
        {
            var bundle = await CustomItemBundle.LoadOffline(addon);

            lock (loadedBundles)
            {
                loadedBundles.Add(bundle);
            }
        }
    }

    private static List<(LocalMarketplaceItem? onlineItem, string url)> allRepos = [];
    private static async Task LoadOnline()
    {
        if (!NebulaPlugin.AllowHttpCommunication) return;

        var response = await NebulaPlugin.HttpClient.GetAsync(new Uri(Helpers.ConvertUrl("https://raw.githubusercontent.com/Dolly1016/MoreCosmic/master/UserCosmics.dat")), HttpCompletionOption.ResponseContentRead);
        if (response.StatusCode != HttpStatusCode.OK) return;

        string repos = await response.Content.ReadAsStringAsync();

        while (!HatManager.InstanceExists) await Task.Delay(1000);

        allRepos.AddRange(repos.Split("\n").Select(url => ((LocalMarketplaceItem? onlineItem, string url))(null, url)));
        allRepos.AddRange(MarketplaceData.Data?.OwningCostumes.Where(item => item.ToCostumeUrl.Length > 3 && !allRepos.Any(r => r.url == item.ToCostumeUrl)).Select(item => ((LocalMarketplaceItem? onlineItem, string url))(item, item.ToCostumeUrl)) ?? []);

        foreach (var repo in allRepos.ToArray())
        {
            try
            {
                var result = await CustomItemBundle.LoadOnline(repo.onlineItem, repo.url);

                lock (loadedBundles)
                {
                    loadedBundles.Add(result);
                }
            }
            catch { }
        }
    }

    public static async Task LoadOnlineExtra(LocalMarketplaceItem? onlineItem, string url)
    {
        if (allRepos.Any(r => r.url == url)) return;
        allRepos.Add((onlineItem, url));

        try
        {
            var result = await CustomItemBundle.LoadOnline(onlineItem, url);

            lock (loadedBundles)
            {
                loadedBundles.Add(result);
            }
        }
        catch (Exception e)
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.MoreCosmic, "Error is occurred while loading costumes.\n" + e.ToString());
        }
    }



    static private Queue<IEnumerator> ActivateQueue = [];

    public static void Update()
    {
        if (!HatManager.InstanceExists) return;
        if (!EOSManager.InstanceExists) return;
        if (!EOSManager.Instance.HasFinishedLoginFlow()) return;

        lock (loadedBundles)
        {
            if (loadedBundles.Count > 0)
            {
                foreach (var bundle in loadedBundles) if (bundle != null) ActivateQueue.Enqueue(bundle!.Activate(true).HighSpeedEnumerator());
                loadedBundles.Clear();
            }
        }

        if (ActivateQueue.Count > 0)
        {
            var current = ActivateQueue.Peek();
            if (!(current?.MoveNext() ?? false))
            {
                ActivateQueue.Dequeue();

                //ゲーム内ならば見た目を更新する
                if (LobbyBehaviour.Instance)
                {
                    foreach (var p in PlayerControl.AllPlayerControls)
                    {
                        var outfit = p.CurrentOutfit;
                        p.cosmetics.SetHat(outfit.HatId, p.PlayerId);
                        p.cosmetics.SetVisor(outfit.VisorId, p.PlayerId);
                    }
                }
            }
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

        _ = LoadAll();

        isLoaded = true;

        VanillaTags = JsonStructure.Deserialize<Dictionary<string, HashSet<string>>>(StreamHelper.OpenFromResource("Nebula.Resources.VanillaTags.json")!) ?? [];

        foreach (var addon in NebulaAddon.AllAddons) LoadLocalizationLocal(addon);
    }

    public static IEnumerable<string> GetTags(NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        if (AllHats.TryGetValue(outfit.HatId, out var hat))
        {
            foreach (var tag in hat.Tags ?? []) yield return "hat." + tag;
        }
        else if (VanillaTags.TryGetValue(outfit.HatId, out var vanillaTags))
        {
            foreach (var tag in vanillaTags ?? []) yield return "hat." + tag;
        }

        if (AllVisors.TryGetValue(outfit.VisorId, out var visor))
        {
            foreach (var tag in visor.Tags ?? []) yield return "visor." + tag;
        }
        else if (VanillaTags.TryGetValue(outfit.VisorId, out var vanillaTags))
        {
            foreach (var tag in vanillaTags ?? []) yield return "visor." + tag;
        }
    }

    public static readonly List<(int id, string title)> UnacquiredItems = [];
    public static readonly RemoteProcess<(int id, string title)> RpcShareMarketplaceItem = new("ShareMPItem", (message, calledByMyself) =>
    {
        if (calledByMyself) return;
        if (!AmongUsClient.Instance || AmongUsClient.Instance.GameState != InnerNet.InnerNetClient.GameStates.Joined) return; //ゲーム開始後は何もしない

        //同じIDのコスチュームを保持していない
        if (!MarketplaceData.Data.OwningCostumes.Any(c => c.EntryId == message.id) && !UnacquiredItems.Any(item => item.id == message.id)) UnacquiredItems.Add((message.id, message.title));
    });

    private readonly static RemoteProcess<(int ownerId, int outfitId, byte kind, string val)> RpcUpdateCostumeArgument = new(
       "UpdateCostumeArgument", (message, _) =>
       {
           if (NebulaGameManager.Instance?.TryGetOutfit(new(message.ownerId, message.outfitId), out var outfit) ?? false)
           {
               string? nullableVal = message.val.Length == 0 ? null : message.val;
               switch (message.kind)
               {
                   case 0:
                       outfit.HatArgument = nullableVal;
                       break;
                   case 1:
                       outfit.VisorArgument = nullableVal;
                       break;
               }
           }
       }
       );

    public static void RpcUpdateHatArgument(OutfitDefinition.OutfitId outfitId, string? arg) => RpcUpdateCostumeArgument.Invoke((outfitId.ownerId, outfitId.outfitId, 0, arg ?? string.Empty));
    public static void RpcUpdateVisorArgument(OutfitDefinition.OutfitId outfitId, string? arg) => RpcUpdateCostumeArgument.Invoke((outfitId.ownerId, outfitId.outfitId, 1, arg ?? string.Empty));
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
        }
        catch (Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }
}

public class WrappedHatAsset : AddressableAsset<HatViewData>
{
    public HatViewData? viewData = null;
    public WrappedHatAsset(IntPtr ptr) : base(ptr) { }
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
public class WrappedVisorAsset : AddressableAsset<VisorViewData>
{
    public VisorViewData viewData = null!;
    public WrappedVisorAsset(IntPtr ptr) : base(ptr) { }
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
public class WrappedNamePlateAsset : AddressableAsset<NamePlateViewData>
{
    public NamePlateViewData viewData = null!;
    public WrappedNamePlateAsset(IntPtr ptr) : base(ptr) { }
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
        if (MoreCosmic.AllHats.TryGetValue(id, out var hat))
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
        var asset = new WrappedHatAsset();
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
        }
        catch { return true; }
    }
}

[HarmonyPatch(typeof(HatParent), nameof(HatParent.SetHat), typeof(int))]
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


[HarmonyPatch(typeof(VisorLayer), nameof(VisorLayer.SetVisor), [typeof(VisorData), typeof(int)])]
public class SetVisorPatch
{
    public static bool Prefix(VisorLayer __instance, [HarmonyArgument(0)] VisorData data, [HarmonyArgument(1)] int color)
    {
        if (!MoreCosmic.AllVisors.TryGetValue(data.ProductId, out var value)) return true;

        if (data == null || data != __instance.visorData) __instance.Image.sprite = null;

        __instance.visorData = data;
        __instance.SetMaterialColor(color);
        __instance.UnloadAsset();
        var asset = new WrappedVisorAsset();
        asset.viewData = value.MyView;

        __instance.viewAsset = asset;
        __instance.LoadAssetAsync(__instance.viewAsset, (Il2CppSystem.Action)(() =>
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
            layer.RequireUpdateMaterial();
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
            Sprite? sprite = viewData != null ? viewData.IdleFrame : null;
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
    public static bool Prefix(CosmeticsCache __instance, ref Il2CppSystem.Collections.IEnumerator __result, [HarmonyArgument(0)] string visorId)
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

            if (down ? value.HasClimbDownImage : value.HasClimbUpImage) __instance.myPlayer.cosmetics.ToggleVisor(true);
        }
        catch { }

        __instance.myPlayer.cosmetics.ToggleVisor(true);
    }
}

public static class VisibilityFixPatch
{
    public static void FixVisibility(this CosmeticsLayer __instance)
    {
        if (__instance.currentBodySprite.Type == PlayerBodyTypes.Seeker) __instance.skin.Visible = false; //Seekerはスキンを表示しない
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.ResetAnimState))]
public static class HatVisibilityFixPatch
{


    public static void Postfix(PlayerPhysics __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        if (NebulaGameManager.Instance.GameState == NebulaGameStates.NotStarted) return;

        try
        {
            __instance.myPlayer.cosmetics.SetHatAndVisorIdle(__instance.myPlayer.GetModInfo()!.Unbox().CurrentOutfit.Outfit.outfit.ColorId);
        }
        catch { }

        __instance.myPlayer.cosmetics.FixVisibility();
    }
}


[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.UpdateVisibility))]
public static class UpdateVisibilityFixPatch
{
    public static void Postfix(CosmeticsLayer __instance)
    {
        __instance.FixVisibility();
    }
}


[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.Visible), MethodType.Setter)]
public static class VisibleSetterFixPatch
{
    public static void Postfix(CosmeticsLayer __instance)
    {
        __instance.FixVisibility();
    }
}


[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.SetBodyCosmeticsVisible))]
public static class BodyVisibilityFixPatch
{
    public static void Postfix(CosmeticsLayer __instance)
    {
        __instance.FixVisibility();
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
        __instance.gameObject.GetOrAddComponent<NebulaCosmeticsLayer>();
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
        AdaptiveRenderer = Instantiate(voteArea.Background, voteArea.Background.transform);
        AdaptiveRenderer.GetComponent<PassiveButton>().enabled = false;
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
        SetColors(voteArea.TargetPlayerId, AdaptiveRenderer);
    }
}

public class NebulaCosmeticsLayerVisorLink : MonoBehaviour
{
    static NebulaCosmeticsLayerVisorLink()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaCosmeticsLayerVisorLink>();
    }

    public Reference<NebulaCosmeticsLayer> NebulaLayer;
}

public class NebulaCosmeticsLayer : MonoBehaviour
{
    public CosmeticsLayer MyLayer = null!;
    public PlayerAnimations? MyAnimations = null!;
    public PlayerPhysics? MyPhysics = null;
    public GamePlayer? myModPlayerCache = null;

    public HatData? CurrentHat;
    public VisorData? CurrentVisor;
    public CosmicHat? CurrentModHat;
    public CosmicVisor? CurrentModVisor;
    //機能的なModコスチュームのキャッシュ
    public CosmicHat? CurrentFunctionalModHatCache = null;
    public CosmicVisor? CurrentFunctionalModVisorCache = null;
    //見た目上のModコスチュームのキャッシュ
    public CosmicHat? CurrentVisualModHatCache = null;
    public CosmicVisor? CurrentVisualModVisorCache = null;

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

    private SpriteRenderer? hatFrontExRenderer;
    private SpriteRenderer? hatBackExRenderer;
    private SpriteRenderer? visorFrontExRenderer;
    private SpriteRenderer? visorBackRenderer;
    private SpriteRenderer? visorBackExRenderer;

    private SpriteMask? bodyMaskByHat;
    private SpriteMask? bodyMaskByVisor;

    private Renderer[] renderersCache = null!;
    private Renderer[] bodyRenderersCache = null!;
    public SpriteRenderer? VisorBackRenderer => visorBackRenderer;
    public IEnumerable<SpriteRenderer> AdditionalRenderers()
    {
        foreach (var r in AdditionalHatRenderers()) yield return r;
        foreach (var r in AdditionalVisorRenderers()) yield return r;
    }
    public IEnumerable<SpriteRenderer> AdditionalHatRenderers()
    {
        if (hatFrontExRenderer != null) yield return hatFrontExRenderer;
        if (hatBackExRenderer != null) yield return hatBackExRenderer;
    }
    public IEnumerable<SpriteRenderer> AdditionalVisorRenderers()
    {
        if (visorBackRenderer != null) yield return visorBackRenderer;
        if (visorFrontExRenderer != null) yield return visorFrontExRenderer;
        if (visorBackExRenderer != null) yield return visorBackExRenderer;
    }

    public IEnumerable<SpriteMask> AdditionalMasks()
    {
        if (bodyMaskByHat != null) yield return bodyMaskByHat;
        if (bodyMaskByVisor != null) yield return bodyMaskByVisor;
    }

    private bool useDefaultShader = true;//追加したRendererのマテリアルを変更する必要があるか否か調べるために使用
    private bool usePlayerShaderOnVisor = false;//追加したRendererのマテリアルを変更する必要があるか否か調べるために使用
    public PlayerAnimState LastAnimState = PlayerAnimState.Idle;

    public bool ShouldSort = false;
    public int SortingBaseOrder = 0;
    public float SortingScale = 1000f;

    static NebulaCosmeticsLayer()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaCosmeticsLayer>();
    }

    public void Awake()
    {
        ShouldSort = false;
        SortingBaseOrder = 0;
        SortingScale = 1000f;
        renderersCache = null!;

        MyLayer = gameObject.GetComponent<CosmeticsLayer>();
        if (MyLayer.visor != null) MyLayer.visor.gameObject.AddComponent<NebulaCosmeticsLayerVisorLink>().NebulaLayer = new() { Value = this };

        var bodyParent = MyLayer.normalBodySprite.BodySprite.transform.parent;
        if (bodyParent.gameObject.TryGetComponent<MeetingCalledAnimation>(out _))
        {
            MyLayer.gameObject.AddComponent<SortingGroup>();
        }
        else if(bodyParent.parent)
        {
            SetSortingProperty(true, 10000f, 1000);
            bodyParent.parent.gameObject.AddComponent<SortingGroup>();
        }
        else
        {
            bodyParent.gameObject.AddComponent<SortingGroup>();
        }

        //PoolablePlayer相手には取得できない
        try
        {
            if (transform.parent && transform.parent.parent)
            {
                MyAnimations = transform.parent.parent.GetComponentInChildren<PlayerAnimations>();
                transform.parent.parent.TryGetComponent<PlayerPhysics>(out MyPhysics);
            }

            if (MyLayer.hat != null)
            {
                hatFrontExRenderer = Instantiate(MyLayer.hat.FrontLayer, MyLayer.hat.FrontLayer.transform);
                hatFrontExRenderer.transform.localPosition = new(0f, 0f, 0f);
                hatFrontExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

                hatBackExRenderer = Instantiate(MyLayer.hat.BackLayer, MyLayer.hat.BackLayer.transform);
                hatBackExRenderer.transform.localPosition = new(0f, 0f, 0f);
                hatBackExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

                /*
                bodyMaskByHat = UnityHelper.CreateObject<SpriteMask>("MaskByHat", bodyParent, Vector3.zero);
                bodyMaskByHat.gameObject.AddComponent<SortingGroupOrderFixer>().Initialize(bodyMaskByHat, 5000);
                */
            }

            if (MyLayer.visor != null)
            {
                visorBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("Back", MyLayer.visor.Image.transform, new(0f, 0f, 1f));
                visorBackRenderer.size = MyLayer.visor.Image.size;
                visorBackRenderer.transform.localPosition = new(0f, 0f, 0f);
                visorBackRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

                visorFrontExRenderer = Instantiate(visorBackRenderer, MyLayer.visor.Image.transform);
                visorFrontExRenderer.transform.localPosition = new(0f, 0f, 0f);
                visorFrontExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

                visorBackExRenderer = Instantiate(visorBackRenderer, visorBackRenderer.transform);
                visorBackExRenderer.transform.localPosition = new(0f, 0f, 0f);
                visorBackExRenderer.transform.localEulerAngles = new(0f, 0f, 0f);

                /*
                bodyMaskByVisor = UnityHelper.CreateObject<SpriteMask>("MaskByVisor", bodyParent, Vector3.zero);
                bodyMaskByVisor.gameObject.AddComponent<SortingGroupOrderFixer>().Initialize(bodyMaskByVisor, 10000);
                */
            }

            useDefaultShader = true;
            usePlayerShaderOnVisor = false;

            GetComponentsInChildren<SpriteRenderer>().Do(r =>
            {
                r.sortingGroupOrder = 0;
                r.sortingOrder = 0;
            });
        }
        catch { }
    }

    public bool IsDead => MyPhysics && MyPhysics!.myPlayer.Data && MyPhysics!.myPlayer.Data.IsDead;
    public bool IsGamePlayer => MyPhysics && MyPhysics!.myPlayer;
    private bool requiredUpdate = false;

    private bool requiredAction = false;
    public bool RejectZOrdering = false;
    public bool ZOrdering => !MyPhysics && !RejectZOrdering;
    private CosmeticsOrder Order = new();
    private struct CosmeticsOrder
    {
        public const int HatFrontDefault = 1000 + 20;
        public const int HatFrontSkinny = 1000 + 10;
        public const int HatBackDefault = 1000 - 20;
        public const int VisorFrontDefault = 1000 + 30;
        public const int VisorFrontBehindHat = 1000 + 15;
        public const int VisorBackDefault = 1000 - 10;
        public const int VisorBackBackmost = 1000 - 30;

        public int HatFront = HatFrontDefault;
        public int HatFrontExDiff = 1;
        public int HatFrontEx => HatFront + HatFrontExDiff;
        public int HatBack = HatBackDefault;
        public int HatBackExDiff = 1;
        public int HatBackEx => HatBack + HatBackExDiff;
        public int VisorFront = VisorFrontDefault;
        public int VisorFrontExDiff = 1;
        public int VisorFrontEx => VisorFront + VisorFrontExDiff;
        public int VisorBack = VisorBackDefault;
        public int VisorBackExDiff = 1;
        public int VisorBackEx => VisorBack + VisorBackExDiff;
        public int Body = 1000;
        public int Skin = 1015;

        public void ResetHatToDefault()
        {
            HatFront = HatFrontDefault;
            HatBack = HatBackDefault;
        }
        public void ResetVisorToDefault()
        {
            VisorFront = VisorFrontDefault;
            VisorBack = VisorBackDefault;
        }
        public CosmeticsOrder() { }
    }

    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CostumeButton.png", 115f);
    public GamePlayer? GetModPlayer()
    {
        if (myModPlayerCache == null && IsGamePlayer && NebulaGameManager.Instance != null && MyPhysics != null)
        {
            myModPlayerCache = NebulaGameManager.Instance.GetPlayer(MyPhysics!.myPlayer.PlayerId);
            if (myModPlayerCache?.AmOwner ?? false)
            {
                //自身のGamePlayerを入手したとき
                var actionButton = new ModAbilityButtonImpl(isLeftSideButton: true);
                actionButton.Register(NebulaGameManager.Instance);
                actionButton.SetSprite(buttonSprite.GetSprite());
                actionButton.Availability = (button) => !requiredAction;
                actionButton.Visibility = (button) => !myModPlayerCache.IsDead && ((CurrentFunctionalModHatCache ?? CurrentModHat)?.OnButton != null || (CurrentFunctionalModVisorCache ?? CurrentModVisor)?.OnButton != null);
                actionButton.OnClick = (button) =>
                {
                    requiredAction = true;
                };
                actionButton.SetLabel("costume");
            }
        }
        return myModPlayerCache;
    }
    public void RequireUpdateMaterial()
    {
        requiredUpdate = true;
    }
    public void LateUpdate()
    {
        bool isDead = IsDead;
        var modPlayer = GetModPlayer();

        if (MyLayer.hat != null) MyLayer.hat.transform.SetLocalZ(0f);

        //内部的なコスチュームの変化に追随する。
        if (MyLayer.hat != null && CurrentHat != MyLayer.hat.Hat)
        {
            CurrentHat = MyLayer.hat.Hat;
            MoreCosmic.AllHats.TryGetValue(MyLayer.hat.Hat.ProductId, out CurrentModHat);
            HatFrontIndex = HatBackIndex = 0;

            if (IsGamePlayer && CurrentModHat?.RelatedMarketplaceItem != null) MoreCosmic.RpcShareMarketplaceItem.Invoke((CurrentModHat!.RelatedMarketplaceItem!.EntryId, CurrentModHat!.RelatedMarketplaceItem!.Title));
        }

        if (MyLayer.visor != null && CurrentVisor != MyLayer.visor.visorData)
        {
            CurrentVisor = MyLayer.visor.visorData;
            MoreCosmic.AllVisors.TryGetValue(MyLayer.visor.visorData.ProductId, out CurrentModVisor);
            VisorIndex = VisorBackIndex = 0;

            if (IsGamePlayer && CurrentModVisor?.RelatedMarketplaceItem != null) MoreCosmic.RpcShareMarketplaceItem.Invoke((CurrentModVisor!.RelatedMarketplaceItem!.EntryId, CurrentModVisor!.RelatedMarketplaceItem!.Title));
        }

        //現在のアニメーションを取得する。
        LastAnimState = PlayerAnimState.Idle;
        bool flip = MyLayer.FlipX;

        if (MyAnimations)
        {
            var current = MyAnimations!.Animator.m_currAnim;
            if (current == MyAnimations!.group.ClimbUpAnim)
                LastAnimState = PlayerAnimState.ClimbUp;
            else if (current == MyAnimations!.group.ClimbDownAnim)
                LastAnimState = PlayerAnimState.ClimbDown;
            else if (current == MyAnimations!.group.RunAnim)
                LastAnimState = PlayerAnimState.Run;
            else if (current == MyAnimations!.group.EnterVentAnim)
                LastAnimState = PlayerAnimState.EnterVent;
            else if (current == MyAnimations!.group.ExitVentAnim)
                LastAnimState = PlayerAnimState.ExitVent;
        }

        void SetImage(ref CosmicImage? current, CosmicImage? normal, CosmicImage? flipped)
        {
            current = (flip ? flipped : normal) ?? normal ?? flipped ?? current;
        }

        void SetMaterial(Material material, Properties properties, params SpriteRenderer[] renderers)
        {
            renderers.Do(r =>
            {
                r.material = material;
                r.material.SetInt(MaskLayer, properties.MaskLayer);
                r.maskInteraction = properties.MaskType switch { MaskType.SimpleUI => SpriteMaskInteraction.VisibleInsideMask, MaskType.Exile => SpriteMaskInteraction.VisibleOutsideMask, _ => SpriteMaskInteraction.None };
            });
            if (properties.MaskLayer <= 0) renderers.Do(r => SetMaskLayerBasedOnLocalPlayer(r, properties.IsLocalPlayer));
        }


        T CheckAndUpdateCache<T>(T orig, ref T? cache, string? id, Func<string, string> productIdConverter, Func<string, T> itemProvider) where T : CustomCosmicItem
        {
            if (id != null)
            {
                if (id != cache?.Id) cache = itemProvider(productIdConverter(id));
                if (cache != null) return cache;
            }
            return orig;
        }

        //Modハット
        if (CurrentModHat != null)
        {
            //機能的なハットを得る
            var functionalHat = CurrentModHat;
            var functionalHatId = GetModPlayer()?.CurrentOutfit.HatArgument;
            functionalHat = CheckAndUpdateCache(functionalHat, ref CurrentFunctionalModHatCache, functionalHatId, CosmicHat.IdToProductId, pi => MoreCosmic.AllHats.TryGetValue(pi, out var h) ? h : null!);

            //見た目上のハットを得る
            var visualHat = functionalHat;
            string? visualHatId = null;
            if (ShipStatus.Instance && AmongUsUtil.InAnySab) visualHatId = (AmongUsUtil.InCommSab ? functionalHat.CommSabAlternative ?? functionalHat.SabotageAlternative : functionalHat.SabotageAlternative)?.Costume;
            if (MyLayer.bodyType == PlayerBodyTypes.Seeker && functionalHat.SeekerAlternative != null) visualHatId = functionalHat.SeekerAlternative?.Costume;
            if (isDead && functionalHat.GhostAlternative != null) visualHatId = functionalHat.GhostAlternative?.Costume;
            visualHat = CheckAndUpdateCache(visualHat, ref CurrentVisualModHatCache, visualHatId, CosmicHat.IdToProductId, pi => MoreCosmic.AllHats.TryGetValue(pi, out var h) ? h : null!);

            //表示する画像の選定
            CosmicImage? frontImage = null;
            CosmicImage? backImage = null;

            if (!visualHat.SeekerCostume && MyLayer.bodyType == PlayerBodyTypes.Seeker)
            {
                //シーカー着用不可かつ現在シーカーであるならばなにもしない
            }
            else
            {
                if (LastAnimState is not PlayerAnimState.ClimbUp and not PlayerAnimState.ClimbDown)
                {
                    SetImage(ref frontImage, visualHat.Main, visualHat.Flip);
                    SetImage(ref backImage, visualHat.Back, visualHat.BackFlip);
                }

                switch (LastAnimState)
                {
                    case PlayerAnimState.Run:
                        SetImage(ref frontImage, visualHat.Move, visualHat.MoveFlip);
                        SetImage(ref backImage, visualHat.MoveBack, visualHat.MoveBackFlip);
                        break;
                    case PlayerAnimState.ClimbUp:
                        SetImage(ref frontImage, visualHat.Climb, visualHat.ClimbFlip);
                        backImage = null;
                        break;
                    case PlayerAnimState.ClimbDown:
                        SetImage(ref frontImage, visualHat.Climb, visualHat.ClimbFlip);
                        SetImage(ref frontImage, visualHat.ClimbDown, visualHat.ClimbDownFlip);

                        SpriteAnimNodeSync? spriteAnimNodeSync = MyLayer.hat?.SpriteSyncNode ?? MyLayer.hat?.GetComponent<SpriteAnimNodeSync>();
                        if (spriteAnimNodeSync) spriteAnimNodeSync!.NodeId = 0;

                        backImage = null;
                        break;
                    case PlayerAnimState.EnterVent:
                        SetImage(ref frontImage, visualHat.EnterVent, visualHat.EnterVentFlip);
                        SetImage(ref backImage, visualHat.EnterVentBack, visualHat.EnterVentBackFlip);
                        break;
                    case PlayerAnimState.ExitVent:
                        SetImage(ref frontImage, visualHat.ExitVent, visualHat.ExitVentFlip);
                        SetImage(ref backImage, visualHat.ExitVentBack, visualHat.ExitVentBackFlip);
                        break;
                }
            }

            //タイマーの更新
            HatTimer -= Time.deltaTime;
            if (HatTimer < 0f)
            {
                HatTimer = 1f / (float)visualHat.GetFPS(HatFrontIndex, frontImage);
                HatBackIndex++;
                HatFrontIndex++;
            }

            //インデックスの調整
            HatFrontIndex %= frontImage?.GetLength() ?? 1;
            HatBackIndex %= backImage?.GetLength() ?? 1;
            if (lastHatFrontImage != frontImage && (frontImage?.RequirePlayFirstState ?? true)) HatFrontIndex = 0;
            if (lastHatBackImage != backImage && (backImage?.RequirePlayFirstState ?? true)) HatBackIndex = 0;
            if (isDead && !(visualHat?.DoAnimationIfDead ?? true)) HatFrontIndex = HatBackIndex = 0;

            lastHatFrontImage = frontImage;
            lastHatBackImage = backImage;


            MyLayer.hat!.FrontLayer.sprite = frontImage?.GetSprite(HatFrontIndex) ?? null;
            MyLayer.hat!.BackLayer.sprite = backImage?.GetSprite(HatBackIndex) ?? null;

            MyLayer.hat!.FrontLayer.enabled = true;
            MyLayer.hat!.BackLayer.enabled = true;

            //追加レイヤー
            var frontHasExImage = frontImage?.HasExImage ?? false;
            hatFrontExRenderer!.gameObject.SetActive(frontHasExImage);
            if (frontHasExImage) hatFrontExRenderer.sprite = frontImage?.GetExSprite(HatFrontIndex) ?? null;

            var backHasExImage = backImage?.HasExImage ?? false;
            hatBackExRenderer!.gameObject.SetActive(backHasExImage);
            if (backHasExImage) hatBackExRenderer.sprite = backImage?.GetExSprite(HatBackIndex) ?? null;

            /*
            var hatHasMask = frontImage?.HasMaskImage ?? false;
            bodyMaskByHat!.gameObject.SetActive(hatHasMask);
            if (hatHasMask)
            {
                bodyMaskByHat.sprite = frontImage?.GetMaskSprite(HatFrontIndex);
                //MyLayer.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }
            */

            if (ZOrdering)
            {
                MyLayer.hat!.FrontLayer.transform.SetLocalZ(MyLayer.zIndexSpacing * (visualHat.IsSkinny ? -1f : -3f));
                MyLayer.hat!.BackLayer.transform.SetLocalZ(MyLayer.zIndexSpacing * 1f);
                hatFrontExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * (frontImage?.ExIsFront ?? false ? -0.125f : 0.125f));
                hatBackExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * (backImage?.ExIsFront ?? false ? -0.125f : 0.125f));
            }
            else
            {
                Order.HatFront = visualHat?.IsSkinny ?? false ? CosmeticsOrder.HatFrontSkinny : CosmeticsOrder.HatFrontDefault;
                Order.HatFrontExDiff = frontImage?.ExIsFront ?? false ? 1 : -1;
                Order.HatBack = CosmeticsOrder.HatBackDefault;
                Order.HatBackExDiff = backImage?.ExIsFront ?? false ? 1 : -1;
            }
        }
        else if (MyLayer.hat != null)
        {
            if (ZOrdering)
            {
                Order.ResetHatToDefault();
            }
            else
            {
                MyLayer.hat!.FrontLayer.transform.SetLocalZ(MyLayer.zIndexSpacing * -3f);
            }

            hatFrontExRenderer!.gameObject.SetActive(false);
            hatBackExRenderer!.gameObject.SetActive(false);
        }

        CosmicVisor? currentVisualVisor = null;
        if (CurrentModVisor != null)
        {
            //機能的なバイザーを得る
            var functionalVisor = CurrentModVisor;
            var functionalVisorId = GetModPlayer()?.CurrentOutfit.VisorArgument;
            functionalVisor = CheckAndUpdateCache(functionalVisor, ref CurrentFunctionalModVisorCache, functionalVisorId, CosmicVisor.IdToProductId, pi => MoreCosmic.AllVisors.TryGetValue(pi, out var v) ? v : null!);

            //見た目上のバイザーを得る
            var visualVisor = functionalVisor;
            string? visualVisorId = null;
            if (ShipStatus.Instance && AmongUsUtil.InAnySab) visualVisorId = (AmongUsUtil.InCommSab ? functionalVisor.CommSabAlternative ?? functionalVisor.SabotageAlternative : functionalVisor.SabotageAlternative)?.Costume;
            if (isDead && functionalVisor.GhostAlternative != null) visualVisorId = functionalVisor.GhostAlternative?.Costume;
            visualVisor = CheckAndUpdateCache(visualVisor, ref CurrentVisualModVisorCache, visualVisorId, CosmicVisor.IdToProductId, pi => MoreCosmic.AllVisors.TryGetValue(pi, out var v) ? v : null!);
            currentVisualVisor = visualVisor;

            //表示する画像の選定
            CosmicImage? image = null;
            CosmicImage? backImage = null;

            if (LastAnimState is not PlayerAnimState.ClimbUp and not PlayerAnimState.ClimbDown)
            {
                SetImage(ref image, visualVisor.Main, visualVisor.Flip);
                SetImage(ref backImage, visualVisor.Back, visualVisor.BackFlip);
            }
            switch (LastAnimState)
            {
                case PlayerAnimState.Run:
                    SetImage(ref image, visualVisor.Move, visualVisor.MoveFlip);
                    SetImage(ref backImage, visualVisor.MoveBack, visualVisor.MoveBackFlip);
                    break;
                case PlayerAnimState.EnterVent:
                    SetImage(ref image, visualVisor.EnterVent, visualVisor.EnterVentFlip);
                    SetImage(ref backImage, visualVisor.EnterVentBack, visualVisor.EnterVentBackFlip);
                    break;
                case PlayerAnimState.ExitVent:
                    SetImage(ref image, visualVisor.ExitVent, visualVisor.ExitVentFlip);
                    SetImage(ref backImage, visualVisor.ExitVentBack, visualVisor.ExitVentBackFlip);
                    break;
                case PlayerAnimState.ClimbUp:
                    SetImage(ref image, visualVisor.Climb, visualVisor.ClimbFlip);
                    break;
                case PlayerAnimState.ClimbDown:
                    SetImage(ref image, visualVisor.Climb, visualVisor.ClimbFlip);
                    SetImage(ref image, visualVisor.ClimbDown, visualVisor.ClimbDownFlip);
                    break;
            }

            //タイマーの更新
            VisorTimer -= Time.deltaTime;
            if (VisorTimer < 0f)
            {
                VisorTimer = 1f / (float)visualVisor.GetFPS(VisorIndex, image);
                VisorIndex++;
                VisorBackIndex++;
            }

            //インデックスの調整
            VisorIndex %= image?.GetLength() ?? 1;
            VisorBackIndex %= backImage?.GetLength() ?? 1;
            if (lastVisorImage != image && (image?.RequirePlayFirstState ?? true)) VisorIndex = 0;
            if (lastVisorBackImage != backImage && (backImage?.RequirePlayFirstState ?? true)) VisorBackIndex = 0;
            if (isDead && !(visualVisor?.DoAnimationIfDead ?? true)) VisorIndex = VisorBackIndex = 0;
            lastVisorImage = image;
            lastVisorBackImage = backImage;

            MyLayer.visor!.Image.sprite = image?.GetSprite(VisorIndex) ?? null;
            visorBackRenderer.gameObject.SetActive(true);
            visorBackRenderer.sprite = backImage?.GetSprite(VisorBackIndex) ?? null;

            //追加レイヤー
            var frontHasExImage = image?.HasExImage ?? false;
            visorFrontExRenderer!.gameObject.SetActive(frontHasExImage);
            visorFrontExRenderer.sprite = image?.GetExSprite(VisorIndex) ?? null;

            var backHasExImage = backImage?.HasExImage ?? false;
            visorBackExRenderer!.gameObject.SetActive(backImage?.HasExImage ?? false);
            visorBackExRenderer.sprite = backImage?.GetExSprite(VisorBackIndex) ?? null;

            /*
            var visorHasMask = image?.HasMaskImage ?? false;
            bodyMaskByVisor!.gameObject.SetActive(visorHasMask);
            if (visorHasMask)
            {
                bodyMaskByVisor.sprite = image?.GetMaskSprite(VisorIndex);
                //MyLayer.currentBodySprite.BodySprite.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
            }
            */

            if (ZOrdering)
            {
                float frontZ = MyLayer.zIndexSpacing * (visualVisor!.BehindHat ? -2f : -4f);
                MyLayer.visor!.Image.transform.SetLocalZ(frontZ);
                visorBackRenderer.transform.SetLocalZ(-frontZ + MyLayer.zIndexSpacing * (visualVisor.BackmostBack ? 1.5f : 0.5f)); //背景は前面の子なので、位置関係の計算に注意

                visorFrontExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * (image?.ExIsFront ?? false ? -0.125f : 0.125f));
                visorBackExRenderer.transform.SetLocalZ(MyLayer.zIndexSpacing * (backImage?.ExIsFront ?? false ? -0.125f : 0.125f));
            }
            else
            {
                Order.VisorFront = visualVisor!.BehindHat ? CosmeticsOrder.VisorFrontBehindHat : CosmeticsOrder.VisorFrontDefault;
                Order.VisorFrontExDiff = image?.ExIsFront ?? false ? 1 : -1;
                Order.VisorBack = visualVisor!.BackmostBack ? CosmeticsOrder.VisorBackBackmost : CosmeticsOrder.VisorBackDefault;
                Order.VisorBackExDiff = backImage?.ExIsFront ?? false ? 1 : -1;
            }
        }
        else if (MyLayer.visor != null)
        {
            MyLayer.visor!.Image.transform.SetLocalZ(MyLayer.zIndexSpacing * (MyLayer.visor.visorData?.behindHats ?? false ? -2f : -4f));

            visorBackRenderer!.gameObject.SetActive(false);
            visorFrontExRenderer!.gameObject.SetActive(false);
            visorBackExRenderer!.gameObject.SetActive(false);
        }

        var shouldUseDefault = !(MyLayer.bodyMatProperties.MaskType is MaskType.ComplexUI or MaskType.ScrollingUI);
        var shouldUsePlayerVisor = currentVisualVisor?.Adaptive ?? false;
        if (shouldUseDefault != useDefaultShader || shouldUsePlayerVisor != usePlayerShaderOnVisor || requiredUpdate)
        {
            useDefaultShader = shouldUseDefault;
            usePlayerShaderOnVisor = shouldUsePlayerVisor;
            requiredUpdate = false;

            Material exShader = shouldUseDefault ? HatManager.Instance.DefaultShader : HatManager.Instance.MaskedMaterial;
            Material visorShader = shouldUsePlayerVisor ? shouldUseDefault ? HatManager.Instance.PlayerMaterial : HatManager.Instance.MaskedPlayerMaterial : exShader;

            if (MyLayer.hat != null) SetMaterial(exShader, MyLayer.hat.matProperties, [hatFrontExRenderer!, hatBackExRenderer!]);
            if (MyLayer.visor != null)
            {
                SetMaterial(exShader, MyLayer.visor.matProperties, [visorFrontExRenderer!, visorBackExRenderer!]);
                SetMaterial(visorShader, MyLayer.visor.matProperties, [visorBackRenderer!]);
                SetColors(MyLayer.ColorId, visorBackRenderer);
            }
        }

        AdditionalRenderers().Do(r => r.enabled = MyLayer.visible);
        AdditionalRenderers().Do(r => r.flipX = MyLayer.FlipX);

        if (requiredAction)
        {
            requiredAction = false;
            using (RPCRouter.CreateSection("CostumeAction"))
            {
                var hatAction = (CurrentFunctionalModHatCache ?? CurrentModHat)?.OnButton;
                if (hatAction != null) MoreCosmic.RpcUpdateHatArgument(modPlayer!.CurrentOutfit.Id, hatAction.Costume);

                var visorAction = (CurrentFunctionalModVisorCache ?? CurrentModVisor)?.OnButton;
                if (visorAction != null) MoreCosmic.RpcUpdateVisorArgument(modPlayer!.CurrentOutfit.Id, visorAction.Costume);
            }
        }


        UpdateZ();
    }

    void UpdateZ()
    {
        if (!ZOrdering)
        {
            MyLayer.currentBodySprite.BodySprite.SetBothOrder(Order.Body);
            MyLayer.skin.layer.SetBothOrder(Order.Skin);
            MyLayer.hat.FrontLayer.SetBothOrder(Order.HatFront);
            hatFrontExRenderer?.SetBothOrder(Order.HatFrontEx);
            MyLayer.hat.BackLayer.SetBothOrder(Order.HatBack);
            hatBackExRenderer?.SetBothOrder(Order.HatBackEx);
            MyLayer.visor.Image.SetBothOrder(Order.VisorFront);
            visorFrontExRenderer?.SetBothOrder(Order.VisorFrontEx);
            visorBackRenderer?.SetBothOrder(Order.VisorBack);
            visorBackExRenderer?.SetBothOrder(Order.VisorBackEx);
        }
        else if (ShouldSort)
        {
            if (renderersCache == null) renderersCache = transform.GetComponentsInChildren<Renderer>().Concat(AdditionalRenderers()).ToArray();
            if (bodyRenderersCache == null) bodyRenderersCache = MyLayer.bodySprites.ToArray().Select(r => r.BodySprite.CastFast<Renderer>()).ToArray();

            foreach (var renderer in renderersCache)
            {
                var z = 0f;
                var t = renderer.transform;
                if (t.GetInstanceID() != transform.GetInstanceID())
                {
                    z -= t.localPosition.z;
                    t = t.parent;
                    if (t == null) break;
                }

                int order = (int)(z * SortingScale) + SortingBaseOrder;
                renderer.sortingOrder = order;
                renderer.sortingGroupOrder = order;
            }

            foreach (var renderer in bodyRenderersCache)
            {
                int order = SortingBaseOrder;
                renderer.sortingOrder = order;
                renderer.sortingGroupOrder = order;
            }
        }
    }

    public void SetSortingProperty(bool shouldSort, float sortingScale, int sortingBaseOrder)
    {
        ShouldSort = shouldSort;
        SortingScale = sortingScale;
        SortingBaseOrder = sortingBaseOrder;
    }

    public void FixVisor()
    {
        if (CurrentModVisor?.Fixed ?? false)
        {
            if (LastAnimState is PlayerAnimState.EnterVent or PlayerAnimState.ExitVent) return;
            var z = MyLayer.visor.transform.localPosition.z;
            MyLayer.visor.transform.localPosition = new(MyLayer.FlipX ? 0.04f : -0.04f, 0.575f, z);
        }
    }
}


[HarmonyPatch(typeof(SpriteAnimNodeSync), nameof(SpriteAnimNodeSync.LateUpdate))]
public class SpriteAnimNodeSyncUpdatePatch
{
    public static void Postfix(SpriteAnimNodeSync __instance)
    {
        if (__instance.gameObject.TryGetComponent<NebulaCosmeticsLayerVisorLink>(out var layer))
        {
            layer.NebulaLayer.Value?.FixVisor();
        }
    }
}

[HarmonyPatch]
public static class TabEnablePatch
{
    public static TMP_Text textTemplate = null!;

    private static float headerSize = 0.8f;
    private static float headerX = 0.85f;
    private static float inventoryZ = -2f;

    private static List<TMP_Text> customTexts = [];

    private static IDividedSpriteLoader additionalIconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CostumeIcon.png", 100f, 2, 1);

    public static void SetUpTab<ItemTab, VanillaItem, ModItem>(ItemTab __instance, VanillaItem emptyItem, (VanillaItem, ModItem?)[] items, Func<VanillaItem> defaultProvider, Action<VanillaItem> selector, Action<VanillaItem, ColorChip>? chipSetter = null) where ItemTab : InventoryTab where ModItem : CustomCosmicItem where VanillaItem : CosmeticData
    {
        Helpers.RefreshMemory();

        textTemplate = __instance.transform.FindChild("Text").gameObject.GetComponent<TMP_Text>();

        var groups = items.GroupBy((tuple) => tuple.Item2?.Package ?? "InnerSloth").OrderBy(group => MoreCosmic.AllPackages.TryGetValue(group.Key, out var package) ? package.Priority : 10000);

        foreach (var text in customTexts) if (text) UnityEngine.Object.Destroy(text.gameObject);
        foreach (var chip in __instance.ColorChips) if (chip) UnityEngine.Object.Destroy(chip.gameObject);
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

        int colorId = __instance.HasLocalPlayer() ? PlayerControl.LocalPlayer.Data.DefaultOutfit.ColorId : DataManager.Player.Customization.Color;

        List<IEnumerator> loader = [];

        foreach (var group in groups)
        {
            TMP_Text title = UnityEngine.Object.Instantiate(textTemplate, __instance.scroller.Inner);
            title.GetComponent<TextTranslatorTMP>().enabled = false;
            var mat = title.GetComponent<MeshRenderer>().material;
            mat.SetFloat("_StencilComp", 4f);
            mat.SetFloat("_Stencil", 1f);

            title.transform.parent = __instance.scroller.Inner;
            title.transform.localPosition = new Vector3(headerX, y, inventoryZ);
            title.text = MoreCosmic.AllPackages.TryGetValue(group.Key, out var package) ? package.DisplayName : group.Key;
            title.alignment = TextAlignmentOptions.Center;
            title.fontSize = 5f;
            title.fontWeight = FontWeight.Thin;
            title.enableAutoSizing = false;
            title.autoSizeTextContainer = true;
            y -= headerSize * __instance.YOffset;
            customTexts.Add(title);


            int index = 0;
            float yInOffset = 0;
            foreach (var item in group.Prepend((emptyItem, null)))
            {
                VanillaItem vanillaItem = item.Item1;
                ModItem? modItem = item.Item2;
                if (index != 0 && vanillaItem == emptyItem) continue;

                yInOffset = index / __instance.NumPerRow * __instance.YOffset;
                float itemX = __instance.XRange.Lerp(index % __instance.NumPerRow / (__instance.NumPerRow - 1f));
                float itemY = y - yInOffset;

                ColorChip colorChip = UnityEngine.Object.Instantiate(__instance.ColorTabPrefab, __instance.scroller.Inner);
                colorChip.transform.localPosition = new Vector3(itemX, itemY, -1f);

                colorChip.Button.OnMouseOver.AddListener(() => selector.Invoke(vanillaItem));
                colorChip.Button.OnMouseOut.AddListener(() => selector.Invoke(defaultProvider.Invoke()));
                colorChip.Button.OnClick.AddListener(__instance.ClickEquip);

                if (DebugTools.ShowCostumeMetadata)
                {
                    IEnumerable<string>? tags = modItem != null ? modItem.Tags : MoreCosmic.VanillaTags.TryGetValue(vanillaItem.ProductId, out var t) ? t : [];
                    string name = modItem != null ? modItem.Name : vanillaItem.name;
                    colorChip.Button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(colorChip.Button, name.Bold() + "<br>" + string.Join("<br>", (tags ?? []).Select(text => "  " + text))));
                    colorChip.Button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(colorChip.Button));
                    var extraButton = colorChip.Button.gameObject.AddComponent<ExtraPassiveBehaviour>();
                    extraButton.OnRightClicked = () =>
                    {
                        ClipboardHelper.PutClipboardString(name);
                        DebugScreen.Push("クリップボードにコピーしました。", 3f);
                    };
                }

                colorChip.Button.ClickMask = __instance.scroller.Hitbox;

                colorChip.ProductId = vanillaItem.ProductId;


                void SetItemPreview()
                {

                    if (chipSetter == null)
                    {
                        colorChip.Inner.SetMaskType(MaskType.ScrollingUI);
                        __instance.UpdateMaterials(colorChip.Inner.FrontLayer, vanillaItem);
                        vanillaItem.SetPreview(colorChip.Inner.FrontLayer, colorId);

                        var previewAdditionalSprite = modItem?.PreviewAdditionalSprite;
                        if (previewAdditionalSprite != null)
                        {
                            var ex = UnityEngine.Object.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                            ex.sharedMaterial = HatManager.Instance.DefaultShader;
                            ex.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            ex.sprite = previewAdditionalSprite;
                            ex.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f, 0f, modItem!.PreviewAdditionalInFront ? -0.1f : 0.1f);
                        }

                        if (modItem is CosmicHat mHat && mHat.Preview == null && mHat.Main != null)
                        {
                            if (mHat.Main.HasExImage)
                            {
                                var exFront = UnityEngine.Object.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                                exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                                exFront.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                exFront.sprite = mHat.Main.GetExSprite(0);
                                exFront.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f, 0f, mHat.Main.ExIsFront ? -0.1f : 0.1f);
                            }


                            if (mHat.Back != null)
                            {
                                __instance.UpdateMaterials(colorChip.Inner.BackLayer, vanillaItem);
                                colorChip.Inner.BackLayer.sprite = mHat.Back.GetSprite(0)!;
                                if (Application.isPlaying) SetColors(colorId, colorChip.Inner.BackLayer);

                                if (mHat.Back.HasExImage)
                                {
                                    var exBack = UnityEngine.Object.Instantiate(colorChip.Inner.BackLayer, colorChip.Inner.BackLayer.transform.parent);
                                    exBack.sharedMaterial = HatManager.Instance.DefaultShader;
                                    exBack.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                    exBack.sprite = mHat.Back.GetExSprite(0);
                                    exBack.transform.localPosition = colorChip.Inner.BackLayer.transform.localPosition + new Vector3(0f, 0f, mHat.Back.ExIsFront ? -0.1f : 0.1f);
                                }
                            }
                        }
                        else if (modItem is CosmicVisor mVisor && mVisor.Preview == null && mVisor.Main != null)
                        {
                            if (mVisor.Main.HasExImage)
                            {
                                var exFront = UnityEngine.Object.Instantiate(colorChip.Inner.FrontLayer, colorChip.Inner.FrontLayer.transform.parent);
                                exFront.sharedMaterial = HatManager.Instance.DefaultShader;
                                exFront.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                exFront.sprite = mVisor.Main.GetExSprite(0);
                                exFront.transform.localPosition = colorChip.Inner.FrontLayer.transform.localPosition + new Vector3(0f, 0f, mVisor.Main.ExIsFront ? -0.1f : 0.1f);
                            }


                            if (mVisor.Back != null)
                            {
                                __instance.UpdateMaterials(colorChip.Inner.BackLayer, vanillaItem);
                                colorChip.Inner.BackLayer.sprite = mVisor.Back.GetSprite(0)!;
                                if (Application.isPlaying) SetColors(colorId, colorChip.Inner.BackLayer);

                                if (mVisor.Back.HasExImage)
                                {
                                    var exBack = UnityEngine.Object.Instantiate(colorChip.Inner.BackLayer, colorChip.Inner.BackLayer.transform.parent);
                                    exBack.sharedMaterial = HatManager.Instance.DefaultShader;
                                    exBack.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                                    exBack.sprite = mVisor.Back.GetExSprite(0);
                                    exBack.transform.localPosition = colorChip.Inner.BackLayer.transform.localPosition + new Vector3(0f, 0f, mVisor.Back.ExIsFront ? -0.1f : 0.1f);
                                }
                            }
                        }
                    }
                    else
                    {
                        chipSetter.Invoke(vanillaItem, colorChip);
                    }

                    try
                    {
                        colorChip.Inner.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        colorChip.Inner.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    }
                    catch (Exception e) { }
                }

                if (modItem != null)
                {
                    loader.Add(modItem.LoadForPreview(() => SetItemPreview()));
                }
                else
                {
                    loader.Add(ManagedEffects.Action(() => SetItemPreview()));
                }

                int iconNum = 0;
                void AddIcon(int iconIndex)
                {
                    GameObject obj = new("Mark");
                    obj.transform.SetParent(colorChip.transform);
                    obj.layer = colorChip.gameObject.layer;
                    obj.transform.localPosition = new Vector3(-0.42f, 0.39f - 0.22f * iconNum++, -10f);
                    SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
                    renderer.sprite = additionalIconSprite.GetSprite(iconIndex);
                    renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }

                if (modItem?.HasAnimation ?? false) AddIcon(0);
                if (modItem?.OnButton != null) AddIcon(1);

                colorChip.Tag = vanillaItem;
                colorChip.SelectionHighlight.gameObject.SetActive(false);
                __instance.ColorChips.Add(colorChip);

                try
                {
                    colorChip.Inner.BackLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    colorChip.Inner.FrontLayer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                }
                catch (Exception e) { }

                index++;
            }

            y -= yInOffset + __instance.YOffset;
        }

        __instance.scroller.ContentYBounds.max = -(y - __instance.YStart) - 3.5f;
        __instance.scroller.UpdateScrollBars();

        __instance.StartCoroutine(ManagedEffects.Sequence([ManagedEffects.Wait(0.1f), ManagedEffects.BeamSequence(5, loader.ToArray())]).WrapToIl2Cpp());
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
                (item, chip) =>
                {
                    __instance.StartCoroutine(__instance.CoLoadAssetAsync(item.Cast<IAddressableAssetProvider<NamePlateViewData>>(), (Il2CppSystem.Action<NamePlateViewData>)((viewData) =>
                    {
                        var plateChip = chip.CastFast<NameplateChip>().image;
                        plateChip.sprite = viewData != null ? viewData.Image : null;

                        if (MoreCosmic.AllNameplates.TryGetValue(item.ProductId, out var mPlate))
                        {
                            var adaptiveChip = UnityEngine.Object.Instantiate(plateChip, plateChip.transform.parent);
                            UnityEngine.Object.Destroy(adaptiveChip.GetComponent<PassiveButton>());
                            UnityEngine.Object.Destroy(adaptiveChip.GetComponent<CircleCollider2D>());
                            adaptiveChip.sprite = mPlate.Adaptive?.GetSprite(0);
                            adaptiveChip.transform.localPosition = plateChip.transform.localPosition + new Vector3(0f, 0f, mPlate.AdaptiveInFront ? -0.1f : 0.1f);
                            adaptiveChip.transform.localScale = plateChip.transform.localScale;
                            adaptiveChip.material = HatManager.Instance.PlayerMaterial;
                            adaptiveChip.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            SetColors(NebulaPlayerTab.PreviewColorId, adaptiveChip);
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
                    SetColors(color, renderer);
                    return false;
                }
                if (__instance.TryCast<VisorData>() != null && MoreCosmic.AllVisors.TryGetValue(__instance.ProductId, out var visor))
                {
                    renderer.sprite = visor.PreviewSprite;
                    SetColors(color, renderer);
                    return false;
                }
                if (__instance.TryCast<NamePlateData>() != null && MoreCosmic.AllNameplates.TryGetValue(__instance.ProductId, out var nameplate))
                {
                    renderer.sprite = nameplate.PreviewSprite;
                    SetColors(color, renderer);
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
            if (!__instance.gameObject.TryGetComponent<NebulaNameplate>(out var nebulaPlate))
                nebulaPlate = __instance.gameObject.AddComponent<NebulaNameplate>();
            else nebulaPlate.UpdateColor();

            var plate = HatManager.Instance.GetNamePlateById(plateID);
            if (plate != null && MoreCosmic.AllNameplates.TryGetValue(plate.ProductId, out var mPlate))
            {
                nebulaPlate.AdaptiveRenderer.sprite = mPlate.Adaptive?.GetSprite(0);
                nebulaPlate.AdaptiveRenderer.transform.localPosition = new Vector3(0, 0, mPlate.AdaptiveInFront ? -0.1f : 0.1f);
                SetColors(__instance.TargetPlayerId, nebulaPlate.AdaptiveRenderer);

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
            var simpleNameplateOption = GetValue(ClientOptionType.SimpleNameplate);
            if (simpleNameplateOption == 1)
            {
                __instance.Background.sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate("nameplate_NoPlate").Image;
                return;
            }
            else
            {
                __instance.Background.sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate(playerInfo.DefaultOutfit.NamePlateId).Image;
            }

            if (!__instance.gameObject.TryGetComponent<NebulaNameplate>(out var nebulaPlate))
                nebulaPlate = __instance.gameObject.AddComponent<NebulaNameplate>();

            var plate = HatManager.Instance.GetNamePlateById(playerInfo.DefaultOutfit.NamePlateId);
            if (plate != null && MoreCosmic.AllNameplates.TryGetValue(plate.ProductId, out var mPlate))
            {
                nebulaPlate.AdaptiveRenderer.sprite = mPlate.Adaptive?.GetSprite(0);
                nebulaPlate.AdaptiveRenderer.transform.localPosition = new Vector3(0, 0, mPlate.AdaptiveInFront ? -0.1f : 0.1f);
                SetColors(playerInfo.PlayerId, nebulaPlate.AdaptiveRenderer);
            }
            else
            {
                nebulaPlate.AdaptiveRenderer.sprite = null;
            }

        }
    }
}