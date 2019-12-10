using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Packages.VisualScripting.Editor.Elements;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.Compilation;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScripting.Model.Compilation;
using UnityEngine;
using UnityEngine.Assertions;
using CompilationOptions = UnityEngine.VisualScripting.CompilationOptions;
using Component = UnityEngine.Component;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [PublicAPI]
    public class EcsStencil : ClassStencil, IHasOrderedStacks
    {
        [UsedImplicitly]
        public bool UseJobSystem;

        public List<VSGraphAssetModel> UpdateAfter = new List<VSGraphAssetModel>();
        public List<VSGraphAssetModel> UpdateBefore = new List<VSGraphAssetModel>();

        static Dictionary<TypeHandle, Type> s_TypeToConstantNodeModelTypeCache;

        DragNDropEcsHandler m_NDropHandler;
        Dictionary<IFunctionModel, HashSet<ComponentQueryDeclarationModel>> m_EntryPointsToQueries =
            new Dictionary<IFunctionModel, HashSet<ComponentQueryDeclarationModel>>();
        ISearcherDatabaseProvider m_SearcherDatabaseProvider;
        ISearcherFilterProvider m_SearcherFilterProvider;
        Dictionary<INodeModel, IEnumerable<ComponentDefinition>> m_ComponentDefinitions;

        public override IBuilder Builder => EcsBuilder.Instance;
        public override IExternalDragNDropHandler DragNDropHandler => m_NDropHandler
        ?? (m_NDropHandler = new DragNDropEcsHandler());
        internal Dictionary<INodeModel, IEnumerable<ComponentDefinition>> ComponentDefinitions => m_ComponentDefinitions
        ?? (m_ComponentDefinitions = new Dictionary<INodeModel, IEnumerable<ComponentDefinition>>());

        public Dictionary<IFunctionModel, HashSet<ComponentQueryDeclarationModel>> EntryPointsToQueries
        {
            get => m_EntryPointsToQueries ?? (m_EntryPointsToQueries = new Dictionary<IFunctionModel, HashSet<ComponentQueryDeclarationModel>>());
            set => m_EntryPointsToQueries = value;
        }

        public override IEnumerable<string> PropertiesVisibleInGraphInspector()
        {
            yield return nameof(UseJobSystem);
        }

        public override void PreProcessGraph(VSGraphModel graphModel)
        {
            if (m_EntryPointsToQueries == null)
                m_EntryPointsToQueries = new Dictionary<IFunctionModel, HashSet<ComponentQueryDeclarationModel>>();
            else
                m_EntryPointsToQueries.Clear();
            new PortInitializationTraversal
            {
                Callbacks =
                {
                    n =>
                    {
                        if (n is IIteratorStackModel iterator)
                        {
                            var key = iterator.OwningFunctionModel ?? iterator;
                            if (key == null || iterator.ComponentQueryDeclarationModel == null)
                                return;
                            if (!EntryPointsToQueries.TryGetValue(key, out var queries))
                            {
                                queries = new HashSet<ComponentQueryDeclarationModel> { iterator.ComponentQueryDeclarationModel };
                                EntryPointsToQueries.Add(key, queries);
                            }
                            else
                                queries.Add(iterator.ComponentQueryDeclarationModel);
                        }
                    }
                }
            }.VisitGraph(graphModel);
        }

        [MenuItem("Assets/Create/Visual Script/ECS Graph")]
        public static void CreateEcsGraph()
        {
            VseWindow.CreateGraphAsset<EcsStencil>();
        }

        public override ISearcherDatabaseProvider GetSearcherDatabaseProvider()
        {
            return m_SearcherDatabaseProvider ?? (m_SearcherDatabaseProvider = new EcsSearcherDatabaseProvider(this));
        }

        public override ISearcherFilterProvider GetSearcherFilterProvider()
        {
            return m_SearcherFilterProvider ?? (m_SearcherFilterProvider = new EcsSearcherFilterProvider(this));
        }

        public override TypeHandle GetThisType()
        {
            return typeof(JobComponentSystem).GenerateTypeHandle(this);
        }

        public override ITranslator CreateTranslator()
        {
            return new RoslynEcsTranslator(this);
        }

        public override IBlackboardProvider GetBlackboardProvider()
        {
            return m_BlackboardProvider ?? (m_BlackboardProvider = new BlackboardEcsProvider(this));
        }

        protected override GraphContext CreateGraphContext()
        {
            return new EcsGraphContext();
        }

        internal void ClearComponentDefinitions()
        {
            m_ComponentDefinitions = new Dictionary<INodeModel, IEnumerable<ComponentDefinition>>();
        }

        public override void OnCompilationSucceeded(VSGraphModel graphModel, CompilationResult results)
        {
            World world = World.Active;
            if (!EditorApplication.isPlaying || world == null)
                return;

            ComponentSystemBase mgr = world.Systems
                .FirstOrDefault(m => m.GetType().Name == graphModel.TypeName);

            if (mgr != null)
            {
                // AFAIK the current frame's loop is already scheduled, so deleting the manager will flag
                // it as nonexistent but won't remove it. to avoid a console error about it, disable it first
                mgr.Enabled = false;
                world.DestroySystem(mgr);
            }

            if (graphModel.State == ModelState.Disabled)
                return;

            Type t = LiveCompileGraph(graphModel, results);
            if (t == null)
                return;

            var newSystem = world.CreateSystem(t);

            var groups = TypeManager.GetSystemAttributes(t, typeof(UpdateInGroupAttribute));
            if (groups.Length == 0) // default group is Simulation
            {
                var groupSystem = world.GetExistingSystem<SimulationSystemGroup>();
                groupSystem.AddSystemToUpdateList(newSystem);
                groupSystem.SortSystemUpdateList();
            }
            else
            {
                for (int g = 0; g < groups.Length; ++g)
                {
                    var updateInGroupAttribute = (UpdateInGroupAttribute)groups[g];
                    Assert.IsNotNull(updateInGroupAttribute);
                    var groupSystem = (ComponentSystemGroup)world.GetExistingSystem(updateInGroupAttribute.GroupType);
                    groupSystem.AddSystemToUpdateList(newSystem);
                    groupSystem.SortSystemUpdateList();
                }
            }

            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(world);
        }

        public static Type LiveCompileGraph(VSGraphModel graphModel, CompilationResult results, bool includeVscriptingAssemblies = false)
        {
            var originalBurstCompilationOption = BurstCompiler.Options.EnableBurstCompilation;
            BurstCompiler.Options.EnableBurstCompilation = false;

            VseUtility.RemoveLogEntries();
            var graphModelTypeName = graphModel.TypeName;

            string src = results.sourceCode[0];

            // TODO: refactor
            var assemblies = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                where !domainAssembly.IsDynamic &&
                (includeVscriptingAssemblies || !domainAssembly.FullName.Contains("VisualScripting.Ecs.Editor")) && // TODO: hack to avoid dll lock during test exec
                domainAssembly.Location != ""
                select File.OpenRead(domainAssembly.Location)).ToArray();

            Script<object> s = CSharpScript.Create(src, ScriptOptions.Default.AddReferences(
                assemblies.Select(stream => MetadataReference.CreateFromStream(stream))));

            foreach (var fileStream in assemblies)
                fileStream.Close();

            bool abort = false;
            foreach (Diagnostic diagnostic in s.Compile().Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                abort = true;
                VseUtility.LogSticky(LogType.Error, LogOption.NoStacktrace, diagnostic.ToString());
            }

            if (abort)
                return null;

            string newString = $"typeof({graphModelTypeName})";
            s = s.ContinueWith(newString);

            Type t = s.RunAsync().Result.ReturnValue as Type;

            // Best theory: Roslyn opens a lot of file handles and let the finalizers close them, which is
            // non-deterministic as it is triggered by the GC whenever it runs. Hopefully this will force the release
            // of all the references' file handles and avoid the 'Too many files' and 'IOError 4' issues
            GC.Collect();

            // The type manager now complains that ' All ComponentType must be known at compile time'.
            // re-initialize it so it finds the newly compiled type
            TypeManager.Shutdown();
            TypeManager.Initialize();

            BurstCompiler.Options.EnableBurstCompilation = originalBurstCompilationOption;

            return t;
        }

        public override string GetSourceFilePath(VSGraphModel graphModel)
        {
            return Path.Combine(ModelUtility.GetAssemblyRelativePath(), graphModel.TypeName + ".cs");
        }

        public override void RegisterReducers(Store store)
        {
            EcsReducers.Register(store);
        }

        public override Type GetConstantNodeModelType(TypeHandle typeHandle)
        {
            if (s_TypeToConstantNodeModelTypeCache == null)
            {
                s_TypeToConstantNodeModelTypeCache = new Dictionary<TypeHandle, Type>
                {
                    { typeof(float2).GenerateTypeHandle(this), typeof(Float2ConstantModel) },
                    { typeof(float3).GenerateTypeHandle(this), typeof(Float3ConstantModel) },
                    { typeof(float4).GenerateTypeHandle(this), typeof(Float4ConstantModel) },
                };
            }

            return s_TypeToConstantNodeModelTypeCache.TryGetValue(typeHandle, out var type)
                ? type
                : base.GetConstantNodeModelType(typeHandle);
        }

        public static bool IsValidGameObjectComponentType(Type type)
        {
            return type != null && typeof(Component).IsAssignableFrom(type) && !typeof(ComponentDataProxyBase).IsAssignableFrom(type);
        }

        public override IEnumerable<INodeModel> SpawnAllNodes(VSGraphModel graph)
        {
            return NodeSpawner.SpawnAllNodeModelsInGraph(graph);
        }
    }

    class EcsGraphContext : GraphContext
    {
        readonly HashSet<MemberInfoValue> m_BlacklistedMembers;

        public EcsGraphContext()
        {
            m_BlacklistedMembers = new HashSet<MemberInfoValue>();

            var asm = typeof(float3).Assembly;
            foreach (var type in asm.GetExportedTypes())
            {
                if (type.Name.StartsWith("float"))
                {
                    foreach (var property in type.GetProperties())
                    {
                        if (property.GetCustomAttribute<EditorBrowsableAttribute>()?.State == EditorBrowsableState.Never)
                            m_BlacklistedMembers.Add(property.ToMemberInfoValue(CSharpTypeSerializer));
                    }
                }
            }

            CSharpTypeSerializer.typeRenames.Add(
                "Unity.Transforms.Position, Unity.Transforms, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
                "Unity.Transforms.Translation, Unity.Transforms, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"
            );
        }

        public override bool MemberAllowed(MemberInfoValue value)
        {
            return !m_BlacklistedMembers.Contains(value);
        }
    }

    class EcsBuilder : IBuilder
    {
        public static IBuilder Instance = new EcsBuilder();
        public void Build(IEnumerable<GraphAssetModel> vsGraphAssetModels, Action<string, CompilerMessage[]> roslynCompilationOnBuildFinished)
        {
            VseUtility.RemoveLogEntries();
            foreach (GraphAssetModel vsGraphAssetModel in vsGraphAssetModels)
            {
                VSGraphModel graphModel = (VSGraphModel)vsGraphAssetModel.GraphModel;
                var t = graphModel.Stencil.CreateTranslator();

                try
                {
                    // important for codegen, otherwise most of it will be skipped
                    graphModel.Stencil.PreProcessGraph(graphModel);
                    var result = t.TranslateAndCompile(graphModel, AssemblyType.Source, CompilationOptions.Default);
                    var graphAssetPath = AssetDatabase.GetAssetPath(vsGraphAssetModel);
                    foreach (var error in result.errors)
                        VseUtility.LogSticky(LogType.Error, LogOption.None, error.ToString(), $"{graphAssetPath}@{error.sourceNodeGuid}", vsGraphAssetModel.GetInstanceID());
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}
