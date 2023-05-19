using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PBDFluid {

    public static class ParticleGenerator {

        // Fills bounds with evenly spaced particles
        public static IList<Vector3> CreateParticles(float spacing, Bounds bounds) {
            Vector3Int particleCount = Vector3Int.FloorToInt(bounds.size / spacing);
            List<Vector3> Positions = new List<Vector3>((int)particleCount.x * particleCount.y * particleCount.z);

            for (int z = 0; z < particleCount.z; z++) {
                for (int y = 0; y < particleCount.y; y++) {
                    for (int x = 0; x < particleCount.x; x++) {
                        Vector3 pos = bounds.min + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * spacing;
                        Positions.Add(pos);
                    }
                }
            }
            return Positions;
        }

        // Fills bounds with evenly spaced particles, excluding particles inside exclusion zone
        public static IList<Vector3> CreateBoundaryParticles(float spacing, Bounds bounds, Bounds exclusion) {
            Vector3Int particleCount = Vector3Int.FloorToInt(bounds.size / spacing) + Vector3Int.one;
            List<Vector3> Positions = new List<Vector3>((int)particleCount.x * particleCount.y * particleCount.z);

            for (int z = 0; z < particleCount.z; z++) {
                for (int y = 0; y < particleCount.y; y++) {
                    for (int x = 0; x < particleCount.x; x++) {
                        Vector3 pos = bounds.min + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * spacing;
                        if(!exclusion.Contains(pos))
                            Positions.Add(pos);
                    }
                }
            }
            return Positions;
        }
    }
}