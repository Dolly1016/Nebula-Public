using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem.Runtime.Remoting.Messaging;
using JetBrains.Annotations;
using Nebula.Behavior;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.PlayerLoop;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Events.Game;
using Virial.Game;
using static Hazel.Udp.UdpConnection;
using static Il2CppSystem.Linq.Expressions.Interpreter.CastInstruction.CastInstructionNoT;
using static Nebula.Modules.ScriptComponents.NebulaSyncStandardObject;
using static UnityEngine.ParticleSystem;
using static UnityEngine.UI.GridLayoutGroup;

namespace Nebula.Roles.Abilities;

public record DyingMessageBrush(Color Color, float Width);
public class DyingMessageCanvasRenderRequest : MonoBehaviour
{
    static DyingMessageCanvasRenderRequest() => ClassInjector.RegisterTypeInIl2Cpp<DyingMessageCanvasRenderRequest>();
    private List<VVector2> requests = [];
    private Queue<(DyingMessageBrush brush, List<VVector2> dots)> storedQueue = [];
    public void AddPoint(VVector2 point) => requests.Add(point);
    public void AddPoint(VVector2 point, DyingMessageBrush brush)
    {
        if (storedQueue.Count == 0) {
            if (brush.Equals(this.brush))
                requests.Add(point);
            else
                storedQueue.Enqueue((brush, [point]));
        }
        else {
            var last = storedQueue.Last();
            if (last.brush.Equals(brush))
                last.dots.Add(point);
            else
                storedQueue.Enqueue((brush, [point]));
        }
    }
    public bool HasNoRequest => requests.Count == 0;
    private RenderTexture myTexture;
    private Material glMaterial;
    private DyingMessageBrush? brush;
    public float brushRadius = 0.014f;

    public const float DyingMessageBrushRadius = 0.014f;
    public const float PaintQuizBrushRadius = 0.003f;

    public void SetUp(RenderTexture texture, float brushRadius)
    {
        this.brushRadius = brushRadius;
        myTexture = texture;
        glMaterial = new(Shader.Find("Unlit/Color"));
        //glMaterial.color = Color.red;
        ClearRenderTexture(myTexture);
    }
    void OnPostRender()
    {
        if (requests.Count == 0 && storedQueue.Count == 0) return;
        if (glMaterial == null) return;
        if (Camera.current != Camera.main) return;

        if(requests.Count == 0)
        {
            var next = storedQueue.Dequeue();
            requests = next.dots;
            brush = next.brush;
            glMaterial.color = brush?.Color ?? Color.white;
            brushRadius = brush?.Width ?? 0.014f;
        }

        RenderTexture.active = myTexture;
        GL.PushMatrix();

        try
        {
            glMaterial.SetPass(0);
            GL.LoadPixelMatrix(0, 1, 1, 0);

            GL.Begin(4); //4: TRIANGLES

            foreach (var pos in requests)
            {
                int quarterSegments = 3; // 1/4円の滑らかさ
                for (int i = 0; i <= quarterSegments; i++)
                {
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    void DrawTriangle(VVector2 center, VVector3 offset1, VVector3 offset2)
                    {
                        GL.Vertex3(center.x, center.y, 0);
                        GL.Vertex3(center.x + offset1.x, center.y + offset1.y, 0);
                        GL.Vertex3(center.x + offset2.x, center.y + offset2.y, 0);
                    }

                    // 現在のステップと次のステップの角度を計算（第1象限のみ）
                    float a1 = i * (Mathn.PI * 0.5f) / quarterSegments;
                    float a2 = (i + 1) * (Mathn.PI * 0.5f) / quarterSegments;

                    float c1 = Mathn.Cos(a1) * brushRadius;
                    float s1 = Mathn.Sin(a1) * brushRadius;
                    float c2 = Mathn.Cos(a2) * brushRadius;
                    float s2 = Mathn.Sin(a2) * brushRadius;

                    // 4つの象限に対して三角形をスタンプ
                    // 第1象限 (+, +)
                    DrawTriangle(pos, new(c1, s1), new(c2, s2));
                    // 第2象限 (-, +)
                    DrawTriangle(pos, new(-s1, c1), new(-s2, c2));
                    // 第3象限 (-, -)
                    DrawTriangle(pos, new(-c1, -s1), new(-c2, -s2));
                    // 第4象限 (+, -)
                    DrawTriangle(pos, new(s1, -c1), new(s2, -c2));
                }
            }
        }catch(Exception e)
        {
            NebulaAPI.Logging.BepInExLogger().Error("[Error on DyingMessage]" + e.ToString());
        }
        GL.End(); // まとめてGPUへ転送

        GL.PopMatrix();
        RenderTexture.active = null;

        requests.Clear();
    }

