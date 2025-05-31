using Nebula.Behavior;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Virial;
using Virial.Assignable;
using Virial.Configuration;
using Virial.Game;
using Virial.Media;
using Virial.Text;

namespace Nebula.Configuration;

[NebulaPreprocess(PreprocessPhase.PostRoles)]
public static class GeneralConfigurations
{
    public enum MapOptionType
    {
        Vent = 0,
        Console = 1,
        Blueprint = 2,
        Wiring = 3,
        Light = 4,
        BlackOut = 6,
        Door = 7,
        Float = -1
    }

    static GeneralConfigurations()
    {
        for(int i = 0; i < NebulaPreSpawnLocation.Locations.Length; i++)
        {
            var mapName = NebulaPreSpawnLocation.MapName[i].HeadLower();
            var locs = NebulaPreSpawnLocation.Locations[i];
            locs.Do(l => l.Configuration = NebulaAPI.Configurations.SharableVariable("location." + mapName + "." + l.LocationName.HeadLower(), true));
        }

    }

    static public GameModeDefinition CurrentGameMode => GameModes.GetGameMode(GameModeOption.GetValue());
    static public IntegerConfiguration GameModeOption = NebulaAPI.Configurations.Configuration("options.gamemode", Helpers.Sequential(GameModes.AllGameModes.Count()), 0);

