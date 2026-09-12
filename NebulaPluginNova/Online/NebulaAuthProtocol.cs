namespace Nebula.Online;

internal static class NebulaAuthProtocol
{
    // 認証RPC。
    public const byte AuthCallId = 129;
    // 認証結果。
    public const byte ResultCallId = 130;
    // 掲載依頼。
    public const byte RoomCallId = 131;
    // Server Notification。
    public const byte MessageCallId = 132;
    // 
    public const int AllClients = -1;

    // 129RPC
    public static class Sub
    {
        // クライアント送信。認証結果の再送要求。
        public const byte Request = 0x01;
        // サーバー送信。
        public const byte Challenge = 0x02;
        // クライアント送信。
        public const byte Response = 0x03;
        // クライアント(ホスト)送信。
        public const byte SetPolicy = 0x04;
        // サーバー送信。
        public const byte Policy = 0x05;
    }

    // 131RPC
    public static class RoomSub
    {
        // クライアント(ホスト)送信。
        public const byte Publish = 0x01;
        // クライアント(ホスト)送信。
        public const byte Unpublish = 0x02;
        // サーバー送信。掲載結果通知。
        public const byte Status = 0x03;
    }
}

// 130RPC
internal enum AuthStatus : byte
{
    // 認証
    Verified = 0,
    // 不正なuid
    Unregistered = 1,
    // チャレンジ応答なし(NoS未導入疑い)
    NoResponse = 2,
    // 認証サーバー応答なし
    Unavailable = 3,
    // 検証中および不明なエラー
    Unknown = 255,
}
