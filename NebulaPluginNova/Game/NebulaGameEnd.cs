using Nebula.Behavior;
using Virial.Game;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Events.Game;
using Virial.Runtime;
using Nebula.Game.Statistics;
using Virial.Text;
using Virial.Assignable;
using UnityEngine.Rendering;

namespace Nebula.Game;

[NebulaRPCHolder]
[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class NebulaGameEnd
{
    static private Color InvalidColor = new Color(72f / 255f, 78f / 255f, 84f / 255f);
    static public readonly GameEnd CrewmateWin = new(16, "crewmate", Palette.CrewmateBlue, 16);
    static public readonly GameEnd ImpostorWin = new(17, "impostor", Palette.ImpostorRed, 16);
    static public readonly GameEnd JackalWin = new(26, "jackal", Roles.Neutral.Jackal.MyRole.UnityColor, 18);
    static public readonly GameEnd VultureWin = new(24, "vulture", Roles.Neutral.Vulture.MyRole.UnityColor, 32);
    static public readonly GameEnd JesterWin = new(25, "jester", Roles.Neutral.Jester.MyRole.UnityColor, 32);
    static public readonly GameEnd ArsonistWin = new(27, "arsonist", Roles.Neutral.Arsonist.MyRole.UnityColor, 32);
    static public readonly GameEnd LoversWin = new(28, "lover", Roles.Modifier.Lover.MyRole.UnityColor, 19);
    static public readonly GameEnd PaparazzoWin = new(29, "paparazzo", Roles.Neutral.Paparazzo.MyRole.UnityColor, 31);
    static public readonly GameEnd AvengerWin = new(30, "avenger", Roles.Neutral.Avenger.MyRole.UnityColor, 62);
    static public readonly GameEnd DancerWin = new(31, "dancer", Roles.Neutral.Dancer.MyRole.UnityColor, 32);
    static public readonly GameEnd ScarletWin = new(32, "scarlet", Roles.Neutral.Scarlet.MyRole.UnityColor, 64);
    static public readonly GameEnd SpectreWin = new(33, "spectre", Roles.Neutral.Spectre.MyRole.UnityColor, 63);
    static public readonly GameEnd TrilemmaWin = new(34, "trilemma", Roles.Modifier.Trilemma.MyRole.UnityColor, 61);
    static public readonly GameEnd GamblerWin = new(35, "gambler", Roles.Neutral.Gambler.MyRole.UnityColor, 30);
    static public readonly GameEnd NoGame = new(63, "nogame", InvalidColor, 128) { AllowWin = false };

    static public readonly ExtraWin ExtraLoversWin = new(0, "lover", (Roles.Modifier.Lover.MyRole as DefinedAssignable).Color);
    static public readonly ExtraWin ExtraObsessionalWin = new(1, "obsessional", (Roles.Modifier.Obsessional.MyRole as DefinedAssignable).Color);
    static public readonly ExtraWin ExtraGrudgeWin = new(2, "grudge", (Roles.Ghost.Neutral.Grudge.MyRole as DefinedAssignable).Color);
    static public readonly ExtraWin ExtraTrilemmaWin = new(3, "trilemma", (Roles.Modifier.Trilemma.MyRole as DefinedAssignable).Color);

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        //Tipsの追加
        string ImpostorTeam(string text) => text.Replace("%IMPOSTOR%", Language.Translate("document.tip.winCond.teams.impostor").Color(Roles.Impostor.Impostor.MyTeam.UnityColor));
        string JackalTeam(string text) => text.Replace("%JACKAL%", Language.Translate("document.tip.winCond.teams.jackal").Color(Roles.Neutral.Jackal.MyTeam.UnityColor));
        string LoverTeam(string text) => text.Replace("%LOVER%", Language.Translate("document.tip.winCond.teams.lover").Color(Roles.Modifier.Lover.MyRole.UnityColor));
        RegisterWinCondTip(CrewmateWin, () => true, "crewmate.task");
        RegisterWinCondTip(CrewmateWin, () => true, "crewmate.exile", text => ImpostorTeam(JackalTeam(text)));
        RegisterWinCondTip(ImpostorWin, () => true, "impostor.kill", text => ImpostorTeam(JackalTeam(LoverTeam(text))));
        //RegisterWinCondTip(ImpostorWin, () => true, "impostor.extinction", text => LoverTeam(text));
        RegisterWinCondTip(ImpostorWin, () => true, "impostor.sabotage");
        RegisterWinCondTip(JackalWin, () => (Roles.Neutral.Jackal.MyRole as ISpawnable).IsSpawnable, "jackal.kill", text => ImpostorTeam(JackalTeam(LoverTeam(text))));
        RegisterWinCondTip(JackalWin, () => (Roles.Neutral.Jackal.MyRole as ISpawnable).IsSpawnable, "jackal.extinction", text => LoverTeam(text));
        RegisterWinCondTip(JesterWin, () => (Roles.Neutral.Jester.MyRole as ISpawnable).IsSpawnable, "jester");
        RegisterWinCondTip(GamblerWin, () => (Roles.Neutral.Gambler.MyRole as ISpawnable).IsSpawnable, "gambler", text => text.Replace("%NUM%", Roles.Neutral.Gambler.GoalChipsOption.GetValue().ToString()));
        RegisterWinCondTip(VultureWin, () => GeneralConfigurations.NeutralSpawnable && (Roles.Neutral.Vulture.MyRole as ISpawnable).IsSpawnable, "vulture");
        RegisterWinCondTip(ArsonistWin, () => GeneralConfigurations.NeutralSpawnable && (Roles.Neutral.Arsonist.MyRole as ISpawnable).IsSpawnable, "arsonist");
        RegisterWinCondTip(PaparazzoWin, () => GeneralConfigurations.NeutralSpawnable && (Roles.Neutral.Paparazzo.MyRole as ISpawnable).IsSpawnable, "paparazzo");
        RegisterWinCondTip(ScarletWin, () => GeneralConfigurations.NeutralSpawnable && (Roles.Neutral.Scarlet.MyRole as ISpawnable).IsSpawnable, "scarlet");
        RegisterWinCondTip(LoversWin, () => (Roles.Modifier.Lover.MyRole as ISpawnable).IsSpawnable, "lovers.normal");
        RegisterWinCondTip(LoversWin, () => (Roles.Modifier.Lover.MyRole as ISpawnable).IsSpawnable && Roles.Modifier.Lover.AllowExtraWinOption, "lovers.extra");
        RegisterWinCondTip(AvengerWin, () => (Roles.Modifier.Lover.MyRole as ISpawnable).IsSpawnable && Roles.Modifier.Lover.AvengerModeOption, "avenger");
        RegisterWinCondTip(DancerWin, () => GeneralConfigurations.NeutralSpawnable && (Roles.Neutral.Dancer.MyRole as ISpawnable).IsSpawnable, "dancer");
        RegisterWinCondTip(SpectreWin, () => (Roles.Neutral.Spectre.MyRole as ISpawnable).IsSpawnable, "spectre");
        RegisterWinCondTip(TrilemmaWin, () => (Roles.Modifier.Trilemma.MyRole as ISpawnable).IsSpawnable && Roles.Modifier.Trilemma.WinConditionOption.GetValue() == 2, "trilemma");
    }

    private static void RegisterWinCondTip(GameEnd gameEnd, Func<bool> predicate, string name, Func<string,string>? decorator = null)
    {
        NebulaAPI.RegisterTip(new WinConditionTip(gameEnd, predicate, () => Language.Translate("document.tip.winCond." + name + ".title"), () =>
        {
            string text = Language.Translate("document.tip.winCond." + name);
            return decorator?.Invoke(text) ?? text;
        }));
    }

    private readonly static RemoteProcess<(byte conditionId, int winnersMask,ulong extraWinMask, GameEndReason endReason, byte originalConditionId, GameEndReason originalEndReason)> RpcEndGame = new(
       "EndGame",
       (message, isCalledByMe) =>
       {
           if (NebulaGameManager.Instance != null)
           {
               var end = GameEnd.TryGet(message.conditionId, out var e1) ? e1 : NebulaGameEnd.NoGame;
               var originalEnd = GameEnd.TryGet(message.originalConditionId, out var e2) ? e2 : NebulaGameEnd.NoGame;
               var winners = BitMasks.AsPlayer((uint)message.winnersMask);
               EditableBitMask<ExtraWin> extraWin = new HashSetMask<ExtraWin>();
               foreach(var exW in ExtraWin.AllExtraWins) if((exW.ExtraWinMask & message.extraWinMask) != 0) extraWin.Add(exW);

               NebulaGameManager.Instance.EndState ??= new EndState(winners, end, message.endReason, originalEnd, message.originalEndReason, extraWin);
               NebulaGameManager.Instance.OnGameEnd();
               GameOperatorManager.Instance?.Run(new GameEndEvent(NebulaGameManager.Instance, NebulaGameManager.Instance.EndState));
               NebulaGameManager.Instance.ToGameEnd();
           }
       }
       );

    public static bool RpcSendGameEnd(Virial.Game.GameEnd winCondition, int winnersMask, ulong extraWinMask, GameEndReason endReason, Virial.Game.GameEnd originalWinCondition, GameEndReason originalEndReason)
    {
        if (NebulaGameManager.Instance?.EndState != null) return false;
        RpcEndGame.Invoke((winCondition.Id, winnersMask, extraWinMask, endReason, originalWinCondition.Id, originalEndReason));
        return true;
    }
}

