using Hazel.Dtls;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using UnityEngine.SceneManagement;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;
using static UnityEngine.ProBuilder.AutoUnwrapSettings;

namespace Nebula.Roles.Impostor;

public class DestroyerAssets
{
    static private Sprite?[] handAnimSprite = [null,null,null,null];
    static private float[] handAnimSampleTime = [0.05f, 0.2f, 0.8f, 0.9f];
    static public Image[] HandSprite = Helpers.Sequential(4).Select(num => new WrapSpriteLoader(() => {
        if (!handAnimSprite[num]) {
            var hand = PlayerControl.LocalPlayer.cosmetics.PettingHand;
            hand.PetClip.SampleAnimation(hand.gameObject, handAnimSampleTime[num]);
            handAnimSprite[num] = hand.HandSprite.sprite;
        }
        return handAnimSprite[num]!;
    })).ToArray();

}

public class Destroyer : DefinedSingleAbilityRoleTemplate<Destroyer.Ability>, DefinedRole
{
    private Destroyer() : base("destroyer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [KillCoolDownOption, PhasesOfDestroyingOption, KillSEStrengthOption,LeaveKillEvidenceOption, CanReportKillSceneOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagBeginner);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Destroyer.png");
    }

    static private readonly IRelativeCoolDownConfiguration KillCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.destroyer.destroyCoolDown", CoolDownType.Immediate, (0f, 60f, 2.5f), 35f, (-30f, 30f, 2.5f), 10f, (0.5f, 5f, 0.125f), 1.5f);
    static private readonly IntegerConfiguration PhasesOfDestroyingOption = NebulaAPI.Configurations.Configuration("options.role.destroyer.phasesOfDestroying", (1, 10), 3, decorator: val => val + ($" ({string.Format("{0:#.#}",3.2f + (2.05f * (val - 1)))}{Language.Translate("options.sec")})").Color(Color.gray));
    static private readonly FloatConfiguration KillSEStrengthOption = NebulaAPI.Configurations.Configuration("options.role.destroyer.killSEStrength", (1f,20f,0.5f),3.5f, FloatConfigurationDecorator.Ratio);
    static private readonly BoolConfiguration LeaveKillEvidenceOption = NebulaAPI.Configurations.Configuration("options.role.destroyer.leaveKillEvidence", true);
    static private readonly BoolConfiguration CanReportKillSceneOption = NebulaAPI.Configurations.Configuration("options.role.destroyer.canReportKillScene", true);
    static public readonly Destroyer MyRole = new();
    static private readonly GameStatsEntry StatsReported = NebulaAPI.CreateStatsEntry("stats.destroyer.reported", GameStatsCategory.Roles, MyRole);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    [NebulaRPCHolder]
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private float DestroyKillDistance = 0.65f;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        ModAbilityButton? destroyButton = null;
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped) {
            if (AmOwner)
            {
                AchievementToken<int> achChallengeToken = new("destroyer.challenge", 0, (val, _) => val >= 3 && (NebulaGameManager.Instance?.EndState?.Winners.Test(MyPlayer) ?? false));

                destroyButton = NebulaAPI.Modules.KillButton(this, MyPlayer, true, Virial.Compat.VirtualKeyInput.Kill, "destroyer.kill",
                   KillCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "destroyerKill", Virial.Components.ModAbilityButton.LabelType.Impostor,
                   null, (player, _) =>
                   {
                       //左右どちらでキルすればよいか考える
                       var targetTruePos = player.VanillaPlayer.GetTruePosition();
                       var targetPos = player.VanillaPlayer.transform.position;
                       var canMoveToLeft = CheckCanMove(MyPlayer.VanillaPlayer, GetDestroyKillPosition(targetPos, true), out var leftDis);
                       var canMoveToRight = CheckCanMove(MyPlayer.VanillaPlayer, GetDestroyKillPosition(targetPos, false), out var rightDis);
                       bool moveToLeft = false;
                       if (canMoveToLeft && canMoveToRight && leftDis < rightDis) moveToLeft = true;
                       else if (!canMoveToRight) moveToLeft = true;

                       lastKilling = player;

                       RpcCoDestroyKill.Invoke((MyPlayer, player!, targetTruePos, moveToLeft));

                       new StaticAchievementToken("destroyer.common1");
                       new StaticAchievementToken("destroyer.common2");
                       achChallengeToken.Value++;

                       NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                   }, filterHeavier: p => CheckDestroyKill(MyPlayer.VanillaPlayer, p.VanillaPlayer.transform.position))
                    .SetAsUsurpableButton(this);
                destroyButton.OnBroken = _ => Snatcher.RewindKillCooldown();
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(destroyButton.GetKillButtonLike());
            }
        }
        bool IPlayerAbility.HideKillButton => !(destroyButton?.IsBroken ?? false); 

        static private bool CheckCanMove(PlayerControl myPlayer, Vector3 position, out float distance)
        {
            distance = myPlayer.transform.position.Distance(position);
            return !PhysicsHelpers.AnythingBetween(myPlayer.Collider, myPlayer.Collider.transform.position, position, Constants.ShipAndAllObjectsMask, false);
        }
        static private Vector3 GetDestroyKillPosition(Vector3 target, bool left)
        {
            return target + new Vector3(left ? -DestroyKillDistance : DestroyKillDistance, 0f, 0f);
        }
        static private bool CheckDestroyKill(PlayerControl myPlayer, Vector3 target)
        {
            return CheckCanMove(myPlayer, GetDestroyKillPosition(target, true), out _) || CheckCanMove(myPlayer, GetDestroyKillPosition(target, false), out _);
        }

        static private IDividedSpriteLoader spriteModBlood = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.DestroyerBlood.png", 136f, 6);
        static private Image spriteBloodPuddle = SpriteLoader.FromResource("Nebula.Resources.BloodPuddle.png", 130f);
        private const string destroyerAttrTag = "nebula::destroyer";
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
            try
            {
                NebulaManager.Instance.StopCoroutine(targetCoroutine);
            }
            catch { }

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
                            myPlayer.GetModInfo()?.MurderPlayer(target.GetModInfo()!, PlayerState.Crushed, null, KillParameter.WithAssigningGhostRole);
                        }
                        yield break;
                    }
                    yield return null;
                }
            }

            SizeModulator sizeModulator = new(Vector2.one, 10000f, false, 100, destroyerAttrTag, false, false);
            PlayerModInfo.RpcAttrModulator.LocalInvoke((target.PlayerId, sizeModulator, true));

            Coroutine monitorMeetingCoroutine = NebulaManager.Instance.StartCoroutine(CoMonitorMeeting().WrapToIl2Cpp());

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
            DeadBody deadBody = GameObject.Instantiate<DeadBody>(GameManager.Instance.DeadBodyPrefab);
            GameObject deadBodyObj = deadBody.gameObject;
            deadBody.enabled = CanReportKillSceneOption;
            deadBody.Reported = !CanReportKillSceneOption;
            deadBody.ParentId = target.PlayerId;
            deadBody.transform.localPosition = target.GetTruePosition();
            foreach (var r in deadBody.bodyRenderers) r.enabled = false;
            var splatter = deadBody.bloodSplatter;
            target.SetPlayerMaterialColors(deadBody.bloodSplatter);
            splatter.gameObject.SetActive(false);

            var modSplatterRenderer = UnityHelper.CreateObject<SpriteRenderer>("ModSplatter", null, target.transform.position, splatter.gameObject.layer);
            modSplatterRenderer.sharedMaterial = splatter.sharedMaterial;
            var modSplatter = modSplatterRenderer.gameObject.AddComponent<ModAnimator>();

            var targetModInfo = target.GetModInfo()!;
            target.Visible = true;
            target.inVent = false;
            targetModInfo.Unbox().WillDie = true;

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

                if (!MeetingHud.Instance) NebulaAsset.PlaySE(audioClip, target.transform.position, 0.8f, KillSEStrengthOption, 1f);

                int sePhase = 0;
                float[] seTime = [0.45f, 0.62f, 0.98f];

                while (p < 1f)
                {
                    scale = startScale + (goalScale - startScale) * p;
                    handRenderer.transform.localPosition = targetPos + new Vector3(randomX + (moveToLeft ? -0.15f : 0.15f), scale * 0.55f + 0.4f, -1f);
                    if (!targetModInfo.IsDead) sizeModulator.Size.y = scale;

                    randomTimer -= Time.deltaTime;
                    if (randomTimer < 0f)
                    {
                        randomX = ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.07f;
                        randomTimer = 0.05f;
                    }

                    if (playKillSE && !MeetingHud.Instance)
                    {
                        if (sePhase < seTime.Length && p > seTime[sePhase])
                        {
                            if (sePhase < 2)
                            {
                                //血を出す
                                splatter.gameObject.SetActive(false);
                                deadBody.transform.localScale = new(sePhase == 0 ? 0.7f : -0.7f, 0.7f, 0.7f);
                                splatter.gameObject.SetActive(true);
                            }
                            else if (sePhase == 2)
                            {
                                modSplatter.PlayOneShot(spriteModBlood, 12f, true);
                            }

                            NebulaAsset.PlaySE(target.KillSfx, target.transform.position + new Vector3(((float)System.Random.Shared.NextDouble() - 0.5f) * 0.05f, ((float)System.Random.Shared.NextDouble() - 0.5f) * 0.05f, 0f), 0.6f, 1.4f, 1f);
                            sePhase++;
                        }
                    }

                    p += Time.deltaTime / duration;

                    yield return null;
                }

                sizeModulator.Size.y = goalScale;
                handRenderer.transform.localPosition = targetPos + new Vector3(moveToLeft ? -0.12f : 0.12f, startScale * 0.55f + 0.21f, -1f);
                handRenderer.sprite = DestroyerAssets.HandSprite[2].GetSprite();

            }

            int phases = PhasesOfDestroyingOption - 1;

            for (int i = 0; i < phases; i++)
            {
                yield return CoScale(
                    1f - 0.4f / phases * i,
                    1f - 0.4f / phases * (i + 1),
                    1.2f, (i % 2 == 0) ? NebulaAudioClip.Destroyer1 : NebulaAudioClip.Destroyer2);
                yield return new WaitForSeconds(0.7f);
            }

            yield return CoScale(phases == 0 ? 1f : 0.6f, 0f, 3.2f, NebulaAudioClip.Destroyer3, true);

            if (myPlayer.AmOwner && !target.Data.IsDead)
            {
                myPlayer.GetModInfo()?.MurderPlayer(target.GetModInfo()!, PlayerState.Crushed, null, KillParameter.WithOverlay | KillParameter.WithAssigningGhostRole, KillCondition.TargetAlive);
            }

            try
            {
                NebulaManager.Instance.StopCoroutine(monitorMeetingCoroutine);
            }
            catch
            {
                //会議に入って停止に失敗しても何もしない
            }

            if (LeaveKillEvidenceOption)
            {
                var bloodRenderer = UnityHelper.CreateObject<SpriteRenderer>("DestroyerBlood", null, (targetPos + new Vector3(0f, 0.1f, 0f)).AsWorldPos(true));
                bloodRenderer.sprite = spriteBloodPuddle.GetSprite();
                bloodRenderer.color = Palette.PlayerColors[target.CurrentOutfit.ColorId];
                bloodRenderer.transform.localScale = new(0.45f, 0.45f, 1f);
            }

            GameObject.Destroy(handRenderer.gameObject);
            if (deadBodyObj) GameObject.Destroy(deadBodyObj);

            myPlayer.moveable = true;

            //こちらの目線で死ぬまで待つ
            while (!targetModInfo.IsDead) yield return null;

            target.moveable = true;
            target.inVent = false;
            target.onLadder = false;
            target.inMovingPlat = false;

            yield return new WaitForSeconds(0.2f);

            PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((targetModInfo.PlayerId, destroyerAttrTag));

            //血しぶきを片付ける
            yield return new WaitForSeconds(0.5f);
            GameObject.Destroy(modSplatter.gameObject);
        }

        private GamePlayer? lastKilling = null;

        [Local]
        void OnReported(ReportDeadBodyEvent ev)
        {
            if (AmOwner && lastKilling != null && ev.Reported == lastKilling)
            {
                new StaticAchievementToken("destroyer.another1");
                StatsReported.Progress();
            }
        }

        static internal readonly RemoteProcess<(GamePlayer player, GamePlayer target, Vector2 targetPosition, bool moveToLeft)> RpcCoDestroyKill = new(
            "DestroyerKill",
            (message, _) =>
            {
                NebulaManager.Instance.StartCoroutine(CoDestroyKill(message.player.VanillaPlayer, message.target.VanillaPlayer, message.targetPosition, message.moveToLeft).WrapToIl2Cpp());
            }
            );
    }
}
