using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;


namespace Noxico
{
	public class NoxicoGame
	{
		public static SoundSystem Sound;

		public int Speed { get; set; }
		public static bool Immediate { get; set; }

		public static MainForm HostForm { get; private set; }
		public static bool[] KeyMap { get; set; }
		public static bool[] KeyTrg { get; set; }
		public static bool[] Modifiers { get; set; }
		public static DateTime[] KeyRepeat { get; set; }
		public static char LastPress { get; set; }
		public static bool ScrollWheeled { get; set; }
		public static int AutoRestTimer { get; set; }
		public static int AutoRestSpeed { get; set; }
		public static bool Mono { get; set; }

		public static List<InventoryItem> KnownItems { get; private set; }
		public List<Board> Boards { get; set; }
		public Board CurrentBoard { get; set; }
		public int[,] Overworld { get; set; }
		public int OverworldBarrier { get; private set; }
		public Player Player { get; set; }
		public static List<string> BookTitles { get; private set; }
		public static List<StatusMessage> Messages { get; set; }
		public static Dictionary<string, double> ScriptVariables = new Dictionary<string, double>();
		public static UserMode Mode { get; set; }
		public static Cursor Cursor { get; set; }
		public static SubscreenFunc Subscreen { get; set; }
		public static Dictionary<string, char> Views { get; set; }
		public static string[] TileDescriptions { get; private set; }

		private DateTime lastUpdate;

		public NoxicoGame(MainForm hostForm)
		{
			lastUpdate = DateTime.Now;
			Speed = 60;
			this.Boards = new List<Board>();
			HostForm = hostForm;
			KeyMap = new bool[256];
			KeyTrg = new bool[256];
			KeyRepeat = new DateTime[256];
			Modifiers = new bool[3];
			AutoRestSpeed = IniFile.GetInt("misc", "autorest", 100); //100;
			if (AutoRestSpeed < 5)
				AutoRestSpeed = 5;
			Cursor = new Cursor();
			Messages = new List<StatusMessage>();
			Sound = new SoundSystem();

			var xDoc = new XmlDocument();
			xDoc.Load("noxico.xml");
			KnownItems = new List<InventoryItem>();
			Views = new Dictionary<string, char>();
			foreach (var item in xDoc.SelectNodes("//items/item").OfType<XmlElement>())
				KnownItems.Add(InventoryItem.FromXML(item));
			foreach (var bodyPlan in xDoc.SelectNodes("//bodyplan").OfType<XmlElement>())
			{
				var id = bodyPlan.GetAttribute("id");
				var plan = bodyPlan.ChildNodes[0].Value.Replace("\r\n", "\n");
				var ascii = Toolkit.GrabToken(plan, "ascii");
				if (ascii != null)
				{
					var ch = ascii.IndexOf("\tchar: ");
					if (ch != -1)
					{
						var part = ascii.Substring(ascii.IndexOf("U+") + 2);
						part = part.Remove(part.IndexOf('\n'));
						var c = int.Parse(part, System.Globalization.NumberStyles.HexNumber);
						Views.Add(id, (char)c);
					}
				}
			}
			
			//Tile descriptions
			TileDescriptions = global::Noxico.Properties.Resources.TileSpecialDescriptions.Split('\n');

			Console.WriteLine("Loading books...");
			string bookData = null;
			if (File.Exists("books.xml"))
			{
				if (!File.Exists("books.dat") || File.GetLastWriteTime("books.xml") > File.GetLastWriteTime("books.dat"))
				{
					Console.WriteLine("Found a raw XML library newer than the encoded one. Packing it in...");
					var bookDat = new CryptStream(new GZipStream(File.Open("books.dat", FileMode.Create), CompressionMode.Compress));
					var bookBytes = File.ReadAllBytes("books.xml");
					bookDat.Write(bookBytes, 0, bookBytes.Length);
					bookData = Encoding.UTF8.GetString(bookBytes);
				}
			}
			BookTitles = new List<string>();
			BookTitles.Add("[null]");
			//var books = Directory.EnumerateFiles("books", "BOK*.txt");
			//foreach (var book in books)
			//	BookTitles.Add(File.ReadLines(book).First());
			if (bookData != null)
				xDoc.LoadXml(bookData);
			else if (File.Exists("books.dat"))
				xDoc.Load(new CryptStream(new GZipStream(File.OpenRead("books.dat"), CompressionMode.Decompress)));
			var books = xDoc.SelectNodes("//book");
			foreach (var b in books.OfType<XmlElement>())
				BookTitles.Add(b.GetAttribute("title"));

			CurrentBoard = new Board();
			if (IniFile.GetBool("misc", "skiptitle", false) && File.Exists("world.bin"))
			{
				HostForm.Noxico = this;
				LoadGame();
				HostForm.Noxico.CurrentBoard.Draw();
				Subscreens.FirstDraw = true;
				Immediate = true;
				AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
				AddMessage("Remember, press F1 for help and options.");
				Mode = UserMode.Walkabout;
			}
			else
				Introduction.Title();
		}

