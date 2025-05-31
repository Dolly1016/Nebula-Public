using Il2CppInterop.Runtime.Injection;
using LibCpp2IL;
using Nebula.Behavior;
using Virial;
using Virial.Assignable;
using Virial.Components;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Neutral;


public class PaparazzoShot : MonoBehaviour
{
    SpriteRenderer flashRenderer = null!;
    SpriteRenderer frameRenderer = null!;
    SpriteRenderer backRenderer = null!;
    public SpriteRenderer centerRenderer = null!;
    BoxCollider2D collider = null!;

    public bool IsVert = false;
    public void ToggleDirection() => IsVert = !IsVert;
    static PaparazzoShot() => ClassInjector.RegisterTypeInIl2Cpp<PaparazzoShot>();

    private bool focus;

    public void SetLayer(int layer)
    {
        gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.layer = layer));
    }

    public void Awake()
    {
        frameRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
        flashRenderer = transform.GetChild(1).GetComponent<SpriteRenderer>();
        backRenderer = transform.GetChild(2).GetComponent<SpriteRenderer>();
        centerRenderer = transform.GetChild(3).GetComponent<SpriteRenderer>();
        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = frameRenderer.size;

        SetLayer(LayerExpansion.GetDefaultLayer());

        focus = true;
    }

    public void SetUpButton(Action action)
    {
        var button = gameObject.SetUpButton();
        button.OnClick.AddListener(() => { if (focus) { action.Invoke(); GameObject.Destroy(button); } });
    }

    public void Update()
    {
        if (focus)
        {
            var mouseInfo = PlayerModInfo.LocalMouseInfo;
            var dis = Mathf.Min(mouseInfo.distance, 2.4f + Mathf.Abs(Mathf.Cos(mouseInfo.angle)) * 1.7f);
            var targetPos = PlayerControl.LocalPlayer.transform.localPosition + new Vector3(Mathf.Cos(mouseInfo.angle), Mathf.Sin(mouseInfo.angle)) * dis;
            targetPos.z = -10f;

            transform.localPosition -= (transform.localPosition - targetPos) * Time.deltaTime * 8.6f;
            var scale = transform.localScale.x;
            var targetScale = Mathf.Clamp((4.1f - dis) * 0.25f + 0.5f, 0.65f, 1f);
            scale -= (scale - targetScale) * Time.deltaTime * 5.4f;
            transform.localScale = Vector3.one * scale;

            if(!Input.GetMouseButton(1))
                transform.eulerAngles = new Vector3(0, 0, mouseInfo.angle * 180f / Mathf.PI + (IsVert ? 90f : 0f));
        }
    }

    public void TakePicture(List<(Transform holder, PaparazzoShot shot,int playerMask)> shots,Action<bool>? callback = null)
    {
        focus = false;

        var scale = transform.localScale;
        
        GameObject camObj = new("CamObj");
        camObj.transform.SetParent(transform);
        camObj.transform.localScale = new Vector3(1, 1);
        camObj.transform.localPosition = new Vector3(0f, 0f, -10f);
        camObj.transform.localEulerAngles = new Vector3(0, 0, 0);
        
        //zを名前テキストより奥へ
        var pos = camObj.transform.position;
        pos.z = -0.4f;
        camObj.transform.position = pos;
        centerRenderer.gameObject.SetActive(false);

        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = transform.localScale.y * frameRenderer.size.y * 0.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 0b1101100000001;
        cam.nearClipPlane = -100;
        cam.farClipPlane = 100;
        cam.enabled = true;
        RenderTexture rt = new((int)(frameRenderer.size.x * 100f * scale.x), (int)(frameRenderer.size.y * 100f * scale.y), 16);
        rt.Create();
        cam.targetTexture = rt;

        foreach (var usable in ShipStatus.Instance.GetComponentsInChildren<IUsable>()) usable.SetOutline(false, false);

        //一時的に影を無視して描画させる
        using(var ignoreShadow = AmongUsUtil.IgnoreShadow(false)) cam.Render();

        RenderTexture.active = cam.targetTexture;
        Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, false);
        texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2D.Apply(false, false);
        var sprite = texture2D.ToSprite(100f);

        cam.targetTexture = null;
        RenderTexture.active = null;
        GameObject.Destroy(rt);
        GameObject.Destroy(camObj);

        centerRenderer.gameObject.SetActive(true);
        centerRenderer.transform.localPosition = new(0f, 0f, 0.1f);
        centerRenderer.transform.localScale = new(1f / scale.x, 1f / scale.y, 0.1f);
        centerRenderer.sprite = sprite;
        centerRenderer.material = VanillaAsset.GetHighlightMaterial();

        NebulaAsset.PlaySE(NebulaAudioClip.Camera);

        //映っているプレイヤーを調べる
        int playerMask = 0;
        int playerNum = 0;
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            if (p.Data.IsDead || !p.Visible || (p.GetModInfo()?.Unbox().IsInvisible ?? false)) continue;
            if (p.AmOwner) continue;

            if (collider.OverlapPoint(p.transform.position))
            {
                playerMask |= 1 << p.PlayerId;
                playerNum++;

                var anim = p.MyPhysics.Animations.Animator.m_currAnim;
                if (anim == p.MyPhysics.Animations.group.EnterVentAnim || anim == p.MyPhysics.Animations.group.ExitVentAnim)
                    new StaticAchievementToken("paparazzo.common3");
            }
        }

        Paparazzo.StatsPlayers.Progress(playerNum);

        foreach (var body in Helpers.AllDeadBodies())
        {
            if (collider.OverlapPoint(body.transform.position))
            {
                var info = NebulaGameManager.Instance!.GetPlayer(body.ParentId);
                if (info?.MyKiller != null && (playerMask & (1 << info.MyKiller.PlayerId)) != 0)
                {
                    //死体とそのキラーが映っているならば
                    new StaticAchievementToken("paparazzo.common4");
                }
            }
        }

        //UIレイヤー上の表示に変換
        SetLayer(LayerExpansion.GetUILayer());

        var pictureScaler = UnityHelper.CreateObject("PictureScaler", HudManager.Instance.transform, Vector3.zero);
        transform.SetParent(pictureScaler.transform, true);
        pictureScaler.transform.localScale = NebulaGameManager.Instance!.WideCamera.ViewerTransform.localScale;
        pictureScaler.transform.localEulerAngles = NebulaGameManager.Instance!.WideCamera.ViewerTransform.localEulerAngles;

        //UI変換後のスケーラ
        IEnumerator CoScale()
        {
            float t = 5f;
            while (t > 0f)
            {
                pictureScaler.transform.localScale -= (pictureScaler.transform.localScale - Vector3.one).Delta(2f, 0.02f);
                pictureScaler.transform.localEulerAngles -= (pictureScaler.transform.localEulerAngles - Vector3.zero).Delta(2f, 0.2f);
                transform.localPosition -= (transform.localPosition - new Vector3(0f, 0f, -10f)).Delta(8f, 0.2f);
                t -= Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator CoFlash()
        {
            flashRenderer.color = Color.white;
            float a = 1f;
            while (a > 0f)
            {
                a -= Time.deltaTime * 1.4f;
                a = Mathf.Clamp01(a);
                flashRenderer.color = Color.white.AlphaMultiplied(a);
                yield return null;
            }
            flashRenderer.gameObject.SetActive(false);

            if ((playerMask & ((~(PlayerControl.LocalPlayer.GetModInfo()?.Role as Paparazzo.Instance)?.DisclosedMask) ?? 0)) == 0)
            {
                //失敗時
                callback?.Invoke(false);

                var scale = transform.localScale.x;
                while (scale > 0f)
                {
                    transform.eulerAngles += new Vector3(0f, 0f, Time.deltaTime * 480f);
                    transform.localScale = new(scale, scale, 1f);
                    scale -= Time.deltaTime * 1.2f;
                    yield return null;
                }
                GameObject.Destroy(gameObject);
                GameObject.Destroy(pictureScaler);
            }
            else
            {
                //成功時
                callback?.Invoke(true);

                NebulaManager.Instance.StartCoroutine(CoScale().WrapToIl2Cpp());

                shots.Add((pictureScaler.transform, this,playerMask));

                GameObject players = UnityHelper.CreateObject("Players", pictureScaler.transform, Vector3.zero);
                players.transform.localEulerAngles = Vector3.zero;
                players.transform.localPosition = new(0, 0, -15f);

                var paparazzo = (PlayerControl.LocalPlayer.GetModInfo()!.Role as Paparazzo.Instance)!;

                int num = 0;
                foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) {
                    if (((1 << p.PlayerId) & playerMask) == 0) continue;

                    var icon = AmongUsUtil.GetPlayerIcon(p.GetModInfo()!.Unbox().CurrentOutfit, players.transform, new Vector3((float)(-(playerNum - 1) + num * 2) * 0.075f, -0.3f, -0.2f), Vector3.one * 0.1f);
                    icon.transform.localEulerAngles = Vector3.zero;
                    var script = icon.gameObject.AddComponent<ScriptBehaviour>();
                    
                    script.UpdateHandler += () =>
                    {
                        icon.SetAlpha(((paparazzo.DisclosedMask & (1 << p.PlayerId)) == 0) ? 0.4f : 1f);
                    };
                    num++;
                }

                if (num >= 4) new StaticAchievementToken("paparazzo.common2");

                players.transform.localScale = new Vector3(0f, 0f, 1f);

                yield return Effects.Wait(2f);
                var playersScale = 0f;
                while (playersScale < 0.995f)
                {
                    playersScale -= (playersScale - 1f) * Time.deltaTime * 3.5f;
                    players.transform.localScale = new(playersScale, playersScale, 1f);
                    yield return null;
                }
                players.transform.localScale = Vector3.one;
                
            }
        }

        StartCoroutine(CoFlash().WrapToIl2Cpp());
    }
}


