using Nebula.Behavior;
using Nebula.Roles.Neutral;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;

namespace Nebula.Roles.Impostor;

public class Thurifer : DefinedSingleAbilityRoleTemplate<Thurifer.Ability>, DefinedRole
{
    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Thuribulum : NebulaSyncShadowObject
    {
        static private IDividedSpriteLoader sprites = DividedSpriteLoader.FromResource("Nebula.Resources.Thuribulum.png", 100f, 3, 1);
        static private IDividedSpriteLoader effect1sprites = DividedSpriteLoader.FromResource("Nebula.Resources.ThuriferEffect1.png", 100f, 8, 1);
        static private IDividedSpriteLoader effect2sprites = DividedSpriteLoader.FromResource("Nebula.Resources.ThuriferEffect2.png", 100f, 8, 1);
        public Thuribulum(Vector2 pos) : base(pos, ZOption.Just, sprites.GetSprite(0), Color.white)
        {
            ModSingleton<ThuribulumManager>.Instance.RegisterThuribulum(this);
            MyRenderer.material = VanillaAsset.GetHighlightMaterial();
        }

        public const string MyTag = "Thuribulum";

        int activeSpriteIndex = 0;
        float activeAnimTimer = 0f;
        private EffectCircle? activatedCircle = null;
        bool isIgnored = false;

        //遅延によるアクティベートが予約されているか否か
        bool localDelayed = false;

        public bool IsIgnored => isIgnored;
        public bool IsActive => activeTimer > 0f;
        public bool CanInteract => !IsActive && !localDelayed;
        
        public void Activate(float duration)
        {
            bool playAnim = !IsActive;

            activeTimer = Math.Max(activeTimer, duration);
            activeAnimTimer = -1f;

            if(playAnim) NebulaManager.Instance.StartCoroutine(ManagedEffects.CoPlayAnimEffect(LayerExpansion.GetDefaultLayer(), "ThuriferSmoke", effect2sprites, null, Position.AsVector3(-1f) + new Vector3(0f,-0.1f,0f), 1.4f, Color.white, 0.08f, Helpers.Prob(0.5f)).WrapToIl2Cpp());
        }

        float activeTimer = 0f;

        void OnMeetingEnd(MeetingEndEvent ev)
        {
            //予約状況をリセット
            localDelayed = false;
        }

        void Update(GameUpdateEvent ev)
        {
            //会議及び追放中でなく、アクティブならばタイマーを進めてアニメーションをさせる。
            if (activeTimer > 0f)
            {
                if (!MeetingHud.Instance && !ExileController.Instance)
                {
                    activeAnimTimer -= Time.deltaTime;
                    activeTimer -= Time.deltaTime;
                    if (activeAnimTimer < 0f)
                    {
                        activeSpriteIndex = (activeSpriteIndex + 1) % 2;
                        Sprite = sprites.GetSprite(activeSpriteIndex + 1);
                        activeAnimTimer = 0.25f;
                    }

                    if (activeTimer < 0f)
                    {
                        Sprite = sprites.GetSprite(0);
                    }

                    if (activatedCircle == null && GamePlayer.LocalPlayer.Role.Role == Thurifer.MyRole)
                    {
                        activatedCircle = EffectCircle.SpawnEffectCircle(null, Position, new(202f / 255f, 1f, 0f), ThuribulumRangeOption, null, true);
                    }
                    if(activatedCircle != null && GamePlayer.LocalPlayer.Role.Role != Thurifer.MyRole)
                    {
                        activatedCircle.Disappear();
                        activatedCircle = null;
                    }
                }
            }
            else
            {
                if(activatedCircle != null)
                {
                    activatedCircle.Disappear();
                    activatedCircle = null;
                }
            }
        }

        public void Ignore()
        {
            isIgnored = true;
        }
        void ResetOutline(GameHudUpdateFasterEvent ev)
        {
            AmongUsUtil.SetHighlight(MyRenderer, false);
        }

        public void ScheduleActivate(float delay, float duration)
        {
            IEnumerator CoDelayedActivate()
            {
                Action PlayEffect = ()=> NebulaManager.Instance.StartCoroutine(ManagedEffects.CoPlayAnimEffect(LayerExpansion.GetDefaultLayer(), "ThuriferSmoke", effect1sprites, null, Position.AsVector3(-1f) + new Vector3(0f, -0.1f, 0f), 1.4f, Color.white, 0.08f, Helpers.Prob(0.5f)).WrapToIl2Cpp()); ;
                while (delay > 0.8f)
                {
                    PlayEffect.Invoke();
                    yield return Effects.Wait(0.8f);
                    delay -= 0.8f;
                }
                if(delay > 0.05f) PlayEffect.Invoke();
                yield return Effects.Wait(delay);

                ModSingleton<ThuribulumManager>.Instance.RpcActivate(this, duration);

                localDelayed = false;
            }
            localDelayed = true;
            NebulaManager.Instance.StartCoroutine(CoDelayedActivate().WrapToIl2Cpp());
        }

        static Thuribulum() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new Thuribulum(new(args[0], args[1])));
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    public class ThuribulumManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static ThuribulumManager() => DIManager.Instance.RegisterModule(() => new ThuribulumManager());
        private List<Thuribulum> allThuribulums = new();
        public IEnumerable<Thuribulum> AllThuribulums => allThuribulums;

