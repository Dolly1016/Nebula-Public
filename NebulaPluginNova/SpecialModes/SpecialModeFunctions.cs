using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;

namespace Nebula.SpecialModes;

internal static class SpecialModeFunctions
{
    static public void IntroSetUp()
    {
        AmongUsClient.Instance.SendClientReady();
        HudManager.Instance.OnGameStart();

        //フレンドボタン非表示
        if (FriendsListManager.InstanceExists && FriendsListManager.Instance.FriendsListButton) FriendsListManager.Instance.FriendsListButton.showInScene = false;

        //ボタン類非表示
        var buttonHolder = HudManager.Instance.AbilityButton.transform.parent.gameObject;
        buttonHolder.transform.localPosition = new(0f, 0f, 20f);
        buttonHolder.SetActive(false);

        NebulaAPI.CurrentGame?.GetModule<Synchronizer>()?.SendSync(SynchronizeTag.PreStartGame);
    }

    static private void SetUpPlayers()
    {
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            p.transform.position = new(0f, -10000f, 0f);
            var player = NebulaGameManager.Instance?.RegisterPlayer(p);
            player?.Unbox().RpcInvokerSetRole(Roles.SpecialMode.AeroguesserPlayer.MyRole, null).InvokeLocal();
        }
    }

    static public void InRpcSetUp()
    {
        SpecialModeFunctions.SetUpPlayers();
        NebulaGameManager.Instance?.CheckGameState(false);
        NebulaGameManager.Instance?.SetGameModeModule(); //ここでPlaySenarioが開始する
    }
}
