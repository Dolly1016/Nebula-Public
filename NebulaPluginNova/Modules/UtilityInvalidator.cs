using Il2CppInterop.Runtime.Injection;
using Nebula.Map;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules;

public class InvalidVent : MonoBehaviour
{
    static InvalidVent() => ClassInjector.RegisterTypeInIl2Cpp<InvalidVent>();
    /// <summary>
    /// 封鎖直前の画像を保持しておく
    /// </summary>
    public Sprite OriginalSprite { get; private set; }
    public Vent Vent { get; private set; }
    public int Level { get; private set; }
    public void Awake()
    {
        Vent = gameObject.GetComponent<Vent>();
        if (Vent.myRend.TryGetComponent<Animator>(out var anim)) anim.enabled = false;
        OriginalSprite = Vent.myRend.sprite;
    }

    public void SetLevel(int level, bool remove, int graphicLevel = -1)
    {
        Level = level;
        if (graphicLevel == -1) graphicLevel = UtilityInvalidationSystem.Instance.GraphicVentLevels[level];
        Vent.myRend.sprite = MapData.GetCurrentMapData().GetSealedVentSprite(Vent, graphicLevel, remove);
    }

    public void Reactivate()
    {
        Vent.myRend.sprite = OriginalSprite;
        if (Vent.myRend.TryGetComponent<Animator>(out var anim)) anim.enabled = true;
    }
}
public class InvalidDoor : MonoBehaviour
{
    static InvalidDoor() => ClassInjector.RegisterTypeInIl2Cpp<InvalidDoor>();
    
    public OpenableDoor Door { get; private set; }
    private PlainDoor? plainDoorCache;
    private MushroomWallDoor? mushroomWallDoorCache;
    public SpriteRenderer SealRenderer { get; private set; }
    public int Level { get; private set; }

    static public bool IsVertDoor(OpenableDoor door) => IsVertDoor(door.GetComponent<PlainDoor>(), door.GetComponent<MushroomWallDoor>());
    static private bool IsVertDoor(PlainDoor? plainDoor, MushroomWallDoor? mushroomWallDoor)
    {
        if (plainDoor != null)
            return plainDoor.myCollider.size.y > plainDoor.myCollider.size.x;
        else if (mushroomWallDoor != null)
            return mushroomWallDoor.wallCollider.size.y > mushroomWallDoor.wallCollider.size.x;
        return true;
    }

    private bool IsVert => IsVertDoor(plainDoorCache, mushroomWallDoorCache);

    public void Awake()
    {
        Door = gameObject.GetComponent<OpenableDoor>();
        plainDoorCache = Door.TryCast<PlainDoor>();
        mushroomWallDoorCache = Door.TryCast<MushroomWallDoor>();
        SealRenderer = UnityHelper.CreateObject<SpriteRenderer>("Seal", Door.transform, MapData.GetCurrentMapData().GetDoorSealingPos(Door, IsVert));

        Door.SetDoorway(true);
    }

    public void SetLevel(int level, bool remove, int graphicLevel = -1)
    {
        Level = level;
        if (graphicLevel == -1) graphicLevel = UtilityInvalidationSystem.Instance.GraphicVentLevels[level];
        SealRenderer.sprite = MapData.GetCurrentMapData().GetSealedDoorSprite(Door, graphicLevel, IsVert, remove);
    }

    void OnDestroy()
    {
        GameObject.Destroy(SealRenderer.gameObject);
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class UtilityInvalidationSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static UtilityInvalidationSystem() => DIManager.Instance.RegisterModule(()=> new UtilityInvalidationSystem());
    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);
    static public UtilityInvalidationSystem Instance { get; private set; }
    private Dictionary<int, InvalidVent> ventMap = [];
    private Dictionary<int, InvalidDoor> doorMap = [];

    /// <summary>
    /// 現在ターゲットしている無効なベント
    /// </summary>
    public InvalidVent? CurrentInvalidVent { get; internal set; } = null;

