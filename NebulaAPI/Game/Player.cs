using System.Diagnostics.CodeAnalysis;
using Virial.Assignable;
using Virial.Command;
using Virial.DI;
using Virial.Text;

namespace Virial.Game;

public enum KillResult
{
    Kill,
    Guard,
    ObviousGuard
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
public class OutfitCandidate
{
    public string Tag { get; private set; }
    public int Priority { get; private set; }
    public bool SelfAware { get; private set; }
    public OutfitTag[] OutfitTags { get; private set; }
    internal NetworkedPlayerInfo.PlayerOutfit outfit { get; private set; }

    internal OutfitCandidate(string tag, int priority, bool selfAware, NetworkedPlayerInfo.PlayerOutfit outfit, OutfitTag[] outfitTags)
    {
        this.Tag = tag;
        this.Priority = priority;
        this.SelfAware = selfAware;
        this.outfit = outfit;
        this.OutfitTags = outfitTags;
    }

    internal OutfitCandidate(string tag, int priority, bool selfAware, Outfit outfit)
    {
        Tag = tag;
        Priority = priority;
        SelfAware = selfAware;
        OutfitTags = outfit.tags;
        this.outfit = outfit.outfit;
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

public interface Player : IModuleContainer, ICommandExecutor
{
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

    public PlayerDiving? CurrentDiving { get; }
    /// <summary>
    /// ダイブしているとき、Trueを返します。
    /// </summary>
    public bool IsDived => CurrentDiving != null;

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
    /// プレイヤーの座標を返します。足元よりは上寄りです。
    /// </summary>
    public Compat.Vector2 Position { get; }

    /// <summary>
    /// プレイヤーの現在の状態を表すタグです。
    /// </summary>
    public CommunicableTextTag PlayerState { get; }




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




    // MurderAPI

    /// <summary>
    /// プレイヤーをキルします。会議中の場合は<paramref name="leftDeadBody"/>の値は無視され、強制的に死体を残さないキルを起こします。
    /// </summary>
    /// <param name="player"></param>
    /// <param name="playerState"></param>
    /// <param name="eventDetail"></param>
    /// <returns></returns>
    public KillResult MurderPlayer(Player player, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams);
    public KillResult Suicide(CommunicableTextTag playerState, CommunicableTextTag? eventDetail,KillParameter killParams);
    public void Revive(Player? healer, Virial.Compat.Vector2 position, bool eraseDeadBody, bool recordEvent = true);
    public Player? MyKiller { get; }




    // AttributeAPI

    /// <summary>
    /// アトリビュートを付与します。
    /// </summary>
    /// <param name="attribute">付与するアトリビュート</param>
    /// <param name="duration">効果時間</param>
    /// <param name="canPassMeeting">会議を超えて効果が持続するかどうか</param>
    /// <param name="priority">優先度</param>
    /// <param name="duplicateTag">重複チェック用タグ</param>
    public void GainAttribute(IPlayerAttribute attribute, float duration, bool canPassMeeting, int priority, string? duplicateTag = null);
    
    /// <summary>
    /// 加減速アトリビュートを付与します。
    /// </summary>
    /// <param name="speedRate">加減速の倍率</param>
    /// <param name="duration">効果時間</param>
    /// <param name="canPassMeeting">会議を超えて高価が持続するかどうか</param>
    /// <param name="priority">優先度</param>
    /// <param name="duplicateTag">重複チェック用タグ</param>
    public void GainAttribute(float speedRate, float duration, bool canPassMeeting, int priority, string? duplicateTag = null);
    
    /// <summary>
    /// アトリビュートを獲得しているかどうか調べます。
    /// </summary>
    /// <param name="attribute">調べる対象のアトリビュート</param>
    /// <returns></returns>
    public bool HasAttribute(IPlayerAttribute attribute);

    /// <summary>
    /// 現在有効化されているアトリビュートを列挙します。
    /// </summary>
    /// <returns></returns>
    public IEnumerable<(IPlayerAttribute attribute, float percentage)> GetAttributes();




    // AssignableAPI

    public RuntimeRole Role { get; }
    public IEnumerable<RuntimeModifier> Modifiers { get; }
    public IEnumerable<RuntimeAssignable> AllAssigned();
    public bool TryGetModifier<Modifier>([MaybeNullWhen(false)] out Modifier modifier) where Modifier : class, RuntimeModifier;
    public bool AttemptedGhostAssignment { get; internal set; }



    // PlayerRoleCategoryAPI

    public bool IsImpostor => Role.Role.Category is RoleCategory.ImpostorRole;
    public bool IsCrewmate => Role.Role.Category is RoleCategory.CrewmateRole;




    // OutfitAPI

    /// <summary>
    /// プレイヤーの見た目を取得します。
    /// </summary>
    /// <param name="maxPriority">優先度の最大値</param>
    /// <returns></returns>
    public Outfit GetOutfit(int maxPriority);

    /// <summary>
    /// プレイヤーの現在の見た目を取得します。
    /// </summary>
    public Outfit CurrentOutfit { get; }

    /// <summary>
    /// プレイヤーの本来の見た目を取得します。
    /// </summary>
    public Outfit DefaultOutfit { get; }



    // TasksAPI

    /// <summary>
    /// プレイヤーのタスク進捗を取得します。
    /// </summary>
    public PlayerTasks Tasks => GetModule<PlayerTasks>()!;
}
