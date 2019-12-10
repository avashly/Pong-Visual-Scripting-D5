using System;
using JetBrains.Annotations;

namespace UnityEditor.VisualScripting.Model
{
    public class Unknown
    {
        Unknown() {}
    }

    public class ExecutionFlow
    {
        ExecutionFlow() {}
    }

    public enum VariableType
    {
        FunctionVariable,
        GraphVariable,
        FunctionParameter,
        ComponentQueryField,
    }

    [Flags]
    [PublicAPI]
    public enum ModifierFlags
    {
        None      = 0,
        ReadOnly  = 1 << 0,
        WriteOnly = 1 << 1,
        ReadWrite = 1 << 2,
    }

    [Flags]
    public enum VariableFlags
    {
        None = 0,
        Generated = 1,
        Hidden = 2,
    }

    public enum PortType
    {
        Execution,
        Event,
        Data,
        Instance,
        Loop
    }
}
