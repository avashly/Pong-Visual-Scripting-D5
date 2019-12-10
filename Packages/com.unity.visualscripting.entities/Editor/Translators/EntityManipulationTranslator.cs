using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Model.Stencils;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    class EntityManipulationTranslator : IEntityManipulationTranslator
    {
        public IEnumerable<StatementSyntax> AddComponent(TranslationContext context, ExpressionSyntax entity,
            ExpressionSyntax componentDeclaration, TypeSyntax componentTypeSyntax, bool isSharedComponent)
        {
            string expressionId = isSharedComponent
                ? nameof(EntityManager.AddSharedComponentData)
                : nameof(EntityManager.AddComponentData);

            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                expressionId,
                IdentifierName(nameof(EntityManager)),
                new[]
                {
                    Argument(entity),
                    Argument(componentDeclaration)
                },
                new[] { componentTypeSyntax }));
        }

        public IEnumerable<StatementSyntax> RemoveComponent(TranslationContext context, ExpressionSyntax entity,
            TypeSyntax componentType)
        {
            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                nameof(EntityManager.RemoveComponent),
                IdentifierName(nameof(EntityManager)),
                new[] { Argument(entity) },
                new[] { componentType }));
        }

        public IEnumerable<StatementSyntax> RemoveComponent(TranslationContext context, ExpressionSyntax entity,
            string componentName)
        {
            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                nameof(EntityManager.RemoveComponent),
                IdentifierName(nameof(EntityManager)),
                new[] { Argument(entity) },
                new[] { IdentifierName(Identifier(componentName)) }));
        }

        public IEnumerable<SyntaxNode> SetComponent(TranslationContext context, ExpressionSyntax entity,
            Type componentType, ExpressionSyntax componentDeclaration)
        {
            var type = TypeSystem.BuildTypeSyntax(componentType);
            var expressionId = typeof(ISharedComponentData).IsAssignableFrom(componentType)
                ? nameof(EntityManager.SetSharedComponentData)
                : nameof(EntityManager.SetComponentData);

            yield return RoslynBuilder.MethodInvocation(
                expressionId,
                IdentifierName(nameof(EntityManager)),
                new[]
                {
                    Argument(entity),
                    Argument(componentDeclaration)
                },
                new[] { type });
        }

        public IEnumerable<SyntaxNode> SetComponent(TranslationContext context, ExpressionSyntax entity,
            string componentTypeName, ExpressionSyntax componentDeclaration, bool isSharedComponent)
        {
            var expressionId = isSharedComponent
                ? nameof(EntityManager.SetSharedComponentData)
                : nameof(EntityManager.SetComponentData);

            yield return RoslynBuilder.MethodInvocation(
                expressionId,
                IdentifierName(nameof(EntityManager)),
                new[]
                {
                    Argument(entity),
                    Argument(componentDeclaration)
                },
                new[] { IdentifierName(componentTypeName) });
        }

        public IEnumerable<SyntaxNode> GetComponent(TranslationContext context, ExpressionSyntax entity,
            Type componentType)
        {
            var type = TypeSystem.BuildTypeSyntax(componentType);
            context.RecordComponentAccess(
                context.IterationContext,
                componentType.GenerateTypeHandle(context.IterationContext.Stencil),
                RoslynEcsTranslator.AccessMode.Read);
            var expressionId = typeof(ISharedComponentData).IsAssignableFrom(componentType)
                ? nameof(EntityManager.GetSharedComponentData)
                : nameof(EntityManager.GetComponentData);

            yield return RoslynBuilder.MethodInvocation(
                expressionId,
                IdentifierName(nameof(EntityManager)),
                new[] { Argument(entity) },
                new[] { type });
        }

        public IEnumerable<SyntaxNode> DestroyEntity(TranslationContext context, ExpressionSyntax entity)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityManager.DestroyEntity),
                IdentifierName(nameof(EntityManager)),
                new[] { Argument(entity) },
                Enumerable.Empty<TypeSyntax>());
        }

        public IEnumerable<SyntaxNode> Instantiate(TranslationContext context, ExpressionSyntax entity)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityManager.Instantiate),
                IdentifierName(nameof(EntityManager)),
                new[] { Argument(entity) },
                Enumerable.Empty<TypeSyntax>());
        }

        public IEnumerable<SyntaxNode> CreateEntity(TranslationContext context)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityManager.CreateEntity),
                IdentifierName(nameof(EntityManager)),
                Enumerable.Empty<ArgumentSyntax>(),
                Enumerable.Empty<TypeSyntax>());
        }

        public void BuildCriteria(RoslynEcsTranslator translator, TranslationContext context, ExpressionSyntax entity,
            IEnumerable<CriteriaModel> criteriaModels)
        {
            this.BuildCriteria(
                translator,
                context,
                entity,
                criteriaModels.Where(x => x.Criteria.Any()),
                ContinueStatement()
            );
        }

        public SyntaxNode SendEvent(TranslationContext translatorContext, ExpressionSyntax entity, Type eventType, ExpressionSyntax newEventSyntax)
        {
            throw new NotImplementedException();
        }
    }
}
