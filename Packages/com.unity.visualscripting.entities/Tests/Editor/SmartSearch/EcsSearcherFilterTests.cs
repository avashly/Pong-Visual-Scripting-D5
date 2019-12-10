using System;
using System.Reflection;
using Moq;
using NUnit.Framework;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.SmartSearch;
using VisualScripting.Entities.Runtime;

namespace UnityEditor.VisualScriptingECSTests.SmartSearch
{
    public class EcsSearcherFilterTests
    {
        [TestCase(typeof(Wait), true, false)]
        [TestCase(typeof(Wait), false, false)]
        [TestCase(typeof(IfConditionNodeModel), true, true)]
        [TestCase(typeof(IfConditionNodeModel), false, false)]
        public void TestWithControlFlowExcept(Type nodeType, bool acceptNode, bool expected)
        {
            var stackMock = new Mock<IStackModel>();
            stackMock.Setup(s => s.AcceptNode(It.IsAny<Type>())).Returns(acceptNode);

            var filter = new SearcherFilter(SearcherContext.Graph)
                .WithControlFlowExcept(stackMock.Object, new[] { typeof(ICoroutine) });
            var data = new ControlFlowSearcherItemData(nodeType);

            var applyFilter = typeof(SearcherFilter).GetMethod(
                "ApplyFilters", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = applyFilter?.Invoke(filter, new object[] { data });
            Assert.AreEqual(expected, result);
        }
    }
}