    void ClearRenderTexture(RenderTexture rt)
    {
        RenderTexture.active = rt;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;
    }
}


internal class DyingMessageCanvas : MonoBehaviour
{
    static DyingMessageCanvas() => ClassInjector.RegisterTypeInIl2Cpp<DyingMessageCanvas>(); 

    private int TextureSize = 512;
    private float rendererSize = 3.5f;
    public float minDistance = 0.01f; // 同じ場所に書かないための閾値

    private RenderTexture canvas;
    private MeshRenderer canvasRenderer;
    private Il2CppArgument<DyingMessageCanvasRenderRequest> request;

    private VVector2? lastPos = null;

    private List<VVector2> currentStroke = [];
    private List<List<VVector2>> allStrokes = [];

    private bool asDyingMessage = false;
    private bool asQuizCanvas = false;
    private SpriteRenderer backRenderer, guageRenderer;
    private float leftTime = 1f;
    private float maxTime = 1f;
    private float inP = 0f;
    private VVector2 worldPosition;
    private Action<Texture2D>? callBack;
    private Action? onFailed;
    public Action? OnDestroyCallBack;
    private DyingMessageBrush? brush;
    private DefinedAssignable? assignable;
    private float brushRadius = 0f;
    public void SetUpAsDyingMessage(float leftTime, VVector2 pos, DefinedAssignable? assignable, Action<Texture2D>? callBack, Action? onFailed = null)
    {
        this.maxTime = leftTime;
        this.leftTime = leftTime;
        this.worldPosition = pos;
        this.callBack = callBack;
        this.onFailed = onFailed;
        this.assignable = assignable;

        this.TextureSize = 512;
        this.rendererSize = 3.5f;

        this.asDyingMessage = true;
        this.asQuizCanvas = false;

        this.brushRadius = DyingMessageCanvasRenderRequest.DyingMessageBrushRadius;
        this.brush = null;
    }

    public void SetUpAsQuizPaint()
    {
        this.maxTime = -1f;
        this.leftTime = 1f;

        this.TextureSize = 768;
        this.rendererSize = 6.2f;

        this.asDyingMessage = false;
        this.asQuizCanvas = true;

        this.brushRadius = DyingMessageCanvasRenderRequest.PaintQuizBrushRadius;
        this.brush = null;
    }

    private static readonly Image guageImage = SpriteLoader.FromResource("Nebula.Resources.WhiteFramed.png", 100f);
    
    void Start()
    {
        canvas = new RenderTexture(TextureSize, TextureSize, 32, RenderTextureFormat.ARGB32);
        var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", transform, new(0f, 0f, -0.02f), LayerExpansion.GetUILayer(), Color.white, UnityHelper.GetMeshRendererMaterial());
        if (asDyingMessage)
        {
            mesh.filter.CreateRectMesh(new(rendererSize, rendererSize), null);
            mesh.renderer.material.color = DyingMessages.BloodColor.ToUnityColor();
        }
        else if (asQuizCanvas)
        {
            var margin = (7f / 16f / 2f);
            mesh.filter.CreateRectMesh(new(rendererSize, rendererSize * 9f / 16f), null, 0f, 1f, 0f + margin, 1f - margin);
            mesh.renderer.material.color = Color.white;
        }
        mesh.renderer.sharedMaterial.mainTexture = canvas;
        canvasRenderer = mesh.renderer;



        request = Camera.main.gameObject.AddComponent<DyingMessageCanvasRenderRequest>();
        request.Value.SetUp(canvas, brushRadius);

        if (asDyingMessage)
        {
            backRenderer = UnityHelper.CreateSpriteRenderer("Background", transform, new(0f, 0f, 0f), LayerExpansion.GetUILayer());
            backRenderer.sprite = guageImage.GetSprite();
            backRenderer.color = new(164f / 255f, 155f / 255f, 138f / 255f);

            guageRenderer = UnityHelper.CreateSpriteRenderer("Background", backRenderer.transform, new(0f, 0f, -0.01f), LayerExpansion.GetUILayer());
            guageRenderer.material = new(NebulaAsset.GuageShader);
            guageRenderer.material.SetFloat("_Guage", 1f);
            guageRenderer.material.SetColor("_Color", new(245f / 255f, 230f / 255f, 201f / 255f));
            guageRenderer.sprite = guageImage.GetSprite();

            var collider = backRenderer.gameObject.AddComponent<BoxCollider2D>();
            collider.size = new(1.2f, 1.2f);
            collider.isTrigger = true;
            collider.gameObject.SetUpButton();
        }else if (asQuizCanvas)
        {
            backRenderer = UnityHelper.CreateSpriteRenderer("Background", transform, new(0f, 0f, 0f), LayerExpansion.GetUILayer());
            backRenderer.sprite = NebulaAsset.WhiteImage.GetSprite();
            backRenderer.color = new(200f / 255f, 200f / 255f, 200f / 255f);
            backRenderer.transform.localScale = new(rendererSize, rendererSize / 16f * 9f);
            minDistance = 0.0035f;
        }
    }

