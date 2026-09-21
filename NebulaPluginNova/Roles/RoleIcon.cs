using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.TextCore.Text;
using Virial.Assignable;
using Virial.Runtime;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;

namespace Nebula.Roles;

public enum TextIcon
{
    Leaf,
    Heart,
    Caution,
    Info,
}

static public class RoleIcon
{
    static private Dictionary<string, Image> imageCache = [];
    static public Image? GetRoleIcon(this DefinedAssignable assignable)
    {
        if (assignable == null) return null;
        var image = assignable.IconImage;
        if(image != null) return image;

        var internalName = assignable.InternalName;
        if (!imageCache.TryGetValue(internalName, out var loader))
        {
            Image? alternativeImage = null;
            if (assignable is DefinedRole role)
            {
                alternativeImage = role.Category switch
                {
                    RoleCategory.CrewmateRole => new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/crewmate.png"),
                    RoleCategory.ImpostorRole => new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/impostor.png"),
                    RoleCategory.NeutralRole => new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/neutral.png"),
                    _ => null
                };
            }
            else if (assignable is DefinedModifier)
            {
                alternativeImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/modifier.png");
            }
            else if (assignable is DefinedGhostRole)
            {
                alternativeImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/ghostRole.png");
            }

            loader = alternativeImage != null ? new NebulaSpriteLoaderWithDefault($"Assets/NebulaAssets/Sprites/Icons/{internalName}.png", alternativeImage) : new NebulaSpriteLoader($"Assets/NebulaAssets/Sprites/Icons/{internalName}.png");
            imageCache[internalName] = loader;
        }
        return loader;
    }

    static public Material GetRoleIconMaterial(DefinedAssignable? assignable, float outline, float? whiteLevel = null)
    {
        var colorTuple = assignable?.IconColor;
        return GetRoleIconMaterial(colorTuple?.mainColor.ToUnityColor() ?? assignable?.UnityColor ?? Color.white, colorTuple?.subColor?.ToUnityColor() ?? Color.white, outline, whiteLevel ?? 0.2f);
    }


    static private Material GetRoleIconMaterial(Color color, Color subColor, float outline, float whiteLevel)
    {
        outline = 1.07f - outline;
        var mat = new Material(NebulaAsset.RoleIconShader);
        mat.SetColor("_RedTo", Color.Lerp(color, Color.white, whiteLevel));
        mat.SetColor("_GreenTo", Color.Lerp(subColor, Color.white, whiteLevel));
        mat.SetFloat("_Outline", outline);
        return mat;
    }

    static public string GetRoleIconTag(this DefinedAssignable assignable, bool masked = false, int size = 100) => GetRoleIconTag(assignable, null, masked, size);
    static public string GetRoleIconTag(this DefinedAssignable assignable, AssignmentType? assignmentType, bool masked = false, int size = 100)
    {
        var tag = RuntimeSpriteGenerator.SpriteTagFromAssignable(assignable, masked, assignmentType);
        if (size == 100) return tag;
        return (tag).Sized(size);
    }

    static public string GetRoleIconTagSmall(this DefinedAssignable assignable, bool masked = false) => GetRoleIconTag(assignable, masked, 70);

    static public string GetRoleIconTag(this RuntimeAssignable runtime, bool masked = false, int size = 100)
    {
        var overridden = runtime.OverriddenRoleIcon;
        if (overridden.HasValue && RuntimeSpriteGenerator.TryGetIconOwner(overridden.Value.image, out var disguised))
            return disguised.GetRoleIconTag(masked, size);

        return runtime.Assignable.GetRoleIconTag(masked, size);
    }

    static public string GetRoleIconTagSmall(this RuntimeAssignable runtime, bool masked = false) => GetRoleIconTag(runtime, masked, 70);

    static public string GetTextIconTag(this TextIcon icon) => RuntimeSpriteGenerator.TextIconTag(icon);

