using AmongUs.GameOptions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Commands.Variations;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using Virial;
using Virial.Command;
using Virial.Compat;
using Virial.Components;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Modules.ScriptComponents;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class ModAbilityButtonImpl : DependentLifespan, ModAbilityButton, IGameOperator
{
    private static readonly Image vanillaKillImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.VanillaKillButton.png", 100f);
    public class AbilityButtonStructure
    {
        public bool isLeftSide = false; 
        public bool showAlways = false;
        public bool arrangedAsKillButton = false;
        public int priority = 0;
        public IExecutable? onClick = null;
        public IExecutable? onSubClick = null;
        public TextComponent? label = null;
        public Image? image = null;
    }
    static ModAbilityButtonImpl()
    {
        EntityCommand.RegisterEntityDefinition("button", EntityCommand.GenerateDefinition(
            new Virial.Command.CommandStructureConverter<AbilityButtonStructure>()
            .Add<bool>("isLeftSide", (structure, val) => structure.isLeftSide = val)
            .Add<bool>("isRightSide", (structure, val) => structure.isLeftSide = !val)
            .Add<bool>("always", (structure, val) => structure.showAlways = val)
            .Add<bool>("asKillButton", (structure, val) => structure.arrangedAsKillButton = val)
            .Add<int>("priority", (structure, val) => structure.priority = Mathf.Clamp(val, 0, 9999))
            .Add<string>("rawLabel", (structure, val) => structure.label = new RawTextComponent(val))
            .Add<string>("localizedLabel", (structure, val) => structure.label = new TranslateTextComponent(val))
            .Add<IExecutable>("action", (structure, val) => structure.onClick = val)
            .Add<IExecutable>("aidAction", (structure, val) => structure.onSubClick = val)
            .Add<string>("icon", (structure, val) => structure.image = NebulaResourceManager.GetResource(val)?.AsImage(115f))
            , () => new AbilityButtonStructure(), val =>
            {
                var button = new ModAbilityButtonImpl(val.isLeftSide, val.arrangedAsKillButton, val.priority, val.showAlways);
                if(val.onClick != null) button.OnClick = b => NebulaManager.Instance.StartCoroutine(val.onClick!.CoExecute([]).CoWait().HighSpeedEnumerator().WrapToIl2Cpp());
                if(val.onSubClick != null) button.OnSubAction = b => NebulaManager.Instance.StartCoroutine(val.onSubClick!.CoExecute([]).CoWait().HighSpeedEnumerator().WrapToIl2Cpp());
                button.SetRawLabel(val.label?.GetString() ?? "button");
                if(val.image != null)button.SetSprite(val.image.GetSprite());
                return button;
            }));
    }

    public ActionButton VanillaButton { get; private set; }

    public IVisualTimer? CoolDownTimer { get; set; } = null;
    public IVisualTimer? EffectTimer { get; set; } = null;
    public IVisualTimer? CurrentTimer => (EffectActive && (EffectTimer?.IsProgressing ?? false)) ? EffectTimer : CoolDownTimer;
    public bool EffectActive = false;

    public float CoolDownOnGameStart = 10f;

    public Action<ModAbilityButtonImpl>? OnEffectStart { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnEffectEnd { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnUpdate { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnClick { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnSubAction { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnMeeting { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnStartTaskPhase { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnReleased { get; set; } = null;
    public Action<ModAbilityButtonImpl>? OnBroken { get; set; } = null;
    public Predicate<ModAbilityButtonImpl>? Availability { get; set; } = null;
    public Predicate<ModAbilityButtonImpl>? Visibility { get; set; } = null;
    private VirtualInput? keyCode { get; set; } = null;
    private VirtualInput? subKeyCode { get; set; } = null;
    private SpriteRenderer? brokenAlternative = null!;
    public bool IsBroken { get; set; } = false;
    public IUsurpableAbility? RelatedAbility { get; set; } = null;
    public ModAbilityButtonImpl(bool isLeftSideButton = false, bool isArrangedAsKillButton = false,int priority = 0, bool alwaysShow = false)
    {
        VanillaButton = UnityEngine.Object.Instantiate(HudManager.Instance.KillButton, HudManager.Instance.KillButton.transform.parent);
        VanillaButton.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((c) => { if (c.name.Equals("HotKeyGuide")) GameObject.Destroy(c); }));
        VanillaButton.cooldownTimerText.gameObject.SetActive(true);
        VanillaButton.buttonLabelText.transform.SetLocalZ(-0.001f);
        SetSprite(vanillaKillImage.GetSprite());

        VanillaButton.buttonLabelText.GetComponent<TextTranslatorTMP>().enabled = false;
        var passiveButton = VanillaButton.GetComponent<PassiveButton>();
        passiveButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        passiveButton.OnClick.AddListener(() => DoClick());

        var gridContent = VanillaButton.gameObject.GetComponent<HudContent>();
        gridContent.UpdateSubPriority();
        gridContent.MarkAsKillButtonContent(isArrangedAsKillButton);
        gridContent.SetPriority(priority);
        gridContent.IsStaticContent = alwaysShow;
        NebulaGameManager.Instance?.HudGrid.RegisterContent(gridContent, isLeftSideButton);

        SetLabelType(ModAbilityButton.LabelType.Standard);

        NebulaManager.Instance.ScheduleDelayAction(PlayFlashOnce);
    }

    void TryGenerateBrokenAlternative()
    {
        if (!brokenAlternative)
        {
            brokenAlternative = UnityHelper.CreateObject<SpriteRenderer>("Broken", VanillaButton.transform, UnityEngine.Vector3.zero);
            brokenAlternative.sprite = VanillaButton.graphic.sprite;
            brokenAlternative.material = NebulaAsset.BrokenShaderMat;
            brokenAlternative.gameObject.SetActive(false);
            NebulaAsset.PlaySE(NebulaAudioClip.ButtonBreaking,pitch: System.Random.Shared.NextSingle() * 0.2f + 0.7f,volume: 1f);
        }
    }
    void IGameOperator.OnReleased()
    {
        if (VanillaButton)
        {
            OnReleased?.Invoke(this);
            UnityEngine.Object.Destroy(VanillaButton.gameObject);
        }
    }

    private bool CheckMouseClick()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            var dis = (UnityEngine.Vector2)Input.mousePosition - new UnityEngine.Vector2(Screen.width, Screen.height) * 0.5f;
            return dis.magnitude < 280f;
        }
        return false;
    }

    public bool IsVisible => Visibility?.Invoke(this) ?? true;
    public bool IsAvailable => IsVisible && IsAvailableUnsafe;
    private bool IsAvailableUnsafe => Availability?.Invoke(this) ?? true;

    public Func<ModAbilityButtonImpl, bool>? PlayFlashWhile { get; set; } = null;
    private float playFlashTimer = 0f;
    
    private IEnumerator CoFlash()
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Flash", VanillaButton.transform, new(0f, 0f, -1f));
        renderer.sprite = VanillaButton.graphic.sprite;
        renderer.material = new(NebulaAsset.WhiteShader);
        renderer.material.SetColor("_Color", new(1f, 1f, 1f, 1f));
        yield return null;

        float a = 1f;
        while (a > 0f)
        {
            renderer.transform.localScale = UnityEngine.Vector3.one * (2f - a);
            renderer.material.SetColor("_Color", new(1f, 1f, 1f, a * 0.85f));
            a -= Time.deltaTime * 1.5f;
            yield return null;
        }

        GameObject.Destroy(renderer.gameObject);
    }

    public void PlayFlashOnce()
    {
        NebulaManager.Instance.StartCoroutine(CoFlash().WrapToIl2Cpp());
    }

    void PlayFlashUpdate(GameHudUpdateEvent ev)
    {
        if (IsVisible)
        {
            if (PlayFlashWhile?.Invoke(this) ?? false)
            {
                playFlashTimer -= Time.deltaTime;
                if(playFlashTimer < 0f)
                {
                    playFlashTimer = 0.9f;
                    PlayFlashOnce();
                }
            }
            else
            {
                playFlashTimer = 0f;
            }
        }
        else
        {
            playFlashTimer = 0f;
        }
    }

    public void UpdateVisibility()
    {
        try
        {
            //表示・非表示切替
            bool isVisible = IsVisible;
            VanillaButton.gameObject.SetActive(isVisible);
            if (isVisible) {
                if (IsBroken) TryGenerateBrokenAlternative();
                if(brokenAlternative) brokenAlternative!.gameObject.SetActive(IsBroken);
                VanillaButton.graphic.enabled = !IsBroken;
            }

            //使用可能性切替
            if (IsAvailableUnsafe)
                VanillaButton.SetEnabled();
            else
            {
                VanillaButton.SetDisabled();
                if (IsBroken) VanillaButton.buttonLabelText.color = Palette.EnabledColor;
            }


        }
        catch(Exception exception)
        {
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "[At " + VanillaButton.buttonLabelText.text + "] " + exception.ToString());
        }
    }

    void OnHudActiveChange(Patches.HudActivePatch.HudActiveChangeEvent ev) => UpdateVisibility();

    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        UpdateVisibility();

        OnUpdate?.Invoke(this);

        if (EffectActive && (EffectTimer == null || !EffectTimer.IsProgressing)) InactivateEffect();

        VanillaButton.SetCooldownFill(CurrentTimer?.Percentage ?? 0f);

        string timerText = "";
        if (CurrentTimer?.IsProgressing ?? false) timerText = CurrentTimer.TimerText ?? "";
        VanillaButton.cooldownTimerText.text = timerText;
        VanillaButton.cooldownTimerText.color = EffectActive ? Color.green : Color.white;

        if ((keyCode?.KeyDownInGame ?? false) || (canUseByMouseClick && CheckMouseClick())) DoClick();
        if (subKeyCode?.KeyDownInGame ?? false) DoSubClick();
        
    }

    void OnMeetingStart(MeetingStartEvent ev)
    {
        OnMeeting?.Invoke(this);
    }


    public ModAbilityButtonImpl InactivateEffect()
    {
        if (!EffectActive) return this;
        EffectActive = false;
        OnEffectEnd?.Invoke(this);
        return this;
    }

    public ModAbilityButtonImpl ToggleEffect()
    {
        if (EffectActive)
            InactivateEffect();
        else
            ActivateEffect();

        return this;
    }

    public ModAbilityButtonImpl ActivateEffect()
    {
        if (EffectActive) return this;
        EffectActive = true;
        EffectTimer?.Start();
        OnEffectStart?.Invoke(this);
        return this;
    }

    public ModAbilityButtonImpl StartCoolDown()
    {
        CoolDownTimer?.Start();
        return this;
    }

    public bool UseCoolDownSupport { get; set; } = true;

    void OnGameReenabled(TaskPhaseRestartEvent ev) {
        if (UseCoolDownSupport) StartCoolDown();
        OnStartTaskPhase?.Invoke(this);
    }

    public void OnGameStart(GameStartEvent ev) {
        if (UseCoolDownSupport && CoolDownTimer != null && CoolDownTimer is TimerImpl timer)
        {
            if (GeneralConfigurations.ShortenCooldownAtGameStart)
                timer.Start(Mathf.Min(timer.Max, CoolDownOnGameStart));
            else
                StartCoolDown();
        }
        OnStartTaskPhase?.Invoke(this);
    }

    public ModAbilityButton DoClick()
    {
        //効果中でなく、クールダウン中ならばなにもしない
        if (!EffectActive && (CoolDownTimer?.IsProgressing ?? false)) return this;
        //使用可能でないかを判定 (ボタン発火のタイミングと可視性更新のタイミングにずれが生じうるためここで再計算)
        if (!IsAvailable) return this;
        if (IsBroken) return this;

        //簒奪が判明したらボタンを壊して、なにも起こさない
        if(RelatedAbility?.IsUsurped ?? false)
        {
            IsBroken = true;
            OnBroken?.Invoke(this);
            return this;
        }

        OnClick?.Invoke(this);
        return this;
    }

    public ModAbilityButton DoSubClick()
    {
        //見えないボタンは使用させない
        if (!(Visibility?.Invoke(this) ?? true)) return this;
        if (IsBroken) return this;

        //簒奪が判明したらボタンを壊して、なにも起こさない
        if (RelatedAbility?.IsUsurped ?? false)
        {
            IsBroken = true;
            OnBroken?.Invoke(this);
            return this;
        }

        OnSubAction?.Invoke(this);
        return this;
    }

    public ModAbilityButtonImpl SetSprite(Sprite? sprite)
    {
        VanillaButton.graphic.sprite = sprite;
        if (sprite != null) VanillaButton.graphic.SetCooldownNormalizedUvs();
        return this;
    }

    public ModAbilityButtonImpl SetLabel(string translationKey) => SetRawLabel(Language.Translate("button.label." + translationKey));
    
    public ModAbilityButtonImpl SetRawLabel(string rawText)
    {
        VanillaButton.buttonLabelText.text = rawText;
        return this;
    }

    public ModAbilityButtonImpl SetLabelType(ModAbilityButton.LabelType labelType)
    {
        Material? material = null;
        switch (labelType)
        {
            case ModAbilityButton.LabelType.Standard:
                material = HudManager.Instance.UseButton.fastUseSettings[ImageNames.UseButton].FontMaterial;
                break;
            case ModAbilityButton.LabelType.Utility:
                material = HudManager.Instance.UseButton.fastUseSettings[ImageNames.PolusAdminButton].FontMaterial;
                break;
            case ModAbilityButton.LabelType.Impostor:
                material = RoleManager.Instance.GetRole(RoleTypes.Shapeshifter).Ability.FontMaterial;
                break;
            case ModAbilityButton.LabelType.Crewmate:
                material = RoleManager.Instance.GetRole(RoleTypes.Engineer).Ability.FontMaterial;
                break;

        }
        if (material != null) VanillaButton.buttonLabelText.SetSharedMaterial(material);
        return this;
    }

    internal GameObject UsesIcon = null!;
    private TMPro.TextMeshPro UsesIconText = null!;
    public TMPro.TextMeshPro ShowUsesIcon(int iconVariation)
    {
        if(UsesIcon)GameObject.Destroy(UsesIcon);
        UsesIcon = ButtonEffect.ShowUsesIcon(VanillaButton,iconVariation,out var text);
        UsesIconText = text;
        return text;
    }
    ModAbilityButton ModAbilityButton.ShowUsesIcon(int variation, string text)
    {
        ShowUsesIcon(variation).text = text;
        return this;
    }
    ModAbilityButton ModAbilityButton.UpdateUsesIcon(string text)
    {
        if(UsesIconText) UsesIconText.text = text; 
        return this;
    }
    ModAbilityButton ModAbilityButton.HideUsesIcon()
    {
        if (UsesIcon) GameObject.Destroy(UsesIcon);
        return this;
    }

    UnityEngine.GameObject ModAbilityButton.AddLockedOverlay() => VanillaButton.AddLockedOverlay().gameObject;

    public ModAbilityButtonImpl ResetKeyBind()
    {
        VanillaButton.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((c) => { if (c.name.Equals("HotKeyGuide")) GameObject.Destroy(c); }));
        keyCode = null;
        subKeyCode = null;
        return this;
    }

    public ModAbilityButtonImpl KeyBind(Virial.Compat.VirtualKeyInput keyCode, string? action = null) => KeyBind(NebulaInput.GetInput(keyCode), action);
    public ModAbilityButtonImpl KeyBind(VirtualInput keyCode, string? action = null)
    {
        VanillaButton.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((c) => { if (c.name.Equals("HotKeyGuide")) GameObject.Destroy(c); }));

        this.keyCode= keyCode;
        ButtonEffect.SetKeyGuide(VanillaButton.gameObject, keyCode.TypicalKey, action: action);
        
        return this;
    }

    private static SpriteLoader aidActionSprite = SpriteLoader.FromResource("Nebula.Resources.KeyBindOption.png", 100f);
    public ModAbilityButtonImpl SubKeyBind(Virial.Compat.VirtualKeyInput keyCode, string? action = null, bool isCriticalSubAction = false) => SubKeyBind(NebulaInput.GetInput(keyCode), action, isCriticalSubAction);
    public ModAbilityButtonImpl SubKeyBind(VirtualInput keyCode, string? action = null, bool isCriticalSubAction = false)
    {
        this.subKeyCode = keyCode;
        var guideObj = ButtonEffect.SetSubKeyGuide(VanillaButton.gameObject, keyCode.TypicalKey, false, action);

        if (guideObj != null)
        {
            var renderer = UnityHelper.CreateObject<SpriteRenderer>("HotKeyOption", guideObj.transform, new UnityEngine.Vector3(0.12f, 0.07f, -2f));
            renderer.sprite = aidActionSprite.GetSprite();

            if (isCriticalSubAction)
            {
                Tutorial.WaitAndShowTutorial(() => !VanillaButton.gameObject.activeSelf || !PlayerControl.LocalPlayer.CanMove,
                    new TutorialBuilder(() => renderer.transform.position, true)
                    .ShowWhile(()=>VanillaButton && renderer)
                    .BindHistory("subaction")
                    .AsGraphicalWidget(aidActionSprite, new(0.3f, 0.3f), Language.Translate("tutorial.variations.subAction")));
            }
        }

        return this;
    }

    private bool canUseByMouseClick = false;
    public ModAbilityButtonImpl SetCanUseByMouseClick(bool onlyLook = false, ButtonEffect.ActionIconType iconType = ButtonEffect.ActionIconType.ClickAction, string? action = "mouseClick", bool atBottom = true)
    {
        if(!onlyLook)canUseByMouseClick = true;
        ButtonEffect.SetMouseActionIcon(VanillaButton.gameObject, true, iconType, action, atBottom);
        return this;
    }

    public ModAbilityButtonImpl SetInfoIcon(string action, bool atBottom = false)
    {
        ButtonEffect.SetMouseActionIcon(VanillaButton.gameObject, true, ButtonEffect.ActionIconType.Info, action, atBottom);
        return this;
    }

    ModAbilityButton ModAbilityButton.SetImage(Image image) => SetSprite(image.GetSprite());
    ModAbilityButton ModAbilityButton.SetLabel(string translationKey) => SetLabel(translationKey);

    public IKillButtonLike GetKillButtonLike() => new ModKillButtonHandler(this);

    ModAbilityButton ModAbilityButton.StartCoolDown() => StartCoolDown();

    ModAbilityButton ModAbilityButton.StartEffect() => ActivateEffect();

    ModAbilityButton ModAbilityButton.InterruptEffect() => InactivateEffect();
    ModAbilityButton ModAbilityButton.ToggleEffect() => ToggleEffect();
    ModAbilityButton ModAbilityButton.SetLabelType(ModAbilityButton.LabelType type) => SetLabelType(type);

    Predicate<ModAbilityButton> ModAbilityButton.Availability { set => Availability = value; }
    Predicate<ModAbilityButton> ModAbilityButton.Visibility { set => Visibility = value; }
    Action<ModAbilityButton> ModAbilityButton.OnUpdate { set => OnUpdate = value; }
    Action<ModAbilityButton> ModAbilityButton.OnClick { set => OnClick = value; }
    Action<ModAbilityButton> ModAbilityButton.OnSubAction { set => OnSubAction = value; }
    Action<ModAbilityButton> ModAbilityButton.OnEffectStart { set => OnEffectStart = value; }
    Action<ModAbilityButton> ModAbilityButton.OnEffectEnd { set => OnEffectEnd = value; }
    Action<ModAbilityButton> ModAbilityButton.OnBroken { set => OnBroken = value; }
    Func<ModAbilityButton, bool> ModAbilityButton.PlayFlashWhile { set => PlayFlashWhile = value; }

    ModAbilityButton ModAbilityButton.BindKey(VirtualKeyInput input, string? action = null) => KeyBind(input, action);
    ModAbilityButton ModAbilityButton.BindSubKey(VirtualKeyInput input, string? action = null, bool withTutrorial = false) => SubKeyBind(input, action, withTutrorial);
    ModAbilityButton ModAbilityButton.ResetKeyBinding() => ResetKeyBind();
    ModAbilityButton ModAbilityButton.SetAsMouseClickButton() => SetCanUseByMouseClick();
    bool ModAbilityButton.IsInEffect => EffectActive;
    bool ModAbilityButton.IsInCooldown => CoolDownTimer?.IsProgressing ?? false;
    public PoolablePlayer? GeneratePlayerIcon(GamePlayer? player)
    {
        if (player == null) return null;
        return AmongUsUtil.GetPlayerIcon(player.DefaultOutfit.outfit, VanillaButton.transform, new UnityEngine.Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
    }


    ModAbilityButton ModAbilityButton.SetAsUsurpableButton(IUsurpableAbility ability)
    {
        RelatedAbility = ability;
        return this;
    }
}

