using AmongUs.GameOptions;
using Nebula.Behaviour;
using Nebula.Roles;
using Nebula.Roles.Complex;
using Virial.Assignable;
using Virial.Command;
using Virial.Common;
using Virial.Game;
using Virial.Text;
using static Mono.CSharp.Parameter;

namespace Nebula.Player;

[NebulaPreLoad(typeof(TranslatableTag))]
public static class PlayerState
{
    public static TranslatableTag Alive = new("state.alive");
    public static TranslatableTag Dead = new("state.dead");
    public static TranslatableTag Exiled = new("state.exiled");
    public static TranslatableTag Misfired = new("state.misfired");
    public static TranslatableTag Sniped = new("state.sniped");
    public static TranslatableTag Beaten = new("state.beaten");
    public static TranslatableTag Guessed = new("state.guessed");
    public static TranslatableTag Misguessed = new("state.misguessed");
    public static TranslatableTag Embroiled = new("state.embroiled");
    public static TranslatableTag Suicide = new("state.suicide");
    public static TranslatableTag Trapped = new("state.trapped");
    public static TranslatableTag Revived = new("state.revived");
    public static TranslatableTag Pseudocide = new("state.pseudocide");
    public static TranslatableTag Deranged = new("state.deranged");
    public static TranslatableTag Cursed = new("state.cursed");
    public static TranslatableTag Crushed = new("state.crushed");

    public static void Load()
    {
        Virial.Text.PlayerStates.Alive = Alive;
        Virial.Text.PlayerStates.Dead = Dead;
        Virial.Text.PlayerStates.Exiled = Exiled;
        Virial.Text.PlayerStates.Misfired = Misfired;
        Virial.Text.PlayerStates.Sniped = Sniped;
        Virial.Text.PlayerStates.Beaten = Beaten;
        Virial.Text.PlayerStates.Guessed = Guessed;
        Virial.Text.PlayerStates.Misguessed = Misguessed;
        Virial.Text.PlayerStates.Embroiled = Embroiled;
        Virial.Text.PlayerStates.Suicide = Suicide;
        Virial.Text.PlayerStates.Revived = Revived;
        Virial.Text.PlayerStates.Pseudocide = Pseudocide;
    }
}


[NebulaPreLoad(typeof(TranslatableTag))]
public class PlayerAttributeImpl : IPlayerAttribute
{
    public int Id { get; init; }
    public string Name { get; init; }
    public int ImageId { get; init; }
    public Predicate<GamePlayer>? Cognizable { get; init; }
    public bool CanCognize(GamePlayer player) => Cognizable?.Invoke(player) ?? true;
    public IPlayerAttribute? IdenticalAttribute { get; init; } = null;
    static private List<IPlayerAttribute> allAttributes = new();
    static public IEnumerable<IPlayerAttribute> AllAttributes => allAttributes;

    //カテゴライズする際の分類用属性(基本的な効果を持つ属性にまとめたいところ)
    public IPlayerAttribute CategorizedAttribute => IdenticalAttribute ?? this;

    static public IPlayerAttribute GetAttributeById(int id) => allAttributes[id];


    public PlayerAttributeImpl(int imageId, string name)
    {
        this.Id = allAttributes.Count;
        this.ImageId = imageId;
        allAttributes.Add(this);
        Name = name;
    }

    public static void Load()
    {
        PlayerAttributes.Accel = new PlayerAttributeImpl(0, "$accel");
        PlayerAttributes.Decel = new PlayerAttributeImpl(1, "$decel");
        PlayerAttributes.Invisible = new PlayerAttributeImpl(2, "invisible");
        PlayerAttributes.InvisibleElseImpostor = new PlayerAttributeImpl(2, "$invisible") { Cognizable = p => p.Role.Role.Category == RoleCategory.ImpostorRole, IdenticalAttribute = PlayerAttributes.Invisible };
        PlayerAttributes.CurseOfBloody = new PlayerAttributeImpl(3, "curseOfBloody");
        PlayerAttributes.Isolation = new PlayerAttributeImpl(4, "$isolation") { Cognizable = p => p.Role.Role.Category == RoleCategory.ImpostorRole };
        PlayerAttributes.BuskerEffect = new PlayerAttributeImpl(4, "busker") { Cognizable = _ => false };
    }
}

[NebulaRPCHolder]
public class PlayerModInfo : IRuntimePropertyHolder, Virial.Game.Player, ICommandExecutor, IPermissionHolder
{
    public static Permission OpPermission = new Permission();

    public class OutfitCandidate
    {
        public string Tag { get; private set; }
        public int Priority { get; private set; }
        public bool SelfAware { get; private set; }
        public GameData.PlayerOutfit outfit { get; private set; }

