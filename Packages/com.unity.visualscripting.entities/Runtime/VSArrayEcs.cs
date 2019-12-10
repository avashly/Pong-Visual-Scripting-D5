using System;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities;

[PublicAPI]
public static class VSArrayExtension
{
    public static Entity GetElement(this NativeArray<Entity> array, int index)
    {
        return array[index];
    }

    public static T GetElement<T>(this NativeArray<T> array, int index) where T : struct, IComponentData
    {
        return array[index];
    }
}
