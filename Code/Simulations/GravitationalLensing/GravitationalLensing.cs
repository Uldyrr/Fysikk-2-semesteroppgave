using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UIElements;


namespace GravitationalLensing
{
    internal struct Blackhole
    {
        public Vector3 Position;
        public float Mass;
        public float SchwarzschildRadius;
        public const int SizeOf = sizeof(float) * 5;

        public Blackhole(Vector3 position, float mass)
        {
            this.Position = position;
            this.Mass = mass;
            this.SchwarzschildRadius = 2.0f * GravitationalLensing.PHYSICS_G * this.Mass / (GravitationalLensing.PHYSICS_C * GravitationalLensing.PHYSICS_C);
        }
    }

    internal struct CelestialObject
    {
        public Vector3 Position;
        public float Radius;
        public const int SizeOf = sizeof(float) * 4;

        public CelestialObject(Vector3 position, float radius)
        {
            this.Position = position;
            this.Radius = radius;
        }
    }

    internal struct Photon
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public const int SizeOf = sizeof(float) * 6;

        public Photon(Vector3 position, Vector3 velocity)
        {
            this.Position = position;
            this.Velocity = velocity;
        }
    }

    internal struct Camera
    {
        public float Fov { get => m_fov; set { m_fov = value; UpdateCameraComponents(); } }
        private float m_fov;
        public Vector2 ScreenSize { get => m_screenSize; set { m_screenSize = value; UpdateCameraComponents(); } }
        private Vector2 m_screenSize;
        public Vector2 ViewportSize { get => m_viewportSize; private set { m_viewportSize = value; } }
        private Vector2 m_viewportSize;
        public float AspectRatio { get => m_aspectRatio; private set { m_aspectRatio = value; } }
        private float m_aspectRatio;
        public const int SizeOf = sizeof(float) * 6;

        public Camera(float fov, Vector2 screenSize)
        {
            m_fov = fov;
            m_screenSize = screenSize;
            m_viewportSize = default;
            m_aspectRatio = default;

            UpdateCameraComponents();
        }

        private void UpdateCameraComponents()
        {
            this.ViewportSize = 2.0f * new Vector2(1.0f, this.ScreenSize.y / this.ScreenSize.x) * Mathf.Tan(this.Fov * Mathf.Deg2Rad * 0.5f);
            this.AspectRatio = this.ScreenSize.x / this.ScreenSize.y;
        }
    }

    public class GravitationalLensing : Simulation
    {
        // Constants
        public const float PHYSICS_AU = 149597870700.0f;
        public const float PHYSICS_G = 6.67430E-11f;
        public const float PHYSICS_C = 299792458.0f;

        public const int SIMULATION_PIXELS = SimulationManager.CAMERA_WIDTH * SimulationManager.CAMERA_HEIGHT;
        public readonly Vector3Int SIMULATION_THREADGROUPS = new Vector3Int(SIMULATION_PIXELS / 1024, 1, 1);

        // Private values
        // -- Simulation
        private RenderTexture m_skyboxTexture;
        private RenderTexture m_celestialObjectTexture;
        private Camera m_camera;
        private CelestialObject m_simulationCelestialObject;
        private Blackhole[] m_simulationBlackholes = new Blackhole[1];

        // -- Compute buffers
        private ComputeBuffer m_cameraBuffer;
        private ComputeBuffer m_celestialObjectBuffer;
        private ComputeBuffer m_blackholesBuffer;

        public override void Init()
        {
            // Simulation values
            // -- Skybox texture
            Texture2D skyboxTexture = SimulationResourcesManager.LoadTexture(SimulationResourcesManager.DIRECTORIES_TEXTURES_GRAVITATIONALLENSING, "Skybox");
            Texture2D celestialObjectTexture = SimulationResourcesManager.LoadTexture(SimulationResourcesManager.DIRECTORIES_TEXTURES_GRAVITATIONALLENSING, "CelestialObject");

            if (skyboxTexture)
            {
                m_skyboxTexture = new RenderTexture(skyboxTexture.width, skyboxTexture.height, 24);
                m_skyboxTexture.enableRandomWrite = true;
                m_skyboxTexture.Create();

                Graphics.Blit(skyboxTexture, m_skyboxTexture);
            } else
            {
                throw new MissingException("Skybox texture");
            }

            if (celestialObjectTexture)
            {
                m_celestialObjectTexture = new RenderTexture(celestialObjectTexture.width, celestialObjectTexture.height, 24);
                m_celestialObjectTexture.enableRandomWrite = true;
                m_celestialObjectTexture.Create();

                Graphics.Blit(celestialObjectTexture, m_celestialObjectTexture);
            } else
            {
                throw new MissingException("CelestialObject texture");
            }

            // -- Camera
            m_camera = new Camera(90.0f, SimulationManager.CAMERA_RESOLUTION);
            
            m_cameraBuffer = new ComputeBuffer(1, Camera.SizeOf);
            m_cameraBuffer.SetData(new Camera[] { m_camera });

            // -- Blackholes
            m_simulationBlackholes[0] = new Blackhole(new Vector3(0.0f, 0.0f, -PHYSICS_AU * 0.001f), 2E31F);

            m_blackholesBuffer = new ComputeBuffer(m_simulationBlackholes.Length, Blackhole.SizeOf);
            m_blackholesBuffer.SetData(m_simulationBlackholes);

            // -- CelestialObject
            m_simulationCelestialObject = new CelestialObject(new Vector3(0.0f, 0.0f, -PHYSICS_AU * 0.01f), 6371000.8f);

            m_celestialObjectBuffer = new ComputeBuffer(1, CelestialObject.SizeOf);
            m_celestialObjectBuffer.SetData(new CelestialObject[] { m_simulationCelestialObject });

            // Update: Set compute shader values
            // -- Textures
            this.ComputeShader.SetTexture(0, "SkyboxTexture", m_skyboxTexture);
            this.ComputeShader.SetTexture(0, "CelestialObjectTexture", m_celestialObjectTexture);

            // -- Compute buffers
            this.ComputeShader.SetBuffer(0, "SimulationCamera", m_cameraBuffer);
            this.ComputeShader.SetBuffer(0, "SimulationBlackholes", m_blackholesBuffer);
            this.ComputeShader.SetBuffer(0, "SimulationCelestialObject", m_celestialObjectBuffer);
        }

        private void PreSimulationUpdate()
        {
            // Update: Smoothly change the fov
            m_camera.Fov = Mathf.Clamp(m_camera.Fov - Input.GetAxis("Mouse ScrollWheel") * 1000 * (m_camera.Fov / 100.0f) * Time.deltaTime, 0.1f, 100.0f);

            m_cameraBuffer.SetData(new Camera[] { m_camera });

            this.ComputeShader.SetBuffer(0, "SimulationCamera", m_cameraBuffer);

            // Update: Move the black hole
            //m_simulationBlackholes[0].Position.x = (Input.mousePosition.x - Screen.width * 0.5f) / (Screen.width * 0.5f) * m_camera.ViewportSize.x * 0.5f * PHYSICS_AU * 0.005f;
            //m_simulationBlackholes[0].Position.y = (Input.mousePosition.y - Screen.height * 0.5f) / (Screen.height * 0.5f) * m_camera.ViewportSize.y * 0.5f * PHYSICS_AU * 0.005f;

            m_blackholesBuffer.SetData(m_simulationBlackholes);

            this.ComputeShader.SetBuffer(0, "SimulationBlackholes", m_blackholesBuffer);
        }

        public override void Dispatch()
        {
            PreSimulationUpdate();

            this.ComputeShader.Dispatch(0, SIMULATION_THREADGROUPS.x, SIMULATION_THREADGROUPS.y, SIMULATION_THREADGROUPS.z);
        }

        public void OnDestroy()
        {
            // Dispose compute buffers
            m_cameraBuffer.Dispose();
            m_celestialObjectBuffer.Dispose();
            m_blackholesBuffer.Dispose();
        }
    }

}
