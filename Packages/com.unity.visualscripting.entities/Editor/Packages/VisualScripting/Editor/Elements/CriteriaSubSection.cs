using System;
using System.Collections.Generic;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    class CriteriaSubSection : SortableExpandableRow, IVisualScriptingField
    {
        const string SectionTitleText = "Additional Criterias";
        Stencil m_Stencil;
        Blackboard m_Blackboard;

        public CriteriaSubSection(Stencil stencil,
                                  ComponentQueryDeclarationModel componentQueryDeclarationModel,
                                  Blackboard blackboard)
            : base(SectionTitleText,
                   graphElementModel: null,
                   store: blackboard.Store,
                   parentElement: null,
                   rebuildCallback: null,
                   canAcceptDrop: null)
        {
            m_Stencil = stencil;
            m_Blackboard = blackboard;

            name = "criteriaSection";
            userData = name;

            AddToClassList("subSection");

            ExpandedButton.style.display = DisplayStyle.None;
            int nbRows = 0;

            BuildCriteriaForComponentQuery(componentQueryDeclarationModel, ref nbRows, blackboard);

            viewDataKey = "BlackboardCriteriaSection";

            Expanded = true;

            SectionTitle.text += " (" + nbRows + ")";
        }

        public CriteriaSubSection(Stencil stencil, IIteratorStackModel iteratorStackModel, Store store)
            : base(SectionTitleText,
                   graphElementModel: null,
                   store: store,
                   parentElement: null,
                   rebuildCallback: null,
                   canAcceptDrop: null)
        {
            m_Stencil = stencil;

            Sortable = true;

            name = "criteriaSection";
            userData = name;

            AddToClassList("subSection");

            GraphElementModel = iteratorStackModel;

            Add(new Button(() => { AddCriteriaModel(iteratorStackModel); }) { name = "addCriteriaButton", text = "+" });

            int nbRows = 0;
            BuildCriteriaForIteratorStackModel(ref nbRows, iteratorStackModel, ExpandedContainer);

            ExpandableGraphElementModel = iteratorStackModel;

            OnExpanded = e => Store.GetState().EditorDataModel?.ExpandElementsUponCreation(new[] { this }, e);

            viewDataKey = "IteratorStackCriteriaSection";

            SectionTitle.text += " (" + nbRows + ")";

            RegisterCallback<AttachToPanelEvent>(AttachToPanel);
        }

        void AttachToPanel(AttachToPanelEvent evt)
        {
            if (Store.GetState().EditorDataModel.ShouldExpandElementUponCreation(this))
                Expand();
            UnregisterCallback<AttachToPanelEvent>(AttachToPanel);
        }

        void BuildCriteriaForComponentQuery(ComponentQueryDeclarationModel componentQueryDeclarationModel, ref int nbRows, Blackboard blackboard)
        {
            Sortable = true;
            ExpandableRowTitleContainer.AddManipulator(new Clickable(() => {}));
            ExpandableRowTitleContainer.Add(new Button(() => { AddCriteriaModel(componentQueryDeclarationModel); }) { name = "addCriteriaButton", text = "+" });
            componentQueryDeclarationModel.ExpandOnCreateUI = false;
            nbRows += AddCriteriaModelRows(componentQueryDeclarationModel, ExpandedContainer);
        }

        void BuildCriteriaForIteratorStackModel(ref int nbRows, IIteratorStackModel iteratorStackModel, ExpandedContainer expandedContainer)
        {
            nbRows += AddCriteriaModelRows(iteratorStackModel, expandedContainer);
        }

        public void AddCriteriaModel(ICriteriaModelContainer criteriaModelContainer)
        {
            Store.Dispatch(new AddCriteriaModelAction(criteriaModelContainer));
        }

        int AddCriteriaModelRows(ComponentQueryDeclarationModel componentQueryDeclarationModel, ExpandedContainer expandedContainer)
        {
            foreach (var criteriaModel in componentQueryDeclarationModel.CriteriaModels)
                AddCriteriaModelRow(criteriaModel, componentQueryDeclarationModel, expandedContainer);

            return componentQueryDeclarationModel.CriteriaModels.Count;
        }

        int AddCriteriaModelRows(IIteratorStackModel iteratorStackModel, ExpandedContainer expandedContainer)
        {
            foreach (var criteriaModel in iteratorStackModel.CriteriaModels)
                AddCriteriaModelRow(criteriaModel, iteratorStackModel, expandedContainer);

            return iteratorStackModel.CriteriaModels.Count;
        }

        void AddCriteriaModelRow(CriteriaModel criteriaModel,
            IGraphElementModel graphElementModel,
            ExpandedContainer expandedContainer)
        {
            Assert.IsNotNull(expandedContainer);

            expandedContainer.Add(new CriteriaModelRow(graphElementModel,
                criteriaModel,
                m_Stencil,
                Store,
                m_Blackboard,
                expandedContainer,
                OnDeleteCriteriaModel));
        }

        void OnDeleteCriteriaModel(EventBase evt)
        {
            var criteriaModelRow = ((VisualElement)evt.target).GetFirstAncestorOfType<CriteriaModelRow>();
            if (criteriaModelRow?.CriteriaModel == null)
                return;

            Store.Dispatch(new RemoveCriteriaModelAction((ICriteriaModelContainer)criteriaModelRow.GraphElementModel, criteriaModelRow.CriteriaModel));
        }

        public override void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            throw new NotImplementedException("This row cannot be duplicated");
        }

        protected override void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            throw new NotImplementedException("This row cannot be moved");
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

            if (selectedElement is CriteriaModelRow && !Expanded)
                Expand();

            base.OnDragUpdatedEvent(evt);
        }

        protected override bool AcceptDrop(GraphElement element)
        {
            return element is CriteriaModelRow && (CanAcceptDrop == null || CanAcceptDrop(element));
        }

        protected override void OnDragPerformEvent(DragPerformEvent evt)
        {
            if (!(DragAndDrop.GetGenericData("DragSelection") is GraphElement selectedElement) || !AcceptDrop(selectedElement))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                evt.StopImmediatePropagation();
                return;
            }

            if (selectedElement is CriteriaModelRow criteriaModelRow)
            {
                criteriaModelRow.DuplicateRow(criteriaModelRow, this, insertAtEnd: true);
                evt.StopImmediatePropagation();
                return;
            }

            base.OnDragPerformEvent(evt);
        }

        public IGraphElementModel ExpandableGraphElementModel { get; }
        public void Expand() => Expanded = true;
        public bool CanInstantiateInGraph() => false;
    }
}
