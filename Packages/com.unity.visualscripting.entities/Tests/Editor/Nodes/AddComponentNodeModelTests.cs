using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class AddComponentNodeModelTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        static AddComponentNodeModel CreateAddComponentInGraph(
            VSGraphModel graphModel,
            IVariableDeclarationModel query,
            Type componentToAdd)
        {
            return CreateAddComponentInGraph<OnUpdateEntitiesNodeModel>(graphModel, query, componentToAdd);
        }

        static AddComponentNodeModel CreateAddComponentInGraph<TStack>(
            VSGraphModel graphModel,
            IVariableDeclarationModel query,
            Type componentToAdd) where TStack : OnEntitiesEventBaseNodeModel
        {
            var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
            var onUpdateEntities = graphModel.CreateNode<TStack>("update", Vector2.zero);
            graphModel.CreateEdge(onUpdateEntities.InstancePort, queryInstance.OutputPort);

            var entityInstance = graphModel.CreateVariableNode(
                onUpdateEntities.FunctionParameterModels.Single(
                    p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                    ),
                Vector2.zero);

            var addComponent = onUpdateEntities.CreateStackedNode<AddComponentNodeModel>("add");
            addComponent.ComponentType = componentToAdd.GenerateTypeHandle(graphModel.Stencil);

            graphModel.CreateEdge(addComponent.EntityPort, entityInstance.OutputPort);

            return addComponent;
        }

        [Test]
        public void TestAddComponent_RootContext([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, scaleType, ComponentDefinitionFlags.None);

                var addComponent = CreateAddComponentInGraph(graphModel, query, typeof(Translation));
                addComponent.DefineNode();

                // Translation has only a float3 field, so addComponent should have 4 inputs: entity, x, y and z
                Assert.That(addComponent.InputsByDisplayOrder.Count, Is.EqualTo(4));
                Assert.That(addComponent.GetPortsForComponent().Count, Is.EqualTo(3));
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) => Assert.That(manager.HasComponent<Translation>(e)));
        }

        [Test]
        public void TestAddComponentConditionalUpdate([Values] CodeGenMode mode, [Values] bool enableStackExecution)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, scaleType, ComponentDefinitionFlags.None);

                var addComponent = CreateAddComponentInGraph<ConditionalUpdateEntitiesNodeModel>(graphModel, query, typeof(Translation));
                addComponent.DefineNode();
                Assert.That(addComponent.ParentStackModel, Is.Not.Null);
                var updateStackModel = (ConditionalUpdateEntitiesNodeModel)addComponent.ParentStackModel;
                updateStackModel.EnableStackExecution = enableStackExecution;
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) => Assert.That(manager.HasComponent<Translation>(e), Is.EqualTo(enableStackExecution)));
        }

        [Test]
        public void TestAddSharedComponent_RootContext([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);
                var query = graphModel.CreateComponentQuery("g");
                query.AddComponent(graphModel.Stencil, scaleType, ComponentDefinitionFlags.None);

                var addComponent = CreateAddComponentInGraph(graphModel, query, typeof(DummySharedComponent));
                addComponent.DefineNode();

                // DummySharedComponent has 1 fields, so addComponent should have 2 inputs: entity + 1
                Assert.That(addComponent.InputsByDisplayOrder.Count, Is.EqualTo(2));
                Assert.That(addComponent.GetPortsForComponent().Count, Is.EqualTo(1));
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) => Assert.That(manager.HasComponent<DummySharedComponent>(e)));
        }

        [Test]
        public void TestAddComponent_ForEachContext([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graphModel =>
            {
                // query1 - Position
                var query1 = graphModel.CreateComponentQuery("g1");
                query1.AddComponent(graphModel.Stencil, typeof(Translation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var query1Instance = graphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Scale (will add DummySharedComponent)
                var query2 = graphModel.CreateComponentQuery("g2");
                query2.AddComponent(graphModel.Stencil, typeof(Scale).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var query2Instance = graphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                var onUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(onUpdateEntities.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = graphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdateEntities,  0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                graphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                graphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                // add DummySharedComponent component
                var addComponent = forAllStack.CreateStackedNode<AddComponentNodeModel>("add");
                addComponent.ComponentType = typeof(DummySharedComponent).GenerateTypeHandle(graphModel.Stencil);

                var entityInstance = graphModel.CreateVariableNode(
                    forAllStack.FunctionParameterModels.Single(
                        p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)
                        ),
                    Vector2.zero);
                graphModel.CreateEdge(addComponent.EntityPort, entityInstance.OutputPort);
            },
                (manager, entityIndex, e) =>
                {
                    // HACK as there is no Single update method as entry point (just the on UpdateEntities right now)
                    var toAdd = entityIndex == 0 ? typeof(Translation) : typeof(Scale);
                    manager.AddComponent(e, toAdd);
                },
                (manager, entityIndex, e) =>
                {
                    Assert.That(manager.HasComponent<Scale>(e)
                        ? manager.HasComponent<DummySharedComponent>(e)
                        : !manager.HasComponent<DummySharedComponent>(e));
                });
        }
    }
}
