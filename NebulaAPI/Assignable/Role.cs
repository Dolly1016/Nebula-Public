using Virial.Attributes;
using Virial.Configuration;
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
    /// インポスター役職。
    /// </summary>
    ImpostorRole = 0x01,
    /// <summary>
    /// 第三陣営役職。
    /// </summary>
    NeutralRole = 0x02,
    /// <summary>
    /// クルーメイト役職。
    /// </summary>
    CrewmateRole = 0x04,
}

/// <summary>
/// その役職のプレイヤーに割り当てられるタスクの種別を表します。
/// </summary>
public enum RoleTaskType
{
    /// <summary>
    /// クルーメイトのタスク。
    /// タスクノルマを持ち、このノルマはクルーメイト勝利に必要なタスクとしてカウントされます。
    /// </summary>
    CrewmateTask,
    /// <summary>
    /// 個人のタスク。
    /// タスクノルマを持つものの、クルーメイト勝利に必要なタスクにはカウントされません。
    /// </summary>
    RoleTask,
    /// <summary>
    /// タスクを持たない。
    /// 主にインポスターや第三陣営が該当します。
    /// </summary>
    NoTask,
}

/// <summary>
/// 短縮した、通常大文字アルファベット3文字からなる名前を持ちます。
/// モディファイアはオプションの簡潔なシリアライズのため、コードネームを必要とします。
/// </summary>
public interface ICodeName
{
    /// <summary>
    /// 役職のコードネーム。他の役職と被らないようにしてください。
    /// 記号類の使用は推奨されません。
    /// </summary>
    string CodeName { get; }
}

/// <summary>
/// ゲーム中に出現しうる役職が実装するインターフェースです。
/// </summary>
public interface ISpawnable
{
    /// <summary>
    /// ゲーム中に出現しうる場合はtrueを返します。
    /// この値は主にゲーム設定によって変化します。
    /// </summary>
    bool IsSpawnable { get; }
}

/// <summary>
/// 役職割り当て器です。
/// 該当の役職が何度割り当てられたか記憶し、都度適切な割り当て確率を返します。
/// </summary>
/// <typeparam name="R"></typeparam>
public interface ICategorizedRoleAllocator<R> where R : DefinedAssignable
{
    /// <summary>
    /// 自身の役職です。
    /// </summary>
    R MyRole { get; }
    /// <summary>
    /// 割り当て確率を0～100の間で返します。
    /// </summary>
    /// <param name="category">割り当てカテゴリ。</param>
    /// <returns></returns>
    int GetChance(RoleCategory category);
    /// <summary>
    /// 役職を割り当てます。
    /// このメソッドの呼び出しによって今後、割り当て確率が変化する可能性があります。
    /// </summary>
    /// <param name="category">割り当てカテゴリ。</param>
    void ConsumeCount(RoleCategory category);
}

/// <summary>
/// 役職割り当て器を提供する役職を表します。
/// </summary>
/// <typeparam name="R"></typeparam>
public interface IHasCategorizedRoleAllocator<R> where R : DefinedAssignable
{
    /// <summary>
    /// 役職割り当て器を生成します。
    /// ゲームの度に新たな割り当て器が生成されます。
    /// </summary>
    /// <returns></returns>
    ICategorizedRoleAllocator<R> GenerateRoleAllocator();
}

/// <summary>
/// 引用元を持つ対象を表します。
/// </summary>
public interface HasCitation
{
    /// <summary>
    /// 引用元を表します。
    /// </summary>
    Citation? Citation { get; }
}

/// <summary>
/// 役職IDをもつ対象を表します。
/// </summary>
public interface IRoleID
{
    /// <summary>
    /// 役職のIDを返します。
    /// IDはアドオンの構成やバージョン間の違いによって変化します。
    /// </summary>
    int Id { get; internal set; }
}

