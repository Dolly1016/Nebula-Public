using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Media;

namespace Nebula.Modules.Cosmetics.EmoteVariations;

file static class HandEmoteHelpers
{
    public static MultiImage Hand = DividedSpriteLoader.FromResource("Nebula.Resources.EmoteHand.png", 100f, 2, 2);
    public static IEnumerator CoUpdateHand(SpriteRenderer renderer, CosmeticsLayer layer, PlayerPhysics physics, bool negativeOnFlipped)
    {
        var anim = physics.Animations;
        
        while (renderer)
        {
            bool shouldNegative = layer.FlipX == negativeOnFlipped;
            renderer.flipX = layer.FlipX;
            if (shouldNegative == renderer.transform.localPosition.x > 0f)
            {
                renderer.transform.SetLocalX(-renderer.transform.localPosition.x);
            }

            float angle = renderer.transform.localEulerAngles.z;
            if (angle > 180f) angle -= 360f;
            if (shouldNegative == angle > 0f)
            {
                renderer.transform.localEulerAngles = new(0f, 0f, -angle);
            }

            renderer.enabled = anim.IsPlayingRunAnimation() || anim.Animator.GetCurrentAnimation() == anim.group.IdleAnim;

            yield return null;
        }
    }

    public static EmoteHandBehaviour GetOrAddEmoteHand(PlayerControl player)
    {
        var child = player.cosmetics.transform.FindChild("EmoteHand");
        if (child) return child.GetComponent<EmoteHandBehaviour>();

        var hand = UnityHelper.CreateObject<EmoteHandBehaviour>("EmoteHand", player.cosmetics.transform, new(0f, 0f, 0f), LayerExpansion.GetPlayersLayer());
        hand.Initialize(player.cosmetics);
        return hand;
    }
}

internal class EmoteHandBehaviour : MonoBehaviour
{
    static EmoteHandBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<EmoteHandBehaviour>();
    private CosmeticsLayer layer;
    private SpriteAnimNodeSync nodeSync;
    public void Initialize(CosmeticsLayer layer)
    {
        this.layer = layer;
    }

    void LateUpdate()
    {
        if (!nodeSync && layer) layer.visor.TryGetComponent<SpriteAnimNodeSync>(out nodeSync);

        if (nodeSync)
        {
            float y = nodeSync.Parent.GetLocalPosition(nodeSync.NodeId, false).y;
            transform.localPosition = new(0f, y - 0.55f, 0f);
        }
    }
}

internal class BadEmote : AbstractEmote
{
    private static Image icon = HandEmoteHelpers.Hand.AsLoader(3);

    protected override Image IconSprite => icon;
    public override IEnumerator CoPlayEmote(PlayerControl player)
    {
        var hand = UnityHelper.CreateObject<SpriteRenderer>("EmoteHandRenderer", HandEmoteHelpers.GetOrAddEmoteHand(player).transform, new(1.1f, 0.15f, 0f));
        GamePlayer.GetPlayer(player.PlayerId)?.Unbox().AddPlayerColorRenderers(hand);
        hand.transform.localScale = new(1.4f, 1.4f, 1f);
        hand.material = player.cosmetics.currentBodySprite.BodySprite.sharedMaterial;
        NebulaManager.Instance.StartCoroutine(HandEmoteHelpers.CoUpdateHand(hand, player.cosmetics, player.MyPhysics, true).WrapToIl2Cpp());
        hand.sprite = HandEmoteHelpers.Hand.GetSprite(2);
        yield return Effects.Wait(0.15f);
        if (hand) hand.sprite = HandEmoteHelpers.Hand.GetSprite(3);
        for(int i = 0; i < 3; i++)
        {
            if (hand) hand.transform.localPosition = new(1.1f.FlipIf(player.cosmetics.FlipX), 0.18f, 0f);
            yield return Effects.Wait(0.05f);
            if (hand) hand.transform.localPosition = new(1.1f.FlipIf(player.cosmetics.FlipX), 0.14f, 0f);
            yield return Effects.Wait(0.05f);
            if (hand) hand.transform.localPosition = new(1.1f.FlipIf(player.cosmetics.FlipX), 0.1f, 0f);
            yield return Effects.Wait(0.25f);
        }
        yield return Effects.Wait(0.8f);
        if (hand)
        {
            hand.enabled = false;
            GameObject.Destroy(hand.gameObject);
        }
    }
}

