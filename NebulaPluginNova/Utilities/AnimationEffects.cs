using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial.Media;

namespace Nebula.Utilities;

static internal class AnimationEffects
{
    static private readonly MultiImage NameEffectsImage = DividedSpriteLoader.FromResource("Nebula.Resources.RoleNameEffect.png", 100f, 1, 5);

    static public IEnumerator CoPlayRoleNameEffect(Transform parent, Vector3 localPos, Color color, int layer, float scale = 1f)
    {
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Anim", parent, localPos, layer);
        renderer.transform.localScale = new(scale, scale, 1f);
        renderer.color = color;
        for (int i = 0; i < 5; i++)
        {
            if (!renderer) break;
            renderer.sprite = NameEffectsImage.GetSprite(i);
            yield return new WaitForSeconds(0.09f);
        }
        if (renderer) UnityEngine.Object.Destroy(renderer.gameObject);
        yield break;
    }

    static public IEnumerator CoPlayRoleNameEffect(GamePlayer player) => CoPlayRoleNameEffect(player.RoleText.transform, new(0f, 0f, -0.1f), player.Role.Role.Color.ToUnityColor(), player.RoleText.gameObject.layer, 1f / 0.7f);
}