    static public void UseRoleIcon(this TMPro.TextMeshPro text) => text.spriteAsset = RuntimeSpriteGenerator.SpriteAsset;
    static public void UseMaskedRoleIcon(this TMPro.TextMeshPro text) => text.spriteAsset = RuntimeSpriteGenerator.MaskedAsset;

    [NebulaPreprocess(PreprocessPhase.PostFixStructure)]
    public static class RuntimeSpriteGenerator
    {
        const float iconOutlineWidth = 0.45f;
        static private void Preprocess(NebulaPreprocessor preprocessor)
        {
            var assignables = Roles.AllAssignables().ToArray();

            foreach (var a in assignables)
            {
                var icon = a.GetRoleIcon();
                if (icon != null) iconOwners.TryAdd(icon, a);
            }

            CreateSpriteAsset(assignables.Select(a => (a.GetRoleIcon()?.GetSprite(), GetRoleIconMaterial(a, iconOutlineWidth, 0f), a.InternalName, a)).ToArray()!);
            AddTextIcons();
            SpriteAsset.MarkDontUnload();
        }

        private const int TextIconUnicode = 0xf1000;
        static private readonly string[] textIconNames = Enum.GetNames(typeof(TextIcon)).Select(name => name.ToLowerInvariant()).ToArray();

        static public string TextIconTag(TextIcon icon) => $"<sprite name=\"{textIconNames[(int)icon]}\">";

        static private void AddTextIcons()
        {
            var texture = GraphicsHelper.LoadTextureFromResources("Nebula.Resources.TextIcons.png");
            if (!texture.AsBoolFast()) return;

            texture.name = "TextIcons";
            texture.MarkDontUnload();

            SpriteAsset.fallbackSpriteAssets.Add(BuildTextIconAsset(texture, new Material(Shader.Find("Sprites/Default")), "NoSTextIcons"));
            MaskedAsset.fallbackSpriteAssets.Add(BuildTextIconAsset(texture, UnityHelper.GetMeshRendererMaskedMaterial(), "NoSMaskedTextIcons"));
        }

        static private TMP_SpriteAsset BuildTextIconAsset(Texture2D texture, Material material, string name)
        {
            material.mainTexture = texture;
            material.MarkDontUnload();

            var size = texture.height;

            var glyphList = new List<TMP_SpriteGlyph>();
            var characterList = new List<TMP_SpriteCharacter>();
            var infoList = new List<TMP_Sprite>();

            for (int i = 0; i < textIconNames.Length; i++)
            {
                var rectX = i * size;

                TMP_SpriteGlyph glyph = new();
                glyph.index = (uint)i;
                glyph.glyphRect = new(rectX, 0, size, size);
                glyph.metrics = new(size, size, 0f, size * 0.8f, size);
                glyphList.Add(glyph);

                TMP_SpriteCharacter character = new((uint)(TextIconUnicode + i), glyph);
                character.name = textIconNames[i];
                character.glyphIndex = glyph.index;
                character.scale = 1.05f;
                characterList.Add(character);

                infoList.Add(new() { x = rectX, y = 0f, width = size, height = size, id = i, pivot = new(0.5f, 0.5f), xAdvance = size, xOffset = 0f, yOffset = size * 0.8f, scale = 1.05f, name = textIconNames[i], hashCode = i, unicode = TextIconUnicode + i });
            }

            var asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name = name;
            asset.material = material;
            asset.spriteSheet = texture;
            asset.spriteGlyphTable = glyphList.ToIl2CppList();
            asset.spriteCharacterTable = characterList.ToIl2CppList();
            asset.spriteInfoList = infoList.ToIl2CppList();

            try
            {
                asset.UpdateLookupTables();

                for (int i = 0; i < asset.spriteCharacterTable.Count; i++)
                {
                    asset.spriteCharacterTable[i].glyphIndex = (uint)i;
                    asset.spriteCharacterTable[i].glyph = asset.spriteGlyphTable[i];
                }
            }
            catch (Exception e)
            {
                LogUtils.WriteToConsole(e.ToString());
            }

            return asset;
        }


