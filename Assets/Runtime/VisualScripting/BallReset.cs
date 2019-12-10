using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class BallReset : ComponentSystem
{
    private Unity.Entities.EntityQuery Ball_Query;
    private Unity.Entities.EntityQuery Ball_Query0;
    public struct GraphData : Unity.Entities.IComponentData
    {
        public float xBoundaryValue;
    }

    protected override void OnCreate()
    {
        Ball_Query = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Translation>(), ComponentType.ReadOnly<Unity.Physics.PhysicsVelocity>(), ComponentType.ReadOnly<BallTag>());
        Ball_Query0 = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Translation>(), ComponentType.ReadOnly<Unity.Physics.PhysicsVelocity>(), ComponentType.ReadOnly<BallTag>());
        EntityManager.CreateEntity(typeof (GraphData));
        SetSingleton(new GraphData{xBoundaryValue = 12F});
    }

    protected override void OnUpdate()
    {
        GraphData graphData = GetSingleton<GraphData>();
        {
            Entities.With(Ball_Query).ForEach((Unity.Entities.Entity Ball_QueryEntity, ref Unity.Transforms.Translation Ball_QueryTranslation) =>
            {
                if ((Ball_QueryTranslation.Value.x <= math.mul(graphData.xBoundaryValue, -1F)))
                {
                    Ball_QueryTranslation.Value = new float3(0F, 0F, 0F);
                }
            }

            );
        }

        {
            Entities.With(Ball_Query0).ForEach((Unity.Entities.Entity Ball_QueryEntity, ref Unity.Transforms.Translation Ball_Query0Translation) =>
            {
                if ((Ball_Query0Translation.Value.x >= graphData.xBoundaryValue))
                {
                    Ball_Query0Translation.Value = new float3(0F, 0F, 0F);
                }
            }

            );
        }
    }
}