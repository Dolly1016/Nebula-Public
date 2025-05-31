using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Impostor;

public class Reaper : DefinedRoleTemplate, DefinedRole
{
    private Reaper() : base("reaper", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [VentConfiguration, CanUseVentWhileHoldingDeadbodyOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Reaper.png");
    }

    bool DefinedRole.IsJackalizable => true;
    IPlayerAbility DefinedRole.GetJackalizedAbility(Virial.Game.Player jackal, int[] arguments) => new Ability(jackal, arguments.GetAsBool(0));
    IUsurpableAbility? DefinedRole.GetUsurpedAbility(Virial.Game.Player player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.VentConfiguration("role.reaper.vent", false, null, -1, (0f, 60f, 2.5f), 15f, (2.5f, 30f, 2.5f), 10f);
    static private BoolConfiguration CanUseVentWhileHoldingDeadbodyOption = NebulaAPI.Configurations.Configuration("options.role.reaper.useVentWhileHoldingDeadbody", true);

    static public Reaper MyRole = new Reaper();
    static private GameStatsEntry StatsVent = NebulaAPI.CreateStatsEntry("stats.reaper.ventWithDeadBody", GameStatsCategory.Roles, MyRole);
    
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped) {
            new Scripts.Draggable(MyPlayer).Register(new FunctionalLifespan(()=> !IsDeadObject && !IsUsurped));
        }
    }

    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;
        bool RuntimeRole.PreventUsingVent => MyPlayer.HoldingAnyDeadBody && !CanUseVentWhileHoldingDeadbodyOption;

        StaticAchievementToken? acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;
        int[] RuntimeAssignable.RoleArguments => [isUsurped.AsInt()];
        public Instance(GamePlayer player) : base(player, VentConfiguration)
        {
        }

        private Vent? GetVent(string name)
        {
            return ShipStatus.Instance.AllVents.FirstOrDefault(v=>v.name == name);
        }