/// <summary>
/// 役職フィルタをもつ対象を表します。
/// モディファイアや幽霊役職が該当します。
/// </summary>
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

    /// <summary>
    /// フリープレイの役職一覧に表示するかどうか設定できます。
    /// </summary>
    bool ShowOnFreeplayScreen { get => true; }

    /// <summary>
    /// 役職の内部名です。
    /// </summary>
    string InternalName => LocalizedName;

    /// <summary>
    /// 役職の表示名です。
    /// </summary>
    string DisplayName => NebulaAPI.Language.Translate("role." + LocalizedName + ".name");
    /// <summary>
    /// 役職の表示名です。リッチテキストタグを用いて色を付けています。
    /// </summary>
    string DisplayColoredName => DisplayName.Color(UnityColor);

    /// <summary>
    /// 一般的な二つ名テキストです。
    /// </summary>
    string GeneralBlurb => NebulaAPI.Language.Translate("role." + LocalizedName + ".blurb");
    /// <summary>
    /// 一般的な二つ名テキストです。リッチテキストタグを用いて色を付けています。
    /// </summary>
    string GeneralColoredBlurb => GeneralBlurb.Color(UnityColor);

    /// <summary>
    /// 役職の色です。
    /// </summary>
    Virial.Color Color { get; }
    internal UnityEngine.Color UnityColor { get; }

    /// <summary>
    /// 自身の役職の設定ホルダです。設定ホルダを持たない場合、nullを返します。
    /// </summary>

    IConfigurationHolder? ConfigurationHolder { get; }

    /// <summary>
    /// ヘルプ画面で表示する称号のグループです。
    /// このグループに含まれるすべての役職の称号が表示されます。
    /// </summary>

    IEnumerable<DefinedAssignable> AchievementGroups => [this];
}

/// <summary>
/// 役職カテゴリで分類される役職の定義を表します。
/// クルーメイト役職・インポスター役職・第三陣営役職が該当します。
/// </summary>
public interface DefinedCategorizedAssignable : DefinedAssignable
{
    /// <summary>
    /// 役職のカテゴリ。役職割り当て時に使用します。
    /// </summary>
    RoleCategory Category { get; }

    /// <summary>
    /// 役職の省略名です。ゲーム終了時の役職開示画面などで使用します。
    /// </summary>
    string DisplayShort => NebulaAPI.Language.Translate("role." + LocalizedName + ".short");
    /// <summary>
    /// 役職の省略名です。リッチテキストタグを用いて色を付けています。
    /// </summary>
    string DisplayColoredShort => DisplayShort.Color(UnityColor);
}

/// <summary>
/// 単一のチームに属する役職の定義です。
/// </summary>
public interface DefinedSingleAssignable : DefinedCategorizedAssignable, ISpawnable
{
    /// <summary>
    /// 役職の属する陣営を表します。おもに勝敗判定に使用されます。
    /// </summary>
    RoleTeam Team { get; }

    /// <summary>
    /// 割り当てのパラメータを返します。ゲーム内オプションで設定した割り当てを表します。
    /// </summary>
    AllocationParameters? AllocationParameters { get; }
    /// <summary>
    /// ジャッカル化割り当てのパラメータを返します。ゲーム内オプションで設定した割り当てを表します。
    /// </summary>
    AllocationParameters? JackalAllocationParameters { get => null; }
}

/// <summary>
/// 役職実体を生成する対象を表します。
/// </summary>
/// <typeparam name="T"></typeparam>
public interface RuntimeAssignableGenerator<T> where T : RuntimeAssignable   
{
    /// <summary>
    /// 役職実体を生成します。
    /// </summary>
    /// <param name="player">割り当て先のプレイヤー</param>
    /// <param name="arguments">割り当て時の引数</param>
    /// <returns>生成された役職実体。</returns>
    T CreateInstance(Virial.Game.Player player, int[] arguments);
}

