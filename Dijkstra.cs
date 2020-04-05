using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public enum DijkstraIgnore
	{
		WallsOnly,
		Type,
		Instance,
	}

	public class Dijkstra
	{
		private int mapRows = WorldMapGenerator.TileHeight, mapCols = WorldMapGenerator.TileWidth;
		private const int vhn = 9000;

		private int[,] map;
		private bool[,] walls;

		public List<Point> Hotspots { get; private set; }
		public DijkstraIgnore Ignore { get; set; }
		public Type IgnoreType { get; set; }
		public Entity IgnoreObject { get; set; }

		public Dijkstra(Board board, bool allowSwimming = true)
		{
			mapRows = board.Height;
			mapCols = board.Width;
			map = new int[mapRows, mapCols];
			walls = new bool[mapRows, mapCols];
			UpdateWalls(board, allowSwimming);
			Hotspots = new List<Point>();
		}

		public void UpdateWalls(Board board, bool allowSwimming = true)
		{
			if (board == null)
				board = NoxicoGame.Me.CurrentBoard;
			if (board == null)
				return;
			for (var row = 0; row < mapRows; row++)
				for (var col = 0; col < mapCols; col++)
					walls[row, col] = board.IsSolid(row, col, allowSwimming ? SolidityCheck.Walker : SolidityCheck.DryWalker);
			foreach (var door in board.Entities.OfType<Door>().Where(d => !d.Locked))
			{
				if (door.YPosition >= board.Height || door.XPosition >= board.Width)
				{
					Program.WriteLine("Warning: invalid door position {0}x{1}", door.XPosition, door.YPosition);
					continue;
				}
				walls[door.YPosition, door.XPosition] = false;
			}
		}

		public bool RollDown(int row, int col, ref Direction dir)
		{
			var lowest = vhn;
			var ret = false;

			var board = NoxicoGame.Me.CurrentBoard;
			var ignored = new List<int?>();
			if (Ignore == DijkstraIgnore.Instance && IgnoreObject != null)
			{
				ignored.Add((IgnoreObject.XPosition << 8) | IgnoreObject.YPosition);
			}
			else if (Ignore == DijkstraIgnore.Type && IgnoreType != null)
			{
				foreach (var entity in board.Entities)
				{
					if (entity.GetType() == IgnoreType)
						ignored.Add((entity.XPosition << 8) | entity.YPosition);
				}
			}

			foreach (var spot in Hotspots)
			{
				if (row == spot.Y && col == spot.X)
					return false;
			}
			if (row > 0 && map[row - 1, col] < lowest && !ignored.Contains((col << 8) | (row - 1)))
			{
				lowest = map[row - 1, col];
				dir = Direction.North;
				ret = true;
			}
			if (row < mapRows - 1 && map[row + 1, col] < lowest && !ignored.Contains((col << 8) | (row + 1)))
			{
				lowest = map[row + 1, col];
				dir = Direction.South;
				ret = true;
			}
			if (col > 0 && map[row, col - 1] < lowest && !ignored.Contains(((col - 1) << 8) | row))
			{
				lowest = map[row, col - 1];
				dir = Direction.West;
				ret = true;
			}
			if (col < mapCols - 1 && map[row, col + 1] < lowest && !ignored.Contains(((col - 1) << 8) | row))
			{
				lowest = map[row, col + 1];
				dir = Direction.East;
				ret = true;
			}
			return ret;
		}

		/*
		 * To get a Dijkstra map, you start with an integer array representing your map, with some set of goal cells set to zero and all the rest set to a very high number.
		 * Iterate through the map's "floor" cells -- skip the impassable wall cells. If any floor tile has a value that is at least 2 greater than its lowest-value floor
		 * neighbor, set it to be exactly 1 greater than its lowest value neighbor. Repeat until no changes are made. The resulting grid of numbers represents the number
		 * of steps that it will take to get from any given tile to the nearest goal.
		 *  -- http://roguebasin.roguelikedevelopment.org/index.php/The_Incredible_Power_of_Dijkstra_Maps
		 */

		public void Update()
		{
			if (Hotspots.Count == 0)
				return;

			for (var row = 0; row < mapRows; row++)
				for (var col = 0; col < mapCols; col++)
					map[row, col] = vhn;
			foreach (var hotspot in Hotspots)
			{
				if (hotspot.Y >= mapRows || hotspot.X >= mapCols)
				{
					Program.WriteLine("Bad dijkstra hotspot {0}x{1}", hotspot.X, hotspot.Y);
					map[0, 0] = 0;
					continue;
				}
				map[hotspot.Y, hotspot.X] = 0;
			}

			var change = false;
			do
			{
				change = false;
				for (var row = 0; row < mapRows; row++)
				{
					for (var col = 0; col < mapCols; col++)
					{
						if (walls[row, col])
							continue;

						//Find lowest-value neighbor
						var lowest = vhn;
						if (row > 0 && map[row - 1, col] < lowest)
							lowest = map[row - 1, col];
						if (row < mapRows - 1 && map[row + 1, col] < lowest)
							lowest = map[row + 1, col];
						if (col > 0 && map[row, col - 1] < lowest)
							lowest = map[row, col - 1];
						if (col < mapCols - 1 && map[row, col + 1] < lowest)
							lowest = map[row, col + 1];

						/*
						if (row > 0 && col > 0 && map[row - 1, col - 1] < lowest)
							lowest = map[row - 1, col - 1];
						if (row > 0 && col < mapCols - 1 && map[row - 1, col + 1] < lowest)
							lowest = map[row - 1, col + 1];
						if (row < mapRows - 1 && col > 0 && map[row + 1, col - 1] < lowest)
							lowest = map[row + 1, col - 1];
						if (row < mapRows - 1 && col < mapCols - 1 && map[row + 1, col + 1] < lowest)
							lowest = map[row + 1, col + 1];
						*/

						if (map[row, col] > lowest + 1)
						{
							map[row, col] = lowest + 1;
							change = true;
						}
					}
				}
			} while (change);
		}

		public static void JustDoIt(ref int[,] map, int mapRows = -1, int mapCols = -1, bool diagonals = true)
		{
			if (mapRows == -1) mapRows = WorldMapGenerator.TileHeight;
			if (mapCols == -1) mapCols = WorldMapGenerator.TileWidth;
			var vhn = 9000;

			//Basically the same as Update but without the hotspots and shit, and adapted for less conksuck definitions of "row" and "column".
			//TODO: make Dijkstra.cs's Update() less conksuck.
			var change = false;
			do
			{
				change = false;
				for (var row = 0; row < mapRows; row++)
				{
					for (var col = 0; col < mapCols; col++)
					{
						//Find lowest-value neighbor
						var lowest = vhn;
						if (row > 0 && map[col, row - 1] < lowest)
							lowest = map[col, row - 1];
						if (row < mapRows - 1 && map[col, row + 1] < lowest)
							lowest = map[col, row + 1];
						if (col > 0 && map[col - 1, row] < lowest)
							lowest = map[col - 1, row];
						if (col < mapCols - 1 && map[col + 1, row] < lowest)
							lowest = map[col + 1, row];

						if (diagonals)
						{
							if (row > 0 && col > 0 && map[col - 1, row - 1] < lowest)
								lowest = map[col - 1, row - 1];
							if (row > 0 && col < mapCols - 1 && map[col + 1, row - 1] < lowest)
								lowest = map[col + 1, row - 1];
							if (row < mapRows - 1 && col > 0 && map[col - 1, row + 1] < lowest)
								lowest = map[col - 1, row + 1];
							if (row < mapRows - 1 && col < mapCols - 1 && map[col + 1, row + 1] < lowest)
								lowest = map[col + 1, row + 1];
						}

						if (map[col, row] > lowest + 1)
						{
							map[col, row] = lowest + 1;
							change = true;
						}
					}
				}
			} while (change);
		}

		#region PillowShout
		/// <summary>
		/// Checks if the location on the map is a local minimum. In this case, a local minimum is defined as a location where
		/// the selected tile is surrounded by tiles that are either greater or equal in value.
		/// </summary>
		/// <param name="x">The x value of the location.</param>
		/// <param name="y">The y value of the location.</param>
		/// <returns>If the location is a local minimum, the function returns true.</returns>
		public bool IsLocalMinimum(int x, int y)
		{
			var val = map[y, x];

			//Tiles beyond the edge of the map are ignored for comparison.
			if (x + 1 < mapCols && map[y, x + 1] < val)
				return false;
			if (x > 0 && map[y, x - 1] < val)
				return false;
			if (y + 1 < mapRows && map[y + 1, x] < val)
				return false;
			if (y > 0 && map[y - 1, x] < val)
				return false;

			return true;
		}
		#endregion
	}
}
