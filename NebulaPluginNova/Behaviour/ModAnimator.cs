using Il2CppInterop.Runtime.Injection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nebula.Modules.HelpScreen;

namespace Nebula.Behaviour;

public class ModAnimator : MonoBehaviour
{
    static ModAnimator() => ClassInjector.RegisterTypeInIl2Cpp<ModAnimator>();

    private SpriteRenderer renderer = null!;

    public void Awake()
    {
        renderer = GetComponent<SpriteRenderer>();
    }

    private IEnumerator CoPlay(IDividedSpriteLoader sprite, float fps, bool insertEmptyFrameAtLast)
    {
        int length = sprite.Length;

        float secondsPerFrame = 1f / fps;

        for(int i = 0;i< length; i++)
        {
            renderer.sprite = sprite.GetSprite(i);
            yield return new WaitForSeconds(secondsPerFrame);
        }

        if (insertEmptyFrameAtLast) renderer.sprite = null;
        yield break;
    }

    public void PlayOneShot(IDividedSpriteLoader sprite, float fps = 12f, bool insertEmptyFrameAtLast = false)
    {
        StartCoroutine(CoPlay(sprite,fps,insertEmptyFrameAtLast).WrapToIl2Cpp());
    }
}
