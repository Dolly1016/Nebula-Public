using Nebula.Game.Statistics;
using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Modifier;


public class Obsessional : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private Obsessional():base("obsessional", "OBS", new(177, 102, 156), [CanWinEvenIfObsessionalDieOption,CanWinEvenIfObsessionalTargetDieOption, ObsessionalSuicideWhenObsessionalTargetDieOption, ImpostorObsessionalObsessesOverOption]) { }

    static private BoolConfiguration CanWinEvenIfObsessionalTargetDieOption = NebulaAPI.Configurations.Configuration("options.role.obsessional.canWinEvenIfObsessionalTargetDie", false);
    static private BoolConfiguration CanWinEvenIfObsessionalDieOption = NebulaAPI.Configurations.Configuration("options.role.obsessional.canWinEvenIfObsessionalDie", true);
    static private BoolConfiguration ObsessionalSuicideWhenObsessionalTargetDieOption = NebulaAPI.Configurations.Configuration("options.role.obsessional.obsessionalSuicideWhenObsessionalTargetDie", true);
    static private ValueConfiguration<int> ImpostorObsessionalObsessesOverOption = NebulaAPI.Configurations.Configuration("options.role.obsessional.impostorObsessionalObsessesOver", [
        "options.role.obsessional.impostorObsessionalObsessesOver.default",
        "options.role.obsessional.impostorObsessionalObsessesOver.neutralOnly",
        "options.role.obsessional.impostorObsessionalObsessesOver.nonCrewmate"
        ], 0);

    static public Obsessional MyRole = new Obsessional();


    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }

        GamePlayer? obsession = null;
        public GamePlayer? Obsession => obsession;

        [Local]
        void DecorateOtherPlayerName(PlayerDecorateNameEvent ev)
        {
            if(ev.Player == obsession) ev.Name += " #".Color(MyRole.UnityColor);
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (canSeeAllInfo)
            {
                name += " $".Color(MyRole.UnityColor);
                if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                    name += $" <size=60%>({obsession?.Name ?? "ERROR" })</size>";
            }
        }


        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var cands = NebulaGameManager.Instance?.AllPlayerInfo.Where(p => !p.TryGetModifier<Obsessional.Instance>(out _))!;
                
                var limitted = cands;
                if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                {
                    switch (ImpostorObsessionalObsessesOverOption.GetValue())
                    {
                        case 1:
                             limitted = cands.Where(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole);
                            break;
                        case 2:
                            limitted = cands.Where(p => p.Role.Role.Category != Virial.Assignable.RoleCategory.CrewmateRole);
                            break;
                    }
                }
                if (limitted.Count() > 0) cands = limitted;

                obsession = cands.ToArray().Random();

                if (obsession != null) NebulaManager.Instance.StartDelayAction(5f, () => RpcSetObsessionalTarget.Invoke((MyPlayer.PlayerId, obsession.PlayerId)));
            }
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (!ObsessionalSuicideWhenObsessionalTargetDieOption) return;

            if (ev.Player.PlayerId == (obsession?.PlayerId ?? 255) && !MyPlayer.IsDead)
            {
                MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
                new StaticAchievementToken("obsessional.another1");
            }
        }

        [Local]
        void OnPlayerDead(PlayerDieOrDisconnectEvent ev)
        {
            if (!ObsessionalSuicideWhenObsessionalTargetDieOption) return;

            if (ev.Player.PlayerId == (obsession?.PlayerId ?? 255) && !MeetingHudExtension.MarkedAsExtraVictims(MyPlayer.PlayerId) && !MyPlayer.IsDead)
            {
                MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill, KillParameter.NormalKill);
                new StaticAchievementToken("obsessional.another1");
            }
        }

        //本来の勝利条件をブロックする
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= true;

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.ObsessionPhase) return;

            if (!CanWinEvenIfObsessionalDieOption && MyPlayer.IsDead) return;
            if (!CanWinEvenIfObsessionalTargetDieOption && (obsession?.IsDead ?? true)) return;

            if (obsession != null && ev.WinnersMask.Test(obsession))
            {
                ev.SetWin(true);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraObsessionalWin);
            }
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            ev.AppendText(Language.Translate("role.obsessional.taskText").Replace("%PLAYER%", obsession?.Name ?? "ERROR").Color(MyRole.UnityColor));
        }

        string? RuntimeModifier.DisplayIntroBlurb => Language.Translate("role.obsessional.blurb").Replace("%NAME%", (obsession?.Name ?? "ERROR").Color(MyRole.UnityColor));

        static RemoteProcess<(byte playerId, byte targetId)> RpcSetObsessionalTarget = new("SetObsessionalTarget",
        (message, _) =>
        {
            if (NebulaGameManager.Instance?.GetPlayer(message.playerId)?.TryGetModifier<Obsessional.Instance>(out var instance) ?? false)
                instance.obsession = NebulaGameManager.Instance.GetPlayer(message.targetId);
        });

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.Winners.Test(MyPlayer))
            {
                //勝利

                new StaticAchievementToken("obsessional.common1");

                if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota >= 5)
                    new StaticAchievementToken("agent.challenge");

                if (ev.EndState.EndCondition == NebulaGameEnd.LoversWin && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                    new StaticAchievementToken("obsessional.lover1");

                //勝者に自身と執着対象しかいない場合
                if (NebulaGameManager.Instance!.AllPlayerInfo.Where(p => ev.EndState.Winners.Test(p)).All(p => p.AmOwner || p.PlayerId == (obsession?.PlayerId ?? 255)))
                    new StaticAchievementToken("obsessional.challenge");
            }
            else
            {
                //敗北

                if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin)
                    new StaticAchievementToken("obsessional.another2");

                if (ev.EndState.Winners.Test(obsession) && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                    new StaticAchievementToken("obsessional.lover2");
            }
        }

        bool RuntimeModifier.MyCrewmateTaskIsIgnored => !(obsession?.IsTrueCrewmate ?? false);
        bool RuntimeAssignable.CanKill(Virial.Game.Player player) => player != obsession;
    }
}
