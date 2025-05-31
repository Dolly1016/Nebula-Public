using AmongUs.Data.Player;
using Il2CppInterop.Runtime.Injection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Modules.Cosmetics;
using Rewired;
using System.Text;
using UnityEngine;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;
using Virial.Utilities;

namespace Nebula.Game.Statistics;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public static class EventDetail
{
    static public readonly TranslatableTag Kill = new("statistics.events.kill");
    static public readonly TranslatableTag Exiled = new("statistics.events.exiled");
    static public readonly TranslatableTag Misfire = new("statistics.events.misfire");
    static public readonly TranslatableTag GameStart = new("statistics.events.startGame");
    static public readonly TranslatableTag GameEnd = new("statistics.events.endGame");
    static public readonly TranslatableTag MeetingEnd = new("statistics.events.endMeeting");
    static public readonly TranslatableTag Report = new("statistics.events.report");
    static public readonly TranslatableTag BaitReport = new("statistics.events.baitReport");
    static public readonly TranslatableTag EmergencyButton = new("statistics.events.emergency");
    static public readonly TranslatableTag MayorButton = new("statistics.events.mayorEmergency");
    static public readonly TranslatableTag Disconnect = new("statistics.events.disconnect");
    static public readonly TranslatableTag Revive = new("statistics.events.revive");
    static public readonly TranslatableTag Eat = new("statistics.events.eat");
    static public readonly TranslatableTag Clean = new("statistics.events.clean");
    static public readonly TranslatableTag Missed = new("statistics.events.missed");
    static public readonly TranslatableTag Guess = new("statistics.events.guess");
    static public readonly TranslatableTag Embroil = new("statistics.events.embroil");
    static public readonly TranslatableTag Trap = new("statistics.events.trap");
    static public readonly TranslatableTag Accident = new("statistics.events.accident");
    static public readonly TranslatableTag FakeSabotage = new("statistics.events.fakeSabotage");
    static public readonly TranslatableTag Curse = new("statistics.events.curse");
    static public readonly TranslatableTag Layoff = new("statistics.events.layoff");
    static public readonly TranslatableTag DestroyKill = new("statistics.events.destroy");
    static public readonly TranslatableTag Dance = new("statistics.events.dance");
    static public readonly TranslatableTag Gassed = new("statistics.events.gassed");
    static public readonly TranslatableTag Bubbled = new("statistics.events.bubbled");
    static public readonly TranslatableTag Meteor = new("statistics.events.meteor");
    static public readonly TranslatableTag Balloon = new("statistics.events.balloon");

    static EventDetail()
    {
        EventDetails.Kill = Kill;
        EventDetails.Exiled = Exiled;
        EventDetails.Misfire = Misfire;
        EventDetails.GameStart = GameStart;
        EventDetails.GameEnd = GameEnd;
        EventDetails.MeetingEnd = MeetingEnd;
        EventDetails.Report = Report;
        EventDetails.BaitReport = BaitReport;
        EventDetails.EmergencyButton = EmergencyButton;
        EventDetails.Disconnect = Disconnect;
        EventDetails.Revive = Revive;
        EventDetails.Eat = Eat;
        EventDetails.Clean = Clean;
        EventDetails.Missed = Missed;
        EventDetails.Guess = Guess;
        EventDetails.Embroil = Embroil;
        EventDetails.Trap = Trap;
        EventDetails.Accident = Accident;
        EventDetails.Gassed = Gassed;
        EventDetails.Bubbled = Bubbled;
        EventDetails.Meteor = Meteor;
        EventDetails.Balloon = Balloon;
    }
}

public enum GameStatisticsGatherTag
{
    Spawn
}


