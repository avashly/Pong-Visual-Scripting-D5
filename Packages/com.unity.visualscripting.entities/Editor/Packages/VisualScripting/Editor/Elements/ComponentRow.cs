using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Redux.Actions;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.Highlighting;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Elements
{
    class ComponentRow : SortableExpandableRow, IVisualScriptingField, ICustomSearcherHandler
    {
        public ComponentDefinition Component { get; }

        Stencil m_Stencil;

        VseGraphView m_GraphView;
        VseGraphView GraphView => m_GraphView ?? (m_GraphView = GetFirstAncestorOfType<UnityEditor.VisualScripting.Editor.Blackboard>().GraphView);

        public IGraphElementModel ExpandableGraphElementModel => null;

        public ComponentRow(ComponentQueryDeclarationModel componentQueryDeclarationModel,
                            ComponentDefinition component,
                            Stencil stencil,
                            Store store,
                            ExpandedContainer parentElement,
                            Action<EventBase> onDeleteComponent,
                            EventCallback<ChangeEvent<bool>> onUsageChanged)
            : base(string.Empty, componentQueryDeclarationModel, store, parentElement, null, null)
        {
            Component = component;
            m_Stencil = stencil;

            ClearClassList();
            AddToClassList("componentRow");

            var fieldViewContainerTooltip = new StringBuilder();
            var fields = Component.TypeHandle.Resolve(stencil).GetFields();
            int nbFields = fields.Length;
            if (nbFields > 0)
            {
                int i = 0;
                foreach (var field in fields)
                {
                    var fieldView = new VisualElement { name = "fieldView" };
                    var fieldName = field.Name + ": ";
                    var fieldTypeName = field.FieldType.Name;
                    fieldView.Add(new Label(fieldName));
                    fieldView.Add(new Label(fieldTypeName));
                    fieldViewContainerTooltip.Append(fieldName + fieldTypeName);
                    i++;
                    if (i < nbFields)
                        fieldViewContainerTooltip.Append('\n');
                    ExpandedContainer.Add(fieldView);
                }
            }
            else
            {
                ExpandedButton.style.display = DisplayStyle.None;
            }

            var deleteComponentButton = new Button { name = "deleteComponentIcon" };
            deleteComponentButton.clickable.clickedWithEventInfo += onDeleteComponent;
            ExpandableRowTitleContainer.Insert(0, deleteComponentButton);

            var componentContainer = new VisualElement { name = "rowFieldContainer" };

            string componentNamespace = component.TypeHandle.GetMetadata(stencil).Namespace;
            string componentName = component.TypeHandle.ToTypeSyntax(stencil).ToString().Replace(componentNamespace + ".", "");

            userData = $"{GraphElementModel}/{componentName}";

            var rowPillContainer = new VisualElement { name = "rowPillContainer" };

            var componentPill = new ComponentPill(component, componentName, fieldViewContainerTooltip.ToString());
            rowPillContainer.Add(componentPill);

            componentContainer.Add(rowPillContainer);

            var usageField = new Toggle("Subtract") { value = component.Subtract };
            usageField.AddToClassList("usage");
            usageField.RegisterValueChangedCallback(onUsageChanged);
            componentContainer.Add(usageField);

            ExpandableRowTitleContainer.Add(componentContainer);

            capabilities |= Capabilities.Selectable | Capabilities.Deletable;

            var expandedRowName = $"{ComponentQueriesRow.BlackboardEcsProviderTypeName}/{typeof(ComponentsSubSection).Name}/{componentQueryDeclarationModel}/{componentName}";

            if (store.GetState().EditorDataModel.ShouldExpandBlackboardRowUponCreation(expandedRowName))
                Expanded = true;

            OnExpanded = e => Store.GetState().EditorDataModel?.ExpandBlackboardRowsUponCreation(new[] { expandedRowName }, e);

            this.AddManipulator(new ContextualMenuManipulator(OnContextualMenuEvent));
        }

        public void Expand() => Expanded = true;
        public bool CanInstantiateInGraph() => false;

        public override bool Equals(object obj)
        {
            if (obj is ComponentRow otherComponentRow)
                return Component == otherComponentRow.Component;
            return false;
        }

        [SuppressMessage("ReSharper", "BaseObjectGetHashCodeCallInGetHashCode")]
        public override int GetHashCode()
        {
            return (Component != null && GraphElementModel != null) ?
                Component.GetHashCode() * GraphElementModel.GetHashCode() : base.GetHashCode();
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

        bool ShouldHighlightItemUsage(IGraphElementModel model)
        {
            switch (model)
            {
                case VariableNodeModel variableNodeModel
                    when ReferenceEquals(GraphElementModel, variableNodeModel.DeclarationModel):
                    return true;
                case ComponentDefinition component
                    when(GraphElementModel is ComponentQueryDeclarationModel queryDeclarationModel && queryDeclarationModel.Components.FirstOrDefault(x => x.Component == component) != null):
                    return true;
            }

            return false;
        }

        void OnContextualMenuEvent(ContextualMenuPopulateEvent evt)
        {
            GraphView.BuildContextualMenu(evt);
        }

        protected override bool AcceptDrop(GraphElement element) => element is ComponentRow && (CanAcceptDrop == null || CanAcceptDrop(element));

        public override void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            throw new NotImplementedException("This row cannot be duplicated");
        }

        protected override void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            if (!(selectedElement is ComponentRow componentRow) || !(insertTargetElement is ComponentRow insertComponentRowElement))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            var targetComponent = insertComponentRowElement.Component;
            if (targetComponent == null)
                return;

            var component = componentRow.Component;
            if (component == targetComponent)
                return;

            Assert.That(GraphElementModel is ComponentQueryDeclarationModel);
            Store.Dispatch(new MoveComponentInQueryAction((ComponentQueryDeclarationModel)GraphElementModel, component, targetComponent, insertAtEnd));
        }

        public bool HandleCustomSearcher(Vector2 mousePosition, SearcherFilter filter = null)
        {
            UpdateType(mousePosition, filter);
            return true;
        }

        public void UpdateType(Vector2 mousePosition, SearcherFilter filter)
        {
            SearcherService.ShowTypes(
                m_Stencil,
                mousePosition,
                (t, i) =>
                {
                    Store.Dispatch(new ChangeComponentTypeAction(
                        (ComponentQueryDeclarationModel)GraphElementModel,
                        Component,
                        t));
                }, filter);
        }
    }
}
