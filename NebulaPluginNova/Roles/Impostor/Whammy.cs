using Il2CppInterop.Runtime.Injection;
using MS.Internal.Xml.XPath;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Map;
using Nebula.Roles.Neutral;
using Newtonsoft.Json.Bson;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using static Nebula.Modules.ScriptComponents.NebulaSyncStandardObject;
using static Nebula.Roles.Impostor.Thurifer;
using static Nebula.Roles.Neutral.Spectre;

namespace Nebula.Roles.Impostor;


internal class Balloon
{
    static internal Image BalloonSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.Balloon.png", 100f);
    static internal Image HighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.BalloonHighlight.png", 100f);
    static internal MultiImage BalloonStringSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Balloon.BalloonString.png", 100f, 3, 3).SetPivot(new(0.5f, 0f));

    public const float DefaultStringLength = 0.97f;
    static private (float Min, float Max) StringRange = (0.61f, DefaultStringLength);
    static private float[] Variation = [0.95f, 0.90f, 0.84f, 0.79f, 0.75f, 0.72f, 0.67f, 0.63f, -100f];
    static private Vector2 HandDiff = new Vector2(0.1f, -0.05f);

    private SpriteRenderer balloonRenderer;
    private SpriteRenderer highlightRenderer;
    private SpriteRenderer stringRenderer;
    private GameObject holder;
    private GamePlayer target;

    public const float DefaultBalloonSize = 0.74f;

    private Vector2 HandCenter = Vector2.zero;
    public Balloon(GamePlayer target)
    {
        this.target = target;

        holder = UnityHelper.CreateObject("BalloonHolder", null, Vector3.zero, LayerExpansion.GetDefaultLayer());
        balloonRenderer = UnityHelper.CreateSpriteRenderer("Balloon", holder.transform, Vector3.zero);
        balloonRenderer.transform.localScale = new(DefaultBalloonSize, DefaultBalloonSize, 1f);
        balloonRenderer.gameObject.AddComponent<SortingGroup>();
        highlightRenderer = UnityHelper.CreateSpriteRenderer("Highlight", balloonRenderer.transform, new(0.03f, 0.38f, -0.01f));
        highlightRenderer.color = Color.white.AlphaMultiplied(0.35f);

        var diff = HandDiff;
        var lossyScale = target.VanillaPlayer.cosmetics.transform.lossyScale.x;
        diff.x /= lossyScale;
        diff.y /= lossyScale;
        HandCenter = diff;
        stringRenderer = UnityHelper.CreateSpriteRenderer("String", target.VanillaPlayer.cosmetics.transform, HandCenter);
        stringRenderer.transform.localScale = new(1f / lossyScale, 1f / lossyScale, 1f);
        balloonRenderer.sprite = BalloonSprite.GetSprite();
        highlightRenderer.sprite = HighlightSprite.GetSprite();
        balloonRenderer.material = HatManager.Instance.PlayerMaterial;
        stringRenderer.sprite = BalloonStringSprite.GetSprite(0);

        UpdateBalloonColor();
    }

    public Vector2 HandPos => stringRenderer.transform.position;
    public Vector2 CenterPos => HandPos + new Vector2(0f, DefaultStringLength);
    
    /// <summary>
    /// 風船を初期位置に戻します。糸を再調整する必要があります。
    /// </summary>
    private void ResetToDefaultPos()
    {
        balloonRenderer.transform.position = CenterPos.AsVector3(BalloonZ);
    }
    private float BalloonZ => target.AmOwner ? -15f : target.VanillaPlayer.transform.position.z - 0.1f;
    /// <summary>
    /// 糸の位置を調整します。
    /// </summary>
    private void ReflectString()
    {
        Vector2 diff = balloonRenderer.transform.position - stringRenderer.transform.position;
        stringRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x).RadToDeg() - 90f);
        var mag = diff.magnitude;
        stringRenderer.sprite = BalloonStringSprite.GetSprite(Variation.FindIndex(num => num < mag));
    }

    private void UpdateAngle(Vector2 lastPos, Vector2 currentPos, Vector2 targetPos)
    {
        if (!(Time.deltaTime > 0f)) return;

        var num = Math.Clamp((currentPos.x - targetPos.x) / 0.8f, -1f, 1f);
        var baseAngle = num * 75f;

        var adjustedP = (1.9f + Mathf.Max(windP, speedP) * -2.4f); //low 1.9 <-> -0.5 high で係数を調整
        var angle = baseAngle * adjustedP;
        balloonRenderer.transform.localEulerAngles = new(0f, 0f, angle);
        highlightRenderer.transform.localEulerAngles = new(0f, 0f, -angle);
    }

    //糸先の速度
    private Vector2 lastPlayerPos = Vector2.zero;
    private Vector2 posSpeed = Vector2.zero;
    private float speedP = 0f;
    private float windP = 0f;
    public void SetVisible(bool visible)
    {
        holder.SetActive(visible);
        stringRenderer.gameObject.SetActive(visible);
    }

    public void Update()
    {
        bool lastActive = holder.active;

        if (target.IsDead || !target.VanillaPlayer.Visible)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        //手元の位置を更新する
        var yDiff = (target.VanillaPlayer.cosmetics.hat.SpriteSyncNode?.Parent.GetLocalPosition(0).y ?? 0f) * 0.7f;
        stringRenderer.transform.localPosition = ((Vector2)HandCenter + new Vector2(0f, yDiff));


        Vector2 lastPos = balloonRenderer.transform.position;
        var targetPos = CenterPos;
        var diff = lastPos - targetPos;
        var lastDistance = diff.magnitude;

        Vector2 currentPlayerPos = target.Position;
        var isMoving = lastPlayerPos.Distance(currentPlayerPos) > 0.005f;
        lastPlayerPos = currentPlayerPos;

        if (!lastActive || lastDistance > 3f) ResetToDefaultPos();
        else
        {

            //弾性力と移動への追従の割合, 0に近いほど弾性力の寄与が大きい
            speedP += (isMoving ? 4f : -2.8f) * Time.deltaTime;
            speedP = Mathf.Clamp01(speedP);

            var elastic = -diff * 10.8f * (1f - speedP); //弾性力
            var resistance = -posSpeed * 1.6f; //粘性抵抗
            
            var windType = MapData.GetCurrentMapData().GetWindType(currentPlayerPos);
            var wind = MapData.CalcWind(currentPlayerPos, windType, NebulaGameManager.Instance?.CurrentTime ?? 0f); //風が風船に及ぼす力

            windP += (wind.magnitude > 1.5f ? 4f : -0.5f) * Time.deltaTime;
            windP = Mathf.Clamp01(windP);

            posSpeed += (elastic + resistance + wind) * Time.deltaTime;
            if(speedP > 0.5f) posSpeed *= 0.95f; //弾性力の寄与が小さいとき、より早く速度が失われる。
            balloonRenderer.transform.position += (Vector3)posSpeed * Time.deltaTime;
            balloonRenderer.transform.position -= (Vector3)diff.Delta(4.4f, 0f) * speedP;

            //範囲外の位置を調整
            Vector2 currentPos = balloonRenderer.transform.position;
            Vector2 handPos = HandPos;
            Vector2 currentDiff = currentPos - handPos;
            float currentDistance = currentDiff.magnitude;

            if (currentDistance > StringRange.Max)
                balloonRenderer.transform.position -= (Vector3)currentDiff.normalized * (currentDistance - StringRange.Max);
            else if (currentDistance < StringRange.Min)
                balloonRenderer.transform.position += (Vector3)currentDiff.normalized * (StringRange.Min - currentDistance);
            

            balloonRenderer.transform.SetWorldZ(BalloonZ);
        }

        ReflectString();
        UpdateAngle(lastPos, balloonRenderer.transform.position, CenterPos);
    }

    public Vector2 BalloonPos => balloonRenderer.transform.position;
    public float Scale => balloonRenderer.transform.localScale.x;
    public void UpdateAlpha(float alpha)
    {
        balloonRenderer.color = Color.white.AlphaMultiplied(alpha);
        stringRenderer.color = Color.white.AlphaMultiplied(alpha);
        highlightRenderer.color = Color.white.AlphaMultiplied(alpha * 0.35f);
    }

    public void UpdateBalloonColor()
    {
        PlayerMaterial.SetColors(target.CurrentOutfit.outfit.ColorId, balloonRenderer);
    }

    public void DestroyBalloon()
    {
        GameObject.Destroy(holder);
        GameObject.Destroy(stringRenderer.gameObject);
    }
}

