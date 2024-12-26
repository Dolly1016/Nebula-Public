using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Abilities;

public class GuideLineAbility : IGameOperator
{
    SpriteRenderer guideRenderer;
    Func<bool> predicate;
    GamePlayer player;
    static private Image guideSprite = SpriteLoader.FromResource("Nebula.Resources.RaiderAxeGuide.png", 100f);
    public GuideLineAbility(GamePlayer player, Func<bool> predicate)
    {
        this.player = player;
        guideRenderer = UnityHelper.CreateObject<SpriteRenderer>("Guideline", player.VanillaPlayer.transform, Vector3.zero, LayerExpansion.GetObjectsLayer());
        guideRenderer.gameObject.SetActive(false);
        guideRenderer.transform.localScale = new(1.5f, 1.5f, 1f);
        guideRenderer.sprite = guideSprite.GetSprite();
        this.predicate = predicate;
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        guideRenderer.gameObject.SetActive(predicate.Invoke());
        float angle = player.Unbox().MouseAngle;
        float degree = angle * 180f / Mathf.PI;
        guideRenderer.transform.localEulerAngles = new(0f, 0f, degree);
        float p = Mathf.Repeat(Time.time / 1.2f, 1f);
        guideRenderer.transform.localPosition = (Vector2.right.Rotate(degree) * (1.4f + Mathf.Sin(p * Mathf.PI * 0.5f) * 0.8f)).AsVector3(-10f);
        guideRenderer.color = new(1f, 1f, 1f, 1f - (p * p * p));
    }

    void IGameOperator.OnReleased()
    {
        GameObject.Destroy(guideRenderer.gameObject);
    }
}
