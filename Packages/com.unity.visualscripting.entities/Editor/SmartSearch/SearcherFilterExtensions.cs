using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using UnityEditor.VisualScripting.Editor.SmartSearch;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;
using UnityEngine;

namespace UnityEditor.VisualScripting.SmartSearch
{
    public static class SearcherFilterExtensions
    {
        public static SearcherFilter WithComponentData(this SearcherFilter self, Stencil stencil)
        {
            self.RegisterType(data => typeof(IComponentData).IsAssignableFrom(data.Type.Resolve(stencil)));
            return self;
        }

        public static SearcherFilter WithComponentData(this SearcherFilter self, Stencil stencil,
            HashSet<TypeHandle> excluded)
        {
            if (excluded == null)
                throw new ArgumentException();

            self.RegisterType(data => typeof(IComponentData).IsAssignableFrom(data.Type.Resolve(stencil)) && !excluded.Contains(data.Type));
            return self;
        }

        public static SearcherFilter WithGameObjectComponents(this SearcherFilter self, Stencil stencil)
        {
            self.RegisterType(data => typeof(Component).IsAssignableFrom(data.Type.Resolve(stencil)));
            return self;
        }

        public static SearcherFilter WithSharedComponentData(this SearcherFilter self, Stencil stencil)
        {
            self.RegisterType(data => typeof(ISharedComponentData).IsAssignableFrom(data.Type.Resolve(stencil)));
            return self;
        }

        public static SearcherFilter WithComponents(this SearcherFilter self, IEnumerable<TypeHandle> types)
        {
            self.RegisterType(data => types.Contains(data.Type));
            return self;
        }

        public static SearcherFilter WithControlFlowExcept(this SearcherFilter self, IStackModel stackModel,
            IEnumerable<Type> exceptions)
        {
            self.RegisterControlFlow(data =>
                !exceptions.Any(e => e.IsAssignableFrom(data.Type)) && stackModel.AcceptNode(data.Type));
            return self;
        }
    }
}
