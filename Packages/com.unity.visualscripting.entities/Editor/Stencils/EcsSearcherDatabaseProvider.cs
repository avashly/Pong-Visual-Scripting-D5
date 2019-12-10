using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.VisualScripting;

namespace Packages.VisualScripting.Editor.Stencils
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ConstantSearcherItemAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class MethodSearcherItemAttribute : Attribute {}

    public class EcsSearcherDatabaseProvider : ClassSearcherDatabaseProvider
    {
        static readonly IEnumerable<Type> k_PredefinedSearcherTypes = new List<Type>
        {
            typeof(Time),
            typeof(math)
        };

        static readonly IEnumerable<Type> k_ConstantTypes = new List<Type>
        {
            typeof(bool),
            typeof(double),
            typeof(int),
            typeof(float),
            typeof(string),
            typeof(Enum),
            typeof(InputName),
            typeof(float2),
            typeof(float3),
            typeof(float4),
            typeof(Color)
        };

        readonly Stencil m_Stencil;
        List<SearcherDatabase> m_GraphElementsSearcherDatabases;
        List<SearcherDatabase> m_TypeSearcherDatabases;
        IEnumerable<Type> m_CustomTypes;
        int m_AssetVersion = AssetWatcher.Version;
        int m_AssetModificationVersion = AssetModificationWatcher.Version;

        public EcsSearcherDatabaseProvider(Stencil stencil)
            : base(stencil)
        {
            m_Stencil = stencil;
        }

        public override List<SearcherDatabase> GetGraphElementsSearcherDatabases()
        {
            if (AssetWatcher.Version != m_AssetVersion || AssetModificationWatcher.Version != m_AssetModificationVersion)
            {
                m_AssetVersion = AssetWatcher.Version;
                m_AssetModificationVersion = AssetModificationWatcher.Version;
                ClearGraphElementsSearcherDatabases();
            }

            return m_GraphElementsSearcherDatabases ?? (m_GraphElementsSearcherDatabases = new List<SearcherDatabase>
            {
                new GraphElementSearcherDatabase(m_Stencil)
                    .AddNodesWithSearcherItemAttribute()
                    .AddOnEventNodes()
                    .AddStickyNote()
                    .AddEmptyFunction()
                    .AddStack()
                    .AddConstants(k_ConstantTypes.Concat(GetCustomConstantTypes()))
                    .AddInlineExpression()
                    .AddUnaryOperators()
                    .AddBinaryOperators()
                    .AddEcsControlFlows()
                    .AddMembers(GetCustomTypes(), MemberFlags.Method, BindingFlags.Static | BindingFlags.Public)
                    .AddMembers(
                        k_PredefinedSearcherTypes,
                        MemberFlags.Constructor | MemberFlags.Field | MemberFlags.Method | MemberFlags.Property | MemberFlags.Extension,
                        BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public
                    )
                    .AddMethods(GetCustomMethods())
                    .AddMacros()
                    .Build()
            });
        }

        IEnumerable<Type> GetCustomTypes()
        {
            return m_CustomTypes ?? (m_CustomTypes = TypeCache.GetTypesWithAttribute<NodeAttribute>()
                    .Where(t => !t.IsInterface)
                    .ToList());
        }

        public override List<SearcherDatabase> GetDynamicSearcherDatabases(IPortModel portModel)
        {
            var dbs = base.GetDynamicSearcherDatabases(portModel);
            if (portModel == null) // graph searcher
            {
                var rngDb = new GraphElementSearcherDatabase(m_Stencil);
                string title = RandomNodeModel.k_DefaultRngTitle;
                var node = new GraphNodeModelSearcherItem(
                    new NodeSearcherItemData(typeof(RandomNodeModel)),
                    data => data.CreateNode<RandomNodeModel>(title),
                    title
                );
                rngDb.Items.Add(node);
                dbs.Add(rngDb.Build());
                return dbs;
            }

            // input to graph
            if (RandomNodeModel.RngByType.TryGetValue(portModel.DataType.Resolve(portModel.NodeModel.GraphModel.Stencil), out var methods))
            {
                var rngDb = new GraphElementSearcherDatabase(m_Stencil);
                foreach (var method in methods)
                {
                    if (method == RandomNodeModel.DefaultMethod)
                        continue;
                    string title = RandomNodeModel.MakeTitle(method);
                    var node = new GraphNodeModelSearcherItem(
                        new NodeSearcherItemData(typeof(RandomNodeModel)),
                        data => data.CreateNode<RandomNodeModel>(title),
                        title
                    );
                    rngDb.Items.Add(node);
                }
                dbs.Add(rngDb.Build());
            }
            return dbs;
        }

        public override void ClearGraphElementsSearcherDatabases()
        {
            m_GraphElementsSearcherDatabases = null;
        }

        public override List<SearcherDatabase> GetTypesSearcherDatabases()
        {
            return m_TypeSearcherDatabases ?? (m_TypeSearcherDatabases = new List<SearcherDatabase>
            {
                new EcsTypeSearcherDatabase(m_Stencil, m_Stencil.GetAssembliesTypesMetadata())
                    .AddComponents()
                    .AddMonoBehaviourComponents()
                    .AddEnums()
                    .AddClasses()
                    .Build()
            });
        }

        public IEnumerable<SearcherDatabase> GetCriteriaSearcherDatabases(ComponentQueryDeclarationModel query)
        {
            return new List<SearcherDatabase> { new CriterionSearcherDatabase(m_Stencil, query).Build() };
        }

        static IEnumerable<Type> GetCustomConstantTypes()
        {
            return TypeCache.GetMethodsWithAttribute<ConstantSearcherItemAttribute>()
                .SelectMany(m => (IEnumerable<Type>)m.Invoke(null, new object[] {}));
        }

        static IEnumerable<MethodInfo> GetCustomMethods()
        {
            return TypeCache.GetMethodsWithAttribute<MethodSearcherItemAttribute>()
                .SelectMany(m => (IEnumerable<MethodInfo>)m.Invoke(null, new object[] {}));
        }
    }
}
