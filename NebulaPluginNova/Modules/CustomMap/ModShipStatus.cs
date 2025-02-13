using Il2CppInterop.Runtime.Injection;
using UnityEngine.Rendering;

namespace Nebula.Modules.CustomMap;

public class ModShipStatus 
{
    public static void CleanOriginalShip(ShipStatus ship)
    {
        Helpers.Sequential(ship.transform.GetChildCount()).Select(i => ship.transform.GetChild(i)).ToArray().Do(
            child =>
            {
                child.SetParent(null);
                GameObject.Destroy(child.gameObject);
            }
            );

        GameObject GenerateRoom(string name,Vector2 pos, Image background, Image doorMask, out SortingGroup doorMaskGroup)
        {
            var roomObj = UnityHelper.CreateObject(name, null, pos.AsVector3(pos.y / 1000f));
            var backRenderer = UnityHelper.CreateObject<SpriteRenderer>("background", roomObj.transform, new(0f, 0f, 3f));
            backRenderer.sprite = background.GetSprite();

            doorMaskGroup = UnityHelper.CreateObject<SortingGroup>("doorMaskGroup", roomObj.transform,Vector3.zero);
            var doorMaskRenderer = UnityHelper.CreateObject<SpriteRenderer>("masked", doorMaskGroup.transform, new(0f, 0f, 2f));
            doorMaskRenderer.sprite = doorMask.GetSprite();
            doorMaskRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            return roomObj;
        }
        var upperRoom = GenerateRoom("UpperRoom", new(0f,2.8f), SpriteLoader.FromResource("Nebula.Resources.Map.Room1.png", 100f), SpriteLoader.FromResource("Nebula.Resources.Map.Room1DoorMask.png", 100f), out var upperGroup);
        var lowerRoom = GenerateRoom("LowerRoom", new(0f,0f), SpriteLoader.FromResource("Nebula.Resources.Map.Room2.png", 100f), SpriteLoader.FromResource("Nebula.Resources.Map.Room2DoorMask.png", 100f), out _);
        var door = GameObject.Instantiate(VanillaAsset.MapAsset[4].AllDoors[2], null);
        door.transform.localScale = new(0.7f, 0.7f, 1f);
        door.transform.localPosition = new(0f, 1.38f, 1f);
        var doorMask = UnityHelper.CreateObject<SpriteMask>("Mask", door.transform, new(0f, 0.1f, 0f));
        doorMask.sprite = SpriteLoader.FromResource("Nebula.Resources.Map.AirshipDoorNonvertMask.png", 100f).GetSprite();
        doorMask.transform.localScale = Vector3.one;
        doorMask.transform.SetParent(upperGroup.transform);
        


        //ship.
    }
}
