using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using static Sentry.MeasurementUnit;

namespace Nebula.Roles.Perks;

[NebulaRPCHolder]
internal class Meteor : PerkFunctionalInstance
{
    static PerkFunctionalDefinition Def = new("meteor", PerkFunctionalDefinition.Category.NoncrewmateOnly, new PerkDefinition("meteor", 4, 55, Virial.Color.ImpostorColor, Virial.Color.ImpostorColor), (def, instance) => new Meteor(def, instance));

    public Meteor(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        PerkInstance.BindTimer(NebulaAPI.Modules.Timer(this, 10f)).MyTimer?.Start();
    }
    void OnMeetingEnd(TaskPhaseStartEvent ev)
    {
        PerkInstance.MyTimer?.Start();
    }
    bool CanUse => !used && !MyPlayer.IsDead && MyPlayer.CanMove && !(PerkInstance.MyTimer?.IsProgressing ?? false) && MyPlayer.Role.Role != Neutral.Vulture.MyRole;
    void OnUpdate(GameHudUpdateEvent ev) => PerkInstance.SetDisplayColor(CanUse ? Color.white : Color.gray);
    

    private bool used = false;
    public override bool HasAction => true;

    static IDividedSpriteLoader ExplosionSprite = DividedSpriteLoader.FromResource("Nebula.Resources.ExplosionAnim.png", 100f, 4, 2);
    static IDividedSpriteLoader MeteorSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Meteor.png", 100f, 2, 1).SetPivot(new(0f,0f));
    public override void OnClick()
    {
        if (!CanUse) return;

        used = true;
        RpcMeteor.Invoke((MyPlayer.Position, MyPlayer, System.Random.Shared.NextSingle() * 360f));

        new StaticAchievementToken("perk.manyPerks1.meteor");
        int killed = 0;
        GameOperatorManager.Instance?.Subscribe<PlayerKillPlayerEvent>(ev =>
        {
            if (ev.Murderer.AmOwner && !ev.Dead.AmOwner && ev.Dead.PlayerState == PlayerState.Meteor)
            {
                killed++;
                if(killed == 3)new StaticAchievementToken("perk.meteor");
            }
        }, FunctionalLifespan.GetTimeLifespan(FireDelay + AfterFireDuration));

    }

