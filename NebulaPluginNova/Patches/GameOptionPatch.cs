using AmongUs.Data;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.Injection;
using Nebula.Modules.GUIWidget;
using TMPro;

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

        //インポスターの人数の上限解放はローカル or カスタムサーバーのみ
        if (AmongUsUtil.IsCustomServer() || (AmongUsClient.Instance?.AmLocalHost ?? false))
        {
            var impostorsOption = __instance.Children.Find((Il2CppSystem.Predicate<OptionBehaviour>)(x => x.TryGetComponent<NumberOption>(out var op) && op.intOptionName == AmongUs.GameOptions.Int32OptionNames.NumImpostors))?.TryCast<NumberOption>();
            if (impostorsOption != null) impostorsOption.ValidRange = new FloatRange(0f, 6f);
        }
    }
}

class CreateGameOptionsNoSBehaviour : MonoBehaviour
{
    static CreateGameOptionsNoSBehaviour() => ClassInjector.RegisterTypeInIl2Cpp<CreateGameOptionsNoSBehaviour>();

    public CreateOptionsPicker MyPicker;

    void Awake()
    {
        MyPicker = gameObject.GetComponent<CreateOptionsPicker>();

        MyPicker.transform.FindChild("Game Mode").gameObject.SetActive(false);

        var impostorsRoot = MyPicker.transform.FindChild("Impostors");
        if (impostorsRoot)
        {
            impostorsRoot.transform.localPosition = new(-1.955f, -0.44f, 0f);

            var temp = impostorsRoot.transform.GetChild(1);

            var list = MyPicker.ImpostorButtons.ToList();
            for (int i = 4; i <= 6; i++) {
                var obj = GameObject.Instantiate(temp, impostorsRoot);
                obj.name = i.ToString();
                obj.transform.localPosition = new((i - 1) * 0.6f, 0f, 0f);
                obj.GetChild(0).GetComponent<TextMeshPro>().text = i.ToString();
                var passiveButton = obj.gameObject.GetComponent<PassiveButton>();
                passiveButton.OnClick = new();
                int impostors = i;
                passiveButton.OnClick.AddListener(() => MyPicker.SetImpostorButtons(impostors));

                list.Add(obj.GetComponent<ImpostorsOptionButton>());
            }

            MyPicker.ImpostorButtons = list.ToArray();
        }
    }

    void OnEnable()
    {
        if (!MyPicker) return;

        bool isCustomServer = AmongUsUtil.IsCustomServer();

        if (MyPicker.MaxPlayersRoot)
        {
            //以前のボタンを削除する
            MyPicker.optionsMenu.ControllerSelectable.Clear();
            MyPicker.MaxPlayerButtons.Clear();

            Helpers.Sequential(MyPicker.MaxPlayersRoot.childCount).Skip(1).Select(i => MyPicker.MaxPlayersRoot.GetChild(i).gameObject).ToArray().Do(GameObject.Destroy);
            
            for (int i = 4; i <= (isCustomServer ? 24 : 15); i++)
            {
                SpriteRenderer spriteRenderer = GameObject.Instantiate<SpriteRenderer>(MyPicker.MaxPlayerButtonPrefab, MyPicker.MaxPlayersRoot);
                spriteRenderer.transform.localPosition = new Vector3((float)((i - 4) % 12) * 0.5f, (float)(i/16) * -0.47f, 0f);
                int numPlayers = i;
                spriteRenderer.name = numPlayers.ToString();
                PassiveButton component = spriteRenderer.GetComponent<PassiveButton>();
                component.OnClick.AddListener(() => MyPicker.SetMaxPlayersButtons(numPlayers));
                spriteRenderer.GetComponentInChildren<TextMeshPro>().text = numPlayers.ToString();
                MyPicker.MaxPlayerButtons.Add(spriteRenderer);
                MyPicker.optionsMenu.ControllerSelectable.Add(component);
            }
        }

        var subMenu = MyPicker.transform.FindChild("SubMenu");
        subMenu.transform.localPosition = new(1.11f, isCustomServer ? -0.4f : 0f, 0f);
        subMenu.GetComponent<ShiftButtonsCrossplayEnabled>().enabled = false;

        //4人以上のオプションはカスタムサーバーのみ使用可能
        for (int i = 4; i <= 6; i++) MyPicker.ImpostorButtons[i - 1].gameObject.SetActive(isCustomServer);

        var options = MyPicker.GetTargetOptions();

        MyPicker.SetMaxPlayersButtons(isCustomServer ? 24 : 15);
    }
}

