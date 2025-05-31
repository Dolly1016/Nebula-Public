using Hazel;
using Il2CppSystem.Reflection.Internal;
using InnerNet;
using Nebula.Scripts;
using System.Reflection;
using System.Runtime.CompilerServices;
using Virial.Assignable;
using Virial.Game;
using Virial.Runtime;
using Virial.Text;

namespace Nebula.Modules;


[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class NebulaRPCHolder : Attribute
{

}


public class NebulaRPCInvoker
{

    Action<MessageWriter> sender;
    Action localBodyProcess;
    int hash;
    public bool IsDummy { get; private set; }
    
    public NebulaRPCInvoker(int hash, Action<MessageWriter> sender, Action localBodyProcess)
    {
        this.hash = hash;
        this.sender = sender;
        this.localBodyProcess = localBodyProcess;
        this.IsDummy = false;
    }

    public NebulaRPCInvoker(Action localAction)
    {
        this.hash = 0;
        this.sender = null!;
        this.localBodyProcess = localAction;
        this.IsDummy = true;
    }

    public void Invoke(MessageWriter writer)
    {
        writer.Write(hash);
        sender.Invoke(writer);
        localBodyProcess.Invoke();
    }

    /// <summary>
    /// RPCプロセスやダミーのRPCプロセスを送信します。
    /// 送信のタイミングはRouterの都合に準じます。
    /// </summary>
    public void InvokeSingle()
    {
        if (IsDummy)
            RPCRouter.SendDummy(localBodyProcess);
        else
            RPCRouter.SendRpc("Invoker", hash, (writer) => sender.Invoke(writer), () => localBodyProcess.Invoke(), true);
    }

    /// <summary>
    /// Routerの都合を無視して、プロセスをローカルでのみ実行します。
    /// </summary>
    public void InvokeLocal()
    {
        localBodyProcess.Invoke();
    }
}

public static class RPCRouter
{
    public class RPCSection : IDisposable
    {
        public string Name;
        public void Dispose()
        {
            if (currentSection != this) return;

            currentSection = null;
            //NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"End Evacuating Rpcs ({Name}, Size = {evacuateds.Count})");

            var rpcArray = evacuateds.ToArray();
            evacuateds.Clear();
            CombinedRemoteProcess.CombinedRPC.Invoke(rpcArray);
            
        }

        public RPCSection(string? name = null)
        {
            Name = name ?? "Untitled";
            if (currentSection == null)
            {
                currentSection = this;
                //NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log,  $"Start Evacuating Rpcs ({Name})");
            }else {
                //NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Rpc section \"{Name}\" is in \"{currentSection.Name}\"! It is ignored.");
            }
        }
    }

    static public RPCSection CreateSection(string? label = null) => new RPCSection(label);

    static RPCSection? currentSection = null;
    static List<NebulaRPCInvoker> evacuateds = new();

    public static void SendDummy(Action localProcess)
    {
        if (currentSection == null) localProcess.Invoke();
        else
        {
            evacuateds.Add(new(localProcess));
        }
    }

    public static void SendRpc(string name, int hash, Action<MessageWriter> sender, Action localBodyProcess, bool shouldBeReliable) {
        if(currentSection == null)
        {
            MessageWriter writer;

            writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, 128, shouldBeReliable ? SendOption.Reliable : SendOption.None, -1);
            writer.Write(hash);
            sender.Invoke(writer);
            //NebulaPlugin.Log.Print("sent RPC:" + name + "(size:" + writer.Length + ")");

            AmongUsClient.Instance.FinishRpcImmediately(writer);


            try
            {
                localBodyProcess.Invoke();
            }
            catch(Exception ex)
            {
                NebulaPlugin.Log.PrintWithBepInEx(NebulaLog.LogLevel.Error, NebulaLog.LogCategory.System, $"Error in RPC(Invoke: {name})\n" + ex.ToString());
            }

            //NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Called RPC : {name}");
        }
        else
        {
            evacuateds.Add(new(hash, sender, localBodyProcess));

            //NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, $"Evacuated RPC : {name} (by {currentSection!.Name})");
        }
    }
}

[NebulaPreprocess(PreprocessPhase.FixStructure)]
public class RemoteProcessBase
{
    static public Dictionary<int, RemoteProcessBase> AllNebulaProcess = new();