        private void EditVentInfo(bool activate)
        {
            switch (AmongUsUtil.CurrentMapId)
            {
                case 0:
                    //Skeld
                    GetVent("NavVentNorth")!.Right = activate ? GetVent("NavVentSouth") : null;
                    GetVent("NavVentSouth")!.Right = activate ? GetVent("NavVentNorth") : null;

                    GetVent("ShieldsVent")!.Left = activate ? GetVent("WeaponsVent") : null;
                    GetVent("WeaponsVent")!.Center = activate ? GetVent("ShieldsVent") : null;

                    GetVent("ReactorVent")!.Left = activate ? GetVent("UpperReactorVent") : null;
                    GetVent("UpperReactorVent")!.Left = activate ? GetVent("ReactorVent") : null;

                    GetVent("SecurityVent")!.Center = activate ? GetVent("ReactorVent") : null;
                    GetVent("ReactorVent")!.Center = activate ? GetVent("SecurityVent") : null;

                    GetVent("REngineVent")!.Center = activate ? GetVent("LEngineVent") : null;
                    GetVent("LEngineVent")!.Center = activate ? GetVent("REngineVent") : null;

                    if (GetVent("StorageVent") != null)
                    {
                        GetVent("AdminVent")!.Center = activate ? GetVent("StorageVent") : null;
                        GetVent("StorageVent")!.Left = activate ? GetVent("ElecVent") : null;
                        GetVent("StorageVent")!.Right = activate ? GetVent("AdminVent") : null;

                        GetVent("StorageVent")!.Center = activate ? GetVent("CafeUpperVent") : null;
                    }
                    else
                    {
                        GetVent("AdminVent")!.Center = activate ? GetVent("MedVent") : null;
                        GetVent("MedVent")!.Center = activate ? GetVent("AdminVent") : null;
                    }

                    if (GetVent("CafeUpperVent") != null)
                    {
                        GetVent("CafeUpperVent")!.Left = activate ? GetVent("LEngineVent") : null;
                        GetVent("LEngineVent")!.Right = activate ? GetVent("CafeUpperVent") : null;

                        GetVent("CafeUpperVent")!.Center = activate ? GetVent("StorageVent") : null;

                        GetVent("CafeUpperVent")!.Right = activate ? GetVent("WeaponsVent") : null;
                        GetVent("WeaponsVent")!.Left = activate ? GetVent("CafeUpperVent") : null;
                    }
                    else
                    {
                        GetVent("CafeVent")!.Center = activate ? GetVent("WeaponsVent") : null;
                        GetVent("WeaponsVent")!.Center = activate ? GetVent("CafeVent") : null;
                    }

                    break;
                case 2:
                    //Polus
                    GetVent("CommsVent")!.Center = activate ? GetVent("ElecFenceVent") : null;
                    GetVent("ElecFenceVent")!.Center = activate ? GetVent("CommsVent") : null;

                    GetVent("ElectricalVent")!.Center = activate ? GetVent("ElectricBuildingVent") : null;
                    GetVent("ElectricBuildingVent")!.Center = activate ? GetVent("ElectricalVent") : null;

                    GetVent("ScienceBuildingVent")!.Right = activate ? GetVent("BathroomVent") : null;
                    GetVent("BathroomVent")!.Center = activate ? GetVent("ScienceBuildingVent") : null;

                    GetVent("SouthVent")!.Center = activate ? GetVent("OfficeVent") : null;
                    GetVent("OfficeVent")!.Center = activate ? GetVent("SouthVent") : null;

                    if (GetVent("SpecimenVent") != null)
                    {
                        GetVent("AdminVent")!.Center = activate ? GetVent("SpecimenVent") : null;
                        GetVent("SpecimenVent")!.Left = activate ? GetVent("AdminVent") : null;

                        GetVent("SubBathroomVent")!.Center = activate ? GetVent("SpecimenVent") : null;
                        GetVent("SpecimenVent")!.Right = activate ? GetVent("SubBathroomVent") : null;
                    }
                    break;
                case 4:
                    //Airship
                    GetVent("VaultVent")!.Right = activate ? GetVent("GaproomVent1") : null;
                    GetVent("GaproomVent1")!.Center = activate ? GetVent("VaultVent") : null;

                    GetVent("EjectionVent")!.Right = activate ? GetVent("KitchenVent") : null;
                    GetVent("KitchenVent")!.Center = activate ? GetVent("EjectionVent") : null;

                    GetVent("HallwayVent1")!.Center = activate ? GetVent("HallwayVent2") : null;
                    GetVent("HallwayVent2")!.Center = activate ? GetVent("HallwayVent1") : null;

                    GetVent("GaproomVent2")!.Center = activate ? GetVent("RecordsVent") : null;
                    GetVent("RecordsVent")!.Center = activate ? GetVent("GaproomVent2") : null;

                    if (GetVent("ElectricalVent") != null)
                    {
                        GetVent("MeetingVent")!.Left = activate ? GetVent("GaproomVent1") : null;

                        GetVent("ElectricalVent")!.Left = activate ? GetVent("MeetingVent") : null;
                        //GetVent("MeetingVent").Right = activate ? GetVent("ElectricalVent") : null;

                        GetVent("ShowersVent")!.Center = activate ? GetVent("ElectricalVent") : null;
                        GetVent("ElectricalVent")!.Right = activate ? GetVent("ShowersVent") : null;
                    }
                    break;
                case 5:
                    //Fungle
                    GetVent("NorthWestJungleVent")!.Center = activate ? GetVent("SouthWestJungleVent") : null;
                    GetVent("SouthWestJungleVent")!.Center = activate ? GetVent("NorthWestJungleVent") : null;

                    GetVent("NorthEastJungleVent")!.Center = activate ? GetVent("SouthEastJungleVent") : null;
                    GetVent("SouthEastJungleVent")!.Center = activate ? GetVent("NorthEastJungleVent") : null;

                    GetVent("StorageVent")!.Center = activate ? GetVent("CommunicationsVent") : null;
                    GetVent("CommunicationsVent")!.Center = activate ? GetVent("StorageVent") : null;

                    break;
            }
        }


        private bool isUsurped = false;
        void Usurp() => isUsurped = true;
        public override void OnActivated()
        {
            new Scripts.Draggable(MyPlayer).Register(new FunctionalLifespan(() => !IsDeadObject && !isUsurped));
            if (AmOwner)
            {
                EditVentInfo(true);

                acTokenChallenge = new("reaper.challenge", 0, (val, _) => val >= 5);
            }
        }

        [Local]
        void EditVentInfoOnGameStart(GameStartEvent ev) => EditVentInfo(true);

        void RuntimeAssignable.OnInactivated()
        {
            if (AmOwner) EditVentInfo(false);
        }

        [Local]
        void OnEnterVent(PlayerVentEnterEvent ev)
        {
            if (MyPlayer.HoldingAnyDeadBody)
            {
                acTokenCommon ??= new("reaper.common1");
                StatsVent.Progress();
            }
        }

        //キルのたびに加算、発見されるたびに減算してレポートされていない死体を計上する
        [Local]
        [OnlyMyPlayer]
        void AddChallengeTokenOnKillPlayer(PlayerKillPlayerEvent ev)
        {
            if (acTokenChallenge != null && !MeetingHud.Instance) acTokenChallenge.Value++;
        }

        [Local]
        void SubChallengeTokenOnReported(ReportDeadBodyEvent ev)
        {
            if(acTokenChallenge != null && (ev.Reported?.MyKiller?.AmOwner ?? false)) acTokenChallenge.Value--;
        }
    }
}