    static public bool NeutralSpawnable => AssignmentNeutralOption > 0;
    static internal IntegerConfiguration AssignmentCrewmateOption = new RoleCountConfiguration("options.assignment.crewmate", 24 + 1, -1);
    static internal IntegerConfiguration AssignmentImpostorOption = new RoleCountConfiguration("options.assignment.impostor", 6 + 1, -1);
    static internal IntegerConfiguration AssignmentNeutralOption = NebulaAPI.Configurations.Configuration("options.assignment.neutral", (0,24), 0);
    static internal BoolConfiguration AssignOpToHostOption = new BoolConfigurationImpl("options.assignment.assignOpToHost", false);
    static internal ValueConfiguration<int> GhostAssignmentOption = NebulaAPI.Configurations.Configuration("options.assignment.ghostAssignmentMethod", ["options.assignment.ghostAssignmentMethod.normal", "options.assignment.ghostAssignmentMethod.thrilling"], 0);
    static internal IConfigurationHolder AssignmentOptions = NebulaAPI.Configurations.Holder("options.assignment", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        AssignmentCrewmateOption, AssignmentImpostorOption, AssignmentNeutralOption, AssignOpToHostOption
        ]);

    static internal IntegerConfiguration NumOfDummiesOption = NebulaAPI.Configurations.Configuration("options.soloFreePlay.numOfDummies",(0, 23), 0);
    static internal IConfigurationHolder SoloFreePlayOptions = NebulaAPI.Configurations.Holder("options.soloFreePlay", [ConfigurationTab.Settings], [GameModes.FreePlay]).AppendConfigurations([
        NumOfDummiesOption
        ]);

    static internal ValueConfiguration<int> SpawnMethodOption = NebulaAPI.Configurations.Configuration("options.map.spawnMethod", ["options.map.spawnMethod.default", "options.map.spawnMethod.selective", "options.map.spawnMethod.random"], 0);
    static internal IntegerConfiguration SpawnCandidatesOption = NebulaAPI.Configurations.Configuration("options.map.spawnCandidates", (1, 8), 1, () => (SpawnMethodOption.GetValue() == 1));
    static internal IConfiguration SpawnCandidateFilterOption = NebulaAPI.Configurations.Configuration(() => null, () => NebulaAPI.GUI.LocalizedButton(Virial.Media.GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf), "options.map.spawnCandidatesFilter", _ => OpenCandidatesFilter(null)),() => SpawnMethodOption.GetValue() > 0);
    static internal BoolConfiguration SilentVentOption = NebulaAPI.Configurations.Configuration("options.map.silentVents", false);
    static internal BoolConfiguration CanOpenMapWhileUsingUtilityOption = NebulaAPI.Configurations.Configuration("options.map.canOpenMapWhileUsingUtility", false);
    static internal IntegerConfiguration NumOfTeleportationPortalOption = NebulaAPI.Configurations.Configuration("options.map.numOfTeleporter", (0, TeleportationSystem.MaxTeleporterKind), 0);
    static internal BoolConfiguration NonCrewmateCanUseTeleporterImmediatelyOption = NebulaAPI.Configurations.Configuration("options.map.nonCrewmateCanUseTeleporterImmediately", true, () => NumOfTeleportationPortalOption > 0);
    static internal BoolConfiguration RandomizedWiringOption = NebulaAPI.Configurations.Configuration("options.map.randomizedWiring", false);
    static internal IntegerConfiguration StepsOfWiringGameOption = NebulaAPI.Configurations.Configuration("options.map.stepsOfWiringGame", (1, 12), 3);
    static internal FloatConfiguration LadderCoolDownOption = NebulaAPI.Configurations.Configuration("options.map.ladderCoolDown", (0f, 20f, 1f), 3f, FloatConfigurationDecorator.Second);
    static internal FloatConfiguration ZiplineCoolDownOption = NebulaAPI.Configurations.Configuration("options.map.ziplineCoolDown", (0f, 20f, 1f), 3f, FloatConfigurationDecorator.Second);
    static internal IConfiguration MapEditorOption = NebulaAPI.Configurations.Configuration(() => null, () => NebulaAPI.GUI.LocalizedButton(Virial.Media.GUIAlignment.Center, NebulaAPI.GUI.GetAttribute(Virial.Text.AttributeAsset.OptionsTitleHalf), "options.map.customization", _ => OpenMapEditor(null)));
    static internal BoolConfiguration MapFlipXOption = NebulaAPI.Configurations.Configuration("options.map.flipX", false);
    static internal BoolConfiguration MapFlipYOption = NebulaAPI.Configurations.Configuration("options.map.flipY", false);
    static internal BoolConfiguration CanHearOthersFootstepOption = NebulaAPI.Configurations.Configuration("options.map.canHearOthersFootstep", false);
    static internal FloatConfiguration OthersFootstepRangeOption = NebulaAPI.Configurations.Configuration("options.map.othersFootstepRange", (float[])[1f, 2.5f, 5f, 7.5f, 10f, 15f, 20f], 5f, FloatConfigurationDecorator.Ratio, () => CanHearOthersFootstepOption);
    static internal IConfigurationHolder MapOptions = NebulaAPI.Configurations.Holder("options.map", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        new GroupConfiguration("options.map.group.spawning", [SpawnMethodOption, SpawnCandidatesOption, SpawnCandidateFilterOption], GroupConfigurationColor.Gray),
        new GroupConfiguration("options.map.group.utilities", [SilentVentOption, CanOpenMapWhileUsingUtilityOption, LadderCoolDownOption, ZiplineCoolDownOption], GroupConfigurationColor.Gray),
        new GroupConfiguration("options.map.group.footstep", [CanHearOthersFootstepOption, OthersFootstepRangeOption], GroupConfigurationColor.Gray),
        new GroupConfiguration("options.map.group.teleporter", [NumOfTeleportationPortalOption, NonCrewmateCanUseTeleporterImmediatelyOption],GroupConfigurationColor.Gray),
        new GroupConfiguration("options.map.group.wiring", [RandomizedWiringOption, StepsOfWiringGameOption], GroupConfigurationColor.Gray),
        new GroupConfiguration("options.map.group.flip", [MapFlipXOption, MapFlipYOption], GroupConfigurationColor.Gray),
        MapEditorOption
        ]);

    static private T MapCustomization<T>(byte mapId, MapOptionType mapOptionType, Vector2 pos,T config) where T : ISharableEntry
    {
        MapCustomizations[mapId].Add(new(config, pos, mapOptionType));
        return config;
    }

    internal static List<MapConfiguration>[] MapCustomizations = [[], [], [], [], [], []];

    internal class MapConfiguration {
        public ISharableEntry Entry { get; init; }
        public Vector2 Position { get; init; }
        public MapOptionType OptionType { get; init; }

        public MapConfiguration(ISharableEntry entry, Vector2 position, MapOptionType optionType)
        {
            this.Entry = entry;
            this.Position = position;
            this.OptionType = optionType;
        }
    }

    static internal ISharableVariable<bool> SkeldAdminOption = MapCustomization(0, MapOptionType.Console, new(4.7f,-8.6f), NebulaAPI.Configurations.SharableVariable("options.map.customization.skeld.useAdmin", true));
    static internal ISharableVariable<bool> SkeldCafeVentOption = MapCustomization(0, MapOptionType.Vent, new(-2.4f, 5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.skeld.cafeVent", false));
    static internal ISharableVariable<bool> SkeldStorageVentOption = MapCustomization(0, MapOptionType.Vent, new(-1f, -16.7f), NebulaAPI.Configurations.SharableVariable("options.map.customization.skeld.storageVent", false));
    
    static internal ISharableVariable<bool> MiraAdminOption = MapCustomization(1, MapOptionType.Console, new(20f, 19f), NebulaAPI.Configurations.SharableVariable("options.map.customization.mira.useAdmin", true));
    
    static internal ISharableVariable<bool> PolusAdminOption = MapCustomization(2, MapOptionType.Console, new(24f, -21.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.polus.useAdmin", true));
    static internal ISharableVariable<bool> PolusSpecimenVentOption = MapCustomization(2, MapOptionType.Vent, new(37f, -22f), NebulaAPI.Configurations.SharableVariable("options.map.customization.polus.specimenVent", false));
    
    static internal ISharableVariable<bool> AirshipCockpitAdminOption = MapCustomization(4, MapOptionType.Console, new(-22f, 1f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.useCockpitAdmin", true));
    static internal ISharableVariable<bool> AirshipRecordAdminOption = MapCustomization(4, MapOptionType.Console, new(19.9f, 12f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.useRecordsAdmin", true));
    static internal ISharableVariable<bool> AirshipMeetingVentOption = MapCustomization(4, MapOptionType.Vent, new(6.6f, 14f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.meetingVent", false));
    static internal ISharableVariable<bool> AirshipElectricalVentOption = MapCustomization(4, MapOptionType.Vent, new(16.3f, -8.8f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.electricalVent", false));
    static internal ISharableVariable<bool> AirshipOneWayMeetingRoomOption = MapCustomization(4, MapOptionType.Blueprint, new(13.5f, 12.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.oneWayMeetingRoom", false));
    static internal ISharableVariable<bool> AirshipArmoryWireOption = MapCustomization(4, MapOptionType.Wiring, new(-11.3f, -7.4f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.armoryWiring", false));
    static internal ISharableVariable<bool> AirshipVaultWireOption = MapCustomization(4, MapOptionType.Wiring, new(-11.5f, 12.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.vaultWiring", false));
    static internal ISharableVariable<bool> AirshipHallwayWireOption = MapCustomization(4, MapOptionType.Wiring, new(-10.3f, -0.25f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.hallwayWiring", false));
    static internal ISharableVariable<bool> AirshipMedicalWireOption = MapCustomization(4, MapOptionType.Wiring, new(27f, -5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.medicalWiring", false));
    static internal ISharableVariable<bool> AirshipHarderDownloadOption = MapCustomization(4, MapOptionType.Blueprint, new(15.4f, 7.3f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.harderDownload", false));
    static internal ISharableVariable<bool> AirshipBetterImpostorVisonOption = MapCustomization(4, MapOptionType.Light, new(7.2f, 7.3f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.betterImpostorVision", false));
    static internal ISharableVariable<bool> AirshipShadedLowerFloorOption = MapCustomization(4, MapOptionType.Light, new(11.3f, 7.3f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.shadedLowerFloor", false));
    static internal ISharableVariable<bool> AirshipMainHallDoorOption = MapCustomization(4, MapOptionType.Door, new(7f, 0f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.mainHallDoor", false));
    static internal ISharableVariable<bool> AirshipMainElecDoorOption = MapCustomization(4, MapOptionType.Door, new(12.5f, -4f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.mainElecDoor", false));
    static internal ISharableVariable<bool> AirshipShowerDoorOption = MapCustomization(4, MapOptionType.Door, new(20f, 2f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.showerDoor", false));
    static internal ISharableVariable<bool> AirshipVentilationDoorOption = MapCustomization(4, MapOptionType.Door, new(27f, 0f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.ventilationDoor", false));
    static internal ISharableVariable<bool> AirshipLoungeDoorOption = MapCustomization(4, MapOptionType.Door, new(26f, 5.572f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.loungeDoor", false));
    static internal ISharableVariable<bool> AirshipLoungeStorageDoorOption = MapCustomization(4, MapOptionType.Door, new(35f, 4.1328f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.loungeStorageDoor", false));
    static internal ISharableVariable<bool> AirshipMedicalDoorOption = MapCustomization(4, MapOptionType.Door, new(26.57f, -10f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.medicalDoor", false));
    static internal ISharableVariable<bool> AirshipSecurityKitchenDoorOption = MapCustomization(4, MapOptionType.Door, new(2.4975f, -12.0855f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.securityKitchenDoor", false));
    static internal ISharableVariable<bool> AirshipSecurityDeckDoorOption = MapCustomization(4, MapOptionType.Door, new(9.09f, -13.8182f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.securityDeckDoor", false));
    static internal ISharableVariable<bool> AirshipMeetingDoorOption = MapCustomization(4, MapOptionType.Door, new(14.595f, 17f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.meetingDoor", false));

    static internal ISharableVariable<bool> FungleSimpleLaboratoryOption = MapCustomization(5, MapOptionType.Blueprint, new(-3.2f, -11f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.simpleLaboratory", false));
    static internal ISharableVariable<bool> FungleThinFogOption = MapCustomization(5, MapOptionType.Blueprint, new(3.1f, -14f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.thinFog", false));
    static internal ISharableVariable<bool> FungleGlowingCampfireOption = MapCustomization(5, MapOptionType.Light, new(-9.8f, 1.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.glowingCampfire", false));
    static internal ISharableVariable<bool> FungleGlowingMushroomOption = MapCustomization(5, MapOptionType.Light, new(14.7f, -12.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.glowingMushroom", false));
    static internal ISharableVariable<bool> FungleQuickPaceDoorMinigameOption = MapCustomization(5, MapOptionType.Console, new(-21f, -15f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.quickPaceDoorMinigame", false));
    static internal ISharableVariable<bool> FungleLightDropshipOption = MapCustomization(5, MapOptionType.BlackOut, new(-7.8f, 13.46f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.blackOutDropship", false));
    static internal ISharableVariable<bool> FungleLightJungleOption = MapCustomization(5, MapOptionType.BlackOut, new(-6.9618f, -5.9982f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.blackOutJungle", false));
    static internal ISharableVariable<bool> FungleLightMiningPitOption = MapCustomization(5, MapOptionType.BlackOut, new(13.68f, 7.83f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.blackOutMiningPit", false));


    static IEnumerable<float> SabotageSelections()
    {
        yield return 10f;
        float time = 10f;
        while (time < 30f)
        {
            time += 1f;
            yield return time;
        }
        while (time < 60f)
        {
            time += 2.5f;
            yield return time;
        }
        while (time < 120f)
        {
            time += 5f;
            yield return time;
        }
    }
    static private float[] SabotageCoolDown = SabotageSelections().ToArray();

    static public ISharableVariable<float> SkeldReactorDurationOption = MapCustomization(0, MapOptionType.Float, new(-21.2f, -5.2f), NebulaAPI.Configurations.SharableVariable("options.map.customization.skeld.reactorDuration", SabotageCoolDown, 30f));
    static public ISharableVariable<float> SkeldO2DurationOption = MapCustomization(0, MapOptionType.Float, new(6.4f, -4.7f), NebulaAPI.Configurations.SharableVariable("options.map.customization.skeld.lifeSupportDuration", SabotageCoolDown, 30f));
    static public ISharableVariable<float> MiraReactorDurationOption = MapCustomization(1, MapOptionType.Float, new(2.5f, 13.5f), NebulaAPI.Configurations.SharableVariable("options.map.customization.mira.reactorDuration", SabotageCoolDown, 45f));
    static public ISharableVariable<float> MiraO2DurationOption = MapCustomization(1, MapOptionType.Float, new(17.8f, 24.2f), NebulaAPI.Configurations.SharableVariable("options.map.customization.mira.lifeSupportDuration", SabotageCoolDown, 45f));
    static public ISharableVariable<float> PolusReactorDurationOption = MapCustomization(2, MapOptionType.Float, new(23f, -2.7f), NebulaAPI.Configurations.SharableVariable("options.map.customization.polus.reactorDuration", SabotageCoolDown, 60f));
    static public ISharableVariable<float> AirshipHeliDurationOption = MapCustomization(4, MapOptionType.Float, new(1.7f, 6.2f), NebulaAPI.Configurations.SharableVariable("options.map.customization.airship.heliDuration", SabotageCoolDown, 60f));
    static public ISharableVariable<float> FungleReactorDurationOption = MapCustomization(5, MapOptionType.Float, new(22.4f, -6.8f), NebulaAPI.Configurations.SharableVariable("options.map.customization.fungle.reactorDuration", SabotageCoolDown, 60f));

    static public IntegerConfiguration NumOfPlantsOption = NebulaAPI.Configurations.Configuration("options.perk.numOfPlants", (0, 5), 0);
    static public IntegerConfiguration NumOfWarpedPlantsOption = NebulaAPI.Configurations.Configuration("options.perk.numOfWarpedPlants", (0, 5), 0);
    static public IntegerConfiguration MaxBloomsPerPlantOption = NebulaAPI.Configurations.Configuration("options.perk.maxBloomsPerPlant", (1, 5), 3);
    static public IntegerConfiguration GrowSpeedOption = NebulaAPI.Configurations.Configuration("options.perk.growSpeed", (1, 8), 3);
    static public BoolConfiguration MovementRestrictionPerksAlsoAffectCastersOption = NebulaAPI.Configurations.Configuration("options.perk.movementRestrictionPerksAlsoAffectCasters", true);
    static public BoolConfiguration MovementRotationPerksAlsoAffectCastersOption = NebulaAPI.Configurations.Configuration("options.perk.movementRotationPerksAlsoAffectCasters", true);
    static private Virial.Media.GUIWidget GeneratePerkShortcutButton(string translationKey, Action action) => GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButtonLonger), translationKey, _ => { action.Invoke(); NebulaAPI.Configurations.RequireUpdateSettingScreen(); });
    static internal IConfigurationHolder PerkOptions = NebulaAPI.Configurations.Holder("options.perk", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        NumOfPlantsOption, NumOfWarpedPlantsOption, MaxBloomsPerPlantOption, GrowSpeedOption,
        MovementRestrictionPerksAlsoAffectCastersOption, MovementRotationPerksAlsoAffectCastersOption,
        /*ショートカットボタン*/
        NebulaAPI.Configurations.Configuration(()=>null, () => NebulaAPI.GUI.VerticalHolder(GUIAlignment.Left,
             NumOfPlantsOption > 0 ? GUI.API.HorizontalHolder(GUIAlignment.Left,
                 GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), "options.perk.shortcut.standard"),
                 ConfigurationAssets.Semicolon,
                 GUI.API.HorizontalMargin(0.1f),
                 GeneratePerkShortcutButton("options.perk.shortcut.standard.0", () => Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.Standard).Do(p => p.SpawnRate.CurrentValue = 0)),
                 GeneratePerkShortcutButton("options.perk.shortcut.standard.100", () => Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.Standard).Do(p => p.SpawnRate.CurrentValue = 100))
                 ) : GUI.API.EmptyWidget,
             NumOfWarpedPlantsOption > 0 ? GUI.API.HorizontalHolder(GUIAlignment.Left,
                 GUI.API.LocalizedText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), "options.perk.shortcut.warped"),
                 ConfigurationAssets.Semicolon,
                 GUI.API.HorizontalMargin(0.1f),
                 GeneratePerkShortcutButton("options.perk.shortcut.warped.0", () => Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.NoncrewmateOnly).Do(p => p.SpawnRate.CurrentValue = 0)),
                 GeneratePerkShortcutButton("options.perk.shortcut.warped.100", () => Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.NoncrewmateOnly).Do(p => p.SpawnRate.CurrentValue = 100))
                 ) : GUI.API.EmptyWidget
            ), () => NumOfPlantsOption > 0 || NumOfWarpedPlantsOption > 0),
        ..Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.Standard).Select(p => p.SpawnRateConfiguration),
        ..Roles.Roles.AllPerks.Where(p => p.PerkCategory is PerkFunctionalDefinition.Category.NoncrewmateOnly).Select(p => p.SpawnRateConfiguration)
        ]);

    static public FloatConfiguration DeathPenaltyOption = NebulaAPI.Configurations.Configuration("options.meeting.deathPenalty", (0f, 20f, 0.5f), 0f, FloatConfigurationDecorator.Second);
    static public BoolConfiguration NoticeExtraVictimsOption = NebulaAPI.Configurations.Configuration("options.meeting.noticeExtraVictims", false);
    static public IntegerConfiguration NumOfMeetingsOption = NebulaAPI.Configurations.Configuration("options.meeting.numOfMeeting", (0, 15), 10);
    static public BoolConfiguration EmergencyCooldownAtGameStart = NebulaAPI.Configurations.Configuration("options.meeting.emergencyCooldownAtGameStart", false);
    static public BoolConfiguration ShowRoleOfExiled = NebulaAPI.Configurations.Configuration("options.meeting.showRoleOfExiled", false, () => GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor);
    static public FloatConfiguration EarlyExtraEmergencyCoolDownOption = NebulaAPI.Configurations.Configuration("options.meeting.extraEmergencyCooldownInTheEarly", (0f, 20f, 2.5f), 0f, FloatConfigurationDecorator.Second);
    static public IntegerConfiguration EarlyExtraEmergencyCoolDownCondOption = NebulaAPI.Configurations.Configuration("options.meeting.extraEmergencyCooldownInTheEarlyCondition", (1, 10), 2, () => EarlyExtraEmergencyCoolDownOption > 0f);
    static public BoolConfiguration ShowVoteStateOption = NebulaAPI.Configurations.Configuration("options.meeting.showVoteState", true);
    static public BoolConfiguration ProhibitMeetingTool = NebulaAPI.Configurations.Configuration("options.meeting.prohibitMeetingTool", false);
    static public BoolConfiguration ShortenCooldownAtGameStart = NebulaAPI.Configurations.Configuration("options.meeting.shortenCooldownAtGameStart", true);
    static internal IConfigurationHolder MeetingOptions = NebulaAPI.Configurations.Holder("options.meeting", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        DeathPenaltyOption, NoticeExtraVictimsOption, NumOfMeetingsOption,EmergencyCooldownAtGameStart, ShowRoleOfExiled, EarlyExtraEmergencyCoolDownOption, EarlyExtraEmergencyCoolDownCondOption, ShowVoteStateOption, ProhibitMeetingTool, ShortenCooldownAtGameStart
        ]);

    static public ExclusiveAssignmentConfiguration[] exclusiveAssignmentOptions = Helpers.Sequential(10).Select(i => new ExclusiveAssignmentConfiguration("options.exclusiveAssignment.category." + i)).ToArray();
    static public IConfigurationHolder ExclusiveAssignmentOptions = NebulaAPI.Configurations.Holder("options.exclusiveAssignment", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations(exclusiveAssignmentOptions);

    static public readonly BoolConfiguration UseVoiceChatOption = NebulaAPI.Configurations.Configuration("options.voiceChat.useVoiceChat", false);
    static public readonly BoolConfiguration CanTalkInWanderingPhaseOption = NebulaAPI.Configurations.Configuration("options.voiceChat.canTalkInWanderingPhase", true, () => UseVoiceChatOption);
    static public readonly BoolConfiguration WallsBlockAudioOption = NebulaAPI.Configurations.Configuration("options.voiceChat.wallsBlockAudio",true, () => CanTalkInWanderingPhaseOption);
    static public readonly ValueConfiguration<int> KillersHearDeadOption = NebulaAPI.Configurations.Configuration("options.voiceChat.killersHearDead", ["options.switch.off", "options.voiceChat.killersHearDead.onlyMyKiller", "options.voiceChat.killersHearDead.onlyImpostors"], 0, () => UseVoiceChatOption);
    static public readonly BoolConfiguration ImpostorsRadioOption = NebulaAPI.Configurations.Configuration("options.voiceChat.impostorsRadio", false, () => UseVoiceChatOption);
    static public readonly BoolConfiguration JackalRadioOption = NebulaAPI.Configurations.Configuration("options.voiceChat.jackalRadio", false, () => UseVoiceChatOption);
    static public readonly BoolConfiguration LoversRadioOption = NebulaAPI.Configurations.Configuration("options.voiceChat.loversRadio", false, () => UseVoiceChatOption);
    static public readonly BoolConfiguration AffectedByCommsSabOption = NebulaAPI.Configurations.Configuration("options.voiceChat.affectedByCommsSab", false, () => CanTalkInWanderingPhaseOption);
    static public readonly BoolConfiguration IsolateGhostsStrictlyOption = NebulaAPI.Configurations.Configuration("options.voiceChat.isolateGhostsStrictly", false, () => UseVoiceChatOption);
    static internal readonly IConfigurationHolder VoiceChatOptions = NebulaAPI.Configurations.Holder("options.voiceChat", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        UseVoiceChatOption, CanTalkInWanderingPhaseOption, WallsBlockAudioOption, KillersHearDeadOption, ImpostorsRadioOption, JackalRadioOption, LoversRadioOption, AffectedByCommsSabOption, IsolateGhostsStrictlyOption
        ]);

    static public readonly BoolConfiguration LowLatencyPlayerSyncOption = NebulaAPI.Configurations.Configuration("options.quality.lowLatencyPlayerSync", true);
    static internal readonly IConfigurationHolder QualityOptions = NebulaAPI.Configurations.Holder("options.quality", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        LowLatencyPlayerSyncOption
        ]);
    static IEnumerable<float> RestrictionSelections()
    {
        yield return -1f;
        yield return 0f;
        float time = 0f;
        while (time < 10f)
        {
            time += 1f;
            yield return time;
        }
        while (time < 120f)
        {
            time += 5f;
            yield return time;
        }
    }
    static string RestrictionDecorator(float val)
    {
        if (val < 0f) return Language.Translate("options.consoleRestriction.unlimited");
        return val + Language.Translate("options.sec");
    }

    static public readonly BoolConfiguration ResetRestrictionsOption = NebulaAPI.Configurations.Configuration("options.consoleRestriction.resetRestrictions", true);
    static public readonly FloatConfiguration AdminRestrictionOption = NebulaAPI.Configurations.Configuration("options.consoleRestriction.adminRestriction", RestrictionSelections().ToArray(), -1f, RestrictionDecorator);
    static public readonly FloatConfiguration VitalsRestrictionOption = NebulaAPI.Configurations.Configuration("options.consoleRestriction.vitalsRestriction", RestrictionSelections().ToArray(), -1f, RestrictionDecorator);
    static public readonly FloatConfiguration CameraRestrictionOption = NebulaAPI.Configurations.Configuration("options.consoleRestriction.cameraRestriction", RestrictionSelections().ToArray(), -1f, RestrictionDecorator);
    static public readonly BoolConfiguration ShowDeadBodiesOnAdminOption = NebulaAPI.Configurations.Configuration("options.consoleRestriction.showDeadBodiesOnAdmin", true);
    static public readonly IConfigurationHolder ConsoleRestrictionOptions = NebulaAPI.Configurations.Holder("options.consoleRestriction", [ConfigurationTab.Settings], [GameModes.FreePlay, GameModes.Standard]).AppendConfigurations([
        ResetRestrictionsOption, AdminRestrictionOption, VitalsRestrictionOption, CameraRestrictionOption, ShowDeadBodiesOnAdminOption
        ]);


    static private readonly XOnlyDividedSpriteLoader mapCustomizationSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.MapCustomizations.png", 100f, 50, true);
    
    static void OpenMapConfigurationEditor(MetaScreen? screen, byte? mapId, Func<byte,IEnumerable<(IMetaParallelPlacableOld button, Vector2 pos)>> widgetBuilder)
    {
        if (screen == null) screen = MetaScreen.GenerateWindow(new(7.5f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, false);

        mapId ??= AmongUsUtil.CurrentMapId;

        MetaWidgetOld widget = new();

        byte Lessen()
        {
            byte id = mapId.Value;
            while (true)
            {
                if (id == 0)
                    id = (byte)(MapCustomizations.Length - 1);
                else
                    id--;
                if (id != 3) return id;//反転マップをスキップ
            }
        }

        byte Increase()
        {
            byte id = mapId.Value;
            while (true)
            {
                if (id == (byte)(MapCustomizations.Length - 1))
                    id = 0;
                else
                    id++;
                if (id != 3) return id;//反転マップをスキップ
            }
        }



        widget.Append(new CombinedWidgetOld(
            new MetaWidgetOld.Button(() => OpenMapConfigurationEditor(screen, Lessen(),widgetBuilder), new(TextAttributeOld.BoldAttr) { Size = new(0.2f, 0.2f) }) { RawText = "<<" },
            new MetaWidgetOld.Text(TextAttributeOld.BoldAttr) { RawText = Constants.MapNames[mapId.Value] },
            new MetaWidgetOld.Button(() => OpenMapConfigurationEditor(screen, Increase(), widgetBuilder), new(TextAttributeOld.BoldAttr) { Size = new(0.2f, 0.2f) }) { RawText = ">>" }
            ));
        if (mapId.Value is 0 or 4) widget.Append(new MetaWidgetOld.VerticalMargin(0.35f));

        widget.Append(MetaWidgetOld.Image.AsMapImage(mapId.Value, 5.6f, widgetBuilder.Invoke(mapId.Value)));

        screen.SetWidget(widget);
    }

    static public void OpenMapEditor(MetaScreen? screen, byte? mapId = null, bool asEditor = true) => OpenMapConfigurationEditor(screen, mapId, mapId =>
        MapCustomizations[mapId].Select(
            c =>
            {
                if (c.OptionType is MapOptionType.Float)
                {
                    TMPro.TextMeshPro valueText = null!;
                    IOrderedSharableVariable<float> variable = (c.Entry as IOrderedSharableVariable<float>)!;
                    return ((IMetaParallelPlacableOld)new CombinedWidgetOld(new MetaWidgetOld(
                        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttrLeft) { Size = new(0.55f, 0.15f) }) { MyText = new TranslateTextComponent(c.Entry.Name) },
                        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Size = new(0.55f, 0.28f) }) { RawText = variable.Value.ToString() + Language.Translate("options.sec"), PostBuilder = text => valueText = text }
                        ), asEditor ? MetaWidgetOld.Button.GetTwoWayButton(increment => { variable.ChangeValue(increment); valueText.text = variable.Value.ToString() + Language.Translate("options.sec"); }) : new MetaWidgetOld.VerticalMargin(0f))
                    { PostBuilder = obj => obj.AddComponent<SortingGroup>().sortingOrder = 12 }, c.Position);
                }
                else
                {
                    IOrderedSharableVariable<bool> variable = (c.Entry as IOrderedSharableVariable<bool>)!;
                    return ((IMetaParallelPlacableOld)new MetaWidgetOld.Image(mapCustomizationSprite.GetSprite((int)c.OptionType))
                    {
                        Width = 0.5f,
                        PostBuilder = (renderer) =>
                        {
                            renderer.color = variable.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.45f);
                            var button = renderer.gameObject.SetUpButton(true);
                            button.OnMouseOver.AddListener(() =>
                            {
                                MetaWidgetOld widget = new();
                                widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = c.Entry.Name }).Append(new MetaWidgetOld.WrappedWidget(ConfigurationAssets.GetOptionOverlay(c.Entry.Name)?.Invoke() ?? new GUIEmptyWidget()));
                                NebulaManager.Instance.SetHelpWidget(button, widget);
                            });
                            button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                            if(asEditor) button.OnClick.AddListener(() => { variable.ChangeValue(true); renderer.color = variable.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.45f); });
                            var collider = button.gameObject.AddComponent<BoxCollider2D>();
                            collider.isTrigger = true;
                            collider.size = new(0.5f, 0.5f);
                        }
                    }, c.Position);
                };
            })
    );

    static public void OpenCandidatesFilter(MetaScreen? screen, byte? mapId = null, bool asEditor = true) => OpenMapConfigurationEditor(screen, mapId, mapId =>
            NebulaPreSpawnLocation.Locations[mapId].Where(l => asEditor || l.Configuration.Value).Select(
                l => ((IMetaParallelPlacableOld)new MetaWidgetOld.Image(mapCustomizationSprite.GetSprite(5))
                {
                    Width = 0.5f,
                    PostBuilder = (renderer) =>
                    {
                        renderer.color = l.Configuration.Value ? Color.white : Color.red.RGBMultiplied(0.65f);
                        var button = renderer.gameObject.SetUpButton(true);
                        button.OnMouseOver.AddListener(() =>
                        {
                            MetaWidgetOld widget = new();
                            widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = l.GetDisplayName(mapId) });
                            NebulaManager.Instance.SetHelpWidget(button, widget);
                        });
                        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                        if(asEditor) button.OnClick.AddListener(() => { l.Configuration.ChangeValue(true); renderer.color = l.Configuration.Value ? Color.white : Color.red.RGBMultiplied(0.65f); });
                        var collider = button.gameObject.AddComponent<BoxCollider2D>();
                        collider.isTrigger = true;
                        collider.size = new(0.5f, 0.5f);
                    }
                }, l.Position!.Value))
    );

    public class ExclusiveAssignmentConfiguration : IConfiguration
    {
        private const int UnitSize = 30;
        internal class FilterSharableVariable : ISharableVariable<int>
        {
            private string name;
            private int id;
            private int currentValue;
            private ExclusiveAssignmentConfiguration myConfig;
            private int index;

            public FilterSharableVariable(ExclusiveAssignmentConfiguration config, int index)
            {
                this.name = config.dataEntry.Name + index;
                this.id = -1;
                this.index = index;
                this.myConfig = config;

                currentValue = config.ToSharableValueFromLocal(this.index);

                ConfigurationValues.RegisterEntry(this);
            }

            string ISharableEntry.Name => name;

            int ISharableEntry.Id { get => id; set => id = value; }
            int ISharableEntry.RpcValue { get => currentValue; set => currentValue = value; }

            int ISharableVariable<int>.CurrentValue
            {
                get => currentValue;
                set
                {
                    ConfigurationValues.AssertOnChangeOptionValue();
                    if (currentValue != value)
                    {
                        currentValue = value;
                        ConfigurationValues.TryShareOption(this);
                    }
                }
            }

            int Virial.Compat.Reference<int>.Value => currentValue;

            void ISharableVariable<int>.SetValueWithoutSaveUnsafe(int value) => currentValue = value;

            void ISharableEntry.RestoreSavedValue() => currentValue = myConfig.ToSharableValueFromLocal(this.index);

        }


        StringArrayDataEntry dataEntry;
        ISharableVariable<int>[] sharableVariables;
        HashSet<DefinedRole> localExclusiveRolesCache;
        public ExclusiveAssignmentConfiguration(string id)
        {
            dataEntry = new(id, ConfigurationValues.ConfigurationSaver, []);

            void RefreshCache()
            {
                localExclusiveRolesCache = new(dataEntry.Value.Select(name => Roles.Roles.AllRoles.FirstOrDefault(a => a.InternalName == name)).Where(a => a != null)!);
            }

            void GenerateSharable()
            {
                sharableVariables = new ISharableVariable<int>[Roles.Roles.AllRoles.Count / UnitSize + 1];

                RefreshCache();

                int length = Roles.Roles.AllRoles.Count / UnitSize + 1;

                sharableVariables = new ISharableVariable<int>[length];
                for (int i = 0; i < length; i++)
                {
                    sharableVariables[i] = new FilterSharableVariable(this, i);
                }
            }

            NebulaAPI.Preprocessor?.SchedulePreprocess(Virial.Attributes.PreprocessPhase.FixStructureRoleFilter, GenerateSharable);
        }

        bool IConfiguration.IsShown => true;

        public IEnumerable<DefinedRole> OnAssigned(DefinedRole role)
        {
            if (Contains(role)) foreach (var r in Roles.Roles.AllRoles.Where(r => r != role && Contains(r))) yield return r;
        }

        public void SaveLocal()
        {
            var cache = Roles.Roles.AllRoles.Where(r => Contains(r)).ToArray();
            var array = cache.Select(r => r.InternalName).ToArray();
            dataEntry.Value = array;
            localExclusiveRolesCache = new(cache);
        }
        GUIWidgetSupplier IConfiguration.GetEditor()
        {
            return () => new HorizontalWidgetsHolder(GUIAlignment.Left,
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitleHalf), new TranslateTextComponent(this.dataEntry.Name)) { OverlayWidget = ConfigurationAssets.GetOptionOverlay(this.dataEntry.Name), OnClickText = ConfigurationAssets.GetCopyAction(this.dataEntry.Name) },
            new NoSGUIText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsTitle), new LazyTextComponent(() => ValueAsDisplayString ?? "None")),
            new GUIButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.OptionsButton), new TranslateTextComponent("options.exclusiveAssignment.edit")) { OnClick = _ => RoleOptionHelper.OpenFilterScreen("exclusiveRole", Roles.Roles.AllRoles, Contains, null, r => { ToggleAndShare(r); NebulaAPI.Configurations.RequireUpdateSettingScreen(); }) }
            );
        }

        string? ValueAsDisplayString
        {
            get
            {
                var roles = Roles.Roles.AllRoles.Where(r => Contains(r)).ToArray();
                if (roles.Length == 0) return null;
                return roles.Join(r => r.DisplayColoredName, ", ");
            }
        }

        string? IConfiguration.GetDisplayText()
        {
            var str = ValueAsDisplayString;
            if (str == null) return null;
            return Language.Translate(this.dataEntry.Name) + ": " + str;
        }

        internal bool Contains(DefinedRole role) => (sharableVariables[role.Id / UnitSize].CurrentValue & (1 << (role.Id % UnitSize))) != 0;
        internal void ToggleAndShare(DefinedRole role)
        {
            sharableVariables[role.Id / UnitSize].CurrentValue ^= (1 << (role.Id % UnitSize));
            SaveLocal();
        }

        internal void SetAndShare(DefinedRole role, bool on)
        {
            if (on)
                sharableVariables[role.Id / UnitSize].CurrentValue |= (1 << (role.Id % UnitSize));
            else
                sharableVariables[role.Id / UnitSize].CurrentValue &= ~(1 << (role.Id % UnitSize));
            SaveLocal();
        }

        private int ToSharableValueFromLocal(int index) => localExclusiveRolesCache.Aggregate(0, (val, a) => { if ((int)(a.Id / UnitSize) == index) return val | (1 << (a.Id % UnitSize)); else return val; });
    }
}

/*
[HarmonyPatch(typeof(GameOptionsData), nameof(GameOptionsData.Serialize))]
public static class GameOptionsSerializePatch
{
    static private int NumImpostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
    public static bool Prefix(GameOptionsData __instance)
    {
        try
        {
            NumImpostors = GameOptionsManager.Instance.CurrentGameOptions.NumImpostors;
            if (NumImpostors == 0)
            {
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, 1);
            }
            else if (NumImpostors > 3)
            {
                GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, 3);
            }
        }
        catch { }
        return true;
    }

    public static void Postfix(GameOptionsData __instance)
    {
        try
        {
            GameOptionsManager.Instance.CurrentGameOptions.SetInt(Int32OptionNames.NumImpostors, NumImpostors);
        }
        catch { }
    }
}
*/