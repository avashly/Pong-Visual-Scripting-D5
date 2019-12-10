using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/On End Entities")]
    [Serializable]
    public class OnEndEntitiesNodeModel : OnEntitiesEventBaseNodeModel
    {
        const string k_Title = "On End Entities";

        protected override string MakeTitle() => k_Title;

        public override UpdateMode Mode => UpdateMode.OnEnd;
    }
}
