using Virial.Compat;
using Virial.Media;

namespace Virial.Components;


public interface ModAbilityButton : IReleasable
{
    public enum LabelType
    {
        Standard,
        Impostor,
        Utility,
        Crewmate,
    }

    ModAbilityButton SetImage(Image image);
    ModAbilityButton SetLabel(string translationKey);
    ModAbilityButton SetCoolDownTimer(IVisualTimer timer);
    ModAbilityButton SetEffectTimer(IVisualTimer timer);
    IVisualTimer? GetCoolDownTimer();
    IVisualTimer? GetEffectTimer();
    ModAbilityButton StartCoolDown();
    ModAbilityButton StartEffect();
    ModAbilityButton InterruptEffect();
    ModAbilityButton SetLabelType(LabelType type);

    Predicate<ModAbilityButton> Availability { set; }
    Predicate<ModAbilityButton> Visibility { set; }
    Action<ModAbilityButton> OnUpdate { set; }
    Action<ModAbilityButton> OnClick { set; }
    Action<ModAbilityButton> OnSubAction { set; }
    Action<ModAbilityButton> OnEffectStart { set; }
    Action<ModAbilityButton> OnEffectEnd { set; }

    ModAbilityButton SetUpAsAbilityButton(IBinder binder, float coolDown, Action onClick);
    ModAbilityButton SetUpAsAbilityButton(IBinder binder, Func<bool>? canUseAbilityNow, Func<bool>? hasAbilityCharge, float coolDown, Action onClick);
    ModAbilityButton SetUpAsEffectButton(IBinder binder, float coolDown, float duration, Action onEffectStart, Action? onEffectEnd = null);
    ModAbilityButton SetUpAsEffectButton(IBinder binder, Func<bool>? canUseAbilityNow, Func<bool>? hasAbilityCharge, float coolDown, float duration, Action onEffectStart, Action? onEffectEnd = null);

    ModAbilityButton BindKey(VirtualKeyInput input);
    ModAbilityButton BindSubKey(VirtualKeyInput input);
}
