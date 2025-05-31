using Il2CppInterop.Runtime.Injection;
using static Nebula.Modules.NebulaAddon;
using System.Text;
using System.IO.Compression;
using UnityEngine.Rendering;
using TMPro;
using static Nebula.Modules.MetaWidgetOld;
using Nebula.Modules.GUIWidget;
using Virial.Text;
using Virial.Media;
using UnityEngine;
using Nebula.Modules.Cosmetics;

namespace Nebula.Behavior;

internal static class MainMenuManagerInstance { 
    internal static MainMenuManager? MainMenu;
    internal static GameObject? CustomizationMenu;
    
    internal static void SetPrefab(MainMenuManager mainMenu)
    {
        MainMenu = mainMenu;
        CustomizationMenu = mainMenu.playerCustomizationPrefab.gameObject;
    }
    internal static void SetPrefab(GameObject customizationMenu)
    {
        MainMenu = null;
        CustomizationMenu = customizationMenu;
    }

    internal static void Open<T>(string objectName, MainMenuManager mainMenu, Action<T> callback) where T : MonoBehaviour
    {
        MainMenuManagerInstance.SetPrefab(mainMenu);
        var obj = UnityHelper.CreateObject<T>(objectName, Camera.main.transform, new Vector3(0, 0, -30f));
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { mainMenu.mainMenuUI.SetActive(false); }, () => callback.Invoke(obj));
    }
    internal static void Close(MonoBehaviour viewer)
    {
        TransitionFade.Instance.DoTransitionFade(viewer.gameObject, null!, () => MainMenuManagerInstance.MainMenu?.mainMenuUI.SetActive(true), () => GameObject.Destroy(viewer.gameObject));
    }

    internal static MetaScreen SetUpScreen(Transform transform, Action closeScreen, bool withoutBackground = false)
    {
        if (MainMenuManagerInstance.CustomizationMenu != null && !withoutBackground)
        {
            var backBlackPrefab = MainMenuManagerInstance.CustomizationMenu.transform.GetChild(1);
            GameObject.Instantiate(backBlackPrefab.gameObject, transform);
            var backGroundPrefab = MainMenuManagerInstance.CustomizationMenu.transform.GetChild(2);
            var backGround = GameObject.Instantiate(backGroundPrefab.gameObject, transform);
            GameObject.Destroy(backGround.transform.GetChild(2).gameObject);

            var closeButtonPrefab = MainMenuManagerInstance.CustomizationMenu.transform.GetChild(0).GetChild(0);
            var closeButton = GameObject.Instantiate(closeButtonPrefab.gameObject, transform);
            GameObject.Destroy(closeButton.GetComponent<AspectPosition>());
            var button = closeButton.GetComponent<PassiveButton>();
            button.gameObject.SetActive(true);
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener(closeScreen);
            button.transform.localPosition = new Vector3(-4.9733f, 2.6708f, -50f);
        }
        return UnityHelper.CreateObject<MetaScreen>("Screen", transform, new Vector3(0, -0.1f, -10f));
    }
}

public class DevStudio : MonoBehaviour
{
    static DevStudio() => ClassInjector.RegisterTypeInIl2Cpp<DevStudio>();

    private MetaScreen myScreen = null!;

    private List<Func<(IMetaWidgetOld widget, Action? postAction,Func<bool>? confirm)>> screenLayer = new();
    private Func<bool>? currentConfirm = null;
    private const float ScreenWidth = 9f;

    private bool HasContent => screenLayer.Count > 0;

    private void ChangeScreen(bool reopen, bool surely = false)
    {
        if (screenLayer.Count == 0) return;
        var content = screenLayer[screenLayer.Count - 1];

        //falseを返す場合は画面を遷移させない
        if (!surely && !(currentConfirm?.Invoke() ?? true)) return;

        if (!reopen)
        {
            screenLayer.RemoveAt(screenLayer.Count - 1);
            currentConfirm = null;
            NebulaManager.Instance.HideHelpWidget();
        }

        if (HasContent)
            OpenFrontScreen();
        else
            Close();

    }

    private void CloseScreen(bool surely = false) => ChangeScreen(false, surely);
    private void ReopenScreen(bool surely = false) => ChangeScreen(true, surely);

    private void OpenFrontScreen()
    {
        if (screenLayer.Count == 0) return;
        var content = screenLayer[screenLayer.Count - 1].Invoke();


        myScreen.GetComponent<SortingGroup>().enabled = true;
        myScreen.SetWidget(new Vector2(ScreenWidth, 5.5f), content.widget);
        content.postAction?.Invoke();
        currentConfirm = content.confirm;
    }

    private void OpenScreen(Func<(IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm)> content)
    {
        screenLayer.Add(content);
        OpenFrontScreen();
    }

    protected void Close() => MainMenuManagerInstance.Close(this);

    static public void Open(MainMenuManager mainMenu) => MainMenuManagerInstance.Open<DevStudio>("DevStudioMenu", mainMenu, viewer => viewer.OnShown());

    public void OnShown() => OpenScreen(ShowMainScreen);

    public void Awake()
    {
        myScreen = MainMenuManagerInstance.SetUpScreen(transform, () => CloseScreen());
    }
    
