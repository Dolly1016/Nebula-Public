using Nebula.Behavior;
using Virial;
using Virial.Components;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Roles.Perks;

[NebulaRPCHolder]
internal class HandOfMercy : PerkFunctionalInstance
{
    const float CoolDown = 10f;
    static internal PerkFunctionalDefinition Def = new("handOfMercy", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("handOfMercy", 5, 61, new(255, 182, 193)).CooldownText("%CD%", () => CoolDown), (def, instance) => new HandOfMercy(def, instance));

    const float StrokeDistance = 0.65f;
    static readonly Vector2 HandOffset = new(0.1f, 0.95f);
    const float HandScale = 0.5f;
    const float MaxStrokeDuration = 5f;
    const float StrokeSEDelay = 0.5f;
    const float IntroRaiseDuration = 0.15f;
    const float IntroLowerDuration = 0.15f;
    const float IntroScareDuration = 0.3f;
    const int StrokeStartFrame = 6;
    const int StrokesForTaskProgress = 3;

    private GameTimer cooldownTimer;
    private ObjectTracker<GamePlayer> tracker;
    private List<(byte playerId, PoolablePlayer icon)> strokedPlayers = [];

    const float StrokedIconScale = 0.23f;
    const float StrokedIconY = 0.7f;
    const float StrokedIconInterval = 0.41f;
    const float StrokedIconZ = -1.0f;

    static private HandOfMercy? localStrokingPerk = null;

    private HandOfMercy(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        cooldownTimer = NebulaAPI.Modules.Timer(this, CoolDown);
        cooldownTimer.Start();
        PerkInstance.BindTimer(cooldownTimer);

        tracker = ObjectTrackers.ForPlayer(this, null, MyPlayer, ObjectTrackers.StandardPredicate, p => CheckCanStroke(MyPlayer.VanillaPlayer, MyPlayer.Position.ToUnityVector(), p.Position.ToUnityVector()), new(1f, 0.6f, 0.75f));
    }

    public override bool HasAction => true;
    public override void OnClick()
    {
        if (cooldownTimer.IsProgressing || !MyPlayer.CanMove || MyPlayer.IsDead) return;
        var target = tracker.CurrentTarget;
        if (target == null) return;

        // 撫でる向きを決定する
        Vector2 targetPos = target.Position.ToUnityVector();
        Vector2 myPos = MyPlayer.Position.ToUnityVector();
        var canMoveToLeft = CheckCanMove(MyPlayer.VanillaPlayer, myPos, GetStrokePosition(targetPos, true), out var leftDis);
        var canMoveToRight = CheckCanMove(MyPlayer.VanillaPlayer, myPos, GetStrokePosition(targetPos, false), out var rightDis);
        bool moveToLeft = false;
        if (canMoveToLeft && canMoveToRight && leftDis < rightDis) moveToLeft = true;
        else if (!canMoveToRight) moveToLeft = true;

        localStrokingPerk = this;
        RpcCoStroke.Invoke((MyPlayer, target, targetPos, moveToLeft));
        cooldownTimer.Start();
    }

    private void OnStroked(GamePlayer target)
    {
        if (IsDeadObject) return;
        if (strokedPlayers.Any(p => p.playerId == target.PlayerId)) return;

        if (strokedPlayers.Count + 1 < StrokesForTaskProgress)
        {
            var icon = AmongUsUtil.GetPlayerIcon(target.Unbox().DefaultOutfit, PerkInstance.Button.transform, Vector3.zero, Vector3.one * StrokedIconScale);
            icon.ToggleName(false);
            strokedPlayers.Add((target.PlayerId, icon));
            UpdateStrokedIcons();
            return;
        }

        ClearStrokedPlayers();
        ProgressTask();
    }

    private void UpdateStrokedIcons()
    {
        for (int i = 0; i < strokedPlayers.Count; i++)
        {
            float x = (i - (strokedPlayers.Count - 1) * 0.5f) * StrokedIconInterval;
            strokedPlayers[i].icon.transform.localPosition = new(x, StrokedIconY, StrokedIconZ - i * 0.01f);
        }
    }

    private void ClearStrokedPlayers()
    {
        foreach (var p in strokedPlayers) if (p.icon) GameObject.Destroy(p.icon.gameObject);
        strokedPlayers.Clear();
    }

