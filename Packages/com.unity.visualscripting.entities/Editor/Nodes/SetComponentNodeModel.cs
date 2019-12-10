using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Components/Set Component")]
    [Serializable]
    public class SetComponentNodeModel : EcsHighLevelNodeModel, IHasEntityInputPort
    {
        public IPortModel EntityPort { get; private set; }

        [TypeSearcher(typeof(SetComponentFilter))]
        public TypeHandle ComponentType = TypeHandle.Unknown;

        ComponentPortsDescription m_ComponentDescription;

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>("Entity");

            if (ComponentType != TypeHandle.Unknown)
            {
                m_ComponentDescription = AddPortsForComponent(ComponentType);
            }
        }

        public IEnumerable<IPortModel> GetPortsForComponent()
        {
            return m_ComponentDescription != null
                ? m_ComponentDescription.GetFieldIds().Select(id => InputsById[id])
                : Enumerable.Empty<IPortModel>();
        }

        internal IEnumerable<TypeHandle> GetComponentTypesFromEntityPort()
        {
            var entityTypeHandle = typeof(Entity).GenerateTypeHandle(EntityPort.GraphModel.Stencil);
            IIteratorStackModel iteratorStackModel = null;

            if (EntityPort.Connected)
            {
                var variable = (EntityPort.ConnectionPortModels?.FirstOrDefault()?.NodeModel
                    as VariableNodeModel)?.DeclarationModel;
                if (variable != null
                    && variable.DataType.Equals(entityTypeHandle)
                    && variable.Owner is IIteratorStackModel ism)
                {
                    iteratorStackModel = ism;
                }
            }
            else if (ParentStackModel.OwningFunctionModel is IIteratorStackModel ism)
            {
                iteratorStackModel = ism;
            }

            return iteratorStackModel != null
                ? iteratorStackModel.ComponentQueryDeclarationModel.Components.Select(c => c.Component.TypeHandle)
                : Enumerable.Empty<TypeHandle>();
        }
    }

    [GraphtoolsExtensionMethods]
    public static class SetComponentTranslator
    {
        public static IEnumerable<SyntaxNode> Build(this RoslynEcsTranslator translator, SetComponentNodeModel model,
            IPortModel portModel)
        {
            var query = translator.context.IterationContext.Query;
            var queryDeclaration = query.ComponentQueryDeclarationModel;
            var componentTypes = model.GetComponentTypesFromEntityPort();

            if (!model.ComponentType.Equals(TypeHandle.Unknown) && !componentTypes.Any(c => c == model.ComponentType))
            {
                var componentName = model.ComponentType.Name(translator.Stencil);
                var queryCopy = queryDeclaration;

                translator.AddError(
                    model,
                    $"A component of type {componentName} is required, which the query {queryDeclaration.Name} " +
                    "doesn't specify",
                    new CompilerQuickFix(
                        $"Add {componentName} to the query",
                        s => s.Dispatch(new AddComponentToQueryAction(
                            queryCopy,
                            model.ComponentType,
                            ComponentDefinitionFlags.None))));

                return Enumerable.Empty<SyntaxNode>();
            }

            if (!queryDeclaration.Components.Any())
            {
                translator.AddError(model, $"The query {queryDeclaration.Name} doesn't contain any component.");
                return Enumerable.Empty<SyntaxNode>();
            }

            if (model.ComponentType.Equals(TypeHandle.Unknown))
            {
                translator.AddError(model, "You must select a valid component type");
                return Enumerable.Empty<SyntaxNode>();
            }

            var entitySyntax = translator.BuildEntityFromPortOrCurrentIteration(model.EntityPort);
            var componentType = model.ComponentType.Resolve(model.GraphModel.Stencil);
            var componentInputs = model.GetPortsForComponent().ToArray();
            var componentSyntax = translator.BuildComponentFromInput(componentType, componentInputs);
            var entityTranslator = translator.context.GetEntityManipulationTranslator();

            return entityTranslator.SetComponent(translator.context, entitySyntax, componentType, componentSyntax);
        }
    }

    class SetComponentFilter : ISearcherFilter
    {
        public SearcherFilter GetFilter(INodeModel model)
        {
            var stencil = (EcsStencil)model.GraphModel.Stencil;
            var componentTypes = ((SetComponentNodeModel)model).GetComponentTypesFromEntityPort().ToList();

            return componentTypes.Any()
                ? new SearcherFilter(SearcherContext.Type).WithComponents(componentTypes)
                : new SearcherFilter(SearcherContext.Type).WithComponentData(stencil).WithSharedComponentData(stencil);
        }
    }
}
