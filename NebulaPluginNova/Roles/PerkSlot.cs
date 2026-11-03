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
    
    private VirtualKeyInput? keyInput;
    private VirtualInput? input;

    public Entry? Current { get; private set; } = null;
    
    public bool HasPerk => !IsDeadObject && Current?.FunctionalInstance != null;

    public PerkFunctionalDefinition? CurrentPerk => IsDeadObject ? null : Current?.Definition;

    public PerkSlot(PerkFunctionalDefinition.Category category, PerkDefinition emptyDefinition, int priority, VirtualKeyInput? keyInput, Color? frameColor = null, ILifespan? lifespan = null)
    {
        Category = category;
        FrameColor = frameColor;
        Priority = priority;
        this.emptyDefinition = emptyDefinition;
        this.keyInput = keyInput;
        input = keyInput.HasValue ? NebulaInput.GetInput(keyInput.Value) : null;

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
        if (functional?.HasAction ?? false)
        {
            if (input != null)
            {
                var obj = ButtonEffect.AddKeyGuide(newInstance.RelatedGameObject.GetUnityObject(), input.TypicalKey, new(0f, -0.75f), false);
                if (obj != null) obj.transform.localScale = new(1f / 0.42f, 1 / 0.42f, 1f);
            }
            newInstance.Button.OnClick.AddListener(functional.OnClick);
        }
        newInstance.Priority = Priority;

        Current = new(newInstance, functional, perk);

        if (perk != null) newInstance.PlayFlashOnce();
    }

    public void Clear() => SetPerk(null);

    void OnHudUpdate(GameHudUpdateEvent ev)
    {
        if (input == null || Current == null) return;
        if (Current.Instance.RelatedGameObject.ActiveSelf && (Current.FunctionalInstance?.HasAction ?? false) && input.KeyDownInGame) Current.FunctionalInstance.OnClick();
    }
}
