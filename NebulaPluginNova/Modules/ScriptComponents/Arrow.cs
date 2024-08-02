using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;

public class Arrow : INebulaScriptComponent, IGameOperator
{
    private SpriteRenderer? arrowRenderer;
    private SpriteRenderer? smallRenderer = null;
    public Vector2 TargetPos;

    public GameObject ArrowObject => arrowRenderer!.gameObject;

    public bool IsAffectedByComms { get; set; } = false;
    public bool IsSmallenNearPlayer { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool FixedAngle { get; set; } = false;
    public bool IsDisappearing { get; set; } = false;
    public bool WithSmallArrow => smallRenderer != null;
    public bool OnJustPoint { get; set; } = false;
    public bool ShowOnlyOutside { get; set; } = false;

    private static SpriteLoader arrowSprite = SpriteLoader.FromResource("Nebula.Resources.Arrow.png", 185f);
    private static SpriteLoader arrowSmallSprite = SpriteLoader.FromResource("Nebula.Resources.ArrowSmall.png", 360f);

    public Arrow(Sprite? sprite = null, bool usePlayerMaterial = true, bool withSmallArrow = false)
    {
        arrowRenderer = UnityHelper.CreateObject<SpriteRenderer>("Arrow", HudManager.Instance.transform, new Vector3(0, 0, -10f), LayerExpansion.GetArrowLayer());
        arrowRenderer.sprite = sprite ?? arrowSprite.GetSprite();
        arrowRenderer.sharedMaterial = usePlayerMaterial ? HatManager.Instance.PlayerMaterial : HatManager.Instance.DefaultShader;
        if (usePlayerMaterial) SetColor(Color.white, Color.gray);
        if (withSmallArrow)
        {
            smallRenderer = UnityHelper.CreateObject<SpriteRenderer>("Arrow", arrowRenderer.transform, new Vector3(0, 0, -0.1f), LayerExpansion.GetArrowLayer());
            smallRenderer.sprite = arrowSmallSprite.GetSprite();
            FixedAngle = true;
        }
    }

    public void SetSprite(Sprite? sprite) => arrowRenderer!.sprite = sprite;

    public Arrow HideArrowSprite()
    {
        arrowRenderer!.sprite = null;
        return this;
    }

    public Arrow SetColorByOutfit(NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        return SetColor(Palette.PlayerColors[outfit.ColorId], Palette.ShadowColors[outfit.ColorId]);
    }

    public Arrow SetColor(Color mainColor, Color shadowColor)
    {
        arrowRenderer?.material.SetColor(PlayerMaterial.BackColor, shadowColor);
        arrowRenderer?.material.SetColor(PlayerMaterial.BodyColor, mainColor);
        return this;
    }

    public Arrow SetColor(Color mainColor) => SetColor(mainColor, mainColor * 0.65f);
    public Arrow SetSmallColor(Color smallColor)
    {
        if (smallRenderer) smallRenderer!.color = smallColor;
        return this;
    }
    void IGameOperator.OnReleased()
    {
        if (arrowRenderer) GameObject.Destroy(arrowRenderer!.gameObject);
        arrowRenderer = null;
    }

    private static float perc = 0.925f;
    void HudUpdate(GameHudUpdateEvent ev)
    {
        if (!arrowRenderer) return;

        //視点中心からのベクトル
        Camera main = UnityHelper.FindCamera(LayerExpansion.GetUILayer())!;

        //距離を測るための表示用のカメラ
        Camera worldCam = (NebulaGameManager.Instance?.WideCamera.IsShown ?? false) ? NebulaGameManager.Instance.WideCamera.Camera : Camera.main;

        //見た目上の矢印の位置のベクトル
        Vector2 vector = (TargetPos - (Vector2)main.transform.position) / (worldCam.orthographicSize / 3f);

        //目的地との見た目上の離れ具合
        float num = vector.magnitude / (main.orthographicSize * perc);

        //近くの矢印を隠す
        bool flag = IsActive && (!IsSmallenNearPlayer || (double)num > 0.3);
        arrowRenderer!.gameObject.SetActive(flag);
        if (!flag) return;

        bool Between(float value, float min, float max) => value > min && value < max;

        //スクリーン上の位置
        Vector2 viewportPoint = worldCam.WorldToViewportPoint(TargetPos);

        if (Between(viewportPoint.x, 0f, 1f) && Between(viewportPoint.y, 0f, 1f))
        {
            if (ShowOnlyOutside) arrowRenderer!.gameObject.SetActive(false);
            else
            {
                //画面内を指す矢印
                arrowRenderer.transform.localPosition = (vector - (OnJustPoint ? Vector2.zero : vector.normalized * (WithSmallArrow ? 0.9f : 0.6f))).AsVector3(2f);
                arrowRenderer.transform.localScale = IsSmallenNearPlayer ? Vector3.one * Mathf.Clamp(num, 0f, 1f) : Vector3.one;
            }
        }
        else
        {
            //画面外を指す矢印
            Vector2 vector3 = new Vector2(Mathf.Clamp(viewportPoint.x * 2f - 1f, -1f, 1f), Mathf.Clamp(viewportPoint.y * 2f - 1f, -1f, 1f));
            
            //UIのカメラに合わせて位置を調節する
            float orthographicSize = main.orthographicSize;
            float num3 = main.orthographicSize * main.aspect;
            Vector3 vector4 = new Vector3(Mathf.LerpUnclamped(0f, num3 * (WithSmallArrow ? 0.82f : 0.88f), vector3.x), Mathf.LerpUnclamped(0f, orthographicSize * (WithSmallArrow ? 0.72f : 0.79f), vector3.y), 2f);
            arrowRenderer.transform.localPosition = vector4;
            arrowRenderer.transform.localScale = Vector3.one;
        }

        vector.Normalize();

        if(FixedAngle)
            arrowRenderer.transform.eulerAngles = new Vector3(0f, 0f, 0f);
        else
            arrowRenderer.transform.eulerAngles = new Vector3(0f, 0f, Mathf.Atan2(vector.y, vector.x) * 180f / Mathf.PI);

        if(smallRenderer != null)
        {
            if (FixedAngle)
            {
                var angle = Mathf.Atan2(vector.y, vector.x) * 180f / Mathf.PI;
                smallRenderer.transform.localPosition = Vector3.right.RotateZ(angle) * 0.45f;
                smallRenderer.transform.eulerAngles = new Vector3(0f, 0f, angle);
            }
            else
            {
                smallRenderer.transform.localPosition = Vector3.right * 0.45f;
            }
        }

        if (IsDisappearing)
        {
            float a = arrowRenderer.color.a;
            a -= Time.deltaTime * 0.85f;
            if (a < 0f)
            {
                this.ReleaseIt();
                a = 0f;
            }
            arrowRenderer.color = new Color(arrowRenderer.color.r, arrowRenderer.color.g, arrowRenderer.color.b, a);
        }
    }

    public void MarkAsDisappering()
    {
        IsDisappearing = true;
    }

    public IEnumerator CoWaitAndDisappear(float waiting)
    {
        yield return new WaitForSeconds(waiting);
        IsDisappearing = true;
    }
}