        private Dictionary<byte, float> inhalationMap = new();
        private float localInhalation = 0f;
        private float lastShared = 0f;
        private float shareTimer = 0f;
        public float LocalInhalationPercentage => localInhalation / MaxInhalation;
        private float MaxInhalation => 30f / InhalationSpeedOption;

        public float GetInhalation(byte playerId) => inhalationMap.TryGetValue(playerId, out var value) ? value : 0f;
        public float GetInhalationAsPercentage(byte playerId) => GetInhalation(playerId) / MaxInhalation;

        private ThuribulumManager()
        {
            ModSingleton<ThuribulumManager>.Instance = this;
            this.Register(NebulaAPI.CurrentGame!);
        }

        public void RegisterThuribulum(Thuribulum thuribulum) => allThuribulums.Add(thuribulum);

        public bool IsAvailable { get; private set; } = false;
        void OnGameStarted(GameStartEvent _)
        {
            IsAvailable = GeneralConfigurations.CurrentGameMode == Virial.Game.GameModes.FreePlay || (Thurifer.MyRole as ISpawnable).IsSpawnable || ((Jackal.MyRole as ISpawnable).IsSpawnable && Jackal.JackalizedImpostorOption && (Thurifer.MyRole as DefinedRole).JackalAllocationParameters!.RoleCountSum > 0);

            if (!IsAvailable) return;

            //ホストが香炉を生成する
            if (AmongUsClient.Instance.AmHost)
            {
                var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();
                spawner?.Spawn(NumOfThuribulumsOption, 7.5f, "thuribulum", Thuribulum.MyTag, MapObjectType.SmallInCorner);
            }

            //解除ボタン
            bool used = false; //使用は一度きり
            var localPlayer = GamePlayer.LocalPlayer;

            ModAbilityButton imputeButton = null!;
            var thuribulumTracker = new ObjectTrackerUnityImpl<Thuribulum, Thuribulum>(localPlayer.VanillaPlayer, 1f, () => ModSingleton<ThuribulumManager>.Instance.AllThuribulums, t => (imputeButton!.IsVisible && t.CanInteract), null, t => t, t => [t.Position, new(t.Position.x, t.Position.y - 0.3f)], t => t.MyRenderer) { MoreHighlight = true } as ObjectTracker<Thuribulum>;

            imputeButton = NebulaAPI.Modules.AbilityButton(NebulaAPI.CurrentGame!, localPlayer, Virial.Compat.VirtualKeyInput.None,
                0f, "thurifer.impute", buttonSprite,
                _ => thuribulumTracker.CurrentTarget != null, _ => LocalInhalationPercentage > 0.5f && localPlayer.Role.Role != Thurifer.MyRole && !used);
            imputeButton.OnClick = (button) => {
                NebulaGameManager.Instance?.RpcDoGameAction(localPlayer, thuribulumTracker.CurrentTarget!.Position, GameActionTypes.ThuriferImputeAction);

                ModSingleton<ThuribulumManager>.Instance.ImputeTo(thuribulumTracker.CurrentTarget!);
                used = true;
                new StaticAchievementToken("thurifer.common2");
                StatsImpute.Progress();
            };
        }

