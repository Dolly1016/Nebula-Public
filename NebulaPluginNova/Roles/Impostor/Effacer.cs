using AmongUs.GameOptions;
using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Effacer : ConfigurableStandardRole, HasCitation
{
    static public Effacer MyRole = null!;//new Effacer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "effacer";
    public override Color RoleColor => Palette.ImpostorRed;
    Citation? HasCitation.Citaion => Citations.SuperNewRoles;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EffaceCoolDownOption = null!;
    private NebulaConfiguration EffaceDurationOption = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagSNR);

        EffaceCoolDownOption = new NebulaConfiguration(RoleConfig, "effaceCoolDown", null, 10f, 60f, 2.5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        EffaceDurationOption = new NebulaConfiguration(RoleConfig, "effaceDuration", null, 5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EffaceButton.png", 115f);
        public override AbstractRole Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        AchievementToken<BitMask<GamePlayer>>? achChallengeToken = null;
        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                //全プレイヤーが、　インポスターでない　あるいは　自分自身　あるいは　生存かつ条件達成済み　→　自身のぞくインポスターが全員生存 & 条件達成済み
                achChallengeToken = new("effacer.challenge", BitMasks.AsPlayer(0), (val, _) =>
                    NebulaGameManager.Instance?.EndState?.EndCondition == NebulaGameEnds.ImpostorGameEnd &&
                    (NebulaGameManager.Instance?.AllPlayerInfo().All(p => !(p as GamePlayer).IsImpostor || p.AmOwner || (!p.IsDead && val.Test(p))) ?? false)
                );
                Bind(achChallengeToken);
                

                var effaceTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, p => ObjectTrackers.StandardPredicate(p) && (p.GetModInfo()?.VisibilityLevel ?? 2) == 0));

                var effaceButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                effaceButton.SetSprite(buttonSprite.GetSprite());
                effaceButton.Availability = (button) => effaceTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove;
                effaceButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                effaceButton.OnClick = (button) => {
                    (effaceTracker.CurrentTarget.GetModInfo() as GamePlayer)?.GainAttribute(PlayerAttributes.InvisibleElseImpostor, MyRole.EffaceDurationOption.GetFloat(), false, 0);
                    effaceButton.StartCoolDown();

                    new StaticAchievementToken("effacer.common1");
                    if((effaceTracker.CurrentTarget.GetModInfo() as GamePlayer)?.IsImpostor ?? false) new StaticAchievementToken("effacer.common2");

                    achChallengeToken.Value.Add(effaceTracker.CurrentTarget.GetModInfo()!);
                };
                effaceButton.CoolDownTimer = Bind(new Timer(MyRole.EffaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                effaceButton.SetLabel("efface");
            }
        }

        void IGameEntity.OnPlayerMurdered(Virial.Game.Player dead, Virial.Game.Player murderer)
        {
            if (AmOwner && !murderer.AmOwner && murderer.IsImpostor && dead.HasAttribute(PlayerAttributes.InvisibleElseImpostor))
            {
                new StaticAchievementToken("effacer.common3");
                if (achChallengeToken != null) achChallengeToken.Value.Add(murderer);
            }

            if(murderer.AmOwner && murderer.HasAttribute(PlayerAttributes.InvisibleElseImpostor) && (dead.Role.Role == Sheriff.MyRole || dead.Role.Role == Neutral.Jackal.MyRole || dead.Role.Role == Neutral.Avenger.MyRole))
            {
                new StaticAchievementToken("effacer.common4");
            }
        }
    }
}
