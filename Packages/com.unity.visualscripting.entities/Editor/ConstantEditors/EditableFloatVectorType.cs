using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Model;
using UnityEngine;

namespace UnityEditor.VisualScripting.Entities.Editor
{
    public class EditableFloatVectorType : IEditableVectorType<float>
    {
        public string UssClassName => "vs-inline-float-editor";

        public IReadOnlyList<string> PropertyLabels { get; }

        public EditableFloatVectorType(IEnumerable<string> propertyNames)
        {
            PropertyLabels = propertyNames.Select(p => p.ToUpper()).ToList();
        }

        public TextValueField<float> MakePreviewField(string propertyLabel)
        {
            return new FloatField(propertyLabel);
        }
    }
}
