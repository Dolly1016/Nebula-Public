using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Attributes;
using Virial.Compat;
using Virial.Media;

namespace DefaultLang.Documents;

[AddonDocument("role.dancer")]
public class DisturberDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        return
            RoleDocumentHelper.GetRoleWidget("disturber",
            RoleDocumentHelper.GetChapter("role.disturber.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText("role.disturber.ability.main"),
                RoleDocumentHelper.GetImageLocalizedContent("Buttons.ElecPolePlaceButton.png", "role.disturber.ability.place"),
                RoleDocumentHelper.GetImageLocalizedContent("Buttons.DisturbButton.png", "role.disturber.ability.disturb")
                ]),
            RoleDocumentHelper.GetChapter("role.disturber.tips"),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}
