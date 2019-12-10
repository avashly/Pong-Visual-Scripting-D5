using System;
using System.Collections.Generic;
using UnityEditor.EditorCommon.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.Editor
{
//    [CustomEditor(typeof(AbstractNodeAsset), true)]
    class NodeModelInspector : GraphElementModelInspector
    {
        bool m_InputsCollapsed = true;
        bool m_OutputsCollapsed = true;

        protected override void GraphElementInspectorGUI(Action refreshUI)
        {
//            if (target is AbstractNodeAsset asset)
//            {
//                var node = asset.Model;
//                node.HasUserColor = EditorGUILayout.Toggle("Set Custom Color", node.HasUserColor);
//                if (node.HasUserColor)
//                    node.Color = EditorGUILayout.ColorField("Node Color", node.Color);
//
//                DisplayPorts(node);
//            }
        }

        protected void DisplayPorts(INodeModel node)
        {
            GUI.enabled = false;

            m_InputsCollapsed = EditorGUILayout.Foldout(m_InputsCollapsed, "Inputs");
            if (m_InputsCollapsed)
                DisplayPorts(node.GraphModel.Stencil, node.InputsByDisplayOrder);

            m_OutputsCollapsed = EditorGUILayout.Foldout(m_OutputsCollapsed, "Outputs");
            if (m_OutputsCollapsed)
                DisplayPorts(node.GraphModel.Stencil, node.OutputsByDisplayOrder);

            GUI.enabled = true;
        }

        static void DisplayPorts(Stencil stencil, IEnumerable<IPortModel> ports)
        {
            EditorGUI.indentLevel++;
            foreach (var port in ports)
            {
                string details = port.PortType + " ( " + port.DataType.GetMetadata(stencil).FriendlyName + " )";
                EditorGUILayout.LabelField(port.UniqueId, details);
                if (Unsupported.IsDeveloperMode())
                {
                    EditorGUI.indentLevel++;
                    foreach (IEdgeModel edgeModel in port.GraphModel.GetEdgesConnections(port))
                    {
                        int edgeIndex = edgeModel.GraphModel.EdgeModels.IndexOf(edgeModel);
                        EditorGUILayout.LabelField(edgeIndex.ToString(), edgeModel.OutputPortModel.ToString());
                        EditorGUILayout.LabelField("to", edgeModel.InputPortModel.ToString());
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUI.indentLevel--;
        }
    }
}
