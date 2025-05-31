using AmongUs.Data;
using Cpp2IL.Core.Extensions;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using TMPro;
using Virial.Runtime;

namespace Nebula.Modules.Cosmetics;

[NebulaPreprocess(PreprocessPhase.PostLoadAddons)]
[NebulaRPCHolder]
public class DynamicPalette
{
    public const int ColorsLength = 32;
    //PlayerIdと紐づけられたバイザーのゲーム内カラーパレット
    static public readonly Color[] VisorColors = new Color[ColorsLength];
    static public readonly Color[] PlayerColors = new Color[ColorsLength];
    static public readonly Color[] ShadowColors = new Color[ColorsLength];

    static public readonly ColorPalette[] AllColorPalette = [new DefaultColorPalette()];
    static public readonly ShadowPattern[] AllShadowPattern = [new DefaultShadowPattern()];

    static public readonly Dictionary<int, (int h, int d, string? name)> ColorNameDic = [];

    static private DataSaver ColorData = null!;
    static public ModColor MyColor = null!;
    static public ColorEntry MyVisorColor = null!;
    static public (ModColor color, ColorEntry visorColor)[] SavedColor = null!;

    public static readonly Dictionary<string, List<RestorableColor>> ColorCatalogue = [];
    public static readonly Dictionary<string, List<RestorableColor>> VisorColorCatalogue = [];

    //バニラカラー(他Mod連携表示用の控え)
    public static Color[] VanillaColorsPalette;

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Loading Color Catalogue");

        ColorData = new DataSaver("DynamicColor");
        MyColor = new ModColor("myColor");
        MyVisorColor = new ColorEntry("myColor.visor");
        SavedColor = new (ModColor, ColorEntry)[5];
        for (int i = 0; i < SavedColor.Length; i++) SavedColor[i] = (new("savedColor" + i), new("savedColor" + i + ".visor"));

        VanillaColorsPalette = Palette.PlayerColors.Select(c => (Color)c).ToArray();

        List<RestorableColor> vanillaCatalogue = [];
        for (int i = 0; i < 18; i++)
        {
            vanillaCatalogue.Add(new RestorableColor()
            {
                MainColor = new(Palette.PlayerColors[i]),
                ShadowColor = new(Palette.ShadowColors[i]),
                ShadowType = byte.MaxValue,
                Category = "innersloth",
                TranslationKey = "inventory.color.vanilla" + i
            });
        }
        List<RestorableColor> vanilaVisorCatalogue = new([ new RestorableColor()
            {
                MainColor = new(Palette.VisorColor),
                ShadowType = byte.MaxValue,
                Category = "innersloth",
                TranslationKey = "inventory.color.visor"
            }]);

        ColorCatalogue.Add("innersloth", vanillaCatalogue);
        VisorColorCatalogue.Add("innersloth", vanilaVisorCatalogue);

        var oldPlayerColors = Palette.PlayerColors;
        var oldShadowColors = Palette.ShadowColors;

        for (int i = 0; i < ColorsLength; i++)
        {
            DynamicPalette.PlayerColors[i] = oldPlayerColors[i >= oldPlayerColors.Count ? 0 : i];
            DynamicPalette.ShadowColors[i] = oldShadowColors[i >= oldPlayerColors.Count ? 0 : i];
        }
        for (int i = 0; i < VisorColors.Length; i++) VisorColors[i] = Palette.VisorColor;

        //カモフラージャーカラー
        DynamicPalette.PlayerColors[NebulaPlayerTab.CamouflageColorId] = oldPlayerColors[6].Multiply(new Color32(180, 180, 180, 255));
        DynamicPalette.ShadowColors[NebulaPlayerTab.CamouflageColorId] = oldShadowColors[6].Multiply(new Color32(180, 180, 180, 255));

        //プレビューカラーを設定しておく(Dev. Studio用)
        DynamicPalette.PlayerColors[NebulaPlayerTab.PreviewColorId] = MyColor.MainColor;
        DynamicPalette.ShadowColors[NebulaPlayerTab.PreviewColorId] = MyColor.ShadowColor;

