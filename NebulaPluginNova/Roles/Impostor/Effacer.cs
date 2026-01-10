using Nebula.Roles.Crewmate;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Effacer : DefinedSingleAbilityRoleTemplate<Effacer.Ability>, HasCitation, DefinedRole, IAssignableDocument
{
    private Effacer() : base("effacer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [EffaceCoolDownOption, EffaceDurationOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagSNR);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Effacer.png");
    }

    Citation? HasCitation.Citation => Citations.SuperNewRoles;

    static private readonly FloatConfiguration EffaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceCoolDown", (10f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EffaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.effacer.effaceDuration", (0f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.CanLoadToMadmate;
    static public readonly Effacer MyRole = new();
    static private readonly GameStatsEntry StatsEfface = NebulaAPI.CreateStatsEntry("stats.effacer.efface", GameStatsCategory.Roles, MyRole);

    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.Allowed;

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(buttonSprite, "role.effacer.ability.efface");
    }

    static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EffaceButton.png", 115f);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility 
    { 
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
                

                var effaceTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeStandardPredicate(p));
                var effaceButton = NebulaAPI.Modules.InteractButton(this, MyPlayer, effaceTracker, new PlayerInteractParameter(RealPlayerOnly: true), Virial.Compat.VirtualKeyInput.Ability, null,
                    EffaceCoolDownOption, "efface", buttonSprite, (p, button) => {
                        p.RealPlayer.GainAttribute(PlayerAttributes.InvisibleElseImpostor, EffaceDurationOption, false, 0);
                        button.StartCoolDown();

                        new StaticAchievementToken("effacer.common1");
                        StatsEfface.Progress();
                        if (p.RealPlayer.IsImpostor) new StaticAchievementToken("effacer.common2");

                        achChallengeToken.Value.Add(p.RealPlayer);
                    }).SetAsUsurpableButton(this);
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
