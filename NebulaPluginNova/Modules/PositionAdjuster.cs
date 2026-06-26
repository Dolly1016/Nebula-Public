using Nebula.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.Compat;
using Virial.DI;
using Virial.Events.Game.Meeting;
using Virial.Events.Game;
using Virial.Events.Player;
using Virial.Game;
using Virial;
using Nebula.Roles.Impostor;
using static Nebula.Roles.Impostor.Thurifer;
using Virial.Components;
using Nebula.Map;

namespace Nebula.Modules;

[NebulaPreprocess(PreprocessPhase.PostBuildNoS)]
internal class PositionAdjuster : AbstractModule<Virial.Game.Game>, IGameOperator
{
    static PositionAdjuster()
    {
        DIManager.Instance.RegisterModule(() => new PositionAdjuster());
    }
    public PositionAdjuster() => ModSingleton<PositionAdjuster>.Instance = this;
    protected override void OnInjected(Virial.Game.Game container) => this.Register(container);
    static private Image buttonSprite = SpriteLoader.FromResource("Nebula.Resources.Buttons.FixPositionButton.png", 115f);

    private VVector2? FixedPosition = null;
    private bool CanFixPosition => FixedPosition != null;
    private float OutsideTimer = 0f;
    void OnGameStart(GameStartEvent ev)
    {
        if (!(ev.Game.GameMode?.ShowMap ?? true)) return;

        //ボタンを追加する

        Modules.ScriptComponents.ModAbilityButtonImpl fixButton = null!;
        var player = MyContainer.LocalPlayer;


        fixButton = new Modules.ScriptComponents.ModAbilityButtonImpl(true, priority: -10).RegisterPermanently();
        fixButton.SetSprite(buttonSprite.GetSprite());
        fixButton.Availability = (button) => true;
        fixButton.Visibility = (button) => !player.IsDead && player.CanMove && CanFixPosition;
        fixButton.OnClick = (button) => {
            player.VanillaPlayer.NetTransform.RpcSnapTo(FixedPosition!.Value - (VVector2)AmongUsLLImpl.LocalPlayer.Collider.offset);
            FixedPosition = null;
        };
        fixButton.SetLabel("fixPos");
    }


    static private VVector2[] SearchCand = [new(-0.4f, 0f), new(0.4f, 0f), new(0f, -0.4f), new(0f, 0.4f)];
    int mask = Constants.ShipAndAllObjectsMask;
    void OnUpdate(GameUpdateEvent ev)
    {
        if (!(ev.Game.GameMode?.ShowMap ?? true)) return;

        var player = MyContainer.LocalPlayer;

        if (!player.CanMove || player.IsDead)
        {
            FixedPosition = null;
            OutsideTimer = 0f;
            return;
        }

        var unityTruePos = player.UnityTruePosition;
        var truePos = new VVector2(unityTruePos);

        var mapData = MapData.GetCurrentMapData();
        if (mapData.CheckMapArea(truePos, 0f))
        {
            FixedPosition = null;
            OutsideTimer = 0f;
            return;
        }

        OutsideTimer += ev.DeltaTime;
        if(OutsideTimer > 2f)
        {
            if (!FixedPosition.HasValue || FixedPosition.Value.Distance(truePos) > 0.6f)
            {
                FixedPosition = null;
                int index = SearchCand.FindIndex(v => mapData.CheckMapArea(truePos + v, 0f) && Helpers.AnyNonTriggersBetween(unityTruePos, truePos + v, out _, mask));
                if(index >= 0){
                    FixedPosition = truePos + SearchCand[index];
                }
            }
        }
    }
}