		public void SaveGame()
		{
			NoxicoGame.HostForm.Text = "Saving...";
			byte bits = 0;
			if (IniFile.GetBool("saving", "gzip", false))
				bits |= 1;
			if (IniFile.GetBool("saving", "flip", false))
				bits |= 2;
			var header = Encoding.UTF8.GetBytes("NOXiCO");
			var file = File.Open("world.bin", FileMode.Create);
			var bin = new BinaryWriter(file);
			bin.Write(header);
			bin.Write(bits);
			if ((bits & 1) == 1)
			{
				var gzip = new GZipStream(file, CompressionMode.Compress);
				if ((bits & 2) == 2)
				{
					var cryp = new CryptStream(gzip);
					bin = new BinaryWriter(cryp);
				}
				else
					bin = new BinaryWriter(gzip);
			}
			else if ((bits & 2) == 2)
			{
				var cryp = new CryptStream(file);
				bin = new BinaryWriter(cryp);
			}

			Console.WriteLine("--------------------------");
			Console.WriteLine("Saving World...");

			bin.Write(Overworld.GetLength(0));

			var currentIndex = 0;
			for (int i = 0; i < Boards.Count; i++)
			{
				if (CurrentBoard == Boards[i])
				{
					currentIndex = i;
					break;
				}
			}

			Player.SaveToFile(bin);

			bin.Write(currentIndex);
			bin.Write(Boards.Count);
			foreach (var b in Boards)
				b.SaveToFile(bin);

			bin.Flush();

			file.Flush();
			file.Close();
			Console.WriteLine("Done.");
			Console.WriteLine("--------------------------");
			NoxicoGame.HostForm.Text = string.Format("Noxico - {0}", CurrentBoard.Name);
		}

		public void LoadGame()
		{
			NoxicoGame.HostForm.Text = "Noxico - Loading...";
			var file = File.Open("world.bin", FileMode.Open);
			var bin = new BinaryReader(file);
			var header = bin.ReadBytes(6);
			if (Encoding.UTF8.GetString(header) != "NOXiCO")
			{
				MessageBox.Message("Invalid world header.");
				return;
			}
			var bits = bin.ReadByte();
			if ((bits & 1) == 1)
			{
				var gzip = new GZipStream(file, CompressionMode.Decompress);
				if ((bits & 2) == 2)
				{
					var cryp = new CryptStream(gzip);
					bin = new BinaryReader(cryp);
				}
				else
					bin = new BinaryReader(gzip);
			}
			else if ((bits & 2) == 2)
			{
				var cryp = new CryptStream(file);
				bin = new BinaryReader(cryp);
			}

			var reach = bin.ReadInt32();
			Overworld = new int[reach, reach];
			OverworldBarrier = reach * reach;
			var z = 0;
			for (var y = 0; y < reach; y++)
				for (var x = 0; x < reach; x++)
					Overworld[x, y] = z++;

			Player = Player.LoadFromFile(bin);
			Player.AdjustView();

			var currentIndex = bin.ReadInt32();
			var boardCount = bin.ReadInt32();
			Boards = new List<Board>();
			for (int i = 0; i < boardCount; i++)
				Boards.Add(Board.LoadFromFile(bin));

			CurrentBoard = Boards[currentIndex];
			CurrentBoard.Entities.Add(Player);
			Player.ParentBoard = CurrentBoard;
			CurrentBoard.Redraw();
			Sound.PlayMusic(CurrentBoard.Music);

			file.Close();
			NoxicoGame.HostForm.Text = string.Format("Noxico - {0}", CurrentBoard.Name);
		}

