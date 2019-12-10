using System.Collections.Generic;
using Packages.VisualScripting.Editor.Stencils;
using Unity.Entities;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;
using VisualScripting.Entities.Runtime;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public static class NodeSpawner
    {
        public static List<NodeModel> SpawnAllNodeModelsInGraph(VSGraphModel graphModel)
        {
            List<NodeModel> spawnedNodes = new List<NodeModel>();
            Stencil stencil = graphModel.Stencil;
            StackModel stack;
            FunctionModel funcModel;
            OnUpdateEntitiesNodeModel onUpdateModel;
            OnKeyPressEcsNodeModel onKeyPressModel;
            //--Floating Nodes--

            //Stack-Derived NodeModels
            spawnedNodes.Add(stack = graphModel.CreateNode<StackModel>("StackModel"));
            spawnedNodes.Add(funcModel = graphModel.CreateNode<FunctionModel>("FunctionModel"));
            spawnedNodes.Add(onUpdateModel = graphModel.CreateNode<OnUpdateEntitiesNodeModel>("OnUpdateEntitiesNodeModel"));
            spawnedNodes.Add(onKeyPressModel = graphModel.CreateNode<OnKeyPressEcsNodeModel>("OnKeyPressEcsNodeModel"));

            var methodInfo = TypeSystem.GetMethod(typeof(Debug), nameof(Debug.Log), true);
            spawnedNodes.Add(graphModel.CreateEventFunction(methodInfo, Vector2.zero));
            spawnedNodes.Add(graphModel.CreateNode<OnEndEntitiesNodeModel>("OnEndEntitiesNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<OnEventNodeModel>("OnEventNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<OnStartEntitiesNodeModel>("OnStartEntitiesNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<PostUpdate>("PostUpdate"));
            spawnedNodes.Add(graphModel.CreateNode<PreUpdate>("PreUpdate"));
            spawnedNodes.Add(graphModel.CreateNode<KeyDownEventModel>("KeyDownEventModel"));
            spawnedNodes.Add(graphModel.CreateNode<CountEntitiesNodeModel>("CountEntitiesNodeModel"));
            spawnedNodes.Add(graphModel.CreateLoopStack(typeof(ForEachHeaderModel), Vector2.zero));
            spawnedNodes.Add(graphModel.CreateLoopStack(typeof(WhileHeaderModel), Vector2.zero));
            spawnedNodes.Add(graphModel.CreateLoopStack(typeof(ForAllEntitiesStackModel), Vector2.zero));
            spawnedNodes.Add(graphModel.CreateLoopStack(typeof(CoroutineStackModel), Vector2.zero));

            //Constant-typed NodeModels
            spawnedNodes.Add(graphModel.CreateNode<BooleanConstantNodeModel>("BooleanConstantNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<ColorConstantModel>("ColorConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<CurveConstantNodeModel>("CurveConstantNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<DoubleConstantModel>("DoubleConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<EnumConstantNodeModel>("EnumConstantNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<FloatConstantModel>("FloatConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<GetPropertyGroupNodeModel>("GetPropertyGroupNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<InputConstantModel>("InputConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<IntConstantModel>("IntConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<LayerConstantModel>("LayerConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<LayerMaskConstantModel>("LayerMaskConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<ObjectConstantModel>("ObjectConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<QuaternionConstantModel>("QuaternionConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<StringConstantModel>("StringConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<TagConstantModel>("TagConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<Vector2ConstantModel>("Vector2ConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<Vector3ConstantModel>("Vector3ConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<Vector4ConstantModel>("Vector4ConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<ConstantSceneAssetNodeModel>("ConstantSceneAssetNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<Float2ConstantModel>("Float2ConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<Float3ConstantModel>("Float3ConstantModel"));
            spawnedNodes.Add(graphModel.CreateNode<Float4ConstantModel>("Float4ConstantModel"));

            //Misc
            void DefineSystemConstant(SystemConstantNodeModel m)
            {
                m.ReturnType = typeof(float).GenerateTypeHandle(stencil);
                m.DeclaringType = typeof(Mathf).GenerateTypeHandle(stencil);
                m.Identifier = "PI";
            }

            spawnedNodes.Add(graphModel.CreateNode<SystemConstantNodeModel>("SystemConstantNodeModel", Vector2.zero, SpawnFlags.Default, DefineSystemConstant));
            spawnedNodes.Add(graphModel.CreateNode<GetInputNodeModel>("GetInputNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<GetKeyNodeModel>("GetKeyNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<GetOrCreateComponentNodeModel>("GetOrCreateComponentNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<GetSingletonNodeModel>("GetSingletonNodeModel"));
            spawnedNodes.Add(graphModel.CreateNode<ThisNodeModel>("ThisNodeModel"));
            VariableDeclarationModel decl = graphModel.CreateGraphVariableDeclaration("MyVariableName", typeof(int).GenerateTypeHandle(graphModel.Stencil), true);
            spawnedNodes.Add((NodeModel)graphModel.CreateVariableNode(decl, Vector2.zero));
            spawnedNodes.Add(graphModel.CreateNode<MacroRefNodeModel>("MacroRefNodeModel"));
            spawnedNodes.Add(graphModel.CreateInlineExpressionNode("2+2", Vector2.zero));
            spawnedNodes.Add(graphModel.CreateBinaryOperatorNode(BinaryOperatorKind.Add, Vector2.zero));
            spawnedNodes.Add(graphModel.CreateUnaryOperatorNode(UnaryOperatorKind.PostIncrement, Vector2.zero));
            spawnedNodes.Add(graphModel.CreateNode<RandomNodeModel>(RandomNodeModel.MakeTitle(RandomNodeModel.DefaultMethod), Vector2.zero));

            //--Stack-Contained Nodes--
            spawnedNodes.Add(stack.CreateStackedNode<AddComponentNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<DestroyEntityNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<ForAllEntitiesNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<ForEachNodeModel>());
            spawnedNodes.Add(stack.CreateFunctionCallNode(TypeSystem.GetMethod(typeof(Debug), nameof(Debug.Log), true)));
            spawnedNodes.Add(stack.CreateFunctionRefCallNode(funcModel));
            spawnedNodes.Add(stack.CreateStackedNode<InstantiateNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<CreateEntityNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<IfConditionNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<LogNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<RemoveComponentNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<SetComponentNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<SetPositionNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<SetRotationNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<WhileNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<SetPropertyGroupNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<SetVariableNodeModel>());
            spawnedNodes.Add(stack.CreateStackedNode<CoroutineNodeModel>(setup: n =>
            {
                n.CoroutineType = typeof(Wait).GenerateTypeHandle(stencil);
            }));
            spawnedNodes.Add(funcModel.CreateStackedNode<ReturnNodeModel>());

            TypeHandle eventTypeHandle = typeof(DummyEvent).GenerateTypeHandle(stencil);
            spawnedNodes.Add(onUpdateModel.CreateStackedNode<SendEventNodeModel>("SendEventNodeModel", 0, SpawnFlags.Default, n => n.EventType = eventTypeHandle));

            return spawnedNodes;
        }

        [DotsEvent, InternalBufferCapacity(1)]
        struct DummyEvent : IBufferElementData {}
    }
}