        float meetingCooldown = 0f;
        void OnUpdate(GameUpdateEvent ev)
        {
            if (!IsAvailable) return;

            if (MeetingHud.Instance || ExileController.Instance)
            {
                meetingCooldown = 5f;
                return;
            }

            if (meetingCooldown > 0f)
            {
                meetingCooldown -= Time.deltaTime;
            }
            else
            {
                var localPlayer = GamePlayer.LocalPlayer;

                //生存中のみ進行する
                if (!localPlayer.IsDead)
                {
                    shareTimer -= Time.deltaTime;

                    foreach (var thuribulum in allThuribulums)
                    {
                        if (!thuribulum.IsActive || thuribulum.IsIgnored) continue;

                        float distance = thuribulum.Position.Distance(localPlayer.Position);
                        float temp = (ThuribulumRangeOption - distance) / ThuribulumRangeOption;
                        if (temp > 0f)
                        {
                            localInhalation += Mathf.Pow(temp, 0.4f) * Time.deltaTime; //そこそこ遠くなると急激に上がらなくなる
                        }
                    }

                    //上限を設ける
                    localInhalation = Mathf.Min(MaxInhalation, localInhalation);

                    if (shareTimer < 0f)
                    {
                        if (localInhalation != lastShared)
                        {
                            RpcUpdateInhalation.Invoke((localPlayer.PlayerId, localInhalation));
                            lastShared = localInhalation;
                        }
                        shareTimer = 0.15f;
                    }
                }
            }

        }

        static RemoteProcess<(int id, float duration)> RpcActivateThuribulum = new("ActivateThuribulum",
            (message, _) =>
            {
                NebulaSyncObject.GetObject<Thuribulum>(message.id)?.Activate(message.duration);
            });

        static RemoteProcess<(byte playerId, float amount)> RpcUpdateInhalation = new("UpdateThuriferInhalation",
            (message, _) =>
            {
                ModSingleton<ThuribulumManager>.Instance.inhalationMap[message.playerId] = message.amount;
            }, false);

        public void RpcActivate(Thuribulum thuribulum, float duration)
        {
            RpcActivateThuribulum.Invoke((thuribulum.ObjectId, duration));
        }

