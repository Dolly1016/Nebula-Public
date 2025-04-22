using Il2CppInterop.Runtime.Injection;
using Nebula.Behaviour;
using Nebula.Map;
using Nebula.Roles.Neutral;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial;
using Virial.Assignable;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Media;
using static Nebula.Roles.Impostor.Thurifer;
using static Nebula.Roles.Neutral.Spectre;

namespace Nebula.Roles.Impostor;

internal class Balloon
{
    static private Image BalloonSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.Balloon.png", 100f);
    static private Image HighlightSprite = SpriteLoader.FromResource("Nebula.Resources.Balloon.BalloonHighlight.png", 100f);
    static private MultiImage BalloonStringSprite = DividedSpriteLoader.FromResource("Nebula.Resources.Balloon.BalloonString.png", 100f, 3, 3).SetPivot(new(0.5f, 0f));

    private const float DefaultStringLength = 0.97f;
    static private (float Min, float Max) StringRange = (0.61f, DefaultStringLength);
    static private float[] Variation = [0.95f, 0.90f, 0.84f, 0.79f, 0.75f, 0.72f, 0.67f, 0.63f, -100f];
    static private Vector2 HandDiff = new Vector2(0.1f, -0.05f);

    private SpriteRenderer balloonRenderer;
    private SpriteRenderer highlightRenderer;
    private SpriteRenderer stringRenderer;
    private GameObject holder;
    private GamePlayer target;

    private Vector2 HandCenter = Vector2.zero;
    public Balloon(GamePlayer target)
    {
        this.target = target;

        holder = UnityHelper.CreateObject("BalloonHolder", null, Vector3.zero, LayerExpansion.GetDefaultLayer());
        balloonRenderer = UnityHelper.CreateSpriteRenderer("Balloon", holder.transform, Vector3.zero);
        balloonRenderer.transform.localScale = new(0.82f, 0.82f, 1f);
        highlightRenderer = UnityHelper.CreateSpriteRenderer("Highlight", balloonRenderer.transform, new(0.03f, 0.38f, -0.01f));
        highlightRenderer.color = Color.white.AlphaMultiplied(0.35f);

        var diff = HandDiff;
        var lossyScale = target.VanillaPlayer.cosmetics.transform.lossyScale.x;
        diff.x /= lossyScale;
        diff.y /= lossyScale;
        HandCenter = diff;
        stringRenderer = UnityHelper.CreateSpriteRenderer("String", target.VanillaPlayer.cosmetics.transform, HandCenter);
        stringRenderer.transform.localScale = new(1f / lossyScale, 1f / lossyScale, 1f);
        balloonRenderer.sprite = BalloonSprite.GetSprite();
        highlightRenderer.sprite = HighlightSprite.GetSprite();
        balloonRenderer.material = HatManager.Instance.PlayerMaterial;
        stringRenderer.sprite = BalloonStringSprite.GetSprite(0);

        UpdateBallonColor();
    }

    public Vector2 HandPos => stringRenderer.transform.position;
    public Vector2 CenterPos => HandPos + new Vector2(0f, DefaultStringLength);
    
