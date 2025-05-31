using AmongUs.GameOptions;
using Il2CppSystem.Text.Json;
using NAudio.CoreAudioApi;
using Nebula.Behavior;
using Nebula.Game.Achievements;
using Nebula.Game.Statistics;
using Nebula.Modules.Cosmetics;
using Nebula.Roles;
using Nebula.Roles.Complex;
using Nebula.Roles.Crewmate;
using Nebula.Roles.Impostor;
using System.Diagnostics.CodeAnalysis;
using UnityEngine.Rendering;
using Virial.Assignable;
using Virial.Command;
using Virial.Common;
using Virial.DI;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial.Text;
using static Nebula.Roles.Crewmate.Investigator;
using static UnityEngine.GraphicsBuffer;

namespace Nebula.Player;

public enum RoleType
{
    Role = 0,
    Modifier = 1,
    GhostRole = 2,
}

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
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
    public static TranslatableTag Frenzied = new("state.frenzied");
    public static TranslatableTag Gassed = new("state.gassed");
    public static TranslatableTag Bubbled = new("state.bubbled");
    public static TranslatableTag Meteor = new("state.meteor");
    public static TranslatableTag Starved = new("state.starved");
    public static TranslatableTag Balloon = new("state.balloon");
    public static TranslatableTag Lost = new("state.lost");
    public static TranslatableTag Disconnected = new("state.disconnected") { Color = Color.gray };
    public static TranslatableTag[] AllDeadStates = [Dead, Exiled, Guessed, Misguessed, Embroiled, Suicide, Trapped, Pseudocide, Deranged, Cursed, Crushed, Frenzied, Gassed, Bubbled, Meteor, Starved, Balloon, Lost];
    static PlayerState()
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
        Virial.Text.PlayerStates.Gassed = Gassed;
        Virial.Text.PlayerStates.Bubbled = Bubbled;
        Virial.Text.PlayerStates.Meteor = Meteor;
        Virial.Text.PlayerStates.Balloon = Balloon;
        Virial.Text.PlayerStates.Lost = Lost;
    }
}