    [HideFromIl2Cpp]
    public void SetBrush(DyingMessageBrush? brush)
    {
        this.brush = brush;
    }

    void StartDraw(VVector2 mousePos)
    {
        currentStroke = [];
        DrawPoint(mousePos, true);
        lastPos = mousePos;
    }

    //lastPos.HasValue == trueが前提条件
    void UpdateDraw(VVector2 mousePos)
    {
        float dist = VVector2.Distance(lastPos.Value, mousePos);
        if (dist > minDistance)
        {
            // 点の間を補間して描画（円を並べる）
            int steps = Mathn.CeilToInt(dist / (minDistance * 0.5f));
            for (int i = 1; i <= steps; i++)
            {
                VVector2 lerpPos = VVector2.Lerp(lastPos.Value, mousePos, (float)i / steps);
                DrawPoint(lerpPos, i == steps);
            }
            lastPos = mousePos;
        }
    }

    void FinishDraw()
    {
        if (currentStroke.Count > 0)
        {
            allStrokes.Add(currentStroke);
        }
        lastPos = null;
    }

    void Update()
    {
        if(inP < 1f && asDyingMessage)
        {
            inP += Time.deltaTime * 5f;
            if (inP > 1f) inP = 1f;

            backRenderer.transform.localScale = new(inP * rendererSize, inP * rendererSize, 0f);

            return;
        }
        if (leftTime > 0f)
        {
            if (asDyingMessage)
            {
                guageRenderer.material.SetFloat("_Guage", leftTime / maxTime);
                leftTime -= Time.deltaTime;
            }

            VVector2 mousePos = GetNormalizedMousePos();

            if (Input.GetMouseButtonDown(0))
            {
                StartDraw(mousePos);
            }
            else if (Input.GetMouseButton(0))
            {
                if (lastPos.HasValue)
                    UpdateDraw(mousePos);
                else
                    StartDraw(mousePos);

            }
            else if (Input.GetMouseButtonUp(0))
            {
                FinishDraw();
            }

            if(asDyingMessage && !(leftTime > 0f))
            {
                DyingMessages.Send(Serialize(), this.worldPosition, this.assignable, this.callBack, this.onFailed);
                StartCoroutine(CoDestroy().WrapToIl2Cpp());
                StartCoroutine(ManagedEffects.Lerp(1f, p => canvasRenderer.material.color = VColor.Lerp(DyingMessages.BloodColor, DyingMessages.BloodDarkColor, p).ToUnityColor()).WrapToIl2Cpp());
            }
        }
    }

    IEnumerator CoDestroy() {
        for (int i = 0; i < 2; i++)
        {
            guageRenderer.material.SetFloat("_Guage", 1f);
            yield return Effects.Wait(0.08f);
            guageRenderer.material.SetFloat("_Guage", 0f);
            yield return Effects.Wait(0.08f);
        }
        yield return Effects.Wait(0.7f);
        float p = 1f;
        while(p > 0f)
        {
            transform.localScale = new(p, p, 1f);
            p -= Time.deltaTime * 7f;
            yield return null;
        }
        GameObject.Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (request.Value) GameObject.Destroy(request.Value);
        OnDestroyCallBack?.Invoke();
    }