    public int Hash { get; private set; } = -1;
    public string Name { get; private set; }
    public bool ShouldBeReliable { get; private init; } = true;

    public RemoteProcessBase(string name, bool shouldBeReliable)
    {
        Hash = name.ComputeConstantHash();
        Name = name;

        if (AllNebulaProcess.ContainsKey(Hash)) NebulaPlugin.Log.Print(NebulaLog.LogLevel.FatalError, NebulaLog.LogCategory.System, name + " is duplicated. (" + Hash + ")");
        else NebulaPlugin.Log.Print(NebulaLog.LogLevel.Log, NebulaLog.LogCategory.System, name + " is registered. (" + Hash + ")");

        AllNebulaProcess[Hash] = this;
        ShouldBeReliable = shouldBeReliable;
    }

    static Dictionary<string, RemoteProcess<object[]>> harmonyRpcMap = new();
    static private void WrapRpcMethod(Harmony harmony, MethodInfo method)
    {
        //元の静的メソッドをコピーしておく
        var copiedOriginal = harmony.Patch(method);

        //メソッド呼び出しのパラメータを取得
        var parameters = method.GetParameters();

        //RPCを定義し、登録

        List<(Action<MessageWriter, object> writer, Func<MessageReader, object> reader)> processList = new();
        if (!method.IsStatic) processList.Add(RemoteProcessAsset.GetProcess(method.DeclaringType!));
        processList.AddRange(parameters.Select(p => RemoteProcessAsset.GetProcess(p.ParameterType)));
        
        RemoteProcess<object[]> rpc = new(method.DeclaringType!.FullName + "." + method.Name,
            (writer, args) =>
            {
                for (int i = 0; i < processList.Count; i++) processList[i].writer(writer, args[i]);
            },
            (reader) =>
            {
                return processList.Select(p => p.reader(reader)).ToArray();
            },
            (args, _) =>
            {
                copiedOriginal.Invoke(null, args);
            }
            );
        harmonyRpcMap[method.DeclaringType!.FullName + "." + method.Name] = rpc;

        //静的メソッドをRPC呼び出しに変更
        static bool RpcPrefix(object? __instance, object[] __args, MethodBase __originalMethod)
        {
            var name = __originalMethod.DeclaringType!.FullName + "." + __originalMethod.Name;
            if (!__originalMethod.IsStatic) __args = __args.Prepend(__instance!).ToArray();
            harmonyRpcMap[name].Invoke(__args);
            return false;
        }
        
        var prefixInfo = RpcPrefix;
        
        var newMethod = harmony.Patch(method, new HarmonyMethod(prefixInfo.Method));
    }

    static IEnumerator Preprocess(NebulaPreprocessor preprocessor)
    {
        yield return preprocessor.SetLoadingText("Building Remote Procedure Calls");

        var types = Assembly.GetAssembly(typeof(RemoteProcessBase))?.GetTypes().Where((type) => type.IsDefined(typeof(NebulaRPCHolder)));
        if (types == null) yield break;

        foreach (var type in types)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            foreach(var method in type.GetMethods().Where(m => m.IsDefined(typeof(NebulaRPC)))) WrapRpcMethod(NebulaPlugin.Harmony, method);
        }

        //全アドオンに対してRPCをセットアップ
        foreach (var script in AddonScriptManager.ScriptAssemblies)
        {
            foreach (var t in script.Assembly.GetTypes())
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(t.TypeHandle);
                foreach (var method in t.GetMethods().Where(m => m.IsDefined(typeof(NebulaRPC))))
                {
                    //RPCを持つならハンドシェイクが必要
                    script.Addon.MarkAsNeedingHandshake();
                    WrapRpcMethod(NebulaPlugin.Harmony, method);
                }
            }
        }
    }

    public virtual void Receive(MessageReader reader) { }
}



public static class RemoteProcessAsset
{
    private static Dictionary<Type, (Action<MessageWriter, object>, Func<MessageReader, object>)> defaultProcessDic = new();

