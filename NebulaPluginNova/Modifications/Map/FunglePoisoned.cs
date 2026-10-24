using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial;
using Virial.Events.Game.Minimap;

namespace Nebula.Modifications.Map;

static internal class FunglePoisoned
{
    /// <summary>ShipStatus.Awake の前。ここで非アクティブにしたコンソールは AllConsoles に入らない</summary>
    static public void ModifyEarliest(ShipStatus ship)
    {
        ship.InitialSpawnCenter = ship.MeetingSpawnCenter;
        DisableTaskConsoles(ship, "Storage", "DropshipRuins", "RecRoom", "Kitchen", "Dock", "Cafeteria");
        RemovePoisonedTasks(ship);
    }

    /// <summary>
    /// 部屋ごとの TaskConsoles を非アクティブにする。ShipStatus.Awake より前に呼べば、
    /// 中のコンソールは AllConsoles に入らず、タスクの行き先に選ばれなくなる。部屋の見た目はそのまま
    /// </summary>
    static private void DisableTaskConsoles(ShipStatus ship, params string[] rooms)
    {
        foreach (var room in rooms)
            if (ship.transform.TryFindChild(out var consoles, "Rooms", room, "TaskConsoles")) consoles.gameObject.SetActive(false);
    }

    static public void ModifyEarlier(ShipStatus ship)
    {
    }

    static public void Modify(ShipStatus ship)
    {
        AddNavigationConsole(ship);
        AddAboveGreenhouseCopy(ship);
        CloseMeetingRoomGap(ship);
        DisableZipline(ship);
        ConnectJungleVents(ship);
        SubscribeMinimap();
    }

