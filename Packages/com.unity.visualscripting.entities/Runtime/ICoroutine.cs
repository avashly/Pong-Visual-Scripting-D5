using System;
using UnityEngine;

namespace VisualScripting.Entities.Runtime
{
    // Implementing this interface allows creating custom coroutine nodes
    public interface ICoroutine
    {
        // your must implement a method called MoveNext(), optionally with parameters,
        //    its parameters will be added as Input Ports on the Node
        //        See WaitUntil for example
        //    Some parameters can have their value injected by the translator rather than create a port for them
        //        See Wait for example

        // bool MoveNext();

#if UNITY_EDITOR
        byte GetProgress();
#endif
    }
}
