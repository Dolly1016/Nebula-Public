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
            voters = NebulaGameManager.Instance!.AllPlayerInfo.Where(p => !p.IsDead && !p.AmOwner && (includeImpostors || p.Role.Role.Category != RoleCategory.ImpostorRole)).ToArray();
        }
        if (voters.Length == 0) return;
        voters[System.Random.Shared.Next(voters.Length)]!.VanillaPlayer.ModMarkAsExtraVictim(player.VanillaPlayer, PlayerState.Embroiled, EventDetail.Embroil);
    }
}

public class Provocateur : DefinedRoleTemplate, DefinedRole
{
    private Provocateur() : base("provocateur", new(112, 225, 89), RoleCategory.CrewmateRole, Crewmate.MyTeam, [EmbroilCoolDownOption, EmbroilAdditionalCoolDownOption, EmbroilDurationOption]) { }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments.Get(0, 0));

    static private FloatConfiguration EmbroilCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EmbroilAdditionalCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilAdditionalCoolDown", (0f, 30f, 2.5f), 5f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration EmbroilDurationOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilDuration", (1f, 20f, 1f), 5f, FloatConfigurationDecorator.Second);

    static public Provocateur MyRole = new Provocateur();
    static private GameStatsEntry StatsTask = NebulaAPI.CreateStatsEntry("stats.provocateur.taskPhase", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsExile = NebulaAPI.CreateStatsEntry("stats.provocateur.exile", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;
        public Instance(GamePlayer player, int embroilNum) : base(player){
            this.embroilNum  = embroilNum;
        }
        private int embroilNum = 0;
        int[] RuntimeAssignable.RoleArguments => [embroilNum];

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
                    if (!button.EffectActive)
                    {
                        button.ActivateEffect();
                        RpcShareEmbroilState.Invoke((MyPlayer, true));
                    }
                };
                var coolDownTimer = Bind(new Timer(0f, EmbroilCoolDownOption).SetAsAbilityCoolDown().Start());
                embroilButton.OnEffectEnd = (button) =>
                {
                    RpcShareEmbroilState.Invoke((MyPlayer, false));
                    coolDownTimer.Expand(EmbroilAdditionalCoolDownOption);
                    embroilButton.StartCoolDown();
                };
                embroilButton.CoolDownTimer = coolDownTimer;
                embroilButton.EffectTimer = Bind(new Timer(0f, EmbroilDurationOption + EmbroilAdditionalCoolDownOption * embroilNum));
                embroilButton.SetLabel("embroil");
            }
        }

        bool embroilActive = false;
        [OnlyHost, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer.AmOwner) return;

            if (embroilActive && !ev.Murderer.IsDead)
            {


                MyPlayer.MurderPlayer(ev.Murderer,PlayerState.Embroiled,EventDetail.Embroil, KillParameter.NormalKill, KillCondition.TargetAlive);
                NebulaAchievementManager.RpcClearAchievement.Invoke(("provocateur.common2", MyPlayer));
                NebulaAchievementManager.RpcProgressStats.Invoke((StatsTask.Id, MyPlayer));

                var murdererRole = ev.Murderer.Role.Role;
                if (murdererRole is Sniper or Raider && ev.Murderer.VanillaPlayer.GetTruePosition().Distance(MyPlayer.VanillaPlayer.GetTruePosition()) > 10f) NebulaAchievementManager.RpcClearAchievement.Invoke(("provocateur.challenge", MyPlayer));
            }
        }

        [Local, OnlyMyPlayer]
        void OnExiled(PlayerExiledEvent ev)
        {

            ExtraExileRoleSystem.MarkExtraVictim(MyPlayer);
            new StaticAchievementToken("provocateur.common1");
            StatsExile.Progress();
        }

        static private RemoteProcess<(GamePlayer player, bool isActive)> RpcShareEmbroilState = new(
            "ShareEmbroilState",
            (message, _) =>
            {
                if(message.player.Role is Instance provocateur)
                {
                    if (message.isActive) provocateur.embroilNum++;
                    provocateur.embroilActive = message.isActive;
                }
            }
            );
    }
}