/// <summary>
/// 推察可能になりうる対象を表します。
/// </summary>
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

    /// <summary>
    /// Guesserによる推測対象になるか設定する変数を返します。通常、オプションがこの値を変更します。
    /// </summary>
    ISharableVariable<bool>? CanBeGuessVariable { get; internal set; }
}

/// <summary>
/// モディファイアおよび幽霊役職の割り当てフィルタを持つ対象を表します。
/// </summary>
public interface AssignableFilterHolder
{
    /// <summary>
    /// 付与されうるモディファイアを制限するフィルタです。
    /// AllocatableDefinedModifierのRoleFilterと同期しています。
    /// </summary>
    ModifierFilter? ModifierFilter { get; }


    /// <summary>
    /// 付与されうる幽霊役職を制限するフィルタ。
    /// DefinedGhostRoleのRoleFilterと同期しています。
    /// </summary>
    GhostRoleFilter? GhostRoleFilter { get; }

    /// <summary>
    /// デフォルト設定で幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="assignable">調べる対象の役職定義</param>
    /// <returns>割り当てられうる場合、true</returns>
    bool CanLoadDefault(DefinedAssignable assignable);

    /// <summary>
    /// 幽霊役職/モディファイアを割り当てられるかどうか返します。
    /// </summary>
    /// <param name="assignable">調べる対象の役職定義</param>
    /// <returns>割り当てられうる場合、true</returns>
    bool CanLoad(DefinedAssignable assignable);
}

/// <summary>
/// 割り当てパラメータを表します。
/// このパラメータの値は通常、オプションによって変更されます。
/// </summary>
public interface AllocationParameters
{
    public delegate (DefinedRole role, int[]? argument) ExtraAssignment(DefinedRole assignable, int playerId);

    /// <summary>
    /// 付随役職の割り当てを表します。
    /// </summary>
    /// <param name="Assigner"></param>
    /// <param name="Category"></param>
    public record ExtraAssignmentInfo(ExtraAssignment Assigner, RoleCategory Category);

    /// <summary>
    /// 関係するすべてのコンフィグを返します。
    /// </summary>
    IEnumerable<IConfiguration> Configurations { get; }

    /// <summary>
    /// 割り当て数の総和です。
    /// </summary>
    int RoleCountSum { get; }

    /// <summary>
    /// 100%割り当て数です。
    /// </summary>
    int RoleCount100 { get; }
    /// <summary>
    /// 確率的な割り当て数です。
    /// </summary>
    int RoleCountRandom { get; }

    /// <summary>
    /// 同陣営への追加割り当てです。
    /// </summary>
    ExtraAssignmentInfo[] TeamAssignment => [];
    /// <summary>
    /// 別陣営への追加割り当てです。
    /// </summary>
    ExtraAssignmentInfo[] OthersAssignment => [];
    /// <summary>
    /// 追加割り当てを持つ場合、trueを返します。
    /// </summary>
    bool HasExtraAssignment => TeamAssignment.Length > 0 || OthersAssignment.Length > 0;
    /// <summary>
    /// 同陣営への割り当てに必要なコストです。
    /// </summary>
    int TeamCost => 1 + TeamAssignment.Length;
    /// <summary>
    /// 残り陣営への割り当てに必要なコストです。
    /// </summary>
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
    /// 役職のゲーム開始時の表示。
    /// </summary>
    string DisplayIntroBlurb => GeneralBlurb;

    /// <summary>
    /// ジャッカル化可能な場合はtrueを返します。
    /// ジャッカル用作用素を生成できる必要があります。
    /// </summary>
    bool IsJackalizable => false;

