using Nebula.Behavior;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Impostor;

internal class Snatcher : DefinedSingleAbilityRoleTemplate<Snatcher.Ability>, DefinedRole
{
    private Snatcher() : base("snatcher", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [
        new GroupConfiguration("options.role.snatcher.group.snatch", [SnatchMethodOption, SnatchCoolDownOption, ObviousGuessFailureOption, KillCooldownRewindAfterUsurpOption], GroupConfigurationColor.ImpostorRed),
        new GroupConfiguration("options.role.snatcher.group.clock", [ClockCoolDownOption,ClockDurationOption, ClockRatioOption], GroupConfigurationColor.ImpostorRed),
    ])
    {
    }

    static private readonly ValueConfiguration<int> SnatchMethodOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.snatchMethod", [
        "options.role.snatcher.snatchMethod.guess",
        "options.role.snatcher.snatchMethod.interaction"
        ], 0);
    static private readonly FloatConfiguration SnatchCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.snatchCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second, () => SnatchMethodOption.GetValue() == 1);
    static private readonly BoolConfiguration ObviousGuessFailureOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.obviousGuessFailure", false, () => SnatchMethodOption.GetValue() == 0);
    static private readonly FloatConfiguration KillCooldownRewindAfterUsurpOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.killCooldownRewindAfterUsurp", (0f, 1f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration ClockCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.clockCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration ClockRatioOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.clockRatio", (1f, 5f, 0.25f), 1.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration ClockDurationOption = NebulaAPI.Configurations.Configuration("options.role.snatcher.clockDuration", (2.5f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static public void RewindKillCooldown() => NebulaAPI.CurrentGame!.KillButtonLikeHandler.KillButtonLike.Do(killButton => killButton.StartCooldown(KillCooldownRewindAfterUsurpOption));
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.Get(0,0) == 1, arguments.Get(1,0) == 1, Roles.GetRole(arguments.Get(2, -1)), arguments.Skip(3).ToArray());
    bool DefinedRole.IsJackalizable => true;
    static public readonly Snatcher MyRole = new();
    static private readonly GameStatsEntry StatsSnatch = NebulaAPI.CreateStatsEntry("stats.snatcher.snatch", GameStatsCategory.Roles, MyRole);

    public override string? GetDisplayAbilityName(Ability ability)
    {
        if(ability.UsurpedRole != null)
            return Language.Translate("role.snatcher.prefix") + " " + ability.UsurpedRole.GetDisplayName(ability.UsurpedAbility!) ?? ability.UsurpedRole.DisplayName ?? null;
        return null;
    }


    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SnatchButton.png", 115f);
        public DefinedRole? UsurpedRole { get; private set; } = null;
        private bool HasTried = false;
        public IUsurpableAbility? UsurpedAbility { get; private set; } = null;
        IEnumerable<IPlayerAbility> IPlayerAbility.SubAbilities => UsurpedAbility == null ? [] : [UsurpedAbility, .. UsurpedAbility.SubAbilities];
        static private readonly RoleRPC.Definition UpdateHasTried = RoleRPC.Get<Ability>("snatcher.hasTried", (ability, num, calledByMe) => ability.HasTried = num == 1, a => !a.HasTried);
        
        private void OnSnatching(GamePlayer target, bool isMatched = true)
        {
            new StaticAchievementToken("snatcher.common1");
            if (isMatched)
            {
                NebulaAchievementManager.RpcClearAchievement.Invoke(("snatcher.another1", target!));
                new AchievementToken<bool>("snatcher.common2", false, (_, _) => target.IsDead && !MyPlayer.IsDead && !NebulaGameManager.Instance!.EndState!.Winners.Test(target) && NebulaGameManager.Instance!.EndState!.Winners.Test(target));
                if (target.IsImpostor && MyPlayer.IsImpostor) new AchievementToken<bool>("snatcher.challenge", false, (_, _) => target.IsDead && !MyPlayer.IsDead && NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer));
            }
        }

        public Ability(GamePlayer player, bool isUsurped, bool hasTried, DefinedRole? usurpedRole, int[] args) : base(player, isUsurped)
        {
            HasTried = hasTried;
            UsurpedRole = usurpedRole;
            if (UsurpedRole != null)
            {
                UsurpedAbility = GetUsurpedAbility(UsurpedRole, MyPlayer, args);
            }
            if (AmOwner && SnatchMethodOption.GetValue() == 1)
            {
                var playerTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate).Register(this);

                var snatchButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "snatcher.snatch",
                    SnatchCoolDownOption, "snatch", buttonSprite,
                    _ => playerTracker.CurrentTarget != null, _ => !HasTried).SetAsUsurpableButton(this);
                snatchButton.OnClick = (button) =>
                {
                    var target = playerTracker.CurrentTarget;
                    if (target == null) return;

                    snatchButton.StartCoolDown();
                    OnSnatching(playerTracker.CurrentTarget!);
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        var args = target.Role.UsurpedAbilityArguments;
                        RpcUsurp.Invoke((MyPlayer, target, target.Role.Role.Id, args ?? [], false));
                        StatsSnatch.Progress();
                        NebulaAsset.PlaySE(NebulaAudioClip.SnatcherSuccess);

                        //エフェクトの再生
                        var text = PlayerControl.LocalPlayer.cosmetics.nameText;
                        text.StartCoroutine(AnimationEffects.CoPlayRoleNameEffect(text.transform.parent, new(0f, 0.185f, -0.1f), GamePlayer.LocalPlayer!.Role.Role.UnityColor, text.gameObject.layer, 1f / 0.7f).WrapToIl2Cpp());
                    });
                    UpdateHasTried.RpcSync(MyPlayer, 1);
                };
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (!HasTried && SnatchMethodOption.GetValue() == 0)
            {
                NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>()?.RegisterMeetingAction(new(MeetingPlayerButtonManager.Icons.AsLoader(6),
            state =>
            {
                var p = state.MyPlayer;
                MetaScreen lastWindow = null!;
                lastWindow = Complex.MeetingRoleSelectWindow.OpenRoleSelectWindow(r => r.IsSpawnable, " ", r =>
                {
                    if (PlayerControl.LocalPlayer.Data.IsDead) return;
                    if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;


                    var isMatched = p?.Role.ExternalRecognitionRole == r;
                    UpdateHasTried.RpcSync(MyPlayer, 1);

                    if (isMatched || !ObviousGuessFailureOption)
                    {
                        RpcUsurp.Invoke((MyPlayer, p!, r.Id, isMatched ? p.Role.UsurpedAbilityArguments ?? [] : [], !isMatched));
                        OnSnatching(p!, isMatched);
                        NebulaAsset.PlaySE(NebulaAudioClip.SnatcherSuccess);
                        //エフェクトの再生
                        if (MeetingHud.Instance.playerStates.Find(pva => pva.TargetPlayerId == MyPlayer.PlayerId, out var pva))
                        {
                            pva.NameText.StartCoroutine(AnimationEffects.CoPlayRoleNameEffect(pva.NameText.transform.parent, new(0.3384f, -0.13f, -0.1f), GamePlayer.LocalPlayer!.Role.Role.UnityColor, LayerExpansion.GetUILayer(), 1.2f).WrapToIl2Cpp());
                        }
                    }
                    else
                    {
                        NebulaAsset.PlaySE(NebulaAudioClip.SnatcherFailed);
                    }

                    if (lastWindow) lastWindow.CloseScreen();
                    lastWindow = null!;
                });
            },
            p => !p.MyPlayer.IsDead && !p.MyPlayer.AmOwner && !HasTried && !PlayerControl.LocalPlayer.Data.IsDead
            ));
            }
        }



        int[] IPlayerAbility.AbilityArguments => UsurpedAbility == null ? [IsUsurped ? 1 : 0, HasTried ? 1 : 0] : [IsUsurped ? 1 : 0, HasTried ? 1 : 0, UsurpedRole?.Id ?? -1, .. UsurpedAbility.AbilityArguments];
        bool IPlayerAbility.HideKillButton => UsurpedAbility?.HideKillButton ?? false;

        [OnlyMyPlayer]
        void OnUsurped(PlayerUsurpedAbilityEvent ev)
        {
            UsurpedAbility?.Usurp();
        }


        static private readonly RemoteProcess<(GamePlayer snatcher, GamePlayer target, int roleId, int[] arguments, bool asUsurpedAbility)> RpcUsurp = new("Usurp", (message, _) =>
        {
            if (message.snatcher.AllAbilities.Find(a => a is Ability sa && sa.UsurpedAbility == null, out var found))
            {
                Ability snatcher = (found as Ability)!;
                snatcher.UsurpedRole = Roles.GetRole(message.roleId);
                if (snatcher.UsurpedRole != null)
                {
                    snatcher.UsurpedAbility = GetUsurpedAbility(snatcher.UsurpedRole, message.snatcher, message.arguments);
                    if (message.asUsurpedAbility) snatcher.UsurpedAbility.Usurp();
                }
            }

            if (!message.asUsurpedAbility)
            {
                message.target.Role.Usurp();
                GameOperatorManager.Instance?.Run(new PlayerUsurpedAbilityEvent(message.target));
            }
        });

        static private IUsurpableAbility GetUsurpedAbility(DefinedRole role, GamePlayer snatcher, int[] arguments)
        {
            return (role.GetUsurpedAbility(snatcher, arguments) ?? new ClockAbility(snatcher, arguments.Get(0, 0) == 1, ClockCoolDownOption, ClockRatioOption, ClockDurationOption)).Register(snatcher.Role);
        }
    }

    public class ClockAbility : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private static readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SnatcherClockButton.png", 115f);
        public ClockAbility(GamePlayer player, bool isUsurped, float clockCooldown,float clockStrength, float clockDuration) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var clockButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "snatcher.clock",
                    clockCooldown, clockDuration, "clock", buttonSprite).SetAsUsurpableButton(this);
                clockButton.OnEffectStart = button => player.GainAttribute(PlayerAttributes.CooldownSpeed, clockDuration, clockStrength, false, 100);
                clockButton.OnEffectEnd = button => button.StartCoolDown();
            }
        }

        int[] IPlayerAbility.AbilityArguments => [IsUsurped ? 1 : 0];
    }

}