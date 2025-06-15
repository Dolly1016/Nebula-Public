using Nebula.Roles.Modifier;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;

namespace Nebula.Roles.Impostor;


[NebulaRPCHolder]
internal class Cupid : DefinedSingleAbilityRoleTemplate<Cupid.Ability>, DefinedRole
{
    private Cupid() : base("cupid", new(Palette.ImpostorRed), RoleCategory.ImpostorRole, Impostor.MyTeam, [
        SelectCooldownOption,
        LaserCooldownOption,
        LaserDelayByCupidOption,
        LaserDelayByLoverOption,
        LaserDurationOption,
        LaserRadiusOption,
        LaserSEStrengthOption
        ])
    {
        //GameActionTypes.CleanCorpseAction = new("cleaner.clean", this, isCleanDeadBodyAction: true);
    }

    static private readonly FloatConfiguration SelectCooldownOption = NebulaAPI.Configurations.Configuration("options.role.cupid.selectCooldown", (0f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LaserDelayByCupidOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserDelayByCupid", (0f, 20f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LaserDelayByLoverOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserDelayByLover", (0f, 10f, 1f), 2f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LaserCooldownOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserCooldown", (0f, 60f, 2.5f), 25f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LaserDurationOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserDuration", (2.5f, 20f, 2.5f), 10f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration LaserRadiusOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserRadius", (0.5f, 2f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    static private readonly FloatConfiguration LaserSEStrengthOption = NebulaAPI.Configurations.Configuration("options.role.cupid.laserSeStrength", (1f, 5f, 0.5f), 2f, FloatConfigurationDecorator.Ratio);
    //static private readonly BoolConfiguration SyncKillAndCleanCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.cleaner.syncKillAndCleanCoolDown", true);

    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new Ability(player, arguments.GetAsBool(0), GamePlayer.GetPlayer((byte)arguments.Get(1, 255)), GamePlayer.GetPlayer((byte)arguments.Get(2, 255)), arguments.GetAsBool(3), arguments.Get(4, 0));
    bool DefinedRole.IsJackalizable => true;
    static public readonly Cupid MyRole = new();
    static private readonly GameStatsEntry StatsLovers = NebulaAPI.CreateStatsEntry("stats.cupid.lovers", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsLaserCupid = NebulaAPI.CreateStatsEntry("stats.cupid.laserCupid", GameStatsCategory.Roles, MyRole);
    static private readonly GameStatsEntry StatsLaserLover = NebulaAPI.CreateStatsEntry("stats.cupid.laserLover", GameStatsCategory.Roles, MyRole);

    static private Image stringButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CupidStringButton.png", 115f);

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        static private Image loverButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CupidLoverButton.png", 115f);

        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt(), selected1?.PlayerId ?? byte.MaxValue, selected2?.PlayerId ?? byte.MaxValue, hasCreatedLover.AsInt(), used];

        private GamePlayer? selected1, selected2;
        private bool hasCreatedLover = false;
        private bool loversIsBrokenObviously = false;
        private int used = 0;

        private void CheckSelectedStatus()
        {
            if (selected2?.IsDead ?? false) selected2 = null;
            if (selected1?.IsDead ?? false)
            {
                selected1 = selected2;
                selected2 = null;
            }
        }

        Action<GamePlayer?>? loversIconAction = null;
        private void UpdateLoverStatus()
        {
            loversIconAction?.Invoke(selected1);
        }

        [Local]
        void OnEditPlayerNameColor(PlayerDecorateNameEvent ev)
        {
            if (NebulaGameManager.Instance!.CanSeeAllInfo && hasCreatedLover) return;
            if (ev.Player.PlayerId == selected1?.PlayerId || ev.Player.PlayerId == selected2?.PlayerId) ev.Name += " ♥".Color(Lover.Colors[0].RGBMultiplied(0.78f));
        }

        [Local, OnlyMyPlayer]
        void OnDead(PlayerMurderedEvent ev)
        {
            if (hasCreatedLover && (ev.Murderer == selected1 || ev.Murderer == selected2) && ev.Dead.PlayerState == PlayerState.Laser) new StaticAchievementToken("cupid.another1");
        }

        [Local]
        void OnGameEnd(GameEndEvent ev)
        {
            if (hasCreatedLover && selected1!.IsDead && selected2!.IsDead && !MyPlayer.IsDead &&
                GamePlayer.AllPlayers.Count(p => p.PlayerState == PlayerState.Laser && (p.MyKiller == MyPlayer || p.MyKiller == selected1 || p.MyKiller == selected2)) >= 5 &&
                GamePlayer.AllPlayers.All(p => p == MyPlayer || p.IsDead) &&
                ev.EndState.EndCondition == NebulaGameEnd.ImpostorWin && ev.EndState.Winners.Test(MyPlayer)
                )
                new StaticAchievementToken("cupid.challenge");
        }
        public Ability(GamePlayer player, bool isUsurped, GamePlayer? selected1, GamePlayer? selected2, bool createdAlready, int used) : base(player, isUsurped)
        {
            if (createdAlready)
            {
                this.selected1 = selected1;
                this.selected2 = selected2;
                loversIsBrokenObviously = (selected1?.IsDead ?? true) || (selected2?.IsDead ?? true);
                this.used = used;
            }

            if (AmOwner)
            {
                var stringButton = NebulaAPI.Modules.EffectButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "cupid.string",
                    LaserCooldownOption, LaserDurationOption + LaserDelayByCupidOption, "cupid.string", stringButtonSprite, null, _ => ((this.selected1 != null && this.selected2 != null) || (hasCreatedLover && !loversIsBrokenObviously)) && used <= 1).SetAsUsurpableButton(this);
                stringButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
                stringButton.OnClick = (button) => {
                    if (button.IsInEffect || button.IsInCooldown) return;

                    using (RPCRouter.CreateSection("CupidLaser"))
                    {
                        if (!hasCreatedLover)
                        {
                            CheckSelectedStatus();
                            if (this.selected2 == null)
                            {
                                UpdateLoverStatus();
                                return;
                            }

                            int maxLoverId = -1;
                            foreach (var p in GamePlayer.AllPlayers) foreach (var l in p.GetModifiers<Lover.Instance>()) if (maxLoverId < l.LoversId) maxLoverId = l.LoversId;


                            this.selected1!.AddModifier(Lover.MyRole, [maxLoverId + 1]);
                            this.selected2!.AddModifier(Lover.MyRole, [maxLoverId + 1]);
                            RpcSetLoverAbility.Invoke((this.selected1, this.selected2));
                            GameOperatorManager.Instance?.Subscribe<PlayerCheckCanKillLocalEvent>(ev => {
                                if (ev.Target == this.selected1 || ev.Target == this.selected2) ev.SetAsCannotKillForcedly();
                            }, NebulaAPI.CurrentGame!);
                            hasCreatedLover = true;
                            StatsLovers.Progress();
                        }
                        else if(this.selected1!.IsDead || this.selected2!.IsDead)
                        {

                            loversIsBrokenObviously = true;
                            return;
                        }
                        StatsLaserCupid.Progress();
                        RpcLaser.Invoke((this.selected1, this.selected2, MyPlayer));
                        button.StartEffect();
                    }
                };
                stringButton.OnEffectEnd = _ =>
                {
                    used++;
                    stringButton.StartCoolDown();
                };


                var loverTracker = NebulaAPI.Modules.PlayerTracker(this, MyPlayer, p => p != this.selected1 && p != this.selected2);
                var cupidButton = NebulaAPI.Modules.InteractButton(this, MyPlayer, loverTracker, Virial.Compat.VirtualKeyInput.Ability, "cupid.lover",
                    SelectCooldownOption, "cupid.lover", loverButtonSprite, (target, button) =>
                    {
                        CheckSelectedStatus();
                        if (this.selected1 == null) this.selected1 = target;
                        else this.selected2 = target;
                        UpdateLoverStatus();
                        if (stringButton.CoolDownTimer!.CurrentTime < 10f) (stringButton.CoolDownTimer as GameTimer)?.SetTime(10f);
                        button.StartCoolDown();
                    }, null, _ => this.selected2 == null && !hasCreatedLover).SetAsUsurpableButton(this);
                PoolablePlayer? playerIcon = null;

                loversIconAction = (player) =>
                {
                    if (playerIcon) GameObject.Destroy(playerIcon!.gameObject);
                    if (player == null)
                        playerIcon = null;
                    else
                        playerIcon = AmongUsUtil.GetPlayerIcon(player.DefaultOutfit.outfit, (cupidButton as ModAbilityButtonImpl)!.VanillaButton.transform, new Vector3(-0.4f, 0.35f, -0.5f), new(0.3f, 0.3f, 1f));
                };
            }
        }
    }

