using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Effacer : DefinedSingleAbilityRoleTemplate<Effacer.Ability>, HasCitation, DefinedRole
{
    private Effacer() : base("effacer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [EffaceCoolDownOption, EffaceDurationOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Effacer.png");
    }

    Citation? HasCitation.Citaion => Citations.SuperNewRoles;

    static private FloatConfiguration EffaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceCoolDown", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EffaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceDuration", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player);
    bool DefinedRole.IsJackalizable => false;
    static public Effacer MyRole = new Effacer();
    static private GameStatsEntry StatsEfface = NebulaAPI.CreateStatsEntry("stats.effacer.efface", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerAbility, IPlayerAbility 
    { 
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EffaceButton.png", 115f);
      
        AchievementToken<EditableBitMask<GamePlayer>>? achChallengeToken = null;
        public Ability(GamePlayer player) :base(player)
        {
            if (AmOwner)
            {
                //全プレイヤーが、　インポスターでない　あるいは　自分自身　あるいは　生存かつ条件達成済み　→　自身のぞくインポスターが全員生存 & 条件達成済み
                achChallengeToken = new("effacer.challenge", BitMasks.AsPlayer(0), (val, _) =>
                    NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.ImpostorGameEnd &&
                    (NebulaGameManager.Instance?.AllPlayerInfo.All(p => !(p as GamePlayer).IsImpostor || p.AmOwner || (!p.IsDead && val.Test(p))) ?? false)
                );
                Bind(achChallengeToken);
                

                var effaceTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p) && (p.Unbox()?.VisibilityLevel ?? 2) == 0));

                var effaceButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                effaceButton.SetSprite(buttonSprite.GetSprite());
                effaceButton.Availability = (button) => effaceTracker.CurrentTarget != null && MyPlayer.CanMove;
                effaceButton.Visibility = (button) => !MyPlayer.IsDead;
                effaceButton.OnClick = (button) => {
                    effaceTracker.CurrentTarget!.GainAttribute(PlayerAttributes.InvisibleElseImpostor, EffaceDurationOption, false, 0);
                    effaceButton.StartCoolDown();

                    new StaticAchievementToken("effacer.common1");
                    StatsEfface.Progress();
                    if (effaceTracker.CurrentTarget!.IsImpostor) new StaticAchievementToken("effacer.common2");

                    achChallengeToken.Value.Add(effaceTracker.CurrentTarget);
                };
                effaceButton.CoolDownTimer = Bind(new Timer(EffaceCoolDownOption).SetAsAbilityCoolDown().Start());
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
