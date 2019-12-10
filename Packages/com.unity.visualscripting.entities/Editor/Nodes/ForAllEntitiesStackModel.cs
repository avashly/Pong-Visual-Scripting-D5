using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Packages.VisualScripting.Editor.Redux.Actions;
using Unity.Entities;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Extensions;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.VisualScripting;

namespace Packages.VisualScripting.Editor.Stencils
{
    [VisualScriptingFriendlyName("For All Entities")]
    [Serializable]
    public class ForAllEntitiesStackModel : LoopStackModel, IPrivateIteratorStackModel
    {
        [SerializeField]
        List<CriteriaModel> m_CriteriaModels = new List<CriteriaModel>();

        HashSet<string> m_TakenNames = new HashSet<string>();

        const int k_NonComponentVariablesCount = 2;

        public ComponentQueryDeclarationModel ComponentQueryDeclarationModel { get; private set; }
        public VariableDeclarationModel ItemVariableDeclarationModel { get; private set; }
        public IReadOnlyList<CriteriaModel> CriteriaModels => m_CriteriaModels;
        public UpdateMode Mode => UpdateMode.OnUpdate;
        public int NonComponentVariablesCount => k_NonComponentVariablesCount;

        public void AddCriteriaModelNoUndo(CriteriaModel criteriaModel)
        {
            m_CriteriaModels.Add(criteriaModel);
            UpdateTakenNames();
        }

        public void InsertCriteriaModelNoUndo(int index, CriteriaModel criteriaModel)
        {
            m_CriteriaModels.Insert(index, criteriaModel);
            UpdateTakenNames();
        }

        public void RemoveCriteriaModelNoUndo(CriteriaModel criteriaModel)
        {
            m_CriteriaModels.Remove(criteriaModel);
            UpdateTakenNames();
        }

        public int IndexOfCriteriaModel(CriteriaModel criteriaModel)
        {
            return m_CriteriaModels.IndexOf(criteriaModel);
        }

        void UpdateTakenNames()
        {
            m_TakenNames = CriteriaModels.Select(x => x.Name).ToHashSet();
        }

        public bool AddTakenName(string proposedName) => m_TakenNames.Add(proposedName);

        VariableDeclarationModel CollectionVariableDeclarationModel { get; set; }

        public override string Title => "For All Entities";

        public override string IconTypeString => "typeForEachLoop";
        public override Type MatchingStackedNodeType => typeof(ForAllEntitiesNodeModel);

        internal const string DefaultCollectionName = "Archetype";
        public override bool AllowChangesToModel => false;
        public override bool IsInstanceMethod => true;

        public override List<TitleComponent> BuildTitle()
        {
            IPortModel insertLoopPortModel = InputPort?.ConnectionPortModels?.FirstOrDefault();
            var insertLoopNodeModel = (ForAllEntitiesNodeModel)insertLoopPortModel?.NodeModel;
            var collectionInputPortModel = insertLoopNodeModel?.InputPort;

            if (CollectionVariableDeclarationModel != null)
                CollectionVariableDeclarationModel.Name = collectionInputPortModel?.Name ?? DefaultCollectionName;

            return new List<TitleComponent>
            {
                new TitleComponent
                {
                    titleComponentType = TitleComponentType.String,
                    titleObject = "For Each"
                },
                ItemVariableDeclarationModel != null
                ? new TitleComponent
                {
                    titleComponentType = TitleComponentType.Token,
                    titleComponentIcon = TitleComponentIcon.Item,
                    titleObject = ItemVariableDeclarationModel
                }
                : new TitleComponent
                {
                    titleComponentType = TitleComponentType.String,
                    titleObject = "Entity"
                },
                new TitleComponent
                {
                    titleComponentType = TitleComponentType.String,
                    titleObject = "In"
                },
                collectionInputPortModel != null
                ? new TitleComponent
                {
                    titleComponentType = TitleComponentType.Token,
                    titleComponentIcon = TitleComponentIcon.Collection,
                    titleObject = CollectionVariableDeclarationModel
                }
                : new TitleComponent
                {
                    titleComponentType = TitleComponentType.String,
                    titleObject = DefaultCollectionName
                }
            };
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                if (ItemVariableDeclarationModel != null)
                    hashCode = (hashCode * 197) ^ (ItemVariableDeclarationModel.GetHashCode());
                return hashCode;
            }
        }

        public override void OnConnection(IPortModel selfConnectedPortModel, IPortModel otherConnectedPortModel)
        {
            if (selfConnectedPortModel.Direction == Direction.Input && otherConnectedPortModel?.NodeModel is ForAllEntitiesNodeModel forAllNode)
            {
                ComponentQueryDeclarationModel = forAllNode.ComponentQueryDeclarationModel;

                CreateLoopVariables(otherConnectedPortModel);
            }

            base.OnConnection(selfConnectedPortModel, otherConnectedPortModel);
        }

