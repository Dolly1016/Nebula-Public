﻿namespace Nebula.Extensions;


static public class LayerExpansion
{
    static int? defaultLayer = null;
    static int? shortObjectsLayer = null;
    static int? objectsLayer = null;
    static int? playersLayer = null;
    static int? ghostLayer = null;
    static int? uiLayer = null;
    static int? shipLayer = null;
    static int? shadowLayer = null;
    static int? drawShadowsLayer = null;

    static public int GetDefaultLayer()
    {
        if (defaultLayer == null) defaultLayer = LayerMask.NameToLayer("Default");
        return defaultLayer.Value;
    }

    static public int GetShortObjectsLayer()
    {
        if (shortObjectsLayer == null) shortObjectsLayer = LayerMask.NameToLayer("ShortObjects");
        return shortObjectsLayer.Value;
    }

    static public int GetObjectsLayer()
    {
        if (objectsLayer == null) objectsLayer = LayerMask.NameToLayer("Objects");
        return objectsLayer.Value;
    }

    static public int GetPlayersLayer()
    {
        if (playersLayer == null) playersLayer = LayerMask.NameToLayer("Players");
        return playersLayer.Value;
    }

    static public int GetGhostLayer()
    {
        if (ghostLayer == null) ghostLayer = LayerMask.NameToLayer("Ghost");
        return ghostLayer.Value;
    }

    static public int GetUILayer()
    {
        if (uiLayer == null) uiLayer = LayerMask.NameToLayer("UI");
        return uiLayer.Value;
    }

    static public int GetShipLayer()
    {
        if (shipLayer == null) shipLayer = LayerMask.NameToLayer("Ship");
        return shipLayer.Value;
    }

    static public int GetShadowLayer()
    {
        if (shadowLayer == null) shadowLayer = LayerMask.NameToLayer("Shadow");
        return shadowLayer.Value;
    }

    static public int GetDrawShadowsLayer()
    {
        if (drawShadowsLayer == null) drawShadowsLayer = LayerMask.NameToLayer("DrawShadows");
        return drawShadowsLayer.Value;
    }

    static public int GetShadowObjectsLayer()
    {
        return 30;
    }

    static public int GetArrowLayer()
    {
        return 29;
    }

    static public int GetRaiderColliderLayer()
    {
        return 28;
    }

    static public int GetVanillaShadowLightLayer()
    {
        return 27;
    }

    static public int GetLayerMask(params int[] layer)
    {
        int result = 0;
        foreach (var l in layer) result |= 1 << l;
        return result;
    }
}

