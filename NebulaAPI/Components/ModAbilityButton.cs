using Virial.Compat;
using Virial.Game;
using Virial.Media;

namespace Virial.Components;

/// <summary>
/// 能力ボタンを表します。
/// </summary>
public interface ModAbilityButton : ILifespan
{
    /// <summary>
    /// ボタンのラベルの種類を表します。
    /// </summary>
    public enum LabelType
    {
        /// <summary>
        /// 通常の黒い文字。
        /// </summary>
        Standard,
        /// <summary>
        /// キルボタンなどの赤い文字。
        /// </summary>
        Impostor,
        /// <summary>
        /// アドミンやバイタルなどの緑色の文字。
        /// </summary>
        Utility,
        /// <summary>
        /// エンジニアやサイエンティストの能力などの青い文字。
        /// </summary>
        Crewmate,
    }

    /// <summary>
    /// ボタンの画像を設定します。
    /// </summary>
    /// <param name="image"></param>
    /// <returns></returns>
    ModAbilityButton SetImage(Image image);

    /// <summary>
    /// ボタン下部のテキストを設定します。
    /// </summary>
    /// <param name="translationKey"></param>
    /// <returns></returns>
    ModAbilityButton SetLabel(string translationKey);

    /// <summary>
    /// クールダウンを表すタイマーです。
    /// </summary>
    IVisualTimer? CoolDownTimer { get; set; }
    /// <summary>
    /// エフェクト効果時間を表すタイマーです。
    /// </summary>
    IVisualTimer? EffectTimer { get; set; }

    /// <summary>
    /// クールダウンを開始します。
    /// </summary>
    /// <returns></returns>
    ModAbilityButton StartCoolDown();

    /// <summary>
    /// エフェクト効果を開始できる場合、エフェクト効果を開始します。
    /// </summary>
    /// <returns></returns>
    ModAbilityButton StartEffect();

    /// <summary>
    /// エフェクト効果を開始/終了します。
    /// </summary>
    /// <returns></returns>
    ModAbilityButton ToggleEffect();

    /// <summary>
    /// エフェクト効果中の場合はエフェクト効果を中断します。
    /// </summary>
    /// <returns></returns>
    ModAbilityButton InterruptEffect();

    /// <summary>
    /// ラベルの見た目を設定します。
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    ModAbilityButton SetLabelType(LabelType type);

    /// <summary>
    /// ボタンの可用性を設定します。可視状態でなければ呼び出されません。
    /// </summary>
    Predicate<ModAbilityButton> Availability { set; }

    /// <summary>
    /// ボタンの可視性を設定します。
    /// </summary>
    Predicate<ModAbilityButton> Visibility { set; }

    /// <summary>
    /// 常に呼び出されるルーチンを設定します。
    /// </summary>
    Action<ModAbilityButton> OnUpdate { set; }

    /// <summary>
    /// ボタンがクリックされた際に呼び出されます。
    /// </summary>
    Action<ModAbilityButton> OnClick { set; }

    /// <summary>
    /// サブ効果がクリックされた際に呼び出されます。
    /// </summary>
    Action<ModAbilityButton> OnSubAction { set; }

    /// <summary>
    /// エフェクト効果が開始する際に呼び出されます。
    /// </summary>
    Action<ModAbilityButton> OnEffectStart { set; }

    /// <summary>
    /// エフェクト効果が終了あるいは中断した際に呼び出されます。
    /// </summary>
    Action<ModAbilityButton> OnEffectEnd { set; }

    /// <summary>
    /// ボタンが壊れたときに呼び出されます。
    /// </summary>
    Action<ModAbilityButton> OnBroken { set; }

    /// <summary>
    /// 強調表示をし続ける間はtrueを返します。
    /// </summary>
    Func<ModAbilityButton, bool> PlayFlashWhile { set; }

    /// <summary>
    /// エフェクト効果中の場合、trueを返します。
    /// </summary>
    bool IsInEffect { get; }

    /// <summary>
    /// クールダウン中の場合、trueを返します。
    /// </summary>
    bool IsInCooldown { get; }

    bool IsVisible { get; }
    bool IsAvailable { get; }

    /// <summary>
    /// 入力キーを設定します。
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    ModAbilityButton BindKey(VirtualKeyInput input, string? action = null);
    /// <summary>
    /// サブ入力キーを設定します。
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    ModAbilityButton BindSubKey(VirtualKeyInput input, string? action = null, bool withTutorial = false);
    ModAbilityButton ResetKeyBinding();
    IKillButtonLike GetKillButtonLike();

    /// <summary>
    /// 簒奪可能な能力のボタンに設定します。
    /// </summary>
    /// <param name="ability"></param>
    /// <returns></returns>
    ModAbilityButton SetAsUsurpableButton(IUsurpableAbility ability);
    bool IsBroken { get; }

    ModAbilityButton SetAsMouseClickButton();

    ModAbilityButton ShowUsesIcon(int variation, string text);
    ModAbilityButton UpdateUsesIcon(string text);
    ModAbilityButton HideUsesIcon();

    internal UnityEngine.GameObject AddLockedOverlay();

    /// <summary>
    /// クリック操作が実行可能ならば実行します。
    /// </summary>
    ModAbilityButton DoClick();

    /// <summary>
    /// サブ操作が実行可能ならば実行します。
    /// </summary>
    ModAbilityButton DoSubClick();
}
