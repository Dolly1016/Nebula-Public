using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behavior;

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
