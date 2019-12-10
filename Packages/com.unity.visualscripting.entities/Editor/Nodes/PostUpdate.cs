using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/Post Update")]
    [Serializable]
    public class PostUpdate : EventFunctionModel {}
}
