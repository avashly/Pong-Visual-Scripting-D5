using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using Unity.Entities;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/On Key Press")]
    [Serializable]
    public class OnKeyPressEcsNodeModel : OnEntitiesEventBaseNodeModel
    {
        public enum KeyPressType
        {
            Down,
            Up,
            Press
        }

        public KeyCode Code = KeyCode.Space;
        public KeyPressType PressType = KeyPressType.Down;

        protected override string MakeTitle() => "On " + Enum.GetName(typeof(KeyCode), Code) + " " + Enum.GetName(typeof(KeyPressType), PressType);

        public override UpdateMode Mode => UpdateMode.OnUpdate;
    }

    [GraphtoolsExtensionMethods]
    public static class KeyPressTranslator
    {
        public static IEnumerable<SyntaxNode> BuildOnKeyPressEcs(this RoslynEcsTranslator translator,
            OnKeyPressEcsNodeModel model, IPortModel portModel)
        {
            return translator.BuildOnEntitiesEventBase(model, MakeKeyPressConstraint(model));
        }

        public static ExpressionSyntax MakeKeyPressConstraint(OnKeyPressEcsNodeModel model)
        {
            var code = model.Code;
            var pressType = model.PressType;
            if (code == KeyCode.None)
                return null;
            return InvocationExpression(KeyFunctionForPressType(pressType))
                .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(GetEnumSyntax(code)))));
        }

        static ExpressionSyntax KeyFunctionForPressType(OnKeyPressEcsNodeModel.KeyPressType type)
        {
            IdentifierNameSyntax getKeyFunction = IdentifierName(nameof(Input.GetKey));
            if (type == OnKeyPressEcsNodeModel.KeyPressType.Down)
                getKeyFunction = IdentifierName(nameof(Input.GetKeyDown));
            else if (type == OnKeyPressEcsNodeModel.KeyPressType.Up)
                getKeyFunction = IdentifierName(nameof(Input.GetKeyUp));
            return MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(nameof(Input)), getKeyFunction);
        }

        static MemberAccessExpressionSyntax GetEnumSyntax<T>(T value) where T : Enum
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(typeof(T).Name),
                IdentifierName(Enum.GetName(typeof(T), value)));
        }
    }
}
