using Virial;
using Virial.Events.Game.Meeting;
using Virial.Text;

namespace Nebula.Listeners;

internal partial class NebulaGameEventListeners
{
    [EventPriority(-1)]
    void NonCrewmateCanSeeSabotage(MeetingStartEvent ev)
    {
        var localPlayer = GamePlayer.LocalPlayer;
        if (localPlayer == null) return;
        bool canSeeSaboStatus = GeneralConfigurations.NonCrewmateCanSeeSabotageStatusOption.GetValue() switch
        {
            1 => localPlayer.IsImpostor,
            2 => !localPlayer.FeelBeTrueCrewmate,
            _ => false
        };

        if (canSeeSaboStatus)
        {
            bool inCommSab = AmongUsUtil.InCommSab;
            bool inElecSab = AmongUsUtil.InElecSab;
            if (inCommSab || inElecSab)
            {
                NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayTitle, "metaInfo.meeting.sabotage"),
                    inCommSab ? GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayContent, " -" + Language.Translate("metaInfo.meeting.sabotage.communications")) : null,
                    inElecSab ? GUI.API.RawText(Virial.Media.GUIAlignment.Left, AttributeAsset.OverlayContent, " -" + Language.Translate("metaInfo.meeting.sabotage.electrical")) : null
                    ), MeetingOverlayHolder.IconsSprite[1], new(200, 60, 60), true);
            }
        }
    }
}

