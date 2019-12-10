using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using VisualScripting.Entities.Runtime;

namespace Packages.VisualScripting.Editor.Stencils
{
    static class GraphElementSearcherDatabaseExtensions
    {
        static readonly Vector2 k_StackOffset = new Vector2(320, 120);
        const string k_ControlFlow = "Control Flow";
        const string k_LoopStack = "... Loop Stack";

        public static GraphElementSearcherDatabase AddEcsControlFlows(this GraphElementSearcherDatabase db)
        {
            db.AddIfCondition(GraphElementSearcherDatabase.IfConditionMode.Basic);
            db.AddIfCondition(GraphElementSearcherDatabase.IfConditionMode.Advanced);
            db.AddIfCondition(GraphElementSearcherDatabase.IfConditionMode.Complete);

            var loopTypes = TypeCache.GetTypesDerivedFrom<LoopStackModel>()
                .Where(t => !t.IsAbstract && !(t == typeof(CoroutineStackModel)))
                .Concat(TypeCache.GetTypesDerivedFrom<ICoroutine>().Where(t => t.AssemblyQualifiedName != null
                    && !t.AssemblyQualifiedName.Contains("VisualScripting.Ecs.Editor.Tests")));

            foreach (var loopType in loopTypes)
            {
                var isCoroutine = typeof(ICoroutine).IsAssignableFrom(loopType);
                var name = isCoroutine ? VseUtility.GetTitle(loopType) : $"{VseUtility.GetTitle(loopType)}{k_LoopStack}";

                db.Items.AddAtPath(new StackNodeModelSearcherItem(
                    new ControlFlowSearcherItemData(loopType),
                    data =>
                    {
                        var stackModel = (StackBaseModel)data.StackModel;
                        var graphModel = (VSGraphModel)stackModel.GraphModel;
                        var stackPosition = stackModel.Position + k_StackOffset;
                        var type = isCoroutine ? typeof(CoroutineStackModel) : loopType;
                        var guidIndex = 0;

                        var loopStack = graphModel.CreateLoopStack(
                            type,
                            stackPosition,
                            data.SpawnFlags,
                            data.GuidAt(guidIndex++));

                        var loopNode = loopStack.CreateLoopNode(
                            stackModel,
                            data.Index,
                            data.SpawnFlags,
                            PredefineSetup,
                            data.GuidAt(guidIndex));

                        var edge = data.SpawnFlags.IsOrphan()
                            ? graphModel.CreateOrphanEdge(loopStack.InputPort, loopNode.OutputPort)
                            : graphModel.CreateEdge(loopStack.InputPort, loopNode.OutputPort);

                        return new IGraphElementModel[] { loopNode, loopStack, edge };

                        void PredefineSetup(NodeModel model)
                        {
                            if (model is CoroutineNodeModel coroutineNodeModel)
                                coroutineNodeModel.CoroutineType = loopType.GenerateTypeHandle(db.Stencil);
                        }
                    },
                    name),
                    k_ControlFlow);
            }

            return db;
        }

        public static GraphElementSearcherDatabase AddOnEventNodes(this GraphElementSearcherDatabase db)
        {
            var types = TypeCache.GetTypesWithAttribute<DotsEventAttribute>();
            foreach (var eventType in types)
            {
                var path = "Events";
                var type = typeof(OnEventNodeModel);
                var category = eventType.GetCustomAttribute<DotsEventAttribute>().Category;
                if (category != null)
                    path += $"/{category}";

                {
                    string name = "On " + eventType.Name;
                    db.Items.AddAtPath(new GraphNodeModelSearcherItem(
                        new NodeSearcherItemData(type),
                        data => data.CreateNode<OnEventNodeModel>(name, n => n.EventType = eventType.GenerateTypeHandle(db.Stencil)),
                        name
                        ), path);
                }
                {
                    string name = "Send " + eventType.Name;
                    db.Items.AddAtPath(new StackNodeModelSearcherItem(
                        new NodeSearcherItemData(type),
                        data => data.CreateNode<SendEventNodeModel>(name, n => n.EventType = eventType.GenerateTypeHandle(db.Stencil)),
                        name
                        ), path);
                }
            }

            return db;
        }
    }
}