public class LastGameHistory
{
    static public MetaWidgetOld? LastWidget = null;
    static public IArchivedGame? ArchivedGame = null;

    public static void SetHistory(TMPro.TMP_FontAsset font, IMetaWidgetOld roleWidget, string endCondition)
    {
        LastWidget = new MetaWidgetOld(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttrLeft) { Font = font }) { RawText = endCondition }, new MetaWidgetOld.VerticalMargin(0.15f), roleWidget);
        ArchivedGame = ArchivedGameImpl.FromCurrentGame();
    }

    public static Texture2D GenerateTexture()
    {
        var gameObject = UnityHelper.CreateObject("History", null, Vector3.zero, 30);

        float height = LastWidget!.Generate(gameObject, new Vector2(0,0),new Vector2(10f,10f),out var width);

        gameObject.ForEachAllChildren(obj => obj.layer = 30);

        var camObject = UnityHelper.CreateObject("Cam", null, new Vector3((width.min + width.max) * 0.5f, -height * 0.5f, -10f));

        Camera cam = camObject.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = (height + 0.35f) * 0.5f;
        cam.transform.localScale = Vector3.one;
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = Color.black;
        cam.cullingMask = 1 << 30;
        cam.enabled = true;

        RenderTexture rt = new RenderTexture((int)((width.max - width.min) * 100f), (int)(height * 100f), 16);
        rt.Create();

        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = cam.targetTexture;
        Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, false);
        texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2D.Apply(false, false);

        RenderTexture.active = null;
        cam.targetTexture = null;
        GameObject.Destroy(rt);
        GameObject.Destroy(gameObject);
        GameObject.Destroy(camObject);

        return texture2D;
    }

    public static void SaveResult(string path)
    {
        File.WriteAllBytesAsync(path, GenerateTexture().EncodeToPNG());
    }

    private static MetaScreen lastStatisticsScreen = null!;
    public static bool ScreenIsVisible => lastStatisticsScreen;
    public static void ShowLastGameStatistics()
    {
        if (ArchivedGame == null) return;

        HudManager hud = HudManager.Instance;
        var window = MetaScreen.GenerateWindow(new(6.7f, 4.2f), hud.transform, Vector3.zero, true, false, false, false);
        window.GetComponent<SortingGroup>().enabled = false;
        lastStatisticsScreen = window;

        var viewer = UnityHelper.CreateObject<GameStatisticsViewer>("Statistics", window.transform, new Vector3(0f, 2.5f, -20f), LayerExpansion.GetUILayer());
        var mapAsset = VanillaAsset.MapAsset[ArchivedGame.MapId];
        viewer.Initialize(ArchivedGame, hud.IntroPrefab.PlayerPrefab, mapAsset.MapPrefab, mapAsset.MapScale, hud.IntroPrefab.TeamTitle, true, 0f);

        if (LastWidget != null)
        {
            var buttonRenderer = UnityHelper.CreateObject<SpriteRenderer>("InfoButton", window.transform, new Vector3(-2.9f, 2.5f, -50f), LayerExpansion.GetUILayer());
            buttonRenderer.sprite = EndGameManagerSetUpPatch.InfoButtonSprite.GetSprite();
            var button = buttonRenderer.gameObject.SetUpButton(false, buttonRenderer);
            button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, LastWidget));
            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
            button.gameObject.AddComponent<BoxCollider2D>().size = new(0.3f, 0.3f);
        }
    }
}


