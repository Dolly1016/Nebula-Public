using Nebula.Behaviour;
using Nebula.Modules.GUIWidget;
using Nebula.Roles;
using Nebula.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using static Il2CppMono.Security.X509.X520;

namespace Nebula.Configuration;

[NebulaPreLoad(typeof(Roles.Roles))]
[NebulaOptionHolder]
public static class GeneralConfigurations
{
    public enum MapOptionType
    {
        Vent = 0,
        Console = 1,
        Blueprint = 2,
        Wiring = 3,
        Light = 4,
    }

    static public NebulaConfiguration GameModeOption = new(null, "options.gamemode", null, CustomGameMode.AllGameMode.Count - 1, 0, 0) { GameModeMask = 0x7FFFFFFF };

    public class MapCustomization
    {
        public List<(NebulaConfiguration.NebulaByteConfiguration configuration, MapOptionType type, Vector2 position)> BoolOptions = new();
        public List<(NebulaConfiguration configuration, Vector2 position)> StandardOptions = new();

        public void Register(NebulaConfiguration.NebulaByteConfiguration configuration, MapOptionType type,Vector2 position) => BoolOptions.Add((configuration,type,position));
        public void Register(NebulaConfiguration configuration, Vector2 position) => StandardOptions.Add((configuration,position));
    }

    static public MapCustomization[] MapCustomizations = new MapCustomization[]{
        new(),
        new(),
        new(),
        null!,
        new(),
        new(),
    };

    static public ConfigurationHolder AssignmentOptions = new("options.assignment", null, ConfigurationTab.Settings, CustomGameMode.AllNormalGameModeMask);
    static private Func<object?, string> AssignmentDecorator = (obj) => (int)obj! == -1 ? Language.Translate("options.assignment.unlimited") : obj.ToString()!;
    static public NebulaConfiguration AssignmentCrewmateOption = new NebulaConfiguration(AssignmentOptions, "crewmate", null, -1, 15, -1, -1) { Decorator = AssignmentDecorator };
    static public NebulaConfiguration AssignmentImpostorOption = new NebulaConfiguration(AssignmentOptions, "impostor", null, -1, 3, -1, -1) { Decorator = AssignmentDecorator };
    static public NebulaConfiguration AssignmentNeutralOption = new NebulaConfiguration(AssignmentOptions, "neutral", null, -1, 15, 0, 0) { Decorator = AssignmentDecorator };
    static public NebulaConfiguration AssignOpToHostOption = new NebulaConfiguration(AssignmentOptions, "assignOpToHost", null, false, false) { GameModeMask = CustomGameMode.Standard };
    static public NebulaConfiguration GhostAssignmentOption = new NebulaConfiguration(AssignmentOptions, "ghostAssignmentMethod", null, ["options.assignment.ghostAssignmentMethod.normal", "options.assignment.ghostAssignmentMethod.thrilling"], 0, 0) { GameModeMask = CustomGameMode.Standard };

    static public ConfigurationHolder SoloFreePlayOptions = new("options.soloFreePlay", null, ConfigurationTab.Settings, CustomGameMode.FreePlay);
    static public NebulaConfiguration NumOfDummiesOption = new NebulaConfiguration(SoloFreePlayOptions, "numOfDummies", null, 0, 14, 0, 0);

