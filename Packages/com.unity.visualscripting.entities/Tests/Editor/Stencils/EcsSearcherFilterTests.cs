using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Model.Compilation;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Stencils
{
    class EcsSearcherFilterTests
    {
        sealed class TestStencil : Stencil
        {
            public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
            {
                return new EcsSearcherDatabaseProvider(this);
            }

            [CanBeNull]
            public override IBuilder Builder => null;
        }

        struct TestComponent : IComponentData {}
        struct TestSharedComponent : ISharedComponentData {}

        Stencil m_Stencil;

        [SetUp]
        public void SetUp() => m_Stencil = new TestStencil();

        [TestCase(typeof(TestComponent), true)]
        [TestCase(typeof(TestSharedComponent), false)]
        [TestCase(typeof(string), false)]
        public void TestWithComponentData(Type type, bool expectedResult)
        {
            var filter = new SearcherFilter(SearcherContext.Type).WithComponentData(m_Stencil);
            var data = new TypeSearcherItemData(type.GenerateTypeHandle(m_Stencil), SearcherItemTarget.Type);
            var result = InvokeApplyFiltersMethod(filter, data);

            Assert.AreEqual(expectedResult, result);
        }

        [Test]
        public void TestWithComponentDataWithExclusion()
        {
            var componentDataTypeHandle = typeof(TestComponent).GenerateTypeHandle(m_Stencil);
            var filter = new SearcherFilter(SearcherContext.Type).WithComponentData(m_Stencil, new HashSet<TypeHandle>(){componentDataTypeHandle});
            var data = new TypeSearcherItemData(componentDataTypeHandle, SearcherItemTarget.Type);
            var result = InvokeApplyFiltersMethod(filter, data);

            Assert.That(result, Is.False);
        }

        [TestCase(typeof(TestComponent), false)]
        [TestCase(typeof(TestSharedComponent), true)]
        [TestCase(typeof(string), false)]
        public void TestWithSharedComponentData(Type type, bool expectedResult)
        {
            var filter = new SearcherFilter(SearcherContext.Type).WithSharedComponentData(m_Stencil);
            var data = new TypeSearcherItemData(type.GenerateTypeHandle(m_Stencil), SearcherItemTarget.Type);
            var result = InvokeApplyFiltersMethod(filter, data);

            Assert.AreEqual(expectedResult, result);
        }

        [TestCase(typeof(Translation), true)]
        [TestCase(typeof(Rotation), false)]
        public void TestWithComponents(Type type, bool expectedResult)
        {
            var components = new List<TypeHandle>
            {
                typeof(Translation).GenerateTypeHandle(m_Stencil),
                typeof(Scale).GenerateTypeHandle(m_Stencil)
            };
            var filter = new SearcherFilter(SearcherContext.Type).WithComponents(components);
            var data = new TypeSearcherItemData(type.GenerateTypeHandle(m_Stencil), SearcherItemTarget.Type);
            var result = InvokeApplyFiltersMethod(filter, data);

            Assert.AreEqual(expectedResult, result);
        }

        static object InvokeApplyFiltersMethod(SearcherFilter filter, TypeSearcherItemData data)
        {
            var method = filter.GetType().GetMethod("ApplyFilters",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return method?.Invoke(filter, new object[] { data });
        }
    }
}