        foreach (var addon in NebulaAddon.AllAddons)
        {
            using var stream = addon.OpenStream("Color/ColorCatalogue.json");
            if (stream != null)
            {
                var colors = JsonStructure.Deserialize<List<RestorableColor>>(stream);
                if (colors == null) continue;

                foreach (var c in colors)
                {
                    if (!ColorCatalogue.ContainsKey(c.Category))
                        ColorCatalogue[c.Category] = [];
                    ColorCatalogue[c.Category].Add(c);
                }
            }

            //Visor
            using var visorStream = addon.OpenStream("Color/VisorColorCatalogue.json");
            if (visorStream != null)
            {
                var visorColors = JsonStructure.Deserialize<List<RestorableColor>>(visorStream);
                if (visorColors == null) continue;

                foreach (var c in visorColors)
                {
                    if (!VisorColorCatalogue.ContainsKey(c.Category))
                        VisorColorCatalogue[c.Category] = [];
                    VisorColorCatalogue[c.Category].Add(c);
                }
            }
            yield return null;
        }
    }

    public class ColorEntry
    {
        public DataEntry<byte> r, g, b;

        public ColorEntry(string colorDataId)
        {
            r = new ByteDataEntry(colorDataId + ".col.r", ColorData, 0);
            g = new ByteDataEntry(colorDataId + ".col.g", ColorData, 0);
            b = new ByteDataEntry(colorDataId + ".col.b", ColorData, 0);
        }

        public void Edit(Color color)
        {
            r.Value = (byte)Math.Clamp((int)(color.r * byte.MaxValue), 0, byte.MaxValue);
            g.Value = (byte)Math.Clamp((int)(color.g * byte.MaxValue), 0, byte.MaxValue);
            b.Value = (byte)Math.Clamp((int)(color.b * byte.MaxValue), 0, byte.MaxValue);
        }

        public void Edit(SerializableColor color)
        {
            r.Value = color.R;
            g.Value = color.G;
            b.Value = color.B;
        }

        public Color ToColor(Color? defaultColor = null)
        {
            if (r.Value == 0 && g.Value == 0 && b.Value == 0 && defaultColor.HasValue) return defaultColor.Value;
            return new Color(r.Value / 255f, g.Value / 255f, b.Value / 255f);
        }
    }
    public class ColorParameters
    {
        public DataEntry<byte> hue, distance;
        public DataEntry<float> brightness;
        public DataEntry<byte> palette;
        public ColorEntry color;
        public ColorParameters(string colorDataId)
        {
            hue = new ByteDataEntry(colorDataId + ".h", ColorData, 0);
            distance = new ByteDataEntry(colorDataId + ".d", ColorData, 8);
            palette = new ByteDataEntry(colorDataId + ".p", ColorData, 0);
            brightness = new FloatDataEntry(colorDataId + ".b", ColorData, 1f);
            color = new(colorDataId);
        }

        public void Edit(byte? hue, byte? distance, float? brightness, byte? palette)
        {
            if (hue.HasValue) this.hue.Value = hue.Value;
            if (distance.HasValue) this.distance.Value = distance.Value;
            if (brightness.HasValue) this.brightness.Value = brightness.Value;
            if (palette.HasValue) this.palette.Value = palette.Value;
        }

        public void Edit(Color color)
        {
            hue.Value = byte.MaxValue;
            this.color.Edit(color);
        }

        public void Edit(SerializableColor color)
        {
            hue.Value = color.Hue;
            distance.Value = color.Distance;
            brightness.Value = color.Brightness;
            this.color.Edit(color);
        }

        public Color ToColor()
        {
            if (hue.Value == byte.MaxValue)
                return color.ToColor();
            else
                return AllColorPalette[palette.Value].GetColor(hue.Value, distance.Value, brightness.Value);
        }
    }
    public class ModColor
    {
        public Color mainColor, shadowColor;
        public Color MainColor { get => mainColor; }
        public Color ShadowColor { get => shadowColor; }
        public string Name { get => nameEntry.Value; set => nameEntry.Value = value; }
        private ColorParameters mainParameters, shadowParameters;
        private DataEntry<byte> shadowType;
        private DataEntry<string> nameEntry;

        public ModColor(string colorDataId)
        {
            mainParameters = new ColorParameters(colorDataId + ".main");
            shadowParameters = new ColorParameters(colorDataId + ".shadow");
            shadowType = new ByteDataEntry(colorDataId + ".type", ColorData, 0);
            nameEntry = new StringDataEntry(colorDataId + ".name", ColorData, "none");

            if (shadowType.Value != byte.MaxValue)
            {
                AllShadowPattern[shadowType.Value].GetShadowColor(mainParameters.ToColor(), shadowParameters.ToColor(), out mainColor, out shadowColor);
            }
            else
            {
                mainColor = mainParameters.ToColor();
                shadowColor = shadowParameters.ToColor();
            }
        }

        public void EditColor(bool isShadow, byte? hue, byte? distance, float? brightness, byte? palette)
        {
            var param = isShadow ? shadowParameters : mainParameters;

            param.Edit(hue, distance, brightness, palette);

            if (shadowType.Value != byte.MaxValue)
            {
                var tempColor = param.ToColor();
                AllShadowPattern[shadowType.Value].GetShadowColor(
                    isShadow ? mainColor : tempColor,
                    isShadow ? tempColor : shadowColor,
                    out mainColor, out shadowColor);
            }
        }

        public void RestoreColor(Color mainColor, Color shadowColor, byte shadowType, (byte hue, byte disatance, float brightness)? mainParam, (byte hue, byte disatance, float brightness)? shadowParam, string? name)
        {
            this.mainColor = mainColor;
            this.shadowColor = shadowColor;
            this.shadowType.Value = shadowType;

            if (mainParam != null)
                mainParameters.Edit(mainParam.Value.hue, mainParam.Value.disatance, mainParam.Value.brightness, 0);
            else
                mainParameters.Edit(mainColor);

            if (shadowParam != null)
                shadowParameters.Edit(shadowParam.Value.hue, shadowParam.Value.disatance, shadowParam.Value.brightness, 0);
            else
                shadowParameters.Edit(shadowColor);

            if (name != null) Name = name;
        }

        public void Restore(RestorableColor color)
        {
            mainColor = color.MainColor.AsColor;
            shadowColor = color.ShadowColor.AsColor;
            shadowType.Value = color.ShadowType;
            mainParameters.Edit(color.MainColor);
            shadowParameters.Edit(color.ShadowColor);
            Name = color.DisplayName;

            if (shadowType.Value != byte.MaxValue)
            {
                var tempColor = mainParameters.ToColor();
                AllShadowPattern[shadowType.Value].GetShadowColor(tempColor, shadowColor,
                    out mainColor, out shadowColor);
            }
        }

        public void Restore(ModColor color)
        {
            mainColor = color.mainColor;
            shadowColor = color.shadowColor;
            shadowType.Value = color.shadowType.Value;

            if (color.mainParameters.hue.Value == byte.MaxValue)
                mainParameters.Edit(color.mainColor);
            else
                mainParameters.Edit(color.mainParameters.hue.Value, color.mainParameters.distance.Value, color.mainParameters.brightness.Value, color.mainParameters.palette.Value);

            if (color.shadowParameters.hue.Value == byte.MaxValue)
                shadowParameters.Edit(color.shadowColor);
            else
                shadowParameters.Edit(color.shadowParameters.hue.Value, color.shadowParameters.distance.Value, color.shadowParameters.brightness.Value, color.shadowParameters.palette.Value);

            Name = color.Name;

            if (shadowType.Value != byte.MaxValue)
            {
                var tempColor = mainParameters.ToColor();
                AllShadowPattern[shadowType.Value].GetShadowColor(tempColor, shadowColor,
                    out mainColor, out shadowColor);
            }
        }

        public byte GetMainHue()
        {
            return mainParameters.hue.Value;
        }
        public void GetMainParam(out byte hue, out byte distance, out float brightness)
        {
            hue = mainParameters.hue.Value;
            distance = mainParameters.distance.Value;
            brightness = mainParameters.brightness.Value;
        }

        public void GetShadowParam(out byte hue, out byte distance, out float brightness)
        {
            hue = shadowParameters.hue.Value;
            distance = shadowParameters.distance.Value;
            brightness = shadowParameters.brightness.Value;
        }

        public void GetParam(bool isShadow, out byte hue, out byte distance, out float brightness)
        {
            var param = isShadow ? shadowParameters : mainParameters;

            hue = param.hue.Value;
            distance = param.distance.Value;
            brightness = param.brightness.Value;
        }

        public void SetShadowPattern(byte pattern)
        {
            shadowType.Value = pattern;
            AllShadowPattern[shadowType.Value].GetShadowColor(mainColor, shadowColor, out mainColor, out shadowColor);
        }
        public byte GetShadowPattern() => shadowType.Value;
    }

    public class SerializableColor
    {
        [JsonSerializableField]
        public byte R, G, B;
        [JsonSerializableField]
        public byte Hue = byte.MaxValue, Distance = 0;
        [JsonSerializableField]
        public float Brightness = 1f;

        public SerializableColor() { }
        public SerializableColor(Color color)
        {
            R = (byte)(color.r * byte.MaxValue);
            G = (byte)(color.g * byte.MaxValue);
            B = (byte)(color.b * byte.MaxValue);
            Hue = byte.MaxValue;
            Distance = 0;
            Brightness = 1f;
        }

        public SerializableColor(ColorParameters color)
        {
            var rawColor = color.ToColor();
            (R, G, B) = ((byte)(rawColor.r * byte.MaxValue), (byte)(rawColor.g * byte.MaxValue), (byte)(rawColor.b * byte.MaxValue));
            Hue = color.hue.Value;
            Distance = color.distance.Value;
            Brightness = color.brightness.Value;
        }

        public Color AsColor => new Color(R / 255f, G / 255f, B / 255f);
    }

    public class RestorableColor
    {
        [JsonSerializableField]
        public SerializableColor MainColor = null!;
        [JsonSerializableField]
        public SerializableColor ShadowColor = null!;
        [JsonSerializableField]
        public string Name = "";
        [JsonSerializableField]
        public string? TranslationKey = null;
        [JsonSerializableField]
        public byte ShadowType = byte.MaxValue;
        [JsonSerializableField]
        public string Category = "";
        public string DisplayName { get => TranslationKey != null ? Language.Translate(TranslationKey) : Name; }
    }
    public class ShareColorMessage
    {
        public Color mainColor, shadowColor, visorColor;
        public byte playerId;
        public Tuple<int, int>? colorNameParam;
        public string? colorName;

        public ShareColorMessage ReflectMyColor()
        {
            mainColor = MyColor.mainColor;
            shadowColor = MyColor.shadowColor;
            visorColor = MyVisorColor.ToColor(Palette.VisorColor);
            MyColor.GetMainParam(out var h, out var d, out _);
            colorNameParam = new(h, d);
            colorName = h == byte.MaxValue ? MyColor.Name : null;
            return this;
        }
    }

    public static void RpcShareMyColor() => RpcShareColor.Invoke(new ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());
    public readonly static RemoteProcess<ShareColorMessage> RpcShareColor = new(
        "ShareColor",
        (writer, message) =>
        {
            writer.Write(message.playerId);
            writer.Write(message.mainColor.r);
            writer.Write(message.mainColor.g);
            writer.Write(message.mainColor.b);
            writer.Write(message.shadowColor.r);
            writer.Write(message.shadowColor.g);
            writer.Write(message.shadowColor.b);
            writer.Write(message.visorColor.r);
            writer.Write(message.visorColor.g);
            writer.Write(message.visorColor.b);
            if (message.colorName != null)
            {
                writer.Write(false);
                writer.Write(message.colorName);
            }
            else
            {
                writer.Write(true);
                writer.Write(message.colorNameParam!.Item1);
                writer.Write(message.colorNameParam!.Item2);
            }
        },
        (reader) =>
        {
            ShareColorMessage message = new()
            {
                playerId = reader.ReadByte(),
                mainColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                shadowColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                visorColor = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
            };
            if (reader.ReadBoolean())
                message.colorNameParam = new(reader.ReadInt32(), reader.ReadInt32());
            else
                message.colorName = reader.ReadString();
            return message;
        },
        (message, isCalledByMe) =>
        {
            PlayerColors[message.playerId] = message.mainColor;
            ShadowColors[message.playerId] = message.shadowColor;
            VisorColors[message.playerId] = message.visorColor;
            ColorNameDic[message.playerId] = (message.colorNameParam?.Item1 ?? byte.MaxValue, message.colorNameParam?.Item2 ?? 0, message.colorName);

            //まだプレイヤーが追加されていない場合は即座に反映させなくても大丈夫
            try
            {
                var player = PlayerControl.AllPlayerControls.Find((Il2CppSystem.Predicate<PlayerControl>)(p => p.PlayerId == message.playerId));
                player?.SetColor(player!.PlayerId);
            }
            catch { }
        }, true
        );

    public interface ColorPalette
    {
        //hue 0～63 distance 0～23 brightness 0f～1f
        Color GetColor(byte hue, byte distance, float brightness);
    }

    public interface ShadowPattern
    {
        void GetShadowColor(Color mainColor, Color shadowColor, out Color resultMain, out Color resultShadow);
        abstract bool AllowEditShadowColor { get; }
    }

    public class DefaultColorPalette : ColorPalette
    {
        public Color GetColor(byte hue, byte distance, float brightness)
        {
            var color = Color.HSVToRGB(hue / 64f, 1f, 1f);
            if (distance < 6)
            {
                float d = 1f - distance / 6f;
                color = Color.Lerp(color, Color.white, d);
            }
            else if (distance > 9)
            {
                float s = (float)(distance - 9f) / 14f;
                float sum = (color.r + color.g + color.b) / 3f;
                color = new Color(sum, sum, sum) * s + color * (1 - s);
            }
            return color * (brightness * 0.5f + 0.5f);
        }
    }

    public class DefaultShadowPattern : ShadowPattern
    {
        public void GetShadowColor(Color mainColor, Color shadowColor, out Color resultMain, out Color resultShadow)
        {
            resultMain = mainColor;
            resultShadow = mainColor.RGBMultiplied(0.5f);
        }

        public bool AllowEditShadowColor => true;
    }

    public static bool IsLightColor(Color color)
    {
        var max = Mathf.Max(color.r, color.g, color.b);
        var sum = color.r + color.g + color.b;
        return max > 0.8f || sum > 2.1f;
    }

    public static string HDToTranslationKey(int h, int d) => "color." + h + "." + d;
    public static string GetColorName(int colorId)
    {
        if (colorId < NebulaPlayerTab.PreviewColorId)
        {
            if (DynamicPalette.ColorNameDic.TryGetValue(colorId, out var tuple))
            {
                if (tuple.h == byte.MaxValue)
                    return tuple.name ?? "";
                else
                    return Language.Translate(HDToTranslationKey(tuple.h, tuple.d));
            }
            else
            {
                return "";
            }
        }
        else if (colorId == NebulaPlayerTab.PreviewColorId)
        {
            DynamicPalette.MyColor.GetMainParam(out var h, out var d, out var b);
            return Language.Translate(HDToTranslationKey(h, d));
        }
        else
        {
            return "";
        }
    }

    public static readonly SpriteLoader colorFullButtonSprite = SpriteLoader.FromResource("Nebula.Resources.ColorButton.png", 100f);
    public static readonly SpriteLoader colorButtonSprite = SpriteLoader.FromResource("Nebula.Resources.ColorHalfButton.png", 100f);
    public static readonly SpriteLoader colorBackSprite = SpriteLoader.FromResource("Nebula.Resources.ColorFullBase.png", 100f);
    public static readonly SpriteLoader colorInvalidSprite = SpriteLoader.FromResource("Nebula.Resources.ColorInvalidButton.png", 100f);
    public static void OpenCatalogue(NebulaPlayerTab playerTab, SpriteRenderer TargetRenderer, Action ShownColor, bool isBodyColor = true)
    {
        var screen = MetaScreen.GenerateWindow(new Vector2(6.7f, 4.2f), HudManager.InstanceExists ? HudManager.Instance.transform : PlayerCustomizationMenu.Instance.transform, new Vector3(0f, 0f, 0f), true, false);
        screen.transform.parent.FindChild("Background").GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.85f);

        MetaWidgetOld widget = new();

        widget.Append(new CombinedWidgetOld(
            new MetaWidgetOld.Button(() => { screen.CloseScreen(); OpenCatalogue(playerTab, TargetRenderer, ShownColor, true); }, TextAttributeOld.BoldAttr) { RawText = "Body Color" },
            new MetaWidgetOld.HorizonalMargin(0.15f),
            new MetaWidgetOld.Button(() => { screen.CloseScreen(); OpenCatalogue(playerTab, TargetRenderer, ShownColor, false); }, TextAttributeOld.BoldAttr) { RawText = "Visor Color" }
            ));
        widget.Append(new MetaWidgetOld.VerticalMargin(0.05f));

        MetaWidgetOld inner = new();

        MetaWidgetOld.ScrollView scrollView = new(new(6.7f, 3.55f), inner, true);

        List<(SpriteRenderer invalidRenderer, Color main)> buttons = [];

        foreach (var category in isBodyColor ? ColorCatalogue : VisorColorCatalogue)
        {
            inner.Append(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial }) { TranslationKey = "inventory.catalogue." + category.Key });
            inner.Append(category.Value, (col) =>
                new MetaWidgetOld.Image(colorButtonSprite.GetSprite())
                {
                    Width = 0.96f,
                    PostBuilder = (renderer) =>
                    {
                        if (isBodyColor)
                        {
                            var invalidRenderer = UnityEngine.Object.Instantiate(renderer, renderer.transform.parent);
                            invalidRenderer.sprite = colorInvalidSprite.GetSprite();
                            invalidRenderer.transform.localPosition += new Vector3(0, 0, -1f);
                            invalidRenderer.sortingOrder = 21;
                            invalidRenderer.color = Color.white;
                            invalidRenderer.enabled = false;
                            invalidRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

                            buttons.Add((invalidRenderer, col.MainColor.AsColor));
                        }

                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        renderer.sortingOrder = 20;
                        renderer.color = col.MainColor.AsColor;

                        if (isBodyColor)
                        {
                            var baseRenderer = UnityEngine.Object.Instantiate(renderer, renderer.transform.parent);
                            baseRenderer.sprite = colorBackSprite.GetSprite();
                            baseRenderer.transform.localPosition += new Vector3(0, 0, 0.1f);
                            baseRenderer.sortingOrder = 10;
                            baseRenderer.color = col.ShadowColor.AsColor;
                        }
                        else
                        {
                            renderer.sprite = colorFullButtonSprite.GetSprite();
                        }

                        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                        collider.size = new(0.7f, 0.5f);
                        collider.isTrigger = true;
                        var button = renderer.gameObject.SetUpButton(true);
                        button.OnClick.AddListener(() =>
                        {
                            screen.CloseScreen();
                            if (isBodyColor)
                            {
                                MyColor.Restore(col);
                                TargetRenderer.gameObject.SetActive(col.MainColor.Hue != byte.MaxValue);
                                TargetRenderer.transform.localPosition = NebulaPlayerTab.ToPalettePosition(col.MainColor.Hue, col.MainColor.Distance);
                            }
                            else
                            {
                                MyVisorColor.Edit(col.MainColor.AsColor);
                            }
                            ShownColor();

                            if (AmongUsClient.Instance && AmongUsClient.Instance.IsInGame && PlayerControl.LocalPlayer) RpcShareColor.Invoke(new ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());
                        });
                        button.OnMouseOver.AddListener(() =>
                        {
                            MetaWidgetOld widget = new();
                            widget.Append(new MetaWidgetOld.VariableText(new(TextAttributeOld.BoldAttr) { Alignment = TextAlignmentOptions.Left }) { RawText = col.DisplayName });
                            if (col.TranslationKey != null)
                            {
                                var detail = Language.Find(col.TranslationKey + ".detail");
                                if (detail != null) widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.ContentAttr) { RawText = detail });
                            }
                            NebulaManager.Instance.SetHelpWidget(button, widget);
                        });
                        button.OnMouseOut.AddListener(NebulaManager.Instance.HideHelpWidget);
                    },
                    Alignment = IMetaWidgetOld.AlignmentOption.Left
                }
            , 6, -1, 0, 0.65f);
        }

        widget.Append(scrollView);

        screen.SetWidget(widget);

        //選択を奨めない色の表示
        if (buttons.Count > 0)
        {
            void UpdateInvalidColor()
            {
                int option = ClientOption.AllOptions[ClientOption.ClientOptionType.AvoidingColorDuplication].Value;
                if (!LobbyBehaviour.Instance) option = 0;

                if (option == 0)
                {
                    foreach (var button in buttons) button.invalidRenderer.enabled = false;
                    return;
                }

                var playerColors = PlayerControl.AllPlayerControls.GetFastEnumerator().Where(p => !p.AmOwner).Select(p => (Color)DynamicPalette.PlayerColors[p.PlayerId]).ToArray();

                float threshold = option == 1 ? 0.3f : 0.05f;
                float max = 0f;
                foreach (var button in buttons)
                {
                    //Strict: 色差で判断
                    button.invalidRenderer.enabled = playerColors.Any(c =>
                    {
                        var dr = c.r - button.main.r;
                        var dg = c.g - button.main.g;
                        var db = c.b - button.main.b;
                        var diff = 2f * (dr * dr) + 4f * (dg * dg) + 3f * (db * db);
                        if (diff > max) max = diff;
                        return diff < threshold;
                    });
                }
                Debug.Log("Max: " + max);
            }
            IEnumerator CoUpdate()
            {
                while (true)
                {
                    yield return Effects.Wait(0.5f);
                    UpdateInvalidColor();
                }
            }
            UpdateInvalidColor();
            screen.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        }
    }
}

