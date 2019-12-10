using System;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Transforms;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScriptingTests;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests
{
    public class ComponentQueryTests : BaseFixture
    {
        protected override bool CreateGraphOnStartup => true;
        protected override Type CreatedGraphType => typeof(EcsStencil);

        [Test]
        public void CreateComponentQueryDeclarationAction([Values] TestingMode mode)
        {
            TestPrereqActionPostreq(mode, () =>
            {
                Assert.That(GraphModel.VariableDeclarations.Count, Is.Zero);
                return new CreateComponentQueryAction("g1");
            }, () => Assert.That(GraphModel.VariableDeclarations.Count, Is.EqualTo(1)));
        }

        [Test]
        public void CreateComponentQueryFromGameObjectAction([Values] TestingMode mode)
        {
            var gameObject = new GameObject();
            TestPrereqActionPostreq(mode, () =>
            {
                Assert.That(GraphModel.VariableDeclarations.Count, Is.Zero);
                return new CreateComponentQueryFromGameObjectAction(gameObject);
            }, () =>
                {
                    Assert.That(GraphModel.VariableDeclarations.Count, Is.EqualTo(1));
                    var decl = GraphModel.VariableDeclarations.OfType<ComponentQueryDeclarationModel>().Single();
                    Assert.That(decl.Components.Count(), Is.AtLeast(1));
                });
        }

        [Test]
        public void CreateComponentQueryAndNodeFromGameObjectAction([Values] TestingMode mode)
        {
            var gameObject = new GameObject();
            var position = Vector2.zero;

            TestPrereqActionPostreq(mode, () =>
            {
                Assert.That(GraphModel.VariableDeclarations.Count, Is.Zero);
                Assert.That(GraphModel.NodeModels.Count, Is.Zero);
                return new CreateQueryAndElementFromGameObjectAction(gameObject, position);
            }, () =>
                {
                    Assert.That(GraphModel.VariableDeclarations.Count, Is.EqualTo(1));
                    Assert.That(GraphModel.NodeModels.Count, Is.EqualTo(1));
                    var decl = GraphModel.VariableDeclarations.OfType<ComponentQueryDeclarationModel>().Single();
                    Assert.That(decl.Components.Count(), Is.AtLeast(1));
                });
        }

        [Test]
        public void AddComponentToQuery([Values] TestingMode mode)
        {
            var query = GraphModel.CreateComponentQuery("query");
            var rotationType = typeof(Rotation).GenerateTypeHandle(Stencil);
            TestPrereqActionPostreq(mode, () =>
            {
                query = GetVariableDeclaration(0) as ComponentQueryDeclarationModel;
                Assert.That(GraphModel.VariableDeclarations.Count, Is.EqualTo(1));
                Assert.That(query.Components.Count(), Is.EqualTo(0));
                return new AddComponentToQueryAction(query, rotationType, ComponentDefinitionFlags.None);
            }, () =>
                {
                    query = GetVariableDeclaration(0) as ComponentQueryDeclarationModel;

                    Assert.That(query.Components.Count(), Is.EqualTo(1));
                    Assert.That(query.Components.Single().Component.TypeHandle, Is.EqualTo(rotationType));
                });
        }
    }
}
