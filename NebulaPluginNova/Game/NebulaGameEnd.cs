using Nebula.Behaviour;
using Virial.Game;
using Nebula.Roles.Modifier;
using Virial;
using Virial.Events.Game;
using Virial.Runtime;
using Nebula.Game.Statistics;

namespace Nebula.Game;

public class CustomEndCondition : Virial.Game.GameEnd
{
    static private HashSet<CustomEndCondition> allEndConditions= new();
    static public CustomEndCondition? GetEndCondition(byte id) => allEndConditions.FirstOrDefault(end => end.Id == id);
    
    public byte Id { get; init; }
    public string LocalizedName { get; init; }
    public string DisplayText => Language.Translate("end." + LocalizedName);
    public Color Color { get; init; }
    public int Priority { get; init; }

    //優先度が高いほど他の勝利を無視して勝利する
    public CustomEndCondition(byte id,string localizedName,Color color,int priority)
    {
        Id = id;
        LocalizedName = localizedName;
        Color = color;
        Priority = priority;

        allEndConditions.Add(this);
    }
}

public class CustomExtraWin : ExtraWin
{
    static private HashSet<CustomExtraWin> allExtraWin = new();
    static public CustomExtraWin? GetEndCondition(byte id) => allExtraWin.FirstOrDefault(end => end.Id == id);
    static public IEnumerable<CustomExtraWin> AllExtraWins => allExtraWin;
    public byte Id { get; private set; }
    public ulong ExtraWinMask => 1ul << Id;
    public string LocalizedName { get; init; }
    public string DisplayText => Language.Translate("end.extra." + LocalizedName).Color(Color);
    public Color Color { get; init; }

    public CustomExtraWin(byte id,string localizedName,Color color)
    {
        Id = id;
        LocalizedName = localizedName;
        Color = color;

        allExtraWin.Add(this);
    }
}

[NebulaRPCHolder]
[NebulaPreprocess(PreprocessPhase.PostRoles)]
public class NebulaGameEnd
{
    static private Color InvalidColor = new Color(72f / 255f, 78f / 255f, 84f / 255f);
    static public CustomEndCondition CrewmateWin = new(16, "crewmate", Palette.CrewmateBlue, 16);
    static public CustomEndCondition ImpostorWin = new(17, "impostor", Palette.ImpostorRed, 16);
    static public CustomEndCondition VultureWin = new(24, "vulture", Roles.Neutral.Vulture.MyRole.UnityColor, 32);
    static public CustomEndCondition JesterWin = new(25, "jester", Roles.Neutral.Jester.MyRole.UnityColor, 32);
    static public CustomEndCondition JackalWin = new(26, "jackal", Roles.Neutral.Jackal.MyRole.UnityColor, 18);
    static public CustomEndCondition ArsonistWin = new(27, "arsonist", Roles.Neutral.Arsonist.MyRole.UnityColor, 32);
    static public CustomEndCondition LoversWin = new(28, "lover", Roles.Modifier.Lover.MyRole.UnityColor, 18);
    static public CustomEndCondition PaparazzoWin = new(29, "paparazzo", Roles.Neutral.Paparazzo.MyRole.UnityColor, 32);
    static public CustomEndCondition AvengerWin = new(30, "avenger", Roles.Neutral.Avenger.MyRole.UnityColor, 64);
    static public CustomEndCondition DancerWin = new(31, "dancer", Roles.Neutral.Dancer.MyRole.UnityColor, 32);
    static public CustomEndCondition NoGame = new(128, "nogame", InvalidColor, 128);

    static public CustomExtraWin ExtraLoversWin = new(0, "lover", Roles.Modifier.Lover.MyRole.UnityColor);
    static public CustomExtraWin ExtraObsessionalWin = new(1, "obsessional", Roles.Modifier.Obsessional.MyRole.UnityColor);
    static public CustomExtraWin ExtraGrudgeWin = new(2, "grudge", Roles.Ghost.Neutral.Grudge.MyRole.UnityColor);

