using System;
using UnityEditor.UIElements;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Stencils
{
    public class OnKeyPressStackNode : IteratorStackNode
    {
        EnumField m_KeyCodeEnumField;
        EnumField m_PressTypeEnumField;

        public OnKeyPressStackNode(Store store, OnKeyPressEcsNodeModel model, INodeBuilder builder)
            : base(store, model, builder)
        {
            void OnKeyCodeChange(ChangeEvent<Enum> e)
            {
                model.Code = (KeyCode)e.newValue;
                store.Dispatch(new RefreshUIAction(UpdateFlags.RequestCompilation));
            }

            void OnKeyPressTypeChange(ChangeEvent<Enum> e)
            {
                model.PressType = (OnKeyPressEcsNodeModel.KeyPressType)e.newValue;
                store.Dispatch(new RefreshUIAction(UpdateFlags.RequestCompilation));
            }

            RegisterCallback<AttachToPanelEvent>(evt =>
            {
                if (m_KeyCodeEnumField == null)
                {
                    title = "On";

                    m_KeyCodeEnumField = new EnumField(model.Code);
                    m_KeyCodeEnumField.RegisterValueChangedCallback(OnKeyCodeChange);
                    TitleElement.Add(m_KeyCodeEnumField);

                    m_PressTypeEnumField = new EnumField(model.PressType);
                    m_PressTypeEnumField.RegisterValueChangedCallback(OnKeyPressTypeChange);

                    TitleElement.Add(m_PressTypeEnumField);
                }
            });

            RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                m_KeyCodeEnumField.UnregisterValueChangedCallback(OnKeyCodeChange);
                m_PressTypeEnumField.UnregisterValueChangedCallback(OnKeyPressTypeChange);
            });
        }
    }
}
