using Nebula.Game.Statistics;
using Nebula.Modules;
using Nebula.Utilities;
using PowerTools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Events.Game;
using Virial.Events.Game.Meeting;
using Virial.Events.Player;
using Virial.Game;

namespace Nebula.Roles.Complex;

[NebulaRPCHolder]
public class Nexus : DefinedSingleAbilityRoleTemplate<IUsurpableAbility>, DefinedRole
{
    private Nexus(bool isEvil) : base(
        isEvil ? "evilNexus" : "niceNexus",
        isEvil ? new(Palette.ImpostorRed) : new(229, 158, 255),
        isEvil ? RoleCategory.ImpostorRole : RoleCategory.CrewmateRole,
        isEvil ? Impostor.Impostor.MyTeam : Crewmate.Crewmate.MyTeam,
        [])//[NumOfChargesOption, isEvil ? CostOfKillTrapOption : CostOfCommTrapOption, PlaceCoolDownOption, PlaceDurationOption, SpeedTrapSizeOption, isEvil ? KillTrapSizeOption : CommTrapSizeOption, SpeedTrapDurationOption, AccelRateOption, DecelRateOption])
    {
        //IsEvil = isEvil;

        //if (IsEvil) ConfigurationHolder?.AppendConfiguration(KillTrapSoundDistanceOption);
        ConfigurationHolder?.ScheduleAddRelated(() => [isEvil ? MyNiceRole.ConfigurationHolder! : MyEvilRole.ConfigurationHolder!]);

        /*
        if (IsEvil)
            GameActionTypes.EvilTrapPlacementAction = new("trapper.placement.evil", this, isPlacementAction: true);
        else
            GameActionTypes.NiceTrapPlacementAction = new("trapper.placement.nice", this, isPlacementAction: true);
        */
    }


    public bool IsEvil => Category == RoleCategory.ImpostorRole;
    AbilityAssignmentStatus DefinedRole.AssignmentStatus => IsEvil ? AbilityAssignmentStatus.KillersSide : AbilityAssignmentStatus.CanLoadToMadmate;
    public override IUsurpableAbility CreateAbility(GamePlayer player, int[] arguments) => IsEvil ? new EvilAbility(player, arguments.GetAsBool(0)) : new NiceAbility(player, arguments.GetAsBool(0));

    //static internal readonly IntegerConfiguration NumOfChargesOption = NebulaAPI.Configurations.Configuration("options.role.trapper.numOfCharges", (1, 15), 3);
    //static internal readonly FloatConfiguration PlaceCoolDownOption = NebulaAPI.Configurations.Configuration("options.role.trapper.placeCoolDown", (5f, 60f, 5f), 20f, FloatConfigurationDecorator.Second);
    //static internal readonly FloatConfiguration PlaceDurationOption = NebulaAPI.Configurations.Configuration("options.role.trapper.placeDuration", (1f, 3f, 0.5f), 2f, FloatConfigurationDecorator.Second);
    //static private readonly FloatConfiguration SpeedTrapDurationOption = NebulaAPI.Configurations.Configuration("options.role.trapper.speedDuration", (2.5f, 40f, 2.5f), 10f, FloatConfigurationDecorator.Second);

    //static private readonly FloatConfiguration SpeedTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.trapper.speedTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    //static private readonly FloatConfiguration CommTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.niceTrapper.commTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);
    //static private readonly FloatConfiguration KillTrapSizeOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.killTrapSize", (0.25f, 5f, 0.25f), 1f, FloatConfigurationDecorator.Ratio);

    //static private readonly FloatConfiguration AccelRateOption = NebulaAPI.Configurations.Configuration("options.role.trapper.accelRate", (1f, 5f, 0.125f), 1.5f, FloatConfigurationDecorator.Ratio);
    //static private readonly FloatConfiguration DecelRateOption = NebulaAPI.Configurations.Configuration("options.role.trapper.decelRate", (0.125f, 1f, 0.125f), 0.5f, FloatConfigurationDecorator.Ratio);

