using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;

namespace Nebula.Roles.Crewmate;

[NebulaRPCHolder]
public class Phosphorus : ConfigurableStandardRole
{
    static public Phosphorus MyRole = new Phosphorus();

    public override RoleCategory Category => RoleCategory.CrewmateRole;

    public override string LocalizedName => "phosphorus";
    public override Color RoleColor => new Color(249f / 255f, 188f / 255f, 81f / 255f);
    public override RoleTeam Team => Crewmate.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    private NebulaConfiguration NumOfLampsOption = null!;
    private NebulaConfiguration PlaceCoolDownOption = null!;
    private NebulaConfiguration LampCoolDownOption = null!;
    private NebulaConfiguration LampDurationOption = null!;
    private NebulaConfiguration LampStrengthOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagFunny);

        NumOfLampsOption = new NebulaConfiguration(RoleConfig, "numOfLamps", null, 1, 10, 2, 2);
        PlaceCoolDownOption = new NebulaConfiguration(RoleConfig, "placeCoolDown", null, 5f, 60f, 5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        LampCoolDownOption = new NebulaConfiguration(RoleConfig, "lampCoolDown", null, 5f, 60f, 5f, 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator };
        LampDurationOption = new NebulaConfiguration(RoleConfig, "lampDuration", null, 7.5f, 30f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        LampStrengthOption = new NebulaConfiguration(RoleConfig, "lampStrength", null, 0.25f, 5f, 0.25f, 1f, 1f) { Decorator = NebulaConfiguration.OddsDecorator };
    }

    private static IDividedSpriteLoader lanternSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.Lantern.png", 100f, 4);

    [NebulaPreLoad]
    public class Lantern : NebulaSyncStandardObject
    {
        public static string MyGlobalTag = "LanternGlobal";
        public static string MyLocalTag = "LanternLocal";
        public Lantern(Vector2 pos,bool isLocal) : base(pos,ZOption.Just,true,lanternSprite.GetSprite(0),isLocal){}

        public static void Load()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new Lantern(new(args[0], args[1]), false));
            NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Lantern(new(args[0], args[1]), true));
        }
    }

    public class Instance : Crewmate.Instance, IGamePlayerEntity
    {
        private ModAbilityButton? placeButton = null;
        private ModAbilityButton? lanternButton = null;

        static private ISpriteLoader placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternPlaceButton.png", 115f);
        static private ISpriteLoader lanternButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.LanternButton.png", 115f);

        public override AbstractRole Role => MyRole;
        
        public Instance(GamePlayer player, int[] arguments) : base(player)
        {
            if (arguments.Length > 0) globalLanterns = arguments;
        }


        private int[]? globalLanterns = null;
        List<NebulaSyncStandardObject> localLanterns = null!;

        public override void OnActivated()
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
                lanternButton.CoolDownTimer = Bind(new Timer(0f, MyRole.LampCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                lanternButton.EffectTimer = Bind(new Timer(0f, MyRole.LampDurationOption.GetFloat()));
                lanternButton.SetLabel("lantern");

                int left = MyRole.NumOfLampsOption;

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
                placeButton.CoolDownTimer = Bind(new Timer(0f, MyRole.PlaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown());
                placeButton.SetLabel("place");
                usesText.text = left.ToString();

                lanternButton.StartCoolDown();
                placeButton.StartCoolDown();
            }
        }


        void IGameEntity.OnMeetingStart()
        {
            //ランタンを全て設置していたら全員に公開する
            if(localLanterns != null && localLanterns.Count == MyRole.NumOfLampsOption)
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
              lightRenderer.transform.localScale *= MyRole.LampStrengthOption.GetFloat();

              IEnumerator CoLight()
              {
                  float t = MyRole.LampDurationOption.GetFloat();
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
