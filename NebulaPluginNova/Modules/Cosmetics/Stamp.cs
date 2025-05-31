using BepInEx.Unity.IL2CPP.Utils;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NAudio.Dmo;
using NAudio.Wave.SampleProviders;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Modules.ScriptComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using Virial;
using Virial.Media;
using Virial.Text;

namespace Nebula.Modules.Cosmetics;

public static class StampHelpers
{
    public const float DefaultDuration = 2f;
    static public GameObject SpawnStamp(int colorId, CosmicStamp stamp, Sprite background, Vector2 pivot, Transform? transform, Vector3 localPos, float duration = DefaultDuration)
    {
        var holder = UnityHelper.CreateObject("Stamp", transform, localPos);
        holder.AddComponent<SortingGroup>();
        var backRenderer = UnityHelper.CreateObject<SpriteRenderer>("Background", holder.transform, -pivot, LayerExpansion.GetUILayer());
        backRenderer.sprite = background;
        backRenderer.color = new(0.15f, 0.15f, 0.15f);
        var widget = stamp.GetStampWidget(null, colorId, GUIAlignment.Center, false);
        var instantiated = widget.Instantiate(new(100f, 100f), out _);
        if (instantiated != null)
        {
            var lastScale = instantiated.transform.localScale;
            instantiated.transform.SetParent(backRenderer.transform);
            instantiated.transform.localPosition = new Vector3(0f, 0f, -0.1f);
            instantiated.transform.localScale = lastScale;
        }

        holder.transform.localScale = Vector3.one * 0f;
        float p = 0f;
        IEnumerator CoUpdate()
        {
            while (holder)
            {
                holder.gameObject.SetActive(!NebulaGameManager.Instance!.WideCamera.HasAttention);
                yield return null;
            }
        }

        IEnumerator CoShowStamp()
        {
            while (p < 1f && holder)
            {
                holder.transform.localScale = Vector3.one * p;
                yield return null;
                p += Time.deltaTime * 6.4f;
            }
            p = 1f;
            if (!holder) yield break;
            holder.transform.localScale = Vector3.one;
            yield return Effects.Wait(duration);
            while (p > 0f && holder)
            {
                holder.transform.localScale = Vector3.one * p;
                yield return null;
                p -= Time.deltaTime * 3.2f;
            }
            if (holder) UnityEngine.Object.Destroy(holder);
            yield break;
        }
        NebulaManager.Instance.StartCoroutine(CoShowStamp().WrapToIl2Cpp());
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        return holder;
    }

