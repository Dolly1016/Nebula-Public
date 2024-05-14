using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles;

public interface IConfiguableAssignable
{
    ConfigurationHolder RoleConfig { get; }
}

public interface IAssignableBase : DefinedAssignable
{
    public ConfigurationHolder? RelatedConfig { get; }

    public void Load();

    //For Config
    public IEnumerable<IAssignableBase> RelatedOnConfig();
}


public abstract class AssignableInstance : ComponentHolder, RuntimeAssignable, Virial.IBinderLifespan, IBindPlayer
{
    public virtual IAssignableBase AssignableBase { get; } = null!;
    public GamePlayer MyPlayer { get; private init; }
    public bool AmOwner => MyPlayer.AmOwner;

    public AssignableInstance(GamePlayer player)
    {
        this.MyPlayer = player;
    }
    
    public virtual void DecoratePlayerName(ref string text, ref Color color) { }
    public virtual void DecorateOtherPlayerName(GamePlayer player,ref string text, ref Color color) { }
    public virtual void DecorateRoleName(ref string text) { }

    public virtual void OnTieVotes(ref List<byte> extraVotes,PlayerVoteArea myVoteArea) { }

    public virtual string? GetExtraTaskText() => null;

    public virtual KillResult CheckKill(GamePlayer killer, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, bool isMeetingKill) { return KillResult.Kill; }


    //////////////////////////////////////////
    //                                      //
    //              Virial API              //
    //                                      //
    //////////////////////////////////////////


    // Virial.AssignableAPI

    DefinedAssignable RuntimeAssignable.Assignable => AssignableBase;


    GamePlayer IBindPlayer.MyPlayer => MyPlayer;
}