public static class ButtonEffect
{
    [NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
    public class KeyCodeInfo
    {
        public static string? GetKeyDisplayName(KeyCode keyCode)
        {
            if (keyCode == KeyCode.Return)
                return "Return";
            if (AllKeyInfo.TryGetValue(keyCode, out var val)) return val.TranslationKey;
            return null;
        }

        static public readonly Dictionary<KeyCode, KeyCodeInfo> AllKeyInfo = [];
        public KeyCode keyCode { get; private set; }
        public DividedSpriteLoader textureHolder { get; private set; }
        public int num { get; private set; }
        public string TranslationKey { get; private set; }
        public KeyCodeInfo(KeyCode keyCode, string translationKey, DividedSpriteLoader spriteLoader, int num)
        {
            this.keyCode = keyCode;
            this.TranslationKey = translationKey;
            this.textureHolder = spriteLoader;
            this.num = num;

            AllKeyInfo.Add(keyCode, this);
        }

        public Sprite Sprite => textureHolder.GetSprite(num);
        static KeyCodeInfo()
        {
            DividedSpriteLoader spriteLoader;
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters0.png", 100f, 18, 19, true);
            _ = new KeyCodeInfo(KeyCode.Tab, "Tab", spriteLoader, 0);
            _ = new KeyCodeInfo(KeyCode.Space, "Space", spriteLoader, 1);
            _ = new KeyCodeInfo(KeyCode.Comma, "<", spriteLoader, 2);
            _ = new KeyCodeInfo(KeyCode.Period, ">", spriteLoader, 3);
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters1.png", 100f, 18, 19, true);
            for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
                _ = new KeyCodeInfo(key, ((char)('A' + key - KeyCode.A)).ToString(), spriteLoader, key - KeyCode.A);
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters2.png", 100f, 18, 19, true);
            for (int i = 0; i < 15; i++)
                _ = new KeyCodeInfo(KeyCode.F1 + i, "F" + (i + 1), spriteLoader, i);
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters3.png", 100f, 18, 19, true);
            _ = new KeyCodeInfo(KeyCode.RightShift, "RShift", spriteLoader, 0);
            _ = new KeyCodeInfo(KeyCode.LeftShift, "LShift", spriteLoader, 1);
            _ = new KeyCodeInfo(KeyCode.RightControl, "RControl", spriteLoader, 2);
            _ = new KeyCodeInfo(KeyCode.LeftControl, "LControl", spriteLoader, 3);
            _ = new KeyCodeInfo(KeyCode.RightAlt, "RAlt", spriteLoader, 4);
            _ = new KeyCodeInfo(KeyCode.LeftAlt, "LAlt", spriteLoader, 5);
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters4.png", 100f, 18, 19, true);
            for (int i = 0; i < 6; i++)
                _ = new KeyCodeInfo(KeyCode.Mouse1 + i, "Mouse " + (i == 0 ? "Right" : i == 1 ? "Middle" : (i + 1).ToString()), spriteLoader, i);
            spriteLoader = DividedSpriteLoader.FromResource("Nebula.Resources.KeyBindCharacters5.png", 100f, 18, 19, true);
            for (int i = 0; i < 10; i++)
                _ = new KeyCodeInfo(KeyCode.Alpha0 + i, "Num" + (i), spriteLoader, i);
        }
    }

    private static IDividedSpriteLoader textureUsesIconsSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.UsesIcon.png", 100f, 10);
    static public GameObject ShowUsesIcon(this ActionButton button)
    {
        Transform template = HudManager.Instance.AbilityButton.transform.GetChild(2);
        var usesObject = GameObject.Instantiate(template.gameObject);
        usesObject.transform.SetParent(button.gameObject.transform);
        usesObject.transform.localScale = template.localScale;
        usesObject.transform.localPosition = template.localPosition * 1.2f;
        return usesObject;
    }

    static public GameObject ShowUsesIcon(this ActionButton button, int iconVariation, out TMPro.TextMeshPro text)
    {
        GameObject result = ShowUsesIcon(button);
        var renderer = result.GetComponent<SpriteRenderer>();
        renderer.sprite = textureUsesIconsSprite.GetSprite(iconVariation);
        text = result.transform.GetChild(0).GetComponent<TMPro.TextMeshPro>();
        return result;
    }

    public static string ReplaceKeyCode(this string text, string target, Virial.Compat.VirtualKeyInput key) => text.Replace(target, KeyCodeInfo.GetKeyDisplayName(NebulaInput.GetInput(key).TypicalKey));

    static public SpriteRenderer AddOverlay(this ActionButton button, Sprite sprite, float order)
    {
        GameObject obj = new("Overlay");
        obj.layer = LayerExpansion.GetUILayer();
        obj.transform.SetParent(button.gameObject.transform);
        obj.transform.localScale = new(1, 1, 1);
        obj.transform.localPosition = new(0, 0, -1f - order);
        var renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        return renderer;
    }

    private static readonly SpriteLoader lockedButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LockedButton.png", 100f);
    static public SpriteRenderer AddLockedOverlay(this ActionButton button) => AddOverlay(button, lockedButtonSprite.GetSprite(), 0f);


    static readonly Image keyBindBackgroundSprite = SpriteLoader.FromResource("Nebula.Resources.KeyBindBackground.png", 100f);
    static readonly Image mouseActionSprite = SpriteLoader.FromResource("Nebula.Resources.MouseActionIcon.png", 100f);
    static readonly Image mouseDisableActionSprite = SpriteLoader.FromResource("Nebula.Resources.MouseActionDisableIcon.png", 100f);
    static readonly Image infoSprite = SpriteLoader.FromResource("Nebula.Resources.ButtonInfoIcon.png", 100f);
    static public Image InfoImage => infoSprite;
    static public GameObject? AddKeyGuide(GameObject button, KeyCode key, UnityEngine.Vector2 pos,bool removeExistingGuide, bool isAidAction = false, string? action = null)
    {
        if(removeExistingGuide)button.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => { if (obj.name == "HotKeyGuide") GameObject.Destroy(obj); }));

