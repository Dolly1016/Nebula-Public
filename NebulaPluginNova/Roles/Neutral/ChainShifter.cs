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

// if (IsMySidekick(player)) player.RpcInvokerSetRole(Jackal.MyRole, new int[] { JackalTeamId }).InvokeSingle();
public class ChainShifter : DefinedRoleTemplate, HasCitation, DefinedRole
{
    static public RoleTeam MyTeam = new Team("teams.chainShifter", new(115, 115, 115), TeamRevealType.OnlyMe);

    private ChainShifter() : base("chainShifter", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [VentConfiguration, ShiftCoolDown, CanCallEmergencyMeetingOption]) { }

    Citation? HasCitation.Citaion => Citations.TheOtherRolesGM;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.chainShifter.vent", true);
    static private FloatConfiguration ShiftCoolDown = NebulaAPI.Configurations.Configuration("options.role.chainShifter.shiftCoolDown", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration CanCallEmergencyMeetingOption = NebulaAPI.Configurations.Configuration("options.role.chainShifter.canCallEmergencyMeeting", true);

    static public ChainShifter MyRole = new ChainShifter();
    bool IGuessed.CanBeGuessDefault => false;


    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private Modules.ScriptComponents.ModAbilityButton? chainShiftButton = null;

        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ChainShiftButton.png", 115f);


        private GameTimer ventCoolDown = (new Timer(VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start() as GameTimer).ResetsAtTaskPhase();
        private GameTimer ventDuration = new Timer(VentConfiguration.Duration);
        private bool canUseVent = VentConfiguration.CanUseVent;
        GameTimer? RuntimeRole.VentCoolDown => ventCoolDown;
        GameTimer? RuntimeRole.VentDuration => ventDuration;
        bool RuntimeRole.CanUseVent => canUseVent;
        
        public Instance(GamePlayer player) : base(player)
        {
        }

        private GamePlayer? shiftTarget = null;
        private bool canExecuteShift = false;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                PoolablePlayer? shiftIcon = null;

                var playerTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                chainShiftButton = Bind(new Modules.ScriptComponents.ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                chainShiftButton.SetSprite(buttonSprite.GetSprite());
                chainShiftButton.Availability = (button) => playerTracker.CurrentTarget != null && MyPlayer.CanMove && shiftTarget == null;
                chainShiftButton.Visibility = (button) => !MyPlayer.IsDead;
                chainShiftButton.OnClick = (button) => {
                    shiftTarget = playerTracker.CurrentTarget;
                    shiftIcon = chainShiftButton.GeneratePlayerIcon(shiftTarget);
                };
                chainShiftButton.OnMeeting = (button) =>
                {
                    if (shiftIcon) GameObject.Destroy(shiftIcon!.gameObject);
                    shiftIcon = null;
                };
                chainShiftButton.CoolDownTimer = Bind(new Timer(ShiftCoolDown).SetAsAbilityCoolDown().Start());
                chainShiftButton.SetLabel("shift");
            }
        }

        //会議開始時に生きていればシフトは実行されうる
        void OnMeetingStart(MeetingStartEvent ev)
        {
            canExecuteShift = !MyPlayer.IsDead;
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

                int myGuess = MyPlayer.Unbox().TryGetModifier<GuesserModifier.Instance>(out var guesser) ? guesser.LeftGuess : -1;

                using (RPCRouter.CreateSection("ChainShift"))
                {
                    Debug.Log("Test1");
                    //タスクに関する書き換え
                    int leftCrewmateTask = 0;
                    if (shiftTarget.Tasks.IsCrewmateTask && shiftTarget.Tasks.HasExecutableTasks)
                    {
                        leftCrewmateTask = Mathf.Max(0, shiftTarget.Tasks.Quota - shiftTarget.Tasks.TotalCompleted);

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


                }

                //会議終了からすぐにゲームが終了すればよい
                new AchievementToken<float>("chainShifter.challenge", Time.time, (val, _) => Time.time - val < 15f && NebulaGameManager.Instance.EndState.Winners.Test(MyPlayer));

                yield return new WaitForSeconds(0.2f);

                yield break;
            }
        }

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            shiftTarget = null;
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
}
