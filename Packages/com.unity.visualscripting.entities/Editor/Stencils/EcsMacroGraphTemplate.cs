using System;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    public class EcsMacroGraphTemplate : ICreatableGraphTemplate
    {
        public Type StencilType => typeof(MacroStencil);
        public string GraphTypeName => "My Macro";
        public string DefaultAssetName => "MyMacro";

        public void InitBasicGraph(VSGraphModel graphModel)
        {
            ((MacroStencil)graphModel.Stencil).SetParent(typeof(EcsStencil), graphModel);
        }
    }
}