internal class PlayerGainBalloonEvent : AbstractPlayerEvent
{
    public GamePlayer Whammy { get; private init; }
    public PlayerGainBalloonEvent(GamePlayer player, GamePlayer whammy) : base(player) {
        this.Whammy = whammy;
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
[NebulaRPCHolder]
internal class BalloonHolder : AbstractModule<GamePlayer>, IGameOperator, IBindPlayer
{
    static public BalloonHolder LocalBalloonHolder = null!;
    public GamePlayer MyPlayer => MyContainer;

    public bool HasBalloon => balloon != null;

    static BalloonHolder() => DIManager.Instance.RegisterModule(() => new BalloonHolder());
    private BalloonHolder()
    {
        this.Register(NebulaAPI.CurrentGame!);
    }

    protected override void OnInjected(GamePlayer container)
    {
        if (container.AmOwner) LocalBalloonHolder = this;
    }

    Balloon? balloon = null;
    GamePlayer? myWhammy = null;
    float timeLimit;
    bool sentDieRpc = false;

    void OnUpdate(GameLateUpdateEvent ev)
    {
        if (HasBalloon)
        {
            balloon?.Update();
            if (MyPlayer.AmOwner && !MeetingHud.Instance && !ExileController.Instance && !MyPlayer.IsDead)
            {
                timeLimit -= Time.deltaTime;
                if(timeLimit < 0f)
                {
                    timeLimit = 0f;
                    if (!sentDieRpc)
                    {
                        RpcBalloonKill.Invoke((MyPlayer, myWhammy!, MyPlayer.Position));
                        sentDieRpc = true;
                    }
                }
            }
        }
    }

    [OnlyMyPlayer, Local]
    void OnDied(PlayerDieEvent ev)
    {
        if (MyPlayer.PlayerState != PlayerState.Pseudocide) OnDeadFinally();
    }

    public void OnDeadFinally()
    {
        if(HasBalloon) RpcUpdateBalloon.Invoke((MyPlayer, null));
    }


    [Local]
    void OnUpdateTaskText(PlayerTaskTextLocalEvent ev)
    {
        if(HasBalloon && timeLimit > 0f)
        {
            ev.AppendText(Language.Translate("roles.whammy.taskText").Replace("%TIME%", ((int)timeLimit + 1).ToString()).Color(Mathf.Repeat(Time.time, 0.5f) < 0.25f ? Color.red : Color.yellow));
        }
    }

    private static Vector2[] VisibilityCheckVectors = [new(-0.68f, 0.45f), new(0.68f, 0.45f), new(0f, 0f), new(0f, 0.9f)];

    [OnlyMyPlayer]
    void OnAlphaChanged(PlayerAlphaUpdateEvent ev)
    {
        if (balloon == null) return;

        if (MyPlayer.AmOwner)
        {
            balloon.UpdateAlpha(ev.AlphaIgnoresWall);
        }else
        {
            if(ev.AlphaIgnoresWall > ev.Alpha)
            {
                int objectMask = Constants.ShipAndAllObjectsMask;
                Vector2 cameraPos = GamePlayer.LocalPlayer!.Position;

                if (Helpers.AnyNonTriggersBetween(ev.Player.Position, balloon.BalloonPos, out _, objectMask))
                {
                    //風船所持者と風船の間に壁(影)がある場合
                    balloon.UpdateAlpha(ev.Alpha);
                }else if (VisibilityCheckVectors.Any(v => !Helpers.AnyNonTriggersBetween(cameraPos, balloon.BalloonPos + v * balloon.Scale, out _, objectMask)))
                {
                    //風船所持者と風船の間を隔てる壁がなく、風船とカメラの間に壁(影)がない場合
                    balloon.UpdateAlpha(ev.AlphaIgnoresWall);
                }
                else
                {
                    //少なくとも風船とカメラの間に壁(影)がある場合
                    balloon.UpdateAlpha(ev.Alpha);
                }
            }
            else
            {
                balloon.UpdateAlpha(ev.Alpha);
            }
        }
    }

    [OnlyMyPlayer]
    void OnOutfitChanged(PlayerOutfitChangeEvent ev)
    {
        balloon?.UpdateBalloonColor();
    }

    internal void TryGainBalloonLocal(GamePlayer whammy)
    {
        if (balloon == null)
        {
            RpcUpdateBalloon.Invoke((MyPlayer, whammy));
            ModSingleton<BalloonManager>.Instance.OnBalloonSetLocal();
        }
    }

    internal void TryReleaseBalloonLocal()
    {
        if (balloon != null)
        {
            RpcUpdateBalloon.Invoke((MyPlayer, null));
        }
    }

    static private readonly RemoteProcess<(GamePlayer player, GamePlayer? whammy)> RpcUpdateBalloon = new("UpdateBalloon", (message, _) =>
    {
        var holder = message.player.GetModule<BalloonHolder>();
        if (holder == null) return;
        bool equip = message.whammy != null;
        if (equip && holder.balloon == null)
        {
            holder.balloon = new Balloon(message.player);
            holder.timeLimit = Whammy.TimeLimitOption;
            holder.myWhammy = message.whammy;
            GameOperatorManager.Instance?.Run(new PlayerGainBalloonEvent(holder.MyPlayer, holder.myWhammy!));
        }
        else if (!equip && holder.balloon != null)
        {
            holder.balloon.DestroyBalloon();
            holder.balloon = null;
        }

        holder.sentDieRpc = false;
    });

    static private readonly RemoteProcess<(GamePlayer player, GamePlayer whammy, Vector2 position)> RpcBalloonKill = new("BalloonKill", (message, _) =>
    {
        PlayBalloonKill(message.whammy, message.player, message.position);
    });

    static private void PlayBalloonKill(GamePlayer whammy, GamePlayer dead, Vector2 position)
    {
        AmongUsUtil.PlayCinematicKill(whammy, dead, 1.8f, 0.6f, PlayerState.Balloon, EventDetail.Balloon, () => {
            var balloonHolder = UnityHelper.CreateObject("BalloonDeadBody", null, position.AsVector3(-1f), LayerExpansion.GetDefaultLayer());
            

            balloonHolder.transform.localScale = new Vector3(1f, 1f, 0.1f);

            GameOperatorManager.Instance?.Subscribe<MeetingEndEvent>(_ => { if (balloonHolder) GameObject.Destroy(balloonHolder); }, new GameObjectLifespan(balloonHolder));

            var balloonRenderer = UnityHelper.CreateSpriteRenderer("Balloon", balloonHolder.transform, new(0f, Balloon.DefaultStringLength, 1f));
            balloonRenderer.transform.localScale = new(Balloon.DefaultBalloonSize, Balloon.DefaultBalloonSize, 1f);
            balloonRenderer.material = HatManager.Instance.PlayerMaterial;
            balloonRenderer.gameObject.AddComponent<SortingGroup>();
            PlayerMaterial.SetColors(dead.PlayerId, balloonRenderer);
            var highlightRenderer = UnityHelper.CreateSpriteRenderer("Highlight", balloonRenderer.transform, new(0.03f, 0.38f, -0.01f));
            highlightRenderer.color = Color.white.AlphaMultiplied(0.35f);
            var stringRenderer = UnityHelper.CreateSpriteRenderer("String", balloonHolder.transform, new(0f, 0f, 1.1f));
            balloonRenderer.sprite = Balloon.BalloonSprite.GetSprite();
            highlightRenderer.sprite = Balloon.HighlightSprite.GetSprite();
            stringRenderer.sprite = Balloon.BalloonStringSprite.GetSprite(0);

            var bodyCenter = UnityHelper.CreateObject("Center", balloonHolder.transform, new(0f, -0.1f, 0f));
            var deadBody = GameObject.Instantiate(HudManager.Instance.KillOverlay.KillAnims[0].victimParts, bodyCenter.transform);
            deadBody.transform.localPosition = new Vector3(0.12f, 0.2f, 0f);
            deadBody.transform.localScale = new(0.36f, 0.36f, 0.5f);
            deadBody.transform.localEulerAngles = new(0f, 0f, 0f);
            deadBody.gameObject.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetDefaultLayer());
            deadBody.UpdateFromPlayerOutfit(dead.DefaultOutfit.outfit, PlayerMaterial.MaskType.None, false, false, (System.Action)(() =>
            {
                var skinView = deadBody.GetSkinView();
                var skinAnim = deadBody.GetSkinSpriteAnim();
                if (skinView != null) skinAnim?.Play(skinView.KillStabVictim);

                deadBody.StartCoroutine(CoAnimDeadBody().WrapToIl2Cpp());
                deadBody.StartCoroutine(CoUpdateAngle().WrapToIl2Cpp());
                deadBody.StartCoroutine(CoUpdatePosition().WrapToIl2Cpp());
            }));
            deadBody.ToggleName(false);

            if (dead.AmOwner) NebulaAsset.PlaySE(NebulaAudioClip.BalloonKill, volume: 1f);

            IEnumerator CoAnimDeadBody()
            {
                var anims = deadBody.GetComponentsInChildren<SpriteAnim>();
                foreach (var anim in anims) anim.Speed = 1f;

                foreach (var anim in anims) anim.Time = 0.25f;
                yield return Effects.Wait(0.64f);

                for (int i = 0; i < 4; i++)
                {
                    foreach (var anim in anims) anim.Time = 0.45f;
                    yield return Effects.Wait(0.44f);
                }
                foreach (var anim in anims)
                {
                    anim.Time = 0.75f;
                    anim.Speed = 0f;
                }
            }

            IEnumerator CoUpdateAngle()
            {
                yield return Effects.Wait(0.08f);
                float angle = 0f;
                for (int i = 0; i < 3; i++)
                {
                    angle -= 20f;
                    bodyCenter.transform.localEulerAngles = new(0f, 0f, angle);
                    yield return Effects.Wait(0.1f);
                }
            }

            IEnumerator CoUpdatePosition()
            {
                float h = 0f;
                float p = 0f;
                while (true)
                {
                    if (h < 0.4f) h += Time.deltaTime * 0.3f; 
                    else h = 0.4f;

                    if (p < 1f) p += Time.deltaTime * 0.5f; 
                    else p = 1f;

                    float scale = Balloon.DefaultBalloonSize + h * 1.4f;
                    balloonRenderer.transform.localScale = new(scale, scale, 1f);

                    var z = balloonHolder.transform.position.z;
                    balloonHolder.transform.position = (position + new Vector2(0f, h + p * 0.12f * Mathf.Sin(Time.time * 0.92f))).AsVector3(z);
                    yield return null;
                }
            }

            return ((position + new Vector2(0f, 1.3f)).AsVector3(-1f), balloonHolder);
        });
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class BalloonConsole : NebulaSyncStandardObject{
    public int ConsoleId = -1;
    public int LeftStone = 5;

    static internal Image ConsoleSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.Console.png", 100f);
    static internal Image ArrowSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.Arrow.png", 120f);
    public BalloonConsole(Vector2 pos) : base(pos, ZOption.Back, true, ConsoleSprite.GetSprite(), Color.white)
    {
        ModSingleton<BalloonManager>.Instance.RegisterConsole(this);
        
        MyRenderer.material = VanillaAsset.GetHighlightMaterial();
        Console = MyRenderer.gameObject.AddComponent<CustomConsole>();
        Console.Renderer = MyRenderer;
        Console.Property = new()
        {
            CanUse = (console) =>
            {
                var myPlayer = GamePlayer.LocalPlayer;
                return !(myPlayer?.IsDead ?? true) && BalloonHolder.LocalBalloonHolder.HasBalloon && LeftStone > 0;
            },
            Use = CustomConsoleProperty.MinigameAction<SlingshotMinigame>(null!, (minigame, console) =>
            {
                minigame.StonesPattern = new bool[LeftStone];
                if (LeftStone > 0 && Helpers.Prob(0.02f)) minigame.StonesPattern[0] = true;
                minigame.OnStoneUsed = () => { LeftStone--; ModSingleton<BalloonManager>.Instance.CurrentStone++; };
            }),
            OutlineColor = Color.yellow
        };
        
        var arrow = new Arrow(ArrowSprite.GetSprite(), false, true) { IsAffectedByComms = false, FixedAngle = true, TargetPos = pos, IsActive = false }.Register(this);
        GameOperatorManager.Instance.Subscribe<GameUpdateEvent>(ev => arrow.IsActive = !GamePlayer.LocalPlayer!.IsDead && (BalloonHolder.LocalBalloonHolder?.HasBalloon ?? false) && LeftStone > 0, NebulaAPI.CurrentGame!);
    }

    public const string MyTag = "WhammyConsole";
    public CustomConsole Console { get; private set; }
    static BalloonConsole() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new BalloonConsole(new(args[0], args[1])));

}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
[NebulaRPCHolder]
public class BalloonManager : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static public readonly MultiImage BalloonTrapSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Balloon.ConsoleBalloon.png", 100f, 4, 1).SetPivot(new(0.025f,0.15f));
    private record TrappedConsole(Console Console, SpriteRenderer Animator);
    private List<TrappedConsole> localTrappedConsoles = [];

    static BalloonManager() => DIManager.Instance.RegisterModule(() => new BalloonManager());
    private List<BalloonConsole> allConsoles = [];
    public IReadOnlyList<BalloonConsole> AllConsoles => allConsoles;
    internal void RegisterConsole(BalloonConsole dish)
    {
        dish.ConsoleId = allConsoles.Count;
        allConsoles.Add(dish);
    }
    private BalloonManager()
    {
        ModSingleton<BalloonManager>.Instance = this;
        this.Register(NebulaAPI.CurrentGame!);
    }
    public bool IsAvailable { get; private set; } = false;
    public int GoalStone = 1, CurrentStone = 0;
    public bool NextStonePopsBalloon => GoalStone <= CurrentStone;

    void OnGameStart(GameStartEvent ev)
    {
        IsAvailable = GeneralConfigurations.CurrentGameMode == Virial.Game.GameModes.FreePlay || (Whammy.MyRole as ISpawnable).IsSpawnable;

        if (!IsAvailable) return;

        if (AmongUsClient.Instance.AmHost)
        {
            using (RPCRouter.CreateSection("SpawnBalloonConsole"))
            {
                var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();
                spawner?.Spawn(Whammy.NumOfMaxMinigameConsolesOption, 9f, "balloonConsole", BalloonConsole.MyTag, MapObjectType.SmallOrTabletopOutOfSight);
            }
        }
    }

    /// <summary>
    /// 風船を拾ってしまったときに呼び出します。ミニゲームの石を調整します。
    /// </summary>
    public void OnBalloonSetLocal()
    {
        int maxStonesByConsole = Whammy.StoneAssignmentOption;
        AllConsoles.Do(c => c.LeftStone = maxStonesByConsole);
        var max = Math.Min(AllConsoles.Count, Whammy.NumOfMaxMinigameToBreakBalloonOption) * maxStonesByConsole;
        var min = Math.Clamp(maxStonesByConsole - 1, 1, max);
        var rand = System.Random.Shared.NextSingle();
        
        CurrentStone = 0;
        GoalStone = (int)((max - min) * rand) + min;
    }

    void OnOpenConsoleLocal(PlayerBeginMinigameByConsoleLocalEvent ev)
    {
        if (IsAvailable) RpcCheckConsole.Invoke((ev.Player, ev.Console.name, ev.Console.transform.position));
    }

    public bool ConsoleHasTrap(Console console) => localTrappedConsoles.Any(c => c.Console.GetInstanceID() == console.GetInstanceID());
    public void EntrapToConsole(Console console) {
        bool flip = console!.transform.position.x > PlayerControl.LocalPlayer.transform.position.x;
        Vector3 pos = GetConsoleBalloonPos(console, ref flip);

        var renderer = UnityHelper.SimpleAnimator(console!.transform, pos, 0.2f, num => BalloonManager.BalloonTrapSprite.GetSprite(num % 4));
        var scale = renderer.transform.lossyScale;
        
        renderer.transform.localScale = new((flip ? -1f : 1f) / scale.x, 1f / scale.y, 1f);
        localTrappedConsoles.Add(new(console, renderer));
    }

    private void ReleaseTrap(TrappedConsole console)
    {
        GameObject.Destroy(console.Animator.gameObject);
        localTrappedConsoles.Remove(console);
    }

    static private readonly RemoteProcess<(GamePlayer player, GamePlayer whammy)> RpcBalloon = new("SetBalloon",
        (message, _) => {
            if (message.player.AmOwner) message.player.GetModule<BalloonHolder>()?.TryGainBalloonLocal(message.whammy);
        });

    static private readonly RemoteProcess<(GamePlayer player, string name, Vector2 pos)> RpcCheckConsole = new("CheckConsole",
        (message, _) => {
            //既に風船を持っているなら何もしない。
            if (message.player.GetModule<BalloonHolder>()?.HasBalloon ?? false || message.player.IsDead) return;

            var instance = ModSingleton<BalloonManager>.Instance;
            var trappedConsoles = instance.localTrappedConsoles;
            foreach(var c in trappedConsoles)
            {
                if(c.Console.name == message.name && c.Console.transform.position.Distance(message.pos) < 1f)
                {
                    instance.ReleaseTrap(c);
                    RpcBalloon.Invoke((message.player, GamePlayer.LocalPlayer!));
                }
            }
        });

    /// <summary>
    /// マップ、タスクごとに風船の位置を調整します。
    /// </summary>
    /// <param name="flip"></param>
    /// <returns></returns>
    static private Vector3 GetConsoleBalloonPos(Console console, ref bool flip)
    {
        Vector3 pos = new(0f, 0f, 0.000001f);
        var mapId = AmongUsUtil.CurrentMapId;
        if(mapId == 2)
        {
            //Polus
            switch (console.name)
            {
                case "panel_telescope":
                    //望遠鏡タスクは覗き口にあわせる。
                    pos.x = 0.2f;
                    pos.y = 0.3f;
                    break;
            }
        }
        else if(mapId == 4)
        {
            //Airship
            switch (console.name)
            {
                case "task_records_folder":
                    //フォルダ整理タスクはzを0にするとちょうど良い表示になる。
                    pos.z = 0f;
                    break;
                case "panel_cockpit_steering":
                    //操縦桿の調整タスクは必ず反転させる。
                    flip = true; 
                    break;
                case "task_tapes_right":
                    //テープタスクは少し上に調整する。
                    pos.y += 0.3f; 
                    break;
                case "task_safe":
                    //金庫タスクは少し上に調整する。
                    pos.y += 0.6f;
                    break;
            } 
        }
        else if (mapId == 5)
        {
            //Fungle
            switch (console.name)
            {
                case "ReplacePartsConsole":
                    //パーツ修理タスクはそれぞれ少し上に調整する。
                    if (console.Room == SystemTypes.Reactor) pos.y += 0.6f;
                    if (console.Room == SystemTypes.Comms) pos.y += 0.2f;
                    break;
                case "RoastMarshmallowFireConsole":
                    //マシュマロを焼くタスクは奥行と高さを調整する。
                    pos.y = 0.3f;
                    pos.z = -0.1f;
                    break;
                case "LiftWeightsConsole":
                    //重量あげタスクは位置を調整する。
                    pos.x = 0.2f;
                    pos.y = 0.2f;
                    break;
                case "CrankGeneratorConsole":
                    //発電タスクは位置を豆電球に揃える。
                    pos.x = -0.3f;
                    pos.y = 0.6f;
                    break;
                case "CatchFishConsole":
                    pos.x = 0.4f;
                    pos.y = 0.5f;
                    break;
            }
        }

        return pos;
    }
}

file static class SlingshotMinigameAssets
{
    static public Image MinigameBalloon = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameBalloon.png", 100f).SetPivot(new(0.46f, 0.19f));
    static public Image MinigameBalloonBroken = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameBalloonBroken.png", 100f).SetPivot(new(0.5f, 0.1f));
    static public Image MinigameString1 = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameString1.png", 100f);
    static public Image MinigameString2 = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameString2.png", 100f);
    static public MultiImage MinigameStones = DividedSpriteLoader.FromResource("Nebula.Resources.Balloon.Stone.png", 100f, 2, 1);
    static public Image MinigameBoxFront = SpriteLoader.FromResource("Nebula.Resources.Balloon.BoxFront.png", 100f);
    static public Image MinigameBoxBack = SpriteLoader.FromResource("Nebula.Resources.Balloon.BoxBack.png", 100f);
    static public Image MinigameBackground = SpriteLoader.FromResource("Nebula.Resources.Balloon.Background.png", 100f);

    static public Image MinigameLampBack = SpriteLoader.FromResource("Nebula.Resources.Balloon.LampBack.png", 100f).SetPivot(new(0.0625f, 0.15f));
    static public MultiImage MinigameLamp = DividedSpriteLoader.FromResource("Nebula.Resources.Balloon.IndicatorLamp.png", 100f, 2, 1);
}
internal class Slingshot : MonoBehaviour
{
    Transform StoneHolder;
    MeshFilter LeftMesh, LeftEdgeLowerMesh, LeftEdgeUpperMesh, RightMesh, RightEdgeLowerMesh, RightEdgeUpperMesh;

