using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using UnityEditor.VisualScripting.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;

namespace UnityEditor.VisualScriptingTests.Extensions
{
    // ReSharper disable once InconsistentNaming
    public class IStackModelExtensionsTests
    {
        static void SetupAndConnect(Mock<IStackModel> source, IEnumerable<Mock<IStackModel>> connections)
        {
            source.SetupGet(t => t.NodeModels).Returns(new List<INodeModel>());
            var outputPort = new Mock<IPortModel>();
            source.SetupGet(t => t.OutputPorts).Returns(new[] { outputPort.Object });

            var connectionPortModels = new List<Mock<IPortModel>>();
            foreach (var connection in connections)
            {
                var stackConnection = new Mock<IPortModel>();
                stackConnection.SetupGet(t => t.NodeModel).Returns(connection.Object);
                connectionPortModels.Add(stackConnection);
            }

            outputPort.SetupGet(t => t.ConnectionPortModels).Returns(connectionPortModels.Select(c => c.Object));
        }

        static void AddCoroutineNode(Mock<IStackModel> stack)
        {
            var coroutineMock = new Mock<CoroutineNodeModel>();
            stack.SetupGet(t => t.NodeModels).Returns(new[] { coroutineMock.Object });
        }

        [Test]
        public void TestContainsCoroutineNoChild()
        {
            var stackMock = new Mock<IStackModel>();
            stackMock.SetupGet(t => t.NodeModels).Returns(new List<INodeModel>());
            stackMock.SetupGet(t => t.OutputPorts).Returns(new List<IPortModel>());

            Assert.IsFalse(stackMock.Object.ContainsCoroutine());
        }

        [Test]
        public void TestContainsCoroutineDirectChild()
        {
            var stackMock = new Mock<IStackModel>();
            AddCoroutineNode(stackMock);

            Assert.IsTrue(stackMock.Object.ContainsCoroutine());
        }

        //   Stack
        //     |
        //   Stack
        //     |
        // Coroutine
        [Test]
        public void TestContainsCoroutineInHierarchy()
        {
            var stackMockA = new Mock<IStackModel>();
            var stackMockB = new Mock<IStackModel>();
            var stackMockC = new Mock<IStackModel>();

            SetupAndConnect(stackMockA, new[] { stackMockB });
            SetupAndConnect(stackMockB, new[] { stackMockC });
            AddCoroutineNode(stackMockC);

            Assert.IsTrue(stackMockA.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockB.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockC.Object.ContainsCoroutine());
        }

        //     Stack
        //      /\
        // Stack  Stack
        //      \/
        //   Coroutine
        [Test]
        public void TestContainsCoroutineInDiamondHierarchy()
        {
            var stackMockA = new Mock<IStackModel>();
            var stackMockB = new Mock<IStackModel>();
            var stackMockC = new Mock<IStackModel>();
            var stackMockD = new Mock<IStackModel>();

            SetupAndConnect(stackMockA, new[] { stackMockB, stackMockC });
            SetupAndConnect(stackMockB, new[] { stackMockD });
            SetupAndConnect(stackMockC, new[] { stackMockD });
            AddCoroutineNode(stackMockD);

            Assert.IsTrue(stackMockA.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockB.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockC.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockD.Object.ContainsCoroutine());
        }

        //     Stack
        //      /\
        // Stack  Stack
        //       /
        //   Coroutine
        [Test]
        public void TestContainsCoroutineInDiamondHierarchyWithNoStackBConnected()
        {
            var stackMockA = new Mock<IStackModel>();
            var stackMockB = new Mock<IStackModel>();
            var stackMockC = new Mock<IStackModel>();
            var stackMockD = new Mock<IStackModel>();

            stackMockB.SetupGet(t => t.NodeModels).Returns(new List<INodeModel>());
            stackMockB.SetupGet(t => t.OutputPorts).Returns(new List<IPortModel>());

            SetupAndConnect(stackMockA, new[] { stackMockB, stackMockC });
            SetupAndConnect(stackMockC, new[] { stackMockD });
            AddCoroutineNode(stackMockD);

            Assert.IsTrue(stackMockA.Object.ContainsCoroutine());
            Assert.IsFalse(stackMockB.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockC.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockD.Object.ContainsCoroutine());
        }

        //     Stack
        //      /\
        // Stack  Stack
        //      \
        //   Coroutine
        [Test]
        public void TestContainsCoroutineInDiamondHierarchyWithNoStackCConnected()
        {
            var stackMockA = new Mock<IStackModel>();
            var stackMockB = new Mock<IStackModel>();
            var stackMockC = new Mock<IStackModel>();
            var stackMockD = new Mock<IStackModel>();

            stackMockC.SetupGet(t => t.NodeModels).Returns(new List<INodeModel>());
            stackMockC.SetupGet(t => t.OutputPorts).Returns(new List<IPortModel>());

            SetupAndConnect(stackMockA, new[] { stackMockB, stackMockC });
            SetupAndConnect(stackMockB, new[] { stackMockD });
            AddCoroutineNode(stackMockD);

            Assert.IsTrue(stackMockA.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockB.Object.ContainsCoroutine());
            Assert.IsFalse(stackMockC.Object.ContainsCoroutine());
            Assert.IsTrue(stackMockD.Object.ContainsCoroutine());
        }
    }
}