[NebulaRPCHolder]
public class Paparazzo : DefinedRoleTemplate, DefinedRole
{
    static readonly public RoleTeam MyTeam = NebulaAPI.Preprocessor!.CreateTeam("teams.paparazzo", new(202,118,140), TeamRevealType.OnlyMe);

    private Paparazzo() : base("paparazzo", MyTeam.Color, RoleCategory.NeutralRole, MyTeam, [ShotCoolDownOption, RequiredSubjectsOption, RequiredDisclosedOption, VentConfiguration])
    {
        ConfigurationHolder?.AddTags(ConfigurationTags.TagFunny, ConfigurationTags.TagDifficult);
        ConfigurationHolder!.Illustration = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Configurations/Paparazzo.png");
    }

    RuntimeRole RuntimeAssignableGenerator<RuntimeRole>.CreateInstance(GamePlayer player, int[] arguments) => new Instance(player, arguments);

    static private FloatConfiguration ShotCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.paparazzo.shotCoolDown", (2.5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private IntegerConfiguration RequiredSubjectsOption = NebulaAPI.Configurations.Configuration("options.role.paparazzo.requiredSubjects", (1, 15), 5);
    static private IntegerConfiguration RequiredDisclosedOption = NebulaAPI.Configurations.Configuration("options.role.paparazzo.requiredDisclosed", (1, 15), 3);
    static private IVentConfiguration VentConfiguration = NebulaAPI.Configurations.NeutralVentConfiguration("role.paparazzo.vent", true);