    static void Preprocess(NebulaPreprocessor preprocessor)
    {
        Virial.Game.NebulaGameEnds.CrewmateGameEnd = CrewmateWin;
        Virial.Game.NebulaGameEnds.ImpostorGameEnd = ImpostorWin;
        Virial.Game.NebulaGameEnds.VultureGameEnd = VultureWin;
        Virial.Game.NebulaGameEnds.JesterGameEnd = JesterWin;
        Virial.Game.NebulaGameEnds.JackalGameEnd = JackalWin;
        Virial.Game.NebulaGameEnds.ArsonistGameEnd = ArsonistWin;
        Virial.Game.NebulaGameEnds.PaparazzoGameEnd = PaparazzoWin;
    }

    private readonly static RemoteProcess<(byte conditionId, int winnersMask,ulong extraWinMask, GameEndReason endReason)> RpcEndGame = new(
       "EndGame",
       (message, isCalledByMe) =>
       {
           if (NebulaGameManager.Instance != null)
           {
               var end = CustomEndCondition.GetEndCondition(message.conditionId) ?? NebulaGameEnd.NoGame;
               var winners = BitMasks.AsPlayer((uint)message.winnersMask);
               EditableBitMask<ExtraWin> extraWin = new HashSetMask<ExtraWin>();
               foreach(var exW in CustomExtraWin.AllExtraWins) if((exW.ExtraWinMask & message.extraWinMask) != 0) extraWin.Add(exW);

               NebulaGameManager.Instance.EndState ??= new EndState(winners, end, message.endReason, extraWin);
               NebulaGameManager.Instance.OnGameEnd();
               GameOperatorManager.Instance?.Run(new GameEndEvent(NebulaGameManager.Instance, NebulaGameManager.Instance.EndState));
               NebulaGameManager.Instance.ToGameEnd();
           }
       }
       );

    public static bool RpcSendGameEnd(Virial.Game.GameEnd winCondition, int winnersMask, ulong extraWinMask, GameEndReason endReason)
    {
        if (NebulaGameManager.Instance?.EndState != null) return false;
        RpcEndGame.Invoke((winCondition.Id, winnersMask, extraWinMask, endReason));
        return true;
    }

    public static bool RpcSendGameEnd(Virial.Game.GameEnd winCondition,HashSet<byte> winners, ulong extraWinMask, GameEndReason endReason)
    {
        int winnersMask = 0;
        foreach (byte w in winners) winnersMask |= ((int)1 << w);
        return RpcSendGameEnd(winCondition, winnersMask, extraWinMask, endReason);
    }
}

public class LastGameHistory
{
    static public IMetaWidgetOld? LastWidget;

    public static void SetHistory(TMPro.TMP_FontAsset font, IMetaWidgetOld roleWidget, string endCondition)
    {
        LastWidget = new MetaWidgetOld(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttrLeft) { Font = font }) { RawText = endCondition }, new MetaWidgetOld.VerticalMargin(0.15f), roleWidget);        
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
        texture2D.Apply();

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
}