public class NebulaPlayerTab : MonoBehaviour
{
    static readonly Image spritePalette = SpriteLoader.FromResource("Nebula.Resources.Palette.png", 100f);
    static readonly Image spriteTarget = SpriteLoader.FromResource("Nebula.Resources.TargetIcon.png", 100f);

    static readonly Image spriteBrPalette = SpriteLoader.FromResource("Nebula.Resources.PaletteBrightness.png", 100f);
    static readonly Image spriteBrTarget = SpriteLoader.FromResource("Nebula.Resources.PaletteKnob.png", 100f);

    static readonly XOnlyDividedSpriteLoader spriteColorIcons = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.ColorIcon.png", 100f, 50, true);

    static readonly XOnlyDividedSpriteLoader spriteSwitches = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.PaletteSwitches.png", 100f, 2);
    static NebulaPlayerTab()
    {
        ClassInjector.RegisterTypeInIl2Cpp<NebulaPlayerTab>();
    }

    int currentPalette = 0;
    bool editingShadowColor = false;
    bool editingVisorColor = false;

    SpriteRenderer DynamicPaletteRenderer = null!;
    SpriteRenderer TargetRenderer = null!;

    SpriteRenderer BrightnessRenderer = null!;
    SpriteRenderer BrightnessTargetRenderer = null!;
    SpriteRenderer BrightnessTargetPreviewRenderer = null!;
    GameObject BrTargetKnob = null!;
    ObjectPool<SpriteRenderer> ColorIcons = null!;

