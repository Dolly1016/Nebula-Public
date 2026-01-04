using Il2CppInterop.Runtime.Injection;
using Nebula.Behavior;
using Nebula.Game.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Roles.Impostor;

internal class Creeping : DefinedSingleAbilityRoleTemplate<Creeping.Ability>, HasCitation, DefinedRole
{
    private Creeping() : base("creeping", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [PoisonDelayOption, DepoisonDurationOption, PoisonCoolDownOption])
    {
    }
    Citation? HasCitation.Citation => Citations.TownOfImpostors;

    static private readonly FloatConfiguration PoisonDelayOption = NebulaAPI.Configurations.Configuration("options.role.creeping.poisonDelay", (float[])[15f, 20f, 22.5f, 25f, 27.5f, 30f, 32.5f, 35f, 37.5f, 40f, 45f, 50f, 55f, 60f], 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration DepoisonDurationOption = NebulaAPI.Configurations.Configuration("options.role.creeping.depoisonDuration", (1f, 10f, 1f), 5f, FloatConfigurationDecorator.Second);
    static private readonly IRelativeCoolDownConfiguration PoisonCoolDownOption = NebulaAPI.Configurations.KillConfiguration("options.role.creeping.poisonCooldown", CoolDownType.Relative, (10f, 60f, 2.5f), 30f, (-40f, 40f, 2.5f), 0f, (0.125f, 2f, 0.125f), 1f);
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));

    AbilityAssignmentStatus DefinedRole.AssignmentStatus => AbilityAssignmentStatus.KillersSide;
    static public readonly Creeping MyRole = new();
    static private readonly GameStatsEntry StatsPoison = NebulaAPI.CreateStatsEntry("stats.creeping.poison", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsDepoison = NebulaAPI.CreateStatsEntry("stats.creeping.depoison", GameStatsCategory.Roles, MyRole);
    MultipleAssignmentType DefinedRole.MultipleAssignment => MultipleAssignmentType.AsUniqueKillAbility;
    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.PoisonButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                List<GamePlayer> poisonedInPoison = [];

                var poisonTracker = ObjectTrackers.ForPlayerlike(this, null, MyPlayer, p => ObjectTrackers.PlayerlikeLocalKillablePredicate.Invoke(p) && !ModSingleton<DepoisonBoxManager>.Instance.PoisonedTo(p.RealPlayer));
                var poisonButton = NebulaAPI.Modules.InteractButton(this, MyPlayer, poisonTracker, Virial.Compat.VirtualKeyInput.FixedAbility, "creeping.poison",
                    PoisonCoolDownOption.GetCoolDown(MyPlayer.TeamKillCooldown), "poison", buttonSprite, (target, button) =>
                    {
                        if (GameOperatorManager.Instance?.Run(new PlayerInteractPlayerLocalEvent(MyPlayer, target, new(RealPlayerOnly: true))).IsCanceled ?? true) return;

                        StatsPoison.Progress();
                        if (ModSingleton<DepoisonBoxManager>.Instance?.AmPoisoned ?? false) poisonedInPoison.Add(target.RealPlayer);
                        DepoisonBoxManager.RpcPoison.Invoke((MyPlayer, target.RealPlayer));
                        if (Constants.ShouldPlaySfx()) SoundManager.Instance.PlaySoundImmediate(ViperAudio, false);
                        MyPlayer.VanillaPlayer.NetTransform.RpcSnapTo(target.RealPlayer.VanillaPlayer.transform.position);
                        NebulaAPI.CurrentGame?.KillButtonLikeHandler.StartCooldown();
                    }).SetAsUsurpableButton(this);
                (poisonButton.CoolDownTimer as GameTimer)?.SetAsKillCoolTimer();
                NebulaAPI.CurrentGame?.KillButtonLikeHandler.Register(poisonButton.GetKillButtonLike());


                GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev => {
                    var lastDead = NebulaGameManager.Instance?.LastDead;
                    if (lastDead == null) return;
                    if (lastDead.MyKiller?.AmOwner ?? false) return;
                    if (lastDead.PlayerState != PlayerState.Poisoned) return;
                    if (GamePlayer.AllPlayers.All(p => p.AmOwner == p.IsAlive) && (poisonedInPoison.Contains(lastDead) || (ModSingleton<DepoisonBoxManager>.Instance?.AmPoisoned ?? false)))
                    {   
                        new StaticAchievementToken("creeping.challenge");
                    }
                }, this);
            }
        }

        static private AudioClip ViperAudio
        {
            get
            {
                if (!field) field = GameManager.Instance.deadBodyPrefab[1].CastFast<ViperDeadBody>().acidSplashSFX;
                return field;
            }
        }

        [OnlyMyPlayer, Local]
        private void OnMurderedAnyone(PlayerKillPlayerEvent ev)
        {
            var currentTime = NebulaGameManager.Instance?.CurrentTime ?? 0f;
            if (GamePlayer.AllPlayers.Any(p => p != ev.Dead && p.IsDead && p.DeathTime + 2f > currentTime && (p.MyKiller?.AmOwner ?? false))) new StaticAchievementToken("creeping.common2");
            if (ev.Dead.PlayerState == PlayerState.Poisoned) {
                new StaticAchievementToken("creeping.common1");
                if (MeetingHud.Instance) NebulaAchievementManager.RpcProgressStats.Invoke(("creeping.another1", ev.Dead));
            }
        }

        [Local]
        private void OnGameEnd(GameEndEvent ev)
        {
            if (MyPlayer.IsAlive && ev.EndState.Winners.Test(MyPlayer) && MeetingHud.Instance) new StaticAchievementToken("creeping.common3");
        }
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class DepoisonBox : NebulaSyncStandardObject
    {
        static private IDividedSpriteLoader sprites = DividedSpriteLoader.FromResource("Nebula.Resources.DepoisonConsole.png", 100f, 2, 1);
        private CustomConsole MyConsole;
        public DepoisonBox(Vector2 pos) : base(pos, ZOption.Just, true, sprites.GetSprite(0))
        {
            ModSingleton<DepoisonBoxManager>.Instance.RegisterBox(this);
            MyRenderer.material = VanillaAsset.GetHighlightMaterial();
            UnityHelper.CreateObject<SpriteRenderer>("ConsoleBack", MyRenderer.transform, new(0f, 0f, 0.0001f)).sprite = sprites.GetSprite(1);

            MyConsole = MyRenderer.gameObject.AddComponent<CustomConsole>();
            MyConsole.Renderer = MyRenderer;
            MyConsole.Property = new()
            {
                CanUse = (console) => {
                    var myPlayer = GamePlayer.LocalPlayer;
                    if (myPlayer == null) return false;
                    if (myPlayer.IsDead) return false;
                    if (!myPlayer.CanMove) return false;
                    return ModSingleton<DepoisonBoxManager>.Instance.AmPoisoned;
                },
                Use = console =>
                {
                    var prefab = VanillaAsset.MapAsset[4].ShortTasks[20].MinigamePrefab;
                    ShowerMinigame minigame = GameObject.Instantiate<ShowerMinigame>(prefab.CastFast<ShowerMinigame>());
                    var depoisonGame = minigame.gameObject.AddComponent<DepoisonMinigame>();
                    depoisonGame.SetUp(minigame);
                    GameObject.Destroy(minigame);
                    depoisonGame.transform.SetParent(Camera.main.transform, false);
                    depoisonGame.transform.localPosition = new Vector3(0f, 0f, -50f);
                    depoisonGame.Console = null;
                    depoisonGame.Begin(null!);
                },
                OutlineColor = Color.yellow
            };

        }

        public const string MyTag = "DepoisonBox";
        
        static DepoisonBox() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new DepoisonBox(new(args[0], args[1])));
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    public class DepoisonBoxManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static DepoisonBoxManager() => DIManager.Instance.RegisterModule(() => new DepoisonBoxManager());
        private DepoisonBox box = null!;
        public DepoisonBox Box => box;

        private DepoisonBoxManager()
        {
            ModSingleton<DepoisonBoxManager>.Instance = this;
            this.RegisterPermanently();
        }

        public void RegisterBox(DepoisonBox box) => this.box = box;

        public bool IsAvailable { get; private set; } = false;

        private GamePlayer? MyCreeping;
        private float timer = 1000f;
        private List<(GamePlayer player, float time)> myPoisonedPlayers = [];
        public bool AmPoisoned => MyCreeping != null;
        public bool PoisonedTo(GamePlayer player) => myPoisonedPlayers.Any(entry => entry.player == player);
        [EventPriority(EventPriority.High)]
        void OnGameStarted(GameStartEvent _)
        {
            IsAvailable = GeneralConfigurations.CurrentGameMode == Virial.Game.GameModes.FreePlay || MyRole.IsSpawnableInSomeForm();
            if (!IsAvailable) return;

            //ホストが救急箱を生成する
            if (AmongUsClient.Instance.AmHost)
            {
                var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();
                spawner?.Spawn(1, 7.5f, "depoisonBox", DepoisonBox.MyTag, MapObjectType.DepoisonBox);
            }
        }

        static public RemoteProcess<(GamePlayer creeping, GamePlayer target)> RpcPoison = new("CreepingPoison",
        (message, _) =>
            {
                var depoison = ModSingleton<DepoisonBoxManager>.Instance;
                if (message.creeping.AmOwner) depoison.myPoisonedPlayers.Add((message.target, NebulaGameManager.Instance?.CurrentTime ?? 0f));
                if (message.target.AmOwner) depoison.GetPoison(message.creeping);
            });

        static public RemoteProcess<GamePlayer> RpcDepoison = new("CreepingDepoison",
        (player, _) =>
        {
            var depoison = ModSingleton<DepoisonBoxManager>.Instance;
            if (player.AmOwner) depoison.MyCreeping = null;
            depoison.myPoisonedPlayers.RemoveAll(entry => entry.player == player);
        });

        private bool GetPoison(GamePlayer creeping)
        {
            if(MyCreeping == null)
            {
                MyCreeping = creeping;
                timer = PoisonDelayOption;
                return true;
            }
            return false;
        }

        void OnUpdate(GameUpdateEvent ev)
        {
            if (MyCreeping != null)
            {
                if (ExileController.Instance) return;
                if (MeetingHud.Instance && MeetingHud.Instance.state >= MeetingHud.VoteStates.Results) return;

                timer -= ev.DeltaTime;
                if (timer < 0f)
                {
                    MyCreeping.MurderPlayer(GamePlayer.LocalPlayer, PlayerState.Poisoned, EventDetail.Poisoned, KillParameter.RemoteKill, KillCondition.TargetAlive);
                    MyCreeping = null;
                }
            }
        }

        [EventPriority(EventPriority.Low)]
        void OnDied(PlayerDieEvent ev)
        {
            if(ev.Player.AmOwner) MyCreeping = null;
            myPoisonedPlayers.RemoveAll(entry => entry.player == ev.Player);
        }

        [EventPriority(EventPriority.VeryLow)]
        void DecorateOtherPlayerName(PlayerSetFakeRoleNameEvent ev)
        {
            int i = myPoisonedPlayers.FindIndex(entry => entry.player == ev.Player);
            if (i != -1)
            {
                var found = myPoisonedPlayers[i];
                var left = PoisonDelayOption - ((NebulaGameManager.Instance?.CurrentTime ?? 0f) - found.time);
                if (left > 0f) ev.Append(StringExtensions.Color(" (" + left.ToString("F1") + "s)", Color.gray));
            }
        }
    }


    private static readonly TextureReplacer depoisonImages = new(new ResourceTextureLoader("Nebula.Resources.Depoison.png"));

    public class DepoisonMinigame : Minigame
    {
        static DepoisonMinigame() => ClassInjector.RegisterTypeInIl2Cpp<DepoisonMinigame>();
        public DepoisonMinigame(System.IntPtr ptr) : base(ptr) { }
        public DepoisonMinigame() : base(ClassInjector.DerivedConstructorPointer<DepoisonMinigame>())
        { ClassInjector.DerivedConstructorBody(this); }

        public void SetUp(ShowerMinigame minigame)
        {
            this.Gauge = minigame.Gauge;
            this.PercentText = minigame.PercentText;
            this.MaxTime = DepoisonDurationOption;
        }

        public override void Begin(PlayerTask task)
        {
            this.BeginInternal(task);
            this.timer = this.MaxTime * (float)Progress / 100f;
            this.PercentText.text = ((int)(100 - Progress)).ToString() + "%";
            base.SetupInput(true, false);

            var headerText = transform.GetChild(2).GetChild(1).GetComponent<TextMeshPro>();
            GameObject.Destroy(headerText.GetComponent<TextTranslatorTMP>());
            headerText.text = Language.Translate("role.creeping.minigame.depoison");
            depoisonImages.ReplaceSprite(transform.GetChild(2).GetComponent<SpriteRenderer>());
            depoisonImages.ReplaceSprite(transform.GetChild(3).GetComponent<SpriteRenderer>());
            depoisonImages.ReplaceSprite(transform.GetChild(3).GetChild(1).GetComponent<SpriteRenderer>());
        }

        public void Update()
        {
            if (this.amClosing != Minigame.CloseState.None) return;
            
            this.timer += Time.deltaTime;
            Progress = this.timer / this.MaxTime * 100f;
            this.Gauge.value = 1f - this.timer / this.MaxTime;
            this.PercentText.text = ((int)(100 - Progress)).ToString() + "%";
            if (Progress >= 100)
            {
                if (Constants.ShouldPlaySfx())
                {
                    var doneSfx = VanillaAsset.MapAsset[5].CommonTasks[3].MinigamePrefab.CastFast<MultistageMinigame>().Stages[1].CastFast<RoastMarshmallowFireMinigame>().sfxMarshmallowDone;
                    SoundManager.Instance.PlaySoundImmediate(doneSfx, false, 1.5f, 1f, null);
                }
                StatsDepoison.Progress();
                DepoisonBoxManager.RpcDepoison.Invoke(GamePlayer.LocalPlayer!);
                base.StartCoroutine(base.CoStartClose(0.5f));
            }
        }

        public float Progress = 0f;
        public VerticalGauge Gauge;
        public TextMeshPro PercentText;
        private float timer;
        public float MaxTime = 12f;


        public override void Close()
        {
            this.CloseInternal();
        }
    }
}
