using System;
using System.Linq;
using System.Text;
using Packages.VisualScripting.Editor.Redux.Actions;
using Packages.VisualScripting.Editor.Stencils;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.ConstantEditor;
using UnityEditor.VisualScripting.Editor.Highlighting;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.UIElements;
using VisualScripting.Model.Common.Extensions;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    class CriterionRow : SortableExpandableRow, IVisualScriptingField, ICustomSearcherHandler
    {
        public CriteriaModel CriteriaModel { get; }
        public Criterion Criterion { get; }

        VseGraphView m_GraphView;

        VseGraphView GraphView
        {
            get
            {
                if (m_GraphView == null)
                {
                    var bb = GetFirstAncestorOfType<Blackboard>();
                    // Since blackboards are no longer in GraphViews, we need to check if we're either in a blackboard
                    // or not (criterion rows can be in function nodes).
                    if (bb != null)
                        m_GraphView = bb.GraphView;
                    else
                        m_GraphView = GetFirstAncestorOfType<VseGraphView>();
                }
                return m_GraphView;
            }
        }

        public IGraphElementModel ExpandableGraphElementModel => null;

        VisualElement m_EditorElement;

        static TypeHandle[] s_PropsToHideLabel =
        {
            TypeHandle.Int,
            TypeHandle.Float,
            TypeHandle.Vector2,
            TypeHandle.Vector3,
            TypeHandle.Vector4,
            TypeHandle.String
        };

        bool EditorNeedsLabel => !s_PropsToHideLabel.Contains(Criterion.ObjectType);

        public CriterionRow(IGraphElementModel graphElementModel,
                            CriteriaModel criteriaModel,
                            Criterion criterion,
                            Stencil stencil,
                            Store store,
                            ExpandedContainer parentElement,
                            Action<EventBase> onDeleteCriterion)
            : base(string.Empty, graphElementModel as ComponentQueryDeclarationModel, store, parentElement, null, null)
        {
            GraphElementModel = graphElementModel;
            CriteriaModel = criteriaModel;
            Criterion = criterion ?? throw new ArgumentNullException(nameof(criterion), "criterion should not be null");
            ClearClassList();
            AddToClassList("criterionRow");

            capabilities |= Capabilities.Selectable | Capabilities.Deletable;

            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UnityEditor.VisualScripting.Editor.UICreationHelper.templatePath + "PropertyField.uss"));

            var deleteCriterionButton = new Button { name = "deleteCriterionIcon" };
            deleteCriterionButton.clickable.clickedWithEventInfo += onDeleteCriterion;
            ExpandableRowTitleContainer.Insert(0, deleteCriterionButton);

            userData = criterion.GetHashCode();

            var criterionContainer = new VisualElement { name = "rowCriterionContainer" };

            string criterionNamespace = criterion.ObjectType.GetMetadata(stencil).Namespace;
            string criterionName = criterion.ObjectType.ToTypeSyntax(stencil).ToString().Replace(criterionNamespace + ".", "");

            var criterionStr = new StringBuilder(criterionName);
            if (criterion.Member.Path.Any())
            {
                criterionStr.Append(" > ");
                criterionStr.Append(string.Join(" > ", criterion.Member.Path));
            }

            var criterionPillContainer = new VisualElement { name = "rowPillContainer" };
            var criterionPill = new Pill { text = criterionStr.ToString() };

            criterionPillContainer.Add(criterionPill);

            criterionPillContainer.Add(new Label() { name = "criterionOperatorKind", text = criterion.Operator.NicifyBinaryOperationKindName(OperatorExtensions.NicifyBinaryOperationKindType.String)});

            // TODO SERIALIZATION