    public SpriteRenderer HandRenderer;

    static Slingshot() => ClassInjector.RegisterTypeInIl2Cpp<Slingshot>();

    private bool isDown = false;
    private float diff = 0f, holderVelocity = 0f;
    private Vector2 downPos;
    private Vector2 dir = Vector2.right.Rotate(-44f);
    private Vector2 dirNeg = Vector2.right.Rotate(-72f);
    private PassiveButton slingshotButton = null!;
    private float power = 0f;
    public float Power => power;

    private static Color bandColor = new(235f / 255f, 197f / 255f, 118f / 255f);
    private static Color edgeColor = new(91f / 255f, 77f / 255f, 47f / 255f);

    private Material bandMat = null!, edgeMat = null!, normalMat = null!;
    private SpriteRenderer[] renderers = null!;
    private SpriteRenderer grabbedStoneRenderer = null!;
    private bool StoneIsReady = false;
    private bool LastStoneIsCrewStone = false;

    private Action<bool>? onShot = null!;
    public void SetCallback(Action<bool> onShotCallback)
    {
        onShot = onShotCallback;
    }

    public bool SetStoneToSlingshot(bool crewStone)
    {
        if (StoneIsReady) return false;

        NebulaAsset.PlaySE(NebulaAudioClip.BalloonRock, volume: 1f);

        StoneIsReady = true;
        LastStoneIsCrewStone = crewStone;
        SetColor(Color.white);
        grabbedStoneRenderer.gameObject.SetActive(true);
        grabbedStoneRenderer.color = Color.clear;
        grabbedStoneRenderer.sprite = SlingshotMinigameAssets.MinigameStones.GetSprite(crewStone ? 1 : 0);
        grabbedStoneRenderer.sharedMaterial = crewStone ? HandRenderer.material : normalMat;

        StartCoroutine(ManagedEffects.Sequence(
            ManagedEffects.Wait(0.35f),
                    ManagedEffects.Lerp(0.4f, p =>
                    {
                        grabbedStoneRenderer.color = Color.white.AlphaMultiplied(p);
                    }),
                    ManagedEffects.Action(() => { })).WrapToIl2Cpp());


        return true;
    }

