using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Components/Add Component")]
    [Serializable]
    public class AddComponentNodeModel : EcsHighLevelNodeModel, IHasEntityInputPort
    {
        public const string EntityLabel = "entity";

        [TypeSearcher(typeof(AddComponentFilter))]
        public TypeHandle ComponentType = TypeHandle.Unknown;

        ComponentPortsDescription m_ComponentDescription;

        public IPortModel EntityPort { get; private set; }

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>(EntityLabel);

            m_ComponentDescription = ComponentType != TypeHandle.Unknown ? AddPortsForComponent(ComponentType) : null;
        }

        public IEnumerable<IPortModel> GetPortsForComponent()
        {
            return m_ComponentDescription == null ? Enumerable.Empty<IPortModel>() : m_ComponentDescription.GetFieldIds().Select(id => InputsById[id]);
        }
    }

    [GraphtoolsExtensionMethods]
    public static class AddComponentTranslator
    {
        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            AddComponentNodeModel model,
            IPortModel portModel)
        {
            var componentType = model.ComponentType.Resolve(model.GraphModel.Stencil);
            var entitySyntax = translator.BuildPort(model.EntityPort).SingleOrDefault() as ExpressionSyntax;
            var componentInputs = model.GetPortsForComponent().ToArray();
            var componentSyntax = translator.BuildComponentFromInput(componentType, componentInputs);
            var entityTranslator = translator.context.GetEntityManipulationTranslator();

            return entityTranslator.AddComponent(translator.context, entitySyntax, componentType, componentSyntax);
        }
    }

    class AddComponentFilter : ISearcherFilter
    {
        public SearcherFilter GetFilter(INodeModel model)
        {
            return new SearcherFilter(SearcherContext.Type)
                .WithComponentData(model.GraphModel.Stencil)
                .WithSharedComponentData(model.GraphModel.Stencil);
        }
    }
}