    // GL命令による円の描画
    void DrawPoint(Vector2 pos, bool shouldRecord)
    {
        if(shouldRecord) currentStroke.Add(pos);

        if (asQuizCanvas)
            request.Value.AddPoint(pos, brush!);
        else 
            request.Value.AddPoint(pos);
    }

    private VVector2 GetNormalizedMousePos()
    {
        // Viewport座標(0-1)を取得、またはRaycastの結果からUVを取得
        VVector2 localPos = UnityHelper.ScreenToLocalPoint(Input.mousePosition, LayerExpansion.GetUILayer(), transform);
        localPos.y = -localPos.y;
        return localPos / rendererSize + new VVector2(0.5f, 0.5f);
    }

    private const int MinDelta = -7;
    private const int MaxDelta = 8;

    public (byte beginX, byte beginY, byte[] trajectory)[] Serialize()
    {
        FinishDraw();
        var strokes = allStrokes;
        var encoded = new List<(byte beginX, byte beginY, byte[] trajectory)>();

        foreach (var rawStroke in strokes)
        {
            if (rawStroke == null || rawStroke.Count <= 1) continue;

            List<VVector2> currentInViewPath = new List<VVector2>();

            for (int i = 0; i < rawStroke.Count - 1; i++)
            {
                if (ClipLine(rawStroke[i], rawStroke[i + 1], out var pStart, out var pEnd, out bool isStartClipped, out bool isEndClipped))
                {
                    // はじめて範囲内に入った、あるいは外側から戻ってきた
                    if (currentInViewPath.Count == 0 || isStartClipped)
                    {
                        if (currentInViewPath.Count > 0)
                        {
                            SerializeNormalizedStroke(currentInViewPath, encoded);
                            currentInViewPath.Clear();
                        }
                        currentInViewPath.Add(pStart);
                    }

                    currentInViewPath.Add(pEnd);

                    // 終点が境界でカットされた場合
                    if (isEndClipped)
                    {
                        SerializeNormalizedStroke(currentInViewPath, encoded);
                        currentInViewPath.Clear();
                    }
                }
                else
                {
                    // 双方範囲外
                    if (currentInViewPath.Count > 0)
                    {
                        SerializeNormalizedStroke(currentInViewPath, encoded);
                        currentInViewPath.Clear();
                    }
                }
            }

            if (currentInViewPath.Count > 0)
            {
                SerializeNormalizedStroke(currentInViewPath, encoded);
            }
        }

        return encoded.ToArray();
    }

    private bool ClipLine(VVector2 p1, VVector2 p2, out VVector2 start, out VVector2 end, out bool isStartClipped, out bool isEndClipped)
    {
        float t0 = 0, t1 = 1;
        float dx = p2.x - p1.x;
        float dy = p2.y - p1.y;

        start = p1;
        end = p2;
        isStartClipped = false;
        isEndClipped = false;

        if (ClipTest(-dx, p1.x, ref t0, ref t1) && ClipTest(dx, 1 - p1.x, ref t0, ref t1) &&
            ClipTest(-dy, p1.y, ref t0, ref t1) && ClipTest(dy, 1 - p1.y, ref t0, ref t1))
        {
            if (t0 > 0)
            {
                start.x = p1.x + t0 * dx;
                start.y = p1.y + t0 * dy;
                isStartClipped = true;
            }
            if (t1 < 1)
            {
                end.x = p1.x + t1 * dx;
                end.y = p1.y + t1 * dy;
                isEndClipped = true;
            }
            return true;
        }

        return false;
    }

    private bool ClipTest(float p, float q, ref float t0, ref float t1)
    {
        if (p < 0)
        {
            float t = q / p;
            if (t > t1) return false;
            if (t > t0) t0 = t;
        }
        else if (p > 0)
        {
            float t = q / p;
            if (t < t0) return false;
            if (t < t1) t1 = t;
        }
        else if (q < 0) return false;
        return true;
    }

