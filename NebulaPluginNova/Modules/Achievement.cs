using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine.Rendering;
using Virial;
using Virial.Assignable;
using Virial.Media;
using Virial.Runtime;
using Virial.Text;
using static Nebula.Modules.AbstractAchievement;

namespace Nebula.Modules;

abstract public class AchievementTokenBase : IReleasable, ILifespan
{
    public ProgressRecord Achievement { get; private init; }
    abstract public AbstractAchievement.ClearState UniteTo(bool update = true);

    public AchievementTokenBase(ProgressRecord achievement)
    {
        this.Achievement = achievement;

        NebulaGameManager.Instance?.AllAchievementTokens.Add(this);
    }
    public bool IsDeadObject { get; private set; } = false;

    public void Release()
    {
        IsDeadObject = true;
        NebulaGameManager.Instance?.AllAchievementTokens.Remove(this);
    }
}

public class StaticAchievementToken : AchievementTokenBase
{
    public StaticAchievementToken(ProgressRecord record): base(record){}

    public StaticAchievementToken(string achievement)
        : base(NebulaAchievementManager.GetAchievement(achievement, out var a) ? a : null!) { }


    public override AbstractAchievement.ClearState UniteTo(bool update)
    {
        if (IsDeadObject) return AbstractAchievement.ClearState.NotCleared;

        return Achievement.Unite(1, update);
    }
}

public class AchievementToken<T> : AchievementTokenBase
{
    public T Value;
    public Func<T,AbstractAchievement,int> Supplier { get; set; }

    public AchievementToken(AbstractAchievement achievement, T value, Func<T, AbstractAchievement, int> supplier) : base(achievement)
    {
        Value = value;
        Supplier = supplier;
    }

    public AchievementToken(string achievement, T value, Func<T, AbstractAchievement, int> supplier) 
        : this(NebulaAchievementManager.GetAchievement(achievement,out var a) ? a : null!, value,supplier) { }

    public AchievementToken(string achievement, T value, Func<T, AbstractAchievement, bool> supplier)
        : this(achievement, value, (t,ac)=> supplier.Invoke(t,ac) ? 1 : 0) { }


    public override AbstractAchievement.ClearState UniteTo(bool update)
    {
        if (IsDeadObject) return AbstractAchievement.ClearState.NotCleared;

        return Achievement.Unite(Supplier.Invoke(Value, (Achievement as AbstractAchievement)!),update);
    }
}

public class AchievementType
{
    static public AchievementType Challenge = new("challenge");
    static public AchievementType Secret = new("secret");
    static public AchievementType Seasonal = new("seasonal");

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

    public bool IsCleared => goal <= entry.Value;

    public string OldEntryTag => "a." + key.ComputeConstantHashAsString();
    public string EntryTag => "a." + this.hashedKey;
    public void AdoptBigger(int value)
    {
        if(entry.Value < value) entry.Value = value;
    }
    public ProgressRecord(string key, int goal, bool canClearOnce)
    {
        this.key = key;
        this.hashedKey = key.ComputeConstantHashAsStringLong();
        this.canClearOnce = canClearOnce;
        this.entry = new IntegerDataEntry("a." + hashedKey, NebulaAchievementManager.AchievementDataSaver, 0);
        this.goal = goal;
        if (NebulaAchievementManager.AllRecords.Any(r => r.entry.Name == this.entry.Name)) NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Duplicate achievement hash: " + key);
        NebulaAchievementManager.RegisterAchievement(this, key);
    }

    public virtual string Id => key;
    public virtual string TranslationKey => "achievement." + key + ".title";
    public string GoalTranslationKey => "achievement." + key + ".goal";
    public string CondTranslationKey => "achievement." + key + ".cond";
    public string FlavorTranslationKey => "achievement." + key + ".flavor";

    protected void UpdateProgress(int newProgress) => entry.Value = newProgress;

