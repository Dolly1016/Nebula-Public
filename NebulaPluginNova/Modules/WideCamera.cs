using Rewired.Utils.Platforms.Windows;
using Virial.Game;

namespace Nebula.Modules;

public interface INoisedCamera
{
    int CameraRoughness { get; }
}

public class WideCamera
{
    private Camera myCamera;

    private float targetRate = 1f; // エフェクト効果に依らない目標拡大率 Wideカメラを有効にしている時のみ掛け合わせられる。

    public bool IsShown => myCamera.gameObject.active;
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
        var customIgnoreShadow = myCamera.gameObject.AddComponent<CustomIgnoreShadowCamera>();
        customIgnoreShadow.IgnoreShadow = () => !DrawShadow;
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
        collider.gameObject.layer = LayerExpansion.GetShipLayer();
        var button = collider.gameObject.SetUpButton();
        button.OnClick.AddListener(() => {
            var cameraPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var worldPos = ConvertToWorldPos(cameraPos);
            int layer = (1 << LayerExpansion.GetShortObjectsLayer()) | (1 << LayerExpansion.GetObjectsLayer());

            PassiveUiElement? passiveButton = null;
            foreach (var button in PassiveButtonManager.Instance.Buttons)
            {
                //船およびオブジェクトレイヤーのボタンが対象
                if (((1 << button.gameObject.layer) & layer) == 0) continue;
                if (!button.Colliders.Any(c => c.OverlapPoint(worldPos))) continue;
                if (passiveButton != null && passiveButton.transform.position.z < button.transform.position.z) continue;
                
                //Debug.Log("Button");
                passiveButton = button;
            }

            if (passiveButton != null) passiveButton.ReceiveClickDown();
        });

        myCamera.gameObject.SetActive(false);

