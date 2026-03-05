using LibCpp2IL.Elf;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Nebula.UI.MainMenu;

internal class MigrationUI
{
    internal static void OpenMigrationWindow()
    {
        Virial.Media.GUIWidget ButtonContent(string actionName, Action onClick) => GUI.API.HorizontalHolder(GUIAlignment.Left,
            GUI.API.LocalizedButton(GUIAlignment.Center, Virial.Text.AttributeAsset.CenteredBoldFixed, "ui.migration.buttons." + actionName, _ => onClick.Invoke()),
            GUI.API.LocalizedText(GUIAlignment.Center, Virial.Text.AttributeAsset.OverlayContent, "ui.migration.buttons." + actionName + ".detail"),
            GUI.API.VerticalMargin(0.6f)
            );
        var window = MetaScreen.GenerateWindow(new(6.2f, 1.8f), null, new(0f, 0f, -200f), false, true, true, BackgroundSetting.Old, true);
        window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
            GUI.API.LocalizedText(GUIAlignment.Center, Virial.Text.AttributeAsset.OverlayTitle, "ui.migration.title"),
            GUI.API.VerticalMargin(0.1f),
            ButtonContent("upload", UploadData),
            ButtonContent("restore", CheckAndDownloadData)
            ), new Vector2(0.5f, 1f), out _);
    }

    private static void UploadData()
    {
        if (!EOSManager.InstanceExists)
        {
            MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.notLogIn"));
            return;
        }

        static void OnGetPassword(string password)
        {
            var window = MetaScreen.GenerateWindow(new(3.9f, 1.9f), null, new(0f, 0f, -350f), true, false, true, BackgroundSetting.Old, false);
            window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "ui.migration.upload.title"),
                GUI.API.LocalizedText(GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.migration.upload.success"),
                GUI.API.VerticalMargin(0.12f),
                GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), password.Sized(150).Bold()),
                GUI.API.VerticalMargin(0.12f),
                GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), MetaUI.ConfirmTranslationKey, _ => window.CloseScreen())
                ), out _);
        }

        static void OnFailedToUpload()
        {
            MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.upload.failed"));
        }

        Migration.CoUploadData(OnGetPassword, OnFailedToUpload).StartOnScene();
    }

    private static void CheckAndDownloadData()
    {
        if (!EOSManager.InstanceExists)
        {
            MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.notLogIn"));
            return;
        }

        var inputWindow = MetaScreen.GenerateWindow(new(4f, 1.9f), null, new Vector3(0f, 0f, -300f), true, true, true, BackgroundSetting.Old, false);
        GUITextField passwordField = new(GUIAlignment.Center, new(3.4f, 0.42f))
        {
            HintText = Language.Translate("ui.migration.download.password").Color(Color.gray),
            IsSharpField = false,
            MaxLines = 1,
            WithMaskMaterial = false
        };
        inputWindow.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
            GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "ui.migration.download.title"),
            GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.migration.download.content"),
            passwordField,
            GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "ui.migration.download.proceed", unused =>
            {
                var password = passwordField.Artifact.FirstOrDefault()?.Text ?? "";
                inputWindow.CloseScreen();

                Migration.CoCheckData(password, (response, time) =>
                {
                    switch(response)
                    {
                        case Migration.CheckResponseType.Error:
                            MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.download.failed"));
                            break;
                        case Migration.CheckResponseType.Found:
                            var window = MetaScreen.GenerateWindow(new(4.8f, 2f), null, new(0f, 0f, -350f), true, false, true);
                            window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                                GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "ui.migration.download.confirm.title"),
                                GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "ui.migration.download.confirm.content"),
                                GUI.API.VerticalMargin(0.15f),
                                GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), Language.Translate("ui.migration.download.confirm.date").Replace("%YEAR%", time!.Value.Year.ToString()).Replace("%MONTH%", time!.Value.Month.ToString()).Replace("%DAY%", time!.Value.Day.ToString()).Replace("%HOUR%", time!.Value.Hour.ToString()).Replace("%MINUTE%", time!.Value.Minute.ToString("D2"))),
                                GUI.API.VerticalMargin(0.15f),
                                GUI.API.HorizontalHolder(GUIAlignment.Center,
                                    GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "ui.migration.download.confirm.button.cancel", _ =>
                                    {
                                        window.CloseScreen();
                                    }),
                                    GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "ui.migration.download.confirm.button.confirm", _ =>
                                    {
                                        DownloadData(response, time, password);
                                        window.CloseScreen();
                                    })
                                    )
                                ), new Vector2(0.5f, 1f), out _);
                            break;
                        case Migration.CheckResponseType.NotFound:
                            MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.download.notFound"));
                            break;
                    }
                }).StartOnScene();
            
            }
            )), new Vector2(0.5f, 1f), out _);
    }

    private static void DownloadData(Migration.CheckResponseType responseType, DateTimeOffset? time, string password)
    {
        Migration.CoDownloadData(password, (response) =>
        {
            if (response == Migration.CheckResponseType.Error)
            {
                MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.download.failed"));
            }
            else
            {
                MetaUI.ShowConfirmDialog(null, GUI.API.LocalizedTextComponent("ui.migration.download.success"), () => Application.Quit(0));
            }
        }).StartOnScene();
    }
}
