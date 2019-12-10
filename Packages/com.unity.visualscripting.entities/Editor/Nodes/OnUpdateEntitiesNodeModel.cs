using System;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEngine;

namespace UnityEditor.VisualScripting.Model.Stencils
{
    [SearcherItem(typeof(EcsStencil), SearcherContext.Graph, "Events/On Update Entities")]
    [Serializable]
    public class OnUpdateEntitiesNodeModel : OnEntitiesEventBaseNodeModel
    {
        const string k_Title = "On Update Entities";

        protected override string MakeTitle() => k_Title;

        public override UpdateMode Mode => UpdateMode.OnUpdate;
    }
}
