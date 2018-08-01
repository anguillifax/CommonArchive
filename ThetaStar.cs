using Priority_Queue;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Pathfinding {
	public class ThetaStar : MonoBehaviour {

		[Serializable]
		public struct InternalPathNode {
			public Vector3Int coord;
			/// <summary>
			/// The action necessary to reach this tile from the parent
			/// </summary>
			public ActionType action;

			public InternalPathNode(Vector3Int coord, ActionType action) {
				this.coord = coord;
				this.action = action;
			}

			public override string ToString() {
				return string.Format("[{0}, {1}]", coord, action.ToString());
			}

		}


		public Action<List<PathNode>> OnPathCompleted;

		public LevelGenerator levelGenerator;
		Level level;

		public Transform startGoal, endGoal;
		Vector3Int startPos, endPos;

		GenericPriorityQueue<PriorityQueuePathNode, float> frontier = new GenericPriorityQueue<PriorityQueuePathNode, float>(2000);
		float[,,] movementCosts;
		InternalPathNode[,,] parents;
		InternalPathNode closestPoint;
		int closestDistance;
		List<PathNode> path = new List<PathNode>();

		public float jumpCost = 3.5f;
		public float fallCost = 1.5f;

		public float gapJumpCost = 4f;

		public int cyclesPerFrame = 2;


		void Start() {
			level = levelGenerator.level;

			startPos = GetGoalContact(startGoal);
			endPos = GetGoalContact(endGoal);
			StartEndDeltaAbs = Util.Abs(startPos - endPos);

			parents = new InternalPathNode[level.sizeX, level.sizeY, level.sizeZ];
			movementCosts = new float[level.sizeX, level.sizeY, level.sizeZ];

			StartCoroutine(Pathfind());
		}

		Vector3Int GetGoalContact(Transform target) {
			var pos = HexUtil.GetNearestCoord(target.position);
			pos.y = 0;
			while (level[pos] != 2 && pos.y < 50) {
				pos += Vector3Int.up;
			}
			target.position = HexUtil.ToWorld(pos);
			return pos;
		}


		#region Pathfinding Core

		IEnumerator Pathfind() {
			frontier.Enqueue(new InternalPathNode(startPos, ActionType.WALK), 0);

			movementCosts.Set(startPos, 0);
			parents.Set(startPos, new InternalPathNode(startPos, ActionType.WALK));
			closestDistance = int.MaxValue;

			InternalPathNode current;

			var cycles = 0;

			while (frontier.Count > 0) {

				++cycles;

				current = frontier.Dequeue();


				var adj = GetAdjacent(current.coord);
				foreach (var pos in adj) {

					if (!IsClosed(pos.coord)) {
						if (!frontier.Contains(pos)) {
							movementCosts.Set(pos.coord, Mathf.Infinity); // so (new < orig) comparison works
						}

						UpdateNode(current, pos);

						if (pos.coord == endPos) {
							ReconstructPath(pos);
							yield break;
						}

						if (HexUtil.DistanceFlat(pos.coord, endPos) < closestDistance) {
							closestDistance = HexUtil.DistanceFlat(pos.coord, endPos);
							closestPoint = pos;
						}
					}

				}

				if (cycles >= cyclesPerFrame) {
					cycles = 0;
					yield return null;
				}
			}


			// endpos was not on connected graph; return closest node
			ReconstructPath(closestPoint);

		}

		void UpdateNode(InternalPathNode from, InternalPathNode neighbor) {

			switch (neighbor.action) {
				case ActionType.WALK:
					var parent = parents.Get(from);
					InternalPathNode visibleParent = HasLineOfSight(parent.coord, neighbor.coord) ? parent : from;
					float cost = movementCosts.Get(visibleParent.coord) + Distance(visibleParent.coord, neighbor.coord) + level.GetCost(neighbor.coord);

					TrySetNodeValues(neighbor, visibleParent, cost);

					break;
				case ActionType.JUMP:
					TrySetNodeValues(neighbor, from, movementCosts.Get(from.coord) + jumpCost);

					break;
				case ActionType.FALL:
					TrySetNodeValues(neighbor, from, movementCosts.Get(from.coord) + fallCost);

					break;
				case ActionType.GAP_JUMP:
					TrySetNodeValues(neighbor, from, movementCosts.Get(from.coord) + gapJumpCost);

					break;
				default:
					Debug.LogWarning("Could not finding movement type for " + neighbor);
					break;
			}

		}

		void TrySetNodeValues(InternalPathNode neighbor, InternalPathNode from, float cost) {
			if (cost < movementCosts.Get(neighbor.coord)) { // only update if it's more efficient
				movementCosts.Set(neighbor.coord, cost);
				parents.Set(neighbor.coord, from);

				AddToFrontier(neighbor);
			}
		}

		void AddToFrontier(InternalPathNode pos) {
			var f = movementCosts.Get(pos.coord) + Heuristic(pos.coord);

			if (frontier.Contains(pos)) {
				frontier.UpdatePriority(pos, f);
			} else {
				frontier.Enqueue(pos, f);
			}
		}

		#endregion


		public float straightness = 0.01f;
		public float heuristicWeight = (1 / 20f) + 1;
		Vector3 StartEndDeltaAbs;
		public float Heuristic(Vector3Int from) {

			var delta = Util.Abs(from - endPos);

			var crossProduct = Vector3.Cross(delta, StartEndDeltaAbs).magnitude;
			var heuristic = straightness * crossProduct + (1 - straightness) * Util.ComponentSum(delta);

			return heuristicWeight * heuristic;
		}

		float Distance(Vector3Int pos1, Vector3Int pos2) {
			return Vector3.Distance(HexUtil.ToWorld(pos1), HexUtil.ToWorld(pos2));
		}

		public float CastRadius = 0.3f;
		static Vector3 LineOfSightOffset = new Vector3(0, 1f, 0);
		bool HasLineOfSight(Vector3Int fromCoord, Vector3Int toCoord) {

			var from = HexUtil.ToWorld(fromCoord);
			var to = HexUtil.ToWorld(toCoord);

			var hasGround = true;
			var points = HexUtil.GetLineLoose(fromCoord, toCoord, CastRadius / 2, true);
			foreach (var item in points) {
				if (level[item] == 0) hasGround = false;
			}

			var dir = to - from;
			var r = new Ray(from + LineOfSightOffset, dir);
			var hit = Physics.SphereCast(r, CastRadius, dir.magnitude);

			Debug.DrawLine(from, to, (!hit && hasGround) ? Color.white : Color.red, 2);
			return !hit && hasGround;
		}

		bool IsClosed(Vector3Int pos) {
			// closed if it's the start, or if it has a nonzero movement cost
			return pos == startPos || !Mathf.Approximately(movementCosts.Get(pos), 0);
		}

		List<InternalPathNode> GetAdjacent(Vector3Int pos) {

			var ret = new List<InternalPathNode>();

			foreach (var dir in HexUtil.ALL_FLAT_GRID_DIRECTIONS) {

				for (int i = -1; i <= 1; i++) {
					var p = pos + dir + new Vector3Int(0, i, 0);

					if (IsMoveableTile(p)) {
						ret.Add(new InternalPathNode() {
							coord = p,
							action = i == 0 ? ActionType.WALK : (i > 0 ? ActionType.JUMP : ActionType.FALL)
						});

					} else { // try to jump over gap
						var landingSpot = p + dir;

						if (IsMoveableTile(landingSpot) && IsTileEmpty(pos + Vector3Int.up) && IsTileEmpty(p + Vector3Int.up)) {
							ret.Add(new InternalPathNode(landingSpot, ActionType.GAP_JUMP));
						}
					}
				}


			}

			return ret;
		}

		bool IsMoveableTile(Vector3Int pos) {
			return level[pos + Vector3Int.up] == 0 && level[pos] > 0;
		}

		bool IsTileEmpty(Vector3Int pos) {
			return level[pos] == 0;
		}

		void ReconstructPath(InternalPathNode lastPos) {
			path.Clear();

			InternalPathNode cur = lastPos;

			while (true) {
				path.Add(new PathNode(HexUtil.ToWorld(cur.coord), cur.action));
				if (cur.coord == startPos) break;
				cur = parents.Get(cur.coord);
			}

			path.Reverse();

			if (OnPathCompleted != null) OnPathCompleted(path);
		}

	}

	public enum ActionType {
		WALK, JUMP, FALL, GAP_JUMP
	}

	[Serializable]
	public struct PathNode {

		public Vector3 pos;
		/// <summary>
		/// The action to necessary to reach the next node
		/// </summary>
		public ActionType action;

		public PathNode(Vector3 pos, ActionType action) {
			this.pos = pos;
			this.action = action;
		}

		public override string ToString() {
			return string.Format("[{0}, {1}]", pos, action.ToString());
		}

	} 
}