using System.Drawing;

namespace DungeonGenerator
{
	public class Room : Map
	{
		public Room(int width, int height)
			: base(width, height)
		{
		}

		public void InitializeRoomCells()
		{
			foreach (Point location in CellLocations)
			{
				Cell cell = new Cell();

				cell.WestSide = (location.X == bounds.X) ? SideType.Wall : SideType.Empty;
				cell.EastSide = (location.X == bounds.Width - 1) ? SideType.Wall : SideType.Empty;
				cell.NorthSide = (location.Y == bounds.Y) ? SideType.Wall : SideType.Empty;
				cell.SouthSide = (location.Y == bounds.Height - 1) ? SideType.Wall : SideType.Empty;

				this[location] = cell;
			}
		}

		public void SetLocation(Point location)
		{
			bounds = new Rectangle(location, bounds.Size);
		}
	}
}