    static public Virial.Media.GUIWidget GetStampLabelWidget(this CosmicStamp? stamp, string? prodId, string? additionalText = null)
    {
        List<Virial.Media.GUIWidget> widgets = [
            GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OverlayTitle), stamp?.Name ?? prodId + Language.Translate("ui.stamp.loading") ?? "Unknown Stamp"),
        ];
        if (stamp?.Author != null) widgets.Add(GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OverlayContent), "by " + stamp!.Author));

        if (additionalText != null)
        {
            widgets.Add(GUI.API.VerticalMargin(0.15f));
            widgets.Add(GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OverlayContent), additionalText.Color(new Color(0.7f, 0.7f, 0.7f)).Sized(80)));
        }

        return GUI.API.VerticalHolder(GUIAlignment.Left, widgets);
    }

    static private Image unknownStampImage = SpriteLoader.FromResource("Nebula.Resources.UnknownStamp.png", 100f);
    static public Virial.Media.GUIWidget GetStampWidget(this CosmicStamp? stamp, string? prodId, int colorId, GUIAlignment alignment, bool isMasked, float size = 0.6f, GUIClickableAction? onClick = null, GUIWidgetSupplier? overlay = null, Action? onMouseOver = null, Action? onMouseOut = null)
    {
        return new NoSGUIImage(alignment, stamp?.Image?.MainSpriteLoader?.AsLoader(0)! ?? unknownStampImage, new(size, size), null, onClick, overlay)
        {
            IsMasked = isMasked,
            PostBuilder = (renderer) =>
            {
                var button = renderer.GetComponent<PassiveButton>();

                if (onMouseOver != null) button.OnMouseOver.AddListener(onMouseOver);
                if (onMouseOut != null) button.OnMouseOut.AddListener(onMouseOut);

                renderer.gameObject.AddComponent<SortingGroup>();
                SpriteRenderer exRenderer = null!;
                if (stamp?.Image!.HasExImage ?? false)
                {
                    exRenderer = UnityHelper.CreateObject<SpriteRenderer>("ExImage", renderer.transform, new(0f, 0f, stamp.Image!.ExIsFront ? -0.01f : 0.01f));
                    exRenderer.sprite = stamp.Image.GetExSprite(0);
                    exRenderer.sortingOrder = renderer.sortingOrder;
                    exRenderer.sortingGroupOrder = renderer.sortingGroupOrder;
                    exRenderer.maskInteraction = renderer.maskInteraction;

                    if (onClick != null)
                    {
                        button.OnMouseOver.AddListener(() => exRenderer.color = new(0.7f, 1f, 0.7f));
                        button.OnMouseOut.AddListener(() => exRenderer.color = Color.white);
                    }
                }
                if (stamp?.Adaptive ?? false)
                {
                    renderer.material = isMasked ? HatManager.Instance.MaskedPlayerMaterial : HatManager.Instance.PlayerMaterial;
                    PlayerMaterial.SetColors(colorId, renderer);
                }

                float time = 0f;
                int index = 0;
                IEnumerator CoUpdate()
                {
                    while (renderer)
                    {
                        time += Time.deltaTime;
                        if (time > 1f / stamp!.GetFPS(index, stamp.Image))
                        {
                            index = (index + 1) % (stamp.Image!.Length ?? 1);
                            renderer.sprite = stamp.Image!.GetSprite(index);
                            if (exRenderer) exRenderer.sprite = stamp.Image!.GetExSprite(index);
                        }
                        yield return null;
                    }
                }
                if (stamp != null) NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
            }
        };
    }

    public static void TryShowStampRingMenu(Func<bool> showWhile)
    {

        bool gameIsNotStated = !GameManager.Instance || !GameManager.Instance.GameHasStarted;
        bool inMeeting = MeetingHud.Instance || ExileController.Instance || IntroCutscene.Instance || NebulaPreSpawnMinigame.PreSpawnMinigame;
        bool isDead = PlayerControl.LocalPlayer && PlayerControl.LocalPlayer.Data != null && PlayerControl.LocalPlayer.Data.IsDead;
        bool canSeeSomeStamps = inMeeting || (isDead && NebulaGameManager.Instance!.CanSeeAllInfo);

        bool shouldShowStampMenu = canSeeSomeStamps;
        bool shouldShowEmoteMenu = !inMeeting && (gameIsNotStated || !isDead);

        if (shouldShowEmoteMenu)
        {
            if (ModSingleton<ShowUp>.Instance.CanUseStamps && (gameIsNotStated || ModSingleton<ShowUp>.Instance.CanUseEmotes))
            {
                var emotes = EmoteManager.AllEmotes.Where(e => !LobbyBehaviour.Instance || e.Value.CanPlayInLobby);
                NebulaManager.Instance.ShowRingMenu(emotes.Select(emote => new RingMenu.RingMenuElement(emote.Value.LocalIconSupplier, () =>
                {
                    EmoteManager.SendLocalEmote(emote.Key);
                })).ToArray(), showWhile, null);
            }
            else
            {
                //DebugScreen.Push(Language.Translate("ui.error.emote.notAllowed"), 3f);
            }
        }
        else if (shouldShowStampMenu)
        {
            if (ModSingleton<ShowUp>.Instance.CanUseStamps)
            {
                var stamps = StampManager.GetTableStamps().ToArray();
                NebulaManager.Instance.ShowRingMenu(stamps.Select(stamp => new RingMenu.RingMenuElement(stamp.GetStampWidget(null, PlayerControl.LocalPlayer.PlayerId, GUIAlignment.Center, false, stamps.Length < 7 ? 0.45f : 0.4f), () =>
                {
                    StampManager.SendStamp(stamp);
                })).ToArray(), showWhile, () => DebugScreen.Push(Language.Translate("ui.error.stamp.notLoaded"), 3f));
            }
            else
            {
                DebugScreen.Push(Language.Translate("ui.error.stamp.notAllowed"), 3f);
            }
        }
    }

    public static void SetStampShowerToUnderHud(Transform transform, float z, Func<bool>? aliveWhile = null)
    {
        GamePlayer.AllPlayers.Do(p =>
        {
            p.Unbox().SpecialStampShower = PopupStampShower.GetHudShower(p.PlayerId, transform, z, aliveWhile);
        });
    }

    public static void ResetStampShower()
    {
        GamePlayer.AllPlayers.Do(p =>
        {
            p.Unbox().SpecialStampShower = null;
        });
    }

}

