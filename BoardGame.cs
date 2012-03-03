using System;
using System.IO.Compression;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Xml;
using System.Windows.Forms;
using System.Drawing;

namespace Noxico
{
	public delegate void SubscreenFunc();

	public struct Rectangle
	{
		public int Left, Top, Right, Bottom;
		public Location GetCenter()
		{
			return new Location(Left + ((Right - Left) / 2), Top + ((Bottom - Top) / 2));
		}
	}

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

	public enum UserMode
	{
		Walkabout, LookAt, Subscreen
	}

	public enum Biome
	{
		Grassland,
		Desert,
		Snow,
		Swamp
	}

	public class Tile
	{
		public char Character { get; set; }
		public char ExTile { get; set; }
		public Color Foreground { get; set; }
		public Color Background { get; set; }
		public bool Solid { get; set; }
		public bool CanBurn { get; set; }
		public int BurnTimer { get; set; }
		public bool HasExTile { get; set; }
		public bool CanFlyOver { get; set; }

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write((byte)Character);
			//stream.Write((byte)((Background * 16) + (Foreground % 16)));
			Foreground.SaveToFile(stream);
			Background.SaveToFile(stream);

			var bits = new BitVector32();
			bits[1] = CanBurn;
			bits[2] = Solid;
			bits[4] = CanFlyOver;
			bits[8] = BurnTimer > 0;
			bits[16] = false; //reserved
			bits[32] = false; //reserved
			bits[64] = HasExTile;
			bits[128] = false; //reserved for "has more settings"
			//stream.Write((byte)((HasExTile ? 8 : 0) | (BurnTimer > 0 ? 8 : 0) | (CanFlyOver ? 4 : 0) | (Solid ? 2 : 0) | (CanBurn ? 1 : 0)));
			stream.Write((byte)bits.Data);
			if (HasExTile)
				stream.Write(ExTile);
			if(BurnTimer > 0)
				stream.Write((byte)BurnTimer);
		}

