using AmongUs.Data;
using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Patches;
using Nebula.Roles.Abilities;
using Nebula.Roles.Neutral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using static UnityEngine.UI.GridLayoutGroup;

namespace Nebula.Roles.Impostor;

internal class Viper : DefinedSingleAbilityRoleTemplate<Viper.Ability>, HasCitation, DefinedRole
{
    private Viper() : base("viper", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [AcidCooldownOption, DissolveDurationOption, AcidLeavesBoneOption, CanReportBoneOption])
    {
    }
    Citation? HasCitation.Citation => Citations.AmongUs;

    static private IRelativeCoolDownConfiguration AcidCooldownOption = NebulaAPI.Configurations.KillConfiguration("options.role.viper.acidCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 30f, (-40f, 40f, 2.5f), +5f, (0.125f, 2f, 0.125f), 1.25f);
    static private FloatConfiguration DissolveDurationOption = NebulaAPI.Configurations.Configuration("options.role.viper.dissolveDuration", (5f, 120f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration AcidLeavesBoneOption = NebulaAPI.Configurations.Configuration("options.role.viper.acidLeavesBone", true);
    static private BoolConfiguration CanReportBoneOption = NebulaAPI.Configurations.Configuration("options.role.viper.canReportBone", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public Viper MyRole = new Viper();

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        bool IPlayerAbility.HideKillButton => true;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var predicate = ObjectTrackers.PlayerlikeLocalKillablePredicate;
                var killButton = NebulaAPI.Modules.PlayerlikeKillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill, null,
                    AcidCooldownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "acid", Virial.Components.ModAbilityButton.LabelType.Impostor,
                    new WrapSpriteLoader(()=> AmongUsUtil.GetRolePrefab<ViperRole>()!.killSprite),
                    (player, button) => {
                        MyPlayer.MurderPlayer(player, PlayerState.Dissolved, null, KillParameter.NormalKill | KillParameter.WithViperDeadBody);
                        button.StartCoolDown();
                    },
                    p => predicate(p));
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());

                HookCommon1Achievement();
                HookChallengeAchievement();
            }
        }

        void OnDeadBodyGenerated(DeadBodyInstantiateEvent ev)
        {
            if (ev.Player != null && ev.Killer == MyPlayer && ev.DeadState == PlayerState.Dissolved)
            {
                if (ev.DeadBody.VanillaDeadBody.gameObject.TryGetComponent<ModViperDeadBody>(out var deadBody))
                {
                    deadBody.SetUp(DissolveDurationOption, AcidLeavesBoneOption, !AcidLeavesBoneOption || CanReportBoneOption);

                    if(AmOwner) HookCommon2Achievement(ev.DeadBody);
                }
            }
        }

        void HookCommon1Achievement()
        {
            bool poisoned = false;
            GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
            {
                if (!ev.Murderer.AmOwner) return;
                if (ev.Dead.PlayerState == PlayerState.Dissolved)
                    poisoned = true;
                else if (!poisoned && !ev.Dead.AmOwner)
                    new StaticAchievementToken("viper.common2");

            }, this);
        }

        void HookCommon2Achievement(Virial.Game.DeadBody deadBody)
        {
            var pos = deadBody.Position;
            if ((!AcidLeavesBoneOption || !CanReportBoneOption) && (NebulaAchievementManager.GetAchievement("viper.common1", out var ach) && !ach.IsCleared))
            {
                float currentTime = NebulaGameManager.Instance!.CurrentTime;
                float maxTime = currentTime + DissolveDurationOption;
                bool got = false;
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(_ => {
                    if (NebulaGameManager.Instance!.CurrentTime < maxTime) return;
                    if (GamePlayer.AllPlayers.Where(p => !p.AmOwner && !p.IsDead).Any(p => p.Position.Distance(pos) < 2f))
                    {
                        new StaticAchievementToken(ach.Id);
                        got = true;
                    }
                }, new FunctionalLifespan(() => !MeetingHud.Instance && !got));
            }
        }

        [OnlyMyPlayer, Local]
        void HookCommon3Achievement(PlayerTryToChangeRoleEvent ev)
        {
            if (ev.NextRole == Sidekick.MyRole)
            {
                new StaticAchievementToken("combination.2.viper.sidekick.common3");
            }
        }

        void HookChallengeAchievement()
        {
            bool reported = false;
            GameOperatorManager.Instance?.Subscribe<ReportDeadBodyEvent>(ev =>
            {
                if (ev.Reported?.MyKiller == MyPlayer) reported = true;
            }, this);
            GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
            {
                if (!reported && !MyPlayer.IsDead && ev.EndState.Winners.Test(MyPlayer)) new StaticAchievementToken("viper.challenge");
            }, this);
        }
    }
}

public class  ModViperDeadBody : MonoBehaviour 
{
    static ModViperDeadBody() => ClassInjector.RegisterTypeInIl2Cpp<ModViperDeadBody>();
    private ViperDeadBody viperDeadBody;
    void Awake()
    {
        this.viperDeadBody = this.gameObject.GetComponent<ViperDeadBody>();
    }

    private float dissolveTimer = 1f, maxDissolveTimer = 1f;
    private bool shouldLeftBone = false;
    private bool canReportBone = true;
    private bool isActive = false;
    private int currentStage = 0;
    public bool IsUnknownDeadBody => currentStage == 2;
    void FixedUpdate()
    {
        if (!isActive) return;

        this.dissolveTimer -= Time.fixedDeltaTime;
        if (this.dissolveTimer < 0f && !shouldLeftBone)
        {
            GameObject.Destroy(this.gameObject);
        }
        else
        {
            float num = dissolveTimer / maxDissolveTimer;
            int nextStage;
            if (shouldLeftBone)
                nextStage = num > 0.5f ? 0 : num > 0.0f ? 1 : 2;
            else
                nextStage = num > 0.8f ? 0 : num > 0.3f ? 1 : 2;
            if (nextStage == currentStage) return;

            if (nextStage == 1)
            {
                currentStage = 1;
                viperDeadBody.spriteAnim.Play(viperDeadBody.dissolveAnims[0], 1f);
                return;
            }
            else if (nextStage == 2)
            {
                currentStage = 2;
                viperDeadBody.spriteAnim.Play(viperDeadBody.dissolveAnims[1], 1f);
                if (!canReportBone)
                {
                    this.viperDeadBody.myCollider.enabled = false;
                    GameObject.Destroy(this.viperDeadBody);

                    GameOperatorManager.Instance?.SubscribeSingleListener<MeetingStartEvent>(_ => {
                        if (this && this.gameObject) GameObject.Destroy(this.gameObject);
                    });
                }
                else
                {
                    this.viperDeadBody.ParentId |= DissolvedDeadBodyClickPatch.DissolvedDeadBodyMask;
                }

                if (shouldLeftBone) isActive = false;
            }
        }
    }

    internal void SetUp(float maxDissolveTimer, bool shouldLeftBone, bool canReportBone)
    {
        isActive = true;
        this.maxDissolveTimer = maxDissolveTimer;
        dissolveTimer = maxDissolveTimer;
        currentStage = 0;
        this.shouldLeftBone = shouldLeftBone;
        this.canReportBone = canReportBone;
    }

    internal void PlayKillerSE()
    {
        if (Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySound(viperDeadBody.acidSplashSFX, false, 1f, null);
    }
}