    private static readonly MultiImage preSpawnImage = DividedSpriteLoader.FromResource("Nebula.Resources.LaserSpawnAnim.png", 100f, 5, 2);
    private static readonly MultiImage preSpawnChargeImage = DividedSpriteLoader.FromResource("Nebula.Resources.LaserSpawnChargeAnim.png", 100f, 5, 1);
    private static IEnumerator CoPlayPreAnim(GamePlayer player, float duration)
    {
        var main_animator = UnityHelper.SimpleAnimator(player.VanillaPlayer.transform, new(0f, 0f, -4f), 0.1f, i => preSpawnImage.GetSprite(i % 5));
        var sub_animator = UnityHelper.SimpleAnimator(main_animator.transform, new(0f, 0f, 0.5f), 0.1f, i => preSpawnImage.GetSprite(5 + i % 5));
        sub_animator.material.shader = NebulaAsset.MultiplyShader;
        var charge_animator = UnityHelper.SimpleAnimator(main_animator.transform, new(0f, 0f, 0f), 0.1f, i => preSpawnChargeImage.GetSprite(i % 5));

        float angle = 0f;
        while(duration > 0f)
        {
            angle -= 2f;
            main_animator.transform.localEulerAngles = new(0f, 0f, angle);
            yield return Effects.Wait(0.1f);
            duration -= 0.1f;
        }
        if (main_animator)GameObject.Destroy(main_animator.gameObject);
    }
    public class RedString : IGameOperator, ILifespan
    {
        static readonly Image laserNodeImage = SpriteLoader.FromResource("Nebula.Resources.LaserNode.png", 100f);
        static readonly Image laserNodeBackImage = SpriteLoader.FromResource("Nebula.Resources.LaserNodeBack.png", 100f);
        static readonly MultiImage laserImage = DividedSpriteLoader.FromResource("Nebula.Resources.Laser.png", 100f, 1, 4);
        static readonly MultiImage laserBackImage = DividedSpriteLoader.FromResource("Nebula.Resources.LaserBack.png", 100f, 1, 4);
        static readonly Image lightImage = new ResourceExpandableSpriteLoader("Nebula.Resources.LaserLight.png", 100f, 80, 2);
        static readonly Image gradationImage = SpriteLoader.FromResource("Nebula.Resources.Gradation.png", 100f);

