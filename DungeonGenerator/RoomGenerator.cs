using System.Drawing;

namespace DungeonGenerator
{
    public class RoomGenerator
    {
        public RoomGenerator()
        {
			MaxRoomHeight = MaxRoomWidth = 6;
			MinRoomWidth = MinRoomHeight = 1;
			NoOfRoomsToPlace = 10;
        }

        public RoomGenerator(int noOfRoomsToPlace, int minRoomWidth, int maxRoomWidth, int minRoomHeight, int maxRoomHeight)
        {
            this.NoOfRoomsToPlace = noOfRoomsToPlace;
            this.MinRoomWidth = minRoomWidth;
            this.MaxRoomWidth = maxRoomWidth;
            this.MinRoomHeight = minRoomHeight;
            this.MaxRoomHeight = maxRoomHeight;
        }

        public int NoOfRoomsToPlace { get; set; } 

        public int MinRoomWidth { get; set; } 

        public int MaxRoomWidth { get; set; } 

        public int MinRoomHeight { get; set; } 

        public int MaxRoomHeight { get; set; } 

        public Room CreateRoom()
        {
            Room room = new Room(Random.Instance.Next(MinRoomWidth, MaxRoomWidth), Random.Instance.Next(MinRoomHeight, MaxRoomHeight));
            room.InitializeRoomCells();
            return room;
        }

        public void PlaceRooms(Dungeon dungeon)
        {
            // Loop for the amount of rooms to place
            for (int roomCounter = 0; roomCounter < NoOfRoomsToPlace; roomCounter++)
            {
                Room room = CreateRoom();
                int bestRoomPlacementScore = int.MaxValue;
                Point? bestRoomPlacementLocation = null;

                foreach (Point currentRoomPlacementLocation in dungeon.CorridorCellLocations)
                {
                    int currentRoomPlacementScore = CalculateRoomPlacementScore(currentRoomPlacementLocation, room, dungeon);

                    if (currentRoomPlacementScore < bestRoomPlacementScore)
                    {
                        bestRoomPlacementScore = currentRoomPlacementScore;
                        bestRoomPlacementLocation = currentRoomPlacementLocation;
                    }
                }

                // Create room at best room placement cell
                if (bestRoomPlacementLocation != null)
                    PlaceRoom(bestRoomPlacementLocation.Value, room, dungeon);
            }
        }

        public int CalculateRoomPlacementScore(Point location, Room room, Dungeon dungeon)
        {
            // Check if the room at the given location will fit inside the bounds of the map
            if (dungeon.Bounds.Contains(new Rectangle(location, new Size(room.Width + 1, room.Height + 1))))
            {
                int roomPlacementScore = 0;

                // Loop for each cell in the room
                foreach (Point roomLocation in room.CellLocations)
                {
                    // Translate the room cell location to its location in the dungeon
                    Point dungeonLocation = new Point(location.X + roomLocation.X, location.Y + roomLocation.Y);

                    // Add 1 point for each adjacent corridor to the cell
                    if (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.North)) roomPlacementScore++;
                    if (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.South)) roomPlacementScore++;
                    if (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.West)) roomPlacementScore++;
                    if (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.East)) roomPlacementScore++;

                    // Add 3 points if the cell overlaps an existing corridor
                    if (dungeon[dungeonLocation].IsCorridor) roomPlacementScore += 3;

                    // Add 100 points if the cell overlaps any existing room cells
                    foreach (Room dungeonRoom in dungeon.Rooms)
                        if (dungeonRoom.Bounds.Contains(dungeonLocation))
                            roomPlacementScore += 100;
                }

                return roomPlacementScore;
            }
            else
            {
                return int.MaxValue;
            }
        }

        public void PlaceRoom(Point location, Room room, Dungeon dungeon)
        {
            // Offset the room origin to the new location
            room.SetLocation(location);

            // Loop for each cell in the room
            foreach (Point roomLocation in room.CellLocations)
            {
                // Translate the room cell location to its location in the dungeon
                Point dungeonLocation = new Point(location.X + roomLocation.X, location.Y + roomLocation.Y);
                dungeon[dungeonLocation].NorthSide = room[roomLocation].NorthSide;
                dungeon[dungeonLocation].SouthSide = room[roomLocation].SouthSide;
                dungeon[dungeonLocation].WestSide = room[roomLocation].WestSide;
                dungeon[dungeonLocation].EastSide = room[roomLocation].EastSide;

                // Create room walls on map (either side of the wall)
                if ((roomLocation.X == 0) && (dungeon.HasAdjacentCellInDirection(dungeonLocation, DirectionType.West))) dungeon.CreateWall(dungeonLocation, DirectionType.West);
                if ((roomLocation.X == room.Width - 1) && (dungeon.HasAdjacentCellInDirection(dungeonLocation, DirectionType.East))) dungeon.CreateWall(dungeonLocation, DirectionType.East);
                if ((roomLocation.Y == 0) && (dungeon.HasAdjacentCellInDirection(dungeonLocation, DirectionType.North))) dungeon.CreateWall(dungeonLocation, DirectionType.North);
                if ((roomLocation.Y == room.Height - 1) && (dungeon.HasAdjacentCellInDirection(dungeonLocation, DirectionType.South))) dungeon.CreateWall(dungeonLocation, DirectionType.South);
            }

            dungeon.AddRoom(room);
        }

        public void PlaceDoors(Dungeon dungeon)
        {
            foreach (Room room in dungeon.Rooms)
            {
                bool hasNorthDoor = false;
                bool hasSouthDoor = false;
                bool hasWestDoor = false;
                bool hasEastDoor = false;

                foreach (Point cellLocation in room.CellLocations)
                {
                    // Translate the room cell location to its location in the dungeon
                    Point dungeonLocation = new Point(room.Bounds.X + cellLocation.X, room.Bounds.Y + cellLocation.Y);

                    // Check if we are on the west boundary of our room
                    // and if there is a corridor to the west
                    if ((cellLocation.X == 0) &&
                        (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.West)) &&
                        (!hasWestDoor))
                    {
                        dungeon.CreateDoor(dungeonLocation, DirectionType.West);
                        hasWestDoor = true;
                    }

                    // Check if we are on the east boundary of our room
                    // and if there is a corridor to the east
                    if ((cellLocation.X == room.Width - 1) &&
                        (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.East)) &&
                        (!hasEastDoor))
                    {
                        dungeon.CreateDoor(dungeonLocation, DirectionType.East);
                        hasEastDoor = true;
                    }

                    // Check if we are on the north boundary of our room 
                    // and if there is a corridor to the north
                    if ((cellLocation.Y == 0) &&
                        (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.North)) &&
                        (!hasNorthDoor))
                    {
                        dungeon.CreateDoor(dungeonLocation, DirectionType.North);
                        hasNorthDoor = true;
                    }


                    // Check if we are on the south boundary of our room 
                    // and if there is a corridor to the south
                    if ((cellLocation.Y == room.Height - 1) &&
                        (dungeon.AdjacentCellInDirectionIsCorridor(dungeonLocation, DirectionType.South)) &&
                        (!hasSouthDoor))
                    {
                        dungeon.CreateDoor(dungeonLocation, DirectionType.South);
                        hasSouthDoor = true;
                    }
                }
            }
        }
    }
}