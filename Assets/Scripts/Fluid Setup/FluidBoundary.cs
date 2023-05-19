using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PBDFluid
{

    public class FluidBoundary : IDisposable
    {
        private const int THREADS = 128;

        public int NumParticles { get; private set; }

        public Bounds Bounds;

        public float ParticleRadius { get; private set; }

        public float ParticleDiameter { get { return ParticleRadius * 2.0f; } }

        public float Density { get; private set; }

        public ComputeBuffer Positions { get; private set; }

        private ComputeBuffer m_argsBuffer;

        public FluidBoundary(IList<Vector3> Positions, float radius, float density, Matrix4x4 RTS) {
            NumParticles = Positions.Count;
            ParticleRadius = radius;
            Density = density;

            CreateParticles(Positions, RTS);
        }

        /// <summary>
        /// Draws the mesh spheres when draw particles is enabled.
        /// </summary>
        public void Draw(Camera cam, Mesh mesh, Material material, int layer) {
            if (m_argsBuffer == null)
                CreateArgBuffer(mesh.GetIndexCount(0));

            material.SetBuffer("positions", Positions);
            material.SetColor("color", Color.red);
            material.SetFloat("diameter", ParticleDiameter);

            ShadowCastingMode castShadow = ShadowCastingMode.Off;
            bool recieveShadow = false;

            Graphics.DrawMeshInstancedIndirect(mesh, 0, material, Bounds, m_argsBuffer, 0, null, castShadow, recieveShadow, layer, cam);
        }

        public void Dispose() {
            if(Positions != null) {
                Positions.Release();
                Positions = null;
            }

            CBUtility.Release(ref m_argsBuffer);
        }

        private void CreateParticles(IList<Vector3> Positions, Matrix4x4 RTS) {
            Vector4[] positions = new Vector4[NumParticles];

            float inf = float.PositiveInfinity;
            Vector3 min = new Vector3(inf, inf, inf);
            Vector3 max = new Vector3(-inf, -inf, -inf);

            for (int i = 0; i < NumParticles; i++) {
                Vector4 pos = RTS * Positions[i];
                positions[i] = pos;

                if (pos.x < min.x) min.x = pos.x;
                if (pos.y < min.y) min.y = pos.y;
                if (pos.z < min.z) min.z = pos.z;

                if (pos.x > max.x) max.x = pos.x;
                if (pos.y > max.y) max.y = pos.y;
                if (pos.z > max.z) max.z = pos.z;
            }

            min.x -= ParticleRadius;
            min.y -= ParticleRadius;
            min.z -= ParticleRadius;

            max.x += ParticleRadius;
            max.y += ParticleRadius;
            max.z += ParticleRadius;

            Bounds = new Bounds();
            Bounds.SetMinMax(min, max);

            this.Positions = new ComputeBuffer(NumParticles, 4 * sizeof(float));
            this.Positions.SetData(positions);
        }

        private void CreateArgBuffer(uint indexCount) {
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            args[0] = indexCount;
            args[1] = (uint)NumParticles;

            m_argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_argsBuffer.SetData(args);
        }
    }
}