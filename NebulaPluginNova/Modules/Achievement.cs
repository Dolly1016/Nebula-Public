using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Steamworks;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.SocialPlatforms.Impl;
using Virial;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;
using static Nebula.Modules.AbstractAchievement;

namespace Nebula.Modules;

abstract public class AchievementTokenBase
{
    public ProgressRecord Achievement { get; private init; }
    abstract public AbstractAchievement.ClearDisplayState UniteTo(bool update = true);

    public AchievementTokenBase(ProgressRecord achievement)
    {
        this.Achievement = achievement;

        NebulaGameManager.Instance?.AllAchievementTokens.Add(this);
    }
}

public class StaticAchievementToken : AchievementTokenBase
{
    public StaticAchievementToken(ProgressRecord record): base(record){}

    public StaticAchievementToken(string achievement)
        : base(NebulaAchievementManager.GetRecord(achievement, out var a) ? a : null!) {
        if (Achievement == null) NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Not found such achievement: " + achievement);
    }


    public override AbstractAchievement.ClearDisplayState UniteTo(bool update)
    {
        return Achievement?.Unite(1, update) ?? ClearDisplayState.None;
    }
}

public class AchievementToken<T> : AchievementTokenBase
{
    public T Value;
    public Func<T, ProgressRecord, int> Supplier { get; set; }

    public AchievementToken(ProgressRecord achievement, T value, Func<T, ProgressRecord, int> supplier) : base(achievement)
    {
        Value = value;
        Supplier = supplier;
    }

    public AchievementToken(string achievement, T value, Func<T, ProgressRecord, int> supplier) 
        : this(NebulaAchievementManager.GetRecord(achievement,out var a) ? a : null!, value,supplier) { }

    public AchievementToken(string achievement, T value, Func<T, ProgressRecord, bool> supplier)
        : this(achievement, value, (t,ac)=> supplier.Invoke(t,ac) ? 1 : 0) { }


    public override AbstractAchievement.ClearDisplayState UniteTo(bool update)
    {
        return Achievement.Unite(Supplier.Invoke(Value, (Achievement as ProgressRecord)!),update);
    }
}

public class SimpleAchievementToken : AchievementTokenBase
{
    public Func<bool> Supplier { get; set; }



    public SimpleAchievementToken(ProgressRecord achievement, Func<bool> supplier) : base(achievement)
    {
        Supplier = supplier;
    }
    public SimpleAchievementToken(string achievement, Func<bool> supplier) : this(NebulaAchievementManager.GetRecord(achievement, out var a) ? a : null!, supplier) { }

    public override AbstractAchievement.ClearDisplayState UniteTo(bool update)
    {
        if (Supplier.Invoke()) return Achievement.Unite(1, update);
        return ClearDisplayState.None;
    }
}

public static class AchievementTokens
{

    /// <summary>
    /// 条件に合わせてtriggeredを適当に立ててください。
    /// </summary>
    /// <param name="id"></param>
    /// <param name="player"></param>
    /// <param name="lifespan"></param>
    /// <returns></returns>
    public static AchievementToken<(bool triggered, bool blocked, bool isCleared)> FirstFailedAchievementToken(string id, GamePlayer player, ILifespan lifespan)
    {
        AchievementToken<(bool triggered, bool blocked, bool isCleared)> token = new(id, (false, false, false), (a, _) => a.isCleared);
        GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(ev => token.Value.blocked = token.Value.triggered, lifespan);
        GameOperatorManager.Instance?.Subscribe<PlayerDieEvent>(ev =>
        {
            //自身の死亡かつ死因が追放か推察
            if (ev.Player == player && (ev.Player.PlayerState == PlayerState.Exiled || ev.Player.PlayerState == PlayerState.Guessed))
                token.Value.isCleared |= token.Value.triggered && !token.Value.blocked;
        }, lifespan);
        return token;
    }

    public static AchievementTokenBase FunctionalToken(string id, Func<bool> checker)
    {
        AchievementToken<bool> token = new(id, false, (_, _) => checker.Invoke());
        return token;
    }
}

public class AchievementType
{
    static public readonly AchievementType Challenge = new("challenge");
    static public readonly AchievementType Secret = new("secret");
    static public readonly AchievementType Seasonal = new("seasonal");
    static public readonly AchievementType Costume = new("costume");
    static public readonly AchievementType Innersloth = new("innersloth");
    static public readonly AchievementType Perk = new("perk");

    static public readonly AchievementType Uljun = new("uljun");
    static public readonly AchievementType Mememura = new("meme");
    static private readonly Dictionary<string, AchievementType> CollabTypes;
    static public bool TryGetCollabType(string id, [MaybeNullWhen(false)] out AchievementType type) => CollabTypes.TryGetValue(id, out type);
    static AchievementType()
    {
        CollabTypes = [];
        CollabTypes.Add("uljun", Uljun);
        CollabTypes.Add("meme", Mememura);
    }
    private AchievementType(string key)
    {
        TranslationKey = "achievement.type." + key;
    }
    public string TranslationKey { get; private set; }
}

public class ProgressRecord
{
    private IntegerDataEntry entry;
    private string key;
    private string hashedKey;
    private int goal;
    private bool canClearOnce;

    public int Progress => entry.Value;
    public int Goal => goal;

    public bool IsCleared => !DebugTools.LockAllAchievement && (DebugTools.ReleaseAllAchievement || goal <= entry.Value);

    public string OldEntryTag => "a." + key.ComputeConstantHashAsString();
    public string EntryTag => "a." + this.hashedKey;
    public void AdoptBigger(int value)
    {
        if(entry.Value < value) entry.Value = value;
    }
    public ProgressRecord(string key, int goal, bool canClearOnce, string? defaultSource = null)
    {
        this.key = key;
        this.hashedKey = key.ComputeConstantHashAsStringLong();
        this.canClearOnce = canClearOnce;

        string? defaultSourceHashed = null;
        if (defaultSource != null) defaultSourceHashed = "a." + defaultSource.ComputeConstantHashAsStringLong();

        this.entry = new IntegerDataEntry("a." + hashedKey, NebulaAchievementManager.AchievementDataSaver, 0, defaultSourceHashed, DebugTools.WriteAllAchievementsData);
        this.goal = goal;
        if (NebulaAchievementManager.AllRecords.Any(r => r.entry.Name == this.entry.Name)) NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Duplicate achievement hash: " + key);
        NebulaAchievementManager.RegisterRecord(this, key);
    }

    public virtual string Id => key;
    public virtual string TranslationKey => "achievement." + key + ".title";
    public string GoalTranslationKey => "achievement." + key + ".goal";
    public string CondTranslationKey => "achievement." + key + ".cond";
    public string FlavorTranslationKey => "achievement." + key + ".flavor";

    protected void UpdateProgress(int newProgress) => entry.Value = newProgress;