[NebulaPreprocess(PreprocessPhase.FixStructure), NebulaRPCHolder]
internal static class StampManager
{
    public const int MaxStamps = 9;
    static private readonly DataSaver MySaver = new("Stamps");
    static private readonly StringArrayDataEntry MyStamps = new("Table", MySaver, [
        "stamp_セオノ_Nice!", "stamp_セオノ_Bad","stamp_セオノ_ぴえん",
        ]);
    public static string[] CurrentTable => MyStamps.Value;
    public static void UpdateTable(string stamp, int index)
    {
        var table = CurrentTable;
        if (index >= table.Length)
        {
            var newTable = new string[index + 1];
            for (int i = 0; i < newTable.Length; i++)
            {
                newTable[i] = i < table.Length ? table[i] : "";
            }
            newTable[index] = stamp;
            MyStamps.Value = newTable;
        }
        else
        {
            table[index] = stamp;
            MyStamps.Value = table;
        }
    }

    public static void AddToTable(string stamp)
    {
        var table = CurrentTable;
        if (CurrentTable.Any(s => s == stamp)) return;
        MyStamps.Value = [.. table, stamp];
    }

    public static void SetToTable(string old, string stamp)
    {
        var table = CurrentTable.ToList();
        table.RemoveAll(s => s == stamp);
        int index = table.IndexOf(old);
        if (index != -1) table[index] = stamp;
        MyStamps.Value = table.ToArray();
    }

    public static void RemoveFromTable(string stamp)
    {
        var table = CurrentTable.ToList();
        table.RemoveAll(s => s == stamp);
        MyStamps.Value = table.ToArray();
    }

    public static CosmicStamp? GetStamp(int index)
    {
        var table = CurrentTable;
        if (0 <= index && index < table.Length) return MoreCosmic.AllStamps.TryGetValue(table[index], out var stamp) ? stamp : null;
        return null;
    }

    public static IEnumerable<CosmicStamp> GetTableStamps()
    {
        var table = CurrentTable;
        for (int i = 0; i < table.Length; i++)
        {
            var stamp = GetStamp(i);
            if (stamp != null) yield return stamp;
        }
    }

    private static RemoteProcess<(byte playerId, string stampProdId)> RpcShowStamp = new("ShowStamp", (message, _) =>
    {
        if (MoreCosmic.AllStamps.TryGetValue(message.stampProdId, out var stamp))
        {
            var player = GamePlayer.GetPlayer(message.playerId)?.Unbox();
            if (player != null)
            {
                if (!player.IsDead || NebulaGameManager.Instance!.CanSeeAllInfo) player.StampShower.Show(stamp, player.PlayerId);
            }
            else
            {
                var vanillaPlayer = Helpers.GetPlayer(message.playerId);
                if (vanillaPlayer != null) LobbyStampShower.Show(stamp, vanillaPlayer);
            }
        }
    });
    static public void SendStamp(CosmicStamp stamp) => SendStamp(stamp.ProductId);
    static public void SendStamp(string stampProdId) => RpcShowStamp.Invoke((PlayerControl.LocalPlayer.PlayerId, stampProdId));
}

