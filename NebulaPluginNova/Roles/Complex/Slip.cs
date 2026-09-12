using Virial;
using Virial.Assignable;
using Virial.Compat;
using Virial.Configuration;
using Virial.Game;

namespace Nebula.Roles.Complex;

[NebulaRPCHolder]
public class Slip : DefinedSingleAbilityRoleTemplate<Slip.Ability>, DefinedRole, IAssignableDocument
{
    private Slip(bool isEvil) : base(
        isEvil ? "evilSlip" : "niceSlip",
        isEvil ? VColor.ImpostorColor : new(59, 130, 140),
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam,
        [isEvil ? EvilSlipCoolDownOption : NiceSlipCoolDownOption])
    {
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!]);

        if (isEvil)
            EvilSlipAction = new("slip.evil", this, isPhysicalAction: true);
        else
            NiceSlipAction = new("slip.nice", this, isPhysicalAction: true);
    }

    public bool IsEvil => Category == RoleCategory.ImpostorRole;
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => IsEvil ? AbilityAssignmentStatus.KillersSide : AbilityAssignmentStatus.CanLoadToMadmate;
    MultipleAssignmentType DefinedRole.MultipleAssignment => IsEvil ? MultipleAssignmentType.Allowed : MultipleAssignmentType.NotAllowed;
    public override Ability CreateAbility(GamePlayer player, int[] arguments) => new(player, arguments.GetAsBool(0), IsEvil);

    static internal GameActionType NiceSlipAction = null!;
    static internal GameActionType EvilSlipAction = null!;

    static private readonly FloatConfiguration NiceSlipCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.niceSlip.slipCoolDown", (5f, 60f, 2.5f), 20f, FloatConfigurationDecorator.Second);
    static private readonly FloatConfiguration EvilSlipCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.evilSlip.slipCoolDown", (5f, 60f, 2.5f), 15f, FloatConfigurationDecorator.Second);

    private const float SlipDuration = 0.9f;

    static public readonly Slip MyNiceRole = new(false);
    static public readonly Slip MyEvilRole = new(true);

    static private readonly GameStatsEntry StatsNiceSlip = NebulaAPI.CreateStatsEntry("stats.niceSlip.slip", GameStatsCategory.Roles, MyNiceRole);
    static private readonly GameStatsEntry StatsEvilSlip = NebulaAPI.CreateStatsEntry("stats.evilSlip.slip", GameStatsCategory.Roles, MyEvilRole);

    static private readonly Image buttonNiceSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.NiceSlipButton.png", 115f);
    static private readonly Image buttonEvilSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.EvilSlipButton.png", 115f);


    static private Image iconImage = new NebulaSpriteLoader("Assets/NebulaAssets/Sprites/Icons/slip.png");
    Image DefinedAssignable.IconImage => iconImage;

    bool IAssignableDocument.HasTips => false;
    bool IAssignableDocument.HasAbility => true;
    IEnumerable<AssignableDocumentImage> IAssignableDocument.GetDocumentImages()
    {
        yield return new(IsEvil ? buttonEvilSprite : buttonNiceSprite, "role.slip.ability.slip");
    }

    IEnumerable<AssignableDocumentReplacement> IAssignableDocument.GetDocumentReplacements()
    {
        yield return new("%SEC%", SlipDuration.DecimalToString("1"));
    }

    internal readonly record struct SlipTarget(VVector2 Approach, VVector2 Destination, bool IsHorizontalSlip);

    //すり抜けの開始、終了位置の距離
    private const float HorizontalSlipDistanceFromDoorCenter = 0.46f;
    private const float VerticalSlipDistanceFromDoorCenter = 0.77f;

    private const float MaxApproachDuration = 1.5f;
    private const float ConsolelessDoorUsableDistance = 1f;

    //Airshipではすり抜けられるドアを制限する
    private const int AirshipMapId = 4;

    static private bool CanSlipDoorOnAirship(OpenableDoor door)
    {
        //SystemTypes.Decontaminationが割り当てられた電気ドアを除外する
        if (door.Room == SystemTypes.Decontamination) return false;

        //ミニゲームを開いて開けるドアのみ許可する
        var doorConsole = door.GetComponent<DoorConsole>();
        return doorConsole.AsBoolFast() && doorConsole.MinigamePrefab.AsBoolFast();
    }

    static private bool CanUseDoorConsole(OpenableDoor door, NetworkedPlayerInfo playerInfo, VVector2 truePosition, out float distance)
    {
        var doorConsole = door.GetComponent<DoorConsole>();
        if (doorConsole.AsBoolFast())
        {
            distance = doorConsole.CanUse(playerInfo, out var canUseDoorConsole, out _);
            return canUseDoorConsole;
        }

        var openDoorConsole = door.GetComponent<OpenDoorConsole>();
        if (openDoorConsole.AsBoolFast())
        {
            distance = openDoorConsole.CanUse(playerInfo, out var canUseOpenDoorConsole, out _);
            return canUseOpenDoorConsole;
        }

        distance = truePosition.Distance((VVector2)door.transform.position);
        return distance <= ConsolelessDoorUsableDistance;
    }

    static private bool TryGetDoorCollider(OpenableDoor door, out BoxCollider2D collider)
    {
        var plainDoor = door.TryCast<PlainDoor>();
        if (plainDoor.AsBoolFast())
        {
            collider = plainDoor!.myCollider;
            return collider.AsBoolFast();
        }

        var mushroomWallDoor = door.TryCast<MushroomWallDoor>();
        if (mushroomWallDoor.AsBoolFast())
        {
            collider = mushroomWallDoor!.wallCollider;
            return collider.AsBoolFast();
        }

        collider = null!;
        return false;
    }

    static private bool TryCalcSlipPositions(BoxCollider2D collider, VVector2 truePosition, out VVector2 approach, out VVector2 destination, out bool isHorizontalSlip)
    {
        approach = destination = VVector2.Zero;

        isHorizontalSlip = collider.size.y > collider.size.x;

        VVector2 center = collider.bounds.center;

        float acrossDiff = isHorizontalSlip ? truePosition.x - center.x : truePosition.y - center.y;

        if (Mathn.Abs(acrossDiff) < 0.01f) return false;

        float distanceFromCenter = isHorizontalSlip ? HorizontalSlipDistanceFromDoorCenter : VerticalSlipDistanceFromDoorCenter;
        float offset = acrossDiff > 0f ? distanceFromCenter : -distanceFromCenter;

        approach = isHorizontalSlip ? new(center.x + offset, center.y) : new(center.x, center.y + offset);
        destination = isHorizontalSlip ? new(center.x - offset, center.y) : new(center.x, center.y - offset);

        return true;
    }

    static internal SlipTarget? SearchSlipTarget(GamePlayer player)
    {
        if (!AmongUsLLImpl.TryGetShipStatus(out var shipStatus)) return null;
        if (player.IsDived) return null;

        var playerInfo = player.VanillaPlayer.Data;
        if (!playerInfo.AsBoolFast()) return null;

        VVector2 truePosition = player.TruePosition;
        VVector2 colliderOffset = truePosition - player.Position;

        SlipTarget? result = null;
        float nearestDistance = float.MaxValue;

        bool isAirship = NebulaAPI.AmongUs.MapId == AirshipMapId;

        foreach (var door in shipStatus.AllDoors.GetFastEnumerator())
        {
            if (door.IsOpen) continue;
            if (isAirship && !CanSlipDoorOnAirship(door)) continue;
            //ドアのコンソールを使用できる状況でのみ、すり抜けの選択肢を取れる
            if (!CanUseDoorConsole(door, playerInfo, truePosition, out var distance)) continue;
            if (!(distance < nearestDistance)) continue;
            if (!TryGetDoorCollider(door, out var collider)) continue;
            if (!TryCalcSlipPositions(collider, truePosition, out var approach, out var destination, out var isHorizontalSlip)) continue;

            nearestDistance = distance;
            result = new(approach - colliderOffset, destination - colliderOffset, isHorizontalSlip);
        }

        return result;
    }

    private const string SlipSizeAttrTag = "nebula::slip::size";
    private const string SlipHiddenNameAttrTag = "nebula::slip::hiddenName";
    private const string SlipVisibleThroughWallAttrTag = "nebula::slip::visibleThroughWall";

    //進行方向と垂直な向きの潰し具合
    private const float HorizontalSlipSquash = 0.06f;
    private const float VerticalSlipSquash = 0.06f;

    //すり抜け終了時の逆向きの変形
    private const float SlipRecoilDuration = 0.2f;
    private const float SlipRecoilShrinkRate = 0.25f;
    private const float SlipRecoilExpandRate = 0.45f;

    //引き伸ばし量調整のための定数
    private const float HorizontalSlipBodyLength = 0.28f;
    private const float VerticalSlipBodyLength = 0.7f;

    static private float CalcSlipStretch(float p, float slipDistance, float bodyLength)
        => 1f + slipDistance * Mathn.Min(p, 1f - p) / bodyLength;

    static private IEnumerator CoSlip(GamePlayer player, VVector2 approach, VVector2 destination, bool isHorizontalSlip)
    {
        if (player == null) yield break;
        var vanillaPlayer = player.VanillaPlayer;
        if (!vanillaPlayer.AsBoolFast()) yield break;

        vanillaPlayer.ChangeMoveMode(false);

        //まずはドアの手前(ドアの中心の正面)まで歩いて移動する
        var walk = vanillaPlayer.MyPhysics.WalkPlayerTo(approach, 0.01f, 1f, true).WrapToManaged();
        float walkTime = 0f;
        while (walkTime < MaxApproachDuration && walk.MoveNext())
        {
            if (MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast() || player.IsDead) break;
            walkTime += Time.deltaTime;
            yield return null;
        }

        yield return Effects.Wait(0.2f);

        vanillaPlayer.SetKinematic(true);
        vanillaPlayer.MyPhysics.body.velocity = UnityEngine.Vector2.zero;

        if (player.AmOwner) NebulaAsset.PlaySE(NebulaAudioClip.Slip, false, 0.85f);
        
        //各種モジュレータ
        SizeModulator sizeModulator = new(VVector2.One, 10000f, false, 100, SlipSizeAttrTag, false, false);
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, sizeModulator, true));
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, new AttributeModulator(PlayerAttributes.HiddenName, 10000f, false, 0, SlipHiddenNameAttrTag), true));
        PlayerModInfo.RpcAttrModulator.LocalInvoke((player.PlayerId, new AttributeModulator(PlayerAttributes.VisibleThroughWall, 10000f, false, 0, SlipVisibleThroughWallAttrTag), true));

        //向き調整
        if (isHorizontalSlip) vanillaPlayer.MyPhysics.FlipX = destination.x < approach.x;

        float slipDistance = approach.Distance(destination);
        float bodyLength = isHorizontalSlip ? HorizontalSlipBodyLength : VerticalSlipBodyLength;
        float squashSize = isHorizontalSlip ? HorizontalSlipSquash : VerticalSlipSquash;
        float p = 0f;
        bool interrupted = false;

        while (p < 1f)
        {
            p = Mathn.Min(1f, p + Time.deltaTime / SlipDuration);

            float stretch = CalcSlipStretch(p, slipDistance, bodyLength);
            float squash = Mathn.Lerp(1f, squashSize, Mathn.Sin(p * Mathn.PI));
            sizeModulator.Size = isHorizontalSlip ? new(stretch, squash) : new(squash, stretch);

            var current = VVector2.Lerp(approach, destination, p);
            vanillaPlayer.transform.position = current.AsGameWorldUnityVector3();
            vanillaPlayer.MyPhysics.body.velocity = UnityEngine.Vector2.zero;

            //会議、あるいは死亡時
            if (MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast() || player.IsDead)
            {
                interrupted = true;
                break;
            }

            yield return null;
        }

        vanillaPlayer.transform.position = destination.AsGameWorldUnityVector3();
        vanillaPlayer.MyPhysics.body.velocity = UnityEngine.Vector2.zero;

        if (player.AmOwner) NebulaAsset.PlaySE(NebulaAudioClip.SlipPop, false, 0.95f);

        //戻る勢いで、伸ばした方向は少し縮み、潰した方向は少し伸びてから元に戻る
        float q = 0f;
        while (!interrupted && q < 1f)
        {
            q = Mathn.Min(1f, q + Time.deltaTime / SlipRecoilDuration);

            float recoil = Mathn.Sin(q * Mathn.PI);
            float shrink = 1f - SlipRecoilShrinkRate * recoil;
            float expand = 1f + SlipRecoilExpandRate * recoil;
            sizeModulator.Size = isHorizontalSlip ? new(shrink, expand) : new(expand, shrink);

            if (MeetingHud.Instance.AsBoolFast() || ExileController.Instance.AsBoolFast() || player.IsDead) break;

            yield return null;
        }

        //各種モジュレータ削除
        sizeModulator.Size = VVector2.One;
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, SlipSizeAttrTag));
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, SlipHiddenNameAttrTag));
        PlayerModInfo.RpcRemoveAttrByTag.LocalInvoke((player.PlayerId, SlipVisibleThroughWallAttrTag));

        vanillaPlayer.ChangeMoveMode(true);
    }

    static private readonly RemoteProcess<(GamePlayer player, VVector2 approach, VVector2 destination, bool isHorizontalSlip)> RpcSlip = new(
        "SlipThroughDoor",
        (message, _) =>
        {
            NebulaManager.Instance.StartCoroutine(CoSlip(message.player, message.approach, message.destination, message.isHorizontalSlip).WrapToIl2Cpp());
        }
        );

    public class Ability : AbstractPlayerUsurpableAbility, IPlayerAbility
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];

        public Ability(GamePlayer player, bool isUsurped, bool isEvil) : base(player, isUsurped)
        {
            if (AmOwner)
            {
                SlipTarget? target = null;

                var slipButton = NebulaAPI.Modules.AbilityButton(this, MyPlayer, Virial.Compat.VirtualKeyInput.Ability, "slip",
                    isEvil ? EvilSlipCoolDownOption : NiceSlipCoolDownOption, "slip", isEvil ? buttonEvilSprite : buttonNiceSprite,
                    _ => target != null).SetAsUsurpableButton(this);

                slipButton.OnUpdate = _ => target = SearchSlipTarget(MyPlayer);
                slipButton.OnClick = (button) =>
                {
                    if (!target.HasValue) return;

                    NebulaGameManager.Instance?.RpcDoGameAction(MyPlayer, MyPlayer.Position, isEvil ? EvilSlipAction : NiceSlipAction);
                    RpcSlip.Invoke((MyPlayer, target.Value.Approach, target.Value.Destination, target.Value.IsHorizontalSlip));
                    (isEvil ? StatsEvilSlip : StatsNiceSlip).Progress();

                    button.StartCoolDown();
                };
            }
        }
    }
}
