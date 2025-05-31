using Nebula.Roles.Complex;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;

[NebulaRPCHolder]
public class ChainShifter : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.chainShifter", new(115, 115, 115), TeamRevealType.OnlyMe);

    private ChainShifter() : base("chainShifter", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [VentConfiguration, ShiftCoolDown, CanCallEmergencyMeetingOption]) { }

    Citation? HasCitation.Citation => Citations.TheOtherRolesGM;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.chainShifter.vent", true);
    static private FloatConfiguration ShiftCoolDown = NebulaAPI.Configurations.Configuration("options.role.chainShifter.shiftCoolDown", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanCallEmergencyMeetingOption = NebulaAPI.Configurations.Configuration("options.role.chainShifter.canCallEmergencyMeeting", true);

    static public ChainShifter MyRole = new ChainShifter();

    static private GameStatsEntry StatsShift = NebulaAPI.CreateStatsEntry("stats.chainShifter.shift", GameStatsCategory.Roles, MyRole);
    bool IGuessed.CanBeGuessDefault => false;


    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private Modules.ScriptComponents.ModAbilityButtonImpl? chainShiftButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ChainShiftButton.png", 115f);
        
        public Instance(GamePlayer player) : base(player, VentConfiguration)
        {
        }

        private GamePlayer? shiftTarget = null;
        private bool canExecuteShift = false;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                PoolablePlayer? shiftIcon = null;

                var playerTracker = ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate).Register(this);

                var chainShiftButton =NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    ShiftCoolDown, "shift", buttonSprite,
                    _ => playerTracker.CurrentTarget != null && shiftTarget == null);
                chainShiftButton.OnClick = (button) => {
                    shiftTarget = playerTracker.CurrentTarget;
                    shiftIcon = (chainShiftButton as ModAbilityButtonImpl)?.GeneratePlayerIcon(shiftTarget);
                    RpcCheckShift.Invoke((MyPlayer, shiftTarget!));
                };
                GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                {
                    if (shiftIcon) GameObject.Destroy(shiftIcon!.gameObject);
                    shiftIcon = null;
                }, this);
            }
        }

        //会議開始時に生きていればシフトは実行されうる
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            //自身が生存しかつ、シフトリストに登録されていて、相手がチェインシフターではない
            canExecuteShift = !MyPlayer.IsDead && currentTargetList.Any(entry => entry.shifter.AmOwner) && shiftTarget?.Role.Role != ChainShifter.MyRole;
        }


        [Local]
        void OnMeetingPreEnd(MeetingPreEndEvent ev)
        {
            ev.PushCoroutine(CoMeetingEnd());
            IEnumerator CoMeetingEnd()
            {
                if (!canExecuteShift) yield break;
                if (shiftTarget == null) yield break;
                if (!(shiftTarget.VanillaPlayer)) yield break;
                var player = shiftTarget.Unbox();

                //会議終了時に死亡している相手とはシフトできない
                if (player == null || player.IsDead) yield break;

                int[] targetArgument = new int[0];
                var targetRole = player.Role.Role;
                int targetGuess = -1;
                yield return player.CoGetRoleArgument((args) => targetArgument = args);
                yield return player.CoGetLeftGuess((guess) => targetGuess = guess);

                int myGuess = MyPlayer.TryGetModifier<GuesserModifier.Instance>(out var guesser) ? guesser.LeftGuess : -1;

                bool targetMJailer = player.TryGetModifier<Impostor.JailerModifier.Instance>(out _);
                bool myMJailer = MyPlayer.TryGetModifier<Impostor.JailerModifier.Instance>(out _);

                bool targetMadmate = player.TryGetModifier<Modifier.Madmate.Instance>(out _);
                bool myMadmate = MyPlayer.TryGetModifier<Modifier.Madmate.Instance>(out _);

                using (RPCRouter.CreateSection("ChainShift"))
                {
                    Debug.Log("Test1");
                    //タスクに関する書き換え
                    int leftCrewmateTask = 0;
                    int leftQuota = 0;
                    if (shiftTarget.Tasks.IsCrewmateTask && shiftTarget.Tasks.HasExecutableTasks)
                    {
                        leftCrewmateTask = Mathf.Max(0, shiftTarget.Tasks.CurrentTasks - shiftTarget.Tasks.CurrentCompleted);
                        leftQuota = Mathf.Max(0, shiftTarget.Tasks.Quota - shiftTarget.Tasks.TotalCompleted);
                    }

                    if (leftCrewmateTask > 0)
                    {
                        int commonTasks = GameOptionsManager.Instance.CurrentGameOptions.GetInt(AmongUs.GameOptions.Int32OptionNames.NumCommonTasks);
                        int shortTasks = GameOptionsManager.Instance.CurrentGameOptions.GetInt(AmongUs.GameOptions.Int32OptionNames.NumShortTasks);
                        int longTasks = GameOptionsManager.Instance.CurrentGameOptions.GetInt(AmongUs.GameOptions.Int32OptionNames.NumLongTasks);
                        float longWeight = (float)longTasks / (float)(commonTasks + shortTasks + longTasks);
                        float commonWeight = (float)commonTasks / (float)(commonTasks + shortTasks + longTasks);

                        int actualLongTasks = (int)((float)System.Random.Shared.NextDouble() * longWeight * leftCrewmateTask);
                        int actualcommonTasks = (int)((float)System.Random.Shared.NextDouble() * commonWeight * leftCrewmateTask);

                        MyPlayer.Tasks.Unbox().ReplaceTasksAndRecompute(leftCrewmateTask - actualLongTasks - actualcommonTasks, actualLongTasks, actualcommonTasks);
                        MyPlayer.Tasks.Unbox().BecomeToCrewmate();
                        MyPlayer.Tasks.Unbox().ReplaceTasks(leftCrewmateTask, leftQuota - leftCrewmateTask);
                    }
                    else
                    {
                        MyPlayer.Tasks.Unbox().ReleaseAllTaskState();
                    }

                    //タスクを整えたうえで役職を変更する
                    player.RpcInvokerSetRole(MyRole, null).InvokeSingle();
                    MyPlayer.Unbox().RpcInvokerSetRole(targetRole, targetArgument).InvokeSingle();

                    if (targetGuess != -1) player.RpcInvokerUnsetModifier(GuesserModifier.MyRole).InvokeSingle();
                    if (myGuess != -1) MyPlayer.Unbox().RpcInvokerUnsetModifier(GuesserModifier.MyRole).InvokeSingle();

                    if (myGuess != -1) player.RpcInvokerSetModifier(GuesserModifier.MyRole, new int[] { myGuess }).InvokeSingle();
                    if (targetGuess != -1) MyPlayer.Unbox().RpcInvokerSetModifier(GuesserModifier.MyRole, new int[] { targetGuess }).InvokeSingle();

                    if(myMJailer != targetMJailer)
                    {
                        if (myMJailer)
                        {
                            MyPlayer.Unbox().RpcInvokerUnsetModifier(Impostor.JailerModifier.MyRole).InvokeSingle();
                            player.RpcInvokerSetModifier(Impostor.JailerModifier.MyRole, []).InvokeSingle();
                        }
                        else
                        {
                            MyPlayer.Unbox().RpcInvokerSetModifier(Impostor.JailerModifier.MyRole, []).InvokeSingle();
                            player.RpcInvokerUnsetModifier(Impostor.JailerModifier.MyRole).InvokeSingle();
                        }
                    }

                    if (myMadmate != targetMadmate)
                    {
                        if (myMadmate)
                        {
                            MyPlayer.Unbox().RpcInvokerUnsetModifier(Modifier.Madmate.MyRole).InvokeSingle();
                            player.RpcInvokerSetModifier(Modifier.Madmate.MyRole, []).InvokeSingle();
                        }
                        else
                        {
                            MyPlayer.Unbox().RpcInvokerSetModifier(Modifier.Madmate.MyRole, []).InvokeSingle();
                            player.RpcInvokerUnsetModifier(Modifier.Madmate.MyRole).InvokeSingle();
                        }
                    }

                    new NebulaRPCInvoker(() => MyPlayer.Unbox().UpdateTaskState()).InvokeSingle();
                }

                //会議終了からすぐにゲームが終了すればよい
                new AchievementToken<float>("chainShifter.challenge", Time.time, (val, _) => Time.time - val < 15f && NebulaGameManager.Instance.EndState.Winners.Test(MyPlayer));
                StatsShift.Progress();

                yield return new WaitForSeconds(0.2f);

                yield break;
            }
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            shiftTarget = null;
            currentTargetList.Clear();
        }


        [OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer.AmOwner) new StaticAchievementToken("chainShifter.common1");
        }

        [Local]
        void OnGameEnd(GameEndEvent ev) => new StaticAchievementToken("chainShifter.another1");
        

        bool RuntimeAssignable.CanCallEmergencyMeeting => CanCallEmergencyMeetingOption;
    }

    static private List<(GamePlayer shifter, GamePlayer target)> currentTargetList = [];
    static private RemoteProcess<(GamePlayer shifter, GamePlayer target)> RpcFixShift = new("FixShift", (message, _) => currentTargetList.Add(message));
    static private RemoteProcess<(GamePlayer shifter, GamePlayer target)> RpcCheckShift = new("CheckShift", (message, _) =>
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (currentTargetList.Any(entry => entry.target == message.target)) return;

        RpcFixShift.Invoke(message);
    });
}
