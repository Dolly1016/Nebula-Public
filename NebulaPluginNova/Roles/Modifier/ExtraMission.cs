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

    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;
        public PlayerModInfo? target { get; set; } = null!;
        public Instance(PlayerModInfo player) : base(player)
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
            if (AmongUsUtil.IsInGameScene && (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) && !AmOwner) text += $" ({ (target?.DefaultName ?? "Undefined") })".Color(MyRole.RoleColor);
        }

        public override void DecorateOtherPlayerName(PlayerModInfo player, ref string text, ref Color color)
        {
            if (player == target) color = MyRole.RoleColor;
        }

        public override bool BlockWins(CustomEndCondition endCondition)
        {
            return !(target?.IsDead ?? true);
        }

        public override string? GetExtraTaskText() => Language.Translate("role.extraMission.taskText").Replace("%PLAYER%", target?.DefaultName ?? "Undefined").Color((target?.IsDead ?? false) ? Color.green : MyRole.RoleColor);
        
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

