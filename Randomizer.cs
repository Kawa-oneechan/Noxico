/* Why this class?
 * 
 * I'll tell you why. There are two basic reasons:
 * 1. There's a single RNG used throughout the game. It was an instance of System.Random, as a member of Noxico.Toolkit.
 *    In turn, it was accessed all through the game as Toolkit.Rand, which seems rather superfluous to me compared to
 *    just plain Random.
 * 2. Having it separated like this allows drop-in replacement with another implementation.
 * 
 * As an added bonus, this lets us extend Noxico.Random with extra fun stuff such as coin flips and changing the seed
 * value, not only with Now or an int, but with any object that implements GetHashCode().
 * If the entire randomizer were reimplemented instead of just wrapping around System.Random, we could easily retrieve
 * the current seed value and save it too, which has its uses for gameplay. As it is, we can do that with some sneaky
 * use of Reflection and peeking at decompilations.
 */

namespace Noxico
{
	/// <summary>
	/// Represents a pseudo-random number generator, a device that produces a sequence
	/// of numbers that meet certain statistical requirements for randomness.
	/// </summary>
	public static class Random
	{
#if !USE_SYSTEM_RANDOM
		private static int seed = 0;

		/// <summary>
		/// Initializes a new instance of the System.Random class, using a time-dependent
		/// default seed value.
		/// </summary>
		public static void Reseed()
		{
			seed = System.Environment.TickCount;
		}

		/// <summary>
		/// Initializes a new instance of the System.Random class, using the specified
		/// seed value.
		/// </summary>
		/// <param name="seed">
		/// A number used to calculate a starting value for the pseudo-random number
		/// sequence. If a negative number is specified, the absolute value of the number
		/// is used.
		/// </param>
		public static void Reseed(int seed)
		{
			Random.seed = seed;
		}

		/// <summary>
		/// Initializes a new instance of the System.Random class, using the specified
		/// object's hash code as the seed value.
		/// </summary>
		/// <param name="seed">
		/// An object whose hash code is used to calculate a starting value for the
		/// pseudo-random number sequence.
		/// </param>
		public static void Reseed(object seed)
		{
			Random.seed = seed.GetHashCode();
		}

		/// <summary>
		/// Returns a nonnegative random number.
		/// </summary>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero and less than System.Int32.MaxValue.
		/// </returns>
		public static int Next()
		{
			//Values for the multiplier and increment taken from Pokémon Fire Red.
			seed = (seed * 0x41C64E6D) + 0x6073;
			return seed;
		}

		/// <summary>
		/// Returns a nonnegative random number less than the specified maximum.
		/// </summary>
		/// <param name="maxValue">
		/// The exclusive upper bound of the random number to be generated. maxValue
		/// must be greater than or equal to zero.
		/// </param>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero, and less than maxValue;
		/// that is, the range of return values ordinarily includes zero but not maxValue.
		/// However, if maxValue equals zero, maxValue is returned.
		/// </returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// maxValue is less than zero.
		/// </exception>
		public static int Next(int maxValue)
		{
			if (maxValue < 0)
				throw new System.ArgumentOutOfRangeException("maxValue", string.Format("'{0}' must be positive.", maxValue));
			return (int)(NextDouble() * maxValue);
		}

		/// <summary>
		/// Returns a random number within a specified range.
		/// </summary>
		/// <param name="minValue">
		/// The inclusive lower bound of the random number returned.
		/// </param>
		/// <param name="maxValue">
		/// The exclusive upper bound of the random number returned. maxValue must be
		/// greater than or equal to minValue.
		/// </param>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to minValue and less than maxValue;
		/// that is, the range of return values includes minValue but not maxValue. If
		/// minValue equals maxValue, minValue is returned.
		/// </returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// minValue is greater than maxValue.
		/// </exception>
		public static int Next(int minValue, int maxValue)
		{
			if (minValue > maxValue)
				throw new System.ArgumentOutOfRangeException("maxValue", string.Format("'{0}' cannot be greater than {1}.", minValue, maxValue));
			long l = maxValue - minValue;
			return (((int)(NextDouble() * l)) + minValue);
		}