		public void LoadFromFile(BinaryReader stream)
		{
			Character = (char)stream.ReadByte();
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
			var HasBurn = bits[8];
			//HasReversedA = bits[16];
			//HasReversedB = bits[32];
			HasExTile = bits[64];
			//HasMoreSettings = bits[128];
			if (HasExTile)
				ExTile = stream.ReadChar();
			if (HasBurn)
				BurnTimer = stream.ReadByte();
		}
	}

	public class Warp
	{
		public static int GeneratorCount = 0;

		//TODO: stairs that only activate when you press < or > while on them
		public string ID { get; set; }
		public int XPosition { get; set; }
		public int YPosition { get; set; }
		public string TargetBoard { get; set; }
		public string TargetWarp { get; set; }

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
			stream.Write(TargetBoard ?? "");
			stream.Write(TargetWarp ?? "");
		}

		public static Warp LoadFromFile(BinaryReader stream)
		{
			var newWarp = new Warp();
			newWarp.ID = stream.ReadString();
			newWarp.XPosition = stream.ReadByte();
			newWarp.YPosition = stream.ReadByte();
			newWarp.TargetBoard = stream.ReadString();
			newWarp.TargetWarp = stream.ReadString();
			return newWarp;
		}
	}

	public class StatusMessage
	{
		public string Message { get; set; }
		public Color Color { get; set; }
		public bool New { get; set; }
	}

	public class Board
	{
		public static int GeneratorCount = 0;

		public string Name { get; set; }
		public string ID { get; private set; }
		public string Music { get; set; }
		public List<Entity> Entities { get; set; }
		public List<Warp> Warps { get; set; }
		public List<Location> DirtySpots { get; set; }
		public List<Entity> EntitiesToRemove { get; set; }
		public List<Entity> EntitiesToAdd { get; set; }

		public Tile[,] Tilemap = new Tile[80, 25];

		//public string Message { get; set; }
		//public Color MessageColor { get; set; }
		//public int MessageTimer { get; set; }

		public Dictionary<string, Rectangle> Sectors { get; set; }
		public List<Location> ExitPossibilities { get; set; }

		public Board()
		{
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

		public void SaveToFile(BinaryWriter stream)
		{
			Console.WriteLine(" * Saving board {0}...", Name);
			stream.Write(Name);
			stream.Write(ID);
			stream.Write(Music);

			stream.Write(Sectors.Count);
			//stream.Write(Entities.OfType<FloorBot>().Count());
			stream.Write(Entities.OfType<BoardChar>().Count() - Entities.OfType<Player>().Count());
			stream.Write(Entities.OfType<DroppedItem>().Count());
			stream.Write(Entities.OfType<Dressing>().Count());
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
			foreach (var e in Entities.OfType<Dressing>())
				e.SaveToFile(stream);
			Warps.ForEach(x => x.SaveToFile(stream));
		}

		public static Board LoadFromFile(BinaryReader stream)
		{
			var newBoard = new Board();
			newBoard.Name = stream.ReadString();
			newBoard.ID = stream.ReadString();
			newBoard.Music = stream.ReadString();

			var secCt = stream.ReadInt32();
			//var botCt = stream.ReadInt32();
			var chrCt = stream.ReadInt32();
			var drpCt = stream.ReadInt32();
			var drsCt = stream.ReadInt32();
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
			for (int i = 0; i < drsCt; i++)
				newBoard.Entities.Add(Dressing.LoadFromFile(stream));

			for (int i = 0; i < wrpCt; i++)
				newBoard.Warps.Add(Warp.LoadFromFile(stream));

			newBoard.BindEntities();

			Console.WriteLine(" * Loaded board {0}...", newBoard.Name);

			return newBoard;
		}

		[Obsolete("Don't use until the Home Base system is in. Other than that, cannibalize away me hearties." , true)]
		public static Board Load(string id)
		{
			var xDoc = new XmlDocument();
			xDoc.Load("boards.xml");
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
						//TODO: add more corner possibilities.
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

			#region Dressing
			var dressings = source.SelectNodes("dressing");
			foreach (var d in dressings.OfType<XmlElement>())
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
				newBoard.Entities.Add(new Dressing(ch, fg, bg, block, name, description) { XPosition = x, YPosition = y });
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
						//TODO: Replace GiveName call with Culture system.
						//TODO: Rework pairings to use relationship tokens.
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
				newBoard.Warps.Add(new Warp() { XPosition = x, YPosition = y, ID = wi, TargetBoard = tb, TargetWarp = tw });
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

		public bool IsSolid(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return true;
			return Tilemap[col, row].Solid;
		}

		public bool IsBurning(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return false;
			return Tilemap[col, row].BurnTimer > 0 && Tilemap[col, row].CanBurn;
		}

		public void SetTile(int row, int col, char tile, char exTile, Color foreColor, Color backColor, bool solid = false, bool burn = false, bool fly = false)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			var t = new Tile()
			{
				Character = tile,
				ExTile = exTile,
				HasExTile = exTile > 0,
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
			var slime = new Dressing()
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
			if (active)
			{
				foreach (var entity in this.Entities.Where(x => !x.Passive && !(x is Player)))
					entity.Update();
				if (!surrounding)
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
			var owI = NoxicoGame.GetOverworldIndex(this);
			var reach = nox.Overworld.GetLength(0);
			if (owI < reach * reach)
			{
				//We are on the overworld. Update the surrounding boards.
				var owX = owI % reach;
				var owY = owI / reach;
				if (owY > 0) //north
					nox.Boards[nox.Overworld[owX, owY - 1]].Update(true, true);
				if (owX > 0 && owY > 0) //northwest
					nox.Boards[nox.Overworld[owX - 1, owY - 1]].Update(true, true);
				if (owX > 0) //west
					nox.Boards[nox.Overworld[owX - 1, owY]].Update(true, true);
				if (owX > 0 && owY < reach - 1) //southwest
					nox.Boards[nox.Overworld[owX - 1, owY + 1]].Update(true, true);
				if (owY < reach - 1) //south
					nox.Boards[nox.Overworld[owX, owY + 1]].Update(true, true);
				if (owX < reach - 1 && owY < reach - 1) //southeast
					nox.Boards[nox.Overworld[owX + 1, owY + 1]].Update(true, true);
				if (owX < reach - 1) //east
					nox.Boards[nox.Overworld[owX + 1, owY]].Update(true, true);
				if (owX > 0 && owY < reach - 1) //northwest
					nox.Boards[nox.Overworld[owX - 1, owY + 1]].Update(true, true);
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
#if USE_EXTENDED_TILES
				NoxicoGame.HostForm.SetCell(l.Y, l.X, t.HasExTile ? t.ExTile : t.Character, t.Foreground, t.Background, force);
#else
				NoxicoGame.HostForm.SetCell(l.Y, l.X, t.Character, t.Foreground, t.Background, force);
#endif
			}
			this.DirtySpots.Clear();

			foreach (var entity in this.Entities.OfType<Dressing>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<DroppedItem>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<BoardChar>())
				entity.Draw();
		}

		/*
		[Obsolete("DungeonGenerator needs replacement (see issue #1)", true)]
		public static Board FromDungeonGenerator(DungeonGenerator.DungeonGenerator generator)
		{
			var dungeon = generator.Generate();
			var dungeonMap = DungeonGenerator.DungeonGenerator.ExpandToTiles(dungeon);
			var newBoard = new Board();
			var grounds = new[] { ',', '\'', '`', '.', };
			var walls = global::Noxico.Properties.Resources.WallLookup;

			var xBound = dungeonMap.GetUpperBound(0);
			var yBound = dungeonMap.GetUpperBound(1);
			for (var x = 0; x <= xBound; x++)
			{
				for (var y = 0; y <= yBound; y++)
				{
					//TODO: use biome/depth-appropriate colors and ground styles
					var newTile = new Tile();
					switch (dungeonMap[x, y])
					{
						case 0: //door -- ignore?
						case 2: //empty space -- use as corridor
							newTile.Foreground = Color.Silver;
							newTile.Character = ' ';
							newTile.Background = Color.Black;
							break;
						case 1: //wall
							newTile.Solid = true;
							newTile.Foreground = Color.Brown;

							var sample = GetSample(x, y, xBound, yBound, dungeonMap);
							if (walls[sample] == 0xDB)
							{
								newTile.Character = ' ';
								newTile.Solid = true;
							}
							else
								newTile.Character = (char)walls[sample];

							break;
						case 3: //room
							newTile.Foreground = Color.Green;
							newTile.Character = grounds[Toolkit.Rand.Next(grounds.Length)];
							break;
					}
					newBoard.Tilemap[y, x] = newTile;
				}
			}

			//Fix right edge
			for (var y = 0; y < 25; y++)
				newBoard.Tilemap[79, y] = new Tile() { Solid = true, Character = ' ', Foreground = Color.Gray };

			//List possible exit locations, and while you're at it, fix a little error in the walls bin: -- above | should be a T. Some patterns that end in T have this right, but some don't.
			newBoard.ExitPossibilities = new List<Location>();
			for (var x = 1; x < xBound; x++)
			{
				for (var y = 1; y < yBound; y++)
				{
					if (newBoard.Tilemap[y, x].Character == (char)0xC4 && newBoard.Tilemap[y, x].Solid &&
						newBoard.Tilemap[y, x + 1].Character == (char)0xB3 && newBoard.Tilemap[y, x + 1].Solid)
						newBoard.Tilemap[y, x].Character = (char)0xC2;

					if (newBoard.Tilemap[y, x].Solid)
						continue;

					if ((newBoard.Tilemap[y + 1, x].Solid && newBoard.Tilemap[y - 1, x].Solid && newBoard.Tilemap[y, x + 1].Solid && !newBoard.Tilemap[y, x - 1].Solid) || // south corridor
						(newBoard.Tilemap[y + 1, x].Solid && newBoard.Tilemap[y - 1, x].Solid && newBoard.Tilemap[y, x - 1].Solid && !newBoard.Tilemap[y, x + 1].Solid) || // north corridor
						(newBoard.Tilemap[y + 1, x].Solid && !newBoard.Tilemap[y - 1, x].Solid && newBoard.Tilemap[y, x - 1].Solid && newBoard.Tilemap[y, x + 1].Solid) || // east corridor
						(!newBoard.Tilemap[y + 1, x].Solid && newBoard.Tilemap[y - 1, x].Solid && newBoard.Tilemap[y, x - 1].Solid && newBoard.Tilemap[y, x + 1].Solid)) // west corridor
					{
#if FOOP
						newBoard.Tilemap[y, x].Character = '?';
						newBoard.Tilemap[y, x].Foreground = 8;
#endif
						newBoard.ExitPossibilities.Add(new Location(y, x));
					}
				}
			}

			//Build a sector list from the generator's rooms
			for (var i = 0; i < dungeon.Rooms.Count; i++)
			{
				var b = dungeon.Rooms[i].Bounds;
				var newRect = new Rectangle()
				{
					Left = b.Top,
					Right = b.Bottom,
					Top = b.Left,
					Bottom = b.Right,
				};
				newBoard.Sectors.Add("dungeonRoom" + i, newRect);
			}

			newBoard.Name = "Unnamed dungeon";
			newBoard.ID = "DungeonGenerator_" + Board.GeneratorCount;
			Board.GeneratorCount++;
			newBoard.Music = "ko0x_-_btto.it";
			return newBoard;
		}
		*/

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

		public static Board CreateBasicOverworldBoard(Biome biome, int x, int y)
		{
			var groundColors = new[] { Color.Green, Color.Yellow, Color.White, Color.MediumPurple };
			var newBoard = new Board();
			var grasses = new[] { ',', '\'', '`', '.', };
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					newBoard.Tilemap[col, row] = new Tile()
					{
						Character = grasses[Toolkit.Rand.Next(grasses.Length)],
						Foreground = groundColors[(int)biome].Darken(2 + (Toolkit.Rand.NextDouble() / 2)),
						Background = groundColors[(int)biome].Darken(2 + (Toolkit.Rand.NextDouble() / 2)),
						CanBurn = (biome == 0),
					};
				}
			}
			newBoard.ID = string.Format("OW_{0}x{1}", x, y);
			newBoard.Name = newBoard.ID;
			newBoard.Music = "set://" + biome.ToString();
			return newBoard;
		}
	}

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
			foreach (var item in xDoc.SelectNodes("//items/item").OfType<XmlElement>())
				KnownItems.Add(InventoryItem.FromXML(item));

			//var test = Character.Generate("foocubus", Gender.Female);
			//test.CreateInfoDump();
			//hostForm.Close();

			//InitializeTestEnvironment();
			
			/*
			var player = new Player(Character.GetUnique("Maria")) { ParentBoard = CurrentBoard, XPosition = 40, YPosition = 9 };
			//var player = new Player(Character.Generate("naga", Gender.Herm)) { ParentBoard = CurrentBoard, XPosition = 40, YPosition = 9 };
			//player.Character.GiveName(new [] { "legend", "spew", "seed" }, player.Character.HasToken("xenoname"));
			CurrentBoard.Entities.Add(player);
			if (CurrentBoard.Warps.Count > 0)
			{
				var startPos = CurrentBoard.Warps.ToList()[Toolkit.Rand.Next(CurrentBoard.Warps.Count)];
				player.XPosition = startPos.XPosition;
				player.YPosition = startPos.YPosition;
			}
			*/
			/*
			for (int i = 0; i < 100; i++)
			{
				var horrorTerror = new BoardChar(Character.Generate("horrorterror", Gender.Random)) { ParentBoard = CurrentBoard, XPosition = Toolkit.Rand.Next(79), YPosition = Toolkit.Rand.Next(24) };
				CurrentBoard.Entities.Add(horrorTerror);
			}
			*/

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
				AddMessage("Welcome back, " + NoxicoGame.HostForm.Noxico.Player.Character.Name + ".");
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
							townGen.Create(biomeMap[x, y]);
							townGen.ToTilemap(ref thisMap.Tilemap);
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
			var dunGen = new CaveGenerator();
			dunGen.Create(Biome.Grassland);
			dunGen.ToTilemap(ref Boards[0].Tilemap);
			Boards[0].Entities.Add(new BoardChar(Character.GetUnique("Lena")) { XPosition = 39, YPosition = 11, ParentBoard = Boards[0] });

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

	public class CryptStream : Stream
	{
		public virtual Stream BaseStream { get; private set; }

		public CryptStream(Stream stream)
		{
			BaseStream = stream;
		}

		public override bool CanRead
		{
			get { return BaseStream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return BaseStream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return BaseStream.CanWrite; }
		}

		public override void Flush()
		{
			BaseStream.Flush();
		}

		public override long Length
		{
			get { return BaseStream.Length; }
		}

		public override long Position
		{
			get
			{
				return BaseStream.Position;
			}
			set
			{
				BaseStream.Position = value;
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			var cb = new Byte[count];
			var j = BaseStream.Read(cb, 0, count);
			for (var i = 0; i < count; i++)
				buffer[i + offset] = (byte)(cb[i] ^ 0x80);
			return j;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return BaseStream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			BaseStream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			var cb = new byte[count];
			for (int i = 0; i < count; i++)
				cb[i] = (byte)(buffer[i + offset] ^ 0x80);
			BaseStream.Write(cb, 0, count);
		}
	}
}
