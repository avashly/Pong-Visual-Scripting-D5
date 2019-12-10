using System;
using System.Linq;
using Packages.VisualScripting.Editor.Redux.Actions;
using UnityEditor;
using UnityEditor.VisualScripting.Editor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Elements
{
    class DragNDropEcsHandler : IExternalDragNDropHandler
    {
        public void HandleDragUpdated(DragUpdatedEvent e, DragNDropContext ctx)
        {
            if (DragAndDrop.objectReferences.Length > 1)
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            else if (DragAndDrop.objectReferences.OfType<GameObject>().Any())
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.None;
            }
        }

        public void HandleDragPerform(DragPerformEvent e, Store store, DragNDropContext ctx, VisualElement element)
        {
            var gameObject = DragAndDrop.objectReferences.OfType<GameObject>().Single();
            switch (ctx)
            {
                case DragNDropContext.Blackboard:
                    store.Dispatch(new CreateComponentQueryFromGameObjectAction(gameObject));
                    break;
                case DragNDropContext.Graph:
                    store.Dispatch(new CreateQueryAndElementFromGameObjectAction(gameObject, element.WorldToLocal(e.mousePosition)));
                    break;
            }
        }
    }
}
