using System;
using System.Collections.Generic;
using UnityEngine;

namespace PBDFluid
{

    public abstract class FluidSolver : IDisposable
    {

        protected const int THREADS = 128;
        protected const int READ = 0;
        protected const int WRITE = 1;

        public int Groups { get; protected set; }

        public FluidBoundary Boundary { get; protected set; }

        public FluidBody Body { get; protected set; }

        public GridHash Hash { get; protected set; }

        public SmoothingKernel Kernel { get; protected set; }

        protected ComputeShader m_shader;

        public FluidSolver(FluidBody body, FluidBoundary boundary) {
            Body = body;
            Boundary = boundary;

            float cellSize = Body.ParticleRadius * 4.0f;
            int total = Body.NumParticles + Boundary.NumParticles;
            Hash = new GridHash(Boundary.Bounds, total, cellSize);
            Kernel = new SmoothingKernel(cellSize);

            int numParticles = Body.NumParticles;
            Groups = numParticles / THREADS;
            if (numParticles % THREADS != 0) Groups++;
        }

        public void Dispose() {
            Hash.Dispose();
        }

        public abstract void StepPhysics(float dt);
    }
}