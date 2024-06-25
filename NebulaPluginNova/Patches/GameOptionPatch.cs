using Nebula.Modules.GUIWidget;

namespace Nebula.Patches;

[HarmonyPatch(typeof(GameSettingMenu), nameof(GameSettingMenu.Start))]
public class GameSettingMenuStartPatch
{
    public static DividedSpriteLoader tabSprites = DividedSpriteLoader.FromResource("Nebula.Resources.TabIcon.png",100f, 6,1);
    public static Image backSprite = SpriteLoader.FromResource("Nebula.Resources.TabBackground.png",100f);
    private static (GameObject, SpriteRenderer, int) CreateTab(GameSettingMenu __instance,GameObject phoneLeft, string tabName, GameObject?[] screens, int id, List<(GameObject, SpriteRenderer, int)> allTabs)
    {
        Vector3 locPos = new(-0.8f + (id * 0.8f), 2.48f, 0.5f);
        var background = UnityHelper.CreateObject<SpriteRenderer>("Tab", __instance.transform, locPos);
        background.sprite = backSprite.GetSprite();
        background.transform.localScale = new(0.5635f, 0.5635f, 1f);
        var renderer = UnityHelper.CreateObject<SpriteRenderer>(tabName, background.transform, new Vector3(0f, 0f, -0.01f));
        renderer.sprite = tabSprites.GetSprite(id * 2 + 1);
        renderer.gameObject.transform.localScale = new(0.53f, 0.53f, 1f);
        var button = renderer.gameObject.SetUpButton(true, renderer);
        button.OnClick.AddListener(() =>
        {
            screens.Do(tab => tab?.SetActive(false));
            screens[id]?.SetActive(true);
            allTabs.Do(tab =>
            {
                //tab.Item2.sprite = tabSprites.GetSprite(tab.Item3 * 2);

                var locPos = tab.Item1.transform.localPosition;
                locPos.z = -0.4f + 0.1f * tab.Item3; 
                tab.Item1.transform.localPosition = locPos;
            });
            //allTabs[id].Item2.sprite = tabSprites.GetSprite(id * 2 + 1);
            
            var locPos = allTabs[id].Item1.transform.localPosition;
            locPos.z = -1f;
            allTabs[id].Item1.transform.localPosition = locPos;

            phoneLeft.SetActive(id != 1);
        });
        var collider = button.gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new(1.6f, 1.6f);
        return (background.gameObject, renderer, id);
    }
    

    private static GameObject CreateSetting(GameSettingMenu __instance, string name)
    {
        return UnityHelper.CreateObject(name, __instance.transform, new Vector3(0, 0, -5f));
    }


    public static void Postfix(GameSettingMenu __instance)
    {
        var inners = ((int[])[2, 3, 4, 5]).Select(i => __instance.transform.GetChild(i)).ToArray();

        //Background(Phone)
        var phoneLeft = __instance.transform.GetChild(1).GetChild(0).gameObject;
        //Close Button
        __instance.transform.GetChild(6).transform.localPosition = new(-4.85f, 2.6f, -25f);
        //Role Settings
        __instance.transform.GetChild(4).GetChild(2).gameObject.SetActive(false);

        var innerHolder = UnityHelper.CreateObject("InnerHolder", __instance.transform, Vector3.zero);
        inners.Do(inner => inner.transform.SetParent(innerHolder.transform, true));

        var nebulaSetting = CreateSetting(__instance, "NebulaSetting");
        var presetSetting = CreateSetting(__instance, "PresetSetting");

        nebulaSetting.AddComponent<NebulaSettingMenu>();
        presetSetting.AddComponent<PresetSettingMenu>().GameSettingMenu = __instance;

        nebulaSetting.SetActive(false);
        presetSetting.SetActive(false);

        GameObject?[] screens = [innerHolder, nebulaSetting, presetSetting];
        List<(GameObject, SpriteRenderer, int)> allTabs = new();
        allTabs.Add(CreateTab(__instance, phoneLeft, "VanillaTab", screens, 0, allTabs));
        allTabs.Add(CreateTab(__instance, phoneLeft, "NebulaTab", screens, 1, allTabs));
        allTabs.Add(CreateTab(__instance, phoneLeft, "PresetTab", screens, 2, allTabs));
        allTabs[0].Item2.GetComponent<PassiveButton>().OnClick.Invoke();

        __instance.ChangeTab(1, false);

        foreach (var child in __instance.GameSettingsTab.Children)
        {
            child.OnValueChanged += (Action<OptionBehaviour>)(option => {
                ConfigurationValues.RpcSharePresetName.Invoke("");
            });
        }
    }
    
}


[HarmonyPatch(typeof(GameOptionsMenu), nameof(GameOptionsMenu.Initialize))]
class GameOptionsMenuStartPatch
{
    public static void Postfix(GameOptionsMenu __instance)
    {

        var commonTasksOption = __instance.Children.Find((Il2CppSystem.Predicate<OptionBehaviour>)(x => x.TryGetComponent<NumberOption>(out var op) && op.intOptionName == AmongUs.GameOptions.Int32OptionNames.NumCommonTasks))?.TryCast<NumberOption>();
        if (commonTasksOption != null) commonTasksOption.ValidRange = new FloatRange(0f, 4f);

        var shortTasksOption = __instance.Children.Find((Il2CppSystem.Predicate<OptionBehaviour>)(x => x.TryGetComponent<NumberOption>(out var op) && op.intOptionName == AmongUs.GameOptions.Int32OptionNames.NumShortTasks))?.TryCast<NumberOption>();
        if (shortTasksOption != null) shortTasksOption.ValidRange = new FloatRange(0f, 23f);

        var longTasksOption = __instance.Children.Find((Il2CppSystem.Predicate<OptionBehaviour>)(x => x.TryGetComponent<NumberOption>(out var op) && op.intOptionName == AmongUs.GameOptions.Int32OptionNames.NumLongTasks))?.TryCast<NumberOption>();
        if (longTasksOption != null) longTasksOption.ValidRange = new FloatRange(0f, 15f);
    }
}
