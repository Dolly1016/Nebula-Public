using BepInEx.Unity.IL2CPP.Utils;
using NAudio.MediaFoundation;
using Nebula.Behaviour;
using Nebula.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngineInternal.Video;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class DestroyerAssets
{
    static private Sprite?[] handAnimSprite = [null,null,null,null];
    static private float[] handAnimSampleTime = [0.05f, 0.2f, 0.8f, 0.9f];
    static public ISpriteLoader[] HandSprite = Helpers.Sequential(4).Select(num => new WrapSpriteLoader(() => {
        if (!handAnimSprite[num]) {
            var hand = PlayerControl.LocalPlayer.cosmetics.PettingHand;
            hand.PetClip.SampleAnimation(hand.gameObject, handAnimSampleTime[num]);
            handAnimSprite[num] = hand.HandSprite.sprite;
        }
        return handAnimSprite[num]!;
    })).ToArray();

}
public class Destroyer : ConfigurableStandardRole
{
    static public Destroyer MyRole = new Destroyer();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "destroyer";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player);

    private KillCoolDownConfiguration KillCoolDownOption = null!;
    private NebulaConfiguration KillSEStrengthOption = null!;
    private NebulaConfiguration PhasesOfDestroyingOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagBeginner, ConfigurationHolder.TagFunny);

        KillCoolDownOption = new (RoleConfig, "destroyCoolDown",KillCoolDownConfiguration.KillCoolDownType.Immediate, 2.5f,10f,60f,-30f,30f,0.125f,0.5f,5f,35f,10f,1.5f);
        PhasesOfDestroyingOption = new NebulaConfiguration(RoleConfig, "phasesOfDestroying", null, 3, 10, 3, 3);
        KillSEStrengthOption = new NebulaConfiguration(RoleConfig, "killSEStrength", null, 1f, 20f, 0.5f, 3.5f, 3.5f) { Decorator = NebulaConfiguration.OddsDecorator };
    }

    [NebulaRPCHolder]
    public class Instance : Impostor.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? destroyButton = null;
        public override AbstractRole Role => MyRole;
        public override bool HasVanillaKillButton => false;
        public Instance(PlayerModInfo player) : base(player)
        {
        }

        static private float DestroyKillDistance = 0.65f;
        static private bool CheckCanMove(PlayerControl myPlayer, Vector3 position, out float distance)
        {
            distance = myPlayer.transform.position.Distance(position);
            return !PhysicsHelpers.AnythingBetween(myPlayer.Collider, myPlayer.Collider.transform.position, position, Constants.ShipAndAllObjectsMask, false);
        }
        static private Vector3 GetDestroyKillPosition(Vector3 target,bool left)
        {
            return target + new Vector3(left ? -DestroyKillDistance : DestroyKillDistance, 0f, 0f);
        }
        static private bool CheckDestroyKill(PlayerControl myPlayer, Vector3 target)
        {
            return CheckCanMove(myPlayer, GetDestroyKillPosition(target,true), out _) || CheckCanMove(myPlayer, GetDestroyKillPosition(target, false), out _);
        }

        static private IDividedSpriteLoader spriteModBlood = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.DestroyerBlood.png",136f,6);
        static private ISpriteLoader spriteBloodPuddle = SpriteLoader.FromResource("Nebula.Resources.BloodPuddle.png",130f);

        static private IEnumerator CoDestroyKill(PlayerControl myPlayer, PlayerControl target, Vector3 targetPos, bool moveToLeft)
        {
            myPlayer.moveable = false;
            target.moveable = false;

            //キルされる相手は今の操作を中断させられる。
            if (target.AmOwner && Minigame.Instance)
            {
                try
                {
                    Minigame.Instance.Close();
                    Minigame.Instance.Close();
                }
                catch
                {
                }
            }

            //自身が動いている間は相手側もうまいこと動かそうとさせる。
            var myAnim = myPlayer.MyPhysics.WalkPlayerTo(GetDestroyKillPosition(targetPos, moveToLeft), 0.001f, 1f, false);
            var targetAnim = target.MyPhysics.WalkPlayerTo(targetPos, 0.001f, 1f, false);

            var targetCoroutine = NebulaManager.Instance.StartCoroutine(targetAnim);
            yield return myAnim;
            NebulaManager.Instance.StopCoroutine(targetCoroutine);
            
            target.NetTransform.SnapTo((Vector2)targetPos - target.Collider.offset);
            myPlayer.MyPhysics.body.velocity = Vector2.zero;
            target.MyPhysics.body.velocity = Vector2.zero;

            myPlayer.MyPhysics.FlipX = !moveToLeft;
            target.MyPhysics.FlipX = !moveToLeft;

            //キルモーション

            IEnumerator CoMonitorMeeting()
            {
                while (true)
                {
                    if (MeetingHud.Instance)
                    {
                        if (!target.Data.IsDead && myPlayer.AmOwner)
                        {
                            myPlayer.ModMeetingKill(target, false, PlayerState.Crushed, null, false);
                        }
                        yield break;
                    }
                    yield return null;
                }
            }


            Coroutine? monitorMeetingCoroutine = null;
            monitorMeetingCoroutine = NebulaManager.Instance.StartCoroutine(CoMonitorMeeting().WrapToIl2Cpp());

            if (myPlayer.AmOwner)
            {
                NebulaGameManager.Instance?.GameStatistics.RpcRecordEvent(GameStatistics.EventVariation.Kill, EventDetail.DestroyKill, myPlayer, target);
            }

            var handRenderer = UnityHelper.CreateObject<SpriteRenderer>("KillerHand", null, Vector3.zero, LayerExpansion.GetPlayersLayer());
            handRenderer.gameObject.AddComponent<PlayerColorRenderer>().SetPlayer(myPlayer.GetModInfo());
            handRenderer.sprite = DestroyerAssets.HandSprite[1].GetSprite();
            handRenderer.flipX = !moveToLeft;
            handRenderer.transform.localScale = new(0.5f, 0.5f, 1f);
            handRenderer.transform.localPosition = targetPos + new Vector3(moveToLeft ? -0.2f : 0.2f, 0.9f, -1f);

            yield return new WaitForSeconds(0.15f);

            //死体を生成
            var deadBody = GameObject.Instantiate<DeadBody>(GameManager.Instance.DeadBodyPrefab);
            deadBody.enabled = true;
            deadBody.ParentId = target.PlayerId;
            deadBody.transform.localPosition = target.GetTruePosition();
            foreach (var r in deadBody.bodyRenderers) r.enabled = false;
            var splatter = deadBody.bloodSplatter;
            target.SetPlayerMaterialColors(deadBody.bloodSplatter);
            splatter.gameObject.SetActive(false);

            var modSplatterRenderer = UnityHelper.CreateObject<SpriteRenderer>("ModSplatter", null, target.transform.position, splatter.gameObject.layer);
            modSplatterRenderer.sharedMaterial = splatter.sharedMaterial;
            var modSplatter = modSplatterRenderer.gameObject.AddComponent<ModAnimator>();

            var targetModInfo =  target.GetModInfo()!;

            IEnumerator CoScale(float startScale, float goalScale, float duration, NebulaAudioClip audioClip, bool playKillSE = false)
            {
                handRenderer.transform.localPosition = targetPos + new Vector3(moveToLeft ? -0.1f : 0.1f, startScale * 0.55f + 0.44f, -1f);
                handRenderer.sprite = DestroyerAssets.HandSprite[2].GetSprite();
                yield return new WaitForSeconds(0.15f);

                float scale = startScale;
                float p = 0f;

                handRenderer.sprite = DestroyerAssets.HandSprite[3].GetSprite();

                float randomX = 0f;
                float randomTimer = 0f;

                if (!MeetingHud.Instance) NebulaAsset.PlaySE(audioClip, target.transform.position, 1f, MyRole.KillSEStrengthOption.GetFloat(), 0.46f);

                int sePhase = 0;
                float[] seTime = [0.45f, 0.62f, 0.98f];

                while (p < 1f)
                {
                    scale = startScale + (goalScale - startScale) * p;
                    handRenderer.transform.localPosition = targetPos + new Vector3(randomX + (moveToLeft ? -0.15f : 0.15f), scale * 0.55f + 0.4f, -1f);
                    if(!targetModInfo.IsDead)targetModInfo.PlayerScaler.transform.localScale = new(1f, scale, 1f);

                    randomTimer -= Time.deltaTime;
                    if(randomTimer < 0f)
                    {
                        randomX = ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.07f;
                        randomTimer = 0.05f;
                    }

                    if (playKillSE && !MeetingHud.Instance)
                    {
                        if(sePhase < seTime.Length && p > seTime[sePhase])
                        {
                            if(sePhase < 2)
                            {
                                //血を出す
                                splatter.gameObject.SetActive(false);
                                deadBody.transform.localScale = new(sePhase == 0 ? 0.7f : -0.7f, 0.7f, 0.7f);
                                splatter.gameObject.SetActive(true);
                            }
                            else if(sePhase == 2)
                            {
                                modSplatter.PlayOneShot(spriteModBlood, 12f,true);
                            }

                            NebulaAsset.PlaySE(target.KillSfx, target.transform.position + new Vector3(((float)System.Random.Shared.NextDouble() - 0.5f) * 0.05f, ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.05f, 0f), 1f, 1.8f, 0.45f);
                            sePhase++;
                        }
                    }

                    p += Time.deltaTime / duration;

                    yield return null;
                }

                targetModInfo.PlayerScaler.transform.localScale = new(1f, goalScale, 1f);
                handRenderer.transform.localPosition = targetPos + new Vector3(moveToLeft ? -0.12f : 0.12f, startScale * 0.55f + 0.21f, -1f);
                handRenderer.sprite = DestroyerAssets.HandSprite[2].GetSprite();

            }

            int phases = MyRole.PhasesOfDestroyingOption.GetMappedInt() - 1;
            for (int i = 0;i < phases; i++)
            {
                yield return CoScale(
                    1f - 0.4f / phases * i,
                    1f - 0.4f / phases * (i + 1),
                    1.2f, (i % 2 == 0) ? NebulaAudioClip.Destroyer1 : NebulaAudioClip.Destroyer2);
                yield return new WaitForSeconds(0.7f);
            }
            
            yield return CoScale(0.6f, 0f, 3.2f, NebulaAudioClip.Destroyer3, true);

            if (myPlayer.AmOwner && !target.Data.IsDead)
            {
                myPlayer.ModMeetingKill(target, true, PlayerState.Crushed, null, false);
            }
            NebulaManager.Instance.StopCoroutine(monitorMeetingCoroutine);

            var bloodRenderer = UnityHelper.CreateObject<SpriteRenderer>("DestroyerBlood", null, (targetPos + new Vector3(0f,0.1f,0f)).AsWorldPos(true));
            bloodRenderer.sprite = spriteBloodPuddle.GetSprite();
            bloodRenderer.color = Palette.PlayerColors[target.CurrentOutfit.ColorId];
            bloodRenderer.transform.localScale = new(0.45f,0.45f,1f);

            GameObject.Destroy(handRenderer.gameObject);
            if(deadBody) GameObject.Destroy(deadBody.gameObject);

            myPlayer.moveable = true;
            target.moveable = true;
            
            //こちらの目線で死ぬまで待つ
            while (!targetModInfo.IsDead) yield return null;
            yield return new WaitForSeconds(0.2f);
            targetModInfo.PlayerScaler.localScale = new(1f, 1f, 1f);

            //血しぶきを片付ける
            yield return new WaitForSeconds(0.5f);
            GameObject.Destroy(modSplatter.gameObject);
        }

        private PlayerModInfo? lastKilling = null;

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                AchievementToken<int> achChallengeToken = new("destroyer.challenge", 0, (val, _) => val >= 3 && (NebulaGameManager.Instance?.EndState?.CheckWin(MyPlayer.PlayerId) ?? false));

                ObjectTracker<PlayerControl> killTracker = Bind(ObjectTrackers.ForPlayer(null, MyPlayer.MyControl,ObjectTrackers.ImpostorKillPredicate, p => CheckDestroyKill(MyPlayer.MyControl, p.transform.position)));
                destroyButton = Bind(new ModAbilityButton(false,true)).KeyBind(Virial.Compat.VirtualKeyInput.Kill);
                destroyButton.Availability = (button) => MyPlayer.MyControl.CanMove && killTracker.CurrentTarget != null;
                destroyButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                destroyButton.OnClick = (button) => {
                    //左右どちらでキルすればよいか考える
                    var targetTruePos = killTracker.CurrentTarget!.GetTruePosition();
                    var targetPos = killTracker.CurrentTarget!.transform.position;
                    var canMoveToLeft = CheckCanMove(MyPlayer.MyControl, GetDestroyKillPosition(targetPos, true), out var leftDis);
                    var canMoveToRight = CheckCanMove(MyPlayer.MyControl, GetDestroyKillPosition(targetPos, false), out var rightDis);
                    bool moveToLeft = false;
                    if (canMoveToLeft && canMoveToRight && leftDis < rightDis) moveToLeft = true;
                    else if (!canMoveToRight) moveToLeft = true;

                    lastKilling = killTracker.CurrentTarget.GetModInfo();

                    RpcCoDestroyKill.Invoke((MyPlayer, killTracker.CurrentTarget!.GetModInfo()!, targetTruePos, moveToLeft));

                    new StaticAchievementToken("destroyer.common1");
                    new StaticAchievementToken("destroyer.common2");
                    achChallengeToken.Value++;

                    destroyButton.StartCoolDown();
                };
                destroyButton.OnMeeting = button => button.StartCoolDown();
                destroyButton.CoolDownTimer = Bind(new Timer(MyRole.KillCoolDownOption.CurrentCoolDown).SetAsKillCoolDown().Start());
                destroyButton.SetLabel("destroyerKill");
                destroyButton.SetLabelType(Virial.Components.AbilityButton.LabelType.Impostor);
            }
        }

        void IGameEntity.OnReported(Virial.Game.Player reporter, Virial.Game.Player reported)
        {
            if (AmOwner && lastKilling != null && reported == lastKilling) new StaticAchievementToken("destroyer.another1");
        }

        static public RemoteProcess<(PlayerModInfo player, PlayerModInfo target, Vector2 targetPosition, bool moveToLeft)> RpcCoDestroyKill = new(
            "DestroyerKill",
            (message, _) =>
            {
                NebulaManager.Instance.StartCoroutine(CoDestroyKill(message.player.MyControl, message.target.MyControl,message.targetPosition,message.moveToLeft).WrapToIl2Cpp());
            }
            );
    }
}
