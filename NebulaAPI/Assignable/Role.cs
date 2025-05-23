﻿using Virial.Configuration;
using Virial.Game;

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

public enum RoleTaskType
{
    CrewmateTask,
    RoleTask,
    NoTask,
}

public interface ICodeName
{
    string CodeName { get; }
}

public interface ISpawnable
{
    /// <summary>
    /// ゲーム中に出現しうる場合はtrueを返します。
    /// </summary>
    bool IsSpawnable { get; }
}

public interface ICategorizedRoleAllocator<R> where R : DefinedAssignable
{
    R MyRole { get; }
    /// <summary>
    /// 割り当て確率を0～100の間で返します。
    /// </summary>
    /// <param name="category"></param>
    /// <returns></returns>
    int GetChance(RoleCategory category);
    void ConsumeCount(RoleCategory category);
}

public interface IHasCategorizedRoleAllocator<R> where R : DefinedAssignable
{
    ICategorizedRoleAllocator<R> GenerateRoleAllocator();
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

public interface IRoleID
{
    /// <summary>
    /// 役職のIDを返します。
    /// アドオンの構成やバージョン間の違いによってIDは変化します。
    /// </summary>
    int Id { get; internal set; }
}

public interface HasRoleFilter
{
    /// <summary>
    /// 割り当て可能な役職に制限をかけます。
    /// </summary>
    RoleFilter RoleFilter { get; }
}

/// <summary>
/// プレイヤーに割り当てられる役職および追加役職の定義を表します。
/// </summary>
public interface DefinedAssignable : IRoleID
{

    /// <summary>
    /// 役職の翻訳用の名称です。
    /// </summary>
    string LocalizedName { get; }

    /// <summary>
    /// ヘルプ画面上で表示するかどうか設定できます。
    /// </summary>
    bool ShowOnHelpScreen { get => true; }
    bool ShowOnFreeplayScreen { get => true; }

    /// <summary>
    /// 役職の内部名
    /// </summary>
    string InternalName => LocalizedName;

    /// <summary>
    /// 役職の表示名
    /// </summary>
    string DisplayName => NebulaAPI.Language.Translate("role." + LocalizedName + ".name");
    string DisplayColoredName => DisplayName.Color(UnityColor);

    /// <summary>
    /// 一般的な二つ名テキスト
    /// </summary>
    string GeneralBlurb => NebulaAPI.Language.Translate("role." + LocalizedName + ".blurb");
    string GeneralColoredBlurb => GeneralBlurb.Color(UnityColor);

    /// <summary>
    /// 役職の色
    /// </summary>
    Virial.Color Color { get; }
    internal UnityEngine.Color UnityColor { get; }

    IConfigurationHolder? ConfigurationHolder { get; }

    IEnumerable<DefinedAssignable> AchievementGroups => [this];
}

public interface DefinedCategorizedAssignable : DefinedAssignable
{
    /// <summary>
    /// 役職の種別 役職割り当て時に使用します
    /// </summary>
    RoleCategory Category { get; }

    /// <summary>
    /// 役職の省略名
    /// </summary>
    string DisplayShort => NebulaAPI.Language.Translate("role." + LocalizedName + ".short");
    string DisplayColoredShort => DisplayShort.Color(UnityColor);
}

/// <summary>
/// 単一のチームに属する役職の定義
/// </summary>
public interface DefinedSingleAssignable : DefinedCategorizedAssignable, ISpawnable
{
    /// <summary>
    /// 役職の属する陣営 おもに勝敗判定に使用されます
    /// </summary>
    RoleTeam Team { get; }

    AllocationParameters? AllocationParameters { get; }
    AllocationParameters? JackalAllocationParameters { get => null; }
}

public interface RuntimeAssignableGenerator<T> where T : RuntimeAssignable
{
    T CreateInstance(Virial.Game.Player player, int[] arguments);
}

public interface IGuessed
{
    /// <summary>
    /// Guesserによる推測対象になる場合trueを返します。
    /// </summary>
    bool CanBeGuessDefault => true;

