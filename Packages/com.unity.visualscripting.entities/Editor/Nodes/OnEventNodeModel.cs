using System;
using UnityEditor.VisualScripting.GraphViewModel;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [Serializable]
    public class OnEventNodeModel : OnEntitiesEventBaseNodeModel
    {
        public override UpdateMode Mode => UpdateMode.OnEvent;
        public TypeHandle EventTypeHandle;

        protected override string MakeTitle() =>  $"On {EventTypeHandle.Name(Stencil)}";

        protected override void OnCreateLoopVariables(VariableCreator variableCreator, IPortModel connectedPortModel)
        {
            base.OnCreateLoopVariables(variableCreator, connectedPortModel);
            variableCreator.DeclareVariable<LoopVariableDeclarationModel>(
                "ev",
                EventTypeHandle,
                LoopStackModel.TitleComponentIcon.Item, VariableFlags.None);
        }

        public static string GetBufferName(Type eventType)
        {
            return $"{eventType.Name}Buffer";
        }
    }
}
