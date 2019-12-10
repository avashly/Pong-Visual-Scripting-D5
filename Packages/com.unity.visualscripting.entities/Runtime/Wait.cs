using System;
using JetBrains.Annotations;
using Unity.Mathematics;
using UnityEngine;

using static VisualScripting.Entities.Runtime.CoroutineInternalVariable;
using InternalVar = VisualScripting.Entities.Runtime.CoroutineInternalVariableAttribute;

namespace VisualScripting.Entities.Runtime
{
    [PublicAPI]
    public struct Wait : ICoroutine
    {
        public float Time;

        // deltaTime will be injected during on translation, no port will be created on the node
        public bool MoveNext([InternalVar(DeltaTime)] float deltaTime)
        {
#if UNITY_EDITOR
            if (m_TotalTime == 0)
                m_TotalTime = Time;
#endif
            Time -= deltaTime;
#if UNITY_EDITOR
            if (Time < 0)
                m_TotalTime = 0;
#endif
            return Time > 0;
        }

#if UNITY_EDITOR
        float m_TotalTime;
        public byte GetProgress()
        {
            return (byte)(Byte.MaxValue -  (byte)Mathf.Clamp(byte.MaxValue * math.max(Time, 0) / m_TotalTime, 0f, 255f));
        }

#endif
    }
}
