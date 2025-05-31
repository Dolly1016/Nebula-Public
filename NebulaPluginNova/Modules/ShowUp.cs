using BepInEx.Unity.IL2CPP.Utils;
using Iced.Intel;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Rendering;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;
using Virial.Text;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
[NebulaRPCHolder]
internal class ShowUp : AbstractModule<Virial.Game.Game>, IGameOperator
{
    public class PickedUpArchive
    {
        List<string> players = [];
        Dictionary<string, ulong> achievements = [];
        PriorityQueue<INebulaAchievement, int> queue = new();
        public void TryAdd(string playerName, IEnumerable<INebulaAchievement> achievements)
        {
            if (players.Contains(playerName)) return;

            int index = players.Count;
            ulong bit = 1UL << index;
            players.Add(playerName);

            foreach (var a in achievements)
            {
                if (this.achievements.TryGetValue(a.Id, out var bitPattern))
                    this.achievements[a.Id] = bitPattern | bit;
                else
                {
                    this.achievements[a.Id] = bit;
                    this.queue.Enqueue(a, -a.Attention);
                }
            }
        }

        public (string playerName, int others, INebulaAchievement achievement)? Dequeue()
        {
            if (this.queue.Count == 0) return null;
            var achievement = queue.Dequeue();
            var bit = this.achievements[achievement.Id];
            var player = Helpers.Sequential(players.Count).Where(i => ((1ul << i) & bit) != 0).Select(i => players[i]).ToArray();
            return (player.Random(), player.Length - 1, achievement);
        }

        internal IEnumerator CoShowAchievements(ShowUp showUp)
        {
            if(queue.Count == 0)
            {
                yield return NebulaManager.Instance.StartCoroutine(showUp.CoShowSocial("NoAchievement", Vector2.zero, GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "achievement.social.noAchievement"),
                    null, 5f, null, false,considerOnlyLobby: true, considerPlayerAppeal: true));
                yield break;
            }

            showUp.lastClearedAchievementsQueue.Clear();

