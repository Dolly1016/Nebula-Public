using BepInEx.Unity.IL2CPP.Utils;
using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.MetaContext;
using Nebula.Roles;
using Nebula.Utilities;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Virial;
using Virial.Media;
using Virial.Text;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;

namespace Nebula.Modules;

abstract public class AchievementTokenBase : IReleasable, ILifespan
{
    public Achievement Achievement { get; private init; }
    abstract public Achievement.ClearState UniteTo(bool update = true);

    public AchievementTokenBase(Achievement achievement)
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
    public StaticAchievementToken(string achievement)
        : base(NebulaAchievementManager.GetAchievement(achievement, out var a) ? a : null!) { }


    public override Achievement.ClearState UniteTo(bool update)
    {
        if (IsDeadObject) return Achievement.ClearState.NotCleared;

        return Achievement.Unite(1, update);
    }
}

public class AchievementToken<T> : AchievementTokenBase
{
    public T Value;
    public Func<T,Achievement,int> Supplier { get; set; }

    public AchievementToken(Achievement achievement, T value, Func<T, Achievement, int> supplier) : base(achievement)
    {
        Value = value;
        Supplier = supplier;
    }

    public AchievementToken(string achievement, T value, Func<T, Achievement, int> supplier) 
        : this(NebulaAchievementManager.GetAchievement(achievement,out var a) ? a : null!, value,supplier) { }

    public AchievementToken(string achievement, T value, Func<T, Achievement, bool> supplier)
        : this(achievement, value, (t,ac)=> supplier.Invoke(t,ac) ? 1 : 0) { }


    public override Achievement.ClearState UniteTo(bool update)
    {
        if (IsDeadObject) return Achievement.ClearState.NotCleared;

        return Achievement.Unite(Supplier.Invoke(Value, Achievement),update);
    }
}

public class AchievementType
{
    static public AchievementType Challenge = new("challenge");
    static public AchievementType Secret = new("secret");

    private AchievementType(string key)
    {
        TranslationKey = "achievement.type." + key;

    }
    public string TranslationKey { get; private set; }
}

public class Achievement {
    public static AchievementToken<(bool isCleared, bool triggered)> GenerateSimpleTriggerToken(string achievement) => new(achievement,(false,false),(val,_)=>val.isCleared);

    static public IDividedSpriteLoader TrophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 100f, 3);

    int goal;
    bool canClearOnce;
    bool isSecret;
    bool noHint;
    IntegerDataEntry entry;
    string key;
    string hashedKey;
    public (Roles.IAssignableBase? role, AchievementType? type) Category { get; private init; }
    public int Trophy { get; private init; }
    public bool IsCleared => goal <= entry.Value;

    static public TextComponent HiddenComponent = new RawTextComponent("???");
    static public TextComponent HiddenDescriptiveComponent = new ColorTextComponent(new Color(0.4f, 0.4f, 0.4f), new TranslateTextComponent("achievement.title.hidden"));
    static public TextComponent HiddenDetailComponent = new ColorTextComponent(new Color(0.8f, 0.8f, 0.8f), new TranslateTextComponent("achievement.title.hiddenDetail"));

    public bool IsHidden { get {
            return isSecret && !IsCleared;
        } }

    public GUIContext GetOverlayContext()
    {
        var gui = NebulaImpl.Instance.GUILibrary;

        var attr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.OblongLeft)) { FontSize = new(1.6f) };
        var detailTitleAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredBoldLeft)) { FontSize = new(1.8f) };
        var detailDetailAttr = new Virial.Text.TextAttribute(gui.GetAttribute(AttributeParams.StandardBaredLeft)) { FontSize = new(1.5f), Size = new(5f, 6f) };

        return new VerticalContextsHolder(GUIAlignment.Left,
                        new NoSGUIText(GUIAlignment.Left, detailDetailAttr, GetHeaderComponent()),
                        new NoSGUIText(GUIAlignment.Left, detailTitleAttr, GetTitleComponent(Achievement.HiddenDescriptiveComponent)),
                        new NoSGUIText(GUIAlignment.Left, detailDetailAttr, GetDetailComponent()));
    }

    public TextComponent? GetHeaderComponent()
    {
        List<TextComponent> list = new();
        if(Category.role != null)
        {
            list.Add(NebulaGUIContextEngine.Instance.TextComponent(Category.role.RoleColor, "role." + Category.role.LocalizedName + ".name"));
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

    public TextComponent GetTitleComponent(TextComponent? hiddenComponent)
    {
        if (hiddenComponent != null && !IsCleared)
            return hiddenComponent;
        return new TranslateTextComponent(TranslationKey);
    }

    public TextComponent GetDetailComponent()
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
                    builder.Append("<br>   ");
                    builder.Append(c);
                }
                builder.Append("</size>");
            }
            return builder.ToString();
        }));

        return new CombinedTextComponent(list.ToArray());
    }

    public Achievement(bool canClearOnce, bool isSecret, bool noHint,string key, int goal, (Roles.IAssignableBase? role, AchievementType? type) category,int trophy) {
        this.goal= goal;
        this.isSecret = isSecret;
        this.canClearOnce= canClearOnce;
        this.noHint = noHint;
        this.key = key;
        this.hashedKey = key.ComputeConstantHashAsString();
        this.Category = category;
        this.Trophy = trophy;
        this.entry = new IntegerDataEntry("a."+ hashedKey, NebulaAchievementManager.AchievementDataSaver, 0);
        NebulaAchievementManager.RegisterAchievement(this, key);
    }

    public string Id => key;
    public string TranslationKey => "achievement." + key + ".title";
    public string GoalTranslationKey => "achievement." + key + ".goal";
    public string CondTranslationKey => "achievement." + key + ".cond";

    public enum ClearState
    {
        Clear,
        FirstClear,
        NotCleared
    }
    
    public ClearState Unite(int localValue, bool update)
    {
        if (localValue < 0) return　ClearState.NotCleared;

        int lastValue = entry.Value;
        int newValue = Math.Min(goal, lastValue + localValue);
        if(update) entry.Value = newValue;

        if (newValue >= goal && lastValue < goal)
            return ClearState.FirstClear;
        
        if (localValue >= goal && !canClearOnce)
            return ClearState.Clear;
        
        return ClearState.NotCleared;
    }
}

