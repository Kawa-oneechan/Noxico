using System;

namespace DungeonGenerator
{
	public enum SideType
	{
		Empty,
		Wall,
		Door
	}

	public enum TileType
	{
		Door,
		Wall,
		Empty,
		Room,
	}

	public class Cell
	{
		public Cell()
		{
			EastSide = NorthSide = SouthSide = WestSide = SideType.Wall;
		}

		public bool Visited { get; set; }
		public SideType NorthSide { get; set; }

		public SideType SouthSide { get; set; }

		public SideType EastSide { get; set; }

		public SideType WestSide { get; set; }

		public bool IsDeadEnd
		{
			get { return WallCount == 3; }
		}

		public bool IsCorridor { get; set; }

		public int WallCount
		{
			get
			{
				int wallCount = 0;
				if (NorthSide == SideType.Wall) wallCount++;
				if (SouthSide == SideType.Wall) wallCount++;
				if (WestSide == SideType.Wall) wallCount++;
				if (EastSide == SideType.Wall) wallCount++;
				return wallCount;
			}
		}

		public DirectionType CalculateDeadEndCorridorDirection()
		{
			if (!IsDeadEnd) throw new InvalidOperationException();

			if (NorthSide == SideType.Empty) return DirectionType.North;
			if (SouthSide == SideType.Empty) return DirectionType.South;
			if (WestSide == SideType.Empty) return DirectionType.West;
			if (EastSide == SideType.Empty) return DirectionType.East;

			throw new InvalidOperationException();
		}
	}
}
