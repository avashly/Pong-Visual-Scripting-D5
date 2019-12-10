using System;
using JetBrains.Annotations;
using Unity.Entities;
using Unity.Mathematics;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
[MaximumChunkCapacity(128)]
[Serializable]
public struct DummySharedComponent : ISharedComponentData
{
    public int Value;
}

[Serializable]
public struct DummyFloat3Component : IComponentData
{
    public float3 Value;
}

[Serializable]
public struct DummyBoolComponent : IComponentData
{
    public bool Value;
}
