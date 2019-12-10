using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Transform/Translate")]
    [Serializable]
    public class SetPositionNodeModel : HighLevelNodeModel, IHasInstancePort
    {
        public enum TranslationMode
        {
            Float3,
            Axis,
        }

        public TranslationMode Mode;
        public bool Add;

        public override string Title => Add ? "Translate by" : "Translate to";

        public IPortModel InstancePort { get; private set; }

        public enum InputType
        {
            Value, X, Y, Z
        }

        public static string GetIdForInput(InputType type)
        {
            return type.ToString();
        }

        public IPortModel GetInput(InputType type)
        {
            if (InputsById.TryGetValue(GetIdForInput(type), out var port))
                return port;
            return null;
        }

        protected override void OnDefineNode()
        {
            InstancePort = AddInstanceInput<Translation>();

            switch (Mode)
            {
                case TranslationMode.Float3:
                    AddDataInput<float3>("Value", GetIdForInput(InputType.Value));
                    break;
                case TranslationMode.Axis:
                    AddDataInput<float>("X", GetIdForInput(InputType.X));
                    AddDataInput<float>("Y", GetIdForInput(InputType.Y));
                    AddDataInput<float>("Z", GetIdForInput(InputType.Z));
                    break;
            }
        }
    }

    [GraphtoolsExtensionMethods]
    public static class SetTranslationTranslator
    {
        public static IEnumerable<SyntaxNode> BuildSetTranslation(this RoslynEcsTranslator translator, SetPositionNodeModel model, IPortModel portModel)
        {
            IPortModel entityOrComponentPort = model.InstancePort;
            if (!translator.GetComponentFromEntityOrComponentPort(model, entityOrComponentPort, out _, out ExpressionSyntax setValue, RoslynEcsTranslator.AccessMode.Write))
                yield break;

            switch (model.Mode)
            {
                case SetPositionNodeModel.TranslationMode.Float3:
                    yield return RoslynBuilder.SetProperty(
                        model.Add ? RoslynBuilder.AssignmentKind.Add : RoslynBuilder.AssignmentKind.Set,
                        setValue,
                        translator.BuildPort(model.GetInput(SetPositionNodeModel.InputType.Value)).FirstOrDefault() as ExpressionSyntax,
                        nameof(Translation.Value));
                    break;
                case SetPositionNodeModel.TranslationMode.Axis:
                    var inputTypes = new[]
                    {
                        Tuple.Create(SetPositionNodeModel.InputType.X, nameof(float3.x)),
                        Tuple.Create(SetPositionNodeModel.InputType.Y, nameof(float3.y)),
                        Tuple.Create(SetPositionNodeModel.InputType.Z, nameof(float3.z))
                    };
                    foreach (var inputType in inputTypes)
                    {
                        IPortModel axisPort = model.GetInput(inputType.Item1);
                        yield return RoslynBuilder.SetProperty(
                            model.Add ? RoslynBuilder.AssignmentKind.Add : RoslynBuilder.AssignmentKind.Set,
                            setValue,
                            translator.BuildPort(axisPort).FirstOrDefault() as ExpressionSyntax,
                            nameof(Translation.Value), inputType.Item2);
                    }
                    break;
            }
        }
    }
}