    private void ResetStone()
    {
        StoneIsReady = false;
        grabbedStoneRenderer.gameObject.SetActive(false);
    }

    void SetColor(Color color)
    {
        bandMat.color = bandColor * color;
        edgeMat.color = edgeColor * color;
        renderers.Do(r => r.color = color);
    }

    void Awake()
    {
        StoneHolder = transform.GetChild(2);

        HandRenderer = transform.GetChild(3).GetComponent<SpriteRenderer>();
        HandRenderer.material = HatManager.Instance.PlayerMaterial;
        PlayerMaterial.SetColors(GamePlayer.LocalPlayer!.PlayerId, HandRenderer);

        renderers = transform.GetComponentsInChildren<SpriteRenderer>().ToArray();

        (var bmr, LeftMesh) = UnityHelper.CreateMeshRenderer("LeftBand", transform, new(0f, 0f, -0.28f), LayerExpansion.GetUILayer(), bandColor);
        (_, RightMesh) = UnityHelper.CreateMeshRenderer("RightBand", transform, new(0f, 0f, -0.08f), LayerExpansion.GetUILayer(), bandColor, bmr.sharedMaterial);
        (var emr, LeftEdgeLowerMesh) = UnityHelper.CreateMeshRenderer("LeftBandE1", transform, new(0f, 0f, -0.3f), LayerExpansion.GetUILayer(), edgeColor);
        (_, LeftEdgeUpperMesh) = UnityHelper.CreateMeshRenderer("LeftBandE2", transform, new(0f, 0f, -0.3f), LayerExpansion.GetUILayer(), edgeColor, emr.sharedMaterial);
        (_, RightEdgeLowerMesh) = UnityHelper.CreateMeshRenderer("RightBandE1", transform, new(0f, 0f, -0.1f), LayerExpansion.GetUILayer(), edgeColor, emr.sharedMaterial);
        (_, RightEdgeUpperMesh) = UnityHelper.CreateMeshRenderer("RightBandE2", transform, new(0f, 0f, -0.1f), LayerExpansion.GetUILayer(), edgeColor, emr.sharedMaterial);
        bandMat = bmr.sharedMaterial;
        edgeMat = emr.sharedMaterial;

        LeftMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        LeftEdgeLowerMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        LeftEdgeUpperMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightEdgeLowerMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightEdgeUpperMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));