    /// <summary>
    /// 現在ターゲットしている無効なドア
    /// </summary>
    public InvalidDoor? CurrentInvalidDoor { get; internal set; } = null;
    private UtilityInvalidationSystem()
    {
        Instance = this;
    }

    public void InvalidateVent(Vent vent, int level, int graphicLevel = -1)
    {
        if (ventMap.ContainsKey(vent.Id)) return;

        var invalidator = vent.gameObject.AddComponent<InvalidVent>();
        ventMap[vent.Id] = invalidator;
        invalidator.SetLevel(level, false, graphicLevel);
    }

    public void ReactivateVent(InvalidVent vent)
    {
        var ventId = vent.Vent.Id;
        vent.Reactivate();
        GameObject.Destroy(vent);
        ventMap.Remove(ventId);
    }

    public void InvalidateDoor(OpenableDoor door, int level, int graphicLevel = -1)
    {
        if (doorMap.ContainsKey(door.Id)) return;

        var invalidator = door.gameObject.AddComponent<InvalidDoor>();
        doorMap[door.Id] = invalidator;
        invalidator.SetLevel(level, false, graphicLevel);
    }

    public void ReactivateDoor(InvalidDoor door)
    {
        var doorId = door.Door.Id;
        //door.Reactivate();
        GameObject.Destroy(door);
        doorMap.Remove(doorId);
    }

    public int[] GraphicVentLevels { get; set; } = [0, 1, 2, 3, 4, 5, 6, 7];
    public int[] GraphicDoorLevels { get; set; } = [0, 1, 2, 3, 4, 5, 6, 7];

    static private Image desealButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.VentSealImpostorButton.png", 115f);

    void OnGameStart(GameStartEvent ev)
    {
        var localPlayer = GamePlayer.LocalPlayer;
        var desealButton =  new Modules.ScriptComponents.ModAbilityButtonImpl(priority: -100).Register(NebulaAPI.CurrentGame!);
        desealButton.SetSprite(desealButtonSprite.GetSprite());
        desealButton.Availability = (button) => localPlayer.CanMove;
        desealButton.Visibility = (button) => (MyContainer.LocalPlayer?.Role?.CanUseVent ?? false) && (CurrentInvalidVent != null || CurrentInvalidDoor != null) && !localPlayer.IsDead;
        desealButton.OnClick = (button) => {
            button.ActivateEffect();
        };
        desealButton.OnEffectEnd = (button) =>
        {
            if ((CurrentInvalidVent == null && CurrentInvalidDoor == null) || !localPlayer.CanMove || localPlayer.IsDead) return;

            if (CurrentInvalidVent != null)
            {
                int nextLevel = CurrentInvalidVent.Level - 1;
                RpcUpdateVentLevel.Invoke((CurrentInvalidVent.Vent.Id, nextLevel, true, GraphicVentLevels.Get(nextLevel, -1)));
                Roles.Crewmate.Navvy.StatsRemoveVent.Progress();
            }else if(CurrentInvalidDoor != null)
            {
                int nextLevel = CurrentInvalidDoor.Level - 1;
                RpcUpdateDoorLevel.Invoke((CurrentInvalidDoor.Door.Id, nextLevel, true, GraphicDoorLevels.Get(nextLevel, -1)));
                Roles.Crewmate.Navvy.StatsRemoveDoor.Progress();
            }
            new StaticAchievementToken("navvy.common2");
            desealButton.StartCoolDown();
        };
        desealButton.OnUpdate = (button) => {
            if (!button.EffectActive) return;
            if (CurrentInvalidVent == null && CurrentInvalidDoor == null) button.InactivateEffect();
        };
        desealButton.EffectTimer = NebulaAPI.Modules.Timer(NebulaAPI.CurrentGame!, Roles.Crewmate.Navvy.RemoveDurationPerStepOption);
        desealButton.SetLabel("vent.seal.remove");
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        //現在選択している無効なドアを更新する。
        CurrentInvalidDoor = null;
        if (doorMap.Count > 0)
        {
            var pos = GamePlayer.LocalPlayer!.VanillaPlayer.transform.position;
            var nearbyDoor = doorMap.Values.MinBy(door => pos.Distance(door.transform.position));
            if (nearbyDoor != null && nearbyDoor.transform.position.Distance(pos) < 1f)
            {
                CurrentInvalidDoor = nearbyDoor;
            }
        }
    }

