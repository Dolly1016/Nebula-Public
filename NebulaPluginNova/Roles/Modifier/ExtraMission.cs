using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Modifier;

[NebulaRPCHolder]
public class ExtraMission : ConfigurableStandardModifier
{
    static public ExtraMission MyRole = new ExtraMission();
    public override string LocalizedName => "extraMission";
    public override string CodeName => "EXM";
    public override Color RoleColor => new Color(222f / 255f, 69f / 255f, 102f / 255f);

    protected override void LoadOptions()
    {
        base.LoadOptions();
    }

    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;
        public GamePlayer? target { get; set; } = null!;
        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                RpcSetExtraMissionTarget.Invoke((MyPlayer.PlayerId, NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.AmOwner).ToArray().Random().PlayerId));
            }
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (AmongUsUtil.IsInGameScene && (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) && !AmOwner) text += $" ({ (target?.Name ?? "Undefined") })".Color(MyRole.RoleColor);
        }

        public override void DecorateOtherPlayerName(GamePlayer player, ref string text, ref Color color)
        {
            if (player == target) color = MyRole.RoleColor;
        }

        public override bool BlockWins(CustomEndCondition endCondition)
        {
            return !(target?.IsDead ?? true);
        }

        void IGameEntity.OnPlayerDead(Virial.Game.Player dead)
        {
            if(AmOwner && dead == target)
            {
                new StaticAchievementToken(MyPlayer.IsDead ? "extraMission.another1" : "extraMission.common1");
            }
        }

        void IGameEntity.OnPlayerExiled(Virial.Game.Player exiled)
        {
            if (AmOwner && MyPlayer.IsCrewmate && exiled.Role.Role.Unbox().IsCrewmateRole && exiled == target && MeetingHudExtension.LastVotedForMap.TryGetValue(MyPlayer.PlayerId, out var votedFor) && votedFor == exiled.PlayerId)
            {
                new StaticAchievementToken("extraMission.common3");
            }
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner)
            {
                if(!(target?.IsDead ?? true)) new StaticAchievementToken("extraMission.another2");
            }
            if ((target?.AmOwner ?? false) && !target.IsDead)
            {
                if (target.Unbox().TryGetModifier<Instance>(out var targetMission) && (targetMission.target?.IsDead ?? false) && endState.CheckWin(target.PlayerId)) new StaticAchievementToken("extraMission.challenge");
                new StaticAchievementToken("extraMission.common2");
            }
        }
        public override string? GetExtraTaskText() => Language.Translate("role.extraMission.taskText").Replace("%PLAYER%", target?.Name ?? "Undefined").Color((target?.IsDead ?? false) ? Color.green : MyRole.RoleColor);
        
    }

    public static RemoteProcess<(byte player, byte target)> RpcSetExtraMissionTarget = new(
        "ExtraMissionTarget",
        (message, _) =>
        {
            if(NebulaGameManager.Instance?.GetModPlayerInfo(message.player)?.TryGetModifier<Instance>(out var extraMission) ?? false)
            {
                extraMission.target = NebulaGameManager.Instance!.GetModPlayerInfo(message.target)!;
            }
        }
        );
}

