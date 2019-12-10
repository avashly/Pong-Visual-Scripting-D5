using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/On Start Entities")]
    [Serializable]
    public class OnStartEntitiesNodeModel : OnEntitiesEventBaseNodeModel
    {
        const string k_Title = "On Start Entities";

        protected override string MakeTitle() => k_Title;

        public override UpdateMode Mode => UpdateMode.OnStart;
    }
}