    public bool TryGetInvalidVent(Vent vent, [MaybeNullWhen(false)]out InvalidVent invalidVent) => ventMap.TryGetValue(vent.Id, out invalidVent);
    public bool TryGetInvalidDoor(OpenableDoor door, [MaybeNullWhen(false)]out InvalidDoor invalidDoor) => doorMap.TryGetValue(door.Id, out invalidDoor);

    public static RemoteProcess<(int ventId, int addition)> RpcAddVentLevel = new("AddInvalidVentLevel", (message, _) => {
        int level = message.addition;
        
        if (Instance!.ventMap.TryGetValue(message.ventId, out var invalidVent)) level += invalidVent.Level;
        else level--;

        level = Mathf.Clamp(level, 0, 7);
        RpcUpdateVentLevel?.LocalInvoke((message.ventId, level, false, -1));
    });
    public static RemoteProcess<(int ventId, int level, bool remove, int graphic)> RpcUpdateVentLevel = new("SetInvalidVentLevel", (message, _) =>
    {
        if(!Instance!.ventMap.TryGetValue(message.ventId, out var invalidVent))
        {
            //無効化したいかつ無効化されていない
            if (message.level < 0) return;


            var vent = ShipStatus.Instance.AllVents.FirstOrDefault(v => v.Id == message.ventId);
            if (vent == null) return;

            Instance.InvalidateVent(vent, message.level, message.graphic);
        }
        else
        {
            if (message.level < 0)
                Instance.ReactivateVent(invalidVent);
            else
                invalidVent.SetLevel(message.level, message.remove, message.graphic);
        }
    });

    public static RemoteProcess<(int doorId, int addition)> RpcAddDoorLevel = new("AddInvalidDoorLevel", (message, _) => {
        int level = message.addition;
        
        if (Instance!.doorMap.TryGetValue(message.doorId, out var invalidDoor)) level += invalidDoor.Level;
        else level--;

        level = Mathf.Clamp(level, 0, 7);
        RpcUpdateDoorLevel?.LocalInvoke((message.doorId, level, false, -1));
    });
    public static RemoteProcess<(int doorId, int level, bool remove, int graphic)> RpcUpdateDoorLevel = new("SetInvalidDoorLevel", (message, _) =>
    {
        if (!Instance!.doorMap.TryGetValue(message.doorId, out var invalidDoor))
        {
            //無効化したいかつ無効化されていない
            if (message.level < 0) return;

            var door = ShipStatus.Instance.AllDoors.FirstOrDefault(v => v.Id == message.doorId);
            if (door == null) return;

            Instance.InvalidateDoor(door, message.level, message.graphic);
        }
        else
        {
            if (message.level < 0)
                Instance.ReactivateDoor(invalidDoor);
            else
                invalidDoor.SetLevel(message.level, message.remove, message.graphic);
        }
    });
}

public static class VentHelpers
{
    public static bool IsInvalid(this Vent vent)
    {
        return vent.TryGetComponent<InvalidVent>(out _);
    }

    public static bool IsInvalid(this Vent vent, [MaybeNullWhen(false)]out InvalidVent invalidVent)
    {
        return vent.TryGetComponent<InvalidVent>(out invalidVent);
    }

    public static Vent? GetValidVent(this Vent vent)
    {
        if (!vent.IsInvalid()) return vent;
        if(vent.Left != null && !vent.Left.IsInvalid()) return vent.Left;
        if (vent.Right != null && !vent.Right.IsInvalid()) return vent.Right;
        if (vent.Center != null && !vent.Center.IsInvalid()) return vent.Center;
        return null;
    }
}
