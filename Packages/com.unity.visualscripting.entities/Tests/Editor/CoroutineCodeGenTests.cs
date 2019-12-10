using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.TestTools;
using VisualScripting.Entities.Runtime;
using WaitUntil = VisualScripting.Entities.Runtime.WaitUntil;

namespace UnityEditor.VisualScriptingECSTests
{
    public struct UnitTestCoroutine : ICoroutine
    {
        public float DeltaTime { get; set; }
        public bool MoveNext()
        {
            return true;
        }

        public byte GetProgress()
        {
            return 0;
        }
    }

    public class CoroutineCodeGenTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestSendEventInCoroutine([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", 0, setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var eventTypeHandle = typeof(UnitTestEvent).GenerateTypeHandle(Stencil);
                var sendEvent = onUpdate.CreateStackedNode<SendEventNodeModel>("Send", 1, setup: n =>
                {
                    n.EventType = eventTypeHandle;
                });

                var entityType = typeof(Entity).GenerateTypeHandle(Stencil);
                var entityVar = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == entityType), Vector2.zero);
                graph.CreateEdge(sendEvent.EntityPort, entityVar.OutputPort);

                var onEvent = graph.CreateNode<OnEventNodeModel>("On Event", preDefineSetup: n =>
                {
                    n.EventTypeHandle = eventTypeHandle;
                });
                graph.CreateEdge(onEvent.InstancePort, query.OutputPort);
                var setProperty = onEvent.CreateSetPropertyGroupNode(0);
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setProperty.AddMember(member);
                ((FloatConstantModel)setProperty.InputConstantsById[member.GetId()]).value = 10f;

                var translation = graph.CreateVariableNode(onEvent.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
            },

                EachEntity((manager, i, e) =>
                {
                    manager.World.CreateSystem<InitializationSystemGroup>();
                    manager.AddComponentData(e, new Translation());
                }),

                // Init State
                EachEntity((manager, i, e) => {}),

                // Wait MoveNext
                EachEntity((manager, i, e) => {}),

