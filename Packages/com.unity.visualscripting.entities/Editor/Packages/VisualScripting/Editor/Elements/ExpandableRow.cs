using System;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Elements
{
    [PublicAPI]
    public class ExpandableRow : GraphElement
    {
        public VisualElement ExpandableRowTitleContainer { get; }
        public ExpandedContainer ExpandedContainer { get; }
        protected Button ExpandedButton { get; }
        protected Label SectionTitle { get; }

        public bool Sortable
        {
            get => ExpandedContainer.Sortable;
            set => ExpandedContainer.Sortable = value;
        }

        bool m_Expanded;
        protected internal bool Expanded
        {
            get => m_Expanded;
            set
            {
                m_Expanded = value;
                UpdateExpandedClasses();
                OnExpanded?.Invoke(m_Expanded);
            }
        }

        protected internal Action<bool> OnExpanded { internal get; set; }

        void UpdateExpandedClasses()
        {
            EnableInClassList("expanded", m_Expanded);
            ExpandedContainer.EnableInClassList("expanded", m_Expanded);
        }

        protected internal ExpandableRow(string sectionTitle)
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.uss"));
            // @TODO: This might need to be reviewed in favor of a better / more scalable approach (non preprocessor based)
            // that would ideally bring the same level of backward/forward compatibility and/or removed when a 2013 beta version lands.
#if UNITY_2019_3_OR_NEWER
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.2019.3.uss"));
#endif

            AddToClassList("expandableRow");

            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UICreationHelper.TemplatePath + "ExpandableRow.uxml").CloneTree(this);

            ExpandableRowTitleContainer = this.MandatoryQ("expandableRowTitleContainer");

            ExpandedContainer = this.MandatoryQ<ExpandedContainer>("expandedContainer");

            SectionTitle = this.MandatoryQ<Label>("sectionTitle");
            SectionTitle.text = sectionTitle;

            ExpandedButton = this.MandatoryQ<Button>("expandButton");
            ExpandedButton.clickable.clicked += () => { Expanded = !Expanded; };

            UpdateExpandedClasses();
        }
    }
}
