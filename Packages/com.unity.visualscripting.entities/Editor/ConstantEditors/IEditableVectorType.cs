using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;

namespace UnityEditor.VisualScripting.Entities.Editor
{
    public interface IEditableVectorType<T>
    {
        string UssClassName { get; }
        IReadOnlyList<string> PropertyLabels { get; }
        TextValueField<T> MakePreviewField(string propertyLabel);
    }
}
