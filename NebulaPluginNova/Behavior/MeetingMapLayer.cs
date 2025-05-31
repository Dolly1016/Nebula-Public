using Il2CppSystem.Xml.Schema;
using JetBrains.Annotations;
using Nebula.Modules.Cosmetics;
using Nebula.Modules.GUIWidget;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Game.Minimap;
using Virial.Game;
using static MeetingHud;

namespace Nebula.Behavior;

public class LineDrawer
{
    public LineDrawer(Transform parent, Vector3 localPosition, Vector2 firstPos, Color color, int layer, float width = 0.05f)
    {
        renderer = UnityHelper.SetUpLineRenderer("Line", parent, localPosition, layer, width);
        renderer.transform.localScale = Vector3.one;
        renderer.numCornerVertices = 5;
        this.color = color;
        positions = [firstPos, firstPos];
        UpdateLine(positions);
    }

    public static LineRenderer Restore(Transform parent, Vector3 localPosition, IReadOnlyList<Vector2> points, Color color, int layer, float width = 0.05f)
    {
        var drawer = new LineDrawer(parent, localPosition, Vector2.zero, color, layer, width);
        drawer.UpdateLine(points);
        return drawer.FixLine(null, null);
    }

    public LineRenderer FixLine(Color? color, float? width)
    {
        finished = true;
        if (width.HasValue) renderer.SetWidth(width.Value, width.Value);
        if (color.HasValue) renderer.SetColors(color.Value, color.Value);

        return renderer;
    }

    LineRenderer renderer;
    List<Vector2> positions = [];
    public IReadOnlyList<Vector2> Positions => positions;
    Color color;
    bool finished = false;
    public bool Finished => true;
    void UpdateLine(IReadOnlyList<Vector2> pos)
    {
        if (!renderer) return;

        renderer.positionCount = pos.Count;
        renderer.SetPositions(pos.Select(p => p.AsVector3(0f)).ToArray());
        renderer.SetColors(color, color);
    }

    /// <summary>
    /// 点を増やす際に削除できる点を探す
    /// </summary>
    void OnFixPoint()
    {
        var lastPos = positions[^1];
        int n = positions.Count - 2;
        int temp = n - 1;
        while (temp >= 0)
        {
            var firstPos = positions[temp];
            var dir = (lastPos - firstPos).normalized;
            var orth = new Vector2(dir.y, -dir.x);
            var trackingPos = lastPos;
            float diff = 0f;
            bool breakFlag = false;
            for (int i = temp + 1; i < positions.Count; i++)
            {
                var currentPos = positions[i];
                var diffVec = currentPos - trackingPos;
                diff += Vector2.Dot(orth, diffVec);
                if (Mathf.Abs(diff) > 0.08f) //ある程度まっすぐでなければ修正しない。
                {
                    breakFlag = true;
                    break;
                }
            }

            if (!breakFlag)
            {
                n = temp;
                temp--;
            }
            else
            {
                break;
            }
        }

        if (n < positions.Count - 2) positions.RemoveRange(n + 1, positions.Count - n - 2);
    }

    public void Update()
    {
        if (finished) return;

        Vector2 currentPos = renderer.transform.InverseTransformPoint(UnityHelper.ScreenToWorldPoint(Input.mousePosition, renderer.gameObject.layer));

        if (positions.Count == 0)
        {
            positions.Add(currentPos);
            positions.Add(currentPos);
        }
        else
        {
            var lastPoint1 = positions[^1];
            var lastPoint2 = positions[^2];

            if (lastPoint2.Distance(currentPos) > 0.01f)
            {
                var distance1 = lastPoint2.Distance(lastPoint1);
                var distance2 = lastPoint2.Distance(currentPos);
                var distance3 = lastPoint1.Distance(currentPos);
                if (distance1 < 0.01f)
                {
                    positions[^1] = currentPos;
                }
                else
                {
                    var dir1 = (lastPoint1 - lastPoint2).normalized;
                    var dir2 = (currentPos - lastPoint2).normalized;
                    var dir3 = (currentPos - lastPoint1).normalized;

                    if (Vector2.Dot(dir1, dir2) < 0.98f)
                    {
                        //大きく曲がっている場合
                        OnFixPoint();
                        positions.Add(currentPos);
                    }
                    else if (distance3 > 0.08f && Vector2.Dot(dir2, dir3) < 0.9f)
                    {
                        OnFixPoint();
                        positions.Add(currentPos);
                    }
                    else
                    {
                        positions[^1] = lastPoint2 + dir1 * distance2;
                    }
                }
            }
        }

        UpdateLine(positions);
    }
}

