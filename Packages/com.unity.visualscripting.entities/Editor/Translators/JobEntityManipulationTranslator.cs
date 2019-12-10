using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Entities;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    public class JobEntityManipulationTranslator : IEntityManipulationTranslator
    {
        readonly bool m_Concurrent;

        public JobEntityManipulationTranslator(bool concurrent)
        {
            m_Concurrent = concurrent;
        }

        public IEnumerable<StatementSyntax> AddComponent(TranslationContext context, ExpressionSyntax entity,
            ExpressionSyntax componentDeclaration, TypeSyntax componentTypeSyntax, bool isSharedComponent)
        {
            var expressionId = isSharedComponent
                ? nameof(EntityCommandBuffer.Concurrent.AddSharedComponent)
                : nameof(EntityCommandBuffer.Concurrent.AddComponent);

            var arguments = m_Concurrent
                ? new[]
            {
                Argument(IdentifierName(context.GetJobIndexParameterName())),
                Argument(entity),
                Argument(componentDeclaration)
            }
            : new[]
            {
                Argument(entity),
                Argument(componentDeclaration)
            };

            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                expressionId,
                context.GetOrDeclareCommandBuffer(true),
                arguments,
                new[] { componentTypeSyntax }));
        }

        public IEnumerable<StatementSyntax> RemoveComponent(TranslationContext context, ExpressionSyntax entity,
            TypeSyntax componentType)
        {
            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                nameof(EntityCommandBuffer.Concurrent.RemoveComponent),
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent
                ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity)
                }
                : new[]
                {
                    Argument(entity)
                },
                new[] { componentType }));
        }

        public IEnumerable<StatementSyntax> RemoveComponent(TranslationContext context, ExpressionSyntax entity,
            string componentName)
        {
            yield return ExpressionStatement(RoslynBuilder.MethodInvocation(
                nameof(EntityCommandBuffer.Concurrent.RemoveComponent),
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent
                ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity)
                }
                : new[]
                {
                    Argument(entity)
                },
                new[] { IdentifierName(Identifier(componentName)) }));
        }

        public IEnumerable<SyntaxNode> SetComponent(TranslationContext context, ExpressionSyntax entity,
            Type componentType, ExpressionSyntax componentDeclaration)
        {
            var type = TypeSystem.BuildTypeSyntax(componentType);
            var expressionId = typeof(ISharedComponentData).IsAssignableFrom(componentType)
                ? nameof(EntityCommandBuffer.Concurrent.SetSharedComponent)
                : nameof(EntityCommandBuffer.Concurrent.SetComponent);

            yield return RoslynBuilder.MethodInvocation(
                expressionId,
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent
                ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity),
                    Argument(componentDeclaration)
                }
                : new[]
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
                ? nameof(EntityCommandBuffer.Concurrent.SetSharedComponent)
                : nameof(EntityCommandBuffer.Concurrent.SetComponent);

            yield return RoslynBuilder.MethodInvocation(
                expressionId,
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent
                ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity),
                    Argument(componentDeclaration)
                }
                : new[]
                {
                    Argument(entity),
                    Argument(componentDeclaration)
                },
                new[] { IdentifierName(componentTypeName) });
        }

        public IEnumerable<SyntaxNode> GetComponent(TranslationContext context, ExpressionSyntax entity,
            Type componentType)
        {
            context.RecordComponentAccess(
                context.IterationContext,
                componentType.GenerateTypeHandle(context.IterationContext.Stencil),
                RoslynEcsTranslator.AccessMode.Read);
            yield return IdentifierName(context.IterationContext.GetComponentDataName(componentType));
        }

        public IEnumerable<SyntaxNode> DestroyEntity(TranslationContext context, ExpressionSyntax entity)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityCommandBuffer.Concurrent.DestroyEntity),
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity)
                } : new[]
                {
                    Argument(entity)
                },
                Enumerable.Empty<TypeSyntax>());
        }

        public IEnumerable<SyntaxNode> Instantiate(TranslationContext context, ExpressionSyntax entity)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityCommandBuffer.Concurrent.Instantiate),
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                    Argument(entity)
                } :  new[]
                {
                    Argument(entity)
                },
                Enumerable.Empty<TypeSyntax>());
        }

        public IEnumerable<SyntaxNode> CreateEntity(TranslationContext context)
        {
            yield return RoslynBuilder.MethodInvocation(
                nameof(EntityCommandBuffer.Concurrent.CreateEntity),
                context.GetOrDeclareCommandBuffer(true),
                m_Concurrent ? new[]
                {
                    Argument(IdentifierName(context.GetJobIndexParameterName())),
                } :
                Enumerable.Empty<ArgumentSyntax>(),
                Enumerable.Empty<TypeSyntax>());
        }

        public void BuildCriteria(RoslynEcsTranslator translator, TranslationContext context, ExpressionSyntax entity,
            IEnumerable<CriteriaModel> criteriaModels)
        {
            this.BuildCriteria(translator, context, entity, criteriaModels, ReturnStatement());
        }

        public SyntaxNode SendEvent(TranslationContext translatorContext, ExpressionSyntax entity, Type eventType, ExpressionSyntax newEventSyntax)
        {
            if (m_Concurrent)
            {
                //throw new RoslynEcsTranslator.JobSystemNotCompatibleException("Sending event in job isn't implemented yet");
            }


            ExpressionSyntax bufferName = translatorContext.GetEventBufferWriter(translatorContext.IterationContext, entity, eventType, out var bufferInitialization);
            if (bufferInitialization != null)
                translatorContext.AddStatement(bufferInitialization);
            // EntityManager.GetBuffer<TestEvent2>(e).Add(<new event syntax>))
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    bufferName,
                    IdentifierName("Add")))
                    .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            newEventSyntax))))
                    .NormalizeWhitespace();
        }
    }
}
