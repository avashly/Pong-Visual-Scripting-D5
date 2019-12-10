using System;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public interface IUniqueNameProvider
    {
        bool AddTakenName(string proposedName);
    }
}
