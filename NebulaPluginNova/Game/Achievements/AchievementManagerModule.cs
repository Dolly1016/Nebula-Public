using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Roles.Modifier;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Game.Achievements;

[NebulaPreprocessForNoS(PreprocessPhaseForNoS.BuildNoSModule)]
internal class AchievementManagerModule : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static AchievementManagerModule()
    {
        DIManager.Instance.RegisterModule(() => new AchievementManagerModule());
    }

    private AchievementManagerModule()
    {
        this.Register(NebulaGameManager.Instance!);
    }
     
    void OnGameStart(GameStartEvent ev)
    {
        //実績
        new AchievementToken<int>("challenge.death2", 0, (exileAnyone, _) => (NebulaGameManager.Instance!.AllPlayerInfo().Where(p => p.IsDead && p.MyKiller == NebulaGameManager.Instance.LocalPlayerInfo && p != NebulaGameManager.Instance.LocalPlayerInfo).Select(p => p.PlayerState).Distinct().Count() + exileAnyone) >= 4);
        if (Helpers.CurrentMonth == 3) new AchievementToken<int>("graduation2", 0, (exileAnyone, _) => exileAnyone >= 3);

        if (Helpers.CurrentMonth == 5 && (AmongUsUtil.CurrentMapId is 1 or 4))
        {
            if (NebulaGameManager.Instance!.LocalPlayerInfo.Unbox().TryGetModifier<Lover.Instance>(out var lover))
            {
                var myLover = lover.MyLover;
                float time = 0f;
                bool isCleared = false;
                new NebulaGameScript()
                {
                    OnUpdateEvent = () =>
                    {
                        if (isCleared) return;

                        if (!lover.IsDeadObject && !NebulaGameManager.Instance.LocalPlayerInfo.IsDead && !myLover!.IsDead)
                        {
                            if (
                                NebulaGameManager.Instance.LocalPlayerInfo.HasAttribute(PlayerAttributes.Accel) &&
                                myLover.HasAttribute(PlayerAttributes.Accel) &&
                                NebulaGameManager.Instance.LocalPlayerInfo.VanillaPlayer.MyPhysics.Velocity.magnitude > 0f &&
                                NebulaGameManager.Instance.LocalPlayerInfo.VanillaPlayer.transform.position.Distance(myLover.VanillaPlayer.transform.position) < 2f
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
                    }
                };
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
                new NebulaGameScript()
                {
                    OnUpdateEvent = () =>
                    {
                        if (isCleared) return;

                        if (!NebulaGameManager.Instance!.LocalPlayerInfo.IsDead)
                        {
                            if (
                                NebulaGameManager.Instance.LocalPlayerInfo.HasAttribute(attr) &&
                                NebulaGameManager.Instance.LocalPlayerInfo.VanillaPlayer.MyPhysics.Velocity.magnitude > 0f
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
                    }
                };
            }
        }
    }
}
