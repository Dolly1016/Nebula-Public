using Il2CppSystem.Reflection.Internal;
using Mono.CSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;


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

    public static int ComputeConstantHash(this string str)
    {
        const long MulPrime = 467;
        const int SurPrime = 9670057;

        long val = 0;
        foreach (char c in str)
        {
            val *= MulPrime;
            val += c;
            val %= SurPrime;
        }
        return (int)(val % SurPrime);
    }

    public static string ComputeConstantHashAsString(this string str)
    {
        var val = str.ComputeConstantHash();
        StringBuilder builder = new StringBuilder();
        while(val > 0)
        {
            builder.Append((char)('a' + (val % 26)));
            val /= 26;
        }
        return builder.ToString();
    }

    public static DeadBody[] AllDeadBodies()
    {
        //Componentで探すよりタグで探す方が相当はやい
        var bodies = GameObject.FindGameObjectsWithTag("DeadBody");
        DeadBody[] deadBodies = new DeadBody[bodies.Count];
        for (int i = 0; i < bodies.Count; i++) deadBodies[i] = bodies[i].GetComponent<DeadBody>();
        return deadBodies;
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
}