        Sprite? numSprite = null;
        if (KeyCodeInfo.AllKeyInfo.ContainsKey(key)) numSprite = KeyCodeInfo.AllKeyInfo[key].Sprite;
        if (numSprite == null) return null;

        GameObject obj = new();
        obj.name = "HotKeyGuide";
        obj.transform.SetParent(button.transform);
        obj.layer = button.layer;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.transform.localPosition = (UnityEngine.Vector3)pos + new UnityEngine.Vector3(0f, 0f, -10f);
        renderer.sprite = keyBindBackgroundSprite.GetSprite();

        GameObject numObj = new();
        numObj.name = "HotKeyText";
        numObj.transform.SetParent(obj.transform);
        numObj.layer = button.layer;
        renderer = numObj.AddComponent<SpriteRenderer>();
        renderer.transform.localPosition = new(0, 0, -1f);
        renderer.sprite = numSprite;

        SetHintOverlay(obj, isAidAction, key, action);

        return obj;
    }
    static public GameObject? SetKeyGuide(GameObject button, KeyCode key, bool removeExistingGuide = true, string? action = null)
    {
        return AddKeyGuide(button, key, new(0.48f, 0.48f), removeExistingGuide, action: action);
    }

    static public GameObject? SetSubKeyGuide(GameObject button, KeyCode key, bool removeExistingGuide, string? action = null)
    {
        return AddKeyGuide(button, key, new(0.48f, 0.13f), removeExistingGuide, true, action);
    }

    static public GameObject? SetKeyGuideOnSmallButton(GameObject button, KeyCode key)
    {
        return AddKeyGuide(button, key, new(0.28f, 0.28f), true);
    }

    static public GameObject? SetKeyGuideOnVanillaSmallButton(GameObject button, KeyCode key)
    {
        var diff = 0.28f / 1.2f * UpperRightButtons.BackgroundScale;
        return AddKeyGuide(button, key, new(diff, diff), true);
    }

    public static void SetHintOverlay(GameObject gameObj, bool isAidAction, KeyCode keyCode, string? action = null)
    {
        var button = gameObj.SetUpButton();
        var collider = gameObj.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.125f;
        button.OnMouseOver.AddListener(() => {
            string str = keyCode != KeyCode.None ? Language.Translate(isAidAction ? "ui.button.aidAction" : "ui.button.mainAction") + " : " + ButtonEffect.KeyCodeInfo.GetKeyDisplayName(keyCode) : "";
            if(action != null)
            {
                if (str.Length > 0) str += "<br>";
                str += "<line-indent=0.8em>" + Language.Translate("ui.action." + action!);
            }
            NebulaManager.Instance.SetHelpWidget(button, str);
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
    }

    static public GameObject? SetMouseActionIcon(GameObject button,bool show, ActionIconType actionType = ActionIconType.ClickAction, string? action = "mouseClick", bool atBottom = true)
    {
        if (!show)
        {
            button.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => { if (obj.name == "MouseAction") GameObject.Destroy(obj); }));
            return null;
        }
        else
        {
            GameObject obj = new("MouseAction");
            obj.transform.SetParent(button.transform);
            obj.layer = button.layer;
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.transform.localPosition = new(0.48f, atBottom ? -0.29f : 0.48f, -10f);
            renderer.sprite = actionType switch
            {
                ActionIconType.ClickAction => mouseActionSprite.GetSprite(),
                ActionIconType.NonclickAction => mouseDisableActionSprite.GetSprite(),
                ActionIconType.Info => infoSprite.GetSprite(),
                _ => null
            };

            if(action != null) SetHintOverlay(obj, false, KeyCode.None, action);

            return obj;
        }   
    }

    public enum ActionIconType
    {
        ClickAction,
        NonclickAction,
        Info
    }
}