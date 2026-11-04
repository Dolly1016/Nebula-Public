using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
public class ExtraMission : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private ExtraMission() : base("extraMission", "EXM", new(222, 69, 102)) { }


    static public ExtraMission MyRole = new ExtraMission();
    static private readonly GameStatsEntry StatsBlocked = NebulaAPI.CreateStatsEntry("stats.extraMission.blocked", GameStatsCategory.Roles, MyRole, annotation: GameStatsAnnotation.From2611);
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public GamePlayer? target { get; set; } = null!;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var targets = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.AmOwner && MyPlayer.CanKill(p));
                NebulaManager.Instance.StartDelayAction(0.2f,
                    () =>
                    {
                        target = targets.ToArray().Random();
                        NebulaManager.Instance.StartDelayAction(2f, () => RpcSetExtraMissionTarget.Invoke((MyPlayer.PlayerId, target.PlayerId)));
                    });
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (inEndScene || (canSeeAllInfo && !AmOwner))
                name += $" ({(target?.Name ?? "Undefined")})".Color(MyRole.RoleColor);
            else if (AmOwner)
                name += MyRole.GetRoleIconTagSmall();
        }

        [Local]
        void DecorateOtherPlayerName(PlayerDecorateNameEvent ev)
        {
            if(target == ev.Player) ev.Color = MyRole.RoleColor;
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev)
        {
            if (ev.SetBlockedIf(!(target?.IsDead ?? true))) ev.Recorder.AddReason(ev.Player.PlayerId, ExtraMissionDetails.Blocked);
        }


        [Local]
        void OnPlayerDead(PlayerDieEvent ev)
        {
            if(AmOwner && ev.Player == target)
                new StaticAchievementToken(MyPlayer.IsDead ? "extraMission.another1" : "extraMission.common1");
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (MyPlayer.IsCrewmate && ev.Player.IsCrewmate && ev.Player == target && MeetingHudExtension.TryGetLastSwappedVotedFor(MyPlayer.PlayerId, out var votedFor) && votedFor == ev.Player.PlayerId)
            {
                new StaticAchievementToken("extraMission.common3");
            }
        }

        void OnGameEnd(GameEndEvent ev)
        {
            if (AmOwner)
            {
                if(!(target?.IsDead ?? true)) new StaticAchievementToken("extraMission.another2");

                //本来勝利していたにもかかわらず、ミッション失敗によって勝利がブロックされた (ブロック理由はブロックによって勝利を逃した場合にのみ付与される)
                if (ev.EndState.FinalStage.Detail?.Phases.Any(phase => phase.Reasons.Any(r => r.reason == ExtraMissionDetails.Blocked && r.players.Test(MyPlayer))) ?? false)
                    StatsBlocked.Progress();
            }
            if ((target?.AmOwner ?? false) && !target.IsDead)
            {
                if (target.Unbox().TryGetModifier<Instance>(out var targetMission) && (targetMission.target?.IsDead ?? false) && ev.CheckWin(target)) new StaticAchievementToken("extraMission.challenge");
                new StaticAchievementToken("extraMission.common2");
            }
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            ev.AppendText(Language.Translate("role.extraMission.taskText").Replace("%PLAYER%", target?.Name ?? "Undefined").Color((target?.IsDead ?? false) ? VColor.Green : MyRole.RoleColor));
        }
    }

    public static RemoteProcess<(byte player, byte target)> RpcSetExtraMissionTarget = new(
        "ExtraMissionTarget",
        (message, _) =>
        {
            if(NebulaGameManager.Instance?.GetPlayer(message.player)?.TryGetModifier<Instance>(out var extraMission) ?? false)
            {
                extraMission.target = NebulaGameManager.Instance!.GetPlayer(message.target)!;
            }
        }
        );
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
file static class ExtraMissionDetails
{
    static internal CommunicableTextTag Blocked = null!;

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Blocked = preprocessor.RegisterCommunicableText("end.detail.extraMission.blocked");
    }
}
