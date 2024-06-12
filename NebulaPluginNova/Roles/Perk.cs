using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles;


public record PerkDefinition(string localizedName, Image backSprite, Image iconSprite, Color perkColor)
{
    public PerkDefinition(string localizedName, int backId, int iconId, Color perkColor) : this(localizedName, GetPerkBackIcon(backId), GetPerkIcon(iconId), perkColor) { }
    public PerkInstance? Instantiate(Virial.ILifespan? lifespan = null)
    {
        var perk = NebulaAPI.CurrentGame?.GetModule<PerkHolder>()?.RegisterPerk(this);
        if (lifespan != null) perk?.Register(lifespan);
        return perk;
    }

    static public Image GetPerkIcon(int id) => SpriteLoader.FromResource("Nebula.Resources.Perks.Front" + id + ".png", 100f);
    static public Image GetPerkBackIcon(int id) => SpriteLoader.FromResource("Nebula.Resources.Perks.Back" + id + ".png", 100f);
}

public class PerkInstance : IGameOperator, ILifespan, IReleasable
{
    static private Image IconFrameBackSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameBack.png", 100f);
    static private Image IconFrameSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Frame.png", 100f);
    static private Image IconMaskSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.PerkMask.png", 100f);
    static private Image IconHighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Highlight.png", 100f);

    private bool isDead = false;
    bool ILifespan.IsDeadObject => isDead;
    void IReleasable.Release() => OnReleased();

    private GameObject obj;
    private SpriteRenderer back;
    private SpriteRenderer icon;
    private SpriteRenderer highlight;
    private SpriteRenderer coolDown;

    //backColorは背景色、perkColorはパーク全体にかかる色
    private Color backColor = Color.white, perkColor = Color.white;

    public PerkInstance(PerkDefinition definition, Transform parent)
    {
        obj = UnityHelper.CreateObject("PerkDisplay", parent, Vector3.zero);
        back = UnityHelper.CreateObject<SpriteRenderer>("Back", obj.transform, new(0f, 0f, 0f));
        back.sprite = IconFrameBackSprite.GetSprite();
        back.color = new(0.4f, 0.4f, 0.4f, 0.5f);
        var frame = UnityHelper.CreateObject<SpriteRenderer>("Frame", obj.transform, new(0f, 0f, -0.6f));
        frame.sprite = IconFrameSprite.GetSprite();
        icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", obj.transform, new(0f, 0f, -0.1f));
        highlight = UnityHelper.CreateObject<SpriteRenderer>("Highlight", obj.transform, new(0f, 0f, -0.5f));
        highlight.sprite = IconHighlightSprite.GetSprite();
        coolDown = UnityHelper.CreateObject<SpriteRenderer>("Mask", obj.transform, new(0f, 0f, -0.3f));
        coolDown.sprite = IconMaskSprite.GetSprite();
        coolDown.material.shader = NebulaAsset.GuageShader;

        SetCoolDownColor(Color.red);
        SetSprite(definition.iconSprite.GetSprite(), definition.backSprite.GetSprite(), definition.perkColor);
        SetHighlight(false);
    }

    public Timer? MyTimer { get; private set; } = null!;

    public int Priority { get; set; } = 0;
    internal int RuntimeId { get; set; } = 0;

    public void OnReleased()
    {
        if (isDead) return;

        isDead = true;

        if (obj) GameObject.Destroy(obj);
        MyTimer?.ReleaseIt();
    }

    public PerkInstance BindTimer(Timer? timer)
    {
        if (MyTimer == timer) return this;

        MyTimer?.ReleaseIt();
        MyTimer = timer;

        return this;
    }

    void Update(GameUpdateEvent ev)
    {
        if (MyTimer != null) SetCoolDown(MyTimer.Percentage);
    }

    public void SetCoolDown(float rate)
    {
        if (isDead) return;

        if (rate > 0f)
        {
            coolDown.gameObject.SetActive(true);
            coolDown.material.SetFloat("_Guage", rate);
        }
        else
            coolDown.gameObject.SetActive(false);
    }

    public PerkInstance SetCoolDownColor(Color color)
    {
        coolDown.sharedMaterial.color = color.AlphaMultiplied(0.5f);

        return this;
    }

    public PerkInstance SetDisplayColor(Color color)
    {
        this.perkColor = color;

        this.back.color = this.backColor * this.perkColor;
        this.icon.color = this.perkColor;

        return this;
    }

    public PerkInstance SetHighlight(bool highlight, Color? color = null)
    {
        if (isDead) return this;

        this.highlight.gameObject.SetActive(highlight);
        if (highlight && color != null) this.highlight.material.color = color.Value;

        return this;
    }

    private void SetSprite(Sprite iconSprite, Sprite backSprite, Color? color = null)
    {
        if (isDead) return;

        this.icon.sprite = iconSprite;
        this.back.sprite = backSprite;
        this.backColor = color ?? Color.white;
        
        this.back.color = this.backColor * this.perkColor;
        
    }

    internal void UpdateLocalPos(Vector3 pos)
    {
        if (isDead) return;

        this.obj.transform.localPosition = pos;
    }
}
