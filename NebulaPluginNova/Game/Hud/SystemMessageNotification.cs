using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Game.Hud;

internal class SystemMessageNotification
{
    ChatNotification chatNotification;
    GameObject chatNotificationObj;
    AspectPosition aspectPosition;
    ChatController auChat;
    public SystemMessageNotification(HudManager hud)
    {
        auChat = hud.Chat;
        chatNotification = GameObject.Instantiate(auChat.chatNotification, hud.transform);
        aspectPosition = chatNotification.GetComponent<AspectPosition>();
        aspectPosition.DistanceFromEdge = new(-1.57f, 2.51f, -500f);

        chatNotification.player.gameObject.SetActive(false);
        chatNotification.playerColorText.gameObject.SetActive(false);

        var nameText = chatNotification.playerNameText;
        nameText.text = Language.Translate("notification.server.systemRole").Bold();
        nameText.color = new(0.7f, 0.8f, 1f, 1f);
        nameText.transform.SetLocalY(0f);

        var chatText = chatNotification.chatText;
        chatText.overflowMode = TMPro.TextOverflowModes.Overflow;
        chatText.fontSizeMin = 1f;
        chatText.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;

        chatNotificationObj = chatNotification.gameObject;
        chatNotificationObj.SetActive(false);
    }

    public void ShowMessage(string message, bool chatOnly, float duration = 10f)
    {
        if (!auChat.IsOpenOrOpening && auChat.notificationRoutine == null)
        {
            if (chatOnly)
            {
                auChat.notificationRoutine = auChat.StartCoroutine(auChat.BounceDot());
            }
            else
            {
                ActivateNotification(message, duration);
            }
        }

        SendMessageToAUChat(message);
    }

    private void ActivateNotification(string message, float duration)
    {
        bool inGame = ShipStatus.Instance.AsBoolFast();

        if (inGame)
        {
            aspectPosition.DistanceFromEdge = new(-1.57f, 0.49f, -500f);
        }
        else
        {
            aspectPosition.DistanceFromEdge = new(-0.9f, 1.2f, -500f);
        }

        chatNotification.timeOnScreen = duration;
        chatNotification.gameObject.SetActive(true);
        chatNotification.chatText.text = message;
    }

    private void SendMessageToAUChat(string message)
    {
        if (!auChat.AsBoolFast()) return;

        SoundManager.Instance.PlaySound(auChat.warningSound, false, 1f, null);

        ChatBubble pooledBubble = auChat.GetPooledBubble();

        pooledBubble.transform.SetParent(auChat.scroller.Inner);
        pooledBubble.transform.localScale = Vector3.one;
        pooledBubble.SetLeft();
        //SetTextここから
        pooledBubble.Player.gameObject.SetActive(false);
        pooledBubble.NameText.gameObject.SetActive(false);
        pooledBubble.ColorBlindName.gameObject.SetActive(false);
        pooledBubble.TextArea.color = Color.black;
        pooledBubble.SetText(message);
        pooledBubble.TextArea.rectTransform.pivot = new Vector2(0f, 0f);
        pooledBubble.TextArea.transform.localPosition = new Vector3(-0.25f, -0.15f, 0f);
        pooledBubble.TextArea.horizontalAlignment = TMPro.HorizontalAlignmentOptions.Left;
        pooledBubble.Background.color = new(0.6f, 0.8f, 1f, 1f);
        //SetTextここまで
        pooledBubble.AlignChildren();
        auChat.AlignAllBubbles();

    }
}
