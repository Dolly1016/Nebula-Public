using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Effacer : ConfigurableStandardRole, HasCitation, DefinedRole
{
    static public Effacer MyRole = null;// new Effacer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    string DefinedAssignable.LocalizedName => "effacer";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EffaceCoolDownOption = null!;
    private NebulaConfiguration EffaceDurationOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagSNR);

        EffaceCoolDownOption = new NebulaConfiguration(RoleConfig, "effaceCoolDown", null, 10f, 60f, 2.5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        EffaceDurationOption = new NebulaConfiguration(RoleConfig, "effaceDuration", null, 5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : Impostor.Instance, RuntimeRole
    {
        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EffaceButton.png", 115f);
        public override AbstractRole Role => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }

        AchievementToken<EditableBitMask<GamePlayer>>? achChallengeToken = null;
        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                //全プレイヤーが、　インポスターでない　あるいは　自分自身　あるいは　生存かつ条件達成済み　→　自身のぞくインポスターが全員生存 & 条件達成済み
                achChallengeToken = new("effacer.challenge", BitMasks.AsPlayer(0), (val, _) =>
                    NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.ImpostorGameEnd &&
                    (NebulaGameManager.Instance?.AllPlayerInfo().All(p => !(p as GamePlayer).IsImpostor || p.AmOwner || (!p.IsDead && val.Test(p))) ?? false)
                );
                Bind(achChallengeToken);
                

                var effaceTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p) && (p.Unbox()?.VisibilityLevel ?? 2) == 0));

                var effaceButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                effaceButton.SetSprite(buttonSprite.GetSprite());
                effaceButton.Availability = (button) => effaceTracker.CurrentTarget != null && MyPlayer.CanMove;
                effaceButton.Visibility = (button) => !MyPlayer.IsDead;
                effaceButton.OnClick = (button) => {
                    effaceTracker.CurrentTarget!.GainAttribute(PlayerAttributes.InvisibleElseImpostor, MyRole.EffaceDurationOption.GetFloat(), false, 0);
                    effaceButton.StartCoolDown();

                    new StaticAchievementToken("effacer.common1");
                    if(effaceTracker.CurrentTarget!.IsImpostor) new StaticAchievementToken("effacer.common2");

                    achChallengeToken.Value.Add(effaceTracker.CurrentTarget);
                };
                effaceButton.CoolDownTimer = Bind(new Timer(MyRole.EffaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                effaceButton.SetLabel("efface");
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if (!ev.Murderer.AmOwner && ev.Murderer.IsImpostor && ev.Dead.HasAttribute(PlayerAttributes.InvisibleElseImpostor))
            {
                new StaticAchievementToken("effacer.common3");
                if (achChallengeToken != null) achChallengeToken.Value.Add(ev.Murderer);
            }
        }

        [Local, OnlyMyPlayer]
        void OnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (ev.Murderer.HasAttribute(PlayerAttributes.InvisibleElseImpostor) && (ev.Dead.Role.Role == Sheriff.MyRole || ev.Dead.Role.Role == Neutral.Jackal.MyRole || ev.Dead.Role.Role == Neutral.Avenger.MyRole))
            {
                new StaticAchievementToken("effacer.common4");
            }
        }
    }
}