[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.SetEverythingUp))]
public class EndGameManagerSetUpPatch
{
    static private bool SendDiscordWebhook(byte[] pngImage)
    {
        try
        {
            using MultipartFormDataContent content = new();
            content.Add(new FormUrlEncodedContent([new("content", "テスト")]));
            content.Add(new ByteArrayContent(pngImage), "file", "image.png");
            var awaiter = NebulaPlugin.HttpClient.PostAsync(ClientOption.WebhookOption.url, content).GetAwaiter();
            awaiter.GetResult();
            return true;
        }catch(Exception e)
        {
            NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, null, "Failed to send webhook. \n" + e.ToString());
            return false;
        }
    }


    static SpriteLoader InfoButtonSprite = SpriteLoader.FromResource("Nebula.Resources.InformationButton.png", 100f);
    static SpriteLoader DiscordButtonSprite = SpriteLoader.FromResource("Nebula.Resources.DiscordIcon.png", 100f);

    private static IMetaWidgetOld GetRoleContent(TMPro.TMP_FontAsset font)
    {
        MetaWidgetOld widget = new();
        string text = "";

        NebulaGameManager.Instance?.ChangeToSpectator();

        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
        {
            //Name Text
            string nameText = p.Name.Color(NebulaGameManager.Instance.EndState!.Winners.Test(p) ? Color.yellow : Color.white);
            if (p.TryGetModifier<ExtraMission.Instance>(out var mission)) nameText += (" <size=60%>(" + (mission.target?.Name ?? "ERROR") + ")</size>").Color(ExtraMission.MyRole.UnityColor);

            string stateText = p.PlayerState.Text;
            if (p.IsDead && p.MyKiller != null) stateText += "<color=#FF6666><size=75%> by " + (p.MyKiller?.Name ?? "ERROR") + "</size></color>";
            string taskText = (!p.IsDisconnected && p.Tasks.Quota > 0) ? $" ({p.Tasks.Unbox().ToString(true)})".Color(p.Tasks.IsCrewmateTask ? PlayerModInfo.CrewTaskColor : PlayerModInfo.FakeTaskColor) : "";

            //Role Text
            string roleText = "";
            var entries = NebulaGameManager.Instance.RoleHistory.EachMoment(history => history.PlayerId == p.PlayerId,
                (role, ghostRole, modifiers) => (RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, true), RoleHistoryHelper.ConvertToRoleName(role, ghostRole, modifiers, false))).ToArray();

            if (entries.Length < 8)
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
            roleText += entries[entries.Length - 1].Item2;

            text += $"{nameText}<indent=20px>{taskText}</indent><indent=29px>{stateText}</indent><indent=47px>{roleText}</indent>\n";
        }

        widget.Append(new MetaWidgetOld.VariableText(new TextAttributeOld(TextAttributeOld.BoldAttr) { Font = font, Size = new(6f, 4.2f), Alignment = TMPro.TextAlignmentOptions.Left }.EditFontSize(1.4f, 1f, 1.4f))
        { Alignment = IMetaWidgetOld.AlignmentOption.Left, RawText = text });

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
        List<byte> winners = new List<byte>();
        bool amWin = false;
        foreach(var p in NebulaGameManager.Instance.AllPlayerInfo())
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
            poolablePlayer.UpdateFromPlayerOutfit(player.Unbox().DefaultOutfit, PlayerMaterial.MaskType.None, player.IsDead, true);

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
        CustomExtraWin.AllExtraWins.Where(e => wins.Test(e)).Do(e => extraText += e.DisplayText);
        textRenderer.text = endCondition?.Unbox().DisplayText.Replace("%EXTRA%", extraText) ?? "Error";
        textRenderer.color = endCondition?.Unbox().Color ?? Color.white;

        __instance.BackgroundBar.material.SetColor("_Color", endCondition?.Unbox().Color ?? new Color(1f, 1f, 1f));

        __instance.WinText.text = DestroyableSingleton<TranslationController>.Instance.GetString(amWin ? StringNames.Victory : StringNames.Defeat);
        __instance.WinText.color = amWin ? new Color(0f, 0.549f, 1f, 1f) : Color.red;

        LastGameHistory.SetHistory(__instance.WinText.font, GetRoleContent(__instance.WinText.font), textRenderer.text.Color(endCondition?.Unbox().Color ?? Color.white));

        GameStatisticsViewer? viewer;

        IEnumerator CoShowStatistics()
        {
            yield return new WaitForSeconds(0.4f);
            viewer = UnityHelper.CreateObject<GameStatisticsViewer>("Statistics", __instance.transform, new Vector3(0f, 2.5f, -20f), LayerExpansion.GetUILayer());
            viewer.PlayerPrefab = __instance.PlayerPrefab;
            viewer.GameEndText = __instance.WinText;
        }
        __instance.StartCoroutine(CoShowStatistics().WrapToIl2Cpp());

        var buttonRenderer = UnityHelper.CreateObject<SpriteRenderer>("InfoButton", __instance.transform, new Vector3(-2.9f, 2.5f, -50f), LayerExpansion.GetUILayer());
        buttonRenderer.sprite = InfoButtonSprite.GetSprite();
        var button = buttonRenderer.gameObject.SetUpButton(false, buttonRenderer);
        button.OnMouseOver.AddListener(() => NebulaManager.Instance.SetHelpWidget(button, GetRoleContent(__instance.WinText.font)));
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        button.gameObject.AddComponent<BoxCollider2D>().size = new(0.3f, 0.3f);


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

        //Achievements
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

