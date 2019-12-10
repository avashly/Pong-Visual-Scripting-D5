using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Redux.Actions;
using Unity.Entities;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.Plugins;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VisualScripting;
using VisualScripting.Entities.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CompilationOptions = UnityEngine.VisualScripting.CompilationOptions;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class CoroutineNodeModel : LoopNodeModel, INodeModelProgress
    {
        public Dictionary<ParameterInfo, RoslynEcsTranslator.AccessMode> ComponentParameters;
        public int NextStateIndex;

        [SerializeField]
        TypeHandle m_CoroutineType;

        List<ParameterInfo> m_RegularParameters;
        Dictionary<ParameterInfo, CoroutineSpecialVariableAttribute> m_SpecialParameters;
        string m_InsertLoopNodeTitle;

        public TypeHandle CoroutineType
        {
            get => m_CoroutineType;
            set
            {
                Assert.IsTrue(typeof(ICoroutine).IsAssignableFrom(value.Resolve(Stencil)),
                    $"The type {value} does not implement ICoroutine.");
                m_CoroutineType = value;
            }
        }

        public MethodInfo MethodInfo { get; private set; }
        public string VariableName => $"{CoroutineType.Name(Stencil)}";

        public override string InsertLoopNodeTitle => m_InsertLoopNodeTitle;

        public override bool IsInsertLoop => true;
        public override LoopConnectionType LoopConnectionType => LoopConnectionType.LoopStack;
        public override Type MatchingStackType => typeof(CoroutineStackModel);
        public Dictionary<string, FieldDeclarationSyntax> Fields { get; private set; }
        public IReadOnlyDictionary<ParameterInfo, CoroutineSpecialVariableAttribute> SpecialParameters => m_SpecialParameters;

        public IPortModel GetFieldPort(FieldInfo field)
        {
            InputsById.TryGetValue(field.Name, out var portModel);
            return portModel;
        }

        public IPortModel GetParameterPort(ParameterInfo parameter)
        {
            InputsById.TryGetValue(parameter.Name, out var portModel);
            return portModel;
        }

        protected override void OnDefineNode()
        {
            if (Fields == null)
                Fields = new Dictionary<string, FieldDeclarationSyntax>();
            base.OnDefineNode();

            var type = CoroutineType.Resolve(Stencil);

            VisualScriptingFriendlyNameAttribute friendlyNameAttribute = null;
            if ((friendlyNameAttribute = type.GetCustomAttribute<VisualScriptingFriendlyNameAttribute>()) != null)
                m_InsertLoopNodeTitle = friendlyNameAttribute.FriendlyName;
            else
                m_InsertLoopNodeTitle = type.Name;

            MethodInfo = type.GetMethod("MoveNext");
            AddField(type, VariableName, AccessibilityFlags.Public);

            foreach (var field in type.GetFields())
                AddDataInput(field.Name, field.FieldType.GenerateTypeHandle(Stencil));

            var parameters = MethodInfo.GetParameters();
            m_RegularParameters = new List<ParameterInfo>(parameters.Length);
            m_SpecialParameters = new Dictionary<ParameterInfo, CoroutineSpecialVariableAttribute>(parameters.Length);
            ComponentParameters = new Dictionary<ParameterInfo, RoslynEcsTranslator.AccessMode>(parameters.Length);

            foreach (var parameter in parameters)
            {
                var attr = System.Attribute.GetCustomAttribute(parameter, typeof(CoroutineSpecialVariableAttribute));
                if (attr == null)
                    m_RegularParameters.Add(parameter);
                else
                    m_SpecialParameters.Add(parameter, (CoroutineSpecialVariableAttribute)attr);
            }

            foreach (var parameter in m_RegularParameters)
            {
                Type paramType = parameter.ParameterType;
                if (typeof(IComponentData).IsAssignableFrom(paramType))
                {
                    ComponentParameters.Add(parameter, RoslynEcsTranslator.AccessMode.Read);
                }
                Type elemType = paramType.GetElementType();
                if (elemType != null)
                {
                    if (!paramType.IsByRef || !typeof(IComponentData).IsAssignableFrom(elemType))
                    {
                        Debug.LogError($"Unsupported parameter type in {MethodInfo.Name}: {paramType.Name} {parameter.Name}");
                        break;
                    }
                    paramType = elemType;
                    ComponentParameters.Add(parameter, RoslynEcsTranslator.AccessMode.Write);
                }

                AddDataInput(parameter.Name, paramType.GenerateTypeHandle(Stencil));
            }
        }

        void AddField(Type variableType, string variableName, AccessibilityFlags variableAccessibility)
        {
            if (!Fields.ContainsKey(variableName))
                Fields.Add(variableName, RoslynBuilder.DeclareField(variableType, variableName, variableAccessibility));
        }
    }

    [GraphtoolsExtensionMethods]
    public static class CoroutineTranslator
    {
        public static IEnumerable<SyntaxNode> BuildCoroutine(this RoslynEcsTranslator translator,
            CoroutineNodeModel model, IPortModel portModel)
        {
            var nodeName = model.CoroutineType.Name(translator.Stencil);
            if (!(translator.context is CoroutineContext coroutineContext))
            {
                translator.AddError(model, $"{nodeName} node is not allowed in a static function");
                yield break;
            }

            var componentTranslations = new Dictionary<ParameterInfo, ArgumentSyntax>(model.ComponentParameters.Count);
            foreach (var parameter in model.ComponentParameters)
            {
                var p = parameter.Key;
                var accessMode = parameter.Value;

                var compType = p.ParameterType.GetElementType();
                var compTypeHandle = compType.GenerateTypeHandle(translator.Stencil);
                var query = translator.context.IterationContext.Query.ComponentQueryDeclarationModel;
                if (!query.Components.Any(q => q.Component.TypeHandle == compTypeHandle))
                {
                    var queryCopy = query;
                    string componentName = compTypeHandle.GetMetadata(translator.Stencil).FriendlyName;
                    translator.AddError(model,
                        $"A component of type {componentName} is required, which the query {query.Name} doesn't specify",
                        new CompilerQuickFix($"Add {componentName} to the query",
                            s => s.Dispatch(new AddComponentToQueryAction(
                                queryCopy,
                                compTypeHandle,
                                ComponentDefinitionFlags.None))));
                    yield break;
                }
                translator.GetComponentFromEntityOrComponentPort(model, model.GetParameterPort(p), out _, out var value, accessMode);
                var translation = Argument(value);
                if (accessMode == RoslynEcsTranslator.AccessMode.Write)
                    translation = translation.WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword));
                componentTranslations.Add(p, translation);
            }

            var moveNextParams = model.MethodInfo.GetParameters().Select(p =>
            {
                if (model.SpecialParameters.TryGetValue(p, out var attr))
                    return Argument(coroutineContext.TranslateCustomParameter(p, attr));

                return componentTranslations.TryGetValue(p, out var argumentSyntax)
                ? argumentSyntax
                : Argument((ExpressionSyntax)translator.BuildPort(model.GetParameterPort(p)).Single());
            }).ToList();

            var conditionStatement = RoslynBuilder.MethodInvocation(
                model.MethodInfo.Name,
                GetVariableAccess(coroutineContext, model.VariableName),
                moveNextParams,
                null);

            var gotoNextStateBlock = Block(coroutineContext.BuildGoToState(model.NextStateIndex));
            var statementBlock = Block();

            if (model.OutputPort.ConnectionPortModels.FirstOrDefault()?.NodeModel is LoopStackModel loopStack
                && loopStack.NodeModels.Count > 0)
            {
                statementBlock = RoslynTranslatorExtensions.BuildLocalDeclarations(translator, loopStack)
                    .Aggregate(statementBlock, (current, localDeclaration) => current.AddStatements(localDeclaration));

                translator.BuildStack(loopStack, ref statementBlock, StackExitStrategy.Continue);

                foreach (var instrumentedStatement in coroutineContext.Instrument(
                    IfStatement(conditionStatement, statementBlock)
                        .WithElse(ElseClause(gotoNextStateBlock)), model, translator.Options))
                {
                    yield return instrumentedStatement;
                }
            }

            if (!statementBlock.Statements.Any())
            {
                foreach (var instrumentedStatement in coroutineContext.Instrument(
                    IfStatement(
                        PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, conditionStatement),
                        coroutineContext.BuildGoToState(model.NextStateIndex)), model, translator.Options))
                {
                    yield return instrumentedStatement;
                }
            }

            yield return ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression));
        }

        public static IEnumerable<StatementSyntax> BuildInitState(CoroutineNodeModel model,
            RoslynEcsTranslator translator)
        {
            if (model.MethodInfo?.DeclaringType == null || !(translator.context is CoroutineContext coroutineContext))
                yield break;

            var coroutineNodeVariable = GetVariableAccess(coroutineContext, model.VariableName);

            foreach (var field in model.MethodInfo.DeclaringType.GetFields())
            {
                yield return ExpressionStatement(
                    RoslynBuilder.Assignment(
                        MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            coroutineNodeVariable,
                            IdentifierName(field.Name)),
                        translator.BuildPort(model.GetFieldPort(field)).Single()
                    )
                );
            }

            if ((translator.Options & CompilationOptions.Tracing) != 0)
            {
                yield return InstrumentForInEditorDebugging.BuildLastCallFrameExpression(
                    0, model.Guid, coroutineContext.GetRecorderName(), coroutineNodeVariable);
            }
        }

        public static string MakeExcludeCoroutineQueryName(RoslynEcsTranslator.IterationContext iterationContext)
        {
            return $"{iterationContext.GroupName}ExcludeCoroutine";
        }

        static MemberAccessExpressionSyntax GetVariableAccess(CoroutineContext context, string variableName)
        {
            return MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(context.CoroutineParameterName),
                IdentifierName(variableName));
        }
    }
}