        public OutfitCandidate(string tag, int priority, bool selfAware, GameData.PlayerOutfit outfit)
        {
            this.Tag = tag;
            this.Priority = priority;
            this.SelfAware = selfAware;
            this.outfit = outfit;
        }
    }

    public PlayerControl MyControl { get; private set; }
    public byte PlayerId { get; private set; }
    public bool AmOwner { get; private set; }
    public bool WithNoS { get; private set; }
    public bool IsDisconnected { get; set; } = false;
    public bool IsDead => IsDisconnected || MyControl.Data.IsDead;
    public float MouseAngle { get; private set; }
    private bool requiredUpdateMouseAngle { get; set; }
    public void RequireUpdateMouseAngle() => requiredUpdateMouseAngle = true;

    public byte? HoldingDeadBodyId { get; private set; } = null;
    public bool HoldingAnyDeadBody => HoldingDeadBodyId != null;
    public bool AmHost => MyControl.AmHost();
    private DeadBody? deadBodyCache { get; set; } = null;

    private IEnumerable<SpeedModulator> SpeedModulators => timeLimitedModulators.Select(m => m as SpeedModulator).Where(m => m != null)!;
    private IEnumerable<AttributeModulator> AttributeModulators => timeLimitedModulators.Select(m => m as AttributeModulator).Where(m => m != null)!;

    private List<TimeLimitedModulator> timeLimitedModulators = new();

    public RoleInstance Role => myRole;
    private RoleInstance myRole = null!;
    private List<ModifierInstance> myModifiers = new();
    RuntimeRole Virial.Game.Player.Role => myRole;
    IEnumerable<RuntimeModifier> Virial.Game.Player.Modifiers => myModifiers;
    public Vector2 PreMeetingPoint { get; private set; } = Vector2.zero;

    private List<OutfitCandidate> outfits = new List<OutfitCandidate>();
    private TMPro.TextMeshPro roleText;
    public Transform PlayerScaler;

    public FakeSabotageStatus FakeSabotage { get; private set; } = new();
    public Vector2? GoalPos = null;

    public PlayerTaskState Tasks { get; set; }

    public bool HasAnyTasks
    {
        get
        {
            bool hasTasks = Role.HasAnyTasks;
            if (Role.HasCrewmateTasks) hasTasks = FeelLikeHaveCrewmateTasks;
            return hasTasks;
        }
    }

    public bool HasCrewmateTasks
    {
        get
        {
            bool hasCrewmateTasks = Role.HasCrewmateTasks;
            ModifierAction((modifier) => { hasCrewmateTasks &= !(modifier.InvalidateCrewmateTask || modifier.MyCrewmateTaskIsIgnored); });
            return hasCrewmateTasks;
        }
    }

    public bool FeelLikeHaveCrewmateTasks
    {
        get
        {
            if (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) return HasCrewmateTasks;

            bool hasCrewmateTasks = Role.HasCrewmateTasks;
            ModifierAction((modifier) => { hasCrewmateTasks &= !modifier.InvalidateCrewmateTask; });
            return hasCrewmateTasks;
        }
    }

    public VariablePermissionHolder PermissionHolder = new([]);
    bool IPermissionHolder.Test(Virial.Common.Permission permission) => PermissionHolder.Test(permission);

    //各種収集データ
    public PlayerModInfo? MyKiller = null;
    public float? DeathTimeStamp = null;
    public CommunicableTextTag? MyState = PlayerState.Alive;

    public IEnumerable<AssignableInstance> AllAssigned()
    {
        if (Role != null) yield return Role;

        foreach (var m in myModifiers) yield return m;
    }

    public void AssignableAction(Action<AssignableInstance> action)
    {
        foreach (var role in AllAssigned()) action(role);
    }

    public void ModifierAction(Action<ModifierInstance> action)
    {
        foreach (var role in myModifiers) action(role);
    }

    public IEnumerable<ModifierInstance> AllModifiers => myModifiers;
    public IEnumerable<Modifier> GetModifiers<Modifier>() where Modifier : ModifierInstance
    {
        foreach (var m in myModifiers) if (m is Modifier targetM) yield return targetM;
    }

    public bool TryGetModifier<Modifier>(out Modifier modifier) where Modifier : ModifierInstance {
        foreach (var m in AllModifiers)
        {
            if (m is Modifier result)
            {
                modifier = result;
                return true;
            }
        }
        modifier = null!;
        return false;
    }