        // 撮影用のカメラとRenderTextureの設定
        static private Vector2Int imageSize = new Vector2Int(64, 64);
        private const int ImagePerLines = 20;
        private const int ImageLines = 20;

        static private Dictionary<string, int> idMap = [];
        static public string SpriteTagFromAssignable(DefinedAssignable assignable, bool masked, AssignmentType? type) => masked ? $"<sprite name=\"masked_{(type != null ? type.Postfix + "_" : "")}{assignable.InternalName}\">" : $"<sprite name=\"{(type != null ? type.Postfix + "_" : "")}{assignable.InternalName}\">";

        /// <summary>
        /// スプライトタグの名前から、アトラス上の位置と元の役職を引く対応表。マスク版は含まない。
        /// </summary>
        /// <remarks>
        /// 閲覧画面へアイコンを配るために使う。<see cref="Atlases"/> と組で意味を成す。
        /// 役職そのものを持たせてあるので、表示名は引く時点の言語で取れる。
        /// </remarks>
        static public IReadOnlyDictionary<string, (int sheet, int cell, DefinedAssignable assignable)> IconMap => iconMap;
        static private readonly Dictionary<string, (int sheet, int cell, DefinedAssignable assignable)> iconMap = [];

        /// <summary>シートの名前。0 番が通常、以降は割り当て種別ごとの色違い。</summary>
        static public IReadOnlyList<string> SheetNames => sheetNames;
        static private readonly List<string> sheetNames = [];

        /// <summary>シートごとのアトラス。<see cref="SheetNames"/> と同じ並び。</summary>
        static public IReadOnlyList<Texture2D> Atlases => atlases;
        static private readonly List<Texture2D> atlases = [];

        /// <summary>アトラス 1 マスの大きさ（ピクセル）。</summary>
        static public int IconCellSize => imageSize.x;
        static public int IconColumns => ImagePerLines;
        static public int IconRows => ImageLines;

        /// <summary>通常シートの名前。割り当て種別が付かない役職はここに入る。</summary>
        public const string DefaultSheetName = "default";

        /// <summary>アイコンの画像 → その画像を自分のものとして持つ役職。</summary>
        static private readonly Dictionary<Image, DefinedAssignable> iconOwners = [];

        /// <summary>その画像を自分のアイコンとして持つ役職。見つからなければ false。</summary>
        static public bool TryGetIconOwner(Image image, out DefinedAssignable assignable) => iconOwners.TryGetValue(image, out assignable!);

        static public TMP_SpriteAsset SpriteAsset { get; private set; } = null!;
        static public TMP_SpriteAsset MaskedAsset { get; private set; } = null!;

        private record RoleIconSheet(Material Mat, string? Prefix)
        {
            public TMP_SpriteAsset SpriteAsset { get; } = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            public TMP_SpriteAsset MaskedAsset { get; } = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            public List<TMP_SpriteCharacter> CharacterList { get; } = [];
            public List<TMP_Sprite> InfoList { get; } = [];
            public List<TMP_SpriteCharacter> MaskedCharacterList { get; } = [];
            public List<TMP_Sprite> MaskedInfoList { get; } = [];
            public string PrefixNotNull => Prefix != null ? Prefix + "_" : "";
        }

