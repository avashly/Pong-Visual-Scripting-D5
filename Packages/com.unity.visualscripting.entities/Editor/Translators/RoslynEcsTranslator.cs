using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using CompilationOptions = UnityEngine.VisualScripting.CompilationOptions;

namespace UnityEditor.VisualScripting.Model.Translators
{
    /*
     * TODO:
     * - dispose NativeArrays
     * - use the right allocator (temp/tempjob)
     * - fix continue/returns
     */
    public class RoslynEcsTranslator : RoslynTranslator
    {
        public const string InputDeps = "inputDeps";

        static bool s_TypeManagerInitialized;

        [Flags]
        public enum TranslationOptions
        {
            None = 0,
            UseJobs = 1,
            BurstCompile = 2,
            Tracing = 4,
        }

        public class IterationContext
        {
            public readonly Stencil Stencil;
            ComponentDefinition[] m_Definitions;
            readonly TranslationContext m_Ctx;
            public readonly IIteratorStackModel Query;
            public readonly string GroupName;
            public readonly string IndexVariableName;
            public readonly HashSet<Type> WrittenEventTypes = new HashSet<Type>();

            public IterationContext(TranslationContext ctx, IIteratorStackModel queryDeclaration, string groupName, UpdateMode mode)
            {
                Stencil = queryDeclaration.GraphModel.Stencil;
                m_Definitions = queryDeclaration.ComponentQueryDeclarationModel.Components.Select(x => x.Component).ToArray();
                m_Ctx = ctx;
                Query = queryDeclaration;
                GroupName = groupName;
                switch (mode)
                {
                    case UpdateMode.OnStart:
                        GroupName += "Enter";
                        break;
                    case UpdateMode.OnEnd:
                        GroupName += "Exit";
                        break;
                }

                IndexVariableName = GroupName + "Idx";
                UpdateMode = mode;
            }

            public string EntitiesArrayName => GroupName + "Entities";
            public Allocator AllocatorType => m_Ctx.AllocatorType;
            public TranslationOptions TranslationOptions => m_Ctx.TranslationOptions;

            public readonly UpdateMode UpdateMode;
            TypeHandle m_StateComponentType;

            public IEnumerable<ComponentDefinition> FlattenedComponentDefinitions()
            {
                return m_Definitions;
            }

            public string GetComponentDataArrayName(string resolvedTypeName)
            {
                return $"{GroupName}{resolvedTypeName}Array";
            }

            public string GetComponentDataName(Type resolvedType)
            {
                return $"{GroupName}{resolvedType.Name}";
            }
        }

        int m_BuiltStackCounter;
        public bool AllowNoJobsFallback = true;

        public TranslationContext context { get; private set; }

        public void PushContext(IIteratorStackModel query, UpdateMode mode, bool isCoroutine = false)
        {
            context = context.PushContext(query, this, mode, isCoroutine);
        }

        public IEnumerable<StatementSyntax> PopContext()
        {
            var curContext = context;
            context = context.Parent;
            var popContext = curContext.PopContext();
            return popContext;
        }

        internal RoslynEcsTranslator(EcsStencil stencil)
            : base(stencil) {}

        protected override Microsoft.CodeAnalysis.SyntaxTree ToSyntaxTree(VSGraphModel graphModel, CompilationOptions compilationOptions)
        {
            var ecsStencil = (EcsStencil)graphModel.Stencil;
            try
            {
                Options = compilationOptions;
                if (!s_TypeManagerInitialized)
                {
                    TypeManager.Initialize(); // avoid NRE in type cache
                    s_TypeManagerInitialized = true;
                }

                return GenerateSyntaxTree(graphModel, compilationOptions, ecsStencil.UseJobSystem);
            }
            catch (JobSystemNotCompatibleException e)
            {
                if (AllowNoJobsFallback)
                {
                    Debug.LogWarning(e.Message + " - Falling back on main thread execution");
                    var newTranslator = new RoslynEcsTranslator(ecsStencil);
                    return newTranslator.GenerateSyntaxTree(graphModel, compilationOptions, false);
                }

                throw;
            }
        }

