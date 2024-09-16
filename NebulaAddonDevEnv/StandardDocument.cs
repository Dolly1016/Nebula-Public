using Nebula.Modules;
using Nebula.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Attributes;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace DefaultLang.Documents;

[AddonDocument("role.alien", RoleType.Role, (string[])["Buttons.EMIButton.png;role.alien.ability.emi", "Buttons.AlienButton.png;role.alien.ability.invalidate"], false, true)]
[AddonDocument("role.effacer", RoleType.Role, (string[])["Buttons.EffaceButton.png;role.effacer.ability.efface"], false, true)]
[AddonDocument("role.evilTrapper", RoleType.Role, (string[])["AccelTrap.png;role.trapper.ability.accelTrap", "DecelTrap.png;role.trapper.ability.decelTrap", "KillTrap.png;role.trapper.ability.killTrap"], false, true)]
[AddonDocument("role.niceTrapper", RoleType.Role, (string[])["AccelTrap.png;role.trapper.ability.accelTrap", "DecelTrap.png;role.trapper.ability.decelTrap", "CommTrap.png;role.trapper.ability.commTrap"], false, true)]
[AddonDocument("role.disturber", RoleType.Role, (string[])["Buttons.ElecPolePlaceButton.png;role.disturber.ability.place", "Buttons.DisturbButton.png;role.disturber.ability.disturb"], false, false)]
[AddonDocument("role.camouflager", RoleType.Role, (string[])["Buttons.CamoButton.png;role.camouflager.ability.camo"], false, false)]
[AddonDocument("role.cannon", RoleType.Role, (string[])["Buttons.MarkButton.png;role.cannon.ability.mark", "Buttons.CannonButton.png;role.cannon.ability.cannon"], false, false)]
[AddonDocument("role.hadar", RoleType.Role, (string[])["Buttons.HadarHideButton.png;role.hadar.ability.dive", "Buttons.HadarAppearButton.png;role.hadar.ability.gush"], false, true)]
[AddonDocument("role.illusioner", RoleType.Role, (string[])["Buttons.SampleButton.png;role.illusioner.ability.sample", "Buttons.MorphButton.png;role.illusioner.ability.morph", "Buttons.PaintButton.png;role.illusioner.ability.paint"], false, false)]
[AddonDocument("role.marionette", RoleType.Role, (string[])["Buttons.DecoyButton.png;role.marionette.ability.place", "Buttons.DecoyDestroyButton.png;role.marionette.ability.destroy", "Buttons.DecoyMonitorButton.png;role.marionette.ability.monitor", "Buttons.DecoySwapButton.png;role.marionette.ability.swap"], false, true)]
[AddonDocument("role.morphing", RoleType.Role, (string[])["Buttons.SampleButton.png;role.morphing.ability.sample", "Buttons.MorphButton.png;role.morphing.ability.morph"], false, false)]
[AddonDocument("role.painter", RoleType.Role, (string[])["Buttons.SampleButton.png;role.painter.ability.sample", "Buttons.PaintButton.png;role.painter.ability.paint"], false, false)]
[AddonDocument("role.raider", RoleType.Role, (string[])["Buttons.AxeButton.png;role.raider.ability.axe"], false, true)]
[AddonDocument("role.thurifer", RoleType.Role, (string[])["Buttons.ThuriferButton.png;role.thurifer.ability.curse", "Buttons.ThuriferButton.png;role.thurifer.ability.impute"], false, false)]
[AddonDocument("role.ubiquitous", RoleType.Role, (string[])["Buttons.DroneButton.png;role.ubiquitous.ability.drone", "Buttons.DroneCallBackButton.png;role.ubiquitous.ability.callBack", "Buttons.DroneHackButton.png;role.ubiquitous.ability.doorHack"], false, true)]
[AddonDocument("role.extraMission", RoleType.Modifier, (string[])[], true, false)]
[AddonDocument("role.confused", RoleType.Modifier, (string[])[], false, false)]
public class StandardDocument : IDocumentWithId
{
    string documentId;
    bool withTips;
    bool withWinCond;
    string[][] abilityContents;
    RoleType roleType;
    public StandardDocument(RoleType roleType, string[] abilityContents, bool withWinCond, bool withTips)
    {
        this.roleType = roleType;
        this.withTips = withTips;
        this.withWinCond = withWinCond;
        this.abilityContents = abilityContents.Select(str => str.Split(";")).ToArray();
    }

