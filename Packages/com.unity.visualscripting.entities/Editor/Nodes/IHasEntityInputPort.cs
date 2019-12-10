using System;
using UnityEditor.VisualScripting.GraphViewModel;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public interface IHasEntityInputPort : INodeModel
    {
        IPortModel EntityPort { get; }
    }
}
