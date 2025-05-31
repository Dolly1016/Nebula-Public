using Il2CppSystem.Runtime.Serialization.Formatters.Binary;
using Nebula.Modules.GUIWidget;
using Nebula.Patches;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnityEngine.UIElements;
using Virial.Events.Player;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules;

static internal class DevTeamContact
{
    static private byte[] CompressLog(byte[]? log)
    {
        using var ms = new System.IO.MemoryStream();
        using (var zipArchive = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zipArchive.CreateEntry("GameLog.log");
            using (var es = entry.Open())
            {
                es.Write(log);
            }
        }
        return ms.ToArray();
    }

    static private HttpContent GenerateContent(string text, byte[][] images, byte[]? log)
    {
        if (log == null && images.Length == 0) return new FormUrlEncodedContent([new("content", text)]);

        MultipartFormDataContent contents = new();

        var payload = new{ content = text };
        string jsonPayload = JsonSerializer.Serialize(payload);
        var jsonContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        contents.Add(jsonContent, "payload_json");

        int num = 1;
        foreach(var image in images) contents.Add(new ByteArrayContent(screenshots.Peek().Texture.EncodeToPNG()), $"files[{num++}]", "Image.png");
        if (log != null)
        {
            if(log.Length > 1000 * 1000 * 24)
                contents.Add(new ByteArrayContent(CompressLog(log)), $"files[{num++}]", "GameLog.zip");
            else
                contents.Add(new ByteArrayContent(log), $"files[{num++}]", "GameLog.log");
        }
        return contents;
    }
    static private bool SendDiscordWebhook(string text, byte[][] images, byte[]? log, Action<HttpResponseMessage> onFinished)
    {
        
        try
        {
            HttpContent content = GenerateContent(text, images, log);
            
            var task = NebulaPlugin.HttpClient.PostAsync("https://discord.com/api/webhooks/1371183523297231052/018eZkY9ew6_q39P8dcNH6Huq_uROtXcgQKO7c9ZkdL4Q5jzfmVe52PMsiIt8q-2d8UY", content);
            NebulaManager.Instance.StartCoroutine(ManagedEffects.Sequence(
                task.WaitAsCoroutine(),
                ManagedEffects.Action(() =>
                {
                    content.Dispose();
                    onFinished.Invoke(task.Result);
                })
                ).WrapToIl2Cpp());
            return true;
        }
        catch (Exception e)
        {
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "Failed to send webhook. \n" + e.ToString());
            return false;
        }
    }

    static private MetaScreen lastWindow = null!;
    static public bool IsShown => lastWindow;
    static public MetaScreen OpenContactWindow(Transform? parent)
    {
        var window = MetaScreen.GenerateWindow(new(7.5f, 4.6f), parent, new(0f, 0f, -200f), true, false, true, BackgroundSetting.Modern);
        lastWindow = window;

        var inputField = new GUITextField(Virial.Media.GUIAlignment.Center, new(7f, 2.5f)) { IsSharpField = false, MaxLines = 12, FontSize = 1.4f, HintText = Language.Translate("ui.contact.guide").Color(Color.gray) };
        var checkBox = new NoSGUICheckbox(Virial.Media.GUIAlignment.Center, true);
        List<ScreenshotData> sendImages = [];
        window.SetWidget(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
            GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.DocumentTitle, "ui.contact.title"),
            inputField,
            GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center, checkBox, GUI.API.HorizontalMargin(0.15f), GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, AttributeAsset.DocumentBold, "ui.contact.sendLog")),
            GUI.API.VerticalMargin(0.05f),
            screenshots.Count > 0 ?
            GUI.API.VerticalHolder(GUIAlignment.Center,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, AttributeAsset.DocumentStandard, "ui.contact.image"),
                GUI.API.VerticalMargin(0.14f),
                GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center, screenshots.Take(8).Select(s =>
                {
                    GameObject checkObj = null!;
                    return new NoSGUIImage(Virial.Media.GUIAlignment.Center, s.Image, new(0.75f, null),
                       overlay: () => new NoSGUIImage(Virial.Media.GUIAlignment.Center, s.Image, new(3.5f, null)),
                       onClick: _ =>
                       {
                           checkObj.SetActive(!checkObj.active);
                           if (checkObj.active)
                               sendImages.Add(s);
                           else
                               sendImages.Remove(s);
                       })
                        {
                            PostBuilder = renderer =>
                            {
                                var checkBackRenderer = UnityHelper.CreateObject<SpriteRenderer>("Check", renderer.transform, new(renderer.sprite.bounds.size.x * 0.5f, renderer.sprite.bounds.size.y * 0.5f - 0.95f, -0.01f));
                                checkBackRenderer.sprite = GUIModernButton.ModernCheckBackSprite;
                                checkBackRenderer.transform.localScale = new(1f / renderer.transform.localScale.x, 1f / renderer.transform.localScale.y, 1f);
                                checkBackRenderer.sortingOrder = 20;

                                var checkRenderer = UnityHelper.CreateObject<SpriteRenderer>("Inner", checkBackRenderer.transform, new(0f, 0f, -0.01f));
                                checkRenderer.sprite = GUIModernButton.ModernCheckSprite;
                                checkRenderer.sortingOrder = 20;

                                checkObj = checkRenderer.gameObject;
                                checkObj.SetActive(false);
                            }
                        }.WithRoom(new(0.15f,0f));
                }))
            ) : GUI.API.LocalizedText(GUIAlignment.Center, AttributeAsset.DocumentStandard, "ui.contact.noImage").WithRoom(new(0f,0.4f)) ,
            GUI.API.VerticalMargin(0.06f),
            new GUIModernButton(Virial.Media.GUIAlignment.Center, AttributeAsset.OptionsButtonMedium, new TranslateTextComponent("ui.contact.send"))
            {
                OnClick = clickable =>
                {
                    var field = inputField.Artifact.FirstOrDefault();
                    if (field == null) return;
                    var text = field.Text;
                    if (text.Length == 0)
                    {
                        field.SetHint(Language.Translate("ui.contact.emptyError").Color(UnityEngine.Color.red.RGBMultiplied(0.7f)).Bold());
                        return;
                    }
                    byte[][] images = sendImages.Select(s => (byte[])s.Texture.EncodeToPNG()).ToArray();
                    var log = (checkBox.Artifact.FirstOrDefault().getter?.Invoke() ?? true) ? MemoryLogger.ToByteArray() : null;

                    if(window) window.CloseScreen();
                    var confirmDialog = MetaUI.ShowConfirmDialog(parent, new TranslateTextComponent("ui.contact.wait"));

                    SendDiscordWebhook(text, images, log, response => {
                        if (confirmDialog) confirmDialog.CloseScreen();

                        MetaUI.ShowConfirmDialog(parent,
                            response.StatusCode == System.Net.HttpStatusCode.OK ?
                            new TranslateTextComponent("ui.contact.finished") :
                            new RawTextComponent(Language.Translate("ui.contact.failed").Replace("%DETAIL%", $"[{response.StatusCode}] {response.Content.ToString()}")));
                    });
                    
                }
            }
        ), new Vector2(0.5f, 1f), out _);

        inputField.Artifact.FirstOrDefault()?.GainFocus();
        return window;
    }

    private class ScreenshotData
    {
        private Texture2D texture;
        private Sprite sprite = null!;
        private Image loader;
        public Image Image => loader;
        public Texture2D Texture=> texture;
        public void Abort()
        {
            if(texture) GameObject.Destroy(texture);
        }

        public ScreenshotData(Texture2D texture)
        {
            texture.MarkDontUnload();
            this.texture = texture;
            loader = new WrapSpriteLoader(() =>
            {
                if (sprite) return sprite;
                sprite = this.texture.ToSprite(50f);
                return sprite;
            });
        }
    }
    static private Queue<ScreenshotData> screenshots = [];
    internal static void PushScreenshot(Texture2D tex)
    {
        screenshots.Enqueue(new ScreenshotData(tex));
        if(!lastWindow) while(screenshots.Count > 8) screenshots.Dequeue().Abort();
    }
}