    private void ProgressTask()
    {
        if (!MyPlayer.Tasks.IsCrewmateTask || !MyPlayer.Tasks.HasExecutableTasks) return;
        MyPlayer.Tasks.Unbox().GainTotalCompleted();

        if (AmongUsLLImpl.HudManagerInstance.AsBoolFast(out var hud)) hud.ShowTaskComplete();
    }

    void OnUpdate(GameHudUpdateEvent ev)
    {
        PerkInstance.SetDisplayColor((cooldownTimer.IsProgressing || tracker.CurrentTarget == null) ? Color.gray : Color.white);
    }

    static private bool CheckCanMove(PlayerControl myPlayer, Vector2 myPlayerPosition, Vector2 position, out float distance)
    {
        distance = myPlayerPosition.Distance(position);
        return !PhysicsHelpers.AnythingBetween(myPlayer.Collider, myPlayer.Collider.transform.GetPositionFast(), position, Constants.ShipAndAllObjectsMask, false);
    }
    static private Vector2 GetStrokePosition(Vector2 target, bool left) => target + new Vector2(left ? -StrokeDistance : StrokeDistance, 0f);
    static private bool CheckCanStroke(PlayerControl myPlayer, Vector2 myPos, Vector2 target)
        => CheckCanMove(myPlayer, myPos, GetStrokePosition(target, true), out _) || CheckCanMove(myPlayer, myPos, GetStrokePosition(target, false), out _);

    static private PlayerPettingHand SpawnHand(PlayerControl myPlayer, PlayerControl target, Vector2 targetPos, bool moveToLeft)
    {
        var original = myPlayer.cosmetics.PettingHand;
        var hand = GameObject.Instantiate(original, null);
        hand.name = "MercifulHand";
        hand.StopAllCoroutines();
        hand.gameObject.SetActive(true);
        hand.transform.localRotation = Quaternion.identity;

        hand.transform.localScale = original.transform.lossyScale;
        hand.PetClip.SampleAnimation(hand.gameObject, 0f);
        float factor = HandScale / Mathf.Abs(hand.HandSprite.transform.lossyScale.x);
        var scale = hand.transform.localScale * factor;
        scale.x = Mathf.Abs(scale.x) * (moveToLeft ? 1f : -1f);
        hand.transform.localScale = scale;

        hand.transform.position = Vector3.zero;
        Vector2 spriteOffset = hand.HandSprite.transform.position;
        Vector2 handPos = targetPos + new Vector2(moveToLeft ? -HandOffset.x : HandOffset.x, HandOffset.y) - spriteOffset;
        hand.transform.position = new Vector3(handPos.x, handPos.y, target.transform.position.z - 0.01f);

        hand.HandSprite.gameObject.AddComponent<PlayerColorRenderer>().SetPlayer(myPlayer.GetModInfo());

        return hand;
    }

    static AudioClip doneSfx = null;
    static private void PlayStrokeSE()
    {
        if (!Constants.ShouldPlaySfx()) return;
        if(!doneSfx.AsBoolFast()) doneSfx = VanillaAsset.MapAsset[5].CommonTasks[3].MinigamePrefab.CastFast<MultistageMinigame>().Stages[1].CastFast<RoastMarshmallowFireMinigame>().sfxMarshmallowDone;
        AmongUsLLImpl.SoundManagerInstance.PlaySoundImmediate(doneSfx, false, 1f, 1f, null);
    }