        var collider = UnityHelper.CreateObject<BoxCollider2D>("Collider", this.transform, new(0.2f, -0.3f, 0f));
        collider.size = new(1f, 1f);
        collider.isTrigger = true;
        slingshotButton = collider.gameObject.SetUpButton(false);
        slingshotButton.OnDown = true;
        slingshotButton.OnUp = false;
        slingshotButton.OnClick.AddListener(() => {
            if (!StoneIsReady) return;

            isDown = true;
            downPos = Input.mousePosition;
        });

        grabbedStoneRenderer = UnityHelper.CreateObject<SpriteRenderer>("GrabbedStone", StoneHolder.transform, new(0.48f, -0.42f, 0.25f));
        grabbedStoneRenderer.sprite = null;
        normalMat = grabbedStoneRenderer.sharedMaterial;

        SetColor(Color.white.RGBMultiplied(0.6f));
    }

    bool playedSECache = false;

    void Update()
    {
        if (isDown)
        {
            holderVelocity = 0f;
            if (!Input.GetMouseButton(0))
            {
                //マウスを離したとき
                isDown = false;
                if (!(power < 1f))
                {
                    //発射成功
                    diff = -0.5f;

                    onShot?.Invoke(LastStoneIsCrewStone);
                    ResetStone();
                }
                power = 0f;

                if (playedSECache)
                {
                    NebulaAsset.StopNamedSE(SEName);
                    playedSECache = false;
                }

                return;
            }
            else
            {
                var diffVec = (Vector2)Input.mousePosition - downPos;
                diff = Mathf.Clamp(Vector2.Dot(diffVec, dir) / 150f, 0f, 1.4f);
                var p = diff / 1.4f;
                var max = Mathf.Clamp01(p * 1.25f);
                var speed = Mathf.Clamp(p * 2f, 0f, 0.78f);
                if (p > 0.2f)
                {
                    if (!playedSECache)
                    {
                        NebulaAsset.PlayNamedLoopSE(NebulaAudioClip.Slingshot, SEName);
                        playedSECache = true;
                    }

                    power += speed * Time.deltaTime;
                    if (power > max) power = max;
                }
                else
                {
                    power -= 1.2f * Time.deltaTime;
                    if (power > 0f) power = 0f;
                }
            }
        }
        else
        {
            holderVelocity -= diff * 200f * Time.deltaTime;
            holderVelocity *= 0.86f;
            diff += holderVelocity * Time.deltaTime;
        }

        var z = StoneHolder.transform.localPosition.z;
        StoneHolder.transform.localPosition = ((diff < 0f ? dirNeg : dir) * diff + new Vector2(-0.3f, 0.5f)).AsVector3(z);

        UpdateMesh();
    }
    void UpdateMesh()
    {
        Vector2 leftEdgeStoneUpper = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.37f, -0.56f), transform);
        Vector2 leftEdgeStoneLower = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.35f, -0.76f), transform);
        Vector2 leftEdgeShotUpper = new(-0.44f, 0.44f);
        Vector2 leftEdgeShotLower = new(-0.44f, 0.25f);

        Vector2 rightEdgeStoneUpper = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.84f, -0.43f), transform);
        Vector2 rightEdgeStoneLower = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.83f, -0.66f), transform);
        Vector2 rightEdgeShotUpper = new(0.45f, 0.15f);
        Vector2 rightEdgeShotLower = new(0.45f, 0.01f);

        Vector2 leftDir = leftEdgeShotUpper - leftEdgeStoneUpper;
        Vector2 leftNorm = new Vector2(leftDir.y, -leftDir.x).normalized;
        Vector2 rightDir = rightEdgeShotUpper - rightEdgeStoneUpper;
        Vector2 rightNorm = new Vector2(rightDir.y, -rightDir.x).normalized;

        LeftEdgeLowerMesh.mesh.SetVertices((Vector3[])[
            leftEdgeStoneLower + leftNorm * -0.015f,
            leftEdgeStoneLower + leftNorm * 0.015f,
            leftEdgeShotLower + leftNorm * -0.015f,
            leftEdgeShotLower + leftNorm * 0.015f
            ]);
        LeftEdgeUpperMesh.mesh.SetVertices((Vector3[])[
            leftEdgeStoneUpper + leftNorm * -0.015f,
            leftEdgeStoneUpper + leftNorm * 0.015f,
            leftEdgeShotUpper + leftNorm * -0.015f,
            leftEdgeShotUpper + leftNorm * 0.015f
            ]);
        RightEdgeLowerMesh.mesh.SetVertices((Vector3[])[
            rightEdgeStoneLower + rightNorm * -0.015f,
            rightEdgeStoneLower + rightNorm * 0.015f,
            rightEdgeShotLower + rightNorm * -0.015f,
            rightEdgeShotLower + rightNorm * 0.015f
            ]);
        RightEdgeUpperMesh.mesh.SetVertices((Vector3[])[
            rightEdgeStoneUpper + rightNorm * -0.015f,
            rightEdgeStoneUpper + rightNorm * 0.015f,
            rightEdgeShotUpper + rightNorm * -0.015f,
            rightEdgeShotUpper + rightNorm * 0.015f
            ]);

        if (leftEdgeShotLower.x < leftEdgeStoneLower.x)
        {
            LeftMesh.mesh.SetVertices((Vector3[])[
                leftEdgeStoneLower,
                leftEdgeStoneUpper,
                leftEdgeShotLower,
                leftEdgeShotUpper
            ]);
        }
        else
        {
            LeftMesh.mesh.SetVertices((Vector3[])[
                leftEdgeStoneUpper,
                leftEdgeStoneLower,
                leftEdgeShotUpper,
                leftEdgeShotLower
            ]);
        }

        if (rightEdgeShotLower.x < rightEdgeStoneLower.x)
        {
            RightMesh.mesh.SetVertices((Vector3[])[
                rightEdgeStoneLower,
                rightEdgeStoneUpper,
                rightEdgeShotLower,
                rightEdgeShotUpper
            ]);
        }
        else
        {
            RightMesh.mesh.SetVertices((Vector3[])[
                rightEdgeStoneUpper,
                rightEdgeStoneLower,
                rightEdgeShotUpper,
                rightEdgeShotLower
            ]);
        }
    }

    void OnDestroy()
    {
        NebulaAsset.StopNamedSE(SEName);
    }

    private const string SEName = "NoSSlingshot";
}