        Microsoft.CodeAnalysis.SyntaxTree GenerateSyntaxTree(VSGraphModel graphModel, CompilationOptions compilationOptions, bool useJobSystem)
        {
            var ecsStencil = (EcsStencil)graphModel.Stencil;

            ecsStencil.ClearComponentDefinitions();

            //TODO fix graph name, do not use the asset name
            var className = graphModel.TypeName;

            TranslationOptions options = TranslationOptions.None;
            if ((compilationOptions & CompilationOptions.Tracing) != 0)
                options |= TranslationOptions.Tracing;
            if (useJobSystem)
                options |= TranslationOptions.UseJobs;
            if (!compilationOptions.HasFlag(CompilationOptions.LiveEditing))
                options |= TranslationOptions.BurstCompile;

            var rootContext = new RootContext(ecsStencil, className, options);
            context = rootContext;

            List<IFunctionModel> entryPoints = graphModel.Stencil.GetEntryPoints(graphModel).Cast<IFunctionModel>().ToList();

            BuildSpecificStack<PreUpdate>(entryPoints, rootContext);

            foreach (FunctionModel stack in entryPoints.Cast<FunctionModel>().OrderBy(x => x is IOrderedStack orderedStack ? orderedStack.Order : -1))
            {
                if (stack is PreUpdate || stack is PostUpdate)
                    continue;

                IEnumerable<SyntaxNode> entrySyntaxNode = BuildNode(stack);
                foreach (var node in entrySyntaxNode)
                {
                    switch (node)
                    {
                        case null:
                            continue;
                        case StatementSyntax statement:
                            context.AddStatement(statement);
                            break;
                        case MemberDeclarationSyntax member:
                            rootContext.AddMember(member);
                            break;
                        default:
                            Debug.LogError($"Cannot process syntax node type {node.GetType().Name}");
                            break;
                    }
                }
            }

            // TODO: events (see KeyDownEvent)
            // foreach (var statementSyntax in m_EventRegistrations) rootContext.AddStatement(statementSyntax);

            BuildSpecificStack<PostUpdate>(entryPoints, rootContext);

            var referencedNamespaces = new List<string>
            {
                "System",
                "Unity.Burst",
                "Unity.Entities",
                "Unity.Jobs",
                "Unity.Mathematics",
                "Unity.Transforms",
                "Unity.Collections",
                "Microsoft.CSharp",
                "UnityEngine",
            };

            referencedNamespaces = referencedNamespaces.Distinct().ToList();

            var usingList = new List<UsingDirectiveSyntax>();
            foreach (var ns in referencedNamespaces)
            {
                string[] identifiers = ns.Split(".".ToCharArray());

                if (identifiers.Length == 1)
                {
                    usingList.Add(UsingDirective(IdentifierName(identifiers[0])));
                }
                else if (identifiers.Length == 2)
                {
                    usingList.Add(UsingDirective(QualifiedName(IdentifierName(identifiers[0]), IdentifierName(identifiers[1]))));
                }
                else if (identifiers.Length == 3)
                {
                    usingList.Add(UsingDirective(QualifiedName(
                        QualifiedName(IdentifierName(identifiers[0]), IdentifierName(identifiers[1])),
                        IdentifierName(identifiers[2]))));
                }
            }

            CompilationUnitSyntax compilationUnit = CompilationUnit()
                .WithUsings(
                List(usingList.ToArray()))
                .WithMembers(
                    SingletonList<MemberDeclarationSyntax>(rootContext.Build(this, graphModel))).NormalizeWhitespace();

            return compilationUnit.SyntaxTree;
        }

        void BuildSpecificStack<T>(IEnumerable<IFunctionModel> allStacks, RootContext ctx) where T : IFunctionModel
        {
            BlockSyntax blockNode = Block();
            foreach (var stack in allStacks.OfType<T>())
                BuildStack(stack, ref blockNode);

            foreach (var statement in blockNode.Statements)
                ctx.AddStatement(statement);
        }

        internal static MethodDeclarationSyntax MakeOnUpdateOverride(BlockSyntax blockSyntax, bool needToCompleteDependenciesFirst, Dictionary<Type, string> createdManagers)
        {
            if (needToCompleteDependenciesFirst)
            {
                return MethodDeclaration(
                    PredefinedType(Token(SyntaxKind.VoidKeyword)),
                    Identifier("OnUpdate"))
                        .WithModifiers(
                    TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword)))
                        .WithBody(
                            blockSyntax)
                        .NormalizeWhitespace();
            }