    static public ConfigurationHolder MapOptions = new("options.map", null, ConfigurationTab.Settings, CustomGameMode.AllClientGameModeMask);
    static public NebulaConfiguration SpawnMethodOption = new(MapOptions, "spawnMethod", null,
        new string[] { "options.map.spawnMethod.default", "options.map.spawnMethod.selective", "options.map.spawnMethod.random" }, 0, 0);
    static public NebulaConfiguration SpawnCandidatesOption = new NebulaConfiguration(MapOptions, "spawnCandidates", null, 1, 8, 1, 1) { Predicate = () => (SpawnMethodOption.GetString()?.Equals("options.map.spawnMethod.selective")) ?? false };
    static public NebulaConfiguration SpawnCandidateFilterOption = new NebulaConfiguration(MapOptions, () => new MetaWidgetOld.Button(() => OpenCandidatesFilter(MetaScreen.GenerateWindow(new(7.5f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, false, true)), TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "options.map.spawnCandidatesFilter" }) { Predicate = () => SpawnMethodOption.CurrentValue > 0 };

    static public NebulaConfiguration SilentVentOption = new NebulaConfiguration(MapOptions, "silentVents", null, false, false);
    static public NebulaConfiguration CanOpenMapWhileUsingUtilityOption = new NebulaConfiguration(MapOptions, "canOpenMapWhileUsingUtility", null, false, false);
    static public NebulaConfiguration RandomizedWiringOption = new NebulaConfiguration(MapOptions, "randomizedWiring", null, false, false);
    static public NebulaConfiguration StepsOfWiringGameOption = new NebulaConfiguration(MapOptions, "stepsOfWiringGame", null, 1, 12, 3, 3);
    static public NebulaConfiguration LadderCoolDownOption = new NebulaConfiguration(MapOptions, "ladderCoolDown", null, 0f, 20f, 1f, 3f, 3f) { Decorator = NebulaConfiguration.SecDecorator };
    static public NebulaConfiguration ZiplineCoolDownOption = new NebulaConfiguration(MapOptions, "ziplineCoolDown", null, 0f, 20f, 1f, 3f, 3f) { Decorator = NebulaConfiguration.SecDecorator };
    static public NebulaConfiguration MapEditorOption = new NebulaConfiguration(MapOptions, ()=> new MetaWidgetOld.Button(() => OpenMapEditor(MetaScreen.GenerateWindow(new(7.5f, 4.5f), HudManager.Instance.transform, Vector3.zero, true, false, true)), TextAttributeOld.BoldAttr) { Alignment =IMetaWidgetOld.AlignmentOption.Center, TranslationKey = "options.map.customization" });


    static private NebulaConfiguration[] GenerateMapOption(string prefix, Action<NebulaConfiguration[]>? postAction = null)
    {
        var result = new NebulaConfiguration[]{
            new NebulaConfiguration(null,prefix + ".skeld",null,int.MaxValue,int.MaxValue,int.MaxValue),
            new NebulaConfiguration(null,prefix + ".mira",null,int.MaxValue,int.MaxValue,int.MaxValue),
            new NebulaConfiguration(null,prefix + ".polus",null,int.MaxValue,int.MaxValue,int.MaxValue),
            null!,
            new NebulaConfiguration(null,prefix + ".airship",null,int.MaxValue,int.MaxValue,int.MaxValue),
            new NebulaConfiguration(null,prefix + ".fungle",null,int.MaxValue,int.MaxValue,int.MaxValue),
        };
        postAction?.Invoke(result);
        return result;
    }


    static public NebulaConfiguration[] MapCustomizationOptions = GenerateMapOption("options.map.customization");
    static public NebulaConfiguration[] SpawnCandidatesFilterOptions = GenerateMapOption("options.map.spawnCandidateFilter", options =>
    {
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i];
            var location = NebulaPreSpawnLocation.Locations[i];
            int index = 0;
            foreach (var loc in location)
            {
                loc.Configuration = new(option, option.Id + "." + loc.LocationName, index, true);
                index++;
            }
        }
    });

    static private NebulaConfiguration.NebulaByteConfiguration GenerateMapCustomization(byte mapId, MapOptionType type,string id,bool defaultValue,Vector2 pos) {
        id = "options.map.customization." + AmongUsUtil.ToMapName(mapId) + "." + id;
        NebulaConfiguration.NebulaByteConfiguration option = new(MapCustomizationOptions[mapId], id, MapCustomizations[mapId].BoolOptions.Count, defaultValue);
        MapCustomizations[mapId].Register(option, type, pos);
        return option;
    }

    static private NebulaConfiguration GenerateMapCustomization(byte mapId, Vector2 pos,NebulaConfiguration config)
    {
        MapCustomizations[mapId].Register(config, pos);
        config.Editor = NebulaConfiguration.EmptyEditor;
        config.Shower = ()=> AmongUsUtil.CurrentMapId == mapId ? (config.Title.GetString() + " : " + config.ToDisplayString()) : null; 
        return config;
    }

    static public NebulaConfiguration.NebulaByteConfiguration SkeldAdminOption = GenerateMapCustomization(0, MapOptionType.Console, "useAdmin",true,new(4.7f,-8.6f));
    static public NebulaConfiguration.NebulaByteConfiguration SkeldCafeVentOption = GenerateMapCustomization(0, MapOptionType.Vent, "cafeteriaVent", false, new(-2.4f, 5f));
    static public NebulaConfiguration.NebulaByteConfiguration SkeldStorageVentOption = GenerateMapCustomization(0, MapOptionType.Vent, "storageVent", false, new(-1f, -16.7f));
    static public NebulaConfiguration.NebulaByteConfiguration MiraAdminOption = GenerateMapCustomization(1, MapOptionType.Console, "useAdmin", true, new(20f, 19f));
    static public NebulaConfiguration.NebulaByteConfiguration PolusAdminOption = GenerateMapCustomization(2, MapOptionType.Console, "useAdmin", true, new(24f, -21.5f));
    static public NebulaConfiguration.NebulaByteConfiguration PolusSpecimenVentOption = GenerateMapCustomization(2, MapOptionType.Vent, "specimenVent", false, new(37f, -22f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipCockpitAdminOption = GenerateMapCustomization(4, MapOptionType.Console, "useCockpitAdmin", true, new(-22f, 1f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipRecordAdminOption = GenerateMapCustomization(4, MapOptionType.Console, "useRecordsAdmin", true, new(19.9f, 12f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipMeetingVentOption = GenerateMapCustomization(4, MapOptionType.Vent, "meetingVent", false, new(6.6f, 14f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipElectricalVentOption = GenerateMapCustomization(4, MapOptionType.Vent, "electricalVent", false, new(16.3f, -8.8f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipOneWayMeetingRoomOption = GenerateMapCustomization(4, MapOptionType.Blueprint, "oneWayMeetingRoom", false, new(13.5f, 12.5f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipArmoryWireOption = GenerateMapCustomization(4, MapOptionType.Wiring, "armoryWiring", false, new(-11.3f, -7.4f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipVaultWireOption = GenerateMapCustomization(4, MapOptionType.Wiring, "vaultWiring", false, new(-11.5f, 12.5f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipHallwayWireOption = GenerateMapCustomization(4, MapOptionType.Wiring, "hallwayWiring", false, new(-10.3f, -0.25f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipMedicalWireOption = GenerateMapCustomization(4, MapOptionType.Wiring, "medicalWiring", false, new(27f, -5f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipHarderDownloadOption = GenerateMapCustomization(4, MapOptionType.Blueprint, "harderDownload", false, new(15.4f, 7.3f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipBetterImpostorVisonOption = GenerateMapCustomization(4, MapOptionType.Light, "betterImpostorVision", false, new(7.2f, 7.3f));
    static public NebulaConfiguration.NebulaByteConfiguration AirshipShadedLowerFloorOption = GenerateMapCustomization(4, MapOptionType.Light, "shadedLowerFloor", false, new(11.3f, 7.3f));
    static public NebulaConfiguration.NebulaByteConfiguration FungleSimpleLaboratoryOption = GenerateMapCustomization(5, MapOptionType.Blueprint, "simpleLaboratory", false, new(-3.2f, -11f));
    static public NebulaConfiguration.NebulaByteConfiguration FungleThinFogOption = GenerateMapCustomization(5, MapOptionType.Blueprint, "thinFog", false, new(3.1f, -14f));
    static public NebulaConfiguration.NebulaByteConfiguration FungleGlowingCampfireOption = GenerateMapCustomization(5, MapOptionType.Light, "glowingCampfire", false, new(-9.8f, 1.65f));
    static public NebulaConfiguration.NebulaByteConfiguration FungleGlowingMushroomOption = GenerateMapCustomization(5, MapOptionType.Light, "glowingMushroom", false, new(14.7f, -12.5f));
    static public NebulaConfiguration.NebulaByteConfiguration FungleQuickPaceDoorMinigameOption = GenerateMapCustomization(5, MapOptionType.Console, "quickPaceDoorMinigame", false, new(-21f, -15f));

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

    static public NebulaConfiguration SkeldReactorDurationOption = GenerateMapCustomization(0, new(-21.2f, -5.2f), new(MapOptions, "customization.skeld.reactorDurationReworked",null, SabotageSelections().ToArray(),30f,30f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.skeld.reactorDuration");
    static public NebulaConfiguration SkeldO2DurationOption = GenerateMapCustomization(0, new(6.4f, -4.7f), new(MapOptions, "customization.skeld.lifeSupportDurationReworked", null, SabotageSelections().ToArray(), 30f, 30f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.skeld.lifeSupportDuration");
    static public NebulaConfiguration MiraReactorDurationOption = GenerateMapCustomization(1, new(2.5f, 13.5f), new(MapOptions, "customization.mira.reactorDurationReworked", null, SabotageSelections().ToArray(), 45f, 45f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.mira.reactorDuration");
    static public NebulaConfiguration MiraO2DurationOption = GenerateMapCustomization(1, new(17.8f, 24.2f), new(MapOptions, "customization.mira.lifeSupportDurationReworked", null, SabotageSelections().ToArray(), 45f, 45f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.mira.lifeSupportDuration");
    static public NebulaConfiguration PolusReactorDurationOption = GenerateMapCustomization(2, new(23f, -2.7f), new(MapOptions, "customization.polus.reactorDurationReworked", null, SabotageSelections().ToArray(), 60f, 60f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.polus.reactorDuration");
    static public NebulaConfiguration AirshipHeliDurationOption = GenerateMapCustomization(4, new(1.7f, 6.2f), new(MapOptions, "customization.airship.heliDurationReworked", null, SabotageSelections().ToArray(), 60f, 60f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.airship.heliDuration");
    static public NebulaConfiguration FungleReactorDurationOption = GenerateMapCustomization(5, new(22.4f, -6.8f), new(MapOptions, "customization.fungle.reactorDurationReworked", null, SabotageSelections().ToArray(), 60f, 60f) { Decorator = NebulaConfiguration.SecDecorator }).ReplaceTitle("customization.fungle.reactorDuration");


    static public ConfigurationHolder MeetingOptions = new("options.meeting", null, ConfigurationTab.Settings, CustomGameMode.AllGameModeMask);
    static public NebulaConfiguration DeathPenaltyOption = new(MeetingOptions, "deathPenalty", null, 0f, 20f, 0.5f, 0f, 0f) { Decorator = NebulaConfiguration.SecDecorator, GameModeMask = CustomGameMode.AllClientGameModeMask };
    static public NebulaConfiguration NoticeExtraVictimsOption = new NebulaConfiguration(MeetingOptions, "noticeExtraVictims", null, false, false) { GameModeMask = CustomGameMode.AllClientGameModeMask };
    static public NebulaConfiguration NumOfMeetingsOption = new(MeetingOptions, "numOfMeeting", null, 0, 15, 10, 10);
    static public NebulaConfiguration ShowRoleOfExiled = new NebulaConfiguration(MeetingOptions, "showRoleOfExiled", null, false, false) { Predicate = () => GameOptionsManager.Instance.currentNormalGameOptions.ConfirmImpostor, GameModeMask = CustomGameMode.AllClientGameModeMask };

    static public ConfigurationHolder ExclusiveAssignmentOptions = new("options.exclusiveAssignment", null, ConfigurationTab.Settings, CustomGameMode.AllNormalGameModeMask);
    static public ExclusiveAssignmentConfiguration ExclusiveOptionBody = new(ExclusiveAssignmentOptions, 10);

    static public ConfigurationHolder VoiceChatOptions = new("options.voiceChat", null, ConfigurationTab.Settings, CustomGameMode.AllClientGameModeMask);
    static public NebulaConfiguration UseVoiceChatOption = new NebulaConfiguration(VoiceChatOptions, "useVoiceChat", null, false, false);
    static public NebulaConfiguration CanTalkInWandaringPhaseOption = new NebulaConfiguration(VoiceChatOptions, "canTalkInWandaringPhase", null, true, true) { Predicate = () => UseVoiceChatOption };
    static public NebulaConfiguration WallsBlockAudioOption = new NebulaConfiguration(VoiceChatOptions, "wallsBlockAudio", null, true, true) { Predicate = () => CanTalkInWandaringPhaseOption };
    static public NebulaConfiguration KillersHearDeadOption = new(VoiceChatOptions, "killersHearDead", null,
    new string[] { "options.switch.off", "options.voiceChat.killersHearDead.onlyMyKiller", "options.voiceChat.killersHearDead.onlyImpostors" }, 0, 0)
    { Predicate = () => UseVoiceChatOption };
    static public NebulaConfiguration ImpostorsRadioOption = new NebulaConfiguration(VoiceChatOptions, "impostorsRadio", null, false, false) { Predicate = () => UseVoiceChatOption };
    static public NebulaConfiguration JackalRadioOption = new NebulaConfiguration(VoiceChatOptions, "jackalRadio", null, false, false) { Predicate = () => UseVoiceChatOption };
    static public NebulaConfiguration LoversRadioOption = new NebulaConfiguration(VoiceChatOptions, "loversRadio", null, false, false) { Predicate = () => UseVoiceChatOption };
    static public NebulaConfiguration AffectedByCommsSabOption = new NebulaConfiguration(VoiceChatOptions, "affectedByCommsSab", null, false, false) { Predicate = () => CanTalkInWandaringPhaseOption };
    static public NebulaConfiguration IsolateGhostsStrictlyOption = new NebulaConfiguration(VoiceChatOptions, "isolateGhostsStrictly", null, false, false) { Predicate = () => UseVoiceChatOption };

    static private XOnlyDividedSpriteLoader mapCustomizationSprite = XOnlyDividedSpriteLoader.FromResource("Nebula.Resources.MapCustomizations.png", 100f, 50, true);
    
    static void OpenMapConfigurationEditor(MetaScreen screen, byte? mapId, Func<byte,IEnumerable<(IMetaParallelPlacableOld button, Vector2 pos)>> widgetBuilder)
    {
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
                if (MapCustomizations[id] != null) return id;
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
                if (MapCustomizations[id] != null) return id;
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

    static void OpenMapEditor(MetaScreen screen, byte? mapId = null) => OpenMapConfigurationEditor(screen, mapId, mapId =>

         Enumerable.Concat(
            MapCustomizations[mapId].BoolOptions.Select(
                c => ((IMetaParallelPlacableOld)new MetaWidgetOld.Image(mapCustomizationSprite.GetSprite((int)c.type))
                {
                    Width = 0.5f,
                    PostBuilder = (renderer) =>
                    {
                        renderer.color = c.configuration.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.45f);
                        var button = renderer.gameObject.SetUpButton(true);
                        button.OnMouseOver.AddListener(() =>
                        {
                            MetaWidgetOld widget = new();
                            widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = c.configuration.Id }).Append(new MetaWidgetOld.WrappedWidget(NebulaConfiguration.GetDetailWidget(c.configuration.Id + ".detail") ?? new GUIEmptyWidget()));
                            NebulaManager.Instance.SetHelpWidget(button, widget);
                        });
                        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                        button.OnClick.AddListener(() => { c.configuration.ToggleValue(); renderer.color = c.configuration.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.45f); });
                        var collider = button.gameObject.AddComponent<BoxCollider2D>();
                        collider.isTrigger = true;
                        collider.size = new(0.5f, 0.5f);
                    }
                }, c.position)),
            MapCustomizations[mapId].StandardOptions.Select(
                c =>
                {
                    TMPro.TextMeshPro valueText = null!;
                    return ((IMetaParallelPlacableOld)new CombinedWidgetOld(new MetaWidgetOld(
                        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttrLeft) { Size = new(0.55f, 0.15f) }) { MyText = c.configuration.Title },
                        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Size = new(0.55f, 0.28f) }) { RawText = c.configuration.ToDisplayString(), PostBuilder = text => valueText = text }
                        ), MetaWidgetOld.Button.GetTwoWayButton(increament => { c.configuration.ChangeValue(increament); valueText.text = c.configuration.ToDisplayString(); }))
                    { PostBuilder = obj => obj.AddComponent<SortingGroup>().sortingOrder = 12 }, c.position);
                })
            )
    );

    static void OpenCandidatesFilter(MetaScreen screen, byte? mapId = null) => OpenMapConfigurationEditor(screen, mapId, mapId =>
            NebulaPreSpawnLocation.Locations[mapId].Select(
                l => ((IMetaParallelPlacableOld)new MetaWidgetOld.Image(mapCustomizationSprite.GetSprite(5))
                {
                    Width = 0.5f,
                    PostBuilder = (renderer) =>
                    {
                        renderer.color = l.Configuration.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.65f);
                        var button = renderer.gameObject.SetUpButton(true);
                        button.OnMouseOver.AddListener(() =>
                        {
                            MetaWidgetOld widget = new();
                            widget.Append(new MetaWidgetOld.VariableText(TextAttributeOld.BoldAttr) { Alignment = IMetaWidgetOld.AlignmentOption.Left, TranslationKey = l.GetDisplayName(mapId) });
                            NebulaManager.Instance.SetHelpWidget(button, widget);
                        });
                        button.OnMouseOut.AddListener(() => NebulaManager.Instance.HideHelpWidgetIf(button));
                        button.OnClick.AddListener(() => { l.Configuration.ToggleValue(); renderer.color = l.Configuration.CurrentValue ? Color.white : Color.red.RGBMultiplied(0.65f); });
                        var collider = button.gameObject.AddComponent<BoxCollider2D>();
                        collider.isTrigger = true;
                        collider.size = new(0.5f, 0.5f);
                    }
                }, l.Position!.Value))
    );


    static IEnumerable<object?> RestrictionSelections()
    {
        yield return null;
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
    static string RestrictionDecorator(object? val)
    {
        if (val is null) return Language.Translate("options.consoleRestriction.unlimited");
        return NebulaConfiguration.SecDecorator(val);
    }

    static public ConfigurationHolder ConsoleRestrictionOptions = new("options.consoleRestriction", null, ConfigurationTab.Settings, CustomGameMode.Standard | CustomGameMode.FreePlay);
    static public NebulaConfiguration ResetRestrictionsOption = new NebulaConfiguration(ConsoleRestrictionOptions, "resetRestrictions", null, true, true);
    static public NebulaConfiguration AdminRestrictionOption = new NebulaConfiguration(ConsoleRestrictionOptions, "adminRestriction", null, RestrictionSelections().ToArray(), null, null, RestrictionDecorator);
    static public NebulaConfiguration VitalsRestrictionOption = new NebulaConfiguration(ConsoleRestrictionOptions, "vitalsRestriction", null, RestrictionSelections().ToArray(), null, null, RestrictionDecorator);
    static public NebulaConfiguration CameraRestrictionOption = new NebulaConfiguration(ConsoleRestrictionOptions, "cameraRestriction", null, RestrictionSelections().ToArray(), null, null, RestrictionDecorator);


    static public CustomGameMode CurrentGameMode => CustomGameMode.AllGameMode[GameModeOption.CurrentUncheckedValue];

    public class ExclusiveAssignmentConfiguration
    {
        private static Func<string, int> RoleMapper = (name) =>
        {
            if (name == "none") return short.MaxValue;
            return Roles.Roles.AllRoles.FirstOrDefault((role) => role.LocalizedName == name)?.Id ?? short.MaxValue;
        };

        private static Func<int,string> RoleSerializer = (id) =>
        {
            if (id == short.MaxValue) return "none";
            return Roles.Roles.AllRoles.FirstOrDefault((role) => role.Id == id)?.LocalizedName ?? "none";
        };

        public class ExclusiveAssignment
        {
            NebulaConfiguration toggleOption = null!;
            NebulaStringConfigEntry[] roles = null!;
            public ExclusiveAssignment(ConfigurationHolder holder,int index)
            {
                toggleOption = new(holder, "category." + index, null, false, false);
                toggleOption.Editor = () =>
                {
                    MetaWidgetOld widget = new();

                    List<IMetaParallelPlacableOld> contents = new();
                    contents.Add(NebulaConfiguration.OptionButtonWidget(() => toggleOption.ChangeValue(true), toggleOption.Title.GetString(), 0.85f));
                    contents.Add(NebulaConfiguration.OptionTextColon);


                    if (!toggleOption)
                    {
                        string innerText = "";
                        bool isFirst = true;
                        foreach (var assignment in roles)
                        {
                            var role = assignment.CurrentValue == short.MaxValue ? null : Roles.Roles.AllRoles[assignment.CurrentValue];
                            if (role == null) continue;
                            if (!isFirst) innerText += ", ";
                            innerText += role?.DisplayName.Color(role.RoleColor) ?? "None";
                            isFirst = false;
                        }
                        if (innerText.Length > 0) innerText = "(" + innerText + ")";
                        contents.Add(new MetaWidgetOld.Text(NebulaConfiguration.GetOptionBoldAttr(4.8f,TMPro.TextAlignmentOptions.Left)) { RawText = Language.Translate("options.inactivated") + " " + innerText.Color(Color.gray) });
                    }
                    else
                    {
                        foreach (var assignment in roles)
                        {
                            var role = assignment.CurrentValue == short.MaxValue ? null : Roles.Roles.AllRoles[assignment.CurrentValue];

                            var copiedAssignment = assignment;
                            contents.Add(new MetaWidgetOld.Button(() =>
                            {
                                MetaScreen screen = MetaScreen.GenerateWindow(new(6.5f, 3f), HudManager.Instance.transform, Vector3.zero, true, true);
                                MetaWidgetOld inner = new();
                                inner.Append(Roles.Roles.AllRoles.Prepend(null), (role) => NebulaConfiguration.OptionButtonWidget(
                                    () =>
                                    {
                                        copiedAssignment.UpdateValue(role?.Id ?? short.MaxValue, true).Share();
                                        screen.CloseScreen();
                                    },
                                    role?.DisplayName.Color(role.RoleColor) ?? "None",
                                    1.1f
                                    ), 4, -1, 0, 0.45f);
                                screen.SetWidget(new MetaWidgetOld.ScrollView(new Vector2(6.5f, 3f), inner));
                            }, new(NebulaConfiguration.OptionValueAttr) { Size = new(1.3f, 0.3f) })
                            { RawText = role?.DisplayName.Color(role.RoleColor) ?? "None", PostBuilder = (_, renderer, _) => renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask });
                        }
                    }

                    widget.Append(new CombinedWidgetOld(0.65f, contents.ToArray()));
                    return widget;
                };
                toggleOption.Shower = () =>
                {
                    if (!toggleOption) return null!;

                    string innerText = "";
                    bool isFirst = true;
                    foreach (var assignment in roles)
                    {
                        var role = assignment.CurrentValue == short.MaxValue ? null : Roles.Roles.AllRoles[assignment.CurrentValue];
                        if(role == null) continue;
                        if (!isFirst) innerText += ", ";
                        innerText += role?.DisplayName.Color(role.RoleColor) ?? "None";
                        isFirst = false;
                    }
                    return toggleOption.Title.GetString() + " : " + innerText;
                };

                roles = new NebulaStringConfigEntry[3];
                
                for (int i = 0; i < 3; i++) roles[i] = new NebulaStringConfigEntry(toggleOption.Id + ".role" + i, "none", RoleMapper, RoleSerializer);
                
            }

            public IEnumerable<AbstractRole> OnAsigned(AbstractRole role) {
                if (!toggleOption) yield break;
                if (!roles.Any(entry => entry.CurrentValue == role.Id)) yield break;

                foreach(var assignment in roles)
                {
                    if (assignment.CurrentValue == role.Id) continue;
                    if (assignment.CurrentValue == short.MaxValue) continue;

                    var r = Roles.Roles.AllRoles.FirstOrDefault((role) => role.Id == assignment.CurrentValue);
                    if(r != null) yield return r;
                }
            }
        }

        ExclusiveAssignment[] allAsignment;
        public ExclusiveAssignmentConfiguration(ConfigurationHolder holder,int num)
        {
            allAsignment = new ExclusiveAssignment[num];
            for (int i = 0; i < num; i++) allAsignment[i] = new ExclusiveAssignment(holder,i);
        }

        public IEnumerable<AbstractRole> OnAssigned(AbstractRole role) {
            foreach (var assignment in allAsignment) foreach (var r in assignment.OnAsigned(role)) yield return r;
        }
    }
}
