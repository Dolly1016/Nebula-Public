using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Media;
using Virial.Runtime;

namespace Nebula.Documents;

public class AssignableDocument : IDocumentWithId
{
    static private string GetAbilityPieceTag(int index) => "ability" + index;
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
                ..abilityContents.Select((c, index) => RoleDocumentHelper.GetImageLocalizedContent(c.Image, c.Content, replacements).MarkCenterIf(GetAbilityPieceTag(index))),
                ]) : null,
            (assignableDocumentCache?.HasTips ?? false) ? RoleDocumentHelper.GetTipsChapter(documentId, replacements) : null,
            RoleDocumentHelper.GetConfigurationCaption()
            );
    }

    private IEnumerable<DocumentPiece> GetPieces()
    {
        var abilityContents = assignableDocumentCache?.GetDocumentImages().ToArray() ?? [];
        var replacements = assignableDocumentCache?.GetDocumentReplacements().ToArray() ?? [];

        if(staticAssignable != null) yield return new(RoleDocumentHelper.HeaderPieceTag, [staticAssignable.GeneralBlurb, staticAssignable.DisplayName], ()=> RoleDocumentHelper.GetAssignableNameWidget(staticAssignable, replacements)!, this);

        if (assignableDocumentCache?.HasWinCondition ?? false) yield return new(RoleDocumentHelper.WinCondPieceTag, [RoleDocumentHelper.GetWinCondText(documentId, replacements)], () => RoleDocumentHelper.GetWinCondChapter(documentId, replacements), this);
        if (assignableDocumentCache?.HasTips ?? false) yield return new(RoleDocumentHelper.TipsPieceTag, [RoleDocumentHelper.GetTipsText(documentId, replacements)], () => RoleDocumentHelper.GetTipsChapter(documentId, replacements), this);
        if (assignableDocumentCache?.HasAbility ?? false)
        {
            int index = 0;
            foreach(var contents in assignableDocumentCache?.GetDocumentImages() ?? [])
            {
                yield return new(GetAbilityPieceTag(index), [RoleDocumentHelper.GetDocumentLocalizedTextForSearch(contents.Content, replacements)], () => RoleDocumentHelper.GetImageLocalizedContent(contents.Image, contents.Content, replacements), this);
                index++;
            }
        }
    }
    IEnumerable<DocumentPiece> IDocument.Pieces => GetPieces();
    DefinedAssignable? IDocument.RelatedAssignable => staticAssignable;
    Image? IDocument.Illustlation => staticAssignable?.ConfigurationHolder?.Illustration;
    bool IDocument.CanBeShown => staticAssignable?.ShowOnHelpScreen ?? false;
}


[NebulaPreprocess(Virial.Attributes.PreprocessPhase.FixStructure)]
public class DocumentLoader
{
    public static void Preprocess(NebulaPreprocessor preprocess)
    {
        foreach(var r in NebulaAPI.Assignables.AllAssignables)
        {
            if (DocumentManager.GetAssignableDocument(r) == null)
            {
                var doc = new AssignableDocument(r);
                (doc as IDocumentWithId).OnSetId(DocumentManager.GetAssignableDocumentId(r));
                DocumentManager.Register("role." + r.InternalName, doc);
            }
        }
    }
}