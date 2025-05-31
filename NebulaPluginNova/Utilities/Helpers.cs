using Hazel;
using InnerNet;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Nebula.Modules.GUIWidget;
using Steamworks;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine.Rendering;
using Virial;
using Virial.Events.Game;
using Virial.Game;
using Virial.Text;


namespace Nebula.Utilities;

public class Reference<T>
{
    public T? Value { get; set; } = default(T);

    public Reference<T> Set(T value)
    {
        Value = value;
        return this;
    }

    public Reference<T> Update(Func<T?,T?> update)
    {
        Value = update.Invoke(Value);
        return this;
    }

    public IEnumerator Wait()
    {
        while (Value == null) yield return null;
        yield break;
    }
}

public static class Helpers
{
    static public float FlipIf(this float value, bool flip) => flip ? -value : value;
    private static Func<string, string>? urlConverter = null;
    static public string ConvertUrl(string url)
    {
        if(urlConverter == null)
        {
            var converter = NebulaPlugin.LoaderPlugin?.GetType().GetMethod("ConvertUrl", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            if (converter != null)
                urlConverter = u => (converter.Invoke(null, [u]) as string)!;
            else
                urlConverter = u => u;
        }

        return urlConverter.Invoke(url);
    }

    public static bool AmHost(this PlayerControl player) => AmongUsClient.Instance.HostId == player.OwnerId;

    /// <summary>
    /// 確率で真偽値を返します。
    /// </summary>
    /// <param name="prob">0から1までの確率</param>
    /// <returns></returns>
    public static bool Prob(float prob) => System.Random.Shared.NextSingle() < prob;
    public static float DegToRad(this float deg) => deg * (float)Math.PI / 180f;
    public static float RadToDeg(this float rad) => rad * 180f / (float)Math.PI;
    public static float MountainCurve(float p, float max)
    {
        float x = p - 0.5f;
        return max - x * x * 4f * max;
    }

    public static float Delta(this float val, float speed, float threshold)
    {
        var smooth = val* Mathf.Clamp01(Time.deltaTime * speed);

        if (val < 0f)
            return smooth < -threshold ? smooth : Mathf.Max(val, -threshold);
        else
            return smooth > threshold ? smooth : Mathf.Min(val, threshold);
    }

    public static Vector2 Delta(this Vector2 vec, float speed, float threshold)
        => vec.normalized * vec.magnitude.Delta(speed, threshold);

    public static Vector3 Delta(this Vector3 vec, float speed, float threshold)
        => vec.normalized * new Vector2(vec.x,vec.y).magnitude.Delta(speed, threshold);

    public static Vector3 AsWorldPos(this Vector3 vec, bool isBack)
    {
        Vector3 result = vec;
        result.z = result.y / 1000f;
        if(isBack) result.z  += 0.001f;
        return result;
    }

    public static int CurrentMonth => DateTime.Now.Month;
    public static void DeleteDirectoryWithInnerFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return;
        
        string[] filePaths = Directory.GetFiles(directoryPath);
        foreach (string filePath in filePaths)
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        string[] directoryPaths = Directory.GetDirectories(directoryPath);
        foreach (string path in directoryPaths) DeleteDirectoryWithInnerFiles(path);
        
        Directory.Delete(directoryPath, false);
    }

    public static PlayerControl? GetPlayer(byte? id)
    {
        if (!id.HasValue) return null;
        return PlayerControl.AllPlayerControls.Find((Il2CppSystem.Predicate<PlayerControl>)((p) => p.PlayerId == id!));
    }

    public static DeadBody? GetDeadBody(byte id)
    {
        return AllDeadBodies().FirstOrDefault((p) => p.ParentId == id);
    }

    public static int ComputeConstantOldHash(this string str)
    {
        const long MulPrime = 467;
        const long SurPrime = 9670057;

        long val = 0;
        foreach (char c in str)
        {
            val *= MulPrime;
            val += c;
            val %= SurPrime;
        }
        return (int)(val % SurPrime);
    }

    public static int ComputeConstantHash(this string str)
    {
        const long MulPrime = 467;
        const long SurPrime = 2147283659;

        long val = 0;
        foreach (char c in str)
        {
            val *= MulPrime;
            val += c;
            val %= SurPrime;
        }
        return (int)(val % SurPrime);
    }

