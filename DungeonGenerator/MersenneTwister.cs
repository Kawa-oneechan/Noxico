using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonGenerator
{
    public class MersenneTwister
    {
        // Period parameters.
        private const int N = 624;
        private const int M = 397;
        private const uint MATRIX_A = 0x9908b0dfU;   // constant vector a
        private const uint UPPER_MASK = 0x80000000U; // most significant w-r bits
        private const uint LOWER_MASK = 0x7fffffffU; // least significant r bits
        private const int MAX_RAND_INT = 0x7fffffff;

        // mag01[x] = x * MATRIX_A  for x=0,1
        private uint[] mag01 = { 0x0U, MATRIX_A };

        // the array for the state vector
        private uint[] mt = new uint[N];

        // mti==N+1 means mt[N] is not initialized
        private int mti = N + 1;

        /// <summary>
        /// Creates a random number generator using the time of day in milliseconds as
        /// the seed.
        /// </summary>
        public MersenneTwister()
        {
            init_genrand((uint)DateTime.Now.Millisecond);
        }

        /// <summary>
        /// Creates a random number generator initialized with the given seed. 
        /// </summary>
        /// <param name="seed">The seed.</param>
        public MersenneTwister(int seed)
        {
            init_genrand((uint)seed);
        }

        /// <summary>
        /// Creates a random number generator initialized with the given array.
        /// </summary>
        /// <param name="init">The array for initializing keys.</param>
        public MersenneTwister(int[] init)
        {
            uint[] initArray = new uint[init.Length];
            for (int i = 0; i < init.Length; ++i)
                initArray[i] = (uint)init[i];

            init_by_array(initArray, (uint)initArray.Length);
        }

        /// <summary>
        /// Gets the maximum random integer value. All random integers generated
        /// by instances of this class are less than or equal to this value. This
        /// value is <c>0x7fffffff</c> (<c>2,147,483,647</c>).
        /// </summary>
        public static int MaxRandomInt
        {
            get
            {
                return 0x7fffffff;
            }
        }

        /// <summary>
        /// Returns a random integer greater than or equal to zero and
        /// less than or equal to <c>MaxRandomInt</c>. 
        /// </summary>
        /// <returns>The next random integer.</returns>
        public int Next()
        {
            return genrand_int31();
        }

        /// <summary>
        /// Returns a positive random integer less than the specified maximum.
        /// </summary>
        /// <param name="maxValue">The maximum value. Must be greater than zero.</param>
        /// <returns>A positive random integer less than or equal to <c>maxValue</c>.</returns>
        public int Next(int maxValue)
        {
            return Next(0, maxValue);
        }

        /// <summary>
        /// Returns a random integer within the specified range.
        /// </summary>
        /// <param name="minValue">The lower bound.</param>
        /// <param name="maxValue">The upper bound.</param>
        /// <returns>A random integer greater than or equal to <c>minValue</c>, and less than
        /// or equal to <c>maxValue</c>.</returns>
        public int Next(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                int tmp = maxValue;
                maxValue = minValue;
                minValue = tmp;
            }

            return (int)(Math.Floor((maxValue - minValue + 1) * genrand_real1() + minValue));
        }

        /// <summary>
        /// Returns a random number between 0.0 and 1.0.
        /// </summary>
        /// <returns>A single-precision floating point number greater than or equal to 0.0, 
        /// and less than 1.0.</returns>
        public float NextFloat()
        {
            return (float)genrand_real2();
        }

        /// <summary>
        /// Returns a random number greater than or equal to zero, and either strictly
        /// less than one, or less than or equal to one, depending on the value of the
        /// given boolean parameter.
        /// </summary>
        /// <param name="includeOne">
        /// If <c>true</c>, the random number returned will be 
        /// less than or equal to one; otherwise, the random number returned will
        /// be strictly less than one.
        /// </param>
        /// <returns>
        /// If <c>includeOne</c> is <c>true</c>, this method returns a
        /// single-precision random number greater than or equal to zero, and less
        /// than or equal to one. If <c>includeOne</c> is <c>false</c>, this method
        /// returns a single-precision random number greater than or equal to zero and
        /// strictly less than one.
        /// </returns>
        public float NextFloat(bool includeOne)
        {
            if (includeOne)
            {
                return (float)genrand_real1();
            }
            return (float)genrand_real2();
        }

        /// <summary>
        /// Returns a random number greater than 0.0 and less than 1.0.
        /// </summary>
        /// <returns>A random number greater than 0.0 and less than 1.0.</returns>
        public float NextFloatPositive()
        {
            return (float)genrand_real3();
        }

        /// <summary>
        /// Returns a random number between 0.0 and 1.0.
        /// </summary>
        /// <returns>A double-precision floating point number greater than or equal to 0.0, 
        /// and less than 1.0.</returns>
        public double NextDouble()
        {
            return genrand_real2();
        }

        /// <summary>
        /// Returns a random number greater than or equal to zero, and either strictly
        /// less than one, or less than or equal to one, depending on the value of the
        /// given boolean parameter.
        /// </summary>
        /// <param name="includeOne">
        /// If <c>true</c>, the random number returned will be 
        /// less than or equal to one; otherwise, the random number returned will
        /// be strictly less than one.
        /// </param>
        /// <returns>
        /// If <c>includeOne</c> is <c>true</c>, this method returns a
        /// single-precision random number greater than or equal to zero, and less
        /// than or equal to one. If <c>includeOne</c> is <c>false</c>, this method
        /// returns a single-precision random number greater than or equal to zero and
        /// strictly less than one.
        /// </returns>
        public double NextDouble(bool includeOne)
        {
            if (includeOne)
            {
                return genrand_real1();
            }
            return genrand_real2();
        }

        /// <summary>
        /// Returns a random number greater than 0.0 and less than 1.0.
        /// </summary>
        /// <returns>A random number greater than 0.0 and less than 1.0.</returns>
        public double NextDoublePositive()
        {
            return genrand_real3();
        }

        /// <summary>
        /// Generates a random number on <c>[0,1)</c> with 53-bit resolution.
        /// </summary>
        /// <returns>A random number on <c>[0,1)</c> with 53-bit resolution</returns>
        public double Next53BitRes()
        {
            return genrand_res53();
        }

        /// <summary>
        /// Reinitializes the random number generator using the time of day in
        /// milliseconds as the seed.
        /// </summary>
        public void Initialize()
        {
            init_genrand((uint)DateTime.Now.Millisecond);
        }


        /// <summary>
        /// Reinitializes the random number generator with the given seed.
        /// </summary>
        /// <param name="seed">The seed.</param>
        public void Initialize(int seed)
        {
            init_genrand((uint)seed);
        }

        /// <summary>
        /// Reinitializes the random number generator with the given array.
        /// </summary>
        /// <param name="init">The array for initializing keys.</param>
        public void Initialize(int[] init)
        {
            uint[] initArray = new uint[init.Length];
            for (int i = 0; i < init.Length; ++i)
                initArray[i] = (uint)init[i];

            init_by_array(initArray, (uint)initArray.Length);
        }

        // initializes mt[N] with a seed
        private void init_genrand(uint s)
        {
            mt[0] = s & 0xffffffffU;
            for (mti = 1; mti < N; mti++)
            {
                mt[mti] =
                  (uint)(1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + mti);
                // See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier. 
                // In the previous versions, MSBs of the seed affect   
                // only MSBs of the array mt[].                        
                // 2002/01/09 modified by Makoto Matsumoto             
                mt[mti] &= 0xffffffffU;
                // for >32 bit machines
            }
        }

        // initialize by an array with array-length
        // init_key is the array for initializing keys 
        // key_length is its length
        private void init_by_array(uint[] init_key, uint key_length)
        {
            int i, j, k;
            init_genrand(19650218U);
            i = 1; j = 0;
            k = (int)(N > key_length ? N : key_length);
            for (; k > 0; k--)
            {
                mt[i] = (uint)((uint)(mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1664525U)) + init_key[j] + j); /* non linear */
                mt[i] &= 0xffffffffU; // for WORDSIZE > 32 machines
                i++; j++;
                if (i >= N) { mt[0] = mt[N - 1]; i = 1; }
                if (j >= key_length) j = 0;
            }
            for (k = N - 1; k > 0; k--)
            {
                mt[i] = (uint)((uint)(mt[i] ^ ((mt[i - 1] ^ (mt[i - 1] >> 30)) * 1566083941U)) - i); /* non linear */
                mt[i] &= 0xffffffffU; // for WORDSIZE > 32 machines
                i++;
                if (i >= N) { mt[0] = mt[N - 1]; i = 1; }
            }

            mt[0] = 0x80000000U; // MSB is 1; assuring non-zero initial array
        }

        // generates a random number on [0,0xffffffff]-interval
        uint genrand_int32()
        {
            uint y;
            if (mti >= N)
            { /* generate N words at one time */
                int kk;

                if (mti == N + 1)   /* if init_genrand() has not been called, */
                    init_genrand(5489U); /* a default initial seed is used */

                for (kk = 0; kk < N - M; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1U];
                }
                for (; kk < N - 1; kk++)
                {
                    y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
                    mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1U];
                }
                y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
                mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1U];

                mti = 0;
            }

            y = mt[mti++];

            // Tempering
            y ^= (y >> 11);
            y ^= (y << 7) & 0x9d2c5680U;
            y ^= (y << 15) & 0xefc60000U;
            y ^= (y >> 18);

            return y;
        }

        // generates a random number on [0,0x7fffffff]-interval
        private int genrand_int31()
        {
            return (int)(genrand_int32() >> 1);
        }

        // generates a random number on [0,1]-real-interval
        double genrand_real1()
        {
            return genrand_int32() * (1.0 / 4294967295.0);
            // divided by 2^32-1
        }

        // generates a random number on [0,1)-real-interval
        double genrand_real2()
        {
            return genrand_int32() * (1.0 / 4294967296.0);
            // divided by 2^32
        }

        // generates a random number on (0,1)-real-interval
        double genrand_real3()
        {
            return (((double)genrand_int32()) + 0.5) * (1.0 / 4294967296.0);
            // divided by 2^32
        }

        // generates a random number on [0,1) with 53-bit resolution
        double genrand_res53()
        {
            uint a = genrand_int32() >> 5, b = genrand_int32() >> 6;
            return (a * 67108864.0 + b) * (1.0 / 9007199254740992.0);
        }
        // These real versions are due to Isaku Wada, 2002/01/09 added
    }
}
