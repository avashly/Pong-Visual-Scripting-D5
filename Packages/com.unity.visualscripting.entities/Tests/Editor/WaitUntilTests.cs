using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Transforms;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using WaitUntil = VisualScripting.Entities.Runtime.WaitUntil;

namespace UnityEditor.VisualScriptingECSTests
{
    public class WaitUntilTests : EndToEndCodeGenBaseFixture
    {
        // test nodes : WaitUntil(translation.x > 0); translation.y = 10f;
        // test behaviour : assert entities.translation.y == 0; set x > 0; assert y == 10f;
        [Test]
        public void TestWaitUntilCoroutine([Values] CodeGenMode mode)
        {
            float yBeforeWait = 0f;
            float yAfterWait = 10f;
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                var waitUntil = onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait Until", setup: n =>
                {
                    n.CoroutineType = typeof(WaitUntil).GenerateTypeHandle(Stencil);
                });
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                var getProperty = GraphModel.CreateGetPropertyGroupNode(Vector2.zero);
                var translationXMember = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                getProperty.AddMember(translationXMember);
                GraphModel.CreateEdge(getProperty.InstancePort, translation.OutputPort);
                var equalNode = GraphModel.CreateBinaryOperatorNode(BinaryOperatorKind.GreaterThan, Vector2.zero);
                GraphModel.CreateEdge(equalNode.InputPortA, getProperty.GetPortsForMembers().Single());
                ((FloatConstantModel)equalNode.InputConstantsById[equalNode.InputPortB.UniqueId]).value = 0f;
                var moveNextParam = typeof(WaitUntil).GetMethod(nameof(WaitUntil.MoveNext))?.GetParameters().Single();
                Assert.That(moveNextParam, Is.Not.Null);
                GraphModel.CreateEdge(waitUntil.GetParameterPort(moveNextParam), equalNode.OutputPort);

                var setProperty = onUpdate.CreateSetPropertyGroupNode(-1);
                var translationYMember = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                setProperty.AddMember(translationYMember);
                GraphModel.CreateEdge(setProperty.InstancePort, translation.OutputPort);
                ((FloatConstantModel)setProperty.InputConstantsById[translationYMember.GetId()]).value = yAfterWait;
            },
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponentData(e, new Translation());
                }),
                EachEntity((manager, i, e) =>
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(yBeforeWait));
                    manager.SetComponentData(e, new Translation { Value = { x = 10f } }); // any x > 0 should stop this WaitUntil
                }),
                (manager, entities) => {},  // Skip Frame where WaitUntil is done and component gets set
                EachEntity((manager, i, e) =>
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(yAfterWait));
                })
            );
        }

        protected override bool CreateGraphOnStartup => true;
    }
}
