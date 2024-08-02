using BepInEx.Unity.IL2CPP.Utils;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game.Meeting;
using Virial.Game;
using Virial.Helpers;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
public class Phosphorus : DefinedRoleTemplate, DefinedRole
{
    private Phosphorus():base("phosphorus", new(249,188,81), RoleCategory.CrewmateRole, Crewmate.MyTeam, [NumOfLampsOption, PlaceCoolDownOption, LampCoolDownOption, LampDurationOption, LampStrengthOption]) {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Phosphorus.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private IntegerConfiguration NumOfLampsOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.numOfLamps", (1, 10), 2);
    static private FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.placeCoolDown", (5f, 60f, 5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration LampCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampCoolDown", (5f, 60f, 5f), 30f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration LampDurationOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampDuration", (7.5f, 30f, 2.5f), 15f, FloatConfigurationDecorator.Second);
    static private FloatConfiguration LampStrengthOption = NebulaAPI.Configurations.Configuration("options.role.phosphorus.lampStrength", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);

    static public Phosphorus MyRole = new Phosphorus();

    private static IDividedSpriteLoader lanternSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Lantern.png", 100f, 4);

    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class Lantern : NebulaSyncStandardObject
    {
        public static string MyGlobalTag = "LanternGlobal";
        public static string MyLocalTag = "LanternLocal";
        public Lantern(Vector2 pos,bool isLocal) : base(pos,ZOption.Just,true,lanternSprite.GetSprite(0),isLocal){}

        static Lantern()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new Lantern(new(args[0], args[1]), false));
            NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Lantern(new(args[0], args[1]), true));
        }
    }

    public class Instance : RuntimeAssignableTemplate, RuntimeRole
    {
        DefinedRole RuntimeRole.Role => MyRole;

        private ModAbilityButton? placeButton = null;
        private ModAbilityButton? lanternButton = null;

        static private Image placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternPlaceButton.png", 115f);
        static private Image lanternButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternButton.png", 115f);

        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length > 0) globalLanterns = arguments;
        }


        private int[]? globalLanterns = null;
        List<NebulaSyncStandardObject> localLanterns = null!;

        void RuntimeAssignable.OnActivated()
        {
            if (AmOwner)
            {
                StaticAchievementToken? acTokenChallenge = null;

                localLanterns = new();

                lanternButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                lanternButton.SetSprite(lanternButtonSprite.GetSprite());
                lanternButton.Availability = (button) => MyPlayer.CanMove ;
                lanternButton.Visibility = (button) => !MyPlayer.IsDead && globalLanterns != null;
                lanternButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                lanternButton.OnEffectStart = (button) =>
                {
                    CombinedRemoteProcess.CombinedRPC.Invoke(globalLanterns!.Select((id)=>RpcLantern.GetInvoker(id)).ToArray());

                    if (acTokenChallenge == null)
                    {
                        var lanterns = globalLanterns!.Select(id => NebulaSyncObject.GetObject<Lantern>(id)!);
                        var deadBodies = Helpers.AllDeadBodies();
                        if (lanterns.Any(l => deadBodies.Any(d => d.TruePosition.Distance(l.Position) < 0.8f))) acTokenChallenge = new("phosphorus.challenge");
                    }
                };
                lanternButton.OnEffectEnd = (button) => lanternButton.StartCoolDown();
                lanternButton.CoolDownTimer = Bind(new Timer(0f, LampCoolDownOption).SetAsAbilityCoolDown().Start());
                lanternButton.EffectTimer = Bind(new Timer(0f, LampDurationOption));
                lanternButton.SetLabel("lantern");

                int left = NumOfLampsOption;

                placeButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                var usesText = placeButton.ShowUsesIcon(3);
                placeButton.SetSprite(placeButtonSprite.GetSprite());
                placeButton.Availability = (button) => MyPlayer.CanMove;
                placeButton.Visibility = (button) => !MyPlayer.IsDead && globalLanterns == null && left > 0;
                placeButton.OnClick = (button) => {
                    var pos = PlayerControl.LocalPlayer.GetTruePosition();
                    localLanterns.Add(Bind<NebulaSyncStandardObject>((NebulaSyncObject.LocalInstantiate(Lantern.MyLocalTag, new float[] { pos.x, pos.y }) as NebulaSyncStandardObject)!));

                    left--;
                    usesText.text = left.ToString();

                    placeButton.StartCoolDown();

                    new StaticAchievementToken("phosphorus.common1");
                    new StaticAchievementToken("phosphorus.common2");
                };
                placeButton.CoolDownTimer = Bind(new Timer(0f, PlaceCoolDownOption).SetAsAbilityCoolDown());
                placeButton.SetLabel("place");
                usesText.text = left.ToString();

                lanternButton.StartCoolDown();
                placeButton.StartCoolDown();
            }
        }

        void OnMeetingStart(MeetingStartEvent ev)
        {
            //ランタンを全て設置していたら全員に公開する
            if(localLanterns != null && localLanterns.Count == NumOfLampsOption)
            {
                globalLanterns = new int[localLanterns.Count];
                for (int i = 0;i<localLanterns.Count;i++) {
                    globalLanterns[i] = NebulaSyncObject.RpcInstantiate(Lantern.MyGlobalTag, new float[] { localLanterns[i].Position.x, localLanterns[i].Position.y })!.ObjectId;
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
