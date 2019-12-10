using System;
using Unity.Mathematics;
using UnityEditor.VisualScripting.Model;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.VisualScripting.Editor.ConstantEditor;

namespace UnityEditor.VisualScripting.Entities.Editor
{
    [GraphtoolsExtensionMethods]
    internal static class ConstantEditorExtensions
    {
        public static VisualElement BuildFloat2Editor(this IConstantEditorBuilder builder, ConstantNodeModel<float2> f)
        {
            return builder.MakeFloatVectorEditor(f, 2,
                (vec, i) => vec[i], (ref float2 data, int i, float value) => data[i] = value);
        }

        public static VisualElement BuildFloat3Editor(this IConstantEditorBuilder builder, ConstantNodeModel<float3> f)
        {
            return builder.MakeFloatVectorEditor(f, 3,
                (vec, i) => vec[i], (ref float3 data, int i, float value) => data[i] = value);
        }

        public static VisualElement BuildFloat4Editor(this IConstantEditorBuilder builder, ConstantNodeModel<float4> f)
        {
            return builder.MakeFloatVectorEditor(f, 4,
                (vec, i) => vec[i], (ref float4 data, int i, float value) => data[i] = value);
        }
    }
}
