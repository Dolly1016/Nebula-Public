using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Nebula.Game;
using Nebula.Modules;
using Nebula.Modules.GUIWidget;
using Virial;
using Virial.Events.Lobby;
using Virial.Media;
using Virial.Text;

namespace Nebula.Online;

static internal class MakeOnlineLobby
{
    public static void SetUpButtons(PassiveButton publicButton, PassiveButton privateButton)
    {
        privateButton.OnClick = new();
        privateButton.OnClick.AddListener(() =>
        { 
            OpenSetUpUI();
            VanillaAsset.PlaySelectSE();
        });
        publicButton.OnClick = new();
        publicButton.OnClick.AddListener(() =>
        {
            PublicRoomService.Unpublish();
            VanillaAsset.PlaySelectSE();
        });
    }

    private const int MaxTitleLength = 40;

    // 掲載を頼んでから、公開になったと知らされるのを待つ上限 (秒)
    private const float PublishTimeoutSeconds = 5f;

    public static void OpenSetUpUI()
    {
        MetaScreen window = MetaScreen.GenerateWindow(new(5f, 2.5f), HudManager.Instance.transform, VVector3.Zero, true, true, true, BackgroundSetting.Modern);
        MetaScreen? subWindow = null;

        // 画面を組み直すと入力欄の中身が消えるので、直前の内容をここに控える
        string enteredTitle = "";

        GUIWidget Header() => GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.DocumentTitle, "ui.onlineLobby.title");

        // 経過を伝えるだけの画面
        void ShowProgress(string translationKey)
        {
            if (window.AsBoolFast()) window.CloseScreen();

            subWindow = MetaUI.ShowConfirmDialog(HudManager.Instance.transform, GUI.API.LocalizedTextComponent(translationKey));
        }

        void ShowResult(string translationKey)
        {
            if (window.AsBoolFast()) window.CloseScreen();
            if (subWindow.AsBoolFast()) subWindow!.CloseScreen();

            MetaUI.ShowConfirmDialog(HudManager.Instance.transform, GUI.API.LocalizedTextComponent(translationKey));
        }

        void ShowInput(string? errorKey)
        {
            if (!window) return;

            GUITextField field = null!;

            void Submit()
            {
                var text = (field.Artifact.FirstOrDefault()?.Text ?? "").Trim();
                if (text.Length == 0)
                {
                    ShowInput("ui.onlineLobby.emptyName");
                    return;
                }
                enteredTitle = text.Length > MaxTitleLength ? text.Substring(0, MaxTitleLength) : text;

                switch (PublicRoomService.CheckPublishable())
                {
                    case PublicRoomService.PublishRefusal.NotConnected:
                        ShowResult("ui.onlineLobby.notConnected");
                        return;
                    case PublicRoomService.PublishRefusal.NotHost:
                        ShowResult("ui.onlineLobby.notHost");
                        return;
                    case PublicRoomService.PublishRefusal.AuthNotRequired:
                        // 掲載には部屋主の認証が要る。まずこの部屋を認証必須にする
                        NoSAuth.SetAuthRequired(true);
                        break;
                }

                ShowProgress("ui.onlineLobby.authenticating");
                NebulaManager.Instance.StartCoroutine(CoPublish(window, enteredTitle, ShowResult).WrapToIl2Cpp());
            }

            field = new GUITextField(GUIAlignment.Center, new(3.9f, 0.4f))
            {
                IsSharpField = false,
                FontSize = 1.5f,
                MaxLines = 1,
                GainFocus = true,
                DefaultText = enteredTitle,
                HintText = errorKey != null ? Language.Translate(errorKey).Color(VColor.Red.RGBMultiplied(0.6f)) : Language.Translate("ui.onlineLobby.roomNameHint").Color(Color.gray),
                EnterAction = _ => { Submit(); return true; }
            };

            window.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                Header(),
                GUI.API.VerticalMargin(0.15f),
                GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.DocumentStandard, "ui.onlineLobby.guide"),
                GUI.API.VerticalMargin(0.2f),
                GUI.API.LocalizedText(GUIAlignment.Left, AttributeAsset.DocumentBold, "ui.onlineLobby.roomName"),
                field,
                GUI.API.VerticalMargin(0.2f),
                new GUIModernButton(GUIAlignment.Center, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.onlineLobby.publish"))
                {
                    OnClick = _ => Submit()
                }
                ), new VVector2(0.5f, 0.5f), out _);
        }

        ShowInput(null);
    }

    /// <summary>ホストの認証を待ってから掲載を依頼する。</summary>
    private static IEnumerator CoPublish(MetaScreen window, string title, Action<string> onResult)
    {
        var wait = 4f;

        while (!NoSAuth.LocalEntry.IsVerified)
        {
            if (wait < 0f)
            {
                onResult.Invoke("ui.onlineLobby.authTimeout");
                yield break;
            }
            yield return Effects.Wait(0.2f);
            wait -= 0.2f;
        }

        // 掲載

        wait = PublishTimeoutSeconds;

        var becamePublic = false;
        GameOperatorManager.Instance?.SubscribeSingleListener<LobbyChangeToPublicHostEvent>(_ => becamePublic = true, new FunctionalLifespan(() => wait > 0f && !becamePublic));

        yield return null;

        if (PublicRoomService.Publish(title, Language.GetCurrentLanguageId()) != PublicRoomService.PublishRefusal.None)
        {
            onResult.Invoke("ui.onlineLobby.failed");
            yield break;
        }

        while (!becamePublic)
        {
            if (wait < 0f)
            {
                onResult.Invoke("ui.onlineLobby.failed");
                yield break;
            }
            yield return Effects.Wait(0.1f);
            wait -= 0.1f;
        }

        onResult.Invoke("ui.onlineLobby.published");
    }
}