		public static void DrawMessages()
		{
			if (Messages.Count == 0)
				return;
			var row = 24;
			for (var i = 0; i < 4 && i < Messages.Count; i++)
			{
				var m = Messages.Count - 1 - i;
				HostForm.Write(' ' + Messages[m].Message + ' ', Messages[m].Color, Color.Black, 0, row);
				row--;
			}
		}
		public static void ClearMessages()
		{
			Messages.Clear();
		}
		public static void UpdateMessages()
		{
			if (Messages.Count == 0)
				return;
			if (Messages[0].New)
			{
				Messages[0].New = false;
				return;
			}
			Messages.RemoveAt(0);
			HostForm.Noxico.CurrentBoard.Redraw();
		}
		public static void AddMessage(string message, Color color)
		{
			Messages.Add(new StatusMessage() { Message = message, Color = color, New = true });
		}
		public static void AddMessage(string message)
		{
			Messages.Add(new StatusMessage() { Message = message, Color = Color.Silver, New = true });
		}

		public void Update()
		{
			if (Mode != UserMode.Subscreen)
			{
				if (Mode == UserMode.Walkabout)
				{
					var timeNow = DateTime.Now;
					//while ((DateTime.Now - timeNow).Milliseconds < (Immediate ? 1 : Speed)) ;
					if ((timeNow - lastUpdate).Milliseconds >= Speed)
					{
						lastUpdate = timeNow;
						AutoRestTimer--;
						if (AutoRestTimer <= 0)
						{
							AutoRestTimer = AutoRestSpeed;
							KeyMap[(int)Keys.OemPeriod] = true;
						}
						CurrentBoard.Update();
					}
				}
				CurrentBoard.Draw();
				DrawMessages();

				if (Mode == UserMode.LookAt)
				{
					var timeNow = DateTime.Now;
					//while ((DateTime.Now - timeNow).Milliseconds < (Immediate ? 1 : Speed)) ;
					if ((timeNow - lastUpdate).Milliseconds >= Speed)
					{
						lastUpdate = timeNow;
						Cursor.Update();
					}
					Cursor.Draw();
				}
			}
			else
			{
				if (Subscreen != null)
					Subscreen();
				else
					Mode = UserMode.Walkabout;
			}

			Sound.Update();
			HostForm.Draw();
			Immediate = false;
			for (int i = 0; i < KeyTrg.Length; i++)
				KeyTrg[i] = false;
			if (ScrollWheeled)
			{
				KeyMap[(int)Keys.Up] = false;
				KeyMap[(int)Keys.Down] = false;
				ScrollWheeled = false;
			}
		}

		public static void ClearKeys()
		{
			for (var i = 0; i < 255; i++)
			{
				KeyMap[i] = false;
				KeyTrg[i] = false;
				KeyRepeat[i] = DateTime.Now;
			}
		}

