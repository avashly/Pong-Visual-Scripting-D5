using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Transforms;
using UnityEditor.EditorCommon.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingECSTests.Stencils
{
    class TypeHandleTests
    {
        [Test]
        public void Test_TypeHandleDeserializationOfRenamedType_PositionTranslation()
        {
            var positionName = typeof(Translation).AssemblyQualifiedName.Replace("Translation", "Position");
            Debug.Log(positionName);
            var typeSerializer = new CSharpTypeSerializer(new Dictionary<string, string>
            {
                { positionName, typeof(Translation).AssemblyQualifiedName }
            });

            TypeHandle th = new TypeHandle { Identification = positionName };

            Type deserializedTypeHandle = th.Resolve(typeSerializer);

            Assert.That(deserializedTypeHandle, Is.EqualTo(typeof(Translation)));
        }

        public static IEnumerable<object[]> GetTypeAndMatchingConstantNodeModelType()
        {
            EcsStencil ecsStencil = new EcsStencil();
            GraphModel graphModel = Activator.CreateInstance<VSGraphModel>();
            graphModel.Stencil = ecsStencil;

            foreach (var baseType in new[] {typeof(ConstantNodeModel<>), typeof(ConstantNodeModel<,>)})
            {
                foreach (var concreteType in GetTypes(ecsStencil, baseType))
                {
                    ConstantNodeModel nodeModel = (ConstantNodeModel)Activator.CreateInstance(concreteType);
                    nodeModel.GraphModel = graphModel;
                    var constantValueType = nodeModel.Type.GenerateTypeHandle(ecsStencil);
                    yield return new object[] {ecsStencil, constantValueType, concreteType};
                }
            }
        }

        static IEnumerable<Type> GetTypes(Stencil stencil, Type type)
        {
            return stencil.GetAssemblies()
                .SelectMany(a => a.GetTypesSafe(), (domainAssembly, assemblyType) => assemblyType)
                .Where(t => !t.IsAbstract
                    && !t.IsInterface
                    && t.BaseType != null
                    && (t.IsSubclassOf(type)
                        || type.IsGenericType
                        && t.BaseType.IsGenericType
                        && t.BaseType.GetGenericTypeDefinition() == type.GetGenericTypeDefinition()))
                .ToList();
        }

        [Test, TestCaseSource(nameof(GetTypeAndMatchingConstantNodeModelType))]
        public void Test_FindConstantNodeModelTypeFromConstantValueType(EcsStencil ecsStencil, TypeHandle constantValueType, Type constantNodeModelType)
        {
            Assert.That(ecsStencil.GetConstantNodeModelType(constantValueType), Is.EqualTo(constantNodeModelType));
        }
    }
}
