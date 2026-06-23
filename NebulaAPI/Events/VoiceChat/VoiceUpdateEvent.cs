using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;
using Virial.Events.Player;

namespace Virial.Events.VoiceChat;

[RecyclableEvent]
public class VoiceUpdateEvent : AbstractPlayerEvent
{
    public bool InMeeting { get; private set; }
    public float NormalVolume { get; set; }
    public float NormalPan { get; set; }
    public float GhostVolume { get; set; }
    public float RadioVolume { get; set; }
    public float DroneVolume { get; set; }
    public float DronePan { get; set; }

    private VoiceUpdateEvent() : base(null!) { }
    static private VoiceUpdateEvent ev = new();
    static internal VoiceUpdateEvent Get(Virial.Game.Player player, bool inMeeting, float normalVolume, float normalPan, float ghostVolume, float radioVolume, float droneVolume, float dronePan)
    {
        ev.Recycle(player);
        ev.InMeeting = inMeeting;
        ev.NormalVolume = normalVolume;
        ev.NormalPan = normalPan;
        ev.GhostVolume = ghostVolume;
        ev.RadioVolume = radioVolume;
        ev.DroneVolume = droneVolume;
        ev.DronePan = dronePan;
        return ev;
    }
}
