namespace UniRes
{
    internal enum BinaryVariantType : byte
    {
        Null = 0,
        Bool = 1,
        Int64 = 2,
        Double = 3,
        Float = 4,
        String = 5,
        Vector2 = 6,
        Vector3 = 7,
        Vector4 = 8,
        Quaternion = 9,
        Color = 10,
        Vector2Int = 11,
        Vector3Int = 12,
        ByteArray = 13,
        Int32Array = 14,
        Int64Array = 15,
        FloatArray = 16,
        DoubleArray = 17,
        StringArray = 18,
        GenericSequence = 19,
        Dictionary = 20,
        ExternalResourceRef = 21,
        EmbeddedResourceRef = 22,
    }

    internal interface IBinaryResourceReferenceWriter
    {
        (ResourceRefKind Kind, string Id) GetReference(Resource resource);
    }
}