internal class SlingshotMinigame : Minigame
{

    static SlingshotMinigame() => ClassInjector.RegisterTypeInIl2Cpp<SlingshotMinigame>();
    public SlingshotMinigame(System.IntPtr ptr) : base(ptr) { }
    public SlingshotMinigame() : base(ClassInjector.DerivedConstructorPointer<SlingshotMinigame>())
    { ClassInjector.DerivedConstructorBody(this); }

    private static readonly Vector3 SlingshotPosition = new(0.4f, -0.8f, -10f);
    public Il2CppArgument<Slingshot> Slingshot;

    private GameObject BalloonAndStringBroken;
    private GameObject BalloonAndString;
    private SpriteRenderer Balloon;

    public Action? OnStoneUsed = null;
    int UsedStones = 0;
    public bool[] StonesPattern = [false, false, false];

    static Color[] LampColors = [Color.red, Color.Lerp(Color.red, Color.yellow, 0.5f), Color.yellow];
    static Color[] LampOffColors = LampColors.Select(c => Color.Lerp(c, Color.white, 0.25f).RGBMultiplied(0.25f)).ToArray();
    static int[] LampPatterns = [2, 2, 2, 1, 1, 0, 0];

    private record Indicator(SpriteRenderer Lamp, GameObject Glow);
    private Indicator[] Indicators = null!;
    private GameObject IndicatorsHolder;
    private SpriteRenderer Background;
    

