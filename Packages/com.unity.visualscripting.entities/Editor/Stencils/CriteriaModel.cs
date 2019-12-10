using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class CriteriaModel : IGraphElementModelWithGuid
    {
        public string Name;

        [SerializeField]
        List<Criterion> m_Criteria;

        public IReadOnlyList<Criterion> Criteria => m_Criteria ?? (m_Criteria = new List<Criterion>());

        [SerializeField]
        GraphModel m_GraphModel;

        public IGraphModel GraphModel
        {
            get => m_GraphModel;
            set => m_GraphModel = (GraphModel)value;
        }

        public void SetUniqueName(string originalName)
        {
            string prefix = string.IsNullOrEmpty(Name) ? "Criteria" : originalName;

            if (UniqueNameProvider.AddTakenName(prefix))
            {
                Name = prefix;
                return;
            }

            int i = 0;
            string s2 = prefix + i;
            while (!UniqueNameProvider.AddTakenName(s2))
                s2 = prefix + ++i;

            Name = s2;
        }

        public void AddCriterion(VSGraphModel graphModel, TypeHandle typeHandle, TypeMember typeMember, BinaryOperatorKind operatorKind)
        {
            var constantNode = graphModel.CreateConstantNode(string.Empty, typeMember.Type, Vector2.zero, SpawnFlags.Default | SpawnFlags.Orphan);
            AddCriterionNoUndo(graphModel, new Criterion
            {
                ObjectType = typeHandle,
                Member = typeMember,
                Operator = operatorKind,
                Value = (ConstantNodeModel)constantNode
            });
        }

        public void AddCriterionNoUndo(VSGraphModel graphModel, Criterion criterion)
        {
            Utility.SaveAssetIntoObject(criterion.Value, (Object)graphModel.AssetModel);

            graphModel.AssetModel.SetAssetDirty();

            if (!Criteria.Contains(criterion))
                m_Criteria.Add(criterion);
        }

        public void RemoveCriterion(Criterion criterion)
        {
            if (Criteria.Contains(criterion))
                m_Criteria.Remove(criterion);
        }

        public void ChangeCriterion(VSGraphModel graphModel,
            Criterion criterion,
            TypeHandle typeHandle,
            TypeMember typeMember,
            BinaryOperatorKind operatorKind)
        {
            var index = m_Criteria.IndexOf(criterion);
            if (index != -1)
            {
                Type t = graphModel.Stencil.GetConstantNodeModelType(typeMember.Type);

                criterion.ObjectType = typeHandle;
                criterion.Member = typeMember;
                criterion.Operator = operatorKind;

                if (criterion.Value is ConstantNodeModel model)
                {
                    model.Destroy();
                }

                criterion.Value = graphModel.CreateNode(t, "", Vector2.zero, SpawnFlags.Default | SpawnFlags.Orphan) as IVariableModel;
                Utility.SaveAssetIntoObject(criterion.Value, (Object)graphModel.AssetModel);
                m_Criteria[index] = criterion;
            }
        }

        public void ReorderCriterion(Criterion criterion, Criterion targetCriterion, bool insertAtEnd)
        {
            var index = m_Criteria.IndexOf(criterion);
            var targetIndex = m_Criteria.IndexOf(targetCriterion);
            if (targetIndex > index && !insertAtEnd)
                targetIndex -= 1;

            if (index == -1)
                throw new ArgumentOutOfRangeException(criterion.ToString());
            if (targetIndex == -1)
                throw new ArgumentOutOfRangeException(targetCriterion.ToString());

            m_Criteria.Remove(criterion);
            m_Criteria.Insert(targetIndex, criterion);
        }

        public void DuplicateCriterion(Criterion criterion,
            CriteriaModel targetCriteriaModel,
            Criterion targetCriterion,
            bool insertAtEnd)
        {
            var index = m_Criteria.IndexOf(criterion);
            if (index == -1)
                throw new ArgumentOutOfRangeException(criterion.ToString());

            var clone = criterion.Clone();

            Utility.SaveAssetIntoObject(clone.Value, (Object)targetCriteriaModel.GraphModel.AssetModel);
            EditorUtility.SetDirty(SerializableAsset);

            if (insertAtEnd)
            {
                targetCriteriaModel.m_Criteria.Add(clone);
            }
            else
            {
                var targetIndex = targetCriteriaModel.m_Criteria.IndexOf(targetCriterion);
                if (targetIndex == -1)
                    throw new ArgumentOutOfRangeException(targetCriterion.ToString());

                targetCriteriaModel.m_Criteria.Insert(targetIndex, clone);
            }
        }

        public CapabilityFlags Capabilities => CapabilityFlags.Deletable | CapabilityFlags.Droppable;
        public ScriptableObject SerializableAsset => (ScriptableObject)AssetModel;
        public IGraphAssetModel AssetModel => GraphModel.AssetModel;

        public IUniqueNameProvider UniqueNameProvider { private get; set; }

        SerializableGUID m_GUID;
        public string GetId()
        {
            return m_GUID.ToString();
        }

        public void AssignNewGuid()
        {
            m_GUID = GUID.Generate();
        }

        public override int GetHashCode()
        {
            return m_GUID.GetHashCode();
        }

        public CriteriaModel Clone()
        {
            var clone = new CriteriaModel();
            EditorUtility.CopySerializedManagedFieldsOnly(this, clone);
            clone.m_GUID = GUID.Generate();
//            clone.m_Criteria = Criteria.Select(x => x.Clone()).ToList();
            return clone;
        }

        public void ResetName()
        {
            SetUniqueName("Criteria");
        }
    }
}