    /// <summary>
    /// ジャッカル化可能な場合はジャッカル化能力を生成します。
    /// ジャッカル化可能でない場合の動作は未定義で構いません。
    /// </summary>
    /// <param name="jackal">割り当て対象のプレイヤー。</param>
    /// <param name="arguments">割り当てのパラメータ。</param>
    /// <returns></returns>
    IPlayerAbility GetJackalizedAbility(Virial.Game.Player jackal, int[] arguments) => null!;
    /// <summary>
    /// 簒奪された能力を生成します。
    /// </summary>
    /// <param name="player">割り当て対象のプレイヤー。</param>
    /// <param name="arguments">割り当てのパラメータ。</param>
    /// <returns>簒奪された能力。簒奪不可能な場合はnull。</returns>
    IUsurpableAbility? GetUsurpedAbility(Virial.Game.Player player, int[] arguments) => null!;

    /// <summary>
    /// マッドメイト系の役職の場合はtrueを返します。
    /// </summary>
    bool IsMadmate => false;

    /// <summary>
    /// 追加割り当てされる役職を一覧で返します。割り当て数と一致する必要はありません。
    /// </summary>
    DefinedRole[] AdditionalRoles => [];

    /// <summary>
    /// 能力から表示名を取得します。主にジャッカル化能力と簒奪された能力で使用します。
    /// </summary>
    /// <param name="ability"></param>
    /// <returns></returns>
    string GetDisplayName(IPlayerAbility ability) => DisplayName;
}

/// <summary>
/// ジャッカル化あるいは簒奪された能力、および通常の能力を同一のクラスで定義できる役職の定義を表します。
/// </summary>
/// <typeparam name="Ability">役職の能力。</typeparam>
public interface DefinedSingleAbilityRole<Ability> : DefinedRole, RuntimeAssignableGenerator<RuntimeRole> where Ability : class, IPlayerAbility
{
    /// <summary>
    /// 役職の能力を生成します。
    /// </summary>
    /// <param name="player">割り当て対象のプレイヤー。</param>
    /// <param name="arguments">割り当てのパラメータ。</param>
    /// <returns></returns>
    Ability CreateAbility(Virial.Game.Player player, int[] arguments);
    private IUsurpableAbility? CreateUsurpedAbility(Virial.Game.Player player, int[] arguments) => typeof(Ability).IsAssignableTo(typeof(IUsurpableAbility)) ? CreateAbility(player, arguments) as IUsurpableAbility : null;
    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(Virial.Game.Player player, int[] arguments) => new RuntimeSingleAbilityAssignable<Ability>(player, this, arguments);
    IPlayerAbility DefinedRole.GetJackalizedAbility(Virial.Game.Player jackal, int[] arguments) => IsJackalizable ? CreateAbility(jackal, arguments) : null!;
    IUsurpableAbility? DefinedRole.GetUsurpedAbility(Virial.Game.Player player, int[] arguments) => CreateUsurpedAbility(player, arguments);
    /// <summary>
    /// 能力から役職の表示名を取得します。
    /// </summary>
    /// <param name="ability">能力</param>
    /// <returns></returns>
    string? GetDisplayAbilityName(Ability ability) => null;
    string DefinedRole.GetDisplayName(IPlayerAbility ability)
    {
        if(ability is Ability a)
        {
            return GetDisplayAbilityName(a) ?? DisplayName;
        }
        return DisplayName;
    }
}

/// <summary>
/// 幽霊役職の役職定義を表します。
/// </summary>
public interface DefinedGhostRole : DefinedCategorizedAssignable, RuntimeAssignableGenerator<RuntimeGhostRole>, ICodeName, HasRoleFilter, IHasCategorizedRoleAllocator<DefinedGhostRole>, IAssignToCategorizedRole
{
}

