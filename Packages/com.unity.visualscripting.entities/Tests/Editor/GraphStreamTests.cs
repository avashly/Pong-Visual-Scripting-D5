using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using UnityEditor.VisualScripting.Editor.Plugins;
using BinaryReader = System.IO.BinaryReader;
using BinaryWriter = System.IO.BinaryWriter;

public class GraphStreamTests
{
    [Test]
    public void RuntimeTypeHandlesWorks()
    {
        Assert.AreEqual(sizeof(bool), UnsafeUtility.SizeOf(Type.GetTypeFromHandle(typeof(bool).TypeHandle)));
    }

    [Test]
    public void WriteBoolWorks()
    {
        ulong nodeGuid1 = 1;
        ulong nodeGuid2 = 2;

        GraphStream stream = new GraphStream(1, new NativeStream(1, Allocator.Persistent), new JobHandle());
        try
        {
            var writer = stream.AsWriter();
            writer.BeginForEachIndex(new Entity { Index = 42 }, 0, nodeGuid1, nodeGuid2);
            var value = true;
            writer.Record(value, nodeGuid1, nodeGuid2);
            writer.EndForEachIndex();

            var reader = stream.NativeStream.AsReader();

            reader.BeginForEachIndex(0);
            Assert.AreEqual(TracingRecorderSystem.DataType.Entity, reader.Read<TracingRecorderSystem.DataType>());
            var entity = TracingRecorderSystem.ReadEntity(ref reader);
            Assert.AreEqual(42, entity.Index);

            ulong guid1, guid2;
            Assert.AreEqual(TracingRecorderSystem.DataType.Step, reader.Read<TracingRecorderSystem.DataType>());
            TracingRecorderSystem.ReadStepRecord(ref reader, out guid1, out guid2, out var offset, out _);
            Assert.AreEqual(nodeGuid1, guid1);
            Assert.AreEqual(nodeGuid2, guid2);
            Assert.AreEqual(0, offset);

            Assert.AreEqual(TracingRecorderSystem.DataType.Data, reader.Read<TracingRecorderSystem.DataType>());
            TracingRecorderSystem.ReadValueRecord(ref reader, out guid1, out guid2, out var readValue);
            Assert.AreEqual(nodeGuid1, guid1);
            Assert.AreEqual(nodeGuid2, guid2);
            Assert.AreEqual(value, readValue);

            reader.EndForEachIndex();
        }
        finally
        {
            stream.Dispose();
        }
    }

    [Test]
    public void TraceSerializationRoundTrip()
    {
        var data = new TraceDump("path", new[]
        {
            new DebuggerTracer.FrameData(10, new Dictionary<DebuggerTracer.FrameData.EntityReference, DebuggerTracer.EntityFrameTrace>
            {
                [new DebuggerTracer.FrameData.EntityReference { EntityIndex = 2 }] = new DebuggerTracer.EntityFrameTrace
                {
                    steps = new List<DebuggerTracer.EntityFrameTrace.NodeRecord>
                    {
                        new DebuggerTracer.EntityFrameTrace.NodeRecord
                        {
                            nodeId1 = 0x20, nodeId2 = 0x21,
                        }
                    },
                    values = new Dictionary<int, List<DebuggerTracer.EntityFrameTrace.ValueRecord>>
                    {
                        [0] = new List<DebuggerTracer.EntityFrameTrace.ValueRecord>
                        {
                            new DebuggerTracer.EntityFrameTrace.ValueRecord
                            {
                                nodeId1 = 32, nodeId2 = 33, readableValue = "asd"
                            }
                        }
                    }
                }
            }),
            new DebuggerTracer.FrameData(11, new Dictionary<DebuggerTracer.FrameData.EntityReference, DebuggerTracer.EntityFrameTrace>
            {
                [new DebuggerTracer.FrameData.EntityReference { EntityIndex = 2 }] = new DebuggerTracer.EntityFrameTrace
                {
                    steps = new List<DebuggerTracer.EntityFrameTrace.NodeRecord>
                    {
                        new DebuggerTracer.EntityFrameTrace.NodeRecord
                        {
                            nodeId1 = 32, nodeId2 = 33,
                        }
                    },
                    values = new Dictionary<int, List<DebuggerTracer.EntityFrameTrace.ValueRecord>>
                    {
                        [0] = new List<DebuggerTracer.EntityFrameTrace.ValueRecord>
                        {
                            new DebuggerTracer.EntityFrameTrace.ValueRecord
                            {
                                nodeId1 = 32, nodeId2 = 33, readableValue = "asd"
                            }
                        }
                    }
                }
            }),
        });

        var ms = new MemoryStream();
        try
        {
            using (var w = new BinaryWriter(ms, new UTF8Encoding(false, true), true))
            {
                data.Serialize(w);
                w.Flush();
            }

            var buffer = ms.GetBuffer();
            File.WriteAllBytes("dump.bin", buffer);
            ms = new MemoryStream(buffer, false);
            using (BinaryReader sr = new BinaryReader(ms))
            {
                var data2 = TraceDump.Deserialize(sr);
                Assert.AreEqual(data.FrameData.Length, data2.FrameData.Length);
            }
        }
        finally
        {
            ms.Dispose();
        }
    }
}