    public override void Begin(PlayerTask task)
    {
        //base.begin
        this.BeginInternal(task);
        //base.begin ここまで

        MetaScreen.InstantiateCloseButton(transform, new(-3.4f, 2f, -0.5f)).OnClick.AddListener(Close);

        void BeginBackground() {
            Background = UnityHelper.CreateObject<SpriteRenderer>("Background", transform, new(0f, 0f, 2f));
            Background.sprite = SlingshotMinigameAssets.MinigameBackground.GetSprite();
            Background.transform.localScale = new(0.7f, 0.7f, 1f);
        }
        void BeginBalloon()
        {
            BalloonAndString = UnityHelper.CreateObject("Balloon", transform, new(-1.4f, 0.8f, 0f));

            Balloon = UnityHelper.CreateObject<SpriteRenderer>("Balloon", BalloonAndString.transform, new(0f, 0f, -0.1f));
            Balloon.sprite = SlingshotMinigameAssets.MinigameBalloon.GetSprite();
            Balloon.material = Slingshot.Value.HandRenderer.material;
            Balloon.transform.localScale = new(1.3f, 1.3f, 1f);

            var balloonBrokenString = UnityHelper.CreateObject<SpriteRenderer>("String", BalloonAndString.transform, new(0.01f, -1.4f, 0f));
            balloonBrokenString.sprite = SlingshotMinigameAssets.MinigameString1.GetSprite();
            balloonBrokenString.transform.localScale = new(1.1f, 1f, 1f);
        }
        void BeginBrokenBalloon()
        {
            BalloonAndStringBroken = UnityHelper.CreateObject("BalloonBroken", transform, new(-1.5f, 0.6f, 0f));

            var balloonBroken = UnityHelper.CreateObject<SpriteRenderer>("BalloonBroken", BalloonAndStringBroken.transform, new(0f, 0f, -0.1f));
            balloonBroken.sprite = SlingshotMinigameAssets.MinigameBalloonBroken.GetSprite();
            balloonBroken.material = Slingshot.Value.HandRenderer.material;
            balloonBroken.transform.localScale = new(1.3f, 1.3f, 1f);

            var balloonBrokenString = UnityHelper.CreateObject<SpriteRenderer>("String", BalloonAndStringBroken.transform, new(-0.2f, -1.3f, 0f));
            balloonBrokenString.sprite = SlingshotMinigameAssets.MinigameString2.GetSprite();
            balloonBrokenString.transform.localScale = new(1.2f, 1.2f, 1f);

            BalloonAndStringBroken.SetActive(false);
        }
        void BeginSlingshot()
        {
            var slingshot = GameObject.Instantiate(NebulaAsset.SlingshotInMinigame, transform).AddComponent<Slingshot>();
            slingshot.transform.localScale = new(1.2f, 1.2f, 1f);
            slingshot.transform.localPosition = SlingshotPosition;
            slingshot.SetCallback(OnShot);
            Slingshot = slingshot;
        }
        GameObject SpawnStone(bool crewStone, Material playerMat)
        {
            float yRandom = System.Random.Shared.NextSingle();
            var stone = UnityHelper.CreateObject<SpriteRenderer>("Stone", transform,
                new Vector3(
                    2.3f + System.Random.Shared.NextSingle() * 1.2f,
                    -0.95f - yRandom * 0.55f,
                    -3.5f - yRandom));
            stone.transform.localEulerAngles = new(0f, 0f, System.Random.Shared.NextSingle() * 360f);
            stone.sprite = SlingshotMinigameAssets.MinigameStones.GetSprite(crewStone ? 1 : 0);
            if (crewStone) stone.sharedMaterial = playerMat;

            var collider = stone.gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.28f;

            var button = stone.gameObject.SetUpButton(false);
            button.OnClick.AddListener(() =>
            {
                if (Slingshot.Value.SetStoneToSlingshot(crewStone))
                {
                    var localPos = stone.transform.localPosition;
                    var localPosTo = stone.transform.localPosition + new Vector3(0f, 0.4f, 0f);
                    collider.enabled = false;
                    StartCoroutine(ManagedEffects.Sequence(
                        ManagedEffects.Lerp(0.4f, p =>
                        {
                            stone.color = Color.white.AlphaMultiplied(1f - p);
                            stone.transform.localPosition = Vector3.Lerp(localPosTo, localPos, 1 - (p * p));
                        }),
                        ManagedEffects.Action(() => { stone.gameObject.SetActive(false); })).WrapToIl2Cpp());
                }
            });

            return stone.gameObject;
        }
        void BeginBox()
        {
            var boxFrontRenderer = UnityHelper.CreateObject<SpriteRenderer>("StoneBox", transform, new(2.8f, -1f, -5f));
            boxFrontRenderer.transform.localScale = new(1.2f, 1.2f, 1f);
            boxFrontRenderer.sprite = SlingshotMinigameAssets.MinigameBoxFront.GetSprite();
            var boxBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("Back", boxFrontRenderer.transform, new(0f, 0f, 2f));
            boxBackRenderer.sprite = SlingshotMinigameAssets.MinigameBoxBack.GetSprite();
        }
        void BeginIndicators()
        {
            var back = UnityHelper.CreateObject<SpriteRenderer>("IndicatorBack", transform, new(1.4f, -0.5f, -15f));
            back.sprite = SlingshotMinigameAssets.MinigameLampBack.GetSprite();
            IndicatorsHolder = back.gameObject;

            var holder = UnityHelper.CreateObject("Holder", back.transform, new(0.89f, 0.665f, -0.01f));
            int length = LampPatterns.Length;
            float baseY = (float)(length - 1) / 2f;
            Indicators = new Indicator[length];
            for (int i = 0; i < length; i++)
            {
                var lamp = UnityHelper.CreateObject<SpriteRenderer>("Indicator", holder.transform, new(0f, (-baseY + (float)i) * 0.22f, (float)i * -0.01f));
                lamp.sprite = SlingshotMinigameAssets.MinigameLamp.GetSprite(0);
                lamp.color = LampColors[LampPatterns[i]];
                var glow = UnityHelper.CreateObject<SpriteRenderer>("Glow", lamp.transform, new(0f, 0f, -0.1f));
                glow.sprite = SlingshotMinigameAssets.MinigameLamp.GetSprite(1);
                glow.color = LampColors[LampPatterns[i]].AlphaMultiplied(0.75f);
                Indicators[i] = new(lamp, glow.gameObject);
            }

            IndicatorsHolder.transform.localScale = new(0f, 0f, 1f);
        }

        BeginBackground();
        BeginSlingshot();
        BeginBalloon();
        BeginBrokenBalloon();
        foreach (bool isCrewStone in StonesPattern) SpawnStone(isCrewStone, Slingshot.Value.HandRenderer.material);
        BeginBox();
        BeginIndicators();
    }

    public override void Close()
    {
        this.CloseInternal();
    }

    public void Update()
    {
        Background.color = Color.Lerp(Color.white, Color.Lerp(Color.blue, Color.cyan, Mathf.Sin(Time.time * 0.6f)), 0.3f);

        var power = Slingshot.Value.Power;
        if (power > 0.3f)
        {
            float coeff = (power - 0.3f) * 0.07f;
            Slingshot.Value.transform.localPosition = SlingshotPosition + new Vector3((System.Random.Shared.NextSingle() - 0.5f) * coeff, (System.Random.Shared.NextSingle() - 0.5f) * coeff);
        }
        else
        {
            Slingshot.Value.transform.localPosition = SlingshotPosition;
        }

        var indicatorScale = IndicatorsHolder.transform.localScale.x;
        indicatorScale += (power > 0f ? 5f : -5f) * Time.deltaTime;
        indicatorScale = Mathf.Clamp01(indicatorScale);
        IndicatorsHolder.transform.localScale = new(indicatorScale, indicatorScale, 1f);

        if (power > 0f)
        {
            int lampLevel = (int)(power * (Indicators.Length - 1));
            for (int i = 0; i < Indicators.Length; i++)
            {
                bool active = lampLevel >= i;
                Indicators[i].Glow.SetActive(active);
                Indicators[i].Lamp.color = (active ? LampColors : LampOffColors)[LampPatterns[i]];
            }
        }
    }

    public void PlayMiss()
    {
        NebulaAsset.PlaySE(NebulaAudioClip.BalloonBounce, false);
        float angle = Helpers.Prob(0.5f) ? 12f : -12f;
        StartCoroutine(ManagedEffects.Lerp(0.25f, p => Balloon.transform.localEulerAngles = new(0f, 0f, angle * (1f - p) * (1f - p))).WrapToIl2Cpp());
    }

    public void PlaySuccess()
    {
        NebulaAsset.PlaySE(NebulaAudioClip.BalloonPop, false);
        BalloonAndString.SetActive(false);
        BalloonAndStringBroken.SetActive(true);
        BalloonHolder.LocalBalloonHolder?.TryReleaseBalloonLocal();
    }

    public void OnShot(bool isCrewStone)
    {
        UsedStones++;
        OnStoneUsed?.Invoke();
        bool isSuccess = ModSingleton<BalloonManager>.Instance.NextStonePopsBalloon;

        if (isSuccess)
        {
            PlaySuccess();
        }
        else
        {
            PlayMiss();
        }

        if (UsedStones == StonesPattern.Length || isSuccess)
        {
            StartCoroutine(ManagedEffects.Sequence(
                    ManagedEffects.Wait(0.4f),
                    ManagedEffects.Action(() =>
                    {
                        if (isSuccess)
                            SoundManager.Instance.PlaySound(HudManager.Instance.TaskCompleteSound, false, 1f, null);
                        else
                            NebulaAsset.PlaySE(NebulaAudioClip.BalloonFailed, false);
                        
                        Close();
                    })
                    ).WrapToIl2Cpp());
        }
    }
}

