using System;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    class CriteriaModelRow : SortableExpandableRow, IVisualScriptingField
    {
        Store m_Store;

        VseGraphView m_GraphView;
        VseGraphView GraphView => m_GraphView ?? (m_GraphView = GetFirstAncestorOfType<Blackboard>().GraphView);

        public IGraphElementModel ExpandableGraphElementModel { get; }

        public CriteriaModel CriteriaModel { get; }

        public CriteriaModelRow(IGraphElementModel graphElementModel,
                                CriteriaModel criteriaModel,
                                Stencil stencil,
                                Store store,
                                Blackboard blackboard,
                                ExpandedContainer parentElement,
                                Action<EventBase> onDeleteCriteriaModel)
            : base(string.Empty,
                   graphElementModel as ComponentQueryDeclarationModel,
                   store,
                   parentElement,
                   rebuildCallback: null,
                   canAcceptDrop: null)
        {
            Sortable = true;

            if (criteriaModel == null)
                throw new ArgumentNullException(nameof(criteriaModel), "criteriaModel should not be null");

            CriteriaModel = criteriaModel;
            GraphElementModel = graphElementModel;
            ExpandableGraphElementModel = criteriaModel;

            m_Store = store;

            if (graphElementModel is IIteratorStackModel)
            {
                OnExpanded = e => m_Store.GetState().EditorDataModel?.ExpandElementsUponCreation(new[] { this }, e);
            }
            else if (graphElementModel is ComponentQueryDeclarationModel componentQueryDeclarationModel)
            {
                var expandedRowName =
                    $"{ComponentQueriesRow.BlackboardEcsProviderTypeName}/{typeof(CriteriaSubSection).Name}/{componentQueryDeclarationModel}/{criteriaModel.Name}";

                if (store.GetState().EditorDataModel.ShouldExpandBlackboardRowUponCreation(expandedRowName))
                    Expanded = true;

                OnExpanded = e => Store.GetState().EditorDataModel?.ExpandBlackboardRowsUponCreation(new[] { expandedRowName }, e);
            }

            ClearClassList();
            AddToClassList("criteriaModelRow");

            int nbCriteria = CriteriaModel.Criteria?.Count ?? 0;
            if (nbCriteria > 0)
            {
                if (CriteriaModel.Criteria != null)
                    foreach (var criterion in CriteriaModel.Criteria)
                    {
                        var criterionRow = new CriterionRow(graphElementModel, criteriaModel, criterion, stencil, store, ExpandedContainer, OnDeleteCriterion);
                        ExpandedContainer.Add(criterionRow);
                    }
            }
            else
            {
                ExpandedButton.style.display = DisplayStyle.None;
            }

            var deleteCriteriaModelButton = new Button { name = "deleteCriteriaModelIcon" };
            deleteCriteriaModelButton.clickable.clickedWithEventInfo += onDeleteCriteriaModel;
            ExpandableRowTitleContainer.Insert(0, deleteCriteriaModelButton);

            var componentContainer = new VisualElement { name = "rowFieldContainer" };

            userData = $"CriteriaModelRow/{graphElementModel}/{criteriaModel.GetHashCode()}";

            var rowCriteriaModelContainer = new VisualElement { name = "rowPillContainer" };
            var criteriaModelLabel = new RenamableLabel(criteriaModel,
                criteriaModel.Name,
                store,
                (n) =>
                {
                    store.Dispatch(new RenameCriteriaModelAction((ICriteriaModelContainer)GraphElementModel, CriteriaModel, n));
                });
            criteriaModelLabel.MandatoryQ<Label>("label").AddToClassList("criteriaModel");
            rowCriteriaModelContainer.Add(criteriaModelLabel);

            componentContainer.Add(rowCriteriaModelContainer);
            componentContainer.Add(new Button(() => { AddCriterionToCriteriaModel(graphElementModel, criteriaModel); }) { name = "addCriterionButton", text = "+" });

            ExpandableRowTitleContainer.Add(componentContainer);

            capabilities |= Capabilities.Selectable | Capabilities.Deletable | Capabilities.Droppable;

            this.AddManipulator(new ContextualMenuManipulator(OnContextualMenuEvent));
        }

        public void Expand() => Expanded = true;
        public bool CanInstantiateInGraph() => false;

        void OnContextualMenuEvent(ContextualMenuPopulateEvent evt)
        {
            GraphView.BuildContextualMenu(evt);
        }

        void AddCriterionToCriteriaModel(IGraphElementModel graphElementModel, CriteriaModel criteriaModel)
        {
            if (graphElementModel == null)
                return;

            ComponentQueryDeclarationModel queryDeclarationModel = null;
            switch (graphElementModel)
            {
                case ComponentQueryDeclarationModel criterionQueryDeclarationModel:
                    queryDeclarationModel = criterionQueryDeclarationModel;
                    break;
                case IIteratorStackModel iteratorStackModel:
                    queryDeclarationModel = iteratorStackModel.ComponentQueryDeclarationModel;
                    break;
            }

            if (queryDeclarationModel == null)
                return;

            Vector2 mousePosition = Event.current.mousePosition;
            EcsSearcherServices.ShowCriteria(queryDeclarationModel,
                k_AddCriterionTitle,
                mousePosition,
                (typeHandle,
                    typeMember,
                    operatorKind) =>
                {
                    m_Store.Dispatch(new AddCriterionAction((ICriteriaModelContainer)graphElementModel,
                        criteriaModel,
                        typeHandle,
                        typeMember,
                        operatorKind));
                });
        }

        const string k_AddCriterionTitle = "Add a Criterion";

        void OnDeleteCriterion(EventBase evt)
        {
            var criterionRow = ((VisualElement)evt.target).GetFirstAncestorOfType<CriterionRow>();
            if (criterionRow?.Criterion == null)
                return;

            Store.Dispatch(new RemoveCriterionAction((ICriteriaModelContainer)GraphElementModel, CriteriaModel, criterionRow.Criterion));
        }

        protected override bool AcceptDrop(GraphElement element)
        {
            return (element is CriteriaModelRow && (CanAcceptDrop == null || CanAcceptDrop(element))) || element is CriterionRow;
        }

        public override void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            var insertCriteriaModelRow = insertTargetElement as CriteriaModelRow;
            var insertCriteriaSubSection = insertTargetElement as CriteriaSubSection;

            if (!(selectedElement is CriteriaModelRow criteriaModelRow) || insertCriteriaModelRow == null && insertCriteriaSubSection == null)
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            if (insertCriteriaModelRow != null)
            {
                var targetCriteriaModel = insertCriteriaModelRow.CriteriaModel;
                if (targetCriteriaModel == null)
                    return;

                Store.Dispatch(new DuplicateCriteriaModelAction((ICriteriaModelContainer)criteriaModelRow.GraphElementModel,
                    criteriaModelRow.CriteriaModel,
                    (ICriteriaModelContainer)insertCriteriaModelRow.GraphElementModel,
                    targetCriteriaModel,
                    insertAtEnd));
                return;
            }

            var criteriaModel = criteriaModelRow.CriteriaModel;
            Store.Dispatch(new DuplicateCriteriaModelAction((ICriteriaModelContainer)criteriaModelRow.GraphElementModel,
                criteriaModel,
                (ICriteriaModelContainer)insertCriteriaSubSection.GraphElementModel,
                targetCriteriaModel: null,
                insertAtEnd: true));
        }

        protected override void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            if (!(selectedElement is CriteriaModelRow criteriaModelRow) || !(insertTargetElement is CriteriaModelRow insertCriteriaModelRow))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            var targetCriteriaModel = insertCriteriaModelRow.CriteriaModel;
            if (targetCriteriaModel == null)
                return;

            var criteriaModel = criteriaModelRow.CriteriaModel;

            if (criteriaModel == targetCriteriaModel)
                return;

            Store.Dispatch(new MoveCriteriaModelAction((ICriteriaModelContainer)GraphElementModel, criteriaModel, targetCriteriaModel, insertAtEnd));
        }

        protected override void OnDragUpdatedEvent(DragUpdatedEvent evt)
        {
            if (!(DragAndDrop.GetGenericData("DragSelection") is GraphElement selectedElement) ||
                !AcceptDrop(selectedElement) ||
                !(evt.currentTarget is SortableExpandableRow))
            {
                m_ParentElement?.SetDragIndicatorVisible(false);
                evt.StopImmediatePropagation();
                return;
            }

            if (selectedElement is CriterionRow && !Expanded)
                Expand();

            base.OnDragUpdatedEvent(evt);
        }

        protected override void OnDragPerformEvent(DragPerformEvent evt)
        {
            if (!(DragAndDrop.GetGenericData("DragSelection") is GraphElement selectedElement) || !AcceptDrop(selectedElement))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                evt.StopImmediatePropagation();
                return;
            }

            if (selectedElement is CriterionRow criterionRow)
            {
                criterionRow.DuplicateRow(criterionRow, this, insertAtEnd: true);
                evt.StopImmediatePropagation();
                return;
            }

            base.OnDragPerformEvent(evt);
        }
    }
}