        protected override void OnCreateLoopVariables(VariableCreator variableCreator, IPortModel connectedPortModel)
        {
            if ((connectedPortModel?.NodeModel as IVariableModel)?.DeclarationModel is
                VariableDeclarationModel variableDeclarationModel &&
                variableDeclarationModel.DataType != typeof(EntityQuery).GenerateTypeHandle(Stencil))
                return;

            var componentQueryDeclarationModel = ComponentQueryDeclarationModel;
            var entityName = componentQueryDeclarationModel != null
                ? $"{componentQueryDeclarationModel.VariableName}Entity"
                : "Entity";

            ItemVariableDeclarationModel = variableCreator.DeclareVariable<LoopVariableDeclarationModel>(
                entityName,
                typeof(Entity).GenerateTypeHandle(Stencil),
                TitleComponentIcon.Item,
                VariableFlags.Generated | VariableFlags.Hidden);
            CollectionVariableDeclarationModel = variableCreator.DeclareVariable<LoopVariableDeclarationModel>(
                DefaultCollectionName,
                typeof(EntityQuery).GenerateTypeHandle(Stencil),
                TitleComponentIcon.Collection,
                VariableFlags.Generated | VariableFlags.Hidden);

            Assert.AreEqual(k_NonComponentVariablesCount, variableCreator.DeclaredVariablesCount);

            CreateComponentVariables(Stencil, variableCreator, this);
        }

        internal static void UpdateComponentsVariables(IPrivateIteratorStackModel iteratorStackModel, IComponentQueryAction action, int movedOldIndex, int movedNewIndex)
        {
            void RemoveComponentVariable(IList<VariableDeclarationModel> list, ComponentDefinition componentDefinition)
            {
                VariableDeclarationModel toRemove = list.FirstOrDefault(x => x.DataType == componentDefinition.TypeHandle);
                if (toRemove != null)
                    ((VSGraphModel)iteratorStackModel.GraphModel).DeleteVariableDeclarations(Enumerable.Repeat(toRemove, 1), true);
                else
                    Debug.LogWarning($"Failed to remove declaration of type {componentDefinition.TypeHandle} from stack {iteratorStackModel} not found");
            }

            IList<VariableDeclarationModel> variableDeclarationModels = iteratorStackModel.FunctionParameters;
            switch (action)
            {
                case RemoveComponentFromQueryAction removeAction:
                    RemoveComponentVariable(variableDeclarationModels, removeAction.ComponentDefinition);
                    break;

                case ChangeComponentUsageAction changeAction when changeAction.Subtract: // putting as subtractive is equivalent to removing the component
                    RemoveComponentVariable(variableDeclarationModels, changeAction.ComponentDefinition);
                    break;

                case ChangeComponentUsageAction changeAction when !changeAction.Subtract: // putting as subtractive is equivalent to removing the component
                    // need to insert it
                    var queryComponent = changeAction.ComponentQueryDeclarationModel.Query.Find(changeAction.ComponentDefinition, out _);
                    if (queryComponent == null)
                        return;

                    queryComponent.ForceInsert = true;
                    break;

                case MoveComponentInQueryAction _:
                    Assert.AreNotEqual(-1, movedNewIndex);
                    Assert.AreNotEqual(-1, movedOldIndex);
                    variableDeclarationModels.Insert(movedNewIndex + iteratorStackModel.NonComponentVariablesCount, variableDeclarationModels[movedOldIndex + iteratorStackModel.NonComponentVariablesCount]);
                    variableDeclarationModels.RemoveAt(movedOldIndex + iteratorStackModel.NonComponentVariablesCount + (movedOldIndex > movedNewIndex ? 1 : 0));
                    break;

                case AddComponentToQueryAction _: // we always add at the end, so nothing to do
                case ChangeComponentTypeAction _: // nothing to do, the previous declaration will get reused
                    break;
            }
        }

        internal static void CreateComponentVariables(Stencil stencil, VariableCreator variableCreator, IIteratorStackModel iteratorStackModel)
        {
            var componentQueryDeclarationModel = iteratorStackModel.ComponentQueryDeclarationModel;
            if (componentQueryDeclarationModel != null)
            {
                foreach (QueryComponent component in componentQueryDeclarationModel.Components.Where(def => !def.Component.Subtract))
                {
                    variableCreator.DeclareVariable<LoopVariableDeclarationModel>(
                        component.Component.TypeHandle.GetMetadata(stencil).FriendlyName,
                        component.Component.TypeHandle,
                        TitleComponentIcon.Item,
                        VariableFlags.Generated,
                        component.ForceInsert);
                    component.ForceInsert = false;
                }
            }
        }
    }
}