    /// <summary>
    /// 風船を初期位置に戻します。糸を再調整する必要があります。
    /// </summary>
    private void ResetToDefaultPos()
    {
        balloonRenderer.transform.position = CenterPos.AsVector3(BalloonZ);
    }
    private float BalloonZ => target.AmOwner ? -15f : target.VanillaPlayer.transform.position.z - 0.1f;
    /// <summary>
    /// 糸の位置を調整します。
    /// </summary>
    private void ReflectString()
    {
        Vector2 diff = balloonRenderer.transform.position - stringRenderer.transform.position;
        stringRenderer.transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x).RadToDeg() - 90f);
        var mag = diff.magnitude;
        stringRenderer.sprite = BalloonStringSprite.GetSprite(Variation.FindIndex(num => num < mag));
    }

    private void UpdateAngle(Vector2 lastPos, Vector2 currentPos, Vector2 targetPos)
    {
        if (!(Time.deltaTime > 0f)) return;

        var num = Math.Clamp((currentPos.x - targetPos.x) / 0.8f, -1f, 1f);
        var baseAngle = num * 75f;

        var adjustedP = (1.9f + Mathf.Max(windP, speedP) * -2.4f); //low 1.9 <-> -0.5 high で係数を調整
        var angle = baseAngle * adjustedP;
        balloonRenderer.transform.localEulerAngles = new(0f, 0f, angle);
        highlightRenderer.transform.localEulerAngles = new(0f, 0f, -angle);
    }

    //糸先の速度
    private Vector2 lastPlayerPos = Vector2.zero;
    private Vector2 posSpeed = Vector2.zero;
    private float speedP = 0f;
    private float windP = 0f;
    public void SetVisible(bool visible)
    {
        holder.SetActive(visible);
        stringRenderer.gameObject.SetActive(visible);
    }

    public void Update()
    {
        bool lastActive = holder.active;

        if (target.IsDead || !target.VanillaPlayer.Visible)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        //手元の位置を更新する
        var yDiff = (target.VanillaPlayer.cosmetics.hat.SpriteSyncNode?.Parent.GetLocalPosition(0).y ?? 0f) * 0.7f;
        stringRenderer.transform.localPosition = ((Vector2)HandCenter + new Vector2(0f, yDiff));


        Vector2 lastPos = balloonRenderer.transform.position;
        var targetPos = CenterPos;
        var diff = lastPos - targetPos;
        var lastDistance = diff.magnitude;

        Vector2 currentPlayerPos = target.Position;
        var isMoving = lastPlayerPos.Distance(currentPlayerPos) > 0.005f;
        lastPlayerPos = currentPlayerPos;

        if (!lastActive || lastDistance > 3f) ResetToDefaultPos();
        else
        {

            //弾性力と移動への追従の割合, 0に近いほど弾性力の寄与が大きい
            speedP += (isMoving ? 4f : -2.8f) * Time.deltaTime;
            speedP = Mathf.Clamp01(speedP);

            var elastic = -diff * 10.8f * (1f - speedP); //弾性力
            var resistance = -posSpeed * 1.6f; //粘性抵抗
            
            var windType = MapData.GetCurrentMapData().GetWindType(currentPlayerPos);
            var wind = MapData.CalcWind(currentPlayerPos, windType, NebulaGameManager.Instance?.CurrentTime ?? 0f); //風が風船に及ぼす力

            windP += (wind.magnitude > 1.5f ? 4f : -0.5f) * Time.deltaTime;
            windP = Mathf.Clamp01(windP);

            posSpeed += (elastic + resistance + wind) * Time.deltaTime;
            if(speedP > 0.5f) posSpeed *= 0.95f; //弾性力の寄与が小さいとき、より早く速度が失われる。
            balloonRenderer.transform.position += (Vector3)posSpeed * Time.deltaTime;
            balloonRenderer.transform.position -= (Vector3)diff.Delta(4.4f, 0f) * speedP;

            //範囲外の位置を調整
            Vector2 currentPos = balloonRenderer.transform.position;
            Vector2 handPos = HandPos;
            Vector2 currentDiff = currentPos - handPos;
            float currentDistance = currentDiff.magnitude;

            if (currentDistance > StringRange.Max)
                balloonRenderer.transform.position -= (Vector3)currentDiff.normalized * (currentDistance - StringRange.Max);
            else if (currentDistance < StringRange.Min)
                balloonRenderer.transform.position += (Vector3)currentDiff.normalized * (StringRange.Min - currentDistance);
            

            balloonRenderer.transform.SetWorldZ(BalloonZ);
        }

        ReflectString();
        UpdateAngle(lastPos, balloonRenderer.transform.position, CenterPos);
    }

    public Vector2 BalloonPos => balloonRenderer.transform.position;
    public float Scale => balloonRenderer.transform.localScale.x;
    public void UpdateAlpha(float alpha)
    {
        balloonRenderer.color = Color.white.AlphaMultiplied(alpha);
        stringRenderer.color = Color.white.AlphaMultiplied(alpha);
        highlightRenderer.color = Color.white.AlphaMultiplied(alpha * 0.35f);
    }

    public void UpdateBallonColor()
    {
        PlayerMaterial.SetColors(target.CurrentOutfit.outfit.ColorId, balloonRenderer);
    }
}

