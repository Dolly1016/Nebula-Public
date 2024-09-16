using Il2CppInterop.Runtime.Injection;
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
public class Cannon : DefinedRoleTemplate, DefinedRole
{
    public Cannon() : base("cannon", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [MarkCoolDownOption, CannonCoolDownOption, NumOfMarksOption, CannonPowerOption, CannonPowerAttenuationOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration MarkCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cannon.markCooldown", (0f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration CannonCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration CannonPowerOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonPower", (5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration CannonPowerAttenuationOption = NebulaAPI.Configurations.Configuration("options.role.cannon.cannonPowerAttenuation", (0.25f, 2f, 0.125f), 0.75f, FloatConfigurationDecorator.Ratio);
    static private IntegerConfiguration NumOfMarksOption = NebulaAPI.Configurations.Configuration("options.role.cannon.numOfMarks", (1, 10), 3);

    static public Cannon MyRole = new Cannon();

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class CannonMark : NebulaSyncStandardObject, IGameOperator
    {
        public static string MyTag = "CannonMark";
        private static SpriteLoader markSprite = SpriteLoader.FromResource("Nebula.Resources.CannonMark.png", 100f);
        public CannonMark(Vector2 pos) : base(pos, ZOption.Back, true, markSprite.GetSprite())
        {
        }

        static CannonMark()
        {
            RegisterInstantiater(MyTag, (args) => new CannonMark(new Vector2(args[0], args[1])));
        }
    }

    private static SpriteLoader mapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButton.png", 100f);
    private static SpriteLoader mapButtonInnerSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButtonInner.png", 100f);
    public class CannonMapLayer : MonoBehaviour
    {
        public Cannon.Instance MyCannon = null;
        static CannonMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<CannonMapLayer>();
        public void AddMark(NebulaSyncStandardObject obj, Action onFired)
        {
            var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
            var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
            var localPos = VanillaAsset.ConvertToMinimapPos(obj.Position, center, scale);

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("CannonButton", transform, localPos.AsVector3(-0.5f));
            renderer.sprite = mapButtonSprite.GetSprite();
            var inner = UnityHelper.CreateObject<SpriteRenderer>("Inner", renderer.transform, new(0f, 0f, -0.1f));
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
                GameOperatorManager.Instance?.Run(new CannonFireLocalEvent());
                MapBehaviour.Instance.Close();
            });
        }
    }

    private class CannonFireLocalEvent : Virial.Events.Event
    {
        public CannonFireLocalEvent(){ }
    }
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MarkButton.png", 115f);
        static private Image cannonButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CannonButton.png", 115f);

        private List<NebulaSyncStandardObject> Marks = new();
        private CannonMapLayer mapLayer = null!;

        AchievementToken<(float lastFire, bool isCleared)>? acTokenCommon1 = null;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var markButtom = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "cannon.mark");
                markButtom.SetSprite(buttonSprite.GetSprite());
                markButtom.Availability = (button) => MyPlayer.CanMove && Marks.Count < NumOfMarksOption;
                markButtom.Visibility = (button) => !MyPlayer.IsDead;
                markButtom.OnClick = (button) =>
                {
                    var mark = Bind(NebulaSyncObject.LocalInstantiate(CannonMark.MyTag, [
                                PlayerControl.LocalPlayer.transform.localPosition.x,
                                PlayerControl.LocalPlayer.transform.localPosition.y - 0.25f
                            ]).SyncObject) as NebulaSyncStandardObject;
                    Marks.Add(mark!);
                    if (mapLayer) mapLayer.AddMark(mark!, () => Marks.Remove(mark!));
                    markButtom.StartCoolDown();

                };
                markButtom.CoolDownTimer = Bind(new Timer(0f, MarkCoolDownOption).SetAsAbilityCoolDown().Start());
                markButtom.SetLabel("mark");
                var icon = markButtom.ShowUsesIcon(0);
                GameOperatorManager.Instance?.Register<GameUpdateEvent>(_ => icon.text = (NumOfMarksOption - Marks.Count).ToString(), markButtom);

