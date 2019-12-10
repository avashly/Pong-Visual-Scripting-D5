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
    class CreateEntityNodeModelTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        IVariableModel m_NewEntityVariable;
        OnUpdateEntitiesNodeModel m_OnUpdateEntities;
        FloatConstantModel m_FloatConstantNode;
        CreateEntityNodeModel m_CreateEntityModel;

        void PrepareGraph(VSGraphModel graphModel)
        {
            var entityTypeHandle = typeof(Entity).GenerateTypeHandle(graphModel.Stencil);

            // Component creation
            var scale = typeof(Scale).GenerateTypeHandle(Stencil);
            var group = graphModel.CreateComponentQuery("g");
            group.AddComponent(graphModel.Stencil, scale, ComponentDefinitionFlags.None);


            // On update
            var groupInstance = graphModel.CreateVariableNode(group, Vector2.zero);
            m_OnUpdateEntities = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(m_OnUpdateEntities.InstancePort, groupInstance.OutputPort);

            m_CreateEntityModel = m_OnUpdateEntities.CreateStackedNode<CreateEntityNodeModel>("instantiate");

            // Variable containing the new entity
            m_OnUpdateEntities.CreateFunctionVariableDeclaration("newEntity", entityTypeHandle);
            m_NewEntityVariable = graphModel.CreateVariableNode(
                m_OnUpdateEntities.FunctionParameterModels.Single(p => p.DataType == entityTypeHandle),
                Vector2.zero);

            m_FloatConstantNode = (FloatConstantModel)graphModel.CreateConstantNode("float", TypeHandle.Float, Vector2.zero);
            m_FloatConstantNode.value = 10f;

            graphModel.CreateEdge(m_CreateEntityModel.InstancePort, m_NewEntityVariable.OutputPort);
        }

        [Test]
        public void TestAddComponentTypeToAdd()
        {
            //Prepare
            var translateTypeHandle = typeof(Translation).GenerateTypeHandle(Stencil);
            var instantiate = GraphModel.CreateNode<CreateEntityNodeModel>("CreateEntityNode", Vector2.zero);

            //Act
            instantiate.AddComponentTypeToAdd(translateTypeHandle);

            //Validate
            Assert.That(instantiate.GetEditableComponents().Single(), Is.EqualTo(translateTypeHandle));
        }

        [Test]
        public void TestCreateEntity([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, PrepareGraph,
                (manager, entityIndex, e) => manager.AddComponent(e, typeof(Scale)),
                (manager, entityIndex, e) => {});
            Assert.That(m_EntityManager.GetAllEntities().Length, Is.EqualTo(k_EntityCount * 2));
        }

        [Test]
        public void TestCreateEntityAndAddComponent([Values] CodeGenMode mode)
        {
            int entitiesWithTranslationComponent = 0;
            int entitiesWithoutTranslationComponent = 0;
            SetupTestGraph(mode, graphModel =>
            {
                PrepareGraph(graphModel);
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);

                // Create instantiate node
                m_CreateEntityModel.AddComponentTypeToAdd(translationType);
                m_CreateEntityModel.DefineNode();

                var translateComponentPort = m_CreateEntityModel.GetPortsForComponent(translationType).First();
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
        public void TestAction_SetOperationForComponentTypeInCreateEntityNodeAction([Values] TestingMode mode)
        {
            var expectedType = typeof(Translation).GenerateTypeHandle(Stencil);
            PrepareGraph(GraphModel);
            TestPrereqActionPostreq(mode,
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<CreateEntityNodeModel>();
                    Assert.That(model, Is.Not.Null);
                    Assert.That(model.GetEditableComponents().Count(th => th == expectedType), Is.Zero);
                    return new SetOperationForComponentTypeInCreateEntityNodeAction(model, expectedType);
                },
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<CreateEntityNodeModel>();
                    Assert.That(model, Is.Not.Null);
                    Assert.That(model.GetEditableComponents(), Contains.Item(expectedType));
                }
            );
        }

        [Test]
        public void TestAction_RemoveOperationForComponentTypeInCreateEntityNodeAction([Values] TestingMode mode)
        {
            var expectedType = typeof(Translation).GenerateTypeHandle(Stencil);
            PrepareGraph(GraphModel);
            m_CreateEntityModel.AddComponentTypeToAdd(expectedType);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<CreateEntityNodeModel>();
                    Assert.That(model, Is.Not.Null);
                    Assert.That(model.GetEditableComponents(), Contains.Item(expectedType));
                    return new RemoveOperationForComponentTypeInCreateEntityNodeAction(model, expectedType);
                },
                () =>
                {
                    var model = GraphModel.FindStackedNodeOfType<CreateEntityNodeModel>();
                    Assert.That(model, Is.Not.Null);
                    Assert.That(model.GetEditableComponents(), Is.Not.Contains(expectedType));
                }
            );
        }
    }
}
