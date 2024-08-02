using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using Nebula.Map;
using Virial.Events.Game.Meeting;
using static Nebula.Roles.Impostor.Hadar;
using Il2CppInterop.Runtime.Injection;
using Virial.Events.Game.Minimap;
using Nebula.Behaviour;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
public class Cannon : DefinedRoleTemplate, DefinedRole
{
    public Cannon() : base("cannon", new(168, 178, 36), RoleCategory.CrewmateRole, Crewmate.MyTeam, [MarkCoolDownOption, NumOfMarksOption, CannonPowerOption, CannonPowerAttenuationOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration MarkCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cannon.markCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
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
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new CannonMark(new Vector2(args[0], args[1])));
        }
    }

    private static SpriteLoader mapButtonSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButton.png", 100f);
    private static SpriteLoader mapButtonInnerSprite = SpriteLoader.FromResource("Nebula.Resources.CannonButtonInner.png", 100f);
    public class CannonMapLayer : MonoBehaviour
    {
        static CannonMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<CannonMapLayer>();
        public void AddMark(NebulaSyncStandardObject obj, Action onFired)
        {
            var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
            var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);
            var localPos = VanillaAsset.ConvertToMinimapPos(obj.Position, center, scale);

            var renderer = UnityHelper.CreateObject<SpriteRenderer>("CannonButton", transform, localPos.AsVector3(-0.5f));
            renderer.sprite = mapButtonSprite.GetSprite();
            var inner = UnityHelper.CreateObject<SpriteRenderer>("Inner", renderer.transform, new(0f,0f,-0.1f));
            inner.sprite = mapButtonInnerSprite.GetSprite();

            var button = renderer.gameObject.SetUpButton(true, renderer);
            var collider = renderer.gameObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = 0.22f;

            button.OnClick.AddListener(() => {
                FireCannon(obj.Position);
                NebulaSyncObject.LocalDestroy(obj.ObjectId);
                GameObject.Destroy(button.gameObject);
                onFired.Invoke();
            });
        }
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.MarkButton.png", 115f);

        private List<NebulaSyncStandardObject> Marks = new();
        private CannonMapLayer mapLayer = null!;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var markButtom = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability, "cannon.mark");
                markButtom.SetSprite(buttonSprite.GetSprite());
                markButtom.Availability = (button) => MyPlayer.CanMove && Marks.Count < NumOfMarksOption;
                markButtom.Visibility = (button) => !MyPlayer.IsDead;
                markButtom.OnClick = (button) => {
                    var mark = Bind(NebulaSyncObject.LocalInstantiate(CannonMark.MyTag, [
                                PlayerControl.LocalPlayer.transform.localPosition.x,
                                PlayerControl.LocalPlayer.transform.localPosition.y - 0.25f
                            ])) as NebulaSyncStandardObject;
                    Marks.Add(mark!);
                    if (mapLayer) mapLayer.AddMark(mark!, ()=>Marks.Remove(mark!));
                    markButtom.StartCoolDown();

                };
                markButtom.CoolDownTimer = Bind(new Timer(0f, MarkCoolDownOption).SetAsAbilityCoolDown().Start());
                markButtom.SetLabel("mark");
            }
        }

        [Local]
        void OnOpenNormalMap(MapOpenNormalEvent ev)
        {
            if (!mapLayer)
            {
                mapLayer = UnityHelper.CreateObject<CannonMapLayer>("CannonLayer", MapBehaviour.Instance.transform, new(0, 0, -1f));
                Marks.Do(m => mapLayer.AddMark(m, ()=>Marks.Remove(m)));
                this.Bind(mapLayer.gameObject);
            }

            mapLayer.gameObject.SetActive(!MeetingHud.Instance);
        }

        [Local]
        void OnOpenAdminMap(MapOpenAdminEvent ev)
        {
            if (mapLayer) mapLayer?.gameObject.SetActive(false);
        }
    }

    private static void FireCannon(Vector2 pos)
    {
        RpcCheckCannon.Invoke(pos);
    }

    private static IDividedSpriteLoader cannonArrowSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonArrow.png", 200f, 3, 3);
    private static IDividedSpriteLoader smokeSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonSmoke.png", 150f, 4, 1);
    private static IDividedSpriteLoader smokeTraceSprite = DividedSpriteLoader.FromResource("Nebula.Resources.CannonSmokeTrace.png", 100f, 7, 1);

    //吹っ飛ばしベクトルを計算する。
    //reductionFactorが1以下だと滑らかで自然に飛ばされる距離が減少する。1より大きいと位置関係の逆転が起こる
    private static Vector2 CalcPowerVector(Vector2 impactPos, Vector2 playerPos, float maxPower, float reductionFactor = 1f)
    {
        var dir = (playerPos - impactPos);
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
        for (int i = 0; i < length; i++) pos[i] = playerPos + maxVector * (float)(i + 1) / (float)length;

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
        var skinAnim = (player.cosmetics.skin.skin.SpawnLeftAnim && isLeft) ? player.cosmetics.skin.skin.SpawnLeftAnim : player.cosmetics.skin.skin.SpawnAnim;
        player.cosmetics.skin.animator.Play(skinAnim, 0f);
        player.cosmetics.skin.animator.SetTime(1.82f);

        //Spawnアニメーションの最終位置はずれがあるので、アニメーションに合わせてずれを補正
        var animTo = to - new Vector2(isLeft ? -0.3f : 0.3f, -0.24f);

        //壁を無視して飛ぶ
        player.Collider.enabled = false;
        yield return player.MyPhysics.WalkPlayerTo(animTo, 0.01f, 3f, true);
        player.Collider.enabled = true;

        player.MyPhysics.Animations.Animator.SetSpeed(1f);
        player.cosmetics.skin.animator.SetSpeed(1f);
        while (player.MyPhysics.Animations.IsPlayingSpawnAnimation() && player.MyPhysics.Animations.Animator.Playing) yield return null;

        //Spawnアニメーションのずれを補正
        player.transform.position = to;

        player.MyPhysics.Animations.PlayIdleAnimation();
        player.cosmetics.AnimateSkinIdle();

        player.moveable = true;

        yield break;
    }

    static RemoteProcess<(GamePlayer player, Vector2 to)> RpcCannon = new("Cannon", (message, _) => {
        NebulaManager.Instance.StartCoroutine(CoPlayJumpAnimation(message.player.VanillaPlayer, message.player.Position, message.to).WrapToIl2Cpp());
    });

    static RemoteProcess<Vector2> RpcCheckCannon = new("CheckCannon", (message, _) =>
    {
        IEnumerator CoShowIcon()
        {
            var icon = new Arrow(cannonArrowSprite.GetSprite(0), false) { TargetPos = message, FixedAngle = true, IsSmallenNearPlayer = false, ShowOnlyOutside = true };
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
            var smoke = UnityHelper.CreateObject<SpriteRenderer>("SmokeRenderer", null, message.AsVector3(-1f), LayerExpansion.GetObjectsLayer());
            smoke.sprite = smokeSprite.GetSprite(0);

            for (int i = 0; i < 4; i++)
            {
                smoke.sprite = smokeSprite.GetSprite(i);
                smoke.color = new(1f, 1f, 1f, 1f - (float)i * 0.15f);
                yield return Effects.Wait(0.12f);
            }
            GameObject.Destroy(smoke.gameObject);
        }
        NebulaManager.Instance.StartCoroutine(CoShowSmoke().WrapToIl2Cpp());

        var myPlayer = PlayerControl.LocalPlayer;
        var modPlayer = myPlayer.GetModInfo()!;

        //空気砲の対象外
        if (modPlayer.IsDead || myPlayer.inVent || myPlayer.inMovingPlat || myPlayer.onLadder || modPlayer.IsDived) return;

        var powerVec = CalcPowerVector(message, modPlayer.Position, CannonPowerOption, CannonPowerAttenuationOption);
        if (powerVec.magnitude < 0.5f) return; //たいして移動しない場合は何もしない。(計算の量を減らすための早期リターン)
        var moveTo = SuggestMoveToPos(modPlayer.TruePosition, powerVec) - (modPlayer.TruePosition - modPlayer.Position);
        if ((moveTo - modPlayer.Position).magnitude < 0.5f) return; //たいして移動しない場合は何もしない。

        //ミニゲームを開いている場合は閉じてから考える
        if (Minigame.Instance) Minigame.Instance.ForceClose();
        if (myPlayer.CanMove) RpcCannon.Invoke((modPlayer, moveTo));
    });
}