    /// <summary>
    /// Guesserによる推測対象になる場合trueを返します。
    /// </summary>
    bool CanBeGuess => CanBeGuessDefault && (CanBeGuessVariable?.CurrentValue ?? true);
    ISharableVariable<bool>? CanBeGuessVariable { get; internal set; }
}

public interface AssignableFilterHolder
{
    /// <summary>
    /// 付与されうる追加役職を制限するフィルタ
    /// AllocatableDefinedModifierのRoleFilterと同期しています
    /// </summary>
    ModifierFilter? ModifierFilter { get; }


    /// <summary>
    /// 付与されうる幽霊役職を制限するフィルタ
    /// DefinedGhostRoleのRoleFilterと同期しています。
    /// </summary>
    GhostRoleFilter? GhostRoleFilter { get; }

    /// <summary>
    /// デフォルト設定で幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="modifier"></param>
    /// <returns></returns>
    bool CanLoadDefault(DefinedAssignable assignable);

    /// <summary>
    /// 幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="assignable"></param>
    /// <returns></returns>
    bool CanLoad(DefinedAssignable assignable);
}

public interface AllocationParameters
{
    public delegate (DefinedRole role, int[]? argument) ExtraAssignment(DefinedRole assignable, int playerId);
    public record ExtraAssignmentInfo(ExtraAssignment Assigner, RoleCategory Category);
    IEnumerable<IConfiguration> Configurations { get; }

    //割り当て数の総和を返します。
    int RoleCountSum { get; }

    //100%割り当て数を返します。
    int RoleCount100 { get; }
    //確率的な割り当て数を返します。
    int RoleCountRandom { get; }

    //同陣営への追加割り当て
    ExtraAssignmentInfo[] TeamAssignment => [];
    //別陣営への追加割り当て
    ExtraAssignmentInfo[] OthersAssignment => [];
    bool HasExtraAssignment => TeamAssignment.Length > 0 || OthersAssignment.Length > 0;
    int TeamCost => 1 + TeamAssignment.Length;
    int OtherCost => OthersAssignment.Length;

    /// <summary>
    /// 割り当て数を取得します。100%か確率的な割り当てか真偽値で選択できます。
    /// </summary>
    /// <param name="get100"></param>
    /// <returns></returns>
    int GetRoleCountWhich(bool get100) => get100 ? RoleCount100 : RoleCountRandom;

    /// <summary>
    /// count人目のプレイヤーの割り当て確率を0～100の値で取得します。
    /// </summary>
    /// <param name="count">何人目のプレイヤーかを表す1以上の値</param>
    /// <returns></returns>
    int GetRoleChance(int count);
}

/// <summary>
/// プレイヤーに割り当てられる役職の定義を表します。
/// </summary>
public interface DefinedRole : DefinedSingleAssignable, RuntimeAssignableGenerator<RuntimeRole>, IGuessed, AssignableFilterHolder
{
    /// <summary>
    /// 役職のゲーム開始時の表示
    /// </summary>
    string DisplayIntroBlurb => GeneralBlurb;

    /// <summary>
    /// ジャッカル化可能な場合はtrueを返します。
    /// ジャッカル用作用素を生成できる必要があります。
    /// </summary>
    bool IsJackalizable => false;

    /// <summary>
    /// ジャッカル化可能な場合はジャッカル用作用素を生成します。
    /// ジャッカル化可能でない場合の動作は未定義です。
    /// </summary>
    /// <param name="jackal"></param>
    /// <param name="arguments"></param>
    /// <returns></returns>
    IPlayerAbility GetJackalizedAbility(Virial.Game.Player jackal, int[] arguments) => null!;

    /// <summary>
    /// Madmate系役職の場合trueを返します。
    /// </summary>
    bool IsMadmate => false;

    /// <summary>
    /// 追加割り当てされる役職を一覧で返します。割り当て数と一致する必要はありません。
    /// </summary>
    DefinedRole[] AdditionalRoles => [];
}

public interface DefinedSingleAbilityRole<Ability> : DefinedRole, RuntimeAssignableGenerator<RuntimeRole> where Ability : class, IPlayerAbility
{
    Ability CreateAbility(Virial.Game.Player player, int[] arguments);
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Virial.Game.Player player, int[] arguments) => new RuntimeSingleAbilityAssignable<Ability>(player, this, arguments);
    IPlayerAbility DefinedRole.GetJackalizedAbility(Virial.Game.Player jackal, int[] arguments) => IsJackalizable ? CreateAbility(jackal, arguments) : null!;
}

