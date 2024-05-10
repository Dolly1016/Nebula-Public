using BepInEx.Unity.IL2CPP.Utils;
using Nebula.Behaviour;
using TMPro;
using Virial.Assignable;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Impostor;

public class Disturber : ConfigurableStandardRole
{
    [NebulaPreLoad]
    [NebulaRPCHolder]
    public class DisturbPole : NebulaSyncStandardObject
    {
        public static string MyTag = "DisturbPole";

        static SpriteLoader mySprite = SpriteLoader.FromResource("Nebula.Resources.ElecPole.png", 145f);

        private bool isActivated = false;
        public bool IsActivated => isActivated;
        private void Activate()
        {
            Color = Color.white;
            isActivated = true;
        }

        public DisturbPole(Vector2 pos) : base(pos, ZOption.Just, true, mySprite.GetSprite(), true)
        {
        }

        public override void OnInstantiated()
        {
            if (!AmOwner) Color = Color.clear;
        }

        public override void OnReleased()
        {
            base.OnReleased();

            try
            {
                NebulaManager.Instance.StartCoroutine(ManagedEffects.CoDisappearEffect(LayerExpansion.GetObjectsLayer(), null, Position.AsVector3(-1f)));
            }catch { }
        }

        public static void Load()
        {
            NebulaSyncObject.RegisterInstantiater(MyTag, (args) => new DisturbPole(new(args[0], args[1])));
        }

        static public DisturbPole GeneratePole(Vector2 pos)
        {
            return (NebulaSyncObject.RpcInstantiate(MyTag, new float[] { pos.x, pos.y }) as DisturbPole)!;
        }

        static public RemoteProcess<int> RpcActivate = new("ActivatePole", (id, _) => NebulaSyncObject.GetObject<DisturbPole>(id)?.Activate());
    }

    static public Disturber MyRole = new Disturber();
    public override RoleCategory Category => RoleCategory.ImpostorRole;

    public override string LocalizedName => "disturber";
    public override Color RoleColor => Palette.ImpostorRed;
    public override RoleTeam Team => Impostor.MyTeam;

    public override RoleInstance CreateInstance(GamePlayer player, int[] arguments) => new Instance(player);

    private NebulaConfiguration PlaceCoolDownOption = null!;
    private NebulaConfiguration DisturbCoolDownOption = null!;
    private NebulaConfiguration DisturbDurationOption = null!;
    private NebulaConfiguration MaxNumOfPolesOption = null!;
    private NebulaConfiguration MaxDistanceBetweenPolesOption = null!;

