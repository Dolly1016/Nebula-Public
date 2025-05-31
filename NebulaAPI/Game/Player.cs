using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Virial.Assignable;
using Virial.Attributes;
using Virial.Command;
using Virial.DI;
using Virial.Helpers;
using Virial.Media;
using Virial.Text;

namespace Virial.Game;

public enum KillResult
{
    Kill,
    Guard,
    ObviousGuard,
    Rejected,
}

public interface IPlayerAttribute
{
    internal int Id { get; }
    internal string Name { get; }
    internal string UIName { get; }
    
    /// <summary>
    /// 分類上の属性を取得します。
    /// </summary>
    IPlayerAttribute CategorizedAttribute { get; }

    /// <summary>
    /// この属性のアイコンを指定します。
    /// </summary>
    Media.Image Image { get; }

    /// <summary>
    /// プレイヤーが属性を認識できるかどうか調べます。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    bool CanCognize(Player player);
}

public static class PlayerAttributes
{
    /// <summary>
    /// 加速効果を表します。速度倍率のパラメータがあります。
    /// </summary>
    static public IPlayerAttribute Accel { get; internal set; }

    /// <summary>
    /// 減速効果を表します。速度倍率のパラメータがあります。
    /// </summary>
    static public IPlayerAttribute Decel { get; internal set; }

    /// <summary>
    /// 速度反転効果を表します。速度倍率のパラメータがあります。
    /// </summary>
    static public IPlayerAttribute Drunk { get; internal set; }

    /// <summary>
    /// サイズ変更効果を表します。サイズ倍率のパラメータがあります。
    /// </summary>
    static public IPlayerAttribute Size { get; internal set; }

    /// <summary>
    /// 透明化効果を表します。
    /// </summary>
    static public IPlayerAttribute Invisible { get; internal set; }

    /// <summary>
    /// Bloodyの血の足跡効果を表します。
    /// </summary>
    static public IPlayerAttribute CurseOfBloody { get; internal set; }

    /// <summary>
    /// パーク「痕跡」の足跡効果を表します。
    /// </summary>
    static public IPlayerAttribute Footprint { get; internal set; }

    /// <summary>
    /// Effacerのインポスターにだけ見える透明化効果を表します。
    /// </summary>
    static public IPlayerAttribute InvisibleElseImpostor { get; internal set; }

    /// <summary>
    /// Alienの情報端末からの無縁化効果を表します。
    /// </summary>
    static public IPlayerAttribute Isolation { get; internal set; }

    /// <summary>
    /// Buskerの偽装死を隠蔽する効果を表します。効果は偽装死に限らず適用されます。
    /// </summary>
    static public IPlayerAttribute BuskerEffect { get; internal set; }

    /// <summary>
    /// 左右反転効果を表します。
    /// </summary>

    static public IPlayerAttribute FlipX { get; internal set; }

    /// <summary>
    /// 上下反転効果を表します。
    /// </summary>
    static public IPlayerAttribute FlipY { get; internal set; }

    /// <summary>
    /// 左右上下反転効果を表します。実際の効果は180度回転です。
    /// </summary>
    static public IPlayerAttribute FlipXY { get; internal set; }

    /// <summary>
    /// 画面拡大/縮小効果を表します。
    /// </summary>
    static public IPlayerAttribute ScreenSize { get; internal set; }

    /// <summary>
    /// 視野拡大/縮小効果を表します。
    /// </summary>
    static public IPlayerAttribute Eyesight { get; internal set; }

    /// <summary>
    /// モザイク効果を表します。
    /// </summary>
    static public IPlayerAttribute Roughening { get; internal set; }

    /// <summary>
    /// Thuriferの感染状態を表します。
    /// </summary>
    static public IPlayerAttribute Thurifer { get; internal set; }

    /// <summary>
    /// クールダウンの進行速度上昇効果を表します。
    /// </summary>
    static public IPlayerAttribute CooldownSpeed { get; internal set; }
}

