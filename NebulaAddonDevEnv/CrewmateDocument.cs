using Nebula.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Attributes;
using Virial.Media;

namespace DefaultLang.Documents;

[AddonDocument("role.collator")]
public class CollatorDocument : AbstractAssignableDocument
{
    public override GUIWidget? GetTipsWidget()
    {
        return RoleDocumentHelper.GetDocumentLocalizedText(DocumentId + ".tips");
    }

    public override string BuildAbilityText(string original)
    {
        var selective = RoleDocumentHelper.ConfigBool("options.role.collator.selectiveCollating", "role.collator.ability.selective", "role.collator.ability.nonSelective");
        return original.Replace("#SELECTIVE", selective);
        
    }
    public override IEnumerable<GUIWidget> GetAbilityWidget()
    {
        var tubes = RoleDocumentHelper.Config<int>("options.role.collator.numOfTubes");
        yield return RoleDocumentHelper.GetImageLocalizedContent("Buttons.CollatorSampleButton.png", "role.collator.ability.sample", str => str.Replace("#NUM", tubes));

        var meeting = RoleDocumentHelper.ConfigBool("options.role.collator.selectiveCollating", "role.collator.ability.meeting");
        if(meeting.Length > 0) yield return RoleDocumentHelper.GetImageContent("CollatorIcon.png", meeting);
    }
    public override RoleType RoleType => RoleType.Role;
}

[AddonDocument("role.justice")]
public class JusticeDocument : AbstractAssignableDocument
{
    public override GUIWidget? GetTipsWidget()
    {
        return RoleDocumentHelper.GetDocumentLocalizedText(DocumentId + ".tips");
    }

    public override string BuildAbilityText(string original)
    {
        var pickUpMe = RoleDocumentHelper.ConfigBool("options.role.justice.putJusticeOnTheBalance", "role.justice.ability.pickJustice");
        var select = RoleDocumentHelper.ConfigBoolRaw("options.role.justice.putJusticeOnTheBalance", "1", "2");
        return original.Replace("#PICKME", pickUpMe).Replace("#NUM", select);

    }
    public override IEnumerable<GUIWidget> GetAbilityWidget()
    {
        yield return RoleDocumentHelper.GetImageLocalizedContent("JusticeIcon.png", "role.justice.ability.meeting");
    }
    public override RoleType RoleType => RoleType.Role;
}