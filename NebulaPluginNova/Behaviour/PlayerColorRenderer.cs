using Il2CppInterop.Runtime.Injection;

namespace Nebula.Behaviour;

public class PlayerColorRenderer : MonoBehaviour
{
    static PlayerColorRenderer() => ClassInjector.RegisterTypeInIl2Cpp<PlayerColorRenderer>();

    private GamePlayer? player = null;
    private int lastColorId = -1;
    private SpriteRenderer renderer = null!;

    public void Awake()
    {
        renderer = GetComponent<SpriteRenderer>();
        renderer.material = HatManager.Instance.PlayerMaterial;
    }

    public void SetPlayer(GamePlayer? player)
    {
        this.player = player;
        lastColorId = -1;
    }

    public void Update()
    {
        if (player == null) return;

        if(player.Unbox().CurrentOutfit.ColorId != lastColorId)
        {
            lastColorId = player.Unbox().CurrentOutfit.ColorId;

            PlayerMaterial.SetColors(lastColorId, renderer.sharedMaterial);
        }
    }

}