                // Send event;
                EachEntity((manager, i, e) =>
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(10f));
                }));
        }

        [Test]
        public void TestCoroutineWithForEachContext([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var translationQuery = SetupQuery(graph, "translationQuery", new[] { typeof(Translation) });
                var scaleQuery = SetupQuery(graph, "scaleQuery", new[] { typeof(Scale) });

                var onUpdate = SetupOnUpdate(graph, translationQuery);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var forAllStack = graph.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdate, 1) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                graph.CreateEdge(forAllNode.InputPort, scaleQuery.OutputPort);
                graph.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                var setProperty = forAllStack.CreateSetPropertyGroupNode(0);
                var member = new TypeMember(TypeHandle.Float, new List<string> { nameof(Scale.Value) });
                setProperty.AddMember(member);
                ((FloatConstantModel)setProperty.InputConstantsById[member.GetId()]).value = 10f;

                var scale = graph.CreateVariableNode(forAllStack.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Scale).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, scale.OutputPort);
            },
                EachEntity((manager, i, e) =>
                {
                    if (i % 2 == 0)
                        manager.AddComponentData(e, new Translation());
                    else
                        manager.AddComponentData(e, new Scale());
                }),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // Wait MoveNext
                EachEntity((manager, i, e) => // ForEach set Scale
                {
                    if (manager.HasComponent<Scale>(e))
                        Assert.That(manager.GetComponentData<Scale>(e).Value, Is.EqualTo(10f));
                })
            );
        }

        [Test]
        public void TestCoroutineAccessComponents([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var setProperty = onUpdate.CreateSetPropertyGroupNode(1);
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setProperty.AddMember(member);
                ((FloatConstantModel)setProperty.InputConstantsById[member.GetId()]).value = 10f;

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // Wait MoveNext
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(10f)))
            );
        }

        [Test]
        public void TestCoroutineAccessStaticValues([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var methodInfo = typeof(Time).GetMethod("get_timeScale");
                var timeScale = graph.CreateFunctionCallNode(methodInfo, Vector2.zero);
                var log = onUpdate.CreateStackedNode<LogNodeModel>("Log");
                graph.CreateEdge(log.InputPort, timeScale.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // Wait MoveNext
                EachEntity((manager, i, e) => LogAssert.Expect(LogType.Log, $"{Time.timeScale}"))
            );
        }

        [Test]
        public void TestGenerateUniqueCoroutineComponentsAndQueries([Values] CodeGenMode mode)
        {
            SetupTestGraph(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var onUpdate2 = SetupOnUpdate(graph, query);
                onUpdate2.CreateStackedNode<CoroutineNodeModel>("Wait 2", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });
            },
                (manager, entityIndex, entity) => { manager.AddComponentData(entity, new Translation()); },
                (manager, entityIndex, entity) =>
                {
                    var coroutines = m_SystemType.GetNestedTypes()
                        .Where(t => t.Name.Contains("Coroutine"))
                        .ToList();
                    Assert.That(coroutines.Count, Is.EqualTo(2));
                    Assert.That(coroutines.Distinct().Count(), Is.EqualTo(coroutines.Count));

                    var queries = m_SystemType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Where(f => f.FieldType == typeof(EntityQuery))
                        .ToList();
                    Assert.That(queries.Count, Is.EqualTo(4)); // 2 queries + 2 queries for coroutine initialization
                    Assert.That(queries.Distinct().Count(), Is.EqualTo(queries.Count));
                });
        }

        [Test]
        public void TestCoroutine([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });
            },
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponentData(e, new Translation());

                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(coroutineType, Is.Not.Null);
                    Assert.That(manager.HasComponent(e, coroutineType), Is.Not.True);
                }),
                EachEntity((manager, i, e) => // Init State
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.True);
                }),
                EachEntity((manager, i, e) => // Wait MoveNext
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.True);
                }),
                EachEntity((manager, i, e) => // Remove component
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                })
            );
        }

        [Test]
        public void TestCoroutineWithConnectedStack([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);

                var connectedStack = graph.CreateStack(string.Empty, Vector2.down);
                connectedStack.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                graph.CreateEdge(connectedStack.InputPorts.First(), onUpdate.OutputPort);
            },
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponentData(e, new Translation());

                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(coroutineType, Is.Not.Null);
                    Assert.That(manager.HasComponent(e, coroutineType), Is.Not.True);
                }),
                EachEntity((manager, i, e) => // Init State
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.True);
                }),
                EachEntity((manager, i, e) => // Wait MoveNext
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.True);
                }),
                EachEntity((manager, i, e) => // Remove component
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                })
            );
        }

        [Test]
        public void TestCoroutineExecutionStack([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                var loopNode = onUpdate.CreateStackedNode<CoroutineNodeModel>("UnitTest", setup: n =>
                {
                    n.CoroutineType = typeof(UnitTestCoroutine).GenerateTypeHandle(Stencil);
                });

                var loopStack = graph.CreateLoopStack<CoroutineStackModel>(Vector2.down);
                graph.CreateEdge(loopStack.InputPort, loopNode.OutputPort);

                var setProperty = loopStack.CreateSetPropertyGroupNode(0);
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setProperty.AddMember(member);
                ((FloatConstantModel)setProperty.InputConstantsById[member.GetId()]).value = 10f;

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // MoveNext -> Execute loop stack
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(10f)))
            );
        }

        [Test]
        public void TestCoroutineAccessingLocalVariable([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var localVar = onUpdate.CreateFunctionVariableDeclaration("localVar", TypeHandle.Float);
                var localVarInstance = graph.CreateVariableNode(localVar, Vector2.zero);
                var log = onUpdate.CreateStackedNode<LogNodeModel>("Log");
                graph.CreateEdge(log.InputPort, localVarInstance.OutputPort);
            },
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponentData(e, new Translation());

                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(coroutineType, Is.Not.Null);
                    Assert.That(manager.HasComponent(e, coroutineType), Is.Not.True);
                }),
                EachEntity((manager, i, e) => Assert.Pass())
            );
        }

        [Test]
        public void TestCoroutineWithReturnNode([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<ReturnNodeModel>("Return");

                var connectedStack = graph.CreateStack(string.Empty, Vector2.down);
                connectedStack.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                graph.CreateEdge(connectedStack.InputPorts.First(), onUpdate.OutputPort);
            },
                EachEntity((manager, i, e) =>
                {
                    manager.AddComponentData(e, new Translation());

                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(coroutineType, Is.Not.Null);
                    Assert.That(manager.HasComponent(e, coroutineType), Is.Not.True);
                }),
                EachEntity((manager, i, e) => // Remove component, calling Return in the first coroutine state
                {
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.Not.True);
                })
            );
        }

        [Test]
        public void TestCoroutineReturnNodeInExecutionStack([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });

                var onUpdate = SetupOnUpdate(graph, query);
                var loopNode = onUpdate.CreateStackedNode<CoroutineNodeModel>("UnitTest", setup: n =>
                {
                    n.CoroutineType = typeof(UnitTestCoroutine).GenerateTypeHandle(Stencil);
                });

                var loopStack = graph.CreateLoopStack<CoroutineStackModel>(Vector2.down);
                graph.CreateEdge(loopStack.InputPort, loopNode.OutputPort);

                loopStack.CreateStackedNode<ReturnNodeModel>("Return", 0);

                var setProperty = loopStack.CreateSetPropertyGroupNode(1);
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setProperty.AddMember(member);
                ((FloatConstantModel)setProperty.InputConstantsById[member.GetId()]).value = 10f;

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setProperty.InstancePort, translation.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // MoveNext -> Execute loop stack
                EachEntity((manager, i, e) => Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f))),
                EachEntity((manager, i, e) =>
                {
                    // Return in execution stack does not complete the coroutine, it just skips to the next frame
                    // So entities still have the coroutine component
                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.True);
                })
            );
        }

        //     OnUpdate
        //       Wait
        //        If
        //        /\
        //    x=10  x=20
        [TestCase(CodeGenMode.Jobs, true, 10f)]
        [TestCase(CodeGenMode.NoJobs, true, 10f)]
        [TestCase(CodeGenMode.Jobs, false, 20f)]
        [TestCase(CodeGenMode.NoJobs, false, 20f)]
        public void TestCoroutineIfConditionNode(CodeGenMode mode, bool condition, float result)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode = onUpdate.CreateStackedNode<IfConditionNodeModel>();
                ((BooleanConstantNodeModel)ifNode.InputConstantsById["Condition"]).value = condition;

                // Then
                var thenStack = graph.CreateStack("then", Vector2.down);
                graph.CreateEdge(thenStack.InputPorts[0], ifNode.ThenPort);

                var setPropertyThen = thenStack.CreateSetPropertyGroupNode(0);
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setPropertyThen.AddMember(member);
                ((FloatConstantModel)setPropertyThen.InputConstantsById[member.GetId()]).value = 10f;

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setPropertyThen.InstancePort, translation.OutputPort);

                // Else
                var elseStack = graph.CreateStack("else", Vector2.down);
                graph.CreateEdge(elseStack.InputPorts[0], ifNode.ElsePort);

                var setPropertyElse = elseStack.CreateSetPropertyGroupNode(0);
                setPropertyElse.AddMember(member);
                ((FloatConstantModel)setPropertyElse.InputConstantsById[member.GetId()]).value = 20f;
                graph.CreateEdge(setPropertyElse.InstancePort, translation.OutputPort);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) =>
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(result));
                })
            );
        }

        //          OnUpdate
        //            Wait
        //             If
        //            /  \
        //         x=1    |
        //            \  /
        //             y=2
        [TestCase(CodeGenMode.Jobs, true, 1f, 2f)]
        [TestCase(CodeGenMode.NoJobs, true, 1f, 2f)]
        [TestCase(CodeGenMode.Jobs, false, 0f, 2f)]
        [TestCase(CodeGenMode.NoJobs, false, 0f, 2f)]
        public void TestCoroutineIfConditionNodeElseIsEndStack(CodeGenMode mode, bool condition, float x, float y)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode = onUpdate.CreateStackedNode<IfConditionNodeModel>();
                ((BooleanConstantNodeModel)ifNode.InputConstantsById["Condition"]).value = condition;

                // Then
                var thenStack = graph.CreateStack("then", Vector2.down);
                graph.CreateEdge(thenStack.InputPorts[0], ifNode.ThenPort);

                var setPropertyThen = thenStack.CreateSetPropertyGroupNode(0);
                var memberX = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                setPropertyThen.AddMember(memberX);
                ((FloatConstantModel)setPropertyThen.InputConstantsById[memberX.GetId()]).value = 1f;

                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setPropertyThen.InstancePort, translation.OutputPort);

                // Else
                var elseStack = graph.CreateStack("else", Vector2.down);
                graph.CreateEdge(elseStack.InputPorts[0], ifNode.ElsePort);

                var setPropertyElse = elseStack.CreateSetPropertyGroupNode(0);
                var memberY = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                setPropertyElse.AddMember(memberY);
                ((FloatConstantModel)setPropertyElse.InputConstantsById[memberY.GetId()]).value = 2f;
                graph.CreateEdge(setPropertyElse.InstancePort, translation.OutputPort);

                graph.CreateEdge(elseStack.InputPorts[0], thenStack.OutputPorts[0]);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => // Then or Else
                {
                    if (condition)
                    {
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(x));
                        Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(0f));
                    }
                    else
                    {
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f));
                        Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(y));
                    }
                }),
                EachEntity((manager, i, e) => // Else
                {
                    if (condition)
                    {
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(x));
                        Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(y));
                    }
                    else
                    {
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f));
                        Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(y));
                    }
                })
            );
        }

        //          OnUpdate
        //            Wait
        //             If
        //            /  \
        //          If    x=100
        //          /\
        //       x=1  x=10
        //          \/
        //          x=20
        [TestCase(CodeGenMode.Jobs, false, 100f)]
        [TestCase(CodeGenMode.NoJobs, false, 100f)]
        public void TestCoroutineNestedIfConditionNodeWithBrokenConnection(CodeGenMode mode, bool condition, float result)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                // Wait
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                // If 0
                var ifNode0 = onUpdate.CreateStackedNode<IfConditionNodeModel>();
                ((BooleanConstantNodeModel)ifNode0.InputConstantsById["Condition"]).value = condition;

                // Then 0
                var thenStack0 = graph.CreateStack("then0", Vector2.down);
                graph.CreateEdge(thenStack0.InputPorts[0], ifNode0.ThenPort);

                // Else 0
                var elseStack0 = graph.CreateStack("else0", Vector2.down);
                graph.CreateEdge(elseStack0.InputPorts[0], ifNode0.ElsePort);

                var setPropertyElse0 = elseStack0.CreateSetPropertyGroupNode(0);
                setPropertyElse0.AddMember(member);
                ((FloatConstantModel)setPropertyElse0.InputConstantsById[member.GetId()]).value = 100f;
                graph.CreateEdge(setPropertyElse0.InstancePort, translation.OutputPort);

                // If 1
                var ifNode1 = thenStack0.CreateStackedNode<IfConditionNodeModel>();

                // Then 1
                var thenStack1 = graph.CreateStack("then1", Vector2.down);
                graph.CreateEdge(thenStack1.InputPorts[0], ifNode1.ThenPort);

                var setPropertyThen1 = thenStack1.CreateSetPropertyGroupNode(0);
                setPropertyThen1.AddMember(member);
                ((FloatConstantModel)setPropertyThen1.InputConstantsById[member.GetId()]).value = 1f;
                graph.CreateEdge(setPropertyThen1.InstancePort, translation.OutputPort);

                // Else 1
                var elseStack1 = graph.CreateStack("else1", Vector2.down);
                graph.CreateEdge(elseStack1.InputPorts[0], ifNode1.ElsePort);

                var setPropertyElse1 = elseStack1.CreateSetPropertyGroupNode(0);
                setPropertyElse1.AddMember(member);
                ((FloatConstantModel)setPropertyElse1.InputConstantsById[member.GetId()]).value = 10f;
                graph.CreateEdge(setPropertyElse1.InstancePort, translation.OutputPort);

                // Complete
                var completeStack = graph.CreateStack("complete", Vector2.down);
                var setPropertyYComplete = completeStack.CreateSetPropertyGroupNode(0);
                setPropertyYComplete.AddMember(member);
                ((FloatConstantModel)setPropertyYComplete.InputConstantsById[member.GetId()]).value = 20f;
                graph.CreateEdge(setPropertyYComplete.InstancePort, translation.OutputPort);

                graph.CreateEdge(completeStack.InputPorts[0], thenStack1.OutputPorts[0]);
                graph.CreateEdge(completeStack.InputPorts[0], elseStack1.OutputPorts[0]);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => // Else (Set x). As return false, component is removed
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(result));

                    var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                    Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                })
            );
        }

        //          OnUpdate
        //            Wait
        //             If
        //            /  \
        //          If    x=100
        //          /\      |
        //       x=1  x=10  /
        //          \  |   /
        //            y=15
        //            z=25
        [TestCase(CodeGenMode.Jobs, true, true, 1f)]
        [TestCase(CodeGenMode.NoJobs, true, true, 1f)]
        [TestCase(CodeGenMode.Jobs, true, false, 10f)]
        [TestCase(CodeGenMode.NoJobs, true, false, 10f)]
        [TestCase(CodeGenMode.Jobs, false, false, 100f)]
        [TestCase(CodeGenMode.NoJobs, false, false, 100f)]
        [TestCase(CodeGenMode.Jobs, false, true, 100f)]
        [TestCase(CodeGenMode.NoJobs, false, true, 100f)]
        public void TestCoroutineNestedIfConditionNode(CodeGenMode mode, bool condition0, bool condition1, float result)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                // Wait
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                // If 0
                var ifNode0 = onUpdate.CreateStackedNode<IfConditionNodeModel>();
                ((BooleanConstantNodeModel)ifNode0.InputConstantsById["Condition"]).value = condition0;

                // Then 0
                var thenStack0 = graph.CreateStack("then0", Vector2.down);
                graph.CreateEdge(thenStack0.InputPorts[0], ifNode0.ThenPort);

                // Else 0
                var elseStack0 = graph.CreateStack("else0", Vector2.down);
                graph.CreateEdge(elseStack0.InputPorts[0], ifNode0.ElsePort);

                var setPropertyElse0 = elseStack0.CreateSetPropertyGroupNode(0);
                setPropertyElse0.AddMember(member);
                ((FloatConstantModel)setPropertyElse0.InputConstantsById[member.GetId()]).value = 100f;
                graph.CreateEdge(setPropertyElse0.InstancePort, translation.OutputPort);

                // If 1
                var ifNode1 = thenStack0.CreateStackedNode<IfConditionNodeModel>();
                ((BooleanConstantNodeModel)ifNode1.InputConstantsById["Condition"]).value = condition1;

                // Then 1
                var thenStack1 = graph.CreateStack("then1", Vector2.down);
                graph.CreateEdge(thenStack1.InputPorts[0], ifNode1.ThenPort);

                var setPropertyThen1 = thenStack1.CreateSetPropertyGroupNode(0);
                setPropertyThen1.AddMember(member);
                ((FloatConstantModel)setPropertyThen1.InputConstantsById[member.GetId()]).value = 1f;
                graph.CreateEdge(setPropertyThen1.InstancePort, translation.OutputPort);

                // Else 1
                var elseStack1 = graph.CreateStack("else1", Vector2.down);
                graph.CreateEdge(elseStack1.InputPorts[0], ifNode1.ElsePort);

                var setPropertyElse1 = elseStack1.CreateSetPropertyGroupNode(0);
                setPropertyElse1.AddMember(member);
                ((FloatConstantModel)setPropertyElse1.InputConstantsById[member.GetId()]).value = 10f;
                graph.CreateEdge(setPropertyElse1.InstancePort, translation.OutputPort);

                // Complete
                var completeStack = graph.CreateStack("complete", Vector2.down);
                var setPropertyYComplete = completeStack.CreateSetPropertyGroupNode(0);
                var memberY = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                setPropertyYComplete.AddMember(memberY);
                ((FloatConstantModel)setPropertyYComplete.InputConstantsById[memberY.GetId()]).value = 15f;
                graph.CreateEdge(setPropertyYComplete.InstancePort, translation.OutputPort);

                var setPropertyZComplete = completeStack.CreateSetPropertyGroupNode(1);
                var memberZ = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.z)
                });
                setPropertyZComplete.AddMember(memberZ);
                ((FloatConstantModel)setPropertyZComplete.InputConstantsById[memberZ.GetId()]).value = 25f;
                graph.CreateEdge(setPropertyZComplete.InstancePort, translation.OutputPort);

                graph.CreateEdge(completeStack.InputPorts[0], thenStack1.OutputPorts[0]);
                graph.CreateEdge(completeStack.InputPorts[0], elseStack1.OutputPorts[0]);
                graph.CreateEdge(completeStack.InputPorts[0], elseStack0.OutputPorts[0]);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => // Set x
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(result));
                }),
                EachEntity((manager, i, e) => // Set y and z
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(15f));
                    Assert.That(manager.GetComponentData<Translation>(e).Value.z, Is.EqualTo(25f));
                })
            );
        }

        //          OnUpdate
        //            Wait
        //             If
        //            /  \
        //       x=100    If
        //          |     /\
        //          |  x=1  x=10
        //           \  |  /
        //            y=15
        //            z=25
        [TestCase(CodeGenMode.Jobs, true, true, 100f)]
        [TestCase(CodeGenMode.NoJobs, true, true, 100f)]
        [TestCase(CodeGenMode.Jobs, true, false, 100f)]
        [TestCase(CodeGenMode.NoJobs, true, false, 100f)]
        [TestCase(CodeGenMode.Jobs, false, false, 10f)]
        [TestCase(CodeGenMode.NoJobs, false, false, 10f)]
        [TestCase(CodeGenMode.Jobs, false, true, 1f)]
        [TestCase(CodeGenMode.NoJobs, false, true, 1f)]
        public void TestCoroutineNestedIfConditionInElseStackNode(CodeGenMode mode, bool condition0, bool condition1,
            float result)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var memberX = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var memberY = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                var memberZ = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.z)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                // Wait
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode0 = AddIfNode(onUpdate, condition0);
                var thenStack0 = AddThenStack(graph, ifNode0);
                AddSetProperty(graph, thenStack0, translation, memberX, 100f);
                var elseStack0 = AddElseStack(graph, ifNode0);

                var ifNode1 = AddIfNode(elseStack0, condition1);
                var thenStack1 = AddThenStack(graph, ifNode1);
                AddSetProperty(graph, thenStack1, translation, memberX, 1f);
                var elseStack1 = AddElseStack(graph, ifNode1);
                AddSetProperty(graph, elseStack1, translation, memberX, 10f);

                var completeStack = graph.CreateStack("complete", Vector2.down);
                AddSetProperty(graph, completeStack, translation, memberY, 15f);
                AddSetProperty(graph, completeStack, translation, memberZ, 25f);
                graph.CreateEdge(completeStack.InputPorts[0], thenStack0.OutputPorts[0]);
                graph.CreateEdge(completeStack.InputPorts[0], elseStack1.OutputPorts[0]);
                graph.CreateEdge(completeStack.InputPorts[0], thenStack1.OutputPorts[0]);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => // Set x
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(result));
                }),
                EachEntity((manager, i, e) => // Set y and z
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(15f));
                    Assert.That(manager.GetComponentData<Translation>(e).Value.z, Is.EqualTo(25f));
                })
            );
        }

        //     OnUpdate
        //       Wait
        //        If
        //      /   \
        //    If     If
        //    / \   / \
        // x=1 x=2 x=3 x=4
        //    \ /   \ /
        //    y=1   y=2
        //      \   /
        //       z=1
        [TestCase(CodeGenMode.Jobs, true, true, true, 1f, 1f, 1f)]
        [TestCase(CodeGenMode.NoJobs, true, true, true, 1f, 1f, 1f)]
        [TestCase(CodeGenMode.Jobs, true, true, false, 1f, 1f, 1f)]
        [TestCase(CodeGenMode.NoJobs, true, true, false, 1f, 1f, 1f)]
        [TestCase(CodeGenMode.Jobs, true, false, true, 2f, 1f, 1f)]
        [TestCase(CodeGenMode.NoJobs, true, false, true, 2f, 1f, 1f)]
        [TestCase(CodeGenMode.Jobs, true, false, false, 2f, 1f, 1f)]
        [TestCase(CodeGenMode.NoJobs, true, false, false, 2f, 1f, 1f)]
        [TestCase(CodeGenMode.Jobs, false, true, true, 3f, 2f, 1f)]
        [TestCase(CodeGenMode.NoJobs, false, true, true, 3f, 2f, 1f)]
        [TestCase(CodeGenMode.Jobs, false, false, true, 3f, 2f, 1f)]
        [TestCase(CodeGenMode.NoJobs, false, false, true, 3f, 2f, 1f)]
        [TestCase(CodeGenMode.Jobs, false, true, false, 4f, 2f, 1f)]
        [TestCase(CodeGenMode.NoJobs, false, true, false, 4f, 2f, 1f)]
        [TestCase(CodeGenMode.Jobs, false, false, false, 4f, 2f, 1f)]
        [TestCase(CodeGenMode.NoJobs, false, false, false, 4f, 2f, 1f)]
        public void TestCoroutineTwoNestedIfConditions(CodeGenMode mode, bool condition1, bool condition2,
            bool condition3, float xResult, float yResult, float zResult)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var memberX = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var memberY = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                var memberZ = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.z)
                });

                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                // Wait
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode1 = AddIfNode(onUpdate, condition1);
                var then1 = AddThenStack(graph, ifNode1);
                var else1 = AddElseStack(graph, ifNode1);

                var ifNode2 = AddIfNode(then1, condition2);
                var then2 = AddThenStack(graph, ifNode2);
                var else2 = AddElseStack(graph, ifNode2);

                var ifNode3 = AddIfNode(else1, condition3);
                var then3 = AddThenStack(graph, ifNode3);
                var else3 = AddElseStack(graph, ifNode3);

                AddSetProperty(graph, then2, translation, memberX, 1f);
                AddSetProperty(graph, else2, translation, memberX, 2f);

                AddSetProperty(graph, then3, translation, memberX, 3f);
                AddSetProperty(graph, else3, translation, memberX, 4f);

                var complete1 = AddCompleteStack(graph, then2, else2);
                AddSetProperty(graph, complete1, translation, memberY, 1f);

                var complete2 = AddCompleteStack(graph, then3, else3);
                AddSetProperty(graph, complete2, translation, memberY, 2f);

                var complete3 = AddCompleteStack(graph, complete1, complete2);
                AddSetProperty(graph, complete3, translation, memberZ, 1f);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}), // Init coroutine
                EachEntity((manager, i, e) => {}), // Wait
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => {}), // If
                EachEntity((manager, i, e) => // Set x
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(xResult));
                }),
                EachEntity((manager, i, e) => // Set y
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.y, Is.EqualTo(yResult));
                }),
                EachEntity((manager, i, e) => // Set z
                {
                    Assert.That(manager.GetComponentData<Translation>(e).Value.z, Is.EqualTo(zResult));
                })
            );
        }

        //     OnUpdate
        //       Wait
        //        If
        //       /  \
        //    x=1
        [TestCase(CodeGenMode.Jobs, true)]
        [TestCase(CodeGenMode.NoJobs, true)]
        [TestCase(CodeGenMode.Jobs, false)]
        [TestCase(CodeGenMode.NoJobs, false)]
        public void TestCoroutineIfConditionMissingElseStatement(CodeGenMode mode, bool condition)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode1 = AddIfNode(onUpdate, condition);
                var then1 = AddThenStack(graph, ifNode1);
                AddSetProperty(graph, then1, translation, member, 1f);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init Wait
                EachEntity((manager, i, e) => {}),  // Wait
                EachEntity((manager, i, e) => // If
                {
                    if (!condition)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                    }
                }),
                EachEntity((manager, i, e) => // Then
                {
                    if (condition)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                    }

                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, condition ? Is.EqualTo(1f) : Is.EqualTo(0f));
                })
            );
        }

        //     OnUpdate
        //       Wait
        //        If
        //       /  \
        //           x=1
        [TestCase(CodeGenMode.Jobs, true)]
        [TestCase(CodeGenMode.NoJobs, true)]
        [TestCase(CodeGenMode.Jobs, false)]
        [TestCase(CodeGenMode.NoJobs, false)]
        public void TestCoroutineIfConditionMissingThenStatement(CodeGenMode mode, bool condition)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);
                onUpdate.CreateStackedNode<CoroutineNodeModel>("Wait", setup: n =>
                {
                    n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil);
                });

                var ifNode1 = AddIfNode(onUpdate, condition);
                var else1 = AddElseStack(graph, ifNode1);
                AddSetProperty(graph, else1, translation, member, 1f);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((manager, i, e) => {}),  // Init Wait
                EachEntity((manager, i, e) => {}),  // Wait
                EachEntity((manager, i, e) => // If
                {
                    if (condition)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                    }
                }),
                EachEntity((manager, i, e) => // Else
                {
                    if (!condition)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(manager.HasComponent(e, coroutineType), Is.False);
                    }

                    Assert.That(manager.GetComponentData<Translation>(e).Value.x, condition ? Is.EqualTo(0f) : Is.EqualTo(1f));
                })
            );
        }

        //     OnUpdate
        //        If
        //       /  \
        //   Wait    Wait
        //    If     |
        //    /\     |
        //   |  x=1  |
        //    \ |   /
        //      x=2
        [TestCase(CodeGenMode.Jobs, true, true), Description("VSB-336 regression test")]
        [TestCase(CodeGenMode.NoJobs, true, true)]
        [TestCase(CodeGenMode.Jobs, true, false)]
        [TestCase(CodeGenMode.NoJobs, true, false)]
        [TestCase(CodeGenMode.Jobs, false, true)]
        [TestCase(CodeGenMode.NoJobs, false, true)]
        [TestCase(CodeGenMode.Jobs, false, false)]
        [TestCase(CodeGenMode.NoJobs, false, false)]
        public void TestCoroutineIfConditionThreeWayJoin(CodeGenMode mode, bool condition1, bool condition2)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                var ifNode1 = AddIfNode(onUpdate, condition1);
                var thenStack1 = AddThenStack(graph, ifNode1);
                var elseStack1 = AddElseStack(graph, ifNode1);

                AddCoroutineNodeModel<Wait>(thenStack1);

                var ifNode2 = AddIfNode(thenStack1, condition2);
                var thenStack2 = AddThenStack(graph, ifNode2);
                var elseStack2 = AddElseStack(graph, ifNode2);

                AddSetProperty(graph, thenStack2, translation, member, 2f);
                AddSetProperty(graph, elseStack2, translation, member, 1f);

                graph.CreateEdge(thenStack2.InputPorts.First(), elseStack2.OutputPorts.First());

                AddCoroutineNodeModel<Wait>(elseStack1);

                graph.CreateEdge(thenStack2.InputPorts.First(), elseStack1.OutputPorts.First());
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((m, i, e) => {}),  // If
                EachEntity((m, i, e) => {}),  // Wait Init
                EachEntity((m, i, e) => {}),  // Wait
                EachEntity((m, i, e) => // If OR x=2
                {
                    if (!condition1)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(m.HasComponent(e, coroutineType), Is.False);
                    }

                    Assert.That(m.GetComponentData<Translation>(e).Value.x, condition1 ? Is.EqualTo(0f) : Is.EqualTo(2f));
                }),
                EachEntity((m, i, e) => // Then/Else (second If)
                {
                    if (condition1)
                    {
                        if (condition2)
                        {
                            var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                            Assert.That(coroutineType, Is.Not.Null);
                            Assert.That(m.HasComponent(e, coroutineType), Is.False);
                        }

                        Assert.That(m.GetComponentData<Translation>(e).Value.x, condition2 ? Is.EqualTo(2f) : Is.EqualTo(1f));
                    }
                }),
                EachEntity((m, i, e) =>
                {
                    if (condition1)
                    {
                        if (!condition2)
                        {
                            var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                            Assert.That(coroutineType, Is.Not.Null);
                            Assert.That(m.HasComponent(e, coroutineType), Is.False);
                        }

                        Assert.That(m.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f));
                    }
                })
            );
        }

        //     OnUpdate
        //        If
        //       /  \
        //   Wait    Wait
        //      |     If
        //      |     /\
        //      |    |  x=1
        //       \   | /
        //         x=2
        [TestCase(CodeGenMode.Jobs, true, true), Description("VSB-336 regression test")]
        [TestCase(CodeGenMode.NoJobs, true, true)]
        [TestCase(CodeGenMode.Jobs, true, false)]
        [TestCase(CodeGenMode.NoJobs, true, false)]
        [TestCase(CodeGenMode.Jobs, false, true)]
        [TestCase(CodeGenMode.NoJobs, false, true)]
        [TestCase(CodeGenMode.Jobs, false, false)]
        [TestCase(CodeGenMode.NoJobs, false, false)]
        public void TestCoroutineIfConditionThreeWayJoinOtherSide(CodeGenMode mode, bool condition1, bool condition2)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var member = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                var ifNode1 = AddIfNode(onUpdate, condition1);
                var thenStack1 = AddThenStack(graph, ifNode1);
                var elseStack1 = AddElseStack(graph, ifNode1);

                AddCoroutineNodeModel<Wait>(thenStack1);
                AddCoroutineNodeModel<Wait>(elseStack1);

                var ifNode2 = AddIfNode(elseStack1, condition2);
                var thenStack2 = AddThenStack(graph, ifNode2);
                var elseStack2 = AddElseStack(graph, ifNode2);

                AddSetProperty(graph, thenStack2, translation, member, 2f);
                AddSetProperty(graph, elseStack2, translation, member, 1f);

                graph.CreateEdge(thenStack2.InputPorts.First(), elseStack2.OutputPorts.First());
                graph.CreateEdge(thenStack2.InputPorts.First(), thenStack1.OutputPorts.First());
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((m, i, e) => {}),  // If
                EachEntity((m, i, e) => {}),  // Wait Init
                EachEntity((m, i, e) => {}),  // Wait
                EachEntity((m, i, e) => // x=2 OR If
                {
                    if (condition1)
                    {
                        var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                        Assert.That(coroutineType, Is.Not.Null);
                        Assert.That(m.HasComponent(e, coroutineType), Is.False);
                    }

                    Assert.That(m.GetComponentData<Translation>(e).Value.x, condition1 ? Is.EqualTo(2f) : Is.EqualTo(0f));
                }),
                EachEntity((m, i, e) => // Then/Else (second If)
                {
                    if (!condition1)
                    {
                        if (condition2)
                        {
                            var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                            Assert.That(coroutineType, Is.Not.Null);
                            Assert.That(m.HasComponent(e, coroutineType), Is.False);
                        }

                        Assert.That(m.GetComponentData<Translation>(e).Value.x, condition2 ? Is.EqualTo(2f) : Is.EqualTo(1f));
                    }
                }),
                EachEntity((m, i, e) =>
                {
                    if (!condition1)
                    {
                        if (!condition2)
                        {
                            var coroutineType = m_SystemType.GetNestedTypes().First(t => t.Name.Contains("Coroutine"));
                            Assert.That(coroutineType, Is.Not.Null);
                            Assert.That(m.HasComponent(e, coroutineType), Is.False);
                        }

                        Assert.That(m.GetComponentData<Translation>(e).Value.x, Is.EqualTo(2f));
                    }
                })
            );
        }

        [Test]
        public void TestNestedCoroutine([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var memberX = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.x)
                });
                var memberY = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.y)
                });
                var memberZ = new TypeMember(TypeHandle.Float, new List<string>
                {
                    nameof(Translation.Value), nameof(Translation.Value.z)
                });

                // On Update
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var translation = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Translation).GenerateTypeHandle(Stencil)), Vector2.zero);

                // Wait Until z > 100
                var waitUntil = AddCoroutineNodeModel<WaitUntil>(onUpdate);
                var getZ = AddGetProperty(graph, translation, memberZ);
                graph.CreateEdge(getZ.InstancePort, translation.OutputPort);
                var equalNode = graph.CreateBinaryOperatorNode(BinaryOperatorKind.GreaterThan, Vector2.zero);
                graph.CreateEdge(equalNode.InputPortA, getZ.GetPortsForMembers().Single());
                ((FloatConstantModel)equalNode.InputConstantsById[equalNode.InputPortB.UniqueId]).value = 100f;
                var moveNextParam = typeof(WaitUntil).GetMethod(nameof(WaitUntil.MoveNext))?.GetParameters().Single();
                graph.CreateEdge(waitUntil.GetParameterPort(moveNextParam), equalNode.OutputPort);

                // x = 5f
                AddSetProperty(graph, onUpdate, translation, memberX, 5f);

                // While Wait Until z > 100, Wait Until z > 50
                var loopStack = graph.CreateLoopStack<CoroutineStackModel>(Vector2.down);
                graph.CreateEdge(loopStack.InputPort, waitUntil.OutputPort);
                var nestedWaitUntil = AddCoroutineNodeModel<WaitUntil>(loopStack);
                var equalNestedNode = graph.CreateBinaryOperatorNode(BinaryOperatorKind.GreaterThan, Vector2.zero);
                graph.CreateEdge(equalNestedNode.InputPortA, getZ.GetPortsForMembers().Single());
                ((FloatConstantModel)equalNestedNode.InputConstantsById[equalNestedNode.InputPortB.UniqueId]).value = 50f;
                graph.CreateEdge(nestedWaitUntil.GetParameterPort(moveNextParam), equalNestedNode.OutputPort);

                // y = 2f
                AddSetProperty(graph, loopStack, translation, memberY, 2f);
            },
                EachEntity((manager, i, e) => manager.AddComponentData(e, new Translation())),
                EachEntity((m, i, e) => {}),  // WaitUntil Init
                EachEntity((m, i, e) =>  // Nested WaitUntil Init
                {
                    var t = m.GetComponentData<Translation>(e);
                    t.Value.z = 60f;
                    m.SetComponentData(e, t);
                }),
                EachEntity((m, i, e) => // Nested WaitUntil Update
                {
                    Assert.That(m.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f));
                    Assert.That(m.GetComponentData<Translation>(e).Value.y, Is.EqualTo(0f));
                }),
                EachEntity((m, i, e) => // y = 2f
                {
                    Assert.That(m.GetComponentData<Translation>(e).Value.x, Is.EqualTo(0f));
                    Assert.That(m.GetComponentData<Translation>(e).Value.y, Is.EqualTo(2f));

                    var t = m.GetComponentData<Translation>(e);
                    t.Value.z = 110f;
                    m.SetComponentData(e, t);
                }),
                EachEntity((m, i, e) => {}),  // WaitUntil Update
                EachEntity((m, i, e) => // x = 5f
                {
                    Assert.That(m.GetComponentData<Translation>(e).Value.x, Is.EqualTo(5f));
                    Assert.That(m.GetComponentData<Translation>(e).Value.y, Is.EqualTo(2f));
                })
            );
        }

        [Test, Description("VSB-351 regression test")]
        public void TestCoroutineInForEachContext([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var translationQuery = SetupQuery(graph, "translationQuery", new[] { typeof(Translation) });
                var scaleQuery = SetupQuery(graph, "scaleQuery", new[] { typeof(Scale) });
                var onUpdate = SetupOnUpdate(graph, translationQuery);

                var forAllStack = graph.CreateLoopStack<ForAllEntitiesStackModel>(Vector2.zero);
                var forAllNode = forAllStack.CreateLoopNode(onUpdate, 0) as ForAllEntitiesNodeModel;
                Assert.That(forAllNode, Is.Not.Null);
                graph.CreateEdge(forAllNode.InputPort, scaleQuery.OutputPort);
                graph.CreateEdge(forAllStack.InputPort, forAllNode.OutputPort);

                AddCoroutineNodeModel<Wait>(forAllStack);

                var setScale = forAllStack.CreateStackedNode<SetComponentNodeModel>("set scale");
                setScale.ComponentType = typeof(Scale).GenerateTypeHandle(graph.Stencil);
                setScale.DefineNode();
                ((FloatConstantModel)setScale.InputConstantsById["Value"]).value = 10f;

                var setTranslation = forAllStack.CreateStackedNode<SetComponentNodeModel>("set translation");
                setTranslation.ComponentType = typeof(Translation).GenerateTypeHandle(graph.Stencil);
                setTranslation.DefineNode();
                ((FloatConstantModel)setTranslation.InputConstantsById["x"]).value = 5f;

                var entity = graph.CreateVariableNode(onUpdate.FunctionParameterModels.Single(p =>
                    p.DataType == typeof(Entity).GenerateTypeHandle(Stencil)), Vector2.zero);
                graph.CreateEdge(setTranslation.EntityPort, entity.OutputPort);
            },
                EachEntity((manager, i, e) =>
                {
                    if (i % 2 == 0)
                        manager.AddComponentData(e, new Translation());
                    else
                        manager.AddComponentData(e, new Scale());
                }),
                EachEntity((manager, i, e) => {}),  // Init State
                EachEntity((manager, i, e) => {}),  // Wait MoveNext
                EachEntity((manager, i, e) => // ForEach set Scale
                {
                    if (manager.HasComponent<Scale>(e))
                        Assert.That(manager.GetComponentData<Scale>(e).Value, Is.EqualTo(10f));

                    if (manager.HasComponent<Translation>(e))
                        Assert.That(manager.GetComponentData<Translation>(e).Value.x, Is.EqualTo(5f));
                })
            );
        }

        [Test, Description("VSB-349 regression test")]
        public void TestCoroutineGetGraphVariable([Values] CodeGenMode mode)
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

                AddCoroutineNodeModel<Wait>(onUpdate);

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

        [Test, Description("VSB-349 regression test")]
        public void TestCoroutineSetGraphVariable([Values(CodeGenMode.NoJobs, CodeGenMode.NoJobsTracing)] CodeGenMode mode)
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

                AddCoroutineNodeModel<Wait>(onUpdate);

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

        static CoroutineNodeModel AddCoroutineNodeModel<T>(IStackModel stack) where T : ICoroutine
        {
            return stack.CreateStackedNode<CoroutineNodeModel>("Coroutine", setup: n =>
            {
                n.CoroutineType = typeof(T).GenerateTypeHandle(stack.GraphModel.Stencil);
            });
        }

        static IfConditionNodeModel AddIfNode(IStackModel stack, bool condition)
        {
            var ifNode = stack.CreateStackedNode<IfConditionNodeModel>();
            ((BooleanConstantNodeModel)ifNode.InputConstantsById["Condition"]).value = condition;
            return ifNode;
        }

        static StackBaseModel AddThenStack(GraphModel graph, IfConditionNodeModel ifNode)
        {
            var thenStack = graph.CreateStack("then", Vector2.down);
            graph.CreateEdge(thenStack.InputPorts[0], ifNode.ThenPort);
            return thenStack;
        }

        static StackBaseModel AddElseStack(GraphModel graph, IfConditionNodeModel ifNode)
        {
            var elseStack = graph.CreateStack("else", Vector2.down);
            graph.CreateEdge(elseStack.InputPorts[0], ifNode.ElsePort);
            return elseStack;
        }

        static void AddSetProperty(GraphModel graph, IStackModel stack, IVariableModel variable, TypeMember member,
            float value)
        {
            var setPropertyElse = stack.CreateSetPropertyGroupNode(stack.NodeModels.Count);
            setPropertyElse.AddMember(member);
            ((FloatConstantModel)setPropertyElse.InputConstantsById[member.GetId()]).value = value;
            graph.CreateEdge(setPropertyElse.InstancePort, variable.OutputPort);
        }

        static GetPropertyGroupNodeModel AddGetProperty(GraphModel graph, IVariableModel variable, TypeMember member)
        {
            var getProperty = graph.CreateGetPropertyGroupNode(Vector2.down);
            getProperty.AddMember(member);
            graph.CreateEdge(getProperty.InstancePort, variable.OutputPort);
            return getProperty;
        }

        static IStackModel AddCompleteStack(GraphModel graph, IStackModel thenStack, IStackModel elseStack)
        {
            var completeStack = graph.CreateStack("complete", Vector2.down);
            graph.CreateEdge(completeStack.InputPorts[0], thenStack.OutputPorts[0]);
            graph.CreateEdge(completeStack.InputPorts[0], elseStack.OutputPorts[0]);
            return completeStack;
        }
    }
}
