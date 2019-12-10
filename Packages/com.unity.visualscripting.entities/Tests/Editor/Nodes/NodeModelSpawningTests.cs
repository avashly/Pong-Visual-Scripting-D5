using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScriptingTests.Models
{
    public class NodeModelSpawningTests : BaseFixture
    {
        protected override bool CreateGraphOnStartup => true;
        public static List<Assembly> ExpectedAssemblies =
            new List<Assembly>
        {
            typeof(NodeModel).Assembly,
            typeof(EcsStencil).Assembly
        };

        [Test]
        public void Test_NodeSpawningWillNotCauseErrors()
        {
            //Prepare
            var reflectedNodeTypes = GetAllNonAbstractNodeModelTypes();

            //Act
            NodeSpawner.SpawnAllNodeModelsInGraph(GraphModel);

            //Validate
            var spawnedNodeTypes = GetAllSpawnedNodeTypes();
            var missingSpawnedNode = reflectedNodeTypes.Except(spawnedNodeTypes).ToList();
            if (missingSpawnedNode.Any())
            {
                string errorMessage = "The following types have not been spawned in the SpawnAllNodeModelsInGraph():\n\n";
                StringBuilder builder = new StringBuilder(errorMessage);
                foreach (var missingNode in missingSpawnedNode)
                    builder.AppendLine(missingNode.ToString());
                Debug.LogError(builder.ToString());
            }

            Assert.That(missingSpawnedNode, Is.Empty);
        }

        HashSet<Type> GetAllSpawnedNodeTypes()
        {
            var spawnedNodeTypes = new HashSet<Type>(GetAllNodes().Select(n => n.GetType()));
            foreach (var nodeType in GetAllNodes().OfType<StackBaseModel>().SelectMany(stack => stack.NodeModels).Select(n => n.GetType()))
                spawnedNodeTypes.Add(nodeType);
            return spawnedNodeTypes;
        }

        static HashSet<Type> GetAllNonAbstractNodeModelTypes()
        {
            HashSet<Type> nodeModelTypes = new HashSet<Type>();
            ConcreteTypesDerivingFrom(typeof(NodeModel), nodeModelTypes);
            return nodeModelTypes;

            void ConcreteTypesDerivingFrom(Type expectedBaseType, HashSet<Type> foundTypes)
            {
                if (!expectedBaseType.IsAbstract && ExpectedAssemblies.Contains(expectedBaseType.Assembly))
                    foundTypes.Add(expectedBaseType);

                var subTypes = TypeCache.GetTypesDerivedFrom(expectedBaseType);
                foreach (var subType in subTypes)
                {
                    if (subType.BaseType.IsGenericType && subType.BaseType.GetGenericTypeDefinition() == expectedBaseType
                        || subType.BaseType == expectedBaseType)
                        ConcreteTypesDerivingFrom(subType, foundTypes);
                }
            }
        }
    }
}
