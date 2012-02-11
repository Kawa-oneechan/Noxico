using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace DungeonGenerator
{
	public class Dungeon : Map
	{
		private readonly List<Point> visitedCells = new List<Point>();
		private readonly List<Room> rooms = new List<Room>();

		public Dungeon(int width, int height)
			: base(width, height)
		{
		}

		internal void AddRoom(Room room)
		{
			rooms.Add(room);
		}

		public void FlagAllCellsAsUnvisited()
		{
			foreach (Point location in CellLocations)
				this[location].Visited = false;
		}

		public Point PickRandomCellAndFlagItAsVisited()
		{
			Point randomLocation = new Point(Random.Instance.Next(Width - 1), Random.Instance.Next(Height - 1));
			FlagCellAsVisited(randomLocation);
			return randomLocation;
		}

		public bool AdjacentCellInDirectionIsVisited(Point location, DirectionType direction)
		{
			Point? target = GetTargetLocation(location, direction);

			if (target == null)
				return false;

			switch (direction)
			{
				case DirectionType.North:
					return this[target.Value].Visited;
				case DirectionType.West:
					return this[target.Value].Visited;
				case DirectionType.South:
					return this[target.Value].Visited;
				case DirectionType.East:
					return this[target.Value].Visited;
				default:
					throw new InvalidOperationException();
			}
		}

		public bool AdjacentCellInDirectionIsCorridor(Point location, DirectionType direction)
		{
			Point? target = GetTargetLocation(location, direction);

			if (target == null)
				return false;

			switch (direction)
			{
				case DirectionType.North:
					return this[target.Value].IsCorridor;
				case DirectionType.West:
					return this[target.Value].IsCorridor;
				case DirectionType.South:
					return this[target.Value].IsCorridor;
				case DirectionType.East:
					return this[target.Value].IsCorridor;
				default:
					return false;
			}
		}

		public void FlagCellAsVisited(Point location)
		{
			if (!Bounds.Contains(location)) throw new ArgumentException("Location is outside of Dungeon bounds", "location");
			if (this[location].Visited) throw new ArgumentException("Location is already visited", "location");

			this[location].Visited = true;
			visitedCells.Add(location);
		}

		public Point GetRandomVisitedCell(Point location)
		{
			if (visitedCells.Count == 0) throw new InvalidOperationException("There are no visited cells to return.");

			int index = Random.Instance.Next(visitedCells.Count - 1);

			// Loop while the current cell is the visited cell
			while (visitedCells[index] == location)
				index = Random.Instance.Next(visitedCells.Count - 1);

			return visitedCells[index];
		}

		public Point CreateCorridor(Point location, DirectionType direction)
		{
			Point targetLocation = CreateSide(location, direction, SideType.Empty);
			this[location].IsCorridor = true; // Set current location to corridor
			this[targetLocation].IsCorridor = true; // Set target location to corridor
			return targetLocation;
		}

		public Point CreateWall(Point location, DirectionType direction)
		{
			return CreateSide(location, direction, SideType.Wall);
		}

		public Point CreateDoor(Point location, DirectionType direction)
		{
			return CreateSide(location, direction, SideType.Door);
		}

		private Point CreateSide(Point location, DirectionType direction, SideType sideType)
		{
			Point? target = GetTargetLocation(location, direction);
			if (target == null) throw new ArgumentException("There is no adjacent cell in the given direction", "location");

			switch (direction)
			{
				case DirectionType.North:
					this[location].NorthSide = sideType;
					this[target.Value].SouthSide = sideType;
					break;
				case DirectionType.South:
					this[location].SouthSide = sideType;
					this[target.Value].NorthSide = sideType;
					break;
				case DirectionType.West:
					this[location].WestSide = sideType;
					this[target.Value].EastSide = sideType;
					break;
				case DirectionType.East:
					this[location].EastSide = sideType;
					this[target.Value].WestSide = sideType;
					break;
			}

			return target.Value;
		}

		public ReadOnlyCollection<Room> Rooms
		{
			get { return rooms.AsReadOnly(); }
		}

		public IEnumerable<Point> DeadEndCellLocations
		{
			get
			{
				foreach (Point point in CellLocations)
					if (this[point].IsDeadEnd) yield return point;
			}
		}

		public IEnumerable<Point> CorridorCellLocations
		{
			get
			{
				foreach (Point point in CellLocations)
					if (this[point].IsCorridor) yield return point;

			}
		}

		public bool AllCellsAreVisited
		{
			get { return visitedCells.Count == (Width * Height); }
		}
	}
}
