using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Dev;

/*
static internal class DiscordVC
{
    static DataSaver TestSaver = new("DiscordVCTest");
    static StringDataEntry ClientId = new("ClientId", TestSaver, "-", shouldWrite: false);
    static StringDataEntry ClientSecret = new("ClientSecret", TestSaver, "-", shouldWrite: false);
    static StringDataEntry TargetId = new("TargetId", TestSaver, "-", shouldWrite: false);
    static StringDataEntry AccessToken = new("AccessToken", TestSaver, "-", shouldWrite: false);

    static public void Start()
    {
        _ = Main();
    }

    private const int HeaderSize = 4;
    private static async Task Main()
    {

        try
        {
            var process = System.Diagnostics.Process.GetCurrentProcess();
            string id = process.Id.ToString();

            ProcessStartInfo processStartInfo = new ProcessStartInfo();
            processStartInfo.FileName = "stargazer.exe";
            processStartInfo.Arguments = id + " " + AccessToken.Value + " " + ClientId.Value + " " + ClientSecret.Value;
            //processStartInfo.CreateNoWindow = true;
            //processStartInfo.UseShellExecute = false;
            processStartInfo.UseShellExecute = true;
            processStartInfo.CreateNoWindow = false;
            Process.Start(processStartInfo);
        }
        catch
        {
        }
        


        //クライアント側のWebSocketを定義
        ClientWebSocket ws = new ClientWebSocket();

        //接続先エンドポイントを指定
        var uri = new Uri("ws://localhost:22500");

        //サーバに対し、接続を開始
        await ws.ConnectAsync(uri, CancellationToken.None);
        var buffer = new byte[1024];

        await ListenAsync();

        async Task ListenAsync()
        {
            //所得情報確保用の配列を準備
            var segment = new ArraySegment<byte>(buffer);

            //情報取得待ちループ
            while (true)
            {
                try
                {
                    //サーバからのレスポンス情報を取得
                    var result = await ws.ReceiveAsync(segment, CancellationToken.None);

                    //エンドポイントCloseの場合、処理を中断
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK",
                          CancellationToken.None);
                        return;
                    }

                    if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        // バイナリメッセージの場合
                        if (result.Count < HeaderSize)
                        {
                            continue;
                        }

                        // 1. ヘッダ（最初の4バイト）を読み出す
                        var headerBytes = new byte[HeaderSize];
                        Buffer.BlockCopy(buffer, 0, headerBytes, 0, HeaderSize);

                        // ビッグエンディアンのバイト列をint32に変換
                        if (BitConverter.IsLittleEndian) Array.Reverse(headerBytes);
                        int messageType = BitConverter.ToInt32(headerBytes, 0);

                        if (messageType == 1001)
                        {
                            var payloadSize = result.Count - HeaderSize;
                            var payloadBytes = new byte[payloadSize];
                            Buffer.BlockCopy(buffer, HeaderSize, payloadBytes, 0, payloadSize);
                            string messageBody = Encoding.UTF8.GetString(payloadBytes);

                            NebulaManager.Instance.ScheduleDelayAction(() =>
                            {
                                AccessToken.Value = messageBody;
                            });
                            break;
                        }

                    }
                }
                catch (WebSocketException)
                {
                    //通信が終了したため、このループを脱出する。
                    return;
                }
            }

            LogUtils.WriteToConsole("プロセス起動まで完了");
            var targetBytes = Encoding.UTF8.GetBytes(TargetId.Value);
            if (GamePlayer.AllPlayers.Find(p => !p.AmOwner, out var player))
            {
                var myPlayer = GamePlayer.LocalPlayer;

                while (true)
                {
                    var distance = myPlayer.Position.Distance(player.Position);
                    var xDiff = myPlayer.Position.x - player.Position.x;
                    var xDiffNeg = -xDiff;

                    int volume = 200;
                    if(distance > 2.5f)
                    {
                        volume = 200 - (int)((distance - 2.5f) * 50);
                        if (volume < 0) volume = 0;
                    }

                    int l = 100;
                    int r = 100;
                    if (xDiff > 0.9f)
                    {
                        r -= (int)((xDiff - 0.9f) * 45);
                        if (r < 0) r = 0;
                    }
                    if (xDiffNeg > 0.9f)
                    {
                        l -= (int)((xDiffNeg - 0.9f) * 45);
                        if (l < 0) l = 0;
                    }

                    byte[] headerBytes = BitConverter.GetBytes((int)2000);
                    if (BitConverter.IsLittleEndian) Array.Reverse(headerBytes);
                    byte[] volumeBytes = BitConverter.GetBytes(volume);
                    if (BitConverter.IsLittleEndian) Array.Reverse(volumeBytes);
                    byte[] lBytes = BitConverter.GetBytes(l);
                    if (BitConverter.IsLittleEndian) Array.Reverse(lBytes);
                    byte[] rBytes = BitConverter.GetBytes(r);
                    if (BitConverter.IsLittleEndian) Array.Reverse(rBytes);


                    byte[] combinedBytes = headerBytes
                        .Concat(volumeBytes)
                        .Concat(lBytes)
                        .Concat(rBytes)
                        .Concat(targetBytes)
                        .ToArray();


                    await ws.SendAsync(
                        new ArraySegment<byte>(combinedBytes),
                        WebSocketMessageType.Binary, // バイナリデータとして送信
                        true,
                        CancellationToken.None
                    );

                    await Task.Delay(100);
                }
            }
        }
    }
}
*/