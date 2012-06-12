using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	//Stolen from... somewhere.
	public class PerlinNoise
	{
		/// Perlin Noise Constructot
		public PerlinNoise(int width, int height)
		{
			this.MAX_WIDTH = width;
			this.MAX_HEIGHT = height;
		}

		public int MAX_WIDTH = 256;
		public int MAX_HEIGHT = 256;

		/// Gets the value for a specific X and Y coordinate
		/// results in range [-1, 1] * maxHeight
		public float GetRandomHeight(float X, float Y, float MaxHeight,
			float Frequency, float Amplitude, float Persistance,
			int Octaves)
		{
			GenerateNoise();
			float FinalValue = 0.0f;
			for (int i = 0; i < Octaves; ++i)
			{
				FinalValue += GetSmoothNoise(X * Frequency, Y * Frequency) * Amplitude;
				Frequency *= 2.0f;
				Amplitude *= Persistance;
			}
			if (FinalValue < -1.0f)
			{
				FinalValue = -1.0f;
			}
			else if (FinalValue > 1.0f)
			{
				FinalValue = 1.0f;
			}
			return FinalValue * MaxHeight;
		}

		//This function is a simple bilinear filtering function which is good (and easy) enough.
		private float GetSmoothNoise(float X, float Y)
		{
			float FractionX = X - (int)X;
			float FractionY = Y - (int)Y;
			int X1 = ((int)X + MAX_WIDTH) % MAX_WIDTH;
			int Y1 = ((int)Y + MAX_HEIGHT) % MAX_HEIGHT;
			//for cool art deco looking images, do +1 for X2 and Y2 instead of -1...
			int X2 = ((int)X + MAX_WIDTH - 1) % MAX_WIDTH;
			int Y2 = ((int)Y + MAX_HEIGHT - 1) % MAX_HEIGHT;
			float FinalValue = 0.0f;
			FinalValue += FractionX * FractionY * Noise[X1, Y1];
			FinalValue += FractionX * (1 - FractionY) * Noise[X1, Y2];
			FinalValue += (1 - FractionX) * FractionY * Noise[X2, Y1];
			FinalValue += (1 - FractionX) * (1 - FractionY) * Noise[X2, Y2];
			return FinalValue;
		}

		float[,] Noise;
		bool NoiseInitialized = false;
		/// create a array of randoms
		private void GenerateNoise()
		{
			if (NoiseInitialized)                //A boolean variable in the class to make sure we only do this once
				return;
			Noise = new float[MAX_WIDTH, MAX_HEIGHT];    //Create the noise table where MAX_WIDTH and MAX_HEIGHT are set to some value>0
			for (int x = 0; x < MAX_WIDTH; ++x)
			{
				for (int y = 0; y < MAX_HEIGHT; ++y)
				{
					Noise[x, y] = ((float)(Toolkit.Rand.NextDouble()) - 0.5f) * 2.0f;  //Generate noise between -1 and 1
				}
			}
			NoiseInitialized = true;
		}

	}

	public class WorldGen
	{
		public int[,] BiomeMap, TownMap;
		public int MapSizeX, MapSizeY;
		public int OverworldSize;
		public int[] OceanBitmap;
		public byte[,] BiomeBitmap;

		public static List<BiomeData> Biomes;
		public static int WaterLevel;

		private static byte[,] CreateHeightMap(int reach)
		{
			var map = new byte[reach, reach];
			var noise = new PerlinNoise(reach, reach);
			var wDiv = 1 / (double)reach;
			var hDiv = 1 / (double)reach;
			var dist = reach / 3;
			var distMod = 1 / (float)dist;
			for (var row = 0; row < reach; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var overall = noise.GetRandomHeight(col, row, 1f, 0.02f, 0.65f, 0.4f, 4) + 0.3;
					var rough = noise.GetRandomHeight(col, row, 1f, 0.05f, 0.65f, 0.5f, 8);
					var extra = noise.GetRandomHeight(col, row, 0.05f, 1f, 1f, 1f, 8);
					//var rough = 0f;
					//var extra = 0f;
					var v = (overall + (rough * 0.75) + extra) + 0.3; // + 0.5;

					if (row < dist) v *= distMod * row;
					if (col < dist) v *= distMod * col;
					if (row > reach - dist) v *= distMod * (reach - row);
					if (col > reach - dist) v *= distMod * (reach - col);

					if (v < 0) v = 0;
					if (v > 1) v = 1;
					var b = (byte)(v * 255);

					map[row, col] = b;
				}
			}
			return map;
		}

		private static byte[,] CreateClouds(int reach, float freq, double offset = 0.3, bool poles = false)
		{
			var map = new byte[reach, reach];
			var noise = new PerlinNoise(reach, reach);
			var wDiv = 1 / (double)reach;
			var hDiv = 1 / (double)reach;
			var dist = reach / 5;
			var distMod = 1 / (float)dist;
			for (var row = 0; row < reach; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var overall = noise.GetRandomHeight(col, row, 1f, freq, 0.45f, 0.8f, 2) + offset;
					var v = overall;

					if (poles)
					{
						v += 0.04;
						if (row < dist) v -= 2 - ((distMod * row) * 2);
						if (row > reach - dist) v -= 2 - ((distMod * (reach - row)) * 2);
					}

					if (v < 0) v = 0;
					if (v > 1) v = 1;
					var b = (byte)(v * 255);

					map[row, col] = b;
				}
			}
			return map;
		}

		private static byte[,] CreateBiomeMap(int reach, byte[,] height, byte[,] precip, byte[,] temp)
		{
			var map = new byte[(reach / 2) + 25, reach + 80];
			for (var row = 0; row < reach / 2; row++)
			{
				for (var col = 0; col < reach; col++)
				{
					var h = height[row * 2, col];
					var p = precip[row * 2, col];
					var t = temp[row * 2, col];
					/*
					if (h < 64)
						map[row, col] = 4; //Water
					//else if (h > 253)
					//	map[row, col] = 3; //Mountain snow
					else if (p < 128 && t > 160)
						map[row, col] = 1; //Desert
					else if (t < 64)
						map[row, col] = 2; //Snow
					else if (p > 160)
						map[row, col] = 3; //Swamp
					*/
					if (h < WaterLevel)
					{
						map[row, col] = 0;
						continue;
					}
					for (var i = 0; i < Biomes.Count; i++)
					{
						var b = Biomes[i];
						if (t >= b.Rect.Left && t <= b.Rect.Right && p >= b.Rect.Top && p <= b.Rect.Bottom)
						{
							map[row, col] = (byte)i;
							continue;
						}
					}
				}
			}
			return map;
		}

		public void Generate(string randSeed = "")
		{
			var seed = 0xF00D;
			var reach = 1000;

			if (!string.IsNullOrWhiteSpace(randSeed))
			{
				if (randSeed.StartsWith("0x"))
				{
					randSeed = randSeed.Substring(2);
					if (!int.TryParse(randSeed, System.Globalization.NumberStyles.HexNumber, null, out seed))
					{
						seed = randSeed.GetHashCode();
						Console.WriteLine("Using hash seed -- \"{0}\" -> 0x{1:X}", randSeed, seed);
					}
					else
						Console.WriteLine("Using seed 0x{0:X}.", seed);
				}
				else
				{
					if (!int.TryParse(randSeed, out seed))
					{
						seed = randSeed.GetHashCode();
						Console.WriteLine("Using hash seed -- \"{0}\" -> 0x{1:X}", randSeed, seed);
					}
					else
						Console.WriteLine("Using seed 0x{0:X}.", seed);
				}
			}
			else
			{
				Console.WriteLine("Using timer as seed.");
				seed = Environment.TickCount;
			}

			var watch = new System.Diagnostics.Stopwatch();
			watch.Start();

			Biomes = new List<BiomeData>();
			var x = new XmlDocument();
			x.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Biomes, "biomes.xml"));
			var realm = x.SelectSingleNode("//realm[@id=\"" + NoxicoGame.HostForm.Noxico.Player.CurrentRealm + "\"]") as XmlElement;
			WaterLevel = int.Parse(realm.GetAttribute("waterLevel"));
			foreach (var b in realm.SelectNodes("biome").OfType<XmlElement>())
				Biomes.Add(BiomeData.FromXML(b));

			Console.WriteLine("Creating heightmap...");
			var height = CreateHeightMap(reach);
			Console.WriteLine("Creating precipitation map...");
			var precip = CreateClouds(reach, 0.015f, 0.3, false);
			Console.WriteLine("Creating temperature map...");
			var temp = CreateClouds(reach, 0.015f, 0.5, true);
			Console.WriteLine("Creating biome map...");
			var biome = CreateBiomeMap(reach, height, precip, temp);

			watch.Stop();
			Console.WriteLine("Generated a world of {0}²px in {1}.", reach, watch.Elapsed.ToString());

			Console.WriteLine("Drawing board bitmap...");
			var bmpWidth = (int)Math.Floor(reach / 80.0) * 80;
			var bmpHeight = reach / 2;
			var bmp = new byte[bmpHeight + 1, bmpWidth + 1];
			for (var row = 0; row < bmpHeight; row++)
				for (var col = 0; col < bmpWidth; col++)
					bmp[row, col] = biome[row, col];
			BiomeBitmap = bmp;

			MapSizeX = 11;
			MapSizeY = 20;
			OverworldSize = 12 * 12;
			BiomeMap = new int[22, 13]; //maps to usual biome list
			OceanBitmap = new int[OverworldSize];
			var oceans = 0;
			for (var bRow = 0; bRow < 20; bRow++)
			{
				for (var bCol = 0; bCol < 12; bCol++)
				{
					var counts = new int[255];
					var oceanTreshold = 2000 - 4;
					//Count the colors, 1 2 and 3. Everything goes, coming up OOO!
					for (var pRow = 0; pRow < 25; pRow++)
					{
						for (var pCol = 0; pCol < 80; pCol++)
						{
							var b = biome[(bRow * 25) + pRow, (bCol * 80) + pCol];
							counts[b]++;
						}
					}
					//Special rule for Oceans
					if (counts[0] >= oceanTreshold)
					{
						BiomeMap[bRow, bCol] = 0;
						//OceanBitmap[(bRow * 21) + bCol] = 1;
						oceans++;
						continue;
					}
					//Determine most significant non-Ocean biome
					if (counts[0] > counts[1] && counts[0] > counts[2] && counts[0] > counts[3])
						BiomeMap[bRow, bCol] = 0;
					else if (counts[1] > counts[0] && counts[1] > counts[2] && counts[1] > counts[3])
						BiomeMap[bRow, bCol] = 1;
					else if (counts[2] > counts[0] && counts[2] > counts[1] && counts[2] > counts[3])
						BiomeMap[bRow, bCol] = 2;
					else if (counts[3] > counts[0] && counts[3] > counts[1] && counts[3] > counts[2])
						BiomeMap[bRow, bCol] = 3;
				}
			}

			Console.WriteLine("Finding watering holes...");
			var towns = 0;
			var townBoards = 0;
			var wateringHoles = 0;
			TownMap = new int[22, 13]; //0 - none, -1 - watering hole (town can go nearby), >0 - town
			for (var bRow = 0; bRow < 20; bRow++)
			{
				for (var bCol = 0; bCol < 12; bCol++)
				{
					//Find a board with a reasonable amount of water
					
					if (BiomeMap[bRow, bCol] == 4)
						continue;

					var waterAmount = 0;
					var waterMin = 128;
					var waterMax = 256;
					for (var pRow = 0; pRow < 25; pRow++)
						for (var pCol = 0; pCol < 80; pCol++)
							if (biome[(bRow * 25) + pRow, (bCol * 80) + pCol] == 0)
								waterAmount++;
					if (waterAmount >= waterMin && waterAmount <= waterMax)
					{
						//Seems like a nice place. Mark off.
						TownMap[bRow, bCol] = -1;
						wateringHoles++;
					}
				}
			}
			for (var bRow = 0; bRow < 20; bRow++)
			{
				for (var bCol = 0; bCol < 12; bCol++)
				{
					if (TownMap[bRow, bCol] == -1)
					{
						var added = 0;
						for (var row = bRow - 1; row < bRow + 1; row++)
						{
							for (var col = bCol - 1; col < bCol + 1; col++)
							{
								if (TownMap[row, col] != 0)
									continue;
								var waterAmount = 0;
								var waterMax = 128;
								for (var pRow = 0; pRow < 25; pRow++)
									for (var pCol = 0; pCol < 80; pCol++)
										if (biome[(row * 25) + pRow, (col * 80) + pCol] == 0)
											waterAmount++;
								if (waterAmount < waterMax)
								{
									TownMap[row, col] = towns + 1;
									townBoards++;
									added++;
								}
							}
						}
						if (added > 0)
							towns++;
					}
				}
			}
		}
	}

	public class BiomeData
	{
		public string Name { get; private set; }
		public System.Drawing.Color Color { get; private set; }
		public System.Drawing.Rectangle Rect { get; private set; }
		public string Music { get; set; }
		public bool IsWater { get; set; }
		public bool CanBurn { get; set; }

		public static BiomeData FromXML(XmlElement x)
		{
			var n = new BiomeData();
			n.Name = x.GetAttribute("name");
			var cvars = x.GetAttribute("rect").Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Rect = new System.Drawing.Rectangle(cvars[0], cvars[1], cvars[2] - cvars[0], cvars[3] - cvars[1]);
			cvars = x.GetAttribute("color").Split(' ').Select(i => int.Parse(i)).ToArray();
			n.Color = System.Drawing.Color.FromArgb(cvars[0], cvars[1], cvars[2]);
			n.Music = x.GetAttribute("music");
			n.IsWater = x.HasAttribute("isWater");
			n.CanBurn = x.HasAttribute("canBurn");
			return n;
		}
	}
}
