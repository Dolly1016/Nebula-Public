using Nebula.Game.Statistics;
using Nebula.Roles.Modifier;
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
    private Agent() : base("agent", new(166, 183, 144), RoleCategory.CrewmateRole, Crewmate.MyTeam, [VentConfiguration, NumOfExemptedTasksOption, NumOfExtraTasksOption, SuicideIfSomeoneElseCompletesTasksBeforeAgentOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
    }

    
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private IntegerConfiguration NumOfExemptedTasksOption = NebulaAPI.Configurations.Configuration("options.role.agent.numOfExemptedTasks", (1, 8), 3);
    static private IntegerConfiguration NumOfExtraTasksOption = NebulaAPI.Configurations.Configuration("options.role.agent.numOfExtraTasks", (0, 8), 3);
    static private BoolConfiguration SuicideIfSomeoneElseCompletesTasksBeforeAgentOption = NebulaAPI.Configurations.Configuration("options.role.agent.suicideIfSomeoneElseCompletesTasksBeforeAgent", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.VentConfiguration("role.agent.vent", false, (0, 16), 3, null, -1f, (2.5f, 30f, 2.5f), 10f);

    static public Agent MyRole = new Agent();

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AgentButton.png", 115f);
        bool RuntimeRole.CanUseVent => leftVent > 0;
        private int leftVent = VentConfiguration.Uses;
        private Timer ventDuration = new Timer(VentConfiguration.Duration);
        private TMPro.TextMeshPro UsesText = null!;

        GameTimer? RuntimeRole.VentDuration => ventDuration;
        public Instance(GamePlayer player, int[] argument) : base(player)
        {
            if (argument.Length >= 1) leftVent = argument[0];
        }

        AchievementToken<(bool useVent, float lastTask, bool cleared)>? acTokenCommon2 = null;

        int[]? RuntimeAssignable.RoleArguments => [leftVent];

        [Local]
        void OnTaskUpdated(PlayerTaskUpdateEvent ev)
        {
            if (SuicideIfSomeoneElseCompletesTasksBeforeAgentOption)
            {
                //生存している、ノルマ未達成かつ非LoversのAgentが対象
                if (!MyPlayer.IsDead && !MyPlayer.TryGetModifier<Lover.Instance>(out _) && !MyPlayer.Tasks.IsAchievedQuota)
                {
                    int tasks = AmongUsUtil.NumOfAllTasks;
                    if (ev.Player.AmOwner) return;
                    //自分以外の通常以上のノルマを持つ生存者
                    if (!ev.Player.IsDead && ev.Player.Tasks.IsCrewmateTask && ev.Player.Tasks.Quota >= tasks && ev.Player.Tasks.IsAchievedQuota)
                    {
                        MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Layoff, KillParameter.NormalKill);
                        new StaticAchievementToken("agent.another1");
                    }
                }
            }
        }

        [OnlyMyPlayer]
        public void OnTaskCompleteLocal(PlayerTaskCompleteLocalEvent ev)
        {
            if (acTokenCommon2 != null) {
                acTokenCommon2.Value.cleared |= (NebulaGameManager.Instance!.CurrentTime - acTokenCommon2.Value.lastTask) < 15f && acTokenCommon2.Value.useVent;
                acTokenCommon2.Value.lastTask = NebulaGameManager.Instance.CurrentTime;
                acTokenCommon2.Value.useVent = false;
            }
        }

        [Local, OnlyMyPlayer]
        void OnEnterVent(PlayerVentEnterEvent ev)
        {
            ventDuration.Start();

            leftVent--;
            UsesText.text = leftVent.ToString();
            if (leftVent <= 0) UsesText.transform.parent.gameObject.SetActive(false);

            if (acTokenCommon2 != null) acTokenCommon2.Value.useVent = true;
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
                acTokenCommon2 = new("agent.common2", (false, -100f, false), (val, _) => val.cleared);

                if (NumOfExtraTasksOption > 0)
                {
                    var taskButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.Ability));
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

