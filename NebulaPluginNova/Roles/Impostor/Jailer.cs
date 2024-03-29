﻿using Nebula.Behaviour;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Jailer : ConfigurableStandardRole
{
    static public Jailer MyRole = new Jailer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "jailer";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration CanMoveWithMapWatchingOption = null!;
    private NebulaConfiguration CanIdentifyDeadBodiesOption = null!;
    private NebulaConfiguration CanIdentifyImpostorsOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanMoveWithMapWatchingOption = new NebulaConfiguration(RoleConfig, "canMoveWithMapWatching", null, false, false);
        CanIdentifyDeadBodiesOption = new NebulaConfiguration(RoleConfig, "canIdentifyDeadBodies", null, false, false);
        CanIdentifyImpostorsOption = new NebulaConfiguration(RoleConfig, "canIdentifyImpostors", null, false, false);
    }

    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        AchievementToken<bool>? acTokenCommon = null;
        AchievementToken<int>? acTokenChallenge = null;

        void IGameEntity.OnOpenNormalMap()
        {
            if (AmOwner)
            {
                MapBehaviour.Instance.countOverlay.gameObject.SetActive(true);
                MapBehaviour.Instance.countOverlay.SetModOption(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, false);
                MapBehaviour.Instance.countOverlay.SetOptions(true, true);
                ConsoleTimer.MarkAsNonConsoleMinigame();

                MapBehaviour.Instance.taskOverlay.Hide();
            }
        }

        void IGameEntity.OnOpenSabotageMap()
        {
            if (AmOwner)
            {
                MapBehaviour.Instance.countOverlay.gameObject.SetActive(true);
                MapBehaviour.Instance.countOverlay.SetModOption(MyRole.CanIdentifyImpostorsOption, MyRole.CanIdentifyDeadBodiesOption, false, Palette.ImpostorRed);
                MapBehaviour.Instance.countOverlay.SetOptions(true, true);
                ConsoleTimer.MarkAsNonConsoleMinigame();

                MapBehaviour.Instance.taskOverlay.Hide();

                MapBehaviour.Instance.countOverlayAllowsMovement = MyRole.CanMoveWithMapWatchingOption;
                if (!MapBehaviour.Instance.countOverlayAllowsMovement) PlayerControl.LocalPlayer.NetTransform.Halt();

                acTokenCommon ??= new("jailer.common1", false, (val, _) => val);
            }
        }

        void IGamePlayerEntity.OnKillPlayer(GamePlayer target)
        {
            if (AmOwner)
            {
                if (acTokenCommon != null) acTokenCommon.Value = true;

                if (acTokenChallenge != null)
                {
                    var pos = PlayerControl.LocalPlayer.GetTruePosition();
                    Collider2D? room = null;
                    foreach (var entry in ShipStatus.Instance.FastRooms)
                    {
                        if (entry.value.roomArea.OverlapPoint(pos))
                        {
                            room = entry.value.roomArea;
                            break;
                        }
                    }

                    if (room != null)
                    {
                        if (Helpers.AllDeadBodies().Any(d => d.ParentId != target.PlayerId && room.OverlapPoint(d.TruePosition)))
                        {
                            acTokenChallenge!.Value++;
                        }
                    }
                }
            }

        }
        protected override void OnInactivated()
        {
            if (AmOwner)
            {
                if (MapBehaviour.Instance) GameObject.Destroy(MapBehaviour.Instance.gameObject);
            }
        }

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                if (MapBehaviour.Instance) GameObject.Destroy(MapBehaviour.Instance.gameObject);

                acTokenChallenge = new("jailer.challenge", 0, (val, _) => val >= 2);
            }
        }

        void IGameEntity.OnMapInstantiated()
        {
            if (AmOwner)
            {
                Transform roomNames;
                if (AmongUsUtil.CurrentMapId == 0) roomNames = MapBehaviour.Instance.transform.FindChild("RoomNames (1)");
                else roomNames = MapBehaviour.Instance.transform.FindChild("RoomNames");

                OptimizeMap(roomNames, MapBehaviour.Instance.countOverlay, MapBehaviour.Instance.infectedOverlay);
            }
        }
    }

    public static void OptimizeMap(Transform roomNames, MapCountOverlay countOverlay, InfectedOverlay infectedOverlay)
    {
        for (int i = 0; i < infectedOverlay.transform.childCount; i++) infectedOverlay.transform.GetChild(i).transform.localScale *= 0.8f;
        foreach (var c in countOverlay.CountAreas) c.YOffset *= -1f;

        switch (AmongUsUtil.CurrentMapId)
        {
            case 0:
                OptimizeMapSkeld(roomNames, countOverlay, infectedOverlay);
                break;
            case 1:
                OptimizeMapMira(roomNames, countOverlay, infectedOverlay);
                break;
            case 2:
                OptimizeMapPolus(roomNames, countOverlay, infectedOverlay);
                break;
            case 4:
                OptimizeMapAirship(roomNames, countOverlay, infectedOverlay);
                break;
        }
    }

    private static void OptimizeMapSkeld(Transform roomNames, MapCountOverlay countOverlay, InfectedOverlay infectedOverlay)
    {
        roomNames.GetChild(13).localPosition += new Vector3(0f, 0.1f, 0f);

        infectedOverlay.transform.GetChild(4).localPosition += new Vector3(0.07f, 0.2f, 0f);

        countOverlay.transform.GetChild(0).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(1).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(2).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(3).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(4).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(5).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(9).localPosition += new Vector3(0f, -0.55f, 0f);
        countOverlay.transform.GetChild(10).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(12).localPosition += new Vector3(0f, -0.75f, 0f);
        countOverlay.transform.GetChild(13).localPosition += new Vector3(0f, -0.42f, 0f);
    }

    private static void OptimizeMapMira(Transform roomNames, MapCountOverlay countOverlay, InfectedOverlay infectedOverlay)
    {
        roomNames.GetChild(2).localPosition += new Vector3(0f, 0.15f, 0f);
        roomNames.GetChild(7).localPosition += new Vector3(0f, 0.15f, 0f);

        infectedOverlay.transform.GetChild(0).localPosition += new Vector3(0f, 0.24f, 0f);
        infectedOverlay.transform.GetChild(1).localPosition += new Vector3(0.45f, 0.5f, 0f);
        infectedOverlay.transform.GetChild(2).localPosition += new Vector3(-0.1f, 0.43f, 0f);
        infectedOverlay.transform.GetChild(3).localPosition += new Vector3(0.6f, 0.25f, 0f);

        countOverlay.transform.GetChild(3).localPosition += new Vector3(0f, -0.2f, 0f);
        countOverlay.transform.GetChild(5).localPosition += new Vector3(0f, -0.6f, 0f);
        countOverlay.transform.GetChild(9).localPosition += new Vector3(0f, -0.45f, 0f);
        countOverlay.transform.GetChild(10).localPosition += new Vector3(0.05f, -0.3f, 0f);
    }

    private static void OptimizeMapPolus(Transform romeNames, MapCountOverlay countOverlay, InfectedOverlay infectedOverlay)
    {
        romeNames.GetChild(1).localPosition += new Vector3(0f, 0.35f, 0f);
        romeNames.GetChild(3).localPosition += new Vector3(0f, 0.35f, 0f);
        romeNames.GetChild(7).localPosition += new Vector3(0f, 0.35f, 0f);

        infectedOverlay.transform.GetChild(0).localPosition += new Vector3(0f, 0.4f, 0f);
        infectedOverlay.transform.GetChild(1).localPosition += new Vector3(-1f, 0.4f, 0f);
        infectedOverlay.transform.GetChild(3).localPosition += new Vector3(0.6f, 0.3f, 0f);
        infectedOverlay.transform.GetChild(4).localPosition += new Vector3(-0.5f, 0.3f, 0f);
        infectedOverlay.transform.GetChild(5).localPosition += new Vector3(0f, 0.28f, 0f);
        infectedOverlay.transform.GetChild(6).localPosition += new Vector3(0f, 0.4f, 0f);

        countOverlay.transform.GetChild(0).localPosition += new Vector3(0f, 0.1f, 0f);
        countOverlay.transform.GetChild(1).localPosition += new Vector3(0.55f, -0.9f, 0f);
        countOverlay.transform.GetChild(2).localPosition += new Vector3(0f, -0.1f, 0f);
        countOverlay.transform.GetChild(3).localPosition += new Vector3(0f, -0.2f, 0f);
        countOverlay.transform.GetChild(4).localPosition += new Vector3(0.0f, 0f, 0f);
        countOverlay.transform.GetChild(5).localPosition += new Vector3(0.0f, -0.15f, 0f);
        countOverlay.transform.GetChild(6).localPosition += new Vector3(0.0f, -0.15f, 0f);
        countOverlay.transform.GetChild(7).localPosition += new Vector3(0.0f, 0f, 0f);
        countOverlay.transform.GetChild(8).localPosition += new Vector3(0.0f, -0.15f, 0f);
        countOverlay.transform.GetChild(9).localPosition += new Vector3(0f, 0.1f, 0f);
        countOverlay.transform.GetChild(10).localPosition += new Vector3(0f, -0.1f, 0f);
    }

    private static void OptimizeMapAirship(Transform romeNames, MapCountOverlay countOverlay, InfectedOverlay infectedOverlay)
    {
        romeNames.GetChild(0).localPosition += new Vector3(0f, 0.2f, 0f);
        romeNames.GetChild(2).localPosition += new Vector3(0f, 0.2f, 0f);
        romeNames.GetChild(3).localPosition += new Vector3(0f, 0.25f, 0f);
        romeNames.GetChild(8).localPosition += new Vector3(0f, 0.3f, 0f);
        romeNames.GetChild(11).localPosition += new Vector3(0f, 0.1f, 0f);
        romeNames.GetChild(15).localPosition += new Vector3(0f, 0.1f, 0f);

        infectedOverlay.transform.GetChild(0).localPosition += new Vector3(0f, -0.15f, 0f);
        infectedOverlay.transform.GetChild(1).localPosition += new Vector3(-0.12f, 0.35f, 0f);
        infectedOverlay.transform.GetChild(2).localPosition += new Vector3(0f, 0.15f, 0f);
        infectedOverlay.transform.GetChild(3).localPosition += new Vector3(0f, 0.15f, 0f);
        infectedOverlay.transform.GetChild(4).localPosition += new Vector3(0.02f, 0.3f, 0f);
        infectedOverlay.transform.GetChild(5).localPosition += new Vector3(0.06f, 0.12f, 0f);
        infectedOverlay.transform.GetChild(6).localPosition += new Vector3(0f, 0.35f, 0f);
        infectedOverlay.transform.GetChild(7).localPosition += new Vector3(0f, 0.25f, 0f);

        countOverlay.transform.GetChild(2).localPosition += new Vector3(-0.2f, -0.4f, 0f);
        countOverlay.transform.GetChild(3).localPosition += new Vector3(0.05f, -0.2f, 0f);
        countOverlay.transform.GetChild(5).localPosition += new Vector3(0.06f, -0.25f, 0f);
        countOverlay.transform.GetChild(6).localPosition += new Vector3(0f, -0.28f, 0f);
        countOverlay.transform.GetChild(16).localPosition += new Vector3(0.15f, -0.3f, 0f);
        countOverlay.transform.GetChild(17).localPosition += new Vector3(-0.1f, -0.5f, 0f);
    }
}
