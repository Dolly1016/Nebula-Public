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

internal class Navvy : DefinedSingleAbilityRoleTemplate<Navvy.Ability>, DefinedRole
{

    private Navvy() : base("navvy", new(71, 93, 206), RoleCategory.CrewmateRole, Crewmate.MyTeam, [SealCoolDownOption, CostOption, CostForVentSealingOption, VentRemoveStepsOption, CostForDoorSealingOption, DoorRemoveStepsOption, RemoveDurationPerStepOption, RedundantSealingOption])
    {
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, CostOption));

    static public readonly FloatConfiguration SealCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.navvy.sealCooldown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static public readonly IntegerConfiguration CostOption = NebulaAPI.Configurations.Configuration("options.role.navvy.maxCost", (1, 30), 4);
    static public readonly IntegerConfiguration CostForVentSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.costForVentSealing", (1, 5), 1);
    static public readonly IntegerConfiguration CostForDoorSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.costForDoorSealing", (1, 5), 2);
    static public readonly FloatConfiguration RemoveDurationPerStepOption = NebulaAPI.Configurations.Configuration("options.role.navvy.removeDuration", (float[])[0.5f, 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f, 4.5f, 5f, 6f, 7f, 8f, 9f, 10f], 3f, FloatConfigurationDecorator.Second);
    static public readonly IntegerConfiguration VentRemoveStepsOption = NebulaAPI.Configurations.Configuration("options.role.navvy.numOfVentRemovalSteps", (1, 4), 2);
    static public readonly IntegerConfiguration DoorRemoveStepsOption = NebulaAPI.Configurations.Configuration("options.role.navvy.numOfDoorRemovalSteps", (1, 4), 2);
    static public readonly BoolConfiguration RedundantSealingOption = NebulaAPI.Configurations.Configuration("options.role.navvy.redundantSealing", true);
    static readonly int[][] VisualSteps = [[3, 3, 3, 3, 4, 5, 6, 7], [3, 3, 3, 3, 4, 5, 6, 7], [1, 3, 3, 3, 4, 5, 6, 7], [0, 2, 3, 3, 4, 5, 6, 7], [0, 1, 2, 3, 4, 5, 6, 7]];
    static readonly int[] VisualStandardSteps = [0, 1, 2, 3, 4, 5, 6, 7];

    static public Navvy MyRole = new();

    static private readonly GameStatsEntry StatsSealVent = NebulaAPI.CreateStatsEntry("stats.navvy.sealVent", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsSealDoor = NebulaAPI.CreateStatsEntry("stats.navvy.sealDoor", GameStatsCategory.Roles, MyRole);
    static public readonly GameStatsEntry StatsRemoveVent = NebulaAPI.CreateStatsEntry("stats.navvy.removeVent", GameStatsCategory.Roles, MyRole);
    static public readonly GameStatsEntry StatsRemoveDoor = NebulaAPI.CreateStatsEntry("stats.navvy.removeDoor", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private readonly Image buttonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.CloseVentButton.png", 115f);
        static private readonly Image sealImage = SpriteLoader.FromResource("Nebula.Resources.Seal.png", 100f);

        List<int> nextSealedVents = [];
        List<int> nextSealedDoors = [];
        int leftTapes = CostOption;
        public Ability(GamePlayer player, bool isUsurped, int left) : base(player, isUsurped)
        {
            UtilityInvalidationSystem.Instance.GraphicVentLevels = RedundantSealingOption ? VisualStandardSteps : VisualSteps[VentRemoveStepsOption];
            UtilityInvalidationSystem.Instance.GraphicDoorLevels = RedundantSealingOption ? VisualStandardSteps : VisualSteps[DoorRemoveStepsOption];

            leftTapes = left;
            if (AmOwner)
            {
                var tracker = ObjectTrackers.ForVents(0.8f, MyPlayer, 
                    RedundantSealingOption ? 
                    v => leftTapes >= CostForVentSealingOption && !nextSealedVents.Contains(v.Id) && (!v.TryGetComponent<InvalidVent>(out var invalidVent) || (invalidVent.Level + VentRemoveStepsOption < 8)) :
                    v => leftTapes >= CostForVentSealingOption && !nextSealedVents.Contains(v.Id) && !v.TryGetComponent<InvalidVent>(out _), MyRole.UnityColor, true).Register(this);
                OpenableDoor? currentTargetDoor = null;
                var sealButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    SealCoolDownOption, "seal", buttonImage,
                    _ => tracker.CurrentTarget != null || currentTargetDoor != null, _ => leftTapes > 0);

                var mapData = MapData.GetCurrentMapData();
                //近くの塞げるドアを探す
                sealButton.OnUpdate = (button) =>
                {
                    if (ShipStatus.Instance.AllDoors.Count == 0) return;
                    currentTargetDoor = null;

                    if (leftTapes < CostForDoorSealingOption) return;

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
                sealButton.ShowUsesIcon(3, leftTapes.ToString());
                sealButton.OnClick = (button) =>
                {
                    SpriteRenderer sealRenderer = null!;
                    if (tracker.CurrentTarget != null)
                    {
                        leftTapes -= CostForVentSealingOption;
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
                        leftTapes -= CostForDoorSealingOption;
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
                        this.BindGameObject(sealRenderer.gameObject);
                        GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev =>
                        {
                            if (sealRenderer) GameObject.Destroy(sealRenderer.gameObject);
                        }, this);
                    }

                    button.UpdateUsesIcon(leftTapes.ToString());
                    new StaticAchievementToken("navvy.common1");

                    sealButton.StartCoolDown();
                };
                sealButton.SetAsUsurpableButton(this);
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

