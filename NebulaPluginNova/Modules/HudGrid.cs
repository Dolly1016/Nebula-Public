using Il2CppInterop.Runtime.Injection;

namespace Nebula.Modules;

public class HudGrid : MonoBehaviour
{
    static HudGrid()
    {
        ClassInjector.RegisterTypeInIl2Cpp<HudGrid>();
    }

    public List<Il2CppArgument<HudContent>>[] Contents = { new(), new() };
    private Transform ButtonsHolder = null!;
    private Transform StaticButtonsHolder = null!;

    public void Awake()
    {
        var buttonParent = HudManager.Instance.UseButton.transform.parent;
        buttonParent.localPosition= Vector3.zero;
        buttonParent.name = "Buttons";
        GameObject.Destroy(buttonParent.gameObject.GetComponent<GridArrange>());
        GameObject.Destroy(buttonParent.gameObject.GetComponent<AspectPosition>());
        ButtonsHolder = buttonParent;
        StaticButtonsHolder = UnityHelper.CreateObject("StaticButtons", buttonParent.parent, new Vector3(0, 0, -90f)).transform;

        HudContent AddVanillaButtons(GameObject obj,int priority)
        {
            var content = obj.AddComponent<HudContent>();
            content.SetPriority(priority);
            content.ActiveFunc = () => obj.activeSelf && !AmongUsUtil.MapIsOpen;
            RegisterContentToRight(content);

            return content;
        }

        AddVanillaButtons(HudManager.Instance.UseButton.gameObject,1000);
        AddVanillaButtons(HudManager.Instance.PetButton.gameObject,1000);
        AddVanillaButtons(HudManager.Instance.ImpostorVentButton.gameObject,997);
        AddVanillaButtons(HudManager.Instance.ReportButton.gameObject,999);
        AddVanillaButtons(HudManager.Instance.SabotageButton.gameObject, 998);
        AddVanillaButtons(HudManager.Instance.KillButton.gameObject, -1).MarkAsKillButtonContent();
        

        //ベントボタンにクールダウンテキストを設定
        HudManager.Instance.ImpostorVentButton.cooldownTimerText = GameObject.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, HudManager.Instance.ImpostorVentButton.transform);
    }

    static public bool UseSmallerHud => ClientOption.GetValue(ClientOption.ClientOptionType.SmallHud) == 1;
    bool lastUseSmallerHud = false;

    private void UpdateHudSize()
    {
        lastUseSmallerHud = UseSmallerHud;
        Vector3 scale = lastUseSmallerHud ? new(0.72f, 0.72f, 1f) : new(1f, 1f, 1f);
        ButtonsHolder.transform.localScale = scale;
        StaticButtonsHolder.transform.localScale = scale;
    }
    public void LateUpdate()
    {
        if (UseSmallerHud != lastUseSmallerHud) UpdateHudSize();


        for (int i = 0; i < 2; i++)
        {
            Contents[i].RemoveAll(c => !c.Value);

            Contents[i].Sort((c1, c2) => { 
                int num = c2.Value.Priority - c1.Value.Priority;
                if (num == 0) num = c1.Value.SubPriority - c2.Value.SubPriority;
                return num;
            });


            if (Contents[i].Count == 0) continue;

            bool killButtonPosArranged = false;

            int row = 0, column = 0;

            bool ShouldBeShown(HudContent c)
            {
                if (!c.IsActive) return false;
                if (MeetingHud.Instance && !c.gameObject.active) return false; //会議中はstaticContents以外除外する
                return true;
            }
            for (int e = 0; e < Contents[i].Count; e++)
            {
                var c = Contents[i][e];

                if(!ShouldBeShown(c.Value)) continue;

                if (c.Value.ShouldBeInLastLine)
                {
                    void DoForRemains(Action<HudContent> action)
                    {
                        for (int k = e; k < Contents[i].Count; k++)
                        {
                            var cc = Contents[i][k];
                            if (ShouldBeShown(cc.Value)) action.Invoke(cc.Value);
                        }
                    }

                    //最後の行に全て圧縮して並べる
                    int numOfLastLineContents = 0;
                    DoForRemains(_ => numOfLastLineContents++);
                    if (numOfLastLineContents <= 3 - column)
                    {
                        DoForRemains(c =>
                        {
                            c.CurrentPos = new(column, row);
                            column++;
                        });
                    }
                    else if(numOfLastLineContents == 1)
                    {
                        //間違いなくいらないけど。
                        DoForRemains(c => c.CurrentPos = new(column, row));
                    } else
                    {
                        float widthSum = 3 - column - 1;
                        float div = numOfLastLineContents - 1;
                        int index = 0;
                        DoForRemains(c =>
                        {
                            c.CurrentPos = new(column + widthSum * (float)index / div , row);
                            index++;
                        });
                    }
                    break;
                }

                if (!killButtonPosArranged && c.Value.MarkedAsKillButtonContent)
                {
                    killButtonPosArranged = true;
                    c.Value.CurrentPos = new Vector2(0, 1);
                    continue;
                }

                c.Value.CurrentPos = new Vector2(column, row);

                if (column < 2 && !c.Value.OccupiesLine)
                    column++;
                else
                {
                    row++;
                    column = 0;
                    if (row == 1 && killButtonPosArranged) column = 1;
                }
            }
        }
    }

    public void RegisterContentToLeft(Il2CppArgument<HudContent> content) => RegisterContent(content, true);
    
    public void RegisterContentToRight(Il2CppArgument<HudContent> content) => RegisterContent(content, false);

    public void RegisterContent(Il2CppArgument<HudContent> content,bool toLeft)
    {
        Contents[toLeft ? 0 : 1].Add(content);
        content.Value.SetSide(toLeft);

        if (content.Value.IsStaticContent) content.Value.transform.SetParent(NebulaGameManager.Instance!.HudGrid.StaticButtonsHolder);
    }

    private int availableSubPriority = 0;
    public int AvailableSubPriority { get { return availableSubPriority++; } }
}