[NebulaRPCHolder]
public class GameStatistics
{
    public class EventVariation : IArchivedEventVariation
    {
        static readonly Dictionary<int, EventVariation> AllEvents = [];
        static private readonly DividedSpriteLoader iconSprite = DividedSpriteLoader.FromResource("Nebula.Resources.GameStatisticsIcon.png", 100f, 8, 1);
        static public readonly EventVariation Kill = new(0, iconSprite.AsLoader(0), iconSprite.AsLoader(0), true, true);
        static public readonly EventVariation Exile = new(1, iconSprite.AsLoader(2), iconSprite.AsLoader(2), false, false);
        static public readonly EventVariation GameStart = new(2, iconSprite.AsLoader(1), iconSprite.AsLoader(1), true, false);
        static public readonly EventVariation GameEnd = new(3, iconSprite.AsLoader(1), iconSprite.AsLoader(1), true, false);
        static public readonly EventVariation MeetingEnd = new(4, iconSprite.AsLoader(1), iconSprite.AsLoader(1), true, false);
        static public readonly EventVariation Report = new(5, iconSprite.AsLoader(4), iconSprite.AsLoader(4), true, false);
        static public readonly EventVariation EmergencyButton = new(6, iconSprite.AsLoader(3), iconSprite.AsLoader(3), true, false);
        static public readonly EventVariation Disconnect = new(7, iconSprite.AsLoader(5), iconSprite.AsLoader(5), false, false);
        static public readonly EventVariation Revive = new(8, iconSprite.AsLoader(6), iconSprite.AsLoader(6), true, false);
        static public readonly EventVariation CleanBody = new(9, iconSprite.AsLoader(7), iconSprite.AsLoader(7), true, false);

        public int Id { get; private init; }
        public Image? EventIcon { get; private init; }
        public Image? InteractionIcon { get; private init; }
        public bool ShowPlayerPosition { get; private init; }
        public bool CanCombine { get; private init; }
        public EventVariation(int id, Image? eventIcon, Image? interactionIcon, bool showPlayerPosition, bool canCombine)
        {
            Id = id;
            EventIcon = eventIcon;
            InteractionIcon = interactionIcon;
            CanCombine = canCombine;

            AllEvents.Add(id, this);
            ShowPlayerPosition = showPlayerPosition;
        }
        static public EventVariation ValueOf(int id) => AllEvents[id];


    }

    public class Event : IArchivedEvent
    {
        public IArchivedEventVariation EventVariation { get; private init; }
        public float Time { get; private init; }
        public byte? SourceId { get; private init; }
        public int TargetIdMask { get; private set; }
        public Tuple<byte, Virial.Compat.Vector2>[] Position { get; private init; }
        public CommunicableTextTag? RelatedTag { get; set; } = null;


        public Event(IArchivedEventVariation variation, byte? sourceId, int targetIdMask, GameStatisticsGatherTag? positionTag = null)
            : this(variation, NebulaGameManager.Instance!.CurrentTime, sourceId, targetIdMask, positionTag) { }

        public Event(IArchivedEventVariation variation, float time, byte? sourceId, int targetIdMask, GameStatisticsGatherTag? positionTag)
        {
            EventVariation = variation;
            Time = time;
            SourceId = sourceId;
            TargetIdMask = targetIdMask;

            if (variation.ShowPlayerPosition)
            {
                List<Tuple<byte, Virial.Compat.Vector2>> list = [];
                foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
                {
                    if (p.Data.IsDead && p.PlayerId != sourceId && (TargetIdMask & 1 << p.PlayerId) == 0) continue;

                    if (positionTag != null)
                        list.Add(new Tuple<byte, Virial.Compat.Vector2>(p.PlayerId, new(NebulaGameManager.Instance!.GameStatistics.Gathering[positionTag.Value][p.PlayerId])));
                    else
                        list.Add(new Tuple<byte, Virial.Compat.Vector2>(p.PlayerId, new(p.transform.position)));
                }
                Position = list.ToArray();
            }
            else
            {
                Position = [];
            }
        }

        public bool IsSimilar(Event target)
        {
            if (!EventVariation.CanCombine) return false;
            return EventVariation == target.EventVariation && SourceId == target.SourceId && RelatedTag == target.RelatedTag;
        }