        public void ImputeTo(Thuribulum thuribulum)
        {
            localInhalation = 0f;
            lastShared = 0f;
            thuribulum.ScheduleActivate(ActivationDelayOption, GamePlayer.LocalPlayer.IsCrewmate ? 999999f : ActivateDurationOption);
            RpcUpdateInhalation.Invoke((GamePlayer.LocalPlayer.PlayerId, 0f));
            thuribulum.Ignore();
        }
    }

    private Thurifer() : base("thurifer", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [NumOfThuribulumsOption, ActivateDurationOption, InhalationSpeedOption, ThuribulumRangeOption, MaxKillDelayOption, ShowBlinkOption, ActivationDelayOption]) {
        GameActionTypes.ThuriferActivateAction = new("thurifer.activate", this, isPlacementAction: true);
        GameActionTypes.ThuriferImputeAction = new("thurifer.impute", null, isPlacementAction: true);
    }

    static private FloatConfiguration InhalationSpeedOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.inhalationSpeed", (0.25f, 3f, 0.125f), 1f, FloatConfigurationDecorator.Ratio);
    static private IntegerConfiguration NumOfThuribulumsOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.numOfThuribulums", (1, 20), 12);
    static private FloatConfiguration ThuribulumRangeOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.thuribulumRange", (2f, 20f, 1f), 5f, FloatConfigurationDecorator.Ratio);
    static private FloatConfiguration MaxKillDelayOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.maxKillDelay", (5f, 50f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration ActivateDurationOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.curseDuration", (20f, 300f, 5f), 40f, FloatConfigurationDecorator.Second);
    static private BoolConfiguration ShowBlinkOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.showBlink", true);
    static private FloatConfiguration ActivationDelayOption = NebulaAPI.Configurations.Configuration("options.role.thurifer.activationDelay", (0f,60f,2.5f),5f, FloatConfigurationDecorator.Second);
    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ThuriferButton.png", 115f);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;

    static public Thurifer MyRole = new Thurifer();

    static private GameStatsEntry StatsActivate = NebulaAPI.CreateStatsEntry("stats.thurifer.activate", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsImpute = NebulaAPI.CreateStatsEntry("stats.thurifer.impute", GameStatsCategory.Roles, MyRole);
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        //画面左下のホルダ
        PlayersIconHolder iconHolder;

        AchievementToken<(EditableBitMask<GamePlayer> more50Players, int killed, bool inMeetingKill, bool maxDelayKill)>? acTokenChallenge = null;
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                acTokenChallenge = new("thurifer.challenge", (BitMasks.AsPlayer(), 0, false, false), (a, _) => a.more50Players.ForEach(NebulaGameManager.Instance!.AllPlayerInfo).Count() >= 8 && a.maxDelayKill && a.inMeetingKill && a.killed >= 4);

                iconHolder = new PlayersIconHolder().Register(this);
                iconHolder.XInterval = 0.35f;
                NebulaGameManager.Instance?.AllPlayerInfo.Do(p => {
                    if (!p.AmOwner)
                    {
                        var icon = iconHolder.AddPlayer(p);
                        icon.SetAlpha(true);
                        icon.SetText(null);
                    }
                });

                ObjectTracker<Thuribulum> thuribulumTracker = new ObjectTrackerUnityImpl<Thuribulum, Thuribulum>(MyPlayer.VanillaPlayer, 1f, () => ModSingleton<ThuribulumManager>.Instance.AllThuribulums, t => t.CanInteract, null, t => t, t => [t.Position, new(t.Position.x, t.Position.y - 0.3f)], t => t.MyRenderer) { MoreHighlight = true }.Register(this);
                
                var activateButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "thurifer.curse",
                    10f, "thurifer.curse", buttonSprite,
                    _ => thuribulumTracker.CurrentTarget != null).SetAsUsurpableButton(this);
                activateButton.OnClick = (button) => {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, thuribulumTracker.CurrentTarget!.Position, GameActionTypes.ThuriferActivateAction);
                    thuribulumTracker.CurrentTarget?.ScheduleActivate(ActivationDelayOption, ActivateDurationOption);
                    new StaticAchievementToken("thurifer.common1");
                    StatsActivate.Progress();
                };
            }
        }

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            foreach(var icon in iconHolder.AllIcons)
            {
                int inhalation = (int)(ModSingleton<ThuribulumManager>.Instance.GetInhalationAsPercentage(icon.Player.PlayerId) * 100f);
                if(inhalation > 0)
                {
                    if (inhalation < 100)
                    {
                        icon.SetText(inhalation + "%", 2f);
                        icon.SetAlpha(true);
                    }
                    else
                    {
                        icon.SetText(null);
                        icon.SetAlpha(false);
                    }
                }

                if (inhalation >= 50) acTokenChallenge.DoIf(a => a.Value.more50Players.Add(icon.Player));
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            foreach(var icon in iconHolder.AllIcons) if (icon.Player.IsDead) iconHolder.Remove(icon);
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            foreach (var icon in iconHolder.AllIcons) if (icon.Player.IsDead) iconHolder.Remove(icon);
        }

        [Local]
        void OnKillAnyone(PlayerTryVanillaKillLocalEventAbstractPlayerEvent ev)
        {
            var inhalation = ModSingleton<ThuribulumManager>.Instance.GetInhalationAsPercentage(ev.Target.PlayerId);
            if(inhalation > 0f)
            {
                var delay = inhalation * MaxKillDelayOption;

                IEnumerator CoDelayKill()
                {
                    float t = 0f;
                    while(t  < delay)
                    {
                        //アニメーション中、追放中は何もしない
                        if (MeetingHud.Instance && MeetingHud.Instance.state < MeetingHud.VoteStates.Discussion)
                        {
                            yield return null;
                        }
                        else if (ExileController.Instance)
                        {
                            yield return null;
                        }
                        else
                        {
                            t += Time.deltaTime;
                            yield return null;
                        }
                    }

                    if (!ev.Target.IsDead)
                    {
                        if (MeetingHud.Instance) acTokenChallenge.DoIf(a => a.Value.inMeetingKill = true);
                        if (!(inhalation < 1f)) acTokenChallenge.DoIf(a => a.Value.maxDelayKill = true);
                        acTokenChallenge.DoIf(a => a.Value.killed++);

                        MyPlayer.MurderPlayer(ev.Target, PlayerStates.Gassed, EventDetails.Gassed, KillParameter.RemoteKill);
                    }
                }

                if (ShowBlinkOption || delay < 3f)
                {
                    //ブリンクを見せる
                    MyPlayer.VanillaPlayer.NetTransform.RpcSnapTo(ev.Target.VanillaPlayer.transform.position);
                }

                NebulaManager.Instance.StartCoroutine(CoDelayKill().WrapToIl2Cpp());

                ev.Cancel(true);
            }
        }
    }
}
