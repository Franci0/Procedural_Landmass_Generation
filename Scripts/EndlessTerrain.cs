using UnityEngine;
using System.Collections.Generic;

public class EndlessTerrain : MonoBehaviour
{
	const float VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = 25f;
	const float SQUARE_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE = VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE * VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE;

	public static float maxViewDistance;
	public static Vector2 viewerPosition;

	static MapGenerator mapGenerator;

	public Transform viewer;
	public Material mapMaterial;
	public LODInfo[] detailLevels;

	int chunkSize;
	int chunkVisibleInViewDistance;
	Dictionary<Vector2,TerrainChunk> terrainChunkDictionary = new Dictionary<Vector2, TerrainChunk> ();
	List<TerrainChunk> terrainChunkVisibleLastUpdate = new List<TerrainChunk> ();
	Vector2 viewerPositionOld;

	void Start ()
	{
		mapGenerator = FindObjectOfType<MapGenerator> ();
		maxViewDistance = detailLevels [detailLevels.Length - 1].visibleDstThreshold;
		chunkSize = MapGenerator.MAP_CHUNK_SIZE - 1;
		chunkVisibleInViewDistance = Mathf.RoundToInt (maxViewDistance / chunkSize);
		UpdateVisibleChunk ();
	}

	void Update ()
	{
		viewerPosition = new Vector2 (viewer.position.x, viewer.position.z);

		if ((viewerPositionOld - viewerPosition).sqrMagnitude > SQUARE_VIEWER_MOVE_THRESHOLD_FOR_CHUNK_UPDATE) {
			viewerPositionOld = viewerPosition;
			UpdateVisibleChunk ();
		}
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
					terrainChunkDictionary.Add (viewedChunkCoord, new TerrainChunk (viewedChunkCoord, chunkSize, detailLevels, transform, mapMaterial));
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
		LODInfo[] detailLevels;
		LODMesh[] lodMeshes;
		MapData mapData;
		bool mapDataReceived;
		int previousLODIndex = -1;

		public TerrainChunk (Vector2 coord, int size, LODInfo[] detailLevels, Transform parent, Material material)
		{
			this.detailLevels = detailLevels;
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
			lodMeshes = new LODMesh[detailLevels.Length];

			for (int i = 0; i < detailLevels.Length; i++) {
				lodMeshes [i] = new LODMesh (detailLevels [i].lod, UpdateTerrainChunk);
			}

			mapGenerator.RequestMapData (position, OnMapDataReceived);
		}

		public void UpdateTerrainChunk ()
		{
			if (mapDataReceived) {
				float viewerDistanceFromNearestEdge = Mathf.Sqrt (bounds.SqrDistance (viewerPosition));
				bool visible = viewerDistanceFromNearestEdge <= maxViewDistance;

				if (visible) {
					int lodIndex = 0;

					for (int i = 0; i < detailLevels.Length - 1; i++) {
						if (viewerDistanceFromNearestEdge > detailLevels [i].visibleDstThreshold) {
							lodIndex = i + 1;
						} else {
							break;
						}
					}

					if (lodIndex != previousLODIndex) {
						LODMesh lodMesh = lodMeshes [lodIndex];

						if (lodMesh.hasMesh) {
							previousLODIndex = lodIndex;
							meshFilter.mesh = lodMesh.mesh;
						} else if (!lodMesh.hasRequestedMesh) {
							lodMesh.RequestMesh (mapData);
						}
					}
				}

				SetVisible (visible);
			}
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
			this.mapData = mapData;
			mapDataReceived = true;
			Texture2D texture = TextureGenerator.textureFromColorMap (mapData.colorMap, MapGenerator.MAP_CHUNK_SIZE, MapGenerator.MAP_CHUNK_SIZE);
			meshRenderer.material.mainTexture = texture;
			UpdateTerrainChunk ();
		}
	}

	class LODMesh
	{
		public Mesh mesh;
		public bool hasRequestedMesh;
		public bool hasMesh;
		int lod;
		System.Action updateCallback;

		public LODMesh (int lod, System.Action updateCallback)
		{
			this.lod = lod;
			this.updateCallback = updateCallback;
		}

		public void RequestMesh (MapData mapData)
		{
			hasRequestedMesh = true;
			mapGenerator.RequestMeshData (mapData, lod, OnMeshDataReceived);
		}

		void OnMeshDataReceived (MeshData meshData)
		{
			mesh = meshData.CreateMesh ();
			hasMesh = true;
			updateCallback ();
		}
	}

	[System.Serializable]
	public struct LODInfo
	{
		public int lod;
		public float visibleDstThreshold;
	}
}
