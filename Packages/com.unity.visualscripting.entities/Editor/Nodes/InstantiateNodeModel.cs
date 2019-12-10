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
using static UnityEditor.VisualScripting.Model.Stencils.ComponentOperation;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public struct ComponentOperation
    {
        public TypeHandle Type;
        public bool FromArchetype;
        public ComponentOperationType OperationType;

        public ComponentOperation(TypeHandle type, ComponentOperationType operationType, bool fromArchetype = false)
        {
            Type = type;
            OperationType = operationType;
            FromArchetype = fromArchetype;
        }

        public enum ComponentOperationType
        {
            AddComponent,
            RemoveComponent,
            SetComponent,
        }
    }

    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Entities/Instantiate")]
    [Serializable]
    public class InstantiateNodeModel : EcsHighLevelNodeModel, IHasEntityInputPort, IHasInstancePort
    {
        [SerializeField]
        List<ComponentOperation> m_AdditionalComponents = new List<ComponentOperation>();
        List<ComponentPortsDescription> m_PortDescriptions = new List<ComponentPortsDescription>();

        const string k_InstancePortId = "InstancePort";
        const string k_SourceEntityPortId = "SourceEntityPort";

        public IPortModel InstancePort { get; private set; }

        public IPortModel EntityPort { get; private set; }

        protected override void OnDefineNode()
        {
            m_PortDescriptions?.Clear();

            InstancePort = AddInstanceInput<Entity>("Set Variable", k_InstancePortId);
            EntityPort = AddDataInput<Entity>("Source Entity", k_SourceEntityPortId);

            var editableComponents = GetEditableComponents();

            m_PortDescriptions = editableComponents
                .Where(c => c.OperationType != ComponentOperationType.RemoveComponent)
                .Select(comp => AddPortsForComponent(comp.Type, comp.Type.Identification))
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

        public List<ComponentOperation> GetEditableComponents()
        {
            var editableComponents = new List<ComponentOperation>();

            var archetype = GetArchetypeComponents();
            // TODO: deactivated until node is split between Duplicate and Instantiate
//            foreach (var component in archetype)
//                editableComponents.Add(new ComponentOperation(component, ComponentOperationType.SetComponent, true));

            foreach (var additionalComponent in m_AdditionalComponents)
            {
                var duplicateIndex = editableComponents.FindIndex(comp => comp.Type == additionalComponent.Type);

                //Since we want to preserve the operations in case the archetype Adds/Removes components
                //This will make sure we override whatever operation was in the initial archetype (which is a set/remove)
                if (duplicateIndex != -1)
                    editableComponents[duplicateIndex] = new ComponentOperation(additionalComponent.Type, additionalComponent.OperationType, true);
                else
                    editableComponents.Add(additionalComponent);
            }
            return editableComponents;
        }

        public void SetComponentOperation(TypeHandle type, ComponentOperationType operation)
        {
            var i = m_AdditionalComponents.FindIndex(compOps => compOps.Type == type);
            if (i == -1)
                m_AdditionalComponents.Add(new ComponentOperation(type, operation));
            else if (operation == ComponentOperationType.AddComponent ||
                     operation == ComponentOperationType.RemoveComponent ||
                     operation == ComponentOperationType.SetComponent)
            {
                m_AdditionalComponents[i] =
                    new ComponentOperation(type, operation, m_AdditionalComponents[i].FromArchetype);
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        public void DeleteComponentOperation(TypeHandle th)
        {
            var i = m_AdditionalComponents.FindIndex(compOps => compOps.Type == th);
            if (i != -1)
                m_AdditionalComponents.RemoveAt(i);
        }

        IEnumerable<TypeHandle> GetArchetypeComponents()
        {
            if (EntityPort == null)
                return Enumerable.Empty<TypeHandle>();

            return GetComponentDefinitionsOfEntity(EntityPort)
                .Where(compDef => !compDef.Subtract)
                .Select(c => c.TypeHandle);
        }

        IEnumerable<ComponentDefinition> GetComponentDefinitionsOfEntity(IPortModel entityPort)
        {
            IPortModel connectedPort = entityPort.ConnectionPortModels.FirstOrDefault();
            if (connectedPort?.NodeModel is VariableNodeModel varNode)
            {
                if (varNode.DataType == typeof(Entity).GenerateTypeHandle(Stencil) &&
                    varNode.DeclarationModel.Owner is IIteratorStackModel iteratorStackModel &&
                    iteratorStackModel.ComponentQueryDeclarationModel != null)
                {
                    return iteratorStackModel.ComponentQueryDeclarationModel.Components.Select(queryComp => queryComp.Component);
                }
            }

            return Enumerable.Empty<ComponentDefinition>();
        }
    }

    [GraphtoolsExtensionMethods]
    public static class InstantiateEntityTranslator
    {
        public static IEnumerable<SyntaxNode> BuildInstantiateEntityTranslator(this RoslynEcsTranslator translator,
            InstantiateNodeModel model, IPortModel portModel)
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

            foreach (ComponentOperation compOperation in model.GetEditableComponents())
            {
                var componentType = compOperation.Type.Resolve(model.GraphModel.Stencil);
                IEnumerable<SyntaxNode> instructions;
                // first instruction forces the declaration of an implicit variable if not already done
                if (!hasAnyComponentInstruction)
                {
                    hasAnyComponentInstruction = true;
                    string entityVariableName = translator.MakeUniqueName("entity");
                    yield return RoslynBuilder.DeclareLocalVariable(typeof(Entity), entityVariableName, newEntity);
                    newInstance = SyntaxFactory.IdentifierName(entityVariableName);
                }
                switch (compOperation.OperationType)
                {
                    case ComponentOperationType.AddComponent:
                        instructions = entityTranslator.AddComponent(translator.context, newInstance, componentType,
                            BuildNewComponent(compOperation, componentType));
                        break;
                    case ComponentOperationType.RemoveComponent:
                        instructions = entityTranslator.RemoveComponent(translator.context, newInstance, componentType);
                        break;
                    case ComponentOperationType.SetComponent:
                        instructions = entityTranslator.SetComponent(translator.context, newInstance, componentType,
                            BuildNewComponent(compOperation, componentType));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                foreach (var instruction in instructions)
                    yield return instruction;
            }

            if (!hasAnyComponentInstruction) // implicit variable, never used in the loop
                yield return SyntaxFactory.ExpressionStatement(newEntity);

            ExpressionSyntax BuildNewComponent(ComponentOperation compOperation, Type componentType)
            {
                var componentInput = model.GetPortsForComponent(compOperation.Type).ToArray();
                var newComponent = translator.BuildComponentFromInput(componentType, componentInput);
                return newComponent;
            }
        }

        static ExpressionSyntax InstantiateEntity(RoslynEcsTranslator translator, InstantiateNodeModel model)
        {
            var entityTranslator = translator.context.GetEntityManipulationTranslator();
            var entitySyntax = translator.BuildPort(model.EntityPort).First() as ExpressionSyntax;

            return entityTranslator.Instantiate(translator.context, entitySyntax).First() as ExpressionSyntax;
        }
    }
}
