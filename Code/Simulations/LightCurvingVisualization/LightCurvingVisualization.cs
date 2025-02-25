using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


namespace LightCurvingVisualization
{
    internal struct Photon
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float4 Color;
        public const int Sizeof = sizeof(float) * 8;

        public Photon(Vector2 position, Vector2 velocity, float4 color)
        {
            this.Position = position;
            this.Velocity = velocity;
            this.Color = color;
        }
    }

    public class LightCurvingVisualization : Simulation
    {
        // Constants
        public const float PHYSICS_C = 299792458.0f;
        public const float PHYSICS_AU = 149597870700.0f;

        public const int SIMULATION_PARTICLES = 1024000;
        public readonly Vector3Int SIMULATION_THREADGROUPSIZE = new Vector3Int(SIMULATION_PARTICLES / 1024, 1, 1);

        // Private values
        // -- Simulation
        private Photon[] m_photons = new Photon[SIMULATION_PARTICLES];

        public override void Init()
        {
            SimulationInit();
        }

        private void SimulationInit()
        {
            Vector2 positionExtents = new Vector2(PHYSICS_AU * 0.02f, PHYSICS_AU * 0.05f);
            Vector2 photonPosition;

            for (int i = 0; i < m_photons.Length; i++)
            {
                photonPosition = new Vector2(
                    UnityEngine.Random.Range(-positionExtents.x, positionExtents.x),  // - PHYSICS_AU * 0.05f
                    UnityEngine.Random.Range(-positionExtents.y, positionExtents.y)
                );

                m_photons[i] = new Photon(
                photonPosition,
                new Vector2(
                    PHYSICS_C,
                    0.0f
                ), new float4(
                    1.0f,
                    0.0f,
                    Mathf.Abs(photonPosition.y) / positionExtents.y,
                    1.0f
                ));
            }

            ComputeBuffer photonsBuffer = new ComputeBuffer(m_photons.Length, Photon.Sizeof);
            photonsBuffer.SetData(m_photons);

            this.ComputeShader.SetBuffer(0, "Photons", photonsBuffer);
        }

        public override void Dispatch()
        {
            this.ComputeShader.Dispatch(0, SIMULATION_THREADGROUPSIZE.x, SIMULATION_THREADGROUPSIZE.y, SIMULATION_THREADGROUPSIZE.z);
        }
    }
}



