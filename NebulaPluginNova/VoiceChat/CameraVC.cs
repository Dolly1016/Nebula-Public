using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game.Meeting;
using Virial.Game;

namespace Nebula.VoiceChat;

[NebulaRPCHolder]
internal class CameraVC : IGameOperator
{
    public static void Activate(params (Vector2 pos, int id)[] cameras)
    {
        new CameraVC();
        var room = ModSingleton<NoSVCRoom>.Instance;
        if (room == null) return;
        foreach (var c in cameras)
        {
            var component = new CameraTerminalVCComponent(c.id, c.pos);
            room.AddVirtualSpeaker(component);
            room.AddVirtualMicrophone(component);
        }
    }

    public CameraVC()
    {
        this.Register(NebulaAPI.CurrentGame!);
        ModSingleton<CameraVC>.Instance = this;
    }

    void OnMeetingStart(MeetingPreStartEvent ev)
    {
        cameraUser.Clear();
        ClearLocalCamera();
    }

    void OnMeetingStart(MeetingStartEvent ev)
    {
        cameraUser.Clear();
        ClearLocalCamera();
    }

    void IGameOperator.OnReleased() => ModSingleton<CameraVC>.Instance = null!;

    void ClearLocalCamera()
    {
        if (localCamera != null)
        {
            ModSingleton<NoSVCRoom>.Instance?.RemoveVirtualSpeaker(localCamera);
            localCamera = null;
        }
    }

    public void UseCamera(GamePlayer player, int cameraId)
    {
        if (player.AmOwner)
        {
            currentMyCameraId = cameraId;
            if(cameraId == -1)
            {
                ClearLocalCamera();
            }
            else
            {
                if (localCamera == null)
                {
                    localCamera = new CameraControllerVCComponent();
                    ModSingleton<NoSVCRoom>.Instance.RemoveVirtualSpeaker(localCamera);
                }
            }
        }


        if (cameraId == -1 && cameraUser.ContainsKey(player.PlayerId))
        {
            cameraUser.Remove(player.PlayerId);
            return;
        }
        cameraUser[player.PlayerId] = cameraId;
    }

    int currentMyCameraId = -1;
    public int CurrentCamera => currentMyCameraId;
    Dictionary<int, int> cameraUser = [];
    CameraControllerVCComponent? localCamera = null;
    public bool AmWatchingCamera => currentMyCameraId != -1;
    public int GetWatchingCamera(GamePlayer player)
    {
        if (cameraUser.TryGetValue(player.PlayerId, out var cam)) return cam;
        return -1;
    }

    static public readonly RemoteProcess<(GamePlayer player, int id)> RpcUseCamera = new("UseCamera", (message, _) =>
    {
        ModSingleton<CameraVC>.Instance?.UseCamera(message.player, message.id);
    });
}

internal class CameraTerminalVCComponent : IVoiceComponent
{
    public int Id { get; private set; }
    private Vector2 position { get; set; }
    public float Radious => 3f;

    public float Volume => 1f;

    public Vector2 Position => position;

    public CameraTerminalVCComponent(int id, Vector2 position)
    {
        Id = id;
        this.position = position;
    }

    bool IVoiceComponent.CanPlaySoundFrom(IVoiceComponent mic)
    {
        if (AmongUsUtil.InCommSab) return false;
        if (ModSingleton<CameraVC>.Instance.AmWatchingCamera)
            return false;
        else
            return mic == this;
    }

    public float CanCatch(GamePlayer player, Vector2 position)
    {
        if (ModSingleton<CameraVC>.Instance.AmWatchingCamera)
        {
            float dis = position.Distance(Position);
            if (dis < Radious) return 1f - dis / Radious;
            return 0f;
        }
        else
        {
            return ModSingleton<CameraVC>.Instance.GetWatchingCamera(player) == Id ? 1f : 0f;
        }
    }
}

internal class CameraControllerVCComponent : IVoiceComponent
{
    float IVoiceComponent.Radious => 1f;

    float IVoiceComponent.Volume => 1f;

    Vector2 IVoiceComponent.Position => GamePlayer.LocalPlayer?.Position ?? new(0f, 0f);

    bool IVoiceComponent.CanPlaySoundFrom(IVoiceComponent mic) => !AmongUsUtil.InCommSab && mic is CameraTerminalVCComponent terminal && terminal.Id == ModSingleton<CameraVC>.Instance.CurrentCamera;
}