    protected override void LoadOptions()
    {
        base.LoadOptions();

        RoleConfig.AddTags(ConfigurationHolder.TagDifficult, ConfigurationHolder.TagFunny);

        PlaceCoolDownOption = new NebulaConfiguration(RoleConfig, "placeCoolDown", null, 0f, 60f, 2.5f, 10f, 10f) { Decorator = NebulaConfiguration.SecDecorator };
        DisturbCoolDownOption = new NebulaConfiguration(RoleConfig, "disturbCoolDown", null, 10f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        DisturbDurationOption = new NebulaConfiguration(RoleConfig, "disturbDuration", null, 5f, 60f, 2.5f, 15f, 15f) { Decorator = NebulaConfiguration.SecDecorator };
        MaxNumOfPolesOption = new NebulaConfiguration(RoleConfig, "maxNumOfPoles", null, 2, 10, 5, 5);
        MaxDistanceBetweenPolesOption = new NebulaConfiguration(RoleConfig, "maxDistanceBetweenPoles", null, 2f, 6f, 1f, 5f, 5f) { Decorator = NebulaConfiguration.OddsDecorator };
    }

    [NebulaRPCHolder]
    public class Instance : Impostor.Instance, IGameOperator
    {
        static float PoleDistanceMin = 0.8f;
        static float PoleDistanceMax => MyRole.MaxDistanceBetweenPolesOption.GetFloat();


        static public ISpriteLoader placeButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.ElecPolePlaceButton.png", 115f);
        static public ISpriteLoader disturbButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.DisturbButton.png", 115f);

        static private ISpriteLoader elecAnimHSprite = SpriteLoader.FromResource("Nebula.Resources.ElecAnim.png", 100f);
        static private ISpriteLoader elecAnimVSprite = SpriteLoader.FromResource("Nebula.Resources.ElecAnimSub.png", 100f);

        public override AbstractRole Role => MyRole;
        public Instance(GamePlayer player) : base(player)
        {
        }

        [OnlyMyPlayer, Local]
        void OnAddSystemTask(PlayerSabotageTaskAddLocalEvent ev)
        {
            if (acTokenChallenge != null)
            {
                acTokenChallenge.Value.cmTask = ev.SystemTask.TryCast<IHudOverrideTask>();
                acTokenChallenge.Value.elTask = ev.SystemTask.TryCast<ElectricTask>();
                acTokenChallenge.Value.time = NebulaGameManager.Instance?.CurrentTime ?? 0f;
                acTokenChallenge.Value.dead = 0;
                acTokenChallenge.Value.ability = disturbButton?.EffectActive ?? false;
            }
        }

        [OnlyMyPlayer, Local]
        void OnRemoveTask(PlayerTaskRemoveLocalEvent ev)
        {
            if(acTokenChallenge != null)
            {
                CheckChallengeAchievement();
                if (acTokenChallenge.Value.cmTask == ev.Task.TryCast<IHudOverrideTask>()) acTokenChallenge.Value.cmTask = null;
                if (acTokenChallenge.Value.elTask == ev.Task.TryCast<ElectricTask>()) acTokenChallenge.Value.elTask = null;
            }
        }

        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (acTokenChallenge != null)
            {
                CheckChallengeAchievement();
                acTokenChallenge.Value.cmTask = null;
                acTokenChallenge.Value.elTask = null;
                acTokenChallenge.Value.dead = 0;
                acTokenChallenge.Value.ability = false;
            }
        }

        [Local]
        void OnPlayerMurdered(PlayerMurderedEvent ev)
        {
            if(acTokenChallenge != null)
            {
                acTokenChallenge.Value.dead++;
                CheckChallengeAchievement();
            }
        }

        void CheckChallengeAchievement()
        {
            if (AmOwner && acTokenChallenge != null && acTokenChallenge.Value.dead >= 3 && acTokenChallenge.Value.time + 40f < (NebulaGameManager.Instance?.CurrentTime ?? 0f) && (acTokenChallenge.Value.elTask != null || acTokenChallenge.Value.cmTask != null) && acTokenChallenge.Value.ability) acTokenChallenge.Value.isCleared = true;
        }


        private AchievementToken<(IHudOverrideTask? cmTask, ElectricTask? elTask, float time, int dead, bool ability, bool isCleared)>? acTokenChallenge = null;
        private ModAbilityButton? disturbButton = null;

        public override void OnActivated()
        {
            base.OnActivated();

            if (AmOwner)
            {
                acTokenChallenge = new("disturber.challenge", (null, null, 0f, 0, false, false), (val, _) => val.isCleared);

                List<DisturbPole> newPoles = new();
                List<DisturbPole> poles = new();
                
                DisturbPole? GetLastPole() => newPoles.Count > 0 ? newPoles[newPoles.Count - 1] : poles.Count > 0 ? poles[poles.Count - 1] : null;

                EffectCircle? effectCircle = null;

                TextMeshPro polesText = null!;
                int GetNumOfLeftPoles() => MyRole.MaxNumOfPolesOption - (newPoles.Count + poles.Count);

                var placeButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability);
                placeButton.SetSprite(placeButtonSprite.GetSprite());
                placeButton.Availability = (button) =>
                {
                    var distance = (GetLastPole()?.Position.Distance(MyPlayer.Position.ToUnityVector()) ?? (PoleDistanceMin + 0.1f));
                    return MyPlayer.CanMove && newPoles.Count + poles.Count < MyRole.MaxNumOfPolesOption && distance > PoleDistanceMin && distance < PoleDistanceMax;
                };
                placeButton.Visibility = (button) => !MyPlayer.IsDead;
                placeButton.OnClick = (button) => {
                    var pole = DisturbPole.GeneratePole(MyPlayer.Position.ToUnityVector());
                    pole.Color = new(1f, 1f, 1f, 0.5f);
                    newPoles.Add(pole);
                    polesText.text = GetNumOfLeftPoles().ToString();

                    button.StartCoolDown();

                    if (effectCircle == null)
                    {
                        effectCircle = EffectCircle.SpawnEffectCircle(null, pole.Position.AsVector3(-10f), Palette.ImpostorRed, PoleDistanceMax, PoleDistanceMin, true);
                        this.Bind(effectCircle.gameObject);
                    }
                    effectCircle.TargetLocalPos = pole.Position;
                };
                placeButton.OnMeeting = (button) =>
                {
                    foreach(var p in newPoles)
                    {
                        DisturbPole.RpcActivate.Invoke(p.ObjectId);
                        poles.Add(p);
                    }
                    newPoles.Clear();
                };
                placeButton.CoolDownTimer = Bind(new Timer(MyRole.PlaceCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                placeButton.SetLabel("place");
                polesText = placeButton.ShowUsesIcon(0);
                polesText.text = GetNumOfLeftPoles().ToString();

                disturbButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.SecondaryAbility);
                disturbButton.SetSprite(disturbButtonSprite.GetSprite());
                disturbButton.Availability = (button) => poles.Count >= 2;
                disturbButton.Visibility = (button) => !MyPlayer.IsDead;
                disturbButton.OnClick = (button) => {
                    button.ActivateEffect();
                };
                disturbButton.OnEffectStart = (button) =>
                {
                    new StaticAchievementToken("disturber.common1");
                    if (poles.Count >= 6) new StaticAchievementToken("disturber.common2");

                    RpcDisturb.Invoke(poles.Select(p => p.Position).ToArray());

                    if (acTokenChallenge != null) acTokenChallenge.Value.ability = true;
                    CheckChallengeAchievement();
                };
                disturbButton.OnEffectEnd = (button) =>
                {
                    NebulaSyncObject.RpcDestroy(poles[0].ObjectId);
                    poles.RemoveAt(0);
                    button.StartCoolDown();

                    polesText.text = GetNumOfLeftPoles().ToString();
                    if (poles.Count == 0 && newPoles.Count == 0)
                    {
                        GameObject.Destroy(effectCircle);
                        effectCircle = null;
                    }
                };
                
                disturbButton.CoolDownTimer = Bind(new Timer(MyRole.DisturbCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                disturbButton.EffectTimer = Bind(new Timer(MyRole.DisturbDurationOption.GetFloat()));
                disturbButton.SetLabel("disturb");
            }
        }

        static private void InstantiateCollider(Vector2 pos1, Vector2 pos2, float duration)
        {
            float absX = Mathf.Abs(pos1.x - pos2.x);
            float absY = Mathf.Abs(pos1.y - pos2.y);
            bool isVertical = absX > 0f ? (absY / absX) > 1.75f : true;

            if(isVertical ? pos1.y > pos2.y : pos1.x > pos2.x) {
                var temp = pos1;
                pos1 = pos2;
                pos2 = temp;
            }

            //ポール中心の位置
            //pos1.y -= 0.3f;
            //pos2.y -= 0.3f;


            var obj = new GameObject("ElecBarrior");
            var meshFilter = obj.AddComponent<MeshFilter>();
            var meshRenderer = obj.AddComponent<MeshRenderer>();
            var colliderObj = UnityHelper.CreateObject("Collider", obj.transform, Vector3.zero, LayerExpansion.GetShipLayer());
            var collider = colliderObj.AddComponent<EdgeCollider2D>();

            //UV座標を更新
            void UpdateUV(int num)
            {
                float fNum1 = (float)num / 3f;
                float fNum2 = (float)(num + 1) / 3f;

                meshFilter.mesh.SetUVs(0, (Vector2[])[new(fNum1, 0f), new(fNum2, 0f), new(fNum2, 1f), new(fNum1, 1f)]);
            }

            //障壁の座標
            Vector3 colliderPos = (pos1 + pos2) * 0.5f;
            colliderPos.z = Mathf.Max(pos1.y, pos2.y) / 1000f;
            obj.transform.localPosition = colliderPos;

            Vector2 pos1Rel = pos1 - (Vector2)colliderPos;
            Vector2 pos2Rel = pos2 - (Vector2)colliderPos;

            meshFilter.mesh = new();

            Vector3[] vertices = isVertical ? 
                [
                pos1Rel + new Vector2(-0.22f,0.2f),
                pos2Rel + new Vector2(-0.22f,0.5f),
                pos2Rel + new Vector2(0.22f,0.5f),
                pos1Rel + new Vector2(0.22f,0.2f)
                ] : 
                [
                pos1Rel + new Vector2(0f,0.42f),
                pos2Rel + new Vector2(0f,0.42f),
                pos2Rel + new Vector2(0f,-0.2f),
                pos1Rel + new Vector2(0f,-0.2f)
                ];
            meshFilter.mesh.SetVertices(vertices);
            UpdateUV(0);
            meshFilter.mesh.SetIndices((int[])[0, 1, 2, 2, 3, 0], MeshTopology.Triangles, 0);

            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.material.mainTexture = (isVertical ? elecAnimVSprite : elecAnimHSprite).GetSprite().texture;
            meshRenderer.gameObject.layer = LayerExpansion.GetObjectsLayer();

            //当たり判定
            collider.points = (Vector2[])[
                pos1Rel + new Vector2(0f, -0.22f),
                pos2Rel + new Vector2(0f, -0.22f),
                pos2Rel + new Vector2(0f, 0.05f),
                pos1Rel + new Vector2(0f, 0.05f),
                pos1Rel + new Vector2(0f, -0.22f)
            ];
            collider.edgeRadius = 0.08f;
            
            IEnumerator CoUpdate()
            {
                float timer = 0f;
                int counter = 0;
                while(duration > 0f)
                {
                    duration -= Time.deltaTime;
                    timer -= Time.deltaTime;

                    if(timer < 0f)
                    {
                        counter = (counter + 1) % 3;
                        UpdateUV(counter);
                        timer = 0.1f;
                    }

                    yield return null;
                }

                GameObject.Destroy(obj);
                yield break;
            }

            NebulaManager.Instance.StartCoroutine(CoUpdate().WrapToIl2Cpp());
        }

        static public RemoteProcess<Vector2[]> RpcDisturb = new("Disturb", (message, _) =>
        {
            float duration = MyRole.DisturbDurationOption.GetFloat();
            for (int i = 0; i < message.Length - 1; i++) InstantiateCollider(message[i], message[i + 1], duration);
        });
    }
}