[NebulaPreprocess(PreprocessPhase.PostRoles)]
internal class BalloonHolder : AbstractModule<GamePlayer>, IGameOperator, IBindPlayer
{
    public GamePlayer MyPlayer => MyContainer;
    static BalloonHolder() => DIManager.Instance.RegisterModule(() => new BalloonHolder());
    private BalloonHolder()
    {
        this.Register(NebulaAPI.CurrentGame!);
    }

    Balloon? balloon = null;
    void OnGameStart(GameStartEvent ev)
    {
        balloon = new Balloon(MyPlayer);
    }

    void OnUpdate(GameLateUpdateEvent ev)
    {
        balloon?.Update();
    }

    private static Vector2[] VisibilityCheckVectors = [new(-0.68f, 0.45f), new(0.68f, 0.45f), new(0f, 0f), new(0f, 0.9f)];

    [OnlyMyPlayer]
    void OnAlphaChanged(PlayerAlphaUpdateEvent ev)
    {
        if (balloon == null) return;

        if (MyPlayer.AmOwner)
        {
            balloon.UpdateAlpha(ev.AlphaIgnoresWall);
        }else
        {
            if(ev.AlphaIgnoresWall > ev.Alpha)
            {
                int objectMask = Constants.ShipAndAllObjectsMask;
                Vector2 cameraPos = GamePlayer.LocalPlayer!.Position;

                if (Helpers.AnyNonTriggersBetween(ev.Player.Position, balloon.BalloonPos, out _, objectMask))
                {
                    //風船所持者と風船の間に壁(影)がある場合
                    balloon.UpdateAlpha(ev.Alpha);
                }else if (VisibilityCheckVectors.Any(v => !Helpers.AnyNonTriggersBetween(cameraPos, balloon.BalloonPos + v * balloon.Scale, out _, objectMask)))
                {
                    //風船所持者と風船の間を隔てる壁がなく、風船とカメラの間に壁(影)がない場合
                    balloon.UpdateAlpha(ev.AlphaIgnoresWall);
                }
                else
                {
                    //少なくとも風船とカメラの間に壁(影)がある場合
                    balloon.UpdateAlpha(ev.Alpha);
                }
            }
            else
            {
                balloon.UpdateAlpha(ev.Alpha);
            }
        }
    }

    [OnlyMyPlayer]
    void OnOutfitChanged(PlayerOutfitChangeEvent ev)
    {
        balloon?.UpdateBallonColor();
    }
}

file static class SlingshotMinigameAssets
{
    static public Image MinigameBalloon = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameBalloon.png", 100f);
    static public Image MinigameBalloonBroken = SpriteLoader.FromResource("Nebula.Resources.Balloon.MinigameBalloonBroken.png", 100f);
}
internal class Slingshot : MonoBehaviour
{
    Transform StoneHolder;
    MeshFilter LeftMesh, LeftEdgeLowerMesh, LeftEdgeUpperMesh, RightMesh, RightEdgeLowerMesh, RightEdgeUpperMesh;

    public SpriteRenderer HandRenderer;

    static Slingshot() => ClassInjector.RegisterTypeInIl2Cpp<Slingshot>();

    private bool isDown = false;
    private float diff = 0f, holderVelocity = 0f;
    private Vector2 downPos;
    private Vector2 dir = Vector2.right.Rotate(-44f);
    private Vector2 dirNeg = Vector2.right.Rotate(-72f);
    private PassiveButton slingshotButton = null!;
    private float power = 0f;