		private void SpreadBiome(Biome[,] map, int x, int y, int reach, Random rand, int[] amounts, int[] counts)
		{
			var b = map[x, y];
			if (b == Biome.Grassland)
				return;
			if (counts[(int)b] == amounts[(int)b])
				return;
			if (y > 0 && rand.NextDouble() >= 0.5 && map[x, y - 1] == Biome.Grassland)
			{
				map[x, y - 1] = b;
				counts[(int)b]++;
				if (counts[(int)b] == amounts[(int)b])
					return;
			}
			if (y < reach - 1 && rand.NextDouble() >= 0.5 && map[x, y + 1] == Biome.Grassland)
			{
				map[x, y + 1] = b;
				counts[(int)b]++;
				if (counts[(int)b] == amounts[(int)b])
					return;
			}
			if (b == Biome.Desert && x + 1 >= reach / 2)
				return;
			if (b == Biome.Snow && x - 1 <= reach - (reach / 2))
				return;
			if (x > 0 && rand.NextDouble() >= 0.5 && map[x - 1, y] == Biome.Grassland)
			{
				map[x - 1, y] = b;
				counts[(int)b]++;
				if (counts[(int)b] == amounts[(int)b])
					return;
			}
			if (x < reach - 1 && rand.NextDouble() >= 0.5 && map[x + 1, y] == Biome.Grassland)
			{
				map[x + 1, y] = b;
				counts[(int)b]++;
				if (counts[(int)b] == amounts[(int)b])
					return;
			}
		}
		private Biome[,] GenerateBiomeMap(int reach)
		{
			if (reach < 8)
				throw new ArgumentOutOfRangeException("reach", "Reach must be at least 8. WHAT ARE YOU THINKING, KAWA?");
			while (true)
			{
				var time = Environment.TickCount;
				var ret = new Biome[reach, reach];
				var rand = Toolkit.Rand;
				var size = reach * reach;
				var amounts = new[] { size / 2, size / 4, size / 8, size / 8 };
				//Place seed nodes
				for (var seeds = 0; seeds < 4; seeds++)
				{
					for (var biome = 1; biome < 4; biome++)
					{
						var x = rand.Next(4, reach - 4);
						var y = rand.Next(2, reach - 2);
						if (biome == 1)
							x = rand.Next(0, reach / 3);
						else if (biome == 2)
							x = rand.Next(reach - (reach / 2), reach);
						ret[x, y] = (Biome)biome;
					}
				}

				var tooLate = false;
				var countsOkay = false;
				var counts = new[] { 0, 1, 1, 1 };
				while (!countsOkay)
				{
					if (Environment.TickCount > time + 1000)
					{
#if DEBUG
						System.Windows.Forms.MessageBox.Show("biome timeout");
#endif
						tooLate = true;
						break;
					}
					for (var row = 0; row < reach; row++)
						for (var col = 0; col < reach; col++)
							SpreadBiome(ret, row, col, reach, rand, amounts, counts);
					if (counts[1] == amounts[1] && counts[2] == amounts[2] && counts[3] == amounts[3])
						countsOkay = true;
				}
				if (tooLate)
					continue;
				return ret;
			}
			throw new Exception("Couldn't get a biome map going.");
		}

