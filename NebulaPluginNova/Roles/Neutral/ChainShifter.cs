using Nebula.Roles.Complex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Profiling;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Neutral;

// if (IsMySidekick(player)) player.RpcInvokerSetRole(Jackal.MyRole, new int[] { JackalTeamId }).InvokeSingle();
public class ChainShifter : ConfigurableStandardRole, HasCitation
{
    static public ChainShifter MyRole = new ChainShifter();
    static public Team MyTeam = new("teams.chainShifter", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory Category => RoleCategory.NeutralRole;

    public override string LocalizedName => "chainShifter";
    public override Color RoleColor => new Color(115f / 255f, 115f / 255f, 115f / 255f);
    public override RoleTeam Team => MyTeam;
    Citation? HasCitation.Citaion => Citations.TheOtherRolesGM;
    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private new VentConfiguration VentConfiguration = null!;
    private NebulaConfiguration ShiftCoolDown = null!;
    private NebulaConfiguration CanCallEmergencyMeetingOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        ShiftCoolDown = new(RoleConfig, "shiftCoolDown", null, 5f, 60f, 5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        CanCallEmergencyMeetingOption = new(RoleConfig, "canCallEmergencyMeeting", null, true, true);
    }

    public override bool CanBeGuessDefault => false;


    public class Instance : RoleInstance, IGamePlayerEntity
    {
        private ModAbilityButton? chainShiftButton = null;

        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ChainShiftButton.png", 115f);

        public override AbstractRole Role => MyRole;

        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;
        
        public Instance(GamePlayer player) : base(player)
        {
        }

        private GamePlayer? shiftTarget = null;
        private bool canExecuteShift = false;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                PoolablePlayer? shiftIcon = null;

                var playerTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer, ObjectTrackers.StandardPredicate));

                chainShiftButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
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
                chainShiftButton.CoolDownTimer = Bind(new Timer(MyRole.ShiftCoolDown.GetFloat()).SetAsAbilityCoolDown().Start());
                chainShiftButton.SetLabel("shift");
            }
        }

        //会議開始時に生きていればシフトは実行されうる
        void IGameEntity.OnMeetingStart()
        {
            canExecuteShift = !MyPlayer.IsDead;
        }

        public override IEnumerator? CoMeetingEnd()
        {
            if (!AmOwner) yield break;

            if (!canExecuteShift) yield break;
            if (shiftTarget == null) yield break;
            if(!(shiftTarget.VanillaPlayer)) yield break;
            var player = shiftTarget.Unbox();

            //会議終了時に死亡している相手とはシフトできない
            if(player == null || player.IsDead) yield break;
            
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
                if (player.Tasks.IsCrewmateTask && player.Tasks.HasExecutableTasks)
                {
                    leftCrewmateTask = Mathf.Max(0, player.Tasks.Quota - player.Tasks.TotalCompleted);

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

                    MyPlayer.Unbox().Tasks.ReplaceTasksAndRecompute(leftCrewmateTask - actualLongTasks - actualcommonTasks, actualLongTasks, actualcommonTasks);
                    MyPlayer.Unbox().Tasks.BecomeToCrewmate();
                }
                else
                {
                    MyPlayer.Unbox().Tasks.ReleaseAllTaskState();
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
            new AchievementToken<float>("chainShifter.challenge", Time.time, (val, _) => Time.time - val < 15f && NebulaGameManager.Instance.EndState.CheckWin(MyPlayer.PlayerId));

            yield return new WaitForSeconds(0.2f);

            yield break;
        }

        void IGameEntity.OnMeetingEnd(GamePlayer[] exiled)
        {
            shiftTarget = null;
        }


        void IGamePlayerEntity.OnMurdered(GamePlayer murder)
        {
            if (murder.AmOwner) new StaticAchievementToken("chainShifter.common1");
        }

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner) new StaticAchievementToken("chainShifter.another1");
        }

        public override bool CanCallEmergencyMeeting => MyRole.CanCallEmergencyMeetingOption;
    }
}
