﻿using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Nebula.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.Modules;


[HarmonyPatch(typeof(OptionsMenuBehaviour), nameof(OptionsMenuBehaviour.Start))]
public static class StartOptionMenuPatch
{
    public static void Postfix(OptionsMenuBehaviour __instance)
    {
        var tabs = new List<TabGroup>(__instance.Tabs.ToArray());

        PassiveButton passiveButton;
        ToggleButtonBehaviour toggleButton;

        //設定項目を追加する

        GameObject nebulaTab = new GameObject("NebulaTab");
        nebulaTab.transform.SetParent(__instance.transform);
        nebulaTab.transform.localScale = new Vector3(1f, 1f, 1f);
        nebulaTab.SetActive(false);

        var nebulaScreen = MetaScreen.GenerateScreen(new(5f, 4.5f), nebulaTab.transform, new(0f, -0.28f, -10f), false, false, false);

        void SetNebulaContext()
        {
            MetaContext nebulaContext = new();
            nebulaContext.Append(new MetaContext.Button(() => {
                __instance.OpenTabGroup(tabs.Count - 1);
                SetKeyBindingContext();
            }, new(TextAttribute.BoldAttr) { Size = new Vector2(2.2f, 0.26f) }) { TranslationKey = "config.client.keyBindings", Alignment = IMetaContext.AlignmentOption.Center });
            nebulaScreen.SetContext(nebulaContext);
        }

        GameObject keyBindingTab = new GameObject("KeyBindingTab");
        keyBindingTab.transform.SetParent(__instance.transform);
        keyBindingTab.transform.localScale = new Vector3(1f, 1f, 1f);
        keyBindingTab.SetActive(false);

        var keyBindingScreen = MetaScreen.GenerateScreen(new(5f, 4.5f), keyBindingTab.transform, new(0f, -0.28f, -10f), false, false, false);

        KeyAssignment? currentAssignment = null;

        void SetKeyBindingContext()
        {
            MetaContext keyBindingContext = new();
            TMPro.TextMeshPro? text = null;
            keyBindingContext.Append(KeyAssignment.AllKeyAssignments, (assignment) =>
            new MetaContext.Button(() =>
            {
                currentAssignment = assignment;
                SetKeyBindingContext();
            }, new(TextAttribute.NormalAttr) { Size = new Vector2(2.2f, 0.26f) })
            { RawText = assignment.DisplayName + " : " + (currentAssignment == assignment ? Language.Translate("input.recording") : ButtonEffect.KeyCodeInfo.AllKeyInfo[assignment.KeyInput].TranslationKey), PostBuilder = (_, _, t) => text = t }, 2, -1, 0, 0.55f);
            keyBindingScreen.SetContext(keyBindingContext);
        }

        void CoUpdate()
        {
            if (currentAssignment != null && Input.anyKeyDown)
            {
                foreach (var keyCode in ButtonEffect.KeyCodeInfo.AllKeyInfo.Values)
                {
                    if (Input.GetKeyDown(keyCode.keyCode))
                    {
                        currentAssignment.KeyInput = keyCode.keyCode;
                        currentAssignment = null;
                        SetKeyBindingContext();
                        break;
                    }
                }
            }
        }

        keyBindingScreen.gameObject.AddComponent<ScriptBehaviour>().UpdateHandler += CoUpdate;

        SetNebulaContext();
        SetKeyBindingContext();

        //タブを追加する

        tabs[tabs.Count - 1] = (GameObject.Instantiate(tabs[1], null));
        var nebulaButton = tabs[tabs.Count - 1];
        nebulaButton.gameObject.name = "NebulaButton";
        nebulaButton.transform.SetParent(tabs[0].transform.parent);
        nebulaButton.transform.localScale = new Vector3(1f, 1f, 1f);
        nebulaButton.Content = nebulaTab;
        var textObj = nebulaButton.transform.FindChild("Text_TMP").gameObject;
        textObj.GetComponent<TextTranslatorTMP>().enabled = false;
        textObj.GetComponent<TMPro.TMP_Text>().text = "NoS";

        tabs.Add((GameObject.Instantiate(tabs[1], null)));
        var keyBindingTabButton = tabs[tabs.Count - 1];
        keyBindingTabButton.gameObject.name = "KeyBindingButton";
        keyBindingTabButton.transform.SetParent(tabs[0].transform.parent);
        keyBindingTabButton.transform.localScale = new Vector3(1f, 1f, 1f);
        keyBindingTabButton.Content = keyBindingTab;
        keyBindingTabButton.gameObject.SetActive(false);

        passiveButton = nebulaButton.gameObject.GetComponent<PassiveButton>();
        passiveButton.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        passiveButton.OnClick.AddListener((UnityEngine.Events.UnityAction)(() =>
        {
            __instance.OpenTabGroup(tabs.Count - 2);
            SetNebulaContext();
        }
        ));

        float y = tabs[0].transform.localPosition.y, z = tabs[0].transform.localPosition.z;
        if (tabs.Count == 4)
            for (int i = 0; i < 3; i++) tabs[i].transform.localPosition = new Vector3(1.7f * (float)(i - 1), y, z);
        else if (tabs.Count == 5)
            for (int i = 0; i < 4; i++) tabs[i].transform.localPosition = new Vector3(1.62f * ((float)i - 1.5f), y, z);

        __instance.Tabs = new Il2CppReferenceArray<TabGroup>(tabs.ToArray());


    }
}