            foreach (var barrierSystem in createdManagers)
            {
                blockSyntax = blockSyntax.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                            MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                IdentifierName(barrierSystem.Value),
                                IdentifierName(nameof(EntityCommandBufferSystem.AddJobHandleForProducer))))
                            .WithArgumentList(
                            ArgumentList(
                                SingletonSeparatedList(
                                    Argument(
                                        IdentifierName(InputDeps)))))));
            }

            blockSyntax = blockSyntax.AddStatements(
                ReturnStatement(IdentifierName(InputDeps))
            );

            return MethodDeclaration(
                IdentifierName("JobHandle"),
                Identifier("OnUpdate"))
                    .WithModifiers(
                TokenList(Token(SyntaxKind.ProtectedKeyword), Token(SyntaxKind.OverrideKeyword)))
                    .WithParameterList(
                        ParameterList(
                            SingletonSeparatedList(
                                Parameter(
                                    Identifier(InputDeps))
                                    .WithType(
                                    IdentifierName("JobHandle")))))
                    .WithBody(
                        blockSyntax)
                    .NormalizeWhitespace();
        }

        // [A, B, C], statement -> if (A && B && C) { statement }
        public static StatementSyntax MakeCondition(IReadOnlyList<ExpressionSyntax> conditions, StatementSyntax bodySyntax)
        {
            if (conditions == null || conditions.Count == 0)
                return bodySyntax;
            int lastIndex = conditions.Count - 1;
            ExpressionSyntax condition = conditions[lastIndex];
            for (int i = lastIndex; i > 0; i++)
            {
                condition = BinaryExpression(
                    SyntaxKind.LogicalAndExpression,
                    conditions[i],
                    condition);
            }
            return IfStatement(condition, bodySyntax);
        }

        protected override IdentifierNameSyntax GetRecorderName() => context.GetRecorderName();

        protected override IEnumerable<SyntaxNode> BuildNode(INodeModel statement, IPortModel portModel)
        {
            if (statement == null)
                return Enumerable.Empty<SyntaxNode>();
            Assert.IsTrue(portModel == null || portModel.NodeModel == statement, "If a Port is provided, it must be owned by the provided node");

            // TODO : Find a better way to map [Node -> Current Archetype]
            if (!(context is RootContext) && statement.IsStacked)
            {
                var stencil = (EcsStencil)statement.GraphModel.Stencil;
                if (!stencil.ComponentDefinitions.ContainsKey(statement))
                    stencil.ComponentDefinitions.Add(statement, context.GetComponentDefinitions());
            }

            var ext = ModelUtility.ExtensionMethodCache<RoslynEcsTranslator>.GetExtensionMethod(
                statement.GetType(),
                FilterMethods,
                KeySelector)
                ?? ModelUtility.ExtensionMethodCache<RoslynTranslator>.GetExtensionMethod(
                statement.GetType(),
                FilterMethods,
                KeySelector);
            if (ext != null)
            {
                var syntaxNode = (IEnumerable<SyntaxNode>)ext.Invoke(null, new object[] { this, statement, portModel }) ?? Enumerable.Empty<SyntaxNode>();
                var annotatedNodes = new List<SyntaxNode>();
                foreach (var node in syntaxNode)
                {
                    var annotatedNode = node?.WithAdditionalAnnotations(new SyntaxAnnotation(Annotations.VSNodeMetadata, statement.Guid.ToString()));
                    annotatedNodes.Add(annotatedNode);
                }

                return annotatedNodes;
            }

            Debug.LogError("Roslyn ECS Translator doesn't know how to create a node of type: " + statement.GetType());

            return Enumerable.Empty<SyntaxNode>();
        }

        public override ExpressionSyntax Constant(object value, Stencil stencil, Type generatedType = null)
        {
            switch (value)
            {
                case float2 _:
                case float3 _:
                case float4 _:
                case quaternion _:
                    return CreateConstantInitializationExpression(value, generatedType ?? value.GetType());
                default:
                    return base.Constant(value, stencil, generatedType);
            }
        }

        static ObjectCreationExpressionSyntax CreateConstantInitializationExpression(object value, Type type)
        {
            var x = Single.NaN;
            var y = Single.NaN;
            var z = Single.NaN;
            var w = Single.NaN;

            if (value is float2 vector2)
            {
                x = vector2.x;
                y = vector2.y;
            }
            else if (value is float3 vector3)
            {
                x = vector3.x;
                y = vector3.y;
                z = vector3.z;
            }
            else if (value is float4 vector4)
            {
                x = vector4.x;
                y = vector4.y;
                z = vector4.z;
                w = vector4.w;
            }
            else if (value is quaternion quaternion)
            {
                x = quaternion.value.x;
                y = quaternion.value.y;
                z = quaternion.value.z;
                w = quaternion.value.w;
            }

            var argumentSyntaxList = new List<SyntaxNodeOrToken>();
            var arguments = new List<float> { x, y, z, w }.Where(arg => !Single.IsNaN(arg)).ToList();
            for (var index = 0; index < arguments.Count; index++)
            {
                var argument = arguments[index];
                argumentSyntaxList.Add(Argument(
                    LiteralExpression(
                        SyntaxKind.NumericLiteralExpression,
                        Literal(argument))));

                if (index < arguments.Count - 1)
                    argumentSyntaxList.Add(Token(SyntaxKind.CommaToken));
            }

            var vectorSyntaxNode = ObjectCreationExpression(
                IdentifierName(type.Name))
                    .WithArgumentList(
                ArgumentList(
                    SeparatedList<ArgumentSyntax>(argumentSyntaxList)));

            return vectorSyntaxNode;
        }

        public enum AccessMode
        {
            None,
            Read,
            Write
        }

        public AccessMode IsRecordingComponentAccesses { get; private set; } = AccessMode.Read;
        public bool GameObjectCodeGen { get; set; }

        public IDisposable RecordComponentAccess(AccessMode accessAccessMode)
        {
            AccessMode prevMode = IsRecordingComponentAccesses;

            // if prev mode is write and new one is read, keep write
            IsRecordingComponentAccesses = accessAccessMode > prevMode ? accessAccessMode : prevMode;
            return new ComponentAccessRecorder(() => IsRecordingComponentAccesses = prevMode);
        }

        struct ComponentAccessRecorder : IDisposable
        {
            readonly Action m_OnDispose;

            public ComponentAccessRecorder(Action onDispose)
            {
                m_OnDispose = onDispose;
            }

            public void Dispose()
            {
                m_OnDispose();
            }
        }

        internal class JobSystemNotCompatibleException : Exception
        {
            public JobSystemNotCompatibleException(string reason)
                : base(reason) {}
        }

        public ExpressionSyntax BuildEntityFromPortOrCurrentIteration(IPortModel entityPort)
        {
            if (entityPort.Connected)
                return BuildPort(entityPort).SingleOrDefault() as ExpressionSyntax;

            var entity = context.IterationContext.Query.ItemVariableDeclarationModel;
            context.RecordEntityAccess(entity);
            return IdentifierName(entity.Name);
        }

        public bool GetComponentFromEntityOrComponentPort(
            INodeModel model,
            IPortModel entityOrComponentPort,
            out ComponentQueryDeclarationModel query,
            out ExpressionSyntax setValue,
            AccessMode mode = AccessMode.Read)
        {
            setValue = null;
            var componentVariableType1 = entityOrComponentPort.DataType;

            var varNode = !entityOrComponentPort.Connected
                ? context.IterationContext.Query.ItemVariableDeclarationModel
                : (entityOrComponentPort.ConnectionPortModels?.FirstOrDefault()?.NodeModel
                    as VariableNodeModel)?.DeclarationModel;

            if (varNode != null &&
                varNode.DataType == typeof(Entity).GenerateTypeHandle(Stencil) &&
                varNode.Owner is IIteratorStackModel iteratorStackModel)
            {
                query = iteratorStackModel.ComponentQueryDeclarationModel;
                if (query.Components.Any(x => x.Component.TypeHandle == componentVariableType1))
                {
                    context.RecordComponentAccess(context.IterationContext,
                        componentVariableType1,
                        mode);
                    var componentVarName = context.GetComponentVariableName(iteratorStackModel, componentVariableType1);
                    setValue = IdentifierName(componentVarName);
                }
                else
                {
                    var componentName = componentVariableType1.Resolve(Stencil).FriendlyName();
                    var queryCopy = query;
                    AddError(model,
                        $"A component of type {componentName} is required, which the query {query.Name} doesn't specify",
                        new CompilerQuickFix($"Add {componentName} to the query",
                            s => s.Dispatch(new AddComponentToQueryAction(
                                queryCopy,
                                componentVariableType1,
                                ComponentDefinitionFlags.None)))
                    );
                    return false;
                }
            }

            if (setValue == null)
            {
                context.RecordComponentAccess(context.IterationContext,
                    componentVariableType1,
                    mode);
                setValue = BuildPort(entityOrComponentPort).FirstOrDefault() as ExpressionSyntax;
            }

            query = null;
            return true;
        }

        public IterationContext FindContext(IIteratorStackModel groupDeclaration)
        {
            var current = context;
            while (current != null && current.IterationContext?.Query != groupDeclaration)
                current = current.Parent;
            return current?.IterationContext;
        }

        public override void BuildStack(IStackModel stack, ref BlockSyntax block,
            StackExitStrategy exitStrategy = StackExitStrategy.Return)
        {
            switch (stack)
            {
                case null:
                    return;

                case IIteratorStackModel iteratorStack when iteratorStack.ContainsCoroutine():
                    BuildCoroutineStack(iteratorStack, ref block);
                    return;

                default:
                    base.BuildStack(stack, ref block, exitStrategy);
                    break;
            }
        }

        void BuildCoroutineStack(IIteratorStackModel stack, ref BlockSyntax block)
        {
            PushContext(stack, context.IterationContext.UpdateMode, true);

            var ctx = (CoroutineContext)context;
            ctx.BuildComponent(stack, this);

            var statement = PopContext();
            block = block.AddStatements(statement.ToArray());
        }
    }
}
