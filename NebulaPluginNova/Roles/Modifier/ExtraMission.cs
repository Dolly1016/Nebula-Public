using Virial.Events.Game;
using Virial.Events.Player;
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
    public class Instance : ModifierInstance, IBindPlayer
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
        public override string? GetExtraTaskText() => Language.Translate("role.extraMission.taskText").Replace("%PLAYER%", target?.Name ?? "Undefined").Color((target?.IsDead ?? false) ? Color.green : MyRole.RoleColor);
        
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