    void Awake()
    {
        StoneHolder = transform.GetChild(2);

        HandRenderer = transform.GetChild(3).GetComponent<SpriteRenderer>();
        HandRenderer.material = HatManager.Instance.PlayerMaterial;
        PlayerMaterial.SetColors(GamePlayer.LocalPlayer!.PlayerId, HandRenderer);

        Color bandColor = new(235f / 255f, 197f / 255f, 118f / 255f);
        Color edgeColor = new(91f / 255f, 77f / 255f, 47f / 255f);
        (_, LeftMesh) = UnityHelper.CreateMeshRenderer("LeftBand", transform, new(0f, 0f, -0.28f), LayerExpansion.GetUILayer(), bandColor);
        (_, RightMesh) = UnityHelper.CreateMeshRenderer("RightBand", transform, new(0f, 0f, -0.08f), LayerExpansion.GetUILayer(), bandColor);
        (_, LeftEdgeLowerMesh) = UnityHelper.CreateMeshRenderer("LeftBandE1", transform, new(0f, 0f, -0.3f), LayerExpansion.GetUILayer(), edgeColor);
        (_, LeftEdgeUpperMesh) = UnityHelper.CreateMeshRenderer("LeftBandE2", transform, new(0f, 0f, -0.3f), LayerExpansion.GetUILayer(), edgeColor);
        (_, RightEdgeLowerMesh) = UnityHelper.CreateMeshRenderer("RightBandE1", transform, new(0f, 0f, -0.1f), LayerExpansion.GetUILayer(), edgeColor);
        (_, RightEdgeUpperMesh) = UnityHelper.CreateMeshRenderer("RightBandE2", transform, new(0f, 0f, -0.1f), LayerExpansion.GetUILayer(), edgeColor);
        LeftMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        LeftEdgeLowerMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        LeftEdgeUpperMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightEdgeLowerMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));
        RightEdgeUpperMesh.CreateRectMesh(new(1f, 1f), new(0.35f, 0f, 0f));

        var collider = UnityHelper.CreateObject<BoxCollider2D>("Collider", this.transform, new(0.2f, -0.5f, 0f));
        collider.size = new(1f, 1f);
        slingshotButton = collider.gameObject.SetUpButton(false);
        slingshotButton.OnDown = true;
        slingshotButton.OnUp = false;
        slingshotButton.OnClick.AddListener(() => {
            isDown = true;
            downPos = Input.mousePosition;
        });
    }

    void Update()
    {
        if (isDown)
        {
            holderVelocity = 0f;
            if (!Input.GetMouseButton(0))
            {
                //マウスを離したとき
                isDown = false;
                if (!(power < 1f))
                {
                    diff = -0.5f;
                }

                return;
            }
            else
            {
                var diffVec = (Vector2)Input.mousePosition - downPos;
                diff = Mathf.Clamp(Vector2.Dot(diffVec, dir) / 150f, 0f, 1.4f);
                var p = diff / 1.4f;

                if (p > 0.8f)
                {
                    power += (diff / 1.4f) * Time.deltaTime;
                    if (power > 1f) power = 1f;
                }
                else
                {
                    power -= 1.2f * Time.deltaTime;
                    if (power > 0f) power = 0f;
                }
            }
        }
        else
        {
            holderVelocity -= diff * 200f * Time.deltaTime;
            holderVelocity *= 0.86f;
            diff += holderVelocity * Time.deltaTime;
        }

        var z = StoneHolder.transform.localPosition.z;
        StoneHolder.transform.localPosition = ((diff < 0f ? dirNeg : dir) * diff + new Vector2(-0.3f, 0.5f)).AsVector3(z);

        UpdateMesh();
    }
    void UpdateMesh()
    {
        Vector2 leftEdgeStoneUpper = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.37f, -0.56f), transform);
        Vector2 leftEdgeStoneLower = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.35f, -0.76f), transform);
        Vector2 leftEdgeShotUpper = new Vector2(-0.44f, 0.44f);
        Vector2 leftEdgeShotLower = new Vector2(-0.44f, 0.25f);

        Vector2 rightEdgeStoneUpper = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.84f, -0.43f), transform);
        Vector2 rightEdgeStoneLower = StoneHolder.transform.TransformPointLocalToLocal(new Vector3(0.83f, -0.66f), transform);
        Vector2 rightEdgeShotUpper = new Vector2(0.45f, 0.15f);
        Vector2 rightEdgeShotLower = new Vector2(0.45f, 0.01f);

        Vector2 leftDir = leftEdgeShotUpper - leftEdgeStoneUpper;
        Vector2 leftNorm = new Vector2(leftDir.y, -leftDir.x).normalized;
        Vector2 rightDir = rightEdgeShotUpper - rightEdgeStoneUpper;
        Vector2 rightNorm = new Vector2(rightDir.y, -rightDir.x).normalized;

        LeftEdgeLowerMesh.mesh.SetVertices((Vector3[])[
            leftEdgeStoneLower + leftNorm * -0.015f,
            leftEdgeStoneLower + leftNorm * 0.015f,
            leftEdgeShotLower + leftNorm * -0.015f,
            leftEdgeShotLower + leftNorm * 0.015f
            ]);
        LeftEdgeUpperMesh.mesh.SetVertices((Vector3[])[
            leftEdgeStoneUpper + leftNorm * -0.015f,
            leftEdgeStoneUpper + leftNorm * 0.015f,
            leftEdgeShotUpper + leftNorm * -0.015f,
            leftEdgeShotUpper + leftNorm * 0.015f
            ]);
        RightEdgeLowerMesh.mesh.SetVertices((Vector3[])[
            rightEdgeStoneLower + rightNorm * -0.015f,
            rightEdgeStoneLower + rightNorm * 0.015f,
            rightEdgeShotLower + rightNorm * -0.015f,
            rightEdgeShotLower + rightNorm * 0.015f
            ]);
        RightEdgeUpperMesh.mesh.SetVertices((Vector3[])[
            rightEdgeStoneUpper + rightNorm * -0.015f,
            rightEdgeStoneUpper + rightNorm * 0.015f,
            rightEdgeShotUpper + rightNorm * -0.015f,
            rightEdgeShotUpper + rightNorm * 0.015f
            ]);

        if (leftEdgeShotLower.x < leftEdgeStoneLower.x)
        {
            LeftMesh.mesh.SetVertices((Vector3[])[
                leftEdgeStoneLower,
                leftEdgeStoneUpper,
                leftEdgeShotLower,
                leftEdgeShotUpper
            ]);
        }
        else
        {
            LeftMesh.mesh.SetVertices((Vector3[])[
                leftEdgeStoneUpper,
                leftEdgeStoneLower,
                leftEdgeShotUpper,
                leftEdgeShotLower
            ]);
        }

        if (rightEdgeShotLower.x < rightEdgeStoneLower.x)
        {
            RightMesh.mesh.SetVertices((Vector3[])[
                rightEdgeStoneLower,
                rightEdgeStoneUpper,
                rightEdgeShotLower,
                rightEdgeShotUpper
            ]);
        }
        else
        {
            RightMesh.mesh.SetVertices((Vector3[])[
                rightEdgeStoneUpper,
                rightEdgeStoneLower,
                rightEdgeShotUpper,
                rightEdgeShotLower
            ]);
        }
    }
}

