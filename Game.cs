using System;
using System.Collections.Generic;
using System.Globalization;
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

		public static string WorldName { get; set; }
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
		public static Board Ocean { get; set; }
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
		public static Dictionary<string, string> BodyplanLevs { get; set; }
		public static string SavePath { get; private set; }
		public static bool InGame { get; set; }
#if CONTEXT_SENSITIVE
		public static string ContextMessage { get; set; }
		private static bool hadContextMessage;
#endif

		public static int StartingOWX = -1, StartingOWY;
		private DateTime lastUpdate;

		public NoxicoGame(MainForm hostForm)
		{
			SavePath = Vista.GetInterestingPath(Vista.SavedGames);
			if (IniFile.GetBool("misc", "vistasaves", true) && SavePath != null)
				SavePath = Path.Combine(SavePath, "Noxico"); //Add a Noxico directory to V/7's Saved Games
			else
			{
				SavePath = IniFile.GetString("misc", "savepath", @"$/Noxico"); //"saves"; //Use <startup>\saves instead
				if (SavePath.StartsWith("$"))
					SavePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + SavePath.Substring(1);
				SavePath = Path.GetFullPath(SavePath);
			}

			WorldName = RollWorldName();
			if (!Directory.Exists(SavePath))
				Directory.CreateDirectory(SavePath);

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
			Console.WriteLine("Loading items...");
			xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Items, "items.xml"));
			KnownItems = new List<InventoryItem>();
			foreach (var item in xDoc.SelectNodes("//items/item").OfType<XmlElement>())
				KnownItems.Add(InventoryItem.FromXML(item));
			Console.WriteLine("Loading bodyplans...");
			xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BodyPlans, "bodyplans.xml"));
			Views = new Dictionary<string, char>();
			var ohboy = new TokenCarrier();
			BodyplanLevs = new Dictionary<string, string>();
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
				ohboy.Tokens = Token.Tokenize(plan);
				var lev = Toolkit.GetLevenshteinString(ohboy);
				BodyplanLevs.Add(id, lev);
			}

			//Tile descriptions
			TileDescriptions = global::Noxico.Properties.Resources.TileSpecialDescriptions.Split('\n');

			Console.WriteLine("Loading books...");
			BookTitles = new List<string>();
			BookTitles.Add("[null]");
			xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Library, "books.xml"));
			var books = xDoc.SelectNodes("//book");
			foreach (var b in books.OfType<XmlElement>())
				BookTitles.Add(b.GetAttribute("title"));

			ScriptVariables.Add("consumed", 0);
			HostForm.Noxico = this;

			WorldGen.LoadBiomes();
			Ocean = Board.CreateBasicOverworldBoard(0, "Ocean", "The Ocean", "set://ocean");

#if DEBUG
			//Towngen test
			var towngenTest = Board.CreateBasicOverworldBoard(2, "TowngenTest", "Towngen Test", "set://debug");
			towngenTest.Type = BoardType.Town;
			CurrentBoard = towngenTest;
			towngenTest.DumpToHTML("ground");
			var townGen = new TownGenerator();
			townGen.Board = towngenTest;
			townGen.Culture = Culture.Cultures["human"];
			townGen.Create(WorldGen.Biomes[2]);
			townGen.ToTilemap(ref towngenTest.Tilemap);
			townGen.ToSectorMap(towngenTest.Sectors);
			towngenTest.DumpToHTML("final");
