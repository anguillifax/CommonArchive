using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace Levels {
	[ExecuteInEditMode]
	public class LevelGenerator : MonoBehaviour {

		public static readonly int BLOCKED = 0;
		public static readonly int HORZ = 1;
		public static readonly int DOWN = 2;
		public static readonly int UP = 3;
		public static readonly int ALL = 4;
		public static readonly int ENTER = 8;
		public static readonly int EXIT = 9;

		public static LevelGenerator instance;
		public static System.Action OnLevelGenerated;

		public bool runOnce;
		public bool alwaysPickFirst;

		public Text guiDebug;

		public int[,] rooms;
		public int MaxX = 4;
		public int MaxY = 4;

		public Vector2Int RoomSize = new Vector2Int(12, 8);
		public Vector3Int StageSize;

		public Tilemap stage;
		public Tilemap[] templates;
		public TileBase frameTile;

		public Vector2Int ObstacleSize = new Vector2Int(5, 3);

		public TileBase tileObstacleGround, tileObstacleAir;

		[System.Serializable]
		public class ProbabilityPair {
			public TileBase read, write;
			[Range(0, 1)] public float probability = 0.5f;

			public TileBase Get() {
				return (Random.value < probability) ? write : null;
			}
		}

		public ProbabilityPair[] probabilityPairs;

		public TileBase tileGold;
		[Range(0, 0.2f)] public float goldChance = 0.2f;

		void Awake() {
			instance = this;
			runOnce = true;
		}

		void Start() {

		}

		void Reset() {
			rooms = new int[MaxX, MaxY];
			StageSize = new Vector3Int(MaxX * RoomSize.x, MaxY * RoomSize.y, 0);

			templates[0].transform.parent.gameObject.SetActive(false);
			stage.ClearAllTiles();
		}

		void Update() {
			if (Input.GetKeyDown(KeyCode.Escape)) {
				runOnce = true;
			}
			if (runOnce) {
				Generate();
				runOnce = false;
			}
		}

#if UNITY_EDITOR
		void OnDrawGizmos() {
			for (int x = 0; x < MaxX; x++) {
				for (int y = 0; y < MaxY; y++) {

					var center = new Vector3(x * RoomSize.x + RoomSize.x / 2, y * RoomSize.y + RoomSize.y / 2);
					Gizmos.color = new Color(0.6f, 0.6f, 0.6f);
					Gizmos.DrawWireCube(center, (Vector2)RoomSize);

				}
			}
		}
#endif

		void Generate() {
			Reset();
			GenerateBoard();
			PlaceTiles();
			PlaceFrame();

			if (OnLevelGenerated != null && Application.isPlaying) {
				OnLevelGenerated();
			}
		}

		void GenerateBoard() {
			int x = Random.Range(0, MaxX);
			int y = MaxY - 1;

			rooms[x, y] = ENTER; // place entrance

			List<int> options = new List<int>();
			while (y >= 0) {
				options.Clear();

				if (x > 0) {
					options.Add(0); // can left
				}
				if (x < MaxX - 1) {
					options.Add(1); // can right
				}

				var dir = options[Random.Range(0, options.Count)] == 0;
				var moves = Random.Range(0, 1 + (dir ? x : MaxX - x - 1));

				//print(string.Format("({0}, {1}) {2} for {3}", x, y, (dir ? "left" : "right"), moves));
				GenerateRow(dir, moves, ref x, ref y);
			}

			guiDebug.text = BoardToString();
		}

		void GenerateRow(bool isLeft, int count, ref int x, ref int y) {
			for (int i = 0; i < count; i++) {
				if (isLeft) {
					x--;
				} else {
					x++;
				}

				rooms[x, y] = HORZ;
			}

			if (count > 0) {
				rooms[x, y] = DOWN;
			} else if (rooms[x, y] != ENTER) { // moving straight down
				rooms[x, y] = ALL;
			}

			y--;
			if (y >= 0) {
				rooms[x, y] = UP;
			} else {
				rooms[x, y + 1] = EXIT;
			}
		}

		void PlaceTiles() {
			for (int x = 0; x < MaxX; x++) {
				for (int y = 0; y < MaxY; y++) {

					var stagepos = new Vector3Int(x * RoomSize.x, y * RoomSize.y, 0);

					var id = rooms[x, y];
					if (id == ENTER || id == EXIT) id = ALL;

					CopyTilemap(templates[id], alwaysPickFirst ? Vector3Int.zero : PickRandomRoom(templates[id], RoomSize), RoomSize,
						stage, stagepos, Random.Range(0, 2) == 0);

					PostProcessObstacles(stagepos);
					PostProcessProbabilities(stagepos);

				}
			}
		}

		void PostProcessObstacles(Vector3Int stagepos) {
			for (int subX = 0; subX < RoomSize.x; subX++) {
				for (int subY = 0; subY < RoomSize.y; subY++) {

					var combinedCoord = stagepos + new Vector3Int(subX, subY, 0);
					var tile = stage.GetTile(combinedCoord);

					if (tile) {
						if (tile == tileObstacleGround) {
							CopyTilemap(templates[5], PickRandomRoom(templates[5], ObstacleSize, alwaysPickFirst), ObstacleSize,
								stage, combinedCoord + new Vector3Int(-2, 0, 0), Random.Range(0, 2) == 0);

						} else if (tile == tileObstacleAir) {
							CopyTilemap(templates[6], PickRandomRoom(templates[6], ObstacleSize, alwaysPickFirst), ObstacleSize,
								stage, combinedCoord + new Vector3Int(-2, 0, 0), Random.Range(0, 2) == 0);
						}

					}

				}
			}
		}

		void PostProcessProbabilities(Vector3Int stagepos) {
			for (int subX = 0; subX < RoomSize.x; subX++) {
				for (int subY = 0; subY < RoomSize.y; subY++) {

					var combinedCoord = stagepos + new Vector3Int(subX, subY, 0);
					var tile = stage.GetTile(combinedCoord);

					if (tile) {
						foreach (var item in probabilityPairs) {
							if (tile == item.read) {
								tile = item.Get();
								stage.SetTile(combinedCoord, tile);
							}
						}

						if (tile == frameTile && Random.value < goldChance) {
							stage.SetTile(combinedCoord, tileGold);
						}
					}

				}
			}
		}

		Vector3Int PickRandomRoom(Tilemap template, Vector2Int regionSize, bool allowOrigin = false) {

			var topRight = template.cellBounds.max;

			var pos = Vector3Int.zero;

			do {
				pos.x = Random.Range(0, topRight.x / regionSize.x) * (regionSize.x + 1);
				pos.y = Random.Range(0, topRight.y / regionSize.y) * (regionSize.y + 1);
			} while ((!allowOrigin && pos.sqrMagnitude == 0) || // try again if we're not allowed to have the origin...
				!RegionHasTiles(template, pos, new Vector3Int(pos.x + regionSize.x, pos.y + regionSize.y, 0))); // or if the region was empty

			return pos; // default

		}

		/// <summary>
		/// range [bottomleft, topright]
		/// </summary>
		bool RegionHasTiles(Tilemap target, Vector3Int bottomleft, Vector3Int topright) {
			for (int x = bottomleft.x; x <= topright.x; x++) {
				for (int y = bottomleft.y; y <= topright.y; y++) {
					if (target.HasTile(new Vector3Int(x, y, 0))) {
						return true;
					}
				}
			}

			return false;
		}

		void CopyTilemap(Tilemap source, Vector3Int sstart, Vector2Int size, Tilemap destination, Vector3Int dstart, bool reversed = false) {
			for (int x = 0; x < size.x; x++) {
				for (int y = 0; y < size.y; y++) {

					var offset = new Vector3Int(x, y, 0);
					var readOffset = offset;
					if (reversed) {
						readOffset.x = size.x - offset.x - 1;
					}

					var tile = source.GetTile(sstart + readOffset);
					destination.SetTile(dstart + offset, tile);

				}
			}
		}

		void PlaceFrame() {
			for (int y = 0; y < StageSize.y; y++) {
				stage.SetTile(new Vector3Int(-1, y, 0), frameTile);
				stage.SetTile(new Vector3Int(StageSize.x, y, 0), frameTile);
			}
		}

		string BoardToString() {
			StringBuilder sb = new StringBuilder();

			for (int y = MaxY - 1; y >= 0; y--) {
				for (int x = 0; x < MaxX; x++) {

					var val = rooms[x, y];
					sb.Append((val == 0 ? "_" : val.ToString()) + " ");

				}
				sb.AppendLine();
			}

			return sb.ToString();

		}

	}
}