using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class SendEventNodeModel : HighLevelNodeModel, IHasEntityInputPort
    {
        [HideInInspector]
        public TypeHandle EventType;

        List<string> m_InputFieldNames;
        public IEnumerable<string> InputFieldNames => m_InputFieldNames ?? Enumerable.Empty<string>();

        public IEnumerable<IPortModel> FieldInputs => InputFieldNames.Select(id => InputsById[id]);

        public IPortModel EntityPort { get; private set; }

        protected override void OnDefineNode()
        {
            EntityPort = AddDataInput<Entity>(AddComponentNodeModel.EntityLabel);
            if (EventType != TypeHandle.Unknown)
            {
                var inputs = HighLevelNodeModelHelpers.GetDataInputsFromComponentType(Stencil, EventType);
                foreach (var(fieldName, fieldType) in inputs)
                    AddDataInput(fieldName, fieldType);
                m_InputFieldNames = inputs.Select(t => t.Item1).ToList();
            }
        }
    }

    [GraphtoolsExtensionMethods]
    public static class SendEventTranslator
    {
        public static IEnumerable<SyntaxNode> Build(
            this RoslynEcsTranslator translator,
            SendEventNodeModel model,
            IPortModel portModel)
        {
            var eventType = model.EventType.Resolve(model.GraphModel.Stencil);
            var entitySyntax = translator.BuildPort(model.EntityPort).SingleOrDefault() as ExpressionSyntax;

            var componentInputs = model.FieldInputs.ToArray();
            var componentSyntax = translator.BuildComponentFromInput(eventType, componentInputs);
            yield return translator.context.GetEntityManipulationTranslator().SendEvent(translator.context,
                entitySyntax, eventType, componentSyntax);
        }

        public static string MakeMissingEventQueryName(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            return $"{iterationContext.GroupName}_Missing{eventType.Name}";
        }

        public static string MakeQueryIncludingEventName(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            return $"{iterationContext.GroupName}_With{eventType.Name}";
        }

        public static string GetBufferVariableName(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            return $"{iterationContext.GroupName}_{eventType.Name}Buffer";
        }
    }
}
