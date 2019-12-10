using System.Collections.Generic;
using System.Linq;
using UnityEditor.VisualScripting.GraphViewModel;
using UnityEditor.VisualScripting.Model.Stencils;

namespace UnityEditor.VisualScripting.Extensions
{
    // ReSharper disable once InconsistentNaming
    public static class IStackModelExtensions
    {
        static HashSet<IStackModel> s_Visited;

        public static bool ContainsCoroutine(this IStackModel stack)
        {
            if (s_Visited == null)
                s_Visited = new HashSet<IStackModel>();
            else
                s_Visited.Clear();
            s_Visited.Add(stack);
            return ContainsCoroutine(stack, s_Visited);
        }

        static bool ContainsCoroutine(IStackModel stack, HashSet<IStackModel> visited)
        {
            if (stack.NodeModels.Any(n => n is CoroutineNodeModel))
                return true;

            foreach (var outputPort in stack.OutputPorts)
                foreach (var connection in outputPort.ConnectionPortModels)
                    if (connection.NodeModel is IStackModel nextStack && visited.Add(nextStack) && ContainsCoroutine(nextStack, visited))
                        return true;

            return false;
        }
    }
}
