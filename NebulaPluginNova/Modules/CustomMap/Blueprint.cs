﻿namespace Nebula.Modules.CustomMap;

public class SerializableVector
{
    [JsonSerializableField]
    public float X;
    [JsonSerializableField]
    public float Y;
    [JsonSerializableField(true)]
    public float? Z;
}

public class SerializableBlueprint
{
    [JsonSerializableField(true)]
    public SerializableVector? Position;

}

/*
public class ImageAsset
{
    [JsonSerializableField]
    string? Path = null;

    [JsonSerializableField]
    int Length = 1;
}

public class BlueprintChain
{
    [JsonSerializableField]
    Vector2 LocalPos = new(0f, 0f);

    [JsonSerializableField]
    public string? Name = null;

    Blueprint? cache = null;
    
}

public class BlueprintShipRoom
{
    [JsonSerializableField]
    Vector2[]? RoomArea = null;

    [JsonSerializableField]
    public string? TranslationKey = null;
}

public class Blueprint
{
    [JsonSerializableField]
    Vector2[][]? Colliders = null;

    [JsonSerializableField]
    Vector2[][]? Shadows = null;

    [JsonSerializableField]
    BlueprintShipRoom? ShipRoom = null;

    [JsonSerializableField]
    int OrderZ = 0;

    [JsonSerializableField]
    BlueprintChain[]? Children = null;
}

*/