using NAudio.CoreAudioApi;
using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;

namespace Nebula.Roles.Perks;

internal class PlayerLook : PerkFunctionalInstance
{
    bool used = false;
    static PerkFunctionalDefinition TeamLook = new("teamLook", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("teamLook", 0, 43, new(174, 127, 52)), (def, instance) => new PlayerLook(def, instance, p => !p.IsDead, p => p.Unbox().ColoredDefaultName + "<br>" + Language.Translate("perk.teamLook.ui.team").Replace("%TEAM%", Language.Translate(p.Role.Role.Team.TranslationKey)).Bold(), MeetingPlayerButtonManager.Icons.AsLoader(4), MeetingOverlayHolder.IconsSprite[4]));
    static PerkFunctionalDefinition GhostLook = new("ghostLook", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("ghostLook", 4, 47, (Crewmate.Psychic.MyRole as DefinedRole).Color), (def, instance) => new PlayerLook(def, instance, p => p.IsDead, p => p.Unbox().ColoredDefaultName + "<br>" + p.PlayerState.Text.Bold(), MeetingPlayerButtonManager.Icons.AsLoader(5), MeetingOverlayHolder.IconsSprite[5]));

    Predicate<GamePlayer> predicate;
    Func<GamePlayer, string> textGenerator;
    Virial.Media.Image playerButtonImage, upperImage;
    public PlayerLook(PerkDefinition def, PerkInstance instance, Predicate<GamePlayer> predicate, Func<GamePlayer, string> textGenerator, Virial.Media.Image meetingPlayerImage,Virial.Media.Image meetingUpperImage) : base(def, instance)
    {
        this.predicate = predicate;
        this.textGenerator = textGenerator;
        this.playerButtonImage = meetingPlayerImage;
        this.upperImage = meetingUpperImage;
    }

    void OnMeetingStart(MeetingStartEvent ev)
    {
        if (!used)
        {
            var buttonManager = NebulaAPI.CurrentGame?.GetModule<MeetingPlayerButtonManager>();
            buttonManager?.RegisterMeetingAction(new(playerButtonImage,
               p =>
               {
                   if (!(MeetingHud.Instance.state == MeetingHud.VoteStates.Voted || MeetingHud.Instance.state == MeetingHud.VoteStates.NotVoted)) return;

                   used = true;

                   NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new TranslateTextComponent("perk."+PerkDefinition.localizedName +".ui.title")),
                    new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent),
                    new RawTextComponent(textGenerator.Invoke(p.MyPlayer))))
                    , upperImage, PerkDefinition.perkColor.RGBMultiplied(0.7f));

               },
               p => !used && predicate.Invoke(p.MyPlayer) && !MyPlayer.IsDead && !p.MyPlayer.AmOwner
               ));
        }
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor(used ? Color.gray : Color.white);
    }
}
