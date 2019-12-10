using System;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEngine;
using VisualScripting.Entities.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    [GraphtoolsExtensionMethods]
    public static class DeltaTimeTranslator
    {
        // Translator for Coroutine MoveNext Parameters have to follow this exact signature
        //   the last parameter being your custom Attribute inheriting from CoroutineSpecialVariableAttribute
        public static ExpressionSyntax GetInternalVariable(this CoroutineParameterTranslator _, CoroutineInternalVariableAttribute attr)
        {
            switch (attr.Variable)
            {
                case CoroutineInternalVariable.DeltaTime:
                    return MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(nameof(Time)),
                        IdentifierName(nameof(Time.deltaTime)));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