        private SpriteRenderer node1Renderer;
        private SpriteRenderer node1BackRenderer;
        private SpriteRenderer node2Renderer;
        private SpriteRenderer node2BackRenderer;
        private SpriteRenderer edgeFrontRenderer;
        private SpriteRenderer edgeBackRenderer;
        private SpriteRenderer edgeLightRenderer;
        private GamePlayer player1, player2;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private Mesh mesh;

        private int meshLength = 0, meshBegin = 0, meshTerminal = 0;
        private bool isActive = true;
        private const int MeshMax = 50;
        private Vector2[] pos1 = new Vector2[MeshMax];
        private Vector2[] pos2 = new Vector2[MeshMax];
        private Vector3[] pos = new Vector3[MeshMax * 3];
        private AudioSource soundSource;
        private GamePlayer invoker;

        private ILifespan? parentLifespan = null;
        public bool IsDeadObject => !isActive && meshTerminal >= meshLength;
        public void Bind(ILifespan parent) => parentLifespan = parent;

        public RedString(GamePlayer invoker, GamePlayer player1, GamePlayer player2)
        {
            this.invoker = invoker;
            this.player1 = player1;
            this.player2 = player2;
        }

        bool initialized = false;

        // -2.5 : 線および頂点
        // -2.49: 乗算レイヤー
        // -2.4 : 残像
        void OnFirstUpdate() {

            node1Renderer = UnityHelper.CreateSpriteRenderer("Node1", null, Vector3.zero);
            node1BackRenderer = UnityHelper.CreateSpriteRenderer("Back", node1Renderer.transform, new(0f, 0f, 0.01f));
            node1Renderer.sprite = laserNodeImage.GetSprite();
            node1BackRenderer.sprite = laserNodeBackImage.GetSprite();
            node1BackRenderer.material.shader = NebulaAsset.MultiplyShader;

            node2Renderer = UnityHelper.CreateSpriteRenderer("Node2", null, Vector3.zero);
            node2BackRenderer = UnityHelper.CreateSpriteRenderer("Back", node2Renderer.transform, new(0f, 0f, 0.01f));
            node2Renderer.sprite = laserNodeImage.GetSprite();
            node2BackRenderer.sprite = laserNodeBackImage.GetSprite();
            node2BackRenderer.material.shader = NebulaAsset.MultiplyShader;


            edgeFrontRenderer = UnityHelper.SimpleAnimator(null, new(0f, 0f, -2.5f), 0.1f, i => laserImage.GetSprite(i % 4));
            edgeBackRenderer = UnityHelper.SimpleAnimator(edgeFrontRenderer.transform, new(0f, 0f, 0.01f), 0.1f, i => laserBackImage.GetSprite(i % 4));
            edgeBackRenderer.material.shader = NebulaAsset.MultiplyShader;
            edgeFrontRenderer.drawMode = SpriteDrawMode.Tiled;
            edgeBackRenderer.drawMode = SpriteDrawMode.Tiled;

            if (player1.AmOwner || player2.AmOwner)
            {
                edgeLightRenderer = AmongUsUtil.GenerateCustomLight(Vector2.zero, lightImage.GetSprite());
                edgeLightRenderer.drawMode = SpriteDrawMode.Tiled;
                edgeLightRenderer.material.color = new(0.8f, 0.2f, 0.2f, 0.5f);
            }

            //メッシュの生成 ここから

            (meshRenderer, meshFilter) = UnityHelper.CreateMeshRenderer("MeshRenderer", null, new(0f, 0f, -2.4f), LayerExpansion.GetDefaultLayer(), null, UnityHelper.GetMeshRendererMaterial());
            meshRenderer.material.mainTexture = gradationImage.GetSprite().texture;
            meshRenderer.material.color = new Color32(240, 0, 0, 180);
            mesh = meshFilter.mesh;

            mesh.SetVertices(pos);


            var color = new Color32(255, 255, 255, 255);
            Color32[] colors = new Color32[MeshMax * 3];
            for (int i = 0; i < MeshMax * 3; i++) colors[i] = color;
            mesh.SetColors(colors);

            Vector2[] uvs = new Vector2[MeshMax * 3];
            for (int i = 0; i < MeshMax; i++)
            {
                uvs[i] = new((float)i / (MeshMax - 1) * 0.95f, 0f);
                uvs[i + MeshMax] = new((float)i / (MeshMax - 1) * 0.95f, 1f);
                uvs[i + MeshMax + MeshMax] = new((float)i / (MeshMax - 1) * 0.95f, 0.5f);
            }
            mesh.SetUVs(0, uvs);

            //メッシュの生成 ここまで

            //効果音の再生
            soundSource = NebulaAsset.PlaySE(NebulaAudioClip.Laser, Vector2.zero, 1f, 3.5f);
            soundSource.loop = true;
            
            initialized = true;
        }