    //トークンによってクリアする場合はこちらから
    virtual public ClearDisplayState Unite(int localValue, bool update)
    {
        if (localValue < 0) return ClearDisplayState.None;

        int lastValue = entry.Value;
        int newValue = Math.Min(goal, lastValue + localValue);
        if (update) entry.Value = newValue;

        if (newValue >= goal && lastValue < goal)
            return ClearDisplayState.FirstClear;

        if (localValue >= goal && !canClearOnce)
            return ClearDisplayState.Clear;

        return ClearDisplayState.None;
    }
}

public class DisplayProgressRecord : ProgressRecord
{
    string translationKey;
    public DisplayProgressRecord(string key, int goal, string translationKey, string? defaultSource = null) : base(key,goal, true, defaultSource)
    {
        this.translationKey = translationKey;
    }

    public override string TranslationKey => translationKey;
}

public interface INebulaAchievement
{
    public enum SocialMessageType
    {
        FirstCleared,
        Cleared,
        ClearedMultiple
    }

    static public readonly TextComponent HiddenComponent = new RawTextComponent("???");
    static public readonly TextComponent HiddenDescriptiveComponent = new ColorTextComponent(new Color(0.4f, 0.4f, 0.4f), new TranslateTextComponent("achievement.title.hidden"));
    static public readonly TextComponent HiddenDetailComponent = new ColorTextComponent(new Color(0.8f, 0.8f, 0.8f), new TranslateTextComponent("achievement.title.hiddenDetail"));
    static public readonly TextAttribute DetailTitleAttribute = GUI.API.GetAttribute(AttributeAsset.OverlayTitle);
    static public readonly TextAttribute SocialCaptionAttribute = new(GUI.API.GetAttribute(AttributeAsset.OverlayTitle)) { FontSize = new(1.3f) };
    static public readonly TextAttribute SocialCategoryAttribute= new(GUI.API.GetAttribute(AttributeAsset.OverlayTitle)) { FontSize = new(1.2f) };
    static public readonly TextAttribute SocialTitleAttribute = new(GUI.API.GetAttribute(AttributeAsset.OverlayTitle)) { FontSize = new(2f, 1f, 2f), Size = new(3f, 1f) };
    static private readonly TextAttribute DetailContentAttribute = GUI.API.GetAttribute(AttributeAsset.OverlayContent);

    string Id { get; }
    string TranslationKey => "achievement." + Id + ".title";
    string GoalTranslationKey => "achievement." + Id + ".goal";
    string CondTranslationKey => "achievement." + Id + ".cond";
    string FlavorTranslationKey => "achievement." + Id + ".flavor";

    int Trophy { get; }
    bool IsHidden { get; }
    bool IsCleared { get; }
    bool NoHint { get; }
    int Attention { get; }
    IEnumerable<DefinedAssignable> RelatedRole { get; }
    IEnumerable<AchievementType> AchievementType();
    Image? BackImage { get => SpecifiedBackImage ?? RelatedRole.FirstOrDefault()?.ConfigurationHolder?.Illustration; }
    Image? SpecifiedBackImage { get; }

    IEnumerable<string> GetKeywords()
    {
        foreach (var r in RelatedRole) yield return r.DisplayName;
        yield return Language.Translate(GoalTranslationKey);
        yield return Language.Translate(CondTranslationKey);
        if (IsCleared) {
            yield return Language.Translate(TranslationKey);
            yield return Language.Find(FlavorTranslationKey) ?? "";
        }
        foreach (var type in AchievementType()) yield return Language.Translate(type.TranslationKey);
    }
    Virial.Media.GUIWidget GetOverlayWidget(bool hiddenNotClearedAchievement = true, bool showCleared = false, bool showTitleInfo = false, bool showTrophy = false, bool showFlavor = false)
    {
        var gui = NebulaAPI.GUI;

        List<Virial.Media.GUIWidget> list = [];

        list.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, GetHeaderComponent()));

        List<Virial.Media.GUIWidget> titleList = [];
        if (showTrophy)
        {
            titleList.Add(new NoSGUIMargin(GUIAlignment.Left, new(-0.04f, 0.2f)));
            titleList.Add(new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => TrophySprite.GetSprite(Trophy)), new(0.3f, 0.3f)));
            titleList.Add(new NoSGUIMargin(GUIAlignment.Left, new(0.05f, 0.2f)));
        }

        titleList.Add(new NoSGUIText(GUIAlignment.Left, DetailTitleAttribute, GetTitleComponent(hiddenNotClearedAchievement ? HiddenDescriptiveComponent : null)));
        if (showCleared && IsCleared)
        {
            titleList.Add(new NoSGUIMargin(GUIAlignment.Left, new(0.2f, 0.2f)));
            titleList.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, gui.TextComponent(new(1f, 1f, 0f), "achievement.ui.cleared")));
        }
        list.Add(new HorizontalWidgetsHolder(GUIAlignment.Left, titleList));

        list.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, GetDetailComponent()));

        if (showFlavor)
        {
            var flavor = GetFlavorComponent();
            if (flavor != null)
            {
                list.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.12f)));
                list.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, flavor) { PostBuilder = text => text.outlineColor = Color.clear });
            }
        }

        if (showTitleInfo && IsCleared)
        {
            list.Add(new NoSGUIMargin(GUIAlignment.Left, new(0f, 0.2f)));
            list.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, new LazyTextComponent(() =>
            (NebulaAchievementManager.MyTitle == this) ?
            (Language.Translate("achievement.ui.equipped").Color(Color.green).Bold() + "<br>" + Language.Translate("achievement.ui.unsetTitle")) :
            Language.Translate("achievement.ui.setTitle"))));
        }
        return new VerticalWidgetsHolder(GUIAlignment.Left, list) { BackImage = BackImage, GrayoutedBackImage = !(IsCleared || !hiddenNotClearedAchievement) };
    }
    TextComponent? GetHeaderComponent()
    {
        List<TextComponent> list = new();
        foreach(var r in RelatedRole)
        {
            if (list.Count != 0) list.Add(new RawTextComponent(" & "));
            list.Add(NebulaGUIWidgetEngine.Instance.RawTextComponent(r.DisplayColoredName));
        }

        foreach(var type in AchievementType())
        {
            if (list.Count != 0) list.Add(new RawTextComponent(" "));
            list.Add(new TranslateTextComponent(type.TranslationKey));
        }

        if (list.Count > 0)
            return new CombinedTextComponent(list.ToArray());
        else
            return null;
    }
    TextComponent GetTitleComponent(TextComponent? hiddenComponent)
    {
        if (hiddenComponent != null && !IsCleared)
            return hiddenComponent;
        return new TranslateTextComponent(TranslationKey);
    }
    TextComponent? GetFlavorComponent()
    {
        var text = Language.Find(FlavorTranslationKey);
        if (text == null) return null;
        return new RawTextComponent($"<color=#e7e5ca><size=78%><i>{text}</i></size></color>");
    }
    Virial.Media.GUIWidget? GetDetailWidget() => null;
    TextComponent GetDetailComponent()
    {
        List<TextComponent> list = [];
        if (!NoHint || IsCleared)
            list.Add(new TranslateTextComponent(GoalTranslationKey));
        else
            list.Add(HiddenDetailComponent);
        list.Add(new LazyTextComponent(() =>
        {
            StringBuilder builder = new();
            var cond = Language.Translate(CondTranslationKey);
            if (cond.Length > 0)
            {
                builder.Append("<size=75%><br><br>");
                builder.Append(Language.Translate("achievement.ui.cond"));
                foreach (var c in cond.Split('+'))
                {
                    builder.Append("<br>  -");
                    builder.Append(c.Replace("<br>", "<br>    "));
                }
                builder.Append("</size>");
            }
            return builder.ToString();
        }));

        return new CombinedTextComponent(list.ToArray());
    }

    static private string GetSocialText(SocialMessageType type, string playerName, int others) => type switch { 
        SocialMessageType.FirstCleared => Language.Translate("achievement.social.firstClear").Replace("%PLAYER%", playerName.Sized(130)),
        SocialMessageType.Cleared => Language.Translate("achievement.social.clear").Replace("%PLAYER%", playerName.Sized(130)),
        SocialMessageType.ClearedMultiple => Language.Translate("achievement.social.clearMultiple").Replace("%PLAYER%", playerName.Sized(130)).Replace("%OTHERS%", others.ToString()),
        _ => "UNDEFINED MESSAGE"
    };
    Virial.Media.GUIWidget GetSocialWidget(SocialMessageType type, string playerName, int others = 0)
    {
        return GUI.API.VerticalHolder(GUIAlignment.Center,
            GUI.API.Text(GUIAlignment.Center, SocialCaptionAttribute, GUI.API.RawTextComponent(GetSocialText(type, playerName, others))),
            GUI.API.VerticalMargin(0.015f),
            GUI.API.Text(GUIAlignment.Center, SocialCategoryAttribute, GetHeaderComponent() ?? GUI.API.RawTextComponent("")),
            GUI.API.VerticalMargin(-0.03f),
            GUI.API.Text(GUIAlignment.Center, SocialTitleAttribute, GUI.API.FunctionalTextComponent(()=> string.Join("",GetTitleComponent(null).GetString().Select(c => 'あ' <= c && c <= 'ゔ' ? (c.ToString().Sized(90)) : c.ToString()))))
            );
    }

    IEnumerator CoShowSocialBillboard(Vector2 pos, SocialMessageType type, string playerName, int others = 0)
    {
        return ModSingleton<ShowUp>.Instance.CoShowSocial("SocialAchievement", pos, GetSocialWidget(type, playerName, others), (widget, size) =>
        {
            var button = widget.SetUpButton(true);
            button.gameObject.layer = LayerExpansion.GetUILayer();
            button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, GetOverlayWidget(false, true, IsCleared, true, true)));
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            if (IsCleared)
            {
                button.OnClick.AddListener(() =>
                {
                    NebulaAchievementManager.SetOrToggleTitle(this);
                    button.OnMouseOut.Invoke();
                    button.OnMouseOver.Invoke();
                });
            }
            var collider = button.gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size.ToUnityVector();
        }, 7.5f, null, false,considerPlayerAppeal: true, considerOnlyLobby: true);
    }

    //他のレコードの進捗によって勝手にクリアする場合にここから進捗を確認する。副作用のある関数。
    ClearDisplayState CheckClear() { return ClearDisplayState.None; }
}