internal class SlingshotMinigame : Minigame
{

    static SlingshotMinigame() => ClassInjector.RegisterTypeInIl2Cpp<SlingshotMinigame>();
    public SlingshotMinigame(System.IntPtr ptr) : base(ptr) { }
    public SlingshotMinigame() : base(ClassInjector.DerivedConstructorPointer<SlingshotMinigame>())
    { ClassInjector.DerivedConstructorBody(this); }

    public override void Begin(PlayerTask task)
    {
        //base.begin
        this.BeginInternal(task);
        //base.begin ここまで

        MetaScreen.InstantiateCloseButton(transform, new(-3.4f, 2f, -0.5f)).OnClick.AddListener(Close);

        var slingshot = GameObject.Instantiate(NebulaAsset.SlingshotInMinigame, transform).AddComponent<Slingshot>();
        slingshot.transform.localScale = new(1.2f, 1.2f, 1f);
        slingshot.transform.localPosition = new(0.4f, -0.8f, -2f);
        var balloon = UnityHelper.CreateObject<SpriteRenderer>("Balloon", transform, new(-1.3f, 1f, 0f));
        balloon.sprite = SlingshotMinigameAssets.MinigameBalloon.GetSprite();
        balloon.material = slingshot.HandRenderer.material;
        balloon.transform.localScale = new(1.3f, 1.3f, 1f);
    }

    public override void Close()
    {
        this.CloseInternal();
    }
}