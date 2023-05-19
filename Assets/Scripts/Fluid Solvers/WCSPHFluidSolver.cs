using System;
using System.Collections.Generic;
using UnityEngine;

namespace PBDFluid
{

    public class WCSPHFluidSolver : FluidSolver {

        public WCSPHFluidSolver(FluidBody body, FluidBoundary boundary) : base(body, boundary) {
            m_shader = Resources.Load("ComputeShaders/WCSPHSolver") as ComputeShader;
        }

        public override void StepPhysics(float dt)
        {

            if (dt <= 0.0) return;

            m_shader.SetInt("NumParticles", Body.NumParticles);
            m_shader.SetVector("Gravity", new Vector3(0.0f, -9.81f, 0.0f));
            m_shader.SetFloat("Dampning", Body.Dampning);
            m_shader.SetFloat("DeltaTime", dt);
            m_shader.SetFloat("Viscosity", 0.25f);//Body.Viscosity);
            m_shader.SetFloat("ParticleMass", Body.ParticleMass);
            m_shader.SetFloat("GasConstant", 1000f); // WCSPH
            m_shader.SetFloat("RestDensity", Body.Density / 2f); // WCSPH

            m_shader.SetFloat("BoundaryPSI", Mathf.Pow(Boundary.Density, 2) / (315.0f / (64.0f * Mathf.PI * Mathf.Pow(Kernel.Radius, 3))));

            m_shader.SetFloat("KernelRadius", Kernel.Radius);
            m_shader.SetFloat("KernelRadius2", Kernel.Radius2);
            m_shader.SetFloat("Poly6Zero", Kernel.Poly6(Vector3.zero));
            m_shader.SetFloat("Poly6", Kernel.POLY6);
            m_shader.SetFloat("SpikyGrad", Kernel.SPIKY_GRAD);
            m_shader.SetFloat("ViscLap", Kernel.VISC_LAP);

            m_shader.SetFloat("HashScale", Hash.InvCellSize);
            m_shader.SetVector("HashSize", Hash.Bounds.size);
            m_shader.SetVector("HashTranslate", Hash.Bounds.min);

            //Predicted and velocities use a double buffer as solver step
            //needs to read from many locations of buffer and write the result
            //in same pass. Could be removed if needed as long as buffer writes 
            //are atomic. Not sure if they are.

            //Hash.Process(Body.Positions);
            Hash.Process(Body.Positions, Boundary.Positions);

            ComputeDensityPressure();

            ComputeForces();

            Integrate(dt);
        }

        public void ComputeDensityPressure() {
            int ComputeDensity = m_shader.FindKernel("ComputeDensity");

            m_shader.SetBuffer(ComputeDensity, "Positions", Body.Positions);
            m_shader.SetBuffer(ComputeDensity, "Densities", Body.Densities);
            m_shader.SetBuffer(ComputeDensity, "Pressures", Body.Pressures);

            m_shader.SetBuffer(ComputeDensity, "Boundary", Boundary.Positions);

            m_shader.SetBuffer(ComputeDensity, "IndexMap", Hash.IndexMap);
            m_shader.SetBuffer(ComputeDensity, "Table", Hash.Table);

            m_shader.Dispatch(ComputeDensity, Groups, 1, 1);
        }

        private void ComputeForces() {
            int ComputeForces = m_shader.FindKernel("ComputeForces");

            m_shader.SetBuffer(ComputeForces, "Positions", Body.Positions);
            m_shader.SetBuffer(ComputeForces, "Velocities", Body.VelocitiesSPH);
            m_shader.SetBuffer(ComputeForces, "Densities", Body.Densities);

            m_shader.SetBuffer(ComputeForces, "Boundary", Boundary.Positions);

            m_shader.SetBuffer(ComputeForces, "Pressures", Body.Pressures);
            m_shader.SetBuffer(ComputeForces, "IndexMap", Hash.IndexMap);
            m_shader.SetBuffer(ComputeForces, "Table", Hash.Table);

            m_shader.SetBuffer(ComputeForces, "Forces", Body.Forces);

            m_shader.Dispatch(ComputeForces, Groups, 1, 1);
        }

        private void Integrate(float dt) {
            int Integrate = m_shader.FindKernel("Integrate");

            m_shader.SetBuffer(Integrate, "Positions", Body.Positions);
            m_shader.SetBuffer(Integrate, "Velocities", Body.VelocitiesSPH);

            m_shader.SetBuffer(Integrate, "Densities", Body.Densities);
            m_shader.SetBuffer(Integrate, "IndexMap", Hash.IndexMap);
            m_shader.SetBuffer(Integrate, "Table", Hash.Table);

            m_shader.SetBuffer(Integrate, "Forces", Body.Forces);

            m_shader.Dispatch(Integrate, Groups, 1, 1);
        }
    }
}