public class AbstractAchievement : ProgressRecord, INebulaAchievement
{
    public static AchievementToken<(bool isCleared, bool triggered)> GenerateSimpleTriggerToken(string achievement) => new(achievement,(false,false),(val,_)=>val.isCleared);

    static public readonly IDividedSpriteLoader TrophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 100f, 4);

    bool isSecret;
    bool noHint;

    public IEnumerable<DefinedAssignable> role;
    public IEnumerable<AchievementType> type;
    public int Trophy { get; private init; }
    public bool NoHint => noHint;
    public IEnumerable<DefinedAssignable> RelatedRole => role;
    public IEnumerable<AchievementType> AchievementType() => type;
    public Image? SpecifiedBackImage { get; set; } = null;
    public int Attention { get; private init; }
    public bool IsHidden { get {
            return isSecret && !IsCleared;
        } }

    public AbstractAchievement(bool canClearOnce, bool isSecret, bool noHint, string key, int goal, IEnumerable<DefinedAssignable> role, IEnumerable<AchievementType> type, int trophy, int attention, Image? specifiedImage) : base(key, goal, canClearOnce) 
    {
        this.isSecret = isSecret;
        this.noHint = noHint;
        this.type = type;
        this.role = role;
        this.Trophy = trophy;
        this.Attention = attention;
        this.SpecifiedBackImage = specifiedImage;
    }

    /// <summary>
    /// クリア状況の表示
    /// </summary>
    public enum ClearDisplayState
    {
        Clear,
        FirstClear,
        None
    }
}

public class StandardAchievement : AbstractAchievement
{
    public StandardAchievement(bool canClearOnce, bool isSecret, bool noHint, string key, int goal, IEnumerable<DefinedAssignable> role, IEnumerable<AchievementType> type, int trophy,int attention, Image? specifiedImage)
        : base(canClearOnce, isSecret, noHint, key, goal, role, type, trophy, attention, specifiedImage)
    {
    }
}

public class InnerslothAchievement : INebulaAchievement
{
    private bool noHint;
    public bool NoHint => noHint;
    public int Attention => 0;

    public string Id { get; private init; }

    int INebulaAchievement.Trophy => 3;

    bool INebulaAchievement.IsHidden => false;
    Image? INebulaAchievement.SpecifiedBackImage => null;

    bool IsClearedSteam => SteamUserStats.GetAchievement(Id.Split('.', 2)[1], out var cleared) && cleared;
    bool INebulaAchievement.IsCleared
    {
        get
        {
            try
            {
                if (Constants.GetCurrentPlatformName() == "Steam")
                    return IsClearedSteam;
                else
                    return false;
            }
            catch 
            {
                //Steamプラットフォームでも初期化失敗で取得できないことがあるらしい。
                return false;
            }
        }
    }

    IEnumerable<DefinedAssignable> INebulaAchievement.RelatedRole => [];

    public InnerslothAchievement(bool noHint, string key)
    {
        Id = key;
        this.noHint = noHint;
        NebulaAchievementManager.RegisterNonrecord(this, key);
    }

    IEnumerable<AchievementType> INebulaAchievement.AchievementType()
    {
        yield return AchievementType.Innersloth;
    }
}

