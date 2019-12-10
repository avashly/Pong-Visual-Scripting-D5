using System;
using System.Collections.Generic;
using UnityEditor.VisualScripting.GraphViewModel;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public interface ICriteriaModelContainer : IGraphElementModel, IUniqueNameProvider
    {
        IReadOnlyList<CriteriaModel> CriteriaModels { get; }

        void AddCriteriaModelNoUndo(CriteriaModel criteriaModel);
        void InsertCriteriaModelNoUndo(int index, CriteriaModel criteriaModel);
        void RemoveCriteriaModelNoUndo(CriteriaModel criteriaModel);
        int IndexOfCriteriaModel(CriteriaModel criteriaModel);
    }
}
