using System;
using System.Collections.Generic;
using System.Drawing;

namespace DungeonGenerator
{
	public class DungeonGenerator
	{
		private readonly RoomGenerator roomGenerator = new RoomGenerator(10, 1, 5, 1, 5);

		public DungeonGenerator()
		{
		}

		public DungeonGenerator(int width, int height, int changeDirectionModifier, int sparsenessModifier, int deadEndRemovalModifier, RoomGenerator roomGenerator)
		{
			this.Width = width;
			this.Height = height;
			this.ChangeDirectionModifier = changeDirectionModifier;
			this.SparsenessModifier = sparsenessModifier;
			this.DeadEndRemovalModifier = deadEndRemovalModifier;
			this.roomGenerator = roomGenerator;
		}

		public Dungeon Generate()
		{
			Dungeon dungeon = new Dungeon(Width, Height);
			dungeon.FlagAllCellsAsUnvisited();

			CreateDenseMaze(dungeon);
			SparsifyMaze(dungeon);
			RemoveDeadEnds(dungeon);
			roomGenerator.PlaceRooms(dungeon);
			roomGenerator.PlaceDoors(dungeon);

			return dungeon;
		}

		public void CreateDenseMaze(Dungeon dungeon)
		{
			Point currentLocation = dungeon.PickRandomCellAndFlagItAsVisited();
			DirectionType previousDirection = DirectionType.North;

			while (!dungeon.AllCellsAreVisited)
			{
				DirectionPicker directionPicker = new DirectionPicker(previousDirection, ChangeDirectionModifier);
				DirectionType direction = directionPicker.GetNextDirection();

				while (!dungeon.HasAdjacentCellInDirection(currentLocation, direction) || dungeon.AdjacentCellInDirectionIsVisited(currentLocation, direction))
				{
					if (directionPicker.HasNextDirection)
						direction = directionPicker.GetNextDirection();
					else
					{
						currentLocation = dungeon.GetRandomVisitedCell(currentLocation); // Get a new previously visited location
						directionPicker = new DirectionPicker(previousDirection, ChangeDirectionModifier); // Reset the direction picker
						direction = directionPicker.GetNextDirection(); // Get a new direction
					}
				}

				currentLocation = dungeon.CreateCorridor(currentLocation, direction);
				dungeon.FlagCellAsVisited(currentLocation);
				previousDirection = direction;
			}
		}

		public void SparsifyMaze(Dungeon dungeon)
		{
			// Calculate the number of cells to remove as a percentage of the total number of cells in the dungeon
			int noOfDeadEndCellsToRemove = (int)Math.Ceiling(((decimal)SparsenessModifier / 100) * (dungeon.Width * dungeon.Height));

			IEnumerator<Point> enumerator = dungeon.DeadEndCellLocations.GetEnumerator();

			for (int i = 0; i < noOfDeadEndCellsToRemove; i++)
			{
				if (!enumerator.MoveNext()) // Check if there is another item in our enumerator
				{
					enumerator = dungeon.DeadEndCellLocations.GetEnumerator(); // Get a new enumerator
					if (!enumerator.MoveNext()) break; // No new items exist so break out of loop
				}

				Point point = enumerator.Current;
				dungeon.CreateWall(point, dungeon[point].CalculateDeadEndCorridorDirection());
				dungeon[point].IsCorridor = false;
			}
		}

		public void RemoveDeadEnds(Dungeon dungeon)
		{
			foreach (Point deadEndLocation in dungeon.DeadEndCellLocations)
			{
				if (ShouldRemoveDeadend())
				{
					Point currentLocation = deadEndLocation;

					do
					{
						// Initialize the direction picker not to select the dead-end corridor direction
						DirectionPicker directionPicker = new DirectionPicker(dungeon[currentLocation].CalculateDeadEndCorridorDirection(), 100);
						DirectionType direction = directionPicker.GetNextDirection();

						while (!dungeon.HasAdjacentCellInDirection(currentLocation, direction))
						{
							if (directionPicker.HasNextDirection)
								direction = directionPicker.GetNextDirection();
							else
								throw new InvalidOperationException("This should not happen");
						}
						// Create a corridor in the selected direction
						currentLocation = dungeon.CreateCorridor(currentLocation, direction);

					} while (dungeon[currentLocation].IsDeadEnd); // Stop when you intersect an existing corridor.
				}
			}
		}

		public bool ShouldRemoveDeadend()
		{
			return Random.Instance.Next(1, 99) < DeadEndRemovalModifier;
		}

		public static int[,] ExpandToTiles(Dungeon dungeon)
		{
			// Instantiate our tile array
			int[,] tiles = new int[dungeon.Width * 2 + 1, dungeon.Height * 2 + 1];

			// Initialize the tile array to rock
			for (int x = 0; x < dungeon.Width * 2 + 1; x++)
				for (int y = 0; y < dungeon.Height * 2 + 1; y++)
					tiles[x, y] = (int)TileType.Wall;

			// Fill tiles with corridor values for each room in dungeon
			foreach (Room room in dungeon.Rooms)
			{
				// Get the room min and max location in tile coordinates
				Point minPoint = new Point(room.Bounds.Location.X * 2 + 1, room.Bounds.Location.Y * 2 + 1);
				Point maxPoint = new Point(room.Bounds.Right * 2, room.Bounds.Bottom * 2);

				// Fill the room in tile space with an empty value
				for (int i = minPoint.X; i < maxPoint.X; i++)
					for (int j = minPoint.Y; j < maxPoint.Y; j++)
						tiles[i, j] = (int)TileType.Room;
			}

			// Loop for each corridor cell and expand it
			foreach (Point cellLocation in dungeon.CorridorCellLocations)
			{
				Point tileLocation = new Point(cellLocation.X * 2 + 1, cellLocation.Y * 2 + 1);
				tiles[tileLocation.X, tileLocation.Y] = (int)TileType.Empty;

				if (dungeon[cellLocation].NorthSide == SideType.Empty) tiles[tileLocation.X, tileLocation.Y - 1] = (int)TileType.Empty;
				if (dungeon[cellLocation].NorthSide == SideType.Door) tiles[tileLocation.X, tileLocation.Y - 1] = (int)TileType.Door;

				if (dungeon[cellLocation].SouthSide == SideType.Empty) tiles[tileLocation.X, tileLocation.Y + 1] = (int)TileType.Empty;
				if (dungeon[cellLocation].SouthSide == SideType.Door) tiles[tileLocation.X, tileLocation.Y + 1] = (int)TileType.Door;

				if (dungeon[cellLocation].WestSide == SideType.Empty) tiles[tileLocation.X - 1, tileLocation.Y] = (int)TileType.Empty;
				if (dungeon[cellLocation].WestSide == SideType.Door) tiles[tileLocation.X - 1, tileLocation.Y] = (int)TileType.Door;

				if (dungeon[cellLocation].EastSide == SideType.Empty) tiles[tileLocation.X + 1, tileLocation.Y] = (int)TileType.Empty;
				if (dungeon[cellLocation].EastSide == SideType.Door) tiles[tileLocation.X + 1, tileLocation.Y] = (int)TileType.Door;
			}

			return tiles;
		}

		public int Width { get; set; }

		public int Height { get; set; }

		public int ChangeDirectionModifier { get; set; }

		public int SparsenessModifier { get; set; }

		public int DeadEndRemovalModifier { get; set; }
	}
}
