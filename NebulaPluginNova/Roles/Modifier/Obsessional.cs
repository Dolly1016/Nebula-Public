using Nebula.Roles.Crewmate;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Modifier;

public class Obsessional : ConfigurableStandardModifier
{
    static public Obsessional MyRole = new Obsessional();
    public override string LocalizedName => "obsessional";
    public override string CodeName => "OBS";
    public override Color RoleColor => new(177f / 255f, 102f / 255f, 156f / 255f);

    private NebulaConfiguration CanWinEvenIfObsessionalTargetDieOption = null!;
    private NebulaConfiguration CanWinEvenIfObsessionalDieOption = null!;
    private NebulaConfiguration ObsessionalSuicideWhenObsessionalTargetDieOption = null!;
    private NebulaConfiguration ImpostorObsessionalObsessesOverOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        CanWinEvenIfObsessionalTargetDieOption = new NebulaConfiguration(RoleConfig, "canWinEvenIfObsessionalTargetDie", null, false, false);
        CanWinEvenIfObsessionalDieOption = new NebulaConfiguration(RoleConfig, "canWinEvenIfObsessionalDie", null, true, true);
        ObsessionalSuicideWhenObsessionalTargetDieOption = new NebulaConfiguration(RoleConfig, "obsessionalSuicideWhenObsessionalTargetDie", null, true, true);
        ImpostorObsessionalObsessesOverOption = new NebulaConfiguration(RoleConfig, "impostorObsessionalObsessesOver", null,
            new string[] { 
                "options.role.obsessional.impostorObsessionalObsessesOver.default",
                "options.role.obsessional.impostorObsessionalObsessesOver.neutralOnly",
                "options.role.obsessional.impostorObsessionalObsessesOver.nonCrewmate"}, 0, 0);
    }

    public override ModifierInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    [NebulaRPCHolder]
    public class Instance : ModifierInstance, RuntimeModifier
    {
        public override AbstractModifier Role => MyRole;

        public Instance(GamePlayer player) : base(player)
        {
        }

        GamePlayer? obsession = null;

        public override void DecorateOtherPlayerName(GamePlayer player, ref string text, ref Color color)
        {
            if(player.PlayerId == (obsession?.PlayerId ?? 255)) text += " #".Color(Role.RoleColor);
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)
            {
                text += " $".Color(Role.RoleColor);
                if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                    text += $" <size=60%>({obsession?.Name ?? "ERROR" })</size>";
            }
        }


        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                var cands = NebulaGameManager.Instance?.AllPlayerInfo().Where(p => !p.TryGetModifier<Obsessional.Instance>(out _))!;
                
                var limitted = cands;
                if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole)
                {
                    switch (MyRole.ImpostorObsessionalObsessesOverOption.CurrentValue)
                    {
                        case 1:
                             limitted = cands.Where(p => p.Role.Role.Category == Virial.Assignable.RoleCategory.NeutralRole);
                            break;
                        case 2:
                            limitted = cands.Where(p => p.Role.Role.Category != Virial.Assignable.RoleCategory.CrewmateRole);
                            break;
                    }
                }
                if (limitted.Count() > 0) cands = limitted;

                var cand = cands.ToArray().Random();
                if(cand != null) RpcSetObsessionalTarget.Invoke((MyPlayer.PlayerId, cand.PlayerId));
            }
        }

        [Local]
        void OnPlayerExiled(PlayerExiledEvent ev)
        {
            if (!MyRole.ObsessionalSuicideWhenObsessionalTargetDieOption) return;

            if (ev.Player.PlayerId == (obsession?.PlayerId ?? 255) && !MyPlayer.IsDead)
            {
                MyPlayer.VanillaPlayer.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
                new StaticAchievementToken("obsessional.another1");
            }
        }

        [Local]
        void OnPlayerDead(PlayerDieEvent ev)
        {
            if (!MyRole.ObsessionalSuicideWhenObsessionalTargetDieOption) return;

            if (ev.Player.PlayerId == (obsession?.PlayerId ?? 255) && !MeetingHudExtension.MarkedAsExtraVictims(MyPlayer.PlayerId) && !MyPlayer.IsDead)
            {
                MyPlayer.Suicide(PlayerState.Suicide, EventDetail.Kill);
                new StaticAchievementToken("obsessional.another1");
            }
        }

        //本来の勝利条件をブロックする
        [OnlyMyPlayer]
        void BlockWins(PlayerBlockWinEvent ev) => ev.IsBlocked |= true;

        [OnlyMyPlayer]
        void CheckExtraWins(PlayerCheckExtraWinEvent ev)
        {
            if (ev.Phase != ExtraWinCheckPhase.ObsessionPhase) return;

            if (!MyRole.CanWinEvenIfObsessionalDieOption && MyPlayer.IsDead) return;
            if (!MyRole.CanWinEvenIfObsessionalTargetDieOption && (obsession?.IsDead ?? true)) return;

            if (obsession != null && ev.WinnersMask.Test(obsession))
            {
                ev.SetWin(true);
                ev.ExtraWinMask.Add(NebulaGameEnd.ExtraObsessionalWin);
            }
        }

        public override string? IntroText => Language.Translate("role.obsessional.blurb").Replace("%NAME%", (obsession?.Name ?? "ERROR").Color(MyRole.RoleColor));

        static RemoteProcess<(byte playerId, byte targetId)> RpcSetObsessionalTarget = new("SetObsessionalTarget",
        (message, _) =>
        {
            if (NebulaGameManager.Instance?.GetPlayer(message.playerId)?.TryGetModifier<Obsessional.Instance>(out var instance) ?? false)
                instance.obsession = NebulaGameManager.Instance.GetPlayer(message.targetId);
        });

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (ev.EndState.Winners.Test(MyPlayer))
            {
                //勝利

                new StaticAchievementToken("obsessional.common1");

                if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota >= 5)
                    new StaticAchievementToken("agent.challenge");

                if (ev.EndState.EndCondition == NebulaGameEnd.LoversWin && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                    new StaticAchievementToken("obsessional.lover1");

                //勝者に自身と執着対象しかいない場合
                if (NebulaGameManager.Instance!.AllPlayerInfo().Where(p => ev.EndState.Winners.Test(p)).All(p => p.AmOwner || p.PlayerId == (obsession?.PlayerId ?? 255)))
                    new StaticAchievementToken("obsessional.challenge");
            }
            else
            {
                //敗北

                if (MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin)
                    new StaticAchievementToken("obsessional.another2");

                if (ev.EndState.Winners.Test(obsession) && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                    new StaticAchievementToken("obsessional.lover2");
            }
        }

        public override bool MyCrewmateTaskIsIgnored => obsession?.Role.Role.Category != Virial.Assignable.RoleCategory.CrewmateRole || obsession?.Role.Role == Madmate.MyRole;
    }
}
