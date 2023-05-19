using UnityEngine;
using System;
using System.Collections.Generic;

using System.IO;
using UnityEngine.SceneManagement;

namespace PBDFluid {

    [Serializable]
    public enum TimeStep { Fast = 1, Medium = 2, Slow = 4, VerySlow = 8 };
    
    [Serializable]
    public enum FluidType { PBD, CSPH, WCSPH }

    public class FluidSetup : MonoBehaviour {

        [Header("Unity References")]
        public Material m_fluidParticleMat;
        public Material m_boundaryParticleMat;
        public Material m_volumeMat;
        public Mesh m_sphereMesh;

        [Header("Debug Settings")]
        public bool m_drawLines = true;
        public bool m_drawGrid = false;
        public bool m_drawBoundaryParticles = false;
        public bool m_drawFluidParticles = false;
        public bool m_drawFluidVolume = true;

        [Header("Particle Settings")]
        public float radius = 0.1f;
        private float density = 0;

        [Header("Simulation Settings")]
        public bool m_run = true;
        public FluidType fluidType = FluidType.PBD;
        public TimeStep timeStep = TimeStep.Fast;
        public Bounds simulationBounds = new Bounds(new Vector3(0, 5, -2), new Vector3(8, 5, 2));
        public Bounds[] fluidSpawnBounds = { new Bounds(new Vector3(-6, 2, -2), new Vector3(2, 2, 2)) };
        public Bounds[] obstacleSpawnBounds = { new Bounds(new Vector3(0, 2, 0), new Vector3(0.2f, 2, 0.2f)) };

        private float dt = 0;
        
        private FluidBody m_fluid;
        private FluidBoundary m_boundary;
        private FluidSolver m_solver;
        private RenderVolume m_volume;

        Bounds outerBounds;

        private bool wasError;

        // STATS
        int iterations = 0;
        int maxIterations = 4;
        List<SimulationResults> results = new List<SimulationResults>();

        [Serializable]
        struct SimulationResults {
            // Simulation Settings
            public int fluidParticles;
            public int boundaryParticles;
            public float radius;
            public FluidType type;
            public float timeStepSize;

            // Simulation Results
            public float duration;
            public int frames;
            public float avgfps;
            public float avgFrametime;

            public SimulationResults(int n, int b, float r, FluidType t, float dt, float d, int f) {
                fluidParticles = n;
                boundaryParticles = b;
                radius = r;
                type = t;
                timeStepSize = dt;
                duration = d;
                frames = f;
                avgfps = frames / duration;
                avgFrametime = duration / frames;
            }
        }
        float startTime;
        float startFrames;

        private void Start() {
            switch (fluidType) {
                case FluidType.PBD:
                    density = 1000;
                    dt = 0.005f;
                    break;
                case FluidType.CSPH:
                case FluidType.WCSPH:
                    density = 1;
                    dt = 0.0008f;
                    break;
            }

            try {
                m_boundary = CreateBoundary(radius, density);
                m_fluid = CreateFluid(radius, density);

                Debug.Log("Fluid Particles = " + m_fluid.NumParticles);
                Debug.Log("Boundary Particles = " + m_boundary.NumParticles);

                m_fluid.Bounds = m_boundary.Bounds;

                switch (fluidType) {
                    case FluidType.PBD:
                        m_solver = new PBDFluidSolver(m_fluid, m_boundary);
                        break;
                    case FluidType.CSPH:
                        m_solver = new CSPHFluidSolver(m_fluid, m_boundary);
                        break;
                    case FluidType.WCSPH:
                        m_solver = new WCSPHFluidSolver(m_fluid, m_boundary);
                        break;
                }

                m_volume = new RenderVolume(m_boundary.Bounds, radius);
                m_volume.CreateMesh(m_volumeMat);
            } catch {
                wasError = true;
                throw;
            }
            startTime = Time.unscaledTime;
            startFrames = Time.frameCount;
        }

        [HideInInspector]
        public void Reset() {
            results.Add(
                new SimulationResults(
                    m_fluid.NumParticles, 
                    m_boundary.NumParticles,
                    radius,
                    fluidType,
                    dt / (int)timeStep,
                    Time.unscaledTime - startTime,
                    (int)(Time.frameCount - startFrames)
                )
            );
            iterations++;

            OnDestroy();
            Start();
        }

        private void Update() {
            if (Input.GetKey("escape") || wasError || radius <= 0.07) {
                Reset();
                writeResults();
                if (Application.isEditor) {
                    UnityEditor.EditorApplication.isPlaying = false;
                } else {
                    Application.Quit();
                }
            }

            if (!wasError && m_run) {
                m_solver.StepPhysics(dt / (int)timeStep);
                m_volume.FillVolume(m_fluid, m_solver.Hash, m_solver.Kernel);
            }

            m_volume.Hide = !m_drawFluidVolume;

            if (m_drawBoundaryParticles)
                m_boundary.Draw(Camera.main, m_sphereMesh, m_boundaryParticleMat, 0);

            if (m_drawFluidParticles)
                m_fluid.Draw(Camera.main, m_sphereMesh, m_fluidParticleMat, 0);

            if (Time.frameCount >= 1500 + startFrames)
                Reset();

            if (maxIterations <= iterations) {
                Reset();
                writeResults();
                Reset();
                radius -= 0.02f;
                results = new List<SimulationResults>();
                iterations = 0;
            }
        }

