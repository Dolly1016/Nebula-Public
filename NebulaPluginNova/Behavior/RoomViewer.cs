using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Assignable;
using Virial.Game;
using Virial.Media;
using Virial.Text;
using Virial;
using UnityEngine.Rendering;
using UnityEngine;
using JetBrains.Annotations;

namespace Nebula.Behavior;

internal class RoomViewer : MonoBehaviour
{
    static RoomViewer() => ClassInjector.RegisterTypeInIl2Cpp<RoomViewer>();
    private MetaScreen myScreen = null!;

    protected void Close() => MainMenuManagerInstance.Close(this);

    static public void Open(MainMenuManager mainMenu) => MainMenuManagerInstance.Open<RoomViewer>("RoomViewer", mainMenu, viewer => viewer.OnShown());

    private LineRenderer Line = null!;
    private SpriteRenderer Dot = null!;
    private List<Vector3> LinePositions = null!;
    private GameObject RoomHolder = null!;
    private SpriteRenderer RoomRenderer = null!;
    private SpriteRenderer RoomMaskedRenderer = null!;
    private OpenableDoor Door = null!, VertDoor = null!;
    private Transform VertDoorTransform = null!;
    private SpriteMask DoorMask = null!;
    private SpriteMask VertDoorMask = null!;
    private bool VertLeft = false;
    private bool HortUpper= false;

