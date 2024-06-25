using Virial;
using Virial.Compat;
using Virial.Media;
using Virial.Text;
using Virial.Attributes;

namespace DefaultLang.Documents;

[AddonDocument("role.dancer")]
public class DancerDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var winCondNum = NebulaAPI.Configurations.GetSharableVariable<int>("options.role.dancer.numOfSuccessfulForecastToWin")?.Value.ToString() ?? "ERROR";
        var lastDance = (NebulaAPI.Configurations.GetSharableVariable<bool>("options.role.dancer.finalDance")?.Value ?? false) ? NebulaAPI.Language.Translate("role.dancer.winCond.main.lastDance") : "";
        return gui.VerticalHolder(GUIAlignment.Left, 
            RoleDocumentHelper.GetRoleNameWidget("dancer"), 
            RoleDocumentHelper.GetChapter("role.dancer.winCond", str => str.Replace("#NUM",winCondNum).Replace("#ADD",lastDance)),
            RoleDocumentHelper.GetChapter("role.dancer.ability", [
            	RoleDocumentHelper.GetDocumentLocalizedText("role.dancer.ability.main"),
            	RoleDocumentHelper.GetImageLocalizedContent("Buttons.DanceButton.png", "role.dancer.ability.dance"),
            	RoleDocumentHelper.GetImageLocalizedContent("Buttons.DanceKillButton.png", "role.dancer.ability.danceKill")
            	]),
            RoleDocumentHelper.GetChapter("role.dancer.tips"),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}