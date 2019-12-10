using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Translators
{
    class CommandBufferTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestRetrievingConcurrentCommandBuffer([Values(CodeGenMode.Jobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                // We instantiate 2 nodes that need a command buffer
                // Only 1 command buffer should be declared

                // Create componentQuery
                var dummyF3Type = typeof(Translation).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, dummyF3Type, ComponentDefinitionFlags.None);

                // On update
                var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(onUpdateEntities.InstancePort, queryInstance.OutputPort);

                // Create entity instance
                var entityInstance = graphModel.CreateVariableNode(
                    onUpdateEntities.FunctionParameterModels.Single(
                        p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                        ),
                    Vector2.zero);

                // Add Component
                var addComponent = onUpdateEntities.CreateStackedNode<AddComponentNodeModel>("add");
                addComponent.ComponentType = typeof(Scale).GenerateTypeHandle(graphModel.Stencil);
                graphModel.CreateEdge(addComponent.EntityPort, entityInstance.OutputPort);

                // Destroy Entity
                var destroy = onUpdateEntities.CreateStackedNode<DestroyEntityNodeModel>("destroy");
                graphModel.CreateEdge(destroy.EntityPort, entityInstance.OutputPort);
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Translation)),
                (manager, entityIndex, e) =>
                {
                    // Assert only 1 EndFrameBarrier field has been created
                    var efbFields = m_SystemType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(f => f.FieldType == typeof(EndSimulationEntityCommandBufferSystem));
                    Assert.AreEqual(1, efbFields.Count());

                    // Assert only 1 ConcurrentCommandBuffer has been created in job
                    var nestedTypes = m_SystemType.GetNestedTypes(BindingFlags.NonPublic)
                        .Where(t => t.IsValueType);
                    var jobType = nestedTypes.First();
                    Assert.IsNotNull(jobType);

                    var cbTypes = jobType.GetFields(BindingFlags.Instance | BindingFlags.Public)
                        .Where(f => f.FieldType == typeof(EntityCommandBuffer.Concurrent));
                    Assert.AreEqual(1, cbTypes.Count());
                });
        }
    }
}
