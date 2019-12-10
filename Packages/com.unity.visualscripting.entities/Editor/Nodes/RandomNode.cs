using System;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Editor;
using UnityEngine;
using UnityEngine.UIElements;
using Node = UnityEditor.VisualScripting.Editor.Node;
using UICreationHelper = Packages.VisualScripting.Editor.UICreationHelper;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public class RandomNode : Node
    {
        static readonly string k_UssClassName = "vs-random-node";

        PopupField<string> m_TitleField;
        EnumField m_ParamField;

        RandomNodeModel Model => (RandomNodeModel)model;

        public RandomNode(RandomNodeModel model, Store store, GraphView graphView) : base(model, store, graphView)
        {
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "Random.uss"));
            AddToClassList(k_UssClassName);

            void OnTitleChange(ChangeEvent<string> e)
            {
                Model.MethodBaseName = e.newValue;
                Model.DefineNode();
                store.Dispatch(new RefreshUIAction(UpdateFlags.RequestCompilation | UpdateFlags.GraphTopology));
            }

            void OnParamChange(ChangeEvent<Enum> e)
            {
                Model.Variant = (RandomNodeModel.ParamVariant)e.newValue;
                Model.DefineNode();
                store.Dispatch(new RefreshUIAction(UpdateFlags.RequestCompilation | UpdateFlags.GraphTopology));
            }

            RegisterCallback<AttachToPanelEvent>(evt =>
            {
                // Title : Random [Float v ] [(min, max) v ]
                if (m_TitleField == null)
                {
                    title = "Random";

                    m_TitleField = new PopupField<string>(RandomNodeModel.BaseMethodNames.ToList(), Model.MethodBaseName);
                    m_TitleField.RegisterValueChangedCallback(OnTitleChange);

                    TitleContainer?.Add(m_TitleField);

                    m_ParamField = new EnumField(Model.Variant);
                    m_ParamField.RegisterValueChangedCallback(OnParamChange);
                    TitleContainer?.Add(m_ParamField);

                    m_ParamField?.SetEnabled(RandomNodeModel.RngMethods.Count(m => m.Name == Model.RngMethod.Name) > 1);
                }
            });

            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                m_TitleField.UnregisterValueChangedCallback(OnTitleChange);
                m_ParamField.UnregisterValueChangedCallback(OnParamChange);
            });
        }
    }
}
