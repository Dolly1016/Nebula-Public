using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Compat;

namespace Virial.Game;

public static class GameActionTypes
{

    static public GameActionType NiceTrapPlacementAction { get; internal set; }
    static public GameActionType EvilTrapPlacementAction { get; internal set; }
    static public GameActionType DecoyPlacementAction { get; internal set; }
    static public GameActionType LanternPlacementAction { get; internal set; }
    static public GameActionType SniperEquippingAction { get; internal set; }
    static public GameActionType RaiderThrowingAction { get; internal set; }
    static public GameActionType RaiderEquippingAction { get; internal set; }
    static public GameActionType HadarDisappearingAction { get; internal set; }
    static public GameActionType HadarAppearingAction { get; internal set; }
    static public GameActionType BerserkerTransformingAction { get; internal set; }
    static public GameActionType CannonMarkPlacementAction { get; internal set; }
    static public GameActionType CleanCorpseAction { get; internal set; }
    static public GameActionType EatCorpseAction { get; internal set; }
    static public GameActionType ThuriferActivateAction { get; internal set; }
    static public GameActionType ThuriferImputeAction { get; internal set; }
    static public GameActionType BuskerRevivingAction { get; internal set; }
    static public GameActionType NecromancerRevivingAction { get; internal set; }
    static public GameActionType UbiquitousInvokeDroneAction { get; internal set; }
    static public GameActionType ClogInvokingGhostAction { get; internal set; }
    static public GameActionType HallucinationAction { get; internal set; }
    static public GameActionType NightmarePlacementAction { get; internal set; }
    static public GameActionType WhammyPlacementAction { get; internal set; }
    static public GameActionType HookshotAction { get; internal set; }
}

public class GameActionType
{
    private static Dictionary<string, GameActionType> allActionTypes = new();
    public static bool TryGetActionType(string id, [MaybeNullWhen(false)] out GameActionType gameActionType) => allActionTypes.TryGetValue(id, out gameActionType);
    public string Id { get; private set; }
    public bool IsPlacementAction { get;private set; }
    public bool IsEquippingAction { get; private set; }
    public bool IsPhysicalAction { get; private set; }
    public bool IsFrequentAction { get; private set; }
    public bool IsCleanDeadBodyAction { get; private set; }
    public DefinedAssignable? RelatedRole { get; private set; }

    public GameActionType(string id, DefinedAssignable? relatedRole, bool isPlacementAction = false, bool isEquippingAction = false, bool isPhysicalAction = false, bool isCleanDeadBodyAction = false)
    {
        this.Id = id;
        this.RelatedRole = relatedRole;
        this.IsPlacementAction = isPlacementAction;
        this.IsEquippingAction = isEquippingAction;
        this.IsPhysicalAction = isPhysicalAction;
        this.IsCleanDeadBodyAction = isCleanDeadBodyAction;

        allActionTypes[id] = this;
    }
}
