using System;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace Packages.VisualScripting.Editor.Elements
{
    class ComponentPill : GraphElement
    {
        public ComponentPill(ComponentDefinition component, string name, string tooltip)
        {
            ClearClassList();

            var pill = new Pill
            {
                text = name,
                icon = component.IsShared
                    ? Resources.Load("BlackboardFieldLed",
                    typeof(Texture2D)) as Texture2D
                    : null,
                tooltip = tooltip
            };
            pill.EnableInClassList("sharedComponent", component.IsShared);
            Add(pill);
        }
    }
}