    //トークンによってクリアする場合はこちらから
    virtual public ClearState Unite(int localValue, bool update)
    {
        if (localValue < 0) return ClearState.NotCleared;

        int lastValue = entry.Value;
        int newValue = Math.Min(goal, lastValue + localValue);
        if (update) entry.Value = newValue;

        if (newValue >= goal && lastValue < goal)
            return ClearState.FirstClear;

        if (localValue >= goal && !canClearOnce)
            return ClearState.Clear;

        return ClearState.NotCleared;
    }

    //他のレコードの進捗によって勝手にクリアする場合はこちらから
    virtual public ClearState CheckClear() { return ClearState.NotCleared; }
}

public class DisplayProgressRecord : ProgressRecord
{
    string translationKey;
    public DisplayProgressRecord(string key, int goal, string translationKey) : base(key,goal, true)
    {
        this.translationKey = translationKey;
    }

    public override string TranslationKey => translationKey;
}

public class AbstractAchievement : ProgressRecord {
    public static AchievementToken<(bool isCleared, bool triggered)> GenerateSimpleTriggerToken(string achievement) => new(achievement,(false,false),(val,_)=>val.isCleared);

    static public IDividedSpriteLoader TrophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 100f, 3);

    bool isSecret;
    bool noHint;
    
    public (DefinedAssignable? role, AchievementType? type) Category { get; private init; }
    public int Trophy { get; private init; }

    static public TextComponent HiddenComponent = new RawTextComponent("???");
    static public TextComponent HiddenDescriptiveComponent = new ColorTextComponent(new Color(0.4f, 0.4f, 0.4f), new TranslateTextComponent("achievement.title.hidden"));
    static public TextComponent HiddenDetailComponent = new ColorTextComponent(new Color(0.8f, 0.8f, 0.8f), new TranslateTextComponent("achievement.title.hiddenDetail"));

    public bool IsHidden { get {
            return isSecret && !IsCleared;
        } }

    //static private TextAttribute HeaderAttribute = new Virial.Text.TextAttribute(GUI.Instance.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.6f) };
    static public TextAttribute DetailTitleAttribute { get; private set; } = GUI.API.GetAttribute(AttributeAsset.OverlayTitle);
    static private TextAttribute DetailContentAttribute = GUI.API.GetAttribute(AttributeAsset.OverlayContent);

    virtual public Virial.Media.GUIWidget GetOverlayWidget(bool hiddenNotClearedAchievement = true, bool showCleared = false, bool showTitleInfo = false, bool showTorophy = false, bool showFlavor = false)
    {
        var gui = NebulaAPI.GUI;

        List<Virial.Media.GUIWidget> list = new();

        list.Add(new NoSGUIText(GUIAlignment.Left, DetailContentAttribute, GetHeaderComponent()));

        List<Virial.Media.GUIWidget> titleList = new();
        if (showTorophy)
        {
            titleList.Add(new NoSGUIMargin(GUIAlignment.Left, new(-0.04f, 0.2f)));
            titleList.Add(new NoSGUIImage(GUIAlignment.Left, new WrapSpriteLoader(() => TrophySprite.GetSprite(Trophy)), new(0.3f, 0.3f)));
            titleList.Add(new NoSGUIMargin(GUIAlignment.Left, new(0.05f, 0.2f)));
        }

        titleList.Add(new NoSGUIText(GUIAlignment.Left, DetailTitleAttribute, GetTitleComponent(hiddenNotClearedAchievement ? AbstractAchievement.HiddenDescriptiveComponent : null)));
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
        return new VerticalWidgetsHolder(GUIAlignment.Left,list);                   
    }

    public TextComponent? GetHeaderComponent()
    {
        List<TextComponent> list = new();
        if(Category.role != null)
        {
            list.Add(NebulaGUIWidgetEngine.Instance.TextComponent(Category.role.UnityColor, "role." + Category.role.LocalizedName + ".name"));
        }

        if(Category.type != null)
        {
            if (list.Count != 0) list.Add(new RawTextComponent(" "));
            list.Add(new TranslateTextComponent(Category.type.TranslationKey));
        }

        if (list.Count > 0)
            return new CombinedTextComponent(list.ToArray());
        else
            return null;
    }

    virtual public TextComponent GetTitleComponent(TextComponent? hiddenComponent)
    {
        if (hiddenComponent != null && !IsCleared)
            return hiddenComponent;
        return new TranslateTextComponent(TranslationKey);
    }

    virtual public TextComponent GetDetailComponent()
    {
        List<TextComponent> list = new();
        if (!noHint || IsCleared)
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
                    builder.Append(c);
                }
                builder.Append("</size>");
            }
            return builder.ToString();
        }));

        return new CombinedTextComponent(list.ToArray());
    }

    virtual public TextComponent? GetFlavorComponent()
    {
        var text = Language.Find(FlavorTranslationKey);
        if (text == null) return null;
        return new RawTextComponent($"<color=#e7e5ca><size=78%><i>{text}</i></size></color>");
    }

    virtual public Virial.Media.GUIWidget? GetDetailWidget() => null;

    public AbstractAchievement(bool canClearOnce, bool isSecret, bool noHint, string key, int goal, (DefinedAssignable? role, AchievementType? type) category, int trophy) : base(key, goal, canClearOnce) 
    {
        this.isSecret = isSecret;
        this.noHint = noHint;
        this.Category = category;
        this.Trophy = trophy;
    }

    public enum ClearState
    {
        Clear,
        FirstClear,
        NotCleared
    }
}