public class Outfit
{
    internal NetworkedPlayerInfo.PlayerOutfit outfit { get; private set; }
    internal OutfitTag[] tags { get; private set; }

    internal Outfit(NetworkedPlayerInfo.PlayerOutfit outfit, OutfitTag[] tags, bool clone = false)
    {
        if (clone)
        {
            this.outfit = new();
            this.outfit.HatId = outfit.HatId;
            this.outfit.VisorId = outfit.VisorId;
            this.outfit.SkinId = outfit.SkinId;
            this.outfit.ColorId = outfit.ColorId;
            this.outfit.PetId = outfit.PetId;
            this.outfit.PlayerName = outfit.PlayerName;
            this.outfit.NamePlateId = outfit.NamePlateId;
        }
        else
        {
            this.outfit = outfit;
        }

        this.tags = tags;
    }

    public override bool Equals(object? obj)
    {
        NetworkedPlayerInfo.PlayerOutfit? outfit = null;
        if (obj is Outfit o) outfit = o.outfit;
        else if (obj is NetworkedPlayerInfo.PlayerOutfit po) outfit = po;

        if(outfit != null){
            return 
                outfit.HatId == this.outfit.HatId &&
                outfit.SkinId == this.outfit.SkinId &&
                outfit.VisorId == this.outfit.VisorId &&
                outfit.ColorId == this.outfit.ColorId;
        }
        return false;
    }
}

public record OutfitTag {
    public Virial.Media.Image TagImage { get;private init; }
    private string TranslationKey { get; init; }
    public string DisplayName => NebulaAPI.Language.Translate("outfit.tag." + TranslationKey);
    public int Id { get; private init; }

    internal Predicate<NetworkedPlayerInfo.PlayerOutfit> Checker { get; private init; }

    internal OutfitTag(Virial.Media.Image tagImage, string translationKey, Predicate<NetworkedPlayerInfo.PlayerOutfit> checker)
    {
        TagImage = tagImage;
        TranslationKey = translationKey;
        Id = NebulaAPI.Hasher.GetIntegerHash(translationKey);
        Checker = checker;
        AllTags[Id] = this;
    }


    private static Dictionary<int, OutfitTag> AllTags = new();
    public static OutfitTag GetTagById(int id) => AllTags[id];
    public static IEnumerable<OutfitTag> GetAllTags() => AllTags.Values;
}
public class OutfitDefinition
{
    public record OutfitId(int ownerId, int outfitId)
    {
        static public OutfitId PlayersDefault(int playerId) => new(playerId, 0);
    }
    public OutfitId Id { get; private init; }
    public OutfitTag[] OutfitTags { get; private init; }
    internal NetworkedPlayerInfo.PlayerOutfit outfit { get; private init; }
    public string? HatArgument { get; internal set; } = null;
    public string? VisorArgument { get; internal set; } = null;

    internal OutfitDefinition(OutfitId id, NetworkedPlayerInfo.PlayerOutfit outfit, OutfitTag[] outfitTags)
    {
        this.Id = id;
        this.outfit = outfit;
        this.OutfitTags = outfitTags;
    }

    internal OutfitDefinition(OutfitId id, Outfit outfit)
    {
        this.Id = id;
        OutfitTags = outfit.tags;
        this.outfit = outfit.outfit;
    }
}

public class OutfitCandidate
{
    public OutfitDefinition Outfit { get; private set; }
    public string Tag { get; private set; }
    public int Priority { get; private set; }
    public bool SelfAware { get; private set; }

    public OutfitCandidate(OutfitDefinition definition, string tag, int priority, bool selfAware)
    {
        this.Outfit = definition;
        Tag = tag;
        Priority = priority;
        SelfAware = selfAware;
    }
}

public record PlayerDiving();