    public void OnShown()
    {
        transform.GetChild(0).GetComponent<SpriteRenderer>().color = new(0.4f, 0.4f, 0.6f, 1f);
        var gui = NebulaAPI.GUI;

        var roomSprite = SpriteLoader.FromResource("Nebula.Resources.Map.Room1.png", 100f);
        var roomMaskSprite = SpriteLoader.FromResource("Nebula.Resources.Map.Room1DoorMask.png", 100f);

        gameObject.SetActive(true);
        //myScreen.SetWidget(new Modules.GUIWidget.VerticalWidgetsHolder(Virial.Media.GUIAlignment.Left, title, GenerateWidget(), GUI.API.VerticalMargin(0.15f), caption) { FixedWidth = 9f }, out _);

        RoomHolder = UnityHelper.CreateObject("Holder", transform, Vector2.zero.AsVector3(-30f));

        RoomRenderer = UnityHelper.CreateObject<SpriteRenderer>("background", RoomHolder.transform, new(0f, 0f, 3f));
        RoomRenderer.sprite = roomSprite.GetSprite();

        var doorMaskGroup = UnityHelper.CreateObject<SortingGroup>("doorMaskGroup", RoomHolder.transform, Vector3.zero);
        RoomMaskedRenderer = UnityHelper.CreateObject<SpriteRenderer>("masked", doorMaskGroup.transform, new(0f, 0f, 2f));
        RoomMaskedRenderer.sprite = roomMaskSprite.GetSprite();
        RoomMaskedRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        Door = GameObject.Instantiate(VanillaAsset.MapAsset[4].AllDoors[2], RoomHolder.transform);
        Door.transform.localPosition = new(-1f, 0f, -1f);
        Door.transform.localScale = new(0.7f, 0.7f, 1f);

        var vertGroup = UnityHelper.CreateObject<SortingGroup>("VertMaskGroup", RoomHolder.transform, new(1f, 0f, -1f));
        VertDoor = GameObject.Instantiate(VanillaAsset.MapAsset[4].AllDoors[1], vertGroup.transform);
        VertDoor.transform.localPosition = Vector3.zero;
        VertDoor.transform.localScale = new(0.7f, 0.7f, 1f);
        VertDoor.GetComponent<SpriteRenderer>().maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        VertDoorTransform = vertGroup.transform;
        var vertMask = UnityHelper.CreateObject<SpriteMask>("Mask", vertGroup.transform, new(0f, 0.008f, 0f));
        vertMask.transform.localScale = new(5f, 1.58f, 1f);
        vertMask.sprite = VanillaAsset.FullScreenSprite;


        DoorMask = UnityHelper.CreateObject<SpriteMask>("Mask", Door.transform, new(0f, 0.1f, 0f));
        DoorMask.sprite = SpriteLoader.FromResource("Nebula.Resources.Map.AirshipDoorNonvertMask.png", 100f).GetSprite();
        DoorMask.transform.localScale = new(1f, 1.2f, 1f); //下方も同時にカバーする (1fで下方のドアのための配置になる)
        DoorMask.transform.SetParent(doorMaskGroup.transform);

        VertDoorMask = UnityHelper.CreateObject<SpriteMask>("Mask", VertDoor.transform, Vector3.zero);
        VertDoorMask.sprite = VanillaAsset.FullScreenSprite;
        VertDoorMask.transform.SetParent(doorMaskGroup.transform);
        VertDoorMask.transform.localScale = new(0.4f, 0.83f, 1f);

        Line = UnityHelper.SetUpLineRenderer("Line", RoomHolder.transform, new(0f, 0f, -10f), width: 0.05f);
        LinePositions = [];
        Line.SetPositions(LinePositions.ToArray());
        Line.SetColors(Color.white, Color.white);
        Dot = UnityHelper.CreateObject<SpriteRenderer>("Dot", RoomHolder.transform, Vector3.zero);
        Dot.sprite = VanillaAsset.FullScreenSprite;
        Dot.transform.localScale = new(0.1f,0.1f,1f);


    }

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => Close());
        myScreen.SetBorder(new(9f, 5.5f));
    }

    public void Update()
    {
        if (DoorMask == null || Door == null) return;

        Transform targetDoor = Input.GetKey(KeyCode.LeftShift) ? VertDoorTransform : Door.transform;
        if (Input.GetKey(KeyCode.DownArrow)) targetDoor.localPosition += new Vector3(0f, -0.01f, 0f);
        if (Input.GetKey(KeyCode.UpArrow)) targetDoor.localPosition += new Vector3(0f, 0.01f, 0f);
        if (Input.GetKey(KeyCode.LeftArrow)) targetDoor.localPosition += new Vector3(-0.01f, 0f, 0f);
        if (Input.GetKey(KeyCode.RightArrow)) targetDoor.localPosition += new Vector3(0.01f, 0f, 0f);

        if (Input.GetKey(KeyCode.S)) RoomHolder.transform.localPosition += new Vector3(0f, 0.02f, 0f);
        if (Input.GetKey(KeyCode.W)) RoomHolder.transform.localPosition += new Vector3(0f, -0.02f, 0f);
        if (Input.GetKey(KeyCode.A)) RoomHolder.transform.localPosition += new Vector3(0.02f, 0f, 0f);
        if (Input.GetKey(KeyCode.D)) RoomHolder.transform.localPosition += new Vector3(-0.02f, 0f, 0f);

        if (Input.GetKey(KeyCode.Q)) RoomHolder.transform.localScale += new Vector3(0.02f, 0.02f);
        if (Input.GetKey(KeyCode.E)) RoomHolder.transform.localScale -= new Vector3(0.02f, 0.02f);

        if (Input.GetKeyDown(KeyCode.X))
        {
            var text = LinePositions.Select(pos => "(" + pos.x.ToString("F2") + "," + pos.y.ToString("F2") + ")").Join(null, ",");
            Debug.Log("Points: " + text);
            ClipboardHelper.PutClipboardString(text);
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            var text = targetDoor.transform.localPosition.x.ToString("F2") + ", " + targetDoor.transform.localPosition.y.ToString("F2");
            Debug.Log("Door: " + text);
            ClipboardHelper.PutClipboardString(text);
        }

        if (Input.GetKeyDown(KeyCode.LeftControl)) VertLeft = !VertLeft;
        
        DoorMask.transform.localPosition = Door.transform.localPosition + new Vector3(0f, 0.07f, 0f);
        VertDoorMask.transform.localPosition = VertDoorTransform.transform.localPosition + new Vector3(VertLeft ? -0.1f : 0.1f, -0.35f, 0f);

        if (Input.GetMouseButtonDown(0))
        {
            var localPos = RoomHolder.transform.InverseTransformPoint(Camera.main.ScreenToWorldPoint(Input.mousePosition));
            localPos.z = 0f;
            LinePositions.Add(localPos);
            Line.positionCount = LinePositions.Count;
            Line.SetPositions(LinePositions.ToArray());

            localPos.z = -40f;
            Dot.transform.localPosition = localPos;
        }
        if (Input.GetMouseButtonDown(1) && LinePositions.Count > 0)
        {
            LinePositions.RemoveAt(LinePositions.Count - 1);
            Line.positionCount = LinePositions.Count;
            Line.SetPositions(LinePositions.ToArray());

            if(Line.positionCount > 0)
            {
                var pos = LinePositions.Last();
                pos.z = -40f;
                Dot.transform.localPosition = pos;
            }
            else
            {
                Dot.transform.localPosition = new(0f,0f,3f);
            }
        }
    }
}