		public void CreateTheWorld()
		{
			var setStatus = new Action<string>(s =>
			{
				var line = UIManager.Elements.Find(x => x.Tag == "worldGen");
				if (line == null)
					return;
				line.Text = s.PadRight(70);
				line.Draw();
			});

			var host = NoxicoGame.HostForm;
			this.Boards.Clear();

			var reach = 8;
			setStatus("Generating biome map...");
			var biomeMap = GenerateBiomeMap(reach);
#if DEBUG
			var colors = new[] { System.Drawing.Color.Green, System.Drawing.Color.Brown, System.Drawing.Color.Silver, System.Drawing.Color.DarkMagenta };
			var mapBitmap = new System.Drawing.Bitmap(reach * 3, reach * 3);
			for (var y = 0; y < reach; y++)
				for (var x = 0; x < reach; x++)
				{
					mapBitmap.SetPixel((y * 3) + 0, (x * 3) + 0, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 1, (x * 3) + 0, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 2, (x * 3) + 0, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 0, (x * 3) + 1, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 1, (x * 3) + 1, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 2, (x * 3) + 1, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 0, (x * 3) + 2, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 1, (x * 3) + 2, colors[(int)biomeMap[x, y]]);
					mapBitmap.SetPixel((y * 3) + 2, (x * 3) + 2, colors[(int)biomeMap[x, y]]);
				}
			//mapBitmap.Save("biomes.png", System.Drawing.Imaging.ImageFormat.Png);
#endif

			setStatus("Generating overworld...");
			Overworld = new int[reach, reach];
			OverworldBarrier = reach * reach;
			for (var y = 0; y < reach; y++)
				for (var x = 0; x < reach; x++)
					Overworld[x, y] = -1;

			for (var x = 0; x < reach; x++)
			{
				for (var y = 0; y < reach; y++)
				{
					var owBoard = Board.CreateBasicOverworldBoard(biomeMap[x, y], x, y);
					Boards.Add(owBoard);
					Overworld[x, y] = Boards.Count - 1;
				}
			}

			//TODO: place world edges

			setStatus("Placing towns...");
			var townGen = new TownGenerator();
			var townsToPlace = (int)Math.Floor(reach * 0.75); //originally, this was 6, based on a reach of 8.
			//TODO: make this more scattery. With a large reach, towns will clump together in the north now.
			while (townsToPlace > 0)
			{
				for (var x = 0; x < reach; x++)
				{
					for (var y = 0; y < reach; y++)
					{
						//setStatus("Placing towns... " + townsToPlace);
						var thisMap = Boards[Overworld[x, y]];
						var chances = new[] { 0.2, 0.02, 0, 0 };
						if (townsToPlace > 0 && Toolkit.Rand.NextDouble() < chances[(int)biomeMap[x, y]])
						{
							townGen.Board = thisMap;
							townGen.Create(biomeMap[x, y]);
							townGen.ToTilemap(ref thisMap.Tilemap);
							townGen.ToSectorMap(thisMap.Sectors);
							while (true)
							{
								var newName = Culture.GetName("human", Culture.NameType.Town);
								if (Boards.Find(b => b.Name == newName) == null)
								{
									thisMap.Name = newName;
									break;
								}
							}
							townsToPlace--;
#if DEBUG
							mapBitmap.SetPixel((y * 3) + 1, (x * 3) + 1, Color.CornflowerBlue);
#endif
						}
					}
				}
			}
			//Now, what SHOULD happen is that the player starts in one of these towns we just placed. Preferably one in the grasslands.

			//TODO: place dungeon entrances
			//TODO: excavate dungeons
#if DEBUG
			mapBitmap.Save("map.png", System.Drawing.Imaging.ImageFormat.Png);
#endif

			this.CurrentBoard = this.Boards[0];
			//NoxicoGame.HostForm.Write("The World is Ready...         ", Color.Silver, Color.Transparent, 50, 0);
			setStatus("The World is Ready.");
			//Sound.PlayMusic(this.CurrentBoard.Music);
			//this.CurrentBoard.Redraw();
		}

		public void CreatePlayerCharacter(string name, Gender gender, string bodyplan, string hairColor, string bodyColor, string eyeColor)
		{
			var pc = Character.Generate(bodyplan, gender);

			pc.IsProperNamed = true;
			if (!string.IsNullOrWhiteSpace(name))
			{
				pc.Name = new Name(name);
				if (gender == Gender.Female)
					pc.Name.Female = true;
				else if (gender == Gender.Herm || gender == Gender.Neuter)
					pc.Name.Female = Toolkit.Rand.NextDouble() > 0.5;
			}
			else
			{
				pc.Name.Culture = Culture.Cultures[pc.GetToken("culture").Tokens[0].Name];
				pc.Name.Regenerate();
			}

			if (pc.Path("skin/type").Tokens[0].Name != "slime")
				pc.Path("skin/color").Text = bodyColor;
			if (pc.Path("hair/color") != null)
				pc.Path("hair/color").Text = hairColor;
			if (pc.HasToken("eyes"))
				pc.GetToken("eyes").Text = eyeColor;

			pc.IncreaseSkill("being_awesome");

			var playerShip = new Token() { Name = Environment.UserName };
			playerShip.Tokens.Add(new Token() { Name = "player" });
			pc.GetToken("ships").Tokens.Add(playerShip);

			this.Player = new Player(pc)
			{
				XPosition = 40,
				YPosition = 12,
				ParentBoard = this.CurrentBoard,
			};
			this.CurrentBoard.Entities.Add(Player);

			/*
			this.CurrentBoard.Entities.Add(new LOSTester()
			{
				XPosition = 44,
				YPosition = 12,
				ParentBoard = this.CurrentBoard,
			});
			*/
			//SaveGame();
		}

		public static int GetOverworldIndex(Board board)
		{
			var b = HostForm.Noxico.Boards;
			for (var i = 0; i < b.Count && i < HostForm.Noxico.OverworldBarrier; i++)
				if (b[i].ID == board.ID)
					return i;
			return -1;
		}
	}
}
