using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Virial.DI;
using Virial.Game;
using Virial;
using System.Diagnostics.CodeAnalysis;
using Virial.Events.Game;
using Nebula.Map;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Components;
using Virial.Configuration;

namespace Nebula.Roles.Perks;

internal class VentRelocate : PerkFunctionalInstance
{
    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    [NebulaRPCHolder]
    private class VentHolderManager : AbstractModule<Virial.Game.Game>, IGameOperator
    {
        static VentHolderManager() => DIManager.Instance.RegisterModule(() => new VentHolderManager());
        private VentHolderManager()
        {
            ModSingleton<VentHolderManager>.Instance = this;
        }
        protected override void OnInjected(Virial.Game.Game container)
        {
            this.Register(container);
        }

        public Dictionary<int, GamePlayer> VentHolders = [];
        private Vent? currentLocalHolding = null;

        public void RequestHoldVent(Vent vent)
        {
            RpcCheckHoldVent.Invoke((GamePlayer.LocalPlayer, vent.Id));
        }
        public void RequestReleaseVent(Vent vent)
        {
            RpcReleaseVent.Invoke((GamePlayer.LocalPlayer, vent.Id, vent.transform.position));
        }
        private void CheckHoldVent(GamePlayer player, Vent vent)
        {
            if (VentHolders.ContainsKey(vent.Id)) return;
            if (VentHolders.Any(entry => entry.Value.PlayerId == player.PlayerId)) return;//同時に1つのベントしか掴めない

            RpcHoldVent.Invoke((player, vent.Id));
        }

        private void HoldVent(GamePlayer player, Vent vent)
        {
            VentHolders.Add(vent.Id, player);
            if (player.AmOwner) currentLocalHolding = vent;
        }

        private void CheckAndReleaseVent(GamePlayer player, Vent vent, Vector3 pos)
        {
            if(VentHolders.TryGetValue(vent.Id, out var holder) && player.PlayerId == holder.PlayerId)
            {
                VentHolders.Remove(vent.Id);
                vent.transform.position = pos;

                if (player.AmOwner && currentLocalHolding?.Id == vent.Id) currentLocalHolding = null;
            }
        }

        public bool AnyoneHolds(Vent vent) => VentHolders.ContainsKey(vent.Id);
        static private bool TryGetVent(int id, [MaybeNullWhen(false)] out Vent vent) => ShipStatus.Instance.AllVents.Find(v => v.Id == id, out vent);
        static private RemoteProcess<(GamePlayer player, int ventId)> RpcCheckHoldVent = new("PlumberCheckHoldVent", (message, _) =>
        {
            if (Helpers.AmHost(PlayerControl.LocalPlayer) && TryGetVent(message.ventId, out var vent)) ModSingleton<VentHolderManager>.Instance.CheckHoldVent(message.player, vent);
        });

        static private RemoteProcess<(GamePlayer player, int ventId)> RpcHoldVent = new("PlumberHoldVent", (message, _) =>
        {
            if (TryGetVent(message.ventId, out var vent)) ModSingleton<VentHolderManager>.Instance.HoldVent(message.player, vent);
        });

        static private RemoteProcess<(GamePlayer player, int ventId, Vector3 pos)> RpcReleaseVent = new("PlumberReleaseVent", (message, _) =>
        {
            if (TryGetVent(message.ventId, out var vent)) ModSingleton<VentHolderManager>.Instance.CheckAndReleaseVent(message.player, vent, message.pos);
        });

        private bool CheckVentPosition(Vector3 position)
        {
            var data = MapData.GetCurrentMapData();
            return data.CheckMapArea(position, 0.3f);
        }
        void OnUpdate(GameHudUpdateEvent ev)
        {
            if (!MeetingHud.Instance)
            {
                if (currentLocalHolding != null)
                {
                    var currentPos = currentLocalHolding.transform.position;
                    var targetPos = GamePlayer.LocalPlayer.VanillaPlayer.GetTruePosition();

                    Vector3 nextPos;
                    if (currentPos.Distance(targetPos) < 0.7f)
                        nextPos = currentPos + (Vector3)((Vector2)(targetPos - (Vector2)currentPos).Delta(4f, 0.02f));
                    else
                        nextPos = targetPos;

                    nextPos.z = nextPos.y / 1000f + 0.01f;

                    if (CheckVentPosition(nextPos)) currentLocalHolding.transform.position = nextPos;
                }
            }
        }

        void OnDead(PlayerDieEvent ev)
        {
            if (ev.Player.AmOwner && currentLocalHolding != null) RequestReleaseVent(currentLocalHolding);
        }

        void OnMeetingStart(MeetingPreStartEvent ev)
        {
            if (currentLocalHolding != null) RequestReleaseVent(currentLocalHolding);
        }
    }



    static private float Duration => DurationOption;
    static private FloatConfiguration DurationOption = NebulaAPI.Configurations.Configuration("perk.ventRelocate.duration", (2.5f, 30f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    static PerkFunctionalDefinition def = new("ventRelocate", PerkFunctionalDefinition.Category.Standard, new PerkDefinition("ventRelocate", 7, 56, new(230, 30, 30)).DurationText("%D%", ()=>Duration), (def, instance) => new VentRelocate(def, instance), [DurationOption]);
    private GameTimer? durationTimer = null;
    private bool canUse => durationTimer == null;
    private Vent? holdingVent = null;
    private ObjectTracker<Vent> ventTracker;

    private VentRelocate(PerkDefinition def, PerkInstance instance) : base(def, instance)
    {
        ventTracker = ObjectTrackers.ForVents(1f, MyPlayer, vent => canUse && !ModSingleton<VentHolderManager>.Instance.AnyoneHolds(vent), MyPlayer.Role.Role.UnityColor).Register(this);
    }

    private float holdTime = 0f;
    public override bool HasAction => true;
    public override void OnClick()
    {
        if (MyPlayer.IsDead) return;

        if (canUse)
        {
            if (ventTracker.CurrentTarget != null)
            {
                durationTimer = NebulaAPI.Modules.Timer(this, Duration);
                durationTimer.Start();
                PerkInstance.BindTimer(durationTimer);
                holdTime = NebulaGameManager.Instance!.CurrentTime;
                holdingVent = ventTracker.CurrentTarget;
                ModSingleton<VentHolderManager>.Instance.RequestHoldVent(holdingVent);
            }
        }else if (durationTimer!.IsProgressing && holdingVent != null && NebulaGameManager.Instance!.CurrentTime - holdTime > 0.5f)
        {
            ModSingleton<VentHolderManager>.Instance.RequestReleaseVent(holdingVent);
            PerkInstance.BindTimer(null);
            holdingVent = null;
        }
    }

    void OnUpdate(GameUpdateEvent ev)
    {
        if(durationTimer != null && !durationTimer.IsProgressing && holdingVent != null)
        {
            ModSingleton<VentHolderManager>.Instance.RequestReleaseVent(holdingVent);
            holdingVent = null;
        }

        PerkInstance.SetDisplayColor((canUse ? ventTracker.CurrentTarget != null : durationTimer!.IsProgressing) ? Color.white : Color.gray);
    }

    void OnRoleChanged(PlayerRoleSetEvent ev)
    {
        if (ev.Player.AmOwner) ventTracker.SetColor(ev.Player.Role.Role.UnityColor);
    }
}
