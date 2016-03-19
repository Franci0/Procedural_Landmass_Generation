using UnityEngine;
using System.Collections.Generic;

public class EndlessTerrain : MonoBehaviour
{
	public const float MAX_VIEW_DISTANCE = 450;

	public static Vector2 viewerPosition;

	static MapGenerator mapGenerator;

	public Transform viewer;
	public Material mapMaterial;

	int chunkSize;
	int chunkVisibleInViewDistance;
	Dictionary<Vector2,TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk> ();
	List<TerrainChunk> terrainChunkVisibleLastUpdate = new List<TerrainChunk> ();

	void Start ()
	{
		mapGenerator = FindObjectOfType<MapGenerator> ();
		chunkSize = MapGenerator.MAP_CHUNK_SIZE - 1;
		chunkVisibleInViewDistance = Mathf.RoundToInt (MAX_VIEW_DISTANCE / chunkSize);
	}

	void Update ()
	{
		viewerPosition = new Vector2 (viewer.position.x, viewer.position.z);
		UpdateVisibleChunk ();
	}

	void UpdateVisibleChunk ()
	{
		for (int i = 0; i < terrainChunkVisibleLastUpdate.Count; i++) {
			terrainChunkVisibleLastUpdate [i].SetVisible (false);
		}

		terrainChunkVisibleLastUpdate.Clear ();

		int currentChunkCoordX = Mathf.RoundToInt (viewerPosition.x / chunkSize);
		int currentChunkCoordY = Mathf.RoundToInt (viewerPosition.y / chunkSize);

		for (int yOffset = -chunkVisibleInViewDistance; yOffset <= chunkVisibleInViewDistance; yOffset++) {
			for (int xOffset = -chunkVisibleInViewDistance; xOffset <= chunkVisibleInViewDistance; xOffset++) {
				Vector2 viewedChunkCoord = new Vector2 (currentChunkCoordX + xOffset, currentChunkCoordY + yOffset);

				if (terrainChunkDictionary.ContainsKey (viewedChunkCoord)) {
					terrainChunkDictionary [viewedChunkCoord].UpdateTerrainChunk ();

					if (terrainChunkDictionary [viewedChunkCoord].IsVisible ()) {
						terrainChunkVisibleLastUpdate.Add (terrainChunkDictionary [viewedChunkCoord]);
					}

				} else {
					terrainChunkDictionary.Add (viewedChunkCoord, new TerrainChunk (viewedChunkCoord, chunkSize, transform, mapMaterial));
				}
			}
		}
	}

	public class TerrainChunk
	{
		Vector2 position;
		GameObject meshObject;
		Bounds bounds;
		MeshRenderer meshRenderer;
		MeshFilter meshFilter;

		public TerrainChunk (Vector2 coord, int size, Transform parent, Material material)
		{
			position = coord * size;
			bounds = new Bounds (position, Vector2.one * size);
			Vector3 positionV3 = new Vector3 (position.x, 0, position.y);
			meshObject = new GameObject ("Terrain Chunk");
			meshRenderer = meshObject.AddComponent<MeshRenderer> ();
			meshFilter = meshObject.AddComponent<MeshFilter> ();
			meshRenderer.material = material;
			meshObject.transform.position = positionV3;
			meshObject.transform.SetParent (parent);
			SetVisible (false);
			mapGenerator.RequestMapData (OnMapDataReceived);
		}

		public void UpdateTerrainChunk ()
		{
			float viewerDistanceFromNearestEdge = Mathf.Sqrt (bounds.SqrDistance (viewerPosition));
			bool visible = viewerDistanceFromNearestEdge <= MAX_VIEW_DISTANCE;
			SetVisible (visible);
		}

		public void SetVisible (bool visible)
		{
			meshObject.SetActive (visible);
		}

		public bool IsVisible ()
		{
			return meshObject.activeSelf;
		}

		void OnMapDataReceived (MapData mapData)
		{
			mapGenerator.RequestMeshData (mapData, OnMeshDataReceived);
		}

		void OnMeshDataReceived (MeshData meshData)
		{
			meshFilter.mesh = meshData.CreateMesh ();
		}
	}
}
