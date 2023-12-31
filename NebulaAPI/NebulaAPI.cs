﻿using System.Runtime.CompilerServices;
using UnityEngine;
using Virial.Assets;
using Virial.Assignable;
using Virial.Components;
using Virial.Game;
using Virial.Text;

[assembly: InternalsVisibleTo("Nebula")]

namespace Virial;

internal interface INebula
{
    void RegisterRole(AbstractRoleDef roleDef);
    RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType);
    void RegisterEventHandler(ILifespan lifespan, object handler);
    CommunicableTextTag RegisterCommunicableText(string translationKey);
    Virial.Components.AbilityButton CreateAbilityButton();
    Virial.Components.GameTimer CreateTimer(float max, float min);
    string APIVersion { get; }
    INameSpace NebulaAsset { get; }
    INameSpace InnerslothAsset { get; }
    INameSpace GetAddon(string addonId);
    IEnumerable<Player> GetPlayers();
    
}

public static class NebulaAPI
{
    static internal INebula instance = null!;

    static public void RegisterRole(AbstractRoleDef roleDef) => instance.RegisterRole(roleDef);
    static public RoleTeam CreateTeam(string translationKey, Color color, TeamRevealType revealType) => instance.CreateTeam(translationKey, color, revealType);

    public static CommunicableTextTag RegisterCommunicableText(string translationKey) => instance.RegisterCommunicableText(translationKey);

    public static string APIVersion => instance.APIVersion;

    static public Virial.Components.AbilityButton CreateAbilityButton() => instance.CreateAbilityButton();
    static public Virial.Components.GameTimer CreateTimer(float max, float min = 0f) => instance.CreateTimer(max, min);

    static public INameSpace NebulaAsset => instance.NebulaAsset;
    static public INameSpace InnerslothAsset => instance.InnerslothAsset;
    static public INameSpace GetAddon(string addonId) => instance.GetAddon(addonId);
    static public void RegisterEventHandler(ILifespan lifespan, object handler) => instance.RegisterEventHandler(lifespan, handler); 
    static public IEnumerable<Player> GetPlayers() => instance.GetPlayers();
}