        public void Combine(Event target)
        {
            TargetIdMask |= target.TargetIdMask;
        }
    }

    private List<Event> allEvents { get; set; } = [];
    public IEnumerable<Event> AllEvents => allEvents;
    public Event[] Sealed { get => allEvents.ToArray(); }

    public Dictionary<GameStatisticsGatherTag, Dictionary<byte, Vector2>> Gathering { get; set; } = [];

    public void RecordEvent(Event statisticsEvent)
    {
        int index = allEvents.Count;

        if (statisticsEvent.EventVariation.CanCombine)
        {
            //末尾から検索
            for (int i = allEvents.Count - 1; i >= 0; i--)
            {
                if (allEvents[i].Time > statisticsEvent.Time) index = i;

                //ある程度以上離れた時間のイベントまで来たら検索をやめる
                if (statisticsEvent.Time - allEvents[i].Time > 5f) break;

                if (allEvents[i].IsSimilar(statisticsEvent))
                {
                    allEvents[i].Combine(statisticsEvent);
                    return;
                }
            }
        }
        allEvents.Insert(index, statisticsEvent);
    }

    public void RpcRecordEvent(EventVariation variation, TranslatableTag relatedTag, PlayerControl? source, params PlayerControl[] targets)
    {
        int mask = 0;
        foreach (var p in targets) mask |= 1 << p.PlayerId;
        RpcRecordEvent(variation, relatedTag, source, mask);
    }

    public void RpcRecordEvent(EventVariation variation, TranslatableTag relatedTag, PlayerControl? source, int targetMask) => RpcRecord.Invoke((variation.Id, relatedTag.Id, source?.PlayerId ?? byte.MaxValue, targetMask, 0f));
    public void RpcRecordEvent(EventVariation variation, TranslatableTag relatedTag, float timeLag, PlayerControl? source, int targetMask) => RpcRecord.Invoke((variation.Id, relatedTag.Id, source?.PlayerId ?? byte.MaxValue, targetMask, timeLag));


    static private readonly RemoteProcess<(int variation, int relatedTag, byte sourceId, int targetMask, float timeLag)> RpcRecord = new(
        "RecordStatistics",
       (message, isCalledByMe) =>
       {
           NebulaGameManager.Instance?.GameStatistics.RecordEvent(new Event(EventVariation.ValueOf(message.variation), NebulaGameManager.Instance.CurrentTime + message.timeLag, message.sourceId == byte.MaxValue ? null : message.sourceId, message.targetMask, null) { RelatedTag = TranslatableTag.ValueOf(message.relatedTag) });
       });

    static public readonly RemoteProcess<(GameStatisticsGatherTag tag, byte playerId, Vector2 pos)> RpcPoolPosition = new(
        "PoolPosition",
        (message, _) =>
        {
            if (NebulaGameManager.Instance == null) return;

            if (!NebulaGameManager.Instance!.GameStatistics.Gathering.ContainsKey(message.tag))
                NebulaGameManager.Instance!.GameStatistics.Gathering.Add(message.tag, new());

            NebulaGameManager.Instance!.GameStatistics.Gathering[message.tag][message.playerId] = message.pos;
        }
        );
}

public class CriticalPoint : MonoBehaviour
{
    static CriticalPoint()
    {
        ClassInjector.RegisterTypeInIl2Cpp<CriticalPoint>();
    }
    static private readonly SpriteLoader momentSprite = SpriteLoader.FromResource("Nebula.Resources.GameStatisticsMoment.png", 100f);
    static private readonly SpriteLoader momentRingSprite = SpriteLoader.FromResource("Nebula.Resources.GameStatisticsMomentRing.png", 100f);

    public int IndexMin { get; private set; }
    public int IndexMax { get; private set; }
    GameObject ring = null!;
    public GameStatisticsViewer MyViewer = null!;

    public void SetIndex(int min, int max)
    {
        IndexMin = min; IndexMax = max;
    }

    public bool Contains(int index) => IndexMin <= index && index <= IndexMax;

