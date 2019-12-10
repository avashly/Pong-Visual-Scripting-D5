using System;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    abstract class SortableExpandableRow : ExpandableRow
    {
        // Manipulates movable objects, can also initiate a Drag and Drop operation
        // FIXME: update this code once we have support for drag and drop events in UIElements.
        class SortableElementDropper : Manipulator
        {
            class DragAndDropDelay
            {
                const float k_StartDragThreshold = 4.0f;

                Vector2 MouseDownPosition { get; set; }

                public void Init(Vector2 mousePosition)
                {
                    MouseDownPosition = mousePosition;
                }

                public bool CanStartDrag(Vector2 mousePosition)
                {
                    return Vector2.Distance(MouseDownPosition, mousePosition) > k_StartDragThreshold;
                }
            }

            MouseButton ActivateButton { get; }

            // selectedElement is used to store a unique selection candidate for cases where user clicks on an item not to
            // drag it but just to reset the selection -- we only know this after the manipulation has ended
            GraphElement SelectedElement { get; set; }
            ISelection SelectionContainer { get; set; }

            readonly DragAndDropDelay m_DragAndDropDelay;
            bool m_Active;
            bool m_Dragging;

            public SortableElementDropper()
            {
                m_Active = false;

                m_DragAndDropDelay = new DragAndDropDelay();

                ActivateButton = MouseButton.LeftMouse;
            }

            protected override void RegisterCallbacksOnTarget()
            {
                target.RegisterCallback<MouseDownEvent>(OnMouseDown);
                target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
                target.RegisterCallback<MouseUpEvent>(OnMouseUp);
                target.RegisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            }

            protected override void UnregisterCallbacksFromTarget()
            {
                target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
                target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
                target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
                target.UnregisterCallback<MouseCaptureOutEvent>(OnMouseCaptureOutEvent);
            }

            void Reset()
            {
                m_Active = false;
                m_Dragging = false;
            }

            void OnMouseCaptureOutEvent(MouseCaptureOutEvent e)
            {
                if (m_Active)
                    Reset();
            }

            void OnMouseDown(MouseDownEvent e)
            {
                if (m_Active)
                {
                    e.StopImmediatePropagation();
                    return;
                }

                m_Active = false;
                m_Dragging = false;

                if (target == null)
                    return;

                SelectionContainer = target.GetFirstAncestorOfType<ISelection>();

                if (SelectionContainer == null)
                {
                    // Keep for potential later use in OnMouseUp (where e.target might be different then)
                    SelectionContainer = target.GetFirstOfType<ISelection>();
                    SelectedElement = e.target as GraphElement;
                    return;
                }

                SelectedElement = target.GetFirstOfType<GraphElement>();

                if (SelectedElement == null)
                    return;

                if (e.button == (int)ActivateButton)
                {
                    // avoid starting a manipulation on a non movable object

                    if (!SelectedElement.IsDroppable())
                        return;

                    // Reset drag and drop
                    Vector2 localParentPosition = SelectedElement.ChangeCoordinatesTo(SelectedElement.parent, e.localMousePosition);

                    m_DragAndDropDelay.Init(localParentPosition);

                    m_Active = true;
                    target.CaptureMouse();
                    e.StopPropagation();
                }
            }

            void OnMouseMove(MouseMoveEvent e)
            {
                if (m_Active && !m_Dragging && SelectedElement != null)
                {
                    bool canStartDrag = SelectedElement.IsDroppable();

                    Vector2 localParentPosition = SelectedElement.ChangeCoordinatesTo(SelectedElement.parent, e.localMousePosition);

                    if (canStartDrag && m_DragAndDropDelay.CanStartDrag(localParentPosition))
                    {
                        DragAndDrop.PrepareStartDrag();
                        DragAndDrop.objectReferences = new UnityEngine.Object[] {};   // this IS required for dragging to work
                        DragAndDrop.SetGenericData("DragSelection", SelectedElement);
                        m_Dragging = true;

                        DragAndDrop.StartDrag("");
                        DragAndDrop.visualMode = e.actionKey ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Move;
                        target.ReleaseMouse();
                    }

                    e.StopPropagation();
                }
            }

            void OnMouseUp(MouseUpEvent e)
            {
                if (!m_Active)
                {
                    Reset();
                    return;
                }

                if (e.button == (int)ActivateButton)
                {
                    target.ReleaseMouse();
                    e.StopPropagation();
                    Reset();
                }
            }
        }

        int m_InsertIndex;
        SortableExpandableRow m_InsertTargetElement;
        bool m_InsertAtEnd;

        Blackboard.RebuildCallback m_RebuildCallback;
        protected Store Store { get; }

        IGraphElementModel m_GraphElementModel;
        public IGraphElementModel GraphElementModel
        {
            get => m_GraphElementModel;
            protected set => m_GraphElementModel = value;
        }

        protected ExpandedContainer m_ParentElement;

        protected SortableExpandableRow(string sectionTitle,
                                        IGraphElementModel graphElementModel,
                                        Store store,
                                        ExpandedContainer parentElement,
                                        Blackboard.RebuildCallback rebuildCallback,
                                        BlackboardSection.CanAcceptDropDelegate canAcceptDrop)
            : base(sectionTitle)
        {
            name = "sortableExpandableRow";

            m_RebuildCallback = rebuildCallback;
            m_GraphElementModel = graphElementModel;
            Store = store;
            m_ParentElement = parentElement;

            capabilities |= Capabilities.Droppable;

            AddToClassList("sortable");

            m_InsertIndex = -1;
            m_InsertTargetElement = null;
            m_InsertAtEnd = false;

            CanAcceptDrop = canAcceptDrop;

            this.AddManipulator(new SortableElementDropper());

            RegisterCallback<DragUpdatedEvent>(OnDragUpdatedEvent);
            RegisterCallback<DragPerformEvent>(OnDragPerformEvent);
            RegisterCallback<DragLeaveEvent>(OnDragLeaveEvent);
        }

        protected BlackboardSection.CanAcceptDropDelegate CanAcceptDrop { get; }

        protected abstract bool AcceptDrop(GraphElement element);

        protected virtual void OnDragUpdatedEvent(DragUpdatedEvent evt)
        {
            if (!(DragAndDrop.GetGenericData("DragSelection") is GraphElement selectedElement) ||
                !AcceptDrop(selectedElement) ||
                !(evt.currentTarget is SortableExpandableRow sortableExpandableRow))
            {
                m_ParentElement?.SetDragIndicatorVisible(false);
                return;
            }

            m_InsertTargetElement = null;

            if (m_ParentElement == null)
            {
                m_InsertIndex = 0;
                m_InsertAtEnd = true;

                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.StopPropagation();

                return;
            }

            Vector2 localParentPosition = sortableExpandableRow.ChangeCoordinatesTo(m_ParentElement, evt.localMousePosition);
            m_InsertIndex = m_ParentElement.GetInsertionIndex(localParentPosition);
            m_InsertAtEnd = false;

            if (m_InsertIndex != -1)
            {
                float indicatorY = 0f;
                if (m_InsertIndex == m_ParentElement.childCount)
                {
                    int lastIndex = m_ParentElement.childCount - 1;
                    m_InsertTargetElement = m_ParentElement[m_ParentElement.childCount - 1] as SortableExpandableRow;
                    m_InsertAtEnd = true;

                    for (int i = 0; i <= lastIndex; i++)
                    {
                        if (!(m_ParentElement[i] is SortableExpandableRow))
                            continue;
                        indicatorY += m_ParentElement[i].layout.height;
                    }
                    if (m_InsertTargetElement != null)
                        indicatorY += m_InsertTargetElement.resolvedStyle.marginBottom;
                }
                else
                {
                    VisualElement childAtInsertIndex = m_ParentElement[m_InsertIndex];
                    m_InsertTargetElement = childAtInsertIndex as SortableExpandableRow;

                    for (int i = 0; i < m_InsertIndex; i++)
                    {
                        if (!(m_ParentElement[i] is SortableExpandableRow))
                            continue;
                        indicatorY += m_ParentElement[i].layout.height;
                    }

                    indicatorY -= childAtInsertIndex.resolvedStyle.marginTop;
                }

                m_ParentElement.SetDragIndicatorVisible(true);
                m_ParentElement.SetDragIndicatorPositionY(indicatorY);
            }
            else
            {
                m_ParentElement.SetDragIndicatorVisible(false);
            }

            if (m_InsertIndex != -1)
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;

            evt.StopPropagation();
        }

        protected virtual void OnDragPerformEvent(DragPerformEvent evt)
        {
            if (!(DragAndDrop.GetGenericData("DragSelection") is GraphElement selectedElement) || !AcceptDrop(selectedElement))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            if (m_InsertIndex != -1)
            {
                if (selectedElement.parent != m_InsertTargetElement.parent)
                    DuplicateRow(selectedElement, m_InsertTargetElement, m_InsertAtEnd);
                else
                    MoveRow(selectedElement, m_InsertTargetElement, m_InsertAtEnd);
            }

            m_ParentElement.SetDragIndicatorVisible(false);
            evt.StopPropagation();

            m_RebuildCallback?.Invoke(Blackboard.RebuildMode.BlackboardAndGraphView);
        }

        public abstract void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd);
        protected abstract void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd);

        void OnDragLeaveEvent(DragLeaveEvent evt)
        {
            var selectedElement = DragAndDrop.GetGenericData("DragSelection") as GraphElement;
            if (AcceptDrop(selectedElement) && evt.currentTarget is SortableExpandableRow sortableExpandableRow && m_ParentElement != null)
            {
                Vector2 localParentPosition = sortableExpandableRow.ChangeCoordinatesTo(m_ParentElement, evt.localMousePosition);
                var insertIndex = m_ParentElement.GetInsertionIndex(localParentPosition);
                if (insertIndex != -1 && insertIndex != m_ParentElement.childCount)
                    return;
            }

            m_ParentElement?.SetDragIndicatorVisible(false);
        }
    }
}
