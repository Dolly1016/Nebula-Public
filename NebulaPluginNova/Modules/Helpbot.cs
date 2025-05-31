using Nebula.Modules.GUIWidget;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Virial.Text;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Modules;

/*
internal static class RestAPIHelpers
{
    async public static Task<T?> GetRequestAsync<T>(string url, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        var param = await new FormUrlEncodedContent(parameters).ReadAsStringAsync();
        var requestUrl = $"{url}?{param}";
        var response = await NebulaPlugin.HttpClient.GetAsync(requestUrl).ConfigureAwait(false);
        return JsonStructure.Deserialize<T>(await response.Content.ReadAsStringAsync());

    }
}

internal static class Helpbot
{
    internal class HelpbotAnswer
    {
        [JsonSerializableField]
        public string answer = "";
        public string DecodedAnswer => HttpUtility.UrlDecode(answer);
    }
    static private MetaScreen? LastHelpbotScreen = null;
    public static void TryOpenHelpbotScreen()
    {
        if (!LastHelpbotScreen) OpenHelpbotWindow();
    }
    static public void OpenHelpbotWindow()
    {
        var parent = HudManager.InstanceExists ? HudManager.Instance.transform : null;
        var window = MetaScreen.GenerateWindow(new(6f, 1.9f), parent, Vector3.zero, true, false, true);
        LastHelpbotScreen = window;

        var textField = new GUITextField(Virial.Media.GUIAlignment.Center, new(5.75f, 0.85f)) { IsSharpField = false, WithMaskMaterial = true, MaxLines = 3, FontSize = 1.5f };
        var fieldArtifact = textField.Artifact;

        TextAttribute answerAttr = new(GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentStandard)) { Size = new(6.8f, 2f), FontSize = new(1.2f, false), IsFlexible = true, Wrapping = true };
        window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
            GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, GUI.API.GetAttribute(Virial.Text.AttributeAsset.DocumentStandard), "ヘルプボット ※開発中につき回答の精度が低いです。ご了承ください。"),
            textField, GUI.API.RawButton(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.CenteredBoldFixed), "尋ねる", _ =>
            {
                //window.CloseScreen();
                //var answerWindow = MetaScreen.GenerateWindow(new(6f, 2.8f), parent, Vector3.zero, true, false, true);

                string question = fieldArtifact.First().Text.Replace('\r', '\n');

                window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "質問: ".Bold() + question),
                    GUI.API.VerticalMargin(0.08f),
                    GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "回答: ".Bold() + "ヘルプボットが回答中です...")
                    ), out var _);

                IEnumerator CoAnswer()
                {
                    var answer = RestAPIHelpers.GetRequestAsync<HelpbotAnswer>("http://168.138.44.249:22020/question/", [new("query", HttpUtility.HtmlEncode(question)!), new("options", "### 現在のオプション\n現在のオプションの設定値です。回答の参考にしてください。\nスナイパー: 出現する")]);
                    yield return answer.WaitAsCoroutine();

                    if (answer.Result == null)
                    {
                        window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                        GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "質問: ".Bold() + question),
                        GUI.API.VerticalMargin(0.08f),
                        GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "回答: ".Bold() + "回答できませんでした。日ごとの質問できる回数の上限に達した可能性があります。")
                        ), out var _);
                    }
                    else
                    {
                        var decodedAnswer = answer.Result.DecodedAnswer;
                        if (decodedAnswer.StartsWith("<b>") && decodedAnswer.EndsWith("</b>") && decodedAnswer.Length > 8 && !decodedAnswer.Substring(3).Contains("<b>")) decodedAnswer = decodedAnswer.Substring(3, decodedAnswer.Length - 7);

                        if (window)
                        {
                            window.SetWidget(GUI.API.ScrollView(Virial.Media.GUIAlignment.Center, new(6f, 1.9f), null,
                                GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                                GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "質問: ".Bold() + question),
                                GUI.API.VerticalMargin(0.08f),
                                GUI.API.RawText(Virial.Media.GUIAlignment.TopLeft, answerAttr, "回答: ".Bold() + decodedAnswer)
                                ), out var _), out var _);
                        }
                    }
                }

                NebulaManager.Instance.StartCoroutine(CoAnswer().WrapToIl2Cpp());

            })), new Vector2(0.5f, 1f), out _);
        fieldArtifact.FirstOrDefault()?.GainFocus();
    }
}

*/
