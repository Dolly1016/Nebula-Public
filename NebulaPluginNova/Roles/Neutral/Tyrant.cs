using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Roles.Neutral;

[NebulaPreprocess(PreprocessPhase.BuildAssignmentTypes)]
internal static class TyrantAssignmentSetUp
{
    static public Virial.Color Color = new(185, 137, 0);
    public static void Preprocess(NebulaPreprocessor preprocessor)
    {
        preprocessor.RegisterAssignmentType(() => Tyrant.MyRole, (lastArgs, role) => Tyrant.GenerateArgument(lastArgs, role), "tyrant", Color, (status, role) => status.HasFlag(AbilityAssignmentStatus.CanLoadToKillNeutral), () => (Tyrant.MyRole as ISpawnable).IsSpawnable && Tyrant.UseImpostorAbilityOption);
    }
}

internal class Tyrant : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = new Team("teams.tyrant", TyrantAssignmentSetUp.Color, TeamRevealType.OnlyMe, () => KillCooldown);

    private Tyrant() : base("tyrant", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [KillCoolDownOption, NumOfKillingToWinOption, UseImpostorAbilityOption])
    {
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0,0), Roles.GetRole(arguments.Get(1, -1)), arguments.Skip(2).ToArray());
    static public int[] GenerateArgument(int[] lastArgs, DefinedRole? ability) => [lastArgs.Get(0, 0), ability?.Id ?? -1, ..(ability?.DefaultAssignableArguments ?? [])];
    static private IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.tyrant.killCooldown", CoolDownType.Relative, (0f, 60f, 2.5f), 25f, (-40f, 40f, 2.5f), -5f, (0.125f, 2f, 0.125f), 1f);
    static private IntegerConfiguration NumOfKillingToWinOption = NebulaAPI.Configurations.Configuration("options.role.tyrant.numOfKillingToWin", (1, 10), 4);
    static public BoolConfiguration UseImpostorAbilityOption = NebulaAPI.Configurations.Configuration("options.role.tyrant.useImpostorAbility", false);
    static public int RequiredKillingToWin => NumOfKillingToWinOption;
    static public float KillCooldown => KillCoolDownOption.CoolDown;

    static public Tyrant MyRole = new Tyrant();
    static private GameStatsEntry StatsKill = NebulaAPI.CreateStatsEntry("stats.tyrant.kill", GameStatsCategory.Roles, MyRole);

    bool DefinedRole.IsKiller => true;
    IUsurpableAbility? DefinedRole.GetUsurpedAbility(Virial.Game.Player player, int[] arguments)
    {
        var role = Roles.GetRole(arguments.Get(0, -1));
        var ability = role?.GetUsurpedAbility(player, arguments.Skip(1).ToArray());
        if (ability != null) return new UsurpedImpostorAbility(player, role!, ability);
        return null;
    }

    string DefinedRole.GetDisplayName(IPlayerAbility ability)
    {
        if (ability is UsurpedImpostorAbility a) return a.Role.GetDisplayName(a.Ability);
        return (this as DefinedRole).DisplayName;
    }

    string DefinedRole.GetDisplayShort(IPlayerAbility ability)
    {
        if (ability is UsurpedImpostorAbility a) return a.Role.GetDisplayShort(a.Ability);
        return (this as DefinedRole).DisplayShort;
    }
    IEnumerable<DefinedRole> DefinedRole.GetGuessableAbilityRoles() => ((this as DefinedRole).IsSpawnable && !UseImpostorAbilityOption) ? [this] : [];
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private int killingTotal = 0;
        IEnumerable<DefinedAssignable> RuntimeAssignable.AssignableOnHelp => MyImpAbilityRole != null ? [MyRole, MyImpAbilityRole] : [MyRole];
        public DefinedRole? MyImpAbilityRole { get; private set; }
        public IPlayerAbility? ImpAbility { get; private set; } = null;
        private int[] StoredJackalizedArgument { get; set; }

        string RuntimeRole.DisplayIntroBlurb => (MyImpAbilityRole ?? MyRole).DisplayIntroBlurb;
        string RuntimeRole.DisplayIntroRoleName => (MyImpAbilityRole ?? MyRole).DisplayName;

        IEnumerable<IPlayerAbility?> RuntimeAssignable.MyAbilities => ImpAbility != null ? [ImpAbility, .. ImpAbility.SubAbilities] : [];
        bool RuntimeRole.CheckGuessAbility(DefinedRole abilityRole) => abilityRole == MyImpAbilityRole || abilityRole == MyRole;
        public Instance(GamePlayer player, int totalKilling, DefinedRole? jackalized, int[] jackalizedArgument) : base(player)
        {
            killingTotal = totalKilling;
            MyImpAbilityRole = jackalized;
            StoredJackalizedArgument = jackalizedArgument;
        }

        bool RuntimeRole.CanUseVent => true;
        int[] TyrantArguments => [killingTotal, MyImpAbilityRole?.Id ?? -1];
        int[]? RuntimeAssignable.RoleArguments => TyrantArguments.Concat(ImpAbility?.AbilityArguments ?? []).ToArray();
        int[]? RuntimeRole.UsurpedAbilityArguments => (ImpAbility?.AbilityArguments ?? []).Prepend(MyImpAbilityRole?.Id ?? -1).ToArray();
        
        void RuntimeRole.Usurp()
        {
            (ImpAbility as IUsurpableAbility)?.Usurp();
        }

        static private readonly IDividedSpriteLoader MeetingNameMaskImages = DividedSpriteLoader.FromResource("Nebula.Resources.TyrantAnimName.png", 100f, 1, 4);
        static private readonly IDividedSpriteLoader MeetingIconMaskImages = DividedSpriteLoader.FromResource("Nebula.Resources.TyrantAnimIcon.png", 100f, 3, 1);
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var myTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, (p) => ObjectTrackers.PlayerlikeLocalKillablePredicate(p), null, Impostor.Impostor.CanKillHidingPlayerOption);

                var killButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill,
                    KillCooldown, "kill", ModAbilityButton.LabelType.Impostor, null!,
                    (target, _) =>
                    {
                        var cancelable = GameOperatorManager.Instance?.Run(new PlayerTryVanillaKillLocalEventAbstractPlayerEvent(MyPlayer, target));
                        if (!(cancelable?.IsCanceled ?? false))
                        {
                            //キャンセルされなければキルを実行する
                            MyPlayer.MurderPlayer(target, PlayerState.Dead, EventDetail.Kill, Virial.Game.KillParameter.NormalKill);
                        }

                        //クールダウンをリセットする
                        if (cancelable?.ResetCooldown ?? false) NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    },
                    null,
                    _ => myTracker.CurrentTarget != null && !MyPlayer.IsDived,
                    _ => MyPlayer.AllowToShowKillButtonByAbilities
                    );
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(killButton.GetKillButtonLike());
            }

            ImpAbility = MyImpAbilityRole?.GetAbilityOnRole(MyPlayer, AbilityAssignmentStatus.CanLoadToKillNeutral, StoredJackalizedArgument)?.Register(this);

            //キル時の処理
            EditableBitMask<GamePlayer> killMask = BitMasks.AsPlayer();
            GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
            {
                if (ev.Player != MyPlayer) return;//自身によるキルのみ
                if (ev.Dead.PlayerState == PlayerStates.Guessed) return; //推察キルを除く
                if (ev.Dead == ev.Player) return; //自滅は除外
                
                killingTotal++;
                if (AmOwner) StatsKill.Progress();
                killMask.Add(ev.Dead);

                if ((GamePlayer.LocalPlayer?.AmHost ?? false) && killingTotal >= NumOfKillingToWinOption) NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.TyrantWin, 1 << MyPlayer.PlayerId);
            }, this);
            GameOperatorManager.Instance?.Subscribe<SetUpVotingAreaEvent>(ev =>
            {
                if (killMask.Test(ev.Player))
                {
                    UnityHelper.SimpleAnimator(ev.VoteArea.transform, new(-0.85f, -0.02f, -2f), 0.15f, i => MeetingIconMaskImages.GetSprite(i % MeetingIconMaskImages.Length));
                    UnityHelper.SimpleAnimator(ev.VoteArea.transform, new(0.45f, 0.02f, -0.2f), 0.15f, i => MeetingNameMaskImages.GetSprite(i % MeetingNameMaskImages.Length));
                    var xMark = ev.VoteArea.XMark.gameObject;
                    IEnumerator CoUpdateXMark()
                    {
                        while (true)
                        {
                            if (xMark.active) xMark.SetActive(false);
                            yield return null;
                        }
                    }
                    ev.VoteArea.StartCoroutine(CoUpdateXMark().WrapToIl2Cpp());
                }
            }, NebulaAPI.CurrentGame!);
        }

        [OnlyMyPlayer, Local]
        void OnDead(PlayerDieEvent ev)
        {
            if (killingTotal == RequiredKillingToWin - 1) new StaticAchievementToken("tyrant.another1");
        }

        float RuntimeRole.WinningOpportunity => killingTotal / RequiredKillingToWin * 0.6f + 0.4f;

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if(!MyPlayer.IsDead && ev.EndState.EndCondition == NebulaGameEnd.TyrantWin && ev.EndState.Winners.Test(MyPlayer))
            {
                HashSet<RoleTeam> teams = [];
                foreach(var p in GamePlayer.AllPlayers) if (p.Role.Role.Team != MyTeam && p.Role.WinningOpportunity > 0.75f) teams.Add(p.Role.Role.Team);
                int teamsCount = teams.Count;
                
                //Lovers単独勝利を考慮に入れる
                if (GamePlayer.AllPlayers.Any(p => p.TryGetModifier<Lover.Instance>(out var lover) && !(lover.MyLover.Get()?.IsDead ?? true) && !p.IsDead) && GamePlayer.AllPlayers.Count(p => !p.IsDead) <= 4) teamsCount++;
                
                if (teams.Count >= 3) new StaticAchievementToken("tyrant.challenge");
                
            }
        }

        string RuntimeAssignable.DisplayName => MyImpAbilityRole?.GetDisplayName(ImpAbility!) ?? (MyRole as DefinedRole).DisplayName;
        string RuntimeAssignable.DisplayColoredName => (this as RuntimeAssignable).DisplayName.Color(MyTeam.UnityColor);
        string RuntimeRole.DisplayShort => MyImpAbilityRole?.GetDisplayShort(ImpAbility!) ?? (MyRole as DefinedRole).DisplayShort;

        bool RuntimeRole.HasImpostorVision => true;
        bool RuntimeRole.IgnoreBlackout => true;
    }
}