		/// <summary>
		/// Returns a random number between 0.0 and 1.0.
		/// </summary>
		/// <returns>
		/// A double-precision floating point number greater than or equal to 0.0, and
		/// less than 1.0.
		/// </returns>
		public static double NextDouble()
		{
			//Hack alert! Not sure if this'll always be in range, but a hundred thousand samples show good results.
			var d = (double)Next() / 0x7fffffffL;
			if (d < 0)
				d = -d;
			return d;
		}

		/// <summary>
		/// Returns the last number determined by the RNG, without rolling a new one.
		/// </summary>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero and less than System.Int32.MaxValue.
		/// </returns>
		public static int ExtractSeed()
		{
			return seed;
		}
#else
		private static System.Random rand = new System.Random();

		/// <summary>
		/// Initializes a new instance of the System.Random class, using a time-dependent
		/// default seed value.
		/// </summary>
		public static void Reseed()
		{
			rand = new System.Random();
		}

		/// <summary>
		/// Initializes a new instance of the System.Random class, using the specified
		/// seed value.
		/// </summary>
		/// <param name="seed">
		/// A number used to calculate a starting value for the pseudo-random number
		/// sequence. If a negative number is specified, the absolute value of the number
		/// is used.
		/// </param>
		public static void Reseed(int seed)
		{
			rand = new System.Random(seed);
		}

		/// <summary>
		/// Initializes a new instance of the System.Random class, using the specified
		/// object's hash code as the seed value.
		/// </summary>
		/// <param name="seed">
		/// An object whose hash code is used to calculate a starting value for the
		/// pseudo-random number sequence.
		/// </param>
		public static void Reseed(object seed)
		{
			rand = new System.Random(seed.GetHashCode());
		}

		/// <summary>
		/// Returns a nonnegative random number.
		/// </summary>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero and less than System.Int32.MaxValue.
		/// </returns>
		public static int Next()
		{
			return rand.Next();
		}

		/// <summary>
		/// Returns a nonnegative random number less than the specified maximum.
		/// </summary>
		/// <param name="maxValue">
		/// The exclusive upper bound of the random number to be generated. maxValue
		/// must be greater than or equal to zero.
		/// </param>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero, and less than maxValue;
		/// that is, the range of return values ordinarily includes zero but not maxValue.
		/// However, if maxValue equals zero, maxValue is returned.
		/// </returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// maxValue is less than zero.
		/// </exception>
		public static int Next(int maxValue)
		{
			return rand.Next(maxValue);
		}

		/// <summary>
		/// Returns a random number within a specified range.
		/// </summary>
		/// <param name="minValue">
		/// The inclusive lower bound of the random number returned.
		/// </param>
		/// <param name="maxValue">
		/// The exclusive upper bound of the random number returned. maxValue must be
		/// greater than or equal to minValue.
		/// </param>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to minValue and less than maxValue;
		/// that is, the range of return values includes minValue but not maxValue. If
		/// minValue equals maxValue, minValue is returned.
		/// </returns>
		/// <exception cref="System.ArgumentOutOfRangeException">
		/// minValue is greater than maxValue.
		/// </exception>
		public static int Next(int minValue, int maxValue)
		{
			return rand.Next(minValue, maxValue);
		}

		/// <summary>
		/// Returns a random number between 0.0 and 1.0.
		/// </summary>
		/// <returns>
		/// A double-precision floating point number greater than or equal to 0.0, and
		/// less than 1.0.
		/// </returns>
		public static double NextDouble()
		{
			return rand.NextDouble();
		}

		//This one's just plain dumb. But it does teach one interesting fact about System.Random, in that it apparently
		//pre-rolls about fifty numbers.
		/// <summary>
		/// Returns the last number determined by the RNG, without rolling a new one.
		/// </summary>
		/// <returns>
		/// A 32-bit signed integer greater than or equal to zero and less than System.Int32.MaxValue.
		/// </returns>
		public static int ExtractSeed()
		{
			var randomType = rand.GetType();
			var bindings = System.Reflection.BindingFlags.GetField | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			var nullObject = new object[] { };
			var inext = (int)randomType.InvokeMember("inext", bindings, null, rand, null);
			var seedArray = (int[])randomType.InvokeMember("SeedArray", bindings, null, rand, null);
			return seedArray[inext];
		}
#endif

		/// <summary>
		/// Flips a coin.
		/// </summary>
		/// <returns>True if it was heads.</returns>
		public static bool Flip()
		{
			return NextDouble() > 0.5;
		}
	}
}
