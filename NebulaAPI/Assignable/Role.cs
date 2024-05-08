using Virial.Configuration;

namespace Virial.Assignable;

/// <summary>
/// 役職の種別を表します。
/// 割り当て時に使用されます。
/// </summary>
[Flags]
public enum RoleCategory
{
    /// <summary>
    /// インポスター役職
    /// </summary>
    ImpostorRole = 0x01,
    /// <summary>
    /// 第三陣営役職
    /// </summary>
    NeutralRole = 0x02,
    /// <summary>
    /// クルーメイト役職
    /// </summary>
    CrewmateRole = 0x04,
}

/// <summary>
/// 引用元を持つオブジェクトを表します。
/// </summary>
public interface HasCitation
{
    /// <summary>
    /// 引用元を表します。
    /// </summary>
    Citation? Citaion { get; }
}

/// <summary>
/// プレイヤーに割り当てられる役職および追加役職の定義を表します。
/// </summary>
public interface DefinedAssignable
{
    /// <summary>
    /// 定義に紐づけられたオプションを取得します。
    /// </summary>
    /// <param name="id">オプションのID</param>
    /// <returns>オプション 見つからなかった場合はnull</returns>
    ValueConfiguration? GetConfiguration(string id);

    /// <summary>
    /// 役職の翻訳用の名称です。
    /// </summary>
    string LocalizedName { get; }

    /// <summary>
    /// ヘルプ画面上で表示するかどうか設定できます。
    /// </summary>
    bool ShowOnHelpScreen { get => true; }
}

/// <summary>
/// プレイヤーに割り当てられる役職の定義を表します。
/// </summary>
public interface DefinedRole : DefinedAssignable
{
    /// <summary>
    /// 付与されうる追加役職を制限するフィルタ
    /// DefinedModifierのRoleFilterと同期しています
    /// </summary>
    ModifierFilter? ModifierFilter { get; }
    /// <summary>
    /// 役職の種別 役職割り当て時に使用します
    /// </summary>
    RoleCategory Category { get; }
    /// <summary>
    /// 役職の属する陣営 おもに勝敗判定に使用されます
    /// </summary>
    RoleTeam Team { get; }
    /// <summary>
    /// 役職の色
    /// </summary>
    Virial.Color RoleColor { get; }

    internal int Id { get; }
}

/// <summary>
/// プレイヤーに割り当てられる追加役職の定義を表します。
/// </summary>
public interface DefinedModifier : DefinedAssignable
{
    /// <summary>
    /// 付与されうる先の役職を制限するフィルタ
    /// DefinedRoleのModifierFilterと同期しています
    /// </summary>
    RoleFilter? RoleFilter { get; }
}

/// <summary>
/// プレイヤーに割り当てられた役職や追加役職のコンテナを表します。
/// </summary>
public interface RuntimeAssignable : IBinder, ILifespan
{
    /// <summary>
    /// 役職および追加役職の定義
    /// </summary>
    DefinedAssignable Assignable { get; }

    /// <summary>
    /// 割当先のプレイヤー
    /// </summary>
    Virial.Game.Player MyPlayer { get; }

    //AssignableAspectAPI

    /// <summary>
    /// 通信障害を修理できる場合Trueを返します。
    /// </summary>
    bool CanFixComm { get; }

    /// <summary>
    /// 通信障害を修理できる場合Trueを返します。
    /// </summary>
    bool CanFixLight { get; }

    /// <summary>
    /// 割り当てられていることを自覚できる場合Trueを返します。
    /// </summary>
    bool CanBeAwareAssignment { get; }

    /// <summary>
    /// 緊急ボタンを押すことができる場合Trueを返します。
    /// </summary>
    bool CanCallEmergencyMeeting { get; }
}

/// <summary>
/// プレイヤーに割り当てられた役職のコンテナを表します。
/// </summary>
public interface RuntimeRole : RuntimeAssignable
{
    /// <summary>
    /// 役職の定義
    /// </summary>
    DefinedRole Role { get; }
    DefinedAssignable RuntimeAssignable.Assignable => Role;
}

/// <summary>
/// プレイヤーに割り当てられた追加役職のコンテナを表します。
/// </summary>
public interface RuntimeModifier : RuntimeAssignable
{
    /// <summary>
    /// 追加役職の定義
    /// </summary>
    DefinedModifier Modifier { get; }
    DefinedAssignable RuntimeAssignable.Assignable => Modifier;
}