        int counter = 0;

        private const float LaserRadiusBase = 0.45f;
        private static float LaserRadius => LaserRadiusBase * LaserRadiusOption;
        bool CheckLocalKill()
        {
            if (player1.AmOwner || player2.AmOwner) return false;

            var myPos = GamePlayer.LocalPlayer!.Position;
            UnityEngine.Vector2 vec = player2.Position - player1.Position;
            UnityEngine.Vector2 myVec = myPos - player1.Position;
            float x = Vector2.Dot(vec.normalized, myVec);
            UnityEngine.Vector2 verVec = new(vec.y, -vec.x);
            float y = Vector2.Dot(verVec.normalized, myVec);
            var length = vec.magnitude;
            if(x <0f)
            {
                return player1.Position.Distance(myPos) < LaserRadius;
            }
            if(x > length)
            {
                return player2.Position.Distance(myPos) < LaserRadius;
            }
            return Mathf.Abs(y) < LaserRadius; 
        }

        void CheckAndRequestLocalKill()
        {
            var player = GamePlayer.LocalPlayer;
            if (player.IsDead) return;
            if (CheckLocalKill())
            {
                invoker.MurderPlayer(player, PlayerState.Laser, null, KillParameter.RemoteKill);
            }
        }

        void OnHudUpdate(GameHudUpdateEvent ev) {
            if (!initialized) OnFirstUpdate();

            if (isActive && (player1.IsDead || player2.IsDead)) Inactivate();
            if (isActive && parentLifespan != null && parentLifespan.IsDeadObject) Inactivate();

            if (isActive) CheckAndRequestLocalKill();

            bool LineSegmentCrossesLine(Vector2 seg1, Vector2 seg2, Vector2 line1, Vector2 line2)
            {
                double s, t;
                s = (seg1.x - seg2.x) * (line1.y - seg1.y) - (seg1.y - seg2.y) * (line1.x - seg1.x);
                t = (seg1.x - seg2.x) * (line2.y - seg1.y) - (seg1.y - seg2.y) * (line2.x - seg1.x);
                return s * t < 0;
            }
            bool LineSegmentCrossesLineSegment(Vector2 seg1_1, Vector2 seg1_2, Vector2 seg2_1, Vector2 seg2_2)
            {
                return LineSegmentCrossesLine(seg1_1, seg1_2, seg2_1, seg2_2) && LineSegmentCrossesLine(seg2_1, seg2_2, seg1_1, seg1_2);
            }
            Vector2 GetIntersectionPos(Vector2 line1_1, Vector2 line1_2, Vector2 line2_1, Vector2 line2_2)
            {
                float cx = line1_2.x - line1_1.x;
                float cy = line1_2.y - line1_1.y;

                float ax = line2_1.x - line1_1.x;
                float ay = line2_1.y - line1_1.y;
                float bx = line2_2.x - line1_1.x;
                float by = line2_2.y - line1_1.y;
                float dx = ax - bx;
                float dy = ay - by;

                float t = (bx * dy - by * dx) / (cx * dy - cy * dx);
                return new(line1_1.x + cx * t, line1_1.y + cy * t);
            }

            var player1Pos = player1.Position.ToUnityVector();
            var player2Pos = player2.Position.ToUnityVector();

            if (isActive)
            {
                edgeFrontRenderer.transform.localScale = new(1f, 0.2f + System.Random.Shared.NextSingle() * 0.9f, 1f);

                node1Renderer.transform.position = player1Pos.AsVector3(-2.5f);
                node1Renderer.transform.localScale = Vector3.one * (System.Random.Shared.NextSingle() * 0.2f + 0.35f);
                node1Renderer.transform.localEulerAngles = new(0f, 0f, System.Random.Shared.NextSingle() * 360f);
                node2Renderer.transform.position = player2Pos.AsVector3(-2.5f);
                node2Renderer.transform.localScale = Vector3.one * (System.Random.Shared.NextSingle() * 0.2f + 0.35f);
                node2Renderer.transform.localEulerAngles = new(0f, 0f, System.Random.Shared.NextSingle() * 360f);
            }


            Vector3 center = HudManager.Instance.transform.position;
            center.z = -2.4f;
            meshRenderer.transform.position = center;
            center.z = 0f;

            if (counter % 3 == 0 || meshLength <= 1)
            {
                if (meshLength < MeshMax) meshLength++; else meshBegin = (meshBegin + 1) % MeshMax;
                if (!isActive) meshTerminal++;
            }
            counter = (counter + 1) % 3;
            int lastIndex = (meshBegin + meshLength - 1) % MeshMax;
            pos1[lastIndex] = player1Pos;
            pos2[lastIndex] = player2Pos;

            if (isActive)
            {

                Vector2 posDiff = player1Pos - player2Pos;
                if (center.Distance(player1Pos) < center.Distance(player2Pos)) posDiff = -posDiff;
                edgeFrontRenderer.transform.position = (player1Pos + player2Pos) * 0.5f;
                edgeFrontRenderer.transform.SetWorldZ(-2.5f);
                edgeFrontRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(posDiff.y, posDiff.x).RadToDeg());
                edgeFrontRenderer.size = new(posDiff.magnitude, 0.4f);
                edgeBackRenderer.size = new(posDiff.magnitude, 0.4f);

                if (edgeLightRenderer)
                {
                    edgeLightRenderer.transform.position = edgeFrontRenderer.transform.position;
                    edgeLightRenderer.transform.SetWorldZ(-10f);
                    edgeLightRenderer.transform.localEulerAngles = edgeFrontRenderer.transform.localEulerAngles;
                    edgeLightRenderer.size = new(posDiff.magnitude + 1.1f, 0.95f);
                }
            }

