using System;
using System.Collections.Generic;
using System.Linq;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor.Searcher;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.SmartSearch
{
    static class EcsSearcherServices
    {
        internal static void ShowCriteria(ComponentQueryDeclarationModel query, string title, Vector2 position,
            Action<TypeHandle, TypeMember, BinaryOperatorKind> onComponentSelected)
        {
            var provider = (EcsSearcherDatabaseProvider)query.GraphModel.Stencil.GetSearcherDatabaseProvider();
            var databases = provider.GetCriteriaSearcherDatabases(query);
            var criteriaAdapter = new SimpleSearcherAdapter(title);
            var searcher = new Searcher.Searcher(databases, criteriaAdapter);

            SearcherWindow.Show(
                EditorWindow.focusedWindow,
                searcher,
                item => OnItemSelected(item, onComponentSelected),
                position,
                null);
        }

        static bool OnItemSelected(SearcherItem item, Action<TypeHandle, TypeMember, BinaryOperatorKind> callback)
        {
            if (!(item is CriterionSearcherItem criterionItem))
                return false;

            var componentType = TypeHandle.Unknown;
            var path = new Stack<string>();
            var parent = item.Parent;
            var memberType = parent is TypeSearcherItem tsi ? tsi.Type : TypeHandle.Unknown;

            while (parent != null)
            {
                if (parent.Parent == null && parent is TypeSearcherItem typeItem)
                    componentType = typeItem.Type;
                else
                    path.Push(parent.Name);

                parent = parent.Parent;
            }

            var op = criterionItem.Operator;
            callback(componentType, new TypeMember(memberType, path.ToList()), op);
            return true;
        }
    }
}
