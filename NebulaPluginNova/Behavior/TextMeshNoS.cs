using Il2CppInterop.Runtime.Injection;
using Il2CppSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Virial.Text;
using static Il2CppSystem.Uri;
using static Nebula.Behavior.FontAssetNoSInfo;

namespace Nebula.Behavior;

public class FontAssetNoSInfo
{
    public class FontAssetNoSCharacterInfo
    {
        [JsonSerializableField]
        public char Character;

        [JsonSerializableField]
        public int Index;

        [JsonSerializableField]
        public float Ratio = 1f;
    }

    [JsonSerializableField]
    public int X;
    [JsonSerializableField]
    public int Y;
    [JsonSerializableField]
    public float DefaultHeight;
    [JsonSerializableField]
    public float? WhitespaceWidth = null;
    [JsonSerializableField]
    public int? LowercaseSequenceBegin = null;
    [JsonSerializableField]
    public int? UppercaseSequenceBegin = null;
    [JsonSerializableField]
    public int? NumberSequenceBegin = null;

    [JsonSerializableField]
    public List<FontAssetNoSCharacterInfo> Characters = [];
}

public class FontAssetNoS
{
    public record CharacterInfo(float top, float bottom, float left, float right, float extraWidth)
    {
        public bool IsWhitespace => top < 0f;
    }
    ITextureLoader texture;
    Dictionary<char, CharacterInfo> allCharacters = new();
    public float LineHeight { get; private init; }
    public Texture2D Texture => texture.GetTexture();
    public void RegisterCharacter(char character, CharacterInfo info) => allCharacters[character] = info;
    public void RegisterCharacterAsWhitespace(char character, float width) => RegisterCharacter(character, new(-1f, -1f, -1f, -1f, width));
    public CharacterInfo? GetCharacter(char character) => allCharacters.TryGetValue(character,out var result) ? result : null;

    public FontAssetNoS(float lineHeight, ITextureLoader texture)
    {
        LineHeight = lineHeight;
        this.texture = texture;
    }

    public FontAssetNoS(FontAssetNoSInfo info, ITextureLoader texture)
    {
        LineHeight = info.DefaultHeight;
        if (info.WhitespaceWidth.HasValue) RegisterCharacterAsWhitespace(' ', info.WhitespaceWidth.Value);

        float halfWidth = 1f / (float)info.X * 0.5f;
        float halfHeight = 1f / (float)info.Y * 0.5f;

        void Register(int index, float ratio, char character)
        {
            int x = (index % info.X);
            int y = (index / info.X);
            float centerX = (x + 0.5f) / (float)info.X;
            float centerY = 1f - (y + 0.5f) / (float)info.Y;

            RegisterCharacter(character, new(centerY + halfHeight, centerY - halfHeight, centerX - halfWidth * ratio, centerX + halfWidth * ratio, 0f));
        }

        if (info.LowercaseSequenceBegin.HasValue) for (int i = 0; i < 26; i++) Register(info.LowercaseSequenceBegin.Value + i, 1f, (char)((int)'a' + i));
        if (info.UppercaseSequenceBegin.HasValue) for (int i = 0; i < 26; i++) Register(info.UppercaseSequenceBegin.Value + i, 1f, (char)((int)'A' + i));
        if (info.NumberSequenceBegin.HasValue) for (int i = 0; i < 10; i++) Register(info.NumberSequenceBegin.Value + i, 1f, (char)((int)'0' + i));
        foreach (var chara in info.Characters) Register(chara.Index, chara.Ratio, chara.Character);
        this.texture = texture;
    }
}

public class TextMeshNoS : MonoBehaviour
{
    static TextMeshNoS() => ClassInjector.RegisterTypeInIl2Cpp<TextMeshNoS>();
    private MeshRenderer myRenderer;
    private MeshFilter myFilter;
    private Mesh myMesh;
    private string currentText = "";
    private string newText = "";
    private bool requiringRebuild = false;
    public float FontSize { get; set; } = 1f;
    /// <summary>
    /// 行の間の余白
    /// </summary>
    public float LineInterval { get; set; } = 0f;
    /// <summary>
    /// 字の間の余白
    /// </summary>
    public float CharacterInterval { get; set; } = 0f;

    public string Text { get => newText; set { newText = value;  requiringRebuild = true; } }
    public Virial.Text.TextAlignment TextAlignment { get; set; } = Virial.Text.TextAlignment.Left;
    public Vector2 Pivot { get; set; } = new(0.5f, 0.5f);
    public FontAssetNoS Font { get; set; } = null!;
    public Color Color { get => myRenderer.material.color; set => myRenderer.material.color = value; }
    public Material Material { get => myRenderer.material; set => myRenderer.material = value; }
    private void Initialize()
    {
        myRenderer = gameObject.AddComponent<MeshRenderer>();
        myFilter = gameObject.AddComponent<MeshFilter>();
        myRenderer.material = UnityHelper.GetMeshRendererMaterial();
        myMesh = new();
        myFilter.mesh = myMesh;
        myRenderer.sortingGroupOrder = 10;
    }

