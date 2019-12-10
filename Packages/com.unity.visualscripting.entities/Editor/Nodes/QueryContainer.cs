using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Stencils
{
    [Serializable]
    public class QueryContainer
    {
        [SerializeField]
        public List<QueryGroup> Groups;
        [SerializeField]
        public List<QueryComponent> Components;

        [SerializeField]
        public QueryGroup RootGroup;

        public QueryContainer(string name)
        {
            Groups = new List<QueryGroup>();
            RootGroup = new QueryGroup(name);
            Components = new List<QueryComponent>();
        }

        public string ToString(bool details)
        {
            var sb = new StringBuilder();
            int printedGroups = 0;

            int printedComponents = 0;
            Dump(sb, RootGroup, 0, details, ref printedGroups, ref printedComponents);
            if (details)
                sb.AppendLine($"Printed Groups: {printedGroups}/{Groups.Count+1}, Components: {printedComponents}/{Components.Count}");
            return sb.ToString();
        }

        public override string ToString() => ToString(true);

        public IEnumerable<QueryComponent> GetComponentsInQuery(QueryGroup g)
        {
            for (int i = 0; i < g.ComponentCount; i++)
            {
                yield return Components[g.ComponentStartIndex + i];
            }
        }

        public IEnumerable<QueryComponent> GetFlattenedComponentDefinitions()
        {
            IEnumerable<QueryComponent> GetComponentsRecursive(QueryGroup group)
            {
                for (int i = 0; i < group.GroupCount; i++)
                {
                    var g = Groups[group.GroupStartIndex + i];
                    foreach (var nested in GetComponentsRecursive(g))
                        yield return nested;
                }
                foreach (var nested in GetComponentsInQuery(group))
                    yield return nested;
            }

            return GetComponentsRecursive(RootGroup);
        }

        public void Dump()
        {
            Debug.Log(ToString());
        }

        void Dump(StringBuilder sb, QueryGroup group, int depth, bool showDetails, ref int printedGroupsCount, ref int printedComponentsCount)
        {
            sb.Append(new string(' ', depth * 2));
            printedGroupsCount++;
            sb.AppendLine(showDetails ? group.ToString() : group.Name);

            for (int i = 0, groupIndex = group.GroupStartIndex; i < group.GroupCount; i++)
            {
                Dump(sb, Groups[groupIndex], depth + 1, showDetails, ref printedGroupsCount, ref printedComponentsCount);
                groupIndex += 1 + Groups[groupIndex].TotalGroupCount;
            }

            for (int i = 0; i < group.ComponentCount; i++)
            {
                printedComponentsCount++;
                var index = group.ComponentStartIndex + i;
                sb.AppendLine(new string(' ', (depth + 1) * 2) + (index >= Components.Count ? "<OutOfBounds>" : Components[index].ToString()));
            }
        }

        int Recompute(QueryGroup group, int curGroupIdx, ref int curCompIndex)
        {
            group.GroupStartIndex = curGroupIdx + 1;
            group.TotalGroupCount = 0;
            var groupIndex = group.GroupStartIndex;
            for (int i = 0; i < group.GroupCount; i++)
            {
                group.TotalGroupCount += 1 + Recompute(Groups[groupIndex + group.TotalGroupCount], groupIndex + group.TotalGroupCount, ref curCompIndex);
            }


            group.ComponentStartIndex = curCompIndex;
            curCompIndex += group.ComponentCount;
            return group.TotalGroupCount;
        }

        void RecomputeAll()
        {
            int i = 0;
            Recompute(RootGroup, -1, ref i);
        }

        public void AddGroup(QueryGroup group, QueryGroup newGroup, int index = -1)
        {
            int insertIndex;
            if (index == -1)
                insertIndex = group.GroupStartIndex + group.TotalGroupCount;
            else
            {
                insertIndex = group.GroupStartIndex;
                for (int i = 0; i < index; i++)
                {
                    insertIndex += 1 + Groups[insertIndex].TotalGroupCount;
                }
            }
            Groups.Insert(insertIndex, newGroup);
            group.GroupCount++;
            RecomputeAll();
        }

        public void AddComponent(QueryGroup group, TypeHandle componentType)
        {
            AddComponent(group, new QueryComponent(componentType));
        }

        public void AddComponent(QueryGroup group, QueryComponent componentType)
        {
            if (GetComponentsInQuery(group).Any(x => x.Component.TypeHandle == componentType.Component.TypeHandle))
                return;

            if (Components == null)
                Components = new List<QueryComponent>();

            Components.Insert(group.ComponentStartIndex + group.ComponentCount, componentType);
            group.ComponentCount++;
            RecomputeAll();
        }

        int FindIndex(QueryGroup group, QueryGroup toFind)
        {
            return Groups.FindIndex(group.GroupStartIndex, group.GroupCount, g => g == toFind);
        }

        public void RemoveComponent(QueryGroup gc, TypeHandle toRemove)
        {
            for (int i = 0; i < gc.ComponentCount; i++)
            {
                if (Components[gc.ComponentStartIndex + i].Component.TypeHandle == toRemove)
                {
                    Components.RemoveAt(gc.ComponentStartIndex + i);
                    gc.ComponentCount--;
                    RecomputeAll();
                    return;
                }
            }
        }

        public void RemoveGroup(QueryGroup group, QueryGroup toRemove)
        {
            var i = FindIndex(group, toRemove);
            if (i == -1)
                return;
            RemoveGroupInternal(group, i);
            RecomputeAll();
        }

        void RemoveGroupInternal(QueryGroup group, int i)
        {
            var toRemove = Groups[i];

            Components.RemoveRange(toRemove.ComponentStartIndex, toRemove.ComponentCount);

            for (int j = toRemove.GroupCount - 1; j >= 0; j--)
                RemoveGroupInternal(toRemove, toRemove.GroupStartIndex + j);

            Groups.RemoveAt(i);
            toRemove.ComponentCount = 0;
            toRemove.GroupCount = 0;
            group.GroupCount--;
        }

        public void ReorderGroup(QueryGroup parent, QueryGroup toReorder, int newIndex)
        {
            int index = FindIndex(parent, toReorder);
            if (index == -1)
                throw new InvalidOperationException();

            index -= parent.GroupStartIndex;

            QueryComponent[] components = GetComponentsInQuery(toReorder).ToArray();
            RemoveGroupInternal(parent, index);
            RecomputeAll();

            Dump();
            if (newIndex > index)
                newIndex--;
            AddGroup(parent, toReorder, newIndex);
            foreach (var component in components)
                AddComponent(toReorder, component);
        }

        public IEnumerable<QueryGroup> GetSubGroups(QueryGroup group)
        {
            for (int i = 0, groupIndex = group.GroupStartIndex; i < group.GroupCount; i++)
            {
                yield return Groups[groupIndex];
                groupIndex += 1 + Groups[groupIndex].TotalGroupCount;
            }
        }

        public bool HasType(TypeHandle t) => Components.Any(c => c.Component.TypeHandle == t);

        public QueryComponent Find(ComponentDefinition componentDefinition, out QueryGroup owner)
        {
            owner = null;
            var found = Components?.FirstOrDefault(x => x.Component == componentDefinition);
            if (found != null)
            {
                QueryGroup FindGroup(QueryGroup targetGroup, QueryComponent component)
                {
                    if (GetComponentsInQuery(targetGroup).Contains(component))
                        return targetGroup;

                    foreach (var subGroup in GetSubGroups(targetGroup))
                    {
                        var inChildren = FindGroup(subGroup, component);
                        if (inChildren != null)
                            return inChildren;
                    }

                    return null;
                }

                owner = FindGroup(RootGroup, found);
            }
            return found;
        }

        public void ReorderComponent(ComponentDefinition toMove, ComponentDefinition target, bool insertAtEnd, out int oldIndex, out int newIndex)
        {
            oldIndex = -1;
            newIndex = -1;
            int targetIndex = -1;
            for (int i = 0; i < RootGroup.ComponentCount; i++)
            {
                var component = Components[RootGroup.GroupStartIndex + i];
                if (component.Component == toMove)
                    oldIndex = i;
                else if (component.Component == target)
                    targetIndex = i;
                if (oldIndex != -1 && targetIndex != -1)
                    break;
            }

            if (oldIndex == -1 || targetIndex == -1)
            {
                var issue = oldIndex == -1 ? "" : "target";
                Debug.LogError($"Could not find the {issue} component while reordering");
                return;
            }

            newIndex = targetIndex + (insertAtEnd ? 1 : 0);

            Components.Insert(newIndex, Components[oldIndex]);
            Components.RemoveAt(oldIndex + (targetIndex < oldIndex ? 1 : 0));
        }
    }

    [Serializable]
    public class QueryGroup
    {
        public string Name;

        public int GroupCount;
        public int GroupStartIndex { get; set; }
        public int TotalGroupCount { get; set; }


        public int ComponentCount;
        public int ComponentStartIndex { get; set; }
        public QueryGroup(string name)
        {
            Name = name;
        }

        public override string ToString()
        {
            return $"{Name} TGC{TotalGroupCount} G{GroupCount}@{GroupStartIndex}, C{ComponentCount}@{ComponentStartIndex}";
        }
    }

    [Serializable]
    public class QueryComponent
    {
        public ComponentDefinition Component;

        public QueryComponent(ComponentDefinition component)
        {
            Component = component;
        }

        public QueryComponent(TypeHandle componentType)
        {
            Component = new ComponentDefinition
            {
                IsShared = false,
                TypeHandle = componentType,
                Subtract = false,
            };
        }

        public bool ForceInsert { get; set; }

        public override string ToString()
        {
            var indexOf = Component.TypeHandle.Identification.IndexOf(',');
            return indexOf == -1 ? Component.TypeHandle.Identification : Component.TypeHandle.Identification.Substring(0, indexOf);
        }
    }
}
