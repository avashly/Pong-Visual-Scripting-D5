using System;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Redux.Actions
{
    public class ChangeComponentUsageAction : IAction, IComponentQueryAction
    {
        public readonly ComponentQueryDeclarationModel ComponentQueryDeclarationModel;
        public readonly ComponentDefinition ComponentDefinition;
        public readonly bool Subtract;

        public ChangeComponentUsageAction(ComponentQueryDeclarationModel componentQueryDeclarationModel, ComponentDefinition componentDefinition, bool subtract)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            ComponentDefinition = componentDefinition;
            Subtract = subtract;
        }
    }
}