internal interface IStampShower
{
    void Show(CosmicStamp stamp, int colorId);
    bool IsValid { get; }
}

internal class PopupStampShower : IStampShower
{
    Image backgroundSprite;
    Vector2 pivot;
    GameObject? lastShown = null;
    Transform parent;
    Vector3 localPos;
    Func<bool>? aliveWhile;
    Func<Vector3>? localAnimatedPos = null;
    bool withName = false;
    static internal readonly SpriteLoader MeetingBack = SpriteLoader.FromResource("Nebula.Resources.StampBackground.png", 100f);
    static private readonly SpriteLoader ExileBack = SpriteLoader.FromResource("Nebula.Resources.StampBackground_Exiled.png", 100f);
    public PopupStampShower(Image backgroundSprite, Vector2 pivot, Transform parent, Vector3 localPos, Func<bool>? aliveWhile, bool withName)
    {
        this.backgroundSprite = backgroundSprite;
        this.parent = parent;
        this.localPos = localPos;
        this.pivot = pivot;
        this.aliveWhile = aliveWhile;
        this.withName = withName;
    }

    static public PopupStampShower GetMeetingShower(PlayerVoteArea voteArea) => new(MeetingBack, new(-0.7f, 0.11f), voteArea.transform, new(-0.7f, 0.15f, -5f), null, false);
    static public PopupStampShower GetExiledShower(PoolablePlayer player, Vector2 diff, Func<bool>? aliveWhile, bool vertical) => new(vertical ? ExileBack : MeetingBack, vertical ? new(-0.25f, -0.55f) : new(-0.7f, 0.11f), HudManager.Instance.transform, Vector3.zero, aliveWhile != null ? () => player && aliveWhile.Invoke() : () => (bool)player, false) { localAnimatedPos = () => player.transform.TransformPointLocalToLocal(Vector3.zero, HudManager.Instance.transform) + diff.AsVector3(-5f) };
    static public PopupStampShower GetHudShower(byte playerId, Transform hud, float z, Func<bool>? aliveWhile) => new(ExileBack, new(-0.25f, -0.55f), hud.transform, new(-4f + 8f * playerId / GamePlayer.AllOrderedPlayers.Count, -3f, z), aliveWhile, true);

    public void Show(CosmicStamp stamp, int colorId)
    {
        if (lastShown) UnityEngine.Object.Destroy(lastShown);
        lastShown = StampHelpers.SpawnStamp(colorId, stamp, backgroundSprite.GetSprite(), pivot, parent, localAnimatedPos?.Invoke() ?? localPos);
        if (lastShown && withName)
        {
            var text = GUI.API.RawText(GUIAlignment.Left, GUI.API.GetAttribute(AttributeAsset.OverlayTitle), GamePlayer.GetPlayer((byte)colorId)?.Name ?? "");
            var instantiated = text.Instantiate(new Anchor(new(0f, 0.5f), new(0f, 0f, 0f)), new(10f, 10f), out _);
            if (instantiated != null)
            {
                instantiated.transform.SetParent(lastShown.transform);
                instantiated.transform.localPosition = new(0.09f, 0.91f, -0.1f);
                instantiated.transform.localScale = new(0.7f, 0.7f, 1f);
            }
        }
        if (lastShown && localAnimatedPos != null)
        {
            IEnumerator CoUpdatePos(GameObject myStamp)
            {
                while (myStamp && IsValid)
                {
                    myStamp.transform.localPosition = localAnimatedPos!.Invoke();
                    yield return null;
                }

                //無効になってもスタンプが残っていたら消去する。(位置が移動するスタンプのみ)
                if (myStamp) UnityEngine.Object.Destroy(myStamp);
            }
            NebulaManager.Instance.StartCoroutine(CoUpdatePos(lastShown).WrapToIl2Cpp());
        }
    }

    public bool IsValid => parent && (aliveWhile?.Invoke() ?? true);
}