[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
public class EndGameManagerSetUpPatch
{
    static private bool SendDiscordWebhook(byte[] pngImage)
    {
        try
        {
            using MultipartFormDataContent content = new()
            {
                // { new FormUrlEncodedContent([new("content", "テスト")]) },
                { new ByteArrayContent(pngImage), "file", "image.png" }
            };
            var awaiter = NebulaPlugin.HttpClient.PostAsync(Helpers.ConvertUrl(ClientOption.WebhookOption.url), content).GetAwaiter();
            awaiter.GetResult();
            return true;
        }catch(Exception e)
        {
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "Failed to send webhook. \n" + e.ToString());
            return false;
        }
    }


    static internal SpriteLoader InfoButtonSprite = SpriteLoader.FromResource("Nebula.Resources.InformationButton.png", 100f);
    static SpriteLoader DiscordButtonSprite = SpriteLoader.FromResource("Nebula.Resources.DiscordIcon.png", 100f);

    private static IMetaWidgetOld GetRoleContent(TMPro.TMP_FontAsset font)
    {
        MetaWidgetOld widget = new();

        NebulaGameManager.Instance?.ChangeToSpectator();
        List<(string name, string state, string task, string role)> players = [];
        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo)
        {
            //Name Text
            string nameText = p.Name.Color(NebulaGameManager.Instance.EndState!.Winners.Test(p) ? Color.yellow : Color.white);
            if (p.TryGetModifier<ExtraMission.Instance>(out var mission)) nameText += (" <size=60%>(" + (mission.target?.Name ?? "ERROR") + ")</size>").Color(ExtraMission.MyRole.UnityColor);
            if (p.TryGetModifier<Obsessional.Instance>(out var obsessional)) nameText += (" <size=60%>(" + (obsessional.Obsession?.Name ?? "ERROR") + ")</size>").Color(Obsessional.MyRole.UnityColor);

            string stateText = p.PlayerState.Text;
            string stateExText = "";
            if (p.PlayerStateExtraInfo != null && p.PlayerStateExtraInfo.State == p.PlayerState) stateExText = p.PlayerStateExtraInfo.ToStateText();
            else if (p.IsDead && p.MyKiller != null) stateExText = "by " + (p.MyKiller?.Name ?? "ERROR");
            if(stateExText.Length > 0) stateText += "<color=#FF6666><size=75%> " + stateExText + "</size></color>";
            
            string taskText = (!p.IsDisconnected && p.Tasks.Quota > 0) ? $"({p.Tasks.Unbox().ToString(true)})".Color(p.Tasks.IsCrewmateTask ? PlayerModInfo.CrewTaskColor : PlayerModInfo.FakeTaskColor) : "";

            //Role Text
            string roleText = "";
            var entries = NebulaGameManager.Instance.RoleHistory.EachMoment(history => history.PlayerId == p.PlayerId,
                (role, ghostRole, modifiers) => (RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, true), RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, false))).ToArray();

            if (entries.Length < 5)
            {
                for (int i = 0; i < entries.Length - 1; i++)
                {
                    if (roleText.Length > 0) roleText += " → ";
                    roleText += entries[i].Item1;
                }
            }
            else
            {
                roleText = entries[0].Item1 + " → ...";
            }

            if (roleText.Length > 0) roleText += " → ";
            roleText += entries[^1].Item2;

            players.Add((nameText, stateText, taskText, roleText));
        }

        widget.Append(new MetaWidgetOld.VariableText(new TextAttributeOld(TextAttributeOld.BoldAttr) { Font = font, Size = new(8f, 5f), Alignment = TMPro.TextAlignmentOptions.Left }.EditFontSize(1.1f, 1.1f, 1.1f))
        { Alignment = IMetaWidgetOld.AlignmentOption.Left,
        PreResizeBuilder = t =>
        {
            
            (float nameMax, float stateMax, float taskMax, float roleMax) = (0f, 0f, 0f, 0f);

            LogUtils.WriteToConsole("FontSize: " + t.fontSize);
            LogUtils.WriteToConsole("PointSize: " + t.font.faceInfo.pointSize);
            LogUtils.WriteToConsole("Scale: " + t.font.faceInfo.scale);
            float coeff = t.fontSize / t.font.faceInfo.pointSize * t.font.faceInfo.scale * 10f;
            foreach (var p in players)
            {
                nameMax = Math.Max(t.GetPreferredValues(p.name).x / coeff, nameMax);
                stateMax = Math.Max(t.GetPreferredValues(p.state).x / coeff, stateMax);
                taskMax = Math.Max(t.GetPreferredValues(p.task).x / coeff, taskMax);
                roleMax = Math.Max(t.GetPreferredValues(p.role).x / coeff, roleMax);
            }

            float indent = 0;
            indent += (float)(nameMax) + 5.5f;
            string afterNameIndent = $"<indent={indent:F3}em>";
            indent += (float)(taskMax) + 2.3f;
            string afterTaskIndent = $"<indent={indent:F3}em>";
            indent += (float)(stateMax) + 6f;
            string afterStateIndent = $"<indent={indent:F3}em>";

            t.fontSizeMax = 1.4f;
            t.fontSize = 1.4f;
            t.text = string.Join("\n", players.Select(p => $"{p.name}{afterNameIndent}{p.task}</indent>{afterTaskIndent}{p.state}</indent>{afterStateIndent}{p.role}</indent>"));
            //text += $"{nameText}<indent=20px>{taskText}</indent><indent=29px>{stateText}</indent><indent=47px>{roleText}</indent>\n";
        }
        });

        return widget;
    }

    public static void Postfix(EndGameManager __instance)
    {
        if (NebulaGameManager.Instance == null) return;
        var endState = NebulaGameManager.Instance.EndState;
        var endCondition = endState?.EndCondition;

        if (endState == null) return;

        /*
        foreach(var h in NebulaGameManager.Instance.RoleHistory)
        {
            NebulaPlugin.Log.Print("ID: " + h.PlayerId + ", " + h.Assignable.GetType().Name + ", Dead: " + h.Dead + ", IsModifier: " + h.IsModifier + ", IsSet: " + h.IsSet + ", Time: " + h.Time);
        }
        */

        //元の勝利チームを削除する
        foreach (PoolablePlayer pb in __instance.transform.GetComponentsInChildren<PoolablePlayer>()) UnityEngine.Object.Destroy(pb.gameObject);


        //勝利メンバーを載せる
        List<byte> winners = [];
        bool amWin = false;
        foreach(var p in NebulaGameManager.Instance.AllPlayerInfo)
        {
            if (endState.Winners.Test(p))
            {
                if (p.AmOwner)
                {
                    amWin = true;
                    winners.Insert(0, p.PlayerId);
                }
                else
                {
                    winners.Add(p.PlayerId);
                }
            }
        }

        int num = Mathf.CeilToInt(7.5f);
        for (int i = 0; i < winners.Count; i++)
        {
            int num2 = (i % 2 == 0) ? -1 : 1;
            int num3 = (i + 1) / 2;
            float num4 = (float)num3 / (float)num;
            float num5 = Mathf.Lerp(1f, 0.75f, num4);
            float num6 = (float)((i == 0) ? -8 : -1);
            PoolablePlayer poolablePlayer = UnityEngine.Object.Instantiate<PoolablePlayer>(__instance.PlayerPrefab, __instance.transform);
            poolablePlayer.transform.localPosition = new Vector3(1f * (float)num2 * (float)num3 * num5, FloatRange.SpreadToEdges(-1.125f, 0f, num3, num), num6 + (float)num3 * 0.01f) * 0.9f;
            float num7 = Mathf.Lerp(1f, 0.65f, num4) * 0.9f;
            Vector3 vector = new Vector3(num7, num7, 1f);
            poolablePlayer.transform.localScale = vector;

            var player = NebulaGameManager.Instance.GetPlayer(winners[i])!;

            if (player.IsDead)//死んでいる場合
            {
                poolablePlayer.SetBodyAsGhost();
                poolablePlayer.SetDeadFlipX(i % 2 == 0);
            }
            else
            {
                poolablePlayer.SetFlipX(i % 2 == 0);
            }
            poolablePlayer.UpdateFromPlayerOutfit(player.Unbox().DefaultOutfit.Outfit.outfit, PlayerMaterial.MaskType.None, player.IsDead, true);

            poolablePlayer.SetName(player.Name, new Vector3(1f / vector.x, 1f / vector.y, 1f / vector.z), Color.white, -15f); ;
            poolablePlayer.SetNamePosition(new Vector3(0f, -1.31f, -0.5f));

            poolablePlayer.gameObject.AddComponent<ModTitleShower>();
        }

        // テキストを追加する
        GameObject bonusText = UnityEngine.Object.Instantiate(__instance.WinText.gameObject);
        bonusText.transform.SetParent(null);
        bonusText.transform.position = new Vector3(__instance.WinText.transform.position.x, __instance.WinText.transform.position.y - 0.5f, __instance.WinText.transform.position.z);
        bonusText.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
        TMPro.TMP_Text textRenderer = bonusText.GetComponent<TMPro.TMP_Text>();

        string extraText = "";
        var wins = NebulaGameManager.Instance.EndState!.ExtraWins;
        ExtraWin.AllExtraWins.Where(e => wins.Test(e)).Do(e => extraText += e.DisplayText);
        textRenderer.text = endCondition?.DisplayText.GetString().Replace("%EXTRA%", extraText) ?? "Error";
        textRenderer.color = endCondition?.Color ?? Color.white;

        __instance.BackgroundBar.material.SetColor("_Color", endCondition?.Color ?? new Color(1f, 1f, 1f));

        __instance.WinText.text = DestroyableSingleton<TranslationController>.Instance.GetString(amWin ? StringNames.Victory : StringNames.Defeat);
        __instance.WinText.color = amWin ? new Color(0f, 0.549f, 1f, 1f) : Color.red;

        LastGameHistory.SetHistory(__instance.WinText.font, GetRoleContent(__instance.WinText.font), textRenderer.text.Color(endCondition?.Color ?? Color.white));

        GameStatisticsViewer? viewer;

        IEnumerator CoShowStatistics()
        {
            yield return new WaitForSeconds(0.4f);
            viewer = UnityHelper.CreateObject<GameStatisticsViewer>("Statistics", __instance.transform, new Vector3(0f, 2.5f, -20f), LayerExpansion.GetUILayer());
            viewer.Initialize(NebulaGameManager.Instance!, __instance.PlayerPrefab, NebulaGameManager.Instance!.RuntimeAsset.MinimapPrefab, NebulaGameManager.Instance!.RuntimeAsset.MapScale,__instance.WinText, false);
        }
        __instance.StartCoroutine(CoShowStatistics().WrapToIl2Cpp());

        var buttonRenderer = UnityHelper.CreateObject<SpriteRenderer>("InfoButton", __instance.transform, new Vector3(-2.9f, 2.5f, -50f), LayerExpansion.GetUILayer());
        buttonRenderer.sprite = InfoButtonSprite.GetSprite();
        var button = buttonRenderer.gameObject.SetUpButton(false, buttonRenderer);
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, GetRoleContent(__instance.WinText.font)));
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        button.gameObject.AddComponent<BoxCollider2D>().size = new(0.3f, 0.3f);


        if (NebulaPlugin.AllowHttpCommunication)
        {
            if (!AmongUsClient.Instance.AmHost || ClientOption.WebhookOption.urlEntry.Value.Length == 0 || !ClientOption.WebhookOption.autoSendEntry.Value)
            {
                var discordButtonRenderer = UnityHelper.CreateObject<SpriteRenderer>("WebhookButton", __instance.transform, new Vector3(-3.4f, 2.5f, -50f), LayerExpansion.GetUILayer());
                discordButtonRenderer.sprite = DiscordButtonSprite.GetSprite();
                var discordButton = discordButtonRenderer.gameObject.SetUpButton(true, discordButtonRenderer);
                discordButton.OnClick.AddListener(() =>
                {
                    var data = LastGameHistory.GenerateTexture().EncodeToPNG();
                    if (ClientOption.WebhookOption.urlEntry.Value.Length == 0 || !SendDiscordWebhook(data))
                        ClientOption.ShowWebhookSetting(() => SendDiscordWebhook(data));
                });
                discordButton.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked = () => ClientOption.ShowWebhookSetting(() => SendDiscordWebhook(LastGameHistory.GenerateTexture().EncodeToPNG()));
                discordButton.gameObject.AddComponent<BoxCollider2D>().size = new(0.3f, 0.3f);
            }
            else
            {
                SendDiscordWebhook(LastGameHistory.GenerateTexture().EncodeToPNG());
            }
;
        }

        //Achievements
        NebulaAchievementManager.ClearHistory();
        //標準ゲームモードで廃村でない、かつOP権限が誰にも付与されていないゲームの場合
        if (GeneralConfigurations.CurrentGameMode == GameModes.Standard && endCondition != NebulaGameEnd.NoGame && !GeneralConfigurations.AssignOpToHostOption)
        {
            NebulaManager.Instance.StartCoroutine(NebulaAchievementManager.CoShowAchievements(NebulaManager.Instance, NebulaAchievementManager.UniteAll()).WrapToIl2Cpp());
        }
    }
}


[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoEndGame))]
public class EndGamePatch
{

    public static bool Prefix(AmongUsClient __instance, ref Il2CppSystem.Collections.IEnumerator __result)
    {
        if (NebulaGameManager.Instance == null) return true;
        NebulaGameManager.Instance.ReceiveVanillaGameResult();
        NebulaGameManager.Instance.ToGameEnd();

        __result = Effects.Wait(0.1f);
        return false;
    }
}

