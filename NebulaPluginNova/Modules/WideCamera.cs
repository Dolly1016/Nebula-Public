using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules;

public class WideCamera
{
    private Camera myCamera;
    //myCameraがアクティブかどうかではなく、カメラを有効化されているか否か
    private bool isActive = false;
    private float targetRate = 3f;

    public bool IsShown => myCamera.gameObject.active;
    public bool IsActivated => isActive;
    public float TargetRate { get => targetRate; set => targetRate = value; }
    public Camera Camera => myCamera;
    public event Action? OnInactivated;
    public WideCamera()
    {
        myCamera = UnityHelper.CreateObject<Camera>("SubCam", HudManager.Instance.transform.parent, Vector3.zero);
        myCamera.backgroundColor = Color.black;
        myCamera.allowHDR = false;
        myCamera.allowMSAA = false;
        myCamera.clearFlags = CameraClearFlags.SolidColor;
        myCamera.cullingMask = 31511;
        myCamera.depth = 5;
        myCamera.nearClipPlane = -1000f;
        myCamera.orthographic = true;
        myCamera.orthographicSize = 3;

        var collider = UnityHelper.CreateObject<BoxCollider2D>("ClickGuard", myCamera.transform, new(0f, 0f, -4.5f));
        collider.size = new(100f, 100f);
        collider.isTrigger = true;
        collider.gameObject.SetUpButton();

        myCamera.gameObject.SetActive(false);
    }

    public void SetDrawShadow(bool drawShadow)
    {
        myCamera.cullingMask = drawShadow ? 97047 : 31511;
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

        if (immediately) myCamera.gameObject.SetActive(false);
        
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
    }
    public void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetDrawShadow(true);
            ToggleCamera();
            AmongUsUtil.ChangeShadowSize(IsActivated ? 3f * TargetRate : 3f);
        }
        */

        if (myCamera.gameObject.active) {
            float currentOrth = myCamera.orthographicSize;
            float targetOrth = (isActive ? targetRate : 1f) * 3f;
            float diff = currentOrth - targetOrth;
            bool reached = Mathf.Abs(diff) < 0.001f;

            if (reached)
                currentOrth = targetOrth;
            else
                currentOrth -= (currentOrth - targetOrth) * Time.deltaTime * (isActive ? 5f : 10f);

            if (reached && !isActive)
            {
                myCamera.gameObject.SetActive(false);
                OnInactivated?.Invoke();
                OnInactivated = null;
            }
            else
                myCamera.orthographicSize = currentOrth;
        }
    }


}
