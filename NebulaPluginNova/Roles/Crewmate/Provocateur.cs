using Nebula.Game.Statistics;
using Nebula.Roles.Impostor;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public static class ExtraExileRoleSystem
{
    public static void MarkExtraVictim(GamePlayer player, bool includeImpostors = true, bool expandTargetWhenNobodyCanBeMarked = false)
    {
        var voters = MeetingHudExtension.LastVotedForMap
                .Where(entry => entry.Value == player.PlayerId && entry.Key != player.PlayerId)
                .Select(entry => NebulaGameManager.Instance!.GetPlayer(entry.Key))
                .Where(p => !p!.IsDead && (includeImpostors || p.Role.Role.Category != RoleCategory.ImpostorRole))
                .ToArray();
        
        if(voters.Length == 0 && expandTargetWhenNobodyCanBeMarked)
        {
            voters = NebulaGameManager.Instance!.AllPlayerInfo().Where(p => !p.IsDead && !p.AmOwner && (includeImpostors || p.Role.Role.Category != RoleCategory.ImpostorRole)).ToArray();
        }
        if (voters.Length == 0) return;
        voters[System.Random.Shared.Next(voters.Length)]!.VanillaPlayer.ModMarkAsExtraVictim(player.VanillaPlayer, PlayerState.Embroiled, EventDetail.Embroil);
    }
}

public class Provocateur : DefinedRoleTemplate, DefinedRole
{
    private Provocateur() : base("provocateur", new(112, 225, 89), RoleCategory.CrewmateRole, Crewmate.MyTeam, [EmbroilCoolDownOption, EmbroilAdditionalCoolDownOption, EmbroilDurationOption]) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    static private FloatConfiguration EmbroilCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EmbroilAdditionalCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilAdditionalCoolDown", (0f, 30f, 2.5f), 5f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EmbroilDurationOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilDuration", (1f, 20f, 1f), 5f, FloatConfigurationDecorator.Second);

    static public Provocateur MyRole = new Provocateur();


    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player) : base(player){}

        private ModAbilityButton embroilButton = null!;
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EmbroilButton.png", 115f);

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                embroilButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                embroilButton.SetSprite(buttonSprite.GetSprite());
                embroilButton.Availability = (button) => MyPlayer.CanMove;
                embroilButton.Visibility = (button) => !MyPlayer.IsDead;
                embroilButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                var coolDownTimer = Bind(new Timer(0f, EmbroilCoolDownOption).SetAsAbilityCoolDown().Start());
                embroilButton.OnEffectEnd = (button) =>
                {
                    coolDownTimer.Expand(EmbroilAdditionalCoolDownOption);
                    embroilButton.StartCoolDown();
                };
                embroilButton.CoolDownTimer = coolDownTimer;
                embroilButton.EffectTimer = Bind(new Timer(0f, EmbroilDurationOption));
                embroilButton.SetLabel("embroil");
            }
        }

        [Local, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer.AmOwner) return;

            if (embroilButton.EffectActive && !ev.Murderer.IsDead)
            {
                MyPlayer.MurderPlayer(ev.Murderer,PlayerState.Embroiled,EventDetail.Embroil, KillParameter.NormalKill);
                new StaticAchievementToken("provocateur.common2");

                var murdererRole = ev.Murderer.Role.Role;
                if (murdererRole is Sniper or Raider && ev.Murderer.VanillaPlayer.GetTruePosition().Distance(MyPlayer.VanillaPlayer.GetTruePosition()) > 10f) new StaticAchievementToken("provocateur.challenge");
            }
        }

        [Local, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {

            ExtraExileRoleSystem.MarkExtraVictim(MyPlayer);
            new StaticAchievementToken("provocateur.common1");
        }
    }
}

