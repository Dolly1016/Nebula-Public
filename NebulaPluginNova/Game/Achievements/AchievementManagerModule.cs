using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using Virial;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Game.Achievements;

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
internal class AchievementManagerModule : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static AchievementManagerModule()
    {
        DIManager.Instance.RegisterModule(() => new AchievementManagerModule());
    }

    private AchievementManagerModule()
    {
        this.Register(NebulaGameManager.Instance!);
        ModSingleton<AchievementManagerModule>.Instance = this;
    }

    public AchievementToken<float> CorpseToken = null!;
    void OnGameStart(GameStartEvent ev)
    {
        CorpseToken = new("dragCorpse", 0f, (dis, _) => (int)dis);

        //実績
        var challengeDeath2Token = new AchievementToken<int>("challenge.death2", 0, (exileAnyone, _) => (NebulaGameManager.Instance!.AllPlayerInfo.Where(p => p.IsDead && p.MyKiller == GamePlayer.LocalPlayer && p != GamePlayer.LocalPlayer).Select(p => p.PlayerState).Distinct().Count() + exileAnyone) >= 4);
        GameOperatorManager.Instance?.Subscribe<PlayerVoteDisclosedLocalEvent>(ev => { if (ev.VoteToWillBeExiled) challengeDeath2Token.Value = 1; }, NebulaAPI.CurrentGame!);
        
        if (Helpers.CurrentMonth == 3) new AchievementToken<int>("graduation2", 0, (exileAnyone, _) => exileAnyone >= 3);

        if (Helpers.CurrentMonth == 5 && (AmongUsUtil.CurrentMapId is 1 or 4))
        {
            if (GamePlayer.LocalPlayer!.Unbox().TryGetModifier<Lover.Instance>(out var lover))
            {
                var myLover = lover.MyLover.Get();
                float time = 0f;
                bool isCleared = false;
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
                    {
                        if (isCleared) return;

                        if (!lover.IsDeadObject && !GamePlayer.LocalPlayer!.IsDead && !myLover!.IsDead)
                        {
                            if (
                                GamePlayer.LocalPlayer.HasAttribute(PlayerAttributes.Accel) &&
                                myLover.HasAttribute(PlayerAttributes.Accel) &&
                                GamePlayer.LocalPlayer.VanillaPlayer.MyPhysics.Velocity.magnitude > 0f &&
                                GamePlayer.LocalPlayer.VanillaPlayer.transform.position.Distance(myLover.VanillaPlayer.transform.position) < 2f
                            )
                            {
                                time += Time.deltaTime;
                                if (time > 5f)
                                {
                                    new StaticAchievementToken("koinobori");
                                    isCleared = true;
                                }
                            }
                            else
                            {
                                time = 0f;
                            }
                        }
                    }, NebulaAPI.CurrentGame!);
            }
        }

        if(Helpers.CurrentMonth == 6)
        {
            if(AmongUsUtil.CurrentMapId is 0 or 2 or 5)
            {
                bool isSkeld = AmongUsUtil.CurrentMapId is 0;
                IPlayerAttribute attr = isSkeld ? PlayerAttributes.Accel : PlayerAttributes.Decel;
                float time = 0f;
                bool isCleared = false;
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
                {

                    if (isCleared) return;

                    if (!GamePlayer.LocalPlayer!.IsDead)
                    {
                        if (
                            GamePlayer.LocalPlayer.HasAttribute(attr) &&
                            GamePlayer.LocalPlayer.VanillaPlayer.MyPhysics.Velocity.magnitude > 0f
                        )
                        {
                            time += Time.deltaTime;
                            if (time > 6f)
                            {
                                new StaticAchievementToken(isSkeld ? "rainyStepAnother" : "rainyStep");
                                isCleared = true;
                            }
                        }
                        else
                        {
                            time = 0f;
                        }
                    }

                }, NebulaAPI.CurrentGame!);
            }
        }

        if (Helpers.CurrentMonth == 8)
        {
            if (AmongUsUtil.CurrentMapId is 5)
            {
                GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
                {
                    if (ev.Murderer.AmOwner && ev.Dead.TryGetModifier<Bloody.Instance>(out _) && ev.Dead.Position.x < -8f && ev.Murderer.Position.x < -8f && ColorHelper.IsGreenOrBlack(DynamicPalette.PlayerColors[ev.Dead.PlayerId]))
                        new StaticAchievementToken("watermelon");
                }, NebulaAPI.CurrentGame!);
            }
        }

        if (Helpers.CurrentMonth == 10)
        {
            AchievementToken<float> acToken10 = new("halloween", 0f, (a, _) => (int)a);
            byte lastDeadBody = byte.MaxValue;
            Vector2 lastPos = Vector2.zeroVector;
            var myPlayer = GamePlayer.LocalPlayer!;
            GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
            {
                byte currentBody = myPlayer.HoldingDeadBody?.PlayerId ?? byte.MaxValue;
                if (currentBody != byte.MaxValue && currentBody == lastDeadBody && !myPlayer.VanillaPlayer.inVent)
                {
                    float val = lastPos.Distance(myPlayer.Position);
                    if(val < 1f) acToken10.Value += val; //大きすぎる値は加算しない。
                }

                //位置を更新する。
                lastDeadBody = currentBody;
                lastPos = myPlayer.Position;
                
            }, NebulaAPI.CurrentGame!);
        }

        //会議なしで勝利
        {
            //会議が始まったらフラグを下げる
            var token = new AchievementToken<bool>("noMeeting", true, (val, _) => val && NebulaGameManager.Instance!.EndState!.Winners.Test(GamePlayer.LocalPlayer));
            GameOperatorManager.Instance?.Subscribe<MeetingStartEvent>(ev => token.Value = false, NebulaAPI.CurrentGame);
        }

        //コスチューム (ゲーム開始時に判定できるもの)
        {
            bool HasVisorTag(GamePlayer player, string tag) => MoreCosmic.GetTags(player.DefaultOutfit.outfit).Any(t => t == "visor." + tag);
            bool HasTag(GamePlayer player, string tag) => MoreCosmic.GetTags(player.DefaultOutfit.outfit).Any(t => t == "hat." + tag || t == "visor." + tag);
            bool HasAnyTag(GamePlayer player, params string[] tags) => tags.Any(t => HasTag(player, t));

            if (HasTag(GamePlayer.LocalPlayer, "animal") && NebulaGameManager.Instance.AllPlayerInfo.Count(p => HasTag(p, "animal")) >= 5)
                new StaticAchievementToken("costume.animals");
            if (HasAnyTag(GamePlayer.LocalPlayer, "music.instrument", "music.conductor") && NebulaGameManager.Instance.AllPlayerInfo.Count(p => HasTag(p, "music.instrument")) >= 3 && NebulaGameManager.Instance.AllPlayerInfo.Count(p => HasTag(p, "music.conductor")) == 1)
                new StaticAchievementToken("costume.music");
            if (HasVisorTag(GamePlayer.LocalPlayer, "party") && ColorHelper.IsVividColor(DynamicPalette.PlayerColors[GamePlayer.LocalPlayer.PlayerId]))
            {
                var partyMembers = NebulaGameManager.Instance.AllPlayerInfo.Where(p => HasVisorTag(p, "party") && ColorHelper.IsVividColor(DynamicPalette.PlayerColors[p.PlayerId])).DistinctBy(p => p.DefaultOutfit.outfit.VisorId).ToArray();
                if(partyMembers.Length >= 3) new StaticAchievementToken("costume.party");
            }
        }
    }
}
