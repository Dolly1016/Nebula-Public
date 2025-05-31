using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Map;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
public class Cannon : DefinedSingleAbilityRoleTemplate<Cannon.Ability>, DefinedRole
{
    public Cannon() : base("cannon", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [MarkCoolDownOption, CannonCoolDownOption, NumOfMarksOption, CannonPowerOption, CannonPowerAttenuationOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);

        GameActionTypes.CannonMarkPlacementAction = new("cannon.placement", this, isPlacementAction: true);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static private readonly FloatConfiguration MarkCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cannon.markCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CannonCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration CannonPowerOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonPower", (5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration CannonPowerAttenuationOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonPowerAttenuation", (0.25f, 2f, 0.125f), 0.75f, FloatConfigurationDecorator.Ratio);
    static private readonly IntegerConfiguration NumOfMarksOption = NebulaAPI.Configurations.Configuration("options.role.cannon.numOfMarks", (1, 10), 3);

    static public readonly Cannon MyRole = new();
    static private readonly GameStatsEntry StatsFire = NebulaAPI.CreateStatsEntry("stats.cannon.fire", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsBlow = NebulaAPI.CreateStatsEntry("stats.cannon.players", GameStatsCategory.Roles, MyRole);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class CannonMark : NebulaSyncStandardObject, IGameOperator
    {
        public const string MyTag = "CannonMark";
        private static SpriteLoader markSprite = SpriteLoader.FromResource("Nebula.Resources.CannonMark.png", 100f);
        public CannonMark(Vector2 pos) : base(pos, ZOption.Back, true, markSprite.GetSprite())
        {
        }

        static CannonMark()
        {
            RegisterInstantiater(MyTag, (args) => new CannonMark(new Vector2(args[0], args[1])));
        }
    }

    private static readonly SpriteLoader mapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButton.png", 100f);
    private static readonly SpriteLoader mapButtonInnerSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButtonInner.png", 100f);
    public class CannonMapLayer : MonoBehaviour
    {
        public Cannon.Ability MyCannon = null!;
        static CannonMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<CannonMapLayer>();
        public void AddMark(NebulaSyncStandardObject obj, Action onFired)
        {
            var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
            var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
            var localPos = VanillaAsset.ConvertToMinimapPos(obj.Position, center, scale);

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("CannonButton", transform, localPos.AsVector3(-0.5f));
            renderer.sprite = mapButtonSprite.GetSprite();
            var inner = UnityHelper.CreateObject<SpriteRenderer>("Inner", renderer.transform, new(0f, 0f, -0.1f));
            inner.gameObject.AddComponent<MinimapScaler>();
            inner.sprite = mapButtonInnerSprite.GetSprite();

            var button = renderer.gameObject.SetUpButton(true, renderer);
            var collider = renderer.gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.22f;

            button.OnClick.AddListener(() =>
            {
                MyCannon.FireCannon(obj.Position);
                NebulaSyncObject.LocalDestroy(obj.ObjectId);
                Destroy(button.gameObject);
                onFired.Invoke();
                StatsFire.Progress();
                GameOperatorManager.Instance?.Run(new CannonFireLocalEvent());
                MapBehaviour.Instance.Close();
            });
        }
    }

    private class CannonFireLocalEvent : Virial.Events.Event
    {
        public CannonFireLocalEvent(){ }
    }
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped) {
            if (AmOwner)
            {
                var markButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "cannon.mark",
                    MarkCoolDownOption, "mark", buttonSprite,
                    _ => Marks.Count < NumOfMarksOption);
                markButton.OnClick = (button) =>
                {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.CannonMarkPlacementAction);

                    var mark = NebulaSyncObject.LocalInstantiate(CannonMark.MyTag, [
                                PlayerControl.LocalPlayer.transform.localPosition.x,
                                PlayerControl.LocalPlayer.transform.localPosition.y - 0.25f
                            ]).SyncObject! as NebulaSyncStandardObject;
                    Marks.Add(mark!);
                    if (mapLayer) mapLayer.AddMark(mark!, () => Marks.Remove(mark!));
                    markButton.StartCoolDown();
                };
                markButton.ShowUsesIcon(0, " ");
                markButton.SetAsUsurpableButton(this);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(_ => markButton.UpdateUsesIcon((NumOfMarksOption - Marks.Count).ToString()), markButton);