    private void SerializeNormalizedStroke(List<VVector2> stroke, List<(byte beginX, byte beginY, byte[] trajectory)> list)
    {
        byte bX = (byte)Mathn.Clamp(Mathn.RoundToInt(stroke[0].x * 255f), 0, 255);
        byte bY = (byte)Mathn.Clamp(Mathn.RoundToInt(stroke[0].y * 255f), 0, 255);
        var nibbles = new List<byte>();

        int lastX = bX;
        int lastY = bY;

        for (int i = 1; i < stroke.Count; i++)
        {
            int targetX = Mathn.Clamp(Mathn.RoundToInt(stroke[i].x * 255f), 0, 255);
            int targetY = Mathn.Clamp(Mathn.RoundToInt(stroke[i].y * 255f), 0, 255);

            while (lastX != targetX || lastY != targetY)
            {
                int fullDx = targetX - lastX;
                int fullDy = targetY - lastY;

                float divX = fullDx > 0 ? (float)fullDx / MaxDelta : (fullDx < 0 ? (float)fullDx / MinDelta : 0);
                float divY = fullDy > 0 ? (float)fullDy / MaxDelta : (fullDy < 0 ? (float)fullDy / MinDelta : 0);

                int stepsNeeded = Mathn.CeilToInt(Mathn.Max(divX, divY, 1.0f));

                int dx = Mathn.RoundToInt((float)fullDx / stepsNeeded);
                int dy = Mathn.RoundToInt((float)fullDy / stepsNeeded);

                dx = Mathn.Clamp(dx, MinDelta, MaxDelta);
                dy = Mathn.Clamp(dy, MinDelta, MaxDelta);

                byte nX = (byte)((dx - MinDelta) & 0x0F);
                byte nY = (byte)((dy - MinDelta) & 0x0F);
                nibbles.Add((byte)((nX << 4) | nY));

                lastX += dx;
                lastY += dy;
            }
        }

        list.Add((bX, bY, nibbles.ToArray()));
    }
}

internal class DyingMessageObject : IGameOperator, ILifespan
{
    SpriteRenderer renderer;
    bool found = false;
    VVector2 position;
    Sprite messageSprite;
    public DyingMessageObject(VVector2 pos, Sprite messageSprite)
    {
        this.position = pos;
        this.messageSprite = messageSprite;
        renderer = UnityHelper.CreateSpriteRenderer("Renderer", null, new(pos.x, pos.y - 0.12f, pos.y / 1000f + 0.001f));
        renderer.sprite = messageSprite;
        renderer.transform.localScale = new(0.14f, 0.14f, 1f);
        renderer.color = VColor.Lerp(DyingMessages.BloodColor, DyingMessages.BloodDarkColor, 0.7f).ToUnityColor();
    }

    bool ILifespan.IsDeadObject => false;

    void OnGameUpdate(GameUpdateEvent ev)
    {
        if (MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast()) return;

        var localPlayer = GamePlayer.LocalPlayer;
        if (localPlayer == null) return;
        if (!found && localPlayer.Position.Distance(position) < 1.1f)
        {
            var roomName = NebulaAPI.CurrentGame?.CurrentMap?.GetRoomName(position, true, false, false) ?? "ERROR";
            found = true;

            NebulaAPI.CurrentGame?.GetModule<MeetingOverlayHolder>()?.RegisterOverlay(GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayTitle, "game.dyingMessage.ui.title"),
                GUI.API.RawText(Virial.Media.GUIAlignment.Left, Virial.Text.AttributeAsset.OverlayContent, Language.Translate("game.dyingMessage.ui.content").Replace("%ROOM%", roomName)),
                new NoSGUIImage(Virial.Media.GUIAlignment.Left, new WrapSpriteLoader(() => messageSprite), new(1.8f, 1.8f), VColor.Lerp(DyingMessages.BloodColor, DyingMessages.BloodDarkColor, 0.5f))
                ), MeetingOverlayHolder.IconsSprite[8], VColor.Red);
        }
    }
}

[NebulaRPCHolder]
internal static class DyingMessages
{
    internal static VColor BloodColor = new(205, 10, 19);
    internal static VColor BloodDarkColor = new(145, 7, 14);

    static private List<DyingMessageCanvas> currentCanvases = [];
    static public bool ThereIsAnyCanvas => currentCanvases.Count > 0 && currentCanvases.Any(c => c.AsBoolFast());
    static public void Send((byte beginX, byte beginY, byte[] trajectory)[] trajectories, VVector2 worldPos, DefinedAssignable? relatedRole, Action<Texture2D>? callBack, Action? onFailed = null)
    {
        if (trajectories.Length == 0)
        {
            onFailed?.Invoke();
            return;
        }
        RpcDyingMessage.Invoke((trajectories, worldPos, relatedRole!));
        GenerateDyingMessage(trajectories, texture =>
        {
            callBack?.Invoke(texture);
            var obj = new DyingMessageObject(worldPos, texture.ToSprite(100f));
            obj.RegisterSelf();
        });
    }

