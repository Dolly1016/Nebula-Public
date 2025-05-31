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

    Citation? HasCitation.Citation => Citations.SuperNewRoles;

    static private readonly FloatConfiguration EffaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceCoolDown", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EffaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceDuration", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => false;
    static public readonly Effacer MyRole = new();
    static private readonly GameStatsEntry StatsEfface = NebulaAPI.CreateStatsEntry("stats.effacer.efface", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility 
    { 
        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EffaceButton.png", 115f);
      
        AchievementToken<EditableBitMask<GamePlayer>>? achChallengeToken = null;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) :base(player, isUsurped)
        {
            if (AmOwner)
            {
                //全プレイヤーが、　インポスターでない　あるいは　自分自身　あるいは　生存かつ条件達成済み　→　自身のぞくインポスターが全員生存 & 条件達成済み
                achChallengeToken = new("effacer.challenge", BitMasks.AsPlayer(0), (val, _) =>
                    NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnd.ImpostorWin &&
                    MyPlayer.Role.Role == MyRole && (NebulaGameManager.Instance?.AllPlayerInfo.All(p => !(p as GamePlayer).IsImpostor || p.AmOwner || (!p.IsDead && val.Test(p))) ?? false)
                );
                

                var effaceTracker = ObjectTrackers.ForPlayer(null, MyPlayer, p => ObjectTrackers.StandardPredicate(p) && (p.Unbox()?.VisibilityLevel ?? 2) == 0).Register(this);

                var effaceButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    EffaceCoolDownOption, "efface", buttonSprite, _ => effaceTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                effaceButton.OnClick = (button) => {
                    effaceTracker.CurrentTarget!.GainAttribute(PlayerAttributes.InvisibleElseImpostor, EffaceDurationOption, false, 0);
                    effaceButton.StartCoolDown();

                    new StaticAchievementToken("effacer.common1");
                    StatsEfface.Progress();
                    if (effaceTracker.CurrentTarget!.IsImpostor) new StaticAchievementToken("effacer.common2");

                    achChallengeToken.Value.Add(effaceTracker.CurrentTarget);
                };
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
