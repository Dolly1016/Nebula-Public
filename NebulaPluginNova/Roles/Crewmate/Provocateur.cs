using Nebula.Configuration;
using Nebula.Roles.Impostor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public static class ExtraExileRoleSystem
{
    public static void MarkExtraVictim(PlayerModInfo player, bool includeImpostors = true, bool expandTargetWhenNobodyCanBeMarked = false)
    {
        var voters = MeetingHudExtension.LastVotedForMap
                .Where(entry => entry.Value == player.PlayerId && entry.Key != player.PlayerId)
                .Select(entry => NebulaGameManager.Instance!.GetModPlayerInfo(entry.Key))
                .Where(p => !p!.IsDead && (includeImpostors || p.Role.Role.Category != RoleCategory.ImpostorRole))
                .ToArray();
        
        if(voters.Length == 0 && expandTargetWhenNobodyCanBeMarked)
        {
            voters = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && !p.AmOwner && (includeImpostors || p.Role.Role.Category != RoleCategory.ImpostorRole)).ToArray();
        }
        if (voters.Length == 0) return;
        voters[System.Random.Shared.Next(voters.Length)]!.MyControl.ModMarkAsExtraVictim(player.MyControl, PlayerState.Embroiled, EventDetail.Embroil);
    }
}

public class Provocateur : ConfigurableStandardRole
{
    static public Provocateur MyRole = new Provocateur();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "provocateur";
    public override Color RoleColor => new Color(112f / 255f, 255f / 255f, 89f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private NebulaConfiguration EmbroilCoolDownOption = null!;
    private NebulaConfiguration EmbroilAdditionalCoolDownOption = null!;
    private NebulaConfiguration EmbroilDurationOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        EmbroilCoolDownOption = new(RoleConfig, "embroilCoolDown", null, 5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        EmbroilAdditionalCoolDownOption = new(RoleConfig, "embroilAdditionalCoolDown", null, 0f, 30f, 2.5f, 5f, 5f) { Decorator = NebulaConfiguration.SecDecorator };
        EmbroilDurationOption = new(RoleConfig, "embroilDuration", null, 1f, 20f, 1f, 5f, 5f) { Decorator = NebulaConfiguration.SecDecorator };
    }

    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        public override AbstractRole Role => MyRole;
        public Instance(PlayerModInfo player) : base(player){}

        private ModAbilityButton embroilButton = null!;
        static private ISpriteLoader buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EmbroilButton.png", 115f);
        
        public override void OnActivated()
        {
            if (AmOwner)
            {
                embroilButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                embroilButton.SetSprite(buttonSprite.GetSprite());
                embroilButton.Availability = (button) => MyPlayer.MyControl.CanMove;
                embroilButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                embroilButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                embroilButton.OnEffectEnd = (button) =>
                {
                    button.CoolDownTimer?.Expand(MyRole.EmbroilAdditionalCoolDownOption.GetFloat());
                    embroilButton.StartCoolDown();
                };
                embroilButton.CoolDownTimer = Bind(new Timer(0f, MyRole.EmbroilCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                embroilButton.EffectTimer = Bind(new Timer(0f, MyRole.EmbroilDurationOption.GetFloat()));
                embroilButton.SetLabel("embroil");
            }
        }

        void IGamePlayerEntity.OnMurdered(GamePlayer murderer)
        {
            if (murderer.PlayerId == MyPlayer.PlayerId) return;

            if (AmOwner && embroilButton.EffectActive && !murderer.VanillaPlayer.Data.IsDead)
            {
                MyPlayer.MyControl.ModKill(murderer.VanillaPlayer,false,PlayerState.Embroiled,EventDetail.Embroil);
                new StaticAchievementToken("provocateur.common2");

                var murdererRole = murderer.Unbox()?.Role.Role;
                if (murdererRole is Sniper or Raider && murderer.VanillaPlayer.GetTruePosition().Distance(MyPlayer.MyControl.GetTruePosition()) > 10f) new StaticAchievementToken("provocateur.challenge");
            }
        }

        void IGamePlayerEntity.OnExiled()
        {
            if (!AmOwner) return;

            ExtraExileRoleSystem.MarkExtraVictim(MyPlayer);
            new StaticAchievementToken("provocateur.common1");
        }
    }
}

