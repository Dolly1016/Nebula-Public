using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using Nebula.Roles.Impostor;
using UnityEngine;
using Nebula.Map;
using Sentry;
using MS.Internal.Xml.XPath;
using Virial.Events.Game;
using Nebula.Roles.Abilities;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
internal class Climber : DefinedSingleAbilityRoleTemplate<Climber.Ability>, DefinedRole
{
    private const float HookSpeed = 19f;
    private const float HookBackSuccessSpeed = 12f;
    private const float HookBackFailedSpeed = 19f;

    private Climber() : base("climber", new(86, 171, 246), RoleCategory.CrewmateRole, Crewmate.MyTeam, [GustCooldownOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        GameActionTypes.HookshotAction = new("hookshot", this, isPhysicalAction: true);
    }

    static private FloatConfiguration GustCooldownOption = NebulaAPI.Configurations.Configuration("options.role.climber.gustCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    static public readonly Climber MyRole = new();
    static private readonly GameStatsEntry StatsGust = NebulaAPI.CreateStatsEntry("stats.climber.gust", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsDistance = NebulaAPI.CreateStatsEntry("stats.climber.distance", GameStatsCategory.Roles, MyRole);

    static private readonly Image hookSprite = SpriteLoader.FromResource("Nebula.Resources.GustHook.png", 100f);
    static private readonly Image ropeSprite = SpriteLoader.FromResource("Nebula.Resources.GustRope.png", 100f);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.GustHookButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var gustButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, GustCooldownOption, "gust", buttonSprite)
                    .SetAsUsurpableButton(this)
                    .SetAsMouseClickButton();
                gustButton.OnClick = (button) =>
                {
                    if (SearchPointAndSendJump())
                    {
                        StatsGust.Progress();
                    }
                    button.StartCoolDown();
                };
                new GuideLineAbility(MyPlayer, () => !gustButton.IsInCooldown && MyPlayer.CanMove && !MyPlayer.IsDead).Register(new FunctionalLifespan(() => !this.IsDeadObject && !gustButton.IsBroken));
            }
        }

