using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial;
using Sentry.Internal;
using Nebula.Game.Statistics;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using static UnityEngine.GridBrushBase;
using Virial.Events.Game;
using Virial.Media;

namespace Nebula.Roles.Impostor;

[NebulaRPCHolder]
internal class Nightmare : DefinedSingleAbilityRoleTemplate<Nightmare.Ability>, DefinedRole
{
    private Nightmare() : base("nightmare", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [placeCooldownOption, nightmareCooldownOption, nightmareDurationOption, darknessSizeOption, inShadowLightSizeOption, disposableNightSeedOption, numOfNightSeedOption])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        //ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Berserker.png");

        GameActionTypes.NightmarePlacementAction = new("nightmare.nightSeed", this, isPlacementAction: true);
    }

    static private FloatConfiguration placeCooldownOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.placeCooldown", (0f, 60f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration nightmareCooldownOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.nightmareCooldown", (0f, 80f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration nightmareDurationOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.nightmareDuration", (float[])[2f, 3f, 4f, 5f, 7.5f, 10f, 12.5f, 15f, 17.5f, 20f, 22.5f, 25f, 30f, 35f, 40f], 10f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration darknessSizeOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.nightSize", (float[])[0.5f, 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f, 3f, 4f, 5f], 1f, FloatConfigurationDecorator.Ratio);
    static private BoolConfiguration disposableNightSeedOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.disposableNightSeed", false);
    static private IntegerConfiguration numOfNightSeedOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.numOfNightSeed", (1, 30), 5, () => !disposableNightSeedOption);
    static private FloatConfiguration inShadowLightSizeOption = NebulaAPI.Configurations.Configuration("options.role.nightmare.inShadowLightSize", (0f, 2f, 0.25f), 0.5f, FloatConfigurationDecorator.Ratio);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0));
    bool DefinedRole.IsJackalizable => true;
    static public Nightmare MyRole = new Nightmare();
    static private GameStatsEntry StatsPlaceNightSeed = NebulaAPI.CreateStatsEntry("stats.nightmare.place", GameStatsCategory.Roles, MyRole);
    static private GameStatsEntry StatsNightmare = NebulaAPI.CreateStatsEntry("stats.nightmare.nightmare", GameStatsCategory.Roles, MyRole);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class NightmareSeed : NebulaSyncStandardObject
    {
        public static string MyTag = "Nightmare";
        public static string MyTempTag = "NightmareTemp";
        private Nightmare.Ability? nightmareRole;
        public bool WillDespawn = false;
        public GamePlayer? OverwritedPlayer = null;
        public GamePlayer ActualOwner => OverwritedPlayer ?? Owner;
        public NightmareSeed(Vector2 pos) : base(pos, ZOption.Back, false, skullSprite.GetSprite(0), false) { 
        }

        public override void OnInstantiated() {
            base.OnInstantiated();
            ActualOwner.TryGetAbility<Nightmare.Ability>(out nightmareRole);
            if (ActualOwner.AmOwner && !WillDespawn) Color = new(1f, 1f, 1f, 0.5f);
        }

        static private MultiImage skullSprite = DividedSpriteLoader.FromResource("Nebula.Resources.NightmareSkull.png", 120f, 5, 1);
        void OnUpdate(GameUpdateEvent ev)
        {
            if(nightmareRole != null)
            {
                if (nightmareRole.EffectIsActive && Color.a > 0.8f)
                {
                    Sprite = skullSprite.GetSprite(1 + (int)((Time.time / 0.2f) + (ObjectId % 10)) % 4);
                }
                else
                {
                    Sprite = skullSprite.GetSprite(0);
                }
            }

            if (WillDespawn && (nightmareRole == null || !nightmareRole.EffectIsActive)) this.Release();
        }

        static NightmareSeed()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new NightmareSeed(new(args[0], args[1])));
            NebulaSyncObject.RegisterInstantiater(MyTempTag, (args) => new NightmareSeed(new(args[0], args[1])) { OverwritedPlayer = GamePlayer.GetPlayer((byte)args[2]), WillDespawn = true });
        }
    }

    private class NightmareSeedInfo
    {
        private NebulaSyncObjectReference MySeed;
        private bool Shared = false;
        public Vector2 Position { get; private init; }

        public NightmareSeedInfo(Vector2 pos)
        {
            this.Position = pos;
            MySeed = NebulaSyncObject.LocalInstantiate(NightmareSeed.MyTag, [pos.x, pos.y]);
        }

        public void OnNight()
        {
            if (disposableNightSeedOption)
            {
                var seed = (MySeed.SyncObject as NightmareSeed);
                if (seed != null)
                {
                    seed.WillDespawn = true;
                    seed.Color = Color.white;
                }
            }
            else if (!Shared)
            {
                Shared = true;
                if (MySeed.SyncObject != null)
                {
                    MySeed.SyncObject.ReflectInstantiationGlobally();
                    (MySeed.SyncObject as NebulaSyncStandardObject)!.Color = Color.white;
                }

            }
        }
    }

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        private ModAbilityButtonImpl? cleanButton = null;

        static private Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.NightmarePlaceButton.png", 115f);
        static private Image nightmareButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.NightmareButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public Ability(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                List<NightmareSeedInfo> placed = [];

                var placeButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "nightmare.place",
                    placeCooldownOption, "place", placeButtonSprite, visibility: _ => disposableNightSeedOption || placed.Count < numOfNightSeedOption)
                    .SetAsUsurpableButton(this);
                if (!disposableNightSeedOption) placeButton.ShowUsesIcon(0, numOfNightSeedOption.GetValue().ToString());
                placeButton.OnClick = (button) => {
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, GameActionTypes.NightmarePlacementAction);
                    placed.Add(new NightmareSeedInfo(MyPlayer.Position + new Virial.Compat.Vector2(0f, -0.1f)));
                    StatsPlaceNightSeed.Progress();

                    placeButton.UpdateUsesIcon((numOfNightSeedOption - placed.Count).ToString());
                    placeButton.StartCoolDown();
                };
                
                var acTokenAnother = AchievementTokens.FirstFailedAchievementToken("nightmare.another2", MyPlayer, this);
                var nightmareButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.SecondaryAbility, "nightmare.nightmare",
                    nightmareCooldownOption, nightmareDurationOption, "nightmare", nightmareButtonSprite,
                    _ => placed.Count > 0).SetAsUsurpableButton(this);
                nightmareButton.OnEffectStart = (button) =>
                {
                    RpcNight.Invoke((MyPlayer,placed.Select(p => p.Position).ToArray()));
                    placed.Do(p => p.OnNight());
                    StatsNightmare.Progress();
                    acTokenAnother.Value.triggered = true;
                    if (disposableNightSeedOption) placed.Clear();
                };
                nightmareButton.OnEffectEnd = (button) => button.StartCoolDown();
            }
        }

        public ILifespan? EffectLifespan { get; set; } = null;
        public bool EffectIsActive => EffectLifespan?.IsAliveObject ?? false;
    }

    static private Image[] shadowSprites = Helpers.Sequential(5).Select(i => SpriteLoader.FromResource($"Nebula.Resources.Shadow.Smoke{i}.png", 100f)).ToArray();
    static private Image lightSprite = SpriteLoader.FromResource($"Nebula.Resources.LightSharpMask.png", 100f);
    static private void GenerateShadow(Vector2 pos, Func<bool> asSurviver)
    {
        var shadow = AmongUsUtil.GenerateCustomShadow(pos, shadowSprites[0].GetSprite());
        shadow.Predicate = asSurviver;
        shadow.MulCoeffForGhosts = 0.9f;
        shadow.MulCoeffForSurvivers = 0.45f;
        shadow.SetAlpha(1f);
        shadow.SetBlend(0f);

        float baseScale = 1.5f * darknessSizeOption;
        shadow.gameObject.transform.localScale = new Vector3(baseScale, baseScale, 1f);

        bool isDisappearing = false;

        IEnumerator CoChangeSpriteAndScaling()
        {
            for(int i = 0; i < 4; i++)
            {
                yield return Effects.Wait(0.125f);
                shadow.SetSprite(shadowSprites[i + 1].GetSprite());
            }
            float t = 0f;
            float additional = 0f;
            while (shadow)
            {
                float p = baseScale + (float)Math.Sin(t * 7.7f) * 0.03f;
                if (isDisappearing) additional += Time.deltaTime / 0.8f;
                shadow.transform.localScale = new(p + additional, p + additional, 1f);
                t += Time.deltaTime;
                yield return null;
            }
        }
        IEnumerator CoUpdate()
        {
            float t = 0f;
            float tGoal = 0.125f * 2.4f;
            while (t < tGoal)
            {
                t += Time.deltaTime;
                shadow.SetBlend(t / tGoal * 0.4f);
                yield return null;
            }

            float p = 0.4f;
            while (p < 1f)
            {
                shadow.SetBlend(p);
                p += Time.deltaTime / 0.28f;
                yield return null;
            }
            shadow.SetBlend(1f);

            while (!isDisappearing) yield return null;

            p = 1f;
            while (p > 0f)
            {
                shadow.SetAlpha(p);
                p -= Time.deltaTime / 0.9f;
                yield return null;
            }
            GameObject.Destroy(shadow.gameObject);
        }
        NebulaManager.Instance.StartCoroutine(CoChangeSpriteAndScaling().WrapToIl2Cpp());
        NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        NebulaManager.Instance.StartDelayAction(nightmareDurationOption, () => isDisappearing = true);
    }

    private class InShadowLight : IGameOperator, ILifespan
    {
        public bool IsDisappearing = false;
        private float Alpha = 0f;
        private float ZeroKeep = 1.2f;
        public bool IsDeadObject => IsDisappearing && !(Alpha > 0f);
        private Vector2[] Positions;
        private SpriteRenderer Light;
        public InShadowLight(Vector2[] positions)
        {
            this.Positions = positions;
            this.Light = AmongUsUtil.GenerateCustomLight(GamePlayer.LocalPlayer!.Position, lightSprite.GetSprite());
            this.Light.transform.SetParent(GamePlayer.LocalPlayer!.VanillaPlayer.transform);
            this.Light.transform.SetWorldZ(-11f);
            Light.material.color = new UnityEngine.Color(1f, 1f, 1f, 0f);
        }

        void OnUpdate(GameHudUpdateEvent ev)
        {
            if(ZeroKeep > 0f)
            {
                ZeroKeep -= Time.deltaTime;
                return;
            }

            var playerPos = GamePlayer.LocalPlayer!.Position.ToUnityVector();
            var isInNight = Positions.Any(p => p.Distance(playerPos) < darknessSizeOption * 1.54f);

            if (IsDisappearing) Alpha -= Time.deltaTime * 1.6f;
            else if (!isInNight) Alpha -= Time.deltaTime * 3.4f;
            else Alpha += Time.deltaTime * 0.8f;
            Alpha = Math.Clamp(Alpha, 0f, 1f);

            Light.material.color = new UnityEngine.Color(1f, 1f, 1f, Alpha * 0.5f);
            float scale = (0.4f + Alpha * 0.6f) * 1.96f * inShadowLightSizeOption;
            Light.transform.localScale = new(scale, scale, 1f);
        }

        void IGameOperator.OnReleased()
        {
            if (Light) GameObject.Destroy(Light.gameObject);
        }
    }
    static private RemoteProcess<(GamePlayer nightmare, Vector2[] pos)> RpcNight = new("Nightmare", (message, calledByMe) => {
        message.pos.Do(p =>
        {
            GenerateShadow(p, () => message.nightmare.CanKill(GamePlayer.LocalPlayer!));
            if (!calledByMe && disposableNightSeedOption) NebulaSyncObject.LocalInstantiate(NightmareSeed.MyTempTag, [p.x, p.y, message.nightmare.PlayerId]);
        });

        //称号のためのチェック
        var pos = message.pos;
        var nightmare = message.nightmare;
        bool IsInNight(Vector2 position) => pos.Any(p => p.Distance(position) < darknessSizeOption * 1.54f);
        bool nightmareKilled = false;
        int totalDead = 0;

        var lifespan = FunctionalLifespan.GetTimeLifespan(nightmareDurationOption);
        GameOperatorManager.Instance?.Subscribe<PlayerMurderedEvent>(ev =>
        {
            bool murdererIsInNight = IsInNight(ev.Murderer.Position);
            if (ev.Murderer.AmOwner && murdererIsInNight) new StaticAchievementToken("nightmare.common2");

            if (ev.Dead.AmOwner && IsInNight(ev.Dead.Position)) new StaticAchievementToken("nightmare.another1");

            if (murdererIsInNight)
            {
                totalDead++;
                nightmareKilled |= ev.Murderer == nightmare;

                if (totalDead >= 3 && nightmareKilled && nightmare.AmOwner) new StaticAchievementToken("nightmare.challenge");
            }
        }, lifespan);

        if(nightmare.TryGetAbility<Nightmare.Ability>(out var ability)) ability.EffectLifespan = lifespan;
        
        if (inShadowLightSizeOption > 0f)
        {
            var inShdowLight = new InShadowLight(pos).Register(NebulaAPI.CurrentGame!);
            NebulaManager.Instance.StartDelayAction(nightmareDurationOption, () => inShdowLight.IsDisappearing = true);
        }

    });
}
