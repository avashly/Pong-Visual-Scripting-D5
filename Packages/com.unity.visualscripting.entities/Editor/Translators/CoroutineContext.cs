using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Plugins;
using UnityEngine;
using VisualScripting.Entities.Runtime;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CompilationOptions = UnityEngine.VisualScripting.CompilationOptions;

namespace UnityEditor.VisualScripting.Model.Translators
{
    public class CoroutineContext : TranslationContext
    {
        protected const string k_CoroutineStateVariableName = "State";

        public bool SkipStateBuilding;

        protected string m_ComponentTypeName;
        protected Dictionary<ArgumentSyntax, ParameterSyntax> m_Parameters =
            new Dictionary<ArgumentSyntax, ParameterSyntax>();
        readonly RoslynEcsTranslator m_Translator;
        HashSet<TypeHandle> m_AccessedComponents = new HashSet<TypeHandle>();
        HashSet<string> m_DeclaredComponentArray = new HashSet<string>();
        Dictionary<string, FieldDeclarationSyntax> m_ComponentVariables =
            new Dictionary<string, FieldDeclarationSyntax>();
        List<StateData> m_States = new List<StateData>();
        int m_BuiltStackCounter;
        StackExitStrategy m_StackExitStrategy = StackExitStrategy.Return;
        Dictionary<IStackModel, int> m_StackIndexes = new Dictionary<IStackModel, int>();

        public string CoroutineParameterName => BuildCoroutineParameterName(m_ComponentTypeName);

        protected string UpdateMethodName => $"Update{m_ComponentTypeName}";

        internal virtual bool IsJobContext => GetParent<JobContext>() != null;

        class StateData
        {
            public List<StatementSyntax> Statements = new List<StatementSyntax>();
            public bool SkipStateBuilding;
            public int NextStateIndex;
        }

        public CoroutineContext(TranslationContext parent, RoslynEcsTranslator translator)
            : base(parent)
        {
            m_Translator = translator;
            IterationContext = parent.IterationContext;
            m_ComponentTypeName = translator.MakeUniqueName($"{IterationContext.GroupName}Coroutine").ToPascalCase();
        }

        public override TranslationContext PushContext(IIteratorStackModel query, RoslynEcsTranslator translator,
            UpdateMode mode, bool isCoroutine = false)
        {
            if (isCoroutine)
                return new NestedCoroutineContext(this, translator);

            return new ForEachContext(query, this, mode);
        }

        protected override IEnumerable<StatementSyntax> OnPopContext()
        {
            // Build Coroutine MoveNext call statement
            var block = Block();

            // Remove component when coroutine is completed
            var removeStatement = Parent.GetEntityManipulationTranslator().RemoveComponent(
                Parent,
                IdentifierName(Parent.EntityName),
                m_ComponentTypeName);

            // TODO Technical Debt : We should be able to RecordComponentAccess with CoroutineComponent to automate this
            var arguments = m_Parameters.Keys.ToList(); // QUICKFIX : This is ugly. Make a copy to avoid adding extra arguments by the SetComponent in the thenStatement
            if (Parent.GetType() == typeof(ForEachContext))
            {
                var thenStatement = Block(
                    ExpressionStatement(
                        Parent.GetEntityManipulationTranslator().SetComponent(
                            this,
                            ElementAccessExpression(IdentifierName(IterationContext.EntitiesArrayName))
                                .WithArgumentList(
                                BracketedArgumentList(
                                    SingletonSeparatedList(
                                        Argument(
                                            IdentifierName(IterationContext.IndexVariableName))))),
                            m_ComponentTypeName,
                            IdentifierName(CoroutineParameterName),
                            false)
                            .Cast<ExpressionSyntax>()
                            .Single()));

                block = block.AddStatements(IfStatement(
                    RoslynBuilder.MethodInvocation(
                        UpdateMethodName,
                        IdentifierName(GetSystemClassName()),
                        arguments,
                        Enumerable.Empty<TypeSyntax>()),
                    thenStatement)
                        .WithElse(
                        ElseClause(removeStatement.Aggregate(Block(), (c, s) => c.AddStatements(s)))));

                yield return block;
                yield break;
            }

            // Call Update
            block = block.AddStatements(IfStatement(
                PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    RoslynBuilder.MethodInvocation(
                        UpdateMethodName,
                        IdentifierName(GetSystemClassName()),
                        arguments,
                        Enumerable.Empty<TypeSyntax>())),
                removeStatement.Aggregate(block, (current, syntax) => current.AddStatements(syntax))));

