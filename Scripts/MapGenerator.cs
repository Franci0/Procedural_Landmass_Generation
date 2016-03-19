using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;

public class MapGenerator : MonoBehaviour
{
	public const int MAP_CHUNK_SIZE = 241;

	[Range (0, 6)]
	public int levelOfDetail;

	public DrawMode drawMode;
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

	public enum DrawMode
	{
		NoiseMap,
		ColorMap,
		Mesh
	}

	public void DrawMapInEditor ()
	{
		MapData mapData = GenerateMapData ();
		MapDisplay mapDisplay = FindObjectOfType<MapDisplay> ();
		if (drawMode == DrawMode.NoiseMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromHeightMap (mapData.heightMap));
		} else if (drawMode == DrawMode.ColorMap) {
			mapDisplay.drawTexture (TextureGenerator.textureFromColorMap (mapData.colorMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
		} else if (drawMode == DrawMode.Mesh) {
			mapDisplay.DrawMesh (MeshGenerator.GeneratTerrainMesh (mapData.heightMap, meshHeightMultipier, meshHeightCurve, levelOfDetail), 
				TextureGenerator.textureFromColorMap (mapData.colorMap, MAP_CHUNK_SIZE, MAP_CHUNK_SIZE));
		}
	}

	public void RequestMapData (Action<MapData> callback)
	{
		ThreadStart threadStart = delegate {
			MapDataThread (callback);
		};

		new Thread (threadStart).Start ();
	}

	public void RequestMeshData (MapData mapData, Action<MeshData> callback)
	{
		ThreadStart threadStart = delegate {
			MeshDataThread (mapData, callback);
		};

		new Thread (threadStart).Start ();
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

	void MapDataThread (Action<MapData> callback)
	{
		MapData mapData = GenerateMapData ();

		lock (mapDataThreadInfoQueue) {
			mapDataThreadInfoQueue.Enqueue (new MapThreadInfo<MapData> (callback, mapData));
		}
	}

	void MeshDataThread (MapData mapData, Action<MeshData> callback)
	{
		MeshData meshData = MeshGenerator.GeneratTerrainMesh (mapData.heightMap, meshHeightMultipier, meshHeightCurve, levelOfDetail);

		lock (meshDataThreadInfoQueue) {
			meshDataThreadInfoQueue.Enqueue (new MapThreadInfo<MeshData> (callback, meshData));
		}
	}

	MapData GenerateMapData ()
	{
		float[,] noiseMap = Noise.GenerateNoiseMap (MAP_CHUNK_SIZE, MAP_CHUNK_SIZE, seed, noiseScale, octaves, persistance, lacunarity, offset);

		Color[] colorMap = new Color[MAP_CHUNK_SIZE * MAP_CHUNK_SIZE];

		for (int y = 0; y < MAP_CHUNK_SIZE; y++) {
			for (int x = 0; x < MAP_CHUNK_SIZE; x++) {
				float currentHeight = noiseMap [x, y];
				for (int i = 0; i < regions.Length; i++) {
					if (currentHeight <= regions [i].height) {
						colorMap [y * MAP_CHUNK_SIZE + x] = regions [i].color;
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