    public static long ComputeConstantLongHash(this string str)
    {
        const long MulPrime = 467;
        const long SurPrime = 531206959292021;

        long val = 0;
        foreach (char c in str)
        {
            val *= MulPrime;
            val += c;
            val %= SurPrime;
        }
        return (val % SurPrime);
    }

    //ハッシュ化した文字列に使用するテキスト
    private static char[] HashCharacters = [
        ..Helpers.Sequential(26).Select(i => (char)('a' + i)),
        ..Helpers.Sequential(26).Select(i => (char)('A' + i)),
        ..Helpers.Sequential(10).Select(i => (char)('0' + i))];

    public static string ComputeConstantHashAsString(this string str)
    {
        var val = str.ComputeConstantOldHash();
        StringBuilder builder = new StringBuilder();

        while (val > 0)
        {
            builder.Append((char)('a' + (val % 26)));
            val /= 26;
        }
        return builder.ToString();
    }

    public static string ComputeConstantHashAsStringLong(this string str)
    {
        var val = str.ComputeConstantLongHash();
        StringBuilder builder = new StringBuilder();

        while(val > 0)
        {
            builder.Append(HashCharacters[val % HashCharacters.Length]);
            val /= HashCharacters.Length;
        }
        return builder.ToString();
    }

    public static IEnumerable<DeadBody> AllDeadBodies()
    {
        //Componentで探すよりタグで探す方が相当はやい
        var bodies = GameObject.FindGameObjectsWithTag("DeadBody");
        for (int i = 0; i < bodies.Count; i++) yield return bodies[i].GetComponent<DeadBody>();
    }

    public static int[] GetRandomArray(int length)
    {
        var array = new int[length];
        for (int i = 0; i < length; i++) array[i] = i;
        return array.OrderBy(i => Guid.NewGuid()).ToArray();
    }

    public static T[] Shuffle<T>(this IReadOnlyList<T> array) => GetRandomArray(array.Count).Select(i => array[i]).ToArray();
    public static T[] Shuffle<T>(this IEnumerable<T> enumerable) => Shuffle(enumerable.ToArray());

    public static string GetClipboardString()
    {
        uint type = 0;
        if (ClipboardHelper.IsClipboardFormatAvailable(1U)) { type = 1U; Debug.Log("ASCII"); }
        if (ClipboardHelper.IsClipboardFormatAvailable(13U)) { type = 13U; Debug.Log("UNICODE"); }
        if (type == 0) return "";

        string result;
        try
        {
            if (!ClipboardHelper.OpenClipboard(IntPtr.Zero))
            {
                result = "";
            }
            else
            {

                IntPtr clipboardData = ClipboardHelper.GetClipboardData(type);
                if (clipboardData == IntPtr.Zero)
                    result = "";
                else
                {
                    IntPtr intPtr = IntPtr.Zero;
                    try
                    {
                        intPtr = ClipboardHelper.GlobalLock(clipboardData);
                        int len = ClipboardHelper.GlobalSize(clipboardData);

                        if (type == 1U)
                            result = Marshal.PtrToStringAnsi(clipboardData, len);
                        else
                        {
                            result = Marshal.PtrToStringUni(clipboardData) ?? "";
                        }
                    }
                    finally
                    {
                        if (intPtr != IntPtr.Zero) ClipboardHelper.GlobalUnlock(intPtr);
                    }
                }
            }
        }
        finally
        {
            ClipboardHelper.CloseClipboard();
        }
        return result;
    }

    static public int[] Sequential(int length)
    {
        var array = new int[length];
        for (int i = 0; i < length; i++) array[i] = i;
        return array;
    }

    static public T Random<T>(this T[] array)
    {
        return array[System.Random.Shared.Next(array.Length)];
    }

    static public T Random<T>(this List<T> list)
    {
        return list[System.Random.Shared.Next(list.Count)];
    }

