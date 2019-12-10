using System;
using UnityEditor.VisualScripting.Model;

namespace Packages.VisualScripting.Editor.Stencils
{
    public static class ComponentLoopVariableExtensions
    {
        public static IIteratorStackModel GetComponentQueryDeclarationModel(this LoopVariableDeclarationModel loopVariableDeclarationModel)
        {
            return (loopVariableDeclarationModel.Owner as IIteratorStackModel);
        }
    }
}