    PassiveButton BrPaletteBackButton = null!;

    public PlayerTab playerTab = null!;

    static private float BrightnessHeight = 2.6f;
    static private float ToBrightness(float y) => Mathf.Clamp01((y + BrightnessHeight * 0.5f) / BrightnessHeight);

    static private SpriteLoader saveButtonSprite = SpriteLoader.FromResource("Nebula.Resources.ColorSave.png", 100f);


    public void Start()
    {
        ColorIcons = new(parent =>
        {
            var renderer = UnityHelper.CreateObject<SpriteRenderer>("ColorIcon", parent, new(5f, 1.73f, -50f), LayerExpansion.GetUILayer());
            var button = renderer.gameObject.SetUpButton(false, renderer);
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            var collider = button.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new(0.46f, 0.46f);

            return renderer;
        }, transform);

        new MetaWidgetOld.Button(() => DynamicPalette.OpenCatalogue(this, TargetRenderer, () => PreviewColor(null, null, null)), TextAttributeOld.BoldAttr) { TranslationKey = "inventory.palette.catalogue" }.Generate(gameObject, new Vector2(2.9f, 2.25f), out _);

        DynamicPaletteRenderer = UnityHelper.CreateObject<SpriteRenderer>("DynamicPalette", transform, new Vector3(0.4f, -0.1f, -80f), LayerExpansion.GetUILayer());
        DynamicPaletteRenderer.sprite = spritePalette.GetSprite();
        var PaletteCollider = DynamicPaletteRenderer.gameObject.AddComponent<CircleCollider2D>();
        PaletteCollider.radius = 2.1f;
        PaletteCollider.isTrigger = true;
        var PaletteButton = DynamicPaletteRenderer.gameObject.SetUpButton();

        PaletteButton.OnClick.AddListener(() =>
        {
            if (editingVisorColor)
            {
                ToColorParam(GetOnPalettePosition(), out var h, out var d);
                Color c = DynamicPalette.AllColorPalette[0].GetColor(h, d, 1f);

                DynamicPalette.MyVisorColor.Edit(c);
                TargetRenderer.gameObject.SetActive(true);
                TargetRenderer.transform.localPosition = ToPalettePosition(h, d);
            }
            else
            {
                if (DynamicPalette.MyColor.GetShadowPattern() == byte.MaxValue) DynamicPalette.MyColor.SetShadowPattern(0);

                ToColorParam(GetOnPalettePosition(), out var h, out var d);

                DynamicPalette.MyColor.EditColor(editingShadowColor, h, d, null, null);
                TargetRenderer.gameObject.SetActive(h != byte.MaxValue && !editingVisorColor);
                TargetRenderer.transform.localPosition = ToPalettePosition(h, d);
            }

            if (AmongUsClient.Instance && AmongUsClient.Instance.IsInGame && PlayerControl.LocalPlayer) DynamicPalette.RpcShareColor.Invoke(new DynamicPalette.ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());
        });
        PaletteButton.OnMouseOut.AddListener(() =>
        {
            PreviewColor(null, null, null);
        });

        TargetRenderer = UnityHelper.CreateObject<SpriteRenderer>("TargetIcon", DynamicPaletteRenderer.transform, new Vector3(0f, 0f, -10f));
        TargetRenderer.sprite = spriteTarget.GetSprite();
        TargetRenderer.gameObject.layer = LayerExpansion.GetUILayer();

        void SetTargetRendererPos()
        {
            DynamicPalette.MyColor.GetParam(editingShadowColor, out byte h, out byte d, out _);
            TargetRenderer.gameObject.SetActive(h != byte.MaxValue);
            TargetRenderer.transform.localPosition = ToPalettePosition(h, d);
        }

        SetTargetRendererPos();

        BrightnessRenderer = UnityHelper.CreateObject<SpriteRenderer>("BrightnessPalette", transform, new Vector3(3.1f, -0.1f, -80f));
        BrightnessRenderer.sprite = spriteBrPalette.GetSprite();
        BrightnessRenderer.gameObject.layer = LayerExpansion.GetUILayer();

        BrightnessTargetRenderer = UnityHelper.CreateObject<SpriteRenderer>("BrightnessPalette", BrightnessRenderer.transform, new Vector3(0f, 0.0f, -1f));
        BrightnessTargetRenderer.sprite = spriteBrTarget.GetSprite();
        BrightnessTargetRenderer.gameObject.layer = LayerExpansion.GetUILayer();

        BrightnessTargetPreviewRenderer = UnityHelper.CreateObject<SpriteRenderer>("BrightnessPalette", BrightnessRenderer.transform, new Vector3(0f, 0.0f, -1.5f));
        BrightnessTargetPreviewRenderer.sprite = spriteBrTarget.GetSprite();
        BrightnessTargetPreviewRenderer.color = new(0f, 0.6f, 1f, 0.5f);
        BrightnessTargetPreviewRenderer.gameObject.layer = LayerExpansion.GetUILayer();
        BrightnessTargetPreviewRenderer.gameObject.SetActive(false);

        var BrPaletteCollider = BrightnessRenderer.gameObject.AddComponent<BoxCollider2D>();
        BrPaletteCollider.size = new Vector2(0.31f, BrightnessHeight);
        BrPaletteCollider.isTrigger = true;

        BrPaletteBackButton = BrightnessRenderer.gameObject.SetUpButton(true);
        BrPaletteBackButton.OnClick.AddListener(() =>
        {
            if (DynamicPalette.MyColor.GetShadowPattern() == byte.MaxValue) return;

            var pos = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer()) - BrPaletteBackButton.transform.position;
            var b = ToBrightness(pos.y);
            DynamicPalette.MyColor.EditColor(editingShadowColor, null, null, b, null);

            var targetLocPos = BrightnessTargetRenderer.transform.localPosition;
            targetLocPos.y = Mathf.Clamp(pos.y, -BrightnessHeight * 0.5f, BrightnessHeight * 0.5f);
            BrightnessTargetRenderer.transform.localPosition = targetLocPos;

            if (AmongUsClient.Instance && AmongUsClient.Instance.IsInGame && PlayerControl.LocalPlayer) DynamicPalette.RpcShareColor.Invoke(new DynamicPalette.ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());

            PreviewColor(null, null, null);
        });

