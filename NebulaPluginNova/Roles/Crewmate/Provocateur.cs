using Nebula.Game.Statistics;
using Nebula.Roles.Impostor;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

public static class ExtraExileRoleSystem
{
    public static void MarkExtraVictim(GamePlayer player, bool includeImpostors = true, bool expandTargetWhenNobodyCanBeMarked = false, GamePlayer[]? cand = null)
    {
        var voters = (cand ?? MeetingHudExtension.LastVotedForMap
                .Where(entry => entry.Value == player.PlayerId && entry.Key != player.PlayerId)
                .Select(entry => NebulaGameManager.Instance!.GetPlayer(entry.Key)))
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

public class Provocateur : DefinedSingleAbilityRoleTemplate<Provocateur.Ability>, DefinedRole
{
    private Provocateur() : base("provocateur", new(112, 225, 89), RoleCategory.CrewmateRole, Crewmate.MyTeam, [EmbroilCoolDownOption, EmbroilAdditionalCoolDownOption, EmbroilDurationOption]) { }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Get(1, 0));

    static private readonly FloatConfiguration EmbroilCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EmbroilAdditionalCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilAdditionalCoolDown", (0f, 30f, 2.5f), 5f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EmbroilDurationOption = NebulaAPI.Configurations.Configuration("options.role.provocateur.embroilDuration", (1f, 20f, 1f), 5f, FloatConfigurationDecorator.Second);

    static public readonly Provocateur MyRole = new();
    static private readonly GameStatsEntry StatsTask = NebulaAPI.CreateStatsEntry("stats.provocateur.taskPhase", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsExile = NebulaAPI.CreateStatsEntry("stats.provocateur.exile", GameStatsCategory.Roles, MyRole);

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IGameOperator, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), embroilNum];
        public Ability(GamePlayer player, bool isUsurped, int embroilNum) : base(player, isUsurped){
            this.embroilNum  = embroilNum;

            if (AmOwner)
            {
                var embroilButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    EmbroilCoolDownOption, EmbroilDurationOption + EmbroilAdditionalCoolDownOption * embroilNum, "embroil", buttonSprite)
                    .SetAsUsurpableButton(this);
                embroilButton.OnEffectStart = (button) => {
                    RpcShareEmbroilState.Invoke((MyPlayer, true));
                };
                embroilButton.OnEffectEnd = (button) =>
                {
                    RpcShareEmbroilState.Invoke((MyPlayer, false));
                    (embroilButton.CoolDownTimer as GameTimer)?.Expand(EmbroilAdditionalCoolDownOption);
                    embroilButton.StartCoolDown();
                };
            }
        }
        private int embroilNum = 0;

        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EmbroilButton.png", 115f);

        bool embroilActive = false;
        [OnlyHost, OnlyMyPlayer]
        void OnMurdered(PlayerMurderedEvent ev)
        {
            if (ev.Murderer.PlayerId == MyPlayer.PlayerId) return;

            if (embroilActive && !ev.Murderer.IsDead)
            {


                MyPlayer.MurderPlayer(ev.Murderer,PlayerState.Embroiled,EventDetail.Embroil, KillParameter.RemoteKill, KillCondition.TargetAlive);
                NebulaAchievementManager.RpcClearAchievement.Invoke(("provocateur.common2", MyPlayer));
                NebulaAchievementManager.RpcProgressStats.Invoke((StatsTask.Id, MyPlayer));

                if ((MyPlayer.PlayerState == PlayerState.Sniped || MyPlayer.PlayerState == PlayerState.Beaten) && ev.Murderer.VanillaPlayer.GetTruePosition().Distance(MyPlayer.VanillaPlayer.GetTruePosition()) > 10f) NebulaAchievementManager.RpcClearAchievement.Invoke(("provocateur.challenge", MyPlayer));
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
                if(message.player.Role is Ability provocateur)
                {
                    if (message.isActive) provocateur.embroilNum++;
                    provocateur.embroilActive = message.isActive;
                }
            }
            );
    }
}

