using System;
using System.Linq;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public static class CriteriaModelContainerExtensions
    {
        public static void AddCriteriaModel(this ICriteriaModelContainer criteriaModelContainer)
        {
            // Each new criteria model added is guaranteed a unique name, no check needed before action
            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Add Criteria Model");
            var criteriaModel = new CriteriaModel();
            criteriaModel.UniqueNameProvider = criteriaModelContainer;
            criteriaModel.AssignNewGuid();
            criteriaModel.ResetName();
            criteriaModel.GraphModel = criteriaModelContainer.GraphModel;
            criteriaModelContainer.AddCriteriaModelNoUndo(criteriaModel);
            EditorUtility.SetDirty((Object)(criteriaModelContainer.GraphModel).AssetModel);
        }

        public static void RemoveCriteriaModel(this ICriteriaModelContainer criteriaModelContainer, CriteriaModel criteriaModel)
        {
            if (criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
            {
                Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Remove Criteria Model");
                criteriaModelContainer.RemoveCriteriaModelNoUndo(criteriaModel);
            }
        }

        public static void RenameCriteriaModel(this ICriteriaModelContainer criteriaModelContainer, CriteriaModel criteriaModel, string newName)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Change Criteria Model name");
            criteriaModel.Name = newName;
        }

        public static void MoveCriteriaModel(this ICriteriaModelContainer criteriaModelContainer, CriteriaModel criteriaModel, CriteriaModel targetCriteriaModel, bool insertAtEnd)
        {
            var index = criteriaModelContainer.IndexOfCriteriaModel(criteriaModel);
            if (index == -1)
                throw new ArgumentOutOfRangeException(criteriaModel.ToString());

            var targetIndex = criteriaModelContainer.IndexOfCriteriaModel(targetCriteriaModel);
            if (targetIndex > index && !insertAtEnd)
                targetIndex -= 1;
            if (targetIndex == -1)
                throw new ArgumentOutOfRangeException(targetCriteriaModel.ToString());

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Move Criteria Model");

            criteriaModelContainer.RemoveCriteriaModelNoUndo(criteriaModel);
            criteriaModelContainer.InsertCriteriaModelNoUndo(targetIndex, criteriaModel);
        }

        public static CriteriaModel DuplicateCriteriaModel(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            ICriteriaModelContainer targetCriteriaModelContainer,
            CriteriaModel targetCriteriaModel,
            bool insertAtEnd)
        {
            Assert.AreNotEqual(criteriaModelContainer, targetCriteriaModelContainer);

            var index = criteriaModelContainer.IndexOfCriteriaModel(criteriaModel);
            if (index == -1)
                throw new ArgumentOutOfRangeException(criteriaModel.ToString());

            Undo.RegisterCompleteObjectUndo(targetCriteriaModelContainer.SerializableAsset, "Duplicate Criteria Model");
            CriteriaModel clone = criteriaModel.Clone();
            foreach (var criterion in clone.Criteria)
                Utility.SaveAssetIntoObject(criterion.Value, (Object)clone.GraphModel.AssetModel);
            clone.GraphModel = targetCriteriaModelContainer.GraphModel;
            clone.UniqueNameProvider = targetCriteriaModelContainer;
            clone.SetUniqueName(clone.Name);

            if (insertAtEnd)
            {
                targetCriteriaModelContainer.AddCriteriaModelNoUndo(clone);
            }
            else
            {
                var targetIndex = targetCriteriaModelContainer.IndexOfCriteriaModel(targetCriteriaModel);
                if (targetIndex == -1)
                    throw new ArgumentOutOfRangeException(targetCriteriaModel.ToString());

                targetCriteriaModelContainer.InsertCriteriaModelNoUndo(targetIndex, clone);
            }

            EditorUtility.SetDirty((Object)(clone.GraphModel).AssetModel);

            return clone;
        }

        public static void AddCriterion(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            TypeHandle typeHandle,
            TypeMember typeMember,
            BinaryOperatorKind operatorKind)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Add Criterion To Criteria Model");
            criteriaModel.AddCriterion((VSGraphModel)criteriaModelContainer.GraphModel, typeHandle, typeMember, operatorKind);
        }

        public static void RemoveCriterion(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            Criterion criterion)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Remove Criterion From Criteria Model");
            criteriaModel.RemoveCriterion(criterion);
        }

        public static void ChangeCriterion(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            Criterion criterion,
            TypeHandle typeHandle,
            TypeMember typeMember,
            BinaryOperatorKind operatorKind)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Change Criterion In Criteria Model");
            criteriaModel.ChangeCriterion((VSGraphModel)criteriaModelContainer.GraphModel, criterion, typeHandle, typeMember, operatorKind);
        }

        public static void MoveCriterion(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            Criterion criterion,
            Criterion targetCriterion,
            bool insertAtEnd)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(criteriaModelContainer.SerializableAsset, "Move Criterion In Criteria Model");
            criteriaModel.ReorderCriterion(criterion, targetCriterion, insertAtEnd);
        }

        public static void DuplicateCriterion(this ICriteriaModelContainer criteriaModelContainer,
            CriteriaModel criteriaModel,
            Criterion criterion,
            IGraphElementModel targetGraphElementModel,
            CriteriaModel targetCriteriaModel,
            Criterion targetCriterion,
            bool insertAtEnd)
        {
            if (!criteriaModelContainer.CriteriaModels.Contains(criteriaModel))
                return;

            Undo.RegisterCompleteObjectUndo(targetCriteriaModel.SerializableAsset, "Duplicate Criterion In Criteria Model");
            criteriaModel.DuplicateCriterion(criterion, targetCriteriaModel, targetCriterion, insertAtEnd);
        }
    }
}
