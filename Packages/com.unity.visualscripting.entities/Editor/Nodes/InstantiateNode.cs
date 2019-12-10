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
    public class InstantiateNode : Node
    {
        public InstantiateNode(INodeModel model, Store store, GraphView graphView) : base(model, store, graphView)
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

        InstantiateNodeModel Model => (InstantiateNodeModel)model;

        protected override void UpdateFromModel()
        {
            base.UpdateFromModel();
            AddToClassList("instantiateNode");
            Add(new Button(ShowComponentPicker) { name = "addInstantiateComponentButton", text = "+ Add Component" });
        }

        void ShowComponentPicker()
        {
            var filter = new SearcherFilter(SearcherContext.Type)
                .WithComponentData(Model.GraphModel.Stencil, new HashSet<TypeHandle>(Model.GetEditableComponents().Select(c => c.Type)))
                .WithSharedComponentData(Model.GraphModel.Stencil);

            //Fetch typename
            var stencil = Model.GraphModel.Stencil;
            SearcherService.ShowTypes(stencil, Event.current.mousePosition, (th, _) =>
                SetComponentOperation(th, ComponentOperation.ComponentOperationType.AddComponent), filter);
        }

        protected override void UpdateInputPortModels()
        {
            Port.CreateInputPort(m_Store, Model.InstancePort, inputContainer, inputContainer);
            Port.CreateInputPort(m_Store, Model.EntityPort, titleContainer, inputContainer);
            //TODO deal with presence of same component type in both lists

            foreach (var componentOperation in Model.GetEditableComponents())
            {
                TypeHandle compType = componentOperation.Type;
                var componentContainer = new VisualElement {name = "instantiateComponentNode"};
                var componentTitleContainer = new VisualElement {name = "componentDataTitle"};
                componentContainer.Add(new VisualElement {name = "componentSeparatorLine"});
                componentContainer.Add(componentTitleContainer);

                var deleteComponentButton = new Button { name = "deleteComponentIcon" };
                deleteComponentButton.clickable.clicked += () => { DeleteComponentOperation(compType); };
                componentTitleContainer.Insert(0, deleteComponentButton);

                deleteComponentButton.EnableInClassList("deletable", !componentOperation.FromArchetype);
                componentTitleContainer.Add(deleteComponentButton);

                componentTitleContainer.Add(new Label(compType.Name(Model.GraphModel.Stencil)) {name = "componentTitle"});

                componentTitleContainer.Add(new VisualElement { name = "filler" });

                var operationType = new EnumField(componentOperation.OperationType) {name = "componentOperationType"};

                //Do I need to unregister it afterwards?
                //Also, allocation a closure for each component operations... meh?
                operationType.RegisterValueChangedCallback(eventValue => SetComponentOperation(compType, (ComponentOperation.ComponentOperationType)eventValue.newValue));
                componentTitleContainer.Add(operationType);
                inputContainer.Add(componentContainer);

                if (componentOperation.OperationType != ComponentOperation.ComponentOperationType.RemoveComponent)
                {
                    var componentPortsContainer = new VisualElement {name = "componentDataPorts"};
                    componentContainer.Add(componentPortsContainer);
                    foreach (var port in Model.GetPortsForComponent(componentOperation.Type))
                        Port.CreateInputPort(m_Store, port, componentPortsContainer, componentPortsContainer);
                }
            }
        }

        void SetComponentOperation(TypeHandle componentType, ComponentOperation.ComponentOperationType newValue)
        {
            var action = new SetOperationForComponentTypeInInstantiateNodeAction(Model, componentType, newValue);
            m_Store.Dispatch(action);
        }

        void DeleteComponentOperation(TypeHandle componentType)
        {
            var action = new RemoveOperationForComponentTypeInInstantiateNodeAction(Model, componentType);
            m_Store.Dispatch(action);
        }
    }
}