        (meshRenderer, meshFilter) = UnityHelper.CreateMeshRenderer("mesh", myCamera.transform, Vector3.zero, LayerExpansion.GetUILayer());
    }

    public bool DrawShadow => drawShadow && !(NebulaGameManager.Instance?.IgnoreWalls ?? false);
    private bool drawShadow = false;
    public void SetDrawShadow(bool drawShadow)
    {
        myCamera.cullingMask = drawShadow ? 97047 : 31511;
        myCamera.cullingMask |= 1 << 29;

        this.drawShadow = drawShadow;
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
    public int Roughness { get => roughness * (int)((AmongUsUtil.CurrentCamTarget as INoisedCamera)?.CameraRoughness ?? 1f); set
        {

            int max = gcd(Screen.height, Screen.width);
            if (max < value) roughness = value;

            int temp = value;
            while (temp < max && (Screen.height % temp != 0 || Screen.width % temp != 0)) temp++;
            roughness = temp;
        }
    } 

    private int consideredWidth => (Screen.width / Roughness);
    private int consideredHeight => (Screen.height / Roughness);

    public void CheckPlayerState(out Vector3 localScale, out float localRotateZ)
    {
        localScale = new(1f, 1f, 1f);

        var p = NebulaGameManager.Instance?.LocalPlayerInfo;
        if (p == null)
        {
            localRotateZ = 0f;
            return;
        }

        if (p.Unbox().CountAttribute(PlayerAttributes.FlipX) % 2 == 1) localScale.x = -1f;
        if (p.Unbox().CountAttribute(PlayerAttributes.FlipY) % 2 == 1) localScale.y = -1f;
        localRotateZ = 180f * p.Unbox().CountAttribute(PlayerAttributes.FlipXY);
    }

    //カメラ上の位置を表すワールド座標を計算します。
    public Vector3 ConvertToWideCameraPos(Vector3 worldPosition)
    {
        var localPos = (worldPosition - Camera.transform.position);
        localPos.x *= ViewerTransform.localScale.x;
        localPos.y *= ViewerTransform.localScale.y;
        return Camera.transform.position + localPos.RotateZ(ViewerTransform.localEulerAngles.z);
    }

    public Vector2 ConvertToWorldPos(Vector2 cameraWorldPosition)
    {
        var localPos = cameraWorldPosition - (Vector2)Camera.transform.position;
        localPos = localPos.Rotate(-ViewerTransform.localEulerAngles.z);
        try
        {
            localPos.x /= ViewerTransform.localScale.x;
        }
        catch
        {
            localPos.x = 0f;
        }
        try
        {
            localPos.y /= ViewerTransform.localScale.y;
        }
        catch
        {
            localPos.y = 0f;
        }
        return (Vector2)Camera.transform.position + localPos;
    }

    private void FixVentArrow()
    {
        if (PlayerControl.LocalPlayer && ShipStatus.Instance)
        {
            var playerPos = PlayerControl.LocalPlayer.transform.position;
            var vent = ShipStatus.Instance.AllVents.MinBy(v => v.transform.position.Distance(playerPos));
            if (vent)
            {
                var myVentPos = NebulaGameManager.Instance!.WideCamera.ConvertToWideCameraPos(vent!.transform.position);

                int length = vent.NearbyVents.Length;
                for (int i = 0; i < length; i++)
                {
                    var targetVent = vent.NearbyVents[i];
                    if (targetVent)
                    {
                        var targetVentPos = NebulaGameManager.Instance!.WideCamera.ConvertToWideCameraPos(targetVent.transform.position);

                        var diff = (targetVentPos - myVentPos).normalized;
                        diff *= 0.7f + vent.spreadShift;
                        var pos = (myVentPos + diff);
                        pos.z = -10f;
                        var transform = vent.Buttons[i].transform;
                        transform.position = pos;
                        transform.localEulerAngles = new(0f, 0f, Mathf.Atan2(diff.y, diff.x) / Mathf.PI * 180f);

                    }
                }
            }
        }
    }

    public void LateUpdate()
    {
        if (myCamera.gameObject.active)
        {
            FixVentArrow();
        }
    }

    public void Update()
    {
        if (myCamera.gameObject.active) {

            if (!myCamera.targetTexture || myCamera.targetTexture.width != consideredWidth || myCamera.targetTexture.height != consideredHeight)
            {
                //割り切れないときは再設定
                if(Screen.width % roughness != 0 || Screen.height % roughness != 0) Roughness = roughness;

                meshRenderer.sharedMaterial.mainTexture = myCamera.SetCameraRenderTexture(consideredWidth, consideredHeight);

                meshFilter.CreateRectMesh(new(Camera.main.orthographicSize / Screen.height * Screen.width * 2f, Camera.main.orthographicSize * 2f));
            }

            CheckPlayerState(out var goalScale, out var goalRotate);
            while (meshAngleZ - goalRotate > 360f) meshAngleZ -= 360f;
            while (meshAngleZ - goalRotate < -360f) meshAngleZ += 360f;
            meshAngleZ -= (meshAngleZ - goalRotate).Delta(2.7f, 0.11f);

            var meshTransform = meshRenderer.transform;
            meshTransform.localScale -= (meshTransform.localScale - goalScale).Delta(2.4f, 0.003f);
            meshTransform.localEulerAngles = new(0f, 0f, meshAngleZ);

            float targetRateByEffect = NebulaGameManager.Instance?.LocalPlayerInfo?.Unbox().CalcAttributeVal(PlayerAttributes.ScreenSize, true) ?? 1f;

            float currentOrth = myCamera.orthographicSize;
            float targetOrth = targetRate * targetRateByEffect * 3f;
            float diff = currentOrth - targetOrth;
            bool reached = Mathf.Abs(diff) < 0.001f;

            if (reached)
                currentOrth = targetOrth;
            else
                currentOrth -= (currentOrth - targetOrth) * Time.deltaTime * 5f;

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
            int currentCommandRoughness =  Mathf.Max(1, (int?)NebulaGameManager.Instance?.LocalPlayerInfo?.Unbox().CalcAttributeVal(PlayerAttributes.Roughening, true) ?? 1);
            if(lastCommandRoughness != currentCommandRoughness)
            {
                lastCommandRoughness = currentCommandRoughness;
                Roughness = lastCommandRoughness;
            }
        }
    }


}
