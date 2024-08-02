using Virial;
using Virial.Compat;
using Virial.Media;
using Virial.Text;
using Virial.Attributes;
using System.Collections.Generic;
using Nebula.Player;

namespace DefaultLang.Documents;

[AddonDocument("role.dancer")]
public class DancerDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var winCondNum = RoleDocumentHelper.Config<int>("options.role.dancer.numOfSuccessfulForecastToWin");
        var lastDance = RoleDocumentHelper.ConfigBool("options.role.dancer.finalDance","role.dancer.winCond.main.lastDance");
        return RoleDocumentHelper.GetRoleWidget("dancer",
            RoleDocumentHelper.GetWinCondChapter("role.dancer", str => str.Replace("#NUM", winCondNum).Replace("#ADD", lastDance)),
            RoleDocumentHelper.GetChapter("role.dancer.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText("role.dancer.ability.main"),
                RoleDocumentHelper.GetImageLocalizedContent("Buttons.DanceButton.png", "role.dancer.ability.dance"),
                RoleDocumentHelper.GetImageLocalizedContent("Buttons.DanceKillButton.png", "role.dancer.ability.danceKill")
                ]),
            RoleDocumentHelper.GetTipsChapter("role.dancer"),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

[AddonDocument("role.paparazzo")]
public class PaparazzoDocument : AbstractAssignableDocument
{
    public override IEnumerable<GUIWidget> GetAbilityWidget()
    { 
        yield return RoleDocumentHelper.GetImageLocalizedContent("Buttons.CameraButton.png", "role.paparazzo.ability.camera");
    }

    public override bool WithWinCond => true;
    public override GUIWidget GetCustomWinCondWidget() {
        var subject = RoleDocumentHelper.Config<int>("options.role.paparazzo.requiredSubjects");
        var enclosed = RoleDocumentHelper.Config<int>("options.role.paparazzo.requiredDisclosed");
        return RoleDocumentHelper.GetWinCondChapter(DocumentId, str => str.Replace("#SUBJECT", subject).Replace("#ENCLOSED", enclosed));
    }
    public override RoleType RoleType => RoleType.Role;
}