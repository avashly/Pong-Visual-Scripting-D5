using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.VisualScripting;

namespace UnityEditor.VisualScriptingECSTests
{
    public class BasicCodeGenBases : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void NestedIfIsNotDetectedAsLoop()
        {
            /* if2's else is not connected
             *    |update|
             *    |if1   |
             *    /   \
             * if2    stack
             *   \    /
             *  endStack
             */
            var graph = GraphModel;

            var query = graph.CreateComponentQuery("m_Query");
            var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
            query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
            var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

            var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
            graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

            var if1 = onUpdate.CreateStackedNode<IfConditionNodeModel>("if1");

            var then1stack = graph.CreateStack("then1", Vector2.left);
            graph.CreateEdge(then1stack.InputPorts[0], if1.ThenPort);

            var if2 = then1stack.CreateStackedNode<IfConditionNodeModel>("if2");

            var else1stack = graph.CreateStack("else1", Vector2.right);
            graph.CreateEdge(else1stack.InputPorts[0], if1.ElsePort);

            var endStack = graph.CreateStack("endStack", Vector2.down);
            graph.CreateEdge(endStack.InputPorts[0], if2.ThenPort);
            graph.CreateEdge(endStack.InputPorts[0], else1stack.OutputPorts[0]);

            var t = graph.CreateTranslator();
            var result = t.TranslateAndCompile(graph, AssemblyType.None, CompilationOptions.Default);
            Assert.AreEqual(CompilationStatus.Succeeded, result.status);
        }

