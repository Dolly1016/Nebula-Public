using Virial.Events.Game.Meeting;
using Virial.Events.Player;
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

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner);

        CurseDurationOption = new NebulaConfiguration(RoleConfig, "curseDuration", null, 2.5f, 30f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
    }
    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);
    public class Instance : ModifierInstance, IBindPlayer
    {
        public override AbstractModifier Role => MyRole;
        AchievementToken<(bool cleared, bool triggered)>? acTokenChallenge;
        public Instance(GamePlayer player) : base(player)
        {
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (AmOwner || (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)) text += " †".Color(MyRole.RoleColor);
        }

        [Local, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (!ev.Murderer.AmOwner)
            {
                PlayerModInfo.RpcAttrModulator.Invoke((ev.Murderer.PlayerId, new AttributeModulator(PlayerAttributes.CurseOfBloody, MyRole.CurseDurationOption.GetFloat(), false, 1), true));
                new StaticAchievementToken("bloody.common1");
                acTokenChallenge = new("bloody.challenge",(false,true),(val,_)=>val.cleared);
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge?.Value.triggered ?? false)
                acTokenChallenge.Value.triggered = false;
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (acTokenChallenge?.Value.triggered ?? false)
                acTokenChallenge.Value.cleared = ev.Player.PlayerId == (MyPlayer.MyKiller?.PlayerId ?? 255);
        }


    }
}