[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
public class PlayerAttributeImpl : IPlayerAttribute
{
    public int Id { get; init; }
    public string Name { get; init; }
    public string? UIName { get; init; }
    public Virial.Media.Image Image { get; private init; }
    public Predicate<GamePlayer>? Cognizable { get; init; }
    public bool CanCognize(GamePlayer player) => Cognizable?.Invoke(player) ?? true;
    public IPlayerAttribute? IdenticalAttribute { get; init; } = null;
    static private List<IPlayerAttribute> allAttributes = new();
    static public IEnumerable<IPlayerAttribute> AllAttributes => allAttributes;

    //カテゴライズする際の分類用属性(基本的な効果を持つ属性にまとめたいところ)
    public IPlayerAttribute CategorizedAttribute => IdenticalAttribute ?? this;

    static public IPlayerAttribute GetAttributeById(int id) => allAttributes[id];


    public PlayerAttributeImpl(int imageId, string name, string? uiId = null)
    {
        this.Id = allAttributes.Count;
        this.Image = AttributeShower.AttributeIcon.GetIconSprite(imageId);
        allAttributes.Add(this);
        Name = name;
        UIName = uiId;
    }

    static PlayerAttributeImpl()
    {
        PlayerAttributes.Accel = new PlayerAttributeImpl(0, "$accel", "accel");
        PlayerAttributes.Decel = new PlayerAttributeImpl(1, "$decel", "decel");
        PlayerAttributes.Drunk = new PlayerAttributeImpl(5, "$inverse", "inverse");
        PlayerAttributes.Size = new PlayerAttributeImpl(6, "$size", "size");
        PlayerAttributes.Invisible = new PlayerAttributeImpl(2, "invisible", "invisible");
        PlayerAttributes.InvisibleElseImpostor = new PlayerAttributeImpl(2, "$invisible") { Cognizable = p => p.IsImpostor, IdenticalAttribute = PlayerAttributes.Invisible };
        PlayerAttributes.CurseOfBloody = new PlayerAttributeImpl(3, "curseOfBloody", "curseOfBloody");
        PlayerAttributes.Footprint = new PlayerAttributeImpl(3, "footprint", "footprint") { Cognizable = _ => false };
        PlayerAttributes.Isolation = new PlayerAttributeImpl(4, "$isolation", "isolation") { Cognizable = p => p.IsImpostor };
        PlayerAttributes.BuskerEffect = new PlayerAttributeImpl(4, "busker", "busker") { Cognizable = _ => false };

        PlayerAttributes.FlipXY = new PlayerAttributeImpl(7, "$flip", "flip");
        PlayerAttributes.FlipX = new PlayerAttributeImpl(7, "$flipX") { IdenticalAttribute = PlayerAttributes.FlipXY };
        PlayerAttributes.FlipY = new PlayerAttributeImpl(7, "$flipY") { IdenticalAttribute = PlayerAttributes.FlipXY };

        PlayerAttributes.ScreenSize = new PlayerAttributeImpl(8, "$screenSize", "screenSize");
        PlayerAttributes.Eyesight = new PlayerAttributeImpl(9, "$eyesight", "eyesight");
        PlayerAttributes.Roughening = new PlayerAttributeImpl(10, "$rough", "rough");

        PlayerAttributes.Thurifer = new PlayerAttributeImpl(11, "$thurifer", "thurifer");
        PlayerAttributes.CooldownSpeed = new PlayerAttributeImpl(12, "cooldown", "cooldown");
    }
}

[NebulaRPCHolder]
internal class PlayerModInfo : AbstractModuleContainer, IRuntimePropertyHolder, Virial.Game.Player, ICommandExecutor, IPermissionHolder
{
    public static Permission OpPermission = new Permission();

    public PlayerControl MyControl { get; private set; }
    public byte PlayerId { get; private set; }
    public bool AmOwner { get; private set; }
    public bool IsDisconnected { get; set; } = false;
    public bool IsDead => IsDisconnected || (MyControl ? MyControl.Data.IsDead : ((this as GamePlayer).PlayerState != PlayerStates.Alive && (this as GamePlayer).PlayerState != PlayerStates.Revived));
    public PlayerDiving? CurrentDiving { get; set; }
    public bool IsBlown => MyControl.MyPhysics.Animations.IsPlayingSpawnAnimation();
    public bool IsTeleporting { get; internal set; } = false;

    /// <summary>
    /// マウスの角度を弧度法で返します。
    /// -PIからPIの範囲で返します。
    /// </summary>
    public float MouseAngle { get; private set; }
    private bool requiredUpdateMouseAngle { get; set; }
    public void RequireUpdateMouseAngle() => requiredUpdateMouseAngle = true;

    public byte? HoldingDeadBodyId { get; private set; } = null;
    public bool HoldingAnyDeadBody => HoldingDeadBodyId != null;
    public bool AmHost => MyControl.AmHost();
    private DeadBody? holdingDeadBodyCache { get; set; } = null;

    public RuntimeRole Role => myRole;
    private RuntimeRole myRole = null!;

    public RuntimeGhostRole? GhostRole => myGhostRole;
    private RuntimeGhostRole? myGhostRole = null;

    private List<RuntimeModifier> myModifiers = new();

    RuntimeRole Virial.Game.Player.Role => myRole;
    IEnumerable<RuntimeModifier> Virial.Game.Player.Modifiers => myModifiers;
    public Vector2 PreMeetingPoint { get; private set; } = Vector2.zero;

    private List<Virial.Game.OutfitCandidate> outfits = new();
    private TMPro.TextMeshPro roleText;


    public FakeSabotageStatus FakeSabotage { get; private set; } = new();
    public Vector2? GoalPos = null;
    public void ResetDeadBodyGoalPos() => GoalPos = null;

    public bool WillDie { get; set; } = false;

    public bool HasAnyTasks
    {
        get
        {
            bool hasTasks = Role.TaskType != RoleTaskType.NoTask;
            //実際は持っていなくとも、クルータスクを持っていると思しきプレイヤーの場合
            if (Role.TaskType == RoleTaskType.CrewmateTask) hasTasks = FeelLikeHaveCrewmateTasks;
            return hasTasks;
        }
    }

    public bool HasCrewmateTasks
    {
        get
        {
            bool hasCrewmateTasks = Role.TaskType == RoleTaskType.CrewmateTask;
            ModifierAction((modifier) => { hasCrewmateTasks &= !(modifier.InvalidateCrewmateTask || modifier.MyCrewmateTaskIsIgnored); });
            return hasCrewmateTasks;
        }
    }

    public bool FeelLikeHaveCrewmateTasks
    {
        get
        {
            if (NebulaGameManager.Instance?.CanBeSpectator ?? false) return HasCrewmateTasks;

            bool hasCrewmateTasks = Role.TaskType == RoleTaskType.CrewmateTask;
            ModifierAction((modifier) => { hasCrewmateTasks &= !modifier.InvalidateCrewmateTask; });
            return hasCrewmateTasks;
        }
    }

    public VariablePermissionHolder PermissionHolder = new([]);
    bool IPermissionHolder.Test(Virial.Common.Permission permission) => PermissionHolder.Test(permission);

    public IStampShower? SpecialStampShower = null;
    public IStampShower DefaultStampShower = null!;
    public IStampShower StampShower => (SpecialStampShower?.IsValid ?? false) ? SpecialStampShower : DefaultStampShower;

    private readonly List<SpriteRenderer> playerAdditionalRenderers = [];
    public void AddPlayerColorRenderers(params SpriteRenderer[] renderers)
    {
        playerAdditionalRenderers.RemoveAll(r => !r);
        playerAdditionalRenderers.AddRange(renderers);
    }
    public void RemovePlayerColorRenderer(SpriteRenderer renderer)
    {
        int instanceId = renderer.GetInstanceID();
        playerAdditionalRenderers.RemoveAll(r => r.GetInstanceID() == instanceId);
    }

    //各種収集データ
    public GamePlayer? MyKiller = null;
    public GamePlayer.ExtraDeadInfo? PlayerStateExtraInfo { get; set; }
    public float? DeathTimeStamp = null;
    public CommunicableTextTag? MyState = PlayerState.Alive;

    public IEnumerable<RuntimeAssignable> AllAssigned()
    {
        if (Role != null) yield return Role;
        if (GhostRole != null) yield return GhostRole;
        foreach (var m in myModifiers) yield return m;
    }

    public void AssignableAction(Action<RuntimeAssignable> action)
    {
        foreach (var role in AllAssigned()) action(role);
    }

    public void ModifierAction(Action<RuntimeModifier> action)
    {
        foreach (var role in myModifiers) action(role);
    }

    public IEnumerable<RuntimeModifier> AllModifiers => myModifiers;
    public IEnumerable<Modifier> GetModifiers<Modifier>() where Modifier : RuntimeModifier
    {
        foreach (var m in myModifiers) if (m is Modifier targetM) yield return targetM;
    }

    public bool TryGetModifier<Modifier>([MaybeNullWhen(false)] out Modifier modifier) where Modifier : class, RuntimeModifier {
        foreach (var m in AllModifiers)
        {
            if (m is Modifier result)
            {
                modifier = result;
                return true;
            }
        }
        modifier = null;
        return false;
    }

    public PlayerModInfo()
    {
    }

    internal void SetPlayer(PlayerControl myPlayer)
    {
        this.MyControl = myPlayer;
        PlayerId = myPlayer.PlayerId;
        AmOwner = myPlayer.AmOwner;

        var outfitId = OutfitDefinition.OutfitId.PlayersDefault(myPlayer.PlayerId);
        var outfit = new OutfitDefinition(outfitId, myPlayer.Data.DefaultOutfit, OutfitTag.GetAllTags().Where(tag => tag.Checker.Invoke(myPlayer.Data.DefaultOutfit)).ToArray());
        NebulaGameManager.Instance!.OutfitMap[outfitId] = outfit;
        DefaultOutfit = new(outfit, "", -100, true);    

        roleText = GameObject.Instantiate(myPlayer.cosmetics.nameText, myPlayer.cosmetics.nameText.transform);
        roleText.transform.localPosition = new Vector3(0, 0.185f, 0f);
        roleText.fontSize = 1.7f;
        roleText.text = "Unassigned";

        PlayerScaler = myPlayer.transform.FindChild("Scaler");

        DefaultStampShower = new ArrowStampShower(this);

        //PlayerScaler.gameObject.AddComponent<SortingGroup>();
        //PlayerScaler.gameObject.GetComponentsInChildren<SpriteRenderer>(true).Do(r => r.sortingOrder = 10);

        if (myPlayer.AmOwner)
        {
            float lastUpdated = 0f;
            GameOperatorManager.Instance?.Subscribe<GameUpdateEvent>(ev =>
            {
                if (NebulaGameManager.Instance!.CurrentTime - lastUpdated > 0.8f)
                {
                    RpcShareFlipX.Invoke((PlayerId, MyControl.cosmetics.FlipX));
                    lastUpdated = NebulaGameManager.Instance.CurrentTime;
                }
            }, NebulaGameManager.Instance);
        }

        if (!AmOwner)
        {
            var footStep = myPlayer.FootSteps;
            footStep.volume = 0.7f;
            footStep.minDistance = 0.5f;
            footStep.maxDistance = GeneralConfigurations.OthersFootstepRangeOption;
            footStep.rolloffMode = AudioRolloffMode.Linear;
            footStep.spatialBlend = 1f;
        }
    }

    public string DefaultName => DefaultOutfit.Outfit.outfit.PlayerName;
    public string ColoredDefaultName => DefaultName.Color(Color.Lerp(DynamicPalette.PlayerColors[PlayerId], Color.white, 0.3f));
    public OutfitCandidate DefaultOutfit { get; private set; }
    public OutfitCandidate CurrentOutfit => outfits.Count > 0 ? outfits[0] : DefaultOutfit;
    public OutfitTag[] DefaultOutfitTags => DefaultOutfit.Outfit.OutfitTags;

    private void UpdateOutfit()
    {
        int lastColor = MyControl.cosmetics.ColorId;

        OutfitCandidate newOutfitCand = DefaultOutfit;
        if (outfits.Count > 0)
        {
            outfits = outfits.OrderBy(o => -o.Priority).ToList();
            newOutfitCand = outfits[0];
        }

        NetworkedPlayerInfo.PlayerOutfit newOutfit = newOutfitCand.Outfit.outfit;
        try
        {
            MyControl.RawSetColor(newOutfit.ColorId);
            //MyControl.RawSetName(newOutfit.PlayerName);
            MyControl.RawSetHat(newOutfit.HatId, newOutfit.ColorId);
            MyControl.RawSetSkin(newOutfit.SkinId, newOutfit.ColorId);
            MyControl.RawSetVisor(newOutfit.VisorId, newOutfit.ColorId);
            MyControl.RawSetPet(newOutfit.PetId, newOutfit.ColorId);
            MyControl.RawSetColor(newOutfit.ColorId);

            foreach(var r in playerAdditionalRenderers) if(r) r.material = MyControl.cosmetics.currentBodySprite.BodySprite.sharedMaterial;
            /*
            if (MyControl.MyPhysics.Animations.IsPlayingRunAnimation())
            {
                MyControl.MyPhysics.ResetAnimState();
                MyControl.cosmetics.StopAllAnimations();
            }
            else
            {
                MyControl.cosmetics.FixVisibility();
            }
            */

            GameOperatorManager.Instance?.Run(new PlayerOutfitChangeEvent(this, newOutfitCand));
        }
        catch (Exception e)
        {
            Debug.LogError("Outfit Error: Error occurred on changing " + DefaultName + " 's outfit");
            if (!MyControl) Debug.LogError(" - PlayerControl is INVALID.");
            Debug.LogError(e.ToString());
        }

        int currentColor = newOutfit.ColorId;

        /*
        Debug.Log("Last: " + lastColor);
        Debug.Log("Current: " + currentColor);
        Debug.Log("LastIsLightGreen: " + ColorHelper.IsLightGreen(Palette.PlayerColors[lastColor]));
        Debug.Log("CurrnetIsPink: " + ColorHelper.IsPink(Palette.PlayerColors[currentColor]));
        Debug.Log("Month: " + Helpers.CurrentMonth);
        */

        if (lastColor != currentColor)
        {
            //色が変化したとき
            if (AmOwner && Helpers.CurrentMonth == 4 && ColorHelper.IsLightGreen(DynamicPalette.PlayerColors[lastColor]) && ColorHelper.IsPink(DynamicPalette.PlayerColors[currentColor]))
            {
                Debug.Log("sakura");
                new StaticAchievementToken("sakura");
            }

            if(AmOwner && !IsDead && lastColor == NebulaPlayerTab.CamouflageColorId && currentColor != NebulaPlayerTab.CamouflageColorId && MoreCosmic.GetTags(newOutfit).Any(tag => tag == "hat.party" || tag == "visor.party"))
            {
                //カモフラージュが解けたら、次の瞬間にパーティーメンバーをチェックする
                NebulaManager.Instance.ScheduleDelayAction(() => {

                    var localPos = PlayerControl.LocalPlayer.transform.position;
                    var count = NebulaGameManager.Instance!.AllPlayerInfo.Count(p => !p.AmOwner && !p.IsDead && p.VanillaPlayer.transform.position.Distance(localPos) < 1f && MoreCosmic.GetTags(p.CurrentOutfit.outfit).Any(tag => tag == "hat.party" || tag == "visor.party"));
                    if (count >= 2) new StaticAchievementToken("costume.partyCamo");
                });
            }
        }
    }

    public void AddOutfit(OutfitCandidate outfit)
    {
        if (!outfit.SelfAware && MyControl.AmOwner) return;
        outfits.Insert(0, outfit);
        UpdateOutfit();
    }

    public void RemoveOutfit(string tag)
    {
        outfits.RemoveAll(o => o.Tag.Equals(tag));
        UpdateOutfit();
    }

    public OutfitCandidate GetOutfit(int maxPriority)
    {
        foreach (var outfit in outfits)
        {
            if (outfit.Priority <= maxPriority)
            {
                return outfit;
            }
        }
        return DefaultOutfit;
    }

    public void UpdateNameText(TMPro.TextMeshPro nameText, bool onMeeting = false, bool showDefaultName = false)
    {
        var text = onMeeting ? DefaultName : CurrentOutfit.Outfit.outfit.PlayerName;

        AssignableAction(r => r.DecorateNameConstantly(ref text, NebulaGameManager.Instance?.CanSeeAllInfo ?? false));
        var ev = GameOperatorManager.Instance?.Run(new PlayerDecorateNameEvent(this, text));
        var color = (ev?.Color.HasValue ?? false) ? ev.Color.Value.ToUnityColor() : Color.white;

        if (showDefaultName && !CurrentOutfit.Outfit.outfit.PlayerName.Equals(DefaultName))
            text += (" (" + DefaultName + ")").Color(Color.gray);

        nameText.text = text;
        nameText.color = color;
    }

    static public readonly Color FakeTaskColor = new Color(0x86 / 255f, 0x86 / 255f, 0x86 / 255f);
    static public readonly Color CrewTaskColor = new Color(0xFA / 255f, 0xD9 / 255f, 0x34 / 255f);
    public void UpdateRoleText(TMPro.TextMeshPro roleText, bool inMeeting) {

        string text = "";

        bool canSeeAll = (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || AmOwner;
        bool canSeeRole = canSeeAll;
        bool canSeeTask = canSeeAll;
        bool canSeeFakeSabo = (NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || (PlayerControl.LocalPlayer.GetModInfo()?.Role.CanSeeOthersFakeSabotage ?? false);

        if (!canSeeAll)
        {
            var ev = GameOperatorManager.Instance?.Run(new PlayerCheckRoleInfoVisibilityLocalEvent(this));
            canSeeRole |= ev.CanSeeRole;
            canSeeTask |= ev.CanSeeTask;
        }


        if (canSeeRole)
        {
            var assignable = ((RuntimeAssignable?)(IsDead ? myGhostRole : myRole) ?? myRole);
            string? roleName = assignable.DisplayColoredName;
            text += roleName ?? "Undefined";

            AssignableAction(r => { var newName = r.OverrideRoleName(text, false); if (newName != null) text = newName; });
        }
        else
        {
            text = GameOperatorManager.Instance?.Run(new PlayerSetFakeRoleNameEvent(this, inMeeting)).Text ?? "";
        }

        if (canSeeTask) { 
            if (HasAnyTasks && ((this as GamePlayer).Tasks.Quota > 0 || (this as GamePlayer).Tasks.TotalTasks > 0))
                text += (" (" + (this as GamePlayer).Tasks.Unbox().ToString((NebulaGameManager.Instance?.CanSeeAllInfo ?? false) || !AmongUsUtil.InCommSab) + ")").Color((FeelLikeHaveCrewmateTasks) ? CrewTaskColor : FakeTaskColor);
        }

        if (canSeeFakeSabo)
        {
            var fakeStr = FakeSabotage.MyFakeTasks.Join(type => Language.Translate("sabotage." + type.ToString().HeadLower()), ", ");
            if (fakeStr.Length > 0) fakeStr = ("(" + fakeStr + ")").Color(Color.gray);
            text += fakeStr;
        }

        roleText.text = text;
        roleText.gameObject.SetActive(true);
    }

    private void SetRole(DefinedRole role, int[] arguments)
    {
        myRole?.Inactivate();
        GameOperatorManager.Instance?.WrapUpDeadLifespans();

        var isDead = MyControl.Data.IsDead;

        if (role.Category == Virial.Assignable.RoleCategory.ImpostorRole)
            DestroyableSingleton<RoleManager>.Instance.SetRole(MyControl, RoleTypes.Impostor);
        else
            DestroyableSingleton<RoleManager>.Instance.SetRole(MyControl, RoleTypes.Crewmate);

        if (isDead) MyControl.Die(DeathReason.Kill, false);

        myRole = role.CreateInstance(this, arguments);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) {
            myRole.OnActivated(); (myRole as IGameOperator)?.Register(myRole);
            GameOperatorManager.Instance?.Run(new PlayerRoleSetEvent(this, myRole));
        }

        NebulaGameManager.Instance?.RoleHistory.Add(new(NebulaGameManager.Instance.CurrentTime, PlayerId, myRole, IsDead));
    }

    private void SetGhostRole(DefinedGhostRole role, int[] arguments)
    {
        myGhostRole?.Inactivate();
        GameOperatorManager.Instance?.WrapUpDeadLifespans();

        myGhostRole = role.CreateInstance(this, arguments);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized)
        {
            myGhostRole.OnActivated(); (myGhostRole as IGameOperator)?.Register(myGhostRole);
        }

        NebulaGameManager.Instance?.RoleHistory.Add(new(NebulaGameManager.Instance.CurrentTime, PlayerId, myGhostRole, IsDead));
    }

    private void SetModifier(DefinedModifier role, int[] arguments)
    {
        var modifier = role.CreateInstance(this, arguments);
        myModifiers.Add(modifier);

        if (NebulaGameManager.Instance?.GameState == NebulaGameStates.Initialized) {
            modifier.OnActivated(); (modifier as IGameOperator)?.Register(modifier);
            GameOperatorManager.Instance?.Run(new PlayerModifierSetEvent(this, modifier));
        }

        NebulaGameManager.Instance?.RoleHistory.Add(new(NebulaGameManager.Instance.CurrentTime, PlayerId, modifier, true, IsDead));
    }

    public NebulaRPCInvoker RpcInvokerSetRole(DefinedRole role, int[]? arguments) => RpcSetAssignable.GetInvoker((PlayerId, role.Id, arguments ?? Array.Empty<int>(), RoleType.Role));
    public NebulaRPCInvoker RpcInvokerSetModifier(DefinedModifier modifier, int[]? arguments) => RpcSetAssignable.GetInvoker((PlayerId, modifier.Id, arguments ?? Array.Empty<int>(), RoleType.Modifier));
    public NebulaRPCInvoker RpcInvokerSetGhostRole(DefinedGhostRole role, int[]? arguments) => RpcSetAssignable.GetInvoker((PlayerId, role.Id, arguments ?? Array.Empty<int>(), RoleType.GhostRole));
    public NebulaRPCInvoker RpcInvokerUnsetModifier(DefinedModifier modifier) => RpcRemoveModifier.GetInvoker(new(PlayerId, modifier.Id));
    public void UnsetModifierLocal(Predicate<RuntimeModifier> predicate)
    {
        myModifiers.RemoveAll(m =>
        {
            if (predicate.Invoke(m))
            {
                m.Inactivate();
                GameOperatorManager.Instance?.WrapUpDeadLifespans();

                GameOperatorManager.Instance?.Run(new PlayerModifierRemoveEvent(this, m));

                NebulaGameManager.Instance?.RoleHistory.Add(new(NebulaGameManager.Instance.CurrentTime, PlayerId, m, false, IsDead));
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
            property = new NebulaInstantProperty() { IntegerArrayProperty = Role?.RoleArguments };
            return true;
        } else if (subStr == "leftGuess")
        {
            property = new NebulaInstantProperty() { IntegerProperty = TryGetModifier<GuesserModifier.Instance>(out var guesser) ? guesser.LeftGuess : -1 };
            return true;
        }

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

    public readonly static RemoteProcess<(byte playerId, int assignableId, int[] arguments, RoleType roleType)> RpcSetAssignable = new(
        "SetAssignable",
        (message, isCalledByMe) =>
        {
            var player = NebulaGameManager.Instance!.RegisterPlayer(PlayerControl.AllPlayerControls.Find((Il2CppSystem.Predicate<PlayerControl>)(p => p.PlayerId == message.playerId))).Unbox();

            if (message.roleType == RoleType.Role)
                player.SetRole(Roles.Roles.AllRoles[message.assignableId], message.arguments);
            else if (message.roleType == RoleType.GhostRole)
                player.SetGhostRole(Roles.Roles.AllGhostRoles[message.assignableId], message.arguments);
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
        "RemoveModifier", (message, _) => NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Unbox().UnsetModifierLocal((m) => m.Modifier.Id == message.modifierId)
        );

    public readonly static RemoteProcess<(byte playerId, OutfitCandidate outfit)> RpcAddOutfit = new(
        "AddOutfit", (message, _) => NebulaGameManager.Instance?.GetPlayer(message.playerId)?.Unbox().AddOutfit(message.outfit)
        );

    public readonly static RemoteProcess<(byte playerId, string tag)> RpcRemoveOutfit = new(
       "RemoveOutfit", (message, _) => NebulaGameManager.Instance!.GetPlayer(message.playerId)?.Unbox().RemoveOutfit(message.tag)
       );

    public readonly static RemoteProcess<(byte playerId, Vector2 position)> RpcSharePreMeetingPoint = new(
       "SharePreMeetingPoint", (message, _) => NebulaGameManager.Instance!.GetPlayer(message.playerId)!.Unbox().PreMeetingPoint = message.position
       );

    //////////////////////////////////////////
    //                                      //
    //             死体掴み関連             //
    //                                      //
    //////////////////////////////////////////

    private void UpdateHoldingDeadBody()
    {
        if (!HoldingAnyDeadBody) return;

        //同じ死体を持つプレイヤーがいる
        if (NebulaGameManager.Instance?.AllPlayerInfo.Any(p => p.PlayerId < PlayerId && p.HoldingAnyDeadBody && p.HoldingDeadBody?.PlayerId == HoldingDeadBodyId) ?? false)
        {
            holdingDeadBodyCache = null;
            if (AmOwner) ReleaseDeadBody();
            return;
        }


        if (!holdingDeadBodyCache || holdingDeadBodyCache!.ParentId != HoldingDeadBodyId)
        {
            holdingDeadBodyCache = Helpers.AllDeadBodies().FirstOrDefault((d) => d.ParentId == HoldingDeadBodyId);
            if (!holdingDeadBodyCache)
            {
                holdingDeadBodyCache = null;
                if (AmOwner) ReleaseDeadBody();
                return;
            }
        }

        //ベント中の死体
        holdingDeadBodyCache!.Reported = MyControl.inVent;
        foreach (var r in holdingDeadBodyCache!.bodyRenderers) r.enabled = !MyControl.inVent;

        var targetPosition = MyControl.transform.position + new Vector3(-0.1f, -0.1f);
        var lastPosition = holdingDeadBodyCache!.transform.position;

        if (MyControl.transform.position.Distance(holdingDeadBodyCache!.transform.position) < 1.8f)
            holdingDeadBodyCache!.transform.position += (targetPosition - holdingDeadBodyCache!.transform.position) * 0.15f;
        else
            holdingDeadBodyCache!.transform.position = targetPosition;

        {
            var diffSingle = holdingDeadBodyCache!.transform.position.Distance(lastPosition);
            if (AmOwner && MyControl.CanMove) ModSingleton<AchievementManagerModule>.Instance.CorpseToken.Value += diffSingle;
        }

        Vector3 playerPos = MyControl.GetTruePosition();
        Vector3 deadBodyPos = holdingDeadBodyCache!.TruePosition;
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

            holdingDeadBodyCache!.transform.localPosition = playerPos + diff.normalized * d;
        }
        else
        {
            holdingDeadBodyCache!.transform.localPosition = holdingDeadBodyCache!.transform.position;
        }

    }

    public void ReleaseDeadBody() {
        RpcHoldDeadBody.Invoke(new(PlayerId, byte.MaxValue, (holdingDeadBodyCache ? holdingDeadBodyCache?.transform.localPosition : null) ?? new Vector2(10000, 10000)));
    }

    public void HoldDeadBody(DeadBody? deadBody) {
        if (deadBody == null) ReleaseDeadBody();
        else RpcHoldDeadBody.Invoke(new(PlayerId, deadBody.ParentId, deadBody.transform.position));
    }

    readonly static RemoteProcess<(byte holderId, byte bodyId, Vector2 pos)> RpcHoldDeadBody = new(
      "HoldDeadBody",
      (message, _) =>
      {
          var info = NebulaGameManager.Instance?.GetPlayer(message.holderId)?.Unbox();
          if (info == null) return;

          if (message.bodyId == byte.MaxValue)
          {
              if (info.holdingDeadBodyCache && message.pos.magnitude < 10000) info.holdingDeadBodyCache!.transform.localPosition = new Vector3(message.Item3.x, message.Item3.y, message.Item3.y / 1000f);
              info.HoldingDeadBodyId = null;
          }
          else
          {
              info.HoldingDeadBodyId = message.bodyId;
              var deadBody = Helpers.AllDeadBodies().FirstOrDefault(d => d.ParentId == message.bodyId);
              info.holdingDeadBodyCache = deadBody;
              if (deadBody && message.pos.magnitude < 10000) deadBody!.transform.localPosition = new Vector3(message.Item3.x, message.Item3.y, message.Item3.y / 1000f);
          }
      }
      );


    //////////////////////////////////////////
    //                                      //
    //         マウス位置の情報更新         //
    //                                      //
    //////////////////////////////////////////

    static public (float angle, float distance) LocalMouseInfo { get
        {
            Vector2 vec = (Vector2)Input.mousePosition - new Vector2(Screen.width / 2, Screen.height / 2);
            var viewer = NebulaGameManager.Instance!.WideCamera.ViewerTransform;
            if (viewer.localScale.x < 0f) vec.x *= -1;
            if (viewer.localScale.y < 0f) vec.y *= -1;

            float currentAngle = Mathf.Atan2(vec.y, vec.x) - (viewer.localEulerAngles.z / 180f * Mathf.PI);
            while (currentAngle < -Mathf.PI) currentAngle += Mathf.PI * 2f;
            while (currentAngle > Mathf.PI) currentAngle -= Mathf.PI * 2f;

            float ratio = (Camera.main.orthographicSize * 2f) / (float)Screen.height;
            return (currentAngle, vec.magnitude * ratio);
        }
    }

    private float lastSentAngle = 0f;
    private void UpdateMouseAngle()
    {
        if (!AmOwner) return;

        float currentAngle = LocalMouseInfo.angle;

        if (requiredUpdateMouseAngle)
        {
            if (Mathf.Repeat(currentAngle - lastSentAngle, Mathf.PI * 2f) > 0.02f)
            {
                RpcUpdateAngle.Invoke((PlayerId, currentAngle));
                lastSentAngle = currentAngle;
            }
            requiredUpdateMouseAngle = false;
        }
        else
        {
            MouseAngle = currentAngle;
        }
        
    }


    public readonly static RemoteProcess<(byte playerId, float angle)> RpcUpdateAngle = new(
       "UpdateAngle", (message, _) => NebulaGameManager.Instance!.GetPlayer(message.playerId)!.Unbox().MouseAngle = message.angle, false
       );

    //////////////////////////////////////////
    //                                      //
    //           モジュレータ関連           //
    //                                      //
    //////////////////////////////////////////

    private IEnumerable<SpeedModulator> SpeedModulators => timeLimitedModulators.Select(m => m as SpeedModulator).Where(m => m != null)!;
    private IEnumerable<AttributeModulator> AttributeModulators => timeLimitedModulators.Select(m => m as AttributeModulator).Where(m => m != null)!;
    private List<TimeLimitedModulator> timeLimitedModulators = new();
    private Vector2 smoothPlayerSize = Vector2.one;
    public Transform PlayerScaler;
    private Vector4 directionalPlayerSpeed = new(1f, 0f, 0f, 1f);
    //ローカルでのみ値が計算できればよい。
    public Vector4 DirectionalPlayerSpeed => directionalPlayerSpeed;

    private bool speedAchievementChecked = false;
    private void UpdateModulators()
    {
        foreach (var m in timeLimitedModulators) m.Update();
        timeLimitedModulators.RemoveAll(m => m.IsBroken);

        //Speed Modulator
        directionalPlayerSpeed.x = 1f;
        directionalPlayerSpeed.y = 0f;
        directionalPlayerSpeed.z = 0f;
        directionalPlayerSpeed.w = 1f;
        MyControl.MyPhysics.Speed = CalcSpeed(ref directionalPlayerSpeed);

        //移動できない称号のチェック
        if (!speedAchievementChecked && Mathf.Abs(directionalPlayerSpeed.x) + Mathf.Abs(directionalPlayerSpeed.y) + Mathf.Abs(directionalPlayerSpeed.z) + Mathf.Abs(directionalPlayerSpeed.w) == 0f)
        {
            new StaticAchievementToken("movable");
            speedAchievementChecked = true;
        }


        //Size Modulator
        CalcSize();
    }

    public IEnumerable<(IPlayerAttribute attribute, float percentage)> GetValidAttributes()
    {
        foreach (var a in PlayerAttributeImpl.AllAttributes)
        {
            //自認できない属性
            if (!a.CanCognize(this)) continue;

            float max = 0f, current = 0f;
            bool isPermanent = false;
            foreach (var attribute in timeLimitedModulators.Where(attr => attr.HasCategorizedAttribute(a, this)))
            {
                if (max < attribute.MaxTime) max = attribute.MaxTime;
                if (current < attribute.Timer) current = attribute.Timer;

                isPermanent |= attribute.IsPermanent;
            }
            if (max > 0f && current > 0f) yield return isPermanent ? (a, 0f) : (a, current / max);
        }

        //香気の表示
        if(ModSingleton<Thurifer.ThuribulumManager>.Instance != null)
        {
            var percentage = ModSingleton<Thurifer.ThuribulumManager>.Instance.LocalInhalationPercentage;
            if(percentage > 0f) yield return (PlayerAttributes.Thurifer, percentage);
        }
    }

    static private TimelimitedCache<bool> canSeeFootprint = new(() => GameOperatorManager.Instance!.Run(new UpdateFootprintVisibilityEvent(NebulaGameManager.Instance!)).Visible, 0.05f);
    public void OnSetAttribute(IPlayerAttribute attribute)
    {
        IEnumerator CoFootprintUpdate(Func<Color> color, Func<bool>? predicate, float duration)
        {
            bool isLeft = false;

            while (true)
            {
                yield return new WaitForSeconds(0.24f);
                if (!HasAttribute(attribute)) yield break;

                if (predicate?.Invoke() ?? true)
                {
                    var pos = FootprintHelpers.GetFootprintPosition(MyControl, isLeft);
                    isLeft = !isLeft;
                    if (pos.HasValue) AmongUsUtil.GenerateFootprint(pos.Value, color.Invoke(), duration, () => canSeeFootprint.Value);
                }
            }
        }

        if (attribute == PlayerAttributes.CurseOfBloody)
        {
            NebulaManager.Instance.StartCoroutine(CoFootprintUpdate(() => Roles.Modifier.Bloody.MyRole.UnityColor, null, 5f).WrapToIl2Cpp());
        }
        if (attribute == PlayerAttributes.Footprint)
        {
            NebulaManager.Instance.StartCoroutine(CoFootprintUpdate(() => DynamicPalette.PlayerColors[CurrentOutfit.Outfit.outfit.ColorId], ()=>!HasAttribute(PlayerAttributes.CurseOfBloody), 3f).WrapToIl2Cpp());
        }
    }

    public bool HasAttribute(IPlayerAttribute attribute) => timeLimitedModulators.Any(m => m.HasAttribute(attribute));
    public bool HasAttributeByTag(string tag) => timeLimitedModulators.Any(m => m.DuplicateTag == tag);
    public int CountAttribute(IPlayerAttribute attribute) => timeLimitedModulators.Count(m => m.HasAttribute(attribute));

    public readonly static RemoteProcess<(byte playerId, TimeLimitedModulator modulator, bool allowDuplicate)> RpcAttrModulator = new(
       "AddAttributeModulator", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetPlayer(message.playerId)?.Unbox();
           if (playerInfo == null) return;

           var modulators = playerInfo!.timeLimitedModulators;

           if (message.modulator is AttributeModulator am)
           {
               //新たな属性が付与されたとき
               if (!playerInfo.AttributeModulators.Any(m => m.HasAttribute(am.Attribute))) playerInfo!.OnSetAttribute(am.Attribute);
           }

           if (message.modulator is SpeedModulator)
           {
               if (playerInfo.AmOwner && modulators.Any(m => m.HasAttribute(PlayerAttributes.Accel)) && modulators.Any(m => m.HasAttribute(PlayerAttributes.Decel))) new StaticAchievementToken("speedAttribute");
           }

           if (!message.allowDuplicate && message.modulator.DuplicateTag.Length > 0) modulators.RemoveAll(m => m.DuplicateTag == message.modulator.DuplicateTag);

           modulators.Add(message.modulator);
           modulators.Sort((m1, m2) => m2.Priority - m1.Priority);
       }
       );

    public readonly static RemoteProcess<(byte playerId, int attributeId)> RpcRemoveAttr = new(
       "RemoveAttribute", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetPlayer(message.playerId)?.Unbox();
           if (playerInfo == null) return;

           var modulators = playerInfo!.timeLimitedModulators;

           var attr = PlayerAttributeImpl.GetAttributeById(message.attributeId);
           modulators.RemoveAll(p => p.HasAttribute(attr));
       }
       );

    public readonly static RemoteProcess<(byte playerId, string tag)> RpcRemoveAttrByTag = new(
       "RemoveAttributeByTag", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetPlayer(message.playerId)?.Unbox();
           if (playerInfo == null) return;

           var modulators = playerInfo!.timeLimitedModulators;

           modulators.RemoveAll(p => p.DuplicateTag == message.tag);
       }
       );

    public bool CachedFlipX { get; private set; }
    public readonly static RemoteProcess<(byte playerId, bool flipX)> RpcShareFlipX = new(
       "CacheFlipX", (message, _) =>
       {
           var playerInfo = NebulaGameManager.Instance!.GetPlayer(message.playerId)?.Unbox();
           if (playerInfo == null) return;

           playerInfo.CachedFlipX = message.flipX;
       }, false);

    //////////////////////////////////////////
    //                                      //
    //              速度の計算              //
    //                                      //
    //////////////////////////////////////////

    public void CalcSpeed(ref Vector4 directionalPlayerSpeed, ref float speed)
    {
        foreach (var m in SpeedModulators) m.Calc(ref directionalPlayerSpeed, ref speed);
    }

    public float CalcSpeed(ref Vector4 directionalPlayerSpeed)
    {
        float speed = 2.5f;
        CalcSpeed(ref directionalPlayerSpeed, ref speed);
        return speed;
    }

    //////////////////////////////////////////
    //                                      //
    //             サイズの計算             //
    //                                      //
    //////////////////////////////////////////

    public void CalcSize()
    {
        Vector2 targetSize = Vector2.one;
        Vector2 nonSmoothSize = Vector2.one;
        foreach (var m in timeLimitedModulators.Select(m => m as SizeModulator).Where(m => m != null))
        {
            if (m.Smooth)
                targetSize *= m.Size;
            else
                nonSmoothSize *= m.Size;
        }

        var diff = smoothPlayerSize - targetSize;
        smoothPlayerSize -= diff * Mathf.Clamp01(Time.deltaTime * 2f);

        PlayerScaler.transform.localScale = (smoothPlayerSize * nonSmoothSize).AsVector3(1f);
    }

    public float CalcAttributeVal(IPlayerAttribute attribute, bool isMultiplier = true)
    {
        float num = isMultiplier ? 1 : 0;
        foreach (var m in timeLimitedModulators.Select(m => m as FloatModulator).Where(m => m?.HasAttribute(attribute) ?? false)!)
        {
            if (isMultiplier)
                num *= m!.Num;
            else
                num += m!.Num;
        }

        return num;
    }


    //////////////////////////////////////////
    //                                      //
    //        プレイヤーの可視性関連        //
    //                                      //
    //////////////////////////////////////////

    private int visibilityCache = 0;
    public bool IsInvisible => visibilityCache == 2;
    public int VisibilityLevel => visibilityCache;
    private float VisibilityAlpha = 1f;
    private bool IsInShadowCache = false;
    private void UpdateVisibilityAlpha(int invisibleLevel)
    {
        float min = 0f, max = 1f;
        if (invisibleLevel == 1) min = 0.25f;

        float goal = invisibleLevel switch
        {
            1 => 0.25f,
            2 => 0f,
            _ => 1f
        };

        if (Math.Abs(VisibilityAlpha - goal) < 0.01f)
        {
            VisibilityAlpha = goal;
        }
        else
        {
            if (VisibilityAlpha > goal)
                VisibilityAlpha -= 0.85f * Time.deltaTime;
            else
                VisibilityAlpha += 0.85f * Time.deltaTime;

            VisibilityAlpha = Mathf.Clamp(VisibilityAlpha, min, max);
        }
    }

    private static Vector2[] VisibilityCheckVectors = [
        Vector2.zero,
        Vector2.right,
        Vector2.left,
        Vector2.up * 1.35f,
        Vector2.down * 1.35f,
        Vector2.right + Vector2.up,
        Vector2.right + Vector2.down,
        Vector2.left + Vector2.up,
        Vector2.left + Vector2.down,
        ];
    public void UpdateVisibility(bool update, bool ignoreShadow = false, bool showNameText = true)
    {
        UpdateVisibilityInner(update, ignoreShadow, showNameText, out var a, out var aIgnoresWall);
        GameOperatorManager.Instance?.Run(new PlayerAlphaUpdateEvent(this, a, aIgnoresWall));
        
    }

    private void UpdateVisibilityInner(bool update, bool ignoreShadow, bool showNameText, out float alpha, out float alphaIgnoresWall) 
    {
        alpha = 1f;
        alphaIgnoresWall = 1f;
        try
        {
            if (update)
            {
                //不可視度合を調べる
                int invisibleLevel = 0;
                PlayerModInfo localInfo = GamePlayer.LocalPlayer.Unbox();

                //属性効果はより透明にする効果を優先する
                if (!IsDead)
                {
                    if (HasAttribute(PlayerAttributes.InvisibleElseImpostor))
                    {
                        if (localInfo.Role.Role.Category == RoleCategory.ImpostorRole)
                            invisibleLevel = Math.Max(invisibleLevel, 1);
                        else if (!AmOwner)
                            invisibleLevel = Math.Max(invisibleLevel, 2);
                    }

                    if (HasAttribute(PlayerAttributes.Invisible)) invisibleLevel = 2;
                }
                else
                {
                    //視点主が生存していてプレイヤーが死亡しているなら見えない
                    if (!localInfo.IsDead) invisibleLevel = 2;
                }

                //属性による可視性の情報を控えておく
                visibilityCache = invisibleLevel;
            }

            int visualInvisibleLevel = visibilityCache;

            //情報が開示済みの場合、あるいは自分自身を見る場合は半透明までにしかならない
            if (AmOwner || NebulaGameManager.Instance!.CanBeSpectator) visualInvisibleLevel = Math.Min(1, visualInvisibleLevel);

            MyControl.cosmetics.nameText.transform.parent.gameObject.SetActive(!ModSingleton<ShowUp>.Instance.AnyoneShowedUp && !MyControl.inVent && (visualInvisibleLevel < 2) && showNameText);

            if (IsDead)
            {
                if (MyControl.cosmetics.currentBodySprite.BodySprite != null) MyControl.cosmetics.currentBodySprite.BodySprite.color = Color.white;

                if (!MyControl.AmOwner && MyControl.cosmetics.currentPet)
                {
                    foreach (var rend in MyControl.cosmetics.currentPet.renderers) rend.color = Color.clear;
                    foreach (var rend in MyControl.cosmetics.currentPet.shadows) rend.color = Color.clear;
                }

                Color c = new(1f, 1f, 1f, 0.5f);
                MyControl.cosmetics.GetComponent<NebulaCosmeticsLayer>().AdditionalRenderers().Do(r => r.color = c);
                foreach (var r in playerAdditionalRenderers) if (r) r.color = c;
                alpha = alphaIgnoresWall = 0.5f;
                return;
            }

            //対象プレイヤーが生存している場合

            if (update)
            {
                UpdateVisibilityAlpha(visualInvisibleLevel);

                bool isInShadow = false;
                if (!GamePlayer.LocalPlayer!.IsDead && !AmOwner)
                {
                    //自身も生存している場合、影の中にいるプレイヤーは見えないようにする

                    int shadowMask = Constants.ShadowMask;
                    int objectMask = Constants.ShipAndAllObjectsMask;

                    var light = PlayerControl.LocalPlayer.lightSource;
                    Vector2 pos = light.transform.position;
                    Vector2 myPos = MyControl.transform.position;

                    var isAcrossWalls = VisibilityCheckVectors.All(v => Helpers.AnyNonTriggersBetween(pos, myPos + v * 0.22f, out _, objectMask));

                    var mag = isAcrossWalls ? 0.22f : 0.4f;

                    //いずれかの追加ライトの範囲内にいない場合
                    if (!LightInfo.AllLightInfo.Any(info => VisibilityCheckVectors.Any(vec => info.CheckPoint(vec))))
                    {
                        isInShadow = VisibilityCheckVectors.All(v => Helpers.AnyCustomNonTriggersBetween(pos, myPos + v * mag,
                            collider => LightSource.OneWayShadows.TryGetValue(collider.gameObject, out var oneWayShadows) ? !oneWayShadows.IsIgnored(light) : true,
                            shadowMask));
                    }
                }

                IsInShadowCache = isInShadow;
            }

            var shadowHidesPlayer =  !ignoreShadow && IsInShadowCache;
            if (shadowHidesPlayer) MyControl.cosmetics.nameText.transform.parent.gameObject.SetActive(false);

            alpha = shadowHidesPlayer ? 0f : VisibilityAlpha;
            alphaIgnoresWall = VisibilityAlpha;
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

            MyControl.cosmetics.GetComponent<NebulaCosmeticsLayer>().AdditionalRenderers().Do(r => r.color = color);
        }
        catch (Exception e){ }
    }



    //////////////////////////////////////////
    //                                      //
    //              情報の更新              //
    //                                      //
    //////////////////////////////////////////
    private void UpdateFlipX()
    {
        if (AmOwner) return;
        if (MyControl.MyPhysics.Velocity.sqrMagnitude > 0) return;
        if(MyControl.MyPhysics.FlipX != CachedFlipX)MyControl.MyPhysics.FlipX = CachedFlipX;
    }
    public void Update()
    {
        if (IsDead && ModSingleton<ShowUp>.Instance.ShowedUp(this)) MyControl.Visible = true;

        UpdateNameText(MyControl.cosmetics.nameText, false, NebulaGameManager.Instance?.CanSeeAllInfo ?? false);
        UpdateRoleText(roleText, false);

        var viewerScale = NebulaGameManager.Instance!.WideCamera.ViewerTransform.localScale;
        var textScale = new Vector3(viewerScale.x < 0f ? -1f : 1f, viewerScale.y < 0f ? -1f : 1f, 1f);
        var textAngle = -NebulaGameManager.Instance!.WideCamera.ViewerTransform.localEulerAngles * (textScale.x * textScale.y);
        MyControl.cosmetics.nameText.transform.parent.localEulerAngles = textAngle;
        MyControl.cosmetics.nameText.transform.parent.localScale = textScale;
        if(AmOwner) MyControl.cosmetics.nameText.transform.parent.SetWorldZ(-15f);

        UpdateMouseAngle();
        UpdateModulators();

        LightInfo.UpdateLightInfo();
        UpdateVisibility(true, !NebulaGameManager.Instance.WideCamera.DrawShadow);
        UpdateFlipX();
    }

    public void HudUpdate()
    {
        UpdateHoldingDeadBody();
    }

    public void UpdateTaskState()
    {
        if (!HasAnyTasks)
            (this as GamePlayer).Tasks.Unbox().WaiveAllTasksAsOutsider();
        else if (!HasCrewmateTasks)
            (this as GamePlayer).Tasks.Unbox().BecomeToOutsider();
    }

    public void OnGameStart()
    {
        if (AmOwner) UpdateTaskState();

        UpdateOutfit();
    }

    public void OnMeetingStart()
    {
        foreach (var m in SpeedModulators) m.OnMeetingStart();

        FakeSabotage.OnMeetingStart();

        ResetDeadBodyGoalPos();
    }

    //////////////////////////////////////////
    //                                      //
    //              Virial API              //
    //                                      //
    //////////////////////////////////////////


    // Virial::AssignableAPI
    IEnumerable<RuntimeAssignable> GamePlayer.AllAssigned() => AllAssigned();
    bool GamePlayer.TryGetModifier<Modifier>([MaybeNullWhen(false)] out Modifier modifier) where Modifier : class => TryGetModifier<Modifier>(out modifier);
    bool GamePlayer.AttemptedGhostAssignment { get; set; } = false;

    // Virial::MurderAPI

    void GamePlayer.MurderPlayer(GamePlayer player, CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams, KillCondition killCondition, Action<KillResult>? callBack) => NebulaGameManager.Instance?.KillRequestHandler.RequestKill(this, player, playerState, eventDetail, killParams, killCondition, callBack);
    void GamePlayer.Suicide(CommunicableTextTag playerState, CommunicableTextTag? eventDetail, KillParameter killParams, Action<KillResult>? callBack) => NebulaGameManager.Instance?.KillRequestHandler.RequestKill(this, this, playerState, eventDetail, killParams, KillCondition.BothAlive, callBack);
    void GamePlayer.Revive(GamePlayer? healer, Virial.Compat.Vector2 position, bool eraseDeadBody, bool recordEvent) => MyControl.ModRevive(healer?.VanillaPlayer, new(position.x, position.y), eraseDeadBody, recordEvent);
    GamePlayer? GamePlayer.MyKiller => MyKiller;

    // Virial::HoldingAPI

    GamePlayer? GamePlayer.HoldingDeadBody => NebulaGameManager.Instance?.GetPlayer(HoldingDeadBodyId ?? 255);
    bool GamePlayer.HoldingAnyDeadBody => HoldingAnyDeadBody;
    void GamePlayer.HoldDeadBody(Virial.Game.Player? deadBody) => HoldDeadBody(deadBody?.RelatedDeadBody);
    void GamePlayer.HoldDeadBodyFast(DeadBody? deadBody) => HoldDeadBody(deadBody);
    void GamePlayer.ReleaseDeadBody() => ReleaseDeadBody();

    GamePlayer? GamePlayer.HoldingPlayer => null;
    bool GamePlayer.HoldingAnyPlayer => false;
    void GamePlayer.HoldPlayer(Virial.Game.Player? player) { }
    void GamePlayer.ReleaseHoldingPlayer() { }


    //Virial::PlayerAPI

    string GamePlayer.Name => DefaultName;
    Virial.Compat.Vector2 IGameObject.Position => new(MyControl.transform.position);
    Virial.Compat.Vector2 GamePlayer.TruePosition => new(MyControl.GetTruePosition());
    bool GamePlayer.CanMove => MyControl.CanMove;
    bool GamePlayer.IsDisconnected => IsDisconnected;
    float? GamePlayer.DeathTime => DeathTimeStamp;
    CommunicableTextTag GamePlayer.PlayerState => MyState ?? PlayerState.Alive;

    // Virial::AttributeAPI

    void GamePlayer.GainAttribute(IPlayerAttribute attribute, float duration, bool canPassMeeting, int priority, string? duplicateTag)
    {
        if (attribute == PlayerAttributes.Accel || attribute == PlayerAttributes.Decel) return;
        RpcAttrModulator.Invoke(new(PlayerId, new AttributeModulator(attribute, duration, canPassMeeting, priority, duplicateTag), false));
    }
    void GamePlayer.GainAttribute(IPlayerAttribute attribute, float duration, float ratio, bool canPassMeeting, int priority, string? duplicateTag)
    {
        if (attribute == PlayerAttributes.Accel || attribute == PlayerAttributes.Decel) return;
        RpcAttrModulator.Invoke(new(PlayerId, new FloatModulator(attribute, duration, ratio, canPassMeeting, priority, duplicateTag), false));
    }
    void GamePlayer.GainSizeAttribute(Virial.Compat.Vector2 size, float duration, bool canPassMeeting, int priority, string? duplicateTag)
    {
        RpcAttrModulator.Invoke(new(PlayerId, new SizeModulator(size, duration, canPassMeeting, priority, duplicateTag), false));
    }
    void GamePlayer.GainSpeedAttribute(float speedRate, float duration, bool canPassMeeting, int priority, string? duplicateTag) => RpcAttrModulator.Invoke(new(PlayerId, new SpeedModulator(speedRate, Vector2.one, true, duration, canPassMeeting, priority, duplicateTag ?? ""), false));
    IEnumerable<(IPlayerAttribute attribute, float percentage)> GamePlayer.GetAttributes() => GetValidAttributes();

    // Virial::OutfitAPI

    Virial.Game.OutfitDefinition GamePlayer.GetOutfit(int maxPriority) => GetOutfit(maxPriority).Outfit;

    Virial.Game.OutfitDefinition GamePlayer.CurrentOutfit => CurrentOutfit.Outfit;
    Virial.Game.OutfitDefinition GamePlayer.DefaultOutfit => DefaultOutfit.Outfit;

    // Virial::Internal

    PlayerControl GamePlayer.VanillaPlayer => MyControl;
    DeadBody? GamePlayer.RelatedDeadBody { get { if (!relatedDeadBodyCache) relatedDeadBodyCache = null; return relatedDeadBodyCache; } }
    internal DeadBody? relatedDeadBodyCache;

    // Virial::PlayerlikeAPI

    GamePlayer IPlayerlike.RealPlayer => this;
    GamePlayer IPlayerlike.VisualPlayer => this;
}
