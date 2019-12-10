using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    class ForEachContext : TranslationContext
    {
        List<StatementSyntax> m_InitComponentDataArrays = new List<StatementSyntax>();
        List<StatementSyntax> m_DisposeComponentDataArrays = new List<StatementSyntax>();
        LocalDeclarationStatementSyntax m_EntityDeclaration;

        protected List<StatementSyntax> m_UpdateStatements = new List<StatementSyntax>();
        protected Dictionary<TypeHandle, RoslynEcsTranslator.AccessMode> m_WrittenComponents = new Dictionary<TypeHandle, RoslynEcsTranslator.AccessMode>();
        string m_CoroutineComponentName;

        public ForEachContext(IIteratorStackModel query, TranslationContext parent, UpdateMode mode)
            : base(parent)
        {
            IterationContext = new RoslynEcsTranslator.IterationContext(this, query, parent.MakeUniqueName(query.ComponentQueryDeclarationModel.VariableName), mode);
            GetOrDeclareComponentQuery(IterationContext);
        }

        protected override IEnumerable<StatementSyntax> OnPopContext()
        {
            BlockSyntax forBody = Block();

            var loopIndexName = IterationContext.IndexVariableName;
            List<StatementSyntax> reassignComponentDataArrays = new List<StatementSyntax>();


            BlockSyntax blockSyntax = Block();

            // foreach in a job: the entity array will be declared as a job field, initialized at job creation

            StatementSyntax initEntitiesArray = GetOrDeclareEntityArray(IterationContext, out var disposeEntitiesArray);

            foreach (ComponentDefinition definition in IterationContext.FlattenedComponentDefinitions())
            {
                if (!RoslynEcsTranslatorExtensions.ShouldGenerateComponentAccess(definition.TypeHandle, true, out var componentType, IterationContext.Stencil, out bool isShared, out bool isGameObjectComponent) ||
                    isGameObjectComponent)
                    continue;

                if (!m_WrittenComponents.TryGetValue(definition.TypeHandle, out var mode))
                    mode = RoslynEcsTranslator.AccessMode.None;

                if (mode == RoslynEcsTranslator.AccessMode.None)
                    continue;

                ExpressionSyntax initValue;
                if (isShared)
                {
                    initValue = InvocationExpression(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(nameof(EntityManager)),
                            GenericName(
                                Identifier(nameof(EntityManager.GetSharedComponentData)))
                                .WithTypeArgumentList(
                                TypeArgumentList(
                                    SingletonSeparatedList(
                                        TypeSystem.BuildTypeSyntax(componentType))))))
                            .WithArgumentList(
                        ArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName(EntityName)))));
                }
                else
                {
                    string compArrayName = GetOrDeclareComponentArray(
                        IterationContext,
                        definition.TypeHandle.Name(IterationContext.Stencil),
                        out var componentDataArrayInitialization,
                        out var componentDataArrayDisposal);
                    if (componentDataArrayInitialization != null)
                        m_InitComponentDataArrays.Add(componentDataArrayInitialization);
                    if (componentDataArrayDisposal != null)
                        m_DisposeComponentDataArrays.Add(componentDataArrayDisposal);

                    initValue = ElementAccessExpression(IdentifierName(compArrayName))
                        .WithArgumentList(
                        BracketedArgumentList(
                            SingletonSeparatedList(
                                Argument(
                                    IdentifierName(loopIndexName)))));
                }

                // per-entity component variable
                StatementSyntax componentVarDeclaration = RoslynBuilder.DeclareLocalVariable(
                    componentType,
                    IterationContext.GetComponentDataName(componentType),
                    initValue,
                    RoslynBuilder.VariableDeclarationType.InferredType);
                forBody = forBody.AddStatements(componentVarDeclaration);

                if (mode == RoslynEcsTranslator.AccessMode.Write)
                {
                    reassignComponentDataArrays.Add(ExpressionStatement(
                        GetEntityManipulationTranslator().SetComponent(
                            this,
                            ElementAccessExpression(IdentifierName(IterationContext.EntitiesArrayName))
                                .WithArgumentList(
                                BracketedArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            IdentifierName(IterationContext.IndexVariableName))))),
                            componentType,
                            IdentifierName(IterationContext.GetComponentDataName(componentType)))
                            .Cast<ExpressionSyntax>()
                            .Single())
                    );
                }
            }

            if (!string.IsNullOrEmpty(m_CoroutineComponentName))
            {
                forBody = forBody.AddStatements(GetCoroutineComponentDeclaration(loopIndexName));
            }

            forBody = forBody.AddStatements(m_UpdateStatements.ToArray());
            forBody = forBody.AddStatements(reassignComponentDataArrays.ToArray());

            if (forBody.Statements.Count != 0) // strip the iteration loop if empty
            {
                forBody = forBody.WithStatements(forBody.Statements.Insert(0, m_EntityDeclaration));

                if (initEntitiesArray != null)
                    blockSyntax = blockSyntax.AddStatements(initEntitiesArray);

                blockSyntax = blockSyntax
                    .AddStatements(m_InitComponentDataArrays.ToArray())
                    .AddStatements(RoslynEcsTranslatorExtensions.ComponentQueryForLoop(forBody, loopIndexName, IterationContext.EntitiesArrayName))
                    .AddStatements(m_DisposeComponentDataArrays.ToArray());

                if (disposeEntitiesArray != null)
                    blockSyntax = blockSyntax.AddStatements(disposeEntitiesArray);
            }

            yield return AddMissingEventBuffers(IterationContext, blockSyntax);
        }

        protected ExpressionSyntax MakeInitRecorderExpression()
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(nameof(TracingRecorderSystem)),
                    IdentifierName(nameof(TracingRecorderSystem.GetRecorder))))
                    .WithArgumentList(
                ArgumentList(
                    SeparatedList(
                        new[]
                        {
                            Argument(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(((UnityEngine.Object)IterationContext.Query.GraphModel.AssetModel).GetInstanceID()))),
                            Argument(
                                MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    IdentifierName(EntityName),
                                    IdentifierName("Index")))
                        })));
        }

        public override TranslationContext PushContext(IIteratorStackModel query,
            RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode, bool isCoroutine = false)
        {
            if (isCoroutine)
                return new CoroutineContext(this, roslynEcsTranslator);

            return new ForEachContext(query, this, mode);
        }

        public override void AddEntityDeclaration(string variableName)
        {
            // Entity {m_EntityName} = {IterationContext.EntitiesArrayName}[{IterationContext.IndexVariableName}];
            EntityName = variableName;
            m_EntityDeclaration = RoslynBuilder.DeclareLocalVariable(
                typeof(Entity),
                EntityName,
                ElementAccessExpression(IdentifierName(IterationContext.EntitiesArrayName))
                    .WithArgumentList(
                    BracketedArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                IdentifierName(IterationContext.IndexVariableName))))),
                RoslynBuilder.VariableDeclarationType.InferredType);
        }

        public override void RecordComponentAccess(RoslynEcsTranslator.IterationContext query, TypeHandle componentType, RoslynEcsTranslator.AccessMode mode)
        {
            if (IterationContext == query)
            {
                // if already in write mode, don't downgrade it to read mode
                if (m_WrittenComponents.TryGetValue(componentType, out var prevMode) && prevMode >= mode)
                    mode = prevMode;
                m_WrittenComponents[componentType] = mode;
            }

            // propagate max mode to parent (eg. write), not initial one (eg. read)
            Parent.RecordComponentAccess(query, componentType, mode);
        }

        public override string GetComponentVariableName(IIteratorStackModel query, TypeHandle componentVariableType)
        {
            // TODO forward to parent if necessary
            return query == IterationContext.Query
                ? IterationContext.GetComponentDataName(componentVariableType.Resolve(IterationContext.Stencil))
                : Parent.GetComponentVariableName(query, componentVariableType);
        }

        public override void AddStatement(StatementSyntax node)
        {
            m_UpdateStatements.Add(node);
        }

        public override ExpressionSyntax GetRandomSeed()
        {
            // (existingSeed) ^ (uint)QueryEntity.Index * 7907
            return BinaryExpression(
                SyntaxKind.ExclusiveOrExpression,
                Parent.GetRandomSeed(),
                BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    CastExpression(
                        PredefinedType(
                            Token(SyntaxKind.UIntKeyword)),
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            IdentifierName(EntityName),
                            IdentifierName(nameof(Entity.Index)))),
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(7907))));
        }

        public static ExpressionSyntax MakeInitEntityArray(RoslynEcsTranslator.IterationContext iterationContext)
        {
            return InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName(iterationContext.GroupName),
                    IdentifierName("ToEntityArray")))
                    .WithArgumentList(
                ArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(nameof(Allocator)),
                                IdentifierName(iterationContext.AllocatorType.ToString()))))))
                    .NormalizeWhitespace();
        }

        internal override void IncludeCoroutineComponent(RoslynEcsTranslator.IterationContext iterationContext,
            string coroutineComponentName)
        {
            // TODO Refactor needed : We should be able to call RecordComponentAccess to access the coroutineComponent
            m_CoroutineComponentName = coroutineComponentName;

            var rootContext = GetParent<RootContext>();
            rootContext.IncludeCoroutineComponent(iterationContext, coroutineComponentName);
        }

        LocalDeclarationStatementSyntax GetCoroutineComponentDeclaration(string loopIndexName)
        {
            string compArrayName = GetOrDeclareComponentArray(
                IterationContext,
                m_CoroutineComponentName,
                out var componentDataArrayInitialization,
                out var componentDataArrayDisposal);

            if (componentDataArrayInitialization != null)
                m_InitComponentDataArrays.Add(componentDataArrayInitialization);
            if (componentDataArrayDisposal != null)
                m_DisposeComponentDataArrays.Add(componentDataArrayDisposal);

            var initValue = ElementAccessExpression(IdentifierName(compArrayName))
                .WithArgumentList(
                BracketedArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            IdentifierName(loopIndexName)))));

            return RoslynBuilder.DeclareLocalVariable(
                (Type)null,
                CoroutineContext.BuildCoroutineParameterName(m_CoroutineComponentName),
                initValue,
                RoslynBuilder.VariableDeclarationType.InferredType);
        }
    }
}
