using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.Model.Translators;
using UnityEditor.VisualScriptingTests;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VisualScripting;

namespace UnityEditor.VisualScriptingECSTests
{
    public abstract class EndToEndCodeGenBaseFixture : BaseFixture
    {
        public enum CodeGenMode
        {
            NoJobs,
            Jobs,
            // TODO: fix tracing codegen to pass all tests
            NoJobsTracing,
            JobsTracing,
        }

        World m_World;
        protected EntityManager m_EntityManager;

        protected override Type CreatedGraphType => typeof(EcsStencil);
        protected Type m_SystemType;
        protected const int k_EntityCount = 100;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            SetUpWorld(false);
        }

        [TearDown]
        public override void TearDown()
        {
            TearDownWorld();

            base.TearDown();

            GC.Collect();
        }

        void SetUpWorld(bool hasTracing)
        {
            m_World = new World("test");
            m_EntityManager = m_World.EntityManager;
            if (hasTracing)
                m_World.CreateSystem<TracingRecorderSystem>();
        }

        void TearDownWorld()
        {
            TypeManager.Shutdown();
            m_World.Dispose();
        }

        public delegate void StepDelegate(EntityManager entityManager, Entity[] entities);

        internal static StepDelegate EachEntity(Action<EntityManager, int, Entity> del)
        {
            return (entityManager, entities) =>
            {
                for (var index = 0; index < entities.Length; index++)
                {
                    Entity entity = entities[index];
                    del(entityManager, index, entity);
                }
            };
        }

        protected void SetupTestGraphMultipleFrames(CodeGenMode mode, Action<VSGraphModel> setupGraph, params StepDelegate[] setup)
        {
            m_SystemType = null;
            setupGraph(GraphModel);
            CompilationResult results;

            try
            {
                m_SystemType = CompileGraph(mode, out results);
            }
            finally
            {
                GC.Collect();
            }

            // Something fishy is going on here, the TypeManager is throwing a fit when adding new ComponentData through
            // live compilation.  Shutting down the TypeManager and re-initializing seems like the way to circumvent the
            // issue, but it does not seem like it's enough.
            // Tearing the world down (along with the TypeManager), and recreating it, works.
            TearDownWorld();
            bool hasTracing = mode == CodeGenMode.JobsTracing;
            SetUpWorld(hasTracing);

            if (setup.Length > 0)
            {
                Entity[] entities = new Entity[k_EntityCount];
                for (var index = 0; index < entities.Length; index++)
                    entities[index] = m_EntityManager.CreateEntity();
                setup[0](m_EntityManager, entities);
            }

            try
            {
                for (int i = 1; i < setup.Length; i++)
                {
                    TestSystem(m_SystemType);

                    setup[i](m_EntityManager, m_EntityManager.GetAllEntities().ToArray());
                }
            }
            catch
            {
                Debug.Log(FormatCode(results));
                throw;
            }
        }

        protected void SetupTestGraph(CodeGenMode mode, Action<VSGraphModel> setupGraph, Action<EntityManager, int, Entity> setup, Action<EntityManager, int, Entity> checkWorld)
        {
            SetupTestGraphMultipleFrames(mode, setupGraph, EachEntity(setup), EachEntity(checkWorld));
        }

        protected void SetupTestSystem(Type systemType, Func<EntityManager, Entity> setup, Action<Entity, EntityManager> checkWorld)
        {
            var entities = setup(m_EntityManager);
            TestSystem(systemType);
            checkWorld(entities, m_EntityManager);
        }

        Type CompileGraph(CodeGenMode mode, out CompilationResult results)
        {
            results = default;
            RoslynEcsTranslator translator = (RoslynEcsTranslator)GraphModel.CreateTranslator();
            translator.AllowNoJobsFallback = false;

            // because of the hack in the translator constructor, override right after
            ((EcsStencil)Stencil).UseJobSystem = mode == CodeGenMode.Jobs || mode == CodeGenMode.JobsTracing;

            var compilationOptions = CompilationOptions.LiveEditing;
            if (mode == CodeGenMode.JobsTracing || mode == CodeGenMode.NoJobsTracing)
                compilationOptions |= CompilationOptions.Tracing;
            results = GraphModel.Compile(AssemblyType.Memory, translator,
                compilationOptions);

            var results2 = results;
            Assert.That(results?.status, Is.EqualTo(CompilationStatus.Succeeded),
                () => $"Compilation failed, errors: {String.Join("\n", results2?.errors)}\r\n{FormatCode(results2)}");
            return EcsStencil.LiveCompileGraph(GraphModel, results, includeVscriptingAssemblies: true);
        }

        static string FormatCode(CompilationResult results)
        {
            string code = null;
            if (results?.sourceCode != null && results.sourceCode.Length != 0)
            {
                code = String.Join(Environment.NewLine, results.sourceCode[(int)SourceCodePhases.Initial]
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                    .Select((s, i) => $"{i + 1,4} {s}"));
            }

            return code;
        }

        void TestSystem(Type systemType)
        {
            ComponentSystemBase system = m_World.CreateSystem(systemType);

            // Force System.OnUpdate to be executed
            var field = typeof(ComponentSystemBase).GetField("m_AlwaysUpdateSystem", BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(system, true);

            Assert.DoesNotThrow(system.Update);

            system.Complete();
            var endFrame = m_World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
            endFrame.Update();
            endFrame.Complete();
        }

        protected IVariableModel SetupQuery(VSGraphModel graph, string name, IEnumerable<Type> components) => SetupQuery(graph, name, components, out _);

        protected IVariableModel SetupQuery(VSGraphModel graph, string name, IEnumerable<Type> components, out ComponentQueryDeclarationModel query)
        {
            query = graph.CreateComponentQuery(name);
            foreach (var component in components)
                query.AddComponent(Stencil, component.GenerateTypeHandle(Stencil), ComponentDefinitionFlags.None);
            return graph.CreateVariableNode(query, Vector2.zero);
        }

        protected static OnUpdateEntitiesNodeModel SetupOnUpdate(GraphModel graph, IHasMainOutputPort query)
        {
            var onUpdate = graph.CreateNode<OnUpdateEntitiesNodeModel>("On Update", Vector2.zero);
            graph.CreateEdge(onUpdate.InstancePort, query.OutputPort);
            return onUpdate;
        }
    }
}
