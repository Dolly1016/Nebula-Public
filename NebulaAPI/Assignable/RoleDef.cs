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
    CrewmateRole = 0x04
}

/// <summary>
/// アドオンによって追加しようとしている役職の定義の共通部分を表します。
/// 定義された役職を表すDefinedRoleとは別物です。
/// </summary>
public abstract class AbstractRoleDef
{
    /// <summary>
    /// 役職の種別
    /// </summary>
    abstract public RoleCategory RoleCategory { get; }
    /// <summary>
    /// 翻訳のための内部的な役職名
    /// </summary>
    abstract public string LocalizedName { get; }
    /// <summary>
    /// 役職の色
    /// </summary>
    abstract public Color RoleColor { get; }
    /// <summary>
    /// 役職が属する陣営
    /// </summary>
    abstract public RoleTeam Team { get; }
    
    virtual internal Type? RoleInstanceType { get => null; }
}

/// <summary>
/// ユニークなコンテナをもつアドオンによる役職の定義を表します。
/// 定義された役職を表すDefinedRoleとは別物です。
/// </summary>
/// <typeparam name="T">役職コンテナの定義クラス</typeparam>
public abstract class VariableRoleDef<T> : AbstractRoleDef
{
    override internal Type? RoleInstanceType => typeof(T);
}