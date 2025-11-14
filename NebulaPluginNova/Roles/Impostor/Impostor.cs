using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Impostor : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.impostor", new(Palette.ImpostorRed), TeamRevealType.Teams);
    
    private Impostor():base("impostor", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, MyTeam, [CanKillHidingPlayerOption]) {
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Impostor.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public BoolConfiguration CanKillHidingPlayerOption = NebulaAPI.Configurations.Configuration("options.role.impostor.canKillHidingPlayer", false);

    static public Impostor MyRole = new Impostor();
    bool ISpawnable.IsSpawnable => true;
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
        }
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class ImpostorGameRule : AbstractModule<IGameModeStandard>, IGameOperator
{
    static ImpostorGameRule() => DIManager.Instance.RegisterModule(() => new ImpostorGameRule());

    public ImpostorGameRule() => this.RegisterPermanently();
    void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.Player.IsImpostor && ev.GameEnd == NebulaGameEnd.ImpostorWin);  
    
    void DecoratePlayerColor(PlayerDecorateNameEvent ev)
    {
        if (ev.Player.IsImpostor && (GamePlayer.LocalPlayer?.IsImpostor ?? false)) ev.Color = new(Palette.ImpostorRed);
    }

    [OnlyHost]
    void CheckSabotageWin(GameUpdateEvent ev)
    {
        if (ShipStatus.Instance != null)
        {
            var status = ShipStatus.Instance;
            if (status.Systems != null)
            {
                ISystemType? systemType = status.Systems.ContainsKey(SystemTypes.LifeSupp) ? status.Systems[SystemTypes.LifeSupp] : null;
                if (systemType != null)
                {
                    LifeSuppSystemType lifeSuppSystemType = systemType.TryCast<LifeSuppSystemType>()!;
                    if (lifeSuppSystemType != null && lifeSuppSystemType.Countdown < 0f)
                    {
                        lifeSuppSystemType.Countdown = 10000f;
                        NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Sabotage);
                    }
                }

                foreach (ISystemType systemType2 in ShipStatus.Instance.Systems.Values)
                {
                    ICriticalSabotage? criticalSabotage = systemType2.TryCast<ICriticalSabotage>();
                    if (criticalSabotage != null && criticalSabotage.Countdown < 0f)
                    {
                        criticalSabotage.ClearSabotage();
                        NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Sabotage);
                    }
                }
            }
        }
    }

    [OnlyHost]
    void CheckKillWin(GameUpdateEvent ev)
    {
        //他陣営が生き残っていれば勝利しない
        if (GameOperatorManager.Instance?.Run(new KillerTeamCallback(NebulaTeams.ImpostorTeam)).RemainingOtherTeam ?? false) return;

        int impostors = 0;
        int totalAlive = 0;

        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            if (p.IsDead) continue;
            totalAlive++;

            //相方生存Loversではないインポスターのみカウントに入れる
            if (p.Role.Role.Team == Impostor.MyTeam && (!p.TryGetModifier<Lover.Instance>(out var lover) || lover.IsAloneLover)) impostors++;
        }

        if (impostors * 2 >= totalAlive) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.ImpostorWin, GameEndReason.Situation);
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class ImpostorBasicRuleOperator : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static ImpostorBasicRuleOperator() => DIManager.Instance.RegisterModule(() => new ImpostorBasicRuleOperator());

    private ImpostorBasicRuleOperator() => this.RegisterPermanently();

    [OnlyLocalPlayer]
    void OnSetRole(PlayerRoleSetEvent ev)
    {
        if (GeneralConfigurations.ImpostorsRadioOption && ev.Role.Role.Category == RoleCategory.ImpostorRole)
        {
            ModSingleton<NoSVCRoom>.Instance?.RegisterRadioChannel(Language.Translate("voiceChat.info.impostorRadio"), 0, p => p.Role.Role.Category == RoleCategory.ImpostorRole, ev.Role, Palette.ImpostorRed);
        }
    }

    void OnCheckCanKill(PlayerCheckCanKillLocalEvent ev)
    {
        if (ev.Player.IsImpostor && ev.Target.IsImpostor) ev.SetAsCannotKillBasically(); 
    }

    void OnPlayerDie(PlayerDieEvent ev)
    {
        ModSingleton<IWinningOpportunity>.Instance.SetOpportunity(NebulaTeams.ImpostorTeam, KillerOpportunityHelpers.CalcTeamOpportunity(p => p.IsImpostor, p => p.IsMadmate));
    }

    void ClaimImpostorRemaining(KillerTeamCallback callback)
    {
        if (callback.ExcludedTeam != NebulaTeams.ImpostorTeam && GamePlayer.AllPlayers.Any(p => p.IsImpostor && p.IsAlive)) callback.MarkRemaining();
    }
}

internal static class KillerOpportunityHelpers { 
    static public float CalcTeamOpportunity(Predicate<GamePlayer> isKiller, Predicate<GamePlayer> isFriend,float coeff = 0.15f)
    {
        float killerScore = 0f, allPlayerScore = 0f;
        foreach (var p in GamePlayer.AllPlayers)
        {
            if (p.IsAlive)
            {
                allPlayerScore += 1f;
                if (isKiller.Invoke(p)) killerScore += 2f;
                else if (isFriend.Invoke(p)) killerScore += 1f;
            }
        }
        return 1f + (killerScore - allPlayerScore) * coeff;
    }
}
