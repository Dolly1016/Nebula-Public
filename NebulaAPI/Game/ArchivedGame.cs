using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;
using Virial.Text;

namespace Virial.Game;

public interface IArchivedPlayer
{
    Virial.Game.OutfitDefinition DefaultOutfit { get; }
    string PlayerName => DefaultOutfit.outfit.PlayerName;
    byte PlayerId { get; }
}

public record ArchivedColor(Virial.Color MainColor, Virial.Color ShadowColor, Virial.Color VisorColor);
public interface IArchivedGame
{
    internal IReadOnlyList<RoleHistory> RoleHistory { get; }
    IArchivedPlayer? GetPlayer(byte playerId);
    IEnumerable<IArchivedPlayer> GetAllPlayers();
    IArchivedEvent[] ArchivedEvents { get; }
    ArchivedColor GetColor(byte colorId);
    byte MapId { get; }
}

public interface IArchivedEventVariation
{
    int Id { get; }
    Media.Image? EventIcon { get; }
    Media.Image? InteractionIcon { get; }
    bool ShowPlayerPosition { get; }
    bool CanCombine { get; }
}
public interface IArchivedEvent
{
    IArchivedEventVariation EventVariation { get; }
    public float Time { get; }
    public byte? SourceId { get; }
    public int TargetIdMask { get; }
    public Tuple<byte, Virial.Compat.Vector2>[] Position { get; }
    public CommunicableTextTag? RelatedTag { get; }
}

internal record RoleHistory
{
    public float Time;
    public byte PlayerId;
    public bool IsModifier;
    public bool IsSet;
    public bool Dead;

    public RuntimeAssignable Assignable;

    public RoleHistory(float time, byte playerId, RuntimeModifier modifier, bool isSet, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = true;
        IsSet = isSet;
        Assignable = modifier;
        Dead = dead;
    }

    public RoleHistory(float time, byte playerId, RuntimeRole role, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = false;
        IsSet = true;
        Assignable = role;
        Dead = dead;
    }

    public RoleHistory(float time, byte playerId, RuntimeGhostRole role, bool dead)
    {
        Time = time;
        PlayerId = playerId;
        IsModifier = false;
        IsSet = true;
        Assignable = role;
        Dead = dead;
    }
}