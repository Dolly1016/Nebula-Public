using System.Runtime.InteropServices;
using System.Text;


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
    public static bool AmHost(this PlayerControl player) => AmongUsClient.Instance.HostId == player.OwnerId;

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

    static public T Get<T>(this T[] array, int index, T defaultValue) {
        if (index < array.Length) return array[index];
        return defaultValue;
    }

    static public T Get<T>(this List<T> array, int index, T defaultValue)
    {
        if (index < array.Count) return array[index];
        return defaultValue;
    }

    //指定の大きさまでQueueを小さくしてから要素を取り出します。呼び出し後のQueueの要素数は最大でexpectedSize - 1になります。
    static public T DequeAt<T>(this Queue<T> queue, int expectedSize)
    {
        while (queue.Count > expectedSize) queue.Dequeue();
        return queue.Dequeue();
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
}
