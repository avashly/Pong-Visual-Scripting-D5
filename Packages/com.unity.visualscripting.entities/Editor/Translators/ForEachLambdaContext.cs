using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.Plugins;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Plugins;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    sealed class ForEachLambdaContext : ForEachContext
    {
        IEntityManipulationTranslator m_EntityTranslator;
        string m_CoroutineComponentName;
        List<ExpressionSyntax> m_BodyConditions;

        public ForEachLambdaContext(IIteratorStackModel query, TranslationContext parent, UpdateMode mode)
            : base(query, parent, mode)
        {
            m_BodyConditions = new List<ExpressionSyntax>();
        }

        public override TranslationContext PushContext(IIteratorStackModel query,
            RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode, bool isCoroutine = false)
        {
            if (isCoroutine)
                return new CoroutineContext(this, roslynEcsTranslator);

            return new ForEachContext(query, this, mode);
        }

        protected override IEnumerable<StatementSyntax> OnPopContext()
        {
            var syntax = RoslynEcsTranslator.MakeCondition(m_BodyConditions, BuildInner());
            yield return AddMissingEventBuffers(IterationContext, syntax);
        }

        StatementSyntax BuildInner()
        {
            const string eventBufferName = "eventBuffer";
            Type eventType = IterationContext.UpdateMode == UpdateMode.OnEvent
                ? ((OnEventNodeModel)IterationContext.Query).EventTypeHandle.Resolve(IterationContext.Stencil)
                : null;

            var parameters = new List<ParameterSyntax>
            {
                Parameter(
                    Identifier(EntityName))
                    .WithType(typeof(Entity).ToTypeSyntax())
            };

            var block = Block();
            if (!string.IsNullOrEmpty(m_CoroutineComponentName))
            {
                parameters.Add(Parameter(
                    Identifier(CoroutineContext.BuildCoroutineParameterName(m_CoroutineComponentName)))
                        .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                        .WithType(IdentifierName(m_CoroutineComponentName)));
            }

            var stateComponentName = IterationContext.UpdateMode == UpdateMode.OnUpdate || IterationContext.UpdateMode == UpdateMode.OnEvent
                ? null
                : IncludeTrackingSystemStateComponent(IterationContext.Query.ComponentQueryDeclarationModel, IterationContext.UpdateMode == UpdateMode.OnEnd);
            var stateComponentParameterName = stateComponentName?.ToLowerInvariant();

            switch (IterationContext.UpdateMode)
            {
                case UpdateMode.OnUpdate:
                    break;
                case UpdateMode.OnEvent:
                    // Add it at the end

                    break;
                case UpdateMode.OnStart:
                    m_UpdateStatements.AddRange(GetEntityManipulationTranslator().AddComponent(
                        this,
                        IdentifierName(EntityName),
                        DefaultExpression(IdentifierName(stateComponentName)),
                        IdentifierName(stateComponentName),
                        false));
                    break;
                case UpdateMode.OnEnd:
                    parameters.Add(MakeLambdaParameter(
                        stateComponentParameterName,
                        IdentifierName(stateComponentName),
                        false,
                        false));

                    m_UpdateStatements.AddRange(GetEntityManipulationTranslator().RemoveComponent(
                        this,
                        IdentifierName(EntityName),
                        IdentifierName(stateComponentName)));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // SharedComponents should be inserted first in ForEach
            var sortedComponents = IterationContext.FlattenedComponentDefinitions()
                .OrderBy(d =>
                    !typeof(ISharedComponentData).IsAssignableFrom(d.TypeHandle.Resolve(IterationContext.Stencil)))
                .ToList();
            foreach (ComponentDefinition definition in sortedComponents)
            {
                if (!RoslynEcsTranslatorExtensions.ShouldGenerateComponentAccess(
                    definition.TypeHandle,
                    true,
                    out Type componentType,
                    IterationContext.Stencil,
                    out bool _,
                    out bool isGameObjectComponent))
                    continue;

                if (!m_WrittenComponents.TryGetValue(definition.TypeHandle, out RoslynEcsTranslator.AccessMode mode))
                    mode = RoslynEcsTranslator.AccessMode.None;

                if (mode == RoslynEcsTranslator.AccessMode.None)
                    continue;

                string componentDataName = IterationContext.GetComponentDataName(componentType);
                parameters.Add(MakeLambdaParameter(
                    componentDataName,
                    componentType.ToTypeSyntax(),
                    isGameObjectComponent,
                    typeof(ISharedComponentData).IsAssignableFrom(componentType)));
            }

            switch (IterationContext.UpdateMode)
            {
                case UpdateMode.OnEvent:
                    parameters.Add(MakeLambdaParameter(
                        eventBufferName,
                        GenericName("DynamicBuffer").WithTypeArgumentList(TypeArgumentList(SingletonSeparatedList(eventType.ToTypeSyntax()))),
                        false,
                        true));
                    break;
            }

            ExpressionSyntax baseQuery = InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Entities"),
                    IdentifierName("With")))
                    .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            IdentifierName(IterationContext.UpdateMode == UpdateMode.OnEvent
                                ? SendEventTranslator.MakeQueryIncludingEventName(IterationContext, eventType)
                                : IterationContext.GroupName)))));

            if (IterationContext.UpdateMode == UpdateMode.OnEnd)
            {
                // REPLACE query
                baseQuery = IdentifierName("Entities");
                m_UpdateStatements.Insert(0, IfStatement(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(stateComponentParameterName),
                        IdentifierName("Processed")),
                    ReturnStatement()));
            }

            BlockSyntax innerLambdaBody;
            if (IterationContext.UpdateMode == UpdateMode.OnEvent)
            {
                //for (int event_index = 0; event_index < eventBuffer.Length; event_index++) { T ev = eventBuffer[event_index]; ... }
                const string eventIndexName = "event_index";
                m_UpdateStatements.Insert(0, RoslynBuilder.DeclareLocalVariable(eventType, "ev",
                    ElementAccessExpression(
                        IdentifierName(eventBufferName))
                        .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName(eventIndexName)))))
                        .NormalizeWhitespace()));
                innerLambdaBody = Block(ForStatement(
                    Block(m_UpdateStatements))
                        .WithDeclaration(
                        VariableDeclaration(
                            PredefinedType(
                                Token(SyntaxKind.IntKeyword)))
                            .WithVariables(
                            SingletonSeparatedList(
                                VariableDeclarator(
                                    Identifier(eventIndexName))
                                    .WithInitializer(
                                    EqualsValueClause(
                                        LiteralExpression(
                                            SyntaxKind.NumericLiteralExpression,
                                            Literal(0)))))))
                        .WithCondition(
                            BinaryExpression(
                                SyntaxKind.LessThanExpression,
                                IdentifierName(eventIndexName),
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(eventBufferName),
                                    IdentifierName("Length"))))
                        .WithIncrementors(
                            SingletonSeparatedList<ExpressionSyntax>(
                                PostfixUnaryExpression(
                                    SyntaxKind.PostIncrementExpression,
                                    IdentifierName(eventIndexName))))
                        .NormalizeWhitespace());
            }
            else
                innerLambdaBody = Block(m_UpdateStatements);

            if ((TranslationOptions & RoslynEcsTranslator.TranslationOptions.Tracing) != 0)
            {
                var recorderDeclaration = RoslynBuilder.DeclareLocalVariable(typeof(DebuggerTracer.EntityFrameTrace), DebugInstrumentationHandler.RecorderVariableName, MakeInitRecorderExpression(), RoslynBuilder.VariableDeclarationType.InferredType);
                var stackDebugInstrumentation = InstrumentForInEditorDebugging.BuildLastCallFrameExpression(0, IterationContext.Query.Guid, IdentifierName(k_RecorderWriterName));
                innerLambdaBody = innerLambdaBody.WithStatements(innerLambdaBody.Statements.Insert(0, recorderDeclaration).Insert(1, stackDebugInstrumentation));
            }

            var parenthesizedLambdaExpressionSyntax = ParenthesizedLambdaExpression(
                innerLambdaBody)
                    .WithParameterList(
                ParameterList().AddParameters(parameters.ToArray()));

            var updateCall = ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        baseQuery,
                        IdentifierName("ForEach")))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                parenthesizedLambdaExpressionSyntax)))));

            if (IterationContext.UpdateMode == UpdateMode.OnEnd)
            {
                var addMissingStateComponents = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("EntityManager"),
                            IdentifierName("AddComponent")))
                        .WithArgumentList(
                        ArgumentList(
                            SeparatedList(
                                new[]
                                {
                                    Argument(
                                        IdentifierName(RootContext.GetQueryAddStateName(IterationContext.GroupName))),
                                    Argument(
                                        TypeOfExpression(
                                            IdentifierName(stateComponentName)))
                                }))));

                var foreachClearProcessed = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName("Entities"),
                            IdentifierName("ForEach")))
                        .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    ParenthesizedLambdaExpression(
                                        MakeProcessed(false))
                                        .WithParameterList(
                                        ParameterList(
                                            SingletonSeparatedList(
                                                Parameter(
                                                    Identifier(stateComponentParameterName))
                                                    .WithModifiers(
                                                    TokenList(
                                                        Token(SyntaxKind.RefKeyword)))
                                                    .WithType(
                                                        IdentifierName(stateComponentName))))))))));
                var foreachQueryMarkProcessed = ExpressionStatement(
                    InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            InvocationExpression(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName("Entities"),
                                    IdentifierName("With")))
                                .WithArgumentList(
                                ArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            IdentifierName(IterationContext.GroupName))))),
                            IdentifierName("ForEach")))
                        .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    ParenthesizedLambdaExpression(MakeProcessed(true))
                                        .WithParameterList(
                                        ParameterList(
                                            SeparatedList(parameters))))))));

                return block.AddStatements(
                    addMissingStateComponents,
                    foreachClearProcessed,
                    foreachQueryMarkProcessed,
                    updateCall);
            }

            return block.AddStatements(updateCall);

            ParameterSyntax MakeLambdaParameter(string componentDataName, TypeSyntax typeSyntax,
                bool isGameObjectComponent, bool isSharedComponent)
            {
                var modifiers = new List<SyntaxToken>();
                if (!isSharedComponent)
                    modifiers.Add(Token(SyntaxKind.RefKeyword));

                var parameterSyntax = Parameter(
                    Identifier(componentDataName))
                        .WithType(
                    typeSyntax);
                if (!isGameObjectComponent)
                    parameterSyntax = parameterSyntax
                        .WithModifiers(TokenList(modifiers));
                return parameterSyntax;
            }

            AssignmentExpressionSyntax MakeProcessed(bool processed)
            {
                return AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(stateComponentParameterName),
                        IdentifierName("Processed")),
                    LiteralExpression(processed
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression));
            }
        }

        public override IEntityManipulationTranslator GetEntityManipulationTranslator()
        {
            return m_EntityTranslator ?? (m_EntityTranslator = new JobEntityManipulationTranslator(false));
        }

        public override IdentifierNameSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            return IdentifierName(nameof(ComponentSystem.PostUpdateCommands));
        }

        internal override void IncludeCoroutineComponent(RoslynEcsTranslator.IterationContext iterationContext,
            string coroutineComponentName)
        {
            // Parent (RootContext) creates queries with coroutine
            Parent.IncludeCoroutineComponent(iterationContext, coroutineComponentName);
            m_CoroutineComponentName = coroutineComponentName;
        }
    }
}