                var mapButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility, "cannon.cannon");
                mapButton.SetSprite(cannonButtonSprite.GetSprite());
                mapButton.Availability = (button) => Marks.Count > 0;
                mapButton.Visibility = (button) => !MyPlayer.IsDead;
                mapButton.OnClick = (button) =>
                {
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        HudManager.Instance.InitMap();
                        MapBehaviour.Instance.ShowNormalMap();
                        MapBehaviour.Instance.taskOverlay.gameObject.SetActive(false);
                    });
                };
                mapButton.SetLabel("cannon");
                mapButton.CoolDownTimer = Bind(new Timer(0f, CannonCoolDownOption).SetAsAbilityCoolDown().Start());
                GameOperatorManager.Instance?.Register<CannonFireLocalEvent>(_ => mapButton.StartCoolDown(), mapButton);

                acTokenCommon1 = new("cannon.common1", (-100f, false), (a, _) => a.isCleared);

                new AchievementToken<bool>("cannon.common2", false, (_, _) => cannonAchievementData.Any(data => data.Sum >= 5 && data.ImpostorButOneself >= 1));
                new AchievementToken<bool>("cannon.another1", false, (_, _) => cannonAchievementData.Any(data => data.Sum == 0));
            }
        }

        [Local]
        void OnOpenMap(AbstractMapOpenEvent ev)
        {
            if (!MeetingHud.Instance && ev is MapOpenNormalEvent)
            {
                if (!mapLayer)
                {
                    mapLayer = UnityHelper.CreateObject<CannonMapLayer>("CannonLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                    mapLayer.MyCannon = this;
                    Marks.Do(m => mapLayer.AddMark(m, () => Marks.Remove(m)));
                    this.Bind(mapLayer.gameObject);
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
                    var players = data.Players.ForEach(NebulaGameManager.Instance!.AllPlayerInfo()).ToArray();
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

        private List<CannonAchievementData> cannonAchievementData = new();
    }
    private static void FireCannon(Vector2 pos, GamePlayer myPlayer, int num)
    {
        RpcCheckCannon.Invoke((pos, myPlayer.PlayerId, num));
    }

    private static IDividedSpriteLoader cannonArrowSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonArrow.png", 200f, 3, 3);
    private static IDividedSpriteLoader smokeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonSmoke.png", 150f, 4, 1);
    private static IDividedSpriteLoader smokeTraceSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonSmokeTrace.png", 100f, 7, 1);

    //吹っ飛ばしベクトルを計算する。
    //reductionFactorが1以下だと滑らかで自然に飛ばされる距離が減少する。1より大きいと位置関係の逆転が起こる
    private static Vector2 CalcPowerVector(Vector2 impactPos, Vector2 playerPos, float maxPower, float reductionFactor = 1f)
    {
        var dir = playerPos - impactPos;
        float mag = Mathf.Max(0f, maxPower - dir.magnitude * reductionFactor);
        return dir.normalized * mag;
    }

    //壁等の当たり判定を考慮して実際の吹き飛ばし先を決定する
    private static Vector2 SuggestMoveToPos(Vector2 playerPos, Vector2 maxVector)
    {
        var currentData = MapData.GetCurrentMapData();
        bool CanWarpTo(Vector2 pos) => currentData.CheckMapArea(pos, 0.25f);

        int length = Mathf.Max((int)(maxVector.magnitude * 4), 100);
        Vector2[] pos = new Vector2[length];
        for (int i = 0; i < length; i++) pos[i] = playerPos + maxVector * (i + 1) / length;

        var moveTo = pos.Select(pos => (pos, CanWarpTo(pos))).LastOrDefault(p => p.Item2);
        if (moveTo.Item2) return moveTo.pos;
        return playerPos; //すべての場所が移動不可なら元の位置から動かない
    }


    private static IEnumerator CoPlayJumpAnimation(PlayerControl player, Vector2 from, Vector2 to)
    {
        player.moveable = false;
        bool isLeft = to.x < from.x;
        player.MyPhysics.FlipX = isLeft;
        player.MyPhysics.Animations.Animator.Play(player.MyPhysics.Animations.group.SpawnAnim, 0f);
        player.MyPhysics.Animations.Animator.SetTime(1.82f);
        var skinAnim = player.cosmetics.skin.skin.SpawnLeftAnim && isLeft ? player.cosmetics.skin.skin.SpawnLeftAnim : player.cosmetics.skin.skin.SpawnAnim;
        player.cosmetics.skin.animator.Play(skinAnim, 0f);
        player.cosmetics.skin.animator.SetTime(1.82f);

        //Spawnアニメーションの最終位置はずれがあるので、アニメーションに合わせてずれを補正
        var animTo = to - new Vector2(isLeft ? -0.3f : 0.3f, -0.24f);

        //移動を滑らかにする
        player.NetTransform.SetPaused(true);
        player.NetTransform.ClearPositionQueues();

        //壁を無視して飛ぶ
        
        player.Collider.enabled = false;

        IEnumerator CoAnimMovement()
        {
            yield return player.MyPhysics.WalkPlayerTo(animTo, 0.01f, 3f, true);

            player.MyPhysics.Animations.Animator.SetSpeed(1f);
            player.cosmetics.skin.animator.SetSpeed(1f);
            while (player.MyPhysics.Animations.IsPlayingSpawnAnimation() && player.MyPhysics.Animations.Animator.Playing) yield return null;
        }

        yield return new ParallelCoroutine(new StackfullCoroutine(CoAnimMovement()), ManagedCoroutineHelper.Continue(() => !MeetingHud.Instance)).WaitAndProcessTillSomeoneFinished();
        player.MyPhysics.Animations.Animator.SetSpeed(1f);
        player.cosmetics.skin.animator.SetSpeed(1f);
        player.Collider.enabled = true;

        //Spawnアニメーションのずれを補正
        player.transform.position = to;

        player.MyPhysics.Animations.PlayIdleAnimation();
        player.cosmetics.AnimateSkinIdle();

        player.NetTransform.SetPaused(false);
        player.moveable = true;

        yield break;
    }

    static RemoteProcess<(GamePlayer player, Vector2 to, byte cannonId, int num)> RpcCannon = new("Cannon", (message, _) =>
    {
        NebulaManager.Instance.StartCoroutine(CoPlayJumpAnimation(message.player.VanillaPlayer, message.player.Position, message.to).WrapToIl2Cpp());

        //Cannon自身は称号に関する情報を更新する。
        if(NebulaGameManager.Instance?.LocalPlayerInfo.PlayerId == message.cannonId && NebulaGameManager.Instance.LocalPlayerInfo.Role is Instance cannon)
        {
            cannon.UpdateAchievementData(message.num, message.player);
        }
    });

    static RemoteProcess<(Vector2 pos, byte cannonId, int num)> RpcCheckCannon = new("CheckCannon", (message, _) =>
    {
        IEnumerator CoShowIcon()
        {
            var icon = new Arrow(cannonArrowSprite.GetSprite(0), false) { TargetPos = message.pos, FixedAngle = true, IsSmallenNearPlayer = false, ShowOnlyOutside = true };
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
        if (modPlayer.IsDead || myPlayer.inVent || myPlayer.inMovingPlat || myPlayer.onLadder || modPlayer.IsDived || modPlayer.IsBlown) return;

        var powerVec = CalcPowerVector(message.pos, modPlayer.Position, CannonPowerOption, CannonPowerAttenuationOption);
        if (powerVec.magnitude < 0.5f) return; //たいして移動しない場合は何もしない。(計算の量を減らすための早期リターン)
        var moveTo = SuggestMoveToPos(modPlayer.TruePosition, powerVec) - (modPlayer.TruePosition - modPlayer.Position);
        if ((moveTo - modPlayer.Position).magnitude < 0.5f) return; //たいして移動しない場合は何もしない。

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