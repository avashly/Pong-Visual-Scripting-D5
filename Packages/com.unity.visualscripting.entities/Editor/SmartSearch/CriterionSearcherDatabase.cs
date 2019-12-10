using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.SmartSearch
{
    class CriterionSearcherDatabase
    {
        static readonly BinaryOperatorKind[] k_SupportedOperators =
        {
            BinaryOperatorKind.Equals,
            BinaryOperatorKind.NotEqual,
            BinaryOperatorKind.LessThan,
            BinaryOperatorKind.LessThanOrEqual,
            BinaryOperatorKind.GreaterThan,
            BinaryOperatorKind.GreaterThanOrEqual
        };

        static readonly Type[] k_SupportedTypes =
        {
            typeof(int),
            typeof(bool),
            typeof(float),
            typeof(double),
            typeof(string),
            typeof(Color),
            typeof(Vector2),
            typeof(float2),
            typeof(Vector3),
            typeof(float3),
            typeof(Vector4),
            typeof(float4)
        };

        // This handles Rotation/Value/value/x
        const int k_Depth = 4;

        Stencil m_Stencil;
        ComponentQueryDeclarationModel m_Query;

        public CriterionSearcherDatabase(Stencil stencil, ComponentQueryDeclarationModel query)
        {
            m_Stencil = stencil;
            m_Query = query;
        }

        public SearcherDatabase Build()
        {
            var items = new List<SearcherItem>();
            foreach (var component in m_Query.Components)
            {
                var handle = component.Component.TypeHandle;
                var componentItem = new TypeSearcherItem(handle, handle.Name(m_Stencil));
                CreateFieldItems(componentItem, handle.Resolve(m_Stencil), k_Depth);

                // We only display components with fields
                if (componentItem.HasChildren)
                    items.Add(componentItem);
            }

            return SearcherDatabase.Create(items, "", false);
        }

        void CreateFieldItems(SearcherItem root, Type type, int depth)
        {
            var operators = GetOperatorsFromType(type);
            foreach (var op in operators)
            {
                root.AddChild(op);
            }

            depth--;
            if (depth <= 0)
                return;

            var infos = GetMemberInfos(type);
            foreach (var info in infos)
            {
                var itemType = info.MemberType == MemberTypes.Field
                    ? (info as FieldInfo)?.FieldType
                    : (info as PropertyInfo)?.PropertyType;
                var item = new TypeSearcherItem(itemType.GenerateTypeHandle(m_Stencil), info.Name);

                CreateFieldItems(item, itemType, depth);

                // We only display item with binary operators
                if (item.HasChildren)
                    root.AddChild(item);
            }
        }

        static IEnumerable<MemberInfo> GetMemberInfos(Type type)
        {
            // We don't want the extra value__ field of an Enum
            if (type.IsEnum)
                return Enumerable.Empty<MemberInfo>();

            // We don't want all the properties from math types (ie. xxy, xxx, xyy, etc)
            var ns = type.Namespace ?? string.Empty;
            return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Concat(ns.StartsWith("Unity.Mathematics")
                    ? Enumerable.Empty<MemberInfo>()
                    : type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetIndexParameters().Length == 0)); // We don't want things like Vector2.this[int]
        }

        static IEnumerable<SearcherItem> GetOperatorsFromType(Type type)
        {
            // We cannot support all the types because of PropertyFields (see ConstantEditorExtensions)
            // that is drawn to represent Criterion type
            if (!type.IsEnum && !k_SupportedTypes.Contains(type))
                return Enumerable.Empty<SearcherItem>();

            return TypeSystem.GetOverloadedBinaryOperators(type)
                .Where(k => k_SupportedOperators.Contains(k))
                .Select(o => new CriterionSearcherItem(o));
        }
    }
}