/// <summary>
/// 役職が割り当てられたプレイヤーに、役職のカテゴリに応じて異なる割り当て確率を与える割り当てパラメータを表します。
/// </summary>
public interface IAssignToCategorizedRole
{
    /// <summary>
    /// カテゴリごとの割り当てパラメータを取得します。
    /// </summary>
    /// <param name="category">カテゴリ。</param>
    /// <param name="assign100">100%割り当て数。</param>
    /// <param name="assignRandom">ランダム割り当て数。</param>
    /// <param name="assignChance">ランダム割り当ての割り当て確率。</param>
    void GetAssignProperties(RoleCategory category, out int assign100, out int assignRandom, out int assignChance);
}
/// <summary>
/// プレイヤーに割り当てられるモディファイアの役職定義を表します。
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
    /// <summary>
    /// 割り当ての優先度を返します。
    /// </summary>
    int AssignPriority { get; }

    /// <summary>
    /// 割り当てを試行します。
    /// </summary>
    /// <param name="roleTable">割り当てテーブル</param>
    void TryAssign(IRoleTable roleTable);
}

/// <summary>
/// ゲーム開始時に割り当てられるモディファイアの役職定義を表します。
/// </summary>

public interface DefinedAllocatableModifier : DefinedModifier, ICodeName, HasRoleFilter, HasAssignmentRoutine, ISpawnable, IAssignToCategorizedRole
{
}

/// <summary>
/// 役職実体です。
/// プレイヤーに実際に割り当てられた役職やモディファイアのコンテナです。
/// </summary>
public interface RuntimeAssignable : ILifespan, IBindPlayer, IGameOperator, IReleasable
{
    /// <summary>
    /// 役職定義。
    /// </summary>
    DefinedAssignable Assignable { get; }

    //AssignableAspectAPI

    /// <summary>
    /// 通信障害を修理できる場合trueを返します。
    /// </summary>
    [Obsolete(AttributeConstants.ObsoleteText)]
    bool CanFixComm => true;

    /// <summary>
    /// 通信障害を修理できる場合trueを返します。
    /// </summary>
    [Obsolete(AttributeConstants.ObsoleteText)]
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

    /// <summary>
    /// アビリティを取得します。
    /// </summary>
    /// <typeparam name="Ability"></typeparam>
    /// <returns></returns>
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
    [Obsolete(AttributeConstants.ObsoleteText)]
    bool CanCallEmergencyMeeting => true;

    /// <summary>
    /// 通報できる場合trueを返します。
    /// </summary>
    [Obsolete(AttributeConstants.ObsoleteText)]
    bool CanReport => true;


    /// <summary>
    /// 役職が、元の役職名を書き換える場合に呼び出されます。
    /// </summary>
    /// <param name="lastRoleName"></param>
    /// <param name="isShort"></param>
    /// <returns></returns>
    string? OverrideRoleName(string lastRoleName, bool isShort) => null;

    /// <summary>
    /// 役職の表示名です。特に指定しない場合、役職定義の表示名をそのまま使用します。
    /// </summary>
    string DisplayName => Assignable.DisplayName;
    /// <summary>
    /// 役職の表示名です。リッチテキストタグを用いて色を付けています。特に指定しない場合、役職定義の表示名をそのまま使用します。
    /// </summary>
    string DisplayColoredName => Assignable.DisplayColoredName;

    /// <summary>
    /// 割り当てのパラメータを取得します。
    /// </summary>
    int[]? RoleArguments { get => null; }

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
        this.Release();
        OnInactivated();
    }

    /// <summary>
    /// 名前のテキストを恒常的に書き換えます。
    /// 状態に依らない書き換えが推奨されます。
    /// </summary>
    /// <param name="name"></param>
    /// <param name="canSeeAllInfo"></param>
    void DecorateNameConstantly(ref string name, bool canSeeAllInfo) { }

    /// <summary>
    /// プレイヤーをキルできるか調べます。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
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
    /// 能力を簒奪します。RPCを送って全クライアントでUsurpを実行する必要があります。
    /// </summary>
    /// <returns></returns>
    void Usurp() { }
    
    /// <summary>
    /// 現在の状態を簒奪可能能力の引数に変換します。
    /// </summary>
    int[]? UsurpedAbilityArguments { get => null; }

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
