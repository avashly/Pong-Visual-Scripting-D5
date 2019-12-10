using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.Highlighting;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    class ComponentQuery : ComponentQueryBase, IVisualScriptingField
    {
        public IGraphElementModel ExpandableGraphElementModel => null;

        public ComponentQuery(ComponentQueryDeclarationModel componentQueryDeclarationModel,
                              Store store,
                              Blackboard.RebuildCallback rebuildCallback)
            : base(componentQueryDeclarationModel, store, rebuildCallback)
        {
            name = "componentQuery";

            capabilities |= Capabilities.Selectable | Capabilities.Droppable | Capabilities.Deletable;

            RegisterCallback<MouseDownEvent>(OnMouseDownEvent);

            this.AddManipulator(new SelectionDropper());
        }

        public void Expand() {}
        public bool CanInstantiateInGraph() => true;

        public override bool Equals(object obj)
        {
            if (obj is ComponentQuery otherComponentQuery)
                return ComponentQueryDeclarationModel == otherComponentQuery.ComponentQueryDeclarationModel;
            return false;
        }

        [SuppressMessage("ReSharper", "BaseObjectGetHashCodeCallInGetHashCode")]
        public override int GetHashCode()
        {
            return ComponentQueryDeclarationModel != null ? ComponentQueryDeclarationModel.GetHashCode() : base.GetHashCode();
        }

        public override void OnSelected()
        {
            base.OnSelected();
            GraphView.HighlightGraphElements();
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            GraphView.ClearGraphElementsHighlight(ShouldHighlightItemUsage);
        }

        void OnMouseDownEvent(MouseDownEvent e)
        {
            if ((e.clickCount == 2) && e.button == (int)MouseButton.LeftMouse && IsRenamable())
            {
                OpenTextEditor();
                e.PreventDefault();
                e.StopImmediatePropagation();
            }
        }
    }
}