    static private IEnumerator CoStroke(PlayerControl myPlayer, PlayerControl target, Vector2 targetPos, bool moveToLeft)
    {
        myPlayer.moveable = false;
        target.moveable = false;

        if (target.AmOwner && Minigame.Instance.AsBoolFast(out var minigame))
        {
            try
            {
                minigame.Close();
                minigame.Close();
            }
            catch
            {
            }
        }

        //自身が動いている間は相手側もうまいこと動かそうとさせる。
        bool myDone = false;
        var myAnim = Effects.Sequence(myPlayer.MyPhysics.WalkPlayerTo(GetStrokePosition(targetPos, moveToLeft), 0.001f, 1f, true), ManagedEffects.Action(() => myDone = true).WrapToIl2Cpp());
        var targetAnim = target.MyPhysics.WalkPlayerTo(targetPos, 0.001f, 1f, true);

        var targetCoroutine = NebulaManager.Instance.StartCoroutine(targetAnim);
        var myCoroutine = NebulaManager.Instance.StartCoroutine(myAnim);

        float leftCount = 1.5f;
        while (leftCount > 0f && !myDone)
        {
            leftCount -= Time.deltaTime;
            yield return null;
        }

        if (!myDone)
        {
            try { NebulaManager.Instance.StopCoroutine(myCoroutine); } catch { }
        }
        try { NebulaManager.Instance.StopCoroutine(targetCoroutine); } catch { }

        bool IsInterrupted() => MeetingHud.Instance.AsBoolFast() || myPlayer.Data.IsDead || target.Data.IsDead;

        PlayerPettingHand? hand = null;
        if (!IsInterrupted())
        {
            target.NetTransform.SnapTo(targetPos);
            myPlayer.MyPhysics.body.velocity = Vector2.zero;
            target.MyPhysics.body.velocity = Vector2.zero;

            myPlayer.MyPhysics.FlipX = !moveToLeft;
            target.MyPhysics.FlipX = !moveToLeft;
            myPlayer.cosmetics.AnimateSkinIdle();
            myPlayer.MyPhysics.Animations.PlayIdleAnimation();

            Vector2 handBasePos = targetPos + target.Collider.offset;

            //デストロイヤーと同じような手の出現
            var introHand = UnityHelper.CreateObject<SpriteRenderer>("MercifulHandIntro", null, Vector3.zero, LayerExpansion.GetPlayersLayer());
            introHand.gameObject.AddComponent<PlayerColorRenderer>().SetPlayer(myPlayer.GetModInfo());
            introHand.flipX = !moveToLeft;
            introHand.transform.localScale = new(HandScale, HandScale, 1f);
            void SetIntroHand(int spriteIndex, Vector2 offset)
            {
                introHand.sprite = Impostor.DestroyerAssets.HandSprite[spriteIndex].GetSprite();
                var pos = handBasePos + new Vector2(moveToLeft ? -offset.x : offset.x, offset.y);
                introHand.transform.position = new Vector3(pos.x, pos.y, target.transform.position.z - 0.01f);
            }

            IEnumerator CoWait(float duration)
            {
                for (float t = 0f; t < duration && !IsInterrupted(); t += Time.deltaTime) yield return null;
            }

            SetIntroHand(1, new(0.2f, 0.9f));
            yield return CoWait(IntroRaiseDuration);
            SetIntroHand(2, new(0.1f, 0.99f));
            yield return CoWait(IntroLowerDuration);
            SetIntroHand(3, new(0.15f, 0.95f));
            yield return CoWait(IntroScareDuration);
            GameObject.Destroy(introHand.gameObject);

            if (!IsInterrupted())
            {
                //撫でる手(バニラのペットを撫でるモーション)を、手が頭に触れるフレームから再生する。
                hand = SpawnHand(myPlayer, target, handBasePos, moveToLeft);
                hand.HandSpriteAnim.Play(hand.PetClip);
                hand.HandSpriteAnim.Time = StrokeStartFrame / 24f;
                yield return null;

                //効果音のタイミングはアニメーション上の時刻で決める。
                bool shouldPlaySE = myPlayer.AmOwner || target.AmOwner;
                float time = 0f;
                while (hand.HandSpriteAnim.IsPlaying(hand.PetClip) && time < MaxStrokeDuration && !IsInterrupted())
                {
                    time += Time.deltaTime;
                    if (shouldPlaySE && hand.HandSpriteAnim.Time >= StrokeSEDelay)
                    {
                        shouldPlaySE = false;
                        PlayStrokeSE();
                    }
                    yield return null;
                }
            }
        }

        bool completed = hand && !IsInterrupted();
        if (hand) GameObject.Destroy(hand!.gameObject);

        if (myPlayer.AmOwner)
        {
            var perk = localStrokingPerk;
            localStrokingPerk = null;
            if (completed) perk?.OnStroked(target.GetModInfo()!);
        }

        myPlayer.moveable = true;
        target.moveable = true;
    }

    static private readonly RemoteProcess<(GamePlayer player, GamePlayer target, Vector2 targetPosition, bool moveToLeft)> RpcCoStroke = new(
        "HandOfMercyStroke",
        (message, _) =>
        {
            NebulaManager.Instance.StartCoroutine(CoStroke(message.player.VanillaPlayer, message.target.VanillaPlayer, message.targetPosition, message.moveToLeft).WrapToIl2Cpp());
        });
}
