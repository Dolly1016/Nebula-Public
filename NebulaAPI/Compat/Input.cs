namespace Virial.Compat;

/// <summary>
/// 入力の種類を表します。
/// 実際のキーバインドに言及する必要はありません。
/// </summary>
public enum VirtualKeyInput
{
    /// <summary>
    /// いずれのキー操作にも対応しません。
    /// </summary>
    None,
    /// <summary>
    /// キル操作。
    /// </summary>
    Kill,
    /// <summary>
    /// ベント潜入・脱出操作。
    /// </summary>
    Vent,
    /// <summary>
    /// 使用操作。
    /// </summary>
    Use,
    /// <summary>
    /// 能力発動操作。
    /// </summary>
    Ability,
    /// <summary>
    /// 副次能力発動操作。
    /// </summary>
    SecondaryAbility,
    /// <summary>
    /// サイドキック操作。
    /// </summary>
    SidekickAction,
    /// <summary>
    /// フリープレイの役職変更操作。
    /// </summary>
    FreeplayAction,
    /// <summary>
    /// サブアクション操作。
    /// </summary>
    AidAction,
    /// <summary>
    /// パーク発動操作。
    /// </summary>
    PerkAction1,
    /// <summary>
    /// パーク発動操作(人外専用パーク)。
    /// </summary>
    PerkAction2,
    /// <summary>
    /// メタアクション操作。
    /// </summary>
    Command,
    /// <summary>
    /// スクリーンショット操作。
    /// </summary>
    Screenshot,
    /// <summary>
    /// ボイスチャットのミュート操作。
    /// </summary>
    Mute,
    /// <summary>
    /// ボイスチャットのフィルタリング操作。
    /// </summary>
    VCFilter,
    /// <summary>
    /// ヘルプ画面を開く操作。
    /// </summary>
    Help,
    /// <summary>
    /// 観戦モード操作。
    /// </summary>
    Spectator,
    /// <summary>
    /// 観戦プレイヤー変更(戻る)操作。
    /// </summary>
    SpectatorLeft,
    /// <summary>
    /// 観戦プレイヤー変更(進む)操作。
    /// </summary>
    SpectatorRight,
    /// <summary>
    /// スタンプ及びエモート操作。
    /// </summary>
    Stamp,
}
