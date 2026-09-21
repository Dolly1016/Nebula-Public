using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Virial;

namespace Nebula.Http;

/// <summary>
/// ゲーム記録をブラウザから閲覧するための、ごく小さなローカル HTTP サーバー。
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HttpListener"/> は使わない。Windows では http.sys の URL 予約が要るため、
/// 管理者権限なしでは <c>http://localhost:port/</c> ですら Access denied になる。
/// 代わりに <see cref="TcpListener"/> の上に必要最小限の HTTP/1.1 を自前で載せている。
/// </para>
/// <para>
/// 待ち受けはループバックのみ。外部からは繋がらず、ファイアウォールの確認も出ない。
/// 触るのはディスク上の JSON と埋め込みリソースだけで Unity API を呼ばないため、
/// メインスレッドへ戻す仕掛けは要らない。
/// </para>
/// </remarks>
internal static class NebulaHttpServer
{
    /// <summary>最初に試すポート。</summary>
    public const int PreferredPort = 27100;

    /// <summary>塞がっていたときに隣を試す回数。</summary>
    private const int PortRetryCount = 10;

    /// <summary>接続してきた相手が黙り込んだときに諦めるまでの時間。</summary>
    private const int SocketTimeoutMs = 10 * 1000;

    private static readonly object Gate = new();

    private static readonly Virial.Logging.ILogger Logger =
        NebulaAPI.Logging.CombinedLogger(
            NebulaAPI.Logging.NebulaLogger("HttpServer"),
            NebulaAPI.Logging.BepInExLogger());

    private static TcpListener? listener = null;
    private static Thread? acceptThread = null;

    public static bool IsRunning { get; private set; } = false;

    /// <summary>待ち受け中のポート。停止中は 0。</summary>
    public static int Port { get; private set; } = 0;

    public static string Url => $"http://127.0.0.1:{Port}/";

    // ------------------------------------------------------------------ 起動・停止

    /// <summary>
    /// 動いていなければ起動し、動いていれば止める。
    /// </summary>
    /// <returns>この呼び出しのあと動いているなら true。</returns>
    public static bool Toggle()
    {
        if (IsRunning)
        {
            Stop();
            return false;
        }

        return Start();
    }

    public static bool Start()
    {
        lock (Gate)
        {
            if (IsRunning) return true;

            //役職アイコンのPNG化はUnityのメインスレッドでしかできない。
            //ここはKキーから呼ばれるのでメインスレッド。先に済ませておき、HTTP側は控えを返すだけにする。
            RoleIconContent.Prepare();
            MapContent.Prepare();

            for (int offset = 0; offset <= PortRetryCount; offset++)
            {
                var port = PreferredPort + offset;
                var candidate = new TcpListener(IPAddress.Loopback, port);

                try
                {
                    candidate.Start();
                }
                catch (SocketException)
                {
                    //塞がっているだけ。隣を試す。
                    continue;
                }

                listener = candidate;
                Port = port;
                IsRunning = true;

                acceptThread = new Thread(AcceptLoop)
                {
                    //取りこぼしてもプロセス終了を妨げないように。
                    IsBackground = true,
                    Name = "NebulaHttpServer",
                };
                acceptThread.Start();

                Logger.Message($"Started. {Url}");
                return true;
            }

            Logger.Error($"Failed to start. No free port in {PreferredPort}-{PreferredPort + PortRetryCount}.");
            return false;
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            if (!IsRunning) return;

            IsRunning = false;

            try
            {
                //待機中の AcceptTcpClient を SocketException で叩き起こす。
                listener?.Stop();
            }
            catch (Exception e)
            {
                Logger.Warning("Failed to stop the listener cleanly.\n" + e.Message);
            }

            listener = null;
            acceptThread = null;
            Port = 0;

            Logger.Message("Stopped.");
        }
    }

    // ------------------------------------------------------------------ 受付

    private static void AcceptLoop()
    {
        var self = listener;

        while (true)
        {
            TcpClient client;

            try
            {
                client = self!.AcceptTcpClient();
            }
            catch (Exception)
            {
                //Stop() による解体か、回復しようのない失敗。どちらにせよ抜ける。
                break;
            }

            if (!IsRunning)
            {
                client.Close();
                break;
            }

            //1 接続ぶんの処理は投げっぱなしにする。ブラウザは複数本まとめて張ってくる。
            Task.Run(() => Handle(client));
        }
    }

    private static void Handle(TcpClient client)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = SocketTimeoutMs;
                client.SendTimeout = SocketTimeoutMs;

                using var stream = client.GetStream();

                var exchange = HttpExchange.Read(stream);
                if (exchange == null) return;

                if (!IsLoopbackHost(exchange))
                {
                    //別名で引かれた場合は断る。DNS リバインディング対策。
                    exchange.RespondStatus(403);
                    return;
                }

                GameRecordApi.Handle(exchange);
            }
        }
        catch (Exception e)
        {
            Logger.Warning("Failed to handle a request.\n" + e.ToString());
        }
    }

    /// <summary>
    /// Host ヘッダがループバックを指しているかを確かめる。
    /// </summary>
    private static bool IsLoopbackHost(HttpExchange exchange)
    {
        if (!exchange.Headers.TryGetValue("Host", out var host)) return false;

        //ポートを落とす。IPv6 リテラルは [::1]:port の形で来る。
        if (host.StartsWith('['))
        {
            var close = host.IndexOf(']');
            if (close < 0) return false;
            host = host.Substring(1, close - 1);
        }
        else
        {
            var colon = host.IndexOf(':');
            if (colon >= 0) host = host.Substring(0, colon);
        }

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host == "127.0.0.1"
            || host == "::1";
    }
}
