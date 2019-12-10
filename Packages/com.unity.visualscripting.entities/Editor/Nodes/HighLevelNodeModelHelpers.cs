using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Mathematics;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    static class HighLevelNodeModelHelpers
    {
        static List<Type> s_PredefinedTypes = new List<Type>
        {
            typeof(double2),
            typeof(double3),
            typeof(double4),
            typeof(float2),
            typeof(float3),
            typeof(float4),
            typeof(int2),
            typeof(int3),
            typeof(int4),
            typeof(uint2),
            typeof(uint3),
            typeof(uint4)
        };

        internal static IEnumerable<Tuple<string, TypeHandle>> GetDataInputsFromComponentType(
            Stencil stencil,
            TypeHandle componentTypeHandle
        )
        {
            var componentType = componentTypeHandle.Resolve(stencil);
            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var type = HasSinglePredefinedFieldType(fields)
                ? fields[0].FieldType
                : componentType;

            return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => new Tuple<string, TypeHandle>(f.Name, f.FieldType.GenerateTypeHandle(stencil)));
        }

        internal static bool HasSinglePredefinedFieldType(IReadOnlyList<FieldInfo> fields)
        {
            return fields.Count == 1 && s_PredefinedTypes.Contains(fields[0].FieldType);
        }
    }
}
