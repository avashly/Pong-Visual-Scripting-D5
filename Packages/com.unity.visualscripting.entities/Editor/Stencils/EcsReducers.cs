using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Packages.VisualScripting.Editor.Redux.Actions;
using UnityEditor;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Packages.VisualScripting.Editor.Stencils
{
    public static class EcsReducers
    {
        public static void Register(Store store)
        {
            store.Register<CreateComponentQueryAction>(CreateComponentQuery);
            store.Register<CreateComponentQueryFromGameObjectAction>(CreateComponentQueryFromGameObject);
            store.Register<CreateQueryAndElementFromGameObjectAction>(CreateQueryAndElementFromGameObject);
            store.Register<RenameComponentQueryAction>(RenameComponentQuery);
            store.Register<AddComponentToQueryAction>(AddComponentToQuery);
            store.Register<RemoveComponentFromQueryAction>(RemoveComponentFromQuery);
            store.Register<ChangeComponentTypeAction>(ChangeComponentType);
            store.Register<ChangeComponentUsageAction>(ChangeComponentUsage);
            store.Register<MoveComponentInQueryAction>(MoveComponentInQuery);
            store.Register<AddCriteriaModelAction>(AddCriteriaModel);
            store.Register<RemoveCriteriaModelAction>(RemoveCriteriaModel);
            store.Register<RenameCriteriaModelAction>(RenameCriteriaModel);
            store.Register<MoveCriteriaModelAction>(MoveCriteriaModel);
            store.Register<DuplicateCriteriaModelAction>(DuplicateCriteriaModel);
            store.Register<AddCriterionAction>(AddCriterion);
            store.Register<RemoveCriterionAction>(RemoveCriterion);
            store.Register<ChangeCriterionAction>(ChangeCriterion);
            store.Register<MoveCriterionAction>(MoveCriterion);
            store.Register<DuplicateCriterionAction>(DuplicateCriterion);
            store.Register<SetOperationForComponentTypeInInstantiateNodeAction>(SetOperationForType);
            store.Register<RemoveOperationForComponentTypeInInstantiateNodeAction>(RemoveOperationForType);
            store.Register<SetOperationForComponentTypeInCreateEntityNodeAction>(SetOperationForType);
            store.Register<RemoveOperationForComponentTypeInCreateEntityNodeAction>(RemoveOperationForType);
        }

        static VSGraphModel GetCurrentGraphModel(State state)
        {
            return (VSGraphModel)state.CurrentGraphModel;
        }

        static State CreateComponentQuery(State previousState, CreateComponentQueryAction action)
        {
            VSGraphModel graphModel = GetCurrentGraphModel(previousState);
            Undo.RegisterCompleteObjectUndo((Object)graphModel.AssetModel, "Create Component Query");
            graphModel.CreateComponentQuery(action.QueryName);
            return previousState;
        }

        static State CreateComponentQueryFromGameObject(State previousState, CreateComponentQueryFromGameObjectAction action)
        {
            VSGraphModel graphModel = GetCurrentGraphModel(previousState);
            Undo.RegisterCompleteObjectUndo((Object)graphModel.AssetModel, "Create Component Query From GameObject");
            graphModel.CreateQueryFromGameObject(action.GameObject);
            return previousState;
        }

        static State CreateQueryAndElementFromGameObject(State previousState, CreateQueryAndElementFromGameObjectAction action)
        {
            var graphModel = GetCurrentGraphModel(previousState);
            Undo.RegisterCompleteObjectUndo((Object)graphModel.AssetModel, "Create Component Query From GameObject");
            var queryFromGameObject = graphModel.CreateQueryFromGameObject(action.GameObject);

            graphModel.CreateVariableNode(queryFromGameObject, action.Position, SpawnFlags.Default & ~SpawnFlags.Undoable);

            return previousState;
        }

        static State RenameComponentQuery(State previousState, RenameComponentQueryAction action)
        {
            var vsGraphModel = (VSGraphModel)previousState.CurrentGraphModel;
            string uniqueName = vsGraphModel.GetUniqueName(action.Name);

            action.ComponentQueryDeclarationModel.SetName(uniqueName);

            IGraphChangeList graphChangeList = previousState.CurrentGraphModel.LastChanges;
            graphChangeList.ChangedElements.AddRange(vsGraphModel.FindUsages(action.ComponentQueryDeclarationModel));
            previousState.MarkForUpdate(UpdateFlags.RequestRebuild);
            return previousState;
        }

        static State AddComponentToQuery(State previousState, AddComponentToQueryAction action)
        {
            ComponentQueryDeclarationModel queryDeclarationModel = action.ComponentQueryDeclarationModel;
            Undo.RegisterCompleteObjectUndo(queryDeclarationModel.SerializableAsset, "Add Component To Query");
            queryDeclarationModel.AddComponent(previousState.CurrentGraphModel.Stencil, action.TypeHandle, action.CreationFlags);
            queryDeclarationModel.ExpandOnCreateUI = true;
            UpdateComponentQuery(previousState, queryDeclarationModel, action);
            return previousState;
        }

        static State RemoveComponentFromQuery(State previousState, RemoveComponentFromQueryAction action)
        {
            action.ComponentQueryDeclarationModel.RemoveComponent(action.ComponentDefinition);
            UpdateComponentQuery(previousState, action.ComponentQueryDeclarationModel, action);
            return previousState;
        }

        static List<IPortModel> s_CachedList = new List<IPortModel>();
        static List<VariableNodeModel> s_CachedVariableList = new List<VariableNodeModel>();

        static void UpdateComponentQueryInternal(State previousState,
            ComponentQueryDeclarationModel queryDeclarationModel,
            IComponentQueryAction queryAction,
            int oldIndex,
            int newIndex)
        {
            UnityEngine.Assertions.Assert.IsTrue(queryAction is MoveComponentInQueryAction || (oldIndex == -1 && newIndex == -1));

            s_CachedVariableList.Clear();
            s_CachedVariableList.AddRange(((VSGraphModel)previousState.CurrentGraphModel).FindUsages(queryDeclarationModel));

            foreach (var usage in s_CachedVariableList)
            {
                IPortModel output = usage.OutputPort;
                if (output != null)
                {
                    s_CachedList.Clear();
                    s_CachedList.AddRange(queryDeclarationModel.GraphModel.GetConnections(output));
                    foreach (var connected in s_CachedList)
                    {
                        if (connected.NodeModel is IIteratorStackModel iteratorStackModel)
                        {
                            Assert.IsTrue(iteratorStackModel is IPrivateIteratorStackModel);
                            ForAllEntitiesStackModel.UpdateComponentsVariables((IPrivateIteratorStackModel)iteratorStackModel, queryAction, oldIndex, newIndex);
                        }
                        connected.NodeModel.OnConnection(connected, output);
                    }
                }

                previousState.CurrentGraphModel.LastChanges.ChangedElements.Add(usage);
            }
        }

        static void UpdateComponentQuery(State previousState, ComponentQueryDeclarationModel queryDeclarationModel, IComponentQueryAction queryAction)
        {
            Assert.False(queryAction is MoveComponentInQueryAction, $"Use {nameof(UpdateComponentQueryInternal)} instead when moving components");
            UpdateComponentQueryInternal(previousState, queryDeclarationModel, queryAction, -1, -1);
        }

        static State ChangeComponentUsage(State previousState, ChangeComponentUsageAction action)
        {
            action.ComponentQueryDeclarationModel.ChangeComponentUsage(action.ComponentDefinition, action.Subtract);
            UpdateComponentQuery(previousState, action.ComponentQueryDeclarationModel, action);
            return previousState;
        }

        static State MoveComponentInQuery(State previousState, MoveComponentInQueryAction action)
        {
            action.ComponentQueryDeclarationModel.MoveComponent(action.ComponentDefinition, action.TargetComponentDefinition, action.InsertAtEnd, out int oldIndex, out int newIndex);
            UpdateComponentQueryInternal(previousState, action.ComponentQueryDeclarationModel, action, oldIndex, newIndex);
            return previousState;
        }

        static State ChangeComponentType(State previousState, ChangeComponentTypeAction action)
        {
            action.ComponentQueryDeclarationModel.ChangeComponentType(action.ComponentDefinition, action.TypeHandle);
            UpdateComponentQuery(previousState, action.ComponentQueryDeclarationModel, action);
            return previousState;
        }

        static State AddCriteriaModel(State previousState, AddCriteriaModelAction action)
        {
            action.CriteriaModelContainer.AddCriteriaModel();
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);
            if (action.CriteriaModelContainer is ComponentQueryDeclarationModel queryDeclarationModel)
                queryDeclarationModel.ExpandOnCreateUI = true;
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);

            return previousState;
        }

        static State RemoveCriteriaModel(State previousState, RemoveCriteriaModelAction action)
        {
            action.CriteriaModelContainer.RemoveCriteriaModel(action.CriteriaModel);
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);

            return previousState;
        }

        static State RenameCriteriaModel(State previousState, RenameCriteriaModelAction action)
        {
            action.CriteriaModelContainer.RenameCriteriaModel(action.CriteriaModel, action.Name);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State MoveCriteriaModel(State previousState, MoveCriteriaModelAction action)
        {
            action.CriteriaModelContainer.MoveCriteriaModel(action.CriteriaModel, action.TargetCriteriaModel, action.InsertAtEnd);

            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.DeletedElements++;
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State DuplicateCriteriaModel(State previousState, DuplicateCriteriaModelAction action)
        {
            CriteriaModel clone = action.CriteriaModelContainer.DuplicateCriteriaModel(
                action.CriteriaModel,
                action.targetCriteriaModelContainer,
                action.TargetCriteriaModel,
                action.InsertAtEnd);

            if (clone != null)
            {
                ((VSGraphModel)clone.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
                ((VSGraphModel)clone.GraphModel).LastChanges.ChangedElements.Add(action.targetCriteriaModelContainer);
                ((VSGraphModel)clone.GraphModel).LastChanges.DeletedElements++;
                EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);
            }

            return previousState;
        }

        static State AddCriterion(State previousState, AddCriterionAction action)
        {
            action.CriteriaModelContainer.AddCriterion(action.CriteriaModel,
                action.TypeHandle,
                action.TypeMember,
                action.OperatorKind);
            if (action.CriteriaModelContainer is ComponentQueryDeclarationModel queryDeclarationModel)
                queryDeclarationModel.ExpandOnCreateUI = true;
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State RemoveCriterion(State previousState, RemoveCriterionAction action)
        {
            action.CriteriaModelContainer.RemoveCriterion(action.CriteriaModel, action.Criterion);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State ChangeCriterion(State previousState, ChangeCriterionAction action)
        {
            action.CriteriaModelContainer.ChangeCriterion(action.CriteriaModel,
                action.Criterion,
                action.TypeHandle,
                action.TypeMember,
                action.OperatorKind);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State MoveCriterion(State previousState, MoveCriterionAction action)
        {
            action.CriteriaModelContainer.MoveCriterion(action.CriteriaModel,
                action.Criterion,
                action.TargetCriterion,
                action.InsertAtEnd);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.DeletedElements++;
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State DuplicateCriterion(State previousState, DuplicateCriterionAction action)
        {
            action.CriteriaModelContainer.DuplicateCriterion(action.CriteriaModel,
                action.Criterion,
                action.TargetCriteriaModelContainer,
                action.TargetCriteriaModel,
                action.TargetCriterion,
                action.InsertAtEnd);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.CriteriaModelContainer);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.ChangedElements.Add(action.TargetCriteriaModelContainer);
            ((VSGraphModel)action.CriteriaModelContainer.GraphModel).LastChanges.DeletedElements++;
            EditorUtility.SetDirty(action.CriteriaModelContainer.SerializableAsset);

            return previousState;
        }

        static State SetOperationForType(State previousState, SetOperationForComponentTypeInInstantiateNodeAction action)
        {
            Undo.RegisterCompleteObjectUndo(action.Model.SerializableAsset, "Set Operation For Type In Instantiate Node");
            action.Model.SetComponentOperation(action.ComponentType, action.Operation);
            action.Model.DefineNode();
            //TODO eventually we'll need to figure how to chain the changes required around the ports & edges.
            //This will require the rework surrounding trackable ports
            EditorUtility.SetDirty(action.Model.SerializableAsset);
            ((VSGraphModel)action.Model.GraphModel).LastChanges.ChangedElements.Add(action.Model);
            return previousState;
        }

        static State RemoveOperationForType(State previousState,  RemoveOperationForComponentTypeInInstantiateNodeAction action)
        {
            Undo.RegisterCompleteObjectUndo(action.Model.SerializableAsset, "Remove Operation For Type In Instantiate Node");
            action.Model.DeleteComponentOperation(action.ComponentType);
            action.Model.DefineNode();
            //TODO eventually we'll need to figure how to chain the changes required around the ports & edges.
            //This will require the rework surrounding trackable ports
            ((VSGraphModel)action.Model.GraphModel).LastChanges.ChangedElements.Add(action.Model);
            ((VSGraphModel)action.Model.GraphModel).LastChanges.DeletedElements++;
            EditorUtility.SetDirty(action.Model.SerializableAsset);
            return previousState;
        }

        static State SetOperationForType(State previousState, SetOperationForComponentTypeInCreateEntityNodeAction action)
        {
            Undo.RegisterCompleteObjectUndo(action.Model.SerializableAsset, "Set Operation For Type In Instantiate Node");
            action.Model.AddComponentTypeToAdd(action.ComponentType);
            action.Model.DefineNode();
            //TODO eventually we'll need to figure how to chain the changes required around the ports & edges.
            //This will require the rework surrounding trackable ports
            EditorUtility.SetDirty(action.Model.SerializableAsset);
            ((VSGraphModel)action.Model.GraphModel).LastChanges.ChangedElements.Add(action.Model);
            return previousState;
        }

        static State RemoveOperationForType(State previousState,  RemoveOperationForComponentTypeInCreateEntityNodeAction action)
        {
            Undo.RegisterCompleteObjectUndo(action.Model.SerializableAsset, "Remove Operation For Type In Instantiate Node");
            action.Model.DeleteComponentOperation(action.ComponentType);
            action.Model.DefineNode();
            //TODO eventually we'll need to figure how to chain the changes required around the ports & edges.
            //This will require the rework surrounding trackable ports
            ((VSGraphModel)action.Model.GraphModel).LastChanges.ChangedElements.Add(action.Model);
            ((VSGraphModel)action.Model.GraphModel).LastChanges.DeletedElements++;
            EditorUtility.SetDirty(action.Model.SerializableAsset);
            return previousState;
        }
    }
}
