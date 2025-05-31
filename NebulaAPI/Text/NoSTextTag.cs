namespace Virial.Text;

/// <summary>
/// プレイヤーの状態を表すために使用されるタグです。
/// </summary>
public static class PlayerStates
{
    /// <summary>
    /// 生存している状態を表します。
    /// </summary>
    public static CommunicableTextTag Alive { get; internal set; } = null!;
    /// <summary>
    /// 特筆すべき死因によらず死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Dead { get; internal set; } = null!;
    /// <summary>
    /// 追放された状態を表します。
    /// </summary>
    public static CommunicableTextTag Exiled { get; internal set; } = null!;
    /// <summary>
    /// Sheriffが誤射によって死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Misfired { get; internal set; } = null!;
    /// <summary>
    /// Sniperによって撃ち抜かれて死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Sniped { get; internal set; } = null!;
    /// <summary>
    /// Raiderの斧によって死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Beaten { get; internal set; } = null!;
    /// <summary>
    /// Guesserに推察されて死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Guessed { get; internal set; } = null!;
    /// <summary>
    /// Guesserの誤った推察によって自滅した状態を表します。
    /// </summary>
    public static CommunicableTextTag Misguessed { get; internal set; } = null!;
    /// <summary>
    /// Provocateurの巻き込みによって死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Embroiled { get; internal set; } = null!;
    /// <summary>
    /// Loversが後追い自殺した状態を表します。
    /// </summary>
    public static CommunicableTextTag Suicide { get; internal set; } = null!;
    /// <summary>
    /// Evil Trapperのキルトラップに引っかかって死亡した状態を表します。
    /// </summary>
    public static CommunicableTextTag Trapped { get; internal set; } = null!;
    /// <summary>
    /// 何らかの理由で復活した状態を表します。
    /// </summary>
    public static CommunicableTextTag Revived { get; internal set; } = null!;
    /// <summary>
    /// Buskerが偽装死した状態を表します。今後Buskerは復活する可能性があります。
    /// </summary>
    public static CommunicableTextTag Pseudocide { get; internal set; } = null!;
    public static CommunicableTextTag Gassed { get; internal set; } = null!;
    public static CommunicableTextTag Bubbled { get; internal set; } = null!;
    public static CommunicableTextTag Meteor { get; internal set; } = null!;
    public static CommunicableTextTag Balloon { get; internal set; } = null!;
    public static CommunicableTextTag Lost { get; internal set; } = null;
}

/// <summary>
/// 記録されたイベントの概要を説明するタグです。
/// </summary>
public static class EventDetails
{
    /// <summary>
    /// キルの発生を表します。
    /// </summary>
    public static CommunicableTextTag Kill { get; internal set; } = null!;
    /// <summary>
    /// 追放の発生を表します。
    /// </summary>
    public static CommunicableTextTag Exiled { get; internal set; } = null!;
    /// <summary>
    /// Sheriffの誤射を表します。
    /// </summary>
    public static CommunicableTextTag Misfire { get; internal set; } = null!;
    /// <summary>
    /// ゲームの開始を表します。
    /// </summary>
    public static CommunicableTextTag GameStart { get; internal set; } = null!;
    /// <summary>
    /// ゲームの終了を表します。
    /// </summary>
    public static CommunicableTextTag GameEnd { get; internal set; } = null!;
    /// <summary>
    /// 会議の終了を表します。
    /// </summary>
    public static CommunicableTextTag MeetingEnd { get; internal set; } = null!;
    /// <summary>
    /// Baitレポートを除く通報による会議の開始を表します。
    /// </summary>
    public static CommunicableTextTag Report { get; internal set; } = null!;
    /// <summary>
    /// Baitレポートによる会議の開始を表します。
    /// </summary>
    public static CommunicableTextTag BaitReport { get; internal set; } = null!;
    /// <summary>
    /// 緊急ボタンによる会議の開始を表します。
    /// </summary>
    public static CommunicableTextTag EmergencyButton { get; internal set; } = null!;
    /// <summary>
    /// プレイヤーの切断を表します。
    /// </summary>
    public static CommunicableTextTag Disconnect { get; internal set; } = null!;
    /// <summary>
    /// プレイヤーの復活を表します。
    /// </summary>
    public static CommunicableTextTag Revive { get; internal set; } = null!;
    /// <summary>
    /// Vultureによる死体消去を表します。
    /// </summary>
    public static CommunicableTextTag Eat { get; internal set; } = null!;
    /// <summary>
    /// Cleanerによる死体消去を表します。
    /// </summary>
    public static CommunicableTextTag Clean { get; internal set; } = null!;
    /// <summary>
    /// Guesserによる推察の失敗やRaiderによるキルを伴わない斧の投擲を表します。
    /// </summary>
    public static CommunicableTextTag Missed { get; internal set; } = null!;
    /// <summary>
    /// Guesserによる成功した推察の発生を表します。
    /// </summary>
    public static CommunicableTextTag Guess { get; internal set; } = null!;
    /// <summary>
    /// Provocateurによる巻き込みの発生を表します。
    /// </summary>
    public static CommunicableTextTag Embroil { get; internal set; } = null!;
    /// <summary>
    /// Evil Trapperのキルトラップの発動を表します。
    /// </summary>
    public static CommunicableTextTag Trap { get; internal set; } = null!;
    /// <summary>
    /// Buskerによる復活の失敗を表します。
    /// </summary>
    public static CommunicableTextTag Accident { get; internal set; } = null!;
    /// <summary>
    /// Thuriferの遅延キルを表します。
    /// </summary>
    public static CommunicableTextTag Gassed { get; internal set; } = null!;
    public static CommunicableTextTag Bubbled { get; internal set; } = null!;
    public static CommunicableTextTag Meteor { get; internal set; } = null!;
    public static CommunicableTextTag Balloon { get; internal set; } = null!;
}