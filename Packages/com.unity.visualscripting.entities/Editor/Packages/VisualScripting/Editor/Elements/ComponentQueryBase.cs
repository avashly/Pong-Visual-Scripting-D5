using System;
using Packages.VisualScripting.Editor.Redux.Actions;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine.UIElements;
using Blackboard = UnityEditor.VisualScripting.Editor.Blackboard;

namespace Packages.VisualScripting.Editor.Elements
{
    abstract class ComponentQueryBase : GraphElement, IHighlightable, IRenamable
    {
        public IGraphElementModel GraphElementModel => ComponentQueryDeclarationModel;

        public bool Highlighted
        {
            get => Pill.highlighted;
            set => Pill.highlighted = value;
        }

        TextField m_TextField;
        string Text => Pill.text;

        Pill Pill { get; }
        public ComponentQueryDeclarationModel ComponentQueryDeclarationModel { get; }
        VseGraphView m_GraphView;
        protected VseGraphView GraphView => m_GraphView ?? (m_GraphView = GetFirstAncestorOfType<Blackboard>().GraphView);
        public Store Store { get; }
        public string TitleValue => Text;
        public VisualElement TitleEditor => m_TextField;
        public VisualElement TitleElement => this;
        public bool IsFramable() => false;

        public bool EditTitleCancelled { get; set; } = false;

        public virtual RenameDelegate RenameDelegate => OpenTextEditor;

        protected Blackboard.RebuildCallback RebuildCallback { get; }

        protected ComponentQueryBase(ComponentQueryDeclarationModel componentQueryDeclarationModel,
                                     Store store,
                                     Blackboard.RebuildCallback rebuildCallback)
        {
            ComponentQueryDeclarationModel = componentQueryDeclarationModel;
            Store = store;
            RebuildCallback = rebuildCallback;

            viewDataKey = componentQueryDeclarationModel.GetId();
            userData = componentQueryDeclarationModel;

            ClearClassList();

            Pill = new Pill { text = componentQueryDeclarationModel.Title };
            Add(Pill);

            m_TextField = new TextField { name = "componentQueryBaseTextField", isDelayed = true };
            m_TextField.style.display = DisplayStyle.None;
            Add(m_TextField);

            var textInput = m_TextField.Q(TextField.textInputUssName);
            textInput.RegisterCallback<FocusOutEvent>(_ => OnEditTextFinished());

            capabilities |= Capabilities.Renamable;

            this.AddManipulator(new ContextualMenuManipulator(OnContextualMenuEvent));
        }

        void OnContextualMenuEvent(ContextualMenuPopulateEvent evt)
        {
            GraphView.BuildContextualMenu(evt);
        }

        void OnEditTextFinished()
        {
            Pill.style.display = DisplayStyle.Flex;
            m_TextField.style.display = DisplayStyle.None;

            if (Text != m_TextField.text)
                Store.Dispatch(new RenameComponentQueryAction(ComponentQueryDeclarationModel, m_TextField.text));
        }

        protected void OpenTextEditor()
        {
            Pill.style.display = DisplayStyle.None;
            m_TextField.SetValueWithoutNotify(Text);
            m_TextField.style.display = DisplayStyle.Flex;
            m_TextField.Q(TextField.textInputUssName).Focus();
            m_TextField.SelectAll();
        }

        public bool ShouldHighlightItemUsage(IGraphElementModel model)
        {
            switch (model)
            {
                case VariableNodeModel variableNodeModel
                    when ReferenceEquals(ComponentQueryDeclarationModel, variableNodeModel.DeclarationModel):
                    return true;
                case ComponentQueryDeclarationModel queryDeclarationModel
                    when ReferenceEquals(ComponentQueryDeclarationModel, queryDeclarationModel):
                    return true;
            }

            return false;
        }
    }
}
