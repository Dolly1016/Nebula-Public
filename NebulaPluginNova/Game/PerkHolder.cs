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
    

    private GameObject myHolder;
    private List<PerkInstance> myPerks = new();
    private int availableId = 0;

    public PerkHolder() {
        myHolder = HudContent.InstantiateContent("PerkHolder", true, true).gameObject;
        myHolder.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
    }

    public PerkInstance RegisterPerk(PerkDefinition perk) {
        var instance = new PerkInstance(perk, myHolder.transform);
        instance.RuntimeId = availableId++;
        myPerks.Add(instance);
        
        return instance;
    }

    private Vector3 ToPerkPos(int index) => new Vector3(-0.25f, 0f, 0f) + new Vector3(2f * index, (float)Math.Sqrt(3) * ((index % 2 == 0) ? -0.4f : 0.4f), (float)index * -0.001f) * 0.6f;

    static private ISpriteLoader IconFrameBackSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.FrameBack.png", 100f);
    static private ISpriteLoader IconFrameSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Frame.png", 100f);
    static private ISpriteLoader IconMaskSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.PerkMask.png", 100f);
    static private ISpriteLoader IconHighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Perks.Highlight.png", 100f);



    void IGameEntity.HudUpdate()
    {
        myPerks.RemoveAll(p => (p as ILifespan).IsDeadObject);
        
        myPerks.Sort((p1,p2) => p1.Priority != p2.Priority ? p2.Priority - p1.Priority : p1.RuntimeId - p2.RuntimeId);

        myHolder.SetActive(myPerks.Count > 0);
        
        for(int i = 0;i < myPerks.Count;i++)
        {
            var p = myPerks[i];
            p.UpdateLocalPos(ToPerkPos(i));
        }
    }
}
