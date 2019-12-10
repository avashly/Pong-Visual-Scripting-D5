using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using UnityEngine.UIElements;
using Node = UnityEditor.VisualScripting.Editor.Node;
using Port = UnityEditor.VisualScripting.Editor.Port;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public class CreateEntityNode : Node
    {
        public CreateEntityNode(INodeModel model, Store store, GraphView graphView) : base(model, store, graphView)
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.templatePath + "Node.uss"));
            // @TODO: This might need to be reviewed in favor of a better / more scalable approach (non preprocessor based)
            // that would ideally bring the same level of backward/forward compatibility and/or removed when a 2013 beta version lands.
#if UNITY_2019_3_OR_NEWER
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.templatePath + "Node.2019.3.uss"));
#endif
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(Packages.VisualScripting.Editor.UICreationHelper.TemplatePath + "InstantiateNode.uss"));
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.templatePath + "PropertyField.uss"));
        }

        CreateEntityNodeModel Model => (CreateEntityNodeModel)model;

        protected override void UpdateFromModel()
        {
            base.UpdateFromModel();
            AddToClassList("instantiateNode");
            Add(new Button(ShowComponentPicker) { name = "addInstantiateComponentButton", text = "+ Add Component" });
        }

        void ShowComponentPicker()
        {
            var filter = new SearcherFilter(SearcherContext.Type)
                .WithComponentData(Model.GraphModel.Stencil, new HashSet<TypeHandle>(Model.GetEditableComponents().Select(c => c)))
                .WithSharedComponentData(Model.GraphModel.Stencil);

            //Fetch typename
            var stencil = Model.GraphModel.Stencil;
            SearcherService.ShowTypes(stencil, Event.current.mousePosition, (th, _) =>
                AddComponentOperation(th), filter);
        }

        protected override void UpdateInputPortModels()
        {
            Port.CreateInputPort(m_Store, Model.InstancePort, inputContainer, inputContainer);
            //TODO deal with presence of same component type in both lists

            foreach (var compType in Model.GetEditableComponents())
            {
                var componentContainer = new VisualElement {name = "instantiateComponentNode"};
                var componentTitleContainer = new VisualElement {name = "componentDataTitle"};
                componentContainer.Add(new VisualElement {name = "componentSeparatorLine"});
                componentContainer.Add(componentTitleContainer);

                var deleteComponentButton = new Button { name = "deleteComponentIcon" };
                deleteComponentButton.clickable.clicked += () => { DeleteComponentOperation(compType); };
                componentTitleContainer.Insert(0, deleteComponentButton);

                deleteComponentButton.EnableInClassList("deletable", true);
                componentTitleContainer.Add(deleteComponentButton);
                componentTitleContainer.Add(new Label(compType.Name(Model.GraphModel.Stencil)) {name = "componentTitle"});
                componentTitleContainer.Add(new VisualElement { name = "filler" });

                inputContainer.Add(componentContainer);
                {
                    var componentPortsContainer = new VisualElement {name = "componentDataPorts"};
                    componentContainer.Add(componentPortsContainer);
                    foreach (var port in Model.GetPortsForComponent(compType))
                        Port.CreateInputPort(m_Store, port, componentPortsContainer, componentPortsContainer);
                }
            }
        }

        void AddComponentOperation(TypeHandle componentType)
        {
            var action = new SetOperationForComponentTypeInCreateEntityNodeAction(Model, componentType);
            m_Store.Dispatch(action);
        }

        void DeleteComponentOperation(TypeHandle componentType)
        {
            var action = new RemoveOperationForComponentTypeInCreateEntityNodeAction(Model, componentType);
            m_Store.Dispatch(action);
        }
    }
}