public class StandardAchievement : AbstractAchievement
{
    public StandardAchievement(bool canClearOnce, bool isSecret, bool noHint, string key, int goal, (DefinedAssignable? role, AchievementType? type) category, int trophy)
        : base(canClearOnce, isSecret, noHint, key, goal, category, trophy)
    {
    }
}

public class SumUpAchievement : AbstractAchievement
{
    public SumUpAchievement(bool isSecret, bool noHint, string key, int goal, (DefinedAssignable? role, AchievementType? type) category, int trophy)
        : base(true, isSecret, noHint, key, goal, category, trophy)
    {
    }

    SpriteLoader guageSprite = SpriteLoader.FromResource("Nebula.Resources.ProgressGuage.png", 100f);

    static private TextAttribute OblongAttribute = new(GUI.Instance.GetAttribute(AttributeParams.Oblong)) { FontSize = new(1.6f), Size = new(0.6f, 0.2f), Color = new(163,204,220) };
    protected virtual void OnWidgetGenerated(GameObject obj) { }
    public override Virial.Media.GUIWidget? GetDetailWidget()
    {
        //クリア済みなら何も出さない
        if (IsCleared) return null;

        return new NoSGameObjectGUIWrapper(GUIAlignment.Left, () =>
        {
            var obj = UnityHelper.CreateObject("Progress", null, Vector3.zero, LayerExpansion.GetUILayer());
            var backGround = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, new Vector3(0f, 0f, 0f));
            var colored = UnityHelper.CreateObject<SpriteRenderer>("Colored", obj.transform, new Vector3(0f, 0f, -0.1f));

            backGround.sprite = guageSprite.GetSprite();
            backGround.color = new(0.21f, 0.21f, 0.21f);
            backGround.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            backGround.sortingOrder = 1;

            colored.sprite = guageSprite.GetSprite();
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

public class CompleteAchievement : SumUpAchievement
{
    ProgressRecord[] records;
    public CompleteAchievement(ProgressRecord[] allRecords, bool isSecret, bool noHint, string key, (DefinedAssignable? role, AchievementType? type) category, int trophy)
        : base(isSecret, noHint, key, allRecords.Length, category, trophy) {
        this.records = allRecords;
    }

    override public ClearState CheckClear() {
        bool wasCleared = IsCleared;
        UpdateProgress(records.Count(r => r.IsCleared));

        if(!wasCleared) return IsCleared ? ClearState.FirstClear : ClearState.NotCleared;
        return ClearState.NotCleared;
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

[NebulaPreprocess(PreprocessPhase.PostRoles)]
static public class NebulaAchievementManager
{
    static public DataSaver AchievementDataSaver = new("Achievements");
    static private Dictionary<string, ProgressRecord> allRecords = new();
    static private StringDataEntry myTitleEntry = new("MyTitle", AchievementDataSaver, "-");

    static public IEnumerable<ProgressRecord> AllRecords => allRecords.Values;
    static public IEnumerable<AbstractAchievement> AllAchievements => allRecords.Values.Where(v => v is AbstractAchievement).Select(v => v as AbstractAchievement)!;

    static public AbstractAchievement? MyTitle { get {
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

    static public void SetOrToggleTitle(AbstractAchievement? achievement)
    {
        if (achievement == null || MyTitle == achievement)
            MyTitle = null;
        else
            MyTitle = achievement;
    }

    static public (int num,int max, int hidden)[] Aggregate(Predicate<AbstractAchievement>? predicate)
    {
        (int num, int max, int hidden)[] result = new (int num, int max, int hidden)[3];
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
        yield return preprocessor.SetLoadingText("Loading Achievements");

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
            PlayerState.Frenzied
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
            PlayerState.Frenzied
        }.Select(tag => new DisplayProgressRecord("death." + tag.TranslateKey, 1, tag.TranslateKey)).ToArray();

        //読み込み
        using var reader = new StreamReader(NebulaResourceManager.NebulaNamespace.GetResource("Achievements.dat")!.AsStream()!);

        List<ProgressRecord> recordsList = new();

        while (true) {
            var line = reader.ReadLine();
            if(line == null) break;

            if (line.StartsWith("#")) continue;

            var args = line.Split(',');

            if (args.Length < 2) continue;

            bool clearOnce = false;
            bool noHint = false;
            bool secret = false;
            bool seasonal = false;
            bool isNotChallenge = false;
            bool isRecord = false;
            IEnumerable<ProgressRecord>? records = recordsList;

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
                        seasonal = true;
                        break;
                    case "nonChallenge":
                        isNotChallenge = true;
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
                }
            }

            DefinedAssignable? relatedRole = null;
            AchievementType? type = null;

            var nameSplitted = args[0].Split('.');
            if(nameSplitted.Length > 1)
            {
                nameSplitted[0] = nameSplitted[0].Replace('-', '.');
                var cand = Roles.Roles.AllAssignables().Where(a => a.LocalizedName == nameSplitted[0]).ToArray();
                if(cand.Length >= 1)
                {
                    relatedRole = cand[0];
                    if (rarity == 2 && !isNotChallenge) type = AchievementType.Challenge;
                }
            }

            if (type == null && secret) type = AchievementType.Secret;
            else if (seasonal) type = AchievementType.Seasonal;

            if (isRecord)
                new DisplayProgressRecord(args[0], goal, "record." + args[0]);
            else if (records.Count() > 0)
                new CompleteAchievement(records.ToArray(), secret, noHint, args[0], (relatedRole, type), rarity);
            else if (goal > 1)
                new SumUpAchievement(secret, noHint, args[0], goal, (relatedRole, type), rarity);
            else
                new StandardAchievement(clearOnce, secret, noHint, args[0], goal, (relatedRole, type), rarity);

            if (recordsList.Count > 0) recordsList.Clear();
        }

        //旧形式から更新する
        if (DataSaver.ExistData("Progress"))
        {
            yield return preprocessor.SetLoadingText("Reformatting Achievement Progress");

            var oldSaver = new DataSaver("Progress");

            foreach (var tuple in oldSaver.AllRawContents())
            {
                if (int.TryParse(tuple.Item2, out var val)) {
                    var record = AllRecords.FirstOrDefault(r => tuple.Item1 == r.OldEntryTag);
                    if (record != null) record.AdoptBigger(val);
                }
            }

            File.Move(DataSaver.ToDataSaverPath("Progress"), DataSaver.ToDataSaverPath("Progress") + ".old", true);
        }

        foreach (var achievement in AllAchievements) achievement.CheckClear();
    }

    static public void RegisterAchievement(ProgressRecord progressRecord,string id)
    {
        allRecords[id] = progressRecord;
    }

    static public bool GetRecord(string id, [MaybeNullWhen(false)] out ProgressRecord record)
    {
        return allRecords.TryGetValue(id, out record);
    }

    static public bool GetAchievement(string id, [MaybeNullWhen(false)] out AbstractAchievement achievement)
    {
        achievement = (allRecords.TryGetValue(id, out var rec) && rec is AbstractAchievement ach) ? ach : null;
        return achievement != null;
    }

    static public (AbstractAchievement achievement, AbstractAchievement.ClearState clearState)[] UniteAll()
    {
        List<(AbstractAchievement achievement, AbstractAchievement.ClearState clearState)> result  =new();

        //トークンによるクリア
        foreach (var token in NebulaGameManager.Instance!.AllAchievementTokens)
        {
            var state = token.UniteTo();
            if (state == AbstractAchievement.ClearState.NotCleared) continue;

            //実績のみ結果に表示(他実績用のレコードは対象外)
            if(token.Achievement is AbstractAchievement ach) result.Add(new(ach, state));
        }

        //他レコードの更新によるクリア
        foreach(var achievement in AllAchievements)
        {
            var state = achievement.CheckClear();
            if (state == AbstractAchievement.ClearState.NotCleared) continue;
            result.Add(new(achievement, state));
        }

        result.OrderBy(val => val.clearState);

        return result.DistinctBy(a=>a.achievement).ToArray();
    }

    static XOnlyDividedSpriteLoader trophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 220f, 3);

    static public IEnumerator CoShowAchievements(MonoBehaviour coroutineHolder, params (AbstractAchievement achievement, AbstractAchievement.ClearState clearState)[] achievements)
    {
        int num = 0;
        (GameObject holder, GameObject animator, GameObject body, SpriteRenderer white) CreateBillboard(AbstractAchievement achievement, AbstractAchievement.ClearState clearState)
        {
            var billboard = UnityHelper.CreateObject("Billboard", null, new Vector3(3.85f, 1.75f - (float)num * 0.6f, -100f));
            var animator = UnityHelper.CreateObject("Animator", billboard.transform, new Vector3(0f, 0f, 0f));
            var body = UnityHelper.CreateObject("Body", animator.transform, new Vector3(0f, 0f, 0f));
            var background = UnityHelper.CreateObject<SpriteRenderer>("Background", body.transform, new Vector3(0f,0f,1f));
            var white = UnityHelper.CreateObject<SpriteRenderer>("White", animator.transform, new Vector3(0f, 0f, -2f));
            var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", body.transform, new Vector3(-0.95f, 0f, 0f));

            background.color = clearState == AbstractAchievement.ClearState.FirstClear ? Color.yellow : new UnityEngine.Color(0.7f, 0.7f, 0.7f);

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

            if (ach.clearState == AbstractAchievement.ClearState.FirstClear)
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
}

public class AchievementShower : MonoBehaviour
{
    static AchievementShower() => ClassInjector.RegisterTypeInIl2Cpp<AchievementShower>();

        /*
        button.sprite = VanillaAsset.TextButtonSprite;
            button.drawMode = SpriteDrawMode.Sliced;
            button.tileMode = SpriteTileMode.Continuous;
            button.size = TextAttribute.Size + new Vector2(TextMargin* 0.84f, TextMargin* 0.84f);
    */
}