    static RemoteProcessAsset()
    {
        defaultProcessDic[typeof(byte)] = ((writer, obj) => writer.Write((byte)obj), (reader) => reader.ReadByte());
        defaultProcessDic[typeof(short)] = ((writer, obj) => writer.Write((short)obj), (reader) => reader.ReadInt16());
        defaultProcessDic[typeof(int)] = ((writer, obj) => writer.Write((int)obj), (reader) => reader.ReadInt32());
        defaultProcessDic[typeof(uint)] = ((writer, obj) => writer.Write((uint)obj), (reader) => reader.ReadUInt32());
        defaultProcessDic[typeof(ulong)] = ((writer, obj) => writer.Write((ulong)obj), (reader) => reader.ReadUInt64());
        defaultProcessDic[typeof(float)] = ((writer, obj) => writer.Write((float)obj), (reader) => reader.ReadSingle());
        defaultProcessDic[typeof(bool)] = ((writer, obj) => writer.Write((bool)obj), (reader) => reader.ReadBoolean());
        defaultProcessDic[typeof(string)] = ((writer, obj) => writer.Write((string)obj), (reader) => reader.ReadString());
        defaultProcessDic[typeof(Vector2)] = ((writer, obj) => { var vec = (Vector2)obj; writer.Write(vec.x); writer.Write(vec.y); }, (reader) => new Vector2(reader.ReadSingle(), reader.ReadSingle()));
        defaultProcessDic[typeof(Vector3)] = ((writer, obj) => { var vec = (Vector3)obj; writer.Write(vec.x); writer.Write(vec.y); writer.Write(vec.z); }, (reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        defaultProcessDic[typeof(Vector4)] = ((writer, obj) => { var vec = (Vector4)obj; writer.Write(vec.x); writer.Write(vec.y); writer.Write(vec.z); writer.Write(vec.w); }, (reader) => new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        defaultProcessDic[typeof(Vector2[])] = ((writer, obj) => { 
            var ary = (Vector2[])obj;
            writer.Write(ary.Length);
            foreach (var vec in ary)
            {
                writer.Write(vec.x); writer.Write(vec.y);
            }
        }, (reader) =>
        {
            Vector2[] ary = new Vector2[reader.ReadInt32()];
            for (int i = 0; i < ary.Length; i++) ary[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
            return ary;
        }
        );

        defaultProcessDic[typeof(OutfitCandidate)] = (
            (writer, obj) => {
                var cand = (OutfitCandidate)obj;
                writer.Write(cand.Outfit.Id.ownerId);
                writer.Write(cand.Outfit.Id.outfitId);

                writer.Write(cand.Tag);
                writer.Write(cand.Priority);
                writer.Write(cand.SelfAware);
            },
            (reader) => {
                OutfitDefinition? outfit = null;
                NebulaGameManager.Instance?.TryGetOutfit(new(reader.ReadInt32(), reader.ReadInt32()), out outfit);
                return new OutfitCandidate(outfit!, reader.ReadString(), reader.ReadInt32(), reader.ReadBoolean());
            }
        );
        defaultProcessDic[typeof(TimeLimitedModulator)] = (
            (writer, obj) =>
            {
                var mod = (TimeLimitedModulator)obj;
                writer.Write(mod.Timer);
                writer.Write(mod.CanPassMeeting);
                writer.Write(mod.Priority);
                writer.Write(mod.DuplicateTag);

                if (mod is SpeedModulator sm)
                {
                    writer.Write(1);
                    writer.Write(sm.Num);
                    writer.Write(sm.DirectionalNum.x);
                    writer.Write(sm.DirectionalNum.y);
                    writer.Write(sm.DirectionalNum.z);
                    writer.Write(sm.DirectionalNum.w);
                    writer.Write(sm.IsMultiplier);
                    writer.Write(sm.CanBeAware);
                }
                else if (mod is SizeModulator sim)
                {
                    writer.Write(2);
                    writer.Write(sim.Size.x);
                    writer.Write(sim.Size.y);
                    writer.Write(sim.CanBeAware);
                    writer.Write(sim.Smooth);
                }
                else if (mod is FloatModulator fm)
                {
                    writer.Write(4);
                    writer.Write(fm.Attribute.Id);
                    writer.Write(fm.Num);
                    writer.Write(fm.CanBeAware);
                }
                else if (mod is AttributeModulator am)
                {
                    writer.Write(3);
                    writer.Write(am.Attribute.Id);
                    writer.Write(am.CanBeAware);
                }
                

            },
            (reader) =>
            {
                float timer = reader.ReadSingle();
                bool canPassMeeting = reader.ReadBoolean();
                int priority = reader.ReadInt32();
                string dupTag = reader.ReadString();
                int type = reader.ReadInt32();

                if (type == 1)
                    return new SpeedModulator(reader.ReadSingle(), new Vector4(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), reader.ReadBoolean(), timer, canPassMeeting, priority, dupTag, reader.ReadBoolean());
                else if (type == 2)
                    return new SizeModulator(new(reader.ReadSingle(), reader.ReadSingle()), timer, canPassMeeting, priority, dupTag, reader.ReadBoolean(), reader.ReadBoolean());
                else if (type == 3)
                    return new AttributeModulator(PlayerAttributeImpl.GetAttributeById(reader.ReadInt32()), timer, canPassMeeting, priority, dupTag, reader.ReadBoolean());
                else if (type == 4)
                    return new FloatModulator(PlayerAttributeImpl.GetAttributeById(reader.ReadInt32()), reader.ReadSingle(), timer, canPassMeeting, priority, dupTag, reader.ReadBoolean());

                return null!;
            }
        );
        defaultProcessDic[typeof(TranslatableTag)] = ((writer, obj) => writer.Write(((TranslatableTag)obj)?.Id ?? int.MaxValue), (reader) => TranslatableTag.ValueOf(reader.ReadInt32())!);
        defaultProcessDic[typeof(CommunicableTextTag)] = defaultProcessDic[typeof(TranslatableTag)];
        defaultProcessDic[typeof(PlayerModInfo)] = ((writer, obj) => writer.Write(((PlayerModInfo)obj)?.PlayerId ?? 255), (reader) => NebulaGameManager.Instance?.GetPlayer(reader.ReadByte())!);
        defaultProcessDic[typeof(GamePlayer)] = defaultProcessDic[typeof(PlayerModInfo)];
        defaultProcessDic[typeof(INebulaAchievement)] = ((writer, obj) => writer.Write((ulong)((INebulaAchievement)obj).Id.ComputeConstantLongHash()), (reader) => (NebulaAchievementManager.TryGetAchievement((long)reader.ReadUInt64(), out var ach) ? ach : null)!);
        defaultProcessDic[typeof(DefinedRole)] = ((writer, obj) => writer.Write(((DefinedRole)obj)?.Id ?? -1), (reader) => Roles.Roles.GetRole(reader.ReadInt32())!);
        defaultProcessDic[typeof(DefinedGhostRole)] = ((writer, obj) => writer.Write(((DefinedGhostRole)obj)?.Id ?? -1), (reader) => Roles.Roles.GetGhostRole(reader.ReadInt32())!);
        defaultProcessDic[typeof(DefinedModifier)] = ((writer, obj) => writer.Write(((DefinedModifier)obj)?.Id ?? -1), (reader) => Roles.Roles.GetModifier(reader.ReadInt32())!);
    }

    static public (Action<MessageWriter, object>, Func<MessageReader, object>) GetProcess(Type type)
    {
        //配列の場合
        if (type.IsAssignableTo(typeof(Array)))
        {
            var elemType = type.GetElementType()!;
            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 1)!;

            var process = GetProcess(elemType);

            return ((writer, param) =>
            {
                var array = (param as Array)!;
                writer.Write(array.Length);
                for (int i = 0; i < array.Length; i++) process.Item1.Invoke(writer, array.GetValue(i)!);
            },
            (reader) =>
            {
                int length = reader.ReadInt32();
                var array = (constructor.Invoke([length]) as Array)!;
                for (int i = 0; i < length; i++) array.SetValue(process.Item2.Invoke(reader), i);
                return array;
            }
            );
        }

        //タプルの場合
        if (type.IsAssignableTo(typeof(ITuple)))
        {
            int count = 0;
            List<(Action<MessageWriter, object>, Func<MessageReader, object>)> processList = new();
            while (true)
            {
                //フィールドの型を取得し、適切なプロセスを取得する。 (入れ子状のタプルでも大丈夫)
                var field = type.GetField("Item" + (count + 1).ToString());
                if (field == null) break; //終端まで来たら終了
                processList.Add(GetProcess(field.FieldType));
                count++;
            }

            var processAry = processList.ToArray();
            var constructor = type.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == processAry.Length);
            if (constructor == null) throw new Exception("Can not Tuple Constructor");

            return ((writer, param) =>
            {
                ITuple tuple = (param as ITuple)!;
                for (int i = 0; i < processAry.Length; i++) processAry[i].Item1.Invoke(writer, tuple[i]!);
            },
             (reader) => constructor.Invoke(processAry.Select(p => p.Item2.Invoke(reader)).ToArray())
            );
        }

        //列挙型であった場合
        if (type.IsAssignableTo(typeof(Enum)))
            return defaultProcessDic[Enum.GetUnderlyingType(type)];

        //その他の型である場合
        return defaultProcessDic[type];
    }

    public static void GetMessageTreater<Parameter>(out Action<MessageWriter, Parameter> sender, out Func<MessageReader, Parameter> receiver)
    {
        Type paramType = typeof(Parameter);

        var process = RemoteProcessAsset.GetProcess(paramType);

        sender = (writer, param) =>
        {
            process.Item1.Invoke(writer, param!);
        };
        receiver = (reader) =>
        {
            return (Parameter)process.Item2.Invoke(reader);
        };
    }

}
public class RemoteProcess<Parameter> : RemoteProcessBase
{
    public delegate void Process(Parameter parameter, bool isCalledByMe);

    private Action<MessageWriter, Parameter> Sender { get; set; }
    private Func<MessageReader, Parameter> Receiver { get; set; }
    private Process Body { get; set; }

    public RemoteProcess(string name, Action<MessageWriter, Parameter> sender, Func<MessageReader, Parameter> receiver, RemoteProcess<Parameter>.Process process, bool shouldBeReliable = true)
    : base(name, shouldBeReliable)
    {
        Sender = sender;
        Receiver = receiver;
        Body = process;
    }

    public RemoteProcess(string name, RemoteProcess<Parameter>.Process process, bool shouldBeReliable = true) : base(name, shouldBeReliable)  
    {
        Body = process;
        RemoteProcessAsset.GetMessageTreater<Parameter>(out var sender,out var receiver);
        Sender = sender;
        Receiver = receiver;
    }

    public void Invoke(Parameter parameter)
    {
        RPCRouter.SendRpc(Name,Hash,(writer)=>Sender(writer,parameter),()=>Body.Invoke(parameter,true), ShouldBeReliable);
    }

    public NebulaRPCInvoker GetInvoker(Parameter parameter)
    {
        return new NebulaRPCInvoker(Hash, (writer) => Sender(writer, parameter), () => Body.Invoke(parameter, true));
    }

    public void LocalInvoke(Parameter parameter)
    {
        Body.Invoke(parameter, true);
    }

    public override void Receive(MessageReader reader)
    {
        try
        {
            Body.Invoke(Receiver.Invoke(reader), false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in RPC(Received: {Name})\n" + ex.ToString());
        }
    }
}

public static class RemotePrimitiveProcess
{
    public static RemoteProcess<int> OfInteger(string name, RemoteProcess<int>.Process process) => new(name, (writer, message) => writer.Write(message), (reader) => reader.ReadInt32(), process);
    public static RemoteProcess<float> OfFloat(string name, RemoteProcess<float>.Process process) => new(name, (writer, message) => writer.Write(message), (reader) => reader.ReadSingle(), process);
    public static RemoteProcess<string> OfString(string name, RemoteProcess<string>.Process process) => new(name, (writer, message) => writer.Write(message), (reader) => reader.ReadString(), process);
    public static RemoteProcess<byte> OfByte(string name, RemoteProcess<byte>.Process process) => new(name, (writer, message) => writer.Write(message), (reader) => reader.ReadByte(), process);
    public static RemoteProcess<Vector2> OfVector2(string name, RemoteProcess<Vector2>.Process process) => new(name, (writer, message) => { writer.Write(message.x); writer.Write(message.y); }, (reader) => new(reader.ReadSingle(), reader.ReadSingle()), process);
    public static RemoteProcess<Vector3> OfVector3(string name, RemoteProcess<Vector3>.Process process) => new(name, (writer, message) => { writer.Write(message.x); writer.Write(message.y); writer.Write(message.z); }, (reader) => new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()), process);
}

[NebulaRPCHolder]
public class CombinedRemoteProcess : RemoteProcessBase
{
    public static CombinedRemoteProcess CombinedRPC = new();
    CombinedRemoteProcess() : base("CombinedRPC", true) { }

    public override void Receive(MessageReader reader)
    {
        int num = reader.ReadInt32();

        for (int i = 0; i < num; i++)
        {
            int id = reader.ReadInt32();
            if (RemoteProcessBase.AllNebulaProcess.TryGetValue(id,out var rpc)){
                rpc.Receive(reader);
            }
            else
            {
                NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "RPC NotFound ID Error. id: " + id + " ,index: " + i + " ,length: " + num);
                throw new Exception("Combined RPC Error");
            }
        }
    }

    public void Invoke(params NebulaRPCInvoker[] invokers)
    {
        RPCRouter.SendRpc(Name, Hash, (writer) =>
        {
            writer.Write(invokers.Count(i=>!i.IsDummy));
            foreach (var invoker in invokers)
            {
                if (!invoker.IsDummy)
                    invoker.Invoke(writer);
                else
                    invoker.InvokeLocal();
            }
        },
        () => { }, ShouldBeReliable);
    }
}

public class RemoteProcess : RemoteProcessBase
{
    public delegate void Process(bool isCalledByMe);
    private Process Body { get; set; }
    public RemoteProcess(string name, Process process, bool shouldBeReliable = true)
    : base(name, shouldBeReliable)
    {
        Body = process;
    }