internal class GoodEmote : AbstractEmote
{
    private static Image icon = HandEmoteHelpers.Hand.AsLoader(1);

    protected override Image IconSprite => icon;
    public override IEnumerator CoPlayEmote(PlayerControl player)
    {
        var hand = UnityHelper.CreateObject<SpriteRenderer>("EmoteHandRenderer", HandEmoteHelpers.GetOrAddEmoteHand(player).transform, new(1.05f, 0.14f, 0f));
        GamePlayer.GetPlayer(player.PlayerId)?.Unbox().AddPlayerColorRenderers(hand);
        hand.transform.localScale = new(1.4f, 1.4f, 1f);
        hand.transform.localEulerAngles = new(0f, 0f, 20f);
        hand.material = player.cosmetics.currentBodySprite.BodySprite.sharedMaterial;
        NebulaManager.Instance.StartCoroutine(HandEmoteHelpers.CoUpdateHand(hand, player.cosmetics, player.MyPhysics, true).WrapToIl2Cpp());
        hand.sprite = HandEmoteHelpers.Hand.GetSprite(0);
        yield return Effects.Wait(0.15f);
        if (hand)
        {
            hand.sprite = HandEmoteHelpers.Hand.GetSprite(1);
            hand.transform.localPosition = new(1.1f.FlipIf(player.cosmetics.FlipX), 0.1f, 0f);
            hand.transform.localEulerAngles = new(0f, 0f, 0f);
        }
        yield return Effects.Wait(1f);
        if (hand)
        {
            hand.enabled = false;
            GameObject.Destroy(hand.gameObject);
        }
    }
}

internal class GreetingEmote : AbstractEmote
{
    private static Image icon = new WrapSpriteLoader(() => VanillaAsset.MapAsset[4].ExileCutscenePrefab.Player.transform.GetChild(3).GetComponent<SpriteRenderer>().sprite);

    protected override Image IconSprite => icon;
    public override IEnumerator CoPlayEmote(PlayerControl player)
    {
        var hand = UnityHelper.CreateObject<SpriteRenderer>("EmoteHandRenderer", HandEmoteHelpers.GetOrAddEmoteHand(player).transform, new(1.05f, 0.6f, 0f));
        GamePlayer.GetPlayer(player.PlayerId)?.Unbox().AddPlayerColorRenderers(hand);
        hand.transform.localScale = new(0.9f, 0.9f, 1f);
        hand.transform.localEulerAngles = new(0f, 0f, 30f);
        hand.material = player.cosmetics.currentBodySprite.BodySprite.sharedMaterial;
        NebulaManager.Instance.StartCoroutine(HandEmoteHelpers.CoUpdateHand(hand, player.cosmetics, player.MyPhysics, true).WrapToIl2Cpp());
        hand.sprite = icon.GetSprite();
        for (int i = 0; i < 5; i++)
        {
            yield return Effects.Wait(0.1f);
            if (hand)
            {
                hand.transform.localEulerAngles = new(0f, 0f, 40f.FlipIf(player.cosmetics.FlipX));
                hand.transform.SetLocalX(0.98f.FlipIf(player.cosmetics.FlipX));
            }
            yield return Effects.Wait(0.1f);
            if (hand)
            {
                hand.transform.localEulerAngles = new(0f, 0f, 35f.FlipIf(player.cosmetics.FlipX));
                hand.transform.SetLocalX(1.02f.FlipIf(player.cosmetics.FlipX));
            }
            yield return Effects.Wait(0.05f);
            if (hand)
            {
                hand.transform.localEulerAngles = new(0f, 0f, 30f.FlipIf(player.cosmetics.FlipX));
                hand.transform.SetLocalX(1.05f.FlipIf(player.cosmetics.FlipX));
            }
        }
        if (hand)
        {
            hand.enabled = false;
            GameObject.Destroy(hand.gameObject);
        }
    }
}