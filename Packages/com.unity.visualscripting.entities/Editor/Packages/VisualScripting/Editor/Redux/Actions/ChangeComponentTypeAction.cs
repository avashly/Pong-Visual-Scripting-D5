using System;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Redux.Actions
{
    public class ChangeComponentTypeAction : IAction, IComponentQueryAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly ComponentDefinition ComponentDefinition;
        public readonly TypeHandle TypeHandle;


        public ChangeComponentTypeAction(ComponentQueryDeclarationModel componentQueryDeclarationModel, ComponentDefinition componentDefinition, TypeHandle typeHandle)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            ComponentDefinition = componentDefinition;
            TypeHandle = typeHandle;
        }
    }
}
