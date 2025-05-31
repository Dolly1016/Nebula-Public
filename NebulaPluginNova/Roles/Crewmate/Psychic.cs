using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial;
using Nebula.Roles.Abilities;
using Virial.Events.Game;
using Nebula.Modules.GUIWidget;
using Virial.Game;
using Virial.Events.Player;
using Nebula.Modules.Cosmetics;

namespace Nebula.Roles.Crewmate;

public class Psychic : DefinedSingleAbilityRoleTemplate<Psychic.Ability>, DefinedRole
{
    private Psychic() : base("psychic", new(96, 206, 137), RoleCategory.CrewmateRole, Crewmate.MyTeam, [SearchCooldownOption, SearchDurationOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Psychic.png");
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    static private readonly FloatConfiguration SearchCooldownOption = NebulaAPI.Configurations.Configuration("options.role.psychic.searchCooldown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration SearchDurationOption = NebulaAPI.Configurations.Configuration("options.role.psychic.searchDuration", (float[])([1f,2f,3f,4f,5f,7.5f,10f,15f,20f]), 5f, FloatConfigurationDecorator.Second);


    static public readonly Psychic MyRole = new();
    static private readonly GameStatsEntry StatsSearching = NebulaAPI.CreateStatsEntry("stats.psychic.searching", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsMessages = NebulaAPI.CreateStatsEntry("stats.psychic.messages", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IGameOperator, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                var searchButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    SearchCooldownOption, SearchDurationOption, "search", buttonSprite);
                searchButton.OnEffectEnd = (button) => button.StartCoolDown();
                searchButton.OnEffectStart = (button) => StatsSearching.Progress();

                //死体を指す矢印を表示する
                var ability = new DeadbodyArrowAbility().Register(this);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => ability.ShowArrow = searchButton.IsInEffect && !MyPlayer.IsDead, this);
            }
        }

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.SearchButton.png", 115f);

        GamePlayer? lastReported = null;

        [Local]
        [OnlyMyPlayer]
        void OnReportedDead(PlayerDieEvent ev)
        {
            if (lastReported == ev.Player && (ev.Player.PlayerState == PlayerState.Guessed || ev.Player.PlayerState == PlayerState.Exiled)) new StaticAchievementToken("psychic.challenge");
        }

        void OnMeetingEnd(MeetingEndEvent ev) => lastReported = null;

        [Local]
        void OnReportDeadBody(ReportDeadBodyEvent ev)
        {
            //Psychic自身が通報した死体であるとき
            if (ev.Reporter.AmOwner && ev.Reported != null)
            {
                StatsMessages.Progress();

                List<(string tag, string message)> cand = new();

                //死亡時間(5秒単位)
                {
                    float elapsedTime = (NebulaGameManager.Instance!.CurrentTime - ev.Reported!.DeathTime!.Value);
                    if (elapsedTime > 20) lastReported = ev.Reported;
                    int aboutTime = (int)(elapsedTime + 2.5f);
                    aboutTime -= aboutTime % 5;

                    if (aboutTime > 0) cand.Add(("elapsedTime", Language.Translate("options.role.psychic.message.elapsedTime").Replace("%SEC%", aboutTime.ToString())));
                }
                //キラーの特徴
                if(ev.Reported.MyKiller != null && ev.Reported.MyKiller != ev.Reported)
                {
                    cand.Add(("killersColor", Language.Translate("options.role.psychic.message.killersColor").Replace("%COLOR%", Language.Translate(DynamicPalette.IsLightColor(Palette.PlayerColors[ev.Reported.MyKiller.PlayerId]) ? "options.role.psychic.message.inner.lightColor" : "options.role.psychic.message.inner.darkColor"))));
                    cand.Add(("killersRole", Language.Translate("options.role.psychic.message.killersRole").Replace("%ROLE%", ev.Reported.MyKiller.Role.DisplayColoredName)));
                    if (ev.Reported.MyKiller.IsDead) cand.Add(("killerIsDead", Language.Translate("options.role.psychic.message.killerIsDead")));
                }
                //自身の特徴
                cand.Add(("myRole",Language.Translate("options.role.psychic.message.myRole").Replace("%ROLE%", ev.Reported.Role.DisplayColoredName)));
                //特殊な死因
                if(ev.Reported.PlayerState != PlayerState.Dead) cand.Add(("myState", Language.Translate("options.role.psychic.message.myState").Replace("%STATE%", ev.Reported.PlayerState.Text)));

                (string tag, string rawText) = cand.Random();
                NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new TranslateTextComponent("options.role.psychic.message.header")),
                    new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent(ev.Reported.Unbox().ColoredDefaultName + "<br>" + rawText)))
                    , MeetingOverlayHolder.IconsSprite[3], MyRole.RoleColor);

                new StaticAchievementToken("psychic.common1");
                new StaticAchievementToken("psychic.common2." + tag);
            }
        }
    }
}