            yield return block;
        }

        public override void AddStatement(StatementSyntax statement)
        {
            Parent.AddStatement(statement);
        }

        public override void AddEntityDeclaration(string variableName)
        {
            Parent.AddEntityDeclaration(variableName);
        }

        bool HasParameter(string name)
        {
            return m_Parameters.Values.Any(parameter => parameter.Identifier.Text == name);
        }

        public override void RecordEntityAccess(IVariableDeclarationModel model)
        {
            Parent.RecordEntityAccess(model);

            if (HasParameter(model.Name))
                return;

            m_Parameters.Add(
                Argument(IdentifierName(model.Name)),
                Parameter(Identifier(model.Name))
                    .WithType(TypeSystem.BuildTypeSyntax(typeof(Entity))));
        }

        public override void RecordComponentAccess(RoslynEcsTranslator.IterationContext query, TypeHandle componentType,
            RoslynEcsTranslator.AccessMode mode)
        {
            Parent.RecordComponentAccess(query, componentType, mode);
            if (IterationContext == query)
                m_AccessedComponents.Add(componentType);
        }

        public override ExpressionSyntax GetCachedValue(string key, ExpressionSyntax value, TypeHandle type,
            params IdentifierNameSyntax[] attributes)
        {
            var constant = base.GetCachedValue(key, value, type, attributes);

            // Use the same identifier as the parentJob. This avoid things like myCoroutine.FixedTime0 = FixedTime
            var variableName = constant is IdentifierNameSyntax ins
                ? ins.Identifier.Text
                : key.Replace(".", "_");

            if (!HasParameter(variableName))
            {
                var param = Parameter(Identifier(variableName))
                    .WithType(TypeSystem.BuildTypeSyntax(type.Resolve(IterationContext.Stencil)));
                if (attributes.Any())
                    param.AddAttributeLists(AttributeList(SeparatedList(attributes.Select(Attribute))));

                m_Parameters.Add(Argument(constant), param);
            }

            return IdentifierName(variableName);
        }

        protected override StatementSyntax GetOrDeclareEntityArray(RoslynEcsTranslator.IterationContext context,
            out StatementSyntax arrayDisposal)
        {
            if (Parent is JobContext)
            {
                m_Parameters.Add(
                    Argument(IdentifierName(context.EntitiesArrayName)),
                    Parameter(Identifier(context.EntitiesArrayName))
                        .WithType(TypeSystem.BuildTypeSyntax(typeof(NativeArray<Entity>))));
            }
            else
            {
                m_Parameters.Add(
                    Argument(IdentifierName(context.GroupName)),
                    Parameter(Identifier(context.GroupName))
                        .WithType(TypeSystem.BuildTypeSyntax(typeof(EntityQuery))));
            }

            return base.GetOrDeclareEntityArray(context, out arrayDisposal);
        }

        public override string GetOrDeclareComponentArray(RoslynEcsTranslator.IterationContext ctx,
            string componentTypeName, out LocalDeclarationStatementSyntax arrayInitialization,
            out StatementSyntax arrayDisposal)
        {
            var declaration = Parent.GetOrDeclareComponentArray(
                ctx,
                componentTypeName,
                out arrayInitialization,
                out arrayDisposal);
            var parameter = declaration.ToCamelCase();

            if (!(Parent is JobContext) || m_DeclaredComponentArray.Contains(componentTypeName))
                return parameter;

            m_Parameters.Add(
                Argument(IdentifierName(declaration)),
                Parameter(Identifier(parameter))
                    .WithType(
                    GenericName(
                        Identifier("NativeArray"))
                        .WithTypeArgumentList(
                        TypeArgumentList(
                            SingletonSeparatedList<TypeSyntax>(
                                IdentifierName(componentTypeName))))));
            m_DeclaredComponentArray.Add(componentTypeName);

            return parameter;
        }

        public override string GetComponentVariableName(IIteratorStackModel query, TypeHandle type)
        {
            return Parent.GetComponentVariableName(query, type);
        }