    public (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowMainScreen()
    {
        void CheckAndGenerateAddon(Il2CppArgument<MetaScreen> editScreen, Il2CppArgument<TextField> id, Il2CppArgument<TextField> name, Il2CppArgument<TextField> author, Il2CppArgument<TextField> desc)
        {
            if (id.Value.Text.Length < 1 || name.Value.Text.Length < 1)
            {
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.requiredText").Color(Color.red * 0.7f));
                name.Value.SetHint(Language.Translate("devStudio.ui.hint.requiredText").Color(Color.red * 0.7f));
                return;
            }
            if (Directory.Exists("Addons/" + id.Value.Text))
            {
                id.Value.SetText("");
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.duplicatedId").Color(Color.red * 0.7f));
                return;
            }

            Directory.CreateDirectory("Addons/" + id.Value.Text);
            using (var stream = new StreamWriter(File.Create("Addons/" + id.Value.Text + "/addon.meta"), Encoding.UTF8))
            {
                stream.Write(JsonStructure.Serialize(new AddonMeta() { Id = id.Value.Text, Name = name.Value.Text, Author = author.Value.Text, Version = "1.0", Description = desc.Value.Text }));
            }
            editScreen.Value.CloseScreen();

            OpenScreen(() => ShowAddonScreen(new DevAddon(name.Value.Text, "Addons/" + id.Value.Text)));
        }

        MetaWidgetOld widget = new();

        widget.Append(
            new ParallelWidgetOld(
                new(new MetaWidgetOld.Text(new Utilities.TextAttributeOld(Utilities.TextAttributeOld.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(4f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.main.title" }, 3f),
                new(new MetaWidgetOld.Button(()=> {
                    PremultipliedConversion.ConvertImages("Utility/Original", "Utility/Multiplied");
                }, new Utilities.TextAttributeOld(Utilities.TextAttributeOld.NormalAttr) { Styles = TMPro.FontStyles.Normal, Size = new(1.1f, 0.25f) }.EditFontSize(1.5f)) { RawText = "乗算済みα変換" }, 1.2f)
                )
            );
        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        //Add-ons
        widget.Append(new MetaWidgetOld.Button(() => 
        {
            var screen = MetaScreen.GenerateWindow(new(5.9f, 3.1f), transform, Vector3.zero, true, false);
            MetaWidgetOld widget = new();

            CombinedWidgetOld GenerateWidget(Reference<TextField> reference, string rawText,bool isMultiline,Predicate<char> predicate)=> 
            new CombinedWidgetOld(
               new MetaWidgetOld.Text(new Utilities.TextAttributeOld(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = rawText },
               new MetaWidgetOld.Text(new Utilities.TextAttributeOld(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
               new MetaWidgetOld.TextInput(isMultiline ? 2 : 1, 2f, new(3.7f, isMultiline ? 0.58f : 0.3f)) { TextFieldRef = reference, TextPredicate = predicate }
                   );

            Reference<TextField> refId = new(), refName = new(), refAuthor = new(), refDesc = new();
            widget.Append(GenerateWidget(refId, "Add-on ID", false, TextField.IdPredicate));
            widget.Append(GenerateWidget(refName, "Name", false, TextField.JsonStringPredicate));
            widget.Append(GenerateWidget(refAuthor, "Author", false, TextField.JsonStringPredicate));
            widget.Append(GenerateWidget(refDesc, "Description", true, TextField.JsonStringPredicate));
            widget.Append(new MetaWidgetOld.VerticalMargin(0.16f));
            widget.Append(new MetaWidgetOld.Button(() => 
            {
                CheckAndGenerateAddon(screen, refId.Value!, refName.Value!, refAuthor.Value!, refDesc.Value!);
            }, new(Utilities.TextAttributeOld.BoldAttr) { Size = new(1.8f, 0.3f) }) { TranslationKey = "devStudio.ui.common.generate", Alignment = IMetaWidgetOld.AlignmentOption.Center });

            screen.SetWidget(widget);
            refId.Value!.InputPredicate = TextField.TokenPredicate;
            TextField.EditFirstField();

        }, new Utilities.TextAttributeOld(Utilities.TextAttributeOld.BoldAttr) { Size = new(0.34f, 0.18f) }.EditFontSize(2.4f)) { RawText = "+" });

        Reference<MetaWidgetOld.ScrollView.InnerScreen> addonsRef = new();
        widget.Append(new MetaWidgetOld.ScrollView(new Vector2(9f, 4f), addonsRef));

        IEnumerator CoLoadAddons()
        {
            yield return addonsRef.Wait();
            
            addonsRef.Value?.SetLoadingWidget();

            var task = DevAddon.SearchDevAddonsAsync();
            yield return task.WaitAsCoroutine();
            
            MetaWidgetOld inner = new();
            foreach (var addon in task.Result)
            {
                inner.Append(
                    new CombinedWidgetOld(
                        new MetaWidgetOld.Text(
                            new(Utilities.TextAttributeOld.NormalAttr)
                            {
                                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                                Size = new(4.6f, 0.27f),
                                Alignment = TMPro.TextAlignmentOptions.Left
                            })
                        { RawText = addon.Name },
                        new MetaWidgetOld.VerticalMargin(0.3f),
                        new MetaWidgetOld.Button(() => OpenScreen(() => ShowAddonScreen(addon)),
                         new(Utilities.TextAttributeOld.NormalAttr)
                         {
                             FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                             Size = new(1f, 0.2f),
                         })
                        { TranslationKey = "devStudio.ui.common.edit" }.SetAsMaskedButton(),
                        new MetaWidgetOld.HorizonalMargin(0.1f),
                        new MetaWidgetOld.Button(() => MetaUI.ShowYesOrNoDialog(transform,() => { Helpers.DeleteDirectoryWithInnerFiles(addon.FolderPath); ReopenScreen(true); }, () => { }, Language.Translate("devStudio.ui.common.confirmDeletingAddon") + $"<br>\"{addon.Name}\""),
                         new(Utilities.TextAttributeOld.NormalAttr)
                         {
                             FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                             Size = new(1f, 0.2f),
                         })
                        { Text = NebulaGUIWidgetEngine.Instance.TextComponent(Color.red.RGBMultiplied(0.7f), "devStudio.ui.common.delete"), Color = Color.red.RGBMultiplied(0.7f) }.SetAsMaskedButton()

                         )
                    { Alignment = IMetaWidgetOld.AlignmentOption.Left }
                    );
            }

            addonsRef.Value?.SetWidget(inner);
        }

        return (widget, ()=>StartCoroutine(CoLoadAddons().WrapToIl2Cpp()), null);
    }

    //Addon
    public (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowAddonScreen(DevAddon addon)
    {
        MetaWidgetOld widget = new();

        void ShowNameEditWindow() {
            var screen = MetaScreen.GenerateWindow(new(3.9f, 1.14f), transform, Vector3.zero, true, false);
            MetaWidgetOld widget = new();
            Reference<TextField> refName = new();

            widget.Append(new MetaWidgetOld.TextInput(1, 2f, new(3.7f, 0.3f)) { TextFieldRef = refName, DefaultText = addon.Name, TextPredicate = TextField.JsonStringPredicate });
            widget.Append(new MetaWidgetOld.Button(() =>
            {
                addon.MetaSetting.Name = refName.Value!.Text;
                UpdateMetaInfo();
                addon.SaveMetaSetting();
                screen.CloseScreen();
                ReopenScreen(true);
            }
            , new(Utilities.TextAttributeOld.BoldAttr) { Size = new(1.8f, 0.3f) })
            { TranslationKey = "devStudio.ui.common.save", Alignment = IMetaWidgetOld.AlignmentOption.Center });

            screen.SetWidget(widget);
        }

        Reference<TextField> authorRef = new();
        Reference<TextField> versionRef = new();
        Reference<TextField> descRef = new();

        //Addon Name
        widget.Append(
            new CombinedWidgetOld(
                new MetaWidgetOld.Button(ShowNameEditWindow, new(Utilities.TextAttributeOld.BoldAttr) { Size = new(0.5f, 0.22f) }) { TranslationKey = "devStudio.ui.common.edit" },
                new MetaWidgetOld.HorizonalMargin(0.14f),
                new MetaWidgetOld.Text(new Utilities.TextAttributeOld(Utilities.TextAttributeOld.TitleAttr) { Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(2.7f)) { RawText = addon.Name }
            ){ Alignment = IMetaWidgetOld.AlignmentOption.Left});

        //Author & Version
        widget.Append( new CombinedWidgetOld(
            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "Author" },
            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaWidgetOld.TextInput(1, 2f, new(2.5f, 0.3f)) { TextFieldRef = authorRef, DefaultText = addon.MetaSetting.Author, TextPredicate = TextField.JsonStringPredicate },

            new MetaWidgetOld.HorizonalMargin(0.4f),

            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.45f, 0.3f) }) { RawText = "Version" },
            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaWidgetOld.TextInput(1, 2f, new(1.5f, 0.3f)) { TextFieldRef = versionRef, DefaultText = addon.MetaSetting.Version, TextPredicate = TextField.NumberPredicate }
               )
        { Alignment = IMetaWidgetOld.AlignmentOption.Left });

        //Description
        widget.Append(new CombinedWidgetOld(
            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "Description" },
            new MetaWidgetOld.Text(new(Utilities.TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaWidgetOld.TextInput(2, 2f, new(6.2f, 0.58f)) { TextFieldRef = descRef, DefaultText = addon.MetaSetting.Description.Replace("<br>","\r"), TextPredicate = TextField.JsonStringPredicate }
               ){ Alignment = IMetaWidgetOld.AlignmentOption.Left });

        bool MetaInfoDirty() => addon.MetaSetting.Author != authorRef.Value!.Text || addon.MetaSetting.Version != versionRef.Value!.Text || addon.MetaSetting.Description != descRef.Value!.Text.Replace("\r", "<br>");

        void UpdateMetaInfo()
        {
            addon.MetaSetting.Author = authorRef.Value!.Text;
            addon.MetaSetting.Version = versionRef.Value!.Text;
            addon.MetaSetting.Description = descRef.Value!.Text.Replace("\r", "<br>");
        }

        //Contents of add-on
        (string translationKey,Func<DevAddon,(IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm)>)[] edtiors = {
            ("devStudio.ui.addon.cosmetics",ShowCosmeticsScreen),
            ("devStudio.ui.addon.document",ShowDocumentScreen),
            ("devStudio.ui.addon.language",ShowLanguageScreen)
        };

        widget.Append(new MetaWidgetOld.VerticalMargin(0.21f));
        widget.Append(edtiors, (entry) => new MetaWidgetOld.Button(() => { UpdateMetaInfo(); addon.SaveMetaSetting(); OpenScreen(() => entry.Item2.Invoke(addon)); }, new(TextAttributeOld.BoldAttr) { Size = new(2.4f, 0.55f) }) { TranslationKey = entry.translationKey }, 3, 3, 0, 0.85f, true);
        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        //Build
        widget.Append(new MetaWidgetOld.Button(() => addon.BuildAddon(), new TextAttributeOld(TextAttributeOld.BoldAttr)) { TranslationKey = "devStudio.ui.addon.build", Alignment = IMetaWidgetOld.AlignmentOption.Right });

        return (widget, null, () => {
            if (!MetaInfoDirty()) return true;
            MetaUI.ShowYesOrNoDialog(transform, ()=> { UpdateMetaInfo(); addon.SaveMetaSetting(); CloseScreen(true); }, () => { CloseScreen(true); },  Language.Translate("devStudio.ui.common.confirmSaving"));
            return false;
        }
        );
    }

    //Languages
    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowLanguageScreen(DevAddon addon)
    {
        Directory.CreateDirectory(addon.FolderPath + Path.DirectorySeparatorChar + "Language");

        List<GUIWidget> widgets = new();
        widgets.Add(GUI.Instance.LocalizedText(Virial.Media.GUIAlignment.Left, GUI.Instance.GetAttribute(AttributeAsset.OblongHeader), "devStudio.ui.addon.document"));
        widgets.Add(GUI.Instance.Margin(new(null, 0.2f)));

        void OpenDetailWindow(uint languageId)
        {
            string lName = Language.GetLanguage(languageId);
            string fPath = "Language"+ Path.DirectorySeparatorChar + lName + ".dat";
            bool exists = addon.ExistsFile(fPath);

            var window = MetaScreen.GenerateWindow(new(2.2f, 2.4f), transform, Vector3.zero, true, true);

            var holder = GUI.API.VerticalHolder(Virial.Media.GUIAlignment.Center,
                GUI.Instance.RawText(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeAsset.CenteredBold), Language.GetLanguageShownString(languageId)),
                GUI.API.VerticalMargin(0.22f),
                exists ?
                GUI.Instance.LocalizedButton(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.language.editFile", _ => {
                    System.Diagnostics.ProcessStartInfo processStartInfo = new();
                    processStartInfo.FileName = addon.FolderPath + Path.DirectorySeparatorChar + fPath;
                    processStartInfo.CreateNoWindow = true;
                    processStartInfo.UseShellExecute = true;
                    System.Diagnostics.Process.Start(processStartInfo);
                    window.CloseScreen();
                    ReopenScreen(true);
                }) : 
                GUI.Instance.LocalizedButton(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.language.newFile", _ => {
                    if(NebulaPlugin.GuardVanillaLangData)
                        addon.WriteFile(fPath, "");
                    else
                        addon.WriteFile(fPath, new StreamReader(Language.OpenDefaultLangStream()!).ReadToEnd());
                    window.CloseScreen();
                    ReopenScreen(true);

                    MetaUI.ShowConfirmDialog(transform, GUI.API.LocalizedTextComponent("devStudio.ui.language.newFile.confirm"));
                }),
                (!exists || NebulaPlugin.GuardVanillaLangData) ? null : GUI.Instance.LocalizedButton(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.language.addMissing", _ => {
                    int num = 0;
                    string newText = "";
                    var myLang = new Language();
                    myLang.Deserialize((addon as IResourceAllocator).GetResource(Virial.Compat.IReadOnlyArray<string>.Empty(), fPath)?.AsStream());
                    using var defaultStream = Language.OpenDefaultLangStream();
                    HashSet<string> keys = new(myLang.translationMap.Keys);
                    Language.Deserialize(defaultStream, (key, text) => {
                        if (myLang.TryGetText(key, out var myText))
                            newText += "\"" + key + "\" : \"" + myText + "\"\n";
                        else
                        {
                            newText += "\"" + key + "\" : \"" + text + "\" @Added\n";
                            num++;
                        }

                        var detail = Language.FindFromDefault(key + ".detail");
                        if(detail == null && myLang.TryGetText(key + ".detail", out var myDetailText)) newText += "\"" + key + ".detail\" : \"" + myDetailText + "\"\n";

                        keys.Remove(key);
                        keys.Remove(key + ".detail");
                    }, comment => newText += comment + "\n");


                    if (keys.Count > 0)
                    {
                        newText += "\n# Others\n\n";
                        foreach (var key in keys) newText += "\"" + key + "\" : \"" + myLang.translationMap[key] + "\"\n";
                    }

                    addon.WriteFile(fPath, newText);

                    window.CloseScreen();
                    ReopenScreen(true);
                    
                    MetaUI.ShowConfirmDialog(transform, GUI.Instance.RawTextComponent(Language.Translate("devStudio.ui.language.addMissing.confirm").Replace("%NUM%",num.ToString())));
                }),
                !exists ? null : GUI.Instance.Button(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeAsset.StandardMediumMasked), GUI.API.TextComponent(Virial.Color.Red, "devStudio.ui.language.delete"), _ => {
                    addon.DeleteFile(fPath);
                    window.CloseScreen();
                    ReopenScreen(true);

                    MetaUI.ShowConfirmDialog(transform, GUI.API.LocalizedTextComponent("devStudio.ui.language.delete.confirm"));
                }, color : Virial.Color.Red)
            );

            window.SetWidget(holder, new Vector2(0.5f,1f), out _);

        }
        
        GUIWidget[] scroller = new GUIWidget[]{ 
            GUI.Instance.Margin(new(2.5f,null)),
            GUI.Instance.ScrollView(Virial.Media.GUIAlignment.Center, new Virial.Compat.Size(4.2f, 4.2f), "devStudioLang", GUI.Instance.VerticalHolder(Virial.Media.GUIAlignment.Center, Language.AllLanguageId().Select(l =>
            {
                string lName = Language.GetLanguage(l);
                bool exists = addon.ExistsFile("Language/" + lName + ".dat");

                return GUI.Instance.RawButton(Virial.Media.GUIAlignment.Center, GUI.Instance.GetAttribute(AttributeParams.StandardBoldNonFlexible),
                    Language.GetLanguageShownString(l), _ => OpenDetailWindow(l), color: (exists ? Virial.Color.White : new(0.5f, 0.5f, 0.5f, 1f)));
            })), out var artifact) };

        widgets.Add(GUI.Instance.HorizontalHolder(Virial.Media.GUIAlignment.Left, scroller));

        return (new WrappedWidget(GUI.Instance.VerticalHolder(Virial.Media.GUIAlignment.Center, widgets)), null, null);
    }

    //Document
    static SerializableDocument? migrated = null;
    (string id, Func<SerializableDocument> generator, Func<bool> predicate)[] documentContentVariations = {
                ("contents", ()=>new SerializableDocument(){ Contents = new() }, () => true),
                ("aligned", ()=>new SerializableDocument(){ Aligned = new() }, () => true),
                ("localizedText", ()=>new SerializableDocument(){ TranslationKey = "undefined", IsVariable = true }, () => true),
                ("rawText", ()=>new SerializableDocument(){ RawText = "Text", IsVariable = true }, () => true),
                ("image", ()=>new SerializableDocument(){ Image = "Nebula::NebulaImage", Width = 0.25f }, () => true),
                ("vertical", ()=>new SerializableDocument(){ VSpace = 0.5f }, () => true),
                ("horizontal", ()=>new SerializableDocument(){ HSpace = 0.5f }, () => true),
                ("documentRef", ()=>new SerializableDocument(){ Document = new(){ Id = "", Arguments = new() } }, () => true),
                ("citation", ()=>new SerializableDocument(){ Citation = "" }, () => true),
                ("paste", ()=>{ var result = migrated ?? new SerializableDocument(){ Contents = new() }; migrated = null; return result; }, () => migrated != null)
            };

    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowDocumentEditorScreen(DevAddon addon, string path, string id, SerializableDocument doc)
    {
        void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, doc.Serialize(), Encoding.UTF8);
        }

        void NewContentEditor(SerializableDocument targetContainer)
        {
            MetaWidgetOld widget = new();

            widget.Append(documentContentVariations.Where(v => v.predicate.Invoke()), (entry) =>
            new MetaWidgetOld.Button(() =>
            {
                targetContainer.AppendContent(entry.generator.Invoke());
                NebulaManager.Instance.HideHelpWidget();
                ReopenScreen(true);
            }, new(TextAttributeOld.BoldAttr) { Size = new(1.2f, 0.24f) })
            { TranslationKey = "devStudio.ui.document.element." + entry.id, Alignment = IMetaWidgetOld.AlignmentOption.Left },
            2, -1, 0, 0.52f, false, IMetaWidgetOld.AlignmentOption.Left);

            NebulaManager.Instance.SetHelpWidget(null, widget);
        }

        void ShowContentEditor(PassiveButton editorButton, SerializableDocument doc, SerializableDocument? parent)
        {
            MetaWidgetOld widget = new();

            MetaWidgetOld.Button GetButton(Action clickAction, string rawText, bool reopenScreen = true, bool useBoldFont = false, bool asMasked = false, Color? color = null)
            {
                var attr = new TextAttributeOld(useBoldFont ? TextAttributeOld.BoldAttr : TextAttributeOld.NormalAttr) { Size = new(0.2f, 0.2f) };
                if (asMasked) attr.FontMaterial = VanillaAsset.StandardMaskedFontMaterial;
                var button = new MetaWidgetOld.Button(() => { clickAction.Invoke(); if (reopenScreen) ReopenScreen(true); }, attr) { RawText = rawText, Color = color};
                if (asMasked) button.SetAsMaskedButton();
                return button;
            }

            MetaWidgetOld.VariableText GetRawTagContent(string rawText, bool asMasked = false) => new MetaWidgetOld.VariableText(asMasked ? new(TextAttributeOld.BoldAttrLeft) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial } : TextAttributeOld.BoldAttrLeft) { RawText = rawText };
            MetaWidgetOld.VariableText GetLocalizedTagContent(string translationKey) => new MetaWidgetOld.VariableText(TextAttributeOld.BoldAttrLeft) { TranslationKey = translationKey };


            List<IMetaParallelPlacableOld> buttons = new();
            void AppendMargin(bool wide = false) { if (buttons.Count > 0) buttons.Add(new MetaWidgetOld.HorizonalMargin(wide ? 0.35f : 0.2f)); }
            void AppendLocalizedButtonText(string translationKey) { buttons.Add(GetLocalizedTagContent(translationKey)); }
            void AppendRawButtonText(string rawText) { buttons.Add(GetRawTagContent(rawText)); }
            void AppendButton(Action clickAction, string rawText, bool reopenScreen = true, bool useBoldFont = false, bool highlighted = false) => buttons.Add(GetButton(clickAction, rawText, reopenScreen, useBoldFont, color: highlighted ? UnityEngine.Color.yellow : null));
            void AppendButtonTag(string translationKey)
            {
                AppendLocalizedButtonText(translationKey);
                buttons.Add(new MetaWidgetOld.HorizonalMargin(0.05f));
                AppendRawButtonText(":");
                buttons.Add(new MetaWidgetOld.HorizonalMargin(0.1f));
            }

            if (doc.Contents != null || doc.Aligned != null)
            {
                AppendMargin();
                AppendButton(() => NewContentEditor(doc), "+", false, true);
            }

            if (parent != null)
            {
                bool isVertical = parent.Contents != null;

                AppendMargin();
                AppendButton(() => parent.ReplaceContent(doc, true), isVertical ? "▲" : "◀", true, false);
                AppendButton(() => parent.ReplaceContent(doc, false), isVertical ? "▼" : "▶", true, false);

                AppendMargin(true);

                AppendButtonTag("devStudio.ui.document.editor.alignment");
                AppendButton(() => doc.Alignment = "Left", "←", true, true, doc.GetAlignment() == GUIAlignment.Left);
                AppendButton(() => doc.Alignment = "Right", "→", true, true, doc.GetAlignment() == GUIAlignment.Right);
                AppendButton(() => doc.Alignment = "Top", "↑", true, true, doc.GetAlignment() == GUIAlignment.Top);
                AppendButton(() => doc.Alignment = "Bottom", "↓", true, true, doc.GetAlignment() == GUIAlignment.Bottom);
                AppendButton(() => doc.Alignment = "Center", "・", true, true, doc.GetAlignment() == GUIAlignment.Center);

                AppendMargin(true);

                AppendButton(() => { migrated = doc; parent.RemoveContent(doc); NebulaManager.Instance.HideHelpWidget(); }, Language.Translate("devStudio.ui.document.editor.move"), true, true);
                
                AppendMargin(true);

                AppendButton(() => { parent.RemoveContent(doc); NebulaManager.Instance.HideHelpWidget(); }, "×".Color(Color.red), true, true);
            }

            widget.Append(new CombinedWidgetOld(buttons.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Left });

            MetaWidgetOld.TextInput GetTextFieldContent(bool isMultiline, float width, string defaultText, Action<string> updateAction, Predicate<char>? textPredicate,bool withMaskMaterial = false)
            {
                return new MetaWidgetOld.TextInput(isMultiline ? 7 : 1, isMultiline ? 1.2f : 1.8f, new(width, isMultiline ? 1.2f : 0.23f))
                {
                    DefaultText = isMultiline ? defaultText.Replace("<br>", "\r") : defaultText,
                    TextPredicate = textPredicate,
                    PostBuilder = field =>
                    {
                        field.LostFocusAction = (myInput) =>
                        {
                            if (isMultiline) myInput = myInput.Replace("\r", "<br>");
                            updateAction.Invoke(myInput);
                            ReopenScreen(true);
                        };
                    },
                    WithMaskMaterial = withMaskMaterial
                };
            }

            List<IMetaParallelPlacableOld> parallelPool = new();
            void AppendParallel(IMetaParallelPlacableOld content) => parallelPool.Add(content);
            void AppendParallelMargin(float margin) => AppendParallel(new MetaWidgetOld.HorizonalMargin(margin));
            void OutputParallelToWidget()
            {
                if (parallelPool.Count == 0) return;
                widget.Append(new CombinedWidgetOld(parallelPool.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Left });
                parallelPool.Clear();
            }

            void AppendTextField(bool isMultiline, float width, string defaultText, Action<string> updateAction, Predicate<char>? textPredicate)
            {
                widget.Append(GetTextFieldContent(isMultiline, width, defaultText, updateAction, textPredicate));
            }

            void AppendTopTag(string translateKey)
            {
                AppendParallel(GetLocalizedTagContent(translateKey));
                AppendParallelMargin(0.05f);
                AppendParallel(GetRawTagContent(":"));
                AppendParallelMargin(0.1f);
            }

           
            if (doc.RawText != null || doc.TranslationKey != null)
            {
                if (doc.RawText != null)
                    AppendTextField(true, 7.5f, doc.RawText, (input) => doc.RawText = input, TextField.JsonStringPredicate);
                else
                    AppendTextField(false, 3f, doc.TranslationKey, (input) => doc.TranslationKey = input, TextField.JsonStringPredicate);


                AppendParallel(MetaWidgetOld.StateButton.TopLabelCheckBox("devStudio.ui.document.editor.isBold", null, true, new Reference<bool>().Set(doc.IsBold ?? false), (val) => { doc.IsBold = val; ReopenScreen(true); }));
                AppendParallelMargin(0.2f);

                AppendTopTag("devStudio.ui.document.editor.fontSize");
                AppendParallel(GetTextFieldContent(false, 0.4f, doc.FontSize.ToString() ?? "", (input) => { if (float.TryParse(input, out var val)) doc.FontSize = val; else doc.FontSize = null; }, TextField.NumberPredicate));
                AppendParallelMargin(0.2f);

                AppendTopTag("devStudio.ui.document.editor.color");
                if (doc.Color == null || doc.Color.Style != null)
                {
                    AppendParallel(GetTextFieldContent(false, 1.8f, doc.Color?.Style ?? "", (input) =>
                    {
                        if (input.Length == 0) doc.Color = null;
                        else doc.Color = new() { Style = input };
                    }, TextField.IdPredicate));
                }
                else
                {
                    AppendParallel(GetRawTagContent("R"));
                    AppendParallel(GetTextFieldContent(false, 0.4f, doc.Color.R?.ToString() ?? "255", (input) => { if (byte.TryParse(input, out var val)) doc.Color.R = val; }, TextField.IntegerPredicate));

                    AppendParallel(GetRawTagContent("G"));
                    AppendParallel(GetTextFieldContent(false, 0.4f, doc.Color.G?.ToString() ?? "255", (input) => { if (byte.TryParse(input, out var val)) doc.Color.G = val; }, TextField.IntegerPredicate));

                    AppendParallel(GetRawTagContent("B"));
                    AppendParallel(GetTextFieldContent(false, 0.4f, doc.Color.B?.ToString() ?? "255", (input) => { if (byte.TryParse(input, out var val)) doc.Color.B = val; }, TextField.IntegerPredicate));
                }

                OutputParallelToWidget();
            }
            else if (doc.Image != null)
            {
                AppendTopTag("devStudio.ui.document.editor.image");
                AppendParallel(GetTextFieldContent(false, 3.2f, doc.Image, (input) =>
                {
                    doc.Image = input;
                }, TextField.NameSpacePredicate));

                AppendParallelMargin(0.2f);


                AppendTopTag("devStudio.ui.document.editor.width");
                AppendParallel(GetTextFieldContent(false, 0.8f, doc.Width?.ToString() ?? "0.25", (input) =>
                {
                    if (float.TryParse(input, out var val)) doc.Width = val;
                }, TextField.NumberPredicate));

                OutputParallelToWidget();
            }
            else if (doc.HSpace != null || doc.VSpace != null)
            {
                bool isHorizontal = doc.HSpace != null;
                AppendTopTag("devStudio.ui.document.editor." + (isHorizontal ? "width" : "height"));
                AppendParallel(GetTextFieldContent(false, 0.8f, (isHorizontal ? doc.HSpace : doc.VSpace)?.ToString() ?? "0.5", (input) =>
                {
                    if (float.TryParse(input, out var val))
                    {
                        if (isHorizontal) doc.HSpace = val;
                        else doc.VSpace = val;
                    }
                }, TextField.NumberPredicate));

                OutputParallelToWidget();
            }else if(doc.Document != null)
            {
                Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();
                void UpdateInner()
                {
                    if (innerRef.Value == null) return;
                    if (!innerRef.Value.IsValid) return;

                    MetaWidgetOld inner = new();
                    foreach (var arg in doc.Document!.Arguments!)
                    {
                        inner.Append(new CombinedWidgetOld(
                            GetTextFieldContent(false, 1.4f, arg.Key, (input) =>
                            {
                                if (arg.Key != input)
                                {
                                    doc.Document.Arguments.Remove(arg.Key);
                                    doc.Document.Arguments[input] = arg.Value;
                                    NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                                }
                            }, TextField.IdPredicate, true),
                            new MetaWidgetOld.HorizonalMargin(0.1f),
                            GetRawTagContent(":"),
                            new MetaWidgetOld.HorizonalMargin(0.1f),
                            GetTextFieldContent(false, 3.1f, arg.Value, (input) =>
                            {
                                if (arg.Value != input)
                                {
                                    doc.Document.Arguments[arg.Key] = input;
                                    NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                                }
                            }, TextField.JsonStringPredicate, true),
                            new MetaWidgetOld.HorizonalMargin(0.1f),
                            GetButton(() => {
                                doc.Document.Arguments.Remove(arg.Key);
                                NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                            }, "×".Color(Color.red), true, true, true)
                            )
                        { Alignment = IMetaWidgetOld.AlignmentOption.Left });
                    }

                    try
                    {
                        innerRef.Value!.SetWidget(inner);
                    }
                    catch { }
                }

                AppendTopTag("devStudio.ui.document.editor.document");
                AppendParallel(GetTextFieldContent(false, 2.6f, doc.Document.Id.ToString() ?? "", (input) =>
                {
                    doc.Document.Id = input;
                    var refDoc = DocumentManager.GetDocument(input) as SerializableDocument;
                    if(refDoc?.Arguments != null)
                    {
                        foreach (var entry in doc.Document.Arguments) if (!refDoc!.Arguments!.Contains(entry.Key)) doc.Document.Arguments.Remove(entry.Key);
                        foreach (var arg in refDoc!.Arguments!) if (!doc.Document.Arguments.ContainsKey(arg)) doc.Document.Arguments[arg] = "";
                        NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                    }
                }, TextField.IdPredicate));
                OutputParallelToWidget();
                AppendParallel(GetButton(() => {
                    int index = 0;
                    while (true) {
                        string str = "argument" + (index == 0 ? "" : index.ToString());
                        if (!doc.Document!.Arguments!.ContainsKey(str))
                        {
                            doc.Document.Arguments[str] = "";
                            break;
                        }
                        index++;
                        continue;
                    }
                    NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                }, "+", false, true));
                OutputParallelToWidget();

                widget.Append(new MetaWidgetOld.ScrollView(new Vector2(6.1f, 2.6f), new MetaWidgetOld(), true) { Alignment = IMetaWidgetOld.AlignmentOption.Left, InnerRef = innerRef, PostBuilder = UpdateInner });
            }
            else if (doc.Citation != null)
            {
                AppendTopTag("devStudio.ui.document.editor.citation");
                AppendParallel(GetTextFieldContent(false, 3.2f, doc.Citation ?? "", (input) =>
                {
                    doc.Citation = input;
                }, TextField.JsonStringPredicate));

                OutputParallelToWidget();
            }

            NebulaManager.Instance.SetHelpWidget(editorButton, widget);
            TextField.EditFirstField();
        }