//            if (criterion.Value.NodeAssetReference != null)
//                m_WatchedObject = new SerializedObject(criterion.Value.NodeAssetReference);
            m_EditorElement = SetupConstantEditor(criterion.Value);
            var label = m_EditorElement.Q<Label>();
            if (label != null && !EditorNeedsLabel)
                label.style.width = 0;

            criterionPillContainer.Add(m_EditorElement);

            criterionContainer.Add(criterionPillContainer);

            ExpandableRowTitleContainer.Add(criterionContainer);

            // CriterionRow's are NOT expandable (for now, until we find a reason to) but since we need everything
            // else that is provided by SortableExpandableRow, we'll just disable expansion altogether
            ExpandedButton.style.display = DisplayStyle.None;

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            this.AddManipulator(new ContextualMenuManipulator(OnContextualMenuEvent));
        }

        public void Expand() => Expanded = true;
        public bool CanInstantiateInGraph() => false;

        void OnContextualMenuEvent(ContextualMenuPopulateEvent evt)
        {
            GraphView.BuildContextualMenu(evt);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            (m_EditorElement as PropertyField)?.RemovePropertyFieldValueLabel();

            // TODO: Not surviving domain reload
            if (Store.GetState().EditorDataModel.ShouldExpandElementUponCreation(this))
                Expand();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            m_EditorElement?.Unbind();
        }

        VisualElement SetupConstantEditor(IVariableModel criterionValue)
        {
            EnableInClassList("constant", true);
            VisualElement propertyField = this.CreateEditorForNodeModel((IConstantNodeModel)criterionValue, _ => Store.Dispatch(new RefreshUIAction(UpdateFlags.RequestCompilation)));
            propertyField.name = "criterionRowValue";
            return propertyField;
        }

        public override bool Equals(object obj)
        {
            if (obj is CriterionRow otherCriterionRow)
                return Criterion == otherCriterionRow.Criterion;
            return false;
        }

        public override int GetHashCode() => Criterion?.GetHashCode() ?? 0;

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
                    when((CriteriaModel)GraphElementModel)?.Criteria.FirstOrDefault(x => (x.ObjectType.GetType() == component.GetType())) != null:
                    return true;
            }

            return false;
        }

        protected override bool AcceptDrop(GraphElement element) => element is CriterionRow && (CanAcceptDrop == null || CanAcceptDrop(element));

        protected override void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            if (!(selectedElement is CriterionRow criterionRow) || !(insertTargetElement is CriterionRow insertCriterionRow))
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            var targetCriterion = insertCriterionRow.Criterion;
            if (targetCriterion == null)
                return;

            if (criterionRow.Criterion == targetCriterion)
                return;

            Store.Dispatch(new MoveCriterionAction((ICriteriaModelContainer)GraphElementModel, criterionRow.CriteriaModel, criterionRow.Criterion, targetCriterion, insertAtEnd));
        }

        public override void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
            var insertCriterionRow = insertTargetElement as CriterionRow;
            var insertCriteriaModelRow = insertTargetElement as CriteriaModelRow;

            if (!(selectedElement is CriterionRow criterionRow) || insertCriterionRow == null && insertCriteriaModelRow == null)
            {
                m_ParentElement.SetDragIndicatorVisible(false);
                return;
            }

            if (insertCriterionRow != null)
            {
                var targetCriterion = insertCriterionRow.Criterion;
                if (targetCriterion == null)
                    return;

                Store.Dispatch(new DuplicateCriterionAction((ICriteriaModelContainer)criterionRow.GraphElementModel,
                    criterionRow.CriteriaModel,
                    criterionRow.Criterion,
                    (ICriteriaModelContainer)insertCriterionRow.GraphElementModel,
                    insertCriterionRow.CriteriaModel,
                    targetCriterion,
                    insertAtEnd));
                return;
            }

            Store.Dispatch(new DuplicateCriterionAction((ICriteriaModelContainer)criterionRow.GraphElementModel,
                criterionRow.CriteriaModel,
                criterionRow.Criterion,
                (ICriteriaModelContainer)insertCriteriaModelRow.GraphElementModel,
                insertCriteriaModelRow.CriteriaModel,
                targetCriterion: null,
                insertAtEnd: true));
        }

        public void Update(Vector2 mousePosition)
        {
            ComponentQueryDeclarationModel queryDeclarationModel;

            switch (GraphElementModel)
            {
                case ComponentQueryDeclarationModel criterionQueryDeclarationModel:
                    queryDeclarationModel = criterionQueryDeclarationModel;
                    break;
                case IIteratorStackModel iteratorStackModel:
                    queryDeclarationModel = iteratorStackModel.ComponentQueryDeclarationModel;
                    break;
                default:
                    throw new ArgumentException($"Update Criterion: Unsupported graph model {GraphElementModel}");
            }

            EcsSearcherServices.ShowCriteria(queryDeclarationModel,
                k_ChangeCriterionTitle,
                mousePosition,
                (typeHandle,
                    typeMember,
                    operatorKind) =>
                {
                    Store.Dispatch(new ChangeCriterionAction(queryDeclarationModel,
                        CriteriaModel,
                        Criterion,
                        typeHandle,
                        typeMember,
                        operatorKind));
                });
        }

        const string k_ChangeCriterionTitle = "Change a Criterion";

        public bool HandleCustomSearcher(Vector2 mousePosition, SearcherFilter filter = null)
        {
            Update(mousePosition);
            return true;
        }
    }
}
