using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.SmartSearch
{
    class EcsSearcherServicesTests
    {
        struct DummyTestComponent : IComponentData {}

        EcsStencil m_Stencil;
        SearcherItem m_Sources;

        [SetUp]
        public void SetUp()
        {
            m_Stencil = new EcsStencil();
            m_Sources = BuildList(m_Stencil);
        }

        static SearcherItem BuildList(Stencil stencil)
        {
            var dummyComponent = typeof(DummyTestComponent).GenerateTypeHandle(stencil);
            return new TypeSearcherItem(dummyComponent, dummyComponent.Name(stencil),
                new List<SearcherItem>
                {
                    new TypeSearcherItem(typeof(float3).GenerateTypeHandle(stencil), "Value", new List<SearcherItem>
                    {
                        new TypeSearcherItem(typeof(float).GenerateTypeHandle(stencil), "x", new List<SearcherItem>
                        {
                            new CriterionSearcherItem(BinaryOperatorKind.Equals)
                        })
                    })
                }
            );
        }

        [Test]
        public void TestCriteriaSearcher()
        {
            var selectedItem = m_Sources.Find(BinaryOperatorKind.Equals.ToString());

            var selectedComponent = TypeHandle.Unknown;
            var selectedMember = new TypeMember();
            var selectedOperator = BinaryOperatorKind.Xor;
            void Callback(TypeHandle handle, TypeMember member, BinaryOperatorKind kind)
            {
                selectedComponent = handle;
                selectedMember = member;
                selectedOperator = kind;
            }

            var onItemSelected = typeof(EcsSearcherServices).GetMethod("OnItemSelected", BindingFlags.Static | BindingFlags.NonPublic);
            onItemSelected?.Invoke(null, new object[]
            {
                selectedItem,
                (Action<TypeHandle, TypeMember, BinaryOperatorKind>)Callback
            });

            var dummyComponent = typeof(DummyTestComponent).GenerateTypeHandle(m_Stencil);
            Assert.AreEqual(dummyComponent, selectedComponent);
            Assert.AreEqual(selectedOperator, BinaryOperatorKind.Equals);
            Assert.AreEqual(selectedMember.Type, typeof(float).GenerateTypeHandle(m_Stencil));
            Assert.That(selectedMember.Path, Is.EqualTo(new List<string> { "Value", "x" }));
        }

        [Test]
        public void TestCriteriaSearcher_NoSelection()
        {
            var selectedItem = m_Sources.Find("Value");

            var isSelection = false;
            void Callback(TypeHandle th, TypeMember tm, BinaryOperatorKind bok) { isSelection = true; }

            var onItemSelected = typeof(EcsSearcherServices).GetMethod("OnItemSelected", BindingFlags.Static | BindingFlags.NonPublic);
            onItemSelected?.Invoke(null, new object[]
            {
                selectedItem,
                (Action<TypeHandle, TypeMember, BinaryOperatorKind>)Callback
            });

            Assert.AreEqual(false, isSelection);
        }
    }
}