    public void Invoke()
    {
        RPCRouter.SendRpc(Name, Hash, (writer) => { }, () => Body.Invoke(true), ShouldBeReliable);
    }

    public NebulaRPCInvoker GetInvoker()
    {
        return new NebulaRPCInvoker(Hash, (writer) => { }, () => Body.Invoke(true));
    }

    public override void Receive(MessageReader reader)
    {
        try
        {
            Body.Invoke(false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in RPC(Received: {Name})\n" + ex.ToString());
        }
    }
}

public class DivisibleRemoteProcess<Parameter, DividedParameter> : RemoteProcessBase
{
    public delegate void Process(DividedParameter parameter, bool isCalledByMe);

    private Func<Parameter, IEnumerator<DividedParameter>> Divider;
    private Action<MessageWriter, DividedParameter> DividedSender { get; set; }
    private Func<MessageReader, DividedParameter> Receiver { get; set; }
    private Process Body { get; set; }

    public DivisibleRemoteProcess(string name, Func<Parameter,IEnumerator<DividedParameter>> divider, Action<MessageWriter, DividedParameter> dividedSender, Func<MessageReader, DividedParameter> receiver, DivisibleRemoteProcess<Parameter, DividedParameter>.Process process)
    : base(name, true)
    {
        Divider = divider;
        DividedSender = dividedSender;
        Receiver = receiver;
        Body = process;
    }

