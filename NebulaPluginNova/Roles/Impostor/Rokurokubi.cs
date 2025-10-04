using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Nebula.Roles.Abilities;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Events.VoiceChat;
using Virial.Game;

namespace Nebula.Roles.Impostor;

internal class Rokurokubi : DefinedSingleAbilityRoleTemplate<Rokurokubi.Ability>, DefinedRole
{
    private Rokurokubi() : base("rokurokubi", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [CraneSpeedOption, MaxNeckLengthOption, AutoKillOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        //ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Berserker.png");
    }

    static private readonly FloatConfiguration CraneSpeedOption = NebulaAPI.Configurations.Configuration("options.role.rokurokubi.craneSpeed", (2.5f, 15f, 2.5f), 7.5f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration MaxNeckLengthOption = NebulaAPI.Configurations.Configuration("options.role.rokurokubi.maxNeckLength", (2.5f, 45f, 2.5f), 15f, FloatConfigurationDecorator.Ratio);
    static private readonly BoolConfiguration AutoKillOption = NebulaAPI.Configurations.Configuration("options.role.rokurokubi.autoKillInCraning", false);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Rokurokubi MyRole = new();
    

    [NebulaRPCHolder]
    public class LongNeckMode : FlexibleLifespan, IGameOperator, IBindPlayer
    {
        Rokurokubi.Ability myAbility;
        GamePlayer myPlayer => (myAbility as IBindPlayer).MyPlayer;
        GamePlayer IBindPlayer.MyPlayer => myPlayer;
        public GameObject HeadTracker { get; private set; } = null!;
        public LongBoiPlayerBody LongBoi { get; private set; } = null!;
        EmptyBehaviour CamTarget = null!;
        public bool IsActive => !calmDownInvoked;
        public Vector3 GetHeadPos()
        {
            if (HeadTracker) return HeadTracker.transform.position;
            return myPlayer.VanillaPlayer.transform.position;
        }

        public LongNeckMode(Rokurokubi.Ability rokurokubi, float angleDeg)
        {
            while (angleDeg > 180f) angleDeg -= 360f;
            while (angleDeg < -180f) angleDeg += 360f;

            myAbility = rokurokubi;
            RpcStartLongNeck.Invoke((myPlayer, angleDeg));

            LongBoi = GetLongBody(myPlayer);
            HeadTracker = UnityHelper.CreateObject("HeadTracker", null, myPlayer.VanillaPlayer.transform.position);
            var light = AmongUsUtil.GenerateCustomLight(new Vector2(0f, 0f));
            light.transform.SetParent(HeadTracker.transform);
            light.transform.localPosition = new(0f, 0f, -11f);
            light.color = new(1f, 1f, 1f, 0.75f);
            light.transform.localScale = new(1.2f, 1.2f, 1f);

            CamTarget = HeadTracker.AddComponent<EmptyBehaviour>();
            MoreCosmic.LongNeckHooks.Get(LongBoi.gameObject).AddAction(() =>
            {
                if (HeadTracker)
                {
                    float neckAngle = myAbility.NeckAngle;
                    var pos = (myAbility.CalcBaseNeckEndPos(neckAngle, LongBoi.GetBaseSumNeckRate(), out _) + Vector2.up.Rotate(-neckAngle) * LongBoi.foregroundNeckSprite.size.y).AsVector3(-3f);
                    var scale = LongBoi.transform.lossyScale;
                    pos.x *= scale.x;
                    pos.y *= scale.y;
                    pos.z *= scale.z;
                    HeadTracker.transform.position = myPlayer.Position.AsVector3(0f) + pos;
                    return true;
                }
                return false;
            });

            AmongUsUtil.SetCamTarget(CamTarget, true);

        }


        public float ActualHeight => LongBoi.transform.lossyScale.y * LongBoi.calculatedNeckHeight;

        void ResetCamTarget()
        {
            var target = AmongUsUtil.CurrentCamTarget;
            if (!target) return;
            if (!HeadTracker) return;
            if (target.transform.IsChildOf(HeadTracker.transform))
            {
                AmongUsUtil.SetCamTarget();
                GameObject.Destroy(HeadTracker);
            }
        }

        void IGameOperator.OnReleased()
        {
            ResetCamTarget();
            RpcCalmDown.Invoke((myPlayer, true));
        }

        bool calmDownInvoked = false;
        public void CalmDown()
        {
            if (calmDownInvoked) return;
            calmDownInvoked = true;

            ResetCamTarget();
            NebulaManager.Instance.ScheduleDelayAction(() =>
            {
                RpcCalmDown.Invoke((myPlayer, false));
            });
            NebulaManager.Instance.StartDelayAction(1f, () => this.Release());
        }

        void OnFixMicPosition(FixMicPositionEvent ev)
        {
            if (!calmDownInvoked) ev.CanIgnoreWalls = true;
        }


        [OnlyMyPlayer]
        void OnMurderPlayer(PlayerKillPlayerEvent ev)
        {
            if (NebulaGameManager.Instance != null) {
                float currentTime = NebulaGameManager.Instance.CurrentTime;
                if (lastStopped.HasValue && currentTime - lastStopped.Value > 3f)
                {
                    new StaticAchievementToken("rokurokubi.common2");
                }

                if (myPlayer.HasAttribute(PlayerAttributes.Invisible) && ev.Dead.Position.Distance(myPlayer.Position) > 10f) new StaticAchievementToken("rokurokubi.common3");

                
                if (NebulaGameManager.Instance.GameStatistics.AllEvents.Any(e =>
                {
                    //2秒以内でなければならない
                    if (currentTime - e.Time > 2f) return false;
                    var killer = GamePlayer.GetPlayer(e.SourceId ?? byte.MaxValue);
                    //自身以外の仲間でなければならない
                    if (killer == null || !killer.IsSameSideOf(myPlayer) || killer.AmOwner) return false;
                    var role = killer.Role.Role;
                    //もののけっぽさのある役職はダメ
                    if (role == Rokurokubi.MyRole || 
                        role == Berserker.MyRole || 
                        role == Destroyer.MyRole || 
                        role == Alien.MyRole || 
                        role == Cupid.MyRole || 
                        role == Gimlet.MyRole || 
                        role == Hadar.MyRole
                        ) return false;
                    return true;
                }))
                {
                    new StaticAchievementToken("rokurokubi.challenge");
                }
            }

            CalmDown();
        }

        void OnMeetingStart(MeetingStartEvent ev) => Release();

        //ベント移動中はベントがカメラ中心に来るようにする。
        [OnlyMyPlayer]
        void OnEnterVent(PlayerVentEnterEvent ev) => AmongUsUtil.SetCamTarget();
        [OnlyMyPlayer]
        void OnExitVent(PlayerVentExitEvent ev)
        {
            if(!calmDownInvoked) AmongUsUtil.SetCamTarget(CamTarget, true);
        }

        float? lastStopped = null;
        void OnUpdate(GameHudUpdateEvent ev)
        {
            if (!calmDownInvoked)
            {
                var action = NebulaInput.GetInput(Virial.Compat.VirtualKeyInput.AidAction);
                if (action.KeyDownForAction)
                {
                    lastStopped = NebulaGameManager.Instance?.CurrentTime;
                    RpcUpdateLongNeck.Invoke((myPlayer, LongBoi.calculatedNeckHeight));
                }
                if (action.KeyUpForAction)
                {
                    lastStopped = null;
                    RpcUpdateLongNeck.Invoke((myPlayer, -1f));
                }
            }
        }

        [OnlyMyPlayer]
        void OnDead(PlayerDieEvent ev)
        {
            if (ev is PlayerMurderedEvent) new StaticAchievementToken("rokurokubi.another1");
            Release();
        }

        private static float CraneNeckSpeed => CraneSpeedOption;
        private static float BackNeckSpeed => MaxNeckLengthOption / 0.35f; //1秒かけて最大長をしまえればよい。
        static private RemoteProcess<(GamePlayer player, float goal)> RpcUpdateLongNeck = new("updateLongNeck", (message, _) =>
        {
            var longBoiBody = GetLongBody(message.player);
            if (longBoiBody && longBoiBody.targetHeight > 0f) longBoiBody.targetHeight = message.goal < 0f ? Ability.NeckMaxLength : Mathf.Max(0.1f, message.goal);
        });

        static private RemoteProcess<(GamePlayer player, float angleDeg)> RpcStartLongNeck = new("longNeck", (message, _) =>
        {
            message.player.VanillaPlayer.NetTransform.Halt();
            message.player.ChangeBodyTypeAndWrapUp(PlayerBodyTypes.Long);

            var longBoiBody = GetLongBody(message.player);
            if (longBoiBody)
            {
                longBoiBody.growSpeed = CraneNeckSpeed;
                longBoiBody.skipNeckAnim = false;
                longBoiBody.ShouldLongAround = true;
                longBoiBody.targetHeight = Ability.NeckMaxLength;
                longBoiBody.SetupNeckGrowth(false, true);

                if (message.player.TryGetAbility<Ability>(out var rokurokubi))
                {
                    int epoch = rokurokubi.StartLongNeckMode(message.angleDeg);
                    var hat = message.player.VanillaCosmetics.hat;
                    
                    float GetRotateAngle() => -rokurokubi.NeckAngle * longBoiBody.GetBaseCurveNeckRate();
                    Vector3 RotateLocalPos(Vector3 localPosition)
                    {
                        var offset = longBoiBody.cosmeticLayer.FlipX ? longBoiBody.cosmeticLayer.FlippedCosmeticOffset : longBoiBody.cosmeticLayer.NormalCosmeticOffset;
                        var origLocalPos = localPosition - offset;
                        return origLocalPos.RotateZ(GetRotateAngle()) + offset;
                    }

                    MoreCosmic.NodeSyncHooks.Get(hat.gameObject).AddAction(() =>
                    {
                        hat.transform.localPosition = RotateLocalPos(hat.transform.localPosition);
                        hat.transform.localEulerAngles += new Vector3(0f, 0f, GetRotateAngle());
                        return longBoiBody.isActiveAndEnabled && rokurokubi.GetNeckEpoch() == epoch && !rokurokubi.IsDeadObject;
                    });
                    var visor = message.player.VanillaCosmetics.visor;
                    MoreCosmic.NodeSyncHooks.Get(visor.gameObject).AddAction(() =>
                    {
                        if (!visor.visorData.TryGetModData(out var modVisor) || !modVisor.Fixed)
                        {
                            visor.transform.localPosition = RotateLocalPos(visor.transform.localPosition);
                            visor.transform.localEulerAngles += new Vector3(0f, 0f, GetRotateAngle());
                        }
                        return longBoiBody.isActiveAndEnabled && rokurokubi.GetNeckEpoch() == epoch && !rokurokubi.IsDeadObject;
                    });

                    MeshRenderer? mr = null;
                    MeshFilter? mf = null;
                    MoreCosmic.LongNeckHooks.Get(longBoiBody.gameObject).AddAction(() =>
                    {
                        rokurokubi.UpdateNeck(longBoiBody, ref mr, ref mf);
                        return rokurokubi.GetNeckEpoch() == epoch && !rokurokubi.IsDeadObject;
                    });

                    rokurokubi.MyPlayer.Unbox().AddPlayerColorRenderers(longBoiBody.headSprite, longBoiBody.foregroundNeckSprite);
                }
            }
        });

        static private void ResetBody(GamePlayer player)
        {
            var longBoiBody = GetLongBody(player);
            if (longBoiBody)
            {
                longBoiBody.ShouldLongAround = false;
            }

            player.ChangeBodyTypeAndWrapUp(PlayerBodyTypes.Normal);
            player.Unbox().UpdateOutfit(); //Longモードで外されたコスチュームを付け直す
        }

        const string EyesightObjName = "Eyesight";
        static private Transform GetEyesightParent(LongBoiPlayerBody longBoiBody) => longBoiBody.headSprite.transform;
        static private RemoteProcess<(GamePlayer player, bool immediately)> RpcCalmDown = new("longNeckCalmDown", (message, _) =>
        {
            if (message.immediately)
            {
                var longBoi = GetLongBody(message.player);
                ResetBody(message.player);
                if (longBoi) message.player.Unbox().RemovePlayerColorRenderers(longBoi.headSprite, longBoi.foregroundNeckSprite);
            }
            else
            {
                var longBoi = GetLongBody(message.player);
                var height = longBoi.calculatedNeckHeight;
                var speed = Mathf.Max(CraneNeckSpeed * 1.5f, height);
                IEnumerator CoUpdateNeck()
                {
                    while (height > 0f && longBoi.isActiveAndEnabled)
                    {
                        longBoi.targetHeight = height;
                        longBoi.SetupNeckGrowth(true, true);
                        yield return null;
                        height -= Time.deltaTime * speed;
                    }
                    if (longBoi.isActiveAndEnabled)
                    {
                        longBoi.targetHeight = 0f;
                        longBoi.SetupNeckGrowth(true, true);
                    }
                }
                CoUpdateNeck().StartOnScene();
            }
        });
    }

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private readonly Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.RokurokubiButton.png", 115f);
        static private readonly Image buttonCalmSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.RokurokubiResetButton.png", 115f);

        public bool InLongNeckMode => !(CurrentLongNeckMode?.IsDeadObject ?? true) && (CurrentLongNeckMode?.IsActive ?? false);
        public LongNeckMode? CurrentLongNeckMode = null;

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        private int neckEpoch = 0;
        public int GetNeckEpoch() => neckEpoch;
        public int StartLongNeckMode(float angleDeg)
        {
            this.NeckAngle = angleDeg;
            return ++neckEpoch;
        } 
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                ModAbilityButton calmButton = null!;
                var rokuroButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    1.2f, "rokuro", buttonSprite, (button) => MyPlayer.CanMove, (button) => !MyPlayer.IsDead && !InLongNeckMode);
                rokuroButton.SetAsMouseClickButton();
                var killTimer = NebulaAPI.Modules.CurrentKillTimer();
                rokuroButton.OnClick = (button) =>
                {
                    if (CurrentLongNeckMode != null) CurrentLongNeckMode.Release();
                    CurrentLongNeckMode = null;
                    
                    NebulaManager.Instance.ScheduleDelayAction(() =>
                    {
                        if(!(killTimer.CurrentTime > 0f)) CurrentLongNeckMode = new LongNeckMode(this, -(MyPlayer.Unbox().MouseAngle.RadToDeg() - 90f)).Register(this);
                    });
                    button.StartCoolDown();
                    calmButton.StartCoolDown();
                };
                rokuroButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                rokuroButton.SetAsUsurpableButton(this);
                rokuroButton.CoolDownTimer = NebulaAPI.Modules.CombinedTimer(killTimer, rokuroButton.CoolDownTimer!, false, true);
                rokuroButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "rokurokubi.pause");
                new GuideLineAbility(MyPlayer, () => !rokuroButton.IsInCooldown && !InLongNeckMode && MyPlayer.CanMove && !MyPlayer.IsDead).Register(new FunctionalLifespan(() => !this.IsDeadObject && !rokuroButton.IsBroken));

                calmButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    0.1f, "rokuro.reset", buttonCalmSprite, (button) => MyPlayer.CanMove, (button) => !MyPlayer.IsDead && InLongNeckMode);
                void CalmDown()
                {
                    CurrentLongNeckMode?.CalmDown();
                    CurrentLongNeckMode = null;
                    rokuroButton.StartCoolDown();
                }
                calmButton.OnClick = (button) =>
                {
                    CalmDown();
                };
                calmButton.SetAsUsurpableButton(this);
                calmButton.OnBroken = (button) =>
                {
                    CalmDown();
                    rokuroButton.Break();
                };
                calmButton.BindSubKey(Virial.Compat.VirtualKeyInput.AidAction, "rokurokubi.pause");

                var killAchToken = new AchievementToken<int>("rokurokubi.common1", 0, (val, _) => val >= 2);
                var tracker = ObjectTrackers.ForPlayerlike(this, AmongUsUtil.VanillaKillDistance + (AutoKillOption ? 0.2f : 0.5f), () => CurrentLongNeckMode?.GetHeadPos() ?? MyPlayer.VanillaPlayer.transform.position, ObjectTrackers.PlayerlikeLocalKillablePredicate, null, Color.red, false, true);
                var killButton = NebulaAPI.Modules.PlayerlikeKillButton(this, MyPlayer, new Virial.Events.Player.PlayerInteractParameter(IsKillInteraction: true), true, Virial.Compat.VirtualKeyInput.Kill, null, 1f, "kill", ModAbilityButton.LabelType.Impostor, null,
                    (target, button) => {
                        var myPos = MyPlayer.Position;
                        var targetPos = target.Position;
                        Vector2 diff = targetPos - myPos;
                        float mag = diff.magnitude;

                        //var deathState = PlayerState.Dead;
                        var killParam = KillParameter.RemoteKill;
                        if (mag < AmongUsUtil.VanillaKillDistance + 0.5f && !PhysicsHelpers.AnyNonTriggersBetween(myPos, diff.normalized, mag, Constants.ShipAndObjectsMask))
                        {
                            killParam = KillParameter.NormalKill;
                        }
                        else
                        {
                            killAchToken.Value++;
                            if(mag > 10f && (MyPlayer.HasAttribute(PlayerAttributes.Invisible) || (MyPlayer.IsImpostor && MyPlayer.HasAttribute(PlayerAttributes.InvisibleElseImpostor))))
                            {

                            }
                        }

                        MyPlayer.MurderPlayer(target, PlayerState.Dead, null, killParam);
                        NebulaManager.Instance.ScheduleDelayAction(CalmDown);
                        NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();


                    }, tracker, null, (button) => InLongNeckMode);
                killButton.OnUpdate = button =>
                {
                    if(AutoKillOption && killButton.IsAvailable && killButton.IsVisible)
                    {
                        killButton.DoClick();
                    }
                };
            }
        }

        internal const float NeckBaseCurveLength = 2.5f;
        internal const float NeckBaseStraightLength = 0.3f;
        internal const float NeckBaseSumLength = NeckBaseCurveLength + NeckBaseStraightLength;
        internal static float NeckMaxLength => MaxNeckLengthOption / 0.35f; //実際の距離に合わせる
        internal float NeckAngle { get; private set; } = 0f;
        private void GrowNeck(LongBoiPlayerBody longBoi)
        {
            float height = Mathf.MoveTowards(longBoi.neckSprite.size.y + longBoi.foregroundNeckSprite.size.y, longBoi.targetHeight, longBoi.growSpeed * Time.deltaTime);
            longBoi.calculatedNeckHeight = height;


            longBoi.neckSprite.size = new Vector2(longBoi.neckSprite.size.x, Mathn.Clamp(height, 0f, 1.1f));
            longBoi.foregroundNeckSprite.size = new Vector2(longBoi.foregroundNeckSprite.size.x, Mathn.Max(height - 1.1f, 0f));
        }

        internal float CalcBaseNeckRadius(float absAngle) => NeckBaseCurveLength * 180 / absAngle / Mathn.PI;
        internal Vector2 CalcBaseNeckEndPos(float neckAngle, float neckSumP, out bool containsCurvePart)
        {
            float absAngle = Math.Abs(neckAngle);
            if (absAngle > 0f && neckSumP > NeckBaseStraightLength / NeckBaseSumLength)
            {
                float curveP = (NeckBaseSumLength * neckSumP - NeckBaseStraightLength) / NeckBaseCurveLength;
                float r = CalcBaseNeckRadius(absAngle);
                float x = r * (1f - Mathn.Cos(absAngle.DegToRad() * curveP));
                float y = r * Mathn.Sin(absAngle.DegToRad() * curveP);
                containsCurvePart = true;
                return new(neckAngle < 0f ? -x : x, y + NeckBaseStraightLength);
            }
            else
            {
                containsCurvePart = false;
                return new(0f, NeckBaseSumLength * neckSumP);
            }
        }

        private const string MeshRendererObjName = "NeckMesh";

        private float LastNeckCurveP = 0f;
        private float LastNeckAngle = 0f;
        private bool LastFlipX = false;
        private float LastCurveNeckWidth = 0f;
        internal void UpdateNeck(LongBoiPlayerBody longBoi, ref MeshRenderer? meshRenderer, ref MeshFilter? meshFilter)
        {
            var neckRenderer = longBoi.neckSprite;
            var foregroundNeck = longBoi.foregroundNeckSprite;

            //根本部分は表示しない(カーブ可能な根元部分で代替)
            neckRenderer.enabled = false;

            // 延長された首の画像と横幅を揃える。
            foregroundNeck.sprite = neckRenderer.sprite;
            foregroundNeck.size = new Vector2(neckRenderer.size.x, foregroundNeck.size.y);
            foregroundNeck.flipX = neckRenderer.flipX;

            bool inSpawnAnim = false;

            if (!longBoi.myPlayerControl.MyPhysics.Animations.IsPlayingSpawnAnimation())
            {
                //首の長さを更新する。
                if (!longBoi.skipNeckAnim) GrowNeck(longBoi);
            }
            else
            {
                //スポーンアニメーション中は首は不要
                neckRenderer.size = new Vector2(neckRenderer.size.x, 0f);
                foregroundNeck.size = new Vector2(foregroundNeck.size.x, 0f);
                if (meshRenderer) meshRenderer.enabled = false;
                inSpawnAnim = true;
            }

            //真上を0度とする
            float neckAngle = NeckAngle;
            float neckP = longBoi.GetBaseSumNeckRate();

            
            float num = 0f;
            
            var endPos = CalcBaseNeckEndPos(neckAngle, neckP, out var containsCurvePart);
            var curveP = longBoi.GetBaseCurveNeckRate();

            //延長された首の縦方向の位置は起点の位置でよい。(=中央下の位置に合わせる。)延長された首は首の子オブジェクト。
            foregroundNeck.transform.localPosition = endPos.AsVector3(num);
            foregroundNeck.transform.localEulerAngles = new(0f, 0f, -neckAngle * curveP);

            var neckBasePos = new Vector2(0f, neckRenderer.transform.localPosition.y);


            Vector2 neckDir = Vector2.up.Rotate(-neckAngle * curveP);

            //頭は首からみて伸ばした分の長さだけ延長した先に置けばよい。長さを0.01fだけ減じているのに注意。
            longBoi.headSprite.transform.localPosition =
                (neckBasePos + endPos + neckDir * (foregroundNeck.size.y - 0.01f)).AsVector3(num);
            longBoi.headSprite.transform.localEulerAngles = new(0f, 0f, inSpawnAnim ? 0f : -neckAngle * curveP);

            Vector3 offset = neckBasePos + endPos + (neckDir * foregroundNeck.size.y);

            //曲がる部分
            if (!inSpawnAnim && (Mathn.Abs(LastNeckAngle - neckAngle) > 0f || Mathn.Abs(LastNeckCurveP - curveP) > 0f || Mathn.Abs(LastCurveNeckWidth - neckRenderer.size.x) > 0f || LastFlipX != longBoi.cosmeticLayer.FlipX))
            {
                LastNeckAngle = neckAngle;
                LastNeckCurveP = curveP;
                LastFlipX = longBoi.cosmeticLayer.FlipX;
                LastCurveNeckWidth = neckRenderer.size.x;

                if (!meshRenderer || !meshFilter)
                {
                    Transform meshTransform = neckRenderer.transform.FindChild(MeshRendererObjName);
                    if (meshTransform)
                    {
                        meshRenderer = meshTransform.GetComponent<MeshRenderer>();
                        meshFilter = meshTransform.GetComponent<MeshFilter>();
                    }
                    if (!meshRenderer || !meshFilter)
                    {
                        var pair = UnityHelper.CreateMeshRenderer(MeshRendererObjName, neckRenderer.transform, Vector3.zero, null, null, longBoi.cosmeticLayer.currentBodySprite.BodySprite.sharedMaterial);
                        MyPlayer.Unbox().AddPlayerColorRenderers((pair.renderer, null));

                        meshRenderer = pair.renderer;
                        meshFilter = pair.filter;
                    }
                }

                if (containsCurvePart)
                {
                    float absAngle = Mathn.Abs(NeckAngle);
                    if (absAngle > Mathf.Epsilon)
                    {
                        float r = CalcBaseNeckRadius(absAngle);
                        float p = longBoi.GetBaseCurveNeckRate();
                        int n = 2 + (int)(Mathn.Abs(absAngle) * p / 14);//除数を大きくするとカーブが荒くなる
                        float unitAngle = Mathn.Abs(absAngle) * p / (float)n;

                        float centerX = NeckAngle > 0f ? r : -r;
                        float centerSign = Mathn.Sign(centerX);
                        float width = neckRenderer.size.x;
                        float halfWidth = width * centerSign * 0.5f;
                        float rInner = r - halfWidth;
                        float rOuter = r + halfWidth;

                        int points = n + 2;
                        Vector3[] vertices = new Vector3[points * 2];
                        Vector2[] uvs = new Vector2[points * 2];
                        vertices[0] = new Vector3(rInner * -centerSign + centerX, 0f, 0f);
                        vertices[1] = new Vector3(rOuter * -centerSign + centerX, 0f, 0f);
                        vertices[2] = new Vector3(rInner * -centerSign + centerX, NeckBaseStraightLength, 0f);
                        vertices[3] = new Vector3(rOuter * -centerSign + centerX, NeckBaseStraightLength, 0f);

                        for (int i = 0; i < n; i++)
                        {
                            var dir = Vector3.right.RotateZ(unitAngle * (i + 1));
                            vertices[4 + 2 * i] = new(centerX + rInner * dir.x * -centerSign, NeckBaseStraightLength + rInner * dir.y, 0f);
                            vertices[5 + 2 * i] = new(centerX + rOuter * dir.x * -centerSign, NeckBaseStraightLength + rOuter * dir.y, 0f);
                        }

                        for (int i = 0; i < points; i++)
                        {
                            float v = (float)i / (float)(points - 1);
                            uvs[2 * i] = new(0.5f + (longBoi.cosmeticLayer.FlipX ? -0.5f : 0.5f), v);//uは0fか1fか 
                            uvs[2 * i + 1] = new(0.5f - (longBoi.cosmeticLayer.FlipX ? -0.5f : 0.5f), v);//uは0fか1fか 
                        }

                        int[] triangles = new int[(points - 1) * 6];
                        for (int i = 0; i < points - 1; i++)
                        {
                            triangles[i * 6] = i * 2;
                            triangles[i * 6 + 1] = i * 2 + (centerSign > 0f ? 2 : 1);
                            triangles[i * 6 + 2] = i * 2 + (centerSign > 0f ? 1 : 2);
                            triangles[i * 6 + 3] = i * 2 + 2;
                            triangles[i * 6 + 4] = i * 2 + (centerSign > 0f ? 3 : 1);
                            triangles[i * 6 + 5] = i * 2 + (centerSign > 0f ? 1 : 3);
                        }

                        var mesh = meshFilter.mesh;
                        mesh.Clear();
                        mesh.SetVertices(vertices);
                        mesh.SetUVs(0, uvs);
                        UpdateColors(meshFilter);
                        mesh.SetTriangles(triangles, 0);
                    }
                }
                else
                {
                    float rate = longBoi.GetBaseSumNeckRate();
                    meshFilter.CreateRectMesh(new(neckRenderer.size.x * (longBoi.cosmeticLayer.FlipX ? -1f : 1f), NeckBaseSumLength * rate), new(0f, NeckBaseSumLength * 0.5f * rate));
                    UpdateColors(meshFilter);
                }
                meshRenderer.localBounds = new(Vector3.zero, new(8f, 8f, 0f));
            }

            void UpdateColors(MeshFilter meshFilter)
            {
                if (meshFilter != null && meshFilter)
                {
                    var colors = new Color[meshFilter.mesh.vertices.Count];
                    var color = longBoi.cosmeticLayer.currentBodySprite.BodySprite.color;
                    for (int i = 0; i < colors.Length; i++) colors[i] = color;
                    meshFilter.mesh.SetColors(colors);
                }
            }

            if (meshRenderer != null && meshRenderer)
            {
                //テクスチャを更新
                UnityHelper.ReflectSpriteST(meshRenderer!.material, neckRenderer.sprite);

                meshRenderer.SetBothOrder(1008);
                meshRenderer.enabled = longBoi.foregroundNeckSprite.enabled;
            }
        

            //コスチュームの位置を調整する。
            longBoi.cosmeticLayer.NormalCosmeticOffset = offset;
            longBoi.cosmeticLayer.FlippedCosmeticOffset = offset;
            longBoi.myPlayerControl.MyPhysics.Animations.UpdateCosmeticOffset(offset, offset);
            longBoi.cosmeticLayer.UpdateCosmeticOffset(num, false);
        }

        [OnlyMyPlayer]
        void OnFixZ(PlayerFixZPositionEvent ev)
        {
            if(ev.Player.VanillaCosmetics.bodyType == PlayerBodyTypes.Long)
            {
                var longBoi = GetLongBody(MyPlayer);
                if (longBoi)
                {
                    var y = longBoi.headSprite.transform.position.y;
                    if (ev.Y > y) ev.Y = y;
                }
            }
        }

        [OnlyMyPlayer]
        void OnFixMicPosition(FixSpeakerPositionEvent ev)
        {
            if (ev.Player.VanillaCosmetics.bodyType == PlayerBodyTypes.Long)
            {
                var longBoi = GetLongBody(MyPlayer);
                if (longBoi)
                {
                    ev.Position = longBoi.headSprite.transform.position;
                }
            }
        }

        bool IPlayerAbility.HideKillButton => InLongNeckMode;
        bool IPlayerAbility.BlockUsingUtility => !InLongNeckMode && MyPlayer.VanillaCosmetics.bodyType == PlayerBodyTypes.Long;
    }

    static LongBoiPlayerBody GetLongBody(GamePlayer player) => player.VanillaPlayer.cosmetics.bodySprites.Find((Il2CppSystem.Predicate<PlayerBodySprite>)(Func<PlayerBodySprite, bool>)(pbs => pbs.Type == PlayerBodyTypes.Long)).BodySprite.GetComponent<LongBoiPlayerBody>();

}

file static class LongNeckHelpers
{
    public static float GetBaseSumNeckRate(this LongBoiPlayerBody longBoi) => Mathf.Clamp01(longBoi.calculatedNeckHeight / Rokurokubi.Ability.NeckBaseSumLength);
    public static float GetBaseCurveNeckRate(this LongBoiPlayerBody longBoi) => Mathf.Clamp01((longBoi.calculatedNeckHeight - Rokurokubi.Ability.NeckBaseStraightLength) / Rokurokubi.Ability.NeckBaseCurveLength);
    public static float GetBaseStraightNeckRate(this LongBoiPlayerBody longBoi) => Mathf.Clamp01(longBoi.calculatedNeckHeight / Rokurokubi.Ability.NeckBaseStraightLength);
}