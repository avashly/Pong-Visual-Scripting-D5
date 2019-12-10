using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Stack, "Transform/Rotate")]
    [Serializable]
    public class SetRotationNodeModel : HighLevelNodeModel
    {
        [PublicAPI]
        public enum RotationMode
        {
            Axis,
            Quaternion,
            Euler,
        }

        public RotationMode Mode;
        public bool Add;

        public override string Title => Add ? "Rotate by" : "Set rotation to";

        public IPortModel InstancePort { get; private set; }

        public enum InputType
        {
            Angle, Axis, Quaternion, X, Y, Z
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
            InstancePort = AddInstanceInput<Rotation>();
            switch (Mode)
            {
                case RotationMode.Axis:
                    AddDataInput<float>("Angle", GetIdForInput(InputType.Angle));
                    AddDataInput<float3>("Axis", GetIdForInput(InputType.Axis));
                    break;
                case RotationMode.Quaternion:
                    AddDataInput<quaternion>("Quaternion", GetIdForInput(InputType.Quaternion));
                    break;
                case RotationMode.Euler:
                    AddDataInput<float>("X", GetIdForInput(InputType.X));
                    AddDataInput<float>("Y", GetIdForInput(InputType.Y));
                    AddDataInput<float>("Z", GetIdForInput(InputType.Z));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    [GraphtoolsExtensionMethods]
    public static class SetRotationTranslator
    {
        public static IEnumerable<SyntaxNode> BuildSetTranslation(this RoslynEcsTranslator translator, SetRotationNodeModel model, IPortModel portModel)
        {
            ExpressionSyntax BuildPortForInput(SetRotationNodeModel.InputType inputType)
            {
                return translator.BuildPort(model.GetInput(inputType)).FirstOrDefault() as ExpressionSyntax;
            }

            ExpressionSyntax value;
            switch (model.Mode)
            {
                case SetRotationNodeModel.RotationMode.Axis:
                    var axisValue = BuildPortForInput(SetRotationNodeModel.InputType.Axis);
                    var angleValue = BuildPortForInput(SetRotationNodeModel.InputType.Angle);
                    axisValue = InvocationExpression(MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(nameof(math)), IdentifierName(nameof(math.normalize)))).WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(axisValue))));
                    value = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(nameof(quaternion)),
                            IdentifierName(nameof(quaternion.AxisAngle))))
                            .WithArgumentList(
                        ArgumentList(
                            SeparatedList(new[]
                            {
                                Argument(axisValue),
                                Argument(angleValue)
                            })));
                    break;
                case SetRotationNodeModel.RotationMode.Quaternion:
                    value = BuildPortForInput(SetRotationNodeModel.InputType.Quaternion);
                    break;
                case SetRotationNodeModel.RotationMode.Euler:
                    value = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(nameof(quaternion)),
                            IdentifierName(nameof(quaternion.EulerXYZ))))
                            .WithArgumentList(
                        ArgumentList(
                            SeparatedList(new[]
                            {
                                Argument(BuildPortForInput(SetRotationNodeModel.InputType.X)),
                                Argument(BuildPortForInput(SetRotationNodeModel.InputType.Y)),
                                Argument(BuildPortForInput(SetRotationNodeModel.InputType.Z))
                            })));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (!translator.GetComponentFromEntityOrComponentPort(model, model.InstancePort, out _, out ExpressionSyntax setValue, RoslynEcsTranslator.AccessMode.Write))
                yield break;
            var finalValue = !model.Add
                ? value
                : InvocationExpression( // rot.value = math.mul(rot.value, <input>)
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(nameof(math)),
                    IdentifierName(nameof(math.mul))))
                    .WithArgumentList(
                ArgumentList(
                    SeparatedList(new[]
                    {
                        Argument(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                setValue,
                                IdentifierName(nameof(Rotation.Value)))),
                        Argument(value)
                    })));
            yield return RoslynBuilder.SetProperty(
                RoslynBuilder.AssignmentKind.Set,
                setValue,
                finalValue,
                nameof(Rotation.Value)
            );
        }
    }
}
