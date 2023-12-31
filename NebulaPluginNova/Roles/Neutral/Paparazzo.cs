﻿using Il2CppInterop.Runtime.Injection;
using LibCpp2IL;
using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Assignable;

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

    public void Awake()
    {
        frameRenderer = transform.GetChild(0).GetComponent<SpriteRenderer>();
        flashRenderer = transform.GetChild(1).GetComponent<SpriteRenderer>();
        backRenderer = transform.GetChild(2).GetComponent<SpriteRenderer>();
        centerRenderer = transform.GetChild(3).GetComponent<SpriteRenderer>();
        collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = frameRenderer.size;

        gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.layer = LayerExpansion.GetUILayer()));

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
            targetPos.z = -5f;

            transform.localPosition -= (transform.localPosition - targetPos) * Time.deltaTime * 8.6f;
            var scale = transform.localScale.x;
            var targetScale = Mathf.Clamp((4.1f - dis) * 0.25f + 0.5f, 0.65f, 1f);
            scale -= (scale - targetScale) * Time.deltaTime * 5.4f;
            transform.localScale = Vector3.one * scale;

            transform.eulerAngles = new Vector3(0, 0, mouseInfo.angle * 180f / Mathf.PI + (IsVert ? 90f : 0f));
        }
    }

    public void TakePicture(List<(Transform holder, PaparazzoShot shot,int playerMask)> shots)
    {
        focus = false;

        var scale = transform.localScale;
        
        GameObject camObj = new GameObject();
        camObj.transform.SetParent(transform);
        camObj.transform.localScale = new Vector3(1, 1);
        camObj.transform.localPosition = new Vector3(0f, 0f, 0f);
        camObj.transform.localEulerAngles = new Vector3(0, 0, 0);
        
        //zを名前テキストより奥へ
        var pos = camObj.transform.position;
        pos.z = -0.4f;
        camObj.transform.position = pos;

        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = transform.localScale.y * frameRenderer.size.y * 0.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.cullingMask = 0b1101100000001;
        cam.enabled = true;
        RenderTexture rt = new RenderTexture((int)(frameRenderer.size.x * 100f * scale.x), (int)(frameRenderer.size.y * 100f * scale.y), 16);
        rt.Create();
        cam.targetTexture = rt;

        foreach (var usable in ShipStatus.Instance.GetComponentsInChildren<IUsable>()) usable.SetOutline(false, false);
        
        cam.Render();

        RenderTexture.active = cam.targetTexture;
        Texture2D texture2D = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false, false);
        texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        texture2D.Apply();
        var sprite = texture2D.ToSprite(100f);

        cam.targetTexture = null;
        RenderTexture.active = null;
        GameObject.Destroy(rt);
        GameObject.Destroy(cam);

        centerRenderer.transform.localPosition = new(0f, 0f, 0.1f);
        centerRenderer.transform.localScale = new(1f / scale.x, 1f / scale.y, 0.1f);
        centerRenderer.sprite = sprite;
        centerRenderer.material = VanillaAsset.GetHighlightMaterial();

        NebulaAsset.PlaySE(NebulaAudioClip.Camera);

        transform.SetParent(HudManager.Instance.transform, true);

        int playerMask = 0;
        int playerNum = 0;
        foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator())
        {
            if (p.Data.IsDead || p.inVent || (p.GetModInfo()?.HasAttribute(Virial.Game.PlayerAttribute.Invisible) ?? false)) continue;
            if (p.AmOwner) continue;

            if (collider.OverlapPoint(p.transform.position))
            {
                playerMask |= 1 << p.PlayerId;
                playerNum++;
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
                var scale = transform.localScale.x;
                while (scale > 0f)
                {
                    transform.eulerAngles += new Vector3(0f, 0f, Time.deltaTime * 480f);
                    transform.localScale = new(scale, scale, 1f);
                    scale -= Time.deltaTime * 1.2f;
                    yield return null;
                }
                GameObject.Destroy(gameObject);
            }
            else
            {
                var t = UnityHelper.CreateObject("Picture", transform.parent, transform.localPosition);
                transform.SetParent(t.transform, true);
                shots.Add((t.transform, this,playerMask));

                GameObject players = UnityHelper.CreateObject("Players", transform.parent, transform.localPosition);
                int num = 0;
                foreach (var p in PlayerControl.AllPlayerControls.GetFastEnumerator()) {
                    if (((1 << p.PlayerId) & playerMask) == 0) continue;

                    var icon = AmongUsUtil.GetPlayerIcon(p.GetModInfo()!.CurrentOutfit, players.transform, new Vector3((float)(-(playerNum - 1) + num * 2) * 0.075f, -0.3f, -0.2f), Vector3.one * 0.1f);
                    var script = icon.gameObject.AddComponent<ScriptBehaviour>();
                    var paparazzo = (PlayerControl.LocalPlayer.GetModInfo()!.Role as Paparazzo.Instance)!;
                    script.UpdateHandler += () =>
                    {
                        icon.SetAlpha(((paparazzo.DisclosedMask & (1 << p.PlayerId)) == 0) ? 0.4f : 1f);
                    };
                    num++;
                }

                players.transform.localScale = new Vector3(0f, 0f, 1f);
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
public class Paparazzo : ConfigurableStandardRole
{
    static public Paparazzo MyRole = new Paparazzo();
    static public Team MyTeam = new("teams.paparazzo", MyRole.RoleColor, TeamRevealType.OnlyMe);

    public override RoleCategory RoleCategory => RoleCategory.NeutralRole;

    public override string LocalizedName => "paparazzo";
    public override Color RoleColor => new Color(202f / 255f, 118f / 255f, 140f / 255f);
    public override RoleTeam Team => MyTeam;

    public override RoleInstance CreateInstance(PlayerModInfo player, int[] arguments) => new Instance(player, arguments);

    private NebulaConfiguration ShotCoolDownOption = null!;
    private NebulaConfiguration RequiredSubjectsOption = null!;
    private NebulaConfiguration RequiredDisclosedOption = null!;
    private new VentConfiguration VentConfiguration = null!;
    protected override void LoadOptions()
    {
        base.LoadOptions();

        VentConfiguration = new(RoleConfig, null, (5f, 60f, 15f), (2.5f, 30f, 10f), true);
        ShotCoolDownOption = new NebulaConfiguration(RoleConfig, "shotCoolDown", null, 2.5f, 60f, 2.5f, 20f, 20f) { Decorator = NebulaConfiguration.SecDecorator };
        RequiredSubjectsOption = new NebulaConfiguration(RoleConfig, "requiredSubjects", null, 1, 15, 5, 5);
        RequiredDisclosedOption = new NebulaConfiguration(RoleConfig, "requiredDisclosed", null, 1, 15, 3, 3);
    }

    public class Instance : RoleInstance
    {
        public override AbstractRole Role => MyRole;

        private Timer ventCoolDown = new Timer(MyRole.VentConfiguration.CoolDown).SetAsAbilityCoolDown().Start();
        private Timer ventDuration = new(MyRole.VentConfiguration.Duration);
        private bool canUseVent = MyRole.VentConfiguration.CanUseVent;
        public override Timer? VentCoolDown => ventCoolDown;
        public override Timer? VentDuration => ventDuration;
        public override bool CanUseVent => canUseVent;
        private List<(Transform holder,PaparazzoShot shot,int playerMask)> shots = new();
        private HudContent? shotsHolder = null;
        private bool canWin = false;

        public Instance(PlayerModInfo player, int[] arguments) : base(player)
        {
            if(arguments.Length == 2)
            {

            }
        }

        private ModAbilityButton? shotButton = null;
        static private ISpriteLoader cameraButtonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.CameraButton.png", 115f);

        private ComponentBinding<PaparazzoShot>? MyFinder = null;

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

        public override bool CheckWins(CustomEndCondition endCondition, ref ulong extraWinMask)
        {
            return endCondition == NebulaGameEnd.PaparazzoWin && canWin;
        }

        static NebulaEndCriteria PaparazzoCriteria = new() { 
            OnExiled = (_) =>
            {
                foreach(var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if(p.Role is Paparazzo.Instance paparazzo && paparazzo.CheckPaparazzoWin())
                    {
                        return new(NebulaGameEnd.PaparazzoWin, 1 << p.PlayerId);
                    }
                }
                return null;
            },
            OnUpdate = () =>
            {
                if (MeetingHud.Instance || (ExileController.Instance && !Minigame.Instance)) return null;

                foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo())
                {
                    if (p.Role is Paparazzo.Instance paparazzo && paparazzo.CheckPaparazzoWin())
                    {
                        NebulaManager.Instance.ScheduleDelayAction(() =>
                        {
                            NebulaGameManager.Instance?.RpcInvokeSpecialWin(NebulaGameEnd.PaparazzoWin, 1 << p.PlayerId);
                        });
                        return null;
                    }
                }
                
                return null;
            }
        };

        public override void OnActivated()
        {
            NebulaGameManager.Instance?.CriteriaManager.AddCriteria(PaparazzoCriteria);

            if (AmOwner)
            {
                shotsHolder = HudContent.InstantiateContent("Pictures", true, true, false, true);
                Bind(shotsHolder.gameObject);

                shotButton = Bind(new ModAbilityButton()).KeyBind(Virial.Compat.VirtualKeyInput.Ability).SubKeyBind(Virial.Compat.VirtualKeyInput.AidAction);
                shotButton.SetSprite(cameraButtonSprite.GetSprite());
                shotButton.Availability = (button) => MyPlayer.MyControl.CanMove && MyFinder != null;
                shotButton.Visibility = (button) => !MyPlayer.MyControl.Data.IsDead;
                shotButton.OnClick = (button) => {
                    GameObject.Destroy(MyFinder?.MyObject?.GetComponent<PassiveButton>());
                    MyFinder?.MyObject?.TakePicture(shots);
                    MyFinder?.Detach();
                    MyFinder = null;
                    shotButton.StartCoolDown();
                };
                shotButton.OnSubAction= (button) => MyFinder?.MyObject!.ToggleDirection();
                shotButton.CoolDownTimer = Bind(new Timer(0f, MyRole.ShotCoolDownOption.GetFloat()).SetAsAbilityCoolDown().Start());
                shotButton.SetLabel("shot");
                shotButton.SetCanUseByMouseClick(true);
            }

        }

        public override void LocalUpdate()
        {
            if (!MyPlayer.IsDead && !(shotButton?.CoolDownTimer?.IsInProcess ?? true) && MyFinder == null && !MeetingHud.Instance && !ExileController.Instance)
            {
                MyFinder = Bind(new ComponentBinding<PaparazzoShot>(GameObject.Instantiate(NebulaAsset.PaparazzoShot, null).AddComponent<PaparazzoShot>()));
                var shot = MyFinder.MyObject!;
                shot.gameObject.layer = LayerExpansion.GetUILayer();
                shot.transform.localScale = Vector3.zero;
                var pos = MyPlayer.MyControl.transform.localPosition;
                pos.z = -10f;
                shot.transform.localPosition = pos;

                shot.SetUpButton(() => shotButton.DoClick());
            }

            if (MyFinder != null && (MeetingHud.Instance || ExileController.Instance || MyPlayer.IsDead))
            {
                MyFinder?.Release();
                MyFinder = null;
            }
        }

        private bool CheckPaparazzoWin()
        {
            return !MyPlayer.IsDead && (GetActivatedBits(CapturedMask) >= MyRole.RequiredSubjectsOption && GetActivatedBits(DisclosedMask) >= MyRole.RequiredDisclosedOption);
        }

        public override void LocalHudUpdate()
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
        public override void OnMeetingStart()
        {
            if (AmOwner && !MyPlayer.IsDead)
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
                    while (!shareFlag && timer > 0f) {
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
                        shot.shot.centerRenderer.material.SetFloat("_Outline", 0f);
                        shot.shot.centerRenderer.material.SetColor("_AddColor", Color.clear);
                    });
                    button.OnMouseOver.AddListener(() =>
                    {
                        shot.shot.centerRenderer.material.SetFloat("_Outline", 1f);
                        shot.shot.centerRenderer.material.SetColor("_OutlineColor", Color.yellow);
                        shot.shot.centerRenderer.material.SetColor("_AddColor", Color.yellow);
                    });
                    button.OnClick.AddListener(() =>
                    {
                        if (MyPlayer.IsDead) return;

                        int aliveMask = 0;
                        foreach (var p in NebulaGameManager.Instance!.AllPlayerInfo()) if (!p.IsDead) aliveMask |= 1 << p.PlayerId;
                        DisclosedMask |= (shot.playerMask & aliveMask);
                        RpcShareState.Invoke((MyPlayer.PlayerId, CapturedMask, DisclosedMask));

                        CheckPaparazzoWin();
                        SharePicture(shot.shot.centerRenderer.transform.localScale.x, shot.shot.transform.localEulerAngles.z, shot.shot.centerRenderer.sprite.texture);
                        shareFlag = true;
                    });


                }
            }
        }

        public override string? GetExtraTaskText()
        {
            var text = Language.Translate("role.paparazzo.taskText");
            var detail = "";

            detail = Language.Translate("role.paparazzo.taskTextDisclosed")
                .Replace("%GD%", MyRole.RequiredDisclosedOption.GetMappedInt().ToString())
                .Replace("%CD%", GetActivatedBits(DisclosedMask).ToString());
            if (MyRole.RequiredDisclosedOption < MyRole.RequiredSubjectsOption)
            {
                detail = Language.Translate("role.paparazzo.taskTextSubject")
                    .Replace("%GS%", MyRole.RequiredSubjectsOption.GetMappedInt().ToString())
                    .Replace("%CS%", GetActivatedBits(CapturedMask).ToString())
                    + ", " + detail;
            }
            return text.Replace("%DETAIL%", detail);
        }
    }

    public static void SharePicture(float scale,float angle,Texture2D texture)
    {
        RpcSharePicture.Invoke((scale,angle,UnityEngine.ImageConversion.EncodeToJPG(texture, 60)));
    }

    public static DivisibleRemoteProcess<(float, float, byte[]), (int id, float scale, float angle, int length, int index, byte[] bytes)> RpcSharePicture = new("SharePicture",
        (message) =>
        {
            int id = System.Random.Shared.Next(100000);

            List<(byte[],int)> arrays = new();
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
                List<byte> data = new();
                foreach (byte[]? b in stored.bytes) data.AddRange(b!);
                Texture2D texture = new Texture2D(1, 1);
                ImageConversion.LoadImage(texture, data.ToArray());
                renderer.sprite = texture.ToSprite(100f);
                renderer.transform.localPosition = new(0f, 0f, 0.1f);
                renderer.transform.localScale = Vector3.one * stored.scale;
                storedTexture.Remove(divided.id);
                obj.transform.localEulerAngles = new(0, 0, stored.angle);
                obj.ForEachChild((Il2CppSystem.Action<GameObject>)((obj) => obj.layer = LayerExpansion.GetUILayer()));
                IEnumerator CoShow()
                {
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
        var role = NebulaGameManager.Instance?.GetModPlayerInfo(message.playerId)?.Role;
        if (role is Paparazzo.Instance paparazzo)
        {
            paparazzo.CapturedMask = message.subjectMask;
            paparazzo.DisclosedMask = message.disclosedMask;
        }
    });

    public static Dictionary<int, (float scale, float angle, int length, byte[]?[] bytes)> storedTexture = new();
}
