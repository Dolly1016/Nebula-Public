using Nebula.Roles.Complex;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Profiling;

namespace Nebula.Roles.Neutral;

// if (IsMySidekick(player)) player.RpcInvokerSetRole(Jackal.MyRole, new int[] { JackalTeamId }).InvokeSingle();
public class ChainShifter : ConfigurableStandardRole
{
    static public ChainShifter MyRole = new ChainShifter();
    static public Team MyTeam = new("teams.chainShifter", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory RoleCategory => RoleCategory.NeutralRole;

    public override string LocalizedName => "chainShifter";
    public override Color RoleColor => new Color(115f / 255f, 115f / 255f, 115f / 255f);
    public override Team Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private new VentConfiguration VentConfiguration = null!;
    private NebulaConfiguration ShiftCoolDown = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        ShiftCoolDown = new(RoleConfig, "shiftCoolDown", null, 5f, 60f, 5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public override bool CanBeGuess => false;


    public class Instance : RoleInstance
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
        
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        private PlayerControl? shiftTarget = null;
        private bool canExecuteShift = false;

        public override void OnActivated()
        {
            if (AmOwner)
            {
                PoolablePlayer? shiftIcon = null;

                var playerTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl, (p) => p.PlayerId != MyPlayer.PlayerId && !p.Data.IsDead));

                chainShiftButton = Bind(new ModAbilityButton()).KeyBind(KeyAssignmentType.Ability);
                chainShiftButton.SetSprite(buttonSprite.GetSprite());
                chainShiftButton.Availability = (button) => playerTracker.CurrentTarget != null && MyPlayer.MyControl.CanMove && shiftTarget == null;
                chainShiftButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                chainShiftButton.OnClick = (button) => {
                    shiftTarget = playerTracker.CurrentTarget;
                    shiftIcon = AmongUsUtil.GetPlayerIcon(shiftTarget.GetModInfo()!.DefaultOutfit, chainShiftButton!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f)).SetAlpha(0.5f);
                };
                chainShiftButton.OnMeeting = (button) =>
                {
                    if (shiftIcon) GameObject.Destroy(shiftIcon!.gameObject);
                    shiftIcon = null;
                };
                chainShiftButton.CoolDownTimer = Bind(new Timer(MyRole.ShiftCoolDown.GetFloat()).SetAsAbilityCoolDown().Start());
                chainShiftButton.SetLabelType(ModAbilityButton.LabelType.Standard);
                chainShiftButton.SetLabel("shift");
            }
        }

        //会議開始時に生きていればシフトは実行されうる
        public override void OnMeetingStart()
        {
            canExecuteShift = !MyPlayer.IsDead;
        }

        public override IEnumerator? CoMeetingEnd()
        {
            if (!canExecuteShift) yield break;
            if (shiftTarget == null) yield break;
            if(!shiftTarget) yield break;
            var player = shiftTarget.GetModInfo();

            //会議終了時に死亡している相手とはシフトできない
            if(player == null || player.IsDead) yield break;
            
            int[] targetArgument = new int[0];
            var targetRole = player.Role.Role;
            int targetGuess = -1;
            yield return player.CoGetRoleArgument((args) => targetArgument = args);
            yield return player.CoGetLeftGuess((guess) => targetGuess = guess);

            int myGuess = MyPlayer.TryGetModifier<GuesserModifier.Instance>(out var guesser) ? guesser.LeftGuess : -1;

            using (RPCRouter.CreateSection("ChainShift"))
            {
                player.RpcInvokerSetRole(MyRole, null).InvokeSingle();
                MyPlayer.RpcInvokerSetRole(targetRole, targetArgument).InvokeSingle();

                if (targetGuess != -1) player.RpcInvokerUnsetModifier(GuesserModifier.MyRole).InvokeSingle();
                if (myGuess != -1) MyPlayer.RpcInvokerUnsetModifier(GuesserModifier.MyRole).InvokeSingle();

                if (myGuess != -1) player.RpcInvokerSetModifier(GuesserModifier.MyRole, new int[] { myGuess }).InvokeSingle();
                if (targetGuess != -1) MyPlayer.RpcInvokerSetModifier(GuesserModifier.MyRole, new int[] { targetGuess }).InvokeSingle();

                int leftCrewmateTask = 0;
                if (player.Tasks.IsCrewmateTask)
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

                    MyPlayer.Tasks.ReplaceTasksAndRecompute(leftCrewmateTask - actualLongTasks - actualcommonTasks, actualLongTasks, actualcommonTasks);
                    MyPlayer.Tasks.BecomeToCrewmate();
                }
                else
                {
                    MyPlayer.Tasks.ReleaseAllTaskState();
                }
            }

            yield return new WaitForSeconds(0.2f);

            yield break;
        }

        public override void OnMeetingEnd()
        {
            shiftTarget = null;
        }
    }
}
