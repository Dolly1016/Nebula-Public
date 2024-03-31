using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Roles;

public class Perk
{

    private Func<Perk, PerkHolder.PerkDisplay, PerkInstance> Generator { get; init; }
    public PerkInstance InstantiateForLocal(PerkHolder.PerkDisplay display)
    {
        return Generator.Invoke(this, display);
    }

    public Perk(string localizedName, Image backSprite, Image iconSprite, Color perkColor, Func<Perk, PerkHolder.PerkDisplay, PerkInstance> generator)
    {
        Generator = generator;
        BackSprite = backSprite;
        IconSprite = iconSprite;
        PerkColor = perkColor;
        TranslationKey = localizedName;

        Roles.Register(this);
    }

    public Image BackSprite { get; private init; }
    public Image IconSprite { get; private init; }
    public Color PerkColor { get; private init; }
    public string TranslationKey { get; private init; }
    public int Id { get; internal set; }
}

public class PerkInstance : ComponentHolder
{
    public Perk MyPerk { get; private init; }
    public PerkHolder.PerkDisplay MyDisplay { get; private init; }

    public PerkInstance(Perk perk, PerkHolder.PerkDisplay display)
    {
        MyPerk = perk;
        MyDisplay = display;
    }

    public virtual void OnActivatedLocal() { }
}
