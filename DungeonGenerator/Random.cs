namespace DungeonGenerator
{
	public sealed class Random
	{
		private Random()
		{
		}

		public static Random Instance
		{
			get { return Nested.instance; }
		}

		private class Nested
		{
			// Explicit static constructor to tell C# compiler
			// not to mark type as beforefieldinit

			internal static readonly Random instance = new Random();

			static Nested()
			{
			}
		}

		private readonly MersenneTwister mersenneTwister = new MersenneTwister();

		public int Next(int maxValue)
		{
			return mersenneTwister.Next(maxValue);
		}

		public int Next(int minValue, int maxValue)
		{
			return mersenneTwister.Next(minValue, maxValue);
		}
	}
}
