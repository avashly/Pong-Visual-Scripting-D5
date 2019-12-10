using System;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Redux.Actions
{
    interface IComponentQueryAction {}

    public class CreateComponentQueryAction : IAction
    {
        public readonly string QueryName;

        public CreateComponentQueryAction(string queryName)
        {
            QueryName = queryName;
        }
    }

    class CreateComponentQueryFromGameObjectAction : IAction
    {
        public readonly GameObject GameObject;

        public CreateComponentQueryFromGameObjectAction(GameObject gameObject)
        {
            GameObject = gameObject;
        }
    }

    class CreateQueryAndElementFromGameObjectAction : IAction
    {
        public readonly GameObject GameObject;
        public readonly Vector2 Position;

        public CreateQueryAndElementFromGameObjectAction(GameObject gameObject, Vector2 position)
        {
            GameObject = gameObject;
            Position = position;
        }
    }

    public class AddComponentToQueryAction : IAction, IComponentQueryAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly TypeHandle TypeHandle;
        public readonly ComponentDefinitionFlags CreationFlags;

        public AddComponentToQueryAction(ComponentQueryDeclarationModel componentQueryDeclarationModel,
                                         TypeHandle typeHandle,
                                         ComponentDefinitionFlags creationFlags
        )
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            TypeHandle = typeHandle;
            CreationFlags = creationFlags;
        }
    }

    public class RemoveComponentFromQueryAction : IAction, IComponentQueryAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly ComponentDefinition ComponentDefinition;

        public RemoveComponentFromQueryAction(ComponentQueryDeclarationModel componentQueryDeclarationModel, ComponentDefinition componentDefinition)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            ComponentDefinition = componentDefinition;
        }
    }

    public class MoveComponentInQueryAction : IAction, IComponentQueryAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly ComponentDefinition ComponentDefinition;
        public readonly ComponentDefinition TargetComponentDefinition;
        public readonly bool InsertAtEnd;

        public MoveComponentInQueryAction(ComponentQueryDeclarationModel componentQueryDeclarationModel,
                                          ComponentDefinition componentDefinition,
                                          ComponentDefinition targetComponentDefinition,
                                          bool insertAtEnd)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            ComponentDefinition = componentDefinition;
            TargetComponentDefinition = targetComponentDefinition;
            InsertAtEnd = insertAtEnd;
        }
    }

    public class RenameComponentQueryAction : IAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly string Name;

        public RenameComponentQueryAction(ComponentQueryDeclarationModel componentQueryDeclarationModel, string name)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            Name = name;
        }
    }
}
