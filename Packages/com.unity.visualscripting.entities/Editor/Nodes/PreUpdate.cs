using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/Pre Update")]
    [Serializable]
    public class PreUpdate : EventFunctionModel {}
}