            int offset = MeshMax - meshLength;
            for (int i = 0; i < meshLength; i++)
            {
                pos[offset + i] = (Vector3)pos1[(meshBegin + i) % MeshMax] - center;
                pos[offset + i + MeshMax] = (Vector3)pos2[(meshBegin + i) % MeshMax] - center;
            }

            List<int> triangleList = [];

            Vector2 temp1, temp2, temp3, dir;
            float cross;
            for (int i = 0; i < meshLength - 1 - meshTerminal; i++)
            {
                if (LineSegmentCrossesLineSegment(
                    pos1[(meshBegin + i) % MeshMax], pos2[(meshBegin + i) % MeshMax],
                    pos1[(meshBegin + i + 1) % MeshMax], pos2[(meshBegin + i + 1) % MeshMax]))
                {
                    //線が交差する場合
                    temp3 = GetIntersectionPos(pos1[(meshBegin + i) % MeshMax], pos2[(meshBegin + i) % MeshMax], pos1[(meshBegin + i + 1) % MeshMax], pos2[(meshBegin + i + 1) % MeshMax]);
                    pos[offset + i + MeshMax + MeshMax] = (Vector3)temp3 - center;

                    temp1 = pos1[(meshBegin + i) % MeshMax];
                    temp2 = pos1[(meshBegin + i + 1) % MeshMax];
                    dir = temp2 - temp1;
                    cross = dir.x * temp3.y - dir.y * temp3.x;
                    if (cross > 0f) triangleList.AddRange([offset + i, offset + i + MeshMax + MeshMax, offset + i + 1]);
                    else triangleList.AddRange([offset + i, offset + i + 1, offset + i + MeshMax + MeshMax]);

                    temp1 = pos2[(meshBegin + i) % MeshMax];
                    temp2 = pos2[(meshBegin + i + 1) % MeshMax];
                    dir = temp2 - temp1;
                    cross = dir.x * temp3.y - dir.y * temp3.x;
                    if (cross > 0f) triangleList.AddRange([offset + i + MeshMax, offset + i + MeshMax + MeshMax, offset + i + 1 + MeshMax]);
                    else triangleList.AddRange([offset + i + MeshMax, offset + i + 1 + MeshMax, offset + i + MeshMax + MeshMax]);
                }
                else
                {
                    //線が交差しない場合
                    temp1 = pos2[(meshBegin + i) % MeshMax];
                    temp2 = pos2[(meshBegin + i + 1) % MeshMax];
                    temp3 = pos1[(meshBegin + i) % MeshMax];
                    dir = temp2 - temp1;
                    cross = dir.x * temp3.y - dir.y * temp3.x;
                    if (cross > 0f) triangleList.AddRange([offset + i + MeshMax, offset + i, offset + i + 1 + MeshMax]);
                    else triangleList.AddRange([offset + i + MeshMax, offset + i + 1 + MeshMax, offset + i]);

                    temp1 = pos1[(meshBegin + i) % MeshMax];
                    temp2 = pos1[(meshBegin + i + 1) % MeshMax];
                    temp3 = pos2[(meshBegin + i + 1) % MeshMax];
                    dir = temp2 - temp1;
                    cross = dir.x * temp3.y - dir.y * temp3.x;
                    if (cross > 0f) triangleList.AddRange([offset + i, offset + i + 1, offset + i + 1 + MeshMax]);
                    else triangleList.AddRange([offset + i, offset + i + 1 + MeshMax, offset + i + 1]);
                }
            }

