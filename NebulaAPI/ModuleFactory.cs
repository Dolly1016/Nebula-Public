using Virial.Compat;
using Virial.Components;
using Virial.Game;
using Virial.Media;

namespace Virial;

public interface IModuleFactory
{
    /// <summary>
    /// 能力ボタンを生成します。
    /// </summary>
    /// <param name="lifespan">寿命。</param>
    /// <returns></returns>
    ModAbilityButton AbilityButton(ILifespan lifespan);
    /// <summary>
    /// 能力ボタンを生成します。
    /// </summary>
    /// <param name="lifespan">寿命。</param>
    /// <param name="isLeftSideButton">画面の左側に表示する場合、<c>true</c>。</param>
    /// <param name="isArrangedAsKillButton">キルボタンの位置に配置する場合、<c>true</c>。</param>
    /// <param name="priority">ボタン配置の優先度</param>
    /// <param name="alwaysShow">常に表示する場合、<c>true</c>。</param>
    ModAbilityButton AbilityButton(ILifespan lifespan, bool isLeftSideButton = false, bool isArrangedAsKillButton = false, int priority = 0, bool alwaysShow = false);

    /// <summary>
    /// プレイヤーの移動可能性と生死を考慮した標準的な能力ボタンを生成します。
    /// ボタンの可用性と可視性には追加の条件を設けられます。
    /// </summary>
    /// <returns></returns>
    ModAbilityButton AbilityButton(ILifespan lifespan, Player player, bool isArrangedAsKillButton, bool isLeftSideButton, VirtualKeyInput input, string? inputHelp, float cooldown, string label, Image image, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, bool asGhostButton = false)
    {
        var button = AbilityButton(lifespan, isArrangedAsKillButton: isArrangedAsKillButton, isLeftSideButton: isLeftSideButton);
        SetUpAbilityOrKillButton(button, lifespan, player, input, inputHelp, cooldown, true, label, image, availability, visibility, asGhostButton);
        return button;
    }
    ModAbilityButton AbilityButton(ILifespan lifespan, Player player, VirtualKeyInput input, float cooldown, string label, Image image, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, bool asGhostButton = false)
        => AbilityButton(lifespan, player, input, null, cooldown, label, image, availability, visibility, asGhostButton);
    ModAbilityButton AbilityButton(ILifespan lifespan, Player player, VirtualKeyInput input, string? inputHelp, float cooldown, string label, Image image, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, bool asGhostButton = false)
        => AbilityButton(lifespan, player, false, false, input, inputHelp, cooldown, label, image, availability, visibility, asGhostButton);

    ModAbilityButton EffectButton(ILifespan lifespan, Player player, VirtualKeyInput input, string? inputHelp, float cooldown, float duration, string label, Image image, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, bool asGhostButton = false, bool isToggleEffect = false)
    {
        var button = AbilityButton(lifespan, player, input, inputHelp, cooldown, label, image, availability, visibility, asGhostButton);
        button.EffectTimer = NebulaAPI.Modules.Timer(lifespan, duration);
        
        if(isToggleEffect) button.OnClick = _ => button.ToggleEffect();
        else button.OnClick = _ => button.StartEffect();

        return button;
    }
    ModAbilityButton EffectButton(ILifespan lifespan, Player player, VirtualKeyInput input, float cooldown, float duration, string label, Image image, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, bool asGhostButton = false, bool isToggleEffect = false)
        => EffectButton(lifespan, player, input, null, cooldown, duration, label, image, availability, visibility, asGhostButton);

    ModAbilityButton KillButton(ILifespan lifespan, Player player, bool arrangeAsKillButton, VirtualKeyInput input, string? inputHelp, float cooldown, string label, Virial.Components.ModAbilityButton.LabelType labelType, Image? image, Action<Player, ModAbilityButton> onKill, Func<Player, bool>? filter = null, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null, Func<Player, bool>? filterHeavier = null)
    {
        var tracker = KillTracker(lifespan, player, filter);
        var button = AbilityButton(lifespan, isArrangedAsKillButton: arrangeAsKillButton);
        button.OnClick = b => onKill.Invoke(tracker.CurrentTarget!, b);
        if (availability == null)
            availability = _ => tracker.CurrentTarget != null;
        else
        {
            var givenAvailability = availability;
            availability = b => tracker.CurrentTarget != null && givenAvailability.Invoke(b);
        }
            
        SetUpAbilityOrKillButton(button, lifespan, player, input, inputHelp, cooldown, false, label, image, availability, visibility, false);
        return button;
    }

    ModAbilityButton KillButton(ILifespan lifespan, Player player, bool arrangeAsKillButton, VirtualKeyInput input, float cooldown, string label, Virial.Components.ModAbilityButton.LabelType labelType, Image? image, Action<Player, ModAbilityButton> onKill, Func<Player, bool>? filter = null, Func<ModAbilityButton, bool>? availability = null, Func<ModAbilityButton, bool>? visibility = null)
     => KillButton(lifespan, player, arrangeAsKillButton, input, null, cooldown, label, labelType, image, onKill, filter, availability, visibility);

    /// <summary>
    /// キル対象を選択するトラッカーを返します。
    /// キル可能なプレイヤーのみ選択できます。
    /// </summary>
    /// <param name="lifespan">寿命。</param>
    /// <param name="player">追跡者。通常、自分自身を指定します。</param>
    /// <param name="filter">さらにプレイヤーを限定するフィルタ。</param>
    /// <param name="filterHeavier">さらにプレイヤーを限定するフィルタ。コストの高い計算をここに記述してください。</param>
    /// <returns></returns>
    ObjectTracker<Player> KillTracker(ILifespan lifespan, Player player, Func<Player, bool>? filter = null, Func<Player, bool>? filterHeavier = null);

    /// <summary>
    /// タイマーを生成します。
    /// </summary>
    /// <param name="max"></param>
    /// <returns></returns>
    GameTimer Timer(ILifespan lifespan, float max);

    static private void SetUpAbilityOrKillButton(ModAbilityButton button, ILifespan lifespan, Player player, VirtualKeyInput input, string? inputHelp, float cooldown, bool asAbilityButton, string label, Image? image, Func<ModAbilityButton, bool>? availability, Func<ModAbilityButton, bool>? visibility, bool asGhostButton)
    {
        if(image != null) button.SetImage(image);
        button.SetLabel(label);
        if(input != VirtualKeyInput.None) button.BindKey(input, inputHelp);
        var timer = NebulaAPI.Modules.Timer(lifespan, cooldown);
        if (asAbilityButton)
            timer.SetAsAbilityTimer();
        else
            timer.SetAsKillCoolTimer();
        button.CoolDownTimer = timer.Start();

        if (availability == null) button.Availability = b => player.CanMove;
        else button.Availability = b => player.CanMove && availability.Invoke(b);

        if (visibility == null) button.Visibility = asGhostButton ? b => player.IsDead : b => !player.IsDead;
        else button.Visibility = asGhostButton ? (b => player.IsDead && visibility.Invoke(b)) : (b => !player.IsDead && visibility.Invoke(b));
    }
}