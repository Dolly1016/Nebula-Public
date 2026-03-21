using Nebula.Behavior;
using Nebula.Game.Statistics;
using Nebula.Roles.Impostor;
using Nebula.Roles.Modifier;
using Nebula.VoiceChat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;
using Virial.Game.Console;
using Virial.Media;
using Virial.Runtime;
using static Nebula.Roles.Impostor.Thurifer;

namespace Nebula.Roles.Neutral;

internal class Plague : DefinedRoleTemplate, DefinedRole, IAssignableDocument
{
    static private Image useButtonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.PlagueSampleButton.png", 115f);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class PoisonPod : NebulaSyncShadowObject
    {
        static private IDividedSpriteLoader image = DividedSpriteLoader.FromResource("Nebula.Resources.PlaguePod.png", 130f, 5, 2);
        static internal Image GetPlantImage => image.AsLoader(1);
        static private UseButtonAlternative useButtonAlternative;
        static private void Preprocess(NebulaPreprocessor preprocessor)
        {
            useButtonAlternative = new UseButtonAlternative(useButtonImage, () => 
                GamePlayer.LocalPlayer?.Role.Role == MyRole && 
                (GamePlayer.LocalPlayer?.IsAlive ?? false) &&
                (GamePlayer.LocalPlayer?.TryGetRole<Instance>(out var plague) ?? false) && 
                plague.CanGainPoison,
                console =>
            {
                if(NebulaSyncObject.GetObjects<PoisonPod>(MyTag).Find(obj => obj.MyRenderer.gameObject.GetInstanceID() == console.gameObject.GetInstanceID(), out var pod))
                {
                    if ((GamePlayer.LocalPlayer?.TryGetRole<Instance>(out var plague) ?? false) && plague.TryGainPoison()) pod.Use();
                }
            }, false, Color.yellow);
        }
        public PoisonPod(Vector2 pos) : base(pos, ZOption.Just, image.GetSprite(1), Color.white)
        {
            SetBackRenderer(image.GetSprite(0));
            ModSingleton<PoisonPodManager>.Instance.RegisterPod(this);
            MyRenderer.material = VanillaAsset.GetHighlightMaterial();

            isFull = true;

            var console = SystemConsolize(MyRenderer.gameObject, MyRenderer, useButtonAlternative.ImageNames, null!);
        }

        public const string MyTag = "PoisonPod";

        private float animInterval = 0f;
        private int animIndex = -1;
        private bool isFull = false;
        public bool IsFull
        {
            get => isFull; set
            {
                isFull = value;
                if (MyRenderer.TryGetComponent<Collider2D>(out var collider))
                {
                    collider.enabled = isFull;
                }
            }
        }
        void AnimUpdate(GameUpdateEvent ev)
        {
            void UpdateImage(int index)
            {
                if (animIndex == index) return;
                animIndex = index;
                Sprite = image.GetSprite(index);
            }
            if (!isFull)
            {
                UpdateImage(1);
            }
            else
            {
                if (animIndex < 2) UpdateImage(2);
                else
                {
                    animInterval -= ev.DeltaTime;
                    if(animInterval < 0f)
                    {
                        UpdateImage((animIndex - 2 + 1) % 8 + 2);
                        animInterval = animIndex == 2 ? 0.8f : 0.09f;
                    }
                }
            }
        }

        void TryGrow(MeetingEndEvent ev)
        {
            if (VisualPodsOption)
            {
                if (!AmongUsClient.Instance.AmHost) return;
                if(System.Random.Shared.NextDouble() < 0.5f) PoisonPodManager.RpcUsePod(this.ObjectId, true);
            }
            else
            {
                if (System.Random.Shared.NextDouble() < 0.5f) IsFull = true;
            }
        }
        
        public void Use()
        {
            if (VisualPodsOption)
            {
                PoisonPodManager.RpcUsePod(this.ObjectId);
            }
            else
            {
                IsFull = false;
            }
        }

        static PoisonPod() => NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new PoisonPod(new(args[0], args[1])));
    }

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    public class PoisonPodManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static PoisonPodManager() => DIManager.Instance.RegisterModule(() => new PoisonPodManager());
        private List<PoisonPod> allThuribulums = new();
        public IEnumerable<PoisonPod> AllPods => allThuribulums;

        private PoisonPodManager()
        {
            ModSingleton<PoisonPodManager>.Instance = this;
            this.RegisterPermanently();
        }

        public void RegisterPod(PoisonPod poisonPod) => allThuribulums.Add(poisonPod);

        public bool IsAvailable { get; private set; } = false;
        void OnGameStarted(GameStartEvent _)
        {
            IsAvailable = GeneralConfigurations.CurrentGameMode == Virial.Game.GameModes.FreePlay || MyRole.IsSpawnableInSomeForm();

            if (!IsAvailable) return;

            //ホストがポッドを生成する
            if (AmongUsClient.Instance.AmHost)
            {
                var spawner = NebulaAPI.CurrentGame?.GetModule<IMapObjectSpawner>();
                spawner?.Spawn(NumOfPodsOption, 7.5f, "plaguePod", PoisonPod.MyTag, MapObjectType.SmallInCorner);
            }
        }

        internal static void RpcUsePod(int id, bool full = false)
        {
            RpcUpdatePod.Invoke((id, full));
        }
        static RemoteProcess<(int id, bool full)> RpcUpdatePod = new("UpdatePlaguePod",
            (message, _) =>
            {
                if(ModSingleton<PoisonPodManager>.Instance.AllPods.Find(pod => pod.ObjectId == message.id, out var found))
                {
                    found.IsFull = message.full;
                }
            });
    }

    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.plague", new(151, 194, 22), TeamRevealType.OnlyMe);

    private Plague() : base("plague", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [NumOfBottlesOption, NumOfPodsOption, VisualPodsOption, VentConfiguration, InfectDurationOption, InfectDistanceOption])
    {
    }


    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments)
    {
        var bottlesLength = arguments.Get(0, 0);
        int[]? bottlesInfo = bottlesLength > 0 ? arguments[1..(1 + bottlesLength)] : null;
        var progressLength = arguments.Get(1 + bottlesLength, 0);
        int[]? progress = progressLength > 0 ? arguments[(2 + bottlesLength)..(2 + bottlesLength + progressLength * 2)] : null;
        return new Instance(player, bottlesInfo, progress);
    }

    static private IntegerConfiguration NumOfBottlesOption = NebulaAPI.Configurations.Configuration("options.role.plague.numOfBottles", (1, 5, 1), 2);
    static private IntegerConfiguration NumOfPodsOption = NebulaAPI.Configurations.Configuration("options.role.plague.numOfPods", (1, 8, 1), 4);
    static private BoolConfiguration VisualPodsOption = NebulaAPI.Configurations.Configuration("options.role.plague.visualPods", false);
    static private FloatConfiguration InfectDurationOption = NebulaAPI.Configurations.Configuration("options.role.plague.infectDuration", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration InfectDistanceOption = NebulaAPI.Configurations.Configuration("options.role.plague.infectDistance", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.plague.vent", true);

    static public Plague MyRole = new Plague();

    static private GameStatsEntry StatsBottle = NebulaAPI.CreateStatsEntry("stats.plague.bottle", GameStatsCategory.Roles, MyRole);

    static private int MaxPoisonLevel => 3;
    static private int NumOfBottles => NumOfBottlesOption;

    static private readonly Image infectButtonImage = SpriteLoader.FromResource("Nebula.Resources.Buttons.InfectButton.png", 115f);

    bool IAssignableDocument.HasTips => true;
    bool IAssignableDocument.HasAbility => true;
    bool IAssignableDocument.HasWinCondition => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(PoisonPod.GetPlantImage, "role.plague.ability.plant");
        yield return new(useButtonImage, "role.plague.ability.use");
        yield return new(infectButtonImage, "role.plague.ability.infect");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%PLANTS%", NumOfPodsOption.GetValue().ToString());
        yield return new("%BOTTLES%", NumOfBottles.ToString());
        yield return new("%VISUAL%", VisualPodsOption ? Language.Translate("role.plague.tips.visual.visual") : Language.Translate("role.plague.tips.visual.indiv"));
    }


    [NebulaRPCHolder]
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        int[]? RuntimeAssignable.RoleArguments
        {
            get
            {
                var bottlesData = currentBottles.RawData;
                return [bottlesData.Length, ..bottlesData];
            }
        }
        public override DefinedRole Role => MyRole;
        
        [NebulaRPCHolder]
        private class Bottles
        {
            private int[] bottles;
            int netIndex = 0;
            Action<Bottles, int?>? onUpdated;

            public Bottles(int[]? data, Action<Bottles, int?>? onUpdated)
            {
                bottles = data ?? new int[NumOfBottles];
                this.onUpdated = onUpdated;
            }

            public bool ShouldDisplayAt(int index) => index < bottles.Length && index >= 0 && bottles[index] != -1;
            public int GetStageAt(int index) => ShouldDisplayAt(index) ? bottles[index] : -1;
            public int[] RawData => bottles;
            public bool IsNoBottle => bottles.All(num => num == -1);
            public bool CanGainPoison => GetAvailableBottleIndexToGain() != -1;
            private int GetAvailableBottleIndexToGain()
            {
                for (int i = 0; i < bottles.Length; i++)
                {
                    if (bottles[i] == -1) continue;
                    if (bottles[i] < MaxPoisonLevel) return i;
                }
                return -1;
            }

            public bool TryGainPoison()
            {
                int index = GetAvailableBottleIndexToGain();
                if (index == -1) return false;
                bottles[index]++;
                OnUpdateAsOwner(index);
                return true;
            }

            public bool CanConsumeBottle => GetAvailableBottleIndexToConsume() != -1;

            private int GetAvailableBottleIndexToConsume()
            {
                for (int i = 0; i < bottles.Length; i++)
                {
                    if (bottles[i] == MaxPoisonLevel) return i;
                }
                return -1;
            }

            int consumeNum = 0;
            public bool TryConsumeBottle()
            {
                int index = GetAvailableBottleIndexToConsume();
                if (index == -1) return false;
                bottles[index] = -1;
                OnUpdateAsOwner(null);
                consumeNum++;
                if (consumeNum == 2) new StaticAchievementToken("plague.common1");
                return true;
            }

            //情報の更新はPlague本人のみが行う前提。
            private void OnUpdateAsOwner(int? lastGained)
            {
                netIndex++;
                RpcUpdate.Invoke((GamePlayer.LocalPlayer!, bottles, netIndex));
                onUpdated?.Invoke(this, lastGained);
            }

            private void Deserialize(int[] data, int netIndex)
            {
                if (netIndex <= this.netIndex) return;
                bottles = data;
                this.netIndex = netIndex;
                this.onUpdated?.Invoke(this, null);
            }

            private static RemoteProcess<(GamePlayer plague, int[] data, int netIndex)> RpcUpdate = new("PlagueUpdateBottle", (message, calledByMe) =>
            {
                if (calledByMe) return;
                if (message.plague.TryGetRole<Instance>(out var plague))
                {
                    plague.currentBottles.Deserialize(message.data, message.netIndex);
                }
            });
        }

        private class BottlesView
        {
            static private MultiImage bottleImage = DividedSpriteLoader.FromResource("Nebula.Resources.PlagueBottle.png", 150, 4, 1);
            private SpriteRenderer[] bottleRenderers;
            private UnityEngine.Vector3 GetLocalPos(int index) => new((float)index * 0.6f - 0.2f, 0f, -0.05f + (float)index * 0.01f);
            private const float UnreadiedBottleScale = 0.7f;
            private FlexibleLifespan BottleLifespan;
            public BottlesView(ILifespan lifespan)
            {
                var holder = HudContent.InstantiateContent("PlagueIcons", true, true, false, false);
                holder.SetPriority(-10);
                BottleLifespan = new FlexibleLifespan(lifespan);
                
                BottleLifespan.BindGameObject(holder.gameObject);
                bottleRenderers = new SpriteRenderer[NumOfBottles];

                for (int i = 0; i < NumOfBottles; i++)
                {
                    var bottle = UnityHelper.CreateObject<SpriteRenderer>("Bottle", holder.transform, Vector3.zero, LayerExpansion.GetUILayer());
                    bottle.sprite = bottleImage.GetSprite(0);
                    bottle.transform.localPosition = GetLocalPos(i);
                    bottle.transform.localScale = new(UnreadiedBottleScale, UnreadiedBottleScale, 1f);
                    bottleRenderers[i] = bottle;
                }
            }
            public void UpdateView(Bottles bottles, int? gained)
            {
                if (BottleLifespan.IsDeadObject) return;

                int displayed = 0;
                for (int i = 0; i < bottleRenderers.Length; i++)
                {
                    if (!bottles.ShouldDisplayAt(i))
                    {
                        bottleRenderers[i].enabled = false;
                        continue;
                    }
                    
                    bottleRenderers[i].enabled = true;
                    int stage = bottles.GetStageAt(i);
                    bottleRenderers[i].sprite = bottleImage.GetSprite(stage);
                    bottleRenderers[i].color = stage == MaxPoisonLevel ? Color.white : new(0.8f, 0.8f, 0.8f);
                    ManagedEffects.Smooth(bottleRenderers[i].transform, GetLocalPos(displayed), 1f).StartOnScene();
                    if(gained.HasValue && gained == i) ManagedEffects.CoShakeTubelike(bottleRenderers[i].transform, UnreadiedBottleScale, bottles.GetStageAt(i) == MaxPoisonLevel ? 1f : UnreadiedBottleScale).StartOnScene();
                    displayed++;
                }

                if (displayed == 0) BottleLifespan.Release();
            }
        }

        private class InfectProgress
        {
            Dictionary<byte, float> progress = [];
            Dictionary<byte, float> contribution = [];
            bool updated = false;
            public bool ShouldShare => updated;
            public InfectProgress(int[] arg)
            {
                progress = [];
                for(int i = 0;i<arg.Length / 2; i++)
                {
                    progress[(byte)arg[i * 2]] = (float)arg[i * 2 + 1] / 100f;
                }
            }

            public float GetProgress(GamePlayer player) => progress.TryGetValue(player.PlayerId, out var v) ? v : 0f;
            public void AddProgress(GamePlayer player, float add, GamePlayer spreader)
            {
                var cont = add;

                if (progress.TryGetValue(player.PlayerId, out var v)) add += v;
                else v = 0f;
                progress[player.PlayerId] = add;
                if (v < 1f && !(add < 1f)) NebulaAchievementManager.RpcClearAchievement.Invoke(("plague.common2", spreader));
                updated = true;

                if (contribution.TryGetValue(spreader.PlayerId, out var c)) cont += c;
                else c = 0f;
                contribution[spreader.PlayerId] = cont;
                if(c < 5f && !(cont < 5f)) NebulaAchievementManager.RpcClearAchievement.Invoke(("plague.common3", spreader));
            }

            public bool IsInfectedFully(GamePlayer player) => !(GetProgress(player) < 1f);
            public bool IsInfectedDirectly(GamePlayer player) => GetProgress(player) > 50f;

            public void Infect(GamePlayer player)
            {
                updated = true;
                progress[player.PlayerId] = 100f;
            }

            public (byte playerId, float progress)[] SerializeForRpc()
            {
                updated = false;
                return progress.Select(kv => (kv.Key, kv.Value)).ToArray();
            }
            public int[] SerializeForRoleArgument() => [progress.Count, ..progress.SelectMany(kv => (int[])[(int)kv.Key, (int)(kv.Value * 100f)])];
            public void DeserializeFromRpc((byte playerId, float progress)[] dic)
            {
                progress.Clear();
                foreach (var tuple in dic) progress[tuple.playerId] = tuple.progress;
            }

            internal IEnumerable<GamePlayer> GetDirectlyInfected() => GamePlayer.AllPlayers.Where(p => GetProgress(p) > 50f);
            internal IEnumerable<GamePlayer> GetInfected() => GamePlayer.AllPlayers.Where(p => !p.AmOwner && !(GetProgress(p) < 1f));

            public bool CanWin => GamePlayer.AllPlayers.All(p => p.AmOwner || p.IsDead || IsInfectedFully(p));
        }

        private Bottles currentBottles;
        private BottlesView? bottlesView;
        public bool CanGainPoison => currentBottles.CanGainPoison;
        public bool TryGainPoison() => currentBottles.TryGainPoison();
       
        public Instance(GamePlayer player, int[]? bottleInfo, int[]? progressInfo) : base(player, VentConfiguration)
        {
            bottleInfo ??= new int[NumOfBottles];
            bottlesView = player.AmOwner ? new BottlesView(this) : null;
            currentBottles = new Bottles(bottleInfo, (bottles, gained) => bottlesView?.UpdateView(bottles, gained));
            bottlesView?.UpdateView(currentBottles, null);

            this.progress = new(progressInfo ?? []);
        }

        PlayersIconHolder iconHolder;
        InfectProgress progress;
        void UpdatePlayerIcon(PlayerIconInfo icon)
        {
            var prog = progress.GetProgress(icon.Player);
            if (prog > 0f)
            {
                if (prog < 1f)
                {
                    icon.SetAlpha(true);
                    icon.SetText($"{(prog * 100):F1}%", 2f);
                }
                else
                {
                    icon.SetAlpha(false);
                    icon.SetText(null);
                }
            }
            else
            {
                icon.SetAlpha(true);
                icon.SetText(null);
            }
        }

        void UpdateAllPlayerIcons() => iconHolder.AllIcons.Do(UpdatePlayerIcon);
        
        void SetUpAllAlives()
        {
            iconHolder = new(true);
            iconHolder.XInterval = 0.33f;
            iconHolder.Register(this);
            foreach(var player in GamePlayer.AllPlayers)
            {
                if (player.AmOwner) continue;
                if (player.IsDead) continue;
                var icon = iconHolder.AddPlayer(player);
                UpdatePlayerIcon(icon);
            }
        }

        [Local]
        void OnMeetingStart(MeetingPreStartEvent ev)
        {
            foreach(var infected in progress.GetInfected())
            {
                if (!infected.TryGetModifier<PlagueInfected.Instance>(out _)) RpcRequestAddInfectedModifier.Invoke(infected);
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev) {
            var remove = iconHolder.AllIcons.Where(p => p.Player.IsDead).ToArray();
            remove.Do(p => iconHolder.Remove(p));
            ShareProgress();
        }

        public override void OnActivated()
        {
            if (AmOwner)
            {
                var tracker = NebulaAPI.Modules.PlayerlikeTracker(this, MyPlayer, target => currentBottles.CanConsumeBottle && progress.GetProgress(target.RealPlayer) < 1f);
                var infectButton = NebulaAPI.Modules.InteractButton(this, MyPlayer, tracker, new PlayerInteractParameter(true, false, true), Virial.Compat.VirtualKeyInput.Ability, null, 10f, "plague.infect", infectButtonImage,
                    (target, button) =>
                    {
                        if (currentBottles.TryConsumeBottle())
                        {
                            progress.Infect(target.RealPlayer);
                            button.StartCoolDown();
                        }
                    }, _ => currentBottles.CanConsumeBottle);

                SetUpAllAlives();
            }
        }


        float updateInterval = 0f;
        float afterMeetingCooldown = 0f;
        bool sentWinningRequest = false;

        [Local]
        void OnUpdate(GameUpdateEvent ev)
        {
            updateInterval -= Time.deltaTime;

            float maxSpreadDistance = InfectDistanceOption;
            float requiredTimeToInfect = InfectDurationOption;

            if (!MeetingHud.Instance && !ExileController.Instance)
            {
                if (afterMeetingCooldown > 0f)
                {
                    afterMeetingCooldown -= Time.deltaTime;
                }
                else
                {
                    //感染が進行する対象
                    foreach (var p in GamePlayer.AllPlayers)
                    {
                        if (p.AmOwner) continue;
                        if (p.IsDead) continue;
                        if (progress.IsInfectedFully(p)) continue;

                        float infectedDistance = 1000f;
                        GamePlayer? spreader = null;

                        //感染源
                        foreach (var s in GamePlayer.AllPlayers)
                        {
                            if (p.AmOwner) continue;
                            if (s.IsDead) continue;
                            if (!progress.IsInfectedFully(s)) continue;
                            float distance = s.Position.Distance(p.Position);
                            if (distance > infectedDistance) continue;
                            if (NebulaAPI.CurrentGame?.CurrentMap?.AnyShadowsBetween(p.Position, s.Position) ?? false) continue;
                            infectedDistance = distance;
                            spreader = s;
                        }

                        if (spreader != null && infectedDistance < maxSpreadDistance) progress.AddProgress(p, 1f / requiredTimeToInfect * Time.deltaTime, spreader);
                    }

                    if(!sentWinningRequest && progress.CanWin)
                    {
                        var winners = BitMasks.AsPlayer(MyPlayer);
                        progress.GetDirectlyInfected().Do(p => winners.Add(p));
                        NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.PlagueWin, (int)winners.AsRawPattern);
                        sentWinningRequest = true;
                    }
                }
            }
            else
            {
                afterMeetingCooldown = 10f;
            }

            UpdateAllPlayerIcons();
            if (updateInterval < 0f && progress.ShouldShare)
            {
                RpcShareProgress.Invoke((MyPlayer, progress.SerializeForRpc()));
                updateInterval = 0.3f;
            }
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (progress.CanWin && ev.EndState.EndCondition != NebulaGameEnd.PlagueWin && ev.EndState.Winners.Test(MyPlayer)) new StaticAchievementToken("plague.challenge");
        }

        [Local]
        void DecorateDirectlyInfected(PlayerDecorateNameEvent ev)
        {
            if (!ev.CanSeeAllInfo && progress.IsInfectedDirectly(ev.Player)) ev.Name += PlagueInfected.MyRole.GetRoleIconTagSmall();
        }

        GUIWidget RuntimeAssignable.ProgressWidget => ProgressGUI.Holder(
            ProgressGUI.OneLineText(Language.Translate("role.plague.gui.progress")),
            ProgressGUI.Holder(GUI.API.VerticalHolder(GUIAlignment.Left, GamePlayer.AllOrderedPlayers.Where(p => p != MyPlayer && p.IsAlive).SelectMany(p =>
            {
                var nameText = ProgressGUI.OneLineText("-" + p.ColoredName, 0.7f);
                nameText.PostponesConsideringSize = true;
                return (IEnumerable<GUIWidget>)[nameText, ProgressGUI.RawText(progress.IsInfectedFully(p) ? Language.Translate("role.plague.gui.infected").Color(MyRole.UnityColor) : (progress.GetProgress(p) * 100f).ToString("F1") + "%", GUIAlignment.Right)];
            })
            )).Move(new(0.04f, 0f))
            );

        private void ShareProgress() => RpcShareProgress.Invoke((MyPlayer, progress.SerializeForRpc()));
        private static readonly RemoteProcess<(GamePlayer plague, (byte playerId, float progress)[] progress)> RpcShareProgress = new("PlagueProgress", (message, _) =>
        {
            if(message.plague.TryGetRole<Instance>(out var plague))
            {
                plague.progress.DeserializeFromRpc(message.progress);
            }
        }, false);

        private static readonly RemoteProcess<GamePlayer> RpcRequestAddInfectedModifier = new("PlagueModifier", (infected, _) =>
        {
            if (!AmongUsClient.Instance.AmHost) return;
            if (infected.TryGetModifier<PlagueInfected.Instance>(out var unused)) return;
            infected.AddModifier(PlagueInfected.MyRole);
        });

    }

}


public class PlagueInfected : DefinedModifierTemplate, DefinedModifier
{
    private PlagueInfected() : base("plagueInfected", Plague.MyTeam.Color, [], true, () => false)
    {
    }

    static public PlagueInfected MyRole = new PlagueInfected();
    RuntimeModifier RuntimeAssignableGenerator<RuntimeModifier>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    public class Instance : RuntimeAssignableTemplate, RuntimeModifier
    {
        DefinedModifier RuntimeModifier.Modifier => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        void RuntimeAssignable.OnActivated()
        {
        }

        void RuntimeAssignable.DecorateNameConstantly(ref string name, bool canSeeAllInfo, bool inEndScene)
        {
            if (AmOwner || canSeeAllInfo) name += MyRole.GetRoleIconTagSmall();
        }
    }
}