    /// <summary>RecRoom の navigationconsole をもう 1 つ置く</summary>
    static internal void AddNavigationConsole(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var original, "Rooms", "RecRoom", "Obstacles", "navigationconsole")) return;
        var copy = GameObject.Instantiate(original, original.parent);
        copy.name = original.name + "_copy";
        copy.position = new(-5.0265f, 0.3173f, 1f);
    }

    /// <summary>ジャングルの AboveGreenhouse（結晶の岩）をもう 1 つ置き、その影を上へ伸ばす</summary>
    static internal void AddAboveGreenhouseCopy(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var original, "Outside", "OutsideJungle", "JungleObstacles", "AboveGreenhouse")) return;
        var copy = GameObject.Instantiate(original, original.parent);
        copy.name = original.name + "_copy";
        copy.position = new(-7.8809f, -14.54f, 1.1f);

        // 子のうち一番上の Crystal の影（Shadows, 4 点）で、いちばん上にある 2 番目の点から真上へ 1.2 伸ばす
        if (!copy.TryFindChild(out var shadows, "Crystal", "Shadows")) return;
        var top = shadows.GetComponent<EdgeCollider2D>().points[2];
        var extension = shadows.gameObject.AddComponent<EdgeCollider2D>();
        extension.SetPoints(new Vector2[] { top, top + (Vector2)shadows.InverseTransformVector(new(0f, 1.2f, 0f)) }.ToIl2CppList());
    }

    /// <summary>MeetingRoom の LeftWall と TopWall の間の隙間をふさぐ</summary>
    static internal void CloseMeetingRoomGap(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var walls, "Rooms", "MeetingRoom", "Walls")) return;

        // LeftWall の最上部（28 点中 21 番目）と TopWall の最左部（28 点中 3 番目）を結ぶ。
        // 間の点は、両端の中点から右下へ垂直に 0.6 ずらした位置。ワールド座標
        Vector2[] points = [new(-6.3987f, -0.0698f), new(-5.2992f, -0.5351f), new(-4.3508f, 0.1900f)];

        var collider = UnityHelper.CreateObject<EdgeCollider2D>("GapWall", walls, Vector3.zero, LayerExpansion.GetShipLayer());
        collider.SetPoints(points.Select(p => (Vector2)collider.transform.InverseTransformPoint(p)).ToArray().ToIl2CppList());
    }

    static internal void RemovePoisonedTasks(ShipStatus ship)
    {
        // Long 2（ExtractFuel）は Short に移す。番号で消すので、先に Short へ足してから Long から消す
        ship.ShortTasks = ship.ShortTasks.Append(ship.LongTasks[2]).ToArray();
        ShipModification.RemoveTasks(ship, [1, 2, 4, 5, 8, 14], [0, 1, 2, 7, 8], [3]);
    }

    /// <summary>配られたタスクの Initialize の後（MinigamePatch の TaskStepPatch から呼ばれる）。インスタンスの値を直接書き換える</summary>
    static internal void OnTaskInitialized(NormalPlayerTask task)
    {
        // ExtractFuel は 2 段目（Storage）を消したので、1 段目（UpperEngine）で終わる 1 段のタスクにする
        // ShortTasks に移したので、長さも Short にする
        if (task.TaskType == TaskTypes.ExtractFuel)
        {
            task.MaxStep = 1;
            task.ShowTaskStep = false;
            task.Length = NormalPlayerTask.TaskLength.Short;
        }
    }

    #region TaskArrow
    /// <summary>
    /// NormalPlayerTask.UpdateArrowAndLocation の代わり（MinigamePatch の TaskArrowAndLocationPatch から呼ばれる）。
    /// true を返すと元の処理に任せる
    /// </summary>
    static internal bool UpdateTaskArrowAndLocation(NormalPlayerTask task)
    {
        if (task.TaskType == TaskTypes.ReplaceParts) return UpdateReplacePartsArrow(task);
        return true;
    }

    /// <summary>
    /// ReplaceParts の次の行き先。元の処理は「Reactor の次は Comms、UpperEngine の次は Dropship」と決め打ちで、
    /// 行き先のコンソールを消すと例外になる。ここでは、いまの段に使える残ったコンソールからランダムに選ぶ。
    /// それ以外（矢印の出し方など）は元の処理と同じ
    /// </summary>
    static private bool UpdateReplacePartsArrow(NormalPlayerTask task)
    {
        if (!task.Arrow.AsBoolFast()) return false;
        if (!task.Owner.AmOwner || task.IsComplete)
        {
            task.Arrow.gameObject.SetActive(false);
            return false;
        }

        if (PlayerTask.PlayerHasTaskOfType<IHudOverrideTask>(PlayerControl.LocalPlayer)) task.arrowSuspended = true;
        else task.Arrow.gameObject.SetActive(true);

        var candidates = ShipStatus.Instance.AllConsoles.Where(c => c.ValidTasks.Length > 0 && c.ValidTasks[0].taskType == TaskTypes.ReplaceParts && c.ValidTasks[0].taskStep.Contains(task.taskStep)).ToArray();
        if (candidates.Length == 0)
        {
            task.Arrow.gameObject.SetActive(false);
            return false;
        }

        var next = candidates[System.Random.Shared.Next(candidates.Length)];
        task.Arrow.target = next.transform.position;
        task.StartAt = next.Room;
        task.Data[0] = (byte)next.ConsoleId;
        task.LocationDirty = true;
        return false;
    }
    #endregion

    static internal void SubscribeMinimap()
    {
        GameOperatorManager.Instance?.Subscribe<MapInstantiateEvent>(ev => ModifyMinimap(MapBehaviour.Instance), NebulaAPI.CurrentGame!);
        GameOperatorManager.Instance?.Subscribe<MapOpenInternalEvent>(ev =>
        {
            // 消した部屋の分だけ寄せて、マップを画面の中ほどに表示する
            ev.map.transform.localPosition = new(-1.3f, 0f, -50f);
            ev.map.transform.localScale = new(0.9f, 0.9f, 1f);
            ev.map.GetComponent<AspectSize>().PercentWidth = 0.9f;
            CenterSabotageText(ev.map);
        }, NebulaAPI.CurrentGame!);
    }

    /// <summary>
    /// マップをずらしたぶん SabText（アドミンの「通信妨害中」など）が中央から外れるので、画面の中央に戻す。
    /// 文字の位置 = マップの位置 + マップの scale × CountOverlay の scale × 文字のローカル座標 なので、これが 0 になるようにする。
    /// マップの反転（FlipX / FlipY）はこのイベントの後の UpdateScale で掛かるので、その符号も先に入れておく。
    /// Nebula の「使用不可」の文字は SabText の複製（SabText_TMP(Clone)）なので、同じ位置にそろえる
    /// </summary>
    static private void CenterSabotageText(MapBehaviour map)
    {
        var flip = MapBehaviourExtension.GetMinimapFlippedScale();
        var mapPos = map.transform.localPosition;
        var mapScale = map.transform.localScale;
        var overlay = map.countOverlay.transform;
        float x = -mapPos.x / (Mathf.Abs(mapScale.x) * Mathf.Sign(flip.x) * overlay.localScale.x);
        float y = -mapPos.y / (Mathf.Abs(mapScale.y) * Mathf.Sign(flip.y) * overlay.localScale.y);

        var original = map.countOverlay.SabotageText.transform;
        for (int i = 0; i < overlay.childCount; i++)
        {
            var text = overlay.GetChild(i);
            if (text == original || text.name.StartsWith(original.name + "(Clone)")) text.localPosition = new(x, y, text.localPosition.z);
        }
    }

    /// <summary>
    /// ミニマップ（FungleMap）の子: 3 CountOverlay / 5 RoomNames / 6 InfectedOverlay。
    /// Storage, DropShip, RecRoom, Kitchen, Dock, Cafeteria を非表示にする。互換性のため削除はしない
    /// </summary>
    static internal void ModifyMinimap(MapBehaviour map)
    {
        foreach (var i in new[] { 2, 4, 8, 9, 10, 13 }) map.transform.GetChild(3).GetChild(i).gameObject.SetActive(false);
        // アイコンは CounterArea ではなく CountOverlay の下に出るので、非表示にしただけでは数えられてしまう。
        // オブジェクトは残したまま、数える対象（CountAreas）から外す
        map.countOverlay.CountAreas = map.countOverlay.CountAreas.Where(area => area.gameObject.activeSelf).ToArray();
        foreach (var i in new[] { 1, 3, 7, 8, 15, 10 }) map.transform.GetChild(5).GetChild(i).gameObject.SetActive(false);
        // InfectedOverlay にあるのは Storage, Kitchen だけ
        foreach (var i in new[] { 2, 5 }) map.transform.GetChild(6).GetChild(i).gameObject.SetActive(false);
    }

    /// <summary>ジャングル西側の 2 つのベント（SouthWest と NorthWest）を相互につなぐ</summary>
    static internal void ConnectJungleVents(ShipStatus ship)
    {
        if (!ship.transform.TryFindChild(out var southWest, "Outside", "OutsideJungle", "Vents", "SouthWestJungleVent")) return;
        if (!ship.transform.TryFindChild(out var northWest, "Outside", "OutsideJungle", "Vents", "NorthWestJungleVent")) return;
        var southWestVent = southWest.GetComponent<Vent>();
        var northWestVent = northWest.GetComponent<Vent>();

        southWestVent.Left = northWestVent;
        northWestVent.Left = southWestVent;
        northWestVent.Right = null;
    }

    static internal void DisableZipline(ShipStatus ship)
    {
        if (ship.transform.TryFindChild(out var zipline, "Zipline")) zipline.gameObject.SetActive(false);
    }
}
