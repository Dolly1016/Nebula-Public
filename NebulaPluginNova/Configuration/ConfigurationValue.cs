using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;

namespace Nebula.Configuration;



/// <summary>
/// ゲーム内設定のオプション値を管理します。
/// </summary>
[NebulaPreLoad(typeof(Roles.Roles))]
[NebulaRPCHolder]
internal static class ConfigurationValues
{
    /// <summary>
    /// オプション値の保存先
    /// </summary>
    static public DataSaver ConfigurationSaver = new DataSaver("Config");

    /// <summary>
    /// 全オプション
    /// </summary>
    static internal List<ISharableEntry> AllEntries = new();

    /// <summary>
    /// オプションの値の共有を遅らせるためのブロッカー
    /// </summary>
    static private ConfigurationUpdateBlocker? CurrentBlocker = null;
    /// <summary>
    /// 値の共有を遅延させられているエントリーのID
    /// </summary>
    static HashSet<int> ChangedEntryIdList = new();

    /// <summary>
    /// 値の共有を遅延させるブロッカー
    /// </summary>
    internal class ConfigurationUpdateBlocker : IDisposable
    {
        public ConfigurationUpdateBlocker()
        {
            if (CurrentBlocker == null) CurrentBlocker = new ConfigurationUpdateBlocker();
        }

        public void Dispose()
        {
            if (CurrentBlocker == this)
            {
                using (RPCRouter.CreateSection("ShareConfigurationValue"))
                {
                    ChangedEntryIdList.Do(e => RpcShare.Invoke((e, AllEntries[e].RpcValue)));
                }
                ChangedEntryIdList.Clear();

                CurrentBlocker = null;
            }
        }
    }

    static public void TryShareOption(ISharableEntry entry)
    {
        if (CurrentBlocker != null)
            ChangedEntryIdList.Add(entry.Id);
        else
            RpcShare.Invoke((entry.Id, entry.RpcValue));
    }

    static public void ShareAll()
    {
        RpcShareAll.Invoke(0); //引数の値は破棄される(未使用)
        ChangedEntryIdList.Clear();
    }

    static public void FlushLocal()
    {
        ConfigurationSaver.Save();
    }

    /// <summary>
    /// オプションの値を書き換えられるかどうかを調べ、必要に応じて例外を投げます。
    /// </summary>
    /// <returns>書き換えられる場合、trueを返します。</returns>
    /// <exception cref="InvalidOperationException">ゲームが既に開始しているか、クライアントがホストではありません。</exception>
    static public bool AssertOnChangeOptionValue()
    {
        if (AmongUsClient.Instance && AmongUsClient.Instance.AmHost && AmongUsClient.Instance.GameState < InnerNet.InnerNetClient.GameStates.Started) return true;
        throw new InvalidOperationException("You don't have the permission to change option values.");
    }

    /// <summary>
    /// すべてのエントリにIDを割り振ります。
    /// </summary>
    /// <returns></returns>
    static public IEnumerator CoLoad()
    {
        Patches.LoadPatch.LoadingText = "Building Configuration Database";
        yield return null;

        AllEntries.Sort((c1, c2) => string.Compare(c1.Name, c2.Name));

        for (int i = 0; i < AllEntries.Count; i++) AllEntries[i].Id = i;
    }

    /// <summary>
    /// ゲーム内のプレイヤーとオプションの値を共有します。
    /// </summary>
    static private RemoteProcess<(int id, int value)> RpcShare = new(
        "ShareOption",
       (message, isCalledByMe) =>
       {
           if (!isCalledByMe) AllEntries[message.id].RpcValue = message.value;
       }
    );

    /// <summary>
    /// ゲーム内のプレイヤーと全オプションの値を共有します。
    /// </summary>
    static private DivisibleRemoteProcess<int, Tuple<int, int>> RpcShareAll = new DivisibleRemoteProcess<int, Tuple<int, int>>(
        "ShareAllOption",
        (message) =>
        {
            //(Item1)番目から(Item2)-1番目まで
            IEnumerator<Tuple<int, int>> GetDivider()
            {
                int done = 0;
                while (done < AllEntries.Count)
                {
                    int max = Mathf.Min(AllEntries.Count, done + 100);
                    yield return new Tuple<int, int>(done, max);
                    done = max;
                }
            }
            return GetDivider();
        },
        (writer, message) =>
        {
            writer.Write(message.Item1);
            writer.Write(message.Item2 - message.Item1);
            for (int i = message.Item1; i < message.Item2; i++) writer.Write(AllEntries[i].RpcValue);
        },
       (reader) =>
       {
           int index = reader.ReadInt32();
           int num = reader.ReadInt32();
           for (int i = 0; i < num; i++)
           {
               AllEntries[index].RpcValue = reader.ReadInt32();
               index++;
           }
           return new Tuple<int, int>(0, 0);
       },
       (message, isCalledByMe) =>
       {
       }
    );
}