    static private LongRemoteProcess<((byte beginX, byte beginY, byte[] trajectory)[] trajectories, VVector2 worldPos, DefinedAssignable relatedRole)> RpcDyingMessage = new("DyingMessage", (message, calledByMe) =>
    {
        if (calledByMe) return; //自身はコールバックを実行ため、呼び出し元で実行している。
        GenerateDyingMessage(message.trajectories, texture =>
        {
            var obj = new DyingMessageObject(message.worldPos, texture.ToSprite(100f));
            obj.RegisterSelf();
            GameOperatorManager.Instance?.Run<DyingMessageGenerateEvent>(new(message.relatedRole, message.worldPos));
        });
    });

    private const float minDistance = 0.01f; // DyingMessageCanvasからコピー。本当は共有するべき。
    static void DecodeTrajectory(byte beginX, byte beginY, byte[] trajectory, Action<VVector2> drawPoint)
    {
        VVector2 currentPos = new(beginX / 255f, beginY / 255f);
        drawPoint(currentPos); 

        float drawStepThreshold = (minDistance * 0.5f);

        foreach (byte b in trajectory)
        {
            // 変位の取り出し
            int dxInt = ((b >> 4) & 0x0F) - 7;
            int dyInt = (b & 0x0F) - 7;

            VVector2 delta = new(dxInt / 255f, dyInt / 255f);
            VVector2 targetPos = currentPos + delta;

            float dist = delta.Magnitude;

            if (dist > drawStepThreshold)
            {
                int subSteps = Mathn.CeilToInt(dist / drawStepThreshold);
                for (int s = 1; s < subSteps; s++)
                {
                    VVector2 lerpPos = VVector2.Lerp(currentPos, targetPos, (float)s / subSteps);
                    drawPoint(lerpPos);
                }
            }

            drawPoint(targetPos);
            currentPos = targetPos;
        }
    }

    private const int TextureSize = 512;
    static private void GenerateDyingMessage((byte beginX, byte beginY, byte[] trajectory)[] encoded, Action<Texture2D> callback)
    {
        var request = Camera.main.gameObject.AddComponent<DyingMessageCanvasRenderRequest>();
        var rt = new RenderTexture(TextureSize, TextureSize, 32, RenderTextureFormat.ARGB32);
        request.SetUp(rt, DyingMessageCanvasRenderRequest.DyingMessageBrushRadius);

        foreach (var entry in encoded) DecodeTrajectory(entry.beginX, entry.beginY, entry.trajectory, request.AddPoint);

        IEnumerator CoWaitDrawing()
        {
            while (!request.HasNoRequest) yield return null;
            Texture2D texture2D = new Texture2D(TextureSize, TextureSize, TextureFormat.ARGB32, false, false);
            RenderTexture.active = rt;
            texture2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture2D.Apply();
            callback.Invoke(texture2D);

            GameObject.Destroy(rt);
        }
        request.StartCoroutine(CoWaitDrawing().WrapToIl2Cpp());
        
    }

    static public DyingMessageCanvas GenerateCanvas(VVector2 position, float duration, DefinedAssignable? assignable, Action<Texture2D>? callBack = null, Action? onFailed = null)
    {
        var canvas = UnityHelper.CreateObject<DyingMessageCanvas>("DyingMessageCanvas", AmongUsLLImpl.HudManagerInstance.transform, new(0f, 0f, -480f));
        canvas.SetUpAsDyingMessage(duration, position, assignable, callBack, onFailed);
        currentCanvases.Add(canvas);
        canvas.OnDestroyCallBack = () => currentCanvases.Remove(canvas);
        return canvas;
    }
}

internal class DyingMessageGenerateEvent : Virial.Events.Event
{
    public DefinedAssignable? RelatedAssignable { get; }
    public VVector2 Position { get; }

    internal DyingMessageGenerateEvent(DefinedAssignable? relatedAssignable, VVector2 position)
    {
        this.RelatedAssignable = relatedAssignable;
        this.Position = position;
    }
}