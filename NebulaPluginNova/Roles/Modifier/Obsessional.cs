using JetBrains.Annotations;
using Mono.CSharp;
using Nebula.Roles.Crewmate;
using Rewired.UI.ControlMapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Game;
using Virial.Text;

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

    public override ModifierInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    [NebulaRPCHolder]
    public class Instance : ModifierInstance, IGamePlayerEntity
    {
        public override AbstractModifier Role => MyRole;

        public Instance(PlayerModInfo player) : base(player)
        {
        }

        PlayerModInfo? obsession = null;

        public override void DecorateOtherPlayerName(PlayerModInfo player, ref string text, ref Color color)
        {
            if(player.PlayerId == (obsession?.PlayerId ?? 255)) text += " #".Color(Role.RoleColor);
        }

        public override void DecoratePlayerName(ref string text, ref Color color)
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false)
            {
                text += " $".Color(Role.RoleColor);
                if (AmongUsClient.Instance.GameState == InnerNet.InnerNetClient.GameStates.Started)
                    text += $" <size=60%>({obsession?.DefaultName ?? "ERROR" })</size>";
            }
        }
    

        public override void OnActivated()
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

        void IGameEntity.OnPlayerExiled(GamePlayer exiled)
        {
            if (AmOwner)
            {
                if (!MyRole.ObsessionalSuicideWhenObsessionalTargetDieOption) return;

                if (exiled.PlayerId == (obsession?.PlayerId ?? 255) && !MyPlayer.IsDead)
                {
                    MyPlayer.MyControl.ModMarkAsExtraVictim(null, PlayerState.Suicide, PlayerState.Suicide);
                    new StaticAchievementToken("obsessional.another1");
                }
            }
        }

        void IGameEntity.OnPlayerDead(GamePlayer dead)
        {
            if (AmOwner)
            {
                if (!MyRole.ObsessionalSuicideWhenObsessionalTargetDieOption) return;

                if (dead.PlayerId == (obsession?.PlayerId ?? 255) && !MeetingHudExtension.MarkedAsExtraVictims(MyPlayer.PlayerId) && !MyPlayer.IsDead)
                {
                    MyPlayer.MyControl.ModSuicide(false, PlayerState.Suicide, EventDetail.Kill);
                    new StaticAchievementToken("obsessional.another1");
                }
            }
        }

        //本来の勝利条件をブロックする
        public override bool BlockWins(CustomEndCondition endCondition) => true;

        //追加勝利
        public override bool CheckExtraWins(CustomEndCondition endCondition, ExtraWinCheckPhase phase, int winnersMask, ref ulong extraWinMask)
        {
            if (phase != ExtraWinCheckPhase.ObsessionPhase) return false;

            if (!MyRole.CanWinEvenIfObsessionalDieOption && MyPlayer.IsDead) return false;
            if (!MyRole.CanWinEvenIfObsessionalTargetDieOption && (obsession?.IsDead ?? true)) return false;

            if (obsession != null && ((1 << obsession.PlayerId) & winnersMask) != 0)
            {
                extraWinMask |= NebulaGameEnd.ExtraObsessionalWin.ExtraWinMask;
                return true;
            }

            return false;
        }

        public override string? IntroText => Language.Translate("role.obsessional.blurb").Replace("%NAME%", (obsession?.DefaultName ?? "ERROR").Color(MyRole.RoleColor));

        static RemoteProcess<(byte playerId, byte targetId)> RpcSetObsessionalTarget = new("SetObsessionalTarget",
        (message, _) =>
        {
            if (NebulaGameManager.Instance?.GetModPlayerInfo(message.playerId)?.TryGetModifier<Obsessional.Instance>(out var instance) ?? false)
                instance.obsession = NebulaGameManager.Instance.GetModPlayerInfo(message.targetId);
        });

        public override void OnGameEnd(NebulaEndState endState)
        {
            if (AmOwner)
            {
                if (endState.CheckWin(MyPlayer.PlayerId))
                {
                    //勝利

                    new StaticAchievementToken("obsessional.common1");

                    if (MyPlayer.Tasks.TotalCompleted - MyPlayer.Tasks.Quota >= 5)
                        new StaticAchievementToken("agent.challenge");

                    if(endState.EndCondition == NebulaGameEnd.LoversWin && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                        new StaticAchievementToken("obsessional.lover1");

                    //勝者に自身と執着対象しかいない場合
                    if(NebulaGameManager.Instance!.AllPlayerInfo().Where(p=> endState.CheckWin(p.PlayerId)).All(p => p.AmOwner || p.PlayerId == (obsession?.PlayerId ?? 255)))
                        new StaticAchievementToken("obsessional.challenge");
                }
                else
                {
                    //敗北

                    if(MyPlayer.Role.Role.Category == Virial.Assignable.RoleCategory.ImpostorRole && endState.EndCondition == NebulaGameEnd.ImpostorWin)
                        new StaticAchievementToken("obsessional.another2");

                    if(endState.CheckWin(obsession?.PlayerId ?? 255) && (obsession?.TryGetModifier<Lover.Instance>(out _) ?? false))
                        new StaticAchievementToken("obsessional.lover2");
                }
            }
        }

        public override bool MyCrewmateTaskIsIgnored => obsession?.Role.Role.Category != Virial.Assignable.RoleCategory.CrewmateRole || obsession?.Role.Role == Madmate.MyRole;
    }
}