        [Test(Description = "VSB-214 regression test")]
        public void TestFunctionCall([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            var originalScale = Time.timeScale;
            try
            {
                SetupTestGraph(mode, graph =>
                {
                    var query = graph.CreateComponentQuery("m_Query");
                    var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                    query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                    var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                    var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                    graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                    var propertyInfo = typeof(Time).GetProperty("timeScale", BindingFlags.Static | BindingFlags.Public);
                    var setProperty = onUpdate.CreateFunctionCallNode(propertyInfo?.SetMethod);

                    var floatConst = graph.CreateConstantNode("floatConst", TypeHandle.Float, Vector2.zero);
                    ((FloatConstantModel)floatConst).value = 0.42f;

                    graph.CreateEdge(setProperty.GetPortForParameter("value"), floatConst.OutputPort);
                },
                    (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                    (manager, entityIndex, entity) => Assert.That(Time.timeScale, Is.EqualTo(0.42f)));
            }
            finally
            {
                Time.timeScale = originalScale;
            }
        }

        [Test(Description = "VSB-178 regression test")]
        public void SetNonGraphVariableDoesntTriggerASingletonUpdate([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = graph.CreateComponentQuery("m_Query");
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                var floatVariable = onUpdate.CreateFunctionVariableDeclaration("MyFloat", TypeHandle.Float);
                floatVariable.CreateInitializationValue();
                var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);

                var set = onUpdate.CreateStackedNode<SetVariableNodeModel>("set");
                graph.CreateEdge(set.InstancePort, floatInstance.OutputPort);

                var setProperty = onUpdate.CreateStackedNode<SetPropertyGroupNodeModel>("Set Property");
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                setProperty.AddMember(member);

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
                graph.CreateEdge(setProperty.InputsById[member.GetId()], floatInstance.OutputPort);
            },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    // We need to check as we created a singleton entity with no Translation but only the GraphData component
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.x, Is.EqualTo(0f));
                });
        }

        [Test]
        public void GetGraphVariable([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = graph.CreateComponentQuery("m_Query");
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                var floatVariable = graph.CreateGraphVariableDeclaration("MyFloat", TypeHandle.Float, true);
                floatVariable.CreateInitializationValue();
                ((ConstantNodeModel)floatVariable.InitializationModel).ObjectValue = 10f;
                var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);

                var setProperty = onUpdate.CreateStackedNode<SetPropertyGroupNodeModel>("Set Property", 0);
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                setProperty.AddMember(member);

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
                graph.CreateEdge(setProperty.InputsById[member.GetId()], floatInstance.OutputPort);
            },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    // We need to check as we created a singleton entity with no Translation but only the GraphData component
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.x, Is.EqualTo(10f));
                });
        }

        [Test] // TODO: fix jobs
        public void SetGraphVariable([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = graph.CreateComponentQuery("m_Query");
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
                graph.CreateEdge(onUpdate.InstancePort, queryInstance.OutputPort);

                var floatVariable = graph.CreateGraphVariableDeclaration("MyFloat", TypeHandle.Float, true);
                var floatInstance = graph.CreateVariableNode(floatVariable, Vector2.zero);
                var floatConst = graph.CreateConstantNode("floatConst", TypeHandle.Float, Vector2.zero);
                ((FloatConstantModel)floatConst).value = 10f;

                var setVariable = onUpdate.CreateStackedNode<SetVariableNodeModel>("Set Variable", 0);
                graph.CreateEdge(setVariable.InstancePort, floatInstance.OutputPort);
                graph.CreateEdge(setVariable.ValuePort, floatConst.OutputPort);
            },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) =>
                {
                    var graphData = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("GraphData"));
                    if (manager.HasComponent(entity, graphData))
                    {
                        var getComponentMethod = typeof(EntityManager)
                            .GetMethod(nameof(EntityManager.GetComponentData)) ?
                                .MakeGenericMethod(graphData);
                        var singleton = getComponentMethod?.Invoke(manager, new object[] {entity});
                        var myFloat = graphData.GetField("MyFloat").GetValue(singleton);
                        Assert.That(myFloat, Is.EqualTo(10f));
                    }
                });
        }

        [Test]
        public void OneGroupIterationSystem([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                ComponentQueryDeclarationModel query = GraphModel.CreateComponentQuery("g1");
                TypeHandle positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);
                IVariableModel queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);

                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                SetPropertyGroupNodeModel set = onUpdateModel.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                set.AddMember(member);

                IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
            },
                (manager, entityIndex, e) => manager.AddComponentData(e, new Translation { Value = { x = entityIndex } }),
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test(Description = "This test should just pass as SharedComponent must be declared as value and be inserted first in ForEachLambda context")]
        public void OneGroupIterationSystem_SharedAndRegularComponents([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                var query = GraphModel.CreateComponentQuery("query");

                var positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, positionType, ComponentDefinitionFlags.None);

                var renderType = typeof(RenderMesh).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, renderType, ComponentDefinitionFlags.Shared);

                var queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);
                var onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                var posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                var renderComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == renderType), Vector2.zero);

                var logTranslation = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] {typeof(object)}), 0);
                var logRenderMesh = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] {typeof(object)}), 1);

                GraphModel.CreateEdge(logTranslation.GetParameterPorts().First(), posComponent.OutputPort);
                GraphModel.CreateEdge(logRenderMesh.GetParameterPorts().First(), renderComponent.OutputPort);
            },
                (manager, entityIndex, e) =>
                {
                    manager.AddComponentData(e, new Translation { Value = { x = entityIndex } });
                    manager.AddSharedComponentData(e, new RenderMesh());
                },
                (manager, entityIndex, e) => Assert.Pass());
        }

        [Test]
        public void OneGroupIterationSystem_LocalVariable([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                ComponentQueryDeclarationModel query = GraphModel.CreateComponentQuery("g1");
                TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                IVariableModel queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                var localDeclaration = onUpdateModel.CreateFunctionVariableDeclaration("local", TypeHandle.Float);
                var local = GraphModel.CreateVariableNode(localDeclaration, Vector2.zero);

                var log = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] {typeof(object)}), 0);

                GraphModel.CreateEdge(log.GetParameterPorts().First(), local.OutputPort);
            },
                (manager, entityIndex, e) => {},
                (manager, entityIndex, e) => Assert.Pass());
        }

        [Test]
        public void OneGroupIterationSystem_ReadOnly([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                ComponentQueryDeclarationModel query = GraphModel.CreateComponentQuery("g1");
                TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                query.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                IVariableModel queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);

                GraphModel.CreateEdge(onUpdateModel.InstancePort, queryInstance.OutputPort);

                var log = onUpdateModel.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] {typeof(object)}), 0);

                IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                GraphModel.CreateEdge(log.GetParameterPorts().First(), posComponent.OutputPort);
            },
                (manager, entityIndex, e) => manager.AddComponents(e,
                    new ComponentTypes(ComponentType.ReadWrite<Translation>(), ComponentType.ReadWrite<Rotation>())),
                (manager, entityIndex, e) => Assert.Pass());
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_SameComponent([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);

                // query1 - Position
                ComponentQueryDeclarationModel query1 = GraphModel.CreateComponentQuery("g1");
                query1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                IVariableModel query1Instance = GraphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Translation too
                ComponentQueryDeclarationModel query2 = GraphModel.CreateComponentQuery("g2");
                query2.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                IVariableModel query2Instance = GraphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(onUpdateModel.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                GraphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                // set  query2.translation = ...
                SetPropertyGroupNodeModel set = forAllStack.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                set.AddMember(member);

                IVariableModel posComponent = GraphModel.CreateVariableNode(forAllStack.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
            },
                (manager, entityIndex, e) => manager.AddComponentData(e, new Translation { Value = { x = entityIndex } }),
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_NestedLocalVariable([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, g =>
            {
                TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);

                // query1 - Position
                ComponentQueryDeclarationModel query1 = GraphModel.CreateComponentQuery("g1");
                query1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                IVariableModel query1Instance = GraphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Rotation too
                ComponentQueryDeclarationModel query2 = GraphModel.CreateComponentQuery("g2");
                query2.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                IVariableModel query2Instance = GraphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(onUpdateModel.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                GraphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                var decl = forAllStack.CreateFunctionVariableDeclaration("x", TypeHandle.Int);
                // set  query1.translation = ...
                SetVariableNodeModel set = forAllStack.CreateStackedNode<SetVariableNodeModel>("set");

                IVariableModel posComponent = GraphModel.CreateVariableNode(decl, Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);
            });
        }

        [Test]
        public void NestedIterationSystem_DifferentGroups_DifferentComponents([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, g =>
            {
                TypeHandle translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                TypeHandle rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);

                // query1 - Position
                ComponentQueryDeclarationModel query1 = GraphModel.CreateComponentQuery("g1");
                query1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                IVariableModel query1Instance = GraphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Rotation too
                ComponentQueryDeclarationModel query2 = GraphModel.CreateComponentQuery("g2");
                query2.AddComponent(Stencil, rotationType, ComponentDefinitionFlags.None);
                IVariableModel query2Instance = GraphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                OnUpdateEntitiesNodeModel onUpdateModel = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(onUpdateModel.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdateModel, 0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                GraphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                // set  query1.translation = ...
                SetPropertyGroupNodeModel set = forAllStack.CreateStackedNode<SetPropertyGroupNodeModel>("set");
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Translation.Value), nameof(Translation.Value.x) });
                set.AddMember(member);

                IVariableModel posComponent = GraphModel.CreateVariableNode(onUpdateModel.FunctionParameterModels.Single(p => p.DataType == translationType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                ((FloatConstantModel)set.InputConstantsById[member.GetId()]).value = 2f;
            },
                (manager, entityIndex, e) =>
                {
                    manager.AddComponentData(e, new Translation { Value = { x = entityIndex } });
                    manager.AddComponentData(e, new Rotation());
                },
                (manager, entityIndex, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f)));
        }

        [Test]
        public void NestedIteration_DifferentGroups_DifferentEntitiesAccess([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                var scaleType = typeof(Scale).GenerateTypeHandle(Stencil);

                // query1 - Position
                var query1 = GraphModel.CreateComponentQuery("g1");
                query1.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var query1Instance = GraphModel.CreateVariableNode(query1, Vector2.zero);

                // query2 - Scale
                var query2 = GraphModel.CreateComponentQuery("g2");
                query2.AddComponent(Stencil, scaleType, ComponentDefinitionFlags.None);
                var query2Instance = GraphModel.CreateVariableNode(query2, Vector2.zero);

                // update query 1
                var update = GraphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
                GraphModel.CreateEdge(update.InstancePort, query1Instance.OutputPort);

                // nested update query 2
                var forAllStack = GraphModel.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(update, 0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                GraphModel.CreateEdge(forAllNode.InputPort, query2Instance.OutputPort);
                GraphModel.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                // entity from query 1
                var entity1 = graph.CreateVariableNode(
                    update.FunctionParameterModels.Single(
                        p => p.DataType == typeof(Entity).GenerateTypeHandle(graph.Stencil)),
                    Vector2.zero);

                // entity from query 2
                var entity2 = graph.CreateVariableNode(
                    forAllStack.FunctionParameterModels.Single(
                        p => p.DataType == typeof(Entity).GenerateTypeHandle(graph.Stencil)),
                    Vector2.zero);

                // set a new Translation to entities of query1
                var setTranslation = forAllStack.CreateStackedNode<SetComponentNodeModel>("set translation");
                setTranslation.ComponentType = typeof(Translation).GenerateTypeHandle(graph.Stencil);
                setTranslation.DefineNode();
                ((FloatConstantModel)setTranslation.InputConstantsById["z"]).value = 10f;
                graph.CreateEdge(setTranslation.EntityPort, entity1.OutputPort);

                // set a new Scale to entities of query2
                var setScale = forAllStack.CreateStackedNode<SetComponentNodeModel>("set scale");
                setScale.ComponentType = typeof(Scale).GenerateTypeHandle(graph.Stencil);
                setScale.DefineNode();
                ((FloatConstantModel)setScale.InputConstantsById["Value"]).value = 30f;
                graph.CreateEdge(setScale.EntityPort, entity2.OutputPort);
            },
                (manager, index, entity) =>
                {
                    if (index % 2 == 0)
                        manager.AddComponentData(entity, new Translation());
                    else
                        manager.AddComponentData(entity, new Scale());
                },
                (manager, index, entity) =>
                {
                    if (manager.HasComponent<Translation>(entity))
                        Assert.That(manager.GetComponentData<Translation>(entity).Value.z, Is.EqualTo(10f));

                    if (manager.HasComponent<Scale>(entity))
                        Assert.That(manager.GetComponentData<Scale>(entity).Value, Is.EqualTo(30f));
                }
            );
        }

        [Test]
        public void MultipleOnUpdateEntities([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                var query = GraphModel.CreateComponentQuery("g1");
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = GraphModel.CreateVariableNode(query, Vector2.zero);

                CreateUpdateAndLogEntity(graph, queryInstance);
                CreateUpdateAndLogEntity(graph, queryInstance);
            },
                (manager, index, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, index, entity) => Assert.Pass()
            );
        }

        [Test]
        public void TestOnKeyPressCompiles([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = graph.CreateComponentQuery("m_Query");
                var translationType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(Stencil, translationType, ComponentDefinitionFlags.None);
                var queryInstance = graph.CreateVariableNode(query, Vector2.zero);

                var onKeyPress = graph.CreateNode<OnKeyPressEcsNodeModel>("On Key Press", Vector2.zero);
                graph.CreateEdge(onKeyPress.InstancePort, queryInstance.OutputPort);
                onKeyPress.Code = KeyCode.Space;
                onKeyPress.PressType = OnKeyPressEcsNodeModel.KeyPressType.Down;
            },
                (manager, entityIndex, entity) => manager.AddComponentData(entity, new Translation()),
                (manager, entityIndex, entity) => Assert.Pass());
        }

        static void CreateUpdateAndLogEntity(VSGraphModel graphModel, IVariableModel variable)
        {
            // update entities
            var update = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", Vector2.zero);
            graphModel.CreateEdge(update.InstancePort, variable.OutputPort);

            // Create entity from update
            var entity = graphModel.CreateVariableNode(
                update.FunctionParameterModels.Single(
                    p => p.DataType == typeof(Entity).GenerateTypeHandle(graphModel.Stencil)),
                Vector2.zero);

            // Log the entity
            var log = update.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] {typeof(object)}), 0);
            graphModel.CreateEdge(log.GetParameterPorts().First(), entity.OutputPort);
        }
    }
}
