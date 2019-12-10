using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Microsoft.CSharp;
using UnityEngine;

public class PlayerControl : ComponentSystem
{
    private Unity.Entities.EntityQuery LeftPaddle_Query;
    private Unity.Entities.EntityQuery RightPaddle_Query;
    private Unity.Entities.EntityQuery Sphere_QueryEnter;
    public struct Sphere_QueryTracking : Unity.Entities.ISystemStateComponentData
    {
    }

    public struct GraphData : Unity.Entities.IComponentData
    {
        public float speed;
        public float leftPaddlePosition;
        public float rightPaddlePosition;
        public float topClamp;
        public float bottomClamp;
    }

    protected override void OnCreate()
    {
        LeftPaddle_Query = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Translation>(), ComponentType.ReadOnly<PlayerTag>());
        RightPaddle_Query = GetEntityQuery(ComponentType.ReadWrite<Unity.Transforms.Translation>(), ComponentType.ReadOnly<Player2Tag>());
        Sphere_QueryEnter = GetEntityQuery(ComponentType.Exclude<Sphere_QueryTracking>(), ComponentType.ReadOnly<Unity.Physics.PhysicsVelocity>(), ComponentType.ReadOnly<BallTag>());
        EntityManager.CreateEntity(typeof (GraphData));
        SetSingleton(new GraphData{speed = 10F, leftPaddlePosition = -8F, rightPaddlePosition = 8F, topClamp = 4F, bottomClamp = -2F});
    }

    protected override void OnUpdate()
    {
        GraphData graphData = GetSingleton<GraphData>();
        {
            Entities.With(LeftPaddle_Query).ForEach((Unity.Entities.Entity LeftPaddle_QueryEntity, ref Unity.Transforms.Translation LeftPaddle_QueryTranslation) =>
            {
                LeftPaddle_QueryTranslation.Value.x = graphData.leftPaddlePosition;
                LeftPaddle_QueryTranslation.Value.y = math.clamp((LeftPaddle_QueryTranslation.Value.y + math.mul(UnityEngine.Input.GetAxis("Vertical"), math.mul(graphData.speed, Time.deltaTime))), graphData.bottomClamp, graphData.topClamp);
                LeftPaddle_QueryTranslation.Value.z = 0F;
            }

            );
        }

        {
            Entities.With(RightPaddle_Query).ForEach((Unity.Entities.Entity RightPaddle_QueryEntity, ref Unity.Transforms.Translation RightPaddle_QueryTranslation) =>
            {
                RightPaddle_QueryTranslation.Value.x = graphData.rightPaddlePosition;
                RightPaddle_QueryTranslation.Value.y = math.clamp((RightPaddle_QueryTranslation.Value.y + math.mul(UnityEngine.Input.GetAxis("VerticalP2"), math.mul(graphData.speed, Time.deltaTime))), graphData.bottomClamp, graphData.topClamp);
                RightPaddle_QueryTranslation.Value.z = 0F;
            }

            );
        }

        {
            Entities.With(Sphere_QueryEnter).ForEach((Unity.Entities.Entity Sphere_QueryEntity) =>
            {
                PostUpdateCommands.SetComponent<Unity.Physics.PhysicsVelocity>(Sphere_QueryEntity, new Unity.Physics.PhysicsVelocity{Linear = new float3(-10F, 0F, 0F), Angular = new float3(0F, 0F, 0F)});
                PostUpdateCommands.AddComponent<Sphere_QueryTracking>(Sphere_QueryEntity, default (Sphere_QueryTracking));
            }

            );
        }
    }
}