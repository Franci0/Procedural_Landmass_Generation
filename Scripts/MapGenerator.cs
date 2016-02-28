using UnityEngine;
using System.Collections;

public class MapGenerator : MonoBehaviour
{
	public DrawMode drawMode;
	public int mapWidth;
	public int mapHeight;
	public float noiseScale;

	public int octaves;
	[Range (0, 1)]
	public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;

	public bool autoUpdate;

	public TerrainType[] regions;

	public enum DrawMode
	{
		NoiseMap,
		ColorMap,
		Mesh
	}

	public void GenerateMap ()
	{
		float[,] noiseMap = Noise.GenerateNoiseMap (mapWidth, mapHeight, seed, noiseScale, octaves, persistance, lacunarity, offset);

		Color[] colorMap = new Color[mapWidth * mapHeight];

		for (int y = 0; y < mapHeight; y++) {
			for (int x = 0; x < mapWidth; x++) {
				float currentHeight = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (currentHeight <= regions [i].height) {
						colorMap [y * mapWidth + x] = regions [i].color;
						break;
					}
				}
			}
		}

		MapDisplay mapDisplay = FindObjectOfType<MapDisplay> ();
		if (drawMode == DrawMode.NoiseMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromHeightMap (noiseMap));
		} else if (drawMode == DrawMode.ColorMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromColorMap (colorMap, mapWidth, mapHeight));
		} else if (drawMode == DrawMode.Mesh) {
			mapDisplay.DrawMesh (MeshGenerator.GeneratTerrainMesh (noiseMap), TextureGenerator.textureFromColorMap (colorMap, mapWidth, mapHeight));
		}
	}

	void OnValidate ()
	{
		if (mapWidth < 1) {
			mapWidth = 1;
		}
		if (mapHeight < 1) {
			mapHeight = 1;
		}
		if (lacunarity < 1) {
			lacunarity = 1;
		}
		if (octaves < 0) {
			octaves = 0;
		}
	}
}

[System.Serializable]
public struct TerrainType
{
	public string name;
	public float height;
	public Color color;
}