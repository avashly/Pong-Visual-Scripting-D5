using System;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEditor.VisualScripting.SmartSearch;
using UnityEngine;
using VisualScripting.Entities.Runtime;

namespace Packages.VisualScripting.Editor.Stencils
{
    public class EcsSearcherFilterProvider : ClassSearcherFilterProvider
    {
        readonly Stencil m_Stencil;

        public EcsSearcherFilterProvider(Stencil stencil)
            : base(stencil)
        {
            m_Stencil = stencil;
        }

        public override SearcherFilter GetStackSearcherFilter(IStackModel stackModel)
        {
            if (stackModel is IPrivateIteratorStackModel ism && ism.Mode == UpdateMode.OnEnd)
            {
                return new SearcherFilter(SearcherContext.Stack)
                    .WithVisualScriptingNodes(stackModel)
                    .WithUnaryOperators()
                    .WithControlFlowExcept(stackModel, new[] { typeof(ICoroutine) })
                    .WithProperties()
                    .WithMethods()
                    .WithFunctionReferences()
                    .WithMacros();
            }

            return base.GetStackSearcherFilter(stackModel);
        }

        public override SearcherFilter GetOutputToGraphSearcherFilter(IPortModel portModel)
        {
            var queryType = typeof(EntityQuery).GenerateTypeHandle(m_Stencil);
            if (portModel.DataType.Equals(queryType))
            {
                return new SearcherFilter(SearcherContext.Graph)
                    .WithVisualScriptingNodes(typeof(IIteratorStackModel));
            }

            return base.GetOutputToGraphSearcherFilter(portModel);
        }

        public override SearcherFilter GetOutputToStackSearcherFilter(IPortModel portModel, IStackModel stackModel)
        {
            var queryType = typeof(EntityQuery).GenerateTypeHandle(m_Stencil);
            if (portModel.DataType.Equals(queryType))
            {
                return new SearcherFilter(SearcherContext.Stack)
                    .WithControlFlow(typeof(IIteratorStackModel), stackModel);
            }

            return base.GetOutputToStackSearcherFilter(portModel, stackModel);
        }
    }
}
