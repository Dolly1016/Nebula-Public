using Nebula.Documents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Compat;
using Virial.Configuration;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Configuration;

internal class ConfigurationDocument : IDocument
{
    IConfigurationHolder myHolder;
    public ConfigurationDocument(IConfigurationHolder myHolder)
    {
        this.myHolder = myHolder;
    }

    IEnumerable<DocumentPiece> GetPieces()
    {
        string extraText = "";
        if (CanEditNow)
        {
            extraText = ("<br><br>" + Language.Translate("help.search.config")).Color(VColor.Gray);
        }
            
        int index = 0;
        foreach(var c in myHolder.Configurations)
        {
            if (c.IsShown)
            {
                yield return new DocumentPiece(RoleDocumentHelper.GetOptionPieceTag(index), [c.GetDisplayText() ?? "", myHolder.RelatedAssignable?.DisplayName ?? ""], () => GUI.API.RawText(GUIAlignment.Left, AttributeAsset.DocumentBold, (c.GetDisplayText() ?? "") + extraText), this);
            }
            index++;
        }
        
    }
    IEnumerable<DocumentPiece> IDocument.Pieces => GetPieces();

    DefinedAssignable? IDocument.RelatedAssignable => myHolder.RelatedAssignable;
    string? IDocument.CustomTitle =>  myHolder.RelatedAssignable == null ? myHolder.Title.GetString() : null;
    Image? IDocument.Illustlation => myHolder.Illustration;
    float? IDocument.RequiredWidth => 8f;
    bool CanBeShown => myHolder.IsShown;

    bool CanEditNow => AmongUsClient.Instance.AmHost && (LobbyBehaviour.Instance || GeneralConfigurations.CurrentGameMode == GameModes.FreePlay);

    GUIWidget? IDocument.Build(Artifact<GUIScreen>? target)
    {
        if (CanEditNow)
        {
            List<GUIWidget> widgets = [GUI.API.Text(GUIAlignment.Left, AttributeAsset.DocumentTitle, myHolder.Title)];
            int index = 0;
            foreach(var config in myHolder.Configurations)
            {
                if (config.IsShown) widgets.Add(config.GetEditor().Invoke().MarkCenterIf(RoleDocumentHelper.GetOptionPieceTag(index)));
                index++;
            }
            var widget = GUI.API.VerticalHolder(GUIAlignment.Left, widgets);
            ConfigurationsAPI.API.SetUpdateAction(() => target.Do(screen =>
            {
                List<GUIWidget> newWidgets = [GUI.API.Text(GUIAlignment.Left, AttributeAsset.DocumentTitle, myHolder.Title), ..myHolder.Configurations.Where(c => c.IsShown).Select(c => c.GetEditor().Invoke())];
                screen.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Left, newWidgets), out _);
            }));
            return widget;
        }
        else
        {
            if (myHolder.RelatedAssignable != null)
            {
                var doc = DocumentManager.GetAssignableDocument(myHolder.RelatedAssignable);
                if (doc != null) return doc.Build(target);
            }
            return null;
        }
    }
}
