using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Configuration;
using Virial.Runtime;

namespace Nebula.Configuration;



/// <summary>
/// ゲーム内設定のオプション値を管理します。
/// </summary>
[NebulaPreprocess(PreprocessPhase.FixStructureConfig)]
[NebulaRPCHolder]
internal static class ConfigurationValues
{
    /// <summary>
    /// オプション値の保存先
    /// </summary>
    static public readonly DataSaver ConfigurationSaver = new("Config");

    /// <summary>
    /// 全オプション
    /// </summary>
    static internal List<ISharableEntry> AllEntries = new();
    static internal List<Action> Reloaders = [];
    static internal StringDataEntry PresetName = new("preset", ConfigurationSaver, "");
    static internal string CurrentPresetName = "";

    static private bool IsFixed = false;
    static internal void RegisterEntry(ISharableEntry entry)
    {
        if (IsFixed) Debug.LogError($"Bad Register Action! Name:{entry.Name}");
        AllEntries.Add(entry);
    }

    /// <summary>
    /// オプションの値の共有を遅らせるためのブロッカー
    /// </summary>
    static private ConfigurationUpdateBlocker? CurrentBlocker = null;
    /// <summary>
    /// 値の共有を遅延させられているエントリーのID
    /// </summary>
    static HashSet<int> ChangedEntryIdList = [];
    
    /// <summary>
    /// 値の共有を遅延させるブロッカー
    /// </summary>
    internal class ConfigurationUpdateBlocker : IDisposable
    {
        public ConfigurationUpdateBlocker()
        {
            CurrentBlocker ??= this;
        }

        public void Dispose()
        {
            if (CurrentBlocker == this)
            {
                CurrentBlocker = null;
                using (RPCRouter.CreateSection("ShareConfigurationValue"))
                {
                    ChangedEntryIdList.Do(e => RpcShare.Invoke((e, AllEntries[e].RpcValue)));
                }
                ChangedEntryIdList.Clear();
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
        ConfigurationSaver.TrySave();
    }

    static public void RestoreAll()
    {
        Reloaders.Do(a => a.Invoke());
        foreach (var entry in AllEntries) entry.RestoreSavedValue();
        CurrentPresetName = PresetName.Value;
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
    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        Patches.LoadPatch.LoadingText = "Building Configuration Database";
        yield return null;

        AllEntries.Sort((c1, c2) => string.Compare(c1.Name, c2.Name));

        for (int i = 0; i < AllEntries.Count; i++) AllEntries[i].Id = i;

        Debug.Log("All NoS Config Entries: " + AllEntries.Count);
        IsFixed = true;

    }

    /// <summary>
    /// ゲーム内のプレイヤーとプリセットの値を共有します。
    /// </summary>
    static public RemoteProcess<string> RpcSharePresetName = new(
        "SharePresetName",
       (message, isCalledByMe) =>
       {
           if (isCalledByMe) PresetName.Value = message;
           CurrentPresetName = message;
       }
    );

    /// <summary>
    /// ゲーム内のプレイヤーとオプションの値を共有します。
    /// </summary>
    static private readonly RemoteProcess<(int id, int value)> RpcShare = new(
        "ShareOption",
       (message, isCalledByMe) =>
       {
           if (!isCalledByMe) AllEntries[message.id].RpcValue = message.value;
           HelpScreen.OnUpdateOptions();
           if (isCalledByMe) PresetName.Value = "";
           CurrentPresetName = "";
           GameOptionsManager.Instance.currentGameOptions.SetInt(AmongUs.GameOptions.Int32OptionNames.RulePreset, (int)AmongUs.GameOptions.RulesPresets.Custom);
       }
    );

    /// <summary>
    /// ゲーム内のプレイヤーと全オプションの値を共有します。
    /// </summary>
    static private readonly DivisibleRemoteProcess<int, Tuple<int, int>> RpcShareAll = new(
        "ShareAllOption",
        (message) =>
        {
            //(Item1)番目から(Item2)-1番目まで
            static IEnumerator<Tuple<int, int>> GetDivider()
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
           Debug.Log($"Received Share All Option RPC, Index: {index}, Num: {num}");
           for (int i = 0; i < num; i++)
           {
               int value = reader.ReadInt32();
               try
               {
                   AllEntries[index + i].RpcValue = value;
               }catch(Exception ex)
               {
                   Debug.LogError($"RPC Set Error (ID: {index + i}, Name: {AllEntries[index + i].Name}, Value: {value})\n" + ex.ToString());
               }
           }
           return new Tuple<int, int>(index, num);
       },
       (message, isCalledByMe) =>
       {
           HelpScreen.OnUpdateOptions();
       }
    );
}