public class SumUpReferenceAchievement : INebulaAchievement
{
    public SumUpReferenceAchievement(bool isSecret, string key, string reference, int goal, IEnumerable<DefinedAssignable> role, IEnumerable<AchievementType> type, int trophy, int attention, Image? specifiedImage)
    {
        this.Id = key;
        this.Trophy = trophy;
        this.IsSecret = isSecret;
        this.goal = goal;
        this.reference = reference;
        this.RelatedRole = role;
        this.achievementType = type;
        this.Attention = attention;
        this.SpecifiedBackImage = specifiedImage;
        NebulaAchievementManager.RegisterNonrecord(this, key);
    }

    static readonly SpriteLoader gaugeSprite = SpriteLoader.FromResource("Nebula.Resources.ProgressGauge.png", 100f);

    static private TextAttribute OblongAttribute = new(GUI.Instance.GetAttribute(AttributeParams.Oblong)) { FontSize = new(1.6f), Size = new(0.6f, 0.2f), Color = new(163, 204, 220) };

    public string Id { get; private init; }
    public int Attention { get; private init; }

    public int Trophy { get; private init; }
    private bool IsSecret { get; init; }
    public bool IsHidden => IsSecret && !IsCleared;
    private int goal { get; init; }
    private string reference { get; init; }
    private ProgressRecord? referenceRecord = null;
    private readonly IEnumerable<AchievementType> achievementType =[];
    public Image? SpecifiedBackImage { get; set; }
    public ProgressRecord? ReferenceRecord { get
        {
            if(referenceRecord == null) NebulaAchievementManager.GetRecord(reference, out referenceRecord);
            return referenceRecord;
        } }

    public bool IsCleared => !DebugTools.LockAllAchievement && (DebugTools.ReleaseAllAchievement || (ReferenceRecord?.Progress ?? 0) >= goal);

    private bool lastCleared = false;
    ClearDisplayState INebulaAchievement.CheckClear() {
        bool alreadyCleared = lastCleared;
        bool isCleared = IsCleared;
        lastCleared = isCleared;
        if(isCleared && !alreadyCleared) return ClearDisplayState.FirstClear;
        return ClearDisplayState.None;
    }

    bool INebulaAchievement.NoHint => false;

    public IEnumerable<DefinedAssignable> RelatedRole { get; init; }

    protected virtual void OnWidgetGenerated(GameObject obj) { }
    Virial.Media.GUIWidget? INebulaAchievement.GetDetailWidget()
    {
        //クリア済み、あるいは1回で達成なら何も出さない
        if (IsCleared || goal == 1) return null;

        return new NoSGameObjectGUIWrapper(GUIAlignment.Left, () =>
        {
            var obj = UnityHelper.CreateObject("Progress", null, Vector3.zero, LayerExpansion.GetUILayer());
            var backGround = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, new Vector3(0f, 0f, 0f));
            var colored = UnityHelper.CreateObject<SpriteRenderer>("Colored", obj.transform, new Vector3(0f, 0f, -0.1f));

            backGround.sprite = gaugeSprite.GetSprite();
            backGround.color = new(0.21f, 0.21f, 0.21f);
            backGround.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            backGround.sortingOrder = 1;

            colored.sprite = gaugeSprite.GetSprite();
            colored.material.shader = NebulaAsset.ProgressShader;
            colored.sharedMaterial.SetFloat("_Guage", Mathf.Min(1f, (float)(referenceRecord?.Progress ?? 0) / (float)goal));
            colored.sharedMaterial.color = new(56f / 255f, 110f / 255f, 191f / 255f);
            colored.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            colored.sortingOrder = 2;

            var text = new NoSGUIText(GUIAlignment.Center, OblongAttribute, new RawTextComponent((referenceRecord?.Progress ?? 0) + "  /  " + goal)).Instantiate(new(1f, 0.2f), out _);
            text!.transform.SetParent(obj.transform);

            OnWidgetGenerated(obj);

            return (obj, new(2f, 0.17f));
        });
    }

    IEnumerable<AchievementType> INebulaAchievement.AchievementType() => achievementType;
}

public class SumUpAchievement : AbstractAchievement, INebulaAchievement
{
    public SumUpAchievement(bool isSecret, bool noHint, string key, int goal, IEnumerable<DefinedAssignable> role, IEnumerable<AchievementType> type, int trophy, int attention, Image? specifiedImage)
        : base(true, isSecret, noHint, key, goal, role, type, trophy, attention, specifiedImage)
    {
    }

    static readonly SpriteLoader gaugeSprite = SpriteLoader.FromResource("Nebula.Resources.ProgressGauge.png", 100f);

    static private TextAttribute OblongAttribute = new(GUI.Instance.GetAttribute(AttributeParams.Oblong)) { FontSize = new(1.6f), Size = new(0.6f, 0.2f), Color = new(163,204,220) };
    protected virtual void OnWidgetGenerated(GameObject obj) { }
    Virial.Media.GUIWidget? INebulaAchievement.GetDetailWidget()
    {
        //クリア済みなら何も出さない
        if (IsCleared) return null;

        return new NoSGameObjectGUIWrapper(GUIAlignment.Left, () =>
        {
            var obj = UnityHelper.CreateObject("Progress", null, Vector3.zero, LayerExpansion.GetUILayer());
            var backGround = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, new Vector3(0f, 0f, 0f));
            var colored = UnityHelper.CreateObject<SpriteRenderer>("Colored", obj.transform, new Vector3(0f, 0f, -0.1f));

            backGround.sprite = gaugeSprite.GetSprite();
            backGround.color = new(0.21f, 0.21f, 0.21f);
            backGround.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            backGround.sortingOrder = 1;

            colored.sprite = gaugeSprite.GetSprite();
            colored.material.shader = NebulaAsset.ProgressShader;
            colored.sharedMaterial.SetFloat("_Guage", Mathf.Min(1f, (float)Progress / (float)Goal));
            colored.sharedMaterial.color = new(56f / 255f, 110f / 255f, 191f / 255f);
            colored.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            colored.sortingOrder = 2;

            var text = new NoSGUIText(GUIAlignment.Center, OblongAttribute, new RawTextComponent(Progress + "  /  " + Goal)).Instantiate(new(1f,0.2f),out _);
            text!.transform.SetParent(obj.transform);

            OnWidgetGenerated(obj);

            return (obj, new(2f, 0.17f));
        });
    }
}

public class CompleteAchievement : SumUpAchievement, INebulaAchievement
{
    ProgressRecord[] records;
    public CompleteAchievement(ProgressRecord[] allRecords, bool isSecret, bool noHint, string key, IEnumerable<DefinedAssignable> role, IEnumerable<AchievementType> type, int trophy, int attention, Image? specifiedImage)
        : base(isSecret, noHint, key, allRecords.Length, role,type, trophy, attention, specifiedImage) {
        this.records = allRecords;
    }

    ClearDisplayState INebulaAchievement.CheckClear() {
        bool wasCleared = IsCleared;
        UpdateProgress(records.Count(r => r.IsCleared));

        if(!wasCleared) return IsCleared ? ClearDisplayState.FirstClear : ClearDisplayState.None;
        return ClearDisplayState.None;
    }

