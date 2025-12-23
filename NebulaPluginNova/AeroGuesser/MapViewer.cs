using Nebula.Behavior;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.AeroGuesser;

internal interface IMapCameraInteraction : IWithAnswerPhase
{
    float MapButtonQ { get; }
    float ShowWidelyQ { get; }
    float EndQuizQ { get; }
    bool MonoColor { get; }
}

internal class MapViewer : CustomCameraBehaviour
{
    IMapCameraInteraction interaction;
    IFunctionalValue<float> mapViewQ = Arithmetic.FloatZero;
    IFunctionalValue<float> mapOrthQ = Arithmetic.FloatZero;
    internal MapViewer(IMapCameraInteraction interaction) {
        this.interaction = interaction;
        SetUp();
    }

    private void SetUp()
    {
        var wideCam = NebulaGameManager.Instance?.WideCamera;
        wideCam?.SetCustomShadow(false, false, false);
        wideCam?.SetCustomBehaviour(this);
    }

    void CustomCameraBehaviour.OnSet(ICustomWideCamera camera)
    {
        camera.UseRectShader();
        camera.UpdateMesh(fitting.x, fitting.y);
        camera.UpdateRect(1f, 1f);
    }

    private EmptyBehaviour camObject = null!;
    private byte lastMapId = byte.MaxValue;
    private Vector2 inQuizFitting = new(3.5f, 1.7f);
    private Vector2 fitting = new(6.4f, 3.8f);

    /// <summary>
    /// マップを用意します。SetHideメソッドで表示状態を切り替えない限り表示されない点に注意してください。
    /// </summary>
    /// <param name="mapId"></param>
    /// <param name="position"></param>
    /// <param name="viewport"></param>
    /// <returns></returns>
    public IEnumerator SetUpMap(byte mapId, Vector2 position, Vector2 viewport)
    {
        void SetUpCamObject()
        {
            if (!camObject) camObject = UnityHelper.CreateObject<EmptyBehaviour>("CamTarget", null, Vector3.zero);
        }
        IEnumerator SetUpShip()
        {
            if (lastMapId == mapId) yield break;
            //Ship以降の全てのオブジェクトを消す
            if (ShipStatus.Instance)
            {
                GameObject.Destroy(ShipStatus.Instance.gameObject);
            }
            var loadHandle = AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
            yield return loadHandle;
            GameObject result = loadHandle.Result;
            var ship = result.GetComponent<ShipStatus>();
            ShipStatus.Instance = ship;
            ModifyShip(ship);
            NebulaGameManager.Instance?.WideCamera.ReflectShipColor();

            CalculateFittingParams(viewport.x, viewport.y);
        }

        void ModifyShip(ShipStatus ship)
        {
            if(mapId == 5)
            {
                //ファングルの色補正
                ship.transform.GetChild(6).GetChild(0).GetChild(13).gameObject.SetActive(false);
                ship.transform.GetChild(9).gameObject.SetActive(false);
            }
            if (mapId == 4)
            {
                //Airship

                //電気室のドアを全開放
                StaticDoor[] doors = ship.GetComponentInChildren<ElectricalDoors>().Doors;
                doors.Do(d => d.SetOpen(true));
            }
            if (mapId == 2)
            {
                //Polus

                //雪を撤去
                ship.transform.GetChild(0).gameObject.SetActive(false);
            }
        }

        void FixPosition()
        {
            camObject.transform.position = position;
            var cam = HudManager.Instance.PlayerCam;
            cam.Locked = true;
            cam.SetTarget(camObject);
        }

        SetUpCamObject();

        mapViewQ = mapOrthQ = Arithmetic.FloatZero;

        yield return SetUpShip();
        FixPosition();
    }

    public void StartShowAnswer()
    {
        mapViewQ = Arithmetic.Decel(0f, 1f, 1f);
        mapOrthQ = Arithmetic.Sequential((() => Arithmetic.FloatZero, 1.2f), (() => Arithmetic.Decel(0f, 1f, 1.2f), 1.2f));
    }

    private bool reshowing = false;
    public void SetReshowing(bool reshowing) => this.reshowing = reshowing;
    private bool hide = false;
    public void Hide() => hide = true;
    public void Show() => hide = false;
    void CustomCameraBehaviour.UpdateCamera(ICustomWideCamera camera, out Vector3 localPosition, out Vector2 localScale, out float localAngle)
    {
        localPosition = new(0f,(1f - interaction.AnswerQ) * (1f - interaction.ShowWidelyQ) * (0.8f + interaction.MapButtonQ * 0.5f) + (reshowing ? 0.6f : 0f), 0f - 10f); //HudManagerのzが-10fなので、分かりやすくするために揃える
        localScale = Vector2.one * (1f - interaction.EndQuizQ);
        localAngle = 0f;

        float mapViewQVal = mapViewQ.Value;

        if (hide)
            camera.UpdateRect(1f, 1f);
        else
            camera.UpdateRect(Mathn.Lerp(_cachedViewportX, 0f, mapViewQVal), Mathn.Lerp(_cachedViewportY, 0f, mapViewQVal));

        if (interaction.MonoColor) camera.SetSaturation(mapViewQVal);
    }

    float CustomCameraBehaviour.OrthographicSize => Mathn.Lerp(_cachedOrthographicSize, 2.9f, mapOrthQ.Value);

    float _cachedOrthographicSize = 3f;
    float _cachedViewportX = 1f;
    float _cachedViewportY = 1f;
    /// <summary>
    /// フィッティング領域に合わせてビューポートのパラメータを計算します。
    /// </summary>
    /// <param name="x">ビューポートの幅</param>
    /// <param name="y">ビューポートの高さ</param>
    public void CalculateFittingParams(float x, float y)
    {
        float fitAspect = inQuizFitting.x / inQuizFitting.y;
        float worldAspect = x / y;


        if (worldAspect > fitAspect)
        {
            _cachedOrthographicSize = x / (2f * fitAspect);

            _cachedViewportX = 0f;
            _cachedViewportY = 0.5f * (1f - (fitAspect / worldAspect));
        }
        else
        {
            _cachedOrthographicSize = y / 2f;

            _cachedViewportX = 0.5f * (1f - (worldAspect / fitAspect));
            _cachedViewportY = 0f;
        }

        _cachedOrthographicSize *= fitting.y / inQuizFitting.y;
        var inQuizRatioX = inQuizFitting.x / fitting.x;
        var inQuizRatioY = inQuizFitting.y / fitting.y;
        _cachedViewportX = _cachedViewportX * inQuizRatioX + (1f - inQuizRatioX) * 0.5f;
        _cachedViewportY = _cachedViewportY * inQuizRatioY + (1f - inQuizRatioY) * 0.5f;
    }
}