    public DivisibleRemoteProcess(string name, Func<Parameter, IEnumerator<DividedParameter>> divider, DivisibleRemoteProcess<Parameter, DividedParameter>.Process process)
    : base(name, true)
    {
        Divider = divider;
        RemoteProcessAsset.GetMessageTreater<DividedParameter>(out var sender,out var receiver);
        DividedSender = sender;
        Receiver = receiver;
        Body = process;
    }

    public void Invoke(Parameter parameter)
    {
        void dividedSend(DividedParameter param)
        {
            RPCRouter.SendRpc(Name, Hash, (writer) => DividedSender(writer, param), () => Body.Invoke(param, true), false);
        }
        var enumerator = Divider.Invoke(parameter);
        while (enumerator.MoveNext()) dividedSend(enumerator.Current);
    }

    public void LocalInvoke(Parameter parameter)
    {
        var enumerator = Divider.Invoke(parameter);
        while (enumerator.MoveNext()) Body.Invoke(enumerator.Current, true);
    }

    public override void Receive(MessageReader reader)
    {
        try
        {
            Body.Invoke(Receiver.Invoke(reader), false);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in RPC(Received: {Name})\n" + ex.ToString());
        }
    }
}

public static class QueryRPC
{
    public static RemoteProcess<QueryParameter> Generate<QueryParameter, AnswerParameter>(string name, Predicate<QueryParameter> predicate, Func<QueryParameter, AnswerParameter> onAsked, RemoteProcess<AnswerParameter>.Process process)
    {
        var answerRpc = new RemoteProcess<AnswerParameter>(name + "Answer", process);
        return new RemoteProcess<QueryParameter>(name, (q, _) =>
        {
            if (predicate.Invoke(q)) answerRpc.Invoke(onAsked.Invoke(q));
        });        
    }
}


[NebulaRPCHolder]
public static class RoleRPC
{
    public class Definition
    {
        private Definition(int id){
            this.Id = id;
        }