    /// <summary>
    /// 配列の範囲内なら配列の値を返します。
    /// 範囲外ならデフォルト値を返します。例外は発しません。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    /// <param name="index"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    static public T Get<T>(this T[] array, int index, T defaultValue) {
        if (index < array.Length && index >= 0) return array[index];
        return defaultValue;
    }

    static public bool GetAsBool(this int[] array, int index, bool defaultValue) => Get(array, index, defaultValue ? 1 : 0) == 1;
    static public bool GetAsBool(this int[] array, int index) => Get(array, index, 0) == 1;
    static public int AsInt(this bool value) => value ? 1 : 0;
    /// <summary>
    /// リストの範囲内ならリストの値を返します。
    /// 範囲外ならデフォルト値を返します。例外は発しません。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="array"></param>
    /// <param name="index"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    static public T Get<T>(this List<T> array, int index, T defaultValue)
    {
        if (index < array.Count && index >= 0) return array[index];
        return defaultValue;
    }

    //指定の大きさまでQueueを小さくしてから要素を取り出します。呼び出し後のQueueの要素数は最大でexpectedSize - 1になります。
    static public T DequeAt<T>(this Queue<T> queue, int expectedSize)
    {
        while (queue.Count > expectedSize) queue.Dequeue();
        return queue.Dequeue();
    }

    static public bool CircleContainsAnyNonTriggers(Vector2 pos, float radious, int? layerMask = null)
    {
        int num = Physics2D.OverlapCircleNonAlloc(pos, radious, PhysicsHelpers.colliderHits, layerMask ?? Constants.ShipAndAllObjectsMask);
        for (int i = 0; i < num; i++)
        {
            if (!PhysicsHelpers.colliderHits[i].isTrigger) return true;
        }
        return false;
    }

    static public bool AnyNonTriggersBetween(Vector2 pos1, Vector2 pos2, out Vector2 vector, int? layerMask = null)
    {
        layerMask ??= Constants.ShipAndAllObjectsMask;
        vector = pos2 - pos1;
        return PhysicsHelpers.AnyNonTriggersBetween(pos1, vector.normalized, vector.magnitude, layerMask!.Value);
    }

    static public bool AnyCustomNonTriggersBetween(Vector2 pos1, Vector2 pos2, Predicate<Collider2D> predicate, int? layerMask = null)
    {
        layerMask ??= Constants.ShipAndAllObjectsMask;
        var vector = pos2 - pos1;
        
        int num = Physics2D.RaycastNonAlloc(pos1, vector.normalized, PhysicsHelpers.castHits, vector.magnitude, layerMask!.Value);
        bool result = false;
        for (int i = 0; i < num; i++)
        {
            if (!PhysicsHelpers.castHits[i].collider.isTrigger && predicate.Invoke(PhysicsHelpers.castHits[i].collider))
            {
                result = true;
                break;
            }
        }
        return result;
    }

    static public IEnumerator DoTransitionFadeOut(this TransitionFade transitionFade)
    {
        yield return Effects.ColorFade(transitionFade.overlay, Color.clear, Color.black, 0.2f);
    }

    static public IEnumerator DoTransitionFadeIn(this TransitionFade transitionFade)
    {
        transitionFade.overlay.color = Color.clear;
        yield break;
    }

    static public bool IsEmpty<T>(this IEnumerable<T> enumerable) => !enumerable.Any(_ => true);
    static public bool IsEmpty<T>(this IReadOnlyList<T> list) => list.Count == 0;

    static public void DoIf<T>(this T? nullableObj, Action<T> action)
    {
        if(nullableObj != null) action.Invoke(nullableObj!);
    }

    static public Transform FindChild(this Transform transform, Predicate<Transform> predicate)
    {
        int num =transform.GetChildCount();
        for(int i = 0; i < num; i++)
        {
            var child = transform.GetChild(i);
            if (predicate.Invoke(child)) return child;
        }
        return null!;
    }

    /*
    static public void SyncSingleNetObject(InnerNetObject obj)
    {
        MessageWriter messageWriter = MessageWriter.Get(obj.sendMode);
        messageWriter.StartMessage(1);
        messageWriter.WritePacked(obj.NetId);
        try
        {
            if (obj.Serialize(messageWriter, false))
            {
                messageWriter.EndMessage();
                AmongUsClient.Instance.SendOrDisconnect(messageWriter);
                messageWriter.Recycle();
            }
            else messageWriter.CancelMessage();

            if (obj.Chunked && obj.IsDirty) return;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex.ToString());
            messageWriter.CancelMessage();
        }
    }
    */

    static public void PlayKillStingerSE()
    {
        var vanillaAnim = HudManager.Instance.KillOverlay.KillAnims[0];
        SoundManager.Instance.PlaySound(vanillaAnim.Stinger, false, 1f, null).volume = vanillaAnim.StingerVolume;
    }

    static public void RefreshMemory()
    {
        Resources.UnloadUnusedAssets();
        GC.Collect();
    }

    static public TMPro.TextMeshPro TextHudContent(string name, ILifespan lifespan, Action<TMPro.TextMeshPro> updater)
    {
        var TextHolder = HudContent.InstantiateContent(name, true, true, false, false);
        lifespan.BindGameObject(TextHolder.gameObject);

        TextMeshPro tmPro = null!;
        var text = new NoSGUIText(Virial.Media.GUIAlignment.Left, new(GUI.API.GetAttribute(Virial.Text.AttributeParams.StandardBaredBoldLeftNonFlexible)) { Alignment = Virial.Text.TextAlignment.BottomLeft, FontSize = new(1.6f), Size = new(3f, 1f) }, new RawTextComponent("")) { PostBuilder = t => { tmPro = t; tmPro.sortingOrder = 0; } };
        text.Instantiate(new Virial.Media.Anchor(new(0f, 0f), new(-0.5f, -0.5f, 0f)), new(20f, 20f), out _)!.transform.SetParent(TextHolder.transform, false);

        GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev => {
            if (tmPro)
            {
                updater.Invoke(tmPro);
            }
        }, lifespan);

        return tmPro;
    }

    static public Vector3 TransformPointLocalToLocal(this Transform transform, Vector3 vector, Transform toTransform)
    {
        return toTransform.InverseTransformPoint(transform.TransformPoint(vector));
    }

    internal static void SetColors(ArchivedColor color, SpriteRenderer renderer)
    {
        var material = renderer.material;

        material.SetColor(PlayerMaterial.BackColor, color.ShadowColor.ToUnityColor());
        material.SetColor(PlayerMaterial.BodyColor, color.MainColor.ToUnityColor());
        material.SetColor(PlayerMaterial.VisorColor, color.VisorColor.ToUnityColor());
    }

    public static int Round(int number, int round)
    {
        int num = number + (round / 2);
        return num - (num % round);
    }

    public static void ToLab(this UnityEngine.Color color, out float L, out float a, out float b)
    {
        const float xr = 0.4124f;
        const float xg = 0.3576f;
        const float xb = 0.1805f;
        const float yr = 0.2126f;
        const float yg = 0.7152f;
        const float yb = 0.0722f;
        const float zr = 0.0193f;
        const float zg = 0.1192f;
        const float zb = 0.9505f;

        const float Xn = xr + xg + xb;
        const float Yn = yr + yg + yb;
        const float Zn = zr + zg + zb;

        const float t = 6f / 29f;
        const float t2 = t * t;
        const float t3 = t * t * t;
        var X = xr * color.r + xg * color.g + xb * color.b;
        var Y = yr * color.r + yg * color.g + yb * color.b;
        var Z = zr * color.r + zg * color.g + zb * color.b;

        var tx = X / Xn;
        var ty = Y / Yn;
        var tz = Z / Zn;
        
        float fx = tx > t3 ? Mathf.Pow(tx, 0.333f) : tx / 3f / t2 + 4f / 29f;
        float fy = ty > t3 ? Mathf.Pow(ty, 0.333f) : ty / 3f / t2 + 4f / 29f;
        float fz = tz > t3 ? Mathf.Pow(tz, 0.333f) : tz / 3f / t2 + 4f / 29f;

        L = 116 * fy - 16;
        a = 500 * (fx - fy);
        b = 200 * (fy - fz);
    }

    public static IEnumerable<T> NotNull<T>(this IEnumerable<T?> enumerable) => enumerable.Where(val => val != null)!;
}
