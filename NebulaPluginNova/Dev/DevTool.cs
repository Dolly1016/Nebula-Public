using Virial;
using Virial.Game;

namespace Nebula.Dev;

/// <summary>
/// デバッグ用ウィンドウのボタンから起動するデバッグツールの基底クラスです。
/// ゲーム中のみ起動でき、Close()を呼ぶかゲームが終了すると破棄されます。
/// </summary>
internal abstract class DevTool : IGameOperator, ILifespan
{
    private static readonly Dictionary<Type, DevTool> runningTools = [];

    private bool isClosed = false;
    private readonly Virial.Game.Game? boundGame = NebulaAPI.CurrentGame;

    public bool IsClosed => isClosed || boundGame == null || boundGame != NebulaAPI.CurrentGame;
    bool ILifespan.IsDeadObject => IsClosed;

    /// <summary>
    /// ツールを終了します。実際の後始末はOnClosedで行います。
    /// </summary>
    public void Close() => isClosed = true;

    void IGameOperator.OnReleased()
    {
        isClosed = true;
        try
        {
            OnClosed();
        }
        catch (Exception ex)
        {
            Nebula.Modules.Logging.NebulaLogger.Instance.Error(ex.ToString());
        }
    }

    /// <summary>
    /// ツールの終了時に呼び出されます。生成したオブジェクトの破棄などを行います。
    /// </summary>
    protected virtual void OnClosed() { }

    /// <summary>
    /// ゲーム中(ShipStatusが存在する状態)であればtrueを返します。
    /// </summary>
    static public bool InGame => NebulaAPI.CurrentGame != null && AmongUsLLImpl.ShipStatusInstance.AsBoolFast();

    /// <summary>
    /// 指定の種類のツールが起動中であればtrueを返します。
    /// </summary>
    static public bool IsRunning<T>() where T : DevTool => runningTools.TryGetValue(typeof(T), out var tool) && !tool.IsClosed;

    /// <summary>
    /// ツールを起動します。既に起動している場合は終了します。
    /// </summary>
    static public void Toggle<T>(Func<T> generator, string name) where T : DevTool
    {
        if (runningTools.TryGetValue(typeof(T), out var running) && !running.IsClosed)
        {
            running.Close();
            DebugScreen.Push($"{name} を終了しました。", 2f);
            return;
        }

        if (!InGame)
        {
            DebugScreen.Push($"{name} はゲーム中のみ起動できます。", 2f);
            return;
        }

        var tool = generator.Invoke();
        runningTools[typeof(T)] = tool;
        tool.RegisterSelf();
        DebugScreen.Push($"{name} を起動しました。", 2f);
    }

    /// <summary>
    /// デバッグ用ウィンドウにツールの起動ボタンを登録します。
    /// </summary>
    static public void RegisterButton<T>(string name, Func<T> generator) where T : DevTool
    {
        DebugTools.RegisterDebugTool(() => (IsRunning<T>() ? "■ " : "") + name, () => Toggle(generator, name));
    }
}
