using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScriptingTests;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class InstantiateNodeModelTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        IVariableModel m_EntityTemplateVariable;
        IVariableModel m_NewEntityVariable;
        OnUpdateEntitiesNodeModel m_OnUpdateEntities;
        FloatConstantModel m_FloatConstantNode;
        InstantiateNodeModel m_InstantiateModel;

        void PrepareGraph(VSGraphModel graphModel)
        {
            var entityTypeHandle = typeof(Entity).GenerateTypeHandle(graphModel.Stencil);

            // Component creation
            var scale = typeof(Scale).GenerateTypeHandle(Stencil);
            var query = graphModel.CreateComponentQuery("g");
            query.AddComponent(graphModel.Stencil, scale, ComponentDefinitionFlags.None);


            // On update
            var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
            m_OnUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(m_OnUpdateEntities.InstancePort, queryInstance.OutputPort);

            m_InstantiateModel = m_OnUpdateEntities.CreateStackedNode<InstantiateNodeModel>("instantiate");

            // Instantiate Entity
            m_EntityTemplateVariable = graphModel.CreateVariableNode(
                m_OnUpdateEntities.FunctionParameterModels.Single(p => p.DataType == entityTypeHandle),
                Vector2.zero);

            // Variable containing the new entity
            m_OnUpdateEntities.CreateFunctionVariableDeclaration("newEntity", entityTypeHandle);
            m_NewEntityVariable = graphModel.CreateVariableNode(
                m_OnUpdateEntities.FunctionParameterModels.Single(p => p.DataType == entityTypeHandle),
                Vector2.zero);

            m_FloatConstantNode = (FloatConstantModel)graphModel.CreateConstantNode("float", TypeHandle.Float, Vector2.zero);
            m_FloatConstantNode.value = 10f;

            graphModel.CreateEdge(m_InstantiateModel.InstancePort, m_NewEntityVariable.OutputPort);
            graphModel.CreateEdge(m_InstantiateModel.EntityPort, m_EntityTemplateVariable.OutputPort);
        }

        [TestCase(ComponentOperation.ComponentOperationType.SetComponent)]
        [TestCase(ComponentOperation.ComponentOperationType.AddComponent)]
        [TestCase(ComponentOperation.ComponentOperationType.RemoveComponent)]
        public void TestSetComponentOperation(ComponentOperation.ComponentOperationType operationType)
        {
            //Prepare
            var translateTypeHandle = typeof(Translation).GenerateTypeHandle(Stencil);
            var instantiate = GraphModel.CreateNode<InstantiateNodeModel>("instantiateNode", Vector2.zero);

            //Act
            instantiate.SetComponentOperation(translateTypeHandle, operationType);

            //Validate
            Assert.That(instantiate.GetEditableComponents().Single().Type, Is.EqualTo(translateTypeHandle));
            Assert.That(instantiate.GetEditableComponents().Single().OperationType, Is.EqualTo(operationType));
        }

        [Test]
        public void TestDeleteComponentOperation()
        {
            //Prepare
            var translateTypeHandle = typeof(Translation).GenerateTypeHandle(Stencil);
            var instantiate = GraphModel.CreateNode<InstantiateNodeModel>("instantiateNode", Vector2.zero);
            instantiate.SetComponentOperation(translateTypeHandle, ComponentOperation.ComponentOperationType.SetComponent);
            Assert.That(instantiate.GetEditableComponents().Count, Is.EqualTo(1));

            //Act
            instantiate.DeleteComponentOperation(translateTypeHandle);

            //Validate
            Assert.That(instantiate.GetEditableComponents(), Is.Empty);
        }

        [Test]
        public void TestStackInstantiateEntity([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, PrepareGraph,
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) => {});
            Assert.That(m_EntityManager.GetAllEntities().Length, Is.EqualTo(k_EntityCount * 2));
        }

        [Test]
        public void TestStackInstantiateEntityAndAddComponent([Values] CodeGenMode mode)
        {
            int entitiesWithTranslationComponent = 0;
            int entitiesWithoutTranslationComponent = 0;
            SetupTestGraph(mode, graphModel =>
            {
                PrepareGraph(graphModel);
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);

                // Create instantiate node
                m_InstantiateModel.SetComponentOperation(translationType, ComponentOperation.ComponentOperationType.AddComponent);
                m_InstantiateModel.DefineNode();

                var translateComponentPort = m_InstantiateModel.GetPortsForComponent(translationType).First();
                graphModel.CreateEdge(translateComponentPort, m_FloatConstantNode.OutputPort);
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) =>
                {
                    if (manager.GetComponentTypes(e).Any(ct => ct.GetManagedType() == typeof(Translation)))
                    {
                        Translation t = manager.GetComponentData<Translation>(e);
                        Assert.That(t.Value.x, Is.EqualTo(m_FloatConstantNode.value));
                        entitiesWithTranslationComponent++;
                    }
                    else
                        entitiesWithoutTranslationComponent++;
                });
            Assert.That(entitiesWithoutTranslationComponent, Is.EqualTo(k_EntityCount));
            Assert.That(entitiesWithTranslationComponent, Is.EqualTo(k_EntityCount));
        }

        [Test]
        public void TestStackInstantiateEntityAndSetComponent([Values] CodeGenMode mode)
        {
            int entitiesWithModifiedTranslationComponent = 0;
            int entitiesWithoutModifiedTranslationComponent = 0;
            SetupTestGraph(mode, graphModel =>
            {
                PrepareGraph(graphModel);
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);

                // Create instantiate node
                m_InstantiateModel.SetComponentOperation(translationType, ComponentOperation.ComponentOperationType.SetComponent);
                m_InstantiateModel.DefineNode();

                var translateComponentPort = m_InstantiateModel.GetPortsForComponent(translationType).First();
                graphModel.CreateEdge(translateComponentPort, m_FloatConstantNode.OutputPort);
            },
                (manager, entityIndex, e) =>
                {
                    manager.AddComponent(e, typeof(Scale));
                    manager.AddComponent(e, typeof(Translation));
                },
                (manager, entityIndex, e) =>
                {
                    Translation t = manager.GetComponentData<Translation>(e);
                    if (Math.Abs(t.Value.x) < float.Epsilon)
                        entitiesWithModifiedTranslationComponent++;
                    else
                        entitiesWithoutModifiedTranslationComponent++;
                });
            Assert.That(entitiesWithoutModifiedTranslationComponent, Is.EqualTo(k_EntityCount));
            Assert.That(entitiesWithModifiedTranslationComponent, Is.EqualTo(k_EntityCount));
        }

        [Test]
        public void TestStackInstantiateEntityAndRemoveComponent([Values] CodeGenMode mode)
        {
            int entitiesWithScaleComponent = 0;
            int entitiesWithoutScaleComponent = 0;
            SetupTestGraph(mode, graphModel =>
            {
                PrepareGraph(graphModel);
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);

                // Create instantiate node
                m_InstantiateModel.SetComponentOperation(scaleType, ComponentOperation.ComponentOperationType.RemoveComponent);
                m_InstantiateModel.DefineNode();
            },
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) =>
                {
                    if (manager.GetComponentTypes(e).Any(ct => ct.GetManagedType() == typeof(Scale)))
                        entitiesWithScaleComponent++;
                    else
                        entitiesWithoutScaleComponent++;
                });
            Assert.That(entitiesWithoutScaleComponent, Is.EqualTo(k_EntityCount));
            Assert.That(entitiesWithScaleComponent, Is.EqualTo(k_EntityCount));
        }

        [Test]
        public void TestAction_SetOperationForComponentTypeInInstantiateNode([Values] TestingMode mode)
        {
            var compType = typeof(Translation).GenerateTypeHandle(Stencil);
            var operation = ComponentOperation.ComponentOperationType.SetComponent;

            PrepareGraph(GraphModel);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<InstantiateNodeModel>();
                    Assert.That(model, Is.Not.Null);
                    Assert.That(model.GetEditableComponents().Count(op => op.Type == compType), Is.Zero);
                    return new SetOperationForComponentTypeInInstantiateNodeAction(model, compType, operation);
                },
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<InstantiateNodeModel>();
                    Assert.That(model, Is.Not.Null);

                    var compOperation = model.GetEditableComponents().FirstOrDefault(op => op.Type == compType);
                    Assert.That(compOperation.Type, Is.EqualTo(compType));
                    Assert.That(compOperation.OperationType, Is.EqualTo(operation));
                }
            );
        }

        [Test]
        public void TestAction_RemoveOperationForComponentTypeInInstantiateNodeAction([Values] TestingMode mode)
        {
            var compType = typeof(Translation).GenerateTypeHandle(Stencil);
            PrepareGraph(GraphModel);
            m_InstantiateModel.SetComponentOperation(compType, ComponentOperation.ComponentOperationType.SetComponent);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<InstantiateNodeModel>();
                    Assert.That(model, Is.Not.Null);

                    var componentCount = model.GetEditableComponents().Count(op => op.Type == compType);
                    Assert.That(componentCount, Is.EqualTo(1));
                    return new RemoveOperationForComponentTypeInInstantiateNodeAction(model, compType);
                },
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<InstantiateNodeModel>();
                    Assert.That(model, Is.Not.Null);

                    var componentCount = model.GetEditableComponents().Count(op => op.Type == compType);
                    Assert.That(componentCount, Is.Zero);
                }
            );
        }
    }
}