    public PlayerModInfo(PlayerControl myPlayer)
    {
        this.MyControl = myPlayer;
        this.Tasks = new PlayerTaskState(myPlayer);
        PlayerId = myPlayer.PlayerId;
        AmOwner = myPlayer.AmOwner;
        WithNoS = !myPlayer.gameObject.TryGetComponent<UncertifiedPlayer>(out var up);
        DefaultOutfit = myPlayer.Data.DefaultOutfit;
        roleText = GameObject.Instantiate(myPlayer.cosmetics.nameText, myPlayer.cosmetics.nameText.transform);
        roleText.transform.localPosition = new Vector3(0, 0.185f, -0.01f);
        roleText.fontSize = 1.7f;
        roleText.text = "Unassigned";

        PlayerScaler = UnityHelper.CreateObject("Scaler", myPlayer.transform, myPlayer.Collider.offset).transform;
        myPlayer.cosmetics.transform.SetParent(PlayerScaler, true);
        myPlayer.transform.FindChild("BodyForms").SetParent(PlayerScaler, true);
    }

    public string DefaultName => DefaultOutfit.PlayerName;
    public string ColoredDefaultName => DefaultName.Color(Color.Lerp(Palette.PlayerColors[PlayerId], Color.white, 0.3f));
    public GameData.PlayerOutfit DefaultOutfit { get; private set; }
    public GameData.PlayerOutfit CurrentOutfit => outfits.Count > 0 ? outfits[0].outfit : DefaultOutfit;

    private void UpdateOutfit()
    {
        int lastColor = CurrentOutfit.ColorId;

        GameData.PlayerOutfit newOutfit = DefaultOutfit;
        if (outfits.Count > 0)
        {
            outfits.Sort((o1, o2) => o2.Priority - o1.Priority);
            newOutfit = outfits[0].outfit;
        }

        try
        {
            MyControl.RawSetColor(newOutfit.ColorId);
            //MyControl.RawSetName(newOutfit.PlayerName);
            MyControl.RawSetHat(newOutfit.HatId, newOutfit.ColorId);
            MyControl.RawSetSkin(newOutfit.SkinId, newOutfit.ColorId);
            MyControl.RawSetVisor(newOutfit.VisorId, newOutfit.ColorId);
            MyControl.RawSetPet(newOutfit.PetId, newOutfit.ColorId);
            MyControl.RawSetColor(newOutfit.ColorId);

            if (MyControl.MyPhysics.Animations.IsPlayingRunAnimation())
            {
                MyControl.MyPhysics.ResetAnimState();
                MyControl.cosmetics.StopAllAnimations();
            }
        }
        catch (Exception e) {
            Debug.LogError("Outfit Error: Error occurred on changing " + DefaultName + " 's outfit");
            if (!MyControl) Debug.LogError(" - PlayerControl is INVALID.");
            Debug.LogError(e.ToString());
        }

        int currentColor = newOutfit.ColorId;

        if (lastColor != currentColor)
        {
            //色が変化したとき

            if (AmOwner && Helpers.CurrentMonth == 4 && ColorHelper.IsLightGreen(Palette.PlayerColors[lastColor]) && ColorHelper.IsPink(Palette.PlayerColors[currentColor]))
            {
                new StaticAchievementToken("sakura");
            }
        }
    }

    public void AddOutfit(OutfitCandidate outfit)
    {
        if (!outfit.SelfAware && MyControl.AmOwner) return;
        outfits.Add(outfit);
        UpdateOutfit();
    }

    public void RemoveOutfit(string tag)
    {
        outfits.RemoveAll(o => o.Tag.Equals(tag));
        UpdateOutfit();
    }

    public GameData.PlayerOutfit GetOutfit(int maxPriority)
    {
        foreach (var outfit in outfits) if (outfit.Priority <= maxPriority) return outfit.outfit;
        return DefaultOutfit;
    }

    public void UpdateNameText(TMPro.TextMeshPro nameText, bool onMeeting = false, bool showDefaultName = false)
    {
        var text = onMeeting ? DefaultName : CurrentOutfit.PlayerName;
        var color = Color.white;

        AssignableAction(r => r.DecoratePlayerName(ref text, ref color));
        PlayerControl.LocalPlayer.GetModInfo()?.AssignableAction(r => r.DecorateOtherPlayerName(this, ref text, ref color));

        if (showDefaultName && !CurrentOutfit.PlayerName.Equals(DefaultName))
            text += (" (" + DefaultName + ")").Color(Color.gray);


        nameText.text = text;
        nameText.color = color;


    }

