using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VisualScripting;

[Node]
public static class GeneratePoint
{
    /// <summary>
    /// Tiny mersenne twister
    /// </summary>
    public unsafe struct TinyMt
    {
        const int k_Tinymt32Sh0 = 1;
        const int k_Tinymt32Sh1 = 10;
        const int k_Tinymt32Sh8 = 8;
        const uint k_Tinymt32Mask = 0x7fffffff;
        const float k_Tinymt32Mul = (1.0f / 4294967296.0f);

        const int k_MinLoop = 8;
        const int k_PreLoop = 8;

        fixed uint m_S[4];

        uint m_M1;
        uint m_M2;
        uint m_Mt;

        void NextState()
        {
            uint x;
            uint y;

            y = m_S[3];
            x = (m_S[0] & k_Tinymt32Mask)
                ^ m_S[1]
                ^ m_S[2];
            x ^= (x << k_Tinymt32Sh0);
            y ^= (y >> k_Tinymt32Sh0) ^ x;
            m_S[0] = m_S[1];
            m_S[1] = m_S[2];
            m_S[2] = x ^ (y << k_Tinymt32Sh1);
            m_S[3] = y;
            uint mask = (uint)-((int)(y & 1));
            m_S[1] ^= mask & m_M1;
            m_S[2] ^= mask & m_M2;
        }

        uint Temper()
        {
            uint t0, t1;
            t0 = m_S[3];
            t1 = m_S[0] + (m_S[2] >> k_Tinymt32Sh8);
            t0 ^= t1;
            t0 ^= (uint)-((int)(t1 & 1)) & m_Mt;
            return t0;
        }

        void Init(uint seed, uint mat1, uint mat2, uint tmat)
        {
            m_S[0] = seed;
            m_S[1] = m_M1 = mat1;
            m_S[2] = m_M2 = mat2;
            m_S[3] = m_Mt = tmat;
            for (uint i = 1; i < k_MinLoop; i++)
            {
                m_S[i & 3] ^= i + 1812433253
                    * (m_S[(i - 1) & 3]
                        ^ (m_S[(i - 1) & 3] >> 30));
            }

            for (int i = 0; i < k_PreLoop; i++)
            {
                NextState();
            }
        }

        public void Init(uint seed)
        {
            Init(seed, 0x8f7011ee, 0xfc78ff1f, 0x3793fdff);
        }

        public uint Generate()
        {
            NextState();
            return Temper();
        }

        /// <summary>
        /// Generates a float between 0 included and 1 excluded
        /// </summary>
        /// <returns>The generated value</returns>
        public float GenerateFloat()
        {
            NextState();
            return Temper() * k_Tinymt32Mul;
        }
    }

    public struct SamplerCircle : IEnumerable<float3>, IEnumerator<float3>
    {
        TinyMt rnd;
        float3 center;
        float radius;
        float3 normal;
        public int Count;

        public SamplerCircle(int count, float3 center, float radius, float3 normal)
        {
            this.rnd = new TinyMt();
            rnd.Init(1);
            this.center = center;
            this.Count = count;
            this.radius = radius;
            this.normal = normal;
        }

        public void Reset()
        {
            rnd.Init(1);
        }

        object IEnumerator.Current => Current;

        public float3 Current
        {
            get
            {
                var a = math.fmod(rnd.GenerateFloat(), math.PI * 2.0f);
                float3 v = radius * math.normalize(new float3 { x = math.cos(a), y = 0, z = math.sin(a) });
                var up = new float3(0, 1, 0);
                quaternion q = quaternion.LookRotation(normal, normal.Equals(up) ? new float3(1, 0, 0) : up);
                return center + math.rotate(q, v);
            }
        }

        public bool MoveNext()
        {
            return --Count >= 0;
        }

        IEnumerator<float3> IEnumerable<float3>.GetEnumerator()
        {
            return GetEnumerator();
        }

        public SamplerCircle GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose() {}
    }

    public struct SamplerSphere : IEnumerable<float3>, IEnumerator<float3>
    {
        TinyMt rnd;
        float3 center;
        float radius;
        public int Count;

        public SamplerSphere(int count, float3 center, float radius)
        {
            this.rnd = new TinyMt();
            rnd.Init(1);
            this.center = center;
            this.Count = count;
            this.radius = radius;
        }

        public void Reset()
        {
            rnd.Init(1);
        }

        object IEnumerator.Current => Current;

        public float3 Current => center + radius * math.normalize(new float3
        {
            x = -1.0f + 2.0f * rnd.GenerateFloat(),
            y = -1.0f + 2.0f * rnd.GenerateFloat(),
            z = -1.0f + 2.0f * rnd.GenerateFloat(),
        });

        public bool MoveNext()
        {
            return --Count >= 0;
        }

        IEnumerator<float3> IEnumerable<float3>.GetEnumerator()
        {
            return GetEnumerator();
        }

        public SamplerSphere GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose() {}
    }

    public static SamplerCircle RandomPointsOnCircle(float3 center, float radius, int count, float3 normal)
    {
        return new SamplerCircle(count, center, radius, normal.Equals(float3.zero) ? new float3(0, 1, 0) : normal);
    }

    public static SamplerSphere RandomPointsOnSphere(float3 center, float radius, int count)
    {
        return new SamplerSphere(count, center, radius);
    }
}