[NebulaPreprocess(PreprocessPhase.BuildNoSModule)]
public class DiscussionSupport : AbstractModule<Virial.Game.Game>, IGameOperator
{
    private record StoredLineInfo(int Id, Vector2[] Points, byte PlayerId, int[] RelatedIcons);
    private record StoredIconInfo(int Id, Vector2 Point, byte PlayerId, int IconType, int[] RelatedLines);
    private record StoredInfo(StoredLineInfo[] Lines, StoredIconInfo[] Icons, int NextLineId, int NextIconId)
    {
        public bool IsEmpty => Lines.IsEmpty() && Icons.IsEmpty();
    }
    private record LineInfo(int Id, LineRenderer Line, LineRenderer BackLine1, LineRenderer BackLine2, PassiveButton Button, Collider2D Collider)
    {
        public StoredLineInfo ToStoredInfo() {
            var points = new Vector2[Line.positionCount];
            for (int i = 0; i < points.Length; i++) points[i] = Line.GetPosition(i);
            return new StoredLineInfo(Id, points, PlayerId, RelatedIcons.Select(icon => icon.Id).ToArray());
        }

        public byte PlayerId = byte.MaxValue;
        public HashSet<IconInfo> RelatedIcons = [];
        public void SetPlayer(byte playerId, bool dontSkip = false)
        {
            if (playerId == PlayerId && !dontSkip) return;

            PlayerId = playerId;
            SetColor(playerId == byte.MaxValue ? Color.gray : DynamicPalette.PlayerColors[playerId]);
            float width = playerId == byte.MaxValue ? 0.015f : 0.03f;
            Line.SetWidth(width, width);
            float backWidth1 = width + 0.03f;
            BackLine1.SetWidth(backWidth1, backWidth1);
            float backWidth2 = width + 0.09f;
            BackLine2.SetWidth(backWidth2, backWidth2);

            //関連するアイコンに変更を波及させる
            RelatedIcons.Do(icon => icon.SetPlayer(playerId));
        }

        public void SetColor(Color color)
        {
            if (Line) Line.SetColors(color, color);
        }

        public void ResetRelation()
        {
            RelatedIcons.Do(icon => icon.RelatedLines.Remove(this));
            RelatedIcons.Clear();
        }

        public void Touch(IconInfo icon)
        {
            if (icon.PlayerId != byte.MaxValue && icon.IsPlayerImage) SetPlayer(icon.PlayerId);

            RelatedIcons.Add(icon);
            icon.RelatedLines.Add(this);
        }

