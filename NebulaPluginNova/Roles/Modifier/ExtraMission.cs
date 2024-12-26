using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
public class ExtraMission : DefinedAllocatableModifierTemplate, DefinedAllocatableModifier
{
    private ExtraMission() : base("extraMission", "EXM", new(222, 69, 102)) { }


    static public ExtraMission MyRole = new ExtraMission();
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
                target = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.AmOwner && MyPlayer.CanKill(p)).ToArray().Random();
                NebulaManager.Instance.StartDelayAction(5f, () => RpcSetExtraMissionTarget.Invoke((MyPlayer.PlayerId, target.PlayerId)));
            }
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo)
        {
            if (canSeeAllInfo) name += $" ({ (target?.Name ?? "Undefined") })".Color(MyRole.UnityColor);
        }

        [Local]
        void DecorateOtherPlayerName(PlayerDecorateNameEvent ev)
        {
            if(target == ev.Player) ev.Color = new(MyRole.UnityColor);
        }

        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= !(target?.IsDead ?? true);


        [Local]
        void OnPlayerDead(PlayerDieEvent ev)
        {
            if(AmOwner && ev.Player == target)
                new StaticAchievementToken(MyPlayer.IsDead ? "extraMission.another1" : "extraMission.common1");
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (MyPlayer.IsCrewmate && ev.Player.IsCrewmate && ev.Player == target && MeetingHudExtension.LastVotedForMap.TryGetValue(MyPlayer.PlayerId, out var votedFor) && votedFor == ev.Player.PlayerId)
            {
                new StaticAchievementToken("extraMission.common3");
            }
        }

        void OnGameEnd(GameEndEvent ev)
        {
            if (AmOwner)
            {
                if(!(target?.IsDead ?? true)) new StaticAchievementToken("extraMission.another2");
            }
            if ((target?.AmOwner ?? false) && !target.IsDead)
            {
                if (target.Unbox().TryGetModifier<Instance>(out var targetMission) && (targetMission.target?.IsDead ?? false) && ev.EndState.Winners.Test(target)) new StaticAchievementToken("extraMission.challenge");
                new StaticAchievementToken("extraMission.common2");
            }
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            ev.AppendText(Language.Translate("role.extraMission.taskText").Replace("%PLAYER%", target?.Name ?? "Undefined").Color((target?.IsDead ?? false) ? Color.green : MyRole.UnityColor));
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