    private void RebuildMesh()
    {
        if (Font == null) return;

        myRenderer.material.mainTexture = Font.Texture;

        //改行文字をいい感じに改変
        var text = currentText.Replace("<br>", "\n").Replace("\r\n", "\n").Replace("\r", "");

        //まずは0,0を左上隅として計算する (Pivot = (0,1)のときと同じ条件)
        List<List<Vector3>> posList = new();
        List<Vector2> uvList = new();
        List<int> indexList = new();

        List<Vector3> currentPosList;

        void MoveNextLine()
        {
            currentPosList = new();
            posList.Add(currentPosList);
        }

        var x = 0f;

        MoveNextLine();//先頭の行へ

        Texture tex = myRenderer.material.mainTexture;
        float texRatio = (float)tex.width / (float)tex.height;
        foreach (var c in text)
        {
            if(c == '\n')
            {
                //次の行へ
                x = 0f;
                MoveNextLine();
            }
            else
            {
                var info = Font.GetCharacter(c);
                if(info != null)
                {
                    if (!info.IsWhitespace)
                    {
                        int offset = currentPosList.Count;
                        
                        //Yを基準に大きさを決定する。
                        
                        float charaY = Font.LineHeight * FontSize;
                        float charaX = (info.right - info.left) / (info.top - info.bottom) * texRatio * Font.LineHeight * FontSize;

                        currentPosList.AddRange([new(x, 0f), new(x + charaX, 0f), new(x + charaX, -charaY), new(x, -charaY)]);
                        uvList.AddRange([new(info.left, info.top), new(info.right, info.top), new(info.right, info.bottom), new(info.left, info.bottom)]);
                        indexList.AddRange([offset, offset + 1, offset + 2, offset + 2, offset + 3, offset]);
                        x += charaX + info.extraWidth * FontSize;
                    }
                    else
                    {
                        x += info.extraWidth * FontSize;
                    }
                }
                //余白分だけ右に進める
                x += CharacterInterval;
            }
        }

        while(posList.Count > 0 && posList[posList.Count - 1].Count == 0) posList.RemoveAt(posList.Count - 1); //末尾が空の行ならば削除

        //文字列が空なら描画を取りやめて何もしない。
        if (posList.Count == 0)
        {
            myRenderer.enabled = false;
            return;
        }
        myRenderer.enabled = true;

        //枠の大きさを計算
        float width = posList.Max(list => list.Count == 0 ? 0f : list[list.Count - 2].x);//最後から一つ前の要素が右端の座標を持っている
        float height = Font.LineHeight * FontSize * posList.Count + LineInterval * (posList.Count - 1);

        Vector2 additionByPivot = new(-Pivot.x * width, (1f - Pivot.y) * height); //Pivotによる位置のずれ

        int lines = 0;
        foreach (var list in posList)
        {
            if (list.Count == 0) continue;
            var currentWidth = list[list.Count - 2].x;//この行の幅

            float additionByAlignment = 0f;
            //ビットフラグ 0x1 左, 0x2 中央, 0x4 右
            if (((int)TextAlignment | 0x2) != 0)
            {
                //中央揃えの場合
                additionByAlignment = (width - currentWidth) * 0.5f;
            }
            else if (((int)TextAlignment | 0x4) != 0)
            {
                //右揃えの場合
                additionByAlignment = width - currentWidth;
            }

            for (int i = 0; i < list.Count; i++) list[i] += new Vector3(additionByPivot.x + additionByAlignment, additionByPivot.y + lines * (Font.LineHeight * FontSize + LineInterval));
        }

        IEnumerable<Vector3>? enumerable = null;
        posList.Do(list => enumerable = enumerable == null ? list : enumerable.Concat(list));

        myFilter.mesh.Clear();
        myFilter.mesh.SetVertices((enumerable ?? []).ToArray());
        myFilter.mesh.SetUVs(0, uvList.ToArray());
        myFilter.mesh.SetIndices(indexList.ToArray(), MeshTopology.Triangles, 0);
    }

    public void Awake()
    {
        Initialize();
    }

    public void LateUpdate()
    {
        if (requiringRebuild)
        {
            currentText = newText;
            requiringRebuild = false;
            RebuildMesh();
        }
    }
}
