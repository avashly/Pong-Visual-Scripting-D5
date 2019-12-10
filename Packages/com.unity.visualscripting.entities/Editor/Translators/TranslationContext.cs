using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    public abstract class TranslationContext
    {
        protected const string k_RecorderWriterName = "recorder";
        public virtual IdentifierNameSyntax GetRecorderName() => Parent.GetRecorderName();
        public readonly TranslationContext Parent;

        public RoslynEcsTranslator.IterationContext IterationContext { get; protected set; }
        public string EntityName { get; protected set; }

        protected TranslationContext(TranslationContext parent)
        {
            Parent = parent;
        }

        public abstract TranslationContext PushContext(IIteratorStackModel query,
            RoslynEcsTranslator roslynEcsTranslator, UpdateMode mode, bool isCoroutine = false);
        protected abstract IEnumerable<StatementSyntax> OnPopContext();
        public abstract void AddStatement(StatementSyntax node);
        public abstract void AddEntityDeclaration(string variableName);

        public virtual void PrependStatement(StatementSyntax statement)
        {
            Parent.PrependStatement(statement);
        }

        public virtual void PrependUniqueStatement(string uniqueKey, StatementSyntax statement)
        {
            Parent.PrependUniqueStatement(uniqueKey, statement);
        }

        public virtual ExpressionSyntax GetRandomSeed()
        {
            return Parent.GetRandomSeed();
        }

        public void AddCriterionCondition(RoslynEcsTranslator translator, string entityName,
            IEnumerable<CriteriaModel> criteriaModels)
        {
            GetEntityManipulationTranslator().BuildCriteria(
                translator,
                this,
                IdentifierName(entityName),
                criteriaModels);
        }

        public virtual ExpressionSyntax GetSingletonVariable(IVariableDeclarationModel variable)
        {
            return Parent.GetSingletonVariable(variable);
        }

        internal virtual void RequestSingletonUpdate()
        {
            Parent.RequestSingletonUpdate();
        }

        public virtual string GetJobIndexParameterName()
        {
            return Parent.GetJobIndexParameterName();
        }

        public virtual string GetOrDeclareComponentQuery(RoslynEcsTranslator.IterationContext iterationContext)
        {
            return Parent.GetOrDeclareComponentQuery(iterationContext);
        }

        public virtual string GetOrDeclareComponentArray(RoslynEcsTranslator.IterationContext ctx, string componentTypeName, out LocalDeclarationStatementSyntax arrayInitialization, out StatementSyntax arrayDisposal)
        {
            return Parent.GetOrDeclareComponentArray(ctx, componentTypeName, out arrayInitialization, out arrayDisposal);
        }

        public abstract string GetComponentVariableName(IIteratorStackModel query, TypeHandle componentVariableType1);

        public virtual IEntityManipulationTranslator GetEntityManipulationTranslator()
        {
            return Parent.GetEntityManipulationTranslator();
        }

        public virtual IEnumerable<ComponentDefinition> GetComponentDefinitions()
        {
            return IterationContext.FlattenedComponentDefinitions();
        }

        public virtual IdentifierNameSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            return Parent.GetOrDeclareCommandBuffer(isConcurrent);
        }

        public virtual string MakeUniqueName(string groupName) => Parent.MakeUniqueName(groupName);

        public virtual Allocator AllocatorType => Parent.AllocatorType;
        public virtual RoslynEcsTranslator.TranslationOptions TranslationOptions => Parent.TranslationOptions;

        public IEnumerable<StatementSyntax> PopContext()
        {
            Assert.IsNotNull(Parent, "cannot pop root context");
            return OnPopContext();
        }

        public abstract void RecordComponentAccess(RoslynEcsTranslator.IterationContext query, TypeHandle componentType, RoslynEcsTranslator.AccessMode mode);

        public virtual void RecordEntityAccess(IVariableDeclarationModel model) {}

        public virtual ExpressionSyntax GetCachedValue(string key, ExpressionSyntax value, TypeHandle modelReturnType, params IdentifierNameSyntax[] attributes)
        {
            return Parent.GetCachedValue(key, value, modelReturnType);
        }

        protected virtual StatementSyntax GetOrDeclareEntityArray(RoslynEcsTranslator.IterationContext iterationContext, out StatementSyntax arrayDisposal)
        {
            return Parent.GetOrDeclareEntityArray(iterationContext, out arrayDisposal);
        }

        protected virtual string IncludeTrackingSystemStateComponent(ComponentQueryDeclarationModel query, bool trackProcessed)
        {
            return Parent.IncludeTrackingSystemStateComponent(query, trackProcessed);
        }

        internal virtual void IncludeCoroutineComponent(RoslynEcsTranslator.IterationContext iterationContext,
            string coroutineComponentName)
        {
            Parent.IncludeCoroutineComponent(iterationContext, coroutineComponentName);
        }

        public virtual void DeclareComponent<T>(string componentName,
            IEnumerable<MemberDeclarationSyntax> members = null)
        {
            Parent.DeclareComponent<T>(componentName, members);
        }

        public virtual void DeclareSystemMethod(MethodDeclarationSyntax methodDeclarationSyntax)
        {
            Parent.DeclareSystemMethod(methodDeclarationSyntax);
        }

        public virtual bool GetEventSystem(RoslynEcsTranslator.IterationContext iterationContext, Type eventType)
        {
            return Parent.GetEventSystem(iterationContext, eventType);
        }

        public virtual IdentifierNameSyntax GetEventBufferWriter(RoslynEcsTranslator.IterationContext iterationContext, ExpressionSyntax entity, Type eventType, out StatementSyntax bufferInitialization)
        {
            return Parent.GetEventBufferWriter(iterationContext, entity, eventType, out bufferInitialization);
        }

        protected virtual StatementSyntax AddMissingEventBuffers(RoslynEcsTranslator.IterationContext iterationContext, StatementSyntax onPopContext) => Parent.AddMissingEventBuffers(iterationContext, onPopContext);

        public virtual void DeclareManager<T>(string name) where T : ComponentSystemBase
        {
            Parent.DeclareManager<T>(name);
        }

        public virtual string GetSystemClassName()
        {
            return Parent.GetSystemClassName();
        }

        protected T GetParent<T>() where T : TranslationContext
        {
            var parent = Parent;
            while (parent != null)
            {
                if (parent.GetType() == typeof(T))
                    return (T)parent;

                parent = parent.Parent;
            }
            return null;
        }
    }
}