    public void Start()
    {
        var renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = momentSprite.GetSprite();
        renderer.color = GameStatisticsViewer.MainColor;
        renderer.transform.localScale = new Vector3(0.65f, 0.65f, 1f);

        var ringRenderer = UnityHelper.CreateObject<SpriteRenderer>("Ring", transform, Vector3.zero);
        ringRenderer.sprite = momentRingSprite.GetSprite();
        ringRenderer.color = GameStatisticsViewer.MainColor;
        ringRenderer.gameObject.SetActive(false);
        ring = ringRenderer.gameObject;


        var button = renderer.gameObject.SetUpButton(true);
        button.OnMouseOver.AddListener(() =>
        {
            renderer.transform.localScale = new Vector3(1f, 1f, 1f);
            MyViewer.OnMouseOver(IndexMin);
        });
        button.OnMouseOut.AddListener(() =>
        {
            renderer.transform.localScale = new Vector3(0.65f, 0.65f, 1f);
            MyViewer.OnMouseOut(IndexMin);
        });
        button.OnClick.AddListener(() =>
        {
            MyViewer.OnSelect(ring.active ? -1 : IndexMin);
        });

        var collider = renderer.gameObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.09f;
    }

    public void OnSomeIndexSelected(int selected)
    {
        ring.gameObject.SetActive(Contains(selected));
    }
}

public class GameStatisticsViewer : MonoBehaviour
{
    static GameStatisticsViewer()
    {
        ClassInjector.RegisterTypeInIl2Cpp<GameStatisticsViewer>();
    }

    public void Initialize(IArchivedGame archivedGame, PoolablePlayer playerPrefab, MapBehaviour mapPrefab, float mapScale, TMPro.TextMeshPro endTextPrefab, bool showMapAlways, float? waitTime = null)
    {
        this.archivedGame = archivedGame;
        this.playerPrefab = playerPrefab;
        this.mapPrefab = mapPrefab;
        this.mapScale = mapScale;
        this.gameEndText = endTextPrefab;
        this.waitTime = waitTime;
        this.showMapAlways = showMapAlways;
    }

    LineRenderer timelineBack = null!, timelineFront = null!;
    GameObject minimap = null!;
    GameObject baseOnMinimap = null!, detailHolder = null!;
    AlphaPulse mapColor = null!;
    IArchivedEvent[] allStatistics = null!;
    IArchivedEvent? eventPiled, eventSelected, currentShown;
    GameObject CriticalPoints = null!;
    
    public float? SelectedTime => eventSelected?.Time;
    private float? waitTime = null;
    private IArchivedGame archivedGame = null!;
    private MapBehaviour mapPrefab = null!;
    private GameObject mapObjPrefab => mapPrefab.transform.GetChild(1).gameObject;
    private float mapScale = 1f;
    private PoolablePlayer playerPrefab = null!;
    private TMPro.TextMeshPro gameEndText = null!;
    private bool showMapAlways = false;
    static public GameStatisticsViewer Instance { get; private set; } = null!;

    public void Start()
    {
        allStatistics = archivedGame.ArchivedEvents;
        if (allStatistics.Length == 0) return;

        timelineBack = UnityHelper.SetUpLineRenderer("TimelineBack", transform, new Vector3(0, 0, -10f), LayerExpansion.GetUILayer(), 0.014f);
        timelineFront = UnityHelper.SetUpLineRenderer("TimelineFront", transform, new Vector3(0, 0, -15f), LayerExpansion.GetUILayer(), 0.014f);

        minimap = UnityHelper.CreateObject("Minimap", transform, new Vector3(0, -1.62f, 0));
        var scaledMinimap = UnityHelper.CreateObject("Scaled", minimap.transform, new Vector3(0, 0, 0));
        scaledMinimap.transform.localScale = new Vector3(0.45f, 0.45f, 1);
        var minimapRenderer = Instantiate(mapObjPrefab, scaledMinimap.transform);
        minimapRenderer.gameObject.name = "MapGraphic";
        minimapRenderer.transform.localScale = new Vector3(1f, 1f, 1f);
        minimapRenderer.transform.localPosition = Vector3.zero;
        mapColor = minimapRenderer.GetComponent<AlphaPulse>();
        mapColor.SetColor(MainColor);
        NebulaAsset.CreateSharpBackground(new Vector2(4.6f, 2.8f), MainColor, minimap.transform);
        baseOnMinimap = UnityHelper.CreateObject("Scaler", scaledMinimap.transform, mapPrefab.HerePoint.transform.parent.localPosition);
        detailHolder = UnityHelper.CreateObject("Detail", transform, new Vector3(0, -3.5f, 0));
        Hide();

        CriticalPoints = UnityHelper.CreateObject("CriticalMoments", transform, Vector3.zero);

        StartCoroutine(CoShowTimeLine().WrapToIl2Cpp());
    }

