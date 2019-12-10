using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Model.Compilation;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Stencils
{
    class HighLevelNodeModelHelpersTests
    {
#pragma warning disable CS0649
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        struct ComponentTest0
        {
            public int Value;
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        struct ComponentTest1
        {
            public float3 Value;
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        struct ComponentTest2
        {
            public float Value;
            public string Name;
        }

        struct ComponentTest3 {}
#pragma warning restore CS0649

        class TestStencil : Stencil
        {
            public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
            {
                return new ClassSearcherDatabaseProvider(this);
            }

            public override IBuilder Builder => null;
        }

        Stencil m_Stencil;

        [SetUp]
        public void SetUp()
        {
            m_Stencil = new TestStencil();
        }

        [UsedImplicitly]
        static IEnumerable<TestCaseData> GetDataInputsFromComponentTypeData
        {
            get
            {
                IEnumerable<Tuple<string, TypeHandle>> result = new[]
                {
                    new Tuple<string, TypeHandle>("Value", TypeHandle.Int),
                };
                yield return new TestCaseData(typeof(ComponentTest0), result)
                    .SetName("Test component with a single non-predefined field type");


                result = new[]
                {
                    new Tuple<string, TypeHandle>("x", TypeHandle.Float),
                    new Tuple<string, TypeHandle>("y", TypeHandle.Float),
                    new Tuple<string, TypeHandle>("z", TypeHandle.Float)
                };
                yield return new TestCaseData(typeof(ComponentTest1), result)
                    .SetName("Test component with a single predefined field type");

                result = new[]
                {
                    new Tuple<string, TypeHandle>("Value", TypeHandle.Float),
                    new Tuple<string, TypeHandle>("Name", TypeHandle.String),
                };
                yield return new TestCaseData(typeof(ComponentTest2), result)
                    .SetName("Test component with multiple fields");

                result = Enumerable.Empty<Tuple<string, TypeHandle>>();
                yield return new TestCaseData(typeof(ComponentTest3), result)
                    .SetName("Test empty component");
            }
        }

        [TestCaseSource(nameof(GetDataInputsFromComponentTypeData))]
        public void TestGetDataInputsFromComponentType(Type type, IEnumerable<Tuple<string, TypeHandle>> expectedResult)
        {
            var result = HighLevelNodeModelHelpers.GetDataInputsFromComponentType(
                m_Stencil,
                type.GenerateTypeHandle(m_Stencil)).ToList();

            var expectedList = expectedResult.ToList();

            Assert.AreEqual(expectedList.Count, result.Count);

            for (var i = 0; i < result.Count; ++i)
            {
                Assert.AreEqual(expectedList[i], result[i]);
            }
        }
    }
}
