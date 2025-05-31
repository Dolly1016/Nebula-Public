namespace Virial.Assignable;

/// <summary>
/// 陣営の開示バリエーションを表します。
/// </summary>
public enum TeamRevealType
{
    /// <summary>
    /// 自分自身のみ表示されます。主に第三陣営で使用します。
    /// </summary>
    OnlyMe,
    /// <summary>
    /// ゲーム中の全プレイヤーが表示されます。クルーメイト陣営で使用します。
    /// </summary>
    Everyone,
    /// <summary>
    /// チームのメンバー全員が表示されます。
    /// </summary>
    Teams,
}

public interface RoleTeam
{
    public string TranslationKey { get; }
    internal UnityEngine.Color UnityColor { get; }
    public Virial.Color Color { get; }
    public int Id { get; }
    public TeamRevealType RevealType { get; }

    /// <summary>
    /// 陣営ベースのキルクールダウンを取得します。
    /// </summary>
    public float KillCooldown { get; }
}

public static class NebulaTeams
{
    public static RoleTeam CrewmateTeam { get; internal set; } = null!;
    public static RoleTeam ImpostorTeam { get; internal set; } = null!;
    public static RoleTeam JackalTeam { get; internal set; } = null!;
    public static RoleTeam JesterTeam { get; internal set; } = null!;
    public static RoleTeam VultureTeam { get; internal set; } = null!;
    public static RoleTeam ArsonistTeam { get; internal set; } = null!;
    public static RoleTeam PaparazzoTeam { get; internal set; } = null!;
    public static RoleTeam ChainShifterTeam { get; internal set; } = null!;
}
