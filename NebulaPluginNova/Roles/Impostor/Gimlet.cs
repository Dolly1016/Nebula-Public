using Il2CppSystem.Security.Cryptography;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using Virial.Text;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Impostor;

internal class Gimlet : DefinedSingleAbilityRoleTemplate<Gimlet.Ability>, DefinedRole
{
    private Gimlet() : base("gimlet", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [DrillCooldownOption, DrillSizeOption, DrillSpeedOption, DrillFrictionResistanceOption, DrillSEStrengthOption, CanKillImpostorOption])
    {
        GameActionTypes.DrillAction = new("gimlet.drill", this, isPhysicalAction: true);
    }

    
    //static private readonly BoolConfiguration SyncKillAndCleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.syncKillAndCleanCoolDown", true);
    static private readonly IRelativeCoolDownConfiguration DrillCooldownOption = NebulaAPI.Configurations.KillConfiguration("options.role.gimlet.drillCooldown", CoolDownType.Relative, (10f, 60f, 2.5f), 30f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    static private readonly FloatConfiguration DrillSizeOption = NebulaAPI.Configurations.Configuration("options.role.gimlet.drillSize", (0.5f, 2f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration DrillSpeedOption = NebulaAPI.Configurations.Configuration("options.role.gimlet.drillSpeed", (0.25f, 3f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly BoolConfiguration CanKillImpostorOption = NebulaAPI.Configurations.Configuration("options.role.gimlet.canKillImpostor", false);
    static private readonly FloatConfiguration DrillSEStrengthOption = NebulaAPI.Configurations.Configuration("options.role.gimlet.drillSeStrength", (1f, 5f, 0.5f), 2f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration DrillFrictionResistanceOption = NebulaAPI.Configurations.Configuration("options.role.gimlet.frictionResistance", (0f, 0.875f, 0.125f), 0.25f, FloatConfigurationDecorator.Ratio);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Gimlet MyRole = new();
    static private readonly GameStatsEntry StatsDrill = NebulaAPI.CreateStatsEntry("stats.gimlet.drill", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DrillButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        internal int Killstreak => killstreak;
        int killstreak = 0;
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var acTokenAnother2 = AchievementTokens.FirstFailedAchievementToken("gimlet.another2", MyPlayer, this);

                var drillButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "gimlet.drill",
                    DrillCooldownOption.CoolDown, "drill", buttonSprite).SetAsUsurpableButton(this);
                drillButton.OnClick = (button) => {
                    acTokenAnother2.Value.triggered = true;
                    killstreak = 0;
                    RpcDrill.Invoke((MyPlayer, MyPlayer.Position, MyPlayer.Unbox().MouseAngle.RadToDeg()));
                    StatsDrill.Progress();
                    NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                };
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(drillButton.GetKillButtonLike());
                drillButton.OnUpdate = _ => {
                    MyPlayer.Unbox().RequireUpdateMouseAngle();
                };
                drillButton.SetAsMouseClickButton();

                new GuideLineAbility(MyPlayer, () => !drillButton.IsInCooldown && MyPlayer.CanMove && !MyPlayer.IsDead).Register(new FunctionalLifespan(() => !this.IsDeadObject && !drillButton.IsBroken));
            }
        }

        [OnlyMyPlayer]
        void OnCheckPlayingFootstep(PlayerCheckPlayFootSoundEvent ev)
        {
            ev.PlayFootSound &= !MyPlayer.HasAttribute(PlayerAttributes.InternalInvisible);
        }

        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (ev.Dead.PlayerState == PlayerState.Drill)
            {
                new StaticAchievementToken("gimlet.common1");
                killstreak++;
                if(killstreak >= 4) new StaticAchievementToken("gimlet.common2");
            }

            if(SabotageRepairPlayers.Any(tuple => tuple.player.PlayerId == ev.Dead.PlayerId && tuple.time + 10f < (NebulaGameManager.Instance?.CurrentTime ?? 0f)))
            {
                lastSabotageDead = ev.Dead;
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(ev.EndState.EndReason == GameEndReason.Sabotage && lastSabotageDead != null && NebulaGameManager.Instance?.LastDead == lastSabotageDead && GamePlayer.AllPlayers.Count(p => p.MyKiller == MyPlayer && p.PlayerState == PlayerState.Drill) >= 4)
            {
                new StaticAchievementToken("gimlet.challenge");
            }
        }

        void OnOpenConsoleLocal(PlayerBeginMinigameByConsoleLocalEvent ev)
        {
            var task = ev.Console.FindTask(PlayerControl.LocalPlayer);
            if (task.TryCast<SabotageTask>())
            {

            }
        }

        List<(GamePlayer player, float time)> SabotageRepairPlayers = [];
        GamePlayer? lastSabotageDead = null;

        RemoteProcess<GamePlayer> RpcBeginSabotageMinigame = new("BeginSabotage", (player, _) =>
        {
            if (GamePlayer.LocalPlayer?.TryGetAbility<Ability>(out var ability) ?? false)
            {
                ability.SabotageRepairPlayers.Add((player, NebulaGameManager.Instance?.CurrentTime ?? 0f));
            }
        }, false);

        [Local]
        void OnMeetingStart(MeetingStartEvent ev) => SabotageRepairPlayers.Clear();

    }

    private const string GimletAttrTag = "nebula::gimlet::invisible";
    private const string GimletSizeAttrTag = "nebula::gimlet::size";
    private readonly static MultiImage DrillImage = DividedSpriteLoader.FromResource("Nebula.Resources.Drill.png", 100f, 3, 3);

    private static float DrillSize => 0.82f * DrillSizeOption;
    private static float DrillVisualSize => Mathf.Max(0.7f, 0.82f * DrillSizeOption);

    static readonly private RemoteProcess<(GamePlayer player, Vector2 pos, float degree)> RpcDrill = new("Drill", (message, _)=>{
        NebulaManager.Instance.StartCoroutine(CoDrill(message.player, message.pos, message.degree).WrapToIl2Cpp());
    });
    static private IEnumerator CoDrill(GamePlayer player, Vector2 pos, float degree)
    {
        bool startDrill = false;

        player.VanillaPlayer.NetTransform.SnapTo(pos);
        player.VanillaPlayer.NetTransform.SetPaused(true);
        player.VanillaPlayer.moveable = false;

        var cosmeticsLayer = player.VanillaPlayer.cosmetics;
        var playerTransform = cosmeticsLayer.transform.parent;
        var anim = player.VanillaPlayer.MyPhysics.Animations;

        Vector2 dir = Vector2.right.Rotate(degree);

        SizeModulator sizeModulator = new(Vector2.one, 10000f, false, 100, GimletSizeAttrTag, false, false);
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, sizeModulator, true));

        NebulaManager.Instance.StartCoroutine(anim.CoPlayJumpAnimation());
        yield return Effects.Wait(0.05f);
        playerTransform.localEulerAngles = new(0f, 0f, -8f.ConsiderPlayerFlip(cosmeticsLayer));
        yield return Effects.Wait(0.05f);
        var drillRenderer = UnityHelper.SimpleAnimator(player.VanillaPlayer.transform, new Vector3(0f, 0f, -0.5f), 0.083f, i => DrillImage.GetSprite(startDrill ? (i % 3) : 6));
        var effectRenderer = UnityHelper.SimpleAnimator(drillRenderer.transform, new Vector3(0f, 0f, -0.5f), 0.125f, i => DrillImage.GetSprite(startDrill ? (i % 3 + 3) : 7));
        drillRenderer.sprite = DrillImage.GetSprite(0);
        drillRenderer.transform.localScale = new Vector3(0.15f, 0.15f, 1f) * DrillVisualSize;
        drillRenderer.transform.localEulerAngles = new(0f,0f,degree - 100f);
        playerTransform.localEulerAngles = new(0f, 0f, -16f.ConsiderPlayerFlip(cosmeticsLayer));
        yield return Effects.Wait(0.05f);
        playerTransform.localEulerAngles = new(0f, 0f, -24f.ConsiderPlayerFlip(cosmeticsLayer));
        drillRenderer.transform.localScale = new Vector3(0.45f, 0.45f, 1f) * DrillVisualSize;
        drillRenderer.transform.localEulerAngles = new(0f, 0f, degree - 75f);
        sizeModulator.Size = new(0.75f, 0.75f);
        yield return Effects.Wait(0.05f);
        playerTransform.localEulerAngles = new(0f, 0f, -32f.ConsiderPlayerFlip(cosmeticsLayer));
        drillRenderer.transform.localScale = new Vector3(0.78f, 0.78f, 1f) * DrillVisualSize;
        drillRenderer.transform.localEulerAngles = new(0f, 0f, degree - 50f);
        sizeModulator.Size = new(0.5f, 0.5f);
        yield return Effects.Wait(0.05f);
        playerTransform.localEulerAngles = new(0f, 0f, -40f.ConsiderPlayerFlip(cosmeticsLayer));
        drillRenderer.transform.localScale = new Vector3(0.93f, 0.93f, 1f) * DrillVisualSize;
        drillRenderer.transform.localEulerAngles = new(0f, 0f, degree - 25f);
        sizeModulator.Size = new(0.25f, 0.25f);
        yield return Effects.Wait(0.05f);
        playerTransform.localEulerAngles = new(0f, 0f, 0f);
        drillRenderer.transform.localScale = new Vector3(1f, 1f, 1f) * DrillVisualSize;
        drillRenderer.transform.localEulerAngles = new(0f, 0f, degree);
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, new AttributeModulator(PlayerAttributes.InternalInvisible, 100f, false, 0, GimletAttrTag), true));
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, GimletSizeAttrTag));

        yield return ManagedEffects.Lerp(0.25f, p => drillRenderer.transform.localScale = Vector3.one * (1f + (1f - p) * (1f - p) * 0.25f) * DrillVisualSize);

        var drillSE = NebulaAsset.PlaySE(NebulaAudioClip.Drill, Vector2.zero, 0.95f, DrillSEStrengthOption, 1f, true);
        var drillFricSE = NebulaAsset.PlaySE(NebulaAudioClip.DrillFric, Vector2.zero, 0.95f, DrillSEStrengthOption, 1f, true);
        drillFricSE.pitch = 1.23f;
        drillSE.transform.SetParent(player.VanillaPlayer.transform);
        drillFricSE.transform.SetParent(player.VanillaPlayer.transform);
        drillSE.transform.localPosition = Vector3.zero;
        drillFricSE.transform.localPosition = Vector3.zero;
        drillFricSE.volume = 0f;
        var fricVolume = 0f;

        yield return Effects.Wait(0.05f);

        startDrill = true;
        drillRenderer.sprite = DrillImage.GetSprite(1);

        var lastPos = player.VanillaPlayer.transform.position;
        var walkTo = player.VanillaPlayer.MyPhysics.WalkPlayerTo(pos + dir * 100f, 0.01f, DrillSpeedOption * 2f).WrapToManaged();
        int count = 3;
        var localPlayer = GamePlayer.LocalPlayer!;
        var killInvoked = false;
        
        float frictionResistance = DrillFrictionResistanceOption;
        float frictionTime = 0f;

        while (walkTo.MoveNext())
        {
            count++;
            var currentPos = player.VanillaPlayer.transform.position;
            if (MeetingHud.Instance) break;
            if (count > 5)
            {
                if (currentPos.Distance(lastPos) < 0.005f) break;
                float dotProd = Vector2.Dot((currentPos - lastPos).normalized, dir);
                //1未満で擦れる音、0.6未満で終了
                if (dotProd < (1f - frictionResistance)) break;

                fricVolume -= (fricVolume - (dotProd < 0.995f ? 1f : 0f)).Delta(dotProd < 0.995f ? 2f : 3f, 0.001f);
                drillFricSE.volume = fricVolume;
                if (dotProd < 0.995f) frictionTime += Time.deltaTime;
            }

            lastPos = currentPos;


            if(!killInvoked && !player.AmOwner && !localPlayer.IsDead && (CanKillImpostorOption || player.CanKill(localPlayer)) && player.Position.Distance(localPlayer.Position) < (DrillSizeOption * 0.4f + 0.25f))
            {
                player.MurderPlayer(localPlayer, PlayerState.Drill, null, KillParameter.RemoteKill);
                killInvoked = true;
            }

            yield return null;
        }
        player.VanillaPlayer.MyPhysics.body.velocity = Vector2.zero;

        if(drillSE) GameObject.Destroy(drillSE.gameObject);
        if (drillFricSE) GameObject.Destroy(drillFricSE.gameObject);
        NebulaAsset.PlaySE(NebulaAudioClip.DrillEnd, player.Position, 0.95f, DrillSEStrengthOption, 1f);

        anim.Animator.Play(anim.group.ExitVentAnim, 1f);
        yield return Effects.Wait(0.1f);
        if (drillRenderer) GameObject.Destroy(drillRenderer.gameObject);
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, GimletAttrTag));
        while (anim.Animator.IsPlaying()) yield return null;
        anim.PlayIdleAnimation();
        player.VanillaPlayer.NetTransform.SetPaused(false);
        player.VanillaPlayer.moveable = true;

        if (player.AmOwner && player.TryGetAbility<Ability>(out var ability))
        {
            if (!MeetingHud.Instance && ability.Killstreak == 0) new StaticAchievementToken("gimlet.another1");
            if (frictionTime > 5f && ability.Killstreak >= 2) new StaticAchievementToken("gimlet.common3");
        }
    }
}