using Virial.Attributes;
using Virial.Compat;
using Virial.Media;
using Virial;
using Virial.Assignable;
using Nebula.Roles;

namespace DefaultLang.Documents;

[AddonDocument("role.obsessional")]
public class ObsessionalDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var targetDie = RoleDocumentHelper.ConfigBool("options.role.obsessional.canWinEvenIfObsessionalTargetDie", "role.obsessional.winCond.canWinEvenIfTargetDie", "role.obsessional.winCond.cannotWinIfTargetDie");
        var selfDie = RoleDocumentHelper.ConfigBool("options.role.obsessional.canWinEvenIfObsessionalDie", "role.obsessional.winCond.canWinEvenIfSelfDie", "role.obsessional.winCond.cannotWinIfSelfDie");
        var suicide = RoleDocumentHelper.ConfigBool("options.role.obsessional.obsessionalSuicideWhenObsessionalTargetDie", "role.obsessional.tips.suicide");
        return RoleDocumentHelper.GetModifierWidget("obsessional",
            RoleDocumentHelper.GetWinCondChapter("role.obsessional", _ => targetDie.Replace("#SELF", selfDie)),
            RoleDocumentHelper.GetTipsChapter("role.obsessional", text => text.Replace("#SUICIDE", suicide)),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}

[AddonDocument("role.lover")]
public class LoverDocument : IDocument
{
    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var winCondAdd = RoleDocumentHelper.ConfigBool("options.role.lover.allowExtraWin", "role.lover.winCond.additional");
        var tips = RoleDocumentHelper.ConfigBool("options.role.lover.avengerMode", "role.lover.tips.standard", "role.lover.tips.avenger");
        var impostorChance = RoleDocumentHelper.Config<int>("options.role.lover.chanceOfAssigningImpostors");
        var assignment = NebulaAPI.Language.Translate(impostorChance == "0" ? "role.lover.tips.assignment.noImpostor" : "role.lover.tips.assignment.impostor").Replace("#NUM", impostorChance);
        return RoleDocumentHelper.GetModifierWidget("lover",
            RoleDocumentHelper.GetWinCondChapter("role.lover", str => str.Replace("#ADD", winCondAdd)),
            RoleDocumentHelper.GetTipsChapter("role.dancer", str => tips + "<br>" + assignment),
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}