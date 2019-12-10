using System;
using System.Collections.Generic;
using System.Linq;
using Packages.VisualScripting.Editor.Redux.Actions;
using UnityEditor;
using UnityEditor.EditorCommon.Redux;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    public class BlackboardEcsProvider : IBlackboardProvider
    {
        const string k_ClassLibrarySubTitle = "System Graph";

        const int k_ComponentQueriesSection = 0;
        const string k_ComponentQueriesSectionTitle = "Component Queries";
        const string k_DefaultComponentQueryName = "Component Query";

        const int k_VariablesSection = 1;
        const string k_VariableSectionTitle = "Variables";
        const string k_DefaultVariableName = "Variable";

        const int k_CurrentScopeSection = 2;
        const string k_CurrentScopeSectionTitle = "Current Scope";

        readonly Stencil m_Stencil;
        Blackboard m_Blackboard;

        public BlackboardEcsProvider(Stencil stencil)
        {
            m_Stencil = stencil;
        }

        /// <summary>
        /// Defines blackboard's sections such as :
        /// - ComponentQueries are graph's variables of type EntityQuery
        /// - Variables are non-EntityQuery graph's variables
        /// - Current scope are data members of the selected scope
        /// </summary>
        public IEnumerable<BlackboardSection> CreateSections()
        {
            var componentQueriesSection = new BlackboardSection { title = k_ComponentQueriesSectionTitle, headerVisible = false };
            componentQueriesSection.canAcceptDrop += CanAcceptDrop;
            yield return componentQueriesSection;

            var variablesSection = new BlackboardSection { title = k_VariableSectionTitle};
            var variablesSectionHeader = variablesSection.Q("sectionHeader");
            variablesSectionHeader.Add(new Button(OnAddVariableButtonClicked) { name = "addButton", text = "+" });
            variablesSection.canAcceptDrop += CanAcceptDrop;
            yield return variablesSection;

            var currentScopeSection = new BlackboardSection { title = k_CurrentScopeSectionTitle };
            currentScopeSection.canAcceptDrop += _ => false;
            yield return currentScopeSection;
        }

        void OnAddVariableButtonClicked()
        {
            if (m_Blackboard != null)
                m_Blackboard.Store.Dispatch(new CreateGraphVariableDeclarationAction(k_DefaultVariableName, true, TypeHandle.Float));
        }

        public string GetSubTitle()
        {
            return k_ClassLibrarySubTitle;
        }

        static bool CanAcceptDrop(ISelectable selected)
        {
            return selected is BlackboardVariableField;
        }

        public void AddItemRequested<TAction>(Store store, TAction _) where TAction : IAction
        {
            store.Dispatch(new CreateComponentQueryAction(k_DefaultComponentQueryName));
        }

        public void MoveItemRequested(Store store, int index, VisualElement field)
        {
            if (field is BlackboardVariableField blackboardField)
                store.Dispatch(new ReorderGraphVariableDeclarationAction(blackboardField.VariableDeclarationModel, index));
        }

        static readonly StyleSheet k_BlackboardStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.uss");

        public void RebuildSections(Blackboard blackboard)
        {
            m_Blackboard = blackboard;

            blackboard.styleSheets.Add(k_BlackboardStyleSheet);
            // @TODO: This might need to be reviewed in favor of a better / more scalable approach (non preprocessor based)
            // that would ideally bring the same level of backward/forward compatibility and/or removed when a 2013 beta version lands.
#if UNITY_2019_3_OR_NEWER
            blackboard.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.2019.3.uss"));
#endif

            // Fetch expanded states as well as active component query toggles (note that this doesn't not apply to
            // elements that do not survive a domain reload)
            var expandedRows = new Dictionary<object, bool>();

            foreach (BlackboardSection blackBoardSection in blackboard.Sections)
            {
                blackBoardSection.Query<ExpandableRow>().ForEach(row =>
                {
                    switch (row.userData)
                    {
                        case IVariableDeclarationModel model:
                            {
                                var queryDeclarationModel = model as ComponentQueryDeclarationModel;
                                bool expanded = row.Expanded;
                                if (!expanded && queryDeclarationModel != null)
                                    expanded = queryDeclarationModel.ExpandOnCreateUI;
                                expandedRows[model] = expanded;
                                break;
                            }
                        case string str:
                            expandedRows[str] = row.Expanded;
                            break;
                    }
                });

                blackBoardSection.Query<BlackboardRow>().ForEach(row =>
                {
                    switch (row.userData)
                    {
                        case IVariableDeclarationModel model:
                            expandedRows[model] = row.expanded;
                            break;
                        case Tuple<IVariableDeclarationModel, bool> modelTuple:
                            expandedRows[modelTuple.Item1] = row.expanded;
                            break;
                    }
                });
            }

            List<ISelectable> selectionCopy = blackboard.selection.ToList();

            // TODO: This is too hardcore.  We need to let all unrolling happen smoothly.
            blackboard.ClearContents();

            var state = blackboard.Store.GetState();
            var graphVariables = ((IVSGraphModel)state.CurrentGraphModel).GraphVariableModels.ToList();

            // Fill component queries section
            var componentQueryDeclarationModels = graphVariables.OfType<ComponentQueryDeclarationModel>().ToList();
            var nbModels = componentQueryDeclarationModels.Count;
            if (nbModels > 0)
            {
                blackboard.Sections[k_ComponentQueriesSection]
                    .Add(new ComponentQueriesRow(componentQueryDeclarationModels,
                    blackboard,
                    m_Stencil,
                    blackboard.Rebuild));
                blackboard.Sections[k_ComponentQueriesSection].title = $"{k_ComponentQueriesSectionTitle} ({nbModels})";
            }

            // Fill variables section
            var variables = graphVariables.Where(v => !(v is ComponentQueryDeclarationModel)).ToList();
            blackboard.Sections[k_VariablesSection].title = $"{k_VariableSectionTitle} ({variables.Count})";

            foreach (var variable in variables)
            {
                var blackboardField = new BlackboardVariableField(blackboard.Store, variable, blackboard.GraphView);
                var blackboardRow = new BlackboardRow(
                    blackboardField,
                    CreateExtendedFieldView(blackboard.Store, variable, m_Stencil, blackboard.Rebuild))
                {
                    userData = variable,
                    expanded = expandedRows.TryGetValue(variable, out var isExpended) && isExpended
                };

                blackboard.Sections[k_VariablesSection].Add(blackboardRow);
                blackboard.GraphVariables.Add(blackboardField);
                blackboard.RestoreSelectionForElement(blackboardField);
            }

            // Fill local scope section
            foreach (Tuple<IVariableDeclarationModel, bool> variableDeclarationModelTuple in blackboard.GraphView.UIController
                     .GetAllVariableDeclarationsFromSelection(selectionCopy))
            {
                var blackboardField = new BlackboardVariableField(blackboard.Store, variableDeclarationModelTuple.Item1, blackboard.GraphView);

                if (variableDeclarationModelTuple.Item1.VariableType == VariableType.FunctionParameter)
                    blackboardField.AddToClassList("parameter");

                if (variableDeclarationModelTuple.Item2)
                {
                    var blackboardRow = new BlackboardRow(blackboardField,
                        CreateExtendedFieldView(blackboard.Store,
                            variableDeclarationModelTuple.Item1,
                            m_Stencil,
                            blackboard.Rebuild))
                    {
                        userData = variableDeclarationModelTuple
                    };
                    blackboard.Sections[k_CurrentScopeSection].Add(blackboardRow);
                }
                else
                {
                    blackboardField.AddToClassList("readonly");
                    blackboard.Sections[k_CurrentScopeSection].Add(blackboardField);
                }

                blackboard.GraphVariables.Add(blackboardField);
                blackboard.RestoreSelectionForElement(blackboardField);
            }

            foreach (BlackboardSection blackBoardSection in blackboard.Sections)
            {
                blackBoardSection.Query<ComponentQuery>().ForEach(blackboard.RestoreSelectionForElement);
                blackBoardSection.Query<ComponentRow>().ForEach(blackboard.RestoreSelectionForElement);
                blackBoardSection.Query<CriterionRow>().ForEach(blackboard.RestoreSelectionForElement);
            }
        }

        public void DisplayAppropriateSearcher(Vector2 mousePosition, Blackboard blackboard)
        {
            VisualElement picked = blackboard.panel.Pick(mousePosition);
            while (picked != null && !(picked is IVisualScriptingField || picked is ICustomSearcherHandler))
                picked = picked.parent;

            // optimization: stop at the first IVsBlackboardField, but still exclude BlackboardThisFields
            if (picked != null)
            {
                if (picked is BlackboardVariableField field)
                {
                    SearcherService.ShowTypes(
                        m_Stencil,
                        Event.current.mousePosition,
                        (t, i) =>
                        {
                            var variableDeclarationModel = (VariableDeclarationModel)field.VariableDeclarationModel;
                            blackboard.Store.Dispatch(new UpdateTypeAction(variableDeclarationModel, t));
                            blackboard.Rebuild(Blackboard.RebuildMode.BlackboardAndGraphView);
                        });
                }
                else if (picked is ComponentQuery query)
                {
                    var componentsSubSection = picked.GetFirstAncestorOfType<ComponentsSubSection>();
                    if (componentsSubSection != null)
                    {
                        componentsSubSection.AddComponentToQuery(query.ComponentQueryDeclarationModel);
                    }
                    else
                    {
                        var criteriaSubSection = picked.GetFirstAncestorOfType<CriteriaSubSection>();
                        criteriaSubSection?.AddCriteriaModel(query.ComponentQueryDeclarationModel);
                    }
                }
                else if (picked is ICustomSearcherHandler customSearcherHandler)
                {
                    customSearcherHandler.HandleCustomSearcher(mousePosition);
                }
            }

            // Else do nothing for now, we don't have a default action for the ECS blackboard
        }

        public bool CanAddItems => true;

        // ECS Specific contextual menu entries fall in here
        public void BuildContextualMenu(DropdownMenu menu,
            VisualElement targetElement,
            Store store,
            Vector2 mousePosition)
        {
            if (targetElement == null)
                return;

            var componentRow = targetElement.GetFirstOfType<ComponentRow>();
            if (componentRow?.Component != null)
            {
                menu.AppendAction("Update", menuAction =>
                {
                    var filter = new SearcherFilter(SearcherContext.Type)
                        .WithComponentData(m_Stencil)
                        .WithSharedComponentData(m_Stencil);
                    componentRow.UpdateType(mousePosition, filter);
                }, eventBase => DropdownMenuAction.Status.Normal);
                menu.AppendAction("Delete", menuAction =>
                {
                    store.Dispatch(new RemoveComponentFromQueryAction((ComponentQueryDeclarationModel)componentRow.GraphElementModel, componentRow.Component));
                }, eventBase => DropdownMenuAction.Status.Normal);
                return;
            }

            if (targetElement is CriterionRow criterionRow && criterionRow.Criterion != null)
            {
                menu.AppendAction("Update", menuAction =>
                {
                    criterionRow.Update(mousePosition);
                }, eventBase => DropdownMenuAction.Status.Normal);
                menu.AppendAction("Delete", menuAction =>
                {
                    store.Dispatch(new RemoveCriterionAction((ICriteriaModelContainer)criterionRow.GraphElementModel, criterionRow.CriteriaModel, criterionRow.Criterion));
                }, eventBase => DropdownMenuAction.Status.Normal);
                return;
            }

            var criteriaModelRow = targetElement.GetFirstOfType<CriteriaModelRow>();
            if (criteriaModelRow?.CriteriaModel != null)
            {
                menu.AppendAction("Delete", menuAction =>
                {
                    store.Dispatch(new RemoveCriteriaModelAction((ICriteriaModelContainer)criteriaModelRow.GraphElementModel, criteriaModelRow.CriteriaModel));
                }, eventBase => DropdownMenuAction.Status.Normal);
            }
        }

        static VisualElement CreateExtendedFieldView(Store store,
            IVariableDeclarationModel variableDeclarationModel,
            Stencil stencil,
            Blackboard.RebuildCallback rebuild)
        {
            return new BlackboardVariablePropertyView(store, variableDeclarationModel, rebuild, stencil)
                .WithTypeSelector()
                .WithTooltipField()
                .WithInitializationField();
        }
    }
}
