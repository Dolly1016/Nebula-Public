using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Bridge;

internal class BHudManager
{
    public UseButton UseButton { get; }
    public ReportButton ReportButton { get; }
    public KillButton KillButton { get; }
    public SabotageButton SabotageButton { get; }
    public VentButton ImpostorVentButton { get; }
    public PassiveButton MapButton { get; }

    public Transform UseButtonParentTransform { get; }
    public Transform MapButtonTransform { get; }

    public Dictionary<ImageNames, UseButtonSettings> UseButtonVanillaSettings;

    public GameObject UseButtonObj { get; }
    public GameObject UseButtonParentObj { get; }
    public GameObject PetButtonObj { get; }
    public GameObject ReportButtonObj { get; }
    public GameObject KillButtonObj { get; }
    public GameObject SabotageButtonObj { get; }
    public GameObject ImpostorVentButtonObj { get; }
    public GameObject MapButtonObj { get; }

    public GameObject TaskPanelObj { get; }
    public GameObject RoomTrackerObj { get; }
    public ChatController Chat { get; }
    public GameObject ShadowQuadObj { get; }

    public BHudManager(HudManager hud)
    {
        this.UseButton = hud.UseButton;
        this.ReportButton = hud.ReportButton;
        this.KillButton = hud.KillButton;
        this.SabotageButton = hud.SabotageButton;
        this.ImpostorVentButton = hud.ImpostorVentButton;
        this.MapButton = hud.MapButton;

        this.UseButtonParentTransform = this.UseButton.transform.parent;
        this.MapButtonTransform = this.MapButton.transform;

        this.UseButtonObj = this.UseButton.gameObject;
        this.UseButtonParentObj = this.UseButtonParentTransform.gameObject;
        this.PetButtonObj = hud.PetButton.gameObject;
        this.ReportButtonObj = this.ReportButton.gameObject;
        this.KillButtonObj = this.KillButton.gameObject;
        this.SabotageButtonObj = this.SabotageButton.gameObject;
        this.ImpostorVentButtonObj = this.ImpostorVentButton.gameObject;
        this.MapButtonObj = this.MapButton.gameObject;

        this.UseButtonVanillaSettings = [];
        foreach (var entry in this.UseButton.UseSettings.GetFastEnumerator()) this.UseButtonVanillaSettings[entry.ButtonType] = entry;

        this.TaskPanelObj = hud.TaskPanel.gameObject;
        this.RoomTrackerObj = hud.roomTracker.gameObject;
        this.TaskPanelObj = hud.TaskPanel.gameObject;
        this.Chat = hud.Chat;
        this.ShadowQuadObj = hud.ShadowQuad.gameObject;
    }
}