        static public Definition Get<Ability>(string id, Action<Ability, int, bool> action, Predicate<Ability>? predicate) where Ability : IPlayerAbility => new(RegisterAction(id, action, predicate));        
        private int Id;
        public void RpcSync(GamePlayer player, int number) => RoleRPC.RpcSync(Id, player, number);
    }

    static public Definition Get<Ability>(string id, Action<Ability, int, bool> action, Predicate<Ability>? predicate = null) where Ability : IPlayerAbility => Definition.Get(id, action, predicate);

    public delegate void SyncRoleArgAction(int number, GamePlayer player, bool calledByMe);
    static private readonly Dictionary<int, SyncRoleArgAction> actions = [];
    static private void ActionInternal<Ability>(GamePlayer player, int num, Action<Ability, int, bool> action, Predicate<Ability> predicate, bool calledByMe)
    {
        foreach(var a in player.AllAbilities)
        {
            if(a is Ability ability && (predicate?.Invoke(ability) ?? true))
            {
                action.Invoke(ability, num, calledByMe);
                break;
            }
        }
    }

    static private int RegisterAction<Ability>(string id, Action<Ability, int, bool> action, Predicate<Ability>? predicate) where Ability : IPlayerAbility
    {
        var hash = id.ComputeConstantHash();
        if (actions.ContainsKey(hash)) NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Duplicated role argument sync message: " + id + " (Hash: " + hash +")");
        actions[hash] = (num, player, calledByMe) => ActionInternal<Ability>(player, num, action, predicate, calledByMe);
        NebulaPlugin.Log.Print("Registered role argument sync message: " + id + " (Hash: " + hash + ")");
        return hash;
    }