    static public readonly Paparazzo MyRole = new();
    static private GameStatsEntry StatsPhoto = NebulaAPI.CreateStatsEntry("stats.paparazzo.photo", GameStatsCategory.Roles, MyRole);
    static internal GameStatsEntry StatsPlayers = NebulaAPI.CreateStatsEntry("stats.paparazzo.players", GameStatsCategory.Roles, MyRole);
    public class Instance : RuntimeVentRoleTemplate, RuntimeRole
    {
        public override DefinedRole Role => MyRole;

        private List<(Transform holder,PaparazzoShot shot,int playerMask)> shots = [];
        private HudContent? shotsHolder = null;

        AchievementToken<(bool cleared, int? lastAlive)>? acTokenChallenge = null;

        public Instance(GamePlayer player, int[] arguments) : base(player, VentConfiguration)
        {
        }

        //private Modules.ScriptComponents.ModAbilityButtonImpl? shotButton = null;
        static private Image cameraButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CameraButton.png", 115f);

        //private ComponentBinding<PaparazzoShot>? MyFinder = null;

        public int DisclosedMask = 0;
        public int CapturedMask = 0;

        private int GetActivatedBits(int mask)
        {
            int num = 0;
            while (mask != 0)
            {
                if ((mask & 1) != 0) num++;
                mask >>= 1;
            }
            return num;
        }