        BrTargetKnob = BrightnessTargetRenderer.gameObject;


        for (int i = 0; i < DynamicPalette.SavedColor.Length; i++)
        {
            int copiedIndex = i;
            var renderer = UnityHelper.CreateObject<SpriteRenderer>("SavedColor", transform, new(4.45f + i * 0.81f, 2.25f, -50f));
            renderer.sprite = DynamicPalette.colorButtonSprite.GetSprite();
            renderer.color = DynamicPalette.SavedColor[copiedIndex].color.MainColor;
            var baseRenderer = UnityHelper.CreateObject<SpriteRenderer>("ShadowColor", renderer.transform, new(0f, 0f, 1f));
            baseRenderer.sprite = DynamicPalette.colorBackSprite.GetSprite();
            baseRenderer.color = DynamicPalette.SavedColor[copiedIndex].color.ShadowColor;
            var saveRenderer = UnityHelper.CreateObject<SpriteRenderer>("SaveButton", renderer.transform, new(0f, -0.38f, 0f));
            saveRenderer.sprite = saveButtonSprite.GetSprite();

            var restoreButton = renderer.gameObject.SetUpButton(true);
            restoreButton.OnClick.AddListener(() =>
            {
                DynamicPalette.MyColor.Restore(DynamicPalette.SavedColor[copiedIndex].color);
                DynamicPalette.MyVisorColor.Edit(DynamicPalette.SavedColor[copiedIndex].visorColor.ToColor());
                SetTargetRendererPos();

                if (AmongUsClient.Instance && AmongUsClient.Instance.IsInGame && PlayerControl.LocalPlayer) DynamicPalette.RpcShareColor.Invoke(new DynamicPalette.ShareColorMessage() { playerId = PlayerControl.LocalPlayer.PlayerId }.ReflectMyColor());

                PreviewColor(null, null, null);
            });
            restoreButton.OnMouseOver.AddListener(() => PreviewColor(DynamicPalette.SavedColor[copiedIndex].color, DynamicPalette.SavedColor[copiedIndex].visorColor.ToColor(Palette.VisorColor)));
            restoreButton.OnMouseOut.AddListener(() => PreviewColor(null, null, null));
            restoreButton.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.7f, 0.5f);

