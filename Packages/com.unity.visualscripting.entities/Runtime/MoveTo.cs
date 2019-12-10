using System;
using JetBrains.Annotations;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VisualScripting;
using static VisualScripting.Entities.Runtime.CoroutineInternalVariable;
using InternalVar = VisualScripting.Entities.Runtime.CoroutineInternalVariableAttribute;

namespace VisualScripting.Entities.Runtime
{
    [PublicAPI]
    [VisualScriptingFriendlyName("Move To")]
    public struct MoveTo : ICoroutine
    {
        public float3 Destination;
        public float Duration;

        public bool MoveNext([InternalVar(DeltaTime)] float deltaTime, ref Translation translation)
        {
#if UNITY_EDITOR
            if (m_TotalDuration < math.FLT_MIN_NORMAL && Duration > 0f)
            {
                m_TotalDuration = Duration;
            }
#endif
            translation.Value = math.lerp(translation.Value, Destination, deltaTime / Duration);
            Duration -= deltaTime;

            return Duration > 0f;
        }

#if UNITY_EDITOR
        float m_TotalDuration;
        public float SafeTotalDuration => math.max(m_TotalDuration, math.FLT_MIN_NORMAL); // always > 0
        public float Progress => 1f - (Duration / SafeTotalDuration);

        public byte GetProgress()
        {
            return (byte)(Byte.MaxValue - (byte)Mathf.Clamp(byte.MaxValue * Progress, 0f, 255f));
        }

#endif
    }
}