    static private readonly RemoteProcess<(int id, int num, GamePlayer player)> RpcSyncArgument = new("SyncRoleArgument", (message, calledByMe) =>
    {
        if(actions.TryGetValue(message.id, out var action))
        {
            action.Invoke(message.num, message.player, calledByMe);
        }
        else
        {
            NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, "Unknown role argument sync message: " + message.id);
        }
    });

    static private void RpcSync(int id, GamePlayer player, int number) => RpcSyncArgument.Invoke((id, number, player));
}
[NebulaRPCHolder]
public static class PropertyRPC
{
    record PropertyRequest
    {
        public RequestType type;
        public Action<object>? Callback;
        public Action? ErrorCallback;
    }

    public enum RequestType
    {
        Byte,
        Integer,
        Float,
        IntegerArray,
        FloatArray,
        ByteArray,
        String,
    }
    private static Dictionary<RequestType, Type> RequestTypeMap = new Dictionary<RequestType, Type>() {
        { RequestType.Byte, typeof(byte) },
        { RequestType.Integer, typeof(int) },
        { RequestType.Float, typeof(float) },
        { RequestType.ByteArray, typeof(byte[])},
        { RequestType.IntegerArray, typeof(int[]) },
        { RequestType.FloatArray, typeof(float[]) },
        { RequestType.String, typeof(string) }
    };
    private static Dictionary<int, PropertyRequest> MyRequests = new();

