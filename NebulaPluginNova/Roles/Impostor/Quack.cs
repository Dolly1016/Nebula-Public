using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using AmongUs.GameOptions;
using Virial.Media;

namespace Nebula.Roles.Impostor;

internal class Quack : DefinedSingleAbilityRoleTemplate<Quack.Ability>, DefinedRole
{
    private Quack() : base("quack", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [])
    {
    }

    //static private readonly FloatConfiguration CleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.cleanCoolDown", (5f, 60f, 2.5f), 30f, FloatConfigurationDecorator.Second);
    //static private readonly BoolConfiguration SyncKillAndCleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.syncKillAndCleanCoolDown", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Quack MyRole = new();
    static private readonly GameStatsEntry StatsReport = NebulaAPI.CreateStatsEntry("stats.quack.report", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ImpostorVitalsButton.png", 100f);
        static private MultiImage reportButtonSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Buttons.QuackReportButton.png", 100f, 2, 1);
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var vitalButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,0f, "vital", buttonSprite)
                    .SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor).SetAsUsurpableButton(this);
                vitalButton.OnClick = (button) =>
                {
                    VitalsMinigame? vitalsMinigame = Crewmate.Doctor.OpenSpecialVitalsMinigame();
                    
                    IEnumerator CoUpdateState(VitalsPanel panel, GamePlayer player)
                    {
                        SpriteRenderer renderer = UnityHelper.CreateObject<SpriteRenderer>("Button", panel.transform, new(-0.3f, -0.2278f, -0.5f));
                        renderer.sprite = reportButtonSprite.GetSprite(0);
                        var collider = renderer.gameObject.AddComponent<BoxCollider2D>();
                        collider.size = new(0.65f, 0.65f);
                        collider.isTrigger = true;
                        PassiveButton button = renderer.gameObject.SetUpButton(true);
                        button.OnMouseOver.AddListener(() => renderer.sprite = reportButtonSprite.GetSprite(1));
                        button.OnMouseOut.AddListener(() => renderer.sprite = reportButtonSprite.GetSprite(0));
                        button.OnClick.AddListener(() =>
                        {
                            MyPlayer.VanillaPlayer.CmdReportDeadBody(player.VanillaPlayer.Data);
                            StatsReport.Progress();
                            new StaticAchievementToken("quack.common1");
                        });
                        while (true)
                        {
                            renderer.gameObject.SetActive(panel.IsDead);
                            yield return null;
                        }
                    }
                    vitalsMinigame.vitals.Do(panel =>
                    {
                        panel.StartCoroutine(CoUpdateState(panel, NebulaGameManager.Instance!.GetPlayer(panel.PlayerInfo.PlayerId)!).WrapToIl2Cpp());
                    });
                };

                //称号
                AchievementToken<int> acTokenChallenge = new("quack.challenge", 0, (num, _) => num >= 3 && NebulaGameManager.Instance!.EndState!.Winners.Test(MyPlayer));
                GameOperatorManager.Instance?.Subscribe<PlayerExiledEvent>(ev => {
                    if (MeetingHudExtension.LastReporter?.AmOwner ?? false)
                    {
                        if (ev.Player.AmOwner) new StaticAchievementToken("quack.another1");
                        acTokenChallenge.Value++;
                    }
                }, this);
            }
        }

    }
}
