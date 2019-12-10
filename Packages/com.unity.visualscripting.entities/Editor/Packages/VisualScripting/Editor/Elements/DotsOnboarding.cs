using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Elements
{
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    class DotsOnboarding : VSOnboardingProvider
    {
        public string Title => "DOTS";
        public VisualElement CreateOnboardingElement(Store store)
        {
            return new DotsOnboardingElement(store);
        }
    }

    class DotsOnboardingElement : VisualElement
    {
        static readonly GUIContent k_NoScriptAssetSelectedText = VseUtility.CreatTextContent("Select a GraphAsset to edit its Visual Script");
        static readonly GUIContent k_MultipleGameObjectsSelectedText = VseUtility.CreatTextContent("Multiple GameObjects selected.");
        static readonly GUIContent k_NoGameObjectSelectedText = VseUtility.CreatTextContent("No GameObject selected.");
        static readonly GUIContent k_DropGameObjectText = VseUtility.CreatTextContent("Drop a single GameObject to create a graph");
        static readonly GUIContent k_OrDragAndDropText = VseUtility.CreatTextContent("Or drag and drop a GameObject");

        const string k_LabelWarningClass = "warning-hint";
        const string k_HideClass = "label-hidden";
        const string k_DropLabelClass = "drop-label";
        const string k_DropRejectedClass = "drop-rejected";

        readonly Store m_Store;

        List<Button> m_ButtonsDependingOnSelection;
        List<Label> m_LabelsDependingOnSelection;

        int m_NumSelectedGameObjects;
        string m_SelectedGameObjectsText;

        GameObject m_SelectedGameObject;
        Label m_DropLabel;

        public DotsOnboardingElement(Store store)
        {
            m_Store = store;
            m_ButtonsDependingOnSelection = new List<Button>();
            m_LabelsDependingOnSelection = new List<Label>();

            m_DropLabel = new Label { text = k_DropGameObjectText.text };
            m_DropLabel.AddToClassList(k_DropLabelClass);
            m_DropLabel.AddToClassList(k_HideClass);

            RegisterCallback<AttachToPanelEvent>(OnEnterPanel);
            RegisterCallback<DetachFromPanelEvent>(OnLeavePanel);
        }

        void OnEnterPanel(AttachToPanelEvent e)
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "DotsOnboarding.uss"));

            Selection.selectionChanged += OnSelectionChanged;

            RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            RegisterCallback<DragPerformEvent>(OnDragPerform);
            RegisterCallback<DragLeaveEvent>(OnDragLeave);
            RegisterCallback<DragEnterEvent>(OnDragEnter);

            UpdateUI();
        }

        void OnDragEnter(DragEnterEvent evt)
        {
            m_DropLabel.RemoveFromClassList(k_HideClass);
        }

        void OnDragLeave(DragLeaveEvent evt)
        {
            m_DropLabel.AddToClassList(k_HideClass);
        }

        void OnDragPerform(DragPerformEvent evt)
        {
            var gameObject = DragAndDrop.objectReferences.OfType<GameObject>().Single();

            var template = new DropGameObjectEcsTemplate(gameObject, this.WorldToLocal(evt.mousePosition));
            template.PromptToCreate(m_Store);
        }

        void OnDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.OfType<GameObject>().Count() == 1)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
            m_DropLabel.EnableInClassList(k_DropRejectedClass, DragAndDrop.visualMode == DragAndDropVisualMode.Rejected);
        }

        void OnLeavePanel(DetachFromPanelEvent e)
        {
            // ReSharper disable once DelegateSubtraction
            Selection.selectionChanged -= OnSelectionChanged;

            UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
            UnregisterCallback<DragPerformEvent>(OnDragPerform);
            UnregisterCallback<DragLeaveEvent>(OnDragLeave);
            UnregisterCallback<DragEnterEvent>(OnDragEnter);
        }

        void OnSelectionChanged()
        {
            UpdateSelectionRelatedElements();
        }

        void UpdateSelectionRelatedElements()
        {
            GameObject[] gameObjects = Selection.gameObjects;
            m_NumSelectedGameObjects = gameObjects.Length;
            if (m_NumSelectedGameObjects == 1)
            {
                m_SelectedGameObject = gameObjects[0];
            }
            else
            {
                m_SelectedGameObject = null;
            }

            foreach (var button in m_ButtonsDependingOnSelection)
            {
                button.SetEnabled(m_SelectedGameObject != null);
            }

            foreach (var label in m_LabelsDependingOnSelection)
            {
                label.text = (m_NumSelectedGameObjects > 0) ? k_MultipleGameObjectsSelectedText.text : k_NoGameObjectSelectedText.text;
                label.EnableInClassList(k_HideClass, m_SelectedGameObject != null);
            }
        }

        void UpdateUI()
        {
            Clear();
            m_ButtonsDependingOnSelection.Clear();
            m_LabelsDependingOnSelection.Clear();

            Add(new Label { text = k_NoScriptAssetSelectedText.text });

            var ecsGraphTemplate = new EcsGraphTemplate();
            Add(new Button(
                () => ecsGraphTemplate.PromptToCreate(m_Store))
                { text = "Create " + ecsGraphTemplate.GraphTypeName });

            Button buttonFromObject = new Button(
                () =>
                {
                    var template = new DropGameObjectEcsTemplate(m_SelectedGameObject);
                    template.PromptToCreate(m_Store);
                })
            { text = "Create from selected GameObject" };
            m_ButtonsDependingOnSelection.Add(buttonFromObject);
            Add(buttonFromObject);

            var label = new Label("");
            label.AddToClassList(k_LabelWarningClass);
            m_LabelsDependingOnSelection.Add(label);
            Add(label);

            var macroGraphTemplate = new EcsMacroGraphTemplate();
            Add(new Button(() => macroGraphTemplate.PromptToCreate(m_Store)) { text = "Create Macros" });

            Add(new Label(k_OrDragAndDropText.text));

            UpdateSelectionRelatedElements();

            m_DropLabel.StretchToParentSize();
            Add(m_DropLabel);
        }
    }
}
