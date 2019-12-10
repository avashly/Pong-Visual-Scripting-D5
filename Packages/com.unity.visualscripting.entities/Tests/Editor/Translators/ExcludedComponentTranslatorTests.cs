using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class ExcludedComponentTranslatorTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestExcludedComponentQuery_RootContext([Values] CodeGenMode mode)
        {
            var defaultValue = new quaternion(0, 0, 0, 0);
            var modifiedValue = new quaternion(1, 1, 1, 1);

            SetupTestGraph(mode, CreateGraphNodes, AddComponentsToEntities, ValidateSubtractive);

            void CreateGraphNodes(VSGraphModel graphModel)
            {
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);
                var rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, scaleType, ComponentDefinitionFlags.Subtract);
                query.AddComponent(graphModel.Stencil, rotationType, ComponentDefinitionFlags.None);

                IVariableModel queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                OnUpdateEntitiesNodeModel onOnEntitiesNodeModel = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("loop", Vector2.zero);
                graphModel.CreateEdge(onOnEntitiesNodeModel.InstancePort, queryInstance.OutputPort);

                SetComponentNodeModel setRotationNode = graphModel.CreateNode<SetComponentNodeModel>("SetComponent", Vector2.zero);
                setRotationNode.ComponentType = typeof(Rotation).GenerateTypeHandle(Stencil);
                onOnEntitiesNodeModel.AddStackedNode(setRotationNode, -1);
                setRotationNode.DefineNode();
                var entityVarDeclaration = onOnEntitiesNodeModel.FunctionParameterModels.Single(p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil));
                var entityInstance = graphModel.CreateVariableNode(entityVarDeclaration, Vector2.zero);
                var float4ConstantNode = (Float4ConstantModel)graphModel.CreateConstantNode("float4", typeof(float4).GenerateTypeHandle(graphModel.Stencil), Vector2.zero);
                float4ConstantNode.value = modifiedValue.value;

                graphModel.CreateEdge(setRotationNode.EntityPort, entityInstance.OutputPort);
                graphModel.CreateEdge(setRotationNode.InputsById[nameof(Rotation.Value)], float4ConstantNode.OutputPort);
            }

            void AddComponentsToEntities(EntityManager manager, int entityIndex, Entity e)
            {
                manager.AddComponent(e, typeof(Rotation));
                if (entityIndex % 2 == 1)
                    manager.AddComponent(e, typeof(Scale));
            }

            void ValidateSubtractive(EntityManager manager, int entityIndex, Entity e)
            {
                var expectedValue = manager.HasComponent<Scale>(e) ? defaultValue : modifiedValue;

                Assert.That(manager.GetComponentData<Rotation>(e).Value, Is.EqualTo(expectedValue));
            }
        }
    }
}