        [OnlyMyPlayer]
        void CheckWins(PlayerCheckWinEvent ev) => ev.SetWinIf(ev.GameEnd == NebulaGameEnd.PaparazzoWin && CheckPaparazzoWin());

        public override void OnActivated()
        {
            if (AmOwner)
            {
                PaparazzoShot? lastFinder = null;

                var acTokenCommon = new AchievementToken<int>("paparazzo.common1", 0, (val, _) => val >= 3);
                acTokenChallenge = new("paparazzo.challenge",(false,null),(val,_)=>val.cleared);

                shotsHolder = HudContent.InstantiateContent("Pictures", true, true, false, true);
                this.BindGameObject(shotsHolder.gameObject);

                var shotButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "paparazzo.camera",
                    ShotCoolDownOption, "shot", cameraButtonSprite, 
                    _ => lastFinder
                    ).BindSubKey(Virial.Compat.VirtualKeyInput.AidAction,"paparazzo.toggle", true)
                    .SetAsMouseClickButton();
                shotButton.OnClick = (button) => {
                    GameObject.Destroy(lastFinder!.GetComponent<PassiveButton>());

                    IEnumerator CoTakePicture(PaparazzoShot finder)
                    {
                        yield return new WaitForEndOfFrame();
                        finder.TakePicture(shots, success => acTokenCommon.Value += success ? 1 : 0);
                    }
                    NebulaManager.Instance.StartCoroutine(CoTakePicture(lastFinder).WrapToIl2Cpp());
                    lastFinder = null;
                    shotButton.StartCoolDown();

                    StatsPhoto.Progress();
                };
                shotButton.OnSubAction = (button) =>
                {
                    if (lastFinder) lastFinder!.ToggleDirection();
                };

                void DestroyFinder()
                {
                    if (lastFinder) GameObject.Destroy(lastFinder!.gameObject);
                    lastFinder = null;
                }

                GameOperatorManager.Instance?.RegisterReleasedAction(DestroyFinder, this);
                GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => {
                    if (!MyPlayer.IsDead && !(shotButton?.CoolDownTimer?.IsProgressing ?? true) && lastFinder == null && !MeetingHud.Instance && !ExileController.Instance)
                    {
                        lastFinder = GameObject.Instantiate(NebulaAsset.PaparazzoShot, null).AddComponent<PaparazzoShot>();
                        lastFinder.gameObject.layer = LayerExpansion.GetUILayer();
                        lastFinder.transform.localScale = Vector3.zero;
                        var pos = MyPlayer.VanillaPlayer.transform.localPosition;
                        pos.z = -10f;
                        lastFinder.transform.localPosition = pos;

                        //shot.SetUpButton(() => shotButton.DoClick());
                    }

                    if (lastFinder != null && (MeetingHud.Instance || ExileController.Instance || MyPlayer.IsDead)) DestroyFinder();
                }, this);
            }
        }

        internal bool CheckPaparazzoWin()
        {
            return !MyPlayer.IsDead && (GetActivatedBits(CapturedMask) >= RequiredSubjectsOption && GetActivatedBits(DisclosedMask) >= RequiredDisclosedOption);
        }

        [Local]
        void LocalHudUpdate(GameHudUpdateEvent ev)
        {
            if (shotsHolder != null) {
                int num = 0;
                shots.RemoveAll(shot => {
                    if ((shot.playerMask & (~DisclosedMask)) == 0)
                    {
                        GameObject.Destroy(shot.holder.gameObject);
                        return true;
                    }
                    return false;
                });

                int mask = 0;
                foreach (var shot in shots) mask |= shot.playerMask;
                if((mask | CapturedMask) != CapturedMask)
                {
                    CapturedMask |= mask;
                    RpcShareState.Invoke((MyPlayer.PlayerId, CapturedMask, DisclosedMask));
                }

                foreach (var shot in shots)
                {
                    

                    if (shot.holder.transform.parent != shotsHolder!.transform) shot.holder.transform.SetParent(shotsHolder.transform, true);

                    var scale = shot.shot.transform.localScale.x;
                    scale -= (scale - 0.2f) * Time.deltaTime * 3.6f;
                    shot.shot.transform.localScale = new(scale, scale, 1f);

                    var diffPos = shot.holder.transform.localPosition - new Vector3(-0.3f + 0.6f * (float)num, -0.25f, -10f - (float)num * 0.6f);
                    shot.holder.transform.localPosition -= diffPos * Mathf.Min(1f, Time.deltaTime) * 6.4f;

                    num++;
                }
            }
        }

        static private SpriteLoader hourGlassSprite = SpriteLoader.FromResource("Nebula.Resources.Hourglass.png", 100f);
        
        [Local]
        void OnMeetingStart(MeetingStartEvent ev)
        {
            if (!MyPlayer.IsDead)
            {
                bool shareFlag = false;
                float timer = 20f;

                var hourglass = UnityHelper.CreateObject<SpriteRenderer>("Hourglass", shotsHolder!.transform, new Vector3(shots.Count * 0.6f, -0.25f, -10f));
                hourglass.sprite = hourGlassSprite.GetSprite();
                var hourText = GameObject.Instantiate(HudManager.Instance.KillButton.cooldownTimerText, hourglass.transform);
                hourText.text = Mathf.CeilToInt(timer).ToString();
                hourText.transform.localScale = new(0.5f, 0.5f, 1f);
                hourText.gameObject.SetActive(true);


                IEnumerator CoWaitSharing()
                {
                    while (!shareFlag && timer > 0f && MeetingHudExtension.CanShowPhotos) {
                        timer -= Time.deltaTime;
                        hourText.text = Mathf.CeilToInt(timer).ToString();

                        yield return null;
                    }

                    foreach (var shot in shots)
                    {
                        if (!shot.shot) continue;
                        if (shot.shot.gameObject.TryGetComponent<PassiveButton>(out var button)) GameObject.Destroy(button);
                    }

                    if (hourglass) GameObject.Destroy(hourglass.gameObject);
                }

                NebulaManager.Instance.StartCoroutine(CoWaitSharing().WrapToIl2Cpp());

                foreach (var shot in shots)
                {
                    var button = shot.shot.gameObject.SetUpButton(true);
                    button.OnMouseOut.AddListener(() =>
                    {
                        AmongUsUtil.SetHighlight(shot.shot.centerRenderer, false);
                    });
                    button.OnMouseOver.AddListener(() =>
                    {
                        AmongUsUtil.SetHighlight(shot.shot.centerRenderer, true);
                    });
                    button.OnClick.AddListener(() =>
                    {
                        if (MyPlayer.IsDead) return;

                        int aliveMask = 0;
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo) if (!p.IsDead) aliveMask |= 1 << p.PlayerId;
                        DisclosedMask |= (shot.playerMask & aliveMask);
                        RpcShareState.Invoke((MyPlayer.PlayerId, CapturedMask, DisclosedMask));

                        SharePicture((shot.playerMask & aliveMask), shot.shot.centerRenderer.transform.localScale.x, shot.shot.transform.localEulerAngles.z, shot.shot.centerRenderer.sprite.texture);
                        shareFlag = true;

                        if(acTokenChallenge != null) acTokenChallenge.Value.lastAlive = NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && !p.AmOwner);
                    });


                }
            }
        }

        [Local]
        void OnMeetingEnd(MeetingEndEvent ev)
        {
            if (acTokenChallenge != null)
            {
                acTokenChallenge.Value.cleared |= (acTokenChallenge.Value.lastAlive ?? 0) - NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.IsDead && !p.AmOwner) >= 4;
                acTokenChallenge.Value.lastAlive = null;
            }
        }

        [Local]
        void AppendExtraTaskText(PlayerTaskTextLocalEvent ev)
        {
            var text = Language.Translate("role.paparazzo.taskText");

            var detail = Language.Translate("role.paparazzo.taskTextDisclosed")
                .Replace("%GD%", RequiredDisclosedOption.GetValue().ToString())
                .Replace("%CD%", GetActivatedBits(DisclosedMask).ToString());
            if (RequiredDisclosedOption < RequiredSubjectsOption)
            {
                detail = Language.Translate("role.paparazzo.taskTextSubject")
                    .Replace("%GS%", RequiredSubjectsOption.GetValue().ToString())
                    .Replace("%CS%", GetActivatedBits(CapturedMask).ToString())
                    + ", " + detail;
            }

            ev.AppendText(text.Replace("%DETAIL%", detail));
        }

        [OnlyHost]
        void CheckWin(GameUpdateEvent ev)
        {
            if(CheckPaparazzoWin() && !MeetingHud.Instance) NebulaAPI.CurrentGame?.TriggerGameEnd(NebulaGameEnd.PaparazzoWin, GameEndReason.SpecialSituation);
        }

        
    }

    public static void SharePicture(int playersMask, float scale,float angle,Texture2D texture)
    {
        RpcSharePicture.Invoke((scale,angle,UnityEngine.ImageConversion.EncodeToJPG(texture, 60)));
        RpcShareDisclosedPlayers.Invoke(playersMask);
    }

    public static readonly RemoteProcess<int> RpcShareDisclosedPlayers = new("ShareDisclosed",
        (message, _) =>
        {
            bool takenSelf = (message & (1 << PlayerControl.LocalPlayer.PlayerId)) != 0;
            if (takenSelf && (GamePlayer.LocalPlayer?.IsImpostor ?? false)) new StaticAchievementToken("paparazzo.another1");
        });
    public static readonly DivisibleRemoteProcess<(float, float, byte[]), (int id, float scale, float angle, int length, int index, byte[] bytes)> RpcSharePicture = new("SharePicture",
        (message) =>
        {
            int id = System.Random.Shared.Next(100000);

            List<(byte[],int)> arrays = [];
            int proceed = 0;
            int index = 0;
            while (proceed < message.Item3.Length)
            {
                int last = proceed;
                proceed = Mathf.Min(proceed + 500, message.Item3.Length);

                arrays.Add((message.Item3.SubArray(last, proceed - last),index));
                index++;
            }
            return arrays.Select(array => (id, message.Item1, message.Item2, arrays.Count, array.Item2, array.Item1)).GetEnumerator();
        },
        (writer, divided) => {
            writer.Write(divided.id);
            writer.Write(divided.scale);
            writer.Write(divided.angle);
            writer.Write(divided.length);
            writer.Write(divided.index);
            writer.WriteBytesAndSize(divided.bytes);
        },
        (reader) => {
            return (reader.ReadInt32(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadBytesAndSize());
        },
        (divided,_) => {
            if (!storedTexture!.ContainsKey(divided.id)) storedTexture[divided.id] = (divided.scale, divided.angle, divided.length, new byte[]?[divided.length]);

            var stored = storedTexture[divided.id];
            stored.bytes[divided.index] = divided.bytes;
            if (stored.bytes.All(b => b != null))
            {
                var obj = GameObject.Instantiate(NebulaAsset.PaparazzoShot, null);
                MeetingHudExtension.AddLeftContent(obj);
                var renderer = obj.transform.GetChild(3).GetComponent<SpriteRenderer>();
                List<byte> data = [];
                foreach (byte[]? b in stored.bytes) data.AddRange(b!);
                Texture2D texture = new(1, 1);
                ImageConversion.LoadImage(texture, data.ToArray());
                renderer.sprite = texture.ToSprite(100f);
                renderer.transform.localPosition = new(0f, 0f, 0.1f);
                renderer.transform.localScale = Vector3.one * stored.scale;
                storedTexture.Remove(divided.id);
                obj.transform.localEulerAngles = new(0, 0, stored.angle);
                obj.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.layer = LayerExpansion.GetUILayer()));
                IEnumerator CoShow()
                {
                    NebulaAsset.PlaySE(NebulaAudioClip.PaparazzoDisclose, volume: 1f);

                    float scale = 0f;
                    while (scale < 0.4f)
                    {
                        scale -= (scale - 0.4f) * Time.deltaTime * 4f;
                        if (obj) obj.transform.localScale = Vector3.one * scale;
                        yield return null;
                    }
                    if (obj) obj.transform.localScale = Vector3.one * 0.4f;
                }

                NebulaManager.Instance.StartCoroutine(CoShow().WrapToIl2Cpp());

                if (MeetingHud.Instance != null) MeetingHud.Instance.ResetPlayerState();
            }
        }
        );

    public static RemoteProcess<(byte playerId, int subjectMask, int disclosedMask)> RpcShareState = new ("SharePaparazzo", (message,_) =>
    {
        var role = NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Role;
        if (role is Paparazzo.Instance paparazzo)
        {
            paparazzo.CapturedMask = message.subjectMask;
            paparazzo.DisclosedMask = message.disclosedMask;
        }
    });

    public static readonly Dictionary<int, (float scale, float angle, int length, byte[]?[] bytes)> storedTexture = [];
}
