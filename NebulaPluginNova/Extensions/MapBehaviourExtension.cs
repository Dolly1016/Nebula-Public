using Nebula.Behavior;
using Virial.Game;

namespace Nebula.Extensions;

public static class MapBehaviourExtension
{
    public static bool CanIdentifyImpostors = false;
    public static bool CanIdentifyDeadBodies = false;
    public static bool AffectedByCommSab = true;
    public static bool AffectedByFakeAdmin = true;
    public static bool ShowDeadBodies = true;
    public static Color? MapColor = null;
    public static void InitializeModOption(this MapCountOverlay overlay)
    {
        CanIdentifyImpostors = false;
        CanIdentifyDeadBodies = false;
        AffectedByCommSab = true;
        AffectedByFakeAdmin = true;
        ShowDeadBodies = GeneralConfigurations.ShowDeadBodiesOnAdminOption;
        MapColor = null;
    }

    public static void SetModOption(this MapCountOverlay overlay, bool? canIdentifyImpostors = null, bool? canIdentifyDeadBodies = null, bool? affectedByCommSab = null, bool? affectedByFakeAdmin = null, bool? showDeadBodies = null, Color ? mapColor = null)
    {
        if (canIdentifyImpostors.HasValue) CanIdentifyImpostors = canIdentifyImpostors.Value;
        if (canIdentifyDeadBodies.HasValue) CanIdentifyDeadBodies = canIdentifyDeadBodies.Value;
        if (affectedByCommSab.HasValue) AffectedByCommSab = affectedByCommSab.Value;
        if (affectedByFakeAdmin.HasValue) AffectedByFakeAdmin = affectedByFakeAdmin.Value;
        if (showDeadBodies.HasValue) ShowDeadBodies = showDeadBodies.Value;
        if (mapColor.HasValue)
        {
            MapColor = mapColor.Value;
            overlay.BackgroundColor.SetColor(MapBehaviourExtension.MapColor ?? Color.green);
        }
    }

    public static void UpdateCount(this CounterArea counterArea, int cnt, int impostors, int deadBodies)
    {
        while (counterArea.myIcons.Count < cnt)
        {
            PoolableBehavior item = counterArea.pool.Get<PoolableBehavior>();
            counterArea.myIcons.Add(item);
        }
        while (counterArea.myIcons.Count > cnt)
        {
            PoolableBehavior poolableBehavior = counterArea.myIcons[counterArea.myIcons.Count - 1];
            counterArea.myIcons.RemoveAt(counterArea.myIcons.Count - 1);
            poolableBehavior.OwnerPool.Reclaim(poolableBehavior);
        }

        for (int i = 0; i < counterArea.myIcons.Count; i++)
        {
            int num = i % counterArea.MaxColumns;
            int num2 = i / counterArea.MaxColumns;
            float num3 = (float)(Mathf.Min(cnt - num2 * counterArea.MaxColumns, counterArea.MaxColumns) - 1) * counterArea.XOffset / -2f;
            counterArea.myIcons[i].transform.position = counterArea.transform.position + new Vector3(num3 + (float)num * counterArea.XOffset, (float)num2 * counterArea.YOffset, -1f);

            if (impostors > 0)
            {
                impostors--;
                PlayerMaterial.SetColors(Palette.ImpostorRed, counterArea.myIcons[i].GetComponent<SpriteRenderer>());
            }
            else if (deadBodies > 0)
            {
                deadBodies--;
                PlayerMaterial.SetColors(Palette.DisabledGrey, counterArea.myIcons[i].GetComponent<SpriteRenderer>());
            }
            else
            {
                PlayerMaterial.SetColors(new Color(224f / 255f, 255f / 255f, 0f / 255f), counterArea.myIcons[i].GetComponent<SpriteRenderer>());
            }
        }
    }

    public static void SetUpAsMinimapContent(GameObject obj)
    {
        obj.GetComponentsInChildren<Renderer>().Do(r => r.gameObject.AddComponent<MinimapScaler>());
        obj.GetComponentsInChildren<TMPro.TextMeshPro>().Do(t => t.gameObject.AddComponent<MinimapScaler>());
    }

    public static Vector3 GetMinimapFlippedScale() {
        var localPlayer = GamePlayer.LocalPlayer?.Unbox();
        if (localPlayer == null) return Vector3.one;

        Vector3 scale = new(1f, 1f, 1f);
        if (localPlayer.CountAttribute(PlayerAttributes.FlipX) % 2 == 1) scale.x *= -1f;
        if (localPlayer.CountAttribute(PlayerAttributes.FlipY) % 2 == 1) scale.y *= -1f;
        if (localPlayer.CountAttribute(PlayerAttributes.FlipXY) % 2 == 1) scale *= -1f;

        return scale;
    }

    public static void UpdateScale(MapBehaviour mapBehaviour)
    {
        Vector2 scale = GetMinimapFlippedScale();

        Vector2 currentScale = mapBehaviour.transform.localScale;

        bool flipX = currentScale.x * scale.x < 0f;
        bool flipY = currentScale.y * scale.y < 0f;

        void Flip(Transform transform)
        {
            if (!flipX && !flipY) return;
            var scale = transform.localScale;
            if (flipX) scale.x = -scale.x;
            if (flipY) scale.y = -scale.y;
            transform.localScale = scale;
        }

        Flip(mapBehaviour.transform);
    }
}