        private void OnDestroy() {
            m_boundary.Dispose();
            m_fluid.Dispose();
            m_solver.Dispose();
            m_volume.Dispose();
        }

        private void OnApplicationQuit() {
        }

        private void OnRenderObject() {
            Camera camera = Camera.current;
            if (camera != Camera.main) return;

            if (m_drawLines) {
                DrawBounds(camera, Color.green, outerBounds);
                DrawBounds(camera, Color.red, simulationBounds);
                foreach (Bounds b in fluidSpawnBounds)
                    DrawBounds(camera, Color.blue, b);
            }

            if(m_drawGrid)
                m_solver.Hash.DrawGrid(camera, Color.yellow);
        }

        private FluidBoundary CreateBoundary(float radius, float density) {
            List<Vector3> Positions = new List<Vector3>();

            // Multiple by 1.2 adds extra thickness in case the radius does not evenly divide into the bounds size,
            // you might have particles missing from one side of the source bounds other wise.
            Vector3 outerBoundsOffset = Vector3.one * radius * 1.2f;

            outerBounds = new Bounds();
            outerBounds.SetMinMax(simulationBounds.min - outerBoundsOffset, simulationBounds.max + outerBoundsOffset);

            // Add boundary particles
            Positions.AddRange(ParticleGenerator.CreateBoundaryParticles(radius * 2, outerBounds, simulationBounds));

            // Add obstacles particles
            foreach (Bounds b in obstacleSpawnBounds)
                Positions.AddRange(ParticleGenerator.CreateParticles(radius * 2f, b));

            // The source will create a array of particles evenly spaced between the inner and outer bounds.
            return new FluidBoundary(Positions, radius, density, Matrix4x4.identity);
        }

        private FluidBody CreateFluid(float radius, float density) {
            List<Vector3> Positions = new List<Vector3>();

            // The sources will create arrays of particles evenly spaced inside the bounds.
            Vector3 particleOffset = Vector3.one * radius;
            foreach (Bounds b in fluidSpawnBounds) {
                b.SetMinMax(b.min + particleOffset, b.max - particleOffset);
                Positions.AddRange(ParticleGenerator.CreateParticles(radius * 1.8f, b));
            }

            return new FluidBody(Positions, radius, density, Matrix4x4.identity, fluidType);
        }

        private static IList<int> m_cube = new int[] {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        public Vector4[] GetCorners(Bounds b) {
            Vector4[] m_corners = new Vector4[8];
            m_corners[0] = new Vector4(b.min.x, b.min.y, b.min.z, 1);
            m_corners[1] = new Vector4(b.min.x, b.min.y, b.max.z, 1);
            m_corners[2] = new Vector4(b.max.x, b.min.y, b.max.z, 1);
            m_corners[3] = new Vector4(b.max.x, b.min.y, b.min.z, 1);

            m_corners[4] = new Vector4(b.min.x, b.max.y, b.min.z, 1);
            m_corners[5] = new Vector4(b.min.x, b.max.y, b.max.z, 1);
            m_corners[6] = new Vector4(b.max.x, b.max.y, b.max.z, 1);
            m_corners[7] = new Vector4(b.max.x, b.max.y, b.min.z, 1);
            return m_corners;
        }

        private void DrawBounds(Camera cam, Color col, Bounds bounds) {
            DrawLines.LineMode = LINE_MODE.LINES;
            DrawLines.Draw(cam, GetCorners(bounds), col, Matrix4x4.identity, m_cube);
        }

        private void writeResults() {
            using (StreamWriter writer = File.AppendText("Results.txt")) {
                writer.WriteLine(SceneManager.GetActiveScene().name);
                writer.WriteLine("Iterations: " + (results.Count - 2));
                SimulationResults avg = new SimulationResults(
                    m_fluid.NumParticles, 
                    m_boundary.NumParticles,
                    results[1].radius,
                    fluidType,
                    results[1].timeStepSize,
                    results[1].duration,
                    results[1].frames
                );
                for (int i = 2; i < results.Count - 1; i++) {
                    avg.radius += results[i].radius;
                    avg.timeStepSize += results[i].timeStepSize;
                    avg.duration += results[i].duration;
                    avg.frames += results[i].frames;
                    avg.avgfps += results[i].avgfps;
                    avg.avgFrametime += results[i].avgFrametime;
                }
                avg.radius /= (results.Count - 2);
                avg.timeStepSize /= (results.Count - 2);
                avg.duration /= (results.Count - 2);
                avg.frames /= (results.Count - 2);
                avg.avgfps /= (results.Count - 2);
                avg.avgFrametime /= (results.Count - 2);

                writer.WriteLine(JsonUtility.ToJson(avg, true));
            }
        }
    }
}