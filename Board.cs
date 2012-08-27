
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml;

namespace Noxico
{
	/// <summary>
	/// Determines which subscreen to run when UserMode is set to Subscreen.
	/// </summary>
	public delegate void SubscreenFunc();

	/// <summary>
	/// The poor man's System.Drawing.Rectangle.
	/// </summary>
	public struct Rectangle
	{
		public int Left, Top, Right, Bottom;
		public Location GetCenter()
		{
			return new Location(Left + ((Right - Left) / 2), Top + ((Bottom - Top) / 2));
		}
	}

	/// <summary>
	/// The poor man's System.Drawing.Point, but with extras.
	/// </summary>
	public struct Point
	{
		public int X, Y;
		public Point(int x, int y)
		{
			X = x;
			Y = y;
		}
		public static bool operator ==(Point l, Point r)
		{
			return l.X == r.X && l.Y == r.Y;
		}
		public static bool operator !=(Point l, Point r)
		{
			return !(l == r);
		}
		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is Point))
				return false;
			var opt = (Point)obj;
			return opt.X == X && opt.Y == Y;
		}
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}
	}

	/// <summary>
	/// The current operation state of the game.
	/// </summary>
	public enum UserMode
	{
		Walkabout, LookAt, Subscreen
	}

	/// <summary>
	/// A special description for board tiles.
	/// </summary>
	public struct TileDescription
	{
		public string Name;
		public Color Color;
		public string Description;
	}

	/// <summary>
	/// A single tile on a board.
	/// </summary>
	public class Tile
	{
		public char Character { get; set; }
		public Color Foreground { get; set; }
		public Color Background { get; set; }
		public bool Solid { get; set; }
		public bool CanBurn { get; set; }
		public bool IsWater { get; set; }
		public int BurnTimer { get; set; }
		public bool HasExTile { get; set; }
		public bool CanFlyOver { get; set; }
		public int SpecialDescription { get; set; }

		/// <summary>
		/// Returns a TileDescription if this tile has one.
		/// </summary>
		/// <returns></returns>
		public TileDescription? GetSpecialDescription()
		{
			if (SpecialDescription == 0)
				return null;
			if (SpecialDescription > NoxicoGame.TileDescriptions.Length)
				return null;
			var tsd = NoxicoGame.TileDescriptions[SpecialDescription];
			var name = tsd.Substring(0, tsd.IndexOf(':'));
			var desc = tsd.Substring(tsd.IndexOf(':') + 1).Trim();
			return new TileDescription() { Name = name, Description = desc, Color = Foreground.GetBrightness() < 0.5 ? Background : Foreground  };
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Character);
			//stream.Write((byte)((Background * 16) + (Foreground % 16)));
			Foreground.SaveToFile(stream);
			Background.SaveToFile(stream);

			var bits = new BitVector32();
			bits[1] = CanBurn;
			bits[2] = Solid;
			bits[4] = CanFlyOver;
			bits[8] = IsWater;
			bits[16] = BurnTimer > 0;
			bits[32] = false; //reserved
			bits[64] = SpecialDescription > 0; //was HasExTile
			bits[128] = false; //reserved for "has more settings"
			//stream.Write((byte)((HasExTile ? 8 : 0) | (BurnTimer > 0 ? 8 : 0) | (CanFlyOver ? 4 : 0) | (Solid ? 2 : 0) | (CanBurn ? 1 : 0)));
			stream.Write((byte)bits.Data);
			if(BurnTimer > 0)
				stream.Write((byte)BurnTimer);
			if (SpecialDescription > 0)
				stream.Write((Int16)SpecialDescription);
		}

		public void LoadFromFile(BinaryReader stream)
		{
			Character = stream.ReadChar();
			//var col = stream.ReadByte();
			//Foreground = col % 16;
			//Background = col / 16;
			Foreground = Toolkit.LoadColorFromFile(stream);
			Background = Toolkit.LoadColorFromFile(stream);
			var set = stream.ReadByte();
			var bits = new BitVector32(set);
			CanBurn = bits[1];
			Solid = bits[2];
			CanFlyOver = bits[4];
			IsWater = bits[8];
			var HasBurn = bits[16];
			//HasReversed = bits[32];
			var HasSpecialDescription = bits[64];
			//HasMoreSettings = bits[128];
			if (HasBurn)
				BurnTimer = stream.ReadByte();
			if (HasSpecialDescription)
				SpecialDescription = stream.ReadInt16();
		}
	}

	/// <summary>
	/// An exit of some sort. Activate them by standing on them and pressing Enter.
	/// </summary>
	public class Warp
	{
		public static int GeneratorCount = 0;

		public string ID { get; set; }
		public int XPosition { get; set; }
		public int YPosition { get; set; }
		public int TargetBoard { get; set; }
		public string TargetWarpID { get; set; }

		public Warp()
		{
			ID = GeneratorCount.ToString();
			GeneratorCount++;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(ID);
			stream.Write((byte)XPosition);
			stream.Write((byte)YPosition);
			stream.Write(TargetBoard);
			stream.Write(TargetWarpID ?? "");
		}

		public static Warp LoadFromFile(BinaryReader stream)
		{
			var newWarp = new Warp();
			newWarp.ID = stream.ReadString();
			newWarp.XPosition = stream.ReadByte();
			newWarp.YPosition = stream.ReadByte();
			newWarp.TargetBoard = stream.ReadInt32();
			newWarp.TargetWarpID = stream.ReadString();
			return newWarp;
		}
	}

	public class StatusMessage
	{
		public string Message { get; set; }
		public Color Color { get; set; }
		public bool New { get; set; }
	}

	public enum BoardType
	{
		Wild, Town, Dungeon, Special
	}

	public class Board : TokenCarrier
	{
		public static int GeneratorCount = 0;

		public int BoardNum { get; set; }

		public int Lifetime { get; set; }
		public string Name { get { return GetToken("name").Text; } set { GetToken("name").Text = value; } }
		public string ID { get { return GetToken("id").Text; } set { GetToken("id").Text = value; } }
		public string Music { get { return GetToken("music").Text; } set { GetToken("music").Text = value; } }
		public BoardType Type { get { return (BoardType)GetToken("type").Value; } set { GetToken("type").Value = (float)value; } }
		public List<Entity> Entities { get; set; }
		public List<Warp> Warps { get; set; }
		public List<Location> DirtySpots { get; set; }
		public List<Entity> EntitiesToRemove { get; set; }
		public List<Entity> EntitiesToAdd { get; set; }

		public Tile[,] Tilemap = new Tile[80, 25];

		public Dictionary<string, Rectangle> Sectors { get; set; }
		public List<Location> ExitPossibilities { get; set; }

		public override string ToString()
		{
			return string.Format("{0} - \"{1}\"", ID, Name);
		}

		public Board()
		{
			this.Tokens = new List<Token>();
			foreach (var t in new[] { "name", "id", "music", "type", "biome", "encounters" })
				this.Tokens.Add(new Token() { Name = t });
			this.Entities = new List<Entity>();
			this.EntitiesToRemove = new List<Entity>();
			this.EntitiesToAdd = new List<Entity>();
			this.Warps = new List<Warp>();
			this.Sectors = new Dictionary<string, Rectangle>();
			this.DirtySpots = new List<Location>();
			for (int row = 0; row < 25; row++)
				for (int col = 0; col < 80; col++)
					this.Tilemap[col, row] = new Tile();
		}

		public void Flush()
		{
			Console.WriteLine("Flushing board {0}.", ID);
			var me = NoxicoGame.HostForm.Noxico.Boards.FindIndex(x => x == this);
			CleanUpSlimeTrails();
			SaveToFile(me);
			NoxicoGame.HostForm.Noxico.Boards[me] = null;
		}

		public void SaveToFile(int index)
		{
			var realm = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, NoxicoGame.HostForm.Noxico.Player.CurrentRealm);
			if (!Directory.Exists(realm))
				Directory.CreateDirectory(realm);
			//Console.WriteLine(" * Saving board {0}...", Name);
			using (var stream = new BinaryWriter(File.Open(System.IO.Path.Combine(realm, "Board" + index + ".brd"), FileMode.Create)))
			{
				//stream.Write(Name);
				//stream.Write(ID);
				//stream.Write(Music);
				stream.Write(Tokens.Count);
				Tokens.ForEach(x => x.SaveToFile(stream));				

				stream.Write(Sectors.Count);
				//stream.Write(Entities.OfType<FloorBot>().Count());
				stream.Write(Entities.OfType<BoardChar>().Count() - Entities.OfType<Player>().Count());
				stream.Write(Entities.OfType<DroppedItem>().Count());
				stream.Write(Entities.OfType<Clutter>().Count() - Entities.OfType<Clutter>().Where(c => c.Life > 0).Count());
				stream.Write(Entities.OfType<Container>().Count());
				stream.Write(Warps.Count);

				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						Tilemap[col, row].SaveToFile(stream);

				foreach (var sector in Sectors)
				{
					//TODO: give sectors their own serialization function. For readability.
					stream.Write(sector.Key);
					stream.Write(sector.Value.Left);
					stream.Write(sector.Value.Top);
					stream.Write(sector.Value.Right);
					stream.Write(sector.Value.Bottom);
				}

				//foreach (var e in Entities.OfType<FloorBot>())
				//	e.SaveToFile(stream);
				foreach (var e in Entities.OfType<BoardChar>())
					if (e is Player)
						continue;
					else
						e.SaveToFile(stream);
				foreach (var e in Entities.OfType<DroppedItem>())
					e.SaveToFile(stream);
				foreach (var e in Entities.OfType<Clutter>())
					if (e.Life > 0)
						continue;
					else
						e.SaveToFile(stream);
				foreach (var e in Entities.OfType<Container>())
					e.SaveToFile(stream);

				Warps.ForEach(x => x.SaveToFile(stream));
			}
		}

		public static Board LoadFromFile(int index)
		{
			var realm = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, NoxicoGame.HostForm.Noxico.Player.CurrentRealm);
			var file = System.IO.Path.Combine(realm, "Board" + index + ".brd");
			if (!File.Exists(file))
				throw new FileNotFoundException("Board #" + index + " not found!");
			var newBoard = new Board();
			using (var stream = new BinaryReader(File.Open(file, FileMode.Open)))
			{
				//newBoard.Name = stream.ReadString();
				//newBoard.ID = stream.ReadString();
				//newBoard.Music = stream.ReadString();
				var numTokens = stream.ReadInt32();
				newBoard.Tokens.Clear();
				for (var i = 0; i < numTokens; i++)
					newBoard.Tokens.Add(Token.LoadFromFile(stream));
				newBoard.Name = newBoard.GetToken("name").Text;
				newBoard.ID = newBoard.GetToken("id").Text;
				newBoard.Music = newBoard.GetToken("music").Text;
				newBoard.Type = (BoardType)newBoard.GetToken("type").Value;

				var secCt = stream.ReadInt32();
				//var botCt = stream.ReadInt32();
				var chrCt = stream.ReadInt32();
				var drpCt = stream.ReadInt32();
				var cltCt = stream.ReadInt32();
				var conCt = stream.ReadInt32();
				var wrpCt = stream.ReadInt32();

				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						newBoard.Tilemap[col, row].LoadFromFile(stream);

				for (int i = 0; i < secCt; i++)
				{
					var secName = stream.ReadString();
					var l = stream.ReadInt32();
					var t = stream.ReadInt32();
					var r = stream.ReadInt32();
					var b = stream.ReadInt32();
					newBoard.Sectors.Add(secName, new Rectangle() { Left = l, Top = t, Right = r, Bottom = b });
				}

				//Unlike in SaveToFile, there's no need to worry about the player because that one's handled on the world level.
				for (int i = 0; i < chrCt; i++)
					newBoard.Entities.Add(BoardChar.LoadFromFile(stream));
				for (int i = 0; i < drpCt; i++)
					newBoard.Entities.Add(DroppedItem.LoadFromFile(stream));
				for (int i = 0; i < cltCt; i++)
					newBoard.Entities.Add(Clutter.LoadFromFile(stream));
				for (int i = 0; i < conCt; i++)
					newBoard.Entities.Add(Container.LoadFromFile(stream));

				for (int i = 0; i < wrpCt; i++)
					newBoard.Warps.Add(Warp.LoadFromFile(stream));

				newBoard.RespawnEncounters();
				newBoard.CleanUpSlimeTrails();

				newBoard.BindEntities();

				//Console.WriteLine(" * Loaded board {0}...", newBoard.Name);
			}
			return newBoard;
		}

		[Obsolete("Don't use until the Home Base system is in. Other than that, cannibalize away me hearties." , true)]
		public static Board Load(string id)
		{
			var xDoc = new XmlDocument();
			xDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Boards, "boards.xml"));
			var source = xDoc.SelectSingleNode("//board[@id=\"" + id + "\"]") as XmlElement;
			if (source == null)
				throw new Exception("No such board.");

			var newBoard = new Board();
			newBoard.Name = id;
			newBoard.Music = source.GetAttribute("music");
			NoxicoGame.HostForm.Text = "Noxico - Loading...";

			#region Base
			if (source.HasAttribute("base"))
			{
				if (source.GetAttribute("base") == "grass")
				{
					var grasses = new[] { ',', '\'', '`', '.', };
					for (int row = 0; row < 25; row++)
					{
						for (int col = 0; col < 80; col++)
						{
							newBoard.Tilemap[col, row] = new Tile()
							{
								Character = grasses[Toolkit.Rand.Next(grasses.Length)],
								Background = Color.Black,
								Foreground = Color.Green,
								CanBurn = true
							};
						}
					}
				}
				else if (source.GetAttribute("base") == "inside")
				{
					for (int row = 0; row < 25; row++)
						for (int col = 0; col < 80; col++)
							newBoard.Tilemap[col, row] = new Tile()
							{
								Character = (char)0xB1,
								Background = Color.Black,
								Foreground = Color.Gray,
								CanBurn = false,
								Solid = true,
							};
				}
			}
			#endregion

			#region Buildings
			var buildings = source.SelectNodes("building");
			foreach (var b in buildings.OfType<XmlElement>())
			{
				var x1 = int.Parse(b.GetAttribute("left"));
				var y1 = int.Parse(b.GetAttribute("top"));
				var x2 = int.Parse(b.GetAttribute("right"));
				var y2 = int.Parse(b.GetAttribute("bottom"));

				if (b.HasAttribute("id"))
				{
					var sid = b.GetAttribute("id");
					if (!newBoard.Sectors.ContainsKey(sid))
						newBoard.Sectors.Add(sid, new Rectangle() { Left = x1, Top = y1, Bottom = x2, Right = y2 });
				}

				for (int row = y1; row <= y2; row++)
				{
					for (int col = x1; col <= x2; col++)
					{
						newBoard.Tilemap[col, row] = new Tile()
						{
							Character = ' ',
							Background = Color.Black,
							Foreground = Color.Brown,
							CanBurn = false
						};
					}
				}

				//Left and right walls
				for (int row = y1 + 1; row < y2; row++)
				{
					newBoard.Tilemap[x1, row].Character = (char)0xBA;
					newBoard.Tilemap[x1, row].Solid = true;
					newBoard.Tilemap[x1, row].CanBurn = true;

					newBoard.Tilemap[x2, row].Character = (char)0xBA;
					newBoard.Tilemap[x2, row].Solid = true;
					newBoard.Tilemap[x2, row].CanBurn = true;
				}

				//Top and bottom walls
				for (int col = x1 + 1; col < x2; col++)
				{
					newBoard.Tilemap[col, y1].Character = (char)0xCD;
					newBoard.Tilemap[col, y1].Solid = true;
					newBoard.Tilemap[col, y1].CanBurn = true;

					newBoard.Tilemap[col, y2].Character = (char)0xCD;
					newBoard.Tilemap[col, y2].Solid = true;
					newBoard.Tilemap[col, y2].CanBurn = true;
				}

				//Top left corner
				newBoard.Tilemap[x1, y1].Character = (char)0xC9;
				newBoard.Tilemap[x1, y1].Solid = true;
				newBoard.Tilemap[x1, y1].CanBurn = true;

				//Top right corner
				newBoard.Tilemap[x2, y1].Character = (char)0xBB;
				newBoard.Tilemap[x2, y1].Solid = true;
				newBoard.Tilemap[x2, y1].CanBurn = true;

				//Bottom left corner
				newBoard.Tilemap[x1, y2].Character = (char)0xC8;
				newBoard.Tilemap[x1, y2].Solid = true;
				newBoard.Tilemap[x1, y2].CanBurn = true;

				//Bottom right corner
				newBoard.Tilemap[x2, y2].Character = (char)0xBC;
				newBoard.Tilemap[x2, y2].Solid = true;
				newBoard.Tilemap[x2, y2].CanBurn = true;

				var exits = b.SelectNodes("exit");
				foreach (var e in exits.OfType<XmlElement>())
				{
					var eH = false;
					var eX = 0;
					var eY = 0;
					switch (e.GetAttribute("direction"))
					{
						case "east":
							eX = x2;
							eY = y1 + (y2 - y1) / 2;
							break;
						case "west":
							eX = x1;
							eY = y1 + (y2 - y1) / 2;
							break;
						case "north":
							eH = true;
							eX = x1 + (x2 - x1) / 2;
							eY = y1;
							break;
						case "south":
							eH = true;
							eX = x1 + (x2 - x1) / 2;
							eY = y2;
							break;
					}

					if (eH)
					{
						newBoard.Tilemap[eX - 1, eY].Character = (char)0xB5;
						newBoard.Tilemap[eX + 1, eY].Character = (char)0xC6;
						newBoard.Tilemap[eX, eY].Character = ' ';
						newBoard.Tilemap[eX, eY].Solid = false;
					}
					else
					{
						newBoard.Tilemap[eX, eY - 1].Character = (char)0xD0;
						newBoard.Tilemap[eX, eY + 1].Character = (char)0xD2;
						newBoard.Tilemap[eX, eY].Character = ' ';
						newBoard.Tilemap[eX, eY].Solid = false;
					}
				}
			}

			//Fix wall pieces
			var wallPieces = "\xBA\xCE\xCD\xC9\xCB\xBB\xB9\xBC\xCA\xC8\xCC";
			for (var row = 1; row < 24; row++)
			{
				for (var col = 1; col < 24; col++)
				{
					if (newBoard.Tilemap[col, row].Foreground == Color.Brown && wallPieces.Contains(newBoard.Tilemap[col, row].Character))
					{
						if (newBoard.Tilemap[col, row].Character == '\xC9' && newBoard.Tilemap[col, row - 1].Character == '\xBA') //╔ ║ > ╠
							newBoard.Tilemap[col, row].Character = '\xCC';
						else if (newBoard.Tilemap[col, row].Character == '\xBC' && newBoard.Tilemap[col + 1, row].Character == '\xCD') //╝ ═ > ╩
							newBoard.Tilemap[col, row].Character = '\xCA';
						else if (newBoard.Tilemap[col, row].Character == '\xBB' && newBoard.Tilemap[col + 1, row].Character == '\xCD') //╗ ═ > ╦
							newBoard.Tilemap[col, row].Character = '\xCB';
						else if (newBoard.Tilemap[col, row].Character == '\xBB' && newBoard.Tilemap[col, row - 1].Character == '\xD2') //╗ ╥ > ╣
							newBoard.Tilemap[col, row].Character = '\xB9';
					}
				}
			}
			#endregion

			#region Patches
			var patches = source.SelectNodes("patch");
			foreach (var p in patches.OfType<XmlElement>())
			{
				var x1 = int.Parse(p.GetAttribute("left"));
				var y1 = int.Parse(p.GetAttribute("top"));
				var x2 = x1;
				var y2 = y1;
				if (p.HasAttribute("right"))
					x2 = int.Parse(p.GetAttribute("right"));
				if (p.HasAttribute("bottom"))
					y2 = int.Parse(p.GetAttribute("bottom"));
				if (p.HasAttribute("id"))
				{
					var sid = p.GetAttribute("id");
					if (!newBoard.Sectors.ContainsKey(sid))
						newBoard.Sectors.Add(sid, new Rectangle() { Left = x1, Top = y1, Right = x2, Bottom = y2 });
				}
				var ch = ' ';
				var fg = Color.Silver;
				var bg = Color.Black;
				try
				{
					ch = (char)int.Parse(p.GetAttribute("character"));
				}
				catch
				{
					ch = p.GetAttribute("character")[0];
				}
				try
				{
					fg = Toolkit.GetColor(p.GetAttribute("forecolor")); //int.Parse(p.GetAttribute("forecolor"));
					bg = Toolkit.GetColor(p.GetAttribute("backcolor")); //int.Parse(p.GetAttribute("backcolor"));
				}
				catch
				{ }
				var bu = p.GetAttribute("burns") == "true";
				var so = p.GetAttribute("solid") == "true";
				for (int row = y1; row <= y2; row++)
				{
					for (int col = x1; col <= x2; col++)
					{
						newBoard.Tilemap[col, row] = new Tile()
						{
							Character = ch,
							Background = bg,
							Foreground = fg,
							CanBurn = bu,
							Solid = so,
						};
					}
				}
			}
			#endregion

			#region Structures
			var structs = source.SelectNodes("structure");
			foreach (var s in structs.OfType<XmlElement>())
			{
				var x = int.Parse(s.GetAttribute("left"));
				var y = int.Parse(s.GetAttribute("top"));
				var w = int.Parse(s.GetAttribute("width"));
				var h = int.Parse(s.GetAttribute("height"));
				var x1 = x;
				var y1 = y;
				if (s.HasAttribute("id"))
				{
					var sid = s.GetAttribute("id");
					if (!newBoard.Sectors.ContainsKey(sid))
						newBoard.Sectors.Add(sid, new Rectangle() { Left = x, Top = y, Right = x + w, Bottom = y + h });
				}
				foreach (var c in s.ChildNodes.OfType<XmlElement>())
				{
					if (c.Name != "tile")
						continue;

					var ch = ' ';
					var fg = Color.Silver;
					var bg = Color.Black;
					try
					{
						ch = (char)int.Parse(c.GetAttribute("character"));
					}
					catch
					{
						ch = c.GetAttribute("character")[0];
					}
					try
					{
						fg = Toolkit.GetColor(c.GetAttribute("forecolor")); //int.Parse(c.GetAttribute("forecolor"));
						bg = Toolkit.GetColor(c.GetAttribute("backcolor")); //int.Parse(c.GetAttribute("backcolor"));
					}
					catch
					{ }
					var bu = c.GetAttribute("burns") == "true";
					var so = c.GetAttribute("solid") == "true";
					newBoard.Tilemap[x1, y1] = new Tile()
					{
						Character = ch,
						Background = bg,
						Foreground = fg,
						CanBurn = bu,
						Solid = so,
					};
					x1++;
					if (x1 == x + w)
					{
						x1 = x;
						y1++;
					}
				}
			}
			#endregion

			#region Floorbots
			/*
			var floorbots = source.SelectNodes("floorbot");
			foreach (var f in floorbots.OfType<XmlElement>())
			{
				var x = int.Parse(f.GetAttribute("left"));
				var y = int.Parse(f.GetAttribute("top"));
				var ch = ' ';
				var fg = 7;
				var bg = 0;
				try
				{
					ch = (char)int.Parse(f.GetAttribute("character"));
				}
				catch
				{
					ch = f.GetAttribute("character")[0];
				}
				try
				{
					fg = int.Parse(f.GetAttribute("forecolor"));
					bg = int.Parse(f.GetAttribute("backcolor"));
				}
				catch
				{ }
				var script = f.InnerText.Trim();
				var floorbot = new FloorBot() { XPosition = x, YPosition = y, AsciiChar = ch, ForegroundColor = fg, BackgroundColor = bg };
				floorbot.LoadScript(script);
				newBoard.Entities.Add(floorbot);
			}
			*/
			#endregion

			#region Clutter
			var clutter = source.SelectNodes("clutter");
			foreach (var d in clutter.OfType<XmlElement>())
			{
				var x = int.Parse(d.GetAttribute("left"));
				var y = int.Parse(d.GetAttribute("top"));
				var ch = ' ';
				var fg = Color.Silver;
				var bg = Color.Black;
				try
				{
					ch = (char)int.Parse(d.GetAttribute("character"));
				}
				catch
				{
					ch = d.GetAttribute("character")[0];
				}
				try
				{
					fg = Toolkit.GetColor(d.GetAttribute("forecolor")); //int.Parse(d.GetAttribute("forecolor"));
					bg = Toolkit.GetColor(d.GetAttribute("backcolor")); //int.Parse(d.GetAttribute("backcolor"));
				}
				catch
				{ }
				var block = d.GetAttribute("blocking") == "true";
				var name = d.GetAttribute("name");
				var description = d.InnerText.Trim();
				newBoard.Entities.Add(new Clutter(ch, fg, bg, block, name, description) { XPosition = x, YPosition = y });
			}
			#endregion

			#region Characters
			var characters = source.SelectNodes("character");
			foreach (var c in characters.OfType<XmlElement>())
			{
				var x = int.Parse(c.GetAttribute("left"));
				var y = int.Parse(c.GetAttribute("top"));
				var m = c.GetAttribute("movement");
				var mm = Motor.Stand;
				if (m == "wander")
					mm = Motor.Wander;
				else if (newBoard.Sectors.ContainsKey(m))
					mm = Motor.WanderSector;
				var bp = c.GetAttribute("bodyplan");
				var nm = c.GetAttribute("name");
				var pa = c.GetAttribute("pairing");
				Character ch;
				if (bp != "")
				{
					var g = (Gender)Enum.Parse(typeof(Gender), c.GetAttribute("gender"), true);
					ch = Character.Generate(bp, g);
				}
				else
					ch = Character.GetUnique(nm);
				if (c.HasAttribute("name"))
				{
					if (c.GetAttribute("name") == "random")
					{
						/*
						ch.GiveName();
						if (pa != "")
						{
							foreach (var so in newBoard.Entities.OfType<BoardChar>())
							{
								if (so.Pairing == pa)
								{
									ch.FamilyName = so.Character.FamilyName;
									break;
								}
							}
						}
						*/
					}
					else
						ch.Name = new Name(c.GetAttribute("name"));
				}
				newBoard.Entities.Add(new BoardChar(ch)
				{
					XPosition = x,
					YPosition = y,
					Movement = mm,
					Sector = m,
					Pairing = pa,
					ParentBoard = newBoard
				});
			}
			#endregion

			#region Warps
			var warps = source.SelectNodes("warp");
			foreach (var w in warps.OfType<XmlElement>())
			{
				var x = int.Parse(w.GetAttribute("left"));
				var y = int.Parse(w.GetAttribute("top"));
				var wi = w.GetAttribute("id");
				var tb = w.GetAttribute("target");
				var tw = w.GetAttribute("warp");
				//newBoard.Warps.Add(new Warp() { XPosition = x, YPosition = y, ID = wi, TargetBoard = tb, TargetWarpID = tw });
				if (w.HasChildNodes)
				{
					var t = w.SelectSingleNode("tile") as XmlElement;
					if (t != null)
					{
						var ch = ' ';
						var fg = Color.Silver;
						var bg = Color.Black;
						try
						{
							ch = (char)int.Parse(t.GetAttribute("character"));
						}
						catch
						{
							ch = t.GetAttribute("character")[0];
						}
						try
						{
							fg = Toolkit.GetColor(t.GetAttribute("forecolor")); //int.Parse(t.GetAttribute("forecolor"));
							bg = Toolkit.GetColor(t.GetAttribute("backcolor")); //int.Parse(t.GetAttribute("backcolor"));
						}
						catch
						{ }
						newBoard.Tilemap[x, y] = new Tile()
						{
							Character = ch,
							Background = bg,
							Foreground = fg,
						};

					}
				}
			}
			#endregion

			NoxicoGame.HostForm.Text = string.Format("Noxico - {0}", id);
			return newBoard;
		}

		[Obsolete("Used by Load.", true)]
		private static char CombineWall(char oldWall, char newWall)
		{
			if (oldWall == newWall)
				return newWall;
			var starters = "\xBA\xCE\xCD\xC9\xCB\xBB\xB9\xBC\xCA\xC8\xCC"; //"║╬═╔╦╗╣╝╩╚╠";
			if (!starters.Contains(oldWall) || !starters.Contains(newWall))
				return newWall;
			var combos = new[]
			{
				"\xBA\xCD\xCE",
				"\xBA\xBB\xB9", "\xBA\xBC\xB9", "\xBA\xC9\xCC", "\xBA\xC8\xCC", "\xBA\xB9\xB9", "\xBA\xCC\xCC", "\xBA\xCB\xCE", "\xBA\xCA\xCE",
				"\xCD\xBB\xCB", "\xCD\xC9\xCB", "\xCD\xC8\xCA", "\xCD\xBC\xCA", "\xCD\xB9\xCE", "\xCD\xCC\xCE", "\xCD\xCB\xCB", "\xCD\xCA\xCA",
				//"║═╬",
				//"║╗╣", "║╝╣", "║╔╠", "║╚╠", "║╣╣", "║╠╠", "║╦╬", "║╩╬",
				//"═╗╦", "═╔╦", "═╚╩", "═╝╩", "═╣╬", "═╠╬", "═╦╦", "═╩╩",
			};
			var c = new List<string>();
			foreach (var combo in combos)
			{
				c.Add(combo);
				c.Add(string.Format("{1}{0}{2}", combo[0], combo[1], combo[2]));
			}
			var thisCombo = c.Find(x => x[0] == oldWall && x[1] == newWall);
			if (thisCombo == null)
				return newWall;
			return thisCombo[2];
		}

		public bool IsSolid(int row, int col, bool flying = false)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return true;
			if (Tilemap[col, row].IsWater || Tilemap[col, row].CanFlyOver)
				return !flying;
			return Tilemap[col, row].Solid;
		}

		public bool IsBurning(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return false;
			return Tilemap[col, row].BurnTimer > 0 && Tilemap[col, row].CanBurn;
		}

		public TileDescription? GetSpecialDescription(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return null;
			return Tilemap[col, row].GetSpecialDescription();
		}

		public void SetTile(int row, int col, char tile, Color foreColor, Color backColor, bool solid = false, bool burn = false, bool fly = false)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			var t = new Tile()
			{
				Character = tile,
				Foreground = foreColor,
				Background = backColor,
				Solid = solid,
				CanBurn = burn,
				CanFlyOver = fly,
			};
			Tilemap[col, row] = t;
			DirtySpots.Add(new Location(col, row));
		}

		public void Immolate(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			var tile = Tilemap[col, row];
			if (tile.CanBurn)
			{
				tile.BurnTimer = Toolkit.Rand.Next(20, 23);
				tile.Character = (char)0xB1; //(char)0x15;
				tile.Background = Color.Red;
				tile.Foreground = Color.Yellow;
				DirtySpots.Add(new Location(col, row));
			}
		}

		public void TrailSlime(int row, int col, Color color)
		{
			var slime = new Clutter()
			{
				ParentBoard = this,
				ForegroundColor = color.Darken(2 + Toolkit.Rand.NextDouble()).Darken(),
				BackgroundColor = color.Darken(1.4),
				AsciiChar = (char)Toolkit.Rand.Next(0xB0, 0xB3),
				Blocking = false,
				XPosition = col,
				YPosition = row,
				Life = 5 + Toolkit.Rand.Next(10),
				Description = string.Format("This is a slime trail."),
				Name = "slime trail",
			};
			this.EntitiesToAdd.Add(slime);
		}

		public void BindEntities()
		{
			foreach (var entity in this.Entities)
				entity.ParentBoard = this;
		}

		public void Burn(bool spread)
		{
			//var flameColors = new[] { Color.Yellow, Color.Red, Color.Brown, Color.Maroon };
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					if (Tilemap[col, row].BurnTimer > 0)
					{
						Tilemap[col, row].Foreground = Color.FromArgb(Toolkit.Rand.Next(20, 25) * 10, Toolkit.Rand.Next(5, 25) * 10, 0); //flameColors[Toolkit.Rand.Next(flameColors.Length)];
						Tilemap[col, row].Background = Color.FromArgb(Toolkit.Rand.Next(20, 25) * 10, Toolkit.Rand.Next(5, 25) * 10, 0);//flameColors[Toolkit.Rand.Next(flameColors.Length)];
						DirtySpots.Add(new Location(col, row));
						if (!spread)
							continue;
						Tilemap[col, row].BurnTimer--;
						if (Tilemap[col, row].BurnTimer == 0)
						{
							Tilemap[col, row] = new Tile()
							{
								Character = (char)0xB0,
								Background = Color.Black,
								Foreground = Color.FromArgb(20,20,20),
							};
						}
						else if (Tilemap[col, row].BurnTimer == 10)
						{
							Immolate(row - 1, col);
							Immolate(row, col - 1);
							Immolate(row + 1, col);
							Immolate(row, col + 1);
							if (Toolkit.Rand.Next(100) > 50)
							{
								Immolate(row - 1, col - 1);
								Immolate(row - 1, col + 1);
								Immolate(row + 1, col - 1);
								Immolate(row + 1, col + 1);
							}
						}
					}
				}
			}
		}

		public void Update(bool active = false, bool surrounding = false)
		{
			Lifetime = 0;

			if (active)
			{
				foreach (var entity in this.Entities.Where(x => !x.Passive && !(x is Player)))
				{
					entity.Update();

					if (NoxicoGame.HostForm.Noxico.Player.Character.GetToken("health").Value <= 0)
						return;
				}
				if (!surrounding && Type != BoardType.Dungeon)
					UpdateSurroundings();
				Burn(true);
				return;
			}

			Burn(false);

			foreach (var entity in this.Entities.Where(x => x.Passive))
				entity.Update();
			if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
				NoxicoGame.HostForm.Noxico.Player.Update();
			if (EntitiesToRemove.Count > 0)
			{
				EntitiesToRemove.ForEach(x => { Entities.Remove(x); this.DirtySpots.Add(new Location(x.XPosition, x.YPosition)); });
				EntitiesToRemove.Clear();
			}
			if (EntitiesToAdd.Count > 0)
			{
				EntitiesToAdd.ForEach(x => Entities.Add(x));
				EntitiesToAdd.Clear();
			}
		}

		public void UpdateSurroundings()
		{
			
			var nox = NoxicoGame.HostForm.Noxico;
			if (this != nox.CurrentBoard)
				return;
			var p = nox.Player;
			if (p.OnOverworld)
			{
				//We are on the overworld. Update the surrounding boards.
				var owW = nox.Overworld.GetUpperBound(0);
				var owH = nox.Overworld.GetUpperBound(1);
				var owX = p.OverworldX;
				var owY = p.OverworldY;
				if (owY > 0) //north
					nox.GetBoard(nox.Overworld[owX, owY - 1]).Update(true, true);
				if (owX > 0 && owY > 0) //northwest
					nox.GetBoard(nox.Overworld[owX - 1, owY - 1]).Update(true, true);
				if (owX > 0) //west
					nox.GetBoard(nox.Overworld[owX - 1, owY]).Update(true, true);
				if (owX > 0 && owY < owH - 1) //southwest
					nox.GetBoard(nox.Overworld[owX - 1, owY + 1]).Update(true, true);
				if (owY < owH - 1) //south
					nox.GetBoard(nox.Overworld[owX, owY + 1]).Update(true, true);
				if (owX < owW - 1 && owY < owH - 1) //southeast
					nox.GetBoard(nox.Overworld[owX + 1, owY + 1]).Update(true, true);
				if (owX < owW - 1) //east
					nox.GetBoard(nox.Overworld[owX + 1, owY]).Update(true, true);
				if (owX > 0 && owY < owH - 1) //northwest
					nox.GetBoard(nox.Overworld[owX - 1, owY + 1]).Update(true, true);
			}
		}

		public void Redraw()
		{
			for (int row = 0; row < 25; row++)
				for (int col = 0; col < 80; col++)
					DirtySpots.Add(new Location(col, row));
		}

		public void Draw(bool force = false)
		{
			foreach (var l in this.DirtySpots)
			{
				var t = this.Tilemap[l.X, l.Y];
				NoxicoGame.HostForm.SetCell(l.Y, l.X, t.Character, t.Foreground, t.Background, force);
			}
			this.DirtySpots.Clear();

			foreach (var entity in this.Entities.OfType<Clutter>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<Container>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<DroppedItem>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<BoardChar>())
				entity.Draw();
		}

		private static int GetSample(int x, int y, int ux, int uy, int[,] source)
		{
			var mask = new BitVector32();
			var i = 1;
			for (var x1 = x - 1; x1 <= x + 1; x1++)
			{
					for (var y1 = y - 1; y1 <= y + 1; y1++)
					{
					var isWall = false;
					if (x1 < 0 || x1 > ux || y1 < 0 || y1 > uy)
						isWall = true;
					else
						isWall = (source[x1, y1] == 1);
					mask[i] = isWall;
					i <<= 1;
				}
			}
			return mask.Data;
		}

		[Obsolete("THIS IS STUPID. Also broken.", true)]
		public void CalculateLightmap()
		{
			if (Entities.OfType<LightSource>().Count() == 0)
				return;
			var map = new int[80, 25];
			for (int y = 0; y < 25; y++)
				for (int x = 0; x < 80; x++)
					map[x, y] = -1;
			foreach (var light in Entities.OfType<LightSource>())
			{
				//map[light.XPosition, light.YPosition] = light.Brightness;
				//SpreadValue(map, light.XPosition, light.YPosition, light.Brightness);
			}
			var bmp = new System.Drawing.Bitmap(80, 25);
			for (int y = 0; y < 25; y++)
			{
				for (int x = 0; x < 80; x++)
				{
					var c = map[x, y];
					if (c < 0)
						c = 0;
					bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(c, c, c));
				}
			}
			bmp.Save("lightmap_" + ID + ".png", System.Drawing.Imaging.ImageFormat.Png);
		}
		[Obsolete("Used by CalculateLightmap.", true)]
		private void SpreadValue(int[,] map, int x, int y, int b, int level = 0)
		{
			if (x < 0 || x >= 80 || y < 0 || y >= 25 || b <= 0 || map[x, y] != -1)
				return;
			if (level % 10 == 0)
				NoxicoGame.HostForm.Write(string.Format("{0}x{1} {2} ({3})", x, y, b, level), Color.Silver, Color.Gray, 50, 0);
			map[x, y] = b;
			SpreadValue(map, x - 1, y, b - 1, level + 1);
			SpreadValue(map, x, y - 1, b - 2, level + 1);
			SpreadValue(map, x + 1, y, b - 1, level + 1);
			SpreadValue(map, x, y + 1, b - 2, level + 1);
		}

		public static Board CreateBasicOverworldBoard(int biomeID, string id, string name, string music)
		{
			var biome = WorldGen.Biomes[biomeID];
			var newBoard = new Board();
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					newBoard.Tilemap[col, row] = new Tile()
					{
						Character = biome.GroundGlyphs[Toolkit.Rand.Next(biome.GroundGlyphs.Length)],
						Foreground = biome.Color.Darken(biome.DarkenPlus + (Toolkit.Rand.NextDouble() / biome.DarkenDiv)),
						Background = biome.Color.Darken(biome.DarkenPlus + (Toolkit.Rand.NextDouble() / biome.DarkenDiv)),
						CanBurn = biome.CanBurn,
						IsWater = biome.IsWater,
					};
				}
			}
			newBoard.Tokens = Token.Tokenize("name: \"" + name + "\"\nid: \"" + id + "\"\nmusic: \"" + music + "\"\ntype: 3\nbiome: " + biomeID + "\nencounters: 0\n");
			newBoard.ID = id;
			newBoard.Name = name;
			newBoard.Music = music;
			return newBoard;
		}

		public static Board CreateFromBitmap(byte[,] bitmap, int biome, int x, int y)
		{
			var newBoard = new Board();
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var b = bitmap[(y * 25) + row, (x * 80) + col];
					var d = WorldGen.Biomes[b];
					var fg = d.Color.Darken();
					var bg = d.Color;
					if (d.DarkenPlus != 0 && d.DarkenDiv != 0)
					{
						fg = d.Color.Darken(d.DarkenPlus + (Toolkit.Rand.NextDouble() / d.DarkenDiv));
						bg = d.Color.Darken(d.DarkenPlus + (Toolkit.Rand.NextDouble() / d.DarkenDiv));
					}
					newBoard.Tilemap[col, row] = new Tile()
					{
						Character = d.GroundGlyphs[Toolkit.Rand.Next(d.GroundGlyphs.Length)],
						Foreground = fg, Background = bg,
						CanBurn = d.CanBurn,
						IsWater = d.IsWater,
					};
				}
			}

			var biomeData = WorldGen.Biomes[biome];
	
			var nameID = string.Format("OW_{0}x{1}", x, y);
			newBoard.Tokens = Token.Tokenize("name: \"" + nameID + "\"\nid: \"" + nameID + "\"\nmusic: \"" + biomeData.Music + "\"\ntype: 0\nbiome: " + biome + "\nencounters: " + biomeData.MaxEncounters + "\n");

			var encounters = newBoard.GetToken("encounters");
			foreach (var e in biomeData.Encounters)
				encounters.Tokens.Add(new Token() { Name = e });

			return newBoard;
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public void DumpToHTML(string suffix = "")
		{
			if (!string.IsNullOrWhiteSpace(suffix) && !suffix.StartsWith("_"))
				suffix = "_" + suffix;
			var file = new StreamWriter("Board-" + ID + suffix + ".html");
			file.WriteLine("<!DOCTYPE html>");
			file.WriteLine("<html>");
			file.WriteLine("<head>");
			file.WriteLine("<meta http-equiv=\"Content-Type\" content=\"text/html; CHARSET=utf-8\" />");
			file.WriteLine("<h1>Noxico board data dump</h1>");
			file.WriteLine("<p>");
			file.WriteLine("\tName: {0}<br />", Name);
			file.WriteLine("\tID: {0}<br />", ID);
			file.WriteLine("\tMusic: {0}<br />", Music);
			file.WriteLine("\tType: {0}<br />", Type);
			file.WriteLine("\tBiome: {0}<br />", WorldGen.Biomes[(int)GetToken("biome").Value].Name);
			file.WriteLine("\tCulture: {0}<br />", HasToken("culture") ? GetToken("culture").Text : "&lt;none&gt;");
			file.WriteLine("</p>");
			file.WriteLine("<pre>");
			file.WriteLine(DumpTokens(Tokens, 0));
			file.WriteLine("</pre>");
			file.WriteLine("<h2>Screendump</h2>");
			file.WriteLine("<table style=\"font-family: monospace;\" cellspacing=0 cellpadding=0>");
			for (int row = 0; row < 25; row++)
			{
				file.WriteLine("\t<tr>");
				for (int col = 0; col < 80; col++)
				{
					var tile = Tilemap[col, row];
					var back = string.Format("rgb({0},{1},{2})", tile.Background.R, tile.Background.G, tile.Background.B);
					var fore = string.Format("rgb({0},{1},{2})", tile.Foreground.R, tile.Foreground.G, tile.Foreground.B);
					var chr = string.Format("&#x{0:X};", (int)tile.Character);
					var tag = "";
					var link = "";

					var ent = Entities.FirstOrDefault(x => x.XPosition == col && x.YPosition == row);
					if (ent != null)
					{
						back = string.Format("rgb({0},{1},{2})", ent.BackgroundColor.R, ent.BackgroundColor.G, ent.BackgroundColor.B);
						fore = string.Format("rgb({0},{1},{2})", ent.ForegroundColor.R, ent.ForegroundColor.G, ent.ForegroundColor.B);
						chr = string.Format("&#x{0:X};", (int)ent.AsciiChar);
						tag = ent.ID;
						if (ent is BoardChar)
						{
							tag = ((BoardChar)ent).Character.Name.ToString(true);
							link = "<a href=\"#" + ent.ID + "\" style=\"color: " + fore + ";\">";
						}
					}
					if (!string.IsNullOrWhiteSpace(tag))
						tag = " title=\"" + tag + "\"";

					file.WriteLine("\t\t<td style=\"background: {0}; color: {1};\"{3}>{4}{2}{5}</td>", back, fore, chr, tag, link, string.IsNullOrWhiteSpace(link) ? "" : "</a>");
					//DirtySpots.Add(new Location(col, row));
				}
				file.WriteLine("</tr>");
			}
			file.WriteLine("</table>");

			if (Type == BoardType.Dungeon || Type == BoardType.Wild)
			{
				file.WriteLine("<h2>Encounter set</h2>");
				file.WriteLine("<ul>");
				GetToken("encounters").Tokens.ForEach(x => file.WriteLine("<li>{0}</li>", x.Name));
				file.WriteLine("</ul>");
			}

			file.WriteLine("<h2>Entities</h2>");
			if (Entities.OfType<BoardChar>().Count() > 0)
			{
				file.WriteLine("<h3>BoardChar</h3>");
				foreach (var bc in Entities.OfType<BoardChar>())
				{
					file.WriteLine("<h4 id=\"{1}\">{0}</h4>", bc.Character.IsProperNamed ? bc.Character.Name.ToString(true) : bc.Character.Title, bc.ID);
					file.WriteLine("<pre>");
					file.WriteLine(bc.Character.DumpTokens(bc.Character.Tokens, 0));
					file.WriteLine("</pre>");
				}
			}
			if (Entities.OfType<Container>().Count() > 0)
			{
				file.WriteLine("<h3>Container</h3>");
				foreach (var c in Entities.OfType<Container>())
				{
					file.WriteLine("<h4>{0} at {1}x{2}</h4>", c.Name, c.XPosition, c.YPosition);
					file.WriteLine("<pre>");
					file.WriteLine(c.Token.DumpTokens(c.Token.Tokens, 0));
					file.WriteLine("</pre>");
				}
			} 
			file.Flush();
			file.Close();
		}

		public void RespawnEncounters()
		{
			if (GetToken("encounters").Value == 0 || Type != BoardType.Dungeon && Type != BoardType.Wild)
				return;
			var encData = GetToken("encounters");
			var count = Entities.OfType<BoardChar>().Count();
			var toAdd = encData.Value - count;
			if (toAdd <= 0)
				return;
			for (var i = 0; i < toAdd; i++)
			{
				var newb = new BoardChar(Character.Generate(Toolkit.PickOne(encData.Tokens.Select(x => x.Name).ToArray()), Gender.Random))
				{
					ParentBoard = this,
				};
				newb.XPosition = Toolkit.Rand.Next(2, 78);
				newb.YPosition = Toolkit.Rand.Next(2, 23);
				var lives = 100;
				while (IsSolid(newb.YPosition, newb.XPosition) && lives > 0)
				{
					lives--;
					newb.XPosition = Toolkit.Rand.Next(2, 78);
					newb.YPosition = Toolkit.Rand.Next(2, 23);
				}
				if (lives == 0)
					continue;
				newb.Character.Tokens.Add(new Token() { Name = "hostile" });
				newb.Character.GetToken("health").Value = 12 * Toolkit.Rand.Next(3);
				this.Entities.Add(newb);
			}

			//Clean up some of the corpses at random.
			foreach (var corpse in Entities.OfType<Clutter>().Where(x => x.Name.EndsWith("'s remains")))
				if (Toolkit.Rand.NextDouble() > 0.7)
					this.EntitiesToRemove.Add(corpse);
		}

		private void CleanUpSlimeTrails()
		{
			foreach (var c in this.Entities.OfType<Clutter>().Where(c => c.Life > 0))
				this.EntitiesToRemove.Add(c);
		}
		
	}


}
