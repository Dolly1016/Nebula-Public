using Nebula.Behavior;
using Nebula.Commands;
using Virial.Command;
using Virial.Common;
using Virial.Helpers;

namespace Nebula.Modules;


public class CommandConsole
{
    private class GuestExecutor : ICommandExecutor, IPermissionHolder
    {
        bool IPermissionHolder.Test(Virial.Common.Permission permission) => false;
    }

    TextField myInput;
    GameObject consoleObject;

    ConsoleShower log;

    public bool IsShown { get => consoleObject.active; set => consoleObject.SetActive(value); }

    static private ICommandExecutor Guest = new GuestExecutor();
    public CommandConsole()
    {
        var uiCamTransform = UnityHelper.FindCamera(LayerExpansion.GetUILayer())!.transform;
        consoleObject = UnityHelper.CreateObject("CommandConsole", uiCamTransform, new Vector3(-2.15f, -2.8f, -800f), LayerExpansion.GetUILayer());
        
        log = UnityHelper.CreateObject<ConsoleShower>("ConsoleLog", uiCamTransform, new Vector3(-2.15f, -2.8f, -800f) + new Vector3(0.05f, 0.3f, 0f));
        log.ConsoleInputHolder = consoleObject;

        Vector2 size = new Vector2(6f, 0.225f);

        myInput = UnityHelper.CreateObject<TextField>("InputField", consoleObject.transform, new Vector3(0, 0, -1f));
        myInput.SetSize(size,1.6f);

        myInput.EnterAction = (text) =>
        {
            if (text == "")
            {
                IsShown = false;
                return true;
            }

            var myLogger = new NebulaCommandLogger(text);
            log.Push(myLogger);

            IEnumerator CoExecute()
            {
                IsShown = false;
                yield return CommandManager.CoExecute(CommandManager.ParseRawCommand(text), new(GamePlayer.LocalPlayer ?? Guest, ThroughCommandModifier.Modifier, myLogger)).CoWait().HighSpeedEnumerator();
            }
            NebulaManager.Instance.StartCoroutine(CoExecute().WrapToIl2Cpp());

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