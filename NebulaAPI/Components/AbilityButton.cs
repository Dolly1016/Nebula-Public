using Virial.Compat;
using Virial.Media;

namespace Virial.Components;


public interface AbilityButton : IReleasable
{
    public enum LabelType
    {
        Standard,
        Impostor,
        Utility,
        Crewmate,
    }

    AbilityButton SetImage(Image image);
    AbilityButton SetLabel(string translationKey);
    AbilityButton SetCoolDownTimer(GameTimer timer);
    AbilityButton SetEffectTimer(GameTimer timer);
    GameTimer? GetCoolDownTimer();
    GameTimer? GetEffectTimer();
    AbilityButton StartCoolDown();
    AbilityButton StartEffect();
    AbilityButton InterruptEffect();
    AbilityButton SetLabelType(LabelType type);

    Predicate<AbilityButton> Availability { set; }
    Predicate<AbilityButton> Visibility { set; }
    Action<AbilityButton> OnUpdate { set; }
    Action<AbilityButton> OnClick { set; }
    Action<AbilityButton> OnSubAction { set; }
    Action<AbilityButton> OnEffectStart { set; }
    Action<AbilityButton> OnEffectEnd { set; }

    AbilityButton SetUpAsAbilityButton(IBinder binder, float coolDown, Action onClick);
    AbilityButton SetUpAsAbilityButton(IBinder binder, Func<bool>? canUseAbilityNow, Func<bool>? hasAbilityCharge, float coolDown, Action onClick);
    AbilityButton SetUpAsEffectButton(IBinder binder, float coolDown, float duration, Action onEffectStart, Action? onEffectEnd = null);
    AbilityButton SetUpAsEffectButton(IBinder binder, Func<bool>? canUseAbilityNow, Func<bool>? hasAbilityCharge, float coolDown, float duration, Action onEffectStart, Action? onEffectEnd = null);

    AbilityButton BindKey(VirtualKeyInput input);
    AbilityButton BindSubKey(VirtualKeyInput input);
}
