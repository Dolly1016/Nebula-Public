using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

internal class Opportunist : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.opportunist", new(106, 252, 45), TeamRevealType.OnlyMe);

    private Opportunist() : base("opportunist", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [NumOfTasksOption, StayingDurationOption, VentConfiguration, CanFixLightOption, CanFixCommsOption])
    {
    }

    Citation? HasCitation.Citation => Citations.TheOtherRolesGM;

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, -1));

    static private IntegerConfiguration NumOfTasksOption = NebulaAPI.Configurations.Configuration("options.role.opportunist.numOfTasks", (1, 8, 1), 3);
    static private FloatConfiguration StayingDurationOption = NebulaAPI.Configurations.Configuration("options.role.opportunist.stayingDuration", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanFixLightOption = NebulaAPI.Configurations.Configuration("options.role.opportunist.canFixLight", true);
    static private BoolConfiguration CanFixCommsOption = NebulaAPI.Configurations.Configuration("options.role.opportunist.canFixComms", true);
    //static private BoolConfiguration ExtraMissionOption = NebulaAPI.Configurations.Configuration("options.role.opportunist.extraMission", false);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.opportunist.vent", true);

    static public Opportunist MyRole = new Opportunist();

    static private GameStatsEntry StatsTask = NebulaAPI.CreateStatsEntry("stats.opportunist.task", GameStatsCategory.Roles, MyRole);
    static public RoleFallback GenerateFallback(int numOfTasks) => new(MyRole, [numOfTasks]);
    public record OpportunistFallbackOption(GroupConfiguration Group, BoolConfiguration Switch, IntegerConfiguration Tasks);
    static public OpportunistFallbackOption FallbackConfiguration(Virial.Color? color, string? translationKey, string optionsPrefix, bool defaultSwitch = false, int defaultTasks = 2, int minTasks = 0, int maxTasks = 8)
    {
        BoolConfiguration switchOption = NebulaAPI.Configurations.Configuration(optionsPrefix + ".opportunistFallback", defaultSwitch, title: GUI.API.LocalizedTextComponent(translationKey != null ? translationKey + ".opportunistFallback" : "options.role.opportunist.fallback"));
        IntegerConfiguration tasksOption = NebulaAPI.Configurations.Configuration(optionsPrefix + ".opportunistFallback.tasks", (minTasks, maxTasks), defaultTasks, () => switchOption, title:  GUI.API.LocalizedTextComponent(translationKey != null ? translationKey + ".opportunistFallback.tasks" : "options.role.opportunist.fallback.tasks"));
        return new(new GroupConfiguration(translationKey != null ? translationKey + ".group.opportunistFallback" : "options.role.opportunist.group.fallback", [switchOption, tasksOption], GroupConfigurationColor.ToDarkenColor((color ?? MyTeam.Color).ToUnityColor())), switchOption, tasksOption);
    }

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        int[]? RuntimeAssignable.RoleArguments => [MyPlayer.Tasks.TotalTasks - MyPlayer.Tasks.TotalCompleted];
        private int NumOfTasks;
        public override DefinedRole Role => MyRole;
        
        public Instance(GamePlayer player, int numOfTasks) : base(player, VentConfiguration)
        {
            NumOfTasks = numOfTasks switch
            {
                < 0 => NumOfTasksOption,
                0 => 1,
                _ => numOfTasks
            };
        }
        public override void OnActivated()
        {
            if (AmOwner)
            {
                MyPlayer.Tasks.Custom(0, NumOfTasks, false);
                myTasks = GetTaskArea(NumOfTasks).Select(area => new TaskInfo(area, StayingDurationOption)).ToArray();


                myArrow = new Arrow().Register(this);
                myArrow.IsActive = false;
                myArrow.SetColor(MyRole.UnityColor);
            }
        }
        bool RuntimeAssignable.CanFixComm => CanFixCommsOption;
        bool RuntimeAssignable.CanFixLight => CanFixLightOption;
        RoleTaskType RuntimeRole.TaskType => RoleTaskType.RoleTask;
        private Arrow? myArrow;

        private TaskInfo[] myTasks = [];
        [Local]
        void OnUpdateTaskText(PlayerTaskTextLocalEvent ev)
        {
            ev.ReplaceBody(string.Join("\n", myTasks.Select(task => task.GetTaskText())));
        }

        void OnUpdate(GameHudUpdateEvent ev)
        {
            if (MyPlayer.IsDead) return;
            if (MeetingHud.Instance || ExileController.Instance) return;

            bool hasTask = false;
            foreach (var task in myTasks)
            {
                task.IsUnveiled = true;
                task.IsActive = false;
                if (task.Progress >= task.Goal) continue;
                if (task.Area.CheckPosition(MyPlayer.TruePosition))
                {
                    task.Progress += ev.DeltaTime;
                    task.IsActive = true;
                    if (task.IsCompleted)
                    {
                        StatsTask.Progress();
                        HudManager.Instance.ShowTaskComplete();
                        MyPlayer.Tasks.Custom(myTasks.Count(t => t.IsCompleted), NumOfTasks, false);
                    }
                }

                //矢印更新
                myArrow!.IsActive = true;
                myArrow!.TargetPos = task.Area.ClosestPoint(MyPlayer.Position);
                hasTask = true;
                break;
            }

            if(!hasTask) myArrow!.IsActive = false;
        }

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.OpportunistPhase) return;
            if(MyPlayer.IsAlive && MyPlayer.Tasks.IsCompletedTotalTasks)
            {
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraOpportunistWin);
                ev.IsExtraWin = true;
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (MyPlayer.IsAlive && ev.EndState.Winners.Test(MyPlayer)) {
                if (ev.EndState.ExtraWins.Test(NebulaGameEnd.ExtraOpportunistWin)) new StaticAchievementToken("opportunist.common1");
                if (GamePlayer.AllPlayers.All(p => ev.EndState.Winners.Test(p) || p.AmOwner)) new StaticAchievementToken("opportunist.common2");
                if (GamePlayer.AllPlayers.Any(p => p.IsAlive && !ev.EndState.Winners.Test(p))) new StaticAchievementToken("opportunist.challenge");
            }
            if (MyPlayer.IsAlive && !ev.EndState.Winners.Test(MyPlayer)) new StaticAchievementToken("opportunist.another1");
            if (MyPlayer.IsDead && (MyPlayer.DeathTime ?? 0f) + 2f > NebulaGameManager.Instance?.CurrentTime) new StaticAchievementToken("opportunist.another2");
        }

    }

    private class TaskInfo
    {
        public TaskArea Area { get; private init; }
        public float Progress { get; set; }
        public float Goal { get; private init; }
        public bool IsUnveiled { get; set; } = false;
        public bool IsActive { get; set; } = false;
        public bool IsCompleted => !(this.Progress < Goal);

        private string? notActiveStrCache = null;
        private string? taskStrCache = null;
        public string GetTaskText()
        {
            if (!IsUnveiled)
            {
                notActiveStrCache ??= Language.Translate("role.opportunist.task.veiled");
                return notActiveStrCache.Color(new UnityEngine.Color(0.5f,0.5f,0.5f));
            }
            if(taskStrCache == null)
            {
                var roomName = TranslationController.Instance.GetString(Area.Room);
                var locName = Area.LocationNameKey == null ? null : Language.Find("role.opportunist.task.loc." + Area.LocationNameKey);
                if (locName != null)
                    taskStrCache = roomName + ": " + Language.Translate("role.opportunist.task").Replace("%LOC%", locName);
                else
                    taskStrCache = roomName + ": " + Language.Translate("role.opportunist.task.room");
            }
            if(!IsCompleted)
            {
                float left = this.Goal - this.Progress;
                string progressStr = " (" + Mathn.CeilToInt(left).ToString() + "s)";
                return GetColoredText(taskStrCache + progressStr, this.Progress > 0f, false, Area.CheckPosition(GamePlayer.LocalPlayer!.TruePosition));
            }
            return GetColoredText(taskStrCache, false, IsCompleted, true);
        }

        private string GetColoredText(string text, bool inProgress, bool completed, bool isActive)
        {
            if (completed) return "<color=#00DD00FF>" + text + "</color>";
            if (inProgress && isActive) return "<color=#FFFF00FF>" + text + "</color>";
            if (inProgress && !isActive) return "<color=#666600FF>" + text + "</color>";
            return text;
        }

        public TaskInfo(TaskArea area, float goal)
        {
            this.Area = area;
            this.Goal = goal;
            this.Progress = 0f;
        }
    }
    private record TaskArea(SystemTypes Room, string? LocationNameKey, Vector2 Min, Vector2 Max)
    {
        public bool CheckPosition(Vector2 position) => Min.x < position.x && position.x < Max.x && Min.y < position.y && position.y < Max.y;
        public Vector2 ClosestPoint(Vector2 position)
        {
            if (position.x < Min.x) position.x = Min.x;
            if (position.x > Max.x) position.x = Max.x;
            if (position.y < Min.y) position.y = Min.y;
            if (position.y > Max.y) position.y = Max.y;
            return position;
        }
        static public TaskArea Create(SystemTypes room, string? locationNameKey, Vector2 center, float radius) => new(room, locationNameKey, new(center.x - radius, center.y - radius), new(center.x + radius, center.y + radius));
        static public TaskArea Create(SystemTypes room, string? locationNameKey, float x1, float x2, float y1, float y2) => new(room, locationNameKey, new(x1 < x2 ? x1 : x2, y1 < y2 ? y1 : y2), new(x1 < x2 ? x2 : x1, y1 < y2 ? y2 : y1));
    }

    private static IEnumerable<TaskArea> GetTaskArea(int num)
    {
        var positions = TaskPositions[AmongUsUtil.CurrentMapId];
        var randomArray = Helpers.GetRandomArray(positions.Length);
        SystemTypes lastType = SystemTypes.Doors;
        int count = 0;
        int index = 0;
        while(count < num)
        {
            var selected = positions[randomArray[index]];
            index++;
            if (selected.Room == lastType) continue;
            lastType = selected.Room;
            yield return selected;
            count++;
        }
    }

    internal static IEnumerable<(Vector2 center, Vector2 size)> GetTaskPositions() => TaskPositions[AmongUsUtil.CurrentMapId].Select(t => ((t.Min + t.Max) * 0.5f, t.Max - t.Min));
    private static TaskArea[][] TaskPositions = [
        [
            TaskArea.Create(SystemTypes.Nav, null, new(18f, -4.6f), 2.5f),
            TaskArea.Create(SystemTypes.LifeSupp, null, new(6.3f, -4.1f), 1.6f),
            TaskArea.Create(SystemTypes.Weapons, null, new(9.5f, 2.3f), 2.5f),
            TaskArea.Create(SystemTypes.Cafeteria, "skeld.cafeteria.window", new(-1.0f, 8.7f), 3.5f),
            TaskArea.Create(SystemTypes.Shields, null, new(9.7f, -12.4f), 2.5f),
            TaskArea.Create(SystemTypes.Storage, "skeld.storage.trash", new(-0.2f, -16.5f), 1.5f),
            TaskArea.Create(SystemTypes.Storage, "skeld.electrical.electrical", new(-8.8f, -8.0f), 2f),
            TaskArea.Create(SystemTypes.LowerEngine, "skeld.lowerEngine.lowerEngine", new(-17.7f, -13.0f), 2f),
            TaskArea.Create(SystemTypes.Reactor, "skeld.reactor.upper", new(-22.5f, -2.4f), 2f),
            TaskArea.Create(SystemTypes.MedBay, "skeld.medbay.scan", new(-7.0f, -5.0f), 1.8f),
        ],
        [
            TaskArea.Create(SystemTypes.Reactor, null, new(2.5f, 11.9f), 1f),
            TaskArea.Create(SystemTypes.Laboratory, null, new(10.7f, 13.3f), 1.6f),
            TaskArea.Create(SystemTypes.Launchpad, null, new(-4.3f, 2.1f), 2.4f),
            TaskArea.Create(SystemTypes.MedBay, null, new(15.7f, -1.6f), 1.8f),
            TaskArea.Create(SystemTypes.Balcony, "mira.balcony.antenna", new(19.1f, -2.6f), 1.5f),
            TaskArea.Create(SystemTypes.Storage, null, new(19.4f, 4.3f), 1.7f),
            TaskArea.Create(SystemTypes.Cafeteria, "mira.cafeteria.vendingMachine", new(27.8f, 4.6f), 1.1f),
            TaskArea.Create(SystemTypes.Admin, null, new(21.9f, 19.2f), 2f),
            TaskArea.Create(SystemTypes.Greenhouse, null, new(17.8f, 25.8f), 2f),
            TaskArea.Create(SystemTypes.Office, null, new(14.7f, 19.4f), 1.8f),
        ],
        [
            TaskArea.Create(SystemTypes.Security, "polus.security.camera", new(2.8f, -12.0f), 1f),
            TaskArea.Create(SystemTypes.Comms, "polus.comms.table", new(11.5f, -16.8f), 1f),
            TaskArea.Create(SystemTypes.Laboratory, "polus.lab.hole", new(26.8f, -7.7f), 1f),
            TaskArea.Create(SystemTypes.Laboratory, "polus.lab.toilet", new(33.7f, -10.0f), 1f),
            TaskArea.Create(SystemTypes.Specimens, null, new(36.6f, -20.8f), 2f),
            TaskArea.Create(SystemTypes.Admin, "polus.admin.bookshelf", new(21.2f, -25.7f), 1.7f),
            TaskArea.Create(SystemTypes.LifeSupp, "polus.lifesupport.bottle", new(0.9f, -20.7f), 1.2f),
            TaskArea.Create(SystemTypes.Outside, "polus.outside.ejection", new(32.4f, -15.7f), 1.9f),
            TaskArea.Create(SystemTypes.Storage, null, new(20.6f, -11.6f), 1f),
            TaskArea.Create(SystemTypes.Outside, "polus.outside.comms", new(7.9f, -16.0f), 1.5f),
        ],
        null!,
        [
            TaskArea.Create(SystemTypes.MeetingRoom, "airship.meetingRoom.meetingRoom", new(16.0f, 15.0f), 1.5f),
            TaskArea.Create(SystemTypes.MainHall, "airship.mainHall.cleaningRoom", new(9.2f, 2.5f), 1.5f),
            TaskArea.Create(SystemTypes.MainHall, "airship.mainHall.treasureBox", new(6.1f, 3.5f), 1f),
            TaskArea.Create(SystemTypes.MainHall, "airship.mainHall.darkRoom", new(12.4f, 2.5f), 1.5f),
            TaskArea.Create(SystemTypes.Showers, null, 20.2f, 24.9f, 1.9f, 3.6f),
            TaskArea.Create(SystemTypes.Lounge, "airship.lounge.toilet", 28.4f, 29.8f,6.7f, 8.2f),
            TaskArea.Create(SystemTypes.Lounge, "airship.lounge.toilet", 30.0f, 31.5f,6.7f, 8.2f),
            TaskArea.Create(SystemTypes.Lounge, "airship.lounge.toilet", 31.5f, 33.0f,6.7f, 8.2f),
            TaskArea.Create(SystemTypes.Lounge, "airship.lounge.toilet", 33.0f, 34.5f,6.7f, 8.2f),
            TaskArea.Create(SystemTypes.CargoBay, "airship.cargoBay.safe", new(37.1f, -3.1f), 1.5f),
            TaskArea.Create(SystemTypes.Ventilation, null, new(27.5f, -0.7f), 2.4f),
            TaskArea.Create(SystemTypes.Medical, "airship.medical.vitals", new(25.2f, -9.2f), 1.5f),
            TaskArea.Create(SystemTypes.Electrical, null, new(19.3f, -6.5f), 1.2f),
            TaskArea.Create(SystemTypes.Electrical, null, new(16.3f, -6.3f), 1.4f),
            TaskArea.Create(SystemTypes.Electrical, null, new(13.3f, -6.3f), 1.4f),
            TaskArea.Create(SystemTypes.Electrical, null, new(10.3f, -6.3f), 1.4f),
            TaskArea.Create(SystemTypes.Electrical, null, new(13.2f, -8.8f), 1.2f),
            TaskArea.Create(SystemTypes.Electrical, null, new(16.3f, -8.8f), 1.2f),
            TaskArea.Create(SystemTypes.Electrical, null, new(19.3f, -8.8f), 1.2f),
            TaskArea.Create(SystemTypes.Electrical, null, new(19.3f, -11.2f), 1.4f),
            TaskArea.Create(SystemTypes.Electrical, null, new(16.3f, -11.2f), 1.4f),
            TaskArea.Create(SystemTypes.Security, "airship.security.outside", new(9.9f, -15.6f), 1.5f),
            TaskArea.Create(SystemTypes.HallOfPortraits, null, -1.5f, 3f, -10.9f, -13.9f),
            TaskArea.Create(SystemTypes.ViewingDeck, "airship.viewingDeck.outside", new(-13.6f, -15.6f), 1.5f),
            TaskArea.Create(SystemTypes.Armory, null, new(-14.4f, -8.7f), 1.5f),
            TaskArea.Create(SystemTypes.Cockpit, null, new(-22.8f, -0.3f), 2f),
        ],
        [
            TaskArea.Create(SystemTypes.MiningPit, null, new(13.7f, 9.8f), 1.2f),
            TaskArea.Create(SystemTypes.Lookout, null, new(7.3f, 0.7f), 1.5f),
            TaskArea.Create(SystemTypes.UpperEngine, null, new(22.4f, 3.0f), 1.3f),
            TaskArea.Create(SystemTypes.Highlands, "fungle.highlands.bigGem", new(15.4f, 2.6f), 2.5f),
            TaskArea.Create(SystemTypes.Highlands, "fungle.highlands.landing", 17.4f, 22.8f, 6.1f, 8.8f),
            TaskArea.Create(SystemTypes.Comms, null, new(22.6f, 16.2f), 4f),
            TaskArea.Create(SystemTypes.SleepingQuarters, null, new(2.3f, -1.6f), 1.5f),
            TaskArea.Create(SystemTypes.Jungle, "fungle.jungle.monitoring", new(13.6f, -15.5f), 2f),
            TaskArea.Create(SystemTypes.Reactor, null, new(21.9f, -7.4f), 2f),
            TaskArea.Create(SystemTypes.Laboratory, null, new(-4.0f, -9.3f), 1.8f),
            TaskArea.Create(SystemTypes.Kitchen, null, -18.2f, -12.6f, -10.6f, -8.5f),
            TaskArea.Create(SystemTypes.FishingDock, null, new(-22.5f, -6.8f), 1.5f),
            TaskArea.Create(SystemTypes.RecRoom, null, new(-19.9f, -0.4f), 2f),
            TaskArea.Create(SystemTypes.Cafeteria, null, new(-16.4f, 6.8f), 3f),
        ],
        ];
}


