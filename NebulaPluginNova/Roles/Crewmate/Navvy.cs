using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial;
using Virial.Game;
using Nebula.Map;

namespace Nebula.Roles.Crewmate;

internal class Navvy : DefinedRoleTemplate, DefinedRole
{

    private Navvy() : base("navvy", new(71, 93, 206), RoleCategory.CrewmateRole, Crewmate.MyTeam, [SealCoolDownOption, CostOption, CostForVentSealingOption, VentRemoveStepsOption, CostForDoorSealingOption, DoorRemoveStepsOption, RemoveDurationPerStepOption, RedundantSealingOption])
    {
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static public FloatConfiguration SealCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.navvy.sealCooldown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static public IntegerConfiguration CostOption = NebulaAPI.Configurations.Configuration("options.role.navvy.maxCost", (1, 30), 4);
    static public IntegerConfiguration CostForVentSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.costForVentSealing", (1, 5), 1);
    static public IntegerConfiguration CostForDoorSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.costForDoorSealing", (1, 5), 2);
    static public FloatConfiguration RemoveDurationPerStepOption = NebulaAPI.Configurations.Configuration("options.role.navvy.removeDuration", (float[])[0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 6f, 7f, 8f, 9f, 10f], 3f, FloatConfigurationDecorator.Second);
    static public IntegerConfiguration VentRemoveStepsOption = NebulaAPI.Configurations.Configuration("options.role.navvy.numOfVentRemovalSteps", (1, 4), 2);
    static public IntegerConfiguration DoorRemoveStepsOption = NebulaAPI.Configurations.Configuration("options.role.navvy.numOfDoorRemovalSteps", (1, 4), 2);
    static public BoolConfiguration RedundantSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.redundantSealing", true);
    static int[][] VisualSteps = [[3, 3, 3, 3, 4, 5, 6, 7], [3, 3, 3, 3, 4, 5, 6, 7], [1, 3, 3, 3, 4, 5, 6, 7], [0, 2, 3, 3, 4, 5, 6, 7], [0, 1, 2, 3, 4, 5, 6, 7]];
    static int[] VisualStandardSteps = [0, 1, 2, 3, 4, 5, 6, 7];

    static public Navvy MyRole = new Navvy();

    static private GameStatsEntry StatsSealVent = NebulaAPI.CreateStatsEntry("stats.navvy.sealVent", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsSealDoor = NebulaAPI.CreateStatsEntry("stats.navvy.sealDoor", GameStatsCategory.Roles, MyRole);
    static public GameStatsEntry StatsRemoveVent = NebulaAPI.CreateStatsEntry("stats.navvy.removeVent", GameStatsCategory.Roles, MyRole);
    static public GameStatsEntry StatsRemoveDoor = NebulaAPI.CreateStatsEntry("stats.navvy.removeDoor", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player) { }

        static private Image buttonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.CloseVentButton.png", 115f);
        static private Image sealImage = SpriteLoader.FromResource("Nebula.Resources.Seal.png", 100f);

        List<int> nextSealedVents = [];
        List<int> nextSealedDoors = [];
        void RuntimeAssignable.OnActivated() {
            UtilityInvalidationSystem.Instance.GraphicVentLevels = RedundantSealingOption ? VisualStandardSteps : VisualSteps[VentRemoveStepsOption];
            UtilityInvalidationSystem.Instance.GraphicDoorLevels = RedundantSealingOption ? VisualStandardSteps : VisualSteps[DoorRemoveStepsOption];

            if (AmOwner)
            {
                int left = CostOption;

                var tracker = Bind(ObjectTrackers.ForVents(0.8f, MyPlayer, 
                    RedundantSealingOption ? 
                    v => left >= CostForVentSealingOption && !nextSealedVents.Contains(v.Id) && (!v.TryGetComponent<InvalidVent>(out var invalidVent) || (invalidVent.Level + VentRemoveStepsOption < 8)) :
                    v => left >= CostForVentSealingOption && !nextSealedVents.Contains(v.Id) && !v.TryGetComponent<InvalidVent>(out _), MyRole.UnityColor, true));
                OpenableDoor? currentTargetDoor = null;
                var sealButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);

                var mapData = MapData.GetCurrentMapData();
                //近くの塞げるドアを探す
                sealButton.OnUpdate = (button) =>
                {
                    if (ShipStatus.Instance.AllDoors.Count == 0) return;
                    currentTargetDoor = null;

                    if (left < CostForDoorSealingOption) return;

                    float min = 10f;
                    var playerPos = MyPlayer.VanillaPlayer.transform.position;
                    var nearbyDoor = ShipStatus.Instance.AllDoors.MinBy(d =>
                    {
                        if (!d.IsOpen) return 100f;
                        if (nextSealedDoors.Contains(d.Id)) return 100f;
                        if (!mapData.IsSealableDoor(d)) return 100f;
                        if (UtilityInvalidationSystem.Instance.TryGetInvalidDoor(d, out var invalidDoor) && (!RedundantSealingOption || invalidDoor.Level + DoorRemoveStepsOption >= 8)) return 100f;
                        var distance = playerPos.Distance(d.transform.position);
                        if (distance < min) min = distance;
                        return distance;
                    });
                    if (min < 1f) currentTargetDoor = nearbyDoor;
                };
                sealButton.SetSprite(buttonImage.GetSprite());
                sealButton.Availability = (button) => MyPlayer.CanMove && (tracker.CurrentTarget != null || currentTargetDoor != null);
                sealButton.Visibility = (button) => !MyPlayer.IsDead && left > 0;
                var icon = sealButton.ShowUsesIcon(3);
                icon.text = left.ToString();
                sealButton.OnClick = (button) =>
                {
                    SpriteRenderer sealRenderer = null!;
                    if (tracker.CurrentTarget != null)
                    {
                        left -= CostForVentSealingOption;
                        nextSealedVents.Add(tracker.CurrentTarget!.Id);

                        //テープを設置
                        sealRenderer = UnityHelper.CreateObject<SpriteRenderer>("Seal", tracker.CurrentTarget.transform,
                        AmongUsUtil.CurrentMapId switch
                        {
                            5 => new(0.35f, -0.3f, -0.001f),
                            2 => new(0.35f, -0.1f, -0.001f),
                            _ => new(0.28f, -0.2f, -0.001f)
                        },
                         LayerExpansion.GetShipLayer());
                        sealRenderer.transform.localScale = Vector3.one * 1.15f;

                        StatsSealVent.Progress();
                    }
                    else if(currentTargetDoor != null)
                    {
                        left -= CostForDoorSealingOption;
                        nextSealedDoors.Add(currentTargetDoor!.Id);

                        //テープを設置
                        var isVert = InvalidDoor.IsVertDoor(currentTargetDoor);
                        sealRenderer = UnityHelper.CreateObject<SpriteRenderer>("Seal", ShipStatus.Instance.transform, Vector3.zero, LayerExpansion.GetShipLayer());
                        sealRenderer.transform.position = currentTargetDoor.transform.position + AmongUsUtil.CurrentMapId switch
                        {
                            5 => isVert ? new(0.25f, -1.0f, -0.001f) : new(0.35f, -0.6f, -0.001f),
                            4 => isVert ? new(0.25f, -0.7f, -0.001f) : new(0.55f, -0.5f, -0.001f),
                            _ => isVert ? new(0.2f, -0.7f, -0.001f) : new(0.48f, -0.45f, -0.001f)
                        };
                        float scale = 1.15f;
                        if (AmongUsUtil.CurrentMapId is 4) scale /= 0.7f;
                        sealRenderer.transform.localScale = Vector3.one * scale;

                        StatsSealDoor.Progress();
                    }

                    if (sealRenderer != null)
                    {
                        //テープの共通処理
                        sealRenderer.sprite = sealImage.GetSprite();
                        Bind(new GameObjectBinding(sealRenderer.gameObject));
                        GameOperatorManager.Instance?.Register<MeetingStartEvent>(ev =>
                        {
                            if (sealRenderer) GameObject.Destroy(sealRenderer.gameObject);
                        }, this);
                    }

                    icon.text = left.ToString();
                    new StaticAchievementToken("navvy.common1");

                    sealButton.StartCoolDown();
                };
                sealButton.OnMeeting = button => button.StartCoolDown();
                sealButton.CoolDownTimer = Bind(new Timer(SealCoolDownOption).SetAsAbilityCoolDown().Start());
                sealButton.SetLabel("seal");
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            nextSealedVents.Do(vent =>
            {
                UtilityInvalidationSystem.RpcAddVentLevel.Invoke((vent, VentRemoveStepsOption));
            });
            nextSealedVents.Clear();

            nextSealedDoors.Do(door =>
            {
                UtilityInvalidationSystem.RpcAddDoorLevel.Invoke((door, DoorRemoveStepsOption));

                if (ShipStatus.Instance.AllDoors.Find(d => d.Id == door, out var found))
                {
                    int notSealedDoor = 0,sealedDoor = 0;
                    ShipStatus.Instance.AllDoors.Do(d =>
                    {
                        if (d.Room != found.Room) return;
                        if (UtilityInvalidationSystem.Instance.TryGetInvalidDoor(d, out _)) sealedDoor++;
                        else notSealedDoor++;
                    });
                    if(notSealedDoor == 0 && sealedDoor >= 2) new StaticAchievementToken("navvy.challenge");
                }
            });
            nextSealedDoors.Clear();
        }

    }
}