            var saveButton = saveRenderer.gameObject.SetUpButton(true, saveRenderer);
            saveButton.OnClick.AddListener(() =>
            {
                DynamicPalette.SavedColor[copiedIndex].color.Restore(DynamicPalette.MyColor);
                DynamicPalette.SavedColor[copiedIndex].visorColor.Edit(DynamicPalette.MyVisorColor.ToColor());
                renderer.color = DynamicPalette.MyColor.mainColor;
                baseRenderer.color = DynamicPalette.MyColor.shadowColor;
            });
            saveButton.gameObject.AddComponent<BoxCollider2D>().size = new Vector2(0.25f, 0.25f);
        }

        var SwitchRenderer = UnityHelper.CreateObject<SpriteRenderer>("Switch", transform, new Vector3(2.5f, -1.73f, -50f));
        SwitchRenderer.sprite = spriteSwitches.GetSprite(1);
        var SwitchButton = SwitchRenderer.gameObject.SetUpButton(true, SwitchRenderer);
        SwitchButton.OnClick.AddListener(() =>
        {
            editingVisorColor = !editingVisorColor;
            if (editingVisorColor)
            {
                editingShadowColor = false;
                TargetRenderer.gameObject.SetActive(false);
            }
            else
            {
                SetTargetRendererPos();
            }

            SwitchRenderer.sprite = spriteSwitches.GetSprite(editingVisorColor ? 0 : 1);

            BrightnessRenderer.gameObject.SetActive(!editingVisorColor);
        });
        var SwitchCollider = SwitchButton.gameObject.AddComponent<BoxCollider2D>();
        SwitchCollider.size = new(0.5f, 0.6f);
        SwitchCollider.isTrigger = true;

        PreviewColor(null, null, null);
    }

    public void OnEnable()
    {
        Helpers.RefreshMemory();
        PreviewColor(null, null, null);
    }

    private Vector2 GetOnPalettePosition()
    {
        return UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer()) - DynamicPaletteRenderer.transform.position;
    }

    public static Vector3 ToPalettePosition(byte hue, byte distance)
    {
        float magnitude = distance / 24f * 2.1f;
        float angle = (float)hue / 64 * (2f * Mathf.PI) + Mathf.PI * 0.5f;
        return new Vector3(Mathf.Cos(angle) * magnitude, Mathf.Sin(angle) * magnitude, -1f);
    }

    private void ToColorParam(Vector2 pos, out byte hue, out byte distance)
    {
        distance = (byte)(pos.magnitude / 2.1f * 24);
        if (distance > 23) distance = 23;
        hue = (byte)(Mathf.Atan2(-pos.x, pos.y) / (2f * Mathf.PI) * 64);
        while (hue < 0) hue += 64;
        while (hue >= 64) hue -= 64;
    }

    byte lastH = 0, lastD = 0;
    float lastTime = 0f;

    float lastPreviewB = -1f;
    public void Update()
    {
        BrTargetKnob.SetActive(DynamicPalette.MyColor.GetShadowPattern() != byte.MaxValue);

        //マウスボタン押下中でカーソルが明度パレット上にある
        if (Input.GetMouseButton(0) && PassiveButtonManager.Instance.currentOver == BrPaletteBackButton)
        {
            var currentPos = UnityHelper.ScreenToWorldPoint(Input.mousePosition, LayerExpansion.GetUILayer()) - BrPaletteBackButton.transform.position;
            var b = ToBrightness(currentPos.y);
            if (Mathf.Abs(lastPreviewB - b) > 0.001f)
            {
                PreviewColor(null, null, b);
                lastPreviewB = b;
            }

            var targetLocPos = BrightnessTargetPreviewRenderer.transform.localPosition;
            targetLocPos.y = Mathf.Clamp(currentPos.y, -BrightnessHeight * 0.5f, BrightnessHeight * 0.5f);
            BrightnessTargetPreviewRenderer.transform.localPosition = targetLocPos;

            BrightnessTargetPreviewRenderer.gameObject.SetActive(true);
        }
        else
        {
            BrightnessTargetPreviewRenderer.gameObject.SetActive(false);
        }


        var pos = GetOnPalettePosition();
        float distance = pos.magnitude;

        if (distance < 2.06f)
        {
            ToColorParam(pos, out var h, out var d);

            if (h == lastH && d == lastD || Mathf.Abs(Time.time - lastTime) < 0.05f) return;
            lastTime = Time.time;
            lastH = h;
            lastD = d;

            if (editingVisorColor)
            {
                DynamicPalette.VisorColors[PreviewColorId] = DynamicPalette.AllColorPalette[0].GetColor(h, d, 1f);
                PreviewColor(DynamicPalette.MyColor, DynamicPalette.VisorColors[PreviewColorId]);
            }
            else
            {
                PreviewColor(h, d, null);
            }
        }
    }

    static public readonly byte PreviewColorId = 28;
    static public readonly byte ArchiveColorId = 29;
    static public readonly byte CamouflageColorId = 30;

    //SetColorの複製をしない版
    private static void SetSharedColors(int colorId, Renderer renderer)
    {
        renderer.sharedMaterial.SetColor(PlayerMaterial.BackColor, DynamicPalette.ShadowColors[colorId]);
        renderer.sharedMaterial.SetColor(PlayerMaterial.BodyColor, DynamicPalette.PlayerColors[colorId]);
        renderer.sharedMaterial.SetColor(PlayerMaterial.VisorColor, DynamicPalette.VisorColors[PreviewColorId]);
    }

    void AddIcon(int imageId, string translationKey)
    {
        int numOfIcons = ColorIcons.Count;
        var s = ColorIcons.Instantiate();
        s.sprite = spriteColorIcons.GetSprite(imageId);
        s.transform.localPosition = new(7f + 0.6f * numOfIcons, 1.25f, -50f);
        var button = s.gameObject.GetComponent<PassiveButton>();
        button.OnMouseOver.RemoveAllListeners();
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, Language.Translate(translationKey)));
    }

    private void AfterPreviewColor(byte concernedHue, byte concernedDistance, string? displayName = null)
    {
        string colorName;
        if (concernedHue == byte.MaxValue)
            colorName = displayName ?? DynamicPalette.MyColor.Name;
        else
            colorName = Language.Translate(DynamicPalette.HDToTranslationKey(concernedHue, concernedDistance));
        PlayerCustomizationMenu.Instance.SetItemName(colorName);

        if (playerTab != null)
        {
            if (playerTab.PlayerPreview.ColorId != PreviewColorId)
            {
                //複製を伴う色更新
                playerTab.PlayerPreview.SetBodyColor(PreviewColorId);
                playerTab.PlayerPreview.SetPetColor(PreviewColorId);
            }
            else
            {
                //複製を伴わない色更新
                if (playerTab.PlayerPreview.cosmetics.currentBodySprite != null)
                    SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.currentBodySprite.BodySprite);
            }

            if (playerTab.currentColor != PreviewColorId)
            {
                //複製を伴う色更新
                playerTab.currentColor = PreviewColorId;
                playerTab.PlayerPreview.SetSkin(DataManager.Player.Customization.Skin, PreviewColorId);
                playerTab.PlayerPreview.SetHat(DataManager.Player.Customization.Hat, PreviewColorId);
                playerTab.PlayerPreview.SetVisor(DataManager.Player.Customization.Visor, PreviewColorId);
            }
            else
            {
                //複製を伴わない色更新
                SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.visor.Image);
                SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.GetComponent<NebulaCosmeticsLayer>().VisorBackRenderer);
                SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.skin.layer);
                SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.hat.FrontLayer);
                if (playerTab.PlayerPreview.cosmetics.hat.BackLayer) SetSharedColors(PreviewColorId, playerTab.PlayerPreview.cosmetics.hat.BackLayer);
            }
        }

        //色に関する追加情報
        if (ColorIcons != null) ColorIcons.RemoveAll();

        if (Helpers.CurrentMonth == 4)
        {
            if (ColorHelper.IsLightGreen(DynamicPalette.PlayerColors[PreviewColorId])) AddIcon(0, "help.color.icon.april0");
            else if (ColorHelper.IsPink(DynamicPalette.PlayerColors[PreviewColorId])) AddIcon(1, "help.color.icon.april1");
        }
        if (Helpers.CurrentMonth == 8)
        {
            if (ColorHelper.IsGreenOrBlack(DynamicPalette.PlayerColors[PreviewColorId])) AddIcon(2, "help.color.icon.august");
        }
    }

    private void PreviewColor(byte? concernedHue, byte? concernedDistance, float? concernedBrightness)
    {

        DynamicPalette.MyColor.GetParam(editingShadowColor, out byte h, out byte d, out var b);
        concernedHue ??= h;
        concernedDistance ??= d;
        concernedBrightness ??= b;

        if (BrightnessRenderer != null)
        {
            BrightnessRenderer.color = DynamicPalette.AllColorPalette[currentPalette].GetColor((byte)concernedHue, (byte)concernedDistance, 1f);
        }

        if (DynamicPalette.MyColor.GetShadowPattern() != byte.MaxValue)
        {
            Color color = DynamicPalette.AllColorPalette[currentPalette].GetColor(concernedHue.Value, concernedDistance.Value, concernedBrightness.Value);
            DynamicPalette.AllShadowPattern[DynamicPalette.MyColor.GetShadowPattern()].GetShadowColor(
                editingShadowColor ? DynamicPalette.MyColor.MainColor : color,
                editingShadowColor ? color : DynamicPalette.MyColor.ShadowColor,
                out var resultMain, out var resultShadow
                );

            DynamicPalette.PlayerColors[PreviewColorId] = resultMain;
            DynamicPalette.ShadowColors[PreviewColorId] = resultShadow;
        }
        else
        {
            DynamicPalette.PlayerColors[PreviewColorId] = DynamicPalette.MyColor.MainColor;
            DynamicPalette.ShadowColors[PreviewColorId] = DynamicPalette.MyColor.ShadowColor;
        }
        DynamicPalette.VisorColors[PreviewColorId] = DynamicPalette.MyVisorColor.ToColor(Palette.VisorColor);

        AfterPreviewColor(concernedHue.Value, concernedDistance.Value);
    }

    private void PreviewColor(DynamicPalette.ModColor color, Color visorColor)
    {
        try
        {
            DynamicPalette.PlayerColors[PreviewColorId] = color.MainColor;
            DynamicPalette.ShadowColors[PreviewColorId] = color.ShadowColor;
            DynamicPalette.VisorColors[PreviewColorId] = visorColor;

            color.GetMainParam(out var h, out var d, out _);
            AfterPreviewColor(h, d, color.Name);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(Palette), nameof(Palette.GetColorName))]