public class Whammy : DefinedSingleAbilityRoleTemplate<Whammy.Ability>, DefinedRole
{
    private Whammy() : base("whammy", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [
        new GroupConfiguration("options.role.whammy.group.place", [PlaceDurationOption,PlaySEOnPlacingBalloonOption, SEStrengthOption, AdditionalKillCooldownByPlacementOption], GroupConfigurationColor.ImpostorRed),
        new GroupConfiguration("options.role.whammy.group.gaining", [NumOfBalloonsInStartingOption, NumOfBalloonsByKillingOption], GroupConfigurationColor.ImpostorRed),
        new GroupConfiguration("options.role.whammy.group.balloon", [TimeLimitOption, NumOfMaxMinigameConsolesOption, NumOfMaxMinigameToBreakBalloonOption, StoneAssignmentOption], GroupConfigurationColor.ImpostorRed),
         ])
    {
        //ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Effacer.png");

        GameActionTypes.WhammyPlacementAction = new("whammy.placement", this, isPlacementAction: true);
    }


    static public readonly FloatConfiguration TimeLimitOption = NebulaAPI.Configurations.Configuration("options.role.whammy.balloonsTimeLimit", (10f, 400f, 10f), 140f, FloatConfigurationDecorator.Second);
    static private readonly IntegerConfiguration NumOfBalloonsInStartingOption = NebulaAPI.Configurations.Configuration("options.role.whammy.balloonsInStarting", (0, 20, 1), 3);
    static private readonly IntegerConfiguration NumOfBalloonsByKillingOption = NebulaAPI.Configurations.Configuration("options.role.whammy.balloonsByKilling", (0, 10, 1), 1);
    static public readonly IntegerConfiguration NumOfMaxMinigameConsolesOption = NebulaAPI.Configurations.Configuration("options.role.whammy.numOfMaxMinigameConsoles", (3, 8, 1), 5);
    static public readonly IntegerConfiguration NumOfMaxMinigameToBreakBalloonOption = NebulaAPI.Configurations.Configuration("options.role.whammy.numOfMaxMinigameToBreakBalloon", (1, 5, 1), 3);
    static private readonly BoolConfiguration PlaySEOnPlacingBalloonOption = NebulaAPI.Configurations.Configuration("options.role.whammy.playSeOnPlacingBalloon", false);
    static private readonly FloatConfiguration SEStrengthOption = NebulaAPI.Configurations.Configuration("options.role.whammy.seStrength", (2.5f,30f,2.5f), 10f, FloatConfigurationDecorator.Ratio, () => PlaySEOnPlacingBalloonOption);
    static private readonly FloatConfiguration PlaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.whammy.placeDuration", (0f, 5f, 1f), 3f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration AdditionalKillCooldownByPlacementOption = NebulaAPI.Configurations.Configuration("options.role.whammy.additionalKillCooldownByPlacement", (0f, 20f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static public readonly IntegerConfiguration StoneAssignmentOption = NebulaAPI.Configurations.Configuration("options.role.whammy.stoneAssignment", (1, 7), 3);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), arguments.Get(1, NumOfBalloonsInStartingOption));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Whammy MyRole = new();
    static private readonly GameStatsEntry StatsBalloon = NebulaAPI.CreateStatsEntry("stats.whammy.balloon", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.BalloonButton.png", 115f);

        int leftBalloons = 0;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), leftBalloons];

        AchievementToken<(bool killed, bool usedAbility, bool cleared)>? acAnother1Token = null;
        public Ability(GamePlayer player, bool isUsurped, int leftBalloons) : base(player, isUsurped)
        {
            this.leftBalloons = leftBalloons;
            if (AmOwner)
            {
                acAnother1Token = new("whammy.another1", (false, false, false), (val, _) => val.cleared);

                Virial.Components.ObjectTracker<Console> consoleTracker = new ObjectTrackerUnityImpl<Console, Console>(
                    MyPlayer.VanillaPlayer, AmongUsUtil.VanillaKillDistance, () => ShipStatus.Instance.AllConsoles,
                    c => !ModSingleton<BalloonManager>.Instance.ConsoleHasTrap(c), c => !c.TryCast<VentCleaningConsole>() && !c.TryCast<AutoTaskConsole>() && !c.TryCast<StoreArmsTaskConsole>(), c => c,
                    c => [c.transform.position], c => c.Image, Color.yellow,
                    true, false).Register(this);
                
                
                var balloonButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    0f, PlaceDurationOption, "balloon", buttonSprite,
                    _ => consoleTracker.CurrentTarget != null && this.leftBalloons > 0
                    ).SetAsUsurpableButton(this);
                balloonButton.ShowUsesIcon(0, this.leftBalloons.ToString());
                balloonButton.OnUpdate = _ => balloonButton.UpdateUsesIcon(this.leftBalloons.ToString());
                Console? selectedConsole = null;
                balloonButton.OnEffectStart = (button) => {
                    selectedConsole = consoleTracker.CurrentTarget;
                    var num = 1f + (AdditionalKillCooldownByPlacementOption / PlaceDurationOption);
                    using (RPCRouter.CreateSection("WhammyEntrap"))
                    {
                        player.GainAttribute(PlayerAttributes.CooldownSpeed, PlaceDurationOption, -num, false, 100);
                        MyPlayer.GainSpeedAttribute(0f, PlaceDurationOption, false, 0);
                        if (PlaySEOnPlacingBalloonOption) NebulaAsset.RpcPlaySE.Invoke((NebulaAudioClip.BalloonKill, MyPlayer.Position, 1f, SEStrengthOption));
                    }
                };
                balloonButton.OnEffectEnd = (button) =>
                {
                    this.leftBalloons--;
                    StatsBalloon.Progress(1);
                    ModSingleton<BalloonManager>.Instance.EntrapToConsole(consoleTracker.CurrentTarget!);
                    acAnother1Token.Value.usedAbility = true;

                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.WhammyPlacementAction);
                };

                {
                    //チャレンジ称号
                    int balloons = 0;
                    bool anyoneClearTask = false;
                    GameOperatorManager.Instance?.Subscribe<TaskPhaseStartEvent>(ev =>
                    {
                        balloons = GamePlayer.AllPlayers.Count(p => p.GetModule<BalloonHolder>()?.HasBalloon ?? false);
                        anyoneClearTask = false;
                    }, this);
                    GameOperatorManager.Instance?.Subscribe<PlayerGainBalloonEvent>(ev => balloons++, this);
                    GameOperatorManager.Instance?.Subscribe<PlayerTaskCompleteEvent>(ev => anyoneClearTask |= !ev.Player.IsDead, this);
                    GameOperatorManager.Instance?.Subscribe<MeetingPreStartEvent>(ev =>
                    {
                        if (balloons >= 3 && !anyoneClearTask) new StaticAchievementToken("whammy.challenge");
                    }, this);
                }
                

            }
        }

        [OnlyMyPlayer, Local]
        void OnKilledAnyone(PlayerKillPlayerEvent ev)
        {
            if (acAnother1Token != null) acAnother1Token.Value.killed = true;

            if (ev.Dead.PlayerState == PlayerState.Dead) this.leftBalloons += NumOfBalloonsByKillingOption;

            if(ev.Dead.PlayerState == PlayerState.Balloon)
            {
                new StaticAchievementToken("whammy.common1");
                new StaticAchievementToken("whammy.common2");
            }
        }

        [OnlyMyPlayer, Local]
        void OnDied(PlayerDieEvent ev)
        {
            if((ev.Player.PlayerState == PlayerState.Guessed || ev.Player.PlayerState == PlayerState.Exiled) && acAnother1Token != null)
            {
                acAnother1Token.Value.cleared |= !acAnother1Token.Value.killed && acAnother1Token.Value.usedAbility;
            } 
        }
    }
}