using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Entities/Create")]
    [Serializable]
    public class CreateEntityNodeModel : EcsHighLevelNodeModel, IHasInstancePort
    {
        [SerializeField]
        List<TypeHandle> m_AdditionalComponents = new List<TypeHandle>();
        List<ComponentPortsDescription> m_PortDescriptions = new List<ComponentPortsDescription>();

        const string k_InstancePortId = "InstancePort";

        public override string Title => "Create";

        public IPortModel InstancePort { get; private set; }

        protected override void OnDefineNode()
        {
            m_PortDescriptions?.Clear();

            InstancePort = AddInstanceInput<Entity>("Set Variable", k_InstancePortId);

            var editableComponents = GetEditableComponents();

            m_PortDescriptions = editableComponents
                .Select(comp => AddPortsForComponent(comp, comp.Identification))
                .ToList();
        }

        public IEnumerable<IPortModel> GetPortsForComponent(TypeHandle th)
        {
            return m_PortDescriptions
                .FirstOrDefault(pm => pm.Component == th)
                ?.GetFieldIds()
                    .Select(id => InputsById[id])
                ?? Enumerable.Empty<IPortModel>();
        }

        public List<TypeHandle> GetEditableComponents() => m_AdditionalComponents;

        public void AddComponentTypeToAdd(TypeHandle type)
        {
            if (!m_AdditionalComponents.Contains(type))
                m_AdditionalComponents.Add(type);
        }

        public void DeleteComponentOperation(TypeHandle type) => m_AdditionalComponents.Remove(type);

        IEnumerable<ComponentDefinition> GetComponentDefinitionsOfEntity(IPortModel archetypePort)
        {
            IPortModel connectedPort = archetypePort.ConnectionPortModels.FirstOrDefault();
            if (connectedPort?.NodeModel is VariableNodeModel varNode)
            {
                if (varNode.DataType == typeof(Entity).GenerateTypeHandle(Stencil) &&
                    varNode.DeclarationModel.Owner is IIteratorStackModel iteratorStackModel)
                {
                    return iteratorStackModel.ComponentQueryDeclarationModel.Components.Select(queryComp => queryComp.Component);
                }
            }

            return Enumerable.Empty<ComponentDefinition>();
        }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers), GraphtoolsExtensionMethods]
    public static class CreateEntityTranslator
    {
        public static IEnumerable<SyntaxNode> BuildCreateEntityTranslator(this RoslynEcsTranslator translator,
            CreateEntityNodeModel model, IPortModel portModel)
        {
            bool hasAnyComponentInstruction = false;
            ExpressionSyntax newInstance = null;
            if (model.InstancePort.Connected)
            {
                newInstance = translator.BuildPort(model.InstancePort, RoslynTranslator.PortSemantic.Write).SingleOrDefault() as ExpressionSyntax;
                hasAnyComponentInstruction = true;
            }

            // always instantiate
            var newEntity = InstantiateEntity(translator, model);

            if (hasAnyComponentInstruction) // == if connected. assignment of implicit variable is done only if it's required in the foreach loop below
                yield return RoslynBuilder.Assignment(newInstance, newEntity);

            var entityTranslator = translator.context.GetEntityManipulationTranslator();

            foreach (TypeHandle th in model.GetEditableComponents())
            {
                var componentType = th.Resolve(model.GraphModel.Stencil);
                IEnumerable<SyntaxNode> instructions;
                // first instruction forces the declaration of an implicit variable if not already done
                if (!hasAnyComponentInstruction)
                {
                    hasAnyComponentInstruction = true;
                    string entityVariableName = translator.MakeUniqueName("entity");
                    yield return RoslynBuilder.DeclareLocalVariable(typeof(Entity), entityVariableName, newEntity);
                    newInstance = SyntaxFactory.IdentifierName(entityVariableName);
                }
                instructions = entityTranslator.AddComponent(translator.context, newInstance, componentType,
                    BuildNewComponent(th, componentType));

                foreach (var instruction in instructions)
                    yield return instruction;
            }

            if (!hasAnyComponentInstruction) // implicit variable, never used in the loop
                yield return SyntaxFactory.ExpressionStatement(newEntity);

            ExpressionSyntax BuildNewComponent(TypeHandle componentTypeHandle, Type componentType)
            {
                var componentInput = model.GetPortsForComponent(componentTypeHandle).ToArray();
                var newComponent = translator.BuildComponentFromInput(componentType, componentInput);
                return newComponent;
            }
        }

        static ExpressionSyntax InstantiateEntity(RoslynEcsTranslator translator, CreateEntityNodeModel model)
        {
            var context = translator.context;
            var entityTranslator = context.GetEntityManipulationTranslator();
            return entityTranslator.CreateEntity(translator.context).First() as ExpressionSyntax;
        }
    }
}