/// <summary>
/// プレイヤーに割り当てられる幽霊役職の定義を表します。
/// </summary>
public interface DefinedGhostRole : DefinedCategorizedAssignable, RuntimeAssignableGenerator<RuntimeGhostRole>, ICodeName, HasRoleFilter, IHasCategorizedRoleAllocator<DefinedGhostRole>, IAssignToCategorizedRole
{
}

public interface IAssignToCategorizedRole
{
    void GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance);
}
/// <summary>
/// プレイヤーに割り当てられる追加役職の定義を表します。
/// </summary>
public interface DefinedModifier : DefinedAssignable, RuntimeAssignableGenerator<RuntimeModifier>
{
    string DefinedAssignable.GeneralBlurb => NebulaAPI.Language.Translate("role." + LocalizedName + ".generalBlurb");
}

/// <summary>
/// 独自の割り当てルーチンを持つ対象を表します。
/// </summary>
public interface HasAssignmentRoutine
{
    int AssignPriority { get; }
    void TryAssign(IRoleTable roleTable);
}

public interface DefinedAllocatableModifier : DefinedModifier, ICodeName, HasRoleFilter, HasAssignmentRoutine, ISpawnable, IAssignToCategorizedRole
{
}






/// <summary>
/// プレイヤーに割り当てられた役職や追加役職のコンテナを表します。
/// </summary>
public interface RuntimeAssignable : IBinder, ILifespan, IReleasable, IBindPlayer, IGameOperator
{
    /// <summary>
    /// 役職および追加役職の定義
    /// </summary>
    DefinedAssignable Assignable { get; }

    //AssignableAspectAPI

    /// <summary>
    /// 通信障害を修理できる場合trueを返します。
    /// </summary>
    bool CanFixComm => true;

    /// <summary>
    /// 通信障害を修理できる場合trueを返します。
    /// </summary>
    bool CanFixLight => true;

    /// <summary>
    /// 割り当てられていることを自覚できる場合trueを返します。
    /// </summary>
    bool CanBeAwareAssignment => true;

    /// <summary>
    /// 自身の能力が追加するアビリティを取得します。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<IPlayerAbility?> MyAbilities => [];
    public Ability? GetAbility<Ability>() where Ability : class, IPlayerAbility
    {
        foreach(var a in MyAbilities) if (a is Ability returned) return returned;
        return null;
    }

    /// <summary>
    /// ヘルプ画面上での表示に使用する役職です。
    /// CanBeAwareAssignmentがfalseの場合、この値は使用されません。
    /// </summary>
    IEnumerable<DefinedAssignable> AssignableOnHelp => [Assignable];

    /// <summary>
    /// 緊急ボタンを押すことができる場合trueを返します。
    /// </summary>
    bool CanCallEmergencyMeeting => true;

    /// <summary>
    /// 通報できる場合trueを返します。
    /// </summary>
    bool CanReport => true;

    //RoleNameAPI

    /// <summary>
    /// 役職が、元の役職名を書き換える場合に呼び出されます。
    /// </summary>
    /// <param name="lastRoleName"></param>
    /// <param name="isShort"></param>
    /// <returns></returns>
    string? OverrideRoleName(string lastRoleName, bool isShort) => null;

    string DisplayName => Assignable.DisplayName;
    string DisplayColoredName => Assignable.DisplayColoredName;

    /// <summary>
    /// 現在の状態を役職引数に変換します。
    /// </summary>
    int[]? RoleArguments { get => null; }

    //GameFlowAPI

    /// <summary>
    /// 役職の割り当て時に呼び出されます。
    /// </summary>
    protected internal void OnActivated();

    /// <summary>
    /// 役職が失わる時に呼び出されます。
    /// </summary>
    protected void OnInactivated() { }

    internal sealed void Inactivate()
    {
        (this as IReleasable).Release();
        OnInactivated();
    }

    /// <summary>
    /// 名前のテキストを恒常的に書き換えます。
    /// 状態に依らない書き換えが推奨されます。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="canSeeAllInfo"></param>
    void DecorateNameConstantly(ref string name, bool canSeeAllInfo) { }

    bool CanKill(Virial.Game.Player player) => true;
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
    /// <summary>
    /// 対外的な認識の役職定義
    /// </summary>
    DefinedRole ExternalRecognitionRole => Role;
    DefinedAssignable RuntimeAssignable.Assignable => Role;

