using System;
using UnityEditor.VisualScripting.GraphViewModel;

namespace UnityEditor.VisualScriptingTests
{
    [Serializable]
    class TestNodeModel : NodeModel
    {
        protected override void OnDefineNode()
        {
            AddDataInput<float>("one");
        }
    }
}