[Flags]
public enum KillParameter
{
    WithBlink = 0x01,
    WithOverlay = 0x02,
    WithAssigningGhostRole = 0x04,
    WithKillSEWidely = 0x08,
    WithDeadBody = 0x10,
    RemoteKill = WithOverlay | WithAssigningGhostRole | WithDeadBody,
    NormalKill = WithBlink | RemoteKill,
    MeetingKill = WithOverlay | WithAssigningGhostRole | WithKillSEWidely
}

public interface Player : IModuleContainer, ICommandExecutor, IArchivedPlayer, IPlayerlike
{
    public abstract record ExtraDeadInfo(CommunicableTextTag State)
    {
        public abstract string ToStateText();
    }

    // Internal

    internal PlayerControl VanillaPlayer { get; }
    internal DeadBody? RelatedDeadBody { get; }


    // PlayerAPI

    /// <summary>
    /// プレイヤーの名前です。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// ゲーム内でプレイヤーを識別するIDです。１ゲームの中で変わることはありません。
    /// </summary>
    public byte PlayerId { get; }

    /// <summary>
    /// 死亡しているとき、Trueを返します。
    /// </summary>
    public bool IsDead { get; }

    /// <summary>
    /// 死が確定しているとき、Trueを返します。
    /// 死亡していても、Trueを返さないときがあります。IsDeadと併用して使用する必要があります。
    /// </summary>
    public bool WillDie { get; }

    public PlayerDiving? CurrentDiving { get; }
    /// <summary>
    /// ダイブしているとき、Trueを返します。
    /// </summary>
    public bool IsDived => CurrentDiving != null;

    /// <summary>
    /// テレポート中、Trueを返します。
    /// </summary>
    public bool IsTeleporting { get; }

    /// <summary>
    /// 吹き飛ばされているとき、Trueを返します。
    /// </summary>
    public bool IsBlown { get; }

    /// <summary>
    /// 死亡時刻をゲーム開始からの経過時間で返します。
    /// </summary>
    public float? DeathTime { get; }

    /// <summary>
    /// 切断されているとき、Trueを返します。切断されている場合は死亡しているものとして扱われます。
    /// </summary>
    bool IsDisconnected { get; }

    /// <summary>
    /// 自身がこのプレイヤーの本来の操作主である場合、Trueを返します。
    /// </summary>
    public bool AmOwner { get; }

    /// <summary>
    /// 自身がこのゲームのホストである場合、Trueを返します。
    /// </summary>
    public bool AmHost { get; }

    /// <summary>
    /// 梯子を使うなどの理由で操作不能な状態になっていない場合、Trueを返します。
    /// </summary>
    public bool CanMove { get; }

    /// <summary>
    /// プレイヤーの足元の座標を返します。
    /// </summary>
    public Compat.Vector2 TruePosition { get; }

    /// <summary>
    /// プレイヤーの現在の状態を表すタグです。
    /// </summary>
    public CommunicableTextTag PlayerState { get; }
    /// <summary>
    /// プレイヤーの死因についての詳細です。死因とPlayerStateが一致しない場合は無視すべきです。
    /// </summary>
    public ExtraDeadInfo? PlayerStateExtraInfo { get; set; }

    /// <summary>
    /// 陣営の基本的なキルクールダウンです。
    /// </summary>
    public float TeamKillCooldown => Role.Role.Team.KillCooldown;

    // HoldingAPI

    /// <summary>
    /// いま掴んでいるプレイヤーを返します。
    /// </summary>
    public Player? HoldingPlayer { get; }
    public bool HoldingAnyPlayer { get; }

    /// <summary>
    /// いま掴んでいる死体を返します。
    /// </summary>
    public Player? HoldingDeadBody { get; }
    public bool HoldingAnyDeadBody { get; }

    /// <summary>
    /// 死体を掴みます。
    /// </summary>
    /// <param name="deadBody"></param>
    public void HoldDeadBody(Player? deadBody);
    internal void HoldDeadBodyFast(DeadBody? deadBody);

