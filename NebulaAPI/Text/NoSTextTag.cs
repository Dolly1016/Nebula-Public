using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Virial.Text;

public static class PlayerStates
{
    public static CommunicableTextTag Alive { get; internal set; } = null!;
    public static CommunicableTextTag Dead { get; internal set; } = null!;
    public static CommunicableTextTag Exiled { get; internal set; } = null!;
    public static CommunicableTextTag Misfired { get; internal set; } = null!;
    public static CommunicableTextTag Sniped { get; internal set; } = null!;
    public static CommunicableTextTag Beaten { get; internal set; } = null!;
    public static CommunicableTextTag Guessed { get; internal set; } = null!;
    public static CommunicableTextTag Misguessed { get; internal set; } = null!;
    public static CommunicableTextTag Embroiled { get; internal set; } = null!;
    public static CommunicableTextTag Suicide { get; internal set; } = null!;
    public static CommunicableTextTag Trapped { get; internal set; } = null!;
    public static CommunicableTextTag Revived { get; internal set; } = null!;
    public static CommunicableTextTag Pseudocide { get; internal set; } = null!;
}

public static class EventDetails
{
    public static CommunicableTextTag Kill { get; internal set; } = null!;
    public static CommunicableTextTag Exiled { get; internal set; } = null!;
    public static CommunicableTextTag Misfire { get; internal set; } = null!;
    public static CommunicableTextTag GameStart { get; internal set; } = null!;
    public static CommunicableTextTag GameEnd { get; internal set; } = null!;
    public static CommunicableTextTag MeetingEnd { get; internal set; } = null!;
    public static CommunicableTextTag Report { get; internal set; } = null!;
    public static CommunicableTextTag BaitReport { get; internal set; } = null!;
    public static CommunicableTextTag EmergencyButton { get; internal set; } = null!;
    public static CommunicableTextTag Disconnect { get; internal set; } = null!;
    public static CommunicableTextTag Revive { get; internal set; } = null!;
    public static CommunicableTextTag Eat { get; internal set; } = null!;
    public static CommunicableTextTag Clean { get; internal set; } = null!;
    public static CommunicableTextTag Missed { get; internal set; } = null!;
    public static CommunicableTextTag Guess { get; internal set; } = null!;
    public static CommunicableTextTag Embroil { get; internal set; } = null!;
    public static CommunicableTextTag Trap { get; internal set; } = null!;
    public static CommunicableTextTag Accident { get; internal set; } = null!;
}