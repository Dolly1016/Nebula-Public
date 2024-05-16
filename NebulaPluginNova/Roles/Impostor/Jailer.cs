using Nebula.Roles.Abilities;
using Virial.Assignable;
using Virial.Events.Game.Minimap;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Jailer : DefinedRoleTemplate, DefinedRole
{
    static public Jailer MyRole = new Jailer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    string DefinedAssignable.LocalizedName => "jailer";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public NebulaConfiguration CanMoveWithMapWatchingOption = null!;
    public NebulaConfiguration CanIdentifyDeadBodiesOption = null!;
    public NebulaConfiguration CanIdentifyImpostorsOption = null!;
    public NebulaConfiguration InheritAbilityOnDyingOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        CanMoveWithMapWatchingOption = new NebulaConfiguration(RoleConfig, "canMoveWithMapWatching", null, false, false);
        CanIdentifyDeadBodiesOption = new NebulaConfiguration(RoleConfig, "canIdentifyDeadBodies", null, false, false);
        CanIdentifyImpostorsOption = new NebulaConfiguration(RoleConfig, "canIdentifyImpostors", null, false, false);
        InheritAbilityOnDyingOption = new NebulaConfiguration(RoleConfig, "inheritAbilityOnDying", null, false, false);
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<bool>? acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        [Local]
        void OnOpenSabotageMap(MapOpenSabotageEvent ev)
        {
            acTokenCommon ??= new("jailer.common1", false, (val, _) => val);
        }

        [OnlyMyPlayer, Local]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (acTokenCommon != null) acTokenCommon.Value = true;

            if (acTokenChallenge != null)
            {
                var pos = PlayerControl.LocalPlayer.GetTruePosition();
                Collider2D? room = null;
                foreach (var entry in ShipStatus.Instance.FastRooms)
                {
                    if (entry.value.roomArea.OverlapPoint(pos))
                    {
                        room = entry.value.roomArea;
                        break;
                    }
                }

                if (room != null && Helpers.AllDeadBodies().Any(d => d.ParentId != ev.Dead.PlayerId && room.OverlapPoint(d.TruePosition)))
                    acTokenChallenge!.Value++;
            }
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                //JailerAbilityを獲得していなければ登録
                if ((GameOperatorManager.Instance?.AllOperators.All(e => e is not JailerAbility) ?? false))
                {
                    new JailerAbility(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, MyRole.CanMoveWithMapWatchingOption).Register(this);
                }
            }
        }

        [OnlyMyPlayer]
        void InheritAbilityOnDead(PlayerDieEvent ev)
        {
            var localPlayer = Virial.NebulaAPI.CurrentGame?.LocalPlayer;

            if (localPlayer == null) return;

            //継承ジェイラーの対象で、JailerAbilityを獲得していなければ登録
            if (MyRole.InheritAbilityOnDyingOption && !localPlayer.IsDead && localPlayer.IsImpostor && (GameOperatorManager.Instance?.AllOperators.All(e => e is not JailerAbility) ?? false))
            {
                new JailerAbility(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, MyRole.CanMoveWithMapWatchingOption).Register(this);
            }

        }
    }
}
