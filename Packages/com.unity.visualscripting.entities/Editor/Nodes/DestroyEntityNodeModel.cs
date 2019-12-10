using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Entities/Destroy Entity")]
    [Serializable]
    public class DestroyEntityNodeModel : HighLevelNodeModel, IHasEntityInputPort
    {
        public IPortModel EntityPort { get; private set; }

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>("entity");
        }
    }

    [GraphtoolsExtensionMethods]
    public static class DestroyEntityTranslator
    {
        public static IEnumerable<SyntaxNode> BuildDestroyEntityTranslator(this RoslynEcsTranslator translator, DestroyEntityNodeModel model, IPortModel portModel)
        {
            var entityTranslator = translator.context.GetEntityManipulationTranslator();
            var entitySyntax = translator.BuildPort(model.EntityPort).First() as ExpressionSyntax;

            return entityTranslator.DestroyEntity(translator.context, entitySyntax);
        }
    }
}
