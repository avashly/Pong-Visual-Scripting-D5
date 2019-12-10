using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

[DotsEvent, InternalBufferCapacity(1)]
public struct UnitTestEvent : IBufferElementData
{
    public float i;
}

namespace UnityEditor.VisualScriptingECSTests
{
    public class EventCodeGenTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void OnEventTest([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, g =>
            {
                ComponentQueryDeclarationModel query = GraphModel.CreateComponentQuery("g1");
                TypeHandle positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);
                IVariableModel queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                TypeHandle eventTypeHandle = typeof(UnitTestEvent).GenerateTypeHandle(Stencil);

                OnEventNodeModel onUpdateModel = GraphModel.CreateNode<OnEventNodeModel>("update", Vector2.zero, SpawnFlags.Default, n => n.EventTypeHandle = eventTypeHandle);

                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                SetPropertyGroupNodeModel set = onUpdateModel.CreateStackedNode<SetPropertyGroupNodeModel>("set", 0);
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                set.AddMember(member);

                IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
            },

                // Add translation to even entities
                EachEntity((manager, i, e) =>
                {
                    if (e.Index % 2 == 0)
                        manager.AddComponent(e, typeof(Translation));
                }),

                // Send event on these
                EachEntity(del: (manager, i, e) =>
                {
                    if (e.Index % 2 == 0)
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f));
                    else
                        Assert.IsFalse(manager.HasComponent<Translation>(e));

                    // Add event on all entities
                    manager.AddBuffer<UnitTestEvent>(e).Add(new UnitTestEvent());
                }),

                // Event handler should set t.x to 2
                EachEntity((manager, i, e) =>
                {
                    if (e.Index % 2 == 0)
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f));
                }),

                // Reset translation
                EachEntity((manager, i, e) =>
                {
                    if (e.Index % 2 == 0)
                    {
                        var t = manager.GetComponentData<Translation>(e);
                        t.Value.x = 0;
                        manager.SetComponentData(e, t);
                    }
                }),

                // As we send events manually, the event system is not running and won't cleanup events.
                // Event handler should set t.x to 2 again
                EachEntity((manager, i, e) =>
                {
                    if (e.Index % 2 == 0)
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f));
                })
            );
        }

        [Test]
        public void SendEventTest([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, g =>
            {
                ComponentQueryDeclarationModel query = GraphModel.CreateComponentQuery("g1");
                TypeHandle positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);
                IVariableModel queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);

                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                TypeHandle eventTypeHandle = typeof(UnitTestEvent).GenerateTypeHandle(Stencil);

                SendEventNodeModel set = onUpdateModel.CreateStackedNode<SendEventNodeModel>("set", 0, SpawnFlags.Default, n => n.EventType = eventTypeHandle);

                TypeHandle entityType = typeof(Entity).GenerateTypeHandle(Stencil);
                IVariableModel entityVar = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == entityType), Vector2.zero);

                GraphModel.CreateEdge(set.EntityPort, entityVar.OutputPort);
                var firstFieldInput = set.FieldInputs.First();
                ((FloatConstantModel)set.InputConstantsById[firstFieldInput.UniqueId]).value = 2f;
            },

                // Add translation to even entities
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponent(e, typeof(Translation));
                    manager.World.CreateSystem<InitializationSystemGroup>();
                }),
                (manager, entities) =>
                {
                    EventSystem<UnitTestEvent> eventSystem = manager.World.GetExistingSystem<EventSystem<UnitTestEvent>>();
                    Assert.That(eventSystem, Is.Not.Null);
                    eventSystem.Update();
                },

                // OnUpdate should have added a buffer and one event
                EachEntity((manager, i, e) =>
                {
                    DynamicBuffer<UnitTestEvent> buffer = default;
                    Assert.DoesNotThrow(() => buffer = manager.GetBuffer<UnitTestEvent>(e));
                    Assert.That(buffer.IsCreated, Is.True);
                    Assert.That(buffer.Length, Is.EqualTo(1));
                    Assert.That(buffer[0].i, Is.EqualTo(2f));
                })
            );
        }

        [Test]
        public void SendEventNestedTest([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graphModel =>
            {
                // query1 - Position
                var query1 = graphModel.CreateComponentQuery("g1");
                query1.AddComponent(graphModel.Stencil, typeof(Translation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var query1Instance = graphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Scale (will add RenderMesh)
                var query2 = graphModel.CreateComponentQuery("g2");
                query2.AddComponent(graphModel.Stencil, typeof(Scale).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var query2Instance = graphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(onUpdateEntities.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdateEntities, 0) as ForAllEntitiesNodeModel;
                graphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                graphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                TypeHandle eventTypeHandle = typeof(UnitTestEvent).GenerateTypeHandle(Stencil);
                SendEventNodeModel set = forAllStack.CreateStackedNode<SendEventNodeModel>("set", 0, SpawnFlags.Default, n => n.EventType = eventTypeHandle);

                TypeHandle entityType = typeof(Entity).GenerateTypeHandle(Stencil);
                IVariableModel entityVar = GraphModel.CreateVariableNode(forAllStack.FunctionParameterModels.Single(p => p.DataType == entityType), Vector2.zero);
                var firstFieldInput = set.EntityPort;
                GraphModel.CreateEdge(firstFieldInput, entityVar.OutputPort);
            }
            );
        }
    }
}
