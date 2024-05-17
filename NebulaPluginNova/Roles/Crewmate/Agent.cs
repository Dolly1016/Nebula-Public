using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

public class Agent : DefinedRoleTemplate, DefinedRole
{
    static public Agent MyRole = new Agent();

    private Agent() : base("agent", new(166, 183, 144), RoleCategory.CrewmateRole, Crewmate.MyTeam, [VentConfiguration, NumOfExemptedTasksOption, NumOfExtraTasksOption, SuicideIfSomeoneElseCompletesTasksBeforeAgentOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private IntegerConfiguration NumOfExemptedTasksOption = NebulaAPI.Configurations.Configuration("role.agent.numOfExemptedTasks", (1, 8), 3);
    static private IntegerConfiguration NumOfExtraTasksOption = NebulaAPI.Configurations.Configuration("role.agent.numOfExtraTasks", (0, 8), 3);
    static private BoolConfiguration SuicideIfSomeoneElseCompletesTasksBeforeAgentOption = NebulaAPI.Configurations.Configuration("role.agent.suicideIfSomeoneElseCompletesTasksBeforeAgent", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.VentConfiguration("role.agent.vent", false, (0, 16), 3, null, -1f, (2.5f, 30f, 2.5f), 10f);

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AgentButton.png", 115f);
        bool RuntimeRole.CanUseVent => leftVent > 0;
        private int leftVent = VentConfiguration.Uses;
        private Timer ventDuration = new Timer(VentConfiguration.Duration);
        private TMPro.TextMeshPro UsesText = null!;

        GameTimer? RuntimeRole.VentDuration => ventDuration;
        public Instance(GamePlayer player, int[] argument) : base(player)
        {
            if (argument.Length >= 1) leftVent = argument[0];
        }

        int[]? RuntimeAssignable.RoleArguments => [leftVent];

        [Local]
        void OnTaskUpdated(PlayerTaskUpdateEvent ev)
        {
            if (!MyPlayer.IsDead)
            {
                int tasks = AmongUsUtil.NumOfAllTasks;
                if (ev.Player.AmOwner) return;
                if (!ev.Player.IsDead && SuicideIfSomeoneElseCompletesTasksBeforeAgentOption && ev.Player.Tasks.IsCrewmateTask && ev.Player.Tasks.TotalTasks >= tasks && ev.Player.Tasks.IsCompletedTotalTasks)
                {
                    MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Layoff);
                    new StaticAchievementToken("agent.another1");
                }
            }
        }

        [Local, OnlyMyPlayer]
        void OnEnterVent(PlayerVentEnterEvent ev)
        {
            ventDuration.Start();

            leftVent--;
            UsesText.text = leftVent.ToString();
            if (leftVent <= 0) UsesText.transform.parent.gameObject.SetActive(false);
        }


        [OnlyMyPlayer]
        void OnSetTaskLocal(PlayerTasksTrySetLocalEvent ev)
        {
            int extempts = NumOfExemptedTasksOption;
            for (int i = 0; i < extempts; i++) ev.Tasks.RemoveAt(System.Random.Shared.Next(ev.Tasks.Count));
        }

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                if (NumOfExtraTasksOption > 0)
                {
                    var taskButton = Bind(new ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));
                    taskButton.SetSprite(buttonSprite.GetSprite());
                    taskButton.Availability = (button) => MyPlayer.CanMove && MyPlayer.Tasks.IsCompletedCurrentTasks;
                    taskButton.Visibility = (button) => !MyPlayer.IsDead;
                    taskButton.OnClick = (button) =>
                    {
                        MyPlayer.Tasks.Unbox().GainExtraTasksAndRecompute(NumOfExtraTasksOption, 0, 0, false);
                    };
                    taskButton.SetLabel("agent");
                }

                Bind(new GameObjectBinding(HudManager.Instance.ImpostorVentButton.ShowUsesIcon(3, out UsesText)));
                UsesText.text = leftVent.ToString();
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.Winners.Test(MyPlayer) && MyPlayer.Tasks.TotalTasks > 0 && NumOfExemptedTasksOption <= 3)
            {
                if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota > 0 && AmongUsUtil.NumOfAllTasks >= 8)
                    new StaticAchievementToken("agent.common1");

                if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota >= 5)
                    new StaticAchievementToken("agent.challenge");
            }
        }
    }
}

