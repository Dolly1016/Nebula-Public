using BepInEx.Unity.IL2CPP.Utils;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
public class Phosphorus : DefinedSingleAbilityRoleTemplate<Phosphorus.Ability>, DefinedRole
{
    private Phosphorus():base("phosphorus", new(249,188,81), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfLampsOption, PlaceCoolDownOption, LampCoolDownOption, LampDurationOption, LampStrengthOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Phosphorus.png");

        GameActionTypes.LanternPlacementAction = new("phosphorus.placement", this, isPlacementAction: true);
    }

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), arguments.Skip(0).ToArray());

    static private readonly IntegerConfiguration NumOfLampsOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.numOfLamps", (1, 10), 2);
    static private readonly FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.placeCoolDown", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LampCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampCoolDown", (5f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LampDurationOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampDuration", (7.5f, 30f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LampStrengthOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampStrength", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);

    static public readonly Phosphorus MyRole = new();
    static private readonly GameStatsEntry StatsLantern = NebulaAPI.CreateStatsEntry("stats.phosphorus.lantern", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsLighting = NebulaAPI.CreateStatsEntry("stats.phosphorus.lighting", GameStatsCategory.Roles, MyRole);

    private static readonly IDividedSpriteLoader lanternSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Lantern.png", 100f, 4);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Lantern : NebulaSyncStandardObject
    {
        public const string MyGlobalTag = "LanternGlobal";
        public const string MyLocalTag = "LanternLocal";
        public Lantern(Vector2 pos,bool isLocal) : base(pos,ZOption.Just,true,lanternSprite.GetSprite(0),isLocal){}

        static Lantern()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new Lantern(new(args[0], args[1]), false));
            NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Lantern(new(args[0], args[1]), true));
        }
    }

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {

        static private readonly Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternPlaceButton.png", 115f);
        static private readonly Image lanternButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternButton.png", 115f);

        private int[]? globalLanterns = null;
        List<NebulaSyncStandardObject> localLanterns = null!;

        public Ability(GamePlayer player, bool isUsurped, int[] arguments) : base(player, isUsurped)
        {
            if (arguments.Length > 0) globalLanterns = arguments;

            if (AmOwner)
            {
                StaticAchievementToken? acTokenChallenge = null;

                localLanterns = new();

                var lanternButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    LampCoolDownOption, LampDurationOption, "lantern", lanternButtonSprite, null, _ => globalLanterns != null)
                    .SetAsUsurpableButton(this);
                lanternButton.OnEffectStart = (button) =>
                {
                    CombinedRemoteProcess.CombinedRPC.Invoke(globalLanterns!.Select((id)=>RpcLantern.GetInvoker(id)).ToArray());

                    StatsLighting.Progress();
                    if (acTokenChallenge == null)
                    {
                        var lanterns = globalLanterns!.Select(id => NebulaSyncObject.GetObject<Lantern>(id)!);
                        var deadBodies = Helpers.AllDeadBodies();
                        if (lanterns.Any(l => deadBodies.Any(d => d.TruePosition.Distance(l.Position) < 0.8f))) acTokenChallenge = new("phosphorus.challenge");
                    }
                };
                lanternButton.OnEffectEnd = (button) => lanternButton.StartCoolDown();

                int left = NumOfLampsOption;

                var placeButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability,
                    PlaceCoolDownOption, "place", placeButtonSprite, null, _ => globalLanterns == null && left > 0)
                    .SetAsUsurpableButton(this);
                placeButton.OnClick = (button) => {
                    var pos = PlayerControl.LocalPlayer.GetTruePosition();
                    
                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, pos, GameActionTypes.LanternPlacementAction);

                    localLanterns.Add((NebulaSyncObject.LocalInstantiate(Lantern.MyLocalTag, new float[] { pos.x, pos.y }).SyncObject as NebulaSyncStandardObject)!);

                    left--;
                    placeButton.UpdateUsesIcon(left.ToString());

                    placeButton.StartCoolDown();

                    StatsLantern.Progress();
                    new StaticAchievementToken("phosphorus.common1");
                    new StaticAchievementToken("phosphorus.common2");
                };
                placeButton.ShowUsesIcon(3, left.ToString());
            }
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            //ランタンを全て設置していたら全員に公開する
            if(localLanterns != null && localLanterns.Count == NumOfLampsOption)
            {
                globalLanterns = new int[localLanterns.Count];
                for (int i = 0;i<localLanterns.Count;i++) {
                    globalLanterns[i] = NebulaSyncObject.RpcInstantiate(Lantern.MyGlobalTag, new float[] { localLanterns[i].Position.x, localLanterns[i].Position.y }).ObjectId;
                    NebulaSyncObject.LocalDestroy(localLanterns[i].ObjectId);
                }
                localLanterns = null!;
            }
        }

      

    }

    public static RemoteProcess<int> RpcLantern = new(
      "Lantern",
      (message, _) =>
      {
          var lantern = NebulaSyncObject.GetObject<Lantern>(message);
          if (lantern != null)
          {
              SpriteRenderer lightRenderer = AmongUsUtil.GenerateCustomLight(lantern.Position);
              lightRenderer.transform.localScale *= LampStrengthOption;

              IEnumerator CoLight()
              {
                  float t = LampDurationOption;
                  float indexT = 0f;
                  int index = 0;
                  while (t > 0f)
                  {
                      t -= Time.deltaTime;
                      indexT -= Time.deltaTime;

                      if (indexT < 0f)
                      {
                          indexT = 0.13f;
                          lantern.Sprite = lanternSprite.GetSprite(index + 1);
                          index = (index + 1) % 3;
                      }
                      yield return null;
                  }

                  lantern.Sprite = lanternSprite.GetSprite(0);
                  t = 1f;

                  while (t > 0f)
                  {
                      t -= Time.deltaTime * 2.9f;
                      lightRenderer.material.color = new Color(1, 1, 1, t);
                      yield return null;
                  }

                  GameObject.Destroy(lightRenderer.gameObject);
              }

              IEnumerator CoLightBegin()
              {
                  float t;

                  t = 0.6f;
                  while (t > 0f)
                  {
                      t -= Time.deltaTime * 1.8f;
                      lightRenderer.material.color = new Color(1, 1, 1, t);
                      yield return null;
                  }

                  t = 0.4f;
                  while (t > 0f)
                  {
                      t -= Time.deltaTime * 0.8f;
                      lightRenderer.material.color = new Color(1, 1, 1, t);
                      yield return null;
                  }

                  while (t < 1f)
                  {
                      t += Time.deltaTime * 0.6f;
                      lightRenderer.material.color = new Color(1, 1, 1, t);
                      yield return null;
                  }

                  lightRenderer.material.color = Color.white;
              }

              NebulaManager.Instance.StartCoroutine(CoLight());
              NebulaManager.Instance.StartCoroutine(CoLightBegin());
          }
      }
      );
}
