using Il2CppInterop.Runtime.Injection;
using JetBrains.Annotations;
using Nebula.Utilities;
using System.IO;
using System;
using static Il2CppSystem.Linq.Expressions.Interpreter.InitializeLocalInstruction;
using static Nebula.Modules.NebulaAddon;
using System.Text;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.UIElements;
using static Il2CppSystem.TypeIdentifiers;
using System.Reflection;
using static Rewired.Controller;
using UnityEngine.Rendering;
using TMPro;
using static Nebula.Modules.MetaContextOld;
using Nebula.Modules.MetaContext;

namespace Nebula.Behaviour;

public class DevStudio : MonoBehaviour
{
    static DevStudio() => ClassInjector.RegisterTypeInIl2Cpp<DevStudio>();
    static public MainMenuManager? MainMenu;

    private MetaScreen myScreen = null!;

    private List<Func<(IMetaContextOld context, Action? postAction,Func<bool>? confirm)>> screenLayer = new();
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
            NebulaManager.Instance.HideHelpContext();
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
        myScreen.SetContext(new Vector2(ScreenWidth, 5.5f), content.context);
        content.postAction?.Invoke();
        currentConfirm = content.confirm;
    }

    private void OpenScreen(Func<(IMetaContextOld context, Action? postAction, Func<bool>? confirm)> content)
    {
        screenLayer.Add(content);
        OpenFrontScreen();
    }

    protected void Close()
    {
        TransitionFade.Instance.DoTransitionFade(gameObject, null!, () => MainMenu?.mainMenuUI.SetActive(true), () => GameObject.Destroy(gameObject));
    }

    static public void Open(MainMenuManager mainMenu)
    {
        MainMenu = mainMenu;

        var obj = UnityHelper.CreateObject<DevStudio>("DevStudioMenu", Camera.main.transform, new Vector3(0, 0, -30f));
        TransitionFade.Instance.DoTransitionFade(null!, obj.gameObject, () => { mainMenu.mainMenuUI.SetActive(false); }, () => { obj.OnShown(); });
    }

    public void OnShown() => OpenScreen(ShowMainScreen);

    public void Awake()
    {
        if (MainMenu != null)
        {
            var backBlackPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(1);
            GameObject.Instantiate(backBlackPrefab.gameObject, transform);
            var backGroundPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(2);
            var backGround = GameObject.Instantiate(backGroundPrefab.gameObject, transform);
            GameObject.Destroy(backGround.transform.GetChild(2).gameObject);

            var closeButtonPrefab = MainMenu.playerCustomizationPrefab.transform.GetChild(0).GetChild(0);
            var closeButton = GameObject.Instantiate(closeButtonPrefab.gameObject, transform);
            GameObject.Destroy(closeButton.GetComponent<AspectPosition>());
            var button = closeButton.GetComponent<PassiveButton>();
            button.gameObject.SetActive(true);
            button.OnClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.OnClick.AddListener(()=>CloseScreen());
            button.transform.localPosition = new Vector3(-4.9733f, 2.6708f, -50f);
        }

        myScreen = UnityHelper.CreateObject<MetaScreen>("Screen", transform, new Vector3(0, -0.1f, -10f));
    }
    
    public (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowMainScreen()
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

        MetaContextOld context = new();

        context.Append(new MetaContextOld.Text(new TextAttribute(TextAttribute.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.main.title" });
        context.Append(new MetaContextOld.VerticalMargin(0.2f));

        //Add-ons
        context.Append(new MetaContextOld.Button(() => 
        {
            var screen = MetaScreen.GenerateWindow(new(5.9f, 3.1f), transform, Vector3.zero, true, false);
            MetaContextOld context = new();

            CombinedContextOld GenerateContext(Reference<TextField> reference, string rawText,bool isMultiline,Predicate<char> predicate)=> 
            new CombinedContextOld(
               new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = rawText },
               new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
               new MetaContextOld.TextInput(isMultiline ? 2 : 1, 2f, new(3.7f, isMultiline ? 0.58f : 0.3f)) { TextFieldRef = reference, TextPredicate = predicate }
                   );

            Reference<TextField> refId = new(), refName = new(), refAuthor = new(), refDesc = new();
            context.Append(GenerateContext(refId, "Add-on ID", false, TextField.IdPredicate));
            context.Append(GenerateContext(refName, "Name", false, TextField.JsonStringPredicate));
            context.Append(GenerateContext(refAuthor, "Author", false, TextField.JsonStringPredicate));
            context.Append(GenerateContext(refDesc, "Description", true, TextField.JsonStringPredicate));
            context.Append(new MetaContextOld.VerticalMargin(0.16f));
            context.Append(new MetaContextOld.Button(() => 
            {
                CheckAndGenerateAddon(screen, refId.Value!, refName.Value!, refAuthor.Value!, refDesc.Value!);
            }, new(TextAttribute.BoldAttr) { Size = new(1.8f, 0.3f) }) { TranslationKey = "devStudio.ui.common.generate", Alignment = IMetaContextOld.AlignmentOption.Center });

            screen.SetContext(context);
            refId.Value!.InputPredicate = TextField.TokenPredicate;
            TextField.EditFirstField();

        }, new TextAttribute(TextAttribute.BoldAttr) { Size = new(0.34f, 0.18f) }.EditFontSize(2.4f)) { RawText = "+" });

        Reference<MetaContextOld.ScrollView.InnerScreen> addonsRef = new();
        context.Append(new MetaContextOld.ScrollView(new Vector2(9f, 4f), addonsRef));

        IEnumerator CoLoadAddons()
        {
            yield return addonsRef.Wait();
            
            addonsRef.Value?.SetLoadingContext();

            var task = DevAddon.SearchDevAddonsAsync();
            yield return task.WaitAsCoroutine();
            
            MetaContextOld inner = new();
            foreach (var addon in task.Result)
            {
                inner.Append(
                    new CombinedContextOld(
                        new MetaContextOld.Text(
                            new(TextAttribute.NormalAttr)
                            {
                                FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                                Size = new(4.6f, 0.27f),
                                Alignment = TMPro.TextAlignmentOptions.Left
                            })
                        { RawText = addon.Name },
                        new MetaContextOld.VerticalMargin(0.3f),
                        new MetaContextOld.Button(() => OpenScreen(() => ShowAddonScreen(addon)),
                         new(TextAttribute.NormalAttr)
                         {
                             FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                             Size = new(1f, 0.2f),
                         })
                        { TranslationKey = "devStudio.ui.common.edit" }.SetAsMaskedButton(),
                        new MetaContextOld.HorizonalMargin(0.1f),
                        new MetaContextOld.Button(() => MetaUI.ShowYesOrNoDialog(transform,() => { Helpers.DeleteDirectoryWithInnerFiles(addon.FolderPath); ReopenScreen(true); }, () => { }, Language.Translate("devStudio.ui.common.confirmDeletingAddon") + $"<br>\"{addon.Name}\""),
                         new(TextAttribute.NormalAttr)
                         {
                             FontMaterial = VanillaAsset.StandardMaskedFontMaterial,
                             Size = new(1f, 0.2f),
                         })
                        { Text = NebulaGUIContextEngine.Instance.TextComponent(Color.red.RGBMultiplied(0.7f), "devStudio.ui.common.delete"), Color = Color.red.RGBMultiplied(0.7f) }.SetAsMaskedButton()

                         )
                    { Alignment = IMetaContextOld.AlignmentOption.Left }
                    );
            }

            addonsRef.Value?.SetContext(inner);
        }

        return (context, ()=>StartCoroutine(CoLoadAddons().WrapToIl2Cpp()), null);
    }

    //Addon
    public (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowAddonScreen(DevAddon addon)
    {
        MetaContextOld context = new();

        void ShowNameEditWindow() {
            var screen = MetaScreen.GenerateWindow(new(3.9f, 1.14f), transform, Vector3.zero, true, false);
            MetaContextOld context = new();
            Reference<TextField> refName = new();

            context.Append(new MetaContextOld.TextInput(1, 2f, new(3.7f, 0.3f)) { TextFieldRef = refName, DefaultText = addon.Name, TextPredicate = TextField.JsonStringPredicate });
            context.Append(new MetaContextOld.Button(() =>
            {
                addon.MetaSetting.Name = refName.Value!.Text;
                UpdateMetaInfo();
                addon.SaveMetaSetting();
                screen.CloseScreen();
                ReopenScreen(true);
            }
            , new(TextAttribute.BoldAttr) { Size = new(1.8f, 0.3f) })
            { TranslationKey = "devStudio.ui.common.save", Alignment = IMetaContextOld.AlignmentOption.Center });

            screen.SetContext(context);
        }

        Reference<TextField> authorRef = new();
        Reference<TextField> versionRef = new();
        Reference<TextField> descRef = new();

        //Addon Name
        context.Append(
            new CombinedContextOld(
                new MetaContextOld.Button(ShowNameEditWindow, new(TextAttribute.BoldAttr) { Size = new(0.5f, 0.22f) }) { TranslationKey = "devStudio.ui.common.edit" },
                new MetaContextOld.HorizonalMargin(0.14f),
                new MetaContextOld.Text(new TextAttribute(TextAttribute.TitleAttr) { Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(2.7f)) { RawText = addon.Name }
            ){ Alignment = IMetaContextOld.AlignmentOption.Left});

        //Author & Version
        context.Append( new CombinedContextOld(
            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "Author" },
            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaContextOld.TextInput(1, 2f, new(2.5f, 0.3f)) { TextFieldRef = authorRef, DefaultText = addon.MetaSetting.Author, TextPredicate = TextField.JsonStringPredicate },

            new MetaContextOld.HorizonalMargin(0.4f),

            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.45f, 0.3f) }) { RawText = "Version" },
            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaContextOld.TextInput(1, 2f, new(1.5f, 0.3f)) { TextFieldRef = versionRef, DefaultText = addon.MetaSetting.Version, TextPredicate = TextField.NumberPredicate }
               )
        { Alignment = IMetaContextOld.AlignmentOption.Left });

        //Description
        context.Append(new CombinedContextOld(
            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "Description" },
            new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
            new MetaContextOld.TextInput(2, 2f, new(6.2f, 0.58f)) { TextFieldRef = descRef, DefaultText = addon.MetaSetting.Description.Replace("<br>","\r"), TextPredicate = TextField.JsonStringPredicate }
               ){ Alignment = IMetaContextOld.AlignmentOption.Left });

        bool MetaInfoDirty() => addon.MetaSetting.Author != authorRef.Value!.Text || addon.MetaSetting.Version != versionRef.Value!.Text || addon.MetaSetting.Description != descRef.Value!.Text.Replace("\r", "<br>");

        void UpdateMetaInfo()
        {
            addon.MetaSetting.Author = authorRef.Value!.Text;
            addon.MetaSetting.Version = versionRef.Value!.Text;
            addon.MetaSetting.Description = descRef.Value!.Text.Replace("\r", "<br>");
        }

        //Contents of add-on
        (string translationKey,Func<DevAddon,(IMetaContextOld context, Action? postAction, Func<bool>? confirm)>)[] edtiors = {
            ("devStudio.ui.addon.cosmetics",ShowCosmeticsScreen),
            ("devStudio.ui.addon.document",ShowDocumentScreen)
        };

        context.Append(new MetaContextOld.VerticalMargin(0.21f));
        context.Append(edtiors, (entry) => new MetaContextOld.Button(() => { UpdateMetaInfo(); addon.SaveMetaSetting(); OpenScreen(() => entry.Item2.Invoke(addon)); }, new(TextAttribute.BoldAttr) { Size = new(2.4f, 0.55f) }) { TranslationKey = entry.translationKey }, 3, 3, 0, 0.85f, true);
        context.Append(new MetaContextOld.VerticalMargin(0.2f));

        //Build
        context.Append(new MetaContextOld.Button(() => addon.BuildAddon(), new TextAttribute(TextAttribute.BoldAttr)) { TranslationKey = "devStudio.ui.addon.build", Alignment = IMetaContextOld.AlignmentOption.Right });

        return (context, null, () => {
            if (!MetaInfoDirty()) return true;
            MetaUI.ShowYesOrNoDialog(transform, ()=> { UpdateMetaInfo(); addon.SaveMetaSetting(); CloseScreen(true); }, () => { CloseScreen(true); },  Language.Translate("devStudio.ui.common.confirmSaving"));
            return false;
        }
        );
    }

    //Document
    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowDocumentEditorScreen(DevAddon addon, string path, string id, SerializableDocument doc)
    {
        void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, doc.Serialize(), Encoding.UTF8);
        }

        void NewContentEditor(SerializableDocument targetContainer)
        {
            MetaContextOld context = new();

            (string id, Func<SerializableDocument> generator)[] variations = {
                ("contents", ()=>new SerializableDocument(){ Contents = new() }),
                ("aligned", ()=>new SerializableDocument(){ Aligned = new() }),
                ("localizedText", ()=>new SerializableDocument(){ TranslationKey = "undefined", IsVariable = true }),
                ("rawText", ()=>new SerializableDocument(){ RawText = "Text", IsVariable = true }),
                ("image", ()=>new SerializableDocument(){ Image = "Nebula::NebulaImage", Width = 0.25f }),
                ("vertical", ()=>new SerializableDocument(){ VSpace = 0.5f }),
                ("horizontal", ()=>new SerializableDocument(){ HSpace = 0.5f }),
                ("documentRef", ()=>new SerializableDocument(){ Document = new(){ Id = "", Arguments = new() } })
            };

            context.Append(variations, (entry) =>
            new MetaContextOld.Button(() =>
            {
                targetContainer.AppendContent(entry.generator.Invoke());
                NebulaManager.Instance.HideHelpContext();
                ReopenScreen(true);
            }, new(TextAttribute.BoldAttr) { Size = new(1.2f, 0.24f) })
            { TranslationKey = "devStudio.ui.document.element." + entry.id, Alignment = IMetaContextOld.AlignmentOption.Left },
            2, -1, 0, 0.52f, false, IMetaContextOld.AlignmentOption.Left);

            NebulaManager.Instance.SetHelpContext(null, context);
        }

        void ShowContentEditor(PassiveButton editorButton, SerializableDocument doc, SerializableDocument? parent)
        {
            MetaContextOld context = new();

            MetaContextOld.Button GetButton(Action clickAction, string rawText, bool reopenScreen = true, bool useBoldFont = false, bool asMasked = false)
            {
                var attr = new TextAttribute(useBoldFont ? TextAttribute.BoldAttr : TextAttribute.NormalAttr) { Size = new(0.2f, 0.2f) };
                if (asMasked) attr.FontMaterial = VanillaAsset.StandardMaskedFontMaterial;
                var button = new MetaContextOld.Button(() => { clickAction.Invoke(); if (reopenScreen) ReopenScreen(true); }, attr) { RawText = rawText };
                if (asMasked) button.SetAsMaskedButton();
                return button;
            }

            List<IMetaParallelPlacableOld> buttons = new();
            void AppendMargin(bool wide = false) { if (buttons.Count > 0) buttons.Add(new MetaContextOld.HorizonalMargin(wide ? 0.35f : 0.2f)); }
            void AppendButton(Action clickAction, string rawText, bool reopenScreen = true, bool useBoldFont = false) => buttons.Add(GetButton(clickAction,rawText,reopenScreen,useBoldFont));
            

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
                AppendButton(() => { parent.RemoveContent(doc); NebulaManager.Instance.HideHelpContext(); }, "×".Color(Color.red), true, true);
            }

            context.Append(new CombinedContextOld(buttons.ToArray()) { Alignment = IMetaContextOld.AlignmentOption.Left });

            MetaContextOld.TextInput GetTextFieldContent(bool isMultiline, float width, string defaultText, Action<string> updateAction, Predicate<char>? textPredicate,bool withMaskMaterial = false)
            {
                return new MetaContextOld.TextInput(isMultiline ? 7 : 1, isMultiline ? 1.2f : 1.8f, new(width, isMultiline ? 1.2f : 0.23f))
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
            void AppendParallelMargin(float margin) => AppendParallel(new MetaContextOld.HorizonalMargin(margin));
            void OutputParallelToContext()
            {
                if (parallelPool.Count == 0) return;
                context.Append(new CombinedContextOld(parallelPool.ToArray()) { Alignment = IMetaContextOld.AlignmentOption.Left });
                parallelPool.Clear();
            }

            void AppendTextField(bool isMultiline, float width, string defaultText, Action<string> updateAction, Predicate<char>? textPredicate)
            {
                context.Append(GetTextFieldContent(isMultiline, width, defaultText, updateAction, textPredicate));
            }

            void AppendTopTag(string translateKey)
            {
                AppendParallel(GetLocalizedTagContent(translateKey));
                AppendParallelMargin(0.05f);
                AppendParallel(GetRawTagContent(":"));
                AppendParallelMargin(0.1f);
            }

            MetaContextOld.VariableText GetRawTagContent(string rawText, bool asMasked = false) => new MetaContextOld.VariableText(asMasked ? new(TextAttribute.BoldAttrLeft) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial } : TextAttribute.BoldAttrLeft) { RawText = rawText };
            MetaContextOld.VariableText GetLocalizedTagContent(string translationKey) => new MetaContextOld.VariableText(TextAttribute.BoldAttrLeft) { TranslationKey = translationKey };

            if (doc.RawText != null || doc.TranslationKey != null)
            {
                if (doc.RawText != null)
                    AppendTextField(true, 7.5f, doc.RawText, (input) => doc.RawText = input, TextField.JsonStringPredicate);
                else
                    AppendTextField(false, 3f, doc.TranslationKey, (input) => doc.TranslationKey = input, TextField.JsonStringPredicate);


                AppendParallel(MetaContextOld.StateButton.TopLabelCheckBox("devStudio.ui.document.editor.isBold", null, true, new Reference<bool>().Set(doc.IsBold ?? false), (val) => { doc.IsBold = val; ReopenScreen(true); }));
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

                OutputParallelToContext();
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

                OutputParallelToContext();
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

                OutputParallelToContext();
            }else if(doc.Document != null)
            {
                Reference<MetaContextOld.ScrollView.InnerScreen> innerRef = new();
                void UpdateInner()
                {
                    if (innerRef.Value == null) return;
                    if (!innerRef.Value.IsValid) return;

                    MetaContextOld inner = new();
                    foreach (var arg in doc.Document!.Arguments!)
                    {
                        inner.Append(new CombinedContextOld(
                            GetTextFieldContent(false, 1.4f, arg.Key, (input) =>
                            {
                                if (arg.Key != input)
                                {
                                    doc.Document.Arguments.Remove(arg.Key);
                                    doc.Document.Arguments[input] = arg.Value;
                                    NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                                }
                            }, TextField.IdPredicate, true),
                            new MetaContextOld.HorizonalMargin(0.1f),
                            GetRawTagContent(":"),
                            new MetaContextOld.HorizonalMargin(0.1f),
                            GetTextFieldContent(false, 3.1f, arg.Value, (input) =>
                            {
                                if (arg.Value != input)
                                {
                                    doc.Document.Arguments[arg.Key] = input;
                                    NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                                }
                            }, TextField.JsonStringPredicate, true),
                            new MetaContextOld.HorizonalMargin(0.1f),
                            GetButton(() => {
                                doc.Document.Arguments.Remove(arg.Key);
                                NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                            }, "×".Color(Color.red), true, true, true)
                            )
                        { Alignment = IMetaContextOld.AlignmentOption.Left });
                    }

                    try
                    {
                        innerRef.Value!.SetContext(inner);
                    }
                    catch { }
                }

                AppendTopTag("devStudio.ui.document.editor.document");
                AppendParallel(GetTextFieldContent(false, 2.6f, doc.Document.Id.ToString() ?? "", (input) =>
                {
                    doc.Document.Id = input;
                    var refDoc = DocumentManager.GetDocument(input);
                    if(refDoc?.Arguments != null)
                    {
                        foreach (var entry in doc.Document.Arguments) if (!refDoc!.Arguments!.Contains(entry.Key)) doc.Document.Arguments.Remove(entry.Key);
                        foreach (var arg in refDoc!.Arguments!) if (!doc.Document.Arguments.ContainsKey(arg)) doc.Document.Arguments[arg] = "";
                        NebulaManager.Instance.ScheduleDelayAction(UpdateInner);
                    }
                }, TextField.IdPredicate));
                OutputParallelToContext();
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
                OutputParallelToContext();

                context.Append(new MetaContextOld.ScrollView(new Vector2(6.1f, 2.6f), new MetaContextOld(), true) { Alignment = IMetaContextOld.AlignmentOption.Left, InnerRef = innerRef, PostBuilder = UpdateInner });
            }

            NebulaManager.Instance.SetHelpContext(editorButton, context);
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
                if (NebulaManager.Instance.HelpRelatedObject == editorButton) NebulaManager.Instance.HideHelpContext();
            });
            editorButton.OnClick.AddListener(() =>
            {
                if (NebulaManager.Instance.HelpRelatedObject != editorButton)
                    ShowContentEditor(editorButton, doc, parent);
                NebulaManager.Instance.HelpIrrelevantize();
            });

        }

        MetaContextOld context = new();
        context.Append(
            new CombinedContextOld(
                new MetaContextOld.Text(new TextAttribute(TextAttribute.TitleAttr) { Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(2.7f)) { RawText = id, Alignment = IMetaContextOld.AlignmentOption.Left },
                new MetaContextOld.Button(() => { NebulaManager.Instance.HideHelpContext(); Save(); }, TextAttribute.BoldAttr) { TranslationKey = "devStudio.ui.common.save" },
                new MetaContextOld.Button(() =>
                {
                    NebulaManager.Instance.HideHelpContext();
                    var screen = MetaScreen.GenerateWindow(new(7f, 4.5f), transform, Vector3.zero, true, true, true);
                    Reference<MetaContextOld.ScrollView.InnerScreen> innerRef = new();
                    screen.SetContext(new MetaContextOld.ScrollView(new Vector2(7f, 4.5f), doc.Build(innerRef, nameSpace: addon) ?? new MetaContextOld()) { InnerRef = innerRef });
                }, TextAttribute.BoldAttr)
                { TranslationKey = "devStudio.ui.common.preview" }
            )
            { Alignment = IMetaContextOld.AlignmentOption.Left }
        );

        context.Append(new MetaContextOld.VerticalMargin(0.1f));
        context.Append(new MetaContextOld.ScrollView(new Vector2(ScreenWidth - 0.4f, 4.65f), doc.BuildForDev(BuildContentEditor) ?? new MetaContextOld(), true) { ScrollerTag = "DocumentEditor" });

        return (context, null, () =>
        {
            NebulaManager.Instance.HideHelpContext();
            MetaUI.ShowYesOrNoDialog(transform, () => { Save(); CloseScreen(true); }, () => { CloseScreen(true); }, Language.Translate("devStudio.ui.common.confirmSaving"), true);
            return false;
        }
        );
    }

    //Documents
    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowDocumentScreen(DevAddon addon)
    {
        void CheckAndGenerateDocument(Il2CppArgument<MetaScreen> editScreen, Il2CppArgument<TextField> id, string? originalId = null)
        {
            if (id.Value.Text.Length < 1)
            {
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.requiredText").Color(Color.red * 0.7f));
                return;
            }

            if (File.Exists(addon.FolderPath + "/Documents/" + id.Value.Text + ".json"))
            {
                id.Value.SetText("");
                id.Value.SetHint(Language.Translate("devStudio.ui.hint.duplicatedId").Color(Color.red * 0.7f));
                return;
            }

            MetaContextOld.ScrollView.RemoveDistHistory("DocumentEditor");
            editScreen.Value.CloseScreen();
            SerializableDocument? doc = null;
            if(originalId != null)
                doc = JsonStructure.Deserialize<SerializableDocument>(File.ReadAllText(addon.FolderPath + "/Documents/" + originalId + ".json"));
            doc ??= new SerializableDocument() { Contents = new() };
            doc.RelatedNamespace = addon;
            OpenScreen(() => ShowDocumentEditorScreen(addon, addon.FolderPath + "/Documents/" + id.Value.Text + ".json", id.Value.Text, doc));
        }

        MetaContextOld context = new();

        context.Append(new MetaContextOld.Text(new TextAttribute(TextAttribute.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.addon.document" });
        context.Append(new MetaContextOld.VerticalMargin(0.2f));

        (string path, string id, SerializableDocument doc)[]? docs = null;

        void OpenGenerateWindow(string? original = null)
        {
            var screen = MetaScreen.GenerateWindow(new(5.9f, original != null ? 1.8f : 1.5f), transform, Vector3.zero, true, false);
            MetaContextOld context = new();

            if (original != null) context.Append(new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(1.5f, 0.3f) }) { RawText = Language.Translate("devStudio,ui.common.original") + " : " + original });

            Reference<TextField> refId = new();
            TMPro.TextMeshPro usingInfoText = null!;


            context.Append(new CombinedContextOld(
               new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Left, Size = new(1.5f, 0.3f) }) { RawText = "ID" },
               new MetaContextOld.Text(new TextAttribute(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.2f, 0.3f) }) { RawText = ":" },
               new MetaContextOld.TextInput(1, 2f, new(3.7f, 0.3f)) { TextFieldRef = refId, TextPredicate = TextField.IdPredicate }
                   ));
            context.Append(new MetaContextOld.Text(new TextAttribute(TextAttribute.NormalAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(5.6f, 0.14f) }.EditFontSize(1.2f, 0.6f, 1.2f)) { PostBuilder = t => usingInfoText = t });
            context.Append(new MetaContextOld.VerticalMargin(0.16f));
            context.Append(new MetaContextOld.Button(() =>
            {
                CheckAndGenerateDocument(screen, refId.Value!,original);
            }, new(TextAttribute.BoldAttr) { Size = new(1.8f, 0.3f) })
            { TranslationKey = original != null ? "devStudio.ui.common.clone" : "devStudio.ui.common.generate", Alignment = IMetaContextOld.AlignmentOption.Center });

            screen.SetContext(context);
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
        context.Append(new MetaContextOld.Button(() =>
        {
            OpenGenerateWindow();
        }, new TextAttribute(TextAttribute.BoldAttr) { Size = new(0.34f, 0.18f) }.EditFontSize(2.4f))
        { RawText = "+" });

        //Scroller
        Reference<MetaContextOld.ScrollView.InnerScreen> inner = new();
        context.Append(new MetaContextOld.ScrollView(new(ScreenWidth, 4f), inner) { Alignment = IMetaContextOld.AlignmentOption.Center });

        //Shower
        IEnumerator CoShowDocument()
        {
            yield return inner.Wait();
            inner.Value?.SetLoadingContext();

            var task = addon.LoadDocumentsAsync();
            yield return task.WaitAsCoroutine();

            MetaContextOld context = new();
            docs = task.Result;
            foreach (var entry in docs)
            {
                context.Append(new CombinedContextOld(
                    new MetaContextOld.Text(new(TextAttribute.NormalAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial,  Alignment = TMPro.TextAlignmentOptions.Left, Size = new(3f, 0.27f) }) { RawText = entry.id },
                    new MetaContextOld.Button(() =>
                    {
                        MetaContextOld.ScrollView.RemoveDistHistory("DocumentEditor");
                        var doc = JsonStructure.Deserialize<SerializableDocument>(File.ReadAllText(entry.path));

                        if (doc != null)
                        {
                            doc.RelatedNamespace = addon;
                            OpenScreen(() => ShowDocumentEditorScreen(addon, entry.path, entry.id, doc));
                        }
                    }, new(TextAttribute.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f) })
                    { TranslationKey = "devStudio.ui.common.edit" }.SetAsMaskedButton(),
                    new MetaContextOld.HorizonalMargin(0.2f),
                    new MetaContextOld.Button(() =>
                    {
                        MetaUI.ShowYesOrNoDialog(transform, () => { File.Delete(entry.path); ReopenScreen(true); }, () => { }, Language.Translate("devStudio.ui.common.confirmDeleting") + $"<br>\"{entry.id}\"");
                    }, new(TextAttribute.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f)  })
                    { Text = NebulaGUIContextEngine.Instance.TextComponent(Color.red, "devStudio.ui.common.delete") }.SetAsMaskedButton(),
                    new MetaContextOld.HorizonalMargin(0.2f),
                    new MetaContextOld.Button(() =>
                    {
                        OpenGenerateWindow(entry.id);
                    }, new(TextAttribute.BoldAttr) { FontMaterial = VanillaAsset.StandardMaskedFontMaterial, Size = new(0.8f, 0.22f) })
                    { Text = NebulaGUIContextEngine.Instance.TextComponent(Color.white, "devStudio.ui.common.clone") }.SetAsMaskedButton()
                    )
                { Alignment = IMetaContextOld.AlignmentOption.Left });
            }

            inner.Value?.SetContext(context);
        }

        return (context, () => StartCoroutine(CoShowDocument().WrapToIl2Cpp()), null);
    }


    (IMetaContextOld context, Reference<PlayerDisplay> player) GetPlayerDisplayContext()
    {
        MetaContextOld context = new();
        Reference<PlayerDisplay> display = new();

        context.Append(new MetaContextOld.CustomContext(new Vector2(1.5f,3.5f),IMetaContextOld.AlignmentOption.Center,
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

        context.Append(allStateButtons, state => new MetaContextOld.Button(state.action, new(TextAttribute.BoldAttr) { Size = new(1.4f, 0.18f) }) { TranslationKey = "devStudio.ui.cosmetics.anim." + state.translationKey }, 2, -1, 0, 0.44f);
        context.Append(new CombinedContextOld(
            new MetaContextOld.StateButton() { OnChanged = (flag) => display.Value!.Cosmetics.SetFlipX(flag) },
            new MetaContextOld.HorizonalMargin(0.15f),
            new MetaContextOld.Text(new(TextAttribute.BoldAttrLeft) { Size = new(0.8f, 0.12f) }) { TranslationKey = "flip" }
            ));

        return (context, display);
    }


    //Cosmetics
    private static readonly string[][] ImageContentTranslationKey = {
        new string[]{ "devStudio.ui.cosmetics.contents.main", "devStudio.ui.cosmetics.contents.climbUp" },
        new string[]{ "devStudio.ui.cosmetics.contents.back", "devStudio.ui.cosmetics.contents.climbUpBack" },
        new string[]{ "devStudio.ui.cosmetics.contents.flipped", "devStudio.ui.cosmetics.contents.climbDown" },
        new string[]{ "devStudio.ui.cosmetics.contents.backFlipped", "devStudio.ui.cosmetics.contents.climbDownBack" },
    };

    private static MetaContextOld.Button GetCostumeContentButton<Costume>(Costume costume, string translationKey, string fieldName,DevAddon addon, Reference<TextField> costumeNameRef,Action? updateAction) where Costume : CustomCosmicItem {
        Color disabledColor = Color.gray.RGBMultiplied(0.48f);

        MetaContextOld.Button? myButton = null;
        SpriteRenderer? myRenderer = null;
        myButton = new MetaContextOld.Button(() =>
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
                destPath += "/" + title;
                Directory.CreateDirectory(destPath);
                destPath += "/" + fieldName + ".png";
                try
                {
                    File.Copy(path[0], destPath, true);

                    var image = new CosmicImage() { Address = costumeNameRef.Value!.Text.ToByteString() + "/" + fieldName + ".png" };
                    costume.GetType().GetField(fieldName)?.SetValue(costume, image);
                    costume.MyBundle = addon.MyBundle!;
                    new StackfullCoroutine(costume.Activate()).Wait();
                    updateAction?.Invoke();
                    myButton!.Color = myRenderer!.color = Color.white;

                    using Stream hashStream = File.OpenRead(destPath);
                    image.Hash = CosmicImage.ComputeImageHash(hashStream);
                    hashStream.Close();
                }
                catch {
                    MetaUI.ShowConfirmDialog(null,new TranslateTextComponent("devStudio.ui.cosmetics.failedToCopy"));
                }
            });
        }, new(TextAttribute.BoldAttr) { Size = new(0.85f, 0.23f) })
        { Color = costume.GetType().GetField(fieldName)!.GetValue(costume) == null ? disabledColor : Color.white, TranslationKey = translationKey, Alignment = IMetaContextOld.AlignmentOption.Center, PostBuilder = (_, renderer, _) => myRenderer = renderer };
        return myButton;
    }

    static private CombinedContextOld GetTextInputContext(string translationKey, string hint, Reference<TextField> textRef, string defaultText, Action<string> onEntered)
    {
        return new CombinedContextOld(
        new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(1f, 0.4f) }) { TranslationKey = translationKey },
        new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.4f) }) { RawText = ":" },
        new MetaContextOld.HorizonalMargin(0.15f),
        new MetaContextOld.TextInput(1, 1.8f, new(3.3f, 0.28f))
        {
            TextFieldRef = textRef,
            Hint = hint.Color(Color.gray),
            TextPredicate = TextField.JsonStringPredicate,
            DefaultText = defaultText,
            PostBuilder = (field) => field.LostFocusAction += onEntered
        })
        { Alignment = IMetaContextOld.AlignmentOption.Left };
    }

    private void SetUpCommonCosmicProperty(MetaContextOld context,DevAddon addon, CustomCosmicItem costume, Reference<TextField> titleRef, Reference<TextField> authorRef)
    {
        TextMeshPro myText = null!;
        context.Append(GetTextInputContext("devStudio.ui.cosmetics.attributes.name", "Title", titleRef, costume.UnescapedName, (text) => costume.Name = CustomCosmicItem.GetEscapedString(text)));
        context.Append(GetTextInputContext("devStudio.ui.cosmetics.attributes.author", "Author", authorRef, costume.UnescapedAuthor, (text) => costume.Author = CustomCosmicItem.GetEscapedString(text)));
        context.Append(new CombinedContextOld(
            new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Right, Size = new(1f, 0.4f) }) { TranslationKey = "devStudio.ui.cosmetics.attributes.package" },
            new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.4f) }) { RawText = ":" },
            new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(2.5f, 0.4f) }) { RawText = costume.Package, PostBuilder = text=>myText=text },
            new MetaContextOld.HorizonalMargin(0.2f),
            new MetaContextOld.Button(() => {
                var window = MetaScreen.GenerateWindow(new(3.3f, 2.4f), null, Vector3.zero, true, false, true);
                MetaContextOld context = new();
                IEnumerable<CosmicPackage> packages = MoreCosmic.AllPackages.Values;
                if (addon.MyBundle?.Packages != null) packages = packages.Concat(addon.MyBundle!.Packages.Where(package => !MoreCosmic.AllPackages.ContainsKey(package.Package)));
                foreach(var package in packages)
                {
                    context.Append(new MetaContextOld.Button(() =>
                    {
                        myText.text = package.DisplayName;
                        costume.Package = package.Package;
                        window.CloseScreen();
                    }, new(TextAttribute.BoldAttr) { Size = new(2.4f, 0.32f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    { RawText = package.DisplayName, Alignment = IMetaContextOld.AlignmentOption.Left }.SetAsMaskedButton());
                }
                window.SetContext(new MetaContextOld.ScrollView(new(3.2f, 2.2f), context));
            },new TextAttribute(TextAttribute.BoldAttr) { Size = new(0.65f,0.3f)}) { TranslationKey = "devStudio.ui.common.edit" }
            )
        { Alignment = IMetaContextOld.AlignmentOption.Left });

        context.Append(new MetaContextOld.VerticalMargin(0.12f));
    }

    

    private void SetUpCosmicContentProperty<Costume>(MetaContextOld context,Costume costume, DevAddon addon, Reference<TextField> costumeNameRef, Action? updateAction, (string translationKey, string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents) where Costume : CustomCosmicItem
    {
        context.Append(contents.Where(c => c.variation == -1), c => {
            return new CombinedContextOld(
                new MetaContextOld.HorizonalMargin(0.16f),
                new MetaContextOld.StateButton()
                {
                    StateRef = new Reference<bool>().Set((bool)(typeof(Costume).GetField(c.fieldName)?.GetValue(costume) ?? false)),
                    OnChanged = flag => {
                        typeof(Costume).GetField(c.fieldName)?.SetValue(costume, flag);
                        updateAction?.Invoke();
                    }
                },
                new MetaContextOld.HorizonalMargin(0.08f),
                new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Size = new(0.8f, 0.2f) }) { TranslationKey = c.translationKey }
                );
        }, 3, -1, 0, 0.3f);

        context.Append(new MetaContextOld.VerticalMargin(0.12f));

        foreach (var content in contents)
        {
            if (content.variation == -1) continue;
            List<IMetaParallelPlacableOld> buttons = new();
            buttons.Add(new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(1f, 0.3f) }) { Alignment = IMetaContextOld.AlignmentOption.Right, TranslationKey = content.translationKey });
            buttons.Add(new MetaContextOld.Text(new(TextAttribute.BoldAttr) { Alignment = TMPro.TextAlignmentOptions.Center, Size = new(0.1f, 0.45f) }) { RawText = ":" });
            buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[0][content.variation], content.fieldName, addon, costumeNameRef, updateAction));
            if (content.flipName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[1][content.variation], content.flipName, addon, costumeNameRef, updateAction));
            if (content.backName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[2][content.variation], content.backName, addon, costumeNameRef, updateAction));
            if (content.backFlipName != null) buttons.Add(GetCostumeContentButton(costume, ImageContentTranslationKey[3][content.variation], content.backFlipName, addon, costumeNameRef, updateAction));

            context.Append(new CombinedContextOld(buttons.ToArray()) { Alignment = IMetaContextOld.AlignmentOption.Left });
        }
    }

    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowNameplateEditorScreen(DevAddon addon, CosmicNameplate nameplate, params (string translationKey, string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents)
    {
        MetaContextOld context = new();

        var contexts = context.Split(0.35f, 0.1f, 0.55f);

        SpriteRenderer? myRenderer = null;
        contexts[0].Append(new MetaContextOld.VerticalMargin(0.9f));
        contexts[0].Append(new MetaContextOld.CustomContext(new(2f, 4f), IMetaContextOld.AlignmentOption.Center, (transform, center) => {
            myRenderer = UnityHelper.CreateObject<SpriteRenderer>("Nameplate", transform, center);
            myRenderer.sprite = nameplate.Plate?.GetSprite(0);
        }));

        void UpdateNameplate()
        {
            myRenderer!.sprite = nameplate.Plate?.GetSprite(0);
        }

        Reference<TextField> titleRef = new(), authorRef = new();
        SetUpCommonCosmicProperty(contexts[2], addon, nameplate, titleRef, authorRef);
        SetUpCosmicContentProperty(contexts[2], nameplate, addon, titleRef, UpdateNameplate, contents);

        return (context, null, null);
    }

    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowCostumeEditorScreen<Costume>(DevAddon addon, Costume costume,params (string translationKey,string fieldName, string? flipName, string? backName, string? backFlipName, int variation)[] contents) where Costume : CustomCosmicItem
    {
        MetaContextOld context = new();

        var contexts = context.Split(0.35f, 0.1f, 0.55f);

        (var displayContext, var displayRef) = GetPlayerDisplayContext();

        void UpdateCostume()
        {
            if (costume is CosmicHat hat)
            {
                hat.MyHat.NoBounce = !hat.Bounce;
                hat.MyView.AltShader = hat.Adaptive ? MoreCosmic.AdaptiveShader : HatManager.Instance.DefaultShader;
                displayRef?.Value?.Cosmetics.SetHat(hat.MyHat, NebulaPlayerTab.PreviewColorId);
            }
            else if (costume is CosmicVisor visor)
            {
                visor.MyView.AltShader = visor.Adaptive ? MoreCosmic.AdaptiveShader : HatManager.Instance.DefaultShader;
                displayRef?.Value?.Cosmetics.SetVisor(visor.MyVisor, NebulaPlayerTab.PreviewColorId);
            }
        }

        contexts[0].Append(displayContext);

        Reference<TextField> titleRef = new(), authorRef = new();
        SetUpCommonCosmicProperty(contexts[2], addon, costume, titleRef, authorRef);
        SetUpCosmicContentProperty(contexts[2], costume, addon,titleRef, UpdateCostume, contents);

        return (context, () => {
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

    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowPackageEditorScreen(DevAddon addon, CosmicPackage package)
    {
        MetaContextOld context = new();

        Reference<TextField> titleRef = new(), keyRef = new();

        CombinedContextOld Centeralize(CombinedContextOld context)
        {
            context.Alignment = IMetaContextOld.AlignmentOption.Center;
            return context;
        }

        context.Append(new MetaContextOld.VerticalMargin(1.2f));
        context.Append(Centeralize(GetTextInputContext("devStudio.ui.cosmetics.attributes.id", "ID", titleRef, package.Package, (text) => package.Package = text)));
        context.Append(Centeralize(GetTextInputContext("devStudio.ui.cosmetics.attributes.format", "Format", keyRef, package.Format, (text) => package.Format = text)));

        return (context, null, null);
    }

    //Cosmetics
    private (IMetaContextOld context, Action? postAction, Func<bool>? confirm) ShowCosmeticsScreen(DevAddon addon)
    {
        if (addon.MyBundle == null)
        {
            Directory.CreateDirectory(addon.FolderPath + "/MoreCosmic");

            using Stream? stream = addon.OpenRead("MoreCosmic/Contents.json");

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
        

        MetaContextOld context = new();

        context.Append(new MetaContextOld.Text(new TextAttribute(TextAttribute.TitleAttr) { Font = VanillaAsset.BrookFont, Styles = TMPro.FontStyles.Normal, Size = new(3f, 0.45f) }.EditFontSize(5.2f)) { TranslationKey = "devStudio.ui.addon.cosmetics" });
        context.Append(new MetaContextOld.VerticalMargin(0.2f));

        Reference<MetaContextOld.ScrollView.InnerScreen> innerRef = new();

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
            ("devStudio.ui.cosmetics.attributes.idle", nameof(CosmicVisor.Main), null, nameof(CosmicVisor.Flip), null, 0),
            ("devStudio.ui.cosmetics.attributes.move", nameof(CosmicVisor.Move), null, nameof(CosmicVisor.MoveFlip), null, 0),
            ("devStudio.ui.cosmetics.attributes.climb", nameof(CosmicVisor.Climb), nameof(CosmicVisor.ClimbFlip), nameof(CosmicVisor.ClimbDown), nameof(CosmicVisor.ClimbDownFlip), 1),
            ("devStudio.ui.cosmetics.attributes.enterVent", nameof(CosmicVisor.EnterVent), null, nameof(CosmicVisor.EnterVentFlip), null, 0),
            ("devStudio.ui.cosmetics.attributes.exitVent", nameof(CosmicVisor.ExitVent), null, nameof(CosmicVisor.ExitVentFlip), null, 0),
            ("devStudio.ui.cosmetics.attributes.preview", nameof(CosmicVisor.Preview), null, null, null, 0)
            ));
        void OpenNameplateEditor(CosmicNameplate nameplate) => OpenScreen(() => ShowNameplateEditorScreen(addon, nameplate,
            ("devStudio.ui.cosmetics.attributes.image", nameof(CosmicNameplate.Plate), null, null, null, 0)
            ));
        void OpenPackageEditor(CosmicPackage package) => OpenScreen(() => ShowPackageEditorScreen(addon, package));

        void SetUpRightClickAction<Costume>(PassiveButton button, List<Costume> costumeList, Costume costume) where Costume : CustomItemGrouped
        {
            string name = "";
            if(costume is CustomCosmicItem item)
                name = item.UnescapedName;
            else if(costume is CosmicPackage package)
                name = package.DisplayName;
            
            button.gameObject.AddComponent<ExtraPassiveBehaviour>().OnRightClicked += () => MetaUI.ShowYesOrNoDialog(transform, () => { costumeList.Remove(costume); ReopenScreen(); }, () => { },
                Language.Translate("devStudio.ui.common.confirmDeleting") + $"<br>\"{name}\"");
        }

        var categories = new (string translationKey, Func<IMetaContextOld> contextProvider, Action contentCreator)[]
        {
            ("devStudio.ui.cosmetics.hats", () => {
                if(addon.MyBundle == null)return null!;
                MetaContextOld context = new();
                context.Append(addon.MyBundle.Hats, hat=>{
                    return new MetaContextOld.Button(()=>OpenHatEditor(hat), new(TextAttribute.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = hat.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Hats,hat);

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.35f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.35f * 2f,0f);
                            GenerateSprite((hat.Preview ?? hat.Main ?? hat.Move)?.GetSprite(0), (hat.Main != null && hat.Preview == null) ? hat.Back?.GetSprite(0) : null,renderer.transform , hat.Adaptive, 0.3f,-1.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return context;
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
                MetaContextOld context = new();
                context.Append(addon.MyBundle.Visors, visor=>{
                    return new MetaContextOld.Button(()=>OpenVisorEditor(visor), new(TextAttribute.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = visor.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Visors,visor);

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.35f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.35f * 2f,0f);
                            GenerateSprite((visor.Preview ?? visor.Main ?? visor.Move)?.GetSprite(0), null,renderer.transform , visor.Adaptive,0.3f,-1.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return context;
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
                MetaContextOld context = new();
                context.Append(addon.MyBundle.Nameplates, nameplate=>{
                    return new MetaContextOld.Button(()=>OpenNameplateEditor(nameplate), new(TextAttribute.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial })
                    {
                        RawText = nameplate.UnescapedName,
                        PostBuilder = (button,renderer,text) => {
                            SetUpRightClickAction(button,addon.MyBundle.Nameplates,nameplate);

                            renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                            text.transform.localPosition += new Vector3(0.46f,0f,0f);
                            text.rectTransform.sizeDelta -= new Vector2(0.46f * 2f,0f);
                            GenerateSprite(nameplate.Plate?.GetSprite(0), null,renderer.transform , false,0.3f);
                        }
                    };
                },2,-1,0,0.65f);
                return context;
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
                MetaContextOld context = new();
                context.Append(addon.MyBundle.Packages, package=>{
                    return new MetaContextOld.Button(()=>OpenPackageEditor(package), new(TextAttribute.BoldAttrLeft){ Size = new(3.2f,0.42f), FontMaterial = VanillaAsset.StandardMaskedFontMaterial})
                    { RawText = package.DisplayName, PostBuilder = (button,renderer,_)=>{
                        SetUpRightClickAction(button,addon.MyBundle.Packages,package);
                        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                    } };
                },2,-1,0,0.65f);
                return context;
            },()=>{
                CosmicPackage package = new();
                package.Package = "";
                package.Format = "";
                addon.MyBundle.Packages.Add(package);
                OpenPackageEditor(package);
            })
        };


        var currentSelection = categories[0];


        context.Append(categories, category =>
        {
            return new MetaContextOld.Button(() => {
                currentSelection = category;
                innerRef.Value!.SetContext(category.contextProvider.Invoke());
            }, TextAttribute.BoldAttr) { TranslationKey = category.translationKey };
        }, categories.Length, 1, 0, 0.6f);

        context.Append(new MetaContextOld.Button(()=>currentSelection.contentCreator.Invoke(), new(TextAttribute.BoldAttr) { Size = new(0.35f, 0.2f) }) { RawText = "+" });


        context.Append(new MetaContextOld.ScrollView(new(8f, 4f), new MetaContextOld()) { Alignment = IMetaContextOld.AlignmentOption.Center, InnerRef = innerRef });


        return (context, () => { innerRef.Value!.SetContext(currentSelection.contextProvider.Invoke()); }, () =>
        {
            File.WriteAllText(addon.FolderPath+"/MoreCosmic/Contents.json", addon.MyBundle.Serialize());
            return true;
        }
        );
    }
}

public class DevAddon : INameSpace
{
    public string Name { get; private set; }
    public string FolderPath { get; private set; }
    public string Id { get; private set; }

    public CustomItemBundle? MyBundle = null;

    private AddonMeta? addonMeta;
    public AddonMeta MetaSetting { get {
            if (addonMeta == null)
            {
                addonMeta = (AddonMeta?)JsonStructure.Deserialize(File.ReadAllText(FolderPath+"/addon.meta"), typeof(AddonMeta)) ??
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

    public DevAddon(string name,string folderPath)
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
        File.Move(tempPath,zipPath);
    }

    public void SaveMetaSetting()
    {
        Name = MetaSetting.Name;
        File.WriteAllText(FolderPath + "/addon.meta", MetaSetting.Serialize());
    }

    public async Task<(string path,string id,SerializableDocument doc)[]> LoadDocumentsAsync()
    {
        if (!Directory.Exists(FolderPath + "/Documents")) return new (string, string, SerializableDocument)[0];

        List<(string path, string id, SerializableDocument doc)> result = new();
        foreach (var path in Directory.GetFiles(FolderPath + "/Documents"))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            var doc = JsonStructure.Deserialize<SerializableDocument>(await File.ReadAllTextAsync(path));
            if(doc == null) continue;

            doc.RelatedNamespace = this;
            result.Add((path, id, doc));
        }
        return result.ToArray();
    }

    private Stream? OpenRead(string folder, string innerAddress)
    {
        if (File.Exists(folder + "/" + innerAddress)) return File.OpenRead(folder + "/" + innerAddress);
        

        foreach (var dir in Directory.GetDirectories(folder))
        {
            string lowestDir = dir.Substring(folder.Length + 1);
            if (innerAddress.Length > (lowestDir.Length) && innerAddress[lowestDir.Length] is '.' && innerAddress.StartsWith(lowestDir))
            {
                var stream = OpenRead(dir, innerAddress.Substring(lowestDir.Length + 1));
                if (stream != null) return stream;
            }
        }

        return null;
    }

    public Stream? OpenRead(string innerAddress)
    {
        try
        {
            return OpenRead(FolderPath,innerAddress);
        }
        catch
        {
            return null;
        }
    }

}