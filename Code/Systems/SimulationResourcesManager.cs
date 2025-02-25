using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SimulationResourcesManager
{
    // Constants
    public const string DIRECTORIES_TEXTURES_GRAVITATIONALLENSING = "Assets/Textures/GravitationalLensing/";

    public static Texture2D LoadTexture(string textureDirectory, string textureName)
    {
        return Resources.Load<Texture2D>(textureDirectory + textureName);
    }

    public static List<Texture2D> LoadTexturesFromDirectory(string textureDirectory)
    {
        return Resources.LoadAll<Texture2D>(textureDirectory).ToList();  
    }
}