#endif

			CurrentBoard = new Board();
			this.Player = new Player()
			{
				CurrentRealm = "Nox",
			};
			if (IniFile.GetBool("misc", "skiptitle", false) && Directory.Exists(WorldName)) //File.Exists("world.bin"))
			{
				LoadGame();
				HostForm.Noxico.CurrentBoard.Draw();
				Subscreens.FirstDraw = true;
				Immediate = true;
				AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".", Color.Yellow);
				AddMessage("Remember, press F1 for help and options.");
				Mode = UserMode.Walkabout;
			}
			else
			{
				Introduction.Title();
			}
		}

		public void SaveGame(bool noPlayer = false, bool force = false)
		{
			if (!InGame && !force)
				return;

			var header = Encoding.UTF8.GetBytes("NOXiCO");

			if (!noPlayer)
			{
				Console.WriteLine("Saving player...");
				var pfile = File.Open(Path.Combine(SavePath, WorldName, "player.bin"), FileMode.Create);
				var pbin = new BinaryWriter(pfile);
				Player.SaveToFile(pbin);
				pbin.Flush();
				pfile.Flush();
				pfile.Close();
			}

			Console.WriteLine("--------------------------");
			Console.WriteLine("Saving World...");

			var realm = Path.Combine(SavePath, WorldName, Player.CurrentRealm);
			if (!Directory.Exists(realm))
				Directory.CreateDirectory(realm);

			var file = File.Open(Path.Combine(SavePath, WorldName, Player.CurrentRealm, "world.bin"), FileMode.Create);
			var bin = new BinaryWriter(file);
			bin.Write(header);

			bin.Write(Overworld.GetLength(0));
			bin.Write(Overworld.GetLength(1));
			for (var y = 0; y < Overworld.GetLength(1); y++)
				for (var x = 0; x < Overworld.GetLength(0); x++)
					bin.Write(Overworld[x, y]);


			bin.Write(CurrentBoard.BoardNum);
			bin.Write(Boards.Count);
			//foreach (var b in Boards)
			//	b.SaveToFile(bin);
			for (var i = 0; i < Boards.Count; i++)
			{
				if (Boards[i] != null)
				{
					Boards[i].SaveToFile(i);
					Boards[i] = null;
				}
			}
			CurrentBoard.SaveToFile(CurrentBoard.BoardNum);

			bin.Write(StartingOWX);
			bin.Write(StartingOWY);

			bin.Flush();

			file.Flush();
			file.Close();
			Console.WriteLine("Done.");
			Console.WriteLine("--------------------------");
		}

		public void LoadGame()
		{
			var playerFile = Path.Combine(SavePath, WorldName, "player.bin");
			if (File.Exists(playerFile))
			{
				var pfile = File.Open(playerFile, FileMode.Open);
				var pbin = new BinaryReader(pfile);
				Player = Player.LoadFromFile(pbin);
				Player.AdjustView();
				pfile.Close();
			}

			var realm = Path.Combine(SavePath, WorldName, Player.CurrentRealm);

			var file = File.Open(Path.Combine(realm, "world.bin"), FileMode.Open);
			var bin = new BinaryReader(file);
			var header = bin.ReadBytes(6);
			if (Encoding.UTF8.GetString(header) != "NOXiCO")
			{
				MessageBox.Message("Invalid world header.");
				return;
			}

			var owX = bin.ReadInt32();
			var owY = bin.ReadInt32();
			Overworld = new int[owX, owY];
			OverworldBarrier = owX * owY;
			for (var y = 0; y < owY; y++)
			{
				for (var x = 0; x < owX; x++)
				{
					Overworld[x, y] = bin.ReadInt32();
				}
			}

			var currentIndex = bin.ReadInt32();
			var boardCount = bin.ReadInt32();
			Boards = new List<Board>(boardCount);
			for (int i = 0; i < boardCount; i++)
				Boards.Add(null);
			//for (int i = 0; i < boardCount; i++)
			//	Boards.Add(Board.LoadFromFile(bin));

			StartingOWX = bin.ReadInt32();
			StartingOWY = bin.ReadInt32();

			file.Close();

			InGame = true;

			if (File.Exists(playerFile))
			{
				GetBoard(currentIndex);

				CurrentBoard = Boards[currentIndex];
				CurrentBoard.Entities.Add(Player);
				Player.ParentBoard = CurrentBoard;
				CurrentBoard.Redraw();
				Sound.PlayMusic(CurrentBoard.Music);

				if (!Player.Character.HasToken("player"))
					Player.Character.Tokens.Add(new Token() { Name = "player", Value = (int)DateTime.Now.Ticks });
				SaveGame();

				Achievements.StartingTime = DateTime.Now;
			}
		}

		public Board GetBoard(int index)
		{
			if (index == -1 || index >= Boards.Count)
				return NoxicoGame.Ocean;

			if (Boards[index] == null)
			{
				Console.WriteLine("Requested board #{0}. Loading...", index);
				Boards[index] = Board.LoadFromFile(index);
				Boards[index].BoardNum = index;
			}
			return Boards[index];
		}

		public static void DrawMessages()
		{
#if CONTEXT_SENSITIVE
			if (!string.IsNullOrWhiteSpace(ContextMessage))
			{
				hadContextMessage = true;
				HostForm.Write(' ' + ContextMessage + ' ', Color.Silver, Color.Black, 80 - ContextMessage.Length - 2, 0);
			}
			else if (hadContextMessage)
			{
				HostForm.Noxico.CurrentBoard.Redraw();
				hadContextMessage = false;
			}
#endif
		
			if (Messages.Count == 0)
				return;
			var row = 24;
			for (var i = 0; i < 4 && i < Messages.Count; i++)
			{
				var m = Messages.Count - 1 - i;
				var c = Messages[m].Color;
				if (c.GetBrightness() < 0.2)
					c = Toolkit.Lerp(c, Color.White, 0.5);
				HostForm.Write(' ' + Messages[m].Message + ' ', c, Color.Black, 0, row);
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

		public void FlushDungeons()
		{
			Console.WriteLine("Flushing dungeons...");
			for (var i = 0; i < Boards.Count; i++)
			{
				var board = Boards[i];
				if (board == null)
					continue;
				if (board == CurrentBoard)
					continue; //Shouldn't have to happen.
				if (board.GetToken("type").Value == (float)BoardType.Dungeon)
					board.Flush();
			}
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
							Sound.PlaySound("Open Gate");
							AutoRestTimer = AutoRestSpeed;
							KeyMap[(int)Keys.OemPeriod] = true;
						}
						CurrentBoard.Update();

						for (var i = 0; i < Boards.Count; i++)
						{
							var board = Boards[i];
							if (board == null)
								continue;
							if (board.HasToken("dungeon"))
								continue; //Don't autoflush dungeons. Use FlushDungeons() for that.
							board.Lifetime++;
							if (board.Lifetime == 1000)
								board.Flush();
						}
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

		public void CreateRealm(string realm = "Nox")
		{
			Console.WriteLine("Creating realm \"{0}\"...", realm);

			var setStatus = new Action<string>(s =>
			{
				var line = UIManager.Elements.Find(x => x.Tag == "worldGen");
				if (line == null)
					return;
				line.Text = s.PadRight(70);
				line.Draw();
				Console.WriteLine(s);
			});

			var host = NoxicoGame.HostForm;
			this.Boards.Clear();

			setStatus("Generating world map...");
			var worldGen = new WorldGen();
			worldGen.Generate(setStatus, "pandora");

			setStatus("Generating boards...");
			Overworld = new int[worldGen.MapSizeX, worldGen.MapSizeY];
			for (var y = 0; y < worldGen.MapSizeY - 1; y++)
				for (var x = 0; x < worldGen.MapSizeX - 1; x++)
					Overworld[x, y] = -1;
			OverworldBarrier = worldGen.MapSizeX * worldGen.MapSizeY;
			for (var y = 0; y < worldGen.MapSizeY - 1; y++)
			{
				for (var x = 0; x < worldGen.MapSizeX - 1; x++)
				{
					//if (WorldGen.Biomes[worldGen.BiomeMap[y, x]].IsWater)
					if (worldGen.BiomeMap[y, x] == 0)
					{
						Boards.Add(null);
						continue;
					}
					var newBoard = Board.CreateFromBitmap(worldGen.BiomeBitmap, worldGen.BiomeMap[y,x], x, y);
					this.Boards.Add(newBoard);
					Overworld[x, y] = Boards.Count - 1;
				}
			}

			setStatus("Placing towns...");
			var townGen = new TownGenerator();
			for (int y = 0; y < worldGen.MapSizeY - 1; y++)
			{
				for (int x = 0; x < worldGen.MapSizeX - 1; x++)
				{
					if (worldGen.TownMap[y, x] > 0)
					{
						if (Overworld[x, y] == 1)
							continue;
						if (StartingOWX == -1)
						{
							StartingOWX = x;
							StartingOWY = y;
						}

						var thisMap = Boards[Overworld[x, y]];
						thisMap.Type = BoardType.Town;
						townGen.Board = thisMap;
						var biome = WorldGen.Biomes[worldGen.BiomeMap[y, x]];
						var cultureName = biome.Cultures[Toolkit.Rand.Next(biome.Cultures.Length)];
						townGen.Culture = Culture.Cultures[cultureName]; //Culture.Cultures["human"];
						townGen.Create(biome);
						townGen.ToTilemap(ref thisMap.Tilemap);
						townGen.ToSectorMap(thisMap.Sectors);
						thisMap.GetToken("music").Text = "set://Town";
						thisMap.Tokens.Add(new Token() { Name = "culture", Text = cultureName });
						while (true)
						{
							var newName = Culture.GetName(townGen.Culture, Culture.NameType.Town);
							if (Boards.Find(b => b != null && b.Name == newName) == null)
							{
								thisMap.Name = newName;
								break;
							}
						}
					}
				}
			}

			var dungeonEntrances = 0;
			while (dungeonEntrances < 25)
			{
				setStatus("Placing dungeon entrances... (" + dungeonEntrances + ")");
				for (int y = 0; y < worldGen.MapSizeY - 1; y++)
				{
					for (int x = 0; x < worldGen.MapSizeX - 1; x++)
					{
						//Don't place one in oceans
						if (worldGen.BiomeMap[y, x] == 0)
							continue;
						//Don't place one in towns either
						if (worldGen.TownMap[y, x] > 0)
							continue;

						//Don't always place one, to prevent north-west clumping
						if (Toolkit.Rand.NextDouble() > 0.4)
							continue;

						//And don't place one where there already is one.
						if (worldGen.TownMap[y, x] == -2)
							continue;

						var thisMap = Boards[Overworld[x, y]];
						var eX = Toolkit.Rand.Next(2, 78);
						var eY = Toolkit.Rand.Next(1, 23);

						if (thisMap.IsSolid(eY, eX))
							continue;
						var sides = 0;
						if (thisMap.IsSolid(eY - 1, eX))
							sides++;
						if (thisMap.IsSolid(eY + 1, eX))
							sides++;
						if (thisMap.IsSolid(eY, eX - 1))
							sides++;
						if (thisMap.IsSolid(eY, eX + 1))
							sides++;
						if (sides > 3)
							continue;
						worldGen.TownMap[y, x] = -2;

						var newWarp = new Warp()
						{
							TargetBoard = -1, //mark as ungenerated dungeon
							ID = thisMap.ID + "_Dungeon",
							XPosition = eX,
							YPosition = eY,
						};
						thisMap.Warps.Add(newWarp);
						thisMap.SetTile(eY, eX, '>', Color.Silver, Color.Black);

						dungeonEntrances++;
					}
				}
			}

			setStatus("Saving chunks... (lol)");
			for (var i = 0; i < this.Boards.Count; i++)
			{
				if (this.Boards[i] == null)
					continue;
				this.Boards[i].SaveToFile(i);
				if (i > 0)
					this.Boards[i] = null;
			}
			SaveGame(true, true);

			//this.CurrentBoard = GetBoard(townID); //this.Boards[townID];
			//NoxicoGame.HostForm.Write("The World is Ready...         ", Color.Silver, Color.Transparent, 50, 0);
			setStatus("The World is Ready.");
			//Sound.PlayMusic(this.CurrentBoard.Music);
			//this.CurrentBoard.Redraw();
		}

		public void CreatePlayerCharacter(string name, Gender gender, string bodyplan, string hairColor, string bodyColor, string eyeColor, string BonusTrait)
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
			
				if (pc.Name.Surname.StartsWith("#patronym"))
				{
					var parentName = new Name() { Culture = pc.Name.Culture };
					if (gender == Gender.Female)
						pc.Name.Female = true;
					parentName.Regenerate();
					pc.Name.ResolvePatronym(parentName, parentName);
				}
			}

			if (pc.Path("skin/type").Tokens[0].Name != "slime")
				pc.Path("skin/color").Text = bodyColor;
			if (pc.Path("hair/color") != null)
				pc.Path("hair/color").Text = hairColor;
			if (pc.HasToken("eyes"))
				pc.GetToken("eyes").Text = eyeColor;

			pc.Tokens.Add(new Token() { Name = "player", Value = (int)DateTime.Now.Ticks });

			var playerShip = new Token() { Name = Environment.UserName };
			playerShip.Tokens.Add(new Token() { Name = "player" });
			pc.GetToken("ships").Tokens.Add(playerShip);
			
			var traitsDoc = new XmlDocument();
			traitsDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BonusTraits, "bonustraits.xml"));
			var trait = traitsDoc.SelectSingleNode("//trait[@name=\"" + BonusTrait + "\"]");
			if (trait != null)
			{
				foreach (var bonus in trait.ChildNodes.OfType<XmlElement>())
				{
					switch (bonus.Name)
					{
						case "stat":
							var increase = 20;
							var percent = true;
							if (bonus.HasAttribute("value"))
							{
								var x = bonus.GetAttribute("value");
								if (x.EndsWith("%"))
								{
									percent = true;
									x = x.Remove(x.Length - 1);
								}
								else
									percent = false;
								increase = int.Parse(x);
							}
							var stat = pc.GetToken(bonus.GetAttribute("id"));
							var oldVal = stat.Value;
							var newVal = oldVal + increase;
							if (percent)
								newVal = oldVal + ((increase / 100.0f) * oldVal);
							stat.Value = newVal;
							break;
						case "skill":
							var skill = bonus.GetAttribute("name").Replace(' ', '_').ToLowerInvariant();
							var by = bonus.HasAttribute("level") ? double.Parse(bonus.GetAttribute("level"), NumberStyles.Float, CultureInfo.InvariantCulture) : 1.0;
							pc.IncreaseSkill(skill);
							break;
						case "rating":
							//TODO: implement the rating bonus trait effect.
							break;
						case "givetoken":
							//TODO: implement the givetoken bonus trait effect.
							break;
					}
				}
			}

			this.CurrentBoard = GetBoard(Overworld[StartingOWX, StartingOWY]);
			this.Player = new Player(pc)
			{
				XPosition = 40,
				YPosition = 12,
				ParentBoard = this.CurrentBoard,
				OverworldX = StartingOWX,
				OverworldY = StartingOWY,
				OnOverworld = true,
				CurrentRealm = "Nox",
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
			InGame = true;
			SaveGame();
		}

		public static int GetOverworldIndex(Board board)
		{
			var n = HostForm.Noxico;
			for (var i = 0; i < n.Boards.Count && i < HostForm.Noxico.OverworldBarrier; i++)
				if (n.GetBoard(i).ID == board.ID)
					return i;
			return -1;
		}

		//TODO: have the game ask for a name through UITextBox. Use this for when 
		public string RollWorldName()
		{
			//var x = new[] { "The Magnificent", "Under", "The Hungry", "The Realm of", "Over", "The Isle of", "The Kingdom of" };
			//var y = new[] { "Boundary", "Earth", "Marrow", "Picking", "Farnsworth", Environment.UserName, "Kipperlings" };
			//var ret = Toolkit.PickOne(x) + ' ' + Toolkit.PickOne(y);
			//return ret;
			var x = Toolkit.ResOrFile(global::Noxico.Properties.Resources.Homestuck, "Homestuck.txt").Split(new[] { '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries);
			var a = Toolkit.PickOne(x);
			var b = Toolkit.PickOne(x);
			while(b == a)
				b = Toolkit.PickOne(x);
			return "Land of " + a + " and " + b;
		}
	}
}
