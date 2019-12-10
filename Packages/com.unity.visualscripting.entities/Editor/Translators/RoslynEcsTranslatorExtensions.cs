using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace UnityEditor.VisualScripting.Model.Translators
{
    [GraphtoolsExtensionMethods]
    static class RoslynEcsTranslatorExtensions
    {
        public static IEnumerable<SyntaxNode> BuildThisNode(this RoslynEcsTranslator translator, ThisNodeModel model, IPortModel portModel)
        {
            yield return ThisExpression();
        }

        public static IEnumerable<SyntaxNode> BuildGetPropertyNode(this RoslynEcsTranslator translator, GetPropertyGroupNodeModel model, IPortModel portModel)
        {
            var instancePort = model.InstancePort;
            var input = !instancePort.Connected ? ThisExpression() : translator.BuildPort(instancePort).SingleOrDefault();

            if (input == null)
                yield break;

            var member = model.Members.FirstOrDefault(m => m.GetId() == portModel.UniqueId);
            if (member.Path == null || member.Path.Count == 0)
                yield break;

            var access = RoslynBuilder.MemberReference(input, member.Path[0]);
            for (int i = 1; i < member.Path.Count; i++)
            {
                access = RoslynBuilder.MemberReference(access, member.Path[i]);
            }

            yield return access;
        }

        public static IEnumerable<SyntaxNode> BuildBinaryOperator(this RoslynEcsTranslator translator, BinaryOperatorNodeModel model, IPortModel portModel)
        {
            if (model.kind == BinaryOperatorKind.Equals)
                yield return InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        PredefinedType(Token(SyntaxKind.ObjectKeyword)),
                        IdentifierName("Equals")))
                        .WithArgumentList(
                    ArgumentList(
                        SeparatedList(new[]
                        {
                            Argument((ExpressionSyntax)translator.BuildPort(model.InputPortA).SingleOrDefault()),
                            Argument((ExpressionSyntax)translator.BuildPort(model.InputPortB).SingleOrDefault())
                        })));

            else
                yield return RoslynBuilder.BinaryOperator(model.kind,
                    translator.BuildPort(model.InputPortA).SingleOrDefault(),
                    translator.BuildPort(model.InputPortB).SingleOrDefault());
        }

        public static IEnumerable<SyntaxNode> BuildReturn(this RoslynEcsTranslator translator,
            ReturnNodeModel returnModel, IPortModel portModel)
        {
            if (translator.context is CoroutineContext coroutineContext)
            {
                if (returnModel.ParentStackModel.OwningFunctionModel is CoroutineStackModel)
                {
                    yield return ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression));
                }
                else
                {
                    coroutineContext.SkipStateBuilding = true;
                    yield return ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression));
                }
            }
            else
            {
                if (returnModel.InputPort == null)
                {
                    yield return ReturnStatement();
                }
                else
                {
                    yield return ReturnStatement(
                        translator.BuildPort(returnModel.InputPort).FirstOrDefault() as ExpressionSyntax);
                }
            }
        }

        public static IEnumerable<SyntaxNode> BuildSetVariable(this RoslynEcsTranslator translator, SetVariableNodeModel statement, IPortModel portModel)
        {
            if (!statement.InstancePort.Connected)
                yield break;
            if (statement.InstancePort.ConnectionPortModels.FirstOrDefault()?.NodeModel is VariableNodeModel variableNode
                && variableNode.DeclarationModel.VariableType == VariableType.GraphVariable)
                translator.context.RequestSingletonUpdate();

            var decl = translator.BuildPort(statement.InstancePort, RoslynTranslator.PortSemantic.Write).SingleOrDefault();
            var value = translator.BuildPort(statement.ValuePort).SingleOrDefault();
            yield return decl == null || value == null ? null : RoslynBuilder.Assignment(decl, value);
        }

        class ReplaceExpressionPorts : CSharpSyntaxRewriter
        {
            readonly RoslynEcsTranslator m_Translator;
            readonly InlineExpressionNodeModel m_Model;

            public ReplaceExpressionPorts(RoslynEcsTranslator translator, InlineExpressionNodeModel model)
            {
                m_Translator = translator;
                m_Model = model;
            }

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                if (node.Expression is IdentifierNameSyntax idns)
                    node = node.WithExpression(MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("math"),
                        idns));
                return base.VisitInvocationExpression(node);
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                if (m_Model.InputsById.TryGetValue(node.Identifier.Text, out var portModel))
                    return m_Translator.BuildPort(portModel).FirstOrDefault();
                return base.VisitIdentifierName(node);
            }
        }

        public static IEnumerable<SyntaxNode> BuildInlineExpression(this RoslynEcsTranslator translator, InlineExpressionNodeModel v, IPortModel portModel)
        {
            var expressionCode = "var ___exp = (" + v.Expression + ")";
            var syntaxTree = CSharpSyntaxTree.ParseText(expressionCode);
            var exp = syntaxTree.GetRoot().DescendantNodesAndSelf().OfType<ParenthesizedExpressionSyntax>().FirstOrDefault();

            yield return new ReplaceExpressionPorts(translator, v).Visit(exp);
        }

        public static IEnumerable<SyntaxNode> BuildVariable(this RoslynEcsTranslator translator, IVariableModel v, IPortModel portModel)
        {
            if (v is IConstantNodeModel model)
            {
                if (model.ObjectValue != null)
                    yield return translator.Constant(model.ObjectValue, translator.Stencil, model.Type);

                yield break;
            }

            if (translator.InMacro.Count > 0 && v.DeclarationModel.VariableType == VariableType.GraphVariable && v.DeclarationModel.Modifiers == ModifierFlags.ReadOnly)
            {
                MacroRefNodeModel oldValue = translator.InMacro.Pop();

                var syntaxNodes = translator.BuildPort(oldValue.InputsById[v.DeclarationModel.VariableName]);
                translator.InMacro.Push(oldValue);
                foreach (var syntaxNode in syntaxNodes)
                    yield return syntaxNode;
                yield break;
            }

            switch (v.DeclarationModel.VariableType)
            {
                case VariableType.GraphVariable:
                    yield return translator.context.GetSingletonVariable(v.DeclarationModel);
                    break;

                case VariableType.FunctionVariable:
                case VariableType.ComponentQueryField:
                    yield return RoslynBuilder.LocalVariableReference(v.DeclarationModel.VariableName);
                    break;

                case VariableType.FunctionParameter:
                    var variableDeclarationModel = v.DeclarationModel;
                    if (variableDeclarationModel.IsGeneratedEcsComponent(out var groupDeclaration))
                    {
                        var relevantContext = translator.FindContext(groupDeclaration);
                        if (relevantContext == null)
                        {
                            var variableName = v.DeclarationModel.Name;
                            translator.AddError(v, $"Could not find a matching component query for variable \"{variableName}\"");
                            throw new InvalidOperationException("No matching translation context for Declaration");
                        }
                        translator.context.RecordComponentAccess(relevantContext, v.DeclarationModel.DataType, translator.IsRecordingComponentAccesses);
                        yield return RoslynBuilder.ArgumentReference(translator.context.GetComponentVariableName(groupDeclaration, variableDeclarationModel.DataType));
                    }
                    else
                    {
                        if (variableDeclarationModel.IsGeneratedEntity())
                            translator.context.RecordEntityAccess(variableDeclarationModel);
                        yield return RoslynBuilder.ArgumentReference(variableDeclarationModel.VariableName);
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        internal static bool IsGeneratedEcsComponent(this IVariableDeclarationModel variableDeclarationModel, out IIteratorStackModel iteratorStackModel)
        {
            var isEntity = variableDeclarationModel.DataType.Equals(typeof(Entity).GenerateTypeHandle(variableDeclarationModel.GraphModel.Stencil));
            iteratorStackModel = variableDeclarationModel is LoopVariableDeclarationModel loopDecl ? loopDecl.GetComponentQueryDeclarationModel() : null;
            return !isEntity && iteratorStackModel != null && (((VariableDeclarationModel)variableDeclarationModel).variableFlags & VariableFlags.Generated) != 0;
        }

        internal static bool IsGeneratedEntity(this IVariableDeclarationModel variableDeclarationModel)
        {
            var isEntity = variableDeclarationModel.DataType.Equals(typeof(Entity).GenerateTypeHandle(variableDeclarationModel.GraphModel.Stencil));
            return isEntity && (((VariableDeclarationModel)variableDeclarationModel).variableFlags & VariableFlags.Generated) != 0;
        }

        public static SyntaxNode GroupLocalVariableReference(string name)
        {
            return ElementAccessExpression(
                IdentifierName(name))
                    .WithArgumentList(
                BracketedArgumentList(
                    SingletonSeparatedList(
                        Argument(
                            IdentifierName("i")))));
        }

        public static IEnumerable<SyntaxNode> BuildIfCondition(this RoslynEcsTranslator translator,
            IfConditionNodeModel nodeModel, IPortModel portModel)
        {
            // TODO: de-duplicate code after if stacks
            var firstThenStack = RoslynTranslator.GetConnectedStack(nodeModel, 0);
            var firstElseStack = RoslynTranslator.GetConnectedStack(nodeModel, 1);

            // this enables more elegant code generation with no duplication
            // find first stack reachable from both then/else stacks
            var endStack = RoslynTranslator.FindCommonDescendant(nodeModel.ParentStackModel, firstThenStack, firstElseStack);
            if (endStack != null)
            {
//                Debug.Log($"If in stack {statement.parentStackModel} Common descendant: {endStack}");
                // building the branches will stop at the common descendant
                translator.EndStack = endStack;
            }

            // IfConditionNode is built by the CoroutineContext in this case
            if (translator.context is CoroutineContext
                && nodeModel.ParentStackModel.OwningFunctionModel.ContainsCoroutine())
                yield break;

            // ie. follow outputs, find all stacks with multiple inputs, compare them until finding the common one if it exists
            // BuildStack should take an abort stack parameter, returning when recursing on it
            // the parent buildStack call will then continue on this end stack


            var origBuiltStacks = translator.BuiltStacks;
            translator.BuiltStacks = new HashSet<IStackModel>(origBuiltStacks);

            var thenBlock = Block();
            if (endStack != firstThenStack)
                translator.BuildStack(firstThenStack, ref thenBlock, StackExitStrategy.Inherit);

            var partialStacks = translator.BuiltStacks;
            translator.BuiltStacks = new HashSet<IStackModel>(origBuiltStacks);

            var elseBlock = Block();
            if (endStack != firstElseStack)
                translator.BuildStack(firstElseStack, ref elseBlock, StackExitStrategy.Inherit);

            translator.BuiltStacks.UnionWith(partialStacks);

            var ifNode = RoslynBuilder.IfStatement(
                translator.BuildPort(nodeModel.IfPort).SingleOrDefault(),
                thenBlock,
                elseBlock);

            yield return ifNode;
        }

        public static IEnumerable<SyntaxNode> BuildFunctionRefCall(this RoslynEcsTranslator translator,
            FunctionRefCallNodeModel call, IPortModel portModel)
        {
            if (!call.Function)
                yield break;

            ExpressionSyntax instance = translator.BuildArgumentList(call.InputsById.Values, out var argumentList);
            if (!call.Function.IsInstanceMethod)
                instance = IdentifierName(((VSGraphModel)call.Function.GraphModel).TypeName);

            var invocationExpressionSyntax =
                instance == null ||
                instance is LiteralExpressionSyntax && instance.IsKind(SyntaxKind.NullLiteralExpression)
                ? InvocationExpression(IdentifierName(call.Function.CodeTitle))
                : InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        instance,
                        IdentifierName(call.Function.CodeTitle)));
            invocationExpressionSyntax = invocationExpressionSyntax.WithArgumentList(
                ArgumentList(SeparatedList(argumentList)));

            if (portModel == null)
                yield return ExpressionStatement(invocationExpressionSyntax).NormalizeWhitespace();
            else
                yield return invocationExpressionSyntax.NormalizeWhitespace();
        }

        public static IEnumerable<SyntaxNode> BuildFunctionCall(this RoslynEcsTranslator translator, FunctionCallNodeModel call, IPortModel portModel)
        {
            if (call.MethodInfo == null)
                yield break;

            var instance = translator.BuildArgumentList(call.InputsById.Values, out var argumentList);

            var typeArgumentList = new List<TypeSyntax>();
            if (call.MethodInfo.IsGenericMethod)
            {
                typeArgumentList.AddRange(call.TypeArguments.Select(t => IdentifierName(t.GetMetadata(translator.Stencil).Name)));
            }

            TypeArgumentListSyntax typeArgList = null;
            if (typeArgumentList.Any())
                typeArgList = TypeArgumentList(SingletonSeparatedList(typeArgumentList.First()));

            var method = RoslynBuilder.MethodInvocation(call.MethodInfo.Name, call.MethodInfo, instance, argumentList, typeArgList);

            if (method is ExpressionSyntax exp &&
                call.MethodInfo is MethodInfo mi &&
                mi.ReturnType != typeof(void) &&
                call.MethodInfo.DeclaringType.Namespace != null &&
                call.MethodInfo.DeclaringType.Namespace.StartsWith("UnityEngine"))
            {
                var key = call.DeclaringType.Name(translator.Stencil).ToPascalCase() + call.Title.ToPascalCase();
                yield return translator.context.GetCachedValue(key, exp, mi.ReturnType.GenerateTypeHandle(translator.Stencil));
            }
            else
            {
                yield return method;
            }
        }

        [CanBeNull]
        public static ExpressionSyntax BuildArgumentList(this RoslynEcsTranslator translator, IEnumerable<IPortModel> parameterPorts, out List<ArgumentSyntax> argumentList)
        {
            ExpressionSyntax instance = null;
            argumentList = new List<ArgumentSyntax>();
            foreach (IPortModel port in parameterPorts)
            {
                if (port.PortType == PortType.Instance)
                {
                    instance = (ExpressionSyntax)translator.BuildPort(port).SingleOrDefault();
                    continue;
                }

                var syntaxNode = translator.BuildPort(port).SingleOrDefault();
                if (syntaxNode != null)
                {
                    if (!(syntaxNode is ArgumentSyntax argumentNode))
                        argumentNode = Argument(syntaxNode as ExpressionSyntax);
                    argumentList.Add(argumentNode);
                }
            }

            return instance;
        }

        public static IEnumerable<SyntaxNode> BuildSetPropertyNode(this RoslynEcsTranslator translator, SetPropertyGroupNodeModel model, IPortModel portModel)
        {
            IPortModel instancePort = model.InstancePort;
            if (instancePort?.PortType != PortType.Instance)
                throw new InvalidOperationException();

            // if building the instance port builds a component variable, it implies a write access to the component
            SyntaxNode leftHand;
            if (!instancePort.Connected)
                leftHand = ThisExpression();
            else
                using (translator.RecordComponentAccess(RoslynEcsTranslator.AccessMode.Write))
                    leftHand = translator.BuildPort(instancePort, RoslynTranslator.PortSemantic.Write).SingleOrDefault();

            foreach (var member in model.Members)
            {
                string id = member.GetId();
                if (model.InputsById.TryGetValue(id, out var inputPort))
                {
                    SyntaxNode rightHandExpression = translator.BuildPort(inputPort).SingleOrDefault();
                    if (rightHandExpression == null)
                        continue;
                    MemberAccessExpressionSyntax access = RoslynBuilder.MemberReference(leftHand, member.Path[0]);
                    for (int i = 1; i < member.Path.Count; i++)
                    {
                        access = RoslynBuilder.MemberReference(access, member.Path[i]);
                    }

                    yield return AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, access, rightHandExpression as ExpressionSyntax);
                }
            }
        }

        public static IEnumerable<SyntaxNode> BuildStaticConstantNode(this RoslynEcsTranslator translator, SystemConstantNodeModel model, IPortModel portModel)
        {
            ITypeMetadata type = model.DeclaringType.GetMetadata(translator.Stencil);
            QualifiedNameSyntax buildStaticConstantNode = RoslynEcsBuilder.StaticConstant(type, model.Identifier);

            if (type.Namespace.StartsWith("UnityEngine"))
            {
                string key = type.Name.ToPascalCase() + model.Identifier.ToPascalCase();
                yield return translator.context.GetCachedValue(key, buildStaticConstantNode, model.ReturnType);
            }
            else
            {
                yield return buildStaticConstantNode;
            }
        }

        public static IEnumerable<SyntaxNode> BuildGetSingletonNode(this RoslynEcsTranslator translator, GetSingletonNodeModel node, IPortModel portModel)
        {
            var method = GetSingletonTranslator.Build(translator, node).First() as ExpressionSyntax;

            var key = $"{nameof(EntityQuery.GetSingleton).ToPascalCase()}{node.ComponentType.Name(translator.Stencil)}";
            yield return translator.context.GetCachedValue(key, method, node.ComponentType);
        }

        public static IEnumerable<SyntaxNode> BuildMethod(this RoslynEcsTranslator roslynTranslator, KeyDownEventModel stack, IPortModel portModel)
        {
            BlockSyntax block = Block();
            roslynTranslator.BuildStack(stack, ref block);

            string methodName;
            switch (stack.mode)
            {
                case KeyDownEventModel.EventMode.Pressed:
                    methodName = nameof(Input.GetKeyDown);
                    break;
                case KeyDownEventModel.EventMode.Released:
                    methodName = nameof(Input.GetKeyUp);
                    break;
                default:
                    methodName = nameof(Input.GetKey);
                    break;
            }

            var conditionExpression = (ExpressionSyntax)roslynTranslator.BuildPort(stack.KeyPort).Single();

            IfStatementSyntax keydownCheck = IfStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(nameof(Input)),
                        IdentifierName(methodName)))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(conditionExpression)))),
                block)
                    .NormalizeWhitespace();
            roslynTranslator.AddEventRegistration(keydownCheck);
            yield break;
        }

        public static IEnumerable<SyntaxNode> BuildLoop(this RoslynEcsTranslator translator, LoopNodeModel statement, IPortModel portModel)
        {
            return RoslynTranslatorExtensions.BuildLoop(translator, statement, portModel);
        }

        public static IEnumerable<SyntaxNode> BuildForEach(this RoslynEcsTranslator translator, ForEachHeaderModel forEachHeaderModelStatement,
            IPortModel portModel)
        {
            return RoslynTranslatorExtensions.BuildForEach(translator, forEachHeaderModelStatement, portModel);
        }

        public static IEnumerable<SyntaxNode> BuildForAllEntities(this RoslynEcsTranslator translator, ForAllEntitiesStackModel forEachHeaderModelStatement,
            IPortModel portModel)
        {
            IPortModel loopExecutionInputPortModel = forEachHeaderModelStatement.InputPort;
            IPortModel insertLoopPortModel = loopExecutionInputPortModel?.ConnectionPortModels?.FirstOrDefault();
            var insertLoopNodeModel = insertLoopPortModel?.NodeModel as ForAllEntitiesNodeModel;
            var collectionInputPortModel = insertLoopNodeModel?.InputPort;
            var connectedConnection = collectionInputPortModel?.ConnectionPortModels.FirstOrDefault();
            if (connectedConnection?.NodeModel is VariableNodeModel varNode && varNode.DeclarationModel is ComponentQueryDeclarationModel)
            {
                translator.PushContext(forEachHeaderModelStatement, UpdateMode.OnUpdate);
                translator.context.AddEntityDeclaration(forEachHeaderModelStatement.ItemVariableDeclarationModel.Name);

                var localDeclarationNodes = RoslynTranslatorExtensions.BuildLocalDeclarations(translator, forEachHeaderModelStatement);
                var block = Block(localDeclarationNodes);
                // TODO StackExitStrategy from iterationContext
                translator.BuildStack(forEachHeaderModelStatement, ref block, StackExitStrategy.Continue);
                foreach (var stmt in block.Statements)
                    translator.context.AddStatement(stmt);

                return translator.PopContext();
            }

            return Enumerable.Empty<SyntaxNode>();
        }

        internal static bool ShouldGenerateComponentAccess(TypeHandle component, bool checkZeroSizedToo, out Type componentType, Stencil translatorStencil, out bool isShared, out bool isGameObjectComponent)
        {
            componentType = component.Resolve(translatorStencil);
            isShared = false;
            isGameObjectComponent = false;
            if (!typeof(IComponentData).IsAssignableFrom(componentType))
            {
                if (typeof(ISharedComponentData).IsAssignableFrom(componentType))
                {
                    isShared = true;
                    return true;
                }

                if (EcsStencil.IsValidGameObjectComponentType(componentType))
                {
                    isGameObjectComponent = true;
                    return true;
                }
                return false;
            }

            if (checkZeroSizedToo)
            {
                // skip tag components
                int typeIndex = TypeManager.GetTypeIndex(componentType);
                ComponentType ecsComponentType = ComponentType.FromTypeIndex(typeIndex);
                if (ecsComponentType.IsZeroSized)
                    return false;
            }

            return true;
        }

        public static ForStatementSyntax ComponentQueryForLoop(BlockSyntax forBlock, string loopIndexName, string entityArrayName)
        {
            return ForStatement(
                forBlock)
                    .WithDeclaration(
                VariableDeclaration(
                    PredefinedType(
                        Token(SyntaxKind.IntKeyword)))
                    .WithVariables(
                    SingletonSeparatedList(
                        VariableDeclarator(
                            Identifier(loopIndexName))
                            .WithInitializer(
                            EqualsValueClause(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(0)))))))
                    .WithCondition(
                        BinaryExpression(
                            SyntaxKind.LessThanExpression,
                            IdentifierName(loopIndexName),
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(entityArrayName),
                                IdentifierName(nameof(NativeArray<Entity>.Length)))))
                    .WithIncrementors(
                        SingletonSeparatedList<ExpressionSyntax>(
                            PostfixUnaryExpression(
                                SyntaxKind.PostIncrementExpression,
                                IdentifierName(loopIndexName))))
                    .NormalizeWhitespace();
        }

        public static IEnumerable<SyntaxNode> BuildWhile(this RoslynEcsTranslator translator, WhileHeaderModel whileHeaderModel,
            IPortModel portModel)
        {
            return RoslynTranslatorExtensions.BuildWhile(translator, whileHeaderModel, portModel);
        }

        public static IEnumerable<SyntaxNode> BuildMethod(this RoslynEcsTranslator roslynTranslator, IFunctionModel stack, IPortModel portModel)
        {
            const AccessibilityFlags accessibility = AccessibilityFlags.Public | AccessibilityFlags.Static;

            var generatedName = roslynTranslator.MakeUniqueName(stack.CodeTitle);

            var methodSyntaxNode = RoslynBuilder.DeclareMethod(
                generatedName, accessibility, stack.ReturnType.Resolve(roslynTranslator.Stencil));
            var localDeclarationNodes = RoslynTranslatorExtensions.BuildLocalDeclarations(roslynTranslator, stack);
            var argumentNodes = RoslynTranslatorExtensions.BuildArguments(roslynTranslator.Stencil, stack);

            methodSyntaxNode = methodSyntaxNode.WithParameterList(ParameterList(
                SeparatedList(argumentNodes.ToArray())));
            methodSyntaxNode = methodSyntaxNode.WithBody(Block(localDeclarationNodes.ToArray()));

            BlockSyntax stackBlock = Block();
            roslynTranslator.BuildStack(stack, ref stackBlock);
            foreach (var statement in stackBlock.Statements)
            {
                methodSyntaxNode = methodSyntaxNode.AddBodyStatements(statement);
            }

            yield return methodSyntaxNode;
        }

        public static IEnumerable<SyntaxNode> BuildGetInput(this RoslynEcsTranslator translator, BaseInputNodeModel model, IPortModel portModel)
        {
            var call = model.BuildCall(translator, portModel, out var inputName, out var methodName);
            yield return translator.context.GetCachedValue($"{methodName.ToPascalCase()}{inputName.ToString().ToPascalCase()}", call, portModel.DataType);
        }

        public static ExpressionSyntax BuildComponentFromInput(
            this RoslynEcsTranslator translator,
            Type componentType,
            IReadOnlyList<IPortModel> inputs)
        {
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var componentIdentifier = TypeSystem.BuildTypeSyntax(componentType);
            var assignments = new List<SyntaxNodeOrToken>();

            for (var i = 0; i < inputs.Count; ++i)
            {
                var value = translator.BuildPort(inputs[i]).SingleOrDefault();
                assignments.Add(RoslynBuilder.Assignment(IdentifierName(inputs[i].Name), value));

                if (i < inputs.Count - 1)
                    assignments.Add(Token(SyntaxKind.CommaToken));
            }

            if (HighLevelNodeModelHelpers.HasSinglePredefinedFieldType(fields))
            {
                var fieldIdentifier = TypeSystem.BuildTypeSyntax(fields[0].FieldType);
                return ObjectCreationExpression(componentIdentifier)
                    .WithInitializer(
                    InitializerExpression(
                        SyntaxKind.ObjectInitializerExpression,
                        SingletonSeparatedList<ExpressionSyntax>(
                            AssignmentExpression(
                                SyntaxKind.SimpleAssignmentExpression,
                                IdentifierName(fields[0].Name),
                                ObjectCreationExpression(fieldIdentifier)
                                    .WithInitializer(
                                    InitializerExpression(
                                        SyntaxKind.ObjectInitializerExpression,
                                        SeparatedList<ExpressionSyntax>(assignments)))))))
                    .NormalizeWhitespace();
            }

            return ObjectCreationExpression(componentIdentifier)
                .WithInitializer(
                InitializerExpression(
                    SyntaxKind.ObjectInitializerExpression,
                    SeparatedList<ExpressionSyntax>(assignments)))
                .NormalizeWhitespace();
        }
    }
}
