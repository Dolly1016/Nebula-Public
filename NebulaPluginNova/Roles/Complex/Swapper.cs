using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Modules;
using Nebula.Utilities;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Complex;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
[NebulaRPCHolder]
file class SwapperSystem : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static SwapperSystem() => DIManager.Instance.RegisterModule(() => new SwapperSystem());
    private SwapperSystem() {
        ModSingleton<SwapperSystem>.Instance = this;
        this.RegisterPermanently();
    }

    private record SwapRequest(GamePlayer Swapper, GamePlayer Swapped1, GamePlayer Swapped2, int CurrentLeftSwap);
    private List<SwapRequest> requests = [];
    public void ReceiveSwapRequest(GamePlayer swapper, GamePlayer swapped1, GamePlayer swapped2, int currentLeftSwap)
    {
        requests.Add(new(swapper, swapped1, swapped2, currentLeftSwap));
    }

    void OnMeetingPreStart(MeetingPreStartEvent ev)
    {
        requests.Clear();
    }

    void OnFixSwapping(MeetingMapVotesHostEvent ev) { 
        foreach(var request in requests)
        {
            if (!MeetingHudExtension.CanVoteFor(request.Swapped1) || !MeetingHudExtension.CanVoteFor(request.Swapped2)) continue;
            ev.Swap(request.Swapped1, request.Swapped2);
            if (request.Swapper.TryGetAbility<Swapper.Ability>(out var swapper)) swapper.EnsureSwap(request.CurrentLeftSwap - 1);
        }
    }
}

[NebulaRPCHolder]
public class Swapper : DefinedSingleAbilityRoleTemplate<IUsurpableAbility>, DefinedRole, IAssignableDocument, HasCitation
{
    private Swapper(bool isEvil) : base(
        isEvil ? "evilSwapper" : "niceSwapper",
        isEvil ? new(Palette.ImpostorRed) : new(171, 74, 146),
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam,
        [isEvil ? NumOfSwapEvilOption : NumOfSwapNiceOption, CanSelectSwapperOption])
    {

        //if (IsEvil) ConfigurationHolder?.AppendConfiguration(KillTrapSoundDistanceOption);
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!]);
    }

    static internal Image IconImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/swapper.png");
    Image? DefinedAssignable.IconImage => IconImage;
    public bool IsEvil => Category == RoleCategory.ImpostorRole;
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => IsEvil ? AbilityAssignmentStatus.KillersSide : AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => IsEvil ? MultipleAssignmentType.Allowed : MultipleAssignmentType.NotAllowed;
    public override IUsurpableAbility CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, IsEvil ? NumOfSwapEvilOption : NumOfSwapNiceOption));

    static internal readonly IntegerConfiguration NumOfSwapNiceOption = NebulaAPI.Configurations.Configuration("options.role.niceSwapper.numOfSwap", (1, 15), 3);
    static internal readonly IntegerConfiguration NumOfSwapEvilOption = NebulaAPI.Configurations.Configuration("options.role.evilSwapper.numOfSwap", (1, 15), 1);
    static internal readonly BoolConfiguration CanSelectSwapperOption = NebulaAPI.Configurations.Configuration("options.role.swapper.canSelectSwapper", true);

    static public readonly Swapper MyNiceRole = new(false);
    static public readonly Swapper MyEvilRole = new(true);

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    Citation? HasCitation.Citation => IsEvil ? Citations.TheOtherRolesGM : Citations.TheOtherRoles;

    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(MeetingIcon, "role.swapper.ability.swap");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%NUM%", (IsEvil ? NumOfSwapEvilOption : NumOfSwapNiceOption).GetValue().ToString());
    }

    static private Image MeetingIcon => MeetingPlayerButtonManager.Icons.AsLoader(8);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        int leftSwap;
        public Ability(GamePlayer player, bool isUsurped, int leftSwap) : base(player, isUsurped)
        {
            this.leftSwap = leftSwap;
            this.awareOfUsurpation = isUsurped;

            if(AmOwner)
            {
                string prefix = Language.Translate("roles.swapper.leftSwap");
                Helpers.TextHudContent("SwapperText", this, (tmPro) => tmPro.text = prefix + ": " + this.leftSwap, true);
            }
        }

        bool awareOfUsurpation = false;
        [Local]
        private void OnMeeting(MeetingStartEvent ev)
        {
            bool awareOfUsurpation = false;
            bool selected = false;
            if (this.leftSwap <= 0) return;
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(MeetingIcon,
            state => {
                if (selected) return;
                var lastSelected = buttonManager.AllStates.FirstOrDefault(p => p.IsSelected);
                var p = state.MyPlayer;

                state.SetSelect(true);
                if (lastSelected != null)
                { 
                    if(!MeetingHudExtension.CanVoteFor(lastSelected.MyPlayer))
                    {
                        lastSelected.SetSelect(false);
                    }
                    else
                    {
                        RpcSwap.Invoke((MyPlayer, lastSelected.MyPlayer, state.MyPlayer, leftSwap));
                        selected = true;
                    }
                }
            },
            p => (!selected || p.IsSelected) && !awareOfUsurpation && leftSwap > 0 && !p.MyPlayer.IsDead && (CanSelectSwapperOption || !p.MyPlayer.AmOwner) && MyPlayer.IsAlive
            ));
        }

        internal void EnsureSwap(int nextLeftSwap) => RpcEnsureSwap.RpcSync(MyPlayer, nextLeftSwap);
        static private readonly RoleRPC.Definition RpcEnsureSwap = RoleRPC.Get<Ability>("swapper.ensureSwap", (ability, num, calledByMe) => ability.leftSwap = num);
        static private readonly RemoteProcess<(GamePlayer swapper, GamePlayer swap1, GamePlayer swap2, int currentLeftSwap)> RpcSwap = new("VoteSwap", (message, _) => {
            ModSingleton<SwapperSystem>.Instance.ReceiveSwapRequest(message.swapper, message.swap1, message.swap2, message.currentLeftSwap);
        });
    }

}

