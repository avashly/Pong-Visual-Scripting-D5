using System;
using NUnit.Framework;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScriptingTests;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Nodes
{
    class RandomNodeModelTests : BaseFixture
    {
        protected override bool CreateGraphOnStartup => true;

        [Test]
        public void TestDefaultRandomNodeModelHasMethod()
        {
            Assert.That(RandomNodeModel.DefaultMethod, Is.Not.Null);
        }

        public enum RandomSupportedTypes
        {
            Bool, Int, Float, Float2, Float3, Float4, Double
        }

        [Test]
        public void TestRandomNodeModelAllowsVariousTypes([Values] RandomSupportedTypes randomType, [Values] RandomNodeModel.ParamVariant variant)
        {
            GUID nodeGuid = GUID.Generate();
            TestPrereqActionPostreq(TestingMode.Action, () =>
            {
                var n = GraphModel.CreateNode<RandomNodeModel>(RandomNodeModel.MakeTitle(RandomNodeModel.DefaultMethod), preDefineSetup: m =>
                {
                    m.Variant = variant;
                    m.MethodBaseName = randomType.ToString();
                }, guid: nodeGuid);
                Assert.That(GraphModel.NodeModels.Count, Is.EqualTo(1));
                return new RefreshUIAction(UpdateFlags.All);
            }, () =>
                {
                    Assert.That(GraphModel.NodeModels.Count, Is.EqualTo(1));
                    Assert.That(GraphModel.NodesByGuid.TryGetValue(nodeGuid, out var n), Is.True);
                    var rng = n as RandomNodeModel;
                    Assert.That(rng, Is.Not.Null);
                    Assert.That(rng.RngMethod.Name, Does.EndWith(randomType.ToString()));
                    int expectedParams = 0;
                    if (randomType != RandomSupportedTypes.Bool && variant != RandomNodeModel.ParamVariant.NoParameters)
                        expectedParams = variant == RandomNodeModel.ParamVariant.Max ? 1 : 2;
                    Assert.That(rng.RngMethod.GetParameters().Length, Is.EqualTo(expectedParams));
                });
        }
    }
}
