namespace Nebula.Utilities;

/*
public interface INameSpace : Virial.Assets.INameSpace
{
    ISpriteLoader? GetSprite(string innerAddress, float pixelsPerUnit = 100f)
    {
        var stream = OpenRead(innerAddress + ".png");
        return stream != null ? new SpriteLoader(new StreamTextureLoader(stream), pixelsPerUnit) : null;
    }

    Image? Virial.Assets.INameSpace.GetImage(string innerAddress, float pixelsPerUnit) => GetSprite(innerAddress, pixelsPerUnit);
}

public class NameSpaceManager
{
    public class NebulaNameSpace : INameSpace
    {
        public Stream? OpenRead(string innerAddress)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream("Nebula.Resources." + innerAddress);
        }

        public ISpriteLoader? GetSprite(string innerAddress,float pixelsPerUnit = 100f)
        {
            return SpriteLoader.FromResource("Nebula.Resources." + innerAddress + ".png", pixelsPerUnit);
        }
    }

    public class VanillaNameSpace : INameSpace
    {
        private Dictionary<string, Func<Sprite>> spriteDoc = new();

        public VanillaNameSpace()
        {
            spriteDoc["Buttons.VitalsButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.VitalsButton].Image : null!;
            spriteDoc["Buttons.SkeldAdminButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.AdminMapButton].Image : null!;
            spriteDoc["Buttons.MIRAAdminButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.MIRAAdminButton].Image : null!;
            spriteDoc["Buttons.PolusAdminButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.PolusAdminButton].Image : null!;
            spriteDoc["Buttons.AirshipAdminButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.AirshipAdminButton].Image : null!;
            spriteDoc["Buttons.CameraButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.CamsButton].Image : null!;
            spriteDoc["Buttons.DoorLogsButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.DoorLogsButton].Image : null!;
            spriteDoc["Buttons.KillButton"] = () => HudManager.InstanceExists ? HudManager.Instance.KillButton.graphic.sprite : null!;
            spriteDoc["Buttons.UseButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.UseButton].Image : null!;
            spriteDoc["Buttons.PetButton"] = () => HudManager.InstanceExists ? HudManager.Instance.UseButton.fastUseSettings[ImageNames.PetButton].Image : null!;
        }
        public Stream? OpenRead(string innerAddress)
        {
            return null;
        }

        public ISpriteLoader? GetSprite(string innerAddress, float pixelsPerUnit = 100f)
        {
            Debug.Log("test:" + innerAddress);
            return new WrapSpriteLoader(() => spriteDoc.TryGetValue(innerAddress, out var sprite) ? sprite.Invoke() : null!);
        }
    }

    public static INameSpace DefaultNameSpace { get; private set; } = new NebulaNameSpace();
    public static INameSpace InnerslothNameSpace { get; private set; } = new VanillaNameSpace();
    public static INameSpace? Resolve(string name)
    {
        if (name == "Nebula") return DefaultNameSpace;
        if (name == "Innersloth") return InnerslothNameSpace;

        var addonSpace = NebulaAddon.GetAddon(name);
        if (addonSpace != null) return addonSpace;

        return null;
    }

    public static INameSpace ResolveOrGetDefault(string name) => Resolve(name) ?? DefaultNameSpace;
}

*/