    /// <summary>
    /// 掴んでいる死体を放します。
    /// </summary>
    public void ReleaseDeadBody();

    /// <summary>
    /// プレイヤーを掴みます。
    /// </summary>
    /// <param name="player"></param>
    public void HoldPlayer(Player? player);

    /// <summary>
    /// 掴んでいるプレイヤーを放します。
    /// </summary>
    public void ReleaseHoldingPlayer();

    /// <summary>
    /// 役職の関係性でキルできるかどうかをチェックします。
    /// </summary>
    /// <param name="player"></param>
    /// <returns></returns>
    public bool CanKill(Player player) => AllAssigned().All(a => a.CanKill(player));


    // MurderAPI

    /// <summary>
    /// プレイヤーをキルします。会議中の場合は<paramref name="killParams"/>の死体を残す設定は無視され、強制的に死体を残さないキルを起こします。
    /// </summary>
    /// <param name="player">キル対象のプレイヤー。</param>
    /// <param name="playerState">キル後のプレイヤー状態。</param>
    /// <param name="eventDetail">キルイベントの詳細。</param>
    /// <param name="killParams">キルのパラメータ。</param>
    /// <param name="killCondition">キルが通る条件。</param>
    /// <param name="callBack">キル時に呼び出されるコールバック。</param>
    /// <returns></returns>
    public void MurderPlayer(Player player, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams, KillCondition killCondition, Action<KillResult>? callBack = null);
    /// <summary>
    /// プレイヤーをキルします。会議中の場合は<paramref name="killParams"/>の死体を残す設定は無視され、強制的に死体を残さないキルを起こします。
    /// </summary>
    /// <param name="player">キル対象のプレイヤー。</param>
    /// <param name="playerState">キル後のプレイヤー状態。</param>
    /// <param name="eventDetail">キルイベントの詳細。</param>
    /// <param name="killParams">キルのパラメータ。</param>
    /// <param name="callBack">キル時に呼び出されるコールバック。</param>
    public void MurderPlayer(Player player, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams, Action<KillResult>? callBack = null)
        => MurderPlayer(player, playerState, eventDetail, killParams, KillCondition.BothAlive, callBack);
    /// <summary>
    /// 自殺します。
    /// </summary>
    /// <param name="playerState">自殺後のプレイヤー状態。</param>
    /// <param name="eventDetail">自殺イベントの詳細。</param>
    /// <param name="killParams">キルのパラメータ。</param>
    /// <param name="callBack">自殺時に呼び出されるコールバック。</param>
    public void Suicide(CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams, Action<KillResult>? callBack = null);
    /// <summary>
    /// 復活します。
    /// </summary>
    /// <param name="healer">復活者。</param>
    /// <param name="position">復活位置。</param>
    /// <param name="eraseDeadBody">死体を消去したうえで復活する場合、true。</param>
    /// <param name="recordEvent">復活イベントをゲーム履歴に残す場合、true。</param>
    public void Revive(Player? healer, Virial.Compat.Vector2 position, bool eraseDeadBody, bool recordEvent = true);
    /// <summary>
    /// 自身をキルした相手を返します。
    /// </summary>
    public Player? MyKiller { get; }




    // AttributeAPI

