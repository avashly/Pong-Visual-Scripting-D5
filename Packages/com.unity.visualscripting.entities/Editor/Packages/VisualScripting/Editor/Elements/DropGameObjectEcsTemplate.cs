using System;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Elements
{
    class DropGameObjectEcsTemplate : IGraphTemplateFromGameObject
    {
        public GameObject GameObject { get; }

        readonly Vector2 m_Position;
        public Type StencilType => typeof(EcsStencil);

        string m_AssetName;

        static readonly Vector2 k_GroupOffset = new Vector2(145, -8);

        public DropGameObjectEcsTemplate(GameObject gameObject)
            : this(gameObject, Vector2.zero)
        {
        }

        public DropGameObjectEcsTemplate(GameObject gameObject, Vector2 position)
        {
            GameObject = gameObject;
            m_Position = position;
        }

        public void InitBasicGraph(VSGraphModel graphModel)
        {
            var query = graphModel.CreateQueryFromGameObject(GameObject);
            var queryInstance = graphModel.CreateVariableNode(query, m_Position);
            var node = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("update", m_Position + k_GroupOffset);

            graphModel.CreateEdge(node.InstancePort, queryInstance.OutputPort);
        }
    }
}
