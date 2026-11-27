using Nebula.Modules.ScriptComponents;
using Virial;
using Virial.Compat;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles;

/// <summary>
/// パーク枠の見た目の情報。
/// </summary>
/// <param name="Category">枠の種類。枠画像はこれをもとに決まる。</param>
/// <param name="Color">枠の色。パークが色を指定しない場合に使われる。</param>
public record PerkFrameInfo(PerkFunctionalDefinition.Category Category, Color? Color);

public class PerkSlot : FlexibleLifespan, IGameOperator
{
    public record Entry(PerkInstance Instance, PerkFunctionalInstance? FunctionalInstance, PerkFunctionalDefinition? Definition);

    public PerkFunctionalDefinition.Category Category { get; private init; }

    public Color? FrameColor { get; private init; }

    public int Priority { get; private init; }
    
    private PerkDefinition emptyDefinition;

    public Entry? Current { get; private set; } = null;
    
    public bool HasPerk => !IsDeadObject && Current?.FunctionalInstance != null;

    public PerkFunctionalDefinition? CurrentPerk => IsDeadObject ? null : Current?.Definition;

    public PerkSlot(PerkFunctionalDefinition.Category category, PerkDefinition emptyDefinition, int priority, Color? frameColor = null, ILifespan? lifespan = null)
    {
        Category = category;
        FrameColor = frameColor;
        Priority = priority;
        this.emptyDefinition = emptyDefinition;

        this.Register(lifespan ?? NebulaAPI.CurrentGame!);
        SetPerk(null);
    }

    public void SetPerk(PerkFunctionalDefinition? perk)
    {
        if (IsDeadObject) return;

        Current?.Instance.Release();
        Current = null;

        //表示は枠の寿命に、機能は表示の寿命に従属させる。
        var newInstance = (perk?.PerkDefinition ?? emptyDefinition).Instantiate(this, new(Category, FrameColor));
        if (newInstance == null) return;

        var functional = perk?.Instantiate(newInstance).Register(newInstance);
        //キーの割り当てはパークホルダが表示位置に応じて行う。
        if (functional?.HasAction ?? false) newInstance.SetAction(functional.OnClick);
        newInstance.Priority = Priority;

        Current = new(newInstance, functional, perk);

        if (perk != null) newInstance.PlayFlashOnce();
    }

    public void Clear() => SetPerk(null);
}
