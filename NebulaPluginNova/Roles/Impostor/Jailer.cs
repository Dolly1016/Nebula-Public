using Nebula.Behaviour;
using Nebula.Configuration;
using Nebula.Roles.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Jailer : ConfigurableStandardRole
{
    static public Jailer MyRole = new Jailer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "jailer";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

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

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<bool>? acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        void IGameEntity.OnOpenSabotageMap()
        {
            if (AmOwner)
            {
                acTokenCommon ??= new("jailer.common1", false, (val, _) => val);
            }
        }

        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
        {
            if (AmOwner)
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

                    if (room != null)
                    {
                        if (Helpers.AllDeadBodies().Any(d => d.ParentId != target.PlayerId && room.OverlapPoint(d.TruePosition)))
                        {
                            acTokenChallenge!.Value++;
                        }
                    }
                }
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                //JailerAbilityを獲得していなければ登録
                if ((GameEntityManager.Instance?.AllEntities.All(e => e is not JailerAbility) ?? false))
                {
                    new JailerAbility(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, MyRole.CanMoveWithMapWatchingOption).Register(this);
                }
            }
        }

        void IGamePlayerEntity.OnDead()
        {
            var localPlayer = Virial.NebulaAPI.CurrentGame?.LocalPlayer;

            if (localPlayer == null) return;

            //継承ジェイラーの対象で、JailerAbilityを獲得していなければ登録
            if (MyRole.InheritAbilityOnDyingOption && !localPlayer.IsDead && localPlayer.IsImpostor && (GameEntityManager.Instance?.AllEntities.All(e => e is not JailerAbility) ?? false))
            {
                new JailerAbility(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, MyRole.CanMoveWithMapWatchingOption).Register(this);
            }

        }
    }
}
