using System;
using System.Collections.Generic;

namespace DungeonGenerator
{
	public enum DirectionType
	{
		North,
		South,
		East,
		West
	}

	public class DirectionPicker
	{
		private readonly List<DirectionType> directionsPicked = new List<DirectionType>();
		private readonly DirectionType previousDirection;
		private readonly int changeDirectionModifier;

		public DirectionPicker(DirectionType previousDirection, int changeDirectionModifier)
		{
			this.previousDirection = previousDirection;
			this.changeDirectionModifier = changeDirectionModifier;
		}

		public bool HasNextDirection
		{
			get { return directionsPicked.Count < 4; }
		}

		private bool MustChangeDirection
		{
			get
			{
				// changeDirectionModifier of 100 will always change direction
				// value of 0 will never change direction
				return ((directionsPicked.Count > 0) || (changeDirectionModifier > Random.Instance.Next(0, 99)));
			}
		}

		private DirectionType PickDifferentDirection()
		{
			DirectionType directionPicked;
			do
			{
				directionPicked = (DirectionType)Random.Instance.Next(3);
			} while ((directionPicked == previousDirection) && (directionsPicked.Count < 3));

			return directionPicked;
		}

		public DirectionType GetNextDirection()
		{
			if (!HasNextDirection) throw new InvalidOperationException("No directions available");

			DirectionType directionPicked;

			do
			{
				directionPicked = MustChangeDirection ? PickDifferentDirection() : previousDirection;
			} while (directionsPicked.Contains(directionPicked));

			directionsPicked.Add(directionPicked);

			return directionPicked;
		}
	}
}
