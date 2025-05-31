using Discord;
using Il2CppInterop.Runtime.Injection;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using static Il2CppSystem.Globalization.CultureInfo;

namespace Nebula.Behavior;

public class CustomKillOverlayData
{
    static private Dictionary<IntPtr, CustomKillOverlayData> dataMap = new();
    public static bool TryGet(KillOverlayInitData vanillaData, [MaybeNullWhen(false)] out CustomKillOverlayData data) => dataMap.TryGetValue(vanillaData.Pointer, out data);

    private KillOverlayInitData vanillaInitData = new KillOverlayInitData(null, PlayerBodyTypes.Normal, null, PlayerBodyTypes.Normal);
    public KillOverlayInitData VanillaData => vanillaInitData;

    private GameObject myPrefab;

    public CustomKillOverlayData(GameObject myPrefab)
    {
        dataMap[vanillaInitData.Pointer] = this;

        this.myPrefab = myPrefab;
    }
    
    public void Initialize(CustomKillOverlay overlay)
    {
        if (myPrefab) GameObject.Destroy(myPrefab);
        Remove();
    }

    private void Remove()
    {
        dataMap.Remove(vanillaInitData.Pointer);
    }

    
    
}
public class CustomKillOverlay : OverlayKillAnimation
{
    static CustomKillOverlay() => ClassInjector.RegisterTypeInIl2Cpp<CustomKillOverlay>();

    CustomKillOverlayData initData = null;
    
    public override void Initialize(KillOverlayInitData initData)
    {
        if (CustomKillOverlayData.TryGet(initData, out var data))
        {
            data.Initialize(this);
            this.initData = data;
        }
    }

    public override Il2CppSystem.Collections.IEnumerator CoShow(KillOverlay parent)
    {
        System.Collections.IEnumerator CoShowInternal()
        {
            var vanillaAnim = parent.KillAnims[0];

            if (Constants.ShouldPlaySfx())　SoundManager.Instance.PlaySound(vanillaAnim.Stinger, false, 1f, null).volume = vanillaAnim.StingerVolume;

            var flameParent = GameObject.Instantiate(parent.flameParent, NebulaGameManager.Instance!.WideCamera.Camera.transform);
            flameParent.transform.GetChild(0).gameObject.layer = LayerExpansion.GetDefaultLayer();
            flameParent.transform.localPosition = new(0f, 0f, -50f);
            flameParent.SetActive(true);
            flameParent.transform.localScale = new Vector3(1f, 0.3f, 1f);
            flameParent.transform.localEulerAngles = new Vector3(0f, 0f, 25f);
            yield return Effects.Wait(0.083333336f);
            flameParent.transform.localScale = new Vector3(1f, 0.5f, 1f);
            flameParent.transform.localEulerAngles = new Vector3(0f, 0f, -15f);
            yield return Effects.Wait(0.083333336f);
            flameParent.transform.localScale = new Vector3(1f, 1f, 1f);
            flameParent.transform.localEulerAngles = new Vector3(0f, 0f, 0f);
            gameObject.SetActive(true);
            
            //アニメーション終了を待つ
            yield return Effects.Wait(2f);

            gameObject.SetActive(false);
            yield return new WaitForLerp(0.16666667f, (Action<float>)(t =>
            {
                flameParent.transform.localScale = new Vector3(1f, 1f - t, 1f);
            }));
            GameObject.Destroy(flameParent);
            yield break;
        }
        return CoShowInternal().WrapToIl2Cpp();
    }
}
