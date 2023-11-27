using Nebula.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;

namespace Nebula.Roles.Crewmate;

public class Madmate : ConfigurableStandardRole
{
    static public Madmate MyRole = new Madmate();

    public override RoleCategory RoleCategory => RoleCategory.CrewmateRole;

    public override string LocalizedName => "madmate";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EmbroilVotersOnExileOption = null!;
    private NebulaConfiguration CanFixLightOption = null!;
    private NebulaConfiguration CanFixCommsOption = null!;
    private NebulaConfiguration CanIdenfityImpostorsOption = null!;
    private NebulaConfiguration[] NumOfTasksToIdentifyImpostorsOptions = null!;


    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanFixLightOption = new NebulaConfiguration(RoleConfig, "canFixLight", null, false, false);
        CanFixCommsOption = new NebulaConfiguration(RoleConfig, "canFixComms", null, false, false);
        EmbroilVotersOnExileOption = new NebulaConfiguration(RoleConfig, "embroilVotersOnExile", null, false, false);
        CanIdenfityImpostorsOption = new NebulaConfiguration(RoleConfig, "canIdentifyImpostors", null, 3, 0, 0);

        NumOfTasksToIdentifyImpostorsOptions = new NebulaConfiguration[]{
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors0",null,1,10,2,2),
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors1",null,1,10,4,4),
            new NebulaConfiguration(RoleConfig,"numOfTasksToIdentifyImpostors2",null,1,10,6,6)
        };

        CanIdenfityImpostorsOption.Shower = () =>
        {
            return CanIdenfityImpostorsOption.DefaultShowerString
                + StringExtensions.Color(
                    " (" +
                    NumOfTasksToIdentifyImpostorsOptions
                        .Take(CanIdenfityImpostorsOption.GetMappedInt())
                        .Join(option => option.ToDisplayString(), ", ")
                    + ")", Color.gray);
        };

        foreach (var option in NumOfTasksToIdentifyImpostorsOptions) { option.Shower = () => null; option.Editor = NebulaConfiguration.EmptyEditor; }

        new NebulaConfiguration(RoleConfig, () =>
        {
            if (CanIdenfityImpostorsOption.CurrentValue == 0) return null;

            List<IMetaParallelPlacable> placable = new();

            for (int i = 0; i < CanIdenfityImpostorsOption.CurrentValue; i++)
            {
                if (i != 0) placable.Add(new MetaContext.HorizonalMargin(0.25f));

                var option = NumOfTasksToIdentifyImpostorsOptions[i];
                placable.Add(NebulaConfiguration.OptionButtonContext(() => option.ChangeValue(false), "<<"));
                placable.Add(new MetaContext.Text(NebulaConfiguration.OptionShortValueAttr) { RawText = option.ToDisplayString() });
                placable.Add(NebulaConfiguration.OptionButtonContext(() => option.ChangeValue(true), ">>"));
            }

            return new CombinedContext(placable.ToArray());
        });
    }
    
    public class Instance : Crewmate.Instance
    {
        List<byte> impostors = new();

        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong _) => endCondition == NebulaGameEnd.ImpostorWin;
        public override bool HasCrewmateTasks => false;

        void SetMadmateTask()
        {
            if (AmOwner)
            {
                var numOfTasksOptions = MyRole.NumOfTasksToIdentifyImpostorsOptions.Take(MyRole.CanIdenfityImpostorsOption.GetMappedInt());
                int max = numOfTasksOptions.Max(option => option.GetMappedInt());

                using (RPCRouter.CreateSection("MadmateTask"))
                {
                    MyPlayer.Tasks.ReplaceTasksAndRecompute(max, 0, 0);
                    MyPlayer.Tasks.BecomeToOutsider();
                }
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            SetMadmateTask();
        }

        public override void OnGameStart()
        {
            base.OnGameStart();

            SetMadmateTask();
        }

        public override void OnTaskCompleteLocal()
        {
            //インポスター判別のチャンスだけ繰り返す
            while(MyRole.CanIdenfityImpostorsOption.GetMappedInt() > impostors.Count && MyPlayer.Tasks.CurrentCompleted >= MyRole.NumOfTasksToIdentifyImpostorsOptions[impostors.Count].GetMappedInt())
            {
                var pool = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && p.Role.Role.RoleCategory == RoleCategory.ImpostorRole && !impostors.Contains(p.PlayerId)).ToArray();
                //候補が残っていなければ何もしない
                if (pool.Length == 0) return;
                impostors.Add(pool[System.Random.Shared.Next(pool.Length)].PlayerId);
            }
            
        }

        public override void DecorateOtherPlayerName(PlayerModInfo player, ref string text, ref Color color)
        {
            if (impostors.Contains(player.PlayerId) && player.Role.Role.RoleCategory == RoleCategory.ImpostorRole) color = Palette.ImpostorRed;
        }

        public override void OnExiled()
        {
            if (!AmOwner) return;
            if (!MyRole.EmbroilVotersOnExileOption) return;

            ExtraExileRoleSystem.MarkExtraVictim(MyPlayer, false, true);
        }

        public override bool HasAnyTasks => MyRole.CanIdenfityImpostorsOption.GetMappedInt() > 0;

        public override bool CanFixComm => MyRole.CanFixCommsOption;
        public override bool CanFixLight => MyRole.CanFixLightOption;
    }
}

