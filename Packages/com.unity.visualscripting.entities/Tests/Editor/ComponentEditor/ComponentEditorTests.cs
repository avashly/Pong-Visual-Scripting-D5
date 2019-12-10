using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor.VisualScripting.ComponentEditor;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;

namespace UnityEditor.VisualScriptingTests.ComponentEditor
{
    public class ComponentEditorTests
    {
        const string k_Code = @"
using System;
using System.ComponentModel;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
        [Obsolete(""Position has been renamed. Use Translation instead (UnityUpgradable) -> Translation"", true)]
        [System.ComponentModel.EditorBrowsable(EditorBrowsableState.Never)]
        public struct Position : IComponentData { public float3 Value; }

        [Serializable]
        [WriteGroup(typeof(LocalToWorld))]
        [WriteGroup(typeof(LocalToParent))]
        public struct Translation : IComponentData
        {
            public float3 Value;
            public float2 Value2;
        }
    }
";

        [Test]
        public void ParseEventStructHasRightType()
        {
            var ev = new StructModel("e", StructType.Event);
            var parsed = FileModel.Parse(ev.Generate().NormalizeWhitespace().ToString(), FileModel.ParseOptions.DisallowMultipleStructs);
            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Structs[0].Type, Is.EqualTo(StructType.Event));
        }

        [Test]
        public void ParseFieldWithQualifiedName()
        {
            var f = FieldModel.Parse(null, RoslynBuilder.DeclareField(typeof(GameObject), "a"));
            Assert.That(f, Is.Not.Null);
            Assert.AreEqual(typeof(GameObject), f.Type);
        }

        [Test]
        public void ParseFieldWithConvertAttribute()
        {
            var f = FieldModel.Parse(null, RoslynBuilder.DeclareField(typeof(Entity), "a")
                .AddAttributeLists(SyntaxFactory.AttributeList().AddAttributes(SyntaxFactory.Attribute(SyntaxFactory.IdentifierName("Convert")))));
            Assert.That(f, Is.Not.Null);
            Assert.AreEqual(typeof(GameObject), f.Type);
        }

        [Test]
        public void ParseFieldWithHideInInspector()
        {
            var f1 = new FieldModel(null, typeof(int), "i"){HideInInspector = true};
            var ast = f1.Generate();
            var f2 = FieldModel.Parse(null, ast);
            Assert.IsTrue(f2.HideInInspector);
        }

        [Test]
        public void Parse()
        {
            var m = FileModel.Parse(k_Code, FileModel.ParseOptions.AllowMultipleStructs);
            var strCount = m.Structs.Count;
            m.Structs.Add(new StructModel("W", StructType.Component)
            {
                {typeof(int), "i"},
            });
            Assert.AreEqual(strCount + 1, m.Structs.Count);

            var fieldCount = m.Structs[0].Fields.Count;

            m.Structs[0].RemoveFieldAt(0);
            Assert.AreEqual(fieldCount - 1, m.Structs[0].Fields.Count);

            m.Structs[0].Add(typeof(float), "newField");
            Assert.AreEqual(fieldCount, m.Structs[0].Fields.Count);
        }
    }
}