    static private TextAttribute TextAttr = new(GUI.Instance.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.25f) };
    protected override void OnWidgetGenerated(GameObject obj) {
        var collider = UnityHelper.CreateObject<BoxCollider2D>("Overlay", obj.transform, Vector3.zero);
        collider.size = new(2f, 0.17f);
        collider.isTrigger = true;

        var button = collider.gameObject.SetUpButton();
        button.OnMouseOver.AddListener(() =>
        {
            string text = string.Join("\n", records.Select(r => "- " + Language.Translate(r.TranslationKey).Color(r.IsCleared ? Color.green : Color.white)));
            NebulaManager.Instance.SetHelpWidget(button, new NoSGUIText(GUIAlignment.Left, TextAttr, new RawTextComponent(text)));
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
    }
}

[NebulaPreprocess(PreprocessPhase.PostFixStructure)]
[NebulaRPCHolder]
static public class NebulaAchievementManager
{
    static public readonly DataSaver AchievementDataSaver = new("Achievements");
    static private readonly Dictionary<string, ProgressRecord> allRecords = [];
    static private readonly Dictionary<string, INebulaAchievement> allNonrecords = [];
    static private readonly Dictionary<long, INebulaAchievement> fastAchievements = [];
    static private readonly StringDataEntry myTitleEntry = new("MyTitle", AchievementDataSaver, "-");
    static private readonly List<INebulaAchievement> allAchievements = [];
    static private readonly List<GameStatsEntry> allStats = [];

    static private INebulaAchievement[] LastFirstClearedArchive = [];
    static private List<INebulaAchievement> ClearedAllOrderedArchive = [];
    static public IEnumerable<INebulaAchievement> RecentlyCleared => ClearedAllOrderedArchive;
    static private HashSet<INebulaAchievement> ClearedArchive = [];

    static public IEnumerable<ProgressRecord> AllRecords => allRecords.Values;
    static public IEnumerable<INebulaAchievement> AllAchievements => allAchievements;
    static public IEnumerable<GameStatsEntry> AllStats => allStats;

    static public bool TryGetAchievement(long hash, [MaybeNullWhen(false)] out INebulaAchievement? achievement) => fastAchievements.TryGetValue(hash, out achievement);
    static public INebulaAchievement? MyTitle { get {
            if (GetAchievement(myTitleEntry.Value, out var achievement) && achievement.IsCleared)
                return achievement;
            return null;
        }
        set {
            if (value?.IsCleared ?? false)
                myTitleEntry.Value = value.Id;
            else
                myTitleEntry.Value = "-";

            if (PlayerControl.LocalPlayer && !ShipStatus.Instance) Certification.RpcShareAchievement.Invoke((PlayerControl.LocalPlayer.PlayerId, myTitleEntry.Value));
        }
    }

    static public void SetOrToggleTitle(INebulaAchievement? achievement)
    {
        if (achievement == null || MyTitle == achievement)
            MyTitle = null;
        else
            MyTitle = achievement;
    }

    static public (int num,int max, int hidden)[] Aggregate(Predicate<INebulaAchievement>? predicate)
    {
        (int num, int max, int hidden)[] result = new (int num, int max, int hidden)[4];
        for (int i = 0; i < result.Length; i++) result[i] = (0, 0, 0);
        return AllAchievements.Where(a => predicate?.Invoke(a) ?? true).Aggregate(result,
            (ac,achievement) => {
                if (!achievement.IsHidden)
                {
                    ac[achievement.Trophy].max++;
                    if (achievement.IsCleared) ac[achievement.Trophy].num++;
                }
                else
                {
                    ac[achievement.Trophy].hidden++;
                }
                return ac;
            });
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor) {
        yield return preprocessor.SetLoadingText("Loading Titles");

        {
            int num = 0;
            PlayerState.AllDeadStates.Do(state =>
            {
                RegisterStats("stats.kill." + state.TranslateKey, GameStatsCategory.Kill, null, new LazyTextComponent(() => Language.Translate("stats.common.kill").Replace("%STATE%", state.Text)), -num);
                RegisterStats("stats.death." + state.TranslateKey, GameStatsCategory.Death, null, new LazyTextComponent(() => Language.Translate("stats.common.death").Replace("%STATE%", state.Text)), -num);
                num++;
            });
        }
        GameEnd.AllEndConditions.Do(end =>
        {
            RegisterStats("stats.end.win." + end.ImmutableId, GameStatsCategory.Game, null, new LazyTextComponent(() => Language.Translate("stats.common.win").Replace("%END%", end.DisplayText.GetString().Replace("%EXTRA%", "").Color(end.Color))), 80);
            RegisterStats("stats.end.lose." + end.ImmutableId, GameStatsCategory.Game, null, new LazyTextComponent(() => Language.Translate("stats.common.defeat").Replace("%END%", end.DisplayText.GetString().Replace("%EXTRA%", "").Color(end.Color))), 70);
        });
        RegisterStats("stats.gamePlay", GameStatsCategory.Game, null, null, 81);
        RegisterStats("stats.plants.gain.normal", GameStatsCategory.Perks, null, null, 101);
        RegisterStats("stats.plants.gain.warped", GameStatsCategory.Perks, null, null, 100);

        Roles.Roles.AllRoles.Do(r =>
        {
            RegisterStats("stats.role." + r.Id + ".assigned", GameStatsCategory.Roles, r, new LazyTextComponent(() => Language.Translate("stats.role.common.assigned").Replace("%ROLE%", r.DisplayColoredName)), 101);
            RegisterStats("stats.role." + r.Id + ".won", GameStatsCategory.Roles, r, new LazyTextComponent(() => Language.Translate("stats.role.common.won").Replace("%ROLE%", r.DisplayColoredName)), 100);
        });
        Roles.Roles.AllModifiers.Do(m =>
        {
            RegisterStats("stats.modifier." + m.Id + ".assigned", GameStatsCategory.Roles, m, new LazyTextComponent(() => Language.Translate("stats.modifier.common.assigned").Replace("%MODIFIER%", m.DisplayColoredName)), 101);
            RegisterStats("stats.modifier." + m.Id + ".won", GameStatsCategory.Roles, m, new LazyTextComponent(() => Language.Translate("stats.modifier.common.won").Replace("%MODIFIER%", m.DisplayColoredName)), 100);
        });
        Roles.Roles.AllGhostRoles.Do(g =>
        {
            RegisterStats("stats.ghostRole." + g.Id + ".assigned", GameStatsCategory.Roles, g, new LazyTextComponent(() => Language.Translate("stats.ghostRole.common.assigned").Replace("%GHOSTROLE%", g.DisplayColoredName)), 101);
            RegisterStats("stats.ghostRole." + g.Id + ".won", GameStatsCategory.Roles, g, new LazyTextComponent(() => Language.Translate("stats.ghostRole.common.won").Replace("%GHOSTROLE%", g.DisplayColoredName)), 100);
        });
        Roles.Roles.AllPerks.Do(p => RegisterStats("stats.perk." + p.Id + ".gain", GameStatsCategory.Perks, null, new LazyTextComponent(()=> Language.Translate("stats.perk.common.gain").Replace("%PERK%", p.PerkDefinition.DisplayName.Color(p.PerkDefinition.perkColor))), 50));

        NebulaAchievementManager.SortStats();

        //組み込みレコード
        ProgressRecord[] killRecord = new TranslatableTag[] { 
            PlayerState.Dead,
            PlayerState.Sniped,
            PlayerState.Beaten,
            PlayerState.Guessed,
            PlayerState.Embroiled,
            PlayerState.Trapped,
            PlayerState.Cursed,
            PlayerState.Crushed,
            PlayerState.Frenzied,
            PlayerState.Bubbled,
            PlayerState.Meteor,
            PlayerState.Balloon,
        }.Select(tag => new DisplayProgressRecord("kill." + tag.TranslateKey, 1, tag.TranslateKey)).ToArray();
        ProgressRecord[] deathRecord = new TranslatableTag[] { 
            PlayerState.Dead,
            PlayerState.Exiled,
            PlayerState.Misfired,
            PlayerState.Sniped,
            PlayerState.Beaten,
            PlayerState.Guessed,
            PlayerState.Misguessed,
            PlayerState.Embroiled,
            PlayerState.Suicide,
            PlayerState.Trapped,
            PlayerState.Pseudocide,
            PlayerState.Deranged,
            PlayerState.Cursed,
            PlayerState.Crushed,
            PlayerState.Frenzied,
            PlayerState.Gassed,
            PlayerState.Bubbled,
            PlayerState.Meteor,
            PlayerState.Starved,
            PlayerState.Balloon,
            PlayerState.Lost,
        }.Select(tag => new DisplayProgressRecord("death." + tag.TranslateKey, 1, tag.TranslateKey)).ToArray();


        //読み込み
        using var reader = new StreamReader(NebulaResourceManager.NebulaNamespace.GetResource("Achievements.dat")!.AsStream()!);

        List<ProgressRecord> recordsList = new();

        List<AchievementType> types = new();
        
        while (true) {
            types.Clear();

            var line = reader.ReadLine();
            if(line == null) break;

            if (line.StartsWith("#")) continue;

            var args = line.Split(',');

            if (args.Length < 2) continue;

            bool clearOnce = false;
            bool noHint = false;
            bool secret = false;
            bool isNotChallenge = false;
            bool isRecord = false;
            bool innersloth = false;
            string? reference = null;
            string? defaultSource = null;
            int attention = 0;
            IEnumerable<ProgressRecord>? records = recordsList;

            IEnumerable<DefinedAssignable> relatedRoles = [];
            Image? specifiedImage = null;

            int rarity = int.Parse(args[1]);
            int goal = 1;
            for (int i = 2;i<args.Length - 1; i++)
            {
                var arg =args[i];

                switch (arg)
                {
                    case "once":
                        clearOnce = true;
                        break;
                    case "noHint":
                        noHint = true;
                        break;
                    case "secret":
                        secret = true;
                        break;
                    case "seasonal":
                        types.Add(AchievementType.Seasonal);
                        break;
                    case "costume":
                        types.Add(AchievementType.Costume);
                        break;
                    case "perk":
                        types.Add(AchievementType.Perk);
                        break;
                    case "nonChallenge":
                        isNotChallenge = true;
                        break;
                    case "innersloth":
                        innersloth = true;
                        break;
                    case string a when a.StartsWith("goal-"):
                        goal = int.Parse(a.Substring(5));
                        break;
                    case "builtIn-kill":
                        records = killRecord;
                        break;
                    case "builtIn-death":
                        records = deathRecord;
                        break;
                    case "isRecord":
                        isRecord = true;
                        break;
                    case string a when a.StartsWith("record-"):
                        if (allRecords.TryGetValue(a.Substring(7), out var r))
                            recordsList.Add(r);
                        else
                            NebulaPlugin.Log.Print(NebulaLog.LogLevel.FatalError, "The record \"" + a.Substring(7) + "\" was not found.");
                        break;
                    case string a when a.StartsWith("sync-"):
                        reference = a.Substring(5);
                        break;
                    case string a when a.StartsWith("default-"):
                        defaultSource = a.Substring(8);
                        break;
                    case string a when a.StartsWith("a-"):
                        if (int.TryParse(a.AsSpan(2), out var val)) attention = val;
                        break;
                    case string a when a.StartsWith("image-role-"):
                        var roleName = a.Substring(11);
                        specifiedImage = Roles.Roles.AllAssignables().FirstOrDefault(a => a.LocalizedName == roleName)?.ConfigurationHolder?.Illustration;
                        break;
                    case string a when a.StartsWith("image-combi-"):
                        var combiName = a.Substring(12);
                        specifiedImage = CombiImageInfo.FastImages.TryGetValue(combiName.HeadUpper(), out var combiInfo) ? combiInfo.Image : null;
                        break;
                    case string a when a.StartsWith("image-"):
                        specifiedImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Achievements/" + a.Substring(6) + ".png");
                        break;
                    case string a when a.StartsWith("collab-"):
                        if(AchievementType.TryGetCollabType(a.Substring(7), out var aType)) types.Add(aType);
                        break;
                }
            }

            if (secret) types.Add(AchievementType.Secret);

            var nameSplitted = args[0].Split('.');
            if(nameSplitted.Length > 1)
            {
                if (nameSplitted[0] == "combination" && nameSplitted.Length > 2 && int.TryParse(nameSplitted[1], out var num) && nameSplitted.Length >= 2 + num)
                {
                    relatedRoles = Helpers.Sequential(num).Select(i =>
                    {
                        var roleName = nameSplitted[2 + i].Replace('-', '.');
                        return Roles.Roles.AllAssignables().FirstOrDefault(a => a.LocalizedName == roleName);
                    }).Where(r => r != null).ToArray()!;
                    if (rarity == 2 && !isNotChallenge) types.Add(AchievementType.Challenge);
                }
                else
                {
                    nameSplitted[0] = nameSplitted[0].Replace('-', '.');
                    var cand = Roles.Roles.AllAssignables().FirstOrDefault(a => a.LocalizedName == nameSplitted[0]);
                    if (cand != null)
                    {
                        relatedRoles = [cand];
                        if (rarity == 2 && !isNotChallenge)
                        {
                            types.Add(AchievementType.Challenge);
                            if (attention < 80) attention = 80;
                        }
                    }
                }
            }

            if (innersloth)
                new InnerslothAchievement(noHint, args[0]);
            else if (isRecord)
                new DisplayProgressRecord(args[0], goal, "record." + args[0], defaultSource);
            else if (!records.IsEmpty())
                new CompleteAchievement(records.ToArray(), secret, noHint, args[0], relatedRoles, types.ToArray(), rarity, attention, specifiedImage);
            else if (reference != null)
                new SumUpReferenceAchievement(secret, args[0], reference, goal, relatedRoles, types.ToArray(), rarity, attention, specifiedImage);
            else if (goal > 1)
                new SumUpAchievement(secret, noHint, args[0], goal, relatedRoles, types.ToArray(), rarity, attention, specifiedImage);
            else
                new StandardAchievement(clearOnce, secret, noHint, args[0], goal, relatedRoles, types.ToArray(), rarity, attention, specifiedImage);

            if (recordsList.Count > 0) recordsList.Clear();
        }

        foreach (var achievement in AllAchievements) achievement.CheckClear();
    }

    static private void RegisterAchievement(INebulaAchievement ach)
    {
        allAchievements.Add(ach);

        long hash = ach.Id.ComputeConstantLongHash();
        if (!fastAchievements.TryAdd(hash, ach)) NebulaPlugin.Log.Print($"Duplicated Achievement! (Hash: {hash}, Achievement: {ach.Id} & {fastAchievements[hash].Id})");
    }
    static internal GameStatsEntry RegisterStats(string id, GameStatsCategory category, DefinedAssignable? relatedAssignable, TextComponent? displayName = null, int innerPriority = 0)
    {
        var record = new ProgressRecord(id, 1000000, false);
        var statsEntry = new GameStatsEntryImpl(record, category, relatedAssignable, displayName, innerPriority);
        allStats.Add(statsEntry);
        return statsEntry;
    }
    static internal void SortStats()
    {
        static string AssignableToStr(DefinedAssignable? assignable)
        {
            if (assignable == null) return "4";
            if (assignable is DefinedRole role) return "1." + (int)role.Category + "." + role.InternalName;
            if (assignable is DefinedModifier) return "2." + assignable.InternalName;
            if (assignable is DefinedGhostRole) return "3." + assignable.InternalName;
            return "5";
        }
        allStats.Sort((stats1, stats2) =>
        {
            if (stats1.Category != stats2.Category) return (int)stats1.Category - (int)stats2.Category;
            if (stats1.RelatedAssignable != stats2.RelatedAssignable)
            {
                int comp = AssignableToStr(stats1.RelatedAssignable).CompareTo(AssignableToStr(stats2.RelatedAssignable));
                if (comp != 0) return comp;
            }
            if (stats1.InnerPriority != stats2.InnerPriority) return stats2.InnerPriority - stats1.InnerPriority;
            return stats1.Id.CompareTo(stats2.Id);
        });
    }
    static internal void RegisterRecord(ProgressRecord progressRecord,string id)
    {
        allRecords[id] = progressRecord;
        if (progressRecord is INebulaAchievement ach) RegisterAchievement(ach);
    }

    static internal void RegisterNonrecord(INebulaAchievement achievement, string id)
    {
        allNonrecords[id] = achievement;
        RegisterAchievement(achievement);
    }

    static public bool GetRecord(string id, [MaybeNullWhen(false)] out ProgressRecord record)
    {
        return allRecords.TryGetValue(id, out record);
    }

    static public bool GetAchievement(string id, [MaybeNullWhen(false)] out INebulaAchievement achievement)
    {
        achievement = (allRecords.TryGetValue(id, out var rec) && rec is AbstractAchievement ach) ? ach : null;
        if (achievement == null) allNonrecords.TryGetValue(id, out achievement);
        return achievement != null;
    }

    static public void ClearHistory()
    {
        LastFirstClearedArchive = [];
    }
    static public (INebulaAchievement achievement, AbstractAchievement.ClearDisplayState clearState)[] UniteAll()
    {
        List<(INebulaAchievement achievement, AbstractAchievement.ClearDisplayState clearState)> result  =[];

        //トークンによるクリア
        foreach (var token in NebulaGameManager.Instance!.AllAchievementTokens)
        {
            var state = token.UniteTo();
            if (state == AbstractAchievement.ClearDisplayState.None) continue;

            //実績のみ結果に表示(他実績用のレコードは対象外)
            if(token.Achievement is AbstractAchievement ach && result.All(a => a.achievement != ach)) result.Add(new(ach, state));
        }

        //他レコードの更新によるクリア
        foreach(var achievement in AllAchievements)
        {
            var state = achievement.CheckClear();
            if (state == AbstractAchievement.ClearDisplayState.None) continue;
            result.Add(new(achievement, state));
        }

        result.Sort((val1, val2) => val1.clearState - val2.clearState);//昇順

        //履歴への追加
        var lastFirstCleared = result.Where(r => r.clearState == ClearDisplayState.FirstClear && r.achievement.Attention >= 50).Select(r => r.achievement).ToArray();
        LastFirstClearedArchive = lastFirstCleared;
        result.Where(r => r.achievement.Attention >= 80).Do(r => ClearedArchive.Add(r.achievement));

        //無条件に追加するクリア履歴
        ClearedAllOrderedArchive.RemoveAll(a => result.Any(r => r.achievement == a));
        ClearedAllOrderedArchive.InsertRange(0, result.Select(r => r.achievement));
        if (ClearedAllOrderedArchive.Count > 10) ClearedAllOrderedArchive.RemoveRange(10, ClearedAllOrderedArchive.Count - 10);

        //重複を許さない
        return result.DistinctBy(a=>a.achievement).ToArray();
    }

    static XOnlyDividedSpriteLoader trophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 220f, 4);
    static public bool HasAnyAchievementResult { get; private set; } = false;
    static public IEnumerator CoShowAchievements(MonoBehaviour coroutineHolder, params (INebulaAchievement achievement, AbstractAchievement.ClearDisplayState clearState)[] achievements)
    {
        HasAnyAchievementResult = true;

        int num = 0;
        (GameObject holder, GameObject animator, GameObject body, SpriteRenderer white) CreateBillboard(INebulaAchievement achievement, AbstractAchievement.ClearDisplayState clearState)
        {
            var billboard = UnityHelper.CreateObject("Billboard", null, new Vector3(3.85f, 1.75f - (float)num * 0.6f, -100f));
            var animator = UnityHelper.CreateObject("Animator", billboard.transform, new Vector3(0f, 0f, 0f));
            var body = UnityHelper.CreateObject("Body", animator.transform, new Vector3(0f, 0f, 0f));
            var background = UnityHelper.CreateObject<SpriteRenderer>("Background", body.transform, new Vector3(0f,0f,1f));
            var white = UnityHelper.CreateObject<SpriteRenderer>("White", animator.transform, new Vector3(0f, 0f, -2f));
            var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", body.transform, new Vector3(-0.95f, 0f, 0f));

            background.color = clearState == AbstractAchievement.ClearDisplayState.FirstClear ? Color.yellow : new UnityEngine.Color(0.7f, 0.7f, 0.7f);

            billboard.AddComponent<SortingGroup>();

            new MetaWidgetOld.Text(new(Nebula.Utilities.TextAttributeOld.BoldAttr) { Font = VanillaAsset.BrookFont, Size = new(2f, 0.4f), FontSize = 1.16f, FontMaxSize = 1.16f, FontMinSize  = 1.16f }) { MyText = achievement.GetHeaderComponent() }.Generate(body, new Vector2(0.25f, 0.13f), out _);
            new MetaWidgetOld.Text(new(Nebula.Utilities.TextAttributeOld.NormalAttr) { Font = VanillaAsset.BrookFont, Size = new(2f, 0.4f) }) { MyText = achievement.GetTitleComponent(null) }.Generate(body, new Vector2(0.25f, -0.06f), out _);

            foreach (var renderer in new SpriteRenderer[] { background, white }) {
                renderer.sprite = VanillaAsset.TextButtonSprite;
                renderer.drawMode = SpriteDrawMode.Sliced;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(2.6f, 0.55f);
            }
            num++;

            var collider = billboard.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(2.6f, 0.55f);
            var button = billboard.SetUpButton();
            button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, achievement.GetOverlayWidget(true, false, true, false, true)));
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            button.OnClick.AddListener(() => {
                NebulaAchievementManager.SetOrToggleTitle(achievement);
                button.OnMouseOut.Invoke();
                button.OnMouseOver.Invoke();
            });

            white.material.shader = NebulaAsset.WhiteShader;
            icon.sprite = trophySprite.GetSprite(achievement.Trophy);

            return (billboard, animator, body, white);
        }

        IEnumerator CoShowFirstClear((GameObject holder, GameObject animator, GameObject body, SpriteRenderer white) billboard)
        {
            IEnumerator Shake(Transform target, float duration, float halfWidth)
            {
                Vector3 origin = target.localPosition;
                for (float timer = 0f; timer < duration; timer += Time.deltaTime)
                {
                    float num = timer / duration;
                    Vector3 vector = UnityEngine.Random.insideUnitCircle * halfWidth;
                    target.localPosition = origin + vector;
                    yield return null;
                }
                target.localPosition = origin;
                yield break;
            }

            billboard.body.SetActive(false);
            billboard.holder.transform.localScale = Vector3.one * 1.1f;

            
            coroutineHolder.StartCoroutine(ManagedEffects.Sequence(
                Shake(billboard.animator.transform, 0.1f, 0.01f),
                Shake(billboard.animator.transform, 0.2f, 0.02f),
                Shake(billboard.animator.transform, 0.3f, 0.03f),
                Shake(billboard.animator.transform, 0.3f, 0.04f)
                ));

            float t;
            
            t = 0f;
            while(t < 0.9f)
            {
                billboard.holder.transform.localScale = Vector3.one * (1.1f + (t / 0.9f * 0.2f));
                billboard.white.color = new Color(1f, 1f, 1f, t / 0.9f);
                t += Time.deltaTime;
                yield return null;
            }

            billboard.body.SetActive(true);

            float p = 1f;
            while (p > 0.0001f)
            {
                billboard.holder.transform.localScale = Vector3.one * (1f + (p * 0.2f));
                billboard.white.color = new Color(1f, 1f, 1f, p);
                p -= p * 5f * Time.deltaTime; 
                yield return null;
            }

            billboard.white.gameObject.SetActive(false);
            billboard.holder.transform.localScale = Vector3.one;
        }

        IEnumerator CoShowClear((GameObject holder, GameObject animator, GameObject body, SpriteRenderer white) billboard)
        {
            billboard.white.gameObject.SetActive(false);
            float p = 3f;
            while (p > 0.0001f)
            {
                billboard.animator.transform.localPosition = new Vector3(p, 0f, 0f);
                p -= p * 8f * Time.deltaTime;
                yield return null;
            }
            billboard.animator.transform.localPosition = Vector3.zero;
        }


        yield return new WaitForSeconds(1.5f);

        foreach (var ach in achievements)
        {
            var billboard = CreateBillboard(ach.achievement,ach.clearState);

            if (ach.clearState == AbstractAchievement.ClearDisplayState.FirstClear)
            {
                coroutineHolder.StartCoroutine(CoShowFirstClear(billboard).WrapToIl2Cpp());
                yield return new WaitForSeconds(1.05f);
            }
            else
            {
                coroutineHolder.StartCoroutine(CoShowClear(billboard).WrapToIl2Cpp());
                yield return new WaitForSeconds(0.45f);
            }
            yield return null;
        }

        yield break;
    }

    static public readonly RemoteProcess<(string achievement, GamePlayer player)> RpcClearAchievement = new("ClearAchievement", (message, _) =>
    {
        if (message.player.AmOwner) new StaticAchievementToken(message.achievement);
    });
    static public RemoteProcess<(string achievement, GamePlayer player)> RpcProgressStats => RpcClearAchievement;

    static public void SendLastClearedAchievements()
    {
        if(LastFirstClearedArchive.Length > 0) RpcShareClearedAchievement.Invoke((PlayerControl.LocalPlayer.name, LastFirstClearedArchive));
    }

    static public void SendPickedUpAchievements()
    {
        if (ClearedArchive.Count > 0) RpcSharePickedUpAchievement.Invoke((PlayerControl.LocalPlayer.name, ClearedArchive.ToArray()));
    }

    static public readonly RemoteProcess<(string playerName, INebulaAchievement[] achievements)> RpcShareClearedAchievement = new("ShareClearedAchievement", (message, _) =>
    {
        ModSingleton<ShowUp>.Instance?.PutLastClearedAchievements(message.playerName, message.achievements);
    });

    public static readonly RemoteProcess<(string playerName, INebulaAchievement[] achievements)> RpcSharePickedUpAchievement = new("SharePickedUpAchievement", (message, _) =>
    {
        ModSingleton<ShowUp>.Instance?.PutPickedUpAchievements(message.playerName, message.achievements);
    });
}

file class GameStatsEntryImpl : GameStatsEntry
{
    private ProgressRecord myRecord;
    private GameStatsCategory category;
    private DefinedAssignable? relatedAssignable;
    private TextComponent? displayName;
    private int innerPriority = 0;
    public GameStatsEntryImpl(ProgressRecord record, GameStatsCategory category, DefinedAssignable? relatedAssignable, TextComponent? displayName, int innerPriority)
    {
        this.myRecord = record;
        this.category = category;
        this.relatedAssignable = relatedAssignable;
        this.displayName = displayName;
        this.innerPriority = innerPriority;
    }
    string GameStatsEntry.Id => myRecord.Id;
    TextComponent GameStatsEntry.DisplayName => displayName ?? GUI.API.LocalizedTextComponent(myRecord.Id);
    int GameStatsEntry.Progress => myRecord.Progress;
    GameStatsCategory GameStatsEntry.Category => category;
    DefinedAssignable? GameStatsEntry.RelatedAssignable => relatedAssignable;
    int GameStatsEntry.InnerPriority => innerPriority;
}
