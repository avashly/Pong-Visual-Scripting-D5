using System;
using NUnit.Framework;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using VisualScripting.Entities.Runtime;

namespace UnityEditor.VisualScriptingECSTests
{
    public class RandomNodeCodeGenTests : EndToEndCodeGenBaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void RandomFloatCall([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                var query = SetupQuery(graph, "query", new[] { typeof(Translation) });
                var onUpdate = SetupOnUpdate(graph, query);
                var log = onUpdate.CreateStackedNode<LogNodeModel>();
                var rnd = graph.CreateNode<RandomNodeModel>();
                graph.CreateEdge(log.InputPort, rnd.OutputPort);
            });
        }

        [Test]
        public void RandomCallsInMultipleStacks([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var query = SetupQuery(graph, "query" + i, new[] { typeof(Translation) });
                    var onUpdate = SetupOnUpdate(graph, query);
                    var log = onUpdate.CreateStackedNode<LogNodeModel>();
                    var rnd = graph.CreateNode<RandomNodeModel>();
                    graph.CreateEdge(log.InputPort, rnd.OutputPort);
                }
            });
        }

        [Test]
        public void RandomCallInCoroutine([Values] CodeGenMode mode)
        {
            SetupTestGraphMultipleFrames(mode, graph =>
            {
                for (int i = 0; i < 2; i++)
                {
                    var query = SetupQuery(graph, "query" + i, new[] { typeof(Translation) });
                    var onUpdate = SetupOnUpdate(graph, query);
                    onUpdate.CreateStackedNode<CoroutineNodeModel>("wait", setup: n => n.CoroutineType = typeof(Wait).GenerateTypeHandle(Stencil));
                    var log = onUpdate.CreateStackedNode<LogNodeModel>();
                    var rnd = graph.CreateNode<RandomNodeModel>();
                    graph.CreateEdge(log.InputPort, rnd.OutputPort);
                }
            });
        }
    }
}