        public override IdentifierNameSyntax GetOrDeclareCommandBuffer(bool isConcurrent)
        {
            var declaration = Parent.GetOrDeclareCommandBuffer(isConcurrent);
            var name = declaration.Identifier.Text;

            if (!HasParameter(name))
            {
                var parameter = IdentifierName(name);
                var cmdType = declaration.Identifier.Text != nameof(ComponentSystem.PostUpdateCommands)
                    ? typeof(EntityCommandBuffer.Concurrent)
                    : typeof(EntityCommandBuffer);
                m_Parameters.Add(
                    Argument(IdentifierName(declaration.Identifier)),
                    Parameter(parameter.Identifier).WithType(TypeSystem.BuildTypeSyntax(cmdType)));
            }

            return declaration;
        }

        public override string GetJobIndexParameterName()
        {
            var declaration = Parent.GetJobIndexParameterName();

            if (!HasParameter(declaration))
            {
                m_Parameters.Add(
                    Argument(IdentifierName(declaration)),
                    Parameter(Identifier(declaration)).WithType(TypeSystem.BuildTypeSyntax(typeof(int))));
            }

            return declaration;
        }

        public override IdentifierNameSyntax GetEventBufferWriter(RoslynEcsTranslator.IterationContext iterationContext,
            ExpressionSyntax entity, Type eventType, out StatementSyntax bufferInitialization)
        {
            var declaration = Parent.GetEventBufferWriter(iterationContext, entity, eventType, out bufferInitialization);
            var parameter = declaration.Identifier.Text.ToCamelCase();

            if (!HasParameter(parameter))
            {
                m_Parameters.Add(
                    Argument(declaration),
                    Parameter(Identifier(parameter))
                        .WithType(
                        GenericName(Identifier("DynamicBuffer"))
                            .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList(TypeSystem.BuildTypeSyntax(eventType))))));
            }

            return IdentifierName(parameter);
        }

        public override ExpressionSyntax GetSingletonVariable(IVariableDeclarationModel variable)
        {
            var ext = Parent.GetSingletonVariable(variable);
            if (!HasParameter(RootContext.SingletonVariableName))
            {
                // If not jobContext (readOnly access) we have to pass the singleton component as reference
                if (IsJobContext)
                {
                    m_Parameters.Add(
                        Argument(ext),
                        Parameter(Identifier(((IdentifierNameSyntax)ext).Identifier.Text))
                            .WithType(TypeSystem.BuildTypeSyntax(variable.DataType.Resolve(IterationContext.Stencil))));
                }
                else
                {
                    m_Parameters.Add(
                        Argument(IdentifierName(RootContext.SingletonVariableName))
                            .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                        Parameter(Identifier(RootContext.SingletonVariableName))
                            .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                            .WithType(IdentifierName(RootContext.SingletonComponentTypeName)));
                }
            }

            return ext;
        }

        public virtual void BuildComponent(IStackModel stack, RoslynEcsTranslator translator)
        {
            // Build stack
            BuildStack(translator, stack, 0);

            // Create coroutine component
            var members = BuildComponentMembers();
            DeclareComponent<ISystemStateComponentData>(m_ComponentTypeName, members);

            // Add coroutine in the lambda/job execute method
            IncludeCoroutineComponent(IterationContext, m_ComponentTypeName);

            // Add coroutine component
            PrependStatement(BuildAddCoroutineComponent(IterationContext, m_ComponentTypeName));

            // Create coroutine update method
            DeclareSystemMethod(BuildUpdateCoroutineMethod());
        }

        static ExpressionStatementSyntax BuildAddCoroutineComponent(
            RoslynEcsTranslator.IterationContext iterationContext, string coroutineName)
        {
            return ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(nameof(EntityManager)),
                        GenericName(
                            Identifier(nameof(EntityManager.AddComponent)))
                            .WithTypeArgumentList(
                            TypeArgumentList(
                                SingletonSeparatedList<TypeSyntax>(
                                    IdentifierName(coroutineName))))))
                    .WithArgumentList(
                    ArgumentList(
                        SingletonSeparatedList(
                            Argument(
                                IdentifierName(
                                    CoroutineTranslator.MakeExcludeCoroutineQueryName(iterationContext)))))));
        }

        protected MethodDeclarationSyntax BuildUpdateCoroutineMethod()
        {
            var states = new List<SwitchSectionSyntax>();
            var index = 0;

            foreach (var state in m_States)
            {
                if (!state.SkipStateBuilding)
                {
                    state.Statements.Add(BuildGoToState(state.NextStateIndex));
                    state.Statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)));
                }

                states.Add(SwitchSection()
                    .WithLabels(
                        SingletonList<SwitchLabelSyntax>(
                            CaseSwitchLabel(
                                LiteralExpression(
                                    SyntaxKind.NumericLiteralExpression,
                                    Literal(index)))))
                    .WithStatements(
                        SingletonList<StatementSyntax>(
                            Block(state.Statements))));

                index++;
            }

            var coroutineIdentifier = BuildCoroutineParameter();
            return RoslynBuilder.DeclareMethod(
                UpdateMethodName,
                AccessibilityFlags.Private | AccessibilityFlags.Static,
                typeof(bool))
                .WithParameterList(
                    ParameterList(SeparatedList(m_Parameters.Values)))
                .WithBody(
                    Block(
                        SwitchStatement(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                coroutineIdentifier,
                                IdentifierName(k_CoroutineStateVariableName)))
                            .WithOpenParenToken(Token(SyntaxKind.OpenParenToken))
                            .WithCloseParenToken(Token(SyntaxKind.CloseParenToken))
                            .WithSections(List(states)),
                        ReturnStatement(
                            LiteralExpression(SyntaxKind.FalseLiteralExpression))));
        }

        protected virtual IdentifierNameSyntax BuildCoroutineParameter()
        {
            var coroutineIdentifier = IdentifierName(CoroutineParameterName);
            m_Parameters.Add(
                Argument(coroutineIdentifier)
                    .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                Parameter(coroutineIdentifier.Identifier)
                    .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                    .WithType(IdentifierName(m_ComponentTypeName)));

            return coroutineIdentifier;
        }

        protected IEnumerable<MemberDeclarationSyntax> BuildComponentMembers()
        {
            // Fields
            var members = new List<MemberDeclarationSyntax>
            {
                RoslynBuilder.DeclareField(
                    typeof(int),
                    k_CoroutineStateVariableName,
                    AccessibilityFlags.Public)
            };
            members.AddRange(m_ComponentVariables.Values);

            foreach (var component in m_AccessedComponents)
            {
                var componentName = GetComponentVariableName(IterationContext.Query, component);
                var componentType = component.Resolve(IterationContext.Stencil);

                m_Parameters.Add(
                    Argument(IdentifierName(componentName))
                        .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                    Parameter(Identifier(componentName))
                        .WithModifiers(TokenList(Token(SyntaxKind.RefKeyword)))
                        .WithType(TypeSystem.BuildTypeSyntax(componentType)));
            }

            foreach (var localVariable in IterationContext.Query.FunctionVariableModels)
            {
                m_Parameters.Add(
                    Argument(IdentifierName(localVariable.Name)),
                    Parameter(Identifier(localVariable.Name))
                        .WithType(TypeSystem.BuildTypeSyntax(localVariable.DataType.Resolve(IterationContext.Stencil))));
            }

            if ((TranslationOptions & RoslynEcsTranslator.TranslationOptions.Tracing) != 0)
            {
                m_Parameters.Add(
                    Argument(GetRecorderName())
                        .WithRefOrOutKeyword(Token(SyntaxKind.RefKeyword)),
                    Parameter(GetRecorderName().Identifier)
                        .WithModifiers(SyntaxTokenList.Create(Token(SyntaxKind.RefKeyword)))
                        .WithType((IsJobContext ? typeof(GraphStream.Writer) : typeof(DebuggerTracer.EntityFrameTrace))
                            .ToTypeSyntax()));
            }

            return members;
        }

        IEnumerable<StatementSyntax> ConvertNodesToSyntaxList(INodeModel node, IEnumerable<SyntaxNode> blocks,
            CompilationOptions options)
        {
            foreach (var block in blocks)
            {
                switch (block)
                {
                    case StatementSyntax statementNode:
                        foreach (var statementSyntax in Instrument(statementNode, node, options))
                            yield return statementSyntax;
                        break;

                    case ExpressionSyntax expressionNode:
                        foreach (var statementSyntax in Instrument(ExpressionStatement(expressionNode)
                            .WithAdditionalAnnotations(
                                new SyntaxAnnotation(Annotations.VSNodeMetadata, node.Guid.ToString())), node, options))
                            yield return statementSyntax;
                        break;

                    default:
                        throw new InvalidOperationException("Expected a statement or expression " +
                            $"node, found a {node.GetType()} when building {block}");
                }
            }
        }

        StateData RequestNewState()
        {
            m_States.Add(new StateData());
            return m_States.Last();
        }

        int GetCurrentStateIndex()
        {
            if (!m_States.Any())
                RequestNewState();

            return m_States.Count - 1;
        }

        protected void BuildStack(RoslynEcsTranslator translator, IStackModel stack, int currentStateIndex,
            StackExitStrategy exitStrategy = StackExitStrategy.Return)
        {
            if (stack == null || stack.State == ModelState.Disabled)
                return;

            translator.RegisterBuiltStack(stack);

            if (m_StackIndexes.TryGetValue(stack, out var endStackIndex))
                currentStateIndex = endStackIndex;

            // JUST in case... until we validate the previous failsafe
            if (m_BuiltStackCounter++ > 10000)
                throw new InvalidOperationException("Infinite loop while building the script, aborting");

            var origStackExitStrategy = m_StackExitStrategy;
            if (exitStrategy != StackExitStrategy.Inherit)
                m_StackExitStrategy = exitStrategy;

            if (m_States.Count == 0)
            {
                var state = RequestNewState();
                state.NextStateIndex = GetCurrentStateIndex();
            }

            var origEndStack = translator.EndStack;
            foreach (var node in stack.NodeModels)
            {
                if (node.State == ModelState.Disabled)
                    continue;

                switch (node)
                {
                    case CoroutineNodeModel coroutineNode:
                        BuildCoroutineNode(coroutineNode, translator, ref currentStateIndex);
                        continue;

                    case IfConditionNodeModel ifConditionNodeModel:
                        BuildIfConditionNode(ifConditionNodeModel, translator, currentStateIndex);
                        continue;

                    default:
                    {
                        var blocks = translator.BuildNode(node);
                        var currentState = m_States[currentStateIndex];
                        currentState.SkipStateBuilding = currentState.SkipStateBuilding || SkipStateBuilding;
                        currentState.Statements.AddRange(ConvertNodesToSyntaxList(node, blocks, translator.Options));

                        if (!SkipStateBuilding && stack.NodeModels.Last() == node && !HasConnectedStack(stack))
                        {
                            currentState.SkipStateBuilding = true;
                            currentState.Statements.Add(ReturnStatement(
                                LiteralExpression(SyntaxKind.FalseLiteralExpression)));
                        }

                        SkipStateBuilding = false;
                        break;
                    }
                }
            }

            if (stack.DelegatesOutputsToNode(out _))
            {
                var nextStack = translator.EndStack;
                m_StackExitStrategy = origStackExitStrategy;

                if (translator.EndStack == origEndStack)
                    return;

                translator.EndStack = origEndStack;

                if (nextStack != null)
                    BuildStack(translator, nextStack, m_StackIndexes[nextStack], exitStrategy);

                return;
            }

            foreach (var outputPort in stack.OutputPorts)
                foreach (var connectedStack in outputPort.ConnectionPortModels)
                    if (connectedStack.NodeModel is IStackModel nextStack)
                        if (!ReferenceEquals(nextStack, translator.EndStack))
                            BuildStack(translator, nextStack, currentStateIndex, exitStrategy);

            m_StackExitStrategy = origStackExitStrategy;
        }

        void BuildIfConditionNode(IfConditionNodeModel node, RoslynEcsTranslator translator, int stateIndex)
        {
            translator.BuildNode(node);

            var firstThenStack = RoslynTranslator.GetConnectedStack(node, 0);
            var firstElseStack = RoslynTranslator.GetConnectedStack(node, 1);
            var ifState = m_States[stateIndex];
            var ifIndex = GetCurrentStateIndex();

            var endStackIndex = 0;
            if (translator.EndStack != null)
                m_StackIndexes.TryGetValue(translator.EndStack, out endStackIndex);

            // Reserve then/else/complete states first
            var thenIndex = ifIndex;
            var thenBlock = Block().AddStatements(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));
            StateData thenState = null;
            if (firstThenStack != null)
            {
                if (firstThenStack == translator.EndStack && endStackIndex != 0)
                {
                    thenBlock = Block().AddStatements(BuildGoToState(endStackIndex));
                }
                else
                {
                    thenIndex += 1;
                    thenState = RequestNewState();
                    TryAddStackIndex(firstThenStack, thenIndex);
                    thenBlock = Block().AddStatements(BuildGoToState(thenIndex));
                }
            }

            var elseIndex = thenIndex + 1;
            var elseBlock = Block().AddStatements(ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression)));
            StateData elseState = null;
            if (firstElseStack != null)
            {
                if (firstElseStack == translator.EndStack && endStackIndex != 0)
                {
                    elseBlock = Block().AddStatements(BuildGoToState(endStackIndex));
                }
                else
                {
                    elseState = RequestNewState();
                    TryAddStackIndex(firstElseStack, elseIndex);
                    elseBlock = Block().AddStatements(BuildGoToState(elseIndex));
                }
            }

            // Then Build stacks
            ifState.Statements.Add(RoslynBuilder.IfStatement(
                translator.BuildPort(node.IfPort).SingleOrDefault(),
                thenBlock,
                elseBlock)
                .WithAdditionalAnnotations(
                    new SyntaxAnnotation(Annotations.VSNodeMetadata, node.Guid.ToString())));
            ifState.Statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)));
            ifState.SkipStateBuilding = true;

            var reserveEndStackState = translator.EndStack != null
                && translator.EndStack != firstElseStack
                && translator.EndStack != firstThenStack
                && endStackIndex == 0;

            if (reserveEndStackState)
            {
                var endState = RequestNewState();
                endState.NextStateIndex = GetNextStateIndex(translator.EndStack);
                TryAddStackIndex(translator.EndStack, GetCurrentStateIndex());
            }

            var origBuiltStacks = translator.BuiltStacks;
            translator.BuiltStacks = new HashSet<IStackModel>(origBuiltStacks);

            if (translator.EndStack != firstThenStack)
            {
                if (translator.EndStack != null && thenState != null)
                    thenState.NextStateIndex = m_StackIndexes[translator.EndStack];
                BuildStack(translator, firstThenStack, thenIndex, StackExitStrategy.Inherit);
            }

            var partialStacks = translator.BuiltStacks;
            translator.BuiltStacks = new HashSet<IStackModel>(origBuiltStacks);

            if (translator.EndStack != firstElseStack)
            {
                if (translator.EndStack != null && elseState != null)
                    elseState.NextStateIndex = m_StackIndexes[translator.EndStack];
                BuildStack(translator, firstElseStack, elseIndex, StackExitStrategy.Inherit);
            }

            translator.BuiltStacks.UnionWith(partialStacks);
        }

        void BuildCoroutineNode(CoroutineNodeModel node, RoslynEcsTranslator translator, ref int currentIndex)
        {
            foreach (var variable in node.Fields.Where(v => !m_ComponentVariables.ContainsKey(v.Key)))
                m_ComponentVariables.Add(variable.Key, variable.Value);

            // Coroutine initialization state
            var initState = m_States[currentIndex];
            initState.Statements.AddRange(CoroutineTranslator.BuildInitState(node, translator));

            var nextStateIndex = initState.NextStateIndex;
            initState.NextStateIndex = m_States.Count;

            // Coroutine update state
            var updateState = RequestNewState();
            updateState.SkipStateBuilding = true;

            var hasNextNode = node.ParentStackModel.NodeModels.Last() != node;
            var nextStack = GetNextStack(node.ParentStackModel);
            int nextIndex;
            if (hasNextNode || nextStack != null && !m_StackIndexes.ContainsKey(nextStack))
            {
                nextIndex = m_States.Count;
                var newState = RequestNewState();
                newState.NextStateIndex = nextStateIndex;
            }
            else
            {
                nextIndex = nextStateIndex == 0 ? m_States.Count : nextStateIndex;
            }

            node.NextStateIndex = nextIndex;
            updateState.Statements.AddRange(translator.BuildNode(node).Cast<StatementSyntax>());
            currentIndex = GetCurrentStateIndex();
        }

        int GetNextStateIndex(IStackModel stack)
        {
            foreach (var outputPort in stack.OutputPorts)
            {
                foreach (var connectionPortModel in outputPort.ConnectionPortModels)
                {
                    if (connectionPortModel.NodeModel is IStackModel nextStack)
                        return m_StackIndexes.TryGetValue(nextStack, out var nextStackIndex)
                            ? nextStackIndex
                            : m_States.Count;
                }
            }

            return m_States.Count;
        }

        void TryAddStackIndex(IStackModel stack, int index)
        {
            if (!m_StackIndexes.ContainsKey(stack))
                m_StackIndexes.Add(stack, index);
        }

        static bool HasConnectedStack(IStackModel stack)
        {
            return stack.OutputPorts.Any(output =>
                output.ConnectionPortModels.Any(outputConnectionPortModel =>
                    outputConnectionPortModel.NodeModel is IStackModel));
        }

        static IStackModel GetNextStack(IStackModel stack)
        {
            foreach (var outputPort in stack.OutputPorts)
            {
                foreach (var connectionPortModel in outputPort.ConnectionPortModels)
                {
                    if (connectionPortModel.NodeModel is IStackModel nextStack)
                    {
                        return nextStack;
                    }
                }
            }

            return null;
        }

        internal IEnumerable<StatementSyntax> Instrument(StatementSyntax syntaxNode, INodeModel nodeModel, CompilationOptions options)
        {
            // TODO: RecordValue codegen counter instead of counting them after the fact
            if ((options & CompilationOptions.Tracing) != 0)
            {
                int recordedValuesCount = syntaxNode.GetAnnotatedNodes(Annotations.RecordValueKind).Count();

                yield return InstrumentForInEditorDebugging.BuildLastCallFrameExpression(recordedValuesCount, nodeModel.Guid,
                    GetRecorderName(),
                    nodeModel is INodeModelProgress && nodeModel is CoroutineNodeModel coroutineNodeModel
                    ? MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(CoroutineParameterName),
                        IdentifierName(coroutineNodeModel.VariableName))
                    : null);
            }

            yield return syntaxNode;
        }

        internal ExpressionStatementSyntax BuildGoToState(int index)
        {
            return ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(CoroutineParameterName),
                        IdentifierName(k_CoroutineStateVariableName)),
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(index))));
        }

        internal static string BuildCoroutineParameterName(string coroutineTypeName)
        {
            return coroutineTypeName.ToCamelCase();
        }

        public ExpressionSyntax TranslateCustomParameter(ParameterInfo p, CoroutineSpecialVariableAttribute attribute)
        {
            var parameterType = p.ParameterType.GenerateTypeHandle(IterationContext.Stencil);
            var attributeType = attribute.GetType();

            ExpressionSyntax expr;

            var ext = ModelUtility.ExtensionMethodCache<CoroutineParameterTranslator>.GetExtensionMethod(
                attributeType,
                FilterMethods,
                KeySelector);
            if (ext != null)
            {
                var translator = new CoroutineParameterTranslator { Translator = m_Translator };
                expr = (ExpressionSyntax)ext.Invoke(null, new object[] { translator, attribute });
            }
            else
            {
                string errorMessage = $"Unable to translate MoveNext parameter: [{attributeType.Name}] {p.ParameterType.Name} {p.Name}";
                expr = LiteralExpression(SyntaxKind.DefaultLiteralExpression,
                    Token(TriviaList(), SyntaxKind.DefaultKeyword, TriviaList(Comment("/* " + errorMessage + " */"))));
                Debug.LogError(errorMessage);
            }

            return GetCachedValue(p.Name, expr, parameterType);
        }

        // Looking for methods like : public static ExpressionSyntax DefineVariable(this CoroutineTranslator translator, CoroutineSpecialVariableAttribute attr)
        static bool FilterMethods(MethodInfo x)
        {
            return x.ReturnType == typeof(ExpressionSyntax) && x.GetParameters().Length == 2;
        }

        static Type KeySelector(MethodInfo x)
        {
            return x.GetParameters()[1].ParameterType;
        }

        public void AddComponentField(string typeName, string fieldName)
        {
            var declaration = FieldDeclaration(
                VariableDeclaration(
                    IdentifierName(typeName))
                    .WithVariables(
                    SingletonSeparatedList(
                        VariableDeclarator(
                            Identifier(fieldName)))))
                    .WithModifiers(
                TokenList(
                    Token(SyntaxKind.PublicKeyword)));
            m_ComponentVariables.Add(typeName, declaration);
        }
    }
}
