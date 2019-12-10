using System;
using System.Collections.Generic;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Stencils
{
    public interface IIteratorStackModel : IFunctionModel, ICriteriaModelContainer
    {
        ComponentQueryDeclarationModel ComponentQueryDeclarationModel { get; }
        VariableDeclarationModel ItemVariableDeclarationModel { get; }
    }

    interface IPrivateIteratorStackModel : IIteratorStackModel
    {
        IList<VariableDeclarationModel> FunctionParameters { get; }
        UpdateMode Mode { get; }
        int NonComponentVariablesCount { get; }
    }
}
