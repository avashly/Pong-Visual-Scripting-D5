using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using VisualScripting.Model.Common.Extensions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CompilationOptions = UnityEngine.VisualScripting.CompilationOptions;

namespace UnityEditor.VisualScripting.Model.Translators
{
    public interface IEntityManipulationTranslator
    {
        IEnumerable<StatementSyntax> AddComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            ExpressionSyntax componentDeclaration, TypeSyntax componentTypeSyntax, bool isSharedComponent);
        IEnumerable<StatementSyntax> RemoveComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            TypeSyntax componentType);
        IEnumerable<StatementSyntax> RemoveComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            string componentTypeName);
        IEnumerable<SyntaxNode> SetComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            Type componentType,
            ExpressionSyntax componentDeclaration);
        IEnumerable<SyntaxNode> SetComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            string componentTypeName,
            ExpressionSyntax componentDeclaration,
            bool isSharedComponent);
        IEnumerable<SyntaxNode> GetComponent(
            TranslationContext context,
            ExpressionSyntax entity,
            Type componentType);
        IEnumerable<SyntaxNode> DestroyEntity(TranslationContext context, ExpressionSyntax entity);
        IEnumerable<SyntaxNode> Instantiate(TranslationContext context, ExpressionSyntax entity);
        IEnumerable<SyntaxNode> CreateEntity(TranslationContext context);

        void BuildCriteria(RoslynEcsTranslator translator, TranslationContext context, ExpressionSyntax entity,
            IEnumerable<CriteriaModel> criteriaModels);

        SyntaxNode SendEvent(TranslationContext translatorContext, ExpressionSyntax entity, Type eventType, ExpressionSyntax componentSyntax);
    }

    static class EntityManipulationTranslatorExtensions
    {
        internal static void BuildCriteria(this IEntityManipulationTranslator self, RoslynEcsTranslator translator,
            TranslationContext context, ExpressionSyntax entity, IEnumerable<CriteriaModel> criteriaModels,
            StatementSyntax conditionBreak)
        {
            var criteriaList = criteriaModels.ToList();
            if (!criteriaList.Any())
                return;

            ExpressionSyntax ifExpressionSyntax = null;
            foreach (var model in criteriaList)
            {
                ExpressionSyntax finalExpression = null;
                foreach (var criterion in model.Criteria)
                {
                    if (!(criterion is Criterion componentCriterion))
                        continue;

                    var rightValue = componentCriterion.Value is ConstantNodeModel constantNodeModel
                        ? translator.Constant(constantNodeModel.ObjectValue, translator.Stencil, constantNodeModel.Type)
                        : IdentifierName(componentCriterion.Value.DeclarationModel.VariableName);
                    var expression = BinaryExpression(
                        componentCriterion.Operator.ToSyntaxKind(),
                        GetLeftValueFromCriterion(self, context, translator.Stencil, entity, componentCriterion),
                        rightValue) as ExpressionSyntax;

                    // TODO : Temporary. Once Unity.Mathematics have IComparable interface, remove this
                    // and use IComparable and IEquatable methods instead of operators
                    var rightValueType = GetRightValueType(componentCriterion, translator.Stencil);
                    if (rightValueType.Namespace != null && rightValueType.Namespace.StartsWith("Unity.Mathematics"))
                    {
                        expression = RoslynBuilder.MethodInvocation(
                            nameof(math.all),
                            typeof(math).ToTypeSyntax(),
                            new[] { Argument(expression) },
                            Enumerable.Empty<TypeSyntax>());
                    }

                    finalExpression = finalExpression == null
                        ? expression
                        : BinaryExpression(SyntaxKind.LogicalAndExpression, finalExpression, expression);
                }

                if (finalExpression == null)
                    continue;
                context.AddStatement(RoslynBuilder.DeclareLocalVariable(
                    typeof(bool),
                    model.Name,
                    finalExpression,
                    RoslynBuilder.VariableDeclarationType.InferredType));

                var unaryExpression = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, IdentifierName(model.Name));
                if (model != criteriaList.First())
                {
                    ifExpressionSyntax = BinaryExpression(
                        SyntaxKind.LogicalAndExpression,
                        ifExpressionSyntax,
                        unaryExpression);
                }
                else
                {
                    ifExpressionSyntax = unaryExpression;
                }
            }

            if (ifExpressionSyntax != null)
            {
                BlockSyntax statementSyntax;
                if ((translator.Options & CompilationOptions.Tracing) != 0 && context is JobContext jobContext)
                    statementSyntax = Block(List<SyntaxNode>()
                        .Add(jobContext.MakeTracingEndForEachIndexStatement())
                        .Add(conditionBreak));
                else
                    statementSyntax = Block(SingletonList(conditionBreak));
                context.AddStatement(IfStatement(ifExpressionSyntax, statementSyntax));
            }
        }

        static MemberAccessExpressionSyntax GetLeftValueFromCriterion(IEntityManipulationTranslator self,
            TranslationContext context, Stencil stencil, ExpressionSyntax entity, Criterion criterion)
        {
            var componentType = criterion.ObjectType.Resolve(stencil);
            var componentExpression = self.GetComponent(context, entity, componentType).Single();

            var access = RoslynBuilder.MemberReference(componentExpression, criterion.Member.Path[0]);
            for (var i = 1; i < criterion.Member.Path.Count; i++)
            {
                access = RoslynBuilder.MemberReference(access, criterion.Member.Path[i]);
            }

            return access;
        }

        static Type GetRightValueType(Criterion criterion, Stencil stencil)
        {
            return criterion.Value is ConstantNodeModel constant
                ? constant.Type
                : criterion.Value.DeclarationModel.DataType.Resolve(stencil);
        }

        internal static IEnumerable<SyntaxNode> AddComponent(this IEntityManipulationTranslator entityManipulationTranslator,
            TranslationContext context,
            ExpressionSyntax entity,
            Type componentType,
            ExpressionSyntax componentDeclaration)
        {
            return entityManipulationTranslator.AddComponent(context, entity, componentDeclaration, componentType.ToTypeSyntax(),
                typeof(ISharedComponentData).IsAssignableFrom(componentType));
        }

        internal static IEnumerable<StatementSyntax> RemoveComponent(
            this IEntityManipulationTranslator entityManipulationTranslator,
            TranslationContext context,
            ExpressionSyntax entity,
            Type componentType)
        {
            return entityManipulationTranslator.RemoveComponent(context, entity, componentType.ToTypeSyntax());
        }
    }
}
