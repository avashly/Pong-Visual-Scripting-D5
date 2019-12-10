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
using UnityEditor.VisualScripting.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Components/Remove Component")]
    [Serializable]
    public class RemoveComponentNodeModel : HighLevelNodeModel, IHasEntityInputPort
    {
        [TypeSearcher(typeof(RemoveComponentFilter))]
        public TypeHandle ComponentType = TypeHandle.Unknown;

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>("entity");
        }

        public IPortModel EntityPort { get; private set; }
    }

    [GraphtoolsExtensionMethods]
    public static class RemoveComponentTranslator
    {
        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            RemoveComponentNodeModel model,
            IPortModel portModel)
        {
            var componentType = model.ComponentType.Resolve(model.GraphModel.Stencil);
            var entityTranslator = translator.context.GetEntityManipulationTranslator();
            var entitySyntax = translator.BuildPort(model.EntityPort).SingleOrDefault() as ExpressionSyntax;

            return entityTranslator.RemoveComponent(translator.context, entitySyntax, componentType);
        }
    }

    class RemoveComponentFilter : ISearcherFilter
    {
        public SearcherFilter GetFilter(INodeModel model)
        {
            var ecsStencil = (EcsStencil)model.GraphModel.Stencil;
            return new SearcherFilter(SearcherContext.Type)
                .WithComponentData(ecsStencil)
                .WithSharedComponentData(ecsStencil);
        }
    }
}