internal class ConditionalStampShower : IStampShower
{
    IStampShower showerMain, showerAnother;
    Func<bool> predicate;
    public ConditionalStampShower(IStampShower showerMain, IStampShower showerAnother, Func<bool> predicate)
    {
        this.showerMain = showerMain;
        this.showerAnother = showerAnother;
        this.predicate = predicate;
    }

    public bool IsValid => showerMain.IsValid && showerAnother.IsValid;

    void IStampShower.Show(CosmicStamp stamp, int colorId)
    {
        if (predicate.Invoke())
            showerMain.Show(stamp, colorId);
        else
            showerAnother.Show(stamp, colorId);
    }
}
internal static class LobbyStampShower
{
    static Dictionary<byte, GameObject> lastShown = [];
    static public void Show(CosmicStamp stamp, PlayerControl player)
    {
        if (lastShown.TryGetValue(player.PlayerId, out var obj) && obj) UnityEngine.Object.Destroy(obj);

        var myShown = StampHelpers.SpawnStamp(player.PlayerId, stamp, PopupStampShower.MeetingBack.GetSprite(), new(-0.7f, 0.11f), null, Vector3.zero);
        lastShown[player.PlayerId] = myShown;

        IEnumerator CoUpdate()
        {
            while (myShown && player)
            {
                var pos = NebulaGameManager.Instance!.WideCamera.ConvertToWideCameraPos(player.transform.position);
                myShown.transform.localPosition = pos + ArrowStampShower.ArrowDiff;
                myShown.transform.SetLocalZ(-5f);
                yield return null;
            }
            if (!player && myShown) UnityEngine.Object.Destroy(myShown);
        }
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
    }
}

internal class ArrowStampShower : IStampShower
{
    Arrow? lastArrow = null;
    GameObject? lastShown = null;
    GamePlayer player;

    public ArrowStampShower(GamePlayer player)
    {
        this.player = player;
    }

    static internal Vector3 ArrowDiff = new(0.24f, 0.2f, -5f);
    public void Show(CosmicStamp stamp, int colorId)
    {
        if (lastArrow != null) lastArrow.Release();
        if (lastShown != null) UnityEngine.Object.Destroy(lastShown);

        if (!NebulaGameManager.Instance!.CanSeeAllInfo) return;

        var myArrow = new Arrow(null, false, false, true)
        {
            FixedAngle = true,
            DisappearanceEffect = Arrow.DisappearanceType.Reduction,
            IsAffectedByComms = false,
            OnJustPoint = true,
            ShowOnlyOutside = true,
        };
        myArrow.SetSprite(null);

        var myShown = StampHelpers.SpawnStamp(colorId, stamp, PopupStampShower.MeetingBack.GetSprite(), new(-0.7f, 0.11f), null, Vector3.zero);
        var widget = stamp.GetStampWidget(null, colorId, GUIAlignment.Center, false);
        var instantiated = widget.Instantiate(new(10f, 10f), out _);
        if (instantiated != null)
        {
            instantiated.layer = LayerExpansion.GetArrowLayer();
            instantiated.ForEachAllChildren(obj => obj.layer = LayerExpansion.GetArrowLayer());
            instantiated.transform.SetParent(myArrow.ArrowObject.transform);
            instantiated.transform.localPosition = Vector3.zero;
            instantiated.transform.localScale = new(0.37f, 0.37f, 1f);
        }

        myArrow.IsActive = false;
        myArrow.ArrowObject.SetActive(false);

        lastArrow = myArrow;
        lastShown = myShown;

        NebulaManager.Instance.StartDelayAction(StampHelpers.DefaultDuration, () => myArrow.MarkAsDisappering());

        IEnumerator CoUpdate()
        {
            myArrow.IsActive = true;
            while (myShown)
            {
                var pos = NebulaGameManager.Instance!.WideCamera.ConvertToWideCameraPos(player.Position.ToUnityVector());
                myShown.transform.localPosition = pos + ArrowDiff;
                myShown.transform.SetLocalZ(-5f);
                myArrow.TargetPos = player.Position;
                yield return null;
            }
        }
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
    }

    public bool IsValid => true;
}
