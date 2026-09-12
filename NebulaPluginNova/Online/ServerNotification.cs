namespace Nebula.Online;

internal static class ServerNotification
{
    public static void Receive(Hazel.MessageReader reader)
    {
        var message = reader.ReadString();
        var chatOnly = reader.ReadBoolean();
        if (string.IsNullOrEmpty(message)) return;

        NebulaGameManager.Instance?.PushSystemMessage(message, chatOnly);
    }
}
