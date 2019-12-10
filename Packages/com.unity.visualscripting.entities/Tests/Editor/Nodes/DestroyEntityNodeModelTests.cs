using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class DestroyEntityNodeModelTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestDestroyEntity([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                // Component creation
                var dummyF3Type = typeof(Translation).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, dummyF3Type, ComponentDefinitionFlags.None);

                // On update
                var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(onUpdateEntities.InstancePort, queryInstance.OutputPort);

                // Destroy Entity
                var entityInstance = graphModel.CreateVariableNode(
                    onUpdateEntities.FunctionParameterModels.Single(
                        p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                        ),
                    Vector2.zero);
                var destroy = onUpdateEntities.CreateStackedNode<DestroyEntityNodeModel>("destroy");
                graphModel.CreateEdge(destroy.EntityPort, entityInstance.OutputPort);
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Translation)),
                (manager, entityIndex, e) => Assert.IsFalse(manager.Exists(e)));
        }
    }
}