                var mapButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "cannon.cannon",
                    CannonCoolDownOption, "cannon", cannonButtonSprite,
                    _ => Marks.Count > 0);
                mapButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        HudManager.Instance.InitMap();
                        MapBehaviour.Instance.ShowNormalMap();
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                    });
                };
                mapButton.SetAsUsurpableButton(this);
                GameOperatorManager.Instance?.Subscribe<CannonFireLocalEvent>(_ => mapButton.StartCoolDown(), mapButton);

                acTokenCommon1 = new("cannon.common1", (-100f, false), (a, _) => a.isCleared);

                new AchievementToken<bool>("cannon.common2", false, (_, _) => cannonAchievementData.Any(data => data.Sum >= 5 && data.ImpostorButOneself >= 1));
                new AchievementToken<bool>("cannon.another1", false, (_, _) => cannonAchievementData.Any(data => data.Sum == 0));
            }
        }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MarkButton.png", 115f);
        static private Image cannonButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CannonButton.png", 115f);

        private List<NebulaSyncStandardObject> Marks = [];
        private CannonMapLayer mapLayer = null!;

        AchievementToken<(float lastFire, bool isCleared)>? acTokenCommon1 = null;

        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (!MeetingHud.Instance && ev is MapOpenNormalEvent && !IsUsurped)
            {
                if (!mapLayer)
                {
                    mapLayer = UnityHelper.CreateObject<CannonMapLayer>("CannonLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    mapLayer.MyCannon = this;
                    Marks.Do(m => mapLayer.AddMark(m, () => Marks.Remove(m)));
                    this.BindGameObject(mapLayer.gameObject);
                }
                mapLayer.gameObject.SetActive(true);
            }
            else
            {
                if (mapLayer) mapLayer.gameObject.SetActive(false);
            }
        }

        public void FireCannon(Vector2 pos)
        {
            int num = cannonAchievementData.Count;
            cannonAchievementData.Add(new());
            new StaticAchievementToken("cannon.common3");
            Cannon.FireCannon(pos, MyPlayer, num);
        }

        public void UpdateAchievementData(int num, GamePlayer player)
        {
            //Cannon自身でのみ以下の処理が走る
            if (!AmOwner) return;

            //Cannon自身が吹っ飛んでいる場合
            if (player.AmOwner)
            {
                acTokenCommon1!.Value.lastFire = NebulaGameManager.Instance!.CurrentTime;

                cannonAchievementData[num].Cannon++;
            }else if (player.IsImpostor)
            {
                cannonAchievementData[num].ImpostorButOneself++;
            }
            else
            {
                cannonAchievementData[num].NonImpostor++;
            }

            cannonAchievementData[num].Players.Add(player);
            StatsBlow.Progress();
        }

        [Local, OnlyMyPlayer]
        void OnMurderedPlayer(PlayerKillPlayerEvent ev)
        {
            if (acTokenCommon1!.Value.lastFire + 3f > NebulaGameManager.Instance!.CurrentTime) acTokenCommon1.Value.isCleared = true;
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev) => CheckChallengeAchievement();

        [Local]
        void OnGameEnd(GameEndEvent ev) => CheckChallengeAchievement();

        void CheckChallengeAchievement() { 
            foreach(var data in cannonAchievementData)
            {
                if (!data.IsCurrentPhase) continue;

                if (data.Sum >= 3)
                {
                    var players = data.Players.ForEach(NebulaGameManager.Instance!.AllPlayerInfo).ToArray();
                    int count = players.Select(p => p.PlayerState).Distinct().Count();
                    if (players.All(p => p.IsDead) && players.Length == count) new StaticAchievementToken("cannon.challenge");
                }

                data.IsCurrentPhase = false;
            }
        }

        private class CannonAchievementData {
            public int NonImpostor = 0;
            public int ImpostorButOneself = 0;
            public int Cannon = 0;
            public int Sum => NonImpostor + ImpostorButOneself + Cannon;
            public EditableBitMask<GamePlayer> Players = BitMasks.AsPlayer();
            public bool IsCurrentPhase = true;
        }

        private List<CannonAchievementData> cannonAchievementData = [];
    }
    private static void FireCannon(Vector2 pos, GamePlayer myPlayer, int num)
    {
        RpcCheckCannon.Invoke((pos, myPlayer.PlayerId, num));
    }

    private static IDividedSpriteLoader cannonArrowSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonArrow.png", 200f, 3, 3);
    private static IDividedSpriteLoader smokeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonSmoke.png", 150f, 4, 1);

    //吹っ飛ばしベクトルを計算する。
    //reductionFactorが1以下だと滑らかで自然に飛ばされる距離が減少する。1より大きいと位置関係の逆転が起こる
    private static Vector2 CalcPowerVector(Vector2 impactPos, Vector2 playerPos, float maxPower, float reductionFactor = 1f)
    {
        var dir = playerPos - impactPos;
        float mag = Mathf.Max(0f, maxPower - dir.magnitude * reductionFactor);
        return dir.normalized * mag;
    }

    //壁等の当たり判定を考慮して実際の吹き飛ばし先を決定する
    public static Vector2 SuggestMoveToPos(Vector2 playerPos, Vector2 maxVector)
    {
        var currentData = MapData.GetCurrentMapData();
        bool CanWarpTo(Vector2 pos) => currentData.CheckMapArea(pos, 0.25f);

        int length = Mathf.Max((int)(maxVector.magnitude * 4), 100);
        Vector2[] pos = new Vector2[length];
        for (int i = 0; i < length; i++) pos[i] = playerPos + maxVector * (i + 1) / length;

        for(int i = 0; i < length; i++)
        {
            var p = pos[pos.Length - 1 - i];
            if (CanWarpTo(p)) return p;
        }
        return playerPos; //すべての場所が移動不可なら元の位置から動かない
    }


    public static IEnumerator CoPlayJumpAnimation(PlayerControl player, Vector2 from, Vector2 to, float animOffset = 1.82f, float speedMul = 3f, Action? onLand = null)
    {
        player.moveable = false;
        bool isLeft = to.x < from.x;
        player.MyPhysics.FlipX = isLeft;
        player.MyPhysics.Animations.Animator.Play(player.MyPhysics.Animations.group.SpawnAnim, 0f);
        player.MyPhysics.Animations.Animator.SetTime(animOffset);
        var skinAnim = player.cosmetics.skin.skin.SpawnLeftAnim && isLeft ? player.cosmetics.skin.skin.SpawnLeftAnim : player.cosmetics.skin.skin.SpawnAnim;
        player.cosmetics.skin.animator.Play(skinAnim, 0f);
        player.cosmetics.skin.animator.SetTime(animOffset);

        //Spawnアニメーションの最終位置はずれがあるので、アニメーションに合わせてずれを補正
        var animTo = to - new Vector2(isLeft ? -0.3f : 0.3f, -0.24f);

        //移動を滑らかにする
        player.NetTransform.SetPaused(true);
        player.NetTransform.ClearPositionQueues();

        //壁を無視して飛ぶ
        
        player.Collider.enabled = false;

        IEnumerator CoAnimMovement()
        {
            yield return player.MyPhysics.WalkPlayerTo(animTo, 0.01f, speedMul, true);

            player.MyPhysics.Animations.Animator.SetSpeed(1f);
            player.cosmetics.skin.animator.SetSpeed(1f);
            while (player.MyPhysics.Animations.IsPlayingSpawnAnimation() && player.MyPhysics.Animations.Animator.Playing) yield return null;
        }

        yield return new ParallelCoroutine(new StackfullCoroutine(CoAnimMovement()), ManagedCoroutineHelper.Continue(() => !MeetingHud.Instance)).WaitAndProcessTillSomeoneFinished();
        player.MyPhysics.Animations.Animator.SetSpeed(1f);
        player.cosmetics.skin.animator.SetSpeed(1f);
        player.Collider.enabled = true;

        onLand?.Invoke();

        //Spawnアニメーションのずれを補正
        player.transform.position = to;

        player.MyPhysics.Animations.PlayIdleAnimation();
        player.cosmetics.AnimateSkinIdle();

        player.NetTransform.SetPaused(false);
        player.moveable = true;

        yield break;
    }

    static readonly RemoteProcess<(GamePlayer player, Vector2 to, byte cannonId, int num)> RpcCannon = new("Cannon", (message, _) =>
    {
        NebulaManager.Instance.StartCoroutine(CoPlayJumpAnimation(message.player.VanillaPlayer, message.player.Position, message.to).WrapToIl2Cpp());

        //Cannon自身は称号に関する情報を更新する。
        if(GamePlayer.LocalPlayer.PlayerId == message.cannonId && GamePlayer.LocalPlayer.Role is Ability cannon)
        {
            cannon.UpdateAchievementData(message.num, message.player);
        }
    });

    static readonly RemoteProcess<(Vector2 pos, byte cannonId, int num)> RpcCheckCannon = new("CheckCannon", (message, _) =>
    {
        IEnumerator CoShowIcon()
        {
            var icon = new Arrow(cannonArrowSprite.GetSprite(0), false) { TargetPos = message.pos, FixedAngle = true, IsSmallenNearPlayer = false, ShowOnlyOutside = true };
            icon.Register(icon);
            for (int i = 0; i < 9; i++)
            {
                icon.SetSprite(cannonArrowSprite.GetSprite(i));
                yield return Effects.Wait(0.1f);
            }
            yield return icon.CoWaitAndDisappear(1f);
        }
        NebulaManager.Instance.StartCoroutine(CoShowIcon().WrapToIl2Cpp());

        IEnumerator CoShowSmoke()
        {
            var smoke = UnityHelper.CreateObject<SpriteRenderer>("SmokeRenderer", null, message.pos.AsVector3(-1f), LayerExpansion.GetObjectsLayer());
            smoke.sprite = smokeSprite.GetSprite(0);

            for (int i = 0; i < 4; i++)
            {
                smoke.sprite = smokeSprite.GetSprite(i);
                smoke.color = new(1f, 1f, 1f, 1f - i * 0.15f);
                yield return Effects.Wait(0.12f);
            }
            UnityEngine.Object.Destroy(smoke.gameObject);
        }
        NebulaManager.Instance.StartCoroutine(CoShowSmoke().WrapToIl2Cpp());

        var myPlayer = PlayerControl.LocalPlayer;
        var modPlayer = myPlayer.GetModInfo()!;

        //空気砲の対象外
        if (modPlayer.IsDead || myPlayer.inVent || myPlayer.inMovingPlat || myPlayer.onLadder || modPlayer.IsDived || modPlayer.IsBlown || modPlayer.IsTeleporting) return;

        var powerVec = CalcPowerVector(message.pos, modPlayer.Position, CannonPowerOption, CannonPowerAttenuationOption);
        if (powerVec.magnitude < 0.5f) return; //たいして移動しない場合は何もしない。(計算の量を減らすための早期リターン)
        var moveTo = SuggestMoveToPos(modPlayer.TruePosition, powerVec) - (UnityEngine.Vector2)(modPlayer.TruePosition - modPlayer.Position);
        if ((moveTo - (UnityEngine.Vector2)modPlayer.Position).magnitude < 0.5f) return; //たいして移動しない場合は何もしない。

        //ミニゲームを開いている場合は閉じてから考える
        bool isPlayerTask = false;
        if (Minigame.Instance)
        {
            if (Minigame.Instance.MyNormTask) isPlayerTask = true;
            Minigame.Instance.ForceClose();
        }
        if (myPlayer.CanMove)
        {
            if (myPlayer.transform.position.Distance(moveTo) > 10f && isPlayerTask) new StaticAchievementToken("cannon.another2");
                RpcCannon.Invoke((modPlayer, moveTo, message.cannonId, message.num));
        }
    });
}