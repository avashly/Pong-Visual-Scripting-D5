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
using UnityEngine.TestTools;
using SetPositionNodeModel = UnityEditor.VisualScripting.Model.Stencils.SetPositionNodeModel;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class EnterExitSystemsTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test] // TODO: fix jobs
        public void OnEnterWorks([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graphModel =>
            {
                var query = graphModel.CreateComponentQuery("g");
                var positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(graphModel.Stencil, positionType, ComponentDefinitionFlags.None);
                query.AddComponent(graphModel.Stencil, typeof(Rotation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                var update = graphModel.CreateNode<OnStartEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(update.InstancePort, queryInstance.OutputPort);

                var set = update.CreateStackedNode<SetPositionNodeModel>("set", 0, SpawnFlags.Default, n => n.Mode = SetPositionNodeModel.TranslationMode.Float3);
                set.Add = true;     // increment so we can detect multiple runs if they happen
                ((Float3ConstantModel)set.InputConstantsById["Value"]).value = new Vector3(1f, 0.0f, 0.0f);

                IVariableModel posComponent = GraphModel.CreateVariableNode(update.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponent(e, typeof(Rotation))),
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponent(e, typeof(Translation)); // will make the entity enter the query
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0));
                }),
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(1))), // translate ran once
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(1))) // not twice
            );
        }

        [Test] // TODO: fix jobs
        public void OnExitWorks([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graphModel =>
            {
                var query = graphModel.CreateComponentQuery("g");
                var positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(graphModel.Stencil, positionType, ComponentDefinitionFlags.None);
                query.AddComponent(graphModel.Stencil, typeof(Rotation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                var update = graphModel.CreateNode<OnEndEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(update.InstancePort, queryInstance.OutputPort);

                var log = update.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] { typeof(object) }), 0);
                IVariableModel entityVariable = GraphModel.CreateVariableNode(update.FunctionParameterModels.Single(p => p.DataType == typeof(Entity).GenerateTypeHandle(Stencil)), Vector2.zero);
                GraphModel.CreateEdge(log.GetParameterPorts().First(), entityVariable.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponent(e, typeof(Rotation))),
                EachEntity((manager, i, e) => manager.AddComponent(e, typeof(Translation))),
                EachEntity((manager, i, e) =>
                {
                    LogAssert.NoUnexpectedReceived();
                    LogAssert.Expect(LogType.Log, $"Entity({i}:1)");
                    manager.RemoveComponent<Rotation>(e);
                }),
                EachEntity((manager, i, e) => {})
            );
        }

        [Test] // TODO: fix jobs
        public void OnEnterAndExitWorks([Values(CodeGenMode.NoJobs)] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graphModel =>
            {
                var query = graphModel.CreateComponentQuery("g");
                var positionType = typeof(Translation).GenerateTypeHandle(Stencil);
                query.AddComponent(graphModel.Stencil, positionType, ComponentDefinitionFlags.None);
                query.AddComponent(graphModel.Stencil, typeof(Rotation).GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
                var queryInstance = graphModel.CreateVariableNode(query, Vector2.zero);
                var update = graphModel.CreateNode<OnStartEntitiesNodeModel>("update", Vector2.zero);
                graphModel.CreateEdge(update.InstancePort, queryInstance.OutputPort);

                var set = update.CreateStackedNode<SetPositionNodeModel>("set", 0, SpawnFlags.Default, n => n.Mode = SetPositionNodeModel.TranslationMode.Float3);
                set.Add = true;     // increment so we can detect multiple runs if they happen
                ((Float3ConstantModel)set.InputConstantsById["Value"]).value = new Vector3(1f, 0.0f, 0.0f);

                IVariableModel posComponent = GraphModel.CreateVariableNode(update.FunctionParameterModels.Single(p => p.DataType == positionType), Vector2.zero);
                GraphModel.CreateEdge(set.InstancePort, posComponent.OutputPort);

                var log = update.CreateFunctionCallNode(typeof(Debug).GetMethod("Log", new[] { typeof(object) }), 0);
                IVariableModel entityVariable = GraphModel.CreateVariableNode(update.FunctionParameterModels.Single(p => p.DataType == typeof(Entity).GenerateTypeHandle(Stencil)), Vector2.zero);
                GraphModel.CreateEdge(log.GetParameterPorts().First(), entityVariable.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponent(e, typeof(Rotation))),
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponent(e, typeof(Translation)); // will make the entity enter the query
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0));
                }),
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(1))), // translate ran once
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(1))), // not twice
                EachEntity((manager, i, e) =>
                {
                    LogAssert.Expect(LogType.Log, $"Entity({i}:1)");
                    manager.RemoveComponent<Rotation>(e);
                }),
                EachEntity((manager, i, e) => {})
            );
        }
    }
}
