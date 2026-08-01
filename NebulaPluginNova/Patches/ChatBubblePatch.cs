namespace Nebula.Patches;

[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
internal class ChatBubbleSetNamePatch
{
    static void Prefix([HarmonyArgument(2)] ref bool voted)
    {
        if (!GeneralConfigurations.ShowVoteStateOption) voted = false;
    }
}
