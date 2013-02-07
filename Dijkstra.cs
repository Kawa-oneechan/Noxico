using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public enum DijkstraIgnores
	{
		WallsOnly,
		Type,
		Instance,
	}

	public class Dijkstra
	{
		private const int mapRows = 25, mapCols = 80, vhn = 9000;

		private int[,] map;
		private bool[,] walls;

		public List<Point> Hotspots { get; set; }
		public DijkstraIgnores Ignore { get; set; }
		public Type IgnoreType { get; set; }
		public Entity IgnoreObject { get; set; }

		public Dijkstra()
		{
			map = new int[mapRows, mapCols];
			walls = new bool[mapRows, mapCols];
			UpdateWalls();
			Hotspots = new List<Point>();
		}

		public void UpdateWalls()
		{
			var board = NoxicoGame.HostForm.Noxico.CurrentBoard;
			for (var row = 0; row < mapRows; row++)
				for (var col = 0; col < mapCols; col++)
					walls[row, col] = board.IsSolid(row, col);
		}

		public bool RollDown(int row, int col, ref Direction dir)
		{
			var lowest = vhn;
			var ret = false;

			var board = NoxicoGame.HostForm.Noxico.CurrentBoard;
			var ignored = new List<int?>();
			if (Ignore == DijkstraIgnores.Instance && IgnoreObject != null)
			{
				ignored.Add((IgnoreObject.XPosition << 8) | IgnoreObject.YPosition);
			}
			else if (Ignore == DijkstraIgnores.Type && IgnoreType != null)
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
				map[hotspot.Y, hotspot.X] = 0;

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

		public static void JustDoIt(ref int[,] map, int mapRows = 25, int mapCols = 80, bool diagonals = true)
		{
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

	}
}
