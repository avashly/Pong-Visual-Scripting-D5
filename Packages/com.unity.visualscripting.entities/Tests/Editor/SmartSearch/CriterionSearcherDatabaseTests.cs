using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Moq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Model.Compilation;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScriptingECSTests.SmartSearch
{
    class CriterionSearcherDatabaseTests
    {
        TestStencil m_Stencil;
        ComponentQueryDeclarationModel m_Query;
        GraphAssetModel m_Asset;

        sealed class TestStencil : Stencil
        {
            public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
            {
                return new EcsSearcherDatabaseProvider(this);
            }

            [CanBeNull]
            public override IBuilder Builder => null;
        }

#pragma warning disable 0649
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        struct TestObject
        {
            public TestChildObject Child;
        }
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        sealed class TestChildObject {}

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        struct Vec1
        {
            public float this[int index]
            {
                get => 0;
                set => x = value;
            }

            public float x;
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        enum TestEnum
        {
            None,
            Some
        }

        struct TestFloat3Component : IComponentData
        {
            public float3 Value;
        }

        struct TestIntComponent : IComponentData
        {
            public int Value;
        }

        struct TestQuaternionComponent : IComponentData
        {
            public quaternion Rotation;
        }

        struct TestObjectComponent : ISharedComponentData, IEquatable<TestObjectComponent>
        {
            public TestObject Object;
            public float3 Value;

            public bool Equals(TestObjectComponent other)
            {
                return Equals(Object, other.Object) && Value.Equals(other.Value);
            }

            public override bool Equals(object obj)
            {
                return obj is TestObjectComponent other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Object.GetHashCode() * 397) ^ Value.GetHashCode();
                }
            }
        }

        struct TestEnumComponent : IComponentData
        {
            public TestEnum Value;
        }

        struct TestVec1Component : IComponentData
        {
            public Vec1 Value;
        }
#pragma warning restore 0649

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            m_Asset = VSGraphAssetModel.Create("test", "", typeof(VSGraphAssetModel));
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Object.DestroyImmediate(m_Asset);
        }

        [SetUp]
        public void SetUp()
        {
            m_Stencil = new TestStencil();
            m_Query = new ComponentQueryDeclarationModel();
            var graph = m_Asset.CreateGraph<VSGraphModel>("graph", typeof(EcsStencil), false);
            m_Query.GraphModel = graph;
        }

        [Test]
        public void TestComponentWithBasicNumericField()
        {
            m_Query.AddComponent(m_Stencil, typeof(TestIntComponent).GenerateTypeHandle(m_Stencil), ComponentDefinitionFlags.None);

            var db = new CriterionSearcherDatabase(m_Stencil, m_Query).Build();
            ValidateHierarchy(db.Search("", out _), new[]
            {
                new SearcherItem("TestIntComponent", string.Empty, new List<SearcherItem>
                {
                    new SearcherItem("Value", string.Empty, GetMathsOperators().ToList())
                }),
            });
        }

        [Test]
        public void TestComponentWithQuaternionField()
        {
            m_Query.AddComponent(m_Stencil, typeof(TestQuaternionComponent).GenerateTypeHandle(m_Stencil), ComponentDefinitionFlags.None);

            var db = new CriterionSearcherDatabase(m_Stencil, m_Query).Build();
            ValidateHierarchy(db.Search("", out _), new[]
            {
                new SearcherItem("TestQuaternionComponent", string.Empty, new List<SearcherItem>
                {
                    new SearcherItem("Rotation", string.Empty, new List<SearcherItem>
                    {
                        new SearcherItem("value", string.Empty, new List<SearcherItem>(GetMathsOperators())
                        {
                            new SearcherItem("x", string.Empty, GetMathsOperators().ToList()),
                            new SearcherItem("y", string.Empty, GetMathsOperators().ToList()),
                            new SearcherItem("z", string.Empty, GetMathsOperators().ToList()),
                            new SearcherItem("w", string.Empty, GetMathsOperators().ToList()),
                        })
                    })
                })
            });
        }

        [Test(Description = "Object and Child shouldn't appear as they do not contain any operator")]
        public void TestComponentWithObject()
        {
            m_Query.AddComponent(m_Stencil, typeof(TestObjectComponent).GenerateTypeHandle(m_Stencil), ComponentDefinitionFlags.None);

            var db = new CriterionSearcherDatabase(m_Stencil, m_Query).Build();
            ValidateHierarchy(db.Search("", out _), new[]
            {
                new SearcherItem("TestObjectComponent", string.Empty, new List<SearcherItem>
                {
                    new SearcherItem("Value", string.Empty, new List<SearcherItem>(GetMathsOperators())
                    {
                        new SearcherItem("x", string.Empty, GetMathsOperators().ToList()),
                        new SearcherItem("y", string.Empty, GetMathsOperators().ToList()),
                        new SearcherItem("z", string.Empty, GetMathsOperators().ToList())
                    })
                })
            });
        }

        [Test(Description = "The extra value__ property of an Enum should not appear")]
        public void TestComponentWithEnum()
        {
            m_Query.AddComponent(m_Stencil, typeof(TestEnumComponent).GenerateTypeHandle(m_Stencil), ComponentDefinitionFlags.None);

            var db = new CriterionSearcherDatabase(m_Stencil, m_Query).Build();
            ValidateHierarchy(db.Search("", out _), new[]
            {
                new SearcherItem("TestEnumComponent", string.Empty, new List<SearcherItem>
                {
                    new SearcherItem("Value", string.Empty, new List<SearcherItem>(GetMathsOperators()))
                })
            });
        }

        [Test(Description = "Things like Vec1.this[int] property should not appear")]
        public void TestComponentWithPropertyWithManyParameters()
        {
            m_Query.AddComponent(m_Stencil, typeof(TestVec1Component).GenerateTypeHandle(m_Stencil), ComponentDefinitionFlags.None);

            var db = new CriterionSearcherDatabase(m_Stencil, m_Query).Build();
            ValidateHierarchy(db.Search("", out _), new[]
            {
                new SearcherItem("TestVec1Component", string.Empty, new List<SearcherItem>
                {
                    new SearcherItem("Value", string.Empty, new List<SearcherItem>
                    {
                        new SearcherItem("x", string.Empty, GetMathsOperators().ToList()),
                    })
                })
            });
        }

        static void ValidateHierarchy(IReadOnlyList<SearcherItem> result, IEnumerable<SearcherItem> hierarchy)
        {
            var index = 0;
            TraverseHierarchy(result, hierarchy, ref index);
            Assert.AreEqual(result.Count, index);
        }

        static void TraverseHierarchy(IReadOnlyList<SearcherItem> result, IEnumerable<SearcherItem> hierarchy,
            ref int index)
        {
            foreach (var item in hierarchy)
            {
                Assert.AreEqual(item.Name, result[index].Name);

                if (item.Parent != null)
                    Assert.AreEqual(item.Parent.Name, result[index].Parent.Name);

                index++;

                TraverseHierarchy(result, item.Children, ref index);
            }
        }

        static IEnumerable<SearcherItem> GetMathsOperators()
        {
            yield return new SearcherItem(BinaryOperatorKind.Equals.ToString());
            yield return new SearcherItem(BinaryOperatorKind.NotEqual.ToString());
            yield return new SearcherItem(BinaryOperatorKind.GreaterThan.ToString());
            yield return new SearcherItem(BinaryOperatorKind.GreaterThanOrEqual.ToString());
            yield return new SearcherItem(BinaryOperatorKind.LessThan.ToString());
            yield return new SearcherItem(BinaryOperatorKind.LessThanOrEqual.ToString());
        }
    }
}