        internal Hookshot? MyHookshot = null;
        internal void SetHookshot(Hookshot? hookshot) => MyHookshot = hookshot;
        bool IPlayerAbility.BlockUsingUtility => MyHookshot != null && !MyHookshot.IsDeadObject && !MyHookshot.IsDisappearing;


    }

    static public bool SearchPointAndSendJump()
    {
        ContactFilter2D filter = new()
        {
            useLayerMask = true,
            layerMask = Constants.ShipAndAllObjectsMask
        };

        var localPlayer = GamePlayer.LocalPlayer!;

        var dir = Vector2.right.Rotate(localPlayer.Unbox().MouseAngle.RadToDeg());
        UnityEngine.Vector2 truePos = localPlayer.TruePosition;
        var mapData = MapData.GetCurrentMapData();
        float searched = 0f;
        Il2CppSystem.Collections.Generic.List<RaycastHit2D> hits = new();

        Vector2? lastHitPos = null;
        while (searched < 30f)
        {
            int num = Physics2D.Raycast(truePos + dir * searched, dir, filter, hits, 30f - searched);

            if (num == 0) break;

            for (int i = 0; i < num; i++)
            {
                var h = hits[i];
                if (h.collider.isTrigger) continue;

                bool overlapAxeIgnoreArea = Impostor.Raider.OverlapAxeIgnoreArea(h.point);
                bool inMap = mapData.CheckMapArea(h.point - dir * 0.05f, 0f);
                if (!overlapAxeIgnoreArea && inMap)
                {
                    //条件に適う位置を発見したとき

                    var to = Impostor.Cannon.SuggestMoveToPos(localPlayer.TruePosition, h.point - localPlayer.VanillaPlayer.GetTruePosition()) - localPlayer.VanillaPlayer.Collider.offset;
                    float delay = localPlayer.TruePosition.Distance(h.point) / HookSpeed;
                    
                    RpcHook.Invoke((localPlayer, h.point, to, delay, true));
                    return true;
                }
                else
                {
                    if (!overlapAxeIgnoreArea) lastHitPos ??= h.point - localPlayer.VanillaPlayer.Collider.offset;

                    //条件にそぐわない位置を発見したとき
                    searched += h.distance + 0.15f;
                    break;
                }
            }
        }

        if (lastHitPos.HasValue)
        {
            float delay = localPlayer.TruePosition.Distance(lastHitPos.Value) / HookSpeed;
            RpcHook.Invoke((localPlayer, lastHitPos.Value, Vector2.zero, delay, false));
        }
        else
        {
            RpcHook.Invoke((localPlayer, localPlayer.Position.ToUnityVector() + dir * 30f, Vector2.zero, 30f / HookSpeed, false));
        }
        NebulaGameManager.Instance?.RpcDoGameAction(localPlayer, localPlayer.Position, GameActionTypes.HookshotAction);

        return false;
    }
    

    static private RemoteProcess<(GamePlayer player, Vector2 hookTo, Vector2 playerTo, float delay, bool jump)> RpcHook = new("gustJumpAction", (message, _) =>
    {
        var hookshot = new Hookshot(message.player, message.hookTo);
        hookshot.Register(hookshot);

        if (message.player.AmOwner)
        {
            NebulaAsset.PlaySE(NebulaAudioClip.Climber1, volume: 1f);
            if(message.player.TryGetAbility<Ability>(out var ability)) ability.SetHookshot(hookshot);
        }

        NebulaManager.Instance.StartDelayAction(message.delay, () =>
        {
            if (message.jump)
            {
                //称号獲得
                if (message.player.AmOwner)
                {
                    int d = (int)(message.player.Position.Distance(message.playerTo) + 0.5f);
                    StatsDistance.Progress(d);
                    if (d >= 5) new StaticAchievementToken("climber.common1");
                    GameOperatorManager.Instance?.Subscribe<PlayerDieEvent>(ev => {
                        if (ev.Player.AmOwner) new StaticAchievementToken("climber.another1");
                    }, FunctionalLifespan.GetTimeLifespan(8f));

                    var myPos = message.player.Position;
                    var killers = GamePlayer.AllPlayers.Where(p => !p.IsDead && !p.AmOwner && (p.IsImpostor || p.IsMadmate || p.Role?.Role == Neutral.Jackal.MyRole)).Count(p => p.Position.Distance(myPos) < 5f);
                    if (killers >= 2) new AchievementToken<bool>("climber.challenge", true, (_, _) => !GamePlayer.LocalPlayer!.IsDead);
                }

                if (message.player.AmOwner) NebulaAsset.PlaySE(NebulaAudioClip.Climber2, volume: 1f);
                NebulaManager.Instance.StartCoroutine(Impostor.Cannon.CoPlayJumpAnimation(message.player.VanillaPlayer, message.player.Position, message.playerTo, 1.9f, 4.9f, () => hookshot.MarkDespawn(HookBackSuccessSpeed)).WrapToIl2Cpp());
            }
            else
            {
                hookshot.MarkDespawn(HookBackFailedSpeed);
            }
        });
    });

    internal class Hookshot : SimpleLifespan, IGameOperator, IBindPlayer
    {
        GamePlayer IBindPlayer.MyPlayer => player;
        private SpriteRenderer ropeRenderer, hookRenderer;
        private GamePlayer player;
        private Vector2 target;
        private Vector2 begin;
        private float p, pMax; //初期状態からの進行具合
        private float length;

        public bool IsDisappearing { get; private set; } = false;
        public bool Reached => !(p < pMax);
        public float Speed = HookSpeed;
        public void MarkDespawn(float speed)
        {
            Speed = speed;
            IsDisappearing = true;
        }

        public Hookshot(GamePlayer player, Vector2 target)
        {
            this.player = player;
            this.target = target;
            this.begin = player.Position;
            this.pMax = this.target.Distance(this.begin);

            ropeRenderer = UnityHelper.CreateObject<SpriteRenderer>("GustRope", player.VanillaPlayer.cosmetics.transform, Vector3.zero, LayerExpansion.GetObjectsLayer());
            ropeRenderer.sprite = ropeSprite.GetSprite();
            ropeRenderer.tileMode = SpriteTileMode.Continuous;
            ropeRenderer.drawMode = SpriteDrawMode.Tiled;
            ropeRenderer.size = new(0.5f, 0.17f);
            ropeRenderer.enabled = false;

            var lossyScale = player.VanillaPlayer.cosmetics.transform.lossyScale.x;
            ropeRenderer.transform.localScale = new(1f / lossyScale, 1f / lossyScale, 1f);

            hookRenderer = UnityHelper.CreateObject<SpriteRenderer>("Hook", ropeRenderer.transform, new(0f,0f,-0.1f));
            hookRenderer.sprite = hookSprite.GetSprite();
            hookRenderer.transform.localEulerAngles = new(0f, 0f, 180f);
            hookRenderer.enabled = false;
        }

        void OnLateUpdate(GameLateUpdateEvent ev)
        {
            if (!IsDisappearing && !Reached)
            {
                p += Speed * Time.deltaTime;
                if (p > pMax) p = pMax;
            }
            if (IsDisappearing && !IsDeadObject)
            {
                length -= Speed * Time.deltaTime;
                if (length < 0)
                {
                    this.Release();
                    return;
                }
            }

            Vector2 playerEdge;
            {
                var cosmetics = player.VanillaPlayer.cosmetics;
                var hat = cosmetics.hat;
                var node = hat.SpriteSyncNode;
                var noBounceHatPos = node.Parent.GetLocalPosition(1, false) + (cosmetics.FlipX ? node.flipOffset : node.normalOffset);
                playerEdge = (Vector2)cosmetics.transform.TransformPoint(noBounceHatPos) - new Vector2(0f, 0.15f);
            }

            float correctedP;
            Vector2 targetGoal;
            if (IsDisappearing)
            {
                targetGoal = playerEdge;
                var distance = playerEdge.Distance(target);
                if(distance > 0f) correctedP = length / distance; else correctedP = 0f;
            }
            else
            {
                targetGoal = begin;
                correctedP = p;
                if (pMax > 0f) correctedP /= pMax; else correctedP = 0f;
            }
            Vector2 targetEdge = target * correctedP + targetGoal * (1f - correctedP);
            Vector2 diff = playerEdge - targetEdge;
            Vector2 center = (targetEdge + playerEdge) * 0.5f;

            ropeRenderer.enabled = true;
            ropeRenderer.transform.position = center;
            ropeRenderer.transform.SetLocalZ(0f);
            ropeRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x).RadToDeg());
            length = diff.magnitude;
            ropeRenderer.size = new(length, 0.17f);

            hookRenderer.enabled = true;
            hookRenderer.transform.localPosition = new(-length * 0.5f, 0f, -0.01f);
            hookRenderer.flipY = targetEdge.x < playerEdge.x;
        }

        void IGameOperator.OnReleased()
        {
            if (ropeRenderer) GameObject.Destroy(ropeRenderer.gameObject);
            if(hookRenderer) GameObject.Destroy(hookRenderer.gameObject);
        }
    }
}