            mesh.SetVertices(pos);
            mesh.SetTriangles(triangleList.ToArray(), 0);

            //効果音の位置調整
            if (soundSource)
            {
                Vector2 localPos = (Vector2)HudManager.Instance.transform.position - (Vector2)player1.Position;
                dir = player2.Position - player1.Position;
                var dirNorm = dir.normalized;
                var diff = Vector2.Dot(localPos, dirNorm);
                var mag = dir.magnitude;
                diff = Mathf.Clamp(diff, 0f, mag);
                soundSource.transform.position = (Vector3)player1.Position.ToUnityVector() + (Vector3)(dirNorm * diff);
            }
        }

        public void Inactivate()
        {
            if (node1Renderer) GameObject.Destroy(node1Renderer.gameObject);
            if (node2Renderer) GameObject.Destroy(node2Renderer.gameObject);
            if (edgeFrontRenderer) GameObject.Destroy(edgeFrontRenderer.gameObject);
            if (edgeLightRenderer) GameObject.Destroy(edgeLightRenderer.gameObject);
            if (soundSource)
            {
                soundSource.loop = false;
                NebulaManager.Instance.StartCoroutine(CoFadeout().WrapToIl2Cpp());
            }
            isActive = false;
        }
        void IGameOperator.OnReleased()
        {
            Inactivate();
            if (meshRenderer) GameObject.Destroy(meshRenderer.gameObject);
        }

