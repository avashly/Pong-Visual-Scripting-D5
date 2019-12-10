using System;
using Packages.VisualScripting.Editor.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    class EcsGraphTemplate : ICreatableGraphTemplate
    {
        public Type StencilType => typeof(EcsStencil);
        public string GraphTypeName => "ECS Graph";
        public string DefaultAssetName => "ECSGraph";

        public void InitBasicGraph(VSGraphModel graphModel)
        {
            var query = graphModel.CreateComponentQuery("myQuery");
            var node = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("On Update Entities", Vector2.zero);
            var queryInstance = graphModel.CreateVariableNode(query, new Vector2(-145, 8));

            graphModel.CreateEdge(node.InstancePort, queryInstance.OutputPort);
        }
    }
}
