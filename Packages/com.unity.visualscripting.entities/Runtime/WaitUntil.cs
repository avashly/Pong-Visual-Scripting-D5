using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.VisualScripting;

namespace VisualScripting.Entities.Runtime
{
    [PublicAPI]
    [VisualScriptingFriendlyName("Wait Until")]
    public struct WaitUntil : ICoroutine
    {
#if UNITY_EDITOR
        public byte GetProgress()
        {
            return m_Done ? byte.MaxValue : byte.MinValue;
        }

        bool m_Done;
#endif

        public bool MoveNext(bool endCondition)
        {
#if UNITY_EDITOR
            if (endCondition)
                m_Done = true;
#endif
            return !endCondition;
        }
    }
}
