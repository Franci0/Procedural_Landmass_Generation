using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
	public const int MAP_CHUNK_SIZE = 239;

	[Range (0, 6)]
	public int editorPreviewLOD;

	public DrawMode drawMode;
	public Noise.NormalizeMode normalizeMode;
	public bool useFalloff;
	public float noiseScale;

	public int octaves;
	[Range (0, 1)]
	public float persistance;
	public float lacunarity;

	public int seed;
	public Vector2 offset;
	public float meshHeightMultipier;
	public AnimationCurve meshHeightCurve;

	public bool autoUpdate;

	public TerrainType[] regions;

	Queue<MapThreadInfo<MapData>> mapDataThreadInfoQueue = new Queue<MapThreadInfo<MapData>> ();
	Queue<MapThreadInfo<MeshData>> meshDataThreadInfoQueue = new Queue<MapThreadInfo<MeshData>> ();

	float[,] falloffMap;

	public enum DrawMode
	{
		NoiseMap,
		ColorMap,
		Mesh,
		FalloffMap
	}

	public void DrawMapInEditor ()
	{
		MapData mapData = GenerateMapData (Vector2.zero);
		MapDisplay mapDisplay = FindObjectOfType<MapDisplay> ();
		if (drawMode == DrawMode.NoiseMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromHeightMap (mapData.heightMap));
		} else if (drawMode == DrawMode.ColorMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromColorMap (mapData.colorMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
		} else if (drawMode == DrawMode.Mesh) {
			mapDisplay.DrawMesh (MeshGenerator.GeneratTerrainMesh (mapData.heightMap, meshHeightMultipier, meshHeightCurve, editorPreviewLOD), 
				TextureGenerator.textureFromColorMap (mapData.colorMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
		} else if (drawMode == DrawMode.FalloffMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromHeightMap (FalloffGenerator.GenerateFalloffMap (MAP_CHUNK_SIZE)));
		}
	}

	public void RequestMapData (Vector2 centre, Action<MapData> callback)
	{
		ThreadStart threadStart = delegate {
			MapDataThread (centre, callback);
		};

		new Thread (threadStart).Start ();
	}

	public void RequestMeshData (MapData mapData, int lod, Action<MeshData> callback)
	{
		ThreadStart threadStart = delegate {
			MeshDataThread (mapData, lod, callback);
		};

		new Thread (threadStart).Start ();
	}

	void Awake ()
	{
		falloffMap = FalloffGenerator.GenerateFalloffMap (MAP_CHUNK_SIZE);
	}

	void Update ()
	{
		if (mapDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < mapDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MapData> threadInfo = mapDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}

		if (meshDataThreadInfoQueue.Count > 0) {
			for (int i = 0; i < meshDataThreadInfoQueue.Count; i++) {
				MapThreadInfo<MeshData> threadInfo = meshDataThreadInfoQueue.Dequeue ();
				threadInfo.callback (threadInfo.parameter);
			}
		}
	}

	void MapDataThread (Vector2 centre, Action<MapData> callback)
	{
		MapData mapData = GenerateMapData (centre);

		lock (mapDataThreadInfoQueue) {
			mapDataThreadInfoQueue.Enqueue (new MapThreadInfo<MapData> (callback, mapData));
		}
	}

	void MeshDataThread (MapData mapData, int lod, Action<MeshData> callback)
	{
		MeshData meshData = MeshGenerator.GeneratTerrainMesh (mapData.heightMap, meshHeightMultipier, meshHeightCurve, lod);

		lock (meshDataThreadInfoQueue) {
			meshDataThreadInfoQueue.Enqueue (new MapThreadInfo<MeshData> (callback, meshData));
		}
	}

	MapData GenerateMapData (Vector2 centre)
	{
		float[,] noiseMap = Noise.GenerateNoiseMap (MAP_CHUNK_SIZE + 2, MAP_CHUNK_SIZE + 2, seed, noiseScale, octaves, persistance, lacunarity, centre + offset, normalizeMode);

		Color[] colorMap = new Color[MAP_CHUNK_SIZE * MAP_CHUNK_SIZE];

		for (int y = 0; y < MAP_CHUNK_SIZE; y++) {
			for (int x = 0; x < MAP_CHUNK_SIZE; x++) {
				if (useFalloff) {
					noiseMap [x, y] = Mathf.Clamp01 (noiseMap [x, y] - falloffMap [x, y]);
				}

				float currentHeight = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (currentHeight >= regions [i].height) {
						colorMap [y * MAP_CHUNK_SIZE + x] = regions [i].color;
					} else {
						break;
					}
				}
			}
		}

		return new MapData (noiseMap, colorMap);
	}

	void OnValidate ()
	{
		if (lacunarity < 1) {
			lacunarity = 1;
		}
		if (octaves < 0) {
			octaves = 0;
		}

		falloffMap = FalloffGenerator.GenerateFalloffMap (MAP_CHUNK_SIZE);
	}

	struct MapThreadInfo<T>
	{
		public readonly Action<T> callback;
		public readonly T parameter;

		public MapThreadInfo (Action<T> callback, T parameter)
		{
			this.callback = callback;
			this.parameter = parameter;
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

public struct MapData
{
	public readonly float[,] heightMap;
	public readonly Color[] colorMap;

	public MapData (float[,] heightMap, Color[] colorMap)
	{
		this.heightMap = heightMap;
		this.colorMap = colorMap;
	}
}