    /// <summary>
    /// アトリビュートを付与します。
    /// </summary>
    /// <param name="attribute">付与するアトリビュート</param>
    /// <param name="duration">効果時間</param>
    /// <param name="canPassMeeting">会議を超えて効果が持続する場合、<c>true</c></param>
    /// <param name="priority">優先度</param>
    /// <param name="duplicateTag">重複チェック用タグ</param>
    public void GainAttribute(IPlayerAttribute attribute, float duration, bool canPassMeeting, int priority, string? duplicateTag = null);
    /// <summary>
    /// 係数付きアトリビュートを付与します。
    /// </summary>
    /// <remarks>
    /// v2.0.1.0で追加。<br />
    /// </remarks>
    /// <param name="attribute">付与するアトリビュート。</param>
    /// <param name="duration">効果時間。</param>
    /// <param name="ratio">係数。</param>
    /// <param name="canPassMeeting">会議を超えて効果が持続する場合、<c>true</c>。</param>
    /// <param name="priority">優先度。</param>
    /// <param name="duplicateTag">重複チェック用タグ。</param>
    public void GainAttribute(IPlayerAttribute attribute, float duration, float ratio, bool canPassMeeting, int priority, string? duplicateTag = null);
    /// <summary>
    /// サイズアトリビュートを付与します。
    /// </summary>
    /// <remarks>
    /// v2.0.1.0で追加。<br />
    /// </remarks>
    /// <param name="size">プレイヤーの大きさ。</param>
    /// <param name="duration">効果時間。</param>
    /// <param name="canPassMeeting">会議を超えて効果が持続する場合、<c>true</c>。</param>
    /// <param name="priority">優先度。</param>
    /// <param name="duplicateTag">重複チェック用タグ。</param>
    public void GainSizeAttribute(Compat.Vector2 size, float duration, bool canPassMeeting, int priority, string? duplicateTag = null);

    /// <summary>
    /// 加減速アトリビュートを付与します。
    /// </summary>
    /// <remarks>
    /// v2.0.1.0でGainAttributeから名前変更。<br />
    /// </remarks>
    /// <param name="speedRate">加減速の倍率。</param>
    /// <param name="duration">効果時間。</param>
    /// <param name="canPassMeeting">会議を超えて効果が持続するかどうか。</param>
    /// <param name="priority">優先度。</param>
    /// <param name="duplicateTag">重複チェック用タグ。</param>
    public void GainSpeedAttribute(float speedRate, float duration, bool canPassMeeting, int priority, string? duplicateTag = null);
    
    /// <summary>
    /// アトリビュートを獲得しているかどうか調べます。
    /// </summary>
    /// <param name="attribute">調べる対象のアトリビュート。</param>
    /// <returns></returns>
    public bool HasAttribute(IPlayerAttribute attribute);

    /// <summary>
    /// 現在有効化されているアトリビュートを列挙します。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<(IPlayerAttribute attribute, float percentage)> GetAttributes();




    
    /// <summary>
    /// 現在割り当てられている役職です。
    /// </summary>
    public RuntimeRole Role { get; }
    /// <summary>
    /// 現在割り当てられている幽霊役職です。
    /// </summary>
    public RuntimeGhostRole? GhostRole { get; }
    /// <summary>
    /// 現在割り当てられているモディファイアです。
    /// </summary>
    public IEnumerable<RuntimeModifier> Modifiers { get; }
    /// <summary>
    /// 現在割り当てられている役職実体を全て返します。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<RuntimeAssignable> AllAssigned();
    /// <summary>
    /// 指定したモディファイアを取得します。
    /// </summary>
    /// <typeparam name="Modifier">モディファイアの型</typeparam>
    /// <param name="modifier">モディファイアが割り当てられている場合、割り当てられているモディファイア。</param>
    /// <returns>モディファイアが割り当てられている場合、true</returns>
    public bool TryGetModifier<Modifier>([MaybeNullWhen(false)] out Modifier modifier) where Modifier : class, RuntimeModifier;
    /// <summary>
    /// 幽霊役職を割り当て済みの場合、trueを返します。
    /// </summary>
    public bool AttemptedGhostAssignment { get; internal set; }
    /// <summary>
    /// 全ての能力を返します。
    /// </summary>
    public IEnumerable<IPlayerAbility> AllAbilities => AllAssigned().Select(a => a.MyAbilities).Smooth()!;
    /// <summary>
    /// 能力を取得します。
    /// </summary>
    /// <typeparam name="Ability"></typeparam>
    /// <param name="ability"></param>
    /// <returns></returns>
    public bool TryGetAbility<Ability>([MaybeNullWhen(false)]out Ability ability) where Ability : class, IPlayerAbility
    {
        bool result = AllAbilities.Find(ability => ability is Ability, out var pAbility);
        if (pAbility == null)
            ability = null;
        else
            ability = pAbility as Ability;
        return result;
    }