    static public readonly Color FakeTaskColor = new Color(0x86 / 255f, 0x86 / 255f, 0x86 / 255f);
    static public readonly Color CrewTaskColor = new Color(0xFA / 255f, 0xD9 / 255f, 0x34 / 255f);
    public void UpdateRoleText(TMPro.TextMeshPro roleText) {

        string text = "";

        if ((NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || AmOwner)
        {
            text += myRole?.DisplayRoleName ?? "Undefined";

            AssignableAction(r => { var newName = r.OverrideRoleName(text, false); if (newName != null) text = newName; });
            AssignableAction(m => m.DecorateRoleName(ref text));

            if (HasAnyTasks)
                text += (" (" + Tasks.ToString((NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || !AmongUsUtil.InCommSab) + ")").Color((FeelLikeHaveCrewmateTasks) ? CrewTaskColor : FakeTaskColor);
        }

        if ((NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || (PlayerControl.LocalPlayer.GetModInfo()?.Role.CanSeeOthersFakeSabotage ?? false))
        {
            var fakeStr = FakeSabotage.MyFakeTasks.Join(type => Language.Translate("sabotage." + type.ToString().HeadLower()), ", ");
            if (fakeStr.Length > 0) fakeStr = ("(" + fakeStr + ")").Color(Color.gray);
            text += fakeStr;
        }

        roleText.text = text;
        roleText.gameObject.SetActive(true);
    }

    private void SetRole(AbstractRole role, int[] arguments)
    {
        myRole?.Inactivate();

        var isDead = MyControl.Data.IsDead;

        if (role.Category == Virial.Assignable.RoleCategory.ImpostorRole)
            DestroyableSingleton<RoleManager>.Instance.SetRole(MyControl, RoleTypes.Impostor);
        else
            DestroyableSingleton<RoleManager>.Instance.SetRole(MyControl, RoleTypes.Crewmate);

        if (isDead) MyControl.Die(DeathReason.Kill, false);

        myRole = role.CreateInstance(this, arguments);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) {
            myRole.OnActivated(); myRole.Register();
            GameEntityManager.Instance?.GetPlayerEntities(PlayerId).Do(e => e.OnSetRole(myRole));
            GameEntityManager.Instance?.AllEntities.Do(e => e.OnSetRole(this, myRole));
        }

        NebulaGameManager.Instance?.RoleHistory.Add(new(PlayerId, myRole));
    }

    private void SetModifier(AbstractModifier role, int[] arguments)
    {
        var modifier = role.CreateInstance(this, arguments);
        myModifiers.Add(modifier);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) {
            modifier.OnActivated(); modifier.Register();
            GameEntityManager.Instance?.GetPlayerEntities(PlayerId).Do(e => e.OnAddModifier(modifier));
            GameEntityManager.Instance?.AllEntities.Do(e => e.OnAddModifier(this, modifier));
        }

        NebulaGameManager.Instance?.RoleHistory.Add(new(PlayerId, modifier, true));
    }

    public void OnSetAttribute(IPlayerAttribute attribute) {
        if (attribute == PlayerAttributes.CurseOfBloody)
        {
            IEnumerator CoCurseUpdate()
            {
                bool isLeft = false;

                while (true)
                {
                    yield return new WaitForSeconds(0.24f);
                    if (!HasAttribute(attribute)) yield break;

                    if (!MyControl.inVent && !MyControl.Data.IsDead)
                    {
                        if (MyControl.MyPhysics.Velocity.magnitude > 0)
                        {
                            var vec = MyControl.MyPhysics.Velocity.normalized * 0.08f * (isLeft ? 1f : -1f);
                            AmongUsUtil.GenerateFootprint(MyControl.transform.position + new Vector3(-vec.y, vec.x - 0.22f), Roles.Modifier.Bloody.MyRole.RoleColor, 5f);
                            isLeft = !isLeft;
                        }
                        else
                        {
                            AmongUsUtil.GenerateFootprint(MyControl.transform.position + new Vector3(0f, -0.22f), Roles.Modifier.Bloody.MyRole.RoleColor, 5f);
                        }
                    }
                }
            }
            NebulaManager.Instance.StartCoroutine(CoCurseUpdate().WrapToIl2Cpp());
        }
    }
    public void OnUnsetAttribute(IPlayerAttribute attribute) { }

    public bool HasAttribute(IPlayerAttribute attribute) => AttributeModulators.Any(m => m.Attribute == attribute);

    public NebulaRPCInvoker RpcInvokerSetRole(AbstractRole role, int[]? arguments) => RpcSetAssignable.GetInvoker((PlayerId, role.Id, arguments ?? Array.Empty<int>(), true));
    public NebulaRPCInvoker RpcInvokerSetModifier(AbstractModifier modifier, int[]? arguments) => RpcSetAssignable.GetInvoker((PlayerId, modifier.Id, arguments ?? Array.Empty<int>(), false));
    public NebulaRPCInvoker RpcInvokerUnsetModifier(AbstractModifier modifier) => RpcRemoveModifier.GetInvoker(new(PlayerId, modifier.Id));
    public void UnsetModifierLocal(Predicate<ModifierInstance> predicate)
    {
        myModifiers.RemoveAll(m =>
        {
            if (predicate.Invoke(m))
            {
                m.Inactivate();
                GameEntityManager.Instance?.GetPlayerEntities(PlayerId).Do(e => e.OnRemoveModifier(m));
                GameEntityManager.Instance?.AllEntities.Do(e => e.OnRemoveModifier(this, m));

                NebulaGameManager.Instance?.RoleHistory.Add(new(PlayerId, m, false));
                return true;
            }
            return false;
        });
        if (NebulaGameManager.Instance?.GameState != NebulaGameStates.NotStarted) HudManager.Instance.UpdateHudContent();
    }

    public bool TryGetProperty(string id, out INebulaProperty? property)
    {
        property = null;
        string prefix = $"players.{PlayerId}.";
        if (!id.StartsWith(prefix)) return false;

        string subStr = id.Substring(prefix.Length);
        if (subStr == "roleArgument")
        {
            property = new NebulaInstantProperty() { IntegerArrayProperty = Role?.GetRoleArgument() };
            return true;
        } else if (subStr == "leftGuess")
        {
            property = new NebulaInstantProperty() { IntegerProperty = TryGetModifier<GuesserModifier.Instance>(out var guesser) ? guesser.LeftGuess : -1 };
            return true;
        }
        if (Role?.TryGetProperty(subStr, out property) ?? false) return true;

        return false;
    }

    public IEnumerator CoGetRoleArgument(Action<int[]> callback)
    {
        yield return PropertyRPC.CoGetProperty<int[]>(PlayerId, $"players.{PlayerId}.roleArgument", callback, () => callback.Invoke(new int[0]));
    }

    public IEnumerator CoGetLeftGuess(Action<int> callback)
    {
        yield return PropertyRPC.CoGetProperty<int>(PlayerId, $"players.{PlayerId}.leftGuess", callback, () => callback.Invoke(-1));
    }

    public readonly static RemoteProcess<(byte playerId, int assignableId, int[] arguments, bool isRole)> RpcSetAssignable = new(
        "SetAssignable",
        (message, isCalledByMe) =>
        {
            var player = NebulaGameManager.Instance!.RegisterPlayer(PlayerControl.AllPlayerControls.Find((Il2CppSystem.Predicate<PlayerControl>)(p => p.PlayerId == message.playerId)));
            if (message.isRole)
                player.SetRole(Roles.Roles.AllRoles[message.assignableId], message.arguments);
            else
                player.SetModifier(Roles.Roles.AllModifiers[message.assignableId], message.arguments);

            if (NebulaGameManager.Instance.GameState != NebulaGameStates.NotStarted)
            {
                HudManager.Instance.UpdateHudContent();
                if (player.AmOwner) player.UpdateTaskState();
            }
        }
        );

    private readonly static RemoteProcess<(byte playerId, int modifierId)> RpcRemoveModifier = new(
        "RemoveModifier", (message, _) => NebulaGameManager.Instance?.GetModPlayerInfo(message.playerId)?.UnsetModifierLocal((m) => m.Role.Id == message.modifierId)
        );

    public readonly static RemoteProcess<(byte playerId, OutfitCandidate outfit)> RpcAddOutfit = new(
        "AddOutfit", (message, _) => NebulaGameManager.Instance?.GetModPlayerInfo(message.playerId)?.AddOutfit(message.outfit)
        );

    public readonly static RemoteProcess<(byte playerId, string tag)> RpcRemoveOutfit = new(
       "RemoveOutfit", (message, _) => NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId)?.RemoveOutfit(message.tag)
       );