public static class ColorNamePatch
{
    static bool Prefix(ref string __result, [HarmonyArgument(0)] int colorId)
    {
        __result = DynamicPalette.GetColorName(colorId);
        return false;
    }
}

[HarmonyPatch(typeof(CosmeticsLayer), nameof(CosmeticsLayer.GetColorBlindText))]
public static class ColorNameCosmeticsLayerPatch
{
    static bool Prefix(CosmeticsLayer __instance, ref string __result)
    {
        __result = DynamicPalette.GetColorName(__instance.ColorId);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.SetColors), typeof(int), typeof(Renderer))]
public static class SetColorPatch
{
    static bool Prefix([HarmonyArgument(0)] int colorId, [HarmonyArgument(1)] Renderer rend)
    {
        if (!rend || colorId < 0 || colorId >= DynamicPalette.VisorColors.Length) return false;


        rend.material.SetColor(PlayerMaterial.BackColor, DynamicPalette.ShadowColors[colorId]);
        rend.material.SetColor(PlayerMaterial.BodyColor, DynamicPalette.PlayerColors[colorId]);
        rend.material.SetColor(PlayerMaterial.VisorColor, DynamicPalette.VisorColors[colorId]);
        return false;
    }
}

[HarmonyPatch(typeof(PlayerMaterial), nameof(PlayerMaterial.SetColors), typeof(int), typeof(Material))]
public static class SetColorMaterialPatch
{
    static bool Prefix([HarmonyArgument(0)] int colorId, [HarmonyArgument(1)] Material material)
    {
        if (!material || colorId < 0 || colorId >= DynamicPalette.VisorColors.Length) return false;

        material.SetColor(PlayerMaterial.BackColor, DynamicPalette.ShadowColors[colorId]);
        material.SetColor(PlayerMaterial.BodyColor, DynamicPalette.PlayerColors[colorId]);
        material.SetColor(PlayerMaterial.VisorColor, DynamicPalette.VisorColors[colorId]);
        return false;
    }
}