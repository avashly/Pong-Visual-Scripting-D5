using System;
using System.Linq;
using Packages.VisualScripting.Editor.Elements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEngine.UIElements;

namespace Packages.VisualScripting.Editor.Stencils
{
    public class IteratorStackNode : FunctionNode
    {
        public IteratorStackNode(Store store, IIteratorStackModel model, INodeBuilder builder)
            : base(store, model, builder)
        {
            // TODO: Move affecting rules in a more generic USS file and share with BlackboardEcsProvider
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.uss"));
            // @TODO: This might need to be reviewed in favor of a better / more scalable approach (non preprocessor based)
            // that would ideally bring the same level of backward/forward compatibility and/or removed when a 2013 beta version lands.
#if UNITY_2019_3_OR_NEWER
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(UICreationHelper.TemplatePath + "BlackboardECS.2019.3.uss"));
#endif

            var stackNodeHeaderContainer = this.MandatoryQ("stackNodeHeaderContainer");

            var stencil = Store.GetState().CurrentGraphModel?.Stencil;

            // TODO Temp fix. Should be removed when CoroutineStackModel supports CriteriaModels
            if (model.CriteriaModels != null)
                stackNodeHeaderContainer.Add(new CriteriaSubSection(stencil, model, store));
        }

        public override void FilterOutChildrenFromSelection()
        {
            base.FilterOutChildrenFromSelection();

            if (m_GraphView == null)
                return;

            foreach (var element in m_GraphView.selection.OfType<IVisualScriptingField>()
                     .Cast<GraphElement>()
                     .Where(e => ReferenceEquals(e.GetFirstOfType<IteratorStackNode>(), this))
                     .ToList())
            {
                m_GraphView.RemoveFromSelection(element);
            }
        }
    }
}
