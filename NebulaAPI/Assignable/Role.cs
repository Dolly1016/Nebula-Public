using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;

namespace Virial.Assignable;

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
