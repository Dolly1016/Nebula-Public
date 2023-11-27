using Nebula.Compat;
using Nebula.Events;
using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Text;

namespace Nebula;

public class NebulaImpl : INebula
{
    public static NebulaImpl Instance = null!;

    public NebulaImpl()
    {
        Instance = this;
    }

    public List<AbstractRoleDef> AllRoles = new();

    public string APIVersion => "1.0";

    public Virial.Assets.INameSpace NebulaAsset => NameSpaceManager.DefaultNameSpace;

    public Virial.Assets.INameSpace InnerslothAsset => NameSpaceManager.InnerslothNameSpace;

    public CommunicableTextTag RegisterCommunicableText(string translationKey)
    {
        return new TranslatableTag(translationKey);
    }

    public void RegisterRole(AbstractRoleDef roleDef)
    {
        AllRoles.Add(roleDef);
    }

    public RoleTeam CreateTeam(string translationKey, Virial.Color color, TeamRevealType revealType)
    {
        return new Team(translationKey,color.ToUnityColor(), revealType);
    }

    public void RegisterEventHandler(ILifespan lifespan,object handler)
    {
        EventManager.RegisterEvent(lifespan, handler);
    }

    public Virial.Components.AbilityButton CreateAbilityButton()
    {
        return new ModAbilityButton();
    }

    public Virial.Components.GameTimer CreateTimer(float max, float min)
    {
        return new Timer(min, max);
    }

    public Virial.Assets.INameSpace GetAddon(string addonId)
    {
        return NameSpaceManager.ResolveOrGetDefault(addonId);
    }

    public IEnumerable<Virial.Game.Player> GetPlayers()
    {
        if (NebulaGameManager.Instance == null) yield break;
        foreach (var p in NebulaGameManager.Instance.AllPlayerInfo()) yield return p;
    }
}
