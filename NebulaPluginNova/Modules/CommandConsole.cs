using BepInEx.Unity.IL2CPP.UnityEngine;
using Mono.CSharp;
using Nebula.Behaviour;
using Nebula.Scripts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Nebula.Modules;

public class MyInteraction : InteractiveBase
{
}

public class CommandConsole
{
    TextField myInput;
    GameObject consoleObject;
    public bool IsShown { get => consoleObject.active; set => consoleObject.SetActive(value); }

    public CommandConsole()
    {
        consoleObject = UnityHelper.CreateObject("CommandConsole", UnityHelper.FindCamera(LayerExpansion.GetUILayer())!.transform, new Vector3(-2.15f, -2.8f, -800f), LayerExpansion.GetUILayer());

        Vector2 size = new Vector2(6f, 0.225f);

        myInput = UnityHelper.CreateObject<TextField>("InputField", consoleObject.transform, new Vector3(0, 0, -1f));
        myInput.SetSize(size,1.6f);

        //おためしでMono.CSharpを使えるようにしています。

        CSScripting script = new();

        myInput.EnterAction = (text) =>
        {
            script.PopLogText();
            if (!script.Evaluate(text, out var error)) Debug.Log(error);
            Debug.Log(script.PopLogText());

            myInput.SetText("");
            return false;
        };

        var backGround = UnityHelper.CreateObject<SpriteRenderer>("Background", myInput.transform, new Vector3(0, 0, 1f));
        backGround.sprite = NebulaAsset.SharpWindowBackgroundSprite.GetSprite();
        backGround.drawMode = SpriteDrawMode.Sliced;
        backGround.tileMode = SpriteTileMode.Continuous;
        backGround.size = size + new Vector2(0.15f, 0.008f);
        backGround.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);
    }

    public void GainFocus()
    {
        myInput.GainFocus();
    }
}
