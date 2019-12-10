using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif
using UnityEngine;
using UnityEngine.Assertions;

/// <summary>
/// This systems reads the data stored in each GraphStream from jobs and convert it to the DebuggerTracer format
/// on the main thread
/// </summary>
public class TracingRecorderSystem : ComponentSystem
{
    readonly List<GraphStream> m_GraphStreams = new List<GraphStream>();

    protected override void OnDestroy()
    {
        for (var index = 0; index < m_GraphStreams.Count; index++)
        {
            var graphStream = m_GraphStreams[index];
            graphStream.Dispose();
        }
    }

    protected override void OnUpdate()
    {
        foreach (GraphStream graphStream in m_GraphStreams)
        {
            graphStream.CompleteInputDependencies();
            DebuggerTracer.GraphTrace graphTrace = DebuggerTracer.GetGraphData(graphStream.GraphId, createIfAbsent: true);
            var reader = graphStream.AsReader();
            var frame = Time.frameCount;

            DebuggerTracer.FrameData frameData = null;
            for (int i = 0; i < reader.ForEachCount; i++)
            {
                reader.BeginForEachIndex(i);
                DebuggerTracer.EntityFrameTrace entityData = null;
                while (reader.RemainingItemCount > 0)
                {
                    if (frameData == null)
                        frameData = graphTrace.GetFrameData(frame, createIfAbsent: true);

                    var callFrame = reader.Read<DataType>();
                    ulong nodeId1, nodeId2;
                    switch (callFrame)
                    {
                        case DataType.Entity:
                            var e = ReadEntity(ref reader);
                            entityData = frameData.GetEntityFrameTrace(e.Index, createIfAbsent: true);
                            break;
                        case DataType.Step:
                            Assert.IsTrue(entityData != null);
                            ReadStepRecord(ref reader, out nodeId1, out nodeId2, out var stepOffset, out var progress);
                            entityData.SetLastCallFrame(nodeId1, nodeId2, stepOffset, progress);
                            break;
                        case DataType.Data:
                        {
                            Assert.IsTrue(entityData != null);
                            ReadValueRecord(ref reader, out nodeId1, out nodeId2, out var value);
                            entityData.RecordValue(value, nodeId1, nodeId2);
                            break;
                        }
                    }
                }

                reader.EndForEachIndex();
            }

            graphStream.Dispose();
        }
        m_GraphStreams.Clear();
    }

    internal static Entity ReadEntity(ref NativeStream.Reader reader)
    {
        return reader.Read<Entity>();
    }

    internal static void ReadStepRecord(ref NativeStream.Reader reader, out ulong nodeId1, out ulong nodeId2, out int stepOffset, out byte progress)
    {
        nodeId1 = reader.Read<ulong>();
        nodeId2 = reader.Read<ulong>();
        stepOffset = reader.Read<int>();
        progress = reader.Read<byte>();
    }

    internal static void ReadValueRecord(ref NativeStream.Reader reader, out ulong nodeId1, out ulong nodeId2, out object value)
    {
        nodeId1 = reader.Read<ulong>();
        nodeId2 = reader.Read<ulong>();
        RuntimeTypeHandle typeHandle = reader.Read<RuntimeTypeHandle>();
        Type type = Type.GetTypeFromHandle(typeHandle);
        value = ReadValueFromNode(type, ref reader);
    }

    // TODO value has to be a struct because record<T:struct>(T value)
    /// <summary>
    /// Reads the node value from the GraphStream's underlying NativeStream. Given that the Record method has a struct
    /// constraint on the value, this method is safe
    /// </summary>
    /// <param name="valueType"></param>
    /// <param name="reader"></param>
    /// <returns></returns>
    static unsafe object ReadValueFromNode(Type valueType, ref NativeStream.Reader reader)
    {
        int size = UnsafeUtility.SizeOf(valueType);
        byte* ptr = reader.ReadUnsafePtr(size);
        object value = Marshal.PtrToStructure(new IntPtr(ptr), valueType);
        return value;
    }

    public GraphStream GetRecordingStream(int count, int graphId)
    {
        // the native doesn't like if the count is 0, and default(NativeStreamWriter) in a job would trigger a safety check error
        var recordingStream = new GraphStream(graphId, new NativeStream(math.max(1, count), Allocator.Persistent), new JobHandle());

        return recordingStream;
    }

    public void FlushRecordingStream(GraphStream recordingStream, JobHandle inputDeps)
    {
        recordingStream.InputDeps = inputDeps;
        m_GraphStreams.Add(recordingStream);
    }

    public enum DataType : byte
    {
        Step,
        Data,
        Entity,
    }

    public static DebuggerTracer.EntityFrameTrace GetRecorder(int graphId, int targetIndex)
    {
#if UNITY_EDITOR
        Assert.IsTrue(InternalEditorUtility.CurrentThreadIsMainThread());
#endif
        // TODO cache graphdata/frametrace
        var frame = Time.frameCount;
        DebuggerTracer.GraphTrace graphTrace = DebuggerTracer.GetGraphData(graphId, true);
        var frameData = graphTrace.GetFrameData(frame, createIfAbsent: true);

        var entityData = frameData.GetEntityFrameTrace(targetIndex, createIfAbsent: true);
        return entityData;
    }
}
