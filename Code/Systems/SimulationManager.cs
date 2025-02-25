using System;
using UnityEngine;


public abstract class Simulation : MonoBehaviour
{
    // Public values
    // -- Static refrences
    public ComputeShader ComputeShader { get => m_computeShader; }
    [SerializeField] protected ComputeShader m_computeShader;

    public Material RenderingMaterial { get => m_renderingMaterial; }
    [SerializeField] protected Material m_renderingMaterial;

    // -- Simulation
    public RenderTexture RenderTexture { get; set; }

    public abstract void Init();
    public abstract void Dispatch();
}


public class MissingException : Exception
{
    public MissingException(string name) : base($"\"{name}\" is missing!") { }
}


public class SimulationManager : MonoBehaviour
{
    // Constants
    public const int CAMERA_WIDTH = 1920;
    public const int CAMERA_HEIGHT = 880;
    public static readonly Vector2Int CAMERA_RESOLUTION = new Vector2Int(CAMERA_WIDTH, CAMERA_HEIGHT);  // Note: Needs to be divisble by at least 1024 (considering 32 threads per wavefront)

    // Public values
    public Simulation Simulation { get => m_simulation; }
    [SerializeField] private Simulation m_simulation;

    // Private values
    // -- Simulation
    private bool m_simulationPaused = false;

    // -- Camera
    private Vector2 m_cameraPosition = Vector2.zero;
    private float m_cameraZoom = 1.0f;

    private void Init()
    {
        // Simulation.RenderTexture init
        this.Simulation.RenderTexture = new RenderTexture(CAMERA_WIDTH, CAMERA_HEIGHT, 24);
        this.Simulation.RenderTexture.enableRandomWrite = true;
        this.Simulation.RenderTexture.Create();

        this.Simulation.ComputeShader.SetTexture(0, "TargetTexture", this.Simulation.RenderTexture);

        // Simulation init
        this.Simulation.Init();
    }

    private void UpdateSimulationInput()
    {
        // Update: Simulation paused
        if (Input.GetKeyDown(KeyCode.Space))
            m_simulationPaused = !m_simulationPaused;
    }

    private void UpdateCameraInput()
    {
        // Update: Camera movement
        Vector2 cameraMovement = new Vector2(Input.GetKey(KeyCode.A) ? 1.0f : (Input.GetKey(KeyCode.D) ? -1.0f : 0.0f), Input.GetKey(KeyCode.W) ? -1.0f : (Input.GetKey(KeyCode.S) ? 1.0f : 0.0f));
        cameraMovement *= CAMERA_WIDTH / m_cameraZoom * Time.deltaTime;

        m_cameraPosition += cameraMovement;

        // Update: Camera zoom
        m_cameraZoom *= Input.GetKeyDown(KeyCode.E) ? 2.0f : (Input.GetKeyDown(KeyCode.Q) ? 0.5f : 1.0f);

        // Update: Simulation compute shader values
        this.Simulation.ComputeShader.SetFloat("CameraPositionX", m_cameraPosition.x);
        this.Simulation.ComputeShader.SetFloat("CameraPositionY", m_cameraPosition.y);
        this.Simulation.ComputeShader.SetFloat("CameraZoom", m_cameraZoom);
    }

    private void ClearRenderTexture(RenderTexture renderTexture)
    {
        RenderTexture activeTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = activeTexture;
    }

    private void UpdateCameraRenderedImage(RenderTexture source, RenderTexture destination)
    {
        // Clear texture
        ClearRenderTexture(destination);

        if (m_simulationPaused == false)
        {
            ClearRenderTexture(this.Simulation.RenderTexture);

            // Run simulation compute shader
            this.Simulation.Dispatch();
        }
        
        // Copy latest simulation frame to camera
        Graphics.Blit(this.Simulation.RenderTexture, destination, this.Simulation.RenderingMaterial);
    }

    private void Awake()
    {
        Init();
    }

    private void Update()
    {
        UpdateSimulationInput();
        UpdateCameraInput();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        UpdateCameraRenderedImage(source, destination);
    }
}
