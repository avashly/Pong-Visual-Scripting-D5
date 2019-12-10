using System;
using Unity.Collections;
using Unity.Entities;

public class EventSystem<T> : ComponentSystem where T : struct, IBufferElementData
{
    public static void Initialize(World world)
    {
        var tes = world.GetOrCreateSystem<EventSystem<T>>();
        world.GetExistingSystem<InitializationSystemGroup>().AddSystemToUpdateList(tes);
    }

    public static void AddMissingBuffers(EntityQueryBuilder builder, EntityQuery query, EntityManager entityManager)
    {
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        builder.With(query).ForEach(e => ecb.AddBuffer<T>(e));
        ecb.Playback(entityManager);
        ecb.Dispose();
    }

    protected override void OnUpdate()
    {
        Entities.ForEach((DynamicBuffer<T> b) => b.Clear());
    }
}
