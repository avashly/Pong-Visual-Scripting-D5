using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    class QuerySubSection : SortableExpandableRow
    {
        public QuerySubSection(Stencil stencil,
                               ComponentQueryDeclarationModel componentQueryDeclarationModel,
                               Blackboard blackboard)
            : base("",
                   graphElementModel: null,
                   store: blackboard.Store,
                   parentElement: null,
                   rebuildCallback: null,
                   canAcceptDrop: null)
        {
            name = "queriesSection";
            userData = name;

            AddToClassList("subSection");
            State state = blackboard.Store.GetState();

            var componentQuery = new ComponentQuery(componentQueryDeclarationModel, Store, blackboard.Rebuild);

            Sortable = true;
            ExpandableRowTitleContainer.AddManipulator(new Clickable(() => {}));
            ExpandableRowTitleContainer.Add(componentQuery);
            ExpandableRowTitleContainer.Add(new Label($"({(componentQueryDeclarationModel.Query?.Components?.Count ?? 0)})") { name = "count" });
            var rowName = $"{ComponentQueriesRow.BlackboardEcsProviderTypeName}/{GetType().Name}/{componentQueryDeclarationModel}";
            OnExpanded += e =>
                Store.GetState().EditorDataModel?.ExpandBlackboardRowsUponCreation(new[] { rowName }, e);
            if (state.EditorDataModel.ShouldExpandBlackboardRowUponCreation(rowName))
                Expanded = true;

            ExpandedContainer.Add(new ComponentsSubSection(stencil, componentQueryDeclarationModel, blackboard));
            ExpandedContainer.Add(new CriteriaSubSection(stencil, componentQueryDeclarationModel, blackboard));
            blackboard.GraphVariables.Add(componentQuery);
        }

        protected override bool AcceptDrop(GraphElement element) => false;

        public override void DuplicateRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
        }

        protected override void MoveRow(GraphElement selectedElement, SortableExpandableRow insertTargetElement, bool insertAtEnd)
        {
        }
    }
}