    public readonly static RemoteProcess<(byte playerId, float angle)> RpcUpdateAngle = new(
       "UpdateAngle", (message, _) => NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId)!.MouseAngle = message.angle
       );

    public readonly static RemoteProcess<(byte playerId, Vector2 position)> RpcSharePreMeetingPoint = new(
       "SharePreMeetingPoint", (message, _) => NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId)!.PreMeetingPoint = message.position
       );

    public readonly static RemoteProcess<(byte playerId, SpeedModulator modulator)> RpcSpeedModulator = new(
       "AddSpeedModulator", (message, _) =>
       {
           var player = NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId)!;
           var modulators = player.timeLimitedModulators;
           if (message.modulator.DuplicateTag != 0)
           {
               modulators.RemoveAll(m => m is SpeedModulator && m.DuplicateTag == message.modulator.DuplicateTag);
           }
           modulators.Add(message.modulator);
           modulators.Sort((m1, m2) => m2.Priority - m1.Priority);

           if (player.AmOwner && player.SpeedModulators.Any(m => m.IsAccelModulator) && player.SpeedModulators.Any(m => m.IsDecelModulator)) new StaticAchievementToken("speedAttribute");
       }
       );

    public readonly static RemoteProcess<(byte playerId, AttributeModulator modulator)> RpcAttrModulator = new(
       "AddAttributeModulator", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId);
           if (playerInfo == null) return;

           var modulators = playerInfo!.timeLimitedModulators;

           //新たな属性が付与されたとき
           if (!playerInfo.AttributeModulators.Any(m => m.Attribute == message.modulator.Attribute)) playerInfo!.OnSetAttribute(message.modulator.Attribute);

           modulators.Add(message.modulator);

           if (message.modulator.DuplicateTag != 0)
               modulators.RemoveAll(m => m.DuplicateTag == message.modulator.DuplicateTag && m != message.modulator);
           

           modulators.Sort((m1, m2) => m2.Priority - m1.Priority);
       }
       );

    public readonly static RemoteProcess<(byte playerId, int attributeId)> RpcRemoveAttr = new(
       "RemoveAttribute", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetModPlayerInfo(message.playerId);
           if (playerInfo == null) return;

           var modulators = playerInfo!.timeLimitedModulators;

           modulators.RemoveAll(p => p is AttributeModulator am && am.Attribute.Id == message.attributeId);
       }
       );

    private void UpdateHoldingDeadBody()
    {
        if (!HoldingDeadBodyId.HasValue) return;

        //同じ死体を持つプレイヤーがいる
        if (NebulaGameManager.Instance?.AllPlayerInfo().Any(p => p.PlayerId < PlayerId && p.HoldingDeadBodyId.HasValue && p.HoldingDeadBodyId.Value == HoldingDeadBodyId.Value) ?? false)
        {
            deadBodyCache = null;
            if (AmOwner) ReleaseDeadBody();
            return;
        }


        if (!deadBodyCache || deadBodyCache!.ParentId != HoldingDeadBodyId.Value)
        {
            deadBodyCache = Helpers.AllDeadBodies().FirstOrDefault((d) => d.ParentId == HoldingDeadBodyId.Value);
            if (!deadBodyCache)
            {
                deadBodyCache = null;
                if (AmOwner) ReleaseDeadBody();
                return;
            }
        }

        //ベント中の死体
        deadBodyCache!.Reported = MyControl.inVent;
        foreach (var r in deadBodyCache!.bodyRenderers) r.enabled = !MyControl.inVent;

        var targetPosition = MyControl.transform.position + new Vector3(-0.1f, -0.1f);

        if (MyControl.transform.position.Distance(deadBodyCache!.transform.position) < 1.8f)
            deadBodyCache!.transform.position += (targetPosition - deadBodyCache!.transform.position) * 0.15f;
        else
            deadBodyCache!.transform.position = targetPosition;


        Vector3 playerPos = MyControl.GetTruePosition();
        Vector3 deadBodyPos = deadBodyCache!.TruePosition;
        Vector3 diff = (deadBodyPos - playerPos);
        float d = diff.magnitude;
        if (PhysicsHelpers.AnythingBetween(playerPos, deadBodyPos, Constants.ShipAndAllObjectsMask, false))
        {
            foreach (var ray in PhysicsHelpers.castHits)
            {
                float temp = ((Vector3)ray.point - playerPos).magnitude;
                if (d > temp) d = temp;
            }

            d -= 0.15f;
            if (d < 0f) d = 0f;

            deadBodyCache!.transform.localPosition = playerPos + diff.normalized * d;
        }
        else
        {
            deadBodyCache!.transform.localPosition = deadBodyCache!.transform.position;
        }

    }

    public void ReleaseDeadBody() {
        RpcHoldDeadBody.Invoke(new(PlayerId, byte.MaxValue, (deadBodyCache ? deadBodyCache?.transform.localPosition : null) ?? new Vector2(10000, 10000)));
    }

    public void HoldDeadBody(DeadBody deadBody) {
        RpcHoldDeadBody.Invoke(new(PlayerId, deadBody.ParentId, deadBody.transform.position));
    }

    readonly static RemoteProcess<(byte holderId, byte bodyId, Vector2 pos)> RpcHoldDeadBody = new(
      "HoldDeadBody",
      (message, _) =>
      {
          var info = NebulaGameManager.Instance?.GetModPlayerInfo(message.holderId);
          if (info == null) return;

          if (message.bodyId == byte.MaxValue)
          {
              if(info.deadBodyCache && message.pos.magnitude < 10000) info.deadBodyCache!.transform.localPosition = new Vector3(message.Item3.x, message.Item3.y, message.Item3.y / 1000f);
              info.HoldingDeadBodyId = null;
          }
          else
          {
              info.HoldingDeadBodyId = message.bodyId;
              var deadBody = Helpers.AllDeadBodies().FirstOrDefault(d => d.ParentId == message.bodyId);
              info.deadBodyCache = deadBody;
              if (deadBody && message.pos.magnitude < 10000) deadBody!.transform.localPosition = new Vector3(message.Item3.x, message.Item3.y, message.Item3.y / 1000f);
          }
      }
      );

    static public (float angle,float distance) LocalMouseInfo { get
        {
            Vector2 vec = (Vector2)Input.mousePosition - new Vector2(Screen.width / 2, Screen.height / 2);
            float currentAngle = Mathf.Atan2(vec.y, vec.x);
            float ratio = (Camera.main.orthographicSize * 2f) / (float)Screen.height;
            return (currentAngle, vec.magnitude * ratio);
        } 
    }

    private void UpdateMouseAngle()
    {
        if (!requiredUpdateMouseAngle) return;

        float currentAngle = LocalMouseInfo.angle;

        if (Mathf.Repeat(currentAngle - MouseAngle, Mathf.PI * 2f) > 0.02f) RpcUpdateAngle.Invoke((PlayerId, currentAngle));

        requiredUpdateMouseAngle = false;
    }

    private void UpdateModulators()
    {
        foreach (var m in timeLimitedModulators) m.Update();
        timeLimitedModulators.RemoveAll(m => m.IsBroken);

        //Speed Modulator
        MyControl.MyPhysics.Speed = CalcSpeed();

        //Attribute Modulator

        //Size Modulator

    }

    /*
    private void UpdateSpeedModulators()
    {
        foreach(var m in speedModulators) m.Update();
        speedModulators.RemoveAll(m => m.IsBroken);
        MyControl.MyPhysics.Speed = CalcSpeed();
    }

    private void UpdateAttributeModulators()
    {
        ulong maskBefore = 0, maskAfter = 0;
        foreach (var m in attributeModulators)
        {
            m.Update();
            maskBefore |= 1ul << (int)m.Attribute.Id;
        }
        attributeModulators.RemoveAll(m => m.IsBroken);
        foreach (var m in attributeModulators) maskAfter |= 1ul << (int)m.Attribute.Id;

        ulong mask = maskBefore ^ maskAfter;
        foreach(var a in PlayerAttributeImpl.AllAttributes)
        {
            if ((mask & (1ul << a.Id)) != 0) OnUnsetAttribute(a);
        }
    }
    */

    public IEnumerable<(IPlayerAttribute attribute, float percentage)> GetValidAttributes()
    {
        float accelMax = 0f, accelCurrent = 0f;
        float decelMax = 0f, decelCurrent = 0f;
        foreach (var mod in SpeedModulators)
        {
            if(mod.IsAccelModulator)
            {
                if (accelMax < mod.MaxTime) accelMax = mod.MaxTime;
                if (accelCurrent < mod.Timer) accelCurrent = mod.Timer;
            }
            else if (mod.IsDecelModulator)
            {
                if (decelMax < mod.MaxTime) decelMax = mod.MaxTime;
                if (decelCurrent < mod.Timer) decelCurrent = mod.Timer;
            }
        }
        if (accelMax > 0f && accelCurrent > 0f) yield return (PlayerAttributes.Accel, accelCurrent / accelMax);
        if (decelMax > 0f && decelCurrent > 0f) yield return (PlayerAttributes.Decel, decelCurrent / decelMax);

        foreach(var a in PlayerAttributeImpl.AllAttributes)
        {
            //自認できない属性
            if (!a.CanCognize(this)) continue;

            float max = 0f, current = 0f;
            bool isPermanent = false;
            foreach(var attribute in AttributeModulators.Where(attr => attr.Attribute.CategorizedAttribute == a && attr.Attribute.CanCognize(this) && attr.CanBeAware))
            {
                if (max < attribute.MaxTime) max = attribute.MaxTime;
                if (current < attribute.Timer) current = attribute.Timer;

                isPermanent |= attribute.IsPermanent;
            }
            if (max > 0f && current > 0f) yield return isPermanent ? (a, 0f) : (a, current / max);
        }

    }

    private int visibilityCache = 0;
    public bool IsInvisible => visibilityCache == 2;
    public int VisibilityLevel => visibilityCache;
    public void UpdateVisibility(bool update)
    {
        //不可視度合を調べる
        int invisibleLevel = 0;
        PlayerModInfo localInfo = NebulaGameManager.Instance!.LocalPlayerInfo;

        //属性効果はより透明にする効果を優先する
        if (!IsDead) {
            if (HasAttribute(PlayerAttributes.InvisibleElseImpostor))
            {
                if (localInfo.Role.Role.Category == RoleCategory.ImpostorRole)
                    invisibleLevel = Math.Max(invisibleLevel, 1);
                else if(!AmOwner)
                    invisibleLevel = Math.Max(invisibleLevel, 2);
            }

            if (HasAttribute(PlayerAttributes.Invisible)) invisibleLevel = 2;
        }
        else
        {
            //視点主が生存していてプレイヤーが死亡しているなら見えない
            if (!localInfo.IsDead) invisibleLevel = 2;
        }

        visibilityCache = invisibleLevel;

        //情報が開示済みの場合、あるいは自分自身を見る場合は半透明までにしかならない
        if (AmOwner || NebulaGameManager.Instance!.CanSeeAllInfo) invisibleLevel = Math.Min(1, invisibleLevel);

        MyControl.cosmetics.nameText.transform.parent.gameObject.SetActive(!MyControl.inVent && (invisibleLevel < 2));

        if (IsDead) return;

        float alpha = MyControl.cosmetics.currentBodySprite.BodySprite.color.a;
        if (update)
        {
            float min = 0f, max = 1f;
            if (invisibleLevel == 1) min = 0.25f;

            float goal = invisibleLevel switch
            {
                1 => 0.25f,
                2 => 0f,
                _ => 1f
            };

            if (Math.Abs(alpha - goal) < 0.01f)
            {
                alpha = goal;
            }
            else
            {
                if (alpha > goal)
                    alpha -= 0.85f * Time.deltaTime;
                else
                    alpha += 0.85f * Time.deltaTime;

                alpha = Mathf.Clamp(alpha, min, max);
            }
        }


        var color = new Color(1f, 1f, 1f, alpha);
        

        if (MyControl.cosmetics.currentBodySprite.BodySprite != null) MyControl.cosmetics.currentBodySprite.BodySprite.color = color;

        if (MyControl.cosmetics.skin.layer != null) MyControl.cosmetics.skin.layer.color = color;

        if (MyControl.cosmetics.hat)
        {
            if (MyControl.cosmetics.hat.FrontLayer != null) MyControl.cosmetics.hat.FrontLayer.color = color;
            if (MyControl.cosmetics.hat.BackLayer != null) MyControl.cosmetics.hat.BackLayer.color = color;
        }

        if (MyControl.cosmetics.currentPet)
        {
            foreach (var rend in MyControl.cosmetics.currentPet.renderers) rend.color = color;
            foreach (var rend in MyControl.cosmetics.currentPet.shadows) rend.color = color;
        }

        if (MyControl.cosmetics.visor != null) MyControl.cosmetics.visor.Image.color = color;
    }

    public void Update()
    {
        UpdateNameText(MyControl.cosmetics.nameText, false, NebulaGameManager.Instance?.CanSeeAllInfo ?? false);
        UpdateRoleText(roleText);
        UpdateHoldingDeadBody();
        UpdateMouseAngle();
        UpdateModulators();
        UpdateVisibility(true);

        if (MyControl.AmOwner) AssignableAction((role) => role.LocalUpdate());
    }

    public void UpdateTaskState()
    {
        if (!HasAnyTasks)
            Tasks.WaiveAllTasksAsOutsider();
        else if (!HasCrewmateTasks)
            Tasks.BecomeToOutsider();
    }

    public void OnGameStart()
    {
        if(AmOwner)UpdateTaskState();

        UpdateOutfit();
    }

    public void OnMeetingStart()
    {
        foreach (var m in SpeedModulators) m.OnMeetingStart();

        FakeSabotage.OnMeetingStart();
    }

    public void CalcSpeed(ref float speed)
    {
        foreach (var m in SpeedModulators) m.Calc(ref speed);
    }

    public float CalcSpeed()
    {
        float speed = 2.5f;
        CalcSpeed(ref speed);
        return speed;
    }

    void Virial.Game.Player.MurderPlayer(Virial.Game.Player player, CommunicableTextTag playerState, CommunicableTextTag eventDetail, bool showBlink, bool showKillOverlay)
    {
        MyControl.ModFlexibleKill(player.VanillaPlayer, showBlink, playerState, eventDetail, showKillOverlay);
    }

    void Virial.Game.Player.Suicide(CommunicableTextTag playerState, CommunicableTextTag eventDetail, bool showKillOverlay)
    {
        MyControl.ModFlexibleKill(MyControl,false,playerState,eventDetail,showKillOverlay);
    }

    void Virial.Game.Player.GainAttribute(IPlayerAttribute attribute, float duration, bool canPassMeeting, int priority, int? duplicateTag)
    {
        if (attribute == PlayerAttributes.Accel || attribute == PlayerAttributes.Decel) return;

        RpcAttrModulator.Invoke(new(PlayerId, new(attribute, duration, canPassMeeting, priority, duplicateTag ?? 0)));
    }

    void Virial.Game.Player.GainAttribute(float speedRate, float duration, bool canPassMeeting, int priority, int? duplicateTag)
    {
        RpcSpeedModulator.Invoke(new(PlayerId, new(speedRate, true, duration, canPassMeeting, priority, duplicateTag ?? 0)));
        
    }

    //Virial

    string Virial.Game.Player.Name => DefaultName;
    PlayerControl Virial.Game.Player.VanillaPlayer => MyControl;
}
