using Nebula.Modules.Cosmetics;
using Virial;
using Virial.Events.Game;
using Virial.Game;

namespace Nebula.Modules.ScriptComponents;

public class Arrow : FlexibleLifespan, IGameOperator
{
    public enum DisappearanceType
    {
        FadeOut,
        Reduction,
    }

    private SpriteRenderer? arrowRenderer;
    private SpriteRenderer? smallRenderer = null;
    private SpriteRenderer? subRenderer = null;
    public Vector2 TargetPos;

    public GameObject ArrowObject => arrowRenderer!.gameObject;

    public bool IsAffectedByComms { get; set; } = false;
    public bool IsSmallenNearPlayer { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public bool FixedAngle { get; set; } = false;
    public bool IsDisappearing { get; set; } = false;
    public DisappearanceType DisappearanceEffect { get; set; } = DisappearanceType.FadeOut;
    public bool WithSmallArrow => smallRenderer != null;
    public bool OnJustPoint { get; set; } = false;
    private bool showOnlyOutside = false;
    public bool ShowOnlyOutside { get => showOnlyOutside; set
        {
            showOnlyOutside = value;
            if (!showOnlyOutside) arrowRenderer?.gameObject.SetActive(false);
        }
    }
    private float disappearProgress = 0f;
    private static SpriteLoader arrowSprite = SpriteLoader.FromResource("Nebula.Resources.Arrow.png", 185f);
    private static SpriteLoader arrowSmallSprite = SpriteLoader.FromResource("Nebula.Resources.ArrowSmall.png", 360f);

    public Arrow(Sprite? sprite = null, bool usePlayerMaterial = true, bool withSmallArrow = false, bool withSubRenderer = false)
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
        if (withSubRenderer)
        {
            subRenderer = UnityHelper.CreateObject<SpriteRenderer>("Subrenderer", arrowRenderer.transform, new Vector3(0, 0, 0.01f), LayerExpansion.GetArrowLayer());
            subRenderer.sprite = null;
        }

        if (ShowOnlyOutside) arrowRenderer.gameObject.SetActive(false);
    }

    public void SetSprite(Sprite? sprite) => arrowRenderer!.sprite = sprite;
    public void SetSubSprite(Sprite? sprite, float z)
    {
        if(subRenderer != null)
        {
            subRenderer.sprite = sprite;
            subRenderer.transform.localPosition = new(0f,0f,z);
        }
    }

    public Arrow HideArrowSprite()
    {
        arrowRenderer!.sprite = null;
        return this;
    }

    public Arrow SetColorByOutfit(NetworkedPlayerInfo.PlayerOutfit outfit)
    {
        return SetColor(DynamicPalette.PlayerColors[outfit.ColorId], DynamicPalette.ShadowColors[outfit.ColorId]);
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

        //表示するカメラ
        Camera main = NebulaGameManager.Instance?.WideCamera.Camera ?? UnityHelper.FindCamera(LayerExpansion.GetUILayer())!;

        //距離を測るための表示用のカメラ
        Camera worldCam = (NebulaGameManager.Instance?.WideCamera.IsShown ?? false) ? NebulaGameManager.Instance.WideCamera.Camera : Camera.main;

        Vector2 del = (TargetPos - (Vector2)main.transform.position);

        //目的地との見た目上の離れ具合
        float num = del.magnitude / (worldCam.orthographicSize * perc);

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
                arrowRenderer.transform.localPosition = (del - (OnJustPoint ? Vector2.zero : del.normalized * (WithSmallArrow ? 0.9f : 0.6f) * (worldCam.orthographicSize / Camera.main.orthographicSize))).AsVector3(2f);
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

        arrowRenderer.transform.localScale *= (worldCam.orthographicSize / Camera.main.orthographicSize);


        //角度の計算のために正規化する(しなくてもいいのかも)
        del.Normalize();

        if(FixedAngle)
            arrowRenderer.transform.eulerAngles = new Vector3(0f, 0f, 0f);
        else
            arrowRenderer.transform.eulerAngles = new Vector3(0f, 0f, Mathf.Atan2(del.y, del.x) * 180f / Mathf.PI);

        if(smallRenderer != null)
        {
            if (FixedAngle)
            {
                var angle = Mathf.Atan2(del.y, del.x) * 180f / Mathf.PI;
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
            float a = 1f - disappearProgress;

            if (DisappearanceEffect == DisappearanceType.FadeOut)
            {
                disappearProgress += Time.deltaTime * 0.85f;

                arrowRenderer.color = new Color(arrowRenderer.color.r, arrowRenderer.color.g, arrowRenderer.color.b, a);
                if (smallRenderer != null) smallRenderer.color = new(1f, 1f, 1f, a);
            }else if(DisappearanceEffect == DisappearanceType.Reduction)
            {
                disappearProgress += Time.deltaTime * 3.2f;

                arrowRenderer.transform.localScale *= a;
            }

            if (disappearProgress > 1f)
            {
                this.Release();
                disappearProgress = 1f;
            }
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
