using UnityEditor.Experimental.GraphView;
using UnityEditor.VisualScripting.Editor;
using UnityEditor.VisualScripting.Model;
using UnityEditor.VisualScripting.Model.Stencils;

namespace Packages.VisualScripting.Editor.Stencils
{
    [GraphtoolsExtensionMethods]
    static class EcsGraphElementFactoryExtensions
    {
        public static GraphElement CreateOrderedStack(this INodeBuilder builder, Store store, IOrderedStack model)
        {
            var functionNode = new IteratorStackNode(store, (IPrivateIteratorStackModel)model, builder);
            return functionNode;
        }

        public static GraphElement CreateIteratorStack(this INodeBuilder builder, Store store, OnKeyPressEcsNodeModel model)
        {
            var iteratorStackNode = new OnKeyPressStackNode(store, model, builder);
            return iteratorStackNode;
        }

        public static GraphElement CreateIteratorStack(this INodeBuilder builder, Store store, IIteratorStackModel model)
        {
            var iteratorStackNode = new IteratorStackNode(store, model, builder);
            return iteratorStackNode;
        }

        public static GraphElement CreateIteratorStack(this INodeBuilder builder, Store store, OnEntitiesEventBaseNodeModel model)
        {
            var iteratorStackNode = new IteratorStackNode(store, model, builder);
            return iteratorStackNode;
        }

        public static GraphElement CreateInstantiateNode(this INodeBuilder builder, Store store, InstantiateNodeModel model)
        {
            var functionNode = new InstantiateNode(model, store, builder.GraphView);
            return functionNode;
        }

        public static GraphElement CreateCreateEntityNode(this INodeBuilder builder, Store store, CreateEntityNodeModel model)
        {
            var functionNode = new CreateEntityNode(model, store, builder.GraphView);
            return functionNode;
        }

        public static GraphElement CreateRandomNode(this INodeBuilder builder, Store store, RandomNodeModel model)
        {
            return new RandomNode(model, store, builder.GraphView);
        }
    }
}
