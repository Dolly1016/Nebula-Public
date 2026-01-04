using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using Virial.Assignable;
using Virial.Runtime;
using static Il2CppSystem.Xml.Schema.FacetsChecker.FacetsCompiler;

namespace Nebula.Roles;

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

    static public string GetRoleIconTag(this DefinedAssignable assignable, bool masked = false, int size = 100)
    {
        var tag = RuntimeSpriteGenerator.SpriteTagFromAssignable(assignable, masked);
        if (size == 100) return tag;
        return (tag).Sized(size);
    }

    static public string GetRoleIconTagSmall(this DefinedAssignable assignable, bool masked = false) => GetRoleIconTag(assignable, masked, 70);

    static public void UseRoleIcon(this TMPro.TextMeshPro text) => text.spriteAsset = RuntimeSpriteGenerator.SpriteAsset;

    [NebulaPreprocess(PreprocessPhase.PostFixStructure)]
    public static class RuntimeSpriteGenerator
    {
        static private void Preprocess(NebulaPreprocessor preprocessor)
        {
            (SpriteAsset, MaskedAsset) = CreateSpriteAsset(Roles.AllAssignables().Select(a => (a.GetRoleIcon()?.GetSprite(), GetRoleIconMaterial(a, 0.45f, 0f), a.InternalName)).ToArray()!);
            SpriteAsset.MarkDontUnload();
        }


        // 撮影用のカメラとRenderTextureの設定
        static private Vector2Int imageSize = new Vector2Int(64, 64);
        private const int ImagePerLines = 20;
        private const int ImageLines = 20;

        static private Dictionary<string, int> idMap = [];
        static public string SpriteTagFromAssignable(DefinedAssignable assignable, bool masked) => masked ? $"<sprite name=\"masked_{assignable.InternalName}\">" : $"<sprite name=\"{assignable.InternalName}\">";

        static public TMP_SpriteAsset SpriteAsset { get; private set; } = null!;
        static public TMP_SpriteAsset MaskedAsset { get; private set; } = null!;
        /// <summary>
        /// Texture2Dのリストからアトラスを作成し、TMP_SpriteAssetを構築する
        /// </summary>
        static private (TMP_SpriteAsset, TMP_SpriteAsset) CreateSpriteAsset((Sprite sprite, Material material, string name)[] images)
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

            TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            TMP_SpriteAsset maskedAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();

            Shader shader = Shader.Find("Sprites/Default");
            Material material = new Material(shader);
            material.MarkDontUnload();

            Material maskedMaterial = new Material(UnityHelper.GetMeshRendererMaskedMaterial());
            maskedMaterial.MarkDontUnload();

            var glyphList = new List<TMP_SpriteGlyph>();
            var characterList = new List<TMP_SpriteCharacter>();
            var infoList = new List<TMP_Sprite>();

            var maskedCharacterList = new List<TMP_SpriteCharacter>();
            var maskedInfoList = new List<TMP_Sprite>();

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

                var rectX = x * imageSize.x;
                var rectY = (ImageLines - (y + 1)) * imageSize.y;
                var rectW = imageSize.x;
                var rectH = imageSize.y;

                
                TMP_SpriteGlyph glyph = new();
                glyph.index = (uint)i;
                glyph.glyphRect = new((int)rectX, (int)rectY, (int)rectW, (int)rectH);
                glyph.metrics = new(rectW, rectH, 0f, rectH * 0.8f, rectW);
                glyphList.Add(glyph);
                
                TMP_SpriteCharacter character = new(0xf0000 + (uint)i, glyph);
                character.name = entry.name;
                character.glyphIndex = glyph.index;
                character.scale = 1.4f;
                characterList.Add(character);

                TMP_SpriteCharacter maskedCharacter = new(0xf0000 + (uint)i, glyph);
                character.name = "masked_" + entry.name;
                character.glyphIndex = glyph.index;
                character.scale = 1.4f;
                maskedCharacterList.Add(maskedCharacter);

                TMP_Sprite sprite = new() { x = rectX, y = rectY, width = rectW, height = rectH, id = i, pivot = new(0.5f, 0.5f), xAdvance = rectW, xOffset = 0f, yOffset = rectH * 0.8f, scale = 1.4f, name = entry.name, hashCode = i, unicode = 0xf0000 + i };
                infoList.Add(sprite);

                TMP_Sprite maskedSprite = new() { x = rectX, y = rectY, width = rectW, height = rectH, id = i, pivot = new(0.5f, 0.5f), xAdvance = rectW, xOffset = 0f, yOffset = rectH * 0.8f, scale = 1.4f, name = "masked_" + entry.name, hashCode = i, unicode = 0xf0000 + i };
                maskedInfoList.Add(maskedSprite);
            }

            //撮影
            RenderTexture rt = RenderTexture.GetTemporary(imageSize.x * ImagePerLines, imageSize.y * ImageLines, 24);
            cam.targetTexture = rt;

            cam.Render();

            RenderTexture.active = rt;
            Texture2D atlas = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            atlas.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            atlas.Apply();
            atlas.MarkDontUnload();
            atlas.name = "RoleIconAtlas";

            RenderTexture.active = null;

            //撮影終了
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            /*
            for (int i = 0; i < infoList.Count; i++)
            {
                var info = infoList[i];
                info.sprite = atlas.ToSprite(new Rect(info.x, info.y, info.width, info.height), 100f);
                info.sprite.MarkDontUnload();
            }
            */

            spriteAsset.name = "NoSRoleIcons";
            material.mainTexture = atlas;
            spriteAsset.material = material;
            spriteAsset.spriteSheet = atlas;
            spriteAsset.spriteGlyphTable = glyphList.ToIl2CppList();
            spriteAsset.spriteCharacterTable = characterList.ToIl2CppList();
            spriteAsset.spriteInfoList = infoList.ToIl2CppList();

            maskedAsset.name = "NoSMaskedRoleIcons";
            maskedMaterial.mainTexture = atlas;
            maskedAsset.material = maskedMaterial;
            maskedAsset.spriteSheet = atlas;
            maskedAsset.spriteGlyphTable = glyphList.ToIl2CppList();
            maskedAsset.spriteCharacterTable = maskedCharacterList.ToIl2CppList();
            maskedAsset.spriteInfoList = maskedInfoList.ToIl2CppList();

            spriteAsset.fallbackSpriteAssets = new List<TMP_SpriteAsset>([maskedAsset]).ToIl2CppList();

            try
            {
                spriteAsset.UpdateLookupTables();

                for (int i = 0; i < spriteAsset.spriteCharacterTable.Count; i++)
                {
                    spriteAsset.spriteCharacterTable[i].glyphIndex = (uint)i;
                    spriteAsset.spriteCharacterTable[i].glyph = spriteAsset.spriteGlyphTable[i];
                }

                maskedAsset.UpdateLookupTables();

                for (int i = 0; i < maskedAsset.spriteCharacterTable.Count; i++)
                {
                    maskedAsset.spriteCharacterTable[i].glyphIndex = (uint)i;
                    maskedAsset.spriteCharacterTable[i].glyph = maskedAsset.spriteGlyphTable[i];
                }

            }
            catch(Exception e)
            {
                LogUtils.WriteToConsole(e.ToString());
            }

            GameObject.Destroy(holder);

            return (spriteAsset, maskedAsset);
        }
    }
}
