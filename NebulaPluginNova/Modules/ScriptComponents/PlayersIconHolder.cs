using Nebula.Behavior;
using Rewired.Utils.Classes.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;

internal class PlayerIconInfo
{
    public GamePlayer Player;
    internal PoolablePlayer Icon;

    public PlayerIconInfo(GamePlayer player, Transform parent)
    {
        Player = player;
        Icon = AmongUsUtil.GetPlayerIcon(player.Unbox().DefaultOutfit.Outfit.outfit, parent, Vector3.zero, Vector3.one * 0.31f);
        Icon.ToggleName(false);
    }

    public void SetText(string? text, float size = 4f)
    {
        if (text == null)
            Icon.ToggleName(false);
        else
        {
            Icon.ToggleName(true);
            Icon.SetName("", Vector3.one * size, Color.white, -1f);
            Icon.cosmetics.nameText.text = text;
        }
    }

    public void SetAlpha(bool semitransparent)
    {
        Icon.SetAlpha(semitransparent ? 0.35f : 1f);
    }
}

internal class PlayersIconHolder : FlexibleLifespan, IGameOperator
{
    HudContent myContent;
    GameObject adjuster;
    List<PlayerIconInfo> icons = new();
    public float XInterval = 0.29f;
    public PlayersIconHolder()
    {
        myContent = HudContent.InstantiateContent("PlayerIcons", true, true, false, true);
        adjuster = UnityHelper.CreateObject("Adjuster", myContent.transform, Vector3.zero);
    }

    private void UpdateIcons()
    {
        for (int i = 0; i < icons.Count; i++) icons[i].Icon.transform.localPosition = new(i * XInterval - 0.3f, -0.1f, -i * 0.01f);
    }

    public void Remove(PlayerIconInfo icon)
    {
        if (icons.Remove(icon)) GameObject.Destroy(icon.Icon.gameObject);
        UpdateIcons();
    }

    public PlayerIconInfo AddPlayer(GamePlayer player) {
        PlayerIconInfo info = new(player, adjuster.transform);
        icons.Add(info);
        UpdateIcons();
        return info;
    }

    public IEnumerable<PlayerIconInfo> AllIcons => icons;

    void IGameOperator.OnReleased()
    {
        if(myContent) GameObject.Destroy(myContent.gameObject);
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        if (MeetingHud.Instance)
        {
            adjuster.transform.localScale = new(0.65f, 0.65f, 1f);
            adjuster.transform.localPosition = new(-0.45f, -0.37f, 0f);
        }
        else
        {
            adjuster.transform.localScale = Vector3.one;
            adjuster.transform.localPosition = Vector3.zero;
        }
    }
}