    private static RemoteProcess<(int requestId, bool isSuccess, RequestType type, object obj)> RpcPropertyReply = new(
        "PropertyReply",
        (writer, message) => {
            var objWriter = RemoteProcessAsset.GetProcess(RequestTypeMap[message.type]).Item1;
            writer.Write(message.requestId);
            writer.Write(message.isSuccess);
            objWriter.Invoke(writer, message.obj);
        },
        (reader) => {
            var requestId = reader.ReadInt32();
            var isSuccess = reader.ReadBoolean();
            if (MyRequests.TryGetValue(requestId, out var request))
            {
                if (isSuccess)
                {
                    var objReader = RemoteProcessAsset.GetProcess(RequestTypeMap[request.type]).Item2;
                    request.Callback?.Invoke(objReader.Invoke(reader));
                }
                else
                {
                    request.ErrorCallback?.Invoke();
                }
                MyRequests.Remove(requestId);
            }
            return (0, false, RequestType.Integer, null!);
        },
        (_, _) => { }
        );

    private static RemoteProcess<(byte targetId, int requestId, string propertyId, RequestType type)> RpcPropertyRequest = new(
        "PropertyRequest",
        (message, _)=>{
            if (!PlayerControl.LocalPlayer) return;

            if(message.targetId == PlayerControl.LocalPlayer.PlayerId)
            {
                var property = PropertyManager.GetProperty(message.propertyId);
                if(property == null)
                {
                    RpcPropertyReply.Invoke((message.requestId, false, RequestType.Integer, null!));
                    return;
                }

                switch (message.type)
                {
                    case RequestType.Byte:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.Byte, property.GetByte()));
                        break;
                    case RequestType.ByteArray:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.ByteArray, property.GetByteArray()));
                        break;
                    case RequestType.Integer:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.Integer, property.GetInteger()));
                        break;
                    case RequestType.IntegerArray:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.IntegerArray, property.GetIntegerArray()));
                        break;
                    case RequestType.Float:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.Float, property.GetFloat()));
                        break;
                    case RequestType.String:
                        RpcPropertyReply.Invoke((message.requestId, true, RequestType.String, property.GetString()));
                        break;
                    default:
                        RpcPropertyReply.Invoke((message.requestId, false, RequestType.Integer, null!));
                        break;
                }
            }
        }
    );

    private static IEnumerator CoGetProperty(byte targetPlayerId, string propertyId, RequestType propertyType, Action<object>? callBack, Action? errorAction)
    {
        int id = System.Random.Shared.Next(int.MaxValue >> 1);
        PropertyRequest request = new() { Callback = callBack, ErrorCallback = errorAction, type = propertyType };
        MyRequests[id] = request;

        RpcPropertyRequest.Invoke((targetPlayerId, id, propertyId, propertyType));

        while (MyRequests.ContainsKey(id)) yield return null;

        yield break;
    }

    public static IEnumerator CoGetProperty<T>(byte targetPlayerId, string propertyId, Action<T>? callBack, Action? errorCallBack) =>
        CoGetProperty(targetPlayerId, propertyId, RequestTypeMap.First(entry=>entry.Value == typeof(T)).Key, (obj)=>callBack?.Invoke((T)obj), errorCallBack);
}

public class RPCScheduler
{
    public enum RPCTrigger
    {
        PreMeeting,
        AfterMeeting
    }

    private Dictionary<RPCTrigger, List<NebulaRPCInvoker>> allDic = new();
    public void Schedule(RPCTrigger trigger, NebulaRPCInvoker invoker)
    {
        List<NebulaRPCInvoker>? list;
        if (!allDic.TryGetValue(trigger, out list))
        {
            list = new();
            allDic[trigger] = list;
        }

        list?.Add(invoker);
    }

    public void Execute(RPCTrigger trigger)
    {
        if(allDic.TryGetValue(trigger,out var list))
        {
            Debug.Log($"Execute RPC ({list.Count})");
            CombinedRemoteProcess.CombinedRPC.Invoke(list.ToArray());
            allDic.Remove(trigger);
        }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
class NebulaRPCInGameHandlerPatch
{
    static public void ReceiveMessage(MessageReader reader)
    {
        int id = reader.ReadInt32();
        if (RemoteProcessBase.AllNebulaProcess.TryGetValue(id, out var rpc))
        {
            rpc.Receive(reader);
        }
        else
        {
            NebulaPlugin.Log.Print("RPC NotFound Error. id: " + id);
            throw new Exception("RPC Error Occurred. (Not found: " + id + ")");
        }
    }

    static void Postfix([HarmonyArgument(0)] byte callId, [HarmonyArgument(1)] MessageReader reader)
    {
        if (callId != 128) return;

        ReceiveMessage(reader);
    }
}