    const float FireDelay = 10f;
    const float FireSilentDelay = 3f;
    const float AfterFireDuration = 4f;
    const float ExplosionSize = 3f;
    static private void PlayMeteor(GamePlayer invoker, Vector2 pos, float angle)
    {
        List<Vector2> positionList = [];

        IEnumerator CoAlert()
        {
            bool even = false;
            int slowEven = 0;

            yield return Effects.Wait(FireSilentDelay);

            var duration = FireDelay - FireSilentDelay;

            GameOperatorManager.Instance?.Subscribe<PlayerTaskTextLocalEvent>(ev => {
                even = !even;
                slowEven = (slowEven + 1) % 6;
                ev.AppendText(Language.Translate("perk.meteor.sabotageText").Color(even ? Color.red : Color.yellow));
            }, FunctionalLifespan.GetTimeLifespan(duration + AfterFireDuration));
            GameOperatorManager.Instance?.Subscribe<CheckCanPushEmergencyButtonEvent>(ev => ev.DenyButton("perk.meteor.meetingButtonText"), FunctionalLifespan.GetTimeLifespan(duration + AfterFireDuration));

            var player = GamePlayer.LocalPlayer;
            float alertTime = 0f;

            var fullscreen = AmongUsUtil.GenerateFullscreen(Palette.ImpostorRed.AlphaMultiplied(0.5f));
            fullscreen.gameObject.SetActive(false);

            float addition = 3f;
            duration += addition;

            while (duration > 0f)
            {
                alertTime -= Time.deltaTime;
                duration -= Time.deltaTime;

                if (MeetingHud.Instance) break;

                float currentAlertSize = Mathf.Lerp(1f, 3f, Mathf.Max(0f, duration - addition) / FireDelay) * ExplosionSize;
                bool inAlertArea = positionList.Any(p => player.Position.Distance(p) < currentAlertSize);
                
                if (alertTime < 0f && duration > addition + 1f)
                {
                    NebulaAsset.PlaySE(NebulaAudioClip.MeteorAlert, true);
                    alertTime = 1.65f;
                }

                fullscreen.gameObject.SetActive(((slowEven < 3) || duration < addition) && inAlertArea && !player!.IsDead);

                yield return null;
            }

            GameObject.Destroy(fullscreen.gameObject);
        }

        IEnumerator CoAnim(float delay, Vector2 pos, Vector2[] anotherPos, NebulaAudioClip farAudio)
        {
            positionList.Add(pos);

            delay += FireDelay;

            if (NebulaGameManager.Instance!.CanSeeAllInfo || invoker.AmOwner)
            {
                while (delay > 0.8f)
                {
                    var circle = EffectCircle.SpawnEffectCircle(null, pos.AsVector3(-10f), Palette.ImpostorRed, ExplosionSize, null, true);
                    yield return Effects.Wait(0.8f);
                    circle.Disappear();
                    delay -= 0.8f;
                }
            }

            yield return Effects.Wait(delay);

            if (MeetingHud.Instance) yield break;

            var explosion = UnityHelper.CreateObject<SpriteRenderer>("Explosion", null, pos.AsVector3(-10f));
            for (int i = 0; i < 2; i++)
            {
                explosion.sprite = MeteorSprite.GetSprite(i);
                yield return Effects.Wait(0.05f);
            }

            var localPlayer = GamePlayer.LocalPlayer;
            var playerPos = localPlayer.Position;
            float distance = playerPos.Distance(pos);
            if (anotherPos.Length == 0 || distance < anotherPos.Min(p => playerPos.Distance(p)))
            {
                //他の候補地が無い、あるいは他のどの候補地よりもプレイヤーに近い場合
                if (distance < 8f) NebulaAsset.PlaySE(NebulaAudioClip.ExplosionNear, pos, 4f, 12f);
                else NebulaAsset.PlaySE(farAudio, pos, 11f, 25f);
            }

            //被弾した場合
            if(distance < ExplosionSize && !localPlayer.IsDead && !localPlayer.VanillaPlayer.inVent && !localPlayer.IsDived)
            {
                invoker.MurderPlayer(localPlayer, PlayerState.Meteor, EventDetail.Meteor, Virial.Game.KillParameter.WithAssigningGhostRole | Virial.Game.KillParameter.WithDeadBody, KillCondition.TargetAlive, result =>
                {
                    if (result == Virial.Game.KillResult.Kill && Constants.ShouldPlaySfx()) NebulaManager.Instance.StartDelayAction(0.8f, () => Helpers.PlayKillStingerSE());
                });
            }

            explosion.transform.localScale = Vector3.one * 1.8f;
            explosion.transform.localEulerAngles = new(0f, 0f, System.Random.Shared.NextSingle() * 360f);
            for (int i = 0; i < 8; i++)
            {
                explosion.sprite = ExplosionSprite.GetSprite(i);
                yield return Effects.Wait(0.12f);
            }

            GameObject.Destroy(explosion.gameObject);
        }

        NebulaManager.Instance.StartCoroutine(CoAnim(0f, pos, [], NebulaAudioClip.ExplosionFar1).WrapToIl2Cpp());
        for (int i = 0; i < 6; i++)
        {
            NebulaManager.Instance.StartCoroutine(CoAnim(i % 2 == 0 ? 0.55f : 1.1f, pos + (Vector2.right * 4.4f).Rotate(60f * i + angle), [
                pos + (Vector2.right * 4.4f).Rotate(60f * (i + 2) + angle), pos + (Vector2.right * 4.4f).Rotate(60f * (i + 4) + angle)
                ], i % 2 == 0 ? NebulaAudioClip.ExplosionFar2 : NebulaAudioClip.ExplosionFar1).WrapToIl2Cpp());
            NebulaManager.Instance.StartCoroutine(CoAnim(1.65f + i * 0.23f, pos + (Vector2.right * 4.4f * Mathf.Sqrt(3)).Rotate(90f + 60f * i + angle), [], i % 2 == 0 ? NebulaAudioClip.ExplosionFar2 : NebulaAudioClip.ExplosionFar1).WrapToIl2Cpp());
        }
        NebulaManager.Instance.StartCoroutine(CoAlert().WrapToIl2Cpp());
    }

    static private RemoteProcess<(Vector2 pos, GamePlayer invoker, float angle)> RpcMeteor = new("Meteor", (message, _) =>
    {
        PlayMeteor(message.invoker, message.pos, message.angle);
    });
}