    //static internal readonly FloatConfiguration KillTrapSoundDistanceOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.killTrapSoundDistance", (0f, 20f, 2.5f), 10f, FloatConfigurationDecorator.Ratio);
    //static private readonly IntegerConfiguration CostOfKillTrapOption = NebulaAPI.Configurations.Configuration("options.role.evilTrapper.costOfKillTrap", (1, 5), 2);
    //static private readonly IntegerConfiguration CostOfCommTrapOption = NebulaAPI.Configurations.Configuration("options.role.niceTrapper.costOfCommTrap", (1, 5), 2);

    static public readonly Nexus MyNiceRole = null;// new(false);
    static public readonly Nexus MyEvilRole = null;// new(true);


    [NebulaPreprocess(PreprocessPhase.PostRoles)]
    public class NexusGate : NebulaSyncStandardObject, IGameOperator
    {
        public const string MyGlobalTag = "NexusGlobal";
        //public const string MyLocalTag = "TrapLocal";

        private Vector2 TeleportTo;

        public NexusGate(Vector2 pos, Vector2 teleportTo) : base(pos, ZOption.Back, true, null)
        {
            TeleportTo = teleportTo;
        }

        
        static NexusGate()
        {
            NebulaSyncObject.RegisterInstantiater(MyGlobalTag, (args) => new NexusGate(new(args[0], args[1]), new(args[2], args[3])));
            //NebulaSyncObject.RegisterInstantiater(MyLocalTag, (args) => new Trap(new(args[1], args[2]), (int)args[0], true));
        }

        void Update(GameUpdateEvent ev)
        {
            
        }

        
        static public void ShowAnimDebug()
        {
            Vector2 pos = GamePlayer.LocalPlayer!.Position.ToUnityVector() + new Vector2(1f, 0f);
            NexusGate gate = new(pos, new(0f, 0f));
            gate.CoMovePlayer(GamePlayer.LocalPlayer).StartOnScene();
        }

        IEnumerator CoMovePlayer(GamePlayer player) {
            player.VanillaPlayer.ChangeMoveMode(false);
            player.VanillaPlayer.SetKinematic(true);
            yield return player.VanillaPlayer.MyPhysics.WalkPlayerTo(Position, ignoreColliderOffset: true);
            player.VanillaPlayer.transform.position = Position.AsVector3(Position.y / 1000f);

            SimpleLifespan lifespan = new();
            GameOperatorManager.Instance?.Subscribe<PlayerFixZPositionEvent>(ev=> {
                if (ev.Player == player) {
                    ev.Z = -15f;
                }
            }, lifespan);

            SimpleLifespan lightLifespan = new();
            float lightRange = 1f;
            GameOperatorManager.Instance?.Subscribe<LightRangeUpdateEvent>(ev =>
            {
                if (lifespan.IsDeadObject)
                    lightRange = Mathn.Clamp01(lightRange + Time.deltaTime * 7f);
                else
                    lightRange = Mathn.Clamp01(lightRange - Time.deltaTime * 1.5f);
                ev.LightQuickRange *= lightRange;
            }, lightLifespan);

            var teleportEffect = CoShowTeleportEffect();
            teleportEffect.animator.StartOnScene();
            yield return ManagedEffects.Wait(0.1f);
            var anim = player.VanillaPlayer.MyPhysics.Animations;
            var animator = anim.Animator;
            var jumpAnim = anim.group.EnterVentAnim;
            animator.Play(jumpAnim, 0.8f);
            IEnumerator CoControlAnim()
            {
                while(animator.m_currAnim == jumpAnim)
                {
                    if(animator.FrameTime >= 5)
                    {
                        animator.Stop();
                        break;
                    }
                    yield return null;
                }
            }
            CoControlAnim().StartOnScene();
            yield return ManagedEffects.Wait(0.6f);
            teleportEffect.objectBreaker.Invoke();
            lifespan.Release();
            if (!MeetingHud.Instance)
            {
                Vector2 diff = HudManager.Instance.PlayerCam.transform.position - player.VanillaPlayer.transform.position;
                player.VanillaPlayer.transform.position = TeleportTo.AsVector3(TeleportTo.y / 1000f); //ベントアニメーション中なのでSnapToが使えない
                HudManager.Instance.PlayerCam.centerPosition = TeleportTo - diff;
                HudManager.Instance.PlayerCam.transform.position = TeleportTo - diff;
            }
            player.VanillaPlayer.ChangeMoveMode(true);
            player.VanillaPlayer.MyPhysics.ResetAnimState();

            yield return ManagedEffects.Wait(1f);
            lightLifespan.Release();

            yield return null;
            


        }

