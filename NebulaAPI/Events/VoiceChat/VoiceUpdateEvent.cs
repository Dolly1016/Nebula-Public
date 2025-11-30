using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Player;

namespace Virial.Events.VoiceChat;

public class VoiceUpdateEvent : AbstractPlayerEvent
{
    public bool InMeeting { get; }
    public float NormalVolume { get; set; }
    public float NormalPan { get; set; }
    public float GhostVolume { get; set; }
    public float RadioVolume { get; set; }
    public float DroneVolume { get; set; }
    public float DronePan { get; set; }

    internal VoiceUpdateEvent(Virial.Game.Player player, bool inMeeting, float normalVolume, float normalPan, float ghostVolume, float radioVolume, float droneVolume, float dronePan) : base(player)
    {
        InMeeting = inMeeting;
        NormalVolume = normalVolume;
        NormalPan = normalPan;
        GhostVolume = ghostVolume;
        RadioVolume = radioVolume;
        DroneVolume = droneVolume;
        DronePan = dronePan;
    }
}
