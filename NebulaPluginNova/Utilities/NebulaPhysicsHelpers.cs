using System.Reflection.Metadata.Ecma335;
using UnityEngine.Experimental.Audio;

namespace Nebula.Utilities;

public static class NebulaPhysicsHelpers
{

    public static bool AnyShadowBetween(Vector2 source, Vector2 target, out float distance)
    {
        return AnyShadowBetween(source, (target - source).normalized, (target - source).magnitude, out distance);
    }
    public static bool AnyShadowBetween(Vector2 source, Vector2 dirNorm, float mag, out float distance) => AnyNonTriggersBetween(source, dirNorm, mag, Constants.ShadowMask, out distance, collider => !IsOneWayShadow(collider));
    public static bool AnyNonTriggersBetween(Vector2 source, Vector2 dirNorm, float mag, int layerMask, out float distance, Func<Collider2D, bool>? predicate = null)
    {
        int num = Physics2D.RaycastNonAlloc(source, dirNorm, PhysicsHelpers.castHits, mag, layerMask);
        bool result = false;
        distance = mag;
        for (int i = 0; i < num; i++)
        {
            var collider = PhysicsHelpers.castHits[i].collider;

            if (!(predicate?.Invoke(collider) ?? true)) continue;

            if (!collider.isTrigger)
            {
                
                float d = source.Distance(PhysicsHelpers.castHits[i].point);
                if (d < distance)
                {
                    distance = d;
                    result = true;
                }
            }
        }
        return result;
    }

    private static bool IsOneWayShadow(Collider2D collider)
    {
        foreach(var key in LightSource.OneWayShadows.Keys) if (key.GetInstanceID() == collider.gameObject.GetInstanceID()) return true;
        return false;
    }
}
