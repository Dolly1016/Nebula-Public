using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Bloody : ConfigurableStandardModifier
{
    static public Bloody MyRole = new Bloody();
    public override string LocalizedName => "bloody";
    public override string CodeName => "BLD";
    public override Color RoleColor => new Color(180f / 255f, 0f / 255f, 0f / 255f);

    private NebulaConfiguration CurseDurationOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();
        CurseDurationOption = new NebulaConfiguration(RoleConfig, "curseDuration", null, 2.5f, 30f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
    }
    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;
        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (AmOwner || (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) text += " †".Color(MyRole.RoleColor);
        }

        void IGamePlayerEntity.OnMurdered(GamePlayer murder)
        {
            if (AmOwner && !murder.AmOwner)
            {
                PlayerModInfo.RpcAttrModulator.Invoke((murder.PlayerId, new AttributeModulator(PlayerAttributes.CurseOfBloody, MyRole.CurseDurationOption.GetFloat(), false, 1)));
                new StaticAchievementToken("bloody.common1");
                acTokenChallenge = new("bloody.challenge",(false,true),(val,_)=>val.cleared);
            }
        }

        void IGameEntity.OnMeetingEnd()
        {
            if (acTokenChallenge?.Value.triggered ?? false)
                acTokenChallenge.Value.triggered = false;
        }

        void IGameEntity.OnPlayerExiled(GamePlayer exiled)
        {
            if (AmOwner)
            {

                if (acTokenChallenge?.Value.triggered ?? false)
                    acTokenChallenge.Value.cleared = exiled.PlayerId == (MyPlayer.MyKiller?.PlayerId ?? 255);
            }
        }


    }
}