        IEnumerator CoFadeout()
        {
            while(soundSource && soundSource.volume > 0f)
            {
                soundSource.volume -= Time.deltaTime * 3f;
                yield return null;
            }
        }

    }

    static private readonly RemoteProcess<(GamePlayer player1, GamePlayer player2, GamePlayer invoker)> RpcLaser = new("CupidLaser", (message, _) =>
    {
        float preDuration = (message.invoker == message.player1 || message.invoker == message.player2) ? LaserDelayByLoverOption : LaserDelayByCupidOption;
        if (message.player1.AmOwner || message.player2.AmOwner || message.invoker.AmOwner)
        {
            NebulaManager.Instance.StartCoroutine(CoPlayPreAnim(message.player1, preDuration).WrapToIl2Cpp());
            NebulaManager.Instance.StartCoroutine(CoPlayPreAnim(message.player2, preDuration).WrapToIl2Cpp());
        }
        if(message.player1.AmOwner || message.player2.AmOwner)
        {
            AmongUsUtil.PlayQuickFlash(Color.red.AlphaMultiplied(0.7f));
        }
        NebulaManager.Instance.StartDelayAction(preDuration, () =>
        {
            if (MeetingHud.Instance) return;
            if (message.invoker.AmOwner && message.player1.Position.Distance(message.player2.Position) > 30f) new StaticAchievementToken("cupid.common1");
            var redStr = new RedString(message.invoker,message.player1, message.player2).Register(NebulaAPI.CurrentGame!);
            NebulaManager.Instance.StartDelayAction(LaserDurationOption, () => redStr.Inactivate());
        });
    });

    static private readonly RemoteProcess<(GamePlayer player1, GamePlayer player2)> RpcSetLoverAbility = new("CupidLover", (message, _) =>
    {
        if (message.player1.AmOwner || message.player2.AmOwner)
        {
            bool isUsed = false;
            var stringButton = NebulaAPI.Modules.EffectButton(NebulaAPI.CurrentGame!, GamePlayer.LocalPlayer!, Virial.Compat.VirtualKeyInput.None, "cupid.lover.string",
                        LaserCooldownOption, LaserDurationOption + LaserDelayByLoverOption, "cupid.string", stringButtonSprite, null, _ => !message.player1.IsDead && !message.player2.IsDead && !isUsed);
            stringButton.SetLabelType(Virial.Components.ModAbilityButton.LabelType.Impostor);
            stringButton.OnEffectStart = (button) =>
            {
                using (RPCRouter.CreateSection("CupidLaser"))
                {
                    StatsLaserLover.Progress();
                    RpcLaser.Invoke((message.player1, message.player2, GamePlayer.LocalPlayer));
                    button.StartEffect();
                }
            };
            stringButton.OnEffectEnd = _ => isUsed = true;

            GameOperatorManager.Instance?.Subscribe<GameEndEvent>(ev =>
            {
                if (ev.EndState.EndCondition == NebulaGameEnds.LoversGameEnd.Get() && !message.player1.IsDead && !message.player2.IsDead && GamePlayer.AllPlayers.Count(p => p.PlayerState == PlayerState.Laser) >= 2)
                    new StaticAchievementToken("combination.2.cupid.lover.common1");

            }, NebulaAPI.CurrentGame!);
        }
    });
}