    /// <summary>
    /// インポスター陣営の場合、trueを返します。
    /// </summary>
    public bool IsImpostor => Role.Role.Category is RoleCategory.ImpostorRole;
    /// <summary>
    /// クルーメイト陣営の場合、trueを返します。マッドメイトであってもtrueを返します。
    /// </summary>
    public bool IsCrewmate => Role.Role.Category is RoleCategory.CrewmateRole;
    /// <summary>
    /// マッドメイトの場合、trueを返します。
    /// </summary>
    public bool IsMadmate => Role.Role.IsMadmate || Modifiers.Any(m => m.IsMadmate);
    /// <summary>
    /// クルー陣営かつマッドメイトでないクルーメイトであればtrueを返します。
    /// <c>IsCrewmate &amp;&amp; !IsMadmate</c>と等価です。
    /// </summary>
    public bool IsTrueCrewmate => IsCrewmate && !IsMadmate;


    /// <summary>
    /// プレイヤーの見た目を取得します。
    /// </summary>
    /// <param name="maxPriority">見た目の優先度の最大値</param>
    /// <returns></returns>
    public OutfitDefinition GetOutfit(int maxPriority);

    /// <summary>
    /// プレイヤーの現在の見た目を取得します。
    /// </summary>
    public OutfitDefinition CurrentOutfit { get; }

    /// <summary>
    /// プレイヤーの本来の見た目を取得します。
    /// </summary>
    public OutfitDefinition DefaultOutfit { get; }

    Virial.Game.OutfitDefinition IArchivedPlayer.DefaultOutfit => DefaultOutfit;


    /// <summary>
    /// プレイヤーのタスク進捗を取得します。
    /// </summary>
    public PlayerTasks Tasks => GetModule<PlayerTasks>()!;

    /// <summary>
    /// キルボタンを表示すべきか否かを取得します。
    /// </summary>
    public bool ShowKillButton => (Role?.HasVanillaKillButton ?? false) && AllowToShowKillButtonByAbilities;

    /// <summary>
    /// 能力がキルボタンの表示を許可しているかを返します。
    /// </summary>
    public bool AllowToShowKillButtonByAbilities => AllAssigned().All(assigned => assigned.MyAbilities.All(ability => !(ability?.HideKillButton ?? false)));

    /// <summary>
    /// 視界が壁を無視する場合、trueを返します。
    /// </summary>
    [Obsolete(AttributeConstants.ObsoleteText)]
    public bool EyesightIgnoreWalls => (Role?.EyesightIgnoreWalls ?? false) || AllAbilities.Any(a => a.EyesightIgnoreWalls);

    /// <summary>
    /// 自身が操作するプレイヤーを取得します。
    /// </summary>
    public static Player? LocalPlayer => NebulaAPI.instance.CurrentGame?.LocalPlayer;
    /// <summary>
    /// 全プレイヤーを取得します。
    /// </summary>
    public static IEnumerable<Player> AllPlayers => NebulaAPI.instance.CurrentGame?.GetAllPlayers() ?? [];
    /// <summary>
    /// 全プレイヤーをPlayerIdの順で取得します。
    /// </summary>
    public static IReadOnlyList<Player> AllOrderedPlayers => NebulaAPI.instance.CurrentGame?.GetAllOrderedPlayers() ?? [];
    /// <summary>
    /// プレイヤーを取得します。
    /// </summary>
    /// <param name="playerId">プレイヤーID</param>
    /// <returns></returns>
    public static Player? GetPlayer(byte playerId) => NebulaAPI.instance.CurrentGame?.GetPlayer(playerId);
}
