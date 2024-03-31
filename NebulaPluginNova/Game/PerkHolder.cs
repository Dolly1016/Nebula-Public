using JetBrains.Annotations;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Game;

namespace Nebula.Game;

public class PerkHolder : IGameEntity
{
    public record PerkDisplay(GameObject obj, SpriteRenderer back, SpriteRenderer icon, SpriteRenderer highlight,SpriteRenderer coolDown)
    {
        public Timer? MyTimer { get; private set; } = null!;

        public void OnReleased()
        {
            MyTimer?.ReleaseIt();
        }

        public void BindTimer(Timer? timer)
        {
            if (MyTimer == timer) return;

            MyTimer?.ReleaseIt();
            MyTimer = timer;
        }

        public void SetCoolDown(float rate)
        {
            if (rate > 0f)
            {
                coolDown.gameObject.SetActive(true);
                coolDown.material.SetFloat("_Guage", rate);
            }
            else
                coolDown.gameObject.SetActive(false);
        }

        public void SetHighlight(bool highlight, Color? color = null)
        {
            this.highlight.gameObject.SetActive(highlight);
            if (highlight && color != null) this.highlight.material.color = color.Value;
        }

        public void SetBackSprite(Sprite sprite, Color? color = null)
        {
            this.back.sprite = sprite;
            if(color != null) this.back.color = color.Value;
        }

        public void SetPerk(Perk perk)
        {
            SetBackSprite(perk.BackSprite.GetSprite(), perk.PerkColor);
            icon.sprite = perk.IconSprite.GetSprite();
        }
    }

    private GameObject myHolder;
    private List<PerkInstance> myPerks = new();
    //新たにパークを装着可能な残スロット数
    private int LeftAvailableSlots = 0;
    private Dictionary<Perk,int> PerksInventory = new();
    private List<PerkDisplay> AvailableSlots = new();
    public PerkHolder() {
        myHolder = HudContent.InstantiateContent("PerkHolder", true, true, false, true).gameObject;
    }

    //public PerkInstance RegisterPerkFromInventory(Perk perk) { }
    private PerkInstance RegisterPerk(Perk perk) {
        var display = GeneratePerkDisplay();
        display.SetPerk(perk);
        var instance = perk.InstantiateForLocal(display);
        myPerks.Add(instance);
        instance.OnActivatedLocal();
        return instance;
    }

    private Vector3 ToPerkPos(int index) => new(2f * index, (float)Math.Sqrt(3) * ((index % 2 == 0) ? -1 : 1));

    static private ISpriteLoader IconFrameBackSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameBack.png", 100f);
    static private ISpriteLoader IconFrameSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Frame.png", 100f);
    static private ISpriteLoader IconMaskSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.PerkMask.png", 100f);
    static private ISpriteLoader IconHighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Highlight.png", 100f);

    private PerkDisplay GeneratePerkDisplay()
    {
        var obj = UnityHelper.CreateObject("PerkDisplay", myHolder.transform, Vector3.zero);
        var back = UnityHelper.CreateObject<SpriteRenderer>("Back", obj.transform, new(0f, 0f, 0f));
        back.sprite = IconFrameBackSprite.GetSprite();
        back.color = new(0.4f, 0.4f, 0.4f, 0.5f);
        var frame = UnityHelper.CreateObject<SpriteRenderer>("Frame", obj.transform, new(0f, 0f, -0.6f));
        frame.sprite = IconFrameSprite.GetSprite();
        var icon = UnityHelper.CreateObject<SpriteRenderer>("Icon", obj.transform, new(0f, 0f, -0.1f));
        var highlight = UnityHelper.CreateObject<SpriteRenderer>("Highlight", obj.transform, new(0f, 0f, -0.5f));
        highlight.sprite = IconHighlightSprite.GetSprite();
        var coolDown = UnityHelper.CreateObject<SpriteRenderer>("Mask", obj.transform, new(0f, 0f, -0.3f));
        coolDown.sprite = IconMaskSprite.GetSprite();
        coolDown.material.shader = NebulaAsset.GuageShader;

        PerkDisplay result = new(obj, back, icon, highlight, coolDown);
        result.SetCoolDown(0f);

        return result;
    }

    void IGameEntity.HudUpdate()
    {
        myPerks.RemoveAll(p =>
        {
            if (p.IsDeadObject) {
                p.MyDisplay.OnReleased();
                return true;
            }
            return false;
        });

        myHolder.SetActive(myPerks.Count > 0 || LeftAvailableSlots > 0);
        
        for(int i = 0;i < myPerks.Count;i++)
        {
            var p = myPerks[i];
            p.MyDisplay.obj.transform.localPosition = ToPerkPos(i);

            if(p.MyDisplay.MyTimer != null)
            {
                p.MyDisplay.SetCoolDown(p.MyDisplay.MyTimer.Percentage);
            }
        }

        while (LeftAvailableSlots > AvailableSlots.Count) AvailableSlots.Add(GeneratePerkDisplay());
        for(int i = 0; i < AvailableSlots.Count; i++)
        {
            if(i >= LeftAvailableSlots)
            {
                AvailableSlots[i].obj.SetActive(false);
            }
            else
            {
                AvailableSlots[i].obj.SetActive(true);
                AvailableSlots[i].obj.transform.localPosition = ToPerkPos(myPerks.Count + i);
            }
        }
    }
}
