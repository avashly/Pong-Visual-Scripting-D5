using System;
using NUnit.Framework;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests
{
    public class BaseLineTests : EndToEndCodeGenBaseFixture
    {
        [DisableAutoCreation]
        class DummySystem : JobComponentSystem
        {
            struct Job : IJobForEach<Translation>
            {
                public void Execute(ref Translation c0)
                {
                    c0.Value.x++;
                }
            }

            protected override JobHandle OnUpdate(JobHandle inputDeps)
            {
                Debug.Log("run");
                return new Job().Schedule(this, inputDeps);
            }
        }

        protected override bool CreateGraphOnStartup => false;

        [Test]
        public void SystemTestingWorks()
        {
            SetupTestSystem(typeof(DummySystem), entityManager =>
            {
                var e = entityManager.CreateEntity(ComponentType.ReadWrite<Translation>());
                entityManager.SetComponentData(e, new Translation { Value = { x = 1 } });

                Assert.That(entityManager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(1));
                return e;
            }, (entity, manager) => Assert.That(manager.GetComponentData<Translation>(entity).Value.x, Is.EqualTo(2)));
        }
    }
}
