using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Elements
{
    [PublicAPI]
    public class ExpandedContainer : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<ExpandedContainer, UxmlTraits> {}

        public new class UxmlTraits : VisualElement.UxmlTraits {}

        VisualElement m_DragIndicator;

        bool m_Sortable;
        public bool Sortable
        {
            get => m_Sortable;
            set
            {
                m_Sortable = value;
                if (m_Sortable && m_DragIndicator == null)
                {
                    m_DragIndicator = new VisualElement { name = "dragIndicator" };
                    Insert(0, m_DragIndicator);
                }
                else if (!m_Sortable && m_DragIndicator != null)
                {
                    m_DragIndicator.RemoveFromHierarchy();
                    m_DragIndicator = null;
                }
            }
        }

        public ExpandedContainer()
        {
            name = "expandedContainer";
        }

        public void SetDragIndicatorVisible(bool newVisible)
        {
            if (m_DragIndicator != null)
                m_DragIndicator.style.display = newVisible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetDragIndicatorPositionY(float y)
        {
            if (m_DragIndicator != null)
            {
                m_DragIndicator.style.left = 0;
                m_DragIndicator.style.top = y - m_DragIndicator.layout.height / 2;
                m_DragIndicator.style.width = layout.width;
            }
        }

        public int GetInsertionIndex(Vector2 pos)
        {
            int index = -1;

            if (ContainsPoint(pos))
            {
                index = 0;

                foreach (var child in Children())
                {
                    if (!(child is SortableExpandableRow))
                    {
                        ++index;
                        continue;
                    }

                    Rect rect = child.layout;

                    if (pos.y > (rect.y + rect.height / 2))
                        ++index;
                    else
                        break;
                }
            }
            else if (pos.y > 0)
            {
                index = childCount;
            }

            return index;
        }
    }
}
