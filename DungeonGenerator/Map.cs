using System;
using System.Collections.Generic;
using System.Drawing;

namespace DungeonGenerator
{
    public abstract class Map
    {
        protected readonly Cell[,] cells;
        protected Rectangle bounds;

        protected Map(int width, int height)
        {
            cells = new Cell[width,height];
            bounds = new Rectangle(0, 0, width, height);

            // Initialize the array of cells
            foreach (Point location in CellLocations)
                this[location] = new Cell();
        }

        public Rectangle Bounds
        {
            get { return bounds; }
        }

        public Cell this[Point point]
        {
            get { return this[point.X, point.Y]; }
            set { this[point.X, point.Y] = value; }
        }

        public Cell this[int x, int y]
        {
            get { return cells[x, y]; }
            set { cells[x, y] = value; }
        }

        public int Width
        {
            get { return bounds.Width; }
        }

        public int Height
        {
            get { return bounds.Height; }
        }

        public IEnumerable<Point> CellLocations
        {
            get
            {
                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                        yield return new Point(x, y);
            }
        }

        public bool HasAdjacentCellInDirection(Point location, DirectionType direction)
        {
            // Check that the location falls within the bounds of the map
            if (!Bounds.Contains(location))
                return false;

            // Check if there is an adjacent cell in the direction
            switch (direction)
            {
                case DirectionType.North:
                    return location.Y > 0;
                case DirectionType.South:
                    return location.Y < (Height - 1);
                case DirectionType.West:
                    return location.X > 0;
                case DirectionType.East:
                    return location.X < (Width - 1);
                default:
                    return false;
            }
        }

        protected Point? GetTargetLocation(Point location, DirectionType direction)
        {
            if (!HasAdjacentCellInDirection(location, direction)) return null;

            switch (direction)
            {
                case DirectionType.North:
                    return new Point(location.X, location.Y - 1);
                case DirectionType.West:
                    return new Point(location.X - 1, location.Y);
                case DirectionType.South:
                    return new Point(location.X, location.Y + 1);
                case DirectionType.East:
                    return new Point(location.X + 1, location.Y);
                default:
                    throw new InvalidOperationException();
            }
        }
    }
}