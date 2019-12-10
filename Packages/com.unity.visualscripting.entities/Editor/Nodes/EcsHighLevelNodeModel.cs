using System;
using System.Linq;
using UnityEditor.VisualScripting.GraphViewModel;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public abstract class EcsHighLevelNodeModel : HighLevelNodeModel
    {
        internal ComponentPortsDescription AddPortsForComponent(TypeHandle comp, string prefix = null)
        {
            var inputsFromComponentType = HighLevelNodeModelHelpers.GetDataInputsFromComponentType(Stencil, comp).ToList();

            var description = ComponentPortsDescription.FromData(comp, inputsFromComponentType, prefix);

            foreach (Tuple<string, TypeHandle> field in inputsFromComponentType)
            {
                AddDataInput($"{field.Item1}", field.Item2, description.GetFieldId(field.Item1));
            }

            return description;
        }
    }
}