        (IEnumerator animator, Action objectBreaker) CoShowTeleportEffect()
        {
            float effectSize = 8f;
            var camera = UnityHelper.CreateRenderingCamera("NexusCamera", null, TeleportTo.AsVector3(0f), effectSize, NebulaGameManager.Instance!.WideCamera.Camera.cullingMask & ~(1 << LayerExpansion.GetDrawShadowsLayer()));
            var shadowCamera = UnityHelper.CreateRenderingCamera("NexusCamera", camera.transform, Vector3.zero, effectSize, AmongUsUtil.GetShadowCollab().ShadowCamera.cullingMask);
            shadowCamera.backgroundColor = new(0f, 0f, 0f, 0f);
            camera.nearClipPlane = -12f;
            shadowCamera.nearClipPlane = -12f;

            var group = UnityHelper.CreateObject<SortingGroup>("NexusGateEffect", null, Position.AsVector3(-3f));

            Material material = new(NebulaAsset.MeshRendererUVMaskedShader);
            var mesh = UnityHelper.CreateMeshRenderer("MeshRenderer", group.transform, new(0f, 0f, 0f), LayerExpansion.GetDefaultLayer(), null, null);
            mesh.filter.CreateCircleMesh(new(effectSize * 2f, effectSize * 2f), 16);
            mesh.renderer.sharedMaterial = material;
            mesh.renderer.sharedMaterial.mainTexture = camera.SetCameraRenderTexture(1000, 1000);
            mesh.renderer.bounds = new(group.transform.position, new(effectSize * 2f, effectSize * 2f, 0f));

            var shadowMesh = UnityHelper.CreateMeshRenderer("MeshRenderer", group.transform, new(0f, 0f, 0f), LayerExpansion.GetShadowLayer(), null, null);
            shadowMesh.filter.CreateCircleMesh(new(effectSize * 2f, effectSize * 2f), 16);
            shadowMesh.renderer.sharedMaterial = material;
            shadowMesh.renderer.sharedMaterial.mainTexture = shadowCamera.SetCameraRenderTexture(1000, 1000);
            shadowMesh.renderer.bounds = new(group.transform.position, new(effectSize * 2f, effectSize * 2f, 0f));

            material.SetFloat("_Threshold", 0f);

            return (ManagedEffects.Lerp(1f, p =>
            {
                if (material) material.SetFloat("_Threshold", p);
            }), ()=>
            {
                GameObject.Destroy(group.gameObject);
                GameObject.Destroy(camera.gameObject);
                GameObject.Destroy(material);
            }
            );
        }
    }

    public class NiceAbility : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public NiceAbility(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            NebulaManager.Instance.StartDelayAction(1f, NexusGate.ShowAnimDebug);
        }
    }

    public class EvilAbility : AbstractPlayerUsurpableAbility, IPlayerAbility, IGameOperator
    {
        int[] IPlayerAbility.AbilityArguments => [IsUsurped.AsInt()];
        public EvilAbility(GamePlayer player, bool isUsurped) : base(player, isUsurped)
        {
            
        }
    }
}

