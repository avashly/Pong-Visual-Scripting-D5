using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.GraphViewModel;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    sealed class NestedCoroutineContext : CoroutineContext
    {
        string NestedCoroutineFieldName => m_ComponentTypeName;

        internal override bool IsJobContext => ((CoroutineContext)Parent).IsJobContext;

        public NestedCoroutineContext(TranslationContext parent, RoslynEcsTranslator translator)
            : base(parent, translator)
        {
            m_ComponentTypeName = translator.MakeUniqueName(
                $"{IterationContext.GroupName}NestedCoroutine").ToPascalCase();
        }

        protected override IEnumerable<StatementSyntax> OnPopContext()
        {
            var thenStatement = ExpressionStatement(RoslynBuilder.Assignment(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    GetNestedCoroutineField(),
                    IdentifierName(k_CoroutineStateVariableName)),
                PrefixUnaryExpression(
                    SyntaxKind.UnaryMinusExpression,
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(1)))));

            var ifStatement = IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    RoslynBuilder.MethodInvocation(
                        UpdateMethodName,
                        IdentifierName(GetSystemClassName()),
                        m_Parameters.Keys,
                        Enumerable.Empty<TypeSyntax>())),
                thenStatement);

            yield return ifStatement;
        }

        public override void BuildComponent(IStackModel stack, RoslynEcsTranslator translator)
        {
            // Build stack
            BuildStack(translator, stack, 0);

            // Create coroutine component
            var members = BuildComponentMembers();
            DeclareComponent<ISystemStateComponentData>(m_ComponentTypeName, members);

            // Create coroutine update method
            DeclareSystemMethod(BuildUpdateCoroutineMethod());

            // Add a nestedCoroutine field in the parent coroutine component
            ((CoroutineContext)Parent).AddComponentField(m_ComponentTypeName, NestedCoroutineFieldName);
        }

        protected override IdentifierNameSyntax BuildCoroutineParameter()
        {
            var coroutineIdentifier = IdentifierName(CoroutineParameterName);
            m_Parameters.Add(
                Argument(GetNestedCoroutineField())
                    .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                Parameter(coroutineIdentifier.Identifier)
                    .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                    .WithType(IdentifierName(m_ComponentTypeName)));

            return coroutineIdentifier;
        }

        MemberAccessExpressionSyntax GetNestedCoroutineField()
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(((CoroutineContext)Parent).CoroutineParameterName),
                IdentifierName(NestedCoroutineFieldName));
        }
    }
}
