using UnityEditor.EditorCommon.Redux;
using UnityEditor.VisualScripting.Model.Stencils;

namespace UnityEditor.VisualScripting.Editor
{
    public class SetOperationForComponentTypeInInstantiateNodeAction : IAction
    {
        public InstantiateNodeModel Model;
        public TypeHandle ComponentType;
        public ComponentOperation.ComponentOperationType Operation;

        public SetOperationForComponentTypeInInstantiateNodeAction(InstantiateNodeModel model, TypeHandle componentType,
                                                                   ComponentOperation.ComponentOperationType operation)
        {
            Model = model;
            ComponentType = componentType;
            Operation = operation;
        }
    }

    public class RemoveOperationForComponentTypeInInstantiateNodeAction : IAction
    {
        public InstantiateNodeModel Model;
        public TypeHandle ComponentType;

        public RemoveOperationForComponentTypeInInstantiateNodeAction(InstantiateNodeModel model, TypeHandle componentType)
        {
            Model = model;
            ComponentType = componentType;
        }
    }


    public class SetOperationForComponentTypeInCreateEntityNodeAction : IAction
    {
        public CreateEntityNodeModel Model;
        public TypeHandle ComponentType;

        public SetOperationForComponentTypeInCreateEntityNodeAction(CreateEntityNodeModel model, TypeHandle componentType)
        {
            Model = model;
            ComponentType = componentType;
        }
    }

    public class RemoveOperationForComponentTypeInCreateEntityNodeAction : IAction
    {
        public CreateEntityNodeModel Model;
        public TypeHandle ComponentType;

        public RemoveOperationForComponentTypeInCreateEntityNodeAction(CreateEntityNodeModel model, TypeHandle componentType)
        {
            Model = model;
            ComponentType = componentType;
        }
    }
}
