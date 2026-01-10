using Nebula.Modules;
using Nebula.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace Nebula.Documents;

public class AssignableDocument : IDocumentWithId
{
    string documentId;
    DefinedAssignable? staticAssignable;
    IAssignableDocument? assignableDocumentCache;
    public AssignableDocument(DefinedAssignable? assignable)
    {
        this.staticAssignable = assignable;
        this.assignableDocumentCache = assignable as IAssignableDocument;
    }

    void IDocumentWithId.OnSetId(string documentId) { 
        this.documentId = documentId;
    }

    Virial.Media.GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        var gui = NebulaAPI.GUI;
        var abilityContents = assignableDocumentCache?.GetDocumentImages().ToArray() ?? [];
        var replacements = assignableDocumentCache?.GetDocumentReplacements().ToArray() ?? [];
        return
            RoleDocumentHelper.GetAssignableWidget(staticAssignable, replacements,
            (assignableDocumentCache?.HasWinCondition ?? false) ? RoleDocumentHelper.GetWinCondChapter(documentId, replacements) : null,
            (assignableDocumentCache?.HasAbility ?? false) ? RoleDocumentHelper.GetChapter($"{documentId}.ability", [
                RoleDocumentHelper.GetDocumentLocalizedText($"{documentId}.ability.main", replacements),
                ..abilityContents.Select(c => RoleDocumentHelper.GetImageLocalizedContent(c.Image, c.Content, replacements)),
                ]) : null,
            (assignableDocumentCache?.HasTips ?? false) ? RoleDocumentHelper.GetTipsChapter(documentId, replacements) : null,
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }
}


[NebulaPreprocess(Virial.Attributes.PreprocessPhase.FixStructure)]
public class DocumentLoader
{
    public static void Preprocess(NebulaPreprocessor preprocess)
    {
        foreach(var r in NebulaAPI.Assignables.AllAssignables)
        {
            if (DocumentManager.GetDocument("role." + r.InternalName) == null)
            {
                var doc = new AssignableDocument(r);
                (doc as IDocumentWithId).OnSetId("role." + r.InternalName);
                DocumentManager.Register("role." + r.InternalName, doc);
            }
        }
    }
}