    public void Update()
    {
        var willShown = eventPiled ?? eventSelected;
        if (willShown != currentShown)
        {
            if (willShown == null)
                Hide();
            else
                Show(willShown);
            currentShown = willShown;
        }
    }

    private const float LineHalfWidth = 2.5f;
    public static readonly Color MainColor = new Color(0f, 242f / 255f, 156f / 255f);
    private const float BackColorRate = 0.4f;

    private IEnumerator CoShowCriticalMoment(float p, int indexMin, int indexMax)
    {
        var point = UnityHelper.CreateObject<CriticalPoint>("Moment", CriticalPoints.transform, new Vector3((p * 2f - 1f) * LineHalfWidth, 0f, -20f - indexMin));
        point.MyViewer = this;
        point.SetIndex(indexMin, indexMax);
        yield return null;
    }

    public void OnSelect(int index)
    {
        eventSelected = index >= 0 ? allStatistics[index] : null;
        CriticalPoints.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.GetComponent<CriticalPoint>().OnSomeIndexSelected(index)));
    }

    private void ShowCriticalMoment(float p, ref int index)
    {
        var sum = allStatistics[^1].Time - allStatistics[0].Time;
        int indexMin = index;
        while (index + 1 < allStatistics.Length && allStatistics[index + 1].Time - allStatistics[indexMin].Time < sum * 0.01f)
        {
            index++;
        }
        int indexMax = index;

        //ゲーム終了と結合する際は後ろに揃える
        if (indexMax == allStatistics.Length - 1) p = 1f;

        StartCoroutine(CoShowCriticalMoment(p, indexMin, indexMax).WrapToIl2Cpp());
        index = indexMax + 1;
    }

    private IEnumerator CoShowTimeLine()
    {
        StartCoroutine(CoShowTimeBackLine().WrapToIl2Cpp());
        yield return new WaitForSeconds(waitTime ?? 1.4f);

        timelineFront.SetPosition(0, new Vector3(-LineHalfWidth, 0));
        timelineFront.SetPosition(1, new Vector3(-LineHalfWidth, 0));
        timelineFront.SetColors(MainColor, MainColor);

        float p = 0f;

        float minTime = allStatistics[0].Time;
        float maxTime = allStatistics[^1].Time;
        int index = 0;

        ShowCriticalMoment(0, ref index);

        float ToP(float p) => (p - minTime) / (maxTime - minTime);

        while (p < 1f)
        {
            while (index < allStatistics.Length - 1 && ToP(allStatistics[index].Time) < p) ShowCriticalMoment(ToP(allStatistics[index].Time), ref index);

            timelineFront.SetPosition(1, new Vector3(LineHalfWidth * (p * 2f - 1f), 0));
            p += Time.deltaTime / 3f;
            yield return null;
        }
        while (index < allStatistics.Length) ShowCriticalMoment(ToP(allStatistics[index].Time), ref index);
        timelineFront.SetPosition(1, new Vector3(LineHalfWidth, 0));
    }
    private IEnumerator CoShowTimeBackLine()
    {
        float t = 0f;

        timelineBack.SetPosition(0, new Vector3(-LineHalfWidth, 0));
        timelineBack.SetColors(MainColor * BackColorRate, MainColor.AlphaMultiplied(0));

        while (true)
        {
            float log = Mathf.Log(t + 1f, 1.92f);
            float exp = t > 1.3f ? Mathf.Pow((t - 1.3f) * 0.86f, 3f) : 0f;
            t += Time.deltaTime;

            timelineBack.SetPosition(1, new Vector3(log < 1 ? log * LineHalfWidth : LineHalfWidth, 0));
            float a = exp;
            if (log > 1) a += (log - 1) / log * 0.3f * LineHalfWidth;
            timelineBack.endColor = MainColor.AlphaMultiplied(a > 1f ? 1f : a) * BackColorRate;

            if (log > 1f && a > 1f) break;

            yield return null;
        }

        timelineBack.SetPosition(1, new Vector3(LineHalfWidth, 0));
        timelineBack.endColor = MainColor * BackColorRate;
    }

    public void ClearDetail(bool onlyMinimap)
    {
        baseOnMinimap.ForEachChild((Il2CppSystem.Action<GameObject>)((c) => Destroy(c)));
        if (!onlyMinimap) detailHolder.ForEachChild((Il2CppSystem.Action<GameObject>)((c) => Destroy(c)));
    }

    public void Hide()
    {
        minimap.SetActive(showMapAlways);
        detailHolder.SetActive(false);
    }
    public void Show(IArchivedEvent statisticsEvent)
    {
        //対象となるCriticalPointを探す
        int index = 0, indexMin = 0, indexMax = 0;
        while (allStatistics[index] != statisticsEvent) index++;
        CriticalPoints.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) =>
        {
            var criticalPoint = obj.GetComponent<CriticalPoint>();
            if (criticalPoint.Contains(index))
            {
                indexMin = criticalPoint.IndexMin;
                indexMax = criticalPoint.IndexMax;
            }
        }));

        //CriticalPointが一致しない場合は詳細も含めてリセットする
        var lastIndex = Array.IndexOf(allStatistics, currentShown);
        var requireGenerateDetail = !(indexMin <= lastIndex && lastIndex <= indexMax);
        ClearDetail(!requireGenerateDetail);

        minimap.SetActive(true);
        detailHolder.SetActive(true);

        float p = 0f;
        foreach (var pos in statisticsEvent.Position)
        {
            var renderer = Instantiate(mapPrefab.HerePoint, baseOnMinimap.transform);

            Helpers.SetColors(archivedGame.GetColor(pos.Item1), renderer);

            renderer.transform.localPosition = (Vector3)(pos.Item2.ToUnityVector() / mapScale) + new Vector3(0, 0, -1f - p);
            var button = renderer.gameObject.SetUpButton();
            button.gameObject.AddComponent<BoxCollider2D>().size = new(0.3f, 0.3f);

            button.OnMouseOver.AddListener(() =>
            {
                MetaWidgetOld widget = new();

                foreach (var near in statisticsEvent.Position)
                {
                    if (near.Item2.Distance(pos.Item2) > 0.6f) continue;

                    if (widget.Count > 0) widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));
                    var roleText = archivedGame.RoleHistory.EachMoment(history => history.PlayerId == near.Item1 && !(history.Time > statisticsEvent.Time),
                        (role, ghostRole, modifiers) => RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, false)).LastOrDefault();
                    widget.Append(new MetaWidgetOld.Text(TextAttributeOld.BoldAttrLeft) { RawText = archivedGame.GetPlayer(near.Item1)!.PlayerName });
                    widget.Append(new MetaWidgetOld.VariableText(new TextAttributeOld(TextAttributeOld.BoldAttrLeft) { Alignment = TMPro.TextAlignmentOptions.TopLeft }.EditFontSize(1.35f)) { RawText = roleText ?? "" });

                }

                NebulaManager.Instance.SetHelpWidget(button, widget);
            });
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));

            p += 0.001f;
        }


        int num = 0;
        void EventToDetailShower(int eventIndex)
        {
            var target = allStatistics[eventIndex];

            GameObject detail = UnityHelper.CreateObject("EventDetail", detailHolder.transform, new Vector3(0, -0.76f * num, -10f));

            var backGround = NebulaAsset.CreateSharpBackground(new Vector2(3.4f, 0.7f), MainColor, detail.transform);

            var collider = detail.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(3.4f, 0.7f);
            var button = detail.gameObject.SetUpButton(true);
            button.OnClick.RemoveAllListeners();
            button.OnMouseOver.AddListener(() =>
            {
                OnMouseOver(eventIndex);
                backGround.color = Color.Lerp(MainColor, Color.white, 0.5f);
            });
            button.OnMouseOut.AddListener(() =>
            {
                OnMouseOver(eventIndex);
                backGround.color = MainColor;
            });

            List<GameObject> objects = [];

            Il2CppArgument<PoolablePlayer> GeneratePlayerView(byte id)
            {
                PoolablePlayer player = Instantiate(playerPrefab, detail.transform);
                var aPlayer = archivedGame?.GetPlayer(id);
                if (aPlayer != null)
                {
                    archivedGame!.GetColor(aPlayer.PlayerId).ReflectToArchivedPalette();
                    var outfit = aPlayer.DefaultOutfit.outfit;
                    outfit.ColorId = NebulaPlayerTab.ArchiveColorId;
                    player.UpdateFromPlayerOutfit(outfit, PlayerMaterial.MaskType.None, false, true, null);
                    
                    player.ToggleName(true);
                    player.SetName(aPlayer!.PlayerName, new Vector3(3.1f, 3.1f, 1f), Color.white, -15f);
                }
                player.transform.localScale = new Vector3(0.24f, 0.24f, 1f);
                player.cosmetics.nameText.transform.parent.localPosition += new Vector3(0f, -1.05f, 0f);
                return player;
            }

            if (target.SourceId.HasValue) objects.Add(GeneratePlayerView(target.SourceId.Value).Value.gameObject);

            SpriteRenderer icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", detail.transform, new Vector3(0, 0, -1f));
            icon.sprite = target.EventVariation.InteractionIcon?.GetSprite()!;
            icon.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            if (target.RelatedTag != null)
            {
                var text = Instantiate(gameEndText, icon.transform);
                text.text = target.RelatedTag.Text;
                text.color = Color.white;
                text.outlineWidth = 0.1f;
                text.fontSize = 7.5f;
                text.fontSizeMax = 7.5f;
                text.fontSizeMin = 7.45f;
                text.transform.localPosition = new Vector3(0f, -0.18f, -1f);
                text.transform.localScale = new Vector3(0.2f / 0.7f, 0.2f / 0.7f, 1f);
                icon.transform.localPosition += new Vector3(0f, 0.05f, 0f);
            }
            objects.Add(icon.gameObject);

            foreach (var p in archivedGame.GetAllPlayers())
                if ((target.TargetIdMask & 1 << p.PlayerId) != 0)
                    objects.Add(GeneratePlayerView(p.PlayerId).Value.gameObject);

            float width = Mathf.Min(1.2f, (objects.Count - 1) * 0.5f);
            for (int i = 0; i < objects.Count; i++)
            {
                float pos = objects.Count == 1 ? 0 : width * ((float)i / (objects.Count - 1) * 2f - 1f);
                objects[i].transform.localPosition += new Vector3(pos, 0, 0f);
            }

            num++;
        }


        if (requireGenerateDetail)
        {
            for (int i = indexMin; i <= indexMax; i++)
            {
                EventToDetailShower(i);
            }
        }

    }


    public void OnMouseOver(int index)
    {
        eventPiled = allStatistics[index];
    }
    public void OnMouseOut(int index)
    {
        if (eventPiled == allStatistics[index]) eventPiled = null;
    }

}