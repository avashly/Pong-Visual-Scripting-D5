using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    class ComponentPortsDescription
    {
        public TypeHandle Component { get; }

        List<string> m_FieldNames;
        string m_Prefix;

        public string GetFieldId(string fieldName)
        {
            return m_Prefix == null ? fieldName : m_Prefix + "." + fieldName;
        }

        ComponentPortsDescription(TypeHandle component, int capacity, string prefix = null)
        {
            Component = component;
            m_FieldNames = new List<string>(capacity);
            m_Prefix = prefix;
        }

        public IEnumerable<string> GetFieldIds()
        {
            return m_FieldNames.Select(GetFieldId);
        }

        public static ComponentPortsDescription FromData(TypeHandle componentType, IReadOnlyList<Tuple<string, TypeHandle>> componentDescriptions, string prefix = null)
        {
            var result = new ComponentPortsDescription(componentType, componentDescriptions.Count, prefix)
            { m_FieldNames = componentDescriptions.Select(t => t.Item1).ToList() };
            return result;
        }
    }
}
