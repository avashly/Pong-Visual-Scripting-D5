using System;
using Unity.Entities;
using UnityEngine;

public static class EcsHelper
{
    public static T GetOrCreateComponentData<T>(this EntityManager manager, Entity e) where T : struct, IComponentData
    {
        if (manager.HasComponent<T>(e))
            return manager.GetComponentData<T>(e);
        T comp = default(T);
        manager.AddComponentData(e, comp);
        return comp;
    }
}

/*
[Node]
public static class EcsHelper
{
    public static NativeArray<Entity> CreateEntities(int count)
    {
        return new NativeArray<Entity>(count, Allocator.Temp);
    }

    public static void SetEntityPosition(EntityManager em, Entity sourceEntity, NativeArray<Entity> entities, NativeArray<float3> positions, int count, bool spawnLocal)
    {
        if (spawnLocal)
        {
            for (var i = 0; i < count; i++)
            {
                var position = new LocalPosition
                {
                    Value = positions[i]
                };
                em.SetComponentData(entities[i], position);
                em.AddComponentData(entities[i], new TransformParent { Value = sourceEntity});
            }
        }
        else
        {
            for (var i = 0; i < count; i++)
            {
                var position = new TransformParent
                {
                    Value = positions[i]
                };
                em.SetComponentData(entities[i], position);
            }
        }
    }
}
*/
