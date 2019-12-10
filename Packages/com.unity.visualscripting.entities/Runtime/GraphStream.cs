using System;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

[NativeContainer]
public struct GraphStream : IDisposable
{
    public readonly int GraphId;
    public NativeStream NativeStream;
    public JobHandle InputDeps;

    public GraphStream(int graphId, NativeStream nativeStream, JobHandle inputDeps)
    {
        GraphId = graphId;
        NativeStream = nativeStream;
        InputDeps = inputDeps;
    }

    [UsedImplicitly]
    public NativeStream.Reader AsReader()
    {
        return NativeStream.AsReader();
    }

    [UsedImplicitly]
    public Writer AsWriter()
    {
        return new Writer(ref this);
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public struct Writer
    {
        NativeStream.Writer m_Writer;

        public Writer(ref GraphStream graphStream)
        {
            m_Writer = graphStream.NativeStream.AsWriter();
        }

        public void BeginForEachIndex(Entity entity, int index, ulong nodeId1, ulong nodeId2)
        {
            m_Writer.BeginForEachIndex(index);
            m_Writer.Write(TracingRecorderSystem.DataType.Entity);
            m_Writer.Write(entity);
            SetLastCallFrame(nodeId1, nodeId2);
        }

        public void EndForEachIndex()
        {
            m_Writer.EndForEachIndex();
        }

        public void SetLastCallFrame(ulong nodeId1, ulong nodeId2, int offset = 0, byte progress = 0)
        {
            m_Writer.Write(TracingRecorderSystem.DataType.Step);
            m_Writer.Write(nodeId1);
            m_Writer.Write(nodeId2);
            m_Writer.Write(offset);
            m_Writer.Write(progress);
        }

        public T Record<T>(T value, ulong nodeId1, ulong nodeId2) where T : struct
        {
            m_Writer.Write(TracingRecorderSystem.DataType.Data); // not callframe, data
            m_Writer.Write(nodeId1);
            m_Writer.Write(nodeId2);
            m_Writer.Write(typeof(T).TypeHandle);
            m_Writer.Write(value);
            return value;
        }
    }

    public void Dispose()
    {
        NativeStream.Dispose();
    }

    public void CompleteInputDependencies()
    {
        InputDeps.Complete();
    }
}