    void IDocumentWithId.OnSetId(string documentId) { 
        this.documentId = documentId;
    }

    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        return
            RoleDocumentHelper.GetAssignableWidget(roleType, documentId.Split('.', 2).Last(),
            withWinCond ? RoleDocumentHelper.GetWinCondChapter(documentId) : null,
            abilityContents.Length > 0 ? RoleDocumentHelper.GetChapter($"{documentId}.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText($"{documentId}.ability.main"),
                ..abilityContents.Select(c => RoleDocumentHelper.GetImageLocalizedContent(c[0], c[1])),
                ]) : null,
            withTips ? RoleDocumentHelper.GetTipsChapter(documentId) : null,
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

public abstract class AbstractAssignableDocument : IDocumentWithId
{
    public string DocumentId { get; private set; }
    void IDocumentWithId.OnSetId(string documentId) => DocumentId = documentId;

    public virtual bool WithWinCond => false;
    public virtual GUIWidget GetCustomWinCondWidget() => RoleDocumentHelper.GetWinCondChapter(DocumentId);
    public virtual GUIWidget? GetTipsWidget() => null;
    public virtual IEnumerable<GUIWidget> GetAbilityWidget() { yield break; }
    public abstract RoleType RoleType { get; }
    public virtual string BuildAbilityText(string original) => original;
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var tipsWidget = GetTipsWidget();
        var abilityWidget = GetAbilityWidget().ToArray();
        var gui = NebulaAPI.GUI;
        return
            RoleDocumentHelper.GetAssignableWidget(RoleType, DocumentId.Split('.', 2).Last(),
            WithWinCond ? GetCustomWinCondWidget() : null,
            abilityWidget.Length > 0 ? RoleDocumentHelper.GetChapter($"{DocumentId}.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText($"{DocumentId}.ability.main", BuildAbilityText),
                ..abilityWidget,
                ]) : null,
            tipsWidget != null ? RoleDocumentHelper.GetChapter("document.tips", [tipsWidget]) : null,
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

[NebulaPreprocess(Virial.Attributes.PreprocessPhase.FixStructure)]
public class DocumentLoader
{
    public static void Preprocess(NebulaPreprocessor preprocess)
    {
        foreach(var r in Nebula.Roles.Roles.AllRoles)
        {
            if (DocumentManager.GetDocument("role." + r.InternalName) == null)
            {
                var doc = new StandardDocument(RoleType.Role, [], false, false);
                (doc as IDocumentWithId).OnSetId("role." + r.InternalName);
                DocumentManager.Register("role." + r.InternalName, doc);
            }
        }
        foreach (var r in Nebula.Roles.Roles.AllModifiers)
        {
            if (DocumentManager.GetDocument("role." + r.InternalName) == null)
            {
                var doc = new StandardDocument(RoleType.Modifier, [], false, false);
                (doc as IDocumentWithId).OnSetId("role." + r.InternalName);
                DocumentManager.Register("role." + r.InternalName, doc);
            }
        }
        foreach (var r in Nebula.Roles.Roles.AllGhostRoles)
        {
            if (DocumentManager.GetDocument("role." + r.InternalName) == null)
            {
                var doc = new StandardDocument(RoleType.GhostRole, [], false, false);
                (doc as IDocumentWithId).OnSetId("role." + r.InternalName);
                DocumentManager.Register("role." + r.InternalName, doc);
            }
        }
    }
}