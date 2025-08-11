using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Crewmate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;

namespace Nebula.Roles.MapLayer;


public class ShowPlayersMapLayer : MonoBehaviour
{
    ObjectPool<SpriteRenderer> darkIconPool = null!;
    ObjectPool<SpriteRenderer> lightIconPool = null!;
    Predicate<IPlayerlike> showPredicate = null!;
    Action<int>? postShownAction = null!;

    static ShowPlayersMapLayer() => ClassInjector.RegisterTypeInIl2Cpp<ShowPlayersMapLayer>();
    public void SetUp(Predicate<IPlayerlike> showPredicate, Action<int>? postAction)
    {
        this.showPredicate = showPredicate;
        this.postShownAction = postAction;
    }

    public void Awake()
    {
        darkIconPool = new(ShipStatus.Instance.MapPrefab.HerePoint, transform);
        darkIconPool.OnInstantiated = icon => PlayerMaterial.SetColors(new Color(0.3f, 0.3f, 0.3f), icon);

        lightIconPool = new(ShipStatus.Instance.MapPrefab.HerePoint, transform);
        lightIconPool.OnInstantiated = icon => PlayerMaterial.SetColors(new Color(1f, 1f, 1f), icon);
    }

    public void Update()
    {
        darkIconPool.RemoveAll();
        lightIconPool.RemoveAll();

        var center = VanillaAsset.GetMapCenter(AmongUsUtil.CurrentMapId);
        var scale = VanillaAsset.GetMapScale(AmongUsUtil.CurrentMapId);

        int alive = 0, shown = 0;

        foreach (var p in NebulaGameManager.Instance!.AllPlayerlike)
        {
            //自分自身、死んでいる場合は何もしない
            if (p.AmOwner || p.IsDead) continue;

            alive++;

            //不可視のプレイヤーは何もしない
            if (p.IsInvisible || p.Logic.InVent || p.IsDived) continue;

            if (showPredicate.Invoke(p))
            {
                var icon = (DynamicPalette.IsLightColor(DynamicPalette.PlayerColors[p.CurrentOutfit.outfit.ColorId]) ? lightIconPool : darkIconPool).Instantiate();
                icon.transform.localPosition = VanillaAsset.ConvertToMinimapPos(p.Position.ToUnityVector().AsVector3(p.Position.y / 1000f), center, scale);
                shown++;
            }
        }

        postShownAction?.Invoke(shown);
    }
}