static internal class ModdedOptionValues
{
    static public int[] MaxImpostors = [
            0,
            0, 0, 0, 1, 1,
            1, 2, 2, 3, 3,
            3, 3, 4, 4, 5,
            5, 5, 6, 6, 6,
            6, 6, 6, 6
        ];

    static public int[] RecommendedImpostors = [
            0,
            0, 0, 0, 1, 1,
            1, 2, 2, 2, 2,
            2, 3, 3, 3, 3,
            3, 4, 4, 4, 4,
            5, 5, 5, 5
        ];

    static public int[] RecommendedKillCondown = [
            0,
            0, 0, 0, 45, 30,
            15, 35, 30, 25, 20,
            20, 20, 20, 20, 20,
            20, 20, 20, 20, 20,
            20, 20, 20, 20
        ];

    static public int[] MinPlayers = [4, 4, 7, 9, 13, 15, 18];
}

[HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Awake))]
class CreateGameOptionsShowPatch
{
    public static bool Prefix(CreateOptionsPicker __instance)
    {
        //各種設定の配列長を24人分まで伸ばす
        NormalGameOptionsV07.MaxImpostors = 
            NormalGameOptionsV08.MaxImpostors = 
            NormalGameOptionsV09.MaxImpostors =
            LegacyGameOptions.MaxImpostors = ModdedOptionValues.MaxImpostors;

        NormalGameOptionsV07.RecommendedImpostors = 
            NormalGameOptionsV08.RecommendedImpostors =
            NormalGameOptionsV09.RecommendedImpostors =
            LegacyGameOptions.RecommendedImpostors = ModdedOptionValues.RecommendedImpostors;

        NormalGameOptionsV07.RecommendedKillCooldown = 
            NormalGameOptionsV08.RecommendedKillCooldown =
            NormalGameOptionsV09.RecommendedKillCooldown =
            LegacyGameOptions.RecommendedKillCooldown = ModdedOptionValues.RecommendedKillCondown;
        NormalGameOptionsV07.MinPlayers =
            NormalGameOptionsV08.MinPlayers =
            NormalGameOptionsV09.MinPlayers =
            LegacyGameOptions.MinPlayers = ModdedOptionValues.MinPlayers;

        //ゲームモードはノーマル固定
        DataManager.Settings.Multiplayer.LastPlayedGameMode = AmongUs.GameOptions.GameModes.Normal;
        DataManager.Settings.Save();
        GameOptionsManager.Instance.SwitchGameMode(AmongUs.GameOptions.GameModes.Normal);

        __instance.gameObject.AddComponent<CreateGameOptionsNoSBehaviour>();

        return false;
    }
}

[HarmonyPatch(typeof(CreateOptionsPicker), nameof(CreateOptionsPicker.Refresh))]
class CreateGameOptionsStartPatch
{
    static int impostors = 1;
    public static void Prefix(CreateOptionsPicker __instance)
    {
        impostors = AmongUsUtil.NumOfImpostors;
        Debug.Log("Impostors(A): " + impostors);
    }
    public static void Postfix(CreateOptionsPicker __instance)
    {
        impostors = Math.Min(impostors, AmongUsUtil.IsCustomServer() ? 6 : 3);
        __instance.SetImpostorButtons(impostors);
        Debug.Log("Impostors(B): " + impostors);
    }
}
