using System;
using UnityEngine;

namespace VisualScripting.Entities.Runtime
{
    // Variable and values that can be injected in ICoroutine.MoveNext parameters.
    // See VisualScripting.Entities.Runtime.Wait for an example
    public enum CoroutineInternalVariable
    {
        DeltaTime
    }

    // Tag a Coroutine parameter in MoveNext with this attribute for it to be translated to a predefined variable instead of exposing a port
    // See VisualScripting.Entities.Runtime.Wait for an example
    public class CoroutineInternalVariableAttribute : CoroutineSpecialVariableAttribute
    {
        public CoroutineInternalVariable Variable { get; }

        public CoroutineInternalVariableAttribute(CoroutineInternalVariable variable)
        {
            Variable = variable;
        }
    }
}