            bool dualPattern = false;
            while (true)
            {
                while (showUp.AnySocialShown) yield return null;

                var entry = Dequeue();
                if (entry == null) yield break;

                if (entry.Value.achievement.Attention <= 80 && queue.Count > 0)
                {
                    var nextEntry = Dequeue()!;

                    RpcSendArchiveDual.Invoke((dualPattern, entry.Value.playerName, entry.Value.others, entry.Value.achievement, nextEntry.Value.playerName, nextEntry.Value.others, nextEntry.Value.achievement));
                    Vector2 vector = dualPattern ? new(1.2f, 0.7f) : new(-1.2f, 0.7f);
                    dualPattern = !dualPattern;
                    //2つ同時出現
                    yield return Effects.All(
                        entry.Value.achievement.CoShowSocialBillboard(vector, entry.Value.others == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, entry.Value.playerName, entry.Value.others).WrapToIl2Cpp(),
                        nextEntry.Value.achievement.CoShowSocialBillboard(-vector, nextEntry.Value.others == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, nextEntry.Value.playerName, nextEntry.Value.others).WrapToIl2Cpp()
                        );
                }
                else
                {
                    RpcSendArchiveSingle.Invoke((entry.Value.playerName, entry.Value.others, entry.Value.achievement));
                    yield return entry.Value.achievement.CoShowSocialBillboard(Vector2.zero, entry.Value.others == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, entry.Value.playerName, entry.Value.others);
                }
            }
        }
    }

    static ShowUp() => DIManager.Instance.RegisterModule(() => new ShowUp());
    public ShowUp() => ModSingleton<ShowUp>.Instance = this;

    static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AppealButton.png", 115f);
    static private readonly Image playButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.AchievementHistoryButton.png", 115f);
    static private readonly Image iconSprite = SpriteLoader.FromResource("Nebula.Resources.AppealIcon.png", 100f);

    private PickedUpArchive? pickedUpArchive = null;

    public bool CanAppealInLobby { get; private set; } = AmongUsClient.Instance.AmHost ? (ClientOption.ShowSocialSettingsOnLobby.Value && ClientOption.CanAppealInLobbyDefault.Value) : false;
    public bool CanAppealInGame { get; private set; } = false;
    public bool CanUseStamps { get; private set; } = true;
    public bool CanUseEmotes { get; private set; } = false;
    public Virial.Media.GUIWidget GetSettingWidget()
    {
        Virial.Media.GUIWidget GetCheckbox(Func<bool> getter, Action<bool> setter, string label)
        {
            var checkBox = new NoSGUICheckbox(Virial.Media.GUIAlignment.Center, getter.Invoke()) { OnValueChanged = setter };
            return GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center, checkBox,
            GUI.API.HorizontalMargin(0.05f),
            new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent), GUI.API.LocalizedTextComponent("social.appeal." + label))
            {
                OnClickText = (() => checkBox.Artifact.Do(entry => entry.toggle.Invoke()), false),
                OverlayWidget = ()=> GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "social.appeal."+ label + ".detail")
            }
            );
        }

        return GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center,
            new NoSGUIImage(Virial.Media.GUIAlignment.Center, iconSprite, new(0.5f, null), overlay: ()=>GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "social.appeal.detail")) { IsMasked = false },
            GUI.API.HorizontalMargin(0.05f),
            GetCheckbox(() => CanAppealInLobby, val => { RpcShareSocialSettings.Invoke((val, CanAppealInGame, CanUseStamps, CanUseEmotes)); ClientOption.CanAppealInLobbyDefault.Value = val; }, "lobby"),
            GUI.API.HorizontalMargin(0.15f),
            GetCheckbox(() => CanAppealInGame, val => RpcShareSocialSettings.Invoke((CanAppealInLobby, val, CanUseStamps, CanUseEmotes)), "game")
            );
    }

    GameObject InLobbySetting = null!;
    public bool ShouldBeShownSocialSettings => ClientOption.ShowSocialSettingsOnLobby.Value || (CanAppealInGame || CanAppealInLobby);
    protected override void OnInjected(Virial.Game.Game container)
    {
        this.Register(container);

        var showUpButton = new ModAbilityButtonImpl(true).Register(NebulaAPI.CurrentGame!);
        showUpButton.SetSprite(buttonSprite.GetSprite());
        showUpButton.Availability = (button) => true;
        showUpButton.Visibility = (button) => LobbyBehaviour.Instance ? CanAppealInLobby : CanAppealInGame;
        showUpButton.OnClick = (button) =>
        {
            button.StartCoolDown();
            RpcRequestShowUp.Invoke(PlayerControl.LocalPlayer.PlayerId);
        };
        showUpButton.CoolDownTimer = NebulaAPI.Modules.Timer(NebulaAPI.CurrentGame!, 5f).Start();
        showUpButton.SetLabel("appeal");

        bool fired = false;
        var playButton = new ModAbilityButtonImpl(true).Register(NebulaAPI.CurrentGame!);
        playButton.SetSprite(playButtonSprite.GetSprite());
        playButton.Availability = (button) => true;
        playButton.Visibility = (button) => LobbyBehaviour.Instance && NebulaAchievementManager.HasAnyAchievementResult && AmongUsClient.Instance.AmHost && !fired;
        playButton.OnClick = (button) =>
        {
            button.StartCoolDown();
            fired = true;
            PlayPickedUpAchievementsAsHost();
        };
        playButton.SetLabel("playAchievement");
    }

    float timer = 10f;
    int lastPlayerNum = 0;
    void OnUpdate(UpdateEvent ev)
    {
        if(timer > 0f) timer -= Time.fixedDeltaTime;
        try
        {
            if (lastPlayerNum != PlayerControl.AllPlayerControls.Count)
            {
                lastPlayerNum = PlayerControl.AllPlayerControls.Count;
                if (timer < 5f) timer = 5f;
            }
        }
        catch { }

        if (AnySocialShown && timer < 1f) timer = 1f;

        if (!InLobbySetting && LobbyBehaviour.Instance && AmongUsClient.Instance.AmHost && GameStartManager.InstanceExists && ShouldBeShownSocialSettings)
        {
            var widget = ModSingleton<ShowUp>.Instance.GetSettingWidget().Instantiate(new(100f, 100f), out _);
            widget!.AddComponent<SortingGroup>();
            widget.gameObject.ForEachAllChildren(c => c.layer = LayerExpansion.GetUILayer());
            widget.transform.SetParent(GameStartManager.Instance.transform, true);
            widget.transform.localPosition = new(-0.3f, -0.38f, -10f);
            InLobbySetting = widget;
        }

        if (AmongUsClient.Instance.AmHost && LobbyBehaviour.Instance && !AnySocialShown && PlayerControl.AllPlayerControls.Count >= 2 && !(timer > 0f) && lastClearedAchievementsQueue.Count > 0) {
            var sent = lastClearedAchievementsQueue.Dequeue();
            RpcSendLastCleared.Invoke((sent.playerName, sent.achievement));
        }
    }

    public PlayerControl? CurrentShowUp { get; private set; } = null;
    private HashSet<GameObject> AllShowUp = [];
    public bool AnyoneShowedUp => CurrentShowUp != null && CurrentShowUp;
    public bool AnySocialShown => AnyoneShowedUp || AllShowUp.Count > 0;
    public bool ShowedUp(GamePlayer player) => AnyoneShowedUp && CurrentShowUp!.PlayerId == player.PlayerId;

    static readonly TextAttribute TitleAttribute = new(GUI.API.GetAttribute(AttributeAsset.OverlayTitle)) { FontSize = new(1.2f, 0.7f, 1.2f), Size = new(3f,3f) };
    static readonly TextAttribute NameAttribute = new(GUI.API.GetAttribute(AttributeAsset.OverlayTitle)) { FontSize = new(2f) };
    public void ShowPlayer(PlayerControl? player, float angle, float duration = 2.5f)
    {
        if (player == null) return;

        string text = player.name;
        string titleText = "";
        if (NebulaGameManager.Instance!.TitleMap.TryGetValue(player.PlayerId, out var title) && title != null) titleText = title.GetTitleComponent(null).GetString();

        NebulaManager.Instance.StartCoroutine(ManagedEffects.Wait(() => AnyoneShowedUp, () =>
        {
            CurrentShowUp = player;

            NebulaGameManager.Instance?.WideCamera.SetDrawShadow(false);
            Vector3 lastPlayerPos = player.transform.position;
            NebulaManager.Instance.StartCoroutine(
                CoShowSocial("PlayerZoom", new(1.5f, -0.6f),
                GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                    GUI.API.RawText(Virial.Media.GUIAlignment.Center, TitleAttribute, titleText.Color(Color.Lerp(Color.white, DynamicPalette.PlayerColors[player.PlayerId], 0.25f))),
                    GUI.API.VerticalMargin(-0.05f),
                    GUI.API.RawText(Virial.Media.GUIAlignment.Center, NameAttribute, text)
                ),
                (obj, size) => { obj.transform.localScale = new(1.8f, 1.8f, 1f); }, duration,
                (0.3f, () =>
                {
                    var diff = (player.transform.position - lastPlayerPos);
                    lastPlayerPos += diff.Delta(5f, 0.0001f);
                    return lastPlayerPos + new Vector3(0.3f, -0.02f).RotateZ(angle);
                }, angle), true, 0.2f, 0.2f).WrapToIl2Cpp()
                );
            NebulaManager.Instance.StartCoroutine(ManagedEffects.Sequence(
                Effects.Wait(0.3f + duration).WrapToManaged(),
                ManagedEffects.Action(() =>
                {
                    CurrentShowUp = null;
                    NebulaGameManager.Instance?.WideCamera.SetDrawShadow(true);
                })
                ).WrapToIl2Cpp());
        }));
    }

    public void ShareSocialSettingsAsHost() => RpcShareSocialSettings.Invoke((CanAppealInLobby, CanAppealInGame, ClientOption.ShowStamps.Value, ClientOption.ShowEmotes.Value));

    private PriorityQueue<(INebulaAchievement achievement, string playerName), int> lastClearedAchievementsQueue = new();
    private HashSet<string> putPlayers = [];

    internal void PutLastClearedAchievements(string playerName, INebulaAchievement[] achievements)
    {
        if (putPlayers.Contains(playerName)) return;
        putPlayers.Add(playerName);

        lastClearedAchievementsQueue.EnqueueRange(achievements.Select(a => ((a, playerName), -a.Attention)));
    }

    internal void PutPickedUpAchievements(string playerName, INebulaAchievement[] achievements)
    {
        pickedUpArchive?.TryAdd(playerName, achievements);
    }

    public static RemoteProcess<(bool inLobby, bool inGame, bool canUseStamps, bool canUseEmotes)> RpcShareSocialSettings = new(
        "ShareSocialSetting",
        (message, _) =>
        {
            var instance = ModSingleton<ShowUp>.Instance;
            instance.CanAppealInLobby = message.inLobby;
            instance.CanAppealInGame = message.inGame;
            instance.CanUseStamps = message.canUseStamps;
            instance.CanUseEmotes = message.canUseEmotes;
        });
    private static RemoteProcess<(byte playerId, float angle, float duration)> RpcShowUp = new(
        "PlayShowUp",
        (message, _) =>
        {
            ModSingleton<ShowUp>.Instance.ShowPlayer(Helpers.GetPlayer(message.playerId), message.angle, message.duration);
        }
        );

    public static RemoteProcess<byte> RpcRequestShowUp = new(
        "RequestShowUp",
        (message, _) =>
        {
            if (AmongUsClient.Instance.AmHost && !(ModSingleton<ShowUp>.Instance?.AnyoneShowedUp ?? true)) RpcShowUp.Invoke((message, -(System.Random.Shared.NextSingle() * 8f), ClientOption.AppealDuration.Value switch { 0 => 2.5f, 1 => 3.8f, _ => 5.5f }));
        });

    public static RemoteProcess<(string playerName, INebulaAchievement ach)> RpcSendLastCleared = new(
        "SendLastClearedSocial", (message, _) =>
        {
            if (message.ach == null) return;
            NebulaManager.Instance.StartCoroutine(message.ach.CoShowSocialBillboard(new(-3.7f, 1.3f), INebulaAchievement.SocialMessageType.FirstCleared, message.playerName).WrapToIl2Cpp());
        });

    public static RemoteProcess<(string playerName, int others, INebulaAchievement ach)> RpcSendArchiveSingle = new(
        "SendArchiveSingleSocial", (message, sendMyself) =>
        {
            if (sendMyself) return;
            if (message.ach == null) return;
            NebulaManager.Instance.StartCoroutine(message.ach.CoShowSocialBillboard(Vector2.zero, message.others == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, message.playerName, message.others).WrapToIl2Cpp());
        });

    public static RemoteProcess<(bool shownType, string playerName1, int others1, INebulaAchievement ach1, string playerName2, int others2, INebulaAchievement ach2)> RpcSendArchiveDual = new(
        "SendArchiveDualSocial", (message, sendMyself) =>
        {
            if (sendMyself) return;
            Vector2 vector = message.shownType ? new(1.2f, 0.7f) : new(-1.2f, 0.7f);
            if (message.ach1 != null)
                NebulaManager.Instance.StartCoroutine(message.ach1.CoShowSocialBillboard(vector, message.others1 == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, message.playerName1, message.others1).WrapToIl2Cpp());
            if (message.ach2 != null)
                NebulaManager.Instance.StartCoroutine(message.ach2.CoShowSocialBillboard(-vector, message.others2 == 0 ? INebulaAchievement.SocialMessageType.Cleared : INebulaAchievement.SocialMessageType.ClearedMultiple, message.playerName2, message.others2).WrapToIl2Cpp());
        });

    public static RemoteProcess<int> RpcRequireAchievementArchive = new(
        "RequireAchievementArchive", (message, _) =>
        {
            NebulaAchievementManager.SendPickedUpAchievements();
        });
    
    public void PlayPickedUpAchievementsAsHost()
    {
        var currentArchive = new PickedUpArchive();
        pickedUpArchive = currentArchive;
        RpcRequireAchievementArchive.Invoke(0);

        NebulaManager.Instance.StartCoroutine(ManagedEffects.Sequence(Effects.Wait(0.5f).WrapToManaged(), currentArchive.CoShowAchievements(this)).WrapToIl2Cpp());
    }



    static private Image SocialBackgroundSprite = SpriteLoader.FromResource("Nebula.Resources.SocialBannerBack.png", 100f);
    public IEnumerator CoShowSocial(string objName, Vector2 pos, Virial.Media.GUIWidget widget, Action<GameObject, Virial.Compat.Size>? postBuilder, float duration, (float zoom, Func<Vector2> pos, float angle)? attention, bool hideHud, float fadeIn = 0.3f, float fadeOut = 0.6f, bool considerPlayerAppeal = false, bool considerOnlyLobby = false)
    {
        var widgetObj = widget.Instantiate(new(3f, 3f), out var size)!;
        postBuilder?.Invoke(widgetObj, size);

        var obj = UnityHelper.CreateObject(objName, HudManager.Instance.transform, pos.AsVector3(-30f));

        AllShowUp.Add(obj);

        obj.AddComponent<SortingGroup>();
        var back = UnityHelper.CreateObject<SpriteRenderer>("Background", obj.transform, new(0f, 0f, 0.1f));
        back.sprite = SocialBackgroundSprite.GetSprite();
        widgetObj!.transform.SetParent(obj.transform, true);
        widgetObj.transform.localPosition = new(0f, 0f, -1f);

        if (hideHud) HudManager.Instance.SetHudActive(false);
        if (attention != null)
        {
            NebulaGameManager.Instance?.WideCamera.SetAttention(new FunctionalAttention(() => attention.Value.angle, () => attention.Value.zoom, attention.Value.pos, FunctionalLifespan.GetTimeLifespan(duration + 0.3f)));
        }

        var texts = widgetObj.GetComponentsInChildren<TextMeshPro>().ToArray();
        var backColor = new Color(0.06f, 0.06f, 0.06f, 1f);
        foreach (var text in texts) text.outlineColor = backColor;

        float alpha;
        float p = 0f;
        while (p < 1f)
        {
            alpha = p;
            var alphaWhite = Color.white.AlphaMultiplied(alpha);
            foreach (var text in texts) text.color = alphaWhite;
            back.color = backColor.AlphaMultiplied(alpha);

            p += Time.deltaTime / fadeIn;
            yield return null;
        }

        foreach (var text in texts) text.color = Color.white;
        back.color = backColor;

        if (considerPlayerAppeal || considerOnlyLobby)
        {
            float timer = duration;
            while(timer > 0f && (!considerPlayerAppeal || !AnyoneShowedUp) && (!considerOnlyLobby || LobbyBehaviour.Instance))
            {
                timer -= Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            yield return Effects.Wait(duration);
        }

        if (hideHud) HudManager.Instance.SetHudActive(true);

        p = 1f;
        while (p > 0f)
        {
            alpha = p;
            var alphaWhite = Color.white.AlphaMultiplied(alpha);
            foreach (var text in texts) text.color = alphaWhite;
            back.color = backColor.AlphaMultiplied(alpha);

            p -= Time.deltaTime / fadeOut;
            yield return null;
        }

        AllShowUp.Remove(obj);

        GameObject.Destroy(obj);
    }
}
