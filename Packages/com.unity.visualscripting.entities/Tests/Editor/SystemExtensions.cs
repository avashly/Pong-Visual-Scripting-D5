using System;
using System.Reflection;
using Unity.Entities;
using Unity.Jobs;

namespace UnityEditor.VisualScriptingECSTests
{
    static class SystemExtensions
    {
        static readonly FieldInfo k_Field = typeof(JobComponentSystem).GetField("m_PreviousFrameDependency", BindingFlags.NonPublic | BindingFlags.Instance);

        public static void Complete(this ComponentSystemBase system)
        {
            if (system is JobComponentSystem jobSystem)
            {
                JobHandle handle = (JobHandle)k_Field.GetValue(jobSystem);
                handle.Complete();
            }
        }
    }
}