        void BuildContentEditor(PassiveButton editorButton, SerializableDocument doc, SerializableDocument? parent)
        {
            editorButton.OnMouseOver.AddListener(() =>
            {
                if ((!NebulaManager.Instance.ShowingAnyHelpContent)) ShowContentEditor(editorButton, doc, parent);
            });
            editorButton.OnMouseOut.AddListener(() =>
            {
                if (NebulaManager.Instance.HelpRelatedObject == editorButton) NebulaManager.Instance.HideHelpWidget();
            });
            editorButton.OnClick.AddListener(() =>
            {
                if (NebulaManager.Instance.HelpRelatedObject != editorButton)
                    ShowContentEditor(editorButton, doc, parent);
                NebulaManager.Instance.HelpIrrelevantize();
            });

        }

        MetaWidgetOld widget = new();
        widget.Append(
            new CombinedWidgetOld(
                new MetaWidgetOld.Text(new Utilities.TextAttributeOld(Utilities.TextAttributeOld.TitleAttr) { Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(2.7f)) { RawText = id, Alignment = IMetaWidgetOld.AlignmentOption.Left },
                new MetaWidgetOld.Button(() => { NebulaManager.Instance.HideHelpWidget(); Save(); }, Utilities.TextAttributeOld.BoldAttr) { TranslationKey = "devStudio.ui.common.save" },
                new MetaWidgetOld.Button(() =>
                {
                    NebulaManager.Instance.HideHelpWidget();
                    var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), transform, UnityEngine.Vector3.zero, true, true);

                    Virial.Compat.Artifact<GUIScreen>? inner = null;
                    var scrollView = new GUIScrollView(Virial.Media.GUIAlignment.Left, new(7f, 4.5f), () => doc.Build(inner, nameSpace: addon) ?? GUIEmptyWidget.Default);
                    inner = scrollView.Artifact;
                    screen.SetWidget(scrollView, new Vector2(0f, 1f), out _);
                }, TextAttributeOld.BoldAttr)
                { TranslationKey = "devStudio.ui.common.preview" }
            )
            { Alignment = IMetaWidgetOld.AlignmentOption.Left }
        );

        widget.Append(new MetaWidgetOld.VerticalMargin(0.1f));

        widget.Append(new WrappedWidget(new GUIScrollView(Virial.Media.GUIAlignment.Left, new(ScreenWidth - 0.4f, 4.65f), () => doc.BuildForDev(BuildContentEditor) ?? GUIEmptyWidget.Default) { ScrollerTag = "DocumentEditor" }));

        return (widget, null, () =>
        {
            NebulaManager.Instance.HideHelpWidget();
            MetaUI.ShowYesOrNoDialog(transform, () => { Save(); CloseScreen(true); }, () => { CloseScreen(true); }, Language.Translate("devStudio.ui.common.confirmSaving"), true);
            return false;
        }
        );
    }

    //Documents
    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowDocumentScreen(DevAddon addon)
    {
        void CheckAndGenerateDocument(Il2CppArgument<MetaScreen> editScreen, Il2CppArgument<TextField> id, string? originalId = null)
        {
            if (id.Value.Text.Length < 1)
            {
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.requiredText").Color(Color.red * 0.7f));
                return;
            }

            if (addon.ExistsFile("Documents/" + id.Value.Text + ".json"))
            {
                id.Value.SetText("");
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.duplicatedId").Color(Color.red * 0.7f));
                return;
            }

            MetaWidgetOld.ScrollView.RemoveDistHistory("DocumentEditor");
            editScreen.Value.CloseScreen();
            SerializableDocument? doc = null;
            if(originalId != null)
                doc = JsonStructure.Deserialize<SerializableDocument>(File.ReadAllText(addon.FolderPath + "/Documents/" + originalId + ".json"));
            doc ??= new SerializableDocument() { Contents = new() };
            doc.RelatedNamespace = addon;
            OpenScreen(() => ShowDocumentEditorScreen(addon, addon.FolderPath + "/Documents/" + id.Value.Text + ".json", id.Value.Text, doc));
        }

        MetaWidgetOld widget = new();

        widget.Append(new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.addon.document" });
        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        (string path, string id, SerializableDocument doc)[]? docs = null;

        void OpenGenerateWindow(string? original = null)
        {
            var screen = MetaScreen.GenerateWindow(new(5.9f, original != null ? 1.8f : 1.5f), transform, Vector3.zero, true, false);
            MetaWidgetOld widget = new();

            if (original != null) widget.Append(new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(1.5f, 0.3f) }) { RawText = Language.Translate("devStudio,ui.common.original") + " : " + original });

            Reference<TextField> refId = new();
            TMPro.TextMeshPro usingInfoText = null!;


            widget.Append(new CombinedWidgetOld(
               new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "ID" },
               new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
               new MetaWidgetOld.TextInput(1, 2f, new(3.7f, 0.3f)) { TextFieldRef = refId, TextPredicate = TextField.IdPredicate }
                   ));
            widget.Append(new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(5.6f, 0.14f) }.EditFontSize(1.2f, 0.6f, 1.2f)) { PostBuilder = t => usingInfoText = t });
            widget.Append(new MetaWidgetOld.VerticalMargin(0.16f));
            widget.Append(new MetaWidgetOld.Button(() =>
            {
                CheckAndGenerateDocument(screen, refId.Value!,original);
            }, new(TextAttributeOld.BoldAttr) { Size = new(1.8f, 0.3f) })
            { TranslationKey = original != null ? "devStudio.ui.common.clone" : "devStudio.ui.common.generate", Alignment = IMetaWidgetOld.AlignmentOption.Center });

            screen.SetWidget(widget);
            TextField.EditFirstField();

            usingInfoText.fontStyle |= TMPro.FontStyles.Italic;
            refId.Value!.UpdateAction = (id) =>
            {
                if (docs?.Any(entry => entry.id == id) ?? false)
                {
                    usingInfoText.color = Color.red;
                    usingInfoText.text = Language.Translate("devStudio.ui.document.isDuplicatedId");
                }
                else if (DocumentManager.GetAllUsingId().Any(str => str == id))
                {
                    usingInfoText.color = Color.green;
                    usingInfoText.text = Language.Translate("devStudio.ui.document.isUsingId");
                }
                else
                {
                    usingInfoText.text = "";
                }
            };
        }

        //Add Button
        widget.Append(new MetaWidgetOld.Button(() =>
        {
            OpenGenerateWindow();
        }, new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(0.34f, 0.18f) }.EditFontSize(2.4f))
        { RawText = "+" });

        //Scroller
        Reference<MetaWidgetOld.ScrollView.InnerScreen> inner = new();
        widget.Append(new MetaWidgetOld.ScrollView(new(ScreenWidth, 4f), inner) { Alignment = IMetaWidgetOld.AlignmentOption.Center });

        //Shower
        IEnumerator CoShowDocument()
        {
            yield return inner.Wait();
            inner.Value?.SetLoadingWidget();

            var task = addon.LoadDocumentsAsync();
            yield return task.WaitAsCoroutine();

            MetaWidgetOld widget = new();
            docs = task.Result;
            foreach (var entry in docs)
            {
                widget.Append(new CombinedWidgetOld(
                    new MetaWidgetOld.Text(new(TextAttributeOld.NormalAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial,  Alignment = TMPro.TextAlignmentOptions.Left, Size = new(3f, 0.27f) }) { RawText = entry.id },
                    new MetaWidgetOld.Button(() =>
                    {
                        MetaWidgetOld.ScrollView.RemoveDistHistory("DocumentEditor");
                        var doc = JsonStructure.Deserialize<SerializableDocument>(File.ReadAllText(entry.path));

                        if (doc != null)
                        {
                            doc.RelatedNamespace = addon;
                            OpenScreen(() => ShowDocumentEditorScreen(addon, entry.path, entry.id, doc));
                        }
                    }, new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f) })
                    { TranslationKey = "devStudio.ui.common.edit" }.SetAsMaskedButton(),
                    new MetaWidgetOld.HorizonalMargin(0.2f),
                    new MetaWidgetOld.Button(() =>
                    {
                        MetaUI.ShowYesOrNoDialog(transform, () => { File.Delete(entry.path); ReopenScreen(true); }, () => { }, Language.Translate("devStudio.ui.common.confirmDeleting") + $"<br>\"{entry.id}\"");
                    }, new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f)  })
                    { Text = NebulaGUIWidgetEngine.Instance.TextComponent(Color.red, "devStudio.ui.common.delete") }.SetAsMaskedButton(),
                    new MetaWidgetOld.HorizonalMargin(0.2f),
                    new MetaWidgetOld.Button(() =>
                    {
                        OpenGenerateWindow(entry.id);
                    }, new(TextAttributeOld.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f) })
                    { Text = NebulaGUIWidgetEngine.Instance.TextComponent(Color.white, "devStudio.ui.common.clone") }.SetAsMaskedButton()
                    )
                { Alignment = IMetaWidgetOld.AlignmentOption.Left });
            }

            inner.Value?.SetWidget(widget);
        }

        return (widget, () => StartCoroutine(CoShowDocument().WrapToIl2Cpp()), null);
    }


    (IMetaWidgetOld widget, Reference<PlayerDisplay> player) GetPlayerDisplayWidget()
    {
        MetaWidgetOld widget = new();
        Reference<PlayerDisplay> display = new();

        widget.Append(new MetaWidgetOld.CustomWidget(new Vector2(1.5f,3.5f),IMetaWidgetOld.AlignmentOption.Center,
            (parent,center) => {
                display.Set(VanillaAsset.GetPlayerDisplay());
                display.Value!.transform.SetParent(parent);
                display.Value!.transform.localPosition = (Vector3)center + new Vector3(0, 0, 1f);
                display.Value!.transform.localScale = new(1.75f, 1.75f, 1f);

                myScreen.GetComponent<SortingGroup>().enabled = false;
            }));

        var allStateButtons = new (string translationKey, Action action)[]
        {
            ("idle", ()=> display.Value!.Animations.PlayIdleAnimation()),
            ("run", ()=> display.Value!.Animations.PlayRunAnimation()),
            ("climbUp", ()=> display.Value!.Animations.PlayClimbAnimation(false)),
            ("climbDown", ()=> display.Value!.Animations.PlayClimbAnimation(true)),
            ("enterVent", ()=> display.Value!.StartCoroutine(display.Value!.Animations.CoPlayEnterVentAnimation(0))),
            ("exitVent", ()=> display.Value!.StartCoroutine(Effects.Sequence(display.Value!.Animations.CoPlayExitVentAnimation(), ManagedEffects.Action(()=>display.Value!.Animations.PlayIdleAnimation()).WrapToIl2Cpp()))),
            ("jump", ()=> display.Value!.StartCoroutine(display.Value!.Animations.CoPlayJumpAnimation())),
        };

        widget.Append(allStateButtons, state => new MetaWidgetOld.Button(state.action, new(TextAttributeOld.BoldAttr) { Size = new(1.4f, 0.18f) }) { TranslationKey = "devStudio.ui.cosmetics.anim." + state.translationKey }, 2, -1, 0, 0.44f);
        widget.Append(new CombinedWidgetOld(
            new MetaWidgetOld.StateButton() { OnChanged = (flag) => display.Value!.Cosmetics.SetFlipX(flag) },
            new MetaWidgetOld.HorizonalMargin(0.15f),
            new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttrLeft) { Size = new(0.8f, 0.12f) }) { TranslationKey = "devStudio.ui.cosmetics.anim.flip" }
            ));

        return (widget, display);
    }


    //Cosmetics
    private static readonly string[][] ImageContentTranslationKey = {
        new string[]{ "devStudio.ui.cosmetics.contents.main", "devStudio.ui.cosmetics.contents.climbUp", "devStudio.ui.cosmetics.contents.image" },
        new string[]{ "devStudio.ui.cosmetics.contents.back", "devStudio.ui.cosmetics.contents.climbUpBack" },
        new string[]{ "devStudio.ui.cosmetics.contents.flipped", "devStudio.ui.cosmetics.contents.climbDown" },
        new string[]{ "devStudio.ui.cosmetics.contents.backFlipped", "devStudio.ui.cosmetics.contents.climbDownBack" },
    };

    private static MetaWidgetOld.Button GetCostumeContentButton<Costume>(Costume costume, string translationKey, string fieldName,DevAddon addon, Reference<TextField> costumeNameRef,Action? updateAction) where Costume : CustomCosmicItem {
        Color disabledColor = Color.gray.RGBMultiplied(0.48f);

        MetaWidgetOld.Button? myButton = null;
        SpriteRenderer? myRenderer = null;
        myButton = new MetaWidgetOld.Button(() =>
        {
            if (costumeNameRef.Value!.Text.Length == 0)
            {
                costumeNameRef.Value.SetHint(Language.Translate("devStudio.ui.cosmetics.hint.needToSetTitle").Color(Color.red.AlphaMultiplied(0.8f)));
                return;
            }
            DragAndDropBehaviour.Show(null, (path) =>
            {
                if (path.Count != 1 || !path[0].EndsWith(".png")) return;

                string destPath = addon.FolderPath + "/MoreCosmic";
                Directory.CreateDirectory(destPath);
                destPath += "/" + costume.Category;
                Directory.CreateDirectory(destPath);
                string title = costumeNameRef.Value!.Text.ToByteString();
                if (title.Length == 0) title = "Undefined";
                destPath += "/" + title + "_" + fieldName + ".png";

                void Copy()
                {
                    try
                    {
                        File.Copy(path[0], destPath, true);

                        var image = new CosmicImage() { Address = costumeNameRef.Value!.Text.ToByteString() + "_" + fieldName + ".png" };
                        costume.GetType().GetField(fieldName)?.SetValue(costume, image);
                        costume.MyBundle = addon.MyBundle!;
                        new StackfullCoroutine(costume.Activate(false)).Wait();
                        updateAction?.Invoke();
                        myButton!.Color = myRenderer!.color = Color.white;

                        using Stream hashStream = File.OpenRead(destPath);
                        image.Hash = CosmicImage.ComputeImageHash(hashStream);
                        hashStream.Close();
                    }
                    catch (Exception e)
                    {
                        NebulaPlugin.Log.Print(NebulaLog.LogLevel.Error, e.ToString());
                        MetaUI.ShowConfirmDialog(null, new TranslateTextComponent("devStudio.ui.cosmetics.failedToCopy"));
                    }
                }

                if (File.Exists(destPath))
                {
                    MetaUI.ShowYesOrNoDialog(null, Copy, () => { }, Language.Translate("devStudio.ui.cosmetics.confirmOverwrite"), false);
                }
                else
                {
                    Copy();
                }

            });
        }, new(TextAttributeOld.BoldAttr) { Size = new(0.85f, 0.23f) })
        { Color = costume.GetType().GetField(fieldName)!.GetValue(costume) == null ? disabledColor : Color.white, TranslationKey = translationKey, Alignment = IMetaWidgetOld.AlignmentOption.Center, PostBuilder = (_, renderer, _) => myRenderer = renderer };
        return myButton;
    }

    static private CombinedWidgetOld GetTextInputWidget(string translationKey, string hint, Reference<TextField> textRef, string defaultText, Action<string> onEntered)
    {
        return new CombinedWidgetOld(
        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(1f, 0.4f) }) { TranslationKey = translationKey },
        new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.4f) }) { RawText = ":" },
        new MetaWidgetOld.HorizonalMargin(0.15f),
        new MetaWidgetOld.TextInput(1, 1.8f, new(3.3f, 0.28f))
        {
            TextFieldRef = textRef,
            Hint = hint.Color(Color.gray),
            TextPredicate = TextField.JsonStringPredicate,
            DefaultText = defaultText,
            PostBuilder = (field) => field.LostFocusAction += onEntered
        })
        { Alignment = IMetaWidgetOld.AlignmentOption.Left };
    }

    private void SetUpCommonCosmicProperty(MetaWidgetOld widget,DevAddon addon, CustomCosmicItem costume, Reference<TextField> titleRef, Reference<TextField> authorRef)
    {
        TextMeshPro myText = null!;
        widget.Append(GetTextInputWidget("devStudio.ui.cosmetics.attributes.name", "Title", titleRef, costume.UnescapedName, (text) => costume.Name = CustomCosmicItem.GetEscapedString(text)));
        widget.Append(GetTextInputWidget("devStudio.ui.cosmetics.attributes.author", "Author", authorRef, costume.UnescapedAuthor, (text) => costume.Author = CustomCosmicItem.GetEscapedString(text)));
        widget.Append(new CombinedWidgetOld(
            new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(1f, 0.4f) }) { TranslationKey = "devStudio.ui.cosmetics.attributes.package" },
            new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.4f) }) { RawText = ":" },
            new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(2.5f, 0.4f) }) { RawText = costume.Package, PostBuilder = text=>myText=text },
            new MetaWidgetOld.HorizonalMargin(0.2f),
            new MetaWidgetOld.Button(() => {
                var window = MetaScreen.GenerateWindow(new(3.3f, 2.4f), null, Vector3.zero, true, false);
                MetaWidgetOld widget = new();
                IEnumerable<CosmicPackage> packages = MoreCosmic.AllPackages.Values;
                if (addon.MyBundle?.Packages != null) packages = packages.Concat(addon.MyBundle!.Packages.Where(package => !MoreCosmic.AllPackages.ContainsKey(package.Package)));
                foreach(var package in packages)
                {
                    widget.Append(new MetaWidgetOld.Button(() =>
                    {
                        myText.text = package.DisplayName;
                        costume.Package = package.Package;
                        window.CloseScreen();
                    }, new(TextAttributeOld.BoldAttr) { Size = new(2.4f, 0.32f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    { RawText = package.DisplayName, Alignment = IMetaWidgetOld.AlignmentOption.Left }.SetAsMaskedButton());
                }
                window.SetWidget(new MetaWidgetOld.ScrollView(new(3.2f, 2.2f), widget));
            },new TextAttributeOld(TextAttributeOld.BoldAttr) { Size = new(0.65f,0.3f)}) { TranslationKey = "devStudio.ui.common.edit" }
            )
        { Alignment = IMetaWidgetOld.AlignmentOption.Left });

        widget.Append(new MetaWidgetOld.VerticalMargin(0.12f));
    }

    

    private void SetUpCosmicContentProperty<Costume>(MetaWidgetOld widget,Costume costume, DevAddon addon, Reference<TextField> costumeNameRef, Action? updateAction, (string translationKey, string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents) where Costume : CustomCosmicItem
    {
        widget.Append(contents.Where(c => c.variation == -1), c => {
            return new CombinedWidgetOld(
                new MetaWidgetOld.HorizonalMargin(0.16f),
                new MetaWidgetOld.StateButton()
                {
                    StateRef = new Reference<bool>().Set((bool)(typeof(Costume).GetField(c.fieldName)?.GetValue(costume) ?? false)),
                    OnChanged = flag => {
                        typeof(Costume).GetField(c.fieldName)?.SetValue(costume, flag);
                        updateAction?.Invoke();
                    }
                },
                new MetaWidgetOld.HorizonalMargin(0.08f),
                new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Size = new(0.8f, 0.2f) }) { TranslationKey = c.translationKey }
                );
        }, 3, -1, 0, 0.3f);

        widget.Append(new MetaWidgetOld.VerticalMargin(0.12f));

        foreach (var content in contents)
        {
            if (content.variation == -1) continue;
            List<IMetaParallelPlacableOld> buttons = new();
            buttons.Add(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(1f, 0.3f) }) { Alignment = IMetaWidgetOld.AlignmentOption.Right, TranslationKey = content.translationKey });
            buttons.Add(new MetaWidgetOld.Text(new(TextAttributeOld.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.45f) }) { RawText = ":" });
            buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[0][content.variation], content.fieldName, addon, costumeNameRef, updateAction));
            if (content.flipName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[1][content.variation], content.flipName, addon, costumeNameRef, updateAction));
            if (content.backName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[2][content.variation], content.backName, addon, costumeNameRef, updateAction));
            if (content.backFlipName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[3][content.variation], content.backFlipName, addon, costumeNameRef, updateAction));

            widget.Append(new CombinedWidgetOld(buttons.ToArray()) { Alignment = IMetaWidgetOld.AlignmentOption.Left });
        }
    }

    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowNameplateEditorScreen(DevAddon addon, CosmicNameplate nameplate, params (string translationKey, string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents)
    {
        MetaWidgetOld widget = new();

        var widgets = widget.Split(0.35f, 0.1f, 0.55f);

        SpriteRenderer? myRenderer = null, myAdaptive = null;
        widgets[0].Append(new MetaWidgetOld.VerticalMargin(0.9f));
        widgets[0].Append(new MetaWidgetOld.CustomWidget(new(2f, 4f), IMetaWidgetOld.AlignmentOption.Center, (transform, center) => {
            myRenderer = UnityHelper.CreateObject<SpriteRenderer>("Nameplate", transform, center);
            myRenderer.sprite = nameplate.Plate?.GetSprite(0);

            myAdaptive = UnityHelper.CreateObject<SpriteRenderer>("NameplateAdaptive", myRenderer.transform, new(0f, 0f, nameplate.AdaptiveInFront ? -1f : 1f));
            myAdaptive.sprite = nameplate.Adaptive?.GetSprite(0);
            myAdaptive.sharedMaterial = HatManager.Instance.PlayerMaterial;
            PlayerMaterial.SetColors(NebulaPlayerTab.PreviewColorId, myAdaptive);
        }));

        void UpdateNameplate()
        {
            myRenderer!.sprite = nameplate.Plate?.GetSprite(0);
            myAdaptive!.sprite = nameplate.Adaptive?.GetSprite(0);
            myAdaptive.transform.localPosition = new(0f, 0f, nameplate.AdaptiveInFront ? -1f : 1f);
        }

        Reference<TextField> titleRef = new(), authorRef = new();
        SetUpCommonCosmicProperty(widgets[2], addon, nameplate, titleRef, authorRef);
        SetUpCosmicContentProperty(widgets[2], nameplate, addon, titleRef, UpdateNameplate, contents);

        return (widget, null, null);
    }

    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowCostumeEditorScreen<Costume>(DevAddon addon, Costume costume,params (string translationKey,string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents) where Costume : CustomCosmicItem
    {
        MetaWidgetOld widget = new();

        var widgets = widget.Split(0.35f, 0.1f, 0.55f);

        (var displayWidget, var displayRef) = GetPlayerDisplayWidget();

        void UpdateCostume()
        {
            if (costume is CosmicHat hat)
            {
                hat.MyHat.NoBounce = !hat.Bounce;
                hat.MyView.MatchPlayerColor = hat.Adaptive;

                hat.MyHat.ProductId = MoreCosmic.DebugProductId;
                displayRef?.Value?.Cosmetics.SetHat(hat.MyHat, NebulaPlayerTab.PreviewColorId);
            }
            else if (costume is CosmicVisor visor)
            {
                visor.MyView.MatchPlayerColor = visor.Adaptive;

                visor.MyVisor.ProductId = MoreCosmic.DebugProductId;
                displayRef?.Value?.Cosmetics.SetVisor(visor.MyVisor, NebulaPlayerTab.PreviewColorId);
            }
        }

        widgets[0].Append(displayWidget);

        Reference<TextField> titleRef = new(), authorRef = new();
        SetUpCommonCosmicProperty(widgets[2], addon, costume, titleRef, authorRef);
        SetUpCosmicContentProperty(widgets[2], costume, addon,titleRef, UpdateCostume, contents);

        return (widget, () => {
            var display = displayRef.Value;
            display!.Cosmetics.SetColor(NebulaPlayerTab.PreviewColorId);

            if (display == null) return;

            if (costume is CosmicHat hat)
            {
                MoreCosmic.RegisterDebugHat(hat);
                display!.Cosmetics.SetHat(hat.MyHat, NebulaPlayerTab.PreviewColorId);
            }else if (costume is CosmicVisor visor)
            {
                MoreCosmic.RegisterDebugVisor(visor);
                display!.Cosmetics.SetVisor(visor.MyVisor, NebulaPlayerTab.PreviewColorId);
            }

            var holder = UnityHelper.CreateObject("Holder", myScreen.transform, Vector3.zero);
            myScreen.gameObject.ForEachChild((Il2CppSystem.Action<GameObject>)(obj => {
                if (obj.name == "CombinedScreen") obj.transform.SetParent(holder.transform);
            }));
            holder.AddComponent<SortingGroup>();

        }, null);
    }

    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowPackageEditorScreen(DevAddon addon, CosmicPackage package)
    {
        MetaWidgetOld widget = new();

        Reference<TextField> titleRef = new(), keyRef = new();

        CombinedWidgetOld Centeralize(CombinedWidgetOld widget)
        {
            widget.Alignment = IMetaWidgetOld.AlignmentOption.Center;
            return widget;
        }

        widget.Append(new MetaWidgetOld.VerticalMargin(1.2f));
        widget.Append(Centeralize(GetTextInputWidget("devStudio.ui.cosmetics.attributes.id", "ID", titleRef, package.Package, (text) => package.Package = text)));
        widget.Append(Centeralize(GetTextInputWidget("devStudio.ui.cosmetics.attributes.format", "Format", keyRef, package.Format, (text) => package.Format = text)));

        return (widget, null, null);
    }

    //Cosmetics
    private (IMetaWidgetOld widget, Action? postAction, Func<bool>? confirm) ShowCosmeticsScreen(DevAddon addon)
    {
        if (addon.MyBundle == null)
        {
            Directory.CreateDirectory(addon.FolderPath + "/MoreCosmic");

            using Stream? stream = addon.OpenStream("MoreCosmic/Contents.json");

            if (stream != null)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                string json = reader.ReadToEnd();
                addon.MyBundle = (CustomItemBundle?)JsonStructure.Deserialize(json, typeof(CustomItemBundle));
                addon.MyBundle!.RelatedLocalAddress = addon.FolderPath + "/MoreCosmic/";

                new StackfullCoroutine(addon.MyBundle.Activate(false)).Wait();
            }

            if(addon.MyBundle == null)
            {
                addon.MyBundle = new CustomItemBundle();
                addon.MyBundle.RelatedLocalAddress = addon.FolderPath + "/MoreCosmic/";
                new StackfullCoroutine(addon.MyBundle.Activate(false)).Wait();
            }
        }
        

        MetaWidgetOld widget = new();

        widget.Append(new MetaWidgetOld.Text(new TextAttributeOld(TextAttributeOld.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.addon.cosmetics" });
        widget.Append(new MetaWidgetOld.VerticalMargin(0.2f));

        Reference<MetaWidgetOld.ScrollView.InnerScreen> innerRef = new();

        void GenerateSprite(Sprite? mainSprite, Sprite? backSprite, Transform parent, bool adaptive,float scale = 0.3f,float position = -1.15f)
        {
            if (mainSprite != null)
            {
                var mainRenderer = UnityHelper.CreateObject<SpriteRenderer>("Main", parent, new Vector3(position, 0f, -1f));
                mainRenderer.transform.localScale = new Vector3(scale,scale,1f);
                mainRenderer.sprite = mainSprite;
                mainRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                if (adaptive)
                {
                    mainRenderer.material = HatManager.Instance.MaskedPlayerMaterial;
                    PlayerMaterial.SetColors(NebulaPlayerTab.PreviewColorId, mainRenderer);
                }
            }

            if(backSprite != null)
            {
                var backRenderer = UnityHelper.CreateObject<SpriteRenderer>("Back", parent, new Vector3(position, 0f, -0.9f));
                backRenderer.transform.localScale = new Vector3(scale, scale, 1f);
                backRenderer.sprite = backSprite;
                backRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                if (adaptive)
                {
                    backRenderer.material = HatManager.Instance.MaskedPlayerMaterial;
                    PlayerMaterial.SetColors(NebulaPlayerTab.PreviewColorId, backRenderer);
                }
            }
        }

        void OpenHatEditor(CosmicHat hat) => OpenScreen(() => ShowCostumeEditorScreen(addon, hat,
            ("devStudio.ui.cosmetics.attributes.adaptive", nameof(CosmicHat.Adaptive), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.bounce", nameof(CosmicHat.Bounce), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.hideHands", nameof(CosmicHat.HideHands), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.isSkinny", nameof(CosmicHat.IsSkinny), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.idle", nameof(CosmicHat.Main), nameof(CosmicHat.Back), nameof(CosmicHat.Flip), nameof(CosmicHat.BackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.move", nameof(CosmicHat.Move), nameof(CosmicHat.MoveBack), nameof(CosmicHat.MoveFlip), nameof(CosmicHat.MoveBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.climb", nameof(CosmicHat.Climb), nameof(CosmicHat.ClimbFlip), nameof(CosmicHat.ClimbDown), nameof(CosmicHat.ClimbDownFlip), 1),
            ("devStudio.ui.cosmetics.attributes.enterVent", nameof(CosmicHat.EnterVent), nameof(CosmicHat.EnterVentBack), nameof(CosmicHat.EnterVentFlip), nameof(CosmicHat.EnterVentBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.exitVent", nameof(CosmicHat.ExitVent), nameof(CosmicHat.ExitVentBack), nameof(CosmicHat.ExitVentFlip), nameof(CosmicHat.ExitVentBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.preview", nameof(CosmicHat.Preview), null, null, null, 0)
            ));
        void OpenVisorEditor(CosmicVisor visor) => OpenScreen(() => ShowCostumeEditorScreen(addon, visor,
            ("devStudio.ui.cosmetics.attributes.adaptive", nameof(CosmicVisor.Adaptive), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.behindHat", nameof(CosmicVisor.BehindHat), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.backmostBack", nameof(CosmicVisor.BackmostBack), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.idle", nameof(CosmicVisor.Main), nameof(CosmicVisor.Back), nameof(CosmicVisor.Flip), nameof(CosmicVisor.BackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.move", nameof(CosmicVisor.Move), nameof(CosmicVisor.MoveBack), nameof(CosmicVisor.MoveFlip), nameof(CosmicVisor.MoveBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.climb", nameof(CosmicVisor.Climb), nameof(CosmicVisor.ClimbFlip), nameof(CosmicVisor.ClimbDown), nameof(CosmicVisor.ClimbDownFlip), 1),
            ("devStudio.ui.cosmetics.attributes.enterVent", nameof(CosmicVisor.EnterVent), nameof(CosmicVisor.EnterVentBack), nameof(CosmicVisor.EnterVentFlip), nameof(CosmicVisor.EnterVentBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.exitVent", nameof(CosmicVisor.ExitVent), nameof(CosmicVisor.ExitVentBack), nameof(CosmicVisor.ExitVentFlip), nameof(CosmicVisor.ExitVentBackFlip), 0),
            ("devStudio.ui.cosmetics.attributes.preview", nameof(CosmicVisor.Preview), null, null, null, 0)
            ));
        void OpenNameplateEditor(CosmicNameplate nameplate) => OpenScreen(() => ShowNameplateEditorScreen(addon, nameplate,
            ("devStudio.ui.cosmetics.attributes.adaptiveInFront", nameof(CosmicNameplate.AdaptiveInFront), null, null, null, -1),
            ("devStudio.ui.cosmetics.attributes.image", nameof(CosmicNameplate.Plate), null, null, null, 0),
            ("devStudio.ui.cosmetics.attributes.adaptiveImage", nameof(CosmicNameplate.Adaptive), null, null, null, 2)
            ));
        void OpenPackageEditor(CosmicPackage package) => OpenScreen(() => ShowPackageEditorScreen(addon, package));

        void SetUpRightClickAction<Costume>(PassiveButton button, List<Costume> costumeList, Costume costume, Action editAction) where Costume : CustomItemGrouped
        {
            string name = "";
            if(costume is CustomCosmicItem item)
                name = item.UnescapedName;
            else if(costume is CosmicPackage package)
                name = package.DisplayName;

            Action<MetaScreen> deleteAction = screen => MetaUI.ShowYesOrNoDialog(transform, () => { screen.CloseScreen(); costumeList.Remove(costume); ReopenScreen(); }, () => { },
                Language.Translate("devStudio.ui.common.confirmDeleting") + $"<br>\"{name}\"");
            Action<MetaScreen>? outputAction = costume is CosmicPackage ? null : screen =>
            {
                var item = costume as CustomCosmicItem;
                if (item == null) return;

                screen.CloseScreen();

                ZipArchiveEntry CreateEntryFromStream(ZipArchive zip, string path,Stream stream) {
                    var entry = zip.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
                    using (var entryStream = entry.Open())
                    {
                        stream.CopyTo(entryStream);
                    }
                    return entry;
                }

                ZipArchiveEntry CreateEntryFromString(ZipArchive zip, string path, string rawText)
                {
                    var entry = zip.CreateEntry(path, System.IO.Compression.CompressionLevel.Optimal);
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(rawText);
                    }
                    return entry;
                }

                Directory.CreateDirectory("GlobalCosOutputs");
                using (var zip = ZipFile.Open(@"GlobalCosOutputs/" + item.Name + ".zip", ZipArchiveMode.Create))
                {
                    //パスを修正しながら画像をコピーする
                    var copiedItem = JsonStructure.Deserialize(costume.Serialize(), costume.GetType()) as CustomCosmicItem;
                    if (copiedItem != null)
                    {
                        foreach (var image in copiedItem.AllImage())
                        {
                            try
                            {
                                using var stream = File.OpenRead(addon.FolderPath + "/MoreCosmic/" + copiedItem.Category + "/" + image.Address);
                                var fileName = Path.GetFileName(image.Address);

                                image.Address = copiedItem.Name + "_" + fileName;
                                CreateEntryFromStream(zip, image.Address, stream);
                            }
                            catch { }
                        }
                        CreateEntryFromString(zip, "Contents.json", copiedItem.Serialize());
                    }
                }

                MetaUI.ShowConfirmDialog(null, new TranslateTextComponent("devStudio.ui.common.outputAsGlobal"));
            };

            button.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked += () =>
            {
                var confirmWindow = MetaScreen.GenerateWindow(new Vector2(2f, 2.7f), null, Vector3.zero, true, true, withMask: true);
                confirmWindow.SetWidget(GUI.API.VerticalHolder(GUIAlignment.Center,
                    GUI.API.RawText(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.CenteredBold), name),
                    GUI.API.VerticalMargin(0.4f),
                    GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.cosmetics.action.edit", _ => editAction.Invoke()),
                    outputAction != null ? GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.cosmetics.action.output", _ => outputAction!.Invoke(confirmWindow)) : null,
                    GUI.API.LocalizedButton(GUIAlignment.Center, GUI.API.GetAttribute(AttributeAsset.StandardMediumMasked), "devStudio.ui.cosmetics.action.delete", _ => deleteAction.Invoke(confirmWindow))
                    ), new Vector2(0.5f, 0.5f), out var _);
            };
        }

        var categories = new (string translationKey, Func<IMetaWidgetOld> widgetProvider, Action contentCreator)[]
        {
            ("devStudio.ui.cosmetics.hats", () => {
                if(addon.MyBundle == null)return null!;
                MetaWidgetOld widget = new();
                widget.Append(addon.MyBundle.Hats, hat=>{
                    return new MetaWidgetOld.Button(()=>OpenHatEditor(hat), new(TextAttributeOld.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = hat.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Hats,hat,()=>OpenHatEditor(hat));

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.35f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.35f * 2f,0f);
                            GenerateSprite((hat.Preview ?? hat.Main ?? hat.Move)?.GetSprite(0), (hat.Main != null && hat.Preview == null) ? hat.Back?.GetSprite(0) : null,renderer.transform , hat.Adaptive, 0.3f,-1.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return widget;
            },()=>{
                CosmicHat hat = new();
                hat.Name = "";
                hat.Author = "";
                new StackfullCoroutine(hat.Activate(false)).Wait();
                addon.MyBundle.Hats.Add(hat);
                OpenHatEditor(hat);
            }),
            ("devStudio.ui.cosmetics.visors", () => {
                if(addon.MyBundle == null)return null!;
                MetaWidgetOld widget = new();
                widget.Append(addon.MyBundle.Visors, visor=>{
                    return new MetaWidgetOld.Button(()=>OpenVisorEditor(visor), new(TextAttributeOld.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = visor.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Visors,visor,()=>OpenVisorEditor(visor));

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.35f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.35f * 2f,0f);
                            GenerateSprite((visor.Preview ?? visor.Main ?? visor.Move)?.GetSprite(0), null,renderer.transform , visor.Adaptive,0.3f,-1.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return widget;
            },()=>{
                CosmicVisor visor = new();
                visor.Name = "";
                visor.Author = "";
                new StackfullCoroutine(visor.Activate(false)).Wait();
                addon.MyBundle.Visors.Add(visor);
                OpenVisorEditor(visor);
            }),
            ("devStudio.ui.cosmetics.nameplates",  () => {
                if(addon.MyBundle == null)return null!;
                MetaWidgetOld widget = new();
                widget.Append(addon.MyBundle.Nameplates, nameplate=>{
                    return new MetaWidgetOld.Button(()=>OpenNameplateEditor(nameplate), new(TextAttributeOld.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = nameplate.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Nameplates,nameplate,()=>OpenNameplateEditor(nameplate));

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.46f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.46f * 2f,0f);
                            GenerateSprite(nameplate.Plate?.GetSprite(0), null,renderer.transform , false,0.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return widget;
            },()=>{
                CosmicNameplate nameplate = new();
                nameplate.Name = "";
                nameplate.Author = "";
                new StackfullCoroutine(nameplate.Activate(false)).Wait();
                addon.MyBundle.Nameplates.Add(nameplate);
                OpenNameplateEditor(nameplate);
            }),
            ("devStudio.ui.cosmetics.packages", () =>{
                if(addon.MyBundle == null)return null!;
                MetaWidgetOld widget = new();
                widget.Append(addon.MyBundle.Packages, package=>{
                    return new MetaWidgetOld.Button(()=>OpenPackageEditor(package), new(TextAttributeOld.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial})
                    { RawText = package.DisplayName, PostBuilder = (button,renderer,_)=>{
                        SetUpRightClickAction(button,addon.MyBundle.Packages,package,()=>OpenPackageEditor(package));
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    } };
                },2,-1,0,0.65f);
                return widget;
            },()=>{
                CosmicPackage package = new();
                package.Package = "";
                package.Format = "";
                addon.MyBundle.Packages.Add(package);
                OpenPackageEditor(package);
            })
        };


        var currentSelection = categories[0];


        widget.Append(categories, category =>
        {
            return new MetaWidgetOld.Button(() => {
                currentSelection = category;
                innerRef.Value!.SetWidget(category.widgetProvider.Invoke());
            }, TextAttributeOld.BoldAttr) { TranslationKey = category.translationKey };
        }, categories.Length, 1, 0, 0.6f);

        widget.Append(new MetaWidgetOld.Button(()=>currentSelection.contentCreator.Invoke(), new(TextAttributeOld.BoldAttr) { Size = new(0.35f, 0.2f) }) { RawText = "+" });


        widget.Append(new MetaWidgetOld.ScrollView(new(8f, 4f), new MetaWidgetOld()) { Alignment = IMetaWidgetOld.AlignmentOption.Center, InnerRef = innerRef });


        return (widget, () => { innerRef.Value!.SetWidget(currentSelection.widgetProvider.Invoke()); }, () =>
        {
            File.WriteAllText(addon.FolderPath+"/MoreCosmic/Contents.json", addon.MyBundle.Serialize());
            return true;
        }
        );
    }
}

public class DevAddon : IResourceAllocator
{
    public string Name { get; private set; }
    public string FolderPath { get; private set; }
    public string Id { get; private set; }

    public CustomItemBundle? MyBundle = null;

    private AddonMeta? addonMeta;
    public AddonMeta MetaSetting { get {
            if (addonMeta == null)
            {
                addonMeta = (AddonMeta?)JsonStructure.Deserialize(File.ReadAllText(FolderPath + "/addon.meta"), typeof(AddonMeta)) ??
                new() { Name = Name, Version = "1.0", Author = "Unknown", Description = "" };
            }
            return addonMeta;
        } }

    static public IEnumerable<DevAddon> SearchDevAddons()
    {
        foreach (var dir in Directory.GetDirectories("Addons", "*"))
        {
            string metaFile = $"{dir}/addon.meta";
            if (File.Exists(metaFile))
            {
                AddonMeta? meta = (AddonMeta?)JsonStructure.Deserialize(File.ReadAllText(metaFile), typeof(AddonMeta));
                if (meta == null) continue;
                yield return new DevAddon(meta.Name, dir) { addonMeta = meta };
            }
        }
    }

    static public async Task<DevAddon[]> SearchDevAddonsAsync()
    {
        List<DevAddon> result = new();
        foreach (var dir in Directory.GetDirectories("Addons", "*"))
        {
            string metaFile = $"{dir}/addon.meta";
            if (File.Exists(metaFile))
            {
                AddonMeta? meta = (AddonMeta?)JsonStructure.Deserialize(await File.ReadAllTextAsync(metaFile), typeof(AddonMeta));
                if (meta == null) continue;
                result.Add(new DevAddon(meta.Name, dir) { addonMeta = meta });
            }
        }
        return result.ToArray();
    }

    public DevAddon(string name, string folderPath)
    {
        Name = name;
        FolderPath = folderPath;
        Id = Path.GetFileName(folderPath);
    }

    public void BuildAddon()
    {
        string zipPath = FolderPath + "/" + Id + ".zip";
        string tempPath = "TempAddon.zip";
        if (File.Exists(zipPath)) File.Delete(zipPath);
        ZipFile.CreateFromDirectory(FolderPath, tempPath);
        File.Move(tempPath, zipPath);
    }

    public void SaveMetaSetting()
    {
        Name = MetaSetting.Name;
        File.WriteAllText(FolderPath + "/addon.meta", MetaSetting.Serialize());
    }

    public async Task<(string path, string id, SerializableDocument doc)[]> LoadDocumentsAsync()
    {
        if (!Directory.Exists(FolderPath + "/Documents")) return new (string, string, SerializableDocument)[0];

        List<(string path, string id, SerializableDocument doc)> result = new();
        foreach (var path in Directory.GetFiles(FolderPath + "/Documents"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            var doc = JsonStructure.Deserialize<SerializableDocument>(await File.ReadAllTextAsync(path));
            if (doc == null) continue;

            doc.RelatedNamespace = this;
            result.Add((path, id, doc));
        }
        return result.ToArray();
    }

    INebulaResource? IResourceAllocator.GetResource(Virial.Compat.IReadOnlyArray<string> namespaceArray, string name)
    {
        if (namespaceArray.Count > 0) return null;
        if (name.Length == 0) return null;

        string folderPath = FolderPath + "/Resources";
        string leftPath = name.Replace('/','.');

        while (true) {
            if (File.Exists(folderPath + "/" + leftPath)) return new StreamResource(() => File.OpenRead(folderPath + "/" + leftPath));

            var dirs = Directory.GetDirectories(folderPath);
            if (dirs.Length == 0) return null;
            foreach (var dir in dirs)
            {
                string lowestDir = dir.Substring(folderPath.Length + 1);
                if (leftPath.Length > lowestDir.Length + 1 && leftPath[lowestDir.Length] is '.' && leftPath.StartsWith(lowestDir))
                {
                    folderPath += "/" + lowestDir;
                    leftPath = leftPath.Substring(lowestDir.Length + 1);
                    break;
                }
            }
        }
    }

    public bool ExistsFile(string path) => File.Exists(FolderPath + "/" + path);
    public bool DeleteFile(string path) {
        try { 
            File.Delete(FolderPath + "/" + path);
            return true;
        } catch {
            return false;
        }
    }
    public bool WriteFile(string path, string contents)
    {
        try
        {
            File.WriteAllText(FolderPath + "/" + path, contents);
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal Stream? OpenStream(string path)
    {
        try
        {
            return File.Open(FolderPath + "/" + path, FileMode.Open);
        }
        catch
        {
            return null;
        }
    }
}