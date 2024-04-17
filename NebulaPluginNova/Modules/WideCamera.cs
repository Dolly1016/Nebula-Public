using Virial.Game;

namespace Nebula.Modules;

public class WideCamera
{
    private Camera myCamera;
    //myCameraがアクティブかどうかではなく、カメラを有効化されているか否か
    private bool isActive = false;
    private float targetRate = 1f; // エフェクト効果に依らない目標拡大率 Wideカメラを有効にしている時のみ掛け合わせられる。

    public bool IsShown => myCamera.gameObject.active;
    public bool IsActivated => isActive;
    public float TargetRate { get => targetRate; set => targetRate = value; }
    public Camera Camera => myCamera;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private float meshAngleZ = 0f;

    public Transform ViewerTransform => meshRenderer.transform;

    public WideCamera()
    {
        myCamera = UnityHelper.CreateObject<Camera>("SubCam", HudManager.Instance.transform.parent, Vector3.zero);
        myCamera.backgroundColor = Color.black;
        myCamera.allowHDR = false;
        myCamera.allowMSAA = false;
        myCamera.clearFlags = CameraClearFlags.SolidColor;
        myCamera.depth = 5;
        myCamera.nearClipPlane = -1000f;
        myCamera.orthographic = true;
        myCamera.orthographicSize = 3;
        SetDrawShadow(true);

        var blackCam = UnityHelper.CreateObject<Camera>("BlackCam", myCamera.transform, Vector3.zero);
        blackCam.backgroundColor = Color.black;
        blackCam.allowHDR = false;
        blackCam.allowMSAA = false;
        blackCam.clearFlags = CameraClearFlags.SolidColor;
        blackCam.cullingMask = 0;
        blackCam.depth = 4;
        blackCam.nearClipPlane = -1000f;
        blackCam.orthographic = true;
        blackCam.orthographicSize = 3;

        var collider = UnityHelper.CreateObject<BoxCollider2D>("ClickGuard", myCamera.transform, new(0f, 0f, -1f));
        collider.size = new(100f, 100f);
        collider.isTrigger = true;
        collider.gameObject.SetUpButton();

        myCamera.gameObject.SetActive(false);

        (meshRenderer, meshFilter) = UnityHelper.CreateMeshRenderer("mesh", myCamera.transform, Vector3.zero, LayerExpansion.GetUILayer());
    }

    private bool drawShadow = false;
    public void SetDrawShadow(bool drawShadow)
    {
        myCamera.cullingMask = drawShadow ? 97047 : 31511;
        myCamera.cullingMask |= 1 << 29;

        this.drawShadow = drawShadow;
    }

    public void Activate()
    {
        if (isActive) return;

        isActive = true;

        if (!myCamera.gameObject.active)
        {
            myCamera.orthographicSize = 3f;
            myCamera.gameObject.SetActive(true);
        }
    }

    public void Inactivate(bool immediately = false)
    {
        if (!isActive) return;

        isActive = false;
    }

    public void ToggleCamera()
    {
        if (isActive)
            Inactivate(false);
        else
            Activate();
    }

    public void OnGameStart()
    {
        myCamera.backgroundColor = ShipStatus.Instance.CameraColor;
        myCamera.gameObject.SetActive(true);

        //Roughness = 20;
        Roughness = 1;
    }

    private static int gcd(int n1, int n2)
    {
        static int gcdInner(int _n1, int _n2) => _n2 == 0 ? _n1 : gcdInner(_n2, _n1 % _n2);
        return n1 > n2 ? gcdInner(n1, n2) : gcdInner(n2, n1);
    }
    
    private int roughness = 1;
    private int lastCommandRoughness = 1;
    public int Roughness { get => roughness; set
        {

            int max = gcd(Screen.height, Screen.width);
            if (max < value) roughness = value;

            int temp = value;
            while (temp < max && (Screen.height % temp != 0 || Screen.width % temp != 0)) temp++;
            roughness = temp;
        }
    } 

    private int consideredWidth => (Screen.width / roughness);
    private int consideredHeight => (Screen.height / roughness);

    private void CheckPlayerState(out Vector3 localScale, out float localRotateZ)
    {
        localScale = new(1f, 1f, 1f);

        var p = NebulaGameManager.Instance?.LocalPlayerInfo;
        if (p == null)
        {
            localRotateZ = 0f;
            return;
        }

        if (p.CountAttribute(PlayerAttributes.FlipX) % 2 == 1) localScale.x = -1f;
        if (p.CountAttribute(PlayerAttributes.FlipY) % 2 == 1) localScale.y = -1f;
        localRotateZ = 180f * p.Unbox().CountAttribute(PlayerAttributes.FlipXY);
    }



    public void Update()
    {
        if (myCamera.gameObject.active) {
            
            if(!myCamera.targetTexture || myCamera.targetTexture.width != consideredWidth || myCamera.targetTexture.height != consideredHeight)
            {
                //割り切れないときは再設定
                if(Screen.width % roughness != 0 || Screen.height % roughness != 0) Roughness = roughness;

                if(myCamera.targetTexture) GameObject.Destroy(myCamera.targetTexture);

                myCamera.targetTexture = new RenderTexture(consideredWidth, consideredHeight, 32, RenderTextureFormat.ARGB32);
                meshRenderer.material.mainTexture = myCamera.targetTexture;

                meshFilter.CreateRectMesh(new(Camera.main.orthographicSize / Screen.height * Screen.width, Camera.main.orthographicSize), Color.white);
            }

            CheckPlayerState(out var goalScale, out var goalRotate);
            while (meshAngleZ - goalRotate > 360f) meshAngleZ -= 360f;
            while (meshAngleZ - goalRotate < -360f) meshAngleZ += 360f;
            meshAngleZ -= (meshAngleZ - goalRotate).Delta(2.2f, 0.5f);

            var meshTransform = meshRenderer.transform;
            meshTransform.localScale -= (meshTransform.localScale - goalScale).Delta(2.2f, 0.005f);
            meshTransform.localEulerAngles = new(0f, 0f, meshAngleZ);

            float targetRateByEffect = NebulaGameManager.Instance?.LocalPlayerInfo?.CalcAttributeVal(PlayerAttributes.ScreenSize, true) ?? 1f;

            float currentOrth = myCamera.orthographicSize;
            float targetOrth = (isActive ? targetRate : 1f) * targetRateByEffect * 3f;
            float diff = currentOrth - targetOrth;
            bool reached = Mathf.Abs(diff) < 0.001f;

            if (reached)
                currentOrth = targetOrth;
            else
                currentOrth -= (currentOrth - targetOrth) * Time.deltaTime * 5f;

            if (reached && !isActive)
            {
                //
            }
            else
                myCamera.orthographicSize = currentOrth;

            if (drawShadow)
            {
                float currentShadow = AmongUsUtil.GetShadowSize();
                
                if (targetOrth > currentShadow)
                    AmongUsUtil.ChangeShadowSize(targetOrth);
                else if((reached && currentShadow > currentOrth) || currentShadow > currentOrth * 1.5f)
                    AmongUsUtil.ChangeShadowSize(currentOrth);
            }

            //コマンドによるモザイクの設定値に変化が生じたら再計算する
            int currentCommandRoughness =  Mathf.Max(1, (int?)NebulaGameManager.Instance?.LocalPlayerInfo?.CalcAttributeVal(PlayerAttributes.Roughening, true) ?? 1);
            if(lastCommandRoughness != currentCommandRoughness)
            {
                lastCommandRoughness = currentCommandRoughness;
                Roughness = lastCommandRoughness;
            }
        }
    }


}
