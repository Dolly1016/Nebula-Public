using AmongUs.Data.Player;

namespace Nebula.Modules.Cosmetics;

internal static class CosmeticSaveData
{
    private class Slot
    {
        public StringDataEntry Entry { get; }
        public Func<PlayerCustomizationData, string> Get { get; }
        public Action<PlayerCustomizationData, string> Set { get; }
        public string VanillaValue { get; set; } = "";

        public Slot(string name, Func<PlayerCustomizationData, string> get, Action<PlayerCustomizationData, string> set)
        {
            Entry = new(name, MySaver, "");
            Get = get;
            Set = set;
        }
    }

    static private readonly DataSaver MySaver = new("Cosmetics");
    static private readonly BooleanDataEntry MigratedEntry = new("migrated", MySaver, false);

    static private readonly Slot HatSlot = new("hat", c => c.hat, (c, v) => c.hat = v);
    static private readonly Slot SkinSlot = new("skin", c => c.skin, (c, v) => c.skin = v);
    static private readonly Slot VisorSlot = new("visor", c => c.visor, (c, v) => c.visor = v);
    static private readonly Slot PetSlot = new("pet", c => c.pet, (c, v) => c.pet = v);
    static private readonly Slot NamePlateSlot = new("namePlate", c => c.namePlate, (c, v) => c.namePlate = v);
    static private readonly Slot[] AllSlots = [HatSlot, SkinSlot, VisorSlot, PetSlot, NamePlateSlot];

    static private bool IsWritingVanilla = false;
    static private bool IsLoaded = false;

    static internal void OnLoaded(PlayerCustomizationData data)
    {
        IsWritingVanilla = false;
        foreach (var slot in AllSlots) slot.VanillaValue = slot.Get(data) ?? "";
        IsLoaded = true;

        if (!MigratedEntry.Value)
        {
            using (new DataSaveSegment())
            {
                foreach (var slot in AllSlots) slot.Entry.Value = slot.VanillaValue;
                MigratedEntry.Value = true;
            }
        }

        foreach (var slot in AllSlots) slot.Set(data, slot.Entry.Value);
    }

    static private void OnChanged(Slot slot, PlayerCustomizationData data)
    {
        if (!IsLoaded || IsWritingVanilla) return;
        var value = slot.Get(data) ?? "";
        if (slot.Entry.Value != value) slot.Entry.Value = value;
    }

    static internal void OnHatChanged(PlayerCustomizationData data) => OnChanged(HatSlot, data);
    static internal void OnSkinChanged(PlayerCustomizationData data) => OnChanged(SkinSlot, data);
    static internal void OnVisorChanged(PlayerCustomizationData data) => OnChanged(VisorSlot, data);
    static internal void OnPetChanged(PlayerCustomizationData data) => OnChanged(PetSlot, data);
    static internal void OnNamePlateChanged(PlayerCustomizationData data) => OnChanged(NamePlateSlot, data);

    static internal void OnSaveStart(PlayerCustomizationData data)
    {
        if (!IsLoaded || IsWritingVanilla) return;
        IsWritingVanilla = true;
        foreach (var slot in AllSlots) slot.Set(data, slot.VanillaValue);
    }

    static internal void OnSaveComplete(PlayerCustomizationData data)
    {
        if (!IsWritingVanilla) return;
        IsWritingVanilla = false;
        foreach (var slot in AllSlots) slot.Set(data, slot.Entry.Value);
    }
}

[HarmonyPatch(typeof(PlayerData), nameof(PlayerData.OnLoadComplete))]
file static class PlayerDataLoadCosmeticPatch
{
    static void Postfix(PlayerData __instance) => CosmeticSaveData.OnLoaded(__instance.customization);
}

//書き出し(JSON化とファイル書き込み)の前後だけ、フィールドをバニラの値にする。
//HandleSaveはSettingsDataと共有しているので、PlayerDataの場合のみ処理する。
[HarmonyPatch(typeof(AmongUs.Data.AbstractUserSaveData), nameof(AmongUs.Data.AbstractUserSaveData.HandleSave))]
file static class PlayerDataHandleSaveCosmeticPatch
{
    static void Prefix(AmongUs.Data.AbstractUserSaveData __instance)
    {
        var playerData = __instance.TryCast<PlayerData>();
        if (playerData != null) CosmeticSaveData.OnSaveStart(playerData.customization);
    }

    static void Postfix(AmongUs.Data.AbstractUserSaveData __instance)
    {
        var playerData = __instance.TryCast<PlayerData>();
        if (playerData != null) CosmeticSaveData.OnSaveComplete(playerData.customization);
    }
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Hat), MethodType.Setter)]
file static class HatSetterPatch
{
    static void Postfix(PlayerCustomizationData __instance) => CosmeticSaveData.OnHatChanged(__instance);
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Skin), MethodType.Setter)]
file static class SkinSetterPatch
{
    static void Postfix(PlayerCustomizationData __instance) => CosmeticSaveData.OnSkinChanged(__instance);
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Visor), MethodType.Setter)]
file static class VisorSetterPatch
{
    static void Postfix(PlayerCustomizationData __instance) => CosmeticSaveData.OnVisorChanged(__instance);
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.Pet), MethodType.Setter)]
file static class PetSetterPatch
{
    static void Postfix(PlayerCustomizationData __instance) => CosmeticSaveData.OnPetChanged(__instance);
}

[HarmonyPatch(typeof(PlayerCustomizationData), nameof(PlayerCustomizationData.NamePlate), MethodType.Setter)]
file static class NamePlateSetterPatch
{
    static void Postfix(PlayerCustomizationData __instance) => CosmeticSaveData.OnNamePlateChanged(__instance);
}