public class HudContent : MonoBehaviour
{
    static HudContent()
    {
        ClassInjector.RegisterTypeInIl2Cpp<HudContent>();
    }

    public Vector2 CurrentPos { get; set; }

    //Priorityの大きいものから配置される
    public int Priority { get => (OccupiesLine ? 20000 : onKillButtonPos ? 10000 : 0) + priority; }
    public int SubPriority => subPriority;
    private int priority;
    private int subPriority;
    private bool onKillButtonPos;
    private bool isLeftSide;
    private bool isDirty = true;
    public bool OccupiesLine = false;
    public bool IsStaticContent = false;
    private bool shouldBeInLastLine = false;
    public bool ShouldBeInLastLine { get => !OccupiesLine && shouldBeInLastLine; set => shouldBeInLastLine = value; }

    public Func<bool>? ActiveFunc = null;
    public bool IsActive => ActiveFunc?.Invoke() ?? gameObject.activeSelf;
    public bool IsLeftSide => isLeftSide;

    private static float EdgeY => HudGrid.UseSmallerHud ? 
        -(3.0f / 0.72f - 0.65f) :
        -(3.0f - 0.7f);
    private static float EdgeX => HudGrid.UseSmallerHud ?
        (3.0f / 0.72f) * (float)Screen.width / (float)Screen.height - 0.67f :
        3.0f * (float)Screen.width / (float)Screen.height - 0.8f;

    public Vector3 ToLocalPos
    {
        get
        {
            var pos = new Vector3((EdgeX - CurrentPos.x) * (isLeftSide ? -1 : 1), EdgeY + CurrentPos.y, CurrentPos.x * 0.05f);

            var arrangement = ClientOption.AllOptions[ClientOption.ClientOptionType.ButtonArrangement].Value;
            if (!MeetingHud.Instance && ((arrangement == 1 && isLeftSide) || arrangement == 2)) pos.y += 0.85f;

            return pos;
        }
    }
    public bool MarkedAsKillButtonContent => onKillButtonPos;
    public void MarkAsKillButtonContent(bool mark = true)
    {
        onKillButtonPos = mark;
    }
    public HudContent SetPriority(int priority)
    {
        this.priority = priority;
        return this;
    }
    public HudContent UpdateSubPriority()
    {
        this.subPriority = NebulaGameManager.Instance?.HudGrid.AvailableSubPriority ?? 0;
        return this;
    }

    public void SetSide(bool asLeftSide)
    {
        isLeftSide = asLeftSide;
    }

    public void OnDisable()
    {
        CurrentPos = new Vector2(-1,-1);
        isDirty = true;
    }

    public void Start()
    {
        CurrentPos = new Vector2(-1, -1);
    }

    public void LateUpdate()
    {
        if (CurrentPos.x < 0) return;
        if (isDirty)
        {
            transform.localPosition = ToLocalPos;
            isDirty = false;
        }
        else
        {
            var diff = ToLocalPos - transform.localPosition;
            transform.localPosition += diff * Time.deltaTime * 5.2f;
        }
    }

    static public HudContent InstantiateContent(string name, bool isLeftSide = true, bool occupiesLine = false,bool asKillButtonContent = false, bool isStaticContent = false)
    {
        var obj = UnityHelper.CreateObject<HudContent>(name, HudManager.Instance.KillButton.transform.parent, Vector3.zero);
        obj.OccupiesLine= occupiesLine;
        obj.MarkAsKillButtonContent(asKillButtonContent);
        obj.IsStaticContent = isStaticContent;

        NebulaGameManager.Instance?.HudGrid.RegisterContent(obj,isLeftSide);
        obj.UpdateSubPriority();

        return obj;
    }
}
