using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Packages.VisualScripting.Editor.Elements;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Count Entities")]
    [Serializable]
    public class CountEntitiesNodeModel : EcsHighLevelNodeModel, IHasMainInputPort, IHasMainOutputPort
    {
        protected override void OnDefineNode()
        {
            InputPort = AddDataInput<ComponentQuery>("Query");
            OutputPort = AddDataOutputPort<int>("Count");
        }

        public IPortModel InputPort { get; private set; }
        public IPortModel OutputPort { get; private set; }
    }

    [GraphtoolsExtensionMethodsAttribute]
    public static class CountEntitiesTranslator
    {
        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            CountEntitiesNodeModel model,
            IPortModel portModel)
        {
            var connected = model.InputPort.ConnectionPortModels.FirstOrDefault();
            if (connected?.NodeModel is VariableNodeModel variableNode &&
                variableNode.DeclarationModel is ComponentQueryDeclarationModel queryDeclaration)
            {
                yield return RoslynBuilder.MethodInvocation(nameof(EntityQuery.CalculateEntityCount),
                    SyntaxFactory.IdentifierName(queryDeclaration.VariableName), null, null);
            }
        }
    }
}