[NebulaPreLoad(typeof(Roles.Roles))]
static public class NebulaAchievementManager
{
    static public DataSaver AchievementDataSaver = new("Progress");
    static private Dictionary<string, Achievement> achievements = new();

    static public IEnumerable<Achievement> AllAchievements => achievements.Values;

    static public (int num,int max, int hidden)[] Aggregate()
    {
        (int num, int max, int hidden)[] result = new (int num, int max, int hidden)[3];
        for (int i = 0; i < result.Length; i++) result[i] = (0, 0, 0);
        return achievements.Values.Aggregate(result,
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

    static public IEnumerator CoLoad() {
        Patches.LoadPatch.LoadingText = "Loading Achievements";
        yield return null;

        using var reader = new StreamReader(NameSpaceManager.DefaultNameSpace.OpenRead("Achievements.dat")!);
        while (true) {
            var line = reader.ReadLine();
            if(line == null) break;

            var args = line.Split(',');

            if (args.Length < 2) continue;

            bool clearOnce = false;
            bool noHint = false;
            bool secret = false;
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
                    case string a when a.StartsWith("goal-"):
                        goal = int.Parse(a.Substring(5));
                        break;
                }
            }

            IAssignableBase? relatedRole = null;
            AchievementType? type = null;

            var nameSplitted = args[0].Split('.');
            if(nameSplitted.Length > 1)
            {
                nameSplitted[0] = nameSplitted[0].Replace('-', '.');
                var cand = Roles.Roles.AllAsignables().Where(a => a.LocalizedName == nameSplitted[0]).ToArray();
                if(cand.Length >= 1)
                {
                    relatedRole = cand[0];
                    if (rarity == 2) type = AchievementType.Challenge;
                    else if(secret) type = AchievementType.Secret;
                }
            }
            new Achievement(clearOnce, secret,noHint,args[0], goal, (relatedRole, type),rarity);
        }
    }

    static public void RegisterAchievement(Achievement achievement,string id)
    {
        achievements[id] = achievement;
    }

    static public bool GetAchievement(string id, [MaybeNullWhen(false)] out Achievement achievement)
    {
        return achievements.TryGetValue(id, out achievement);
    }

    static public (Achievement achievement, Achievement.ClearState clearState)[] UniteAll()
    {
        List<(Achievement achievement, Achievement.ClearState clearState)> result  =new();

        foreach (var token in NebulaGameManager.Instance!.AllAchievementTokens)
        {
            var state = token.UniteTo();
            if (state == Achievement.ClearState.NotCleared) continue;
            result.Add(new(token.Achievement, state));
        }

        result.OrderBy(val => val.clearState);

        return result.DistinctBy(a=>a.achievement).ToArray();
    }

    static XOnlyDividedSpriteLoader trophySprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Trophy.png", 220f, 3);

    static public IEnumerator CoShowAchievements(MonoBehaviour coroutineHolder, params (Achievement achievement, Achievement.ClearState clearState)[] achievements)
    {
        int num = 0;
        (GameObject holder, GameObject animator, GameObject body, SpriteRenderer white) CreateBillboard(Achievement achievement, Achievement.ClearState clearState)
        {
            var billboard = UnityHelper.CreateObject("Billboard", null, new Vector3(3.85f, 1.75f - (float)num * 0.6f, -100f));
            var animator = UnityHelper.CreateObject("Animator", billboard.transform, new Vector3(0f, 0f, 0f));
            var body = UnityHelper.CreateObject("Body", animator.transform, new Vector3(0f, 0f, 0f));
            var background = UnityHelper.CreateObject<SpriteRenderer>("Background", body.transform, new Vector3(0f,0f,1f));
            var white = UnityHelper.CreateObject<SpriteRenderer>("White", animator.transform, new Vector3(0f, 0f, -2f));
            var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", body.transform, new Vector3(-0.95f, 0f, 0f));

            background.color = clearState == Achievement.ClearState.FirstClear ? Color.yellow : new UnityEngine.Color(0.7f, 0.7f, 0.7f);
            billboard.AddComponent<SortingGroup>();

            new MetaContextOld.Text(new(Nebula.Utilities.TextAttribute.BoldAttr) { Font = VanillaAsset.BrookFont, Size = new(2f, 0.4f), FontSize = 1.16f, FontMaxSize = 1.16f, FontMinSize  = 1.16f }) { MyText = achievement.GetHeaderComponent() }.Generate(body, new Vector2(0.25f, 0.13f), out _);
            new MetaContextOld.Text(new(Nebula.Utilities.TextAttribute.NormalAttr) { Font = VanillaAsset.BrookFont, Size = new(2f, 0.4f) }) { MyText = achievement.GetTitleComponent(null) }.Generate(body, new Vector2(0.25f, -0.06f), out _);

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
            button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpContext(button, achievement.GetOverlayContext()));
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpContextIf(button));

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

            if (ach.clearState == Achievement.ClearState.FirstClear)
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