        public void OpenPlayerOverlay()
        {
            var player = GamePlayer.GetPlayer(PlayerId);
            if (player != null)
            {
                NebulaManager.Instance.SetHelpWidget(Button, GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), Language.Translate("meeting.memo.player.line").Replace("%PLAYER%", player.PlayerName)),
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.control.line")
                    ));
            }
            else
            {
                NebulaManager.Instance.SetHelpWidget(Button, GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.control.line")
                    ));
            }
        }

        public void OpenEditorOverlay()
        {
            TMPro.TextMeshPro nameCandText = null!;
            NebulaManager.Instance.SetHelpWidget(null, GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.player.color"),
                GUI.API.Arrange(Virial.Media.GUIAlignment.Left, GamePlayer.AllOrderedPlayers.Select(p => new NoSGUIImage(Virial.Media.GUIAlignment.Left, MeetingPlayerIcons.AsLoader(0), new(0.4f, null), null, _ =>
                {
                    SetPlayer(p.PlayerId);
                    NebulaManager.Instance.HideHelpWidget();
                })
                {
                    PostBuilder = renderer =>
                    {
                        renderer.material = HatManager.Instance.PlayerMaterial;
                        PlayerMaterial.SetColors(p.PlayerId, renderer);

                        var button = renderer.GetComponent<PassiveButton>();
                        button.OnMouseOver.AddListener(() =>
                        {
                            if (nameCandText) nameCandText.text = p.Name;
                        });
                        button.OnMouseOut.AddListener(() =>
                        {
                            if (nameCandText) nameCandText.text = "";
                        });
                    }
                }), 8),
                new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent(GamePlayer.AllPlayers.MaxBy(p => p.Name.Length)?.Name ?? "-----"))
                {
                    PostBuilder = text => { nameCandText = text; text.text = ""; }
                }
                ));
            NebulaManager.Instance.MouseOverPopup.Parameters.RelatedPredicate = (() => MapBehaviour.Instance && MapBehaviour.Instance.IsOpen && Line);
        }
    }

    private record IconInfo(int Id, SpriteRenderer Renderer, PassiveButton Button, Collider2D Collider)
    {
        public StoredIconInfo ToStoredInfo() => new(Id, Collider.transform.localPosition, PlayerId, IconIndex, RelatedLines.Select(line => line.Id).ToArray());

        public int IconIndex = 0;
        public bool IsPlayerImage => IconIndex < 32;
        public byte PlayerId = byte.MaxValue;
        public Vector3 LastPosition = Vector3.zero;
        public HashSet<LineInfo> RelatedLines = [];
        private bool usePlayerMat = true;
        
        public void SetImageType(int index)
        {
            IconIndex = index;
            Renderer.sprite = (IsPlayerImage ? MeetingPlayerIcons : MeetingIcons).GetSprite(IconIndex % 32);

            if (usePlayerMat != IsPlayerImage)
            {
                Renderer.material = new(IsPlayerImage ? HatManager.Instance.PlayerMaterial : HatManager.Instance.DefaultShader);
                if (Renderer)
                {
                    PlayerMaterial.SetColors(PlayerId, Renderer);
                }

                usePlayerMat = IsPlayerImage;
            }
        }

        public void SetPlayer(byte playerId)
        {
            if (!IsPlayerImage) return;
            if (playerId == PlayerId) return;
            if (playerId == byte.MaxValue) return;

            if (Renderer && IsPlayerImage)
            {
                PlayerMaterial.SetColors(playerId, Renderer);
            }
            PlayerId = playerId;

            //関連する線に変更を波及させる
            RelatedLines.Do(line => line.SetPlayer(playerId));
        }

        public void UpdateLastPosition()
        {
            LastPosition = Button.transform.localPosition;
        }

        public void MoveTo(Vector2 diff)
        {
            Button.transform.localPosition = LastPosition + (Vector3)diff;

            ResetRelation();
        }

        public void OpenPlayerOverlay()
        {
            var player = GamePlayer.GetPlayer(PlayerId);
            if (player != null)
            {
                NebulaManager.Instance.SetHelpWidget(Button, GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    IsPlayerImage ? GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    NebulaGameManager.Instance!.TryGetTitle(PlayerId, out var title) ? GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), Language.Translate(title.TranslationKey).Sized(80).Color(DynamicPalette.PlayerColors[PlayerId])) : null,
                    GUI.API.RawText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), player.PlayerName)
                    ) : null,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.control.icon")
                    ));
            }
        }

        public void OpenEditorOverlay()
        {
            TMPro.TextMeshPro nameCandText = null!;
            NebulaManager.Instance.SetHelpWidget(null, GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.player.type"),
                GUI.API.Arrange(Virial.Media.GUIAlignment.Left, Helpers.Sequential(MeetingPlayerIcons.Length).Select(i => new NoSGUIImage(Virial.Media.GUIAlignment.Left, MeetingPlayerIcons.AsLoader(i), new(0.4f, null), null, _ =>
                {
                    SetImageType(i);
                    NebulaManager.Instance.HideHelpWidget();
                })
                {
                    PostBuilder = renderer =>
                    {
                        renderer.material = HatManager.Instance.PlayerMaterial;
                        PlayerMaterial.SetColors(PlayerId, renderer);
                    }
                }), 8),

                GUI.API.Arrange(Virial.Media.GUIAlignment.Left, Helpers.Sequential(MeetingIcons.Length).Select(i => new NoSGUIImage(Virial.Media.GUIAlignment.Left, MeetingIcons.AsLoader(i), new(0.4f, null), null, _ =>
                {
                    SetImageType(i + 32);
                    NebulaManager.Instance.HideHelpWidget();
                })), 8),

                IsPlayerImage ? GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Left,
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), "meeting.memo.player.color"),
                    GUI.API.Arrange(Virial.Media.GUIAlignment.Left, GamePlayer.AllOrderedPlayers.Select(p => new NoSGUIImage(Virial.Media.GUIAlignment.Left, MeetingPlayerIcons.AsLoader(IconIndex), new(0.4f, null), null, _ =>
                    {
                        SetPlayer(p.PlayerId);
                        NebulaManager.Instance.HideHelpWidget();
                    })
                    {
                        PostBuilder = renderer =>
                    {
                        renderer.material = HatManager.Instance.PlayerMaterial;
                        PlayerMaterial.SetColors(p.PlayerId, renderer);
                        var button = renderer.GetComponent<PassiveButton>();
                        button.OnMouseOver.AddListener(() =>
                        {
                            if (nameCandText) nameCandText.text = p.Name;
                        });
                        button.OnMouseOut.AddListener(() =>
                        {
                            if (nameCandText) nameCandText.text = "";
                        });
                    }
                    }), 8),
                    new NoSGUIText(Virial.Media.GUIAlignment.Left, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayContent), new RawTextComponent(GamePlayer.AllPlayers.MaxBy(p => p.Name.Length)?.Name ?? "-----"))
                    {
                        PostBuilder = text => { nameCandText = text; text.text = ""; }
                    }
                    ) : null
                ));
            NebulaManager.Instance.MouseOverPopup.Parameters.RelatedPredicate = (() => MapBehaviour.Instance && MapBehaviour.Instance.IsOpen && Renderer);
        }

        public void ResetRelation()
        {
            RelatedLines.Do(line => line.RelatedIcons.Remove(this));
            RelatedLines.Clear();
        }

        public void Touch(LineInfo line)
        {
            if (line.PlayerId != byte.MaxValue) SetPlayer(line.PlayerId);

            RelatedLines.Add(line);
            line.RelatedIcons.Add(this);
        }
    }

    static DiscussionSupport() => DIManager.Instance.RegisterModule(() => new DiscussionSupport());
    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);

    //トラッキング情報を保存する
    private List<StoredInfo> storedData = [];
    private int currentPage = 0;

    LineDrawer? currentLineDrawer = null;
    GameObject meetingLayer = null!;
    PassiveUiElement meetingLayerButton = null!;
    List<LineInfo> lines = [];
    List<IconInfo> icons = [];
    int lineIndex = 0;
    int iconIndex = 0;

    public bool CursorIsOnMeetingLayer => PassiveButtonManager.Instance.currentOver && meetingLayerButton && PassiveButtonManager.Instance.currentOver.GetInstanceID() == meetingLayerButton.GetInstanceID();
    public bool FindCurrentOver<T>(IEnumerable<T> candidates, Func<T, PassiveUiElement> selector, [MaybeNullWhen(false)] out T found) where T : class
    {
        found = null;
        if (!PassiveButtonManager.Instance.currentOver) return false;
        var instanceId = PassiveButtonManager.Instance.currentOver.GetInstanceID();
        foreach(var c in candidates)
        {
            if (selector(c).GetInstanceID() == instanceId)
            {
                found = c;
                return true;
            }
        }
        return false;
    }
    void ResetAll()
    {
        currentLineDrawer = null;
        clickBackLayer = false;
        lines.Clear();
        icons.Clear();
        lineIndex = 0;
        iconIndex = 0;
        targetIcon = null;
    }
    void OnMapOpen(AbstractMapOpenEvent ev)
    {
        if(MeetingHud.Instance && ev is MapOpenNormalEvent && !GeneralConfigurations.ProhibitMeetingTool)
        {
            if (!meetingLayer)
            {
                MapBehaviour.Instance.transform.FindChild("CloseButton")?.TryGetComponent<ButtonBehavior>(out closeButton);

                ResetAll();
                meetingLayer = UnityHelper.CreateObject("MeetingLayer", MapBehaviour.Instance.transform, new(0f,0f,-3f));
                var collider = meetingLayer.AddComponent<BoxCollider2D>();
                collider.size = new(100f, 100f);
                collider.isTrigger = true;
                var button = meetingLayer.SetUpButton(false);

                button.OnClick.AddListener(() =>
                {
                    if (dragIcon) return; //位置を移動させた場合はメニューを開かない
                });
                meetingLayerButton = button;

                TMPro.TextMeshPro pageText = null!;
                var holder = GUI.API.HorizontalHolder(Virial.Media.GUIAlignment.Center, 
                    GUI.API.LocalizedText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), "meeting.memo.page"),
                    GUI.API.HorizontalMargin(0.1f),
                    new NoSGUIText(Virial.Media.GUIAlignment.Center, GUI.API.GetAttribute(Virial.Text.AttributeAsset.OverlayTitle), new RawTextComponent("---")) { 
                        PostBuilder = (text) => pageText = text
                    },
                    GUI.API.HorizontalMargin(0.1f),
                    new GUISpinButton(Virial.Media.GUIAlignment.Center, increment => {
                        var index = Store();
                        currentPage = increment ? index.next : index.prev;
                        Restore();
                        pageText.text = currentPage.ToString();
                    }) { AsMaskedButton = false }
                    );

                var pageChanger = holder.Instantiate(new(100f, 100f), out _);
                pageChanger!.AddComponent<SortingGroup>();
                pageChanger.name = "PageChanger";
                pageChanger.transform.SetParent(meetingLayer.transform);
                pageChanger.transform.localPosition = AmongUsUtil.CurrentMapId switch {
                    5 => new(0f, 2.4f, -20f),
                    _ => new(-2f, 2.4f, -20f)
                    };

                Restore();

                pageText.text = currentPage.ToString();

                //チュートリアル
                Tutorial.ShowTutorial(
                            new TutorialBuilder().AsSimpleTitledOnceTextWidget("meetingTool")
                            .ShowWhile(() => MeetingHud.Instance && MapBehaviour.Instance && MapBehaviour.Instance.IsOpen && lines.Count == 0 && icons.Count == 0)
                            );
            }
        }
        else
        {
            ResetAll();
            if(meetingLayer) GameObject.Destroy(meetingLayer);
        }
    }

    void OnMeetingEnd(MeetingEndEvent ev)
    {
        Store();
    }

    Vector2 MouseWorldPos => meetingLayer ? UnityHelper.ScreenToWorldPoint(Input.mousePosition, meetingLayer.gameObject.layer) : Vector2.zero;
    Vector2 MousePos => meetingLayer ? meetingLayer.transform.InverseTransformPoint(MouseWorldPos) : Vector2.zero;
    Vector2 downPos = Vector2.zero;
    bool clickBackLayer = false;
    bool dragIcon = false;
    bool isSpecialSpawning = false;
    IconInfo? targetIcon = null;
    ButtonBehavior closeButton = null!;
    private bool CanShowNormalOverlay => CanShowClickOverlay && !NebulaManager.Instance.MouseOverPopup.ShowUnrelatedOverlay;
    private bool CanShowClickOverlay => (!dragIcon || (!Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))) && currentLineDrawer == null;
    static private readonly IDividedSpriteLoader MeetingIcons = DividedSpriteLoader.FromResource("Nebula.Resources.MeetingToolIcons.png", 100f, 80, 80, true);
    static private readonly IDividedSpriteLoader MeetingPlayerIcons = DividedSpriteLoader.FromResource("Nebula.Resources.MeetingToolPlayerIcons.png", 100f, 80, 80, true);
    void OnUpdate(GameHudUpdateEvent ev)
    {
        if (meetingLayer)
        {
            //マウスボタンの押下を受け付け、オーバーレイの除去かアイコン・線の描画か決める。
            bool cursorIsOnMeetingLayer = CursorIsOnMeetingLayer;
            if (Input.GetMouseButtonDown(0))
            {
                if (closeButton && closeButton.colliders.Any(c => c.OverlapPoint(MouseWorldPos))) return; 
                dragIcon = false;
                isSpecialSpawning = false;
                targetIcon = null;
                downPos = MousePos;
                if (cursorIsOnMeetingLayer)
                {
                    if (NebulaManager.Instance.MouseOverPopup.ShowAnyOverlay)
                    {
                        //オーバーレイが表示されていれば隠す。
                        NebulaManager.Instance.HideHelpWidget();
                    }
                    else
                    {
                        //表示されているオーバーレイが無ければアイコンの設置か線の描画を始める。
                        clickBackLayer = true;
                    }
                }
                else if (FindCurrentOver(icons, icon => icon.Button, out var currentIcon))
                {
                    targetIcon = currentIcon;
                    dragIcon = false;
                }
            }

            //カーソルの動きを見てアイコンの設置か線の描画か決める。
            if (MapBehaviour.Instance && meetingLayer)
            {
                //線の描画かアイコンの位置移動
                if (Input.GetMouseButton(0) && downPos.Distance(MousePos) > 0.1f && !isSpecialSpawning)
                {
                    if (clickBackLayer && currentLineDrawer == null)
                    {
                        currentLineDrawer = new(meetingLayer.transform, new(0f, 0f, (lineIndex + 1) * -0.0001f), downPos, new(0.75f, 0.3f, 0.3f), LayerExpansion.GetUILayer());
                    }
                    else if (targetIcon != null && !dragIcon)
                    {
                        NebulaManager.Instance.HideHelpWidget();
                        dragIcon = true;
                        targetIcon.UpdateLastPosition();
                    }
                }

                //アイコンの設置
                if (clickBackLayer && Input.GetMouseButtonUp(0))
                {
                    clickBackLayer = false;
                    if (currentLineDrawer == null && cursorIsOnMeetingLayer && !isSpecialSpawning && MapBehaviour.Instance.IsOpen)
                    {
                        var icon = GenerateIcon(downPos, null);
                        icon.OpenEditorOverlay();
                    }

                }
            }

            //線の描画(アイコンの設置がスルーされて以降)
            if (currentLineDrawer != null)
            {
                if (MapBehaviour.Instance && Input.GetMouseButton(0))
                {
                    currentLineDrawer.Update();
                }

                //線を描いている途中で右クリックすると代わりに全クルーをスポーンさせる
                if (Input.GetMouseButtonDown(1))
                {
                    var pos1 = downPos;
                    var pos2 = MousePos;

                    var allPlayers = GamePlayer.AllOrderedPlayers;
                    int num = allPlayers.Count;
                    int div = num <= 1 ? 1 : num - 1;
                    for(int i = 0;i< num; i++)
                    {
                        float p2 = (float)i / (float)div;
                        float p1 = 1f - p2;
                        GenerateIcon((pos1 * p1) +(pos2 * p2), null).SetPlayer(allPlayers[i].PlayerId);
                    }
                    
                    GameObject.Destroy(currentLineDrawer.FixLine(null, null).gameObject);
                    currentLineDrawer = null;
                    isSpecialSpawning = true;
                }
                else if (Input.GetMouseButtonUp(0))
                {
                    var line = currentLineDrawer.FixLine(Color.gray, 0.03f);
                    var lineInfo = GenerateLine(line, null);

                    if (FindCurrentOver(icons, icon => icon.Button, out var currentIcon)) lineInfo.Touch(currentIcon);

                    //if (lineInfo.PlayerId == byte.MaxValue) lineInfo.OpenEditorOverlay();

                    currentLineDrawer = null;
                    dragIcon = false;
                }
            }

            //アイコンの移動
            if (targetIcon != null && dragIcon)
            {
                var diff = MousePos - downPos;
                targetIcon.MoveTo(diff);

                if (Input.GetMouseButtonUp(0))
                {
                    var mouseWorldPos = MouseWorldPos;
                    if (lines.Find(line => line.Collider.ClosestPoint(mouseWorldPos).Distance(mouseWorldPos) < 0.08f, out var found))
                    {
                        //線がプレイヤー未決定の線なら線がタッチする。線にプレイヤーが当たっていればアイコンがタッチする。
                        if (found.PlayerId == byte.MaxValue)
                            found.Touch(targetIcon);
                        else
                            targetIcon.Touch(found);
                    }

                    targetIcon = null;
                }
            }
        }
    }

    private IconInfo GenerateIcon(Vector2 position, int? id)
    {
        id ??= iconIndex++;
        var collider = UnityHelper.CreateObject<CircleCollider2D>("Icon", meetingLayer.transform, position.AsVector3(-0.01f + id.Value * -0.0001f));
        var renderer = UnityHelper.CreateObject<SpriteRenderer>("Sprite", collider.transform, new(0f, 0f, -11f));
        renderer.transform.localScale = Vector3.one * 0.8f;
        renderer.sprite = MeetingPlayerIcons.GetSprite(0);
        renderer.material = HatManager.Instance.PlayerMaterial;
        renderer.gameObject.GetOrAddComponent<MinimapScaler>();
        collider.radius = 0.3f;
        var button = collider.gameObject.SetUpButton(true, renderer, selectedColor: Color.Lerp(Color.white, Color.green, 0.2f));
        var exButton = collider.gameObject.AddComponent<ExtraPassiveBehaviour>();
        var iconInfo = new IconInfo(id.Value, renderer, button, collider);
        iconInfo.SetPlayer(GamePlayer.LocalPlayer!.PlayerId);

        exButton.OnRightClicked = () => {
            if (Input.GetMouseButton(0)) return;//左クリックとの同時押しでは消去しない

            iconInfo.ResetRelation();
            GameObject.Destroy(collider.gameObject);
            icons.Remove(iconInfo);
        };
        button.OnMouseOver.AddListener(() => {
            if (CanShowNormalOverlay) iconInfo.OpenPlayerOverlay();
        });
        button.OnClick.AddListener(() =>
        {
            if (CanShowClickOverlay) iconInfo.OpenEditorOverlay();
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        icons.Add(iconInfo);

        return iconInfo;
    }

    private LineInfo GenerateLine(LineRenderer line, int? id)
    {
        var backLine1 = GameObject.Instantiate(line, line.transform);
        var backLine2 = GameObject.Instantiate(backLine1, line.transform);
        backLine1.transform.localPosition = new(0f, 0f, 0.00001f);
        backLine2.transform.localPosition = new(0f, 0f, 0.00002f);
        backLine1.SetColors(Color.white, Color.white);
        backLine2.SetColors(Color.black, Color.black);

        line.numCapVertices = 5;
        backLine1.numCapVertices = 5;
        backLine2.numCapVertices = 5;

        var collider = line.gameObject.AddComponent<EdgeCollider2D>();
        collider.edgeRadius = 0.08f;

        Il2CppSystem.Collections.Generic.List<Vector2> points = new();
        for (int i = 0; i < line.positionCount; i++) points.Add(line.GetPosition(i));
        collider.SetPoints(points);
        var button = collider.gameObject.SetUpButton(true);
        var exButton = collider.gameObject.AddComponent<ExtraPassiveBehaviour>();

        var lineInfo = new LineInfo(id ?? lineIndex++, line, backLine1, backLine2, button, collider);
        lineInfo.SetPlayer(byte.MaxValue, true);

        exButton.OnRightClicked = () => {
            if (Input.GetMouseButton(0)) return;//左クリックとの同時押しでは消去しない

            lineInfo.ResetRelation();
            GameObject.Destroy(line.gameObject);
            lines.Remove(lineInfo);
        };
        button.OnMouseOver.AddListener(() => {
            if (CanShowNormalOverlay) lineInfo.OpenPlayerOverlay();
        });
        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
        button.OnClick.AddListener(() => {
            if (CanShowClickOverlay) lineInfo.OpenEditorOverlay();
        });
        lines.Add(lineInfo);

        return lineInfo;
    }

    private StoredInfo ToStoredInfo() => new(lines.Select(line => line.ToStoredInfo()).ToArray(), icons.Select(icon => icon.ToStoredInfo()).ToArray(), lineIndex, iconIndex);
    
    private (int prev, int next) Store()
    {
        var storedInfo = ToStoredInfo();
        if (currentPage < storedData.Count)
        {
            if (!storedInfo.IsEmpty)
            {
                storedData[currentPage] = storedInfo;
                return (Math.Max(0, currentPage - 1), currentPage + 1);
            }
            else
            {
                storedData.RemoveAt(currentPage);
                return (Math.Max(0, currentPage - 1), currentPage);
            }
        }
        else
        {
            if (!storedInfo.IsEmpty)
            {
                storedData.Add(storedInfo);
                return (Math.Max(0, storedData.Count - 2), storedData.Count);
            }
            else
            {
                return (Math.Max(0, currentPage - 1), storedData.Count);
            }
        }
    }

    private void CleanObjects()
    {
        meetingLayer.ForEachChild(obj =>
        {
            if (obj.name != "PageChanger") GameObject.Destroy(obj);
        });
    }

    private void Restore()
    {
        ResetAll();
        CleanObjects();
        currentPage = Math.Clamp(currentPage, 0, storedData.Count);
        if(currentPage < storedData.Count) Restore(storedData[currentPage]);
    }

    private void Restore(StoredInfo stored)
    {
        ResetAll();
        CleanObjects();
        lineIndex = stored.NextLineId;
        iconIndex = stored.NextIconId;

        for(int i = 0; i < stored.Lines.Length; i++)
        {
            var line = stored.Lines[i];
            var renderer = LineDrawer.Restore(meetingLayer.transform, new(0f, 0f, (line.Id + 1) * -0.0001f), line.Points, Color.gray, LayerExpansion.GetUILayer());
            var lineInfo = GenerateLine(renderer, line.Id);
            lineInfo.SetPlayer(line.PlayerId);
        }

        for (int i = 0; i < stored.Icons.Length; i++)
        {
            var icon = stored.Icons[i];
            var iconInfo = GenerateIcon(icon.Point, icon.Id);
            iconInfo.SetPlayer(icon.PlayerId);
            iconInfo.SetImageType(icon.IconType);
        }

        for (int i = 0; i < stored.Lines.Length; i++)
        {
            lines[i].RelatedIcons = new(stored.Lines[i].RelatedIcons.Select(n => icons.FirstOrDefault(icon => icon.Id == n)).NotNull());
        }

        for (int i = 0; i < stored.Icons.Length; i++)
        {
            icons[i].RelatedLines = new(stored.Icons[i].RelatedLines.Select(n => lines.FirstOrDefault(line => line.Id == n)).NotNull());
        }
    }
}