    //AssignableAspectAPI

    /// <summary>
    /// サボタージュを起こすことができる場合trueを返します。
    /// </summary>
    bool CanInvokeSabotage => Role.Category == RoleCategory.ImpostorRole;

    /// <summary>
    /// バニラのキルボタンを使用できる場合trueを返します。
    /// </summary>
    bool HasVanillaKillButton => Role.Category == RoleCategory.ImpostorRole;

    /// <summary>
    /// ベントを使用できる場合はtrueを返します。
    /// </summary>
    bool CanUseVent => Role.Category == RoleCategory.ImpostorRole;
    /// <summary>
    /// ベントの使用を一時的に阻むときにtrueを返します。
    /// </summary>
    bool PreventUsingVent => false;

    /// <summary>
    /// ベントの使用にクールダウンを設けます。
    /// </summary>
    Virial.Components.GameTimer? VentCoolDown => null;

    /// <summary>
    /// ベントの潜伏時間に制限を設けます。
    /// </summary>
    Virial.Components.GameTimer? VentDuration => null;

    /// <summary>
    /// ベント間の移動ができる場合はtrueを返します。
    /// <see cref="CanUseVent"/>がfalseの場合は意味がありません。
    /// </summary>
    bool CanMoveInVent => true;

    /// <summary>
    /// インポスターの視界を持つ場合はtrueを返します。
    /// </summary>
    bool HasImpostorVision => Role.Category == RoleCategory.ImpostorRole;

    /// <summary>
    /// 停電の影響を受ける場合はtrueを返します。
    /// </summary>
    bool IgnoreBlackout => HasImpostorVision;

    /// <summary>
    /// 視界が壁を無視する場合はtrueを返します。
    /// </summary>
    bool EyesightIgnoreWalls => false;

    /// <summary>
    /// 他人のフェイクサボタージュが確認できる場合はtrueを返します。
    /// </summary>
    bool CanSeeOthersFakeSabotage => Role.Category == RoleCategory.ImpostorRole;

    /// <summary>
    /// ノイズメーカーの通知を拒否する場合はtrueを返します。
    /// </summary>
    bool IgnoreNoisemakerNotification => false;

    /// <summary>
    /// 役職の省略名です。
    /// </summary>
    string DisplayShort => Role.DisplayShort;
    string DisplayColoredShort => Role.DisplayColoredShort;

    RoleTaskType TaskType => Role.Category == RoleCategory.CrewmateRole ? RoleTaskType.CrewmateTask : RoleTaskType.NoTask;

    string DisplayIntroBlurb => Role.DisplayIntroBlurb;
    string DisplayIntroRoleName => Role.DisplayColoredName;

    /// <summary>
    /// チームの関係でキルできるか否かを調べます。ここで生死や距離を考慮する必要はありません。
    /// </summary>
    /// <returns></returns>
    bool RuntimeAssignable.CanKill(Virial.Game.Player player) => Role.Category is RoleCategory.ImpostorRole ? player.Role.Role.Category != RoleCategory.ImpostorRole : true;
}

/// <summary>
/// プレイヤーに割り当てられた役職のコンテナを表します。
/// </summary>
public interface RuntimeGhostRole : RuntimeAssignable
{
    /// <summary>
    /// 役職の定義
    /// </summary>
    DefinedGhostRole Role { get; }
    DefinedAssignable RuntimeAssignable.Assignable => Role;

    /// <summary>
    /// 役職の省略名です。
    /// </summary>
    string DisplayShort => Role.DisplayShort;
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

    /// <summary>
    /// ゲーム開始時に割り当てられているとき、役職開示画面で表示されます。
    /// </summary>
    string? DisplayIntroBlurb => null;

    /// <summary>
    /// クルーメイトタスクを持っていた場合、明示的に無効化する場合はtrue
    /// </summary>
    public virtual bool InvalidateCrewmateTask => false;

    /// <summary>
    /// クルーメイトタスクを持っていたとしても、クルーメイトタスクの総数に計上されない場合はtrue
    /// </summary>
    public virtual bool MyCrewmateTaskIsIgnored => false;

    public virtual bool IsMadmate => false;
}