        /// <summary>
        /// Texture2Dのリストからアトラスを作成し、TMP_SpriteAssetを構築する
        /// </summary>
        static private void CreateSpriteAsset((Sprite sprite, Material material, string name, DefinedAssignable assignable)[] images)
        {
            int layer = 20;
            GameObject holder = UnityHelper.CreateObject("Holder", null, Vector3.zero);
            Camera cam = UnityHelper.CreateObject<Camera>("Camera", holder.transform, new(0f, 0f, -50f));
            cam.orthographic = true;
            cam.orthographicSize = imageSize.y * ImageLines / 100f / 2f;
            cam.cullingMask = 1 << layer;
            cam.transform.localScale = Vector3.one;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear;

            RoleIconSheet defaultSheet = new(null!, null);
            RoleIconSheet[] extraSheets = AssignmentType.AllTypes.Select(type => new RoleIconSheet(GetRoleIconMaterial(type.Color.ToUnityColor(), new(1f, 1f, 1f), iconOutlineWidth, 0f), type.Postfix)).ToArray();
            RoleIconSheet[] allSheets = [defaultSheet, .. extraSheets];

            var glyphList = new List<TMP_SpriteGlyph>();

            List<SpriteRenderer> renderers = [];
            for (int i = 0; i < images.Length; i++)
            {
                var entry = images[i];
                idMap[entry.name] = i;

                int x = i % ImagePerLines;
                int y = i / ImageLines;

                SpriteRenderer renderer = UnityHelper.CreateObject<SpriteRenderer>("Renderer", holder.transform, new(((float)x - (ImagePerLines - 1) * 0.5f) * (imageSize.x / 100f), ((ImageLines - 1) * 0.5f - (float)y) * (imageSize.y / 100f), 1f));
                renderer.sprite = entry.sprite;
                renderer.material = entry.material;
                renderer.gameObject.layer = layer;
                renderer.transform.localScale = new(0.48f, 0.48f, 1f);
                renderers.Add(renderer);

                var rectX = x * imageSize.x;
                var rectY = (ImageLines - (y + 1)) * imageSize.y;
                var rectW = imageSize.x;
                var rectH = imageSize.y;

                TMP_SpriteGlyph glyph = new();
                glyph.index = (uint)i;
                glyph.glyphRect = new((int)rectX, (int)rectY, (int)rectW, (int)rectH);
                glyph.metrics = new(rectW, rectH, 0f, rectH * 0.8f, rectW);
                glyphList.Add(glyph);

                for (int sheetIndex = 0; sheetIndex < allSheets.Length; sheetIndex++)
                {
                    var sheets = allSheets[sheetIndex];

                    //閲覧画面でこのタグを見つけたとき、どのシートのどのマスを切り取ればよいか。
                    iconMap[sheets.PrefixNotNull + entry.name] = (sheetIndex, i, entry.assignable);

                    TMP_SpriteCharacter character = new(0xf0000 + (uint)i, glyph);
                    character.name = sheets.PrefixNotNull + entry.name;
                    character.glyphIndex = glyph.index;
                    character.scale = 1.4f;
                    sheets.CharacterList.Add(character);

                    TMP_SpriteCharacter maskedCharacter = new(0xf0000 + (uint)i, glyph);
                    character.name = "masked_" + sheets.PrefixNotNull + entry.name;
                    character.glyphIndex = glyph.index;
                    character.scale = 1.4f;
                    sheets.MaskedCharacterList.Add(maskedCharacter);

                    TMP_Sprite sprite = new() { x = rectX, y = rectY, width = rectW, height = rectH, id = i, pivot = new(0.5f, 0.5f), xAdvance = rectW, xOffset = 0f, yOffset = rectH * 0.8f, scale = 1.4f, name = sheets.PrefixNotNull + entry.name, hashCode = i, unicode = 0xf0000 + i };
                    sheets.InfoList.Add(sprite);

                    TMP_Sprite maskedSprite = new() { x = rectX, y = rectY, width = rectW, height = rectH, id = i, pivot = new(0.5f, 0.5f), xAdvance = rectW, xOffset = 0f, yOffset = rectH * 0.8f, scale = 1.4f, name = "masked_" + sheets.PrefixNotNull + entry.name, hashCode = i, unicode = 0xf0000 + i };
                    sheets.MaskedInfoList.Add(maskedSprite);
                }
            }

            //撮影する
            void PrintTexture(RoleIconSheet sheet)
            {
                RenderTexture rt = RenderTexture.GetTemporary(imageSize.x * ImagePerLines, imageSize.y * ImageLines, 24);
                cam.targetTexture = rt;

                cam.Render();

                RenderTexture.active = rt;
                Texture2D atlas = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                atlas.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                atlas.Apply();
                atlas.MarkDontUnload();
                atlas.name = "RoleIconAtlas";

                //シートの並びは allSheets と同じ。閲覧画面へはこの並びで配る。
                sheetNames.Add(sheet.Prefix ?? DefaultSheetName);
                atlases.Add(atlas);

                RenderTexture.active = null;

                //撮影終了
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(rt);

                Shader shader = Shader.Find("Sprites/Default");
                Material material = new Material(shader);
                material.MarkDontUnload();

                Material maskedMaterial = new Material(UnityHelper.GetMeshRendererMaskedMaterial());
                maskedMaterial.MarkDontUnload();

                sheet.SpriteAsset.name = "NoSRoleIcons";
                material.mainTexture = atlas;
                sheet.SpriteAsset.material = material;
                sheet.SpriteAsset.spriteSheet = atlas;
                sheet.SpriteAsset.spriteGlyphTable = glyphList.ToIl2CppList();
                sheet.SpriteAsset.spriteCharacterTable = sheet.CharacterList.ToIl2CppList();
                sheet.SpriteAsset.spriteInfoList = sheet.InfoList.ToIl2CppList();

                sheet.MaskedAsset.name = "NoSMasked" + (sheet.Prefix ?? "") +  "RoleIcons";
                maskedMaterial.mainTexture = atlas;
                sheet.MaskedAsset.material = maskedMaterial;
                sheet.MaskedAsset.spriteSheet = atlas;
                sheet.MaskedAsset.spriteGlyphTable = glyphList.ToIl2CppList();
                sheet.MaskedAsset.spriteCharacterTable = sheet.MaskedCharacterList.ToIl2CppList();
                sheet.MaskedAsset.spriteInfoList = sheet.MaskedInfoList.ToIl2CppList();

                try
                {
                    sheet.SpriteAsset.UpdateLookupTables();

                    for (int i = 0; i < sheet.SpriteAsset.spriteCharacterTable.Count; i++)
                    {
                        sheet.SpriteAsset.spriteCharacterTable[i].glyphIndex = (uint)i;
                        sheet.SpriteAsset.spriteCharacterTable[i].glyph = sheet.SpriteAsset.spriteGlyphTable[i];
                    }

                    sheet.MaskedAsset.UpdateLookupTables();

                    for (int i = 0; i < sheet.MaskedAsset.spriteCharacterTable.Count; i++)
                    {
                        sheet.MaskedAsset.spriteCharacterTable[i].glyphIndex = (uint)i;
                        sheet.MaskedAsset.spriteCharacterTable[i].glyph = sheet.MaskedAsset.spriteGlyphTable[i];
                    }

                }
                catch (Exception e)
                {
                    LogUtils.WriteToConsole(e.ToString());
                }
            }

            PrintTexture(defaultSheet);
            foreach(var sheet in extraSheets)
            {
                foreach (var r in renderers) r.sharedMaterial = sheet.Mat;
                PrintTexture(sheet);
            }

            defaultSheet.SpriteAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset>([defaultSheet.MaskedAsset, ..extraSheets.Select(sheet => sheet.SpriteAsset)]).ToIl2CppList();
            defaultSheet.MaskedAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset>(extraSheets.Select(sheet => sheet.MaskedAsset)).ToIl2CppList();

            GameObject.Destroy(holder);

            SpriteAsset = defaultSheet.SpriteAsset;
            MaskedAsset = defaultSheet.MaskedAsset;
        }
    }
}
