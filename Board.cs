using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Noxico
{
	/// <summary>
	/// Determines which subscreen to run when UserMode is set to Subscreen.
	/// </summary>
	public delegate void SubscreenFunc();

	/// <summary>
	/// Stores a set of four integers that represent the location and size of a rectangle.
	/// Basically a version of <see cref="System.Drawing.Rectangle"/> that replaces the extra stuff with a single feature.
	/// </summary>
	public struct Rectangle
	{
		/// <summary>
		/// Gets the x-coordinate of the left edge of this <see cref="Noxico.Rectangle"/> structure.
		/// </summary>
		public int Left { get; set; }
		public int Top { get; set; }
		public int Right { get; set; }
		public int Bottom { get; set; }

		/// <summary>
		/// Creates and returns a <see cref="Noxico.Point"/> that is the centerpoint of this <c>Noxico.Rectangle</c>.
		/// </summary>
		/// <returns></returns>
		public Point GetCenter()
		{
			return new Point(Left + ((Right - Left) / 2), Top + ((Bottom - Top) / 2));
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Left);
			stream.Write(Top);
			stream.Write(Right);
			stream.Write(Bottom);
		}

		public static Rectangle LoadFromFile(BinaryReader stream)
		{
			var l = stream.ReadInt32();
			var t = stream.ReadInt32();
			var r = stream.ReadInt32();
			var b = stream.ReadInt32();
			return new Rectangle() { Left = l, Top = t, Right = r, Bottom = b };
		}
	}

	/// <summary>
	/// Represents an ordered pair of integer x- and y-coordinates that defines a point in a two-dimensional plane.
	/// Basically a version of <see cref="System.Drawing.Point"/> without the extra stuff.
	/// </summary>
	public struct Point
	{
		public int X { get; set; }
		public int Y { get; set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Noxico.Point"/> class with the specified coordinates.
		/// </summary>
		/// <param name="x">The horizontal position of the point.</param>
		/// <param name="y">The vertical position of the point.</param>
		public Point(int x, int y) : this()
		{
			X = x;
			Y = y;
		}

		/// <summary>
		/// Compares two <see cref="Noxico.Point"/> objects.
		/// The result specifies whether the values of the <see cref="Noxico.Point.X"/> and <see cref="Noxico.Point.Y"/> properties of the two <see cref="Noxico.Point"/> objects are equal.
		/// </summary>
		/// <param name="l">A <see cref="Noxico.Point"/> to compare.</param>
		/// <param name="r">A <see cref="Noxico.Point"/> to compare.</param>
		/// <returns>true if the <see cref="Noxico.Point.X"/> and <see cref="Noxico.Point.Y"/> values of left and right are equal; otherwise, false.</returns>
		public static bool operator ==(Point l, Point r)
		{
			return l.X == r.X && l.Y == r.Y;
		}

		/// <summary>
		/// Compares two <see cref="Noxico.Point"/> objects.
		/// The result specifies whether the values of the <see cref="Noxico.Point.X"/> and <see cref="Noxico.Point.Y"/> properties of the two <see cref="Noxico.Point"/> objects are unequal.
		/// </summary>
		/// <param name="l">A <see cref="Noxico.Point"/> to compare.</param>
		/// <param name="r">A <see cref="Noxico.Point"/> to compare.</param>
		/// <returns>true if the <see cref="Noxico.Point.X"/> and <see cref="Noxico.Point.Y"/> values of left and right differ; otherwise, false.</returns>
		public static bool operator !=(Point l, Point r)
		{
			return !(l == r);
		}

		/// <summary>
		/// Specifies whether this <see cref="Noxico.Point"/> contains the same coordinates as the specified <c>System.Object</c>.
		/// </summary>
		/// <param name="obj">The <see cref="System.Object"/> to test.</param>
		/// <returns>true if <paramref name="obj"/> is a <see cref="Noxico.Point"/> and has the same coordinates as this <see cref="Noxico.Point"/>.</returns>
		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is Point))
				return false;
			var opt = (Point)obj;
			return opt.X == X && opt.Y == Y;
		}

		/// <summary>
		/// Returns a hash code for this <see cref="Noxico.Point"/>.
		/// </summary>
		/// <returns>An integer value that specifies a hash value for this <see cref="Noxico.Point"/>.</returns>
		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		/// <summary>
		/// Converts this <see cref="Noxico.Point"/> to a human-readable string.
		/// </summary>
		/// <returns>A string that represents this <see cref="Noxico.Point"/>.</returns>
		public override string ToString()
		{
			return string.Format("{0}x{1}", X, Y);
		}
	}

	/// <summary>
	/// The current operation state of the game.
	/// </summary>
	public enum UserMode
	{
		Walkabout, Aiming, Subscreen
	}

	public enum SolidityCheck
	{
		Walker, DryWalker, Flyer, Projectile
	}

	public enum Fluids
	{
		Dry, Water, KoolAid, Slime, Blood, Semen, Reserved1, Reserved2
	}

	public class TileDefinition
	{
		public string Name { get; private set; }
		public int Index { get; private set; }
		public int Glyph { get; private set; }
		public Color Foreground { get; private set; }
		public Color Background { get; private set; }
		public Color MultiForeground { get; private set; }
		public bool Wall { get; private set; }
		public bool Ceiling { get; private set; }
		public bool Cliff { get; private set; }
		public bool Fence { get; private set; }
		public bool Grate { get; private set; }
		public bool CanBurn { get; private set; }
		public string FriendlyName { get; private set; }
		public string Description { get; private set; }
		public Token Variants { get; private set; }
		public int VariableWall { get; private set; }
		public bool IsVariableWall { get { return VariableWall > 0; } }

		public bool SolidToWalker { get { return Wall || Fence || Cliff; } }
		public bool SolidToFlyer { get { return Ceiling || Wall; } }
		public bool SolidToProjectile { get { return (Wall && !Grate); } }

		private static Dictionary<int, TileDefinition> defs;
		static TileDefinition()
		{
			defs = new Dictionary<int, TileDefinition>();
			var tml = Mix.GetTokenTree("tiles.tml", true);
			foreach (var tile in tml.Where(t => t.Name == "tile"))
			{
				var i = (int)tile.Value;
				var def = new TileDefinition()
				{
					Index = i,
					Name = tile.GetToken("id").Text,
					Glyph = (int)tile.GetToken("char").Value,
					Background = Color.FromName(tile.GetToken("back").Text),
					Foreground = tile.HasToken("fore") ? Color.FromName(tile.GetToken("fore").Text) : Color.FromName(tile.GetToken("back").Text).Darken(),
					Wall = tile.HasToken("wall"),
					Ceiling = tile.HasToken("ceiling"),
					Cliff = tile.HasToken("cliff"),
					Fence = tile.HasToken("fence"),
					Grate = tile.HasToken("grate"),
					CanBurn = tile.HasToken("canburn"),
					FriendlyName = tile.HasToken("_n") ? tile.GetToken("_n").Text : null,
					Description = tile.HasToken("description") ? tile.GetToken("description").Text : null,
					Variants = tile.HasToken("variants") ? tile.GetToken("variants") : new Token("variants"),
					VariableWall = tile.HasToken("varwall") ? (int)tile.GetToken("varwall").Value : 0,
				};
				def.MultiForeground = tile.HasToken("mult") ? Color.FromName(tile.GetToken("mult").Text) : def.Foreground;
				defs.Add(i, def);
			}
		}

		public static TileDefinition Find(string tileName, bool noVariants = false)
		{
			var def = defs.FirstOrDefault(t => t.Value.Name.Equals(tileName, StringComparison.InvariantCultureIgnoreCase)).Value;
			if (def == null)
				return defs[0];
			if (!noVariants && def.Variants.Tokens.Count > 0)
			{
				var iant = def.Variants.Tokens.PickOne();
				if (Random.NextDouble() > iant.Value)
					def = TileDefinition.Find(iant.Name);
			}
			return def;
		}

		public static TileDefinition Find(int index, bool noVariants = false)
		{
			if (defs.ContainsKey(index))
			{
				var def = defs[index];
				if (!noVariants && def.Variants.Tokens.Count > 0)
				{
					var iant = def.Variants.Tokens.PickOne();
					if (Random.NextDouble() > iant.Value)
						def = TileDefinition.Find(iant.Name);
				}
				return def;
			}
			else
				return null;
		}

		public override string ToString()
		{
			return Name;
		}
	}

	/// <summary>
	/// A single tile on a board.
	/// </summary>
	public class Tile
	{
		public int Index { get; set; }

		public Fluids Fluid { get; set; }
		public bool Shallow { get; set; }
		public int BurnTimer { get; set; }
		public bool Seen { get; set; }
		public Color SlimeColor { get; set; }
		public int InherentLight { get; set; }

		public bool SolidToWalker { get { return Definition.SolidToWalker; } }
		public bool SolidToDryWalker { get { return Definition.SolidToWalker || (Fluid != Fluids.Dry && !Shallow); } }
		public bool SolidToFlyer { get { return Definition.SolidToFlyer; } }
		public bool SolidToProjectile { get { return Definition.SolidToProjectile; } }

		public TileDefinition Definition
		{
			get
			{
				return TileDefinition.Find(Index, true);
			}
			set
			{
				Index = Definition.Index; 
			}
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write((UInt16)Index);

			var bits = new BitVector32();
			bits[32] = Shallow;
			bits[64] = (InherentLight > 0);
			bits[128] = Seen;
			stream.Write((byte)((byte)bits.Data | (byte)Fluid));
			//if (BurnTimer > 0)
				stream.Write((byte)BurnTimer);
			//if (Fluid == Fluids.Slime)
				if (SlimeColor == null)
					SlimeColor = Color.Transparent;
				SlimeColor.SaveToFile(stream);
			if (InherentLight > 0)
				stream.Write((byte)InherentLight);
		}

		public void LoadFromFile(BinaryReader stream)
		{
			Index = stream.ReadUInt16();

			var set = stream.ReadByte();
			var bits = new BitVector32(set);
			Shallow = bits[32];
			Seen = bits[128];
			BurnTimer = stream.ReadByte();
			Fluid = (Fluids)(set & 7);
			//if (Fluid == Fluids.Slime)
				SlimeColor = Toolkit.LoadColorFromFile(stream);
				if (bits[64])
					InherentLight = stream.ReadByte();
		}

		public Tile Clone()
		{
			return new Tile()
			{
				Index = this.Index,
				Fluid = this.Fluid,
				Shallow = this.Shallow,
				BurnTimer = this.BurnTimer,
				Seen = this.Seen,
				SlimeColor = this.SlimeColor,
				InherentLight = this.InherentLight
			};
		}

		public override string ToString()
		{
			if (Fluid != Fluids.Dry)
				return string.Format("{0} - {1}", Definition, Fluid);
			return Definition.ToString();
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
			stream.Write(TargetWarpID.OrEmpty());
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

	public enum BoardType
	{
		Wild, Town, Dungeon, Special
	}

	public enum Realms
	{
		Nox, Seradevari,
	}

	public partial class Board : TokenCarrier
	{
		public static int GeneratorCount = 0;
		public static string HackishBoardTypeThing = "wild";

		public int BoardNum { get; set; }

		public int Lifetime { get; set; }
		public string Name { get { return GetToken("name").Text; } set { GetToken("name").Text = value; } }
		public string ID { get { return GetToken("id").Text; } set { GetToken("id").Text = value; } }
		public string Music { get { return GetToken("music").Text; } set { GetToken("music").Text = value; } }
		public BoardType BoardType { get { return (BoardType)GetToken("type").Value; } set { GetToken("type").Value = (float)value; } }
		public int ToNorth { get { return (int)GetToken("north").Value; } set { GetToken("north").Value = value; } }
		public int ToSouth { get { return (int)GetToken("south").Value; } set { GetToken("south").Value = value; } }
		public int ToEast { get { return (int)GetToken("east").Value; } set { GetToken("east").Value = value; } }
		public int ToWest { get { return (int)GetToken("west").Value; } set { GetToken("west").Value = value; } }
		public bool AllowTravel { get { return !HasToken("noTravel"); } set { RemoveToken("noTravel"); if (!value)  AddToken("noTravel"); } }
		public int Stock { get { return (int)Path("encounters/stock").Value; } set { Path("encounters/stock").Value = value; } }
		public Realms Realm { get { return (Realms)GetToken("realm").Value; } set { GetToken("realm").Value = (float)value; } }
		public Point Coordinate
		{
			get
			{
				var coordinate = GetToken("coordinate");
				if (coordinate != null)
					return new Point((int)coordinate.GetToken("x").Value, (int)coordinate.GetToken("y").Value);
				return default(Point);
			}
			set
			{
				var coordinate = GetToken("coordinate");
				if (coordinate == null)
				{
					coordinate = AddToken("coordinate");
					coordinate.AddToken("x", value.X);
					coordinate.AddToken("y", value.Y);
					return;
				}
				coordinate.GetToken("x").Value = value.X;
				coordinate.GetToken("y").Value = value.Y;
			}
		}
		public List<Entity> Entities { get; private set; }
		public List<Warp> Warps { get; private set; }
		public List<Point> DirtySpots { get; private set; }
		public List<Entity> EntitiesToRemove { get; private set; }
		public List<Entity> EntitiesToAdd { get; private set; }

		public Tile[,] Tilemap = new Tile[80, 50];
		public bool[,] Lightmap = new bool[50, 80];

		public Dictionary<string, Rectangle> Sectors { get; private set; }
		public List<Point> ExitPossibilities { get; private set; }

		public override string ToString()
		{
			return string.Format("#{0} {1} - \"{2}\"", BoardNum, ID, Name);
		}

		public Board()
		{
			foreach (var t in new[] { "name", "id", "type", "music", "realm", "biome", "encounters" })
				this.AddToken(t);
			this.AddToken("culture", 0, "human");
			this.GetToken("encounters").AddToken("stock", 0);
			foreach (var t in new[] { "north", "south", "east", "west" })
				this.AddToken(t, -1, string.Empty);
			this.Entities = new List<Entity>();
			this.EntitiesToRemove = new List<Entity>();
			this.EntitiesToAdd = new List<Entity>();
			this.Warps = new List<Warp>();
			this.Sectors = new Dictionary<string, Rectangle>();
			this.DirtySpots = new List<Point>();
			for (int row = 0; row < 50; row++)
				for (int col = 0; col < 80; col++)
					this.Tilemap[col, row] = new Tile();
		}

		public void Flush()
		{
			Program.WriteLine("Flushing board {0}.", ID);
			var me = NoxicoGame.Me.Boards.FindIndex(x => x == this);
			CleanUpSlimeTrails();
			SaveToFile(me);
			NoxicoGame.Me.Boards[me] = null;
		}

		public void SaveToFile(int index)
		{
			var realm = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "boards");
			if (!Directory.Exists(realm))
				Directory.CreateDirectory(realm);
			//Program.WriteLine(" * Saving board {0}...", Name);
			using (var stream = new BinaryWriter(File.Open(System.IO.Path.Combine(realm, "Board" + index + ".brd"), FileMode.Create)))
			{
				Toolkit.SaveExpectation(stream, "BORD");
				Toolkit.SaveExpectation(stream, "TOKS");
				stream.Write(Tokens.Count);
				Tokens.ForEach(x => x.SaveToFile(stream));

				Toolkit.SaveExpectation(stream, "AMNT");
				stream.Write(Sectors.Count);
				stream.Write(Entities.OfType<BoardChar>().Count() - Entities.OfType<Player>().Count());
				stream.Write(Entities.OfType<DroppedItem>().Count());
				stream.Write(Entities.OfType<Clutter>().Count());
				stream.Write(Entities.OfType<Container>().Count());
				stream.Write(Entities.OfType<Door>().Count());
				stream.Write(Warps.Count);

				Toolkit.SaveExpectation(stream, "TMAP");
				for (int row = 0; row < 50; row++)
					for (int col = 0; col < 80; col++)
						Tilemap[col, row].SaveToFile(stream);

				Toolkit.SaveExpectation(stream, "SECT");
				foreach (var sector in Sectors)
				{
					stream.Write(sector.Key);
					sector.Value.SaveToFile(stream);
				}

				Toolkit.SaveExpectation(stream, "ENTT");
				foreach (var e in Entities.OfType<BoardChar>())
					if (e is Player)
						continue;
					else
						e.SaveToFile(stream);
				foreach (var e in Entities.OfType<DroppedItem>())
					e.SaveToFile(stream);
				foreach (var e in Entities.OfType<Clutter>())
					e.SaveToFile(stream);
				foreach (var e in Entities.OfType<Container>())
					e.SaveToFile(stream);
				foreach (var e in Entities.OfType<Door>())
					e.SaveToFile(stream);

				Toolkit.SaveExpectation(stream, "WARP");
				Warps.ForEach(x => x.SaveToFile(stream));
			}
		}

		public static Board LoadFromFile(int index)
		{
			var realm = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "boards");
			var file = System.IO.Path.Combine(realm, "Board" + index + ".brd");
			if (!File.Exists(file))
				throw new FileNotFoundException("Board #" + index + " not found!");
			var newBoard = new Board();
			using (var stream = new BinaryReader(File.Open(file, FileMode.Open)))
			{
				Toolkit.ExpectFromFile(stream, "BORD", "board description");
				Toolkit.ExpectFromFile(stream, "TOKS", "board token tree");
				var numTokens = stream.ReadInt32();
				newBoard.Tokens.Clear();
				for (var i = 0; i < numTokens; i++)
					newBoard.Tokens.Add(Token.LoadFromFile(stream));
				newBoard.Name = newBoard.GetToken("name").Text;
				newBoard.BoardType = (BoardType)newBoard.GetToken("type").Value;
				if (newBoard.HasToken("music") && newBoard.GetToken("music").Text == "-")
					newBoard.RemoveToken("music");
				if (!newBoard.HasToken("music"))
				{
					var music = BiomeData.Biomes[(int)newBoard.GetToken("biome").Value].Music;
					if (newBoard.BoardType == BoardType.Town)
						music = "set://Town";
					else if (newBoard.BoardType == BoardType.Town)
						music = "set://Dungeon";
					newBoard.AddToken("music", music);
				}
				newBoard.Music = newBoard.GetToken("music").Text;

				Toolkit.ExpectFromFile(stream, "AMNT", "board part amounts");
				var secCt = stream.ReadInt32();
				//var botCt = stream.ReadInt32();
				var chrCt = stream.ReadInt32();
				var drpCt = stream.ReadInt32();
				var cltCt = stream.ReadInt32();
				var conCt = stream.ReadInt32();
				var dorCt = stream.ReadInt32();
				var wrpCt = stream.ReadInt32();

				Toolkit.ExpectFromFile(stream, "TMAP", "tile map");
				for (int row = 0; row < 50; row++)
					for (int col = 0; col < 80; col++)
						newBoard.Tilemap[col, row].LoadFromFile(stream);

				Toolkit.ExpectFromFile(stream, "SECT", "sector");
				for (int i = 0; i < secCt; i++)
				{
					var secName = stream.ReadString();
					newBoard.Sectors.Add(secName, Rectangle.LoadFromFile(stream));
				}

				Toolkit.ExpectFromFile(stream, "ENTT", "board entity");
				//Unlike in SaveToFile, there's no need to worry about the player because that one's handled on the world level.
				Clutter.ParentBoardHack = newBoard;
				for (int i = 0; i < chrCt; i++)
					newBoard.Entities.Add(BoardChar.LoadFromFile(stream));
				for (int i = 0; i < drpCt; i++)
					newBoard.Entities.Add(DroppedItem.LoadFromFile(stream));
				for (int i = 0; i < cltCt; i++)
					newBoard.Entities.Add(Clutter.LoadFromFile(stream));
				for (int i = 0; i < conCt; i++)
					newBoard.Entities.Add(Container.LoadFromFile(stream));
				for (int i = 0; i < dorCt; i++)
					newBoard.Entities.Add(Door.LoadFromFile(stream));

				Toolkit.ExpectFromFile(stream, "WARP", "board warp");
				for (int i = 0; i < wrpCt; i++)
					newBoard.Warps.Add(Warp.LoadFromFile(stream));

				//Quick hack, doesn't really warrant another version bump.
				if (newBoard.Path("encounters/stock") == null)
					newBoard.GetToken("encounters").AddToken("stock", newBoard.GetToken("encounters").Value * 2);

				newBoard.RespawnEncounters();
				newBoard.CleanUpSlimeTrails();
				newBoard.CleanUpCorpses();

				newBoard.BindEntities();

				foreach (var e in newBoard.Entities.OfType<BoardChar>())
					e.RunScript(e.OnLoad);

				foreach (var e in newBoard.Entities.OfType<Door>())
					e.UpdateMapSolidity();

				newBoard.UpdateLightmap(null, true);
				newBoard.CheckCombatStart();

				//Program.WriteLine(" * Loaded board {0}...", newBoard.Name);
			}
			return newBoard;
		}

		private void CleanUpCorpses()
		{
			foreach (var corpse in Entities.OfType<Container>().Where(x => x.Token.HasToken("corpse")))
				if (Random.NextDouble() > 0.7)
					this.EntitiesToRemove.Add(corpse);
		}

		public bool IsSolid(int row, int col, SolidityCheck check = SolidityCheck.Walker)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			if (check == SolidityCheck.Walker && Tilemap[col, row].SolidToWalker)
				return true;
			else if (check == SolidityCheck.DryWalker && Tilemap[col, row].SolidToDryWalker)
				return true;
			else if (check == SolidityCheck.Flyer && Tilemap[col, row].SolidToFlyer)
				return true;
			else if (check == SolidityCheck.Projectile && Tilemap[col, row].SolidToProjectile)
				return true;
			return Tilemap[col, row].Definition.Wall;
		}

		public bool IsBurning(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			if (Tilemap[col, row].Fluid != Fluids.Dry)
				return false;
			if (!Tilemap[col, row].Definition.CanBurn)
				return false;
			return Tilemap[col, row].BurnTimer > 0;
		}

		public bool IsWater(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			return Tilemap[col, row].Fluid != Fluids.Dry && !Tilemap[col, row].Shallow;
		}

		public bool IsLit(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			return Lightmap[row, col];
		}

		public bool IsSeen(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			return Tilemap[col, row].Seen;
		}

		public string GetDescription(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			var tileDef = Tilemap[col, row].Definition;
			if (tileDef.IsVariableWall)
				return TileDefinition.Find(tileDef.VariableWall, true).Description;
			return Tilemap[col, row].Definition.Description;
		}

		public string GetName(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			return Tilemap[col, row].Definition.Name;
		}

		public void SetTile(int row, int col, int index)
		{
			Tilemap[col, row].Index = index;
			DirtySpots.Add(new Point(col, row));
		}
		public void SetTile(int row, int col, string tileName)
		{
			SetTile(row, col, TileDefinition.Find(tileName).Index);
		}
		public void SetTile(int row, int col, TileDefinition def)	
		{
			if (def == null)
				return;
			SetTile(row, col, def.Index);
		}

		public void Immolate(int row, int col)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			var tile = Tilemap[col, row];
			if (tile.Definition.CanBurn && tile.Fluid == Fluids.Dry)
			{
				tile.BurnTimer = Random.Next(20, 23) * 100;
				DirtySpots.Add(new Point(col, row));
			}
		}

		public void TrailSlime(int row, int col, Color color)
		{
			if (col >= 80)
				col = 79;
			if (col < 0)
				col = 0;
			if (row >= 50)
				row = 49;
			if (row < 0)
				row = 0;
			var tile = Tilemap[col, row];
			if (tile.Fluid == Fluids.Dry)
			{
				tile.Fluid = Fluids.Slime;
				tile.SlimeColor = color.Darken(1.4);
				tile.Shallow = true;
				tile.BurnTimer = (Random.Next(0, 4) * 10) + 100;
				DirtySpots.Add(new Point(row, col));
			}
		}

		public void BindEntities()
		{
			foreach (var entity in this.Entities)
				entity.ParentBoard = this;
		}

		public void Burn(bool spread)
		{
			//var flameColors = new[] { Color.Yellow, Color.Red, Color.Brown, Color.Maroon };
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					if (Tilemap[col, row].BurnTimer > 0)
					{
						if (Tilemap[col, row].Fluid == Fluids.Dry)
						{
							//Tilemap[col, row].Foreground = Color.FromArgb(Random.Next(20, 25) * 10, Random.Next(5, 25) * 10, 0); //flameColors[Randomizer.Next(flameColors.Length)];
							//Tilemap[col, row].Background = Color.FromArgb(Random.Next(20, 25) * 10, Random.Next(5, 25) * 10, 0); //flameColors[Randomizer.Next(flameColors.Length)];
							DirtySpots.Add(new Point(col, row));
							if (!spread)
								continue;
							Tilemap[col, row].BurnTimer--;
							if (Tilemap[col, row].BurnTimer == 0)
							{
								Tilemap[col, row].Definition = TileDefinition.Find("ash");
								DirtySpots.Add(new Point(col, row));
							}
							else if (Tilemap[col, row].BurnTimer == 10)
							{
								Immolate(row - 1, col);
								Immolate(row, col - 1);
								Immolate(row + 1, col);
								Immolate(row, col + 1);
								if (Random.Next(100) > 50)
								{
									Immolate(row - 1, col - 1);
									Immolate(row - 1, col + 1);
									Immolate(row + 1, col - 1);
									Immolate(row + 1, col + 1);
								}
							}
						}
						else if (Tilemap[col, row].Fluid == Fluids.Slime && Tilemap[col, row].Shallow)
						{
							Tilemap[col, row].BurnTimer--;
							if (Tilemap[col, row].BurnTimer == 0)
							{
								Tilemap[col, row].Fluid = Fluids.Dry;
								DirtySpots.Add(new Point(col, row));
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

					if (NoxicoGame.Me.Player.Character.Health <= 0)
						return;
				}
				if (!surrounding && BoardType != BoardType.Dungeon)
					UpdateSurroundings();
				Burn(true);
				return;
			}

			Burn(false);

			foreach (var entity in this.Entities.Where(x => x.Passive))
				entity.Update();
			if (NoxicoGame.Me.CurrentBoard == this)
				NoxicoGame.Me.Player.Update();
			if (EntitiesToRemove.Count > 0)
			{
				EntitiesToRemove.ForEach(x => { if (x is BoardChar) { ((BoardChar)x).Character.ResetEquipmentCarries(); } });
				EntitiesToRemove.ForEach(x => { Entities.Remove(x); this.DirtySpots.Add(new Point(x.XPosition, x.YPosition)); });
				EntitiesToRemove.Clear();
			}
			if (EntitiesToAdd.Count > 0)
			{
				foreach (var entity in EntitiesToAdd)
				{
					entity.ParentBoard = this;
					Entities.Add(entity);
				}
				//Entities.AddRange(EntitiesToAdd);
				EntitiesToAdd.Clear();
			}
		}

		public void UpdateSurroundings()
		{
			var nox = NoxicoGame.Me;
			if (this != nox.CurrentBoard)
				return;
			if (this.ToNorth > -1)
				nox.GetBoard(this.ToNorth).Update(true, true);
			if (this.ToSouth > -1)
				nox.GetBoard(this.ToSouth).Update(true, true);
			if (this.ToEast > -1)
				nox.GetBoard(this.ToEast).Update(true, true);
			if (this.ToWest > -1)
				nox.GetBoard(this.ToWest).Update(true, true);

			//Handle the NorthWest corner through either North or West.
			if (this.ToNorth > -1 || this.ToWest > -1)
			{
				if (this.ToNorth > -1 && nox.GetBoard(this.ToNorth).ToWest > -1)
					nox.GetBoard(nox.GetBoard(this.ToNorth).ToWest).Update(true, true);
				else if (this.ToWest > -1 && nox.GetBoard(this.ToWest).ToNorth > -1)
					nox.GetBoard(nox.GetBoard(this.ToWest).ToNorth).Update(true, true);
			}
			//Similar for other corners
			if (this.ToNorth > -1 || this.ToEast > -1)
			{
				if (this.ToNorth > -1 && nox.GetBoard(this.ToNorth).ToEast > -1)
					nox.GetBoard(nox.GetBoard(this.ToNorth).ToEast).Update(true, true);
				else if (this.ToEast > -1 && nox.GetBoard(this.ToEast).ToNorth > -1)
					nox.GetBoard(nox.GetBoard(this.ToEast).ToNorth).Update(true, true);
			}
			if (this.ToSouth > -1 || this.ToWest > -1)
			{
				if (this.ToSouth > -1 && nox.GetBoard(this.ToSouth).ToWest > -1)
					nox.GetBoard(nox.GetBoard(this.ToSouth).ToWest).Update(true, true);
				else if (this.ToWest > -1 && nox.GetBoard(this.ToWest).ToSouth > -1)
					nox.GetBoard(nox.GetBoard(this.ToWest).ToSouth).Update(true, true);
			}
			if (this.ToSouth > -1 || this.ToEast > -1)
			{
				if (this.ToSouth > -1 && nox.GetBoard(this.ToSouth).ToEast > -1)
					nox.GetBoard(nox.GetBoard(this.ToSouth).ToEast).Update(true, true);
				else if (this.ToEast > -1 && nox.GetBoard(this.ToEast).ToSouth > -1)
					nox.GetBoard(nox.GetBoard(this.ToEast).ToSouth).Update(true, true);
			}
		}

		public void AimCamera()
		{
			AimCamera(NoxicoGame.Me.Player.XPosition, NoxicoGame.Me.Player.YPosition);
		}

		public void AimCamera(int x, int y)
		{
			//Program.WriteLine("AimCamera({0}, {1})", x, y);
			var oldCamY = NoxicoGame.CameraY;
			NoxicoGame.CameraY = y - 12;
			if (NoxicoGame.CameraY < 0)
				NoxicoGame.CameraY = 0;
			if (NoxicoGame.CameraY > 25)
				NoxicoGame.CameraY = 25;
			//Program.WriteLine("AimCamera: old {0}, new {1}", oldCamY, NoxicoGame.CameraY);
			if (oldCamY < NoxicoGame.CameraY) //went down
				Redraw();
			else if (oldCamY > NoxicoGame.CameraY) //went up
				Redraw();
		}

		public void Redraw()
		{
			for (int row = 0; row < 50; row++)
				for (int col = 0; col < 80; col++)
					DirtySpots.Add(new Point(col, row));
		}

		public void Draw(bool force = false)
		{
			var waterGlyphs = new[] { 0, 0x157, 0x146, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB };
			var waterColors = new[] { Color.Black, Color.Navy, Color.FromCSS("B22222"), Color.Black, Color.Red, Color.White, Color.Black, Color.Black };
			foreach (var l in this.DirtySpots)
			{
				var localX = l.X - NoxicoGame.CameraX;
				var localY = l.Y - NoxicoGame.CameraY;
				if (localX >= 80 || localY >= 25 || localX < 0 || localY < 0)
					continue;
				var t = this.Tilemap[l.X, l.Y];
				var def = t.Definition;
				var glyph = def.Glyph;
				var fore = ((MainForm)NoxicoGame.HostForm).IsMultiColor ? def.MultiForeground : def.Foreground;
				var back = def.Background;
				if (t.Fluid != Fluids.Dry)
				{
					glyph = waterGlyphs[(int)t.Fluid];
					fore = waterColors[(int)t.Fluid];
					if (t.Fluid == Fluids.Slime)
						fore = t.SlimeColor;
					back = fore.Darken();
				}
				if (t.InherentLight > 0)
				{
					fore = fore.LerpDarken(t.InherentLight / 12.0);
					back = back.LerpDarken(t.InherentLight / 12.0);
				}
				if (Lightmap[l.Y, l.X])
					NoxicoGame.HostForm.SetCell(localY, localX, glyph, fore, back, force);
				else if (t.Seen)
					NoxicoGame.HostForm.SetCell(localY, localX, glyph, fore.Night(), back.Night(), force);
				else
					NoxicoGame.HostForm.SetCell(localY, localX, ' ', Color.Black, Color.Black, force);
			}
			this.DirtySpots.Clear();

			foreach (var entity in this.Entities.OfType<Door>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<Clutter>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<Container>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<DroppedItem>())
				entity.Draw();
			foreach (var entity in this.Entities.OfType<BoardChar>())
				entity.Draw();
		}

		public void UpdateLightmap(Entity source, bool torches)
		{
			if ((source != null && source is BoardChar && ((BoardChar)source).Character.Path("eyes/glow") != null) || (!HasToken("dark") && !Toolkit.IsNight()))
			{
				for (int row = 0; row < 50; row++)
					for (int col = 0; col < 80; col++)
						Lightmap[row, col] = Tilemap[col, row].Seen = true;
				return;
			}
			
			var previousMap = new bool[50, 80];
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					previousMap[row, col] = Lightmap[row, col];
					Lightmap[row, col] = false;
				}
			}

			var radius = source != null ? ((BoardChar)source).SightRadius : 10;
			var doingTorches = false;
			Func<int, int, bool> f = (x1, y1) =>
			{
				if (y1 < 0 || y1 >= 50 | x1 < 0 || x1 >= 80)
					return true;
				if (!doingTorches)
					Tilemap[x1, y1].Seen = true;
				return Tilemap[x1, y1].SolidToProjectile;
			};
			Action<int, int> a = (x2, y2) =>
			{
				if (y2 < 0 || y2 >= 50 | x2 < 0 || x2 >= 80)
					return;
				Lightmap[y2, x2] = true;
			};

			if (source != null)
				SilverlightShadowCasting.ShadowCaster.ComputeFieldOfViewWithShadowCasting(source.XPosition, source.YPosition, radius, f, a);
			if (torches)
			{
				doingTorches = true;
				foreach (var light in Entities.OfType<Clutter>().Where(x => x.ID.Contains("Torch")))
				{
					SilverlightShadowCasting.ShadowCaster.ComputeFieldOfViewWithShadowCasting(light.XPosition, light.YPosition, 5, f, a);
				}
			}

			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					if (Lightmap[row, col] != previousMap[row, col])
						DirtySpots.Add(new Point(col, row));
				}
			}
		}

		public static Board CreateBasicOverworldBoard(int biomeID, string id, string name, string music)
		{
			var newBoard = new Board();
			newBoard.Clear(biomeID);
			newBoard.Tokenize("name: \"" + name + "\"\nid: \"" + id + "\"\ntype: 3\nbiome: " + biomeID + "\nmusic: \"" + music + "\"\nrealm: 0\nencounters: 0\n\tstock: 0\nnorth: -1\nsouth: -1\neast: -1\nwest: -1\n");
			newBoard.ID = id;
			newBoard.Name = name;
			newBoard.Music = music;
			newBoard.AddClutter();
			return newBoard;
		}

		[System.Diagnostics.Conditional("DEBUG")]
		public void DumpToHtml(string suffix)
		{
			if (!suffix.IsBlank() && !suffix.StartsWith('_'))
				suffix = '_' + suffix;
			var file = new StreamWriter("Board-" + ID + suffix + ".html");
			file.WriteLine("<!DOCTYPE html>");
			file.WriteLine("<html>");
			file.WriteLine("<head>");
			file.WriteLine("<meta http-equiv=\"Content-Type\" content=\"text/html; CHARSET=utf-8\" />");
			file.WriteLine("<h1>Noxico board data dump</h1>");
			file.WriteLine("<p>");
			file.WriteLine("\tName: {0}<br />", Name);
			file.WriteLine("\tID: {0}<br />", ID);
			file.WriteLine("\tType: {0}<br />", BoardType);
			file.WriteLine("\tBiome: {0}<br />", BiomeData.Biomes[(int)GetToken("biome").Value].Name);
			file.WriteLine("\tCulture: {0}<br />", HasToken("culture") ? GetToken("culture").Text : "&lt;none&gt;");
			file.WriteLine("\tMusic: {0}<br />", Music);
			file.WriteLine("</p>");
			file.WriteLine("<pre>");
			file.WriteLine(DumpTokens(Tokens, 0));
			file.WriteLine("</pre>");
			file.WriteLine("<h2>Screendump</h2>");
			CreateHtmlScreenshot(file, true);

			if (BoardType == BoardType.Dungeon || BoardType == BoardType.Wild)
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
					file.WriteLine("<h4 id=\"{1}_{2}x{3}\">{0}</h4>", bc.Character.IsProperNamed ? bc.Character.Name.ToString(true) : bc.Character.Title, bc.ID, bc.XPosition, bc.YPosition);
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
					file.WriteLine("<h4 id=\"{3}_{1}x{2}\">{0} at {1}x{2}</h4>", c.Name, c.XPosition, c.YPosition, c.ID);
					file.WriteLine("<pre>");
					file.WriteLine(c.Token.DumpTokens(c.Token.Tokens, 0));
					file.WriteLine("</pre>");
				}
			}
			if (Entities.OfType<Clutter>().Count() > 0)
			{
				file.WriteLine("<h3>Clutter</h3>");
				foreach (var c in Entities.OfType<Clutter>())
				{
					file.WriteLine("<h4 id=\"{3}_{1}x{2}\">{0} at {1}x{2}</h4>", c.Name, c.XPosition, c.YPosition, c.ID);
					file.WriteLine("<pre>");
					file.WriteLine("ID: {0}", c.ID);
					file.WriteLine("Description: {0}", c.Description);
					file.WriteLine("</pre>");
				}
			}
			if (Entities.OfType<DroppedItem>().Count() > 0)
			{
				file.WriteLine("<h3>DroppedItem</h3>");
				foreach (var c in Entities.OfType<DroppedItem>())
				{
					file.WriteLine("<h4 id=\"{3}_{1}x{2}\">{0} at {1}x{2}</h4>", c.Name, c.XPosition, c.YPosition, c.ID);
					file.WriteLine("<pre>Item:");
					file.WriteLine(c.Item.DumpTokens(c.Item.Tokens, 0));
					file.WriteLine("DroppedItem:");
					file.WriteLine(c.Token.DumpTokens(c.Token.Tokens, 0));
					file.WriteLine("</pre>");
				}
			}
			file.Flush();
			file.Close();
		}

		public void RespawnEncounters()
		{
			if (GetToken("encounters").Value == 0 || BoardType != BoardType.Dungeon && BoardType != BoardType.Wild)
				return;
			if (Stock == 0)
				return;
			var encData = GetToken("encounters");
			var count = Entities.OfType<BoardChar>().Count();
			var toAdd = (int)encData.Value - count;
			if (toAdd <= 0)
				return;
			if (toAdd > Stock)
				toAdd = Stock;
			Board.HackishBoardTypeThing = this.BoardType.ToString().ToLowerInvariant();
			for (var i = 0; i < toAdd; i++)
			{
				var bodyplan = Toolkit.PickOne(encData.Tokens.Select(x => x.Name).Where(x => x != "stock").ToArray());
				var newb = new BoardChar(Character.Generate(bodyplan, Gender.RollDice, Gender.RollDice, this.Realm))
				{
					ParentBoard = this,
				};
				newb.XPosition = Random.Next(2, 78);
				newb.YPosition = Random.Next(2, 23);
				var lives = 100;
				while (IsSolid(newb.YPosition, newb.XPosition, SolidityCheck.DryWalker) && lives > 0)
				{
					lives--;
					newb.XPosition = Random.Next(2, 78);
					newb.YPosition = Random.Next(2, 23);
				}
				if (lives == 0)
					continue;
				newb.Character.Health = 12 * Random.Next(3);
				newb.Character.AddToken("hostile");
				//Arming the character was removed -- lootsets applied in character creation covered that much better.
				newb.AdjustView();
				this.Entities.Add(newb);
			}
			Stock -= toAdd;
		}

		private void CleanUpSlimeTrails()
		{
			foreach (var c in this.Entities.OfType<Clutter>().Where(c => c.Life > 0))
				this.EntitiesToRemove.Add(c);
		}

		public static Board GetBoardByNumber(int index)
		{
			return NoxicoGame.Me.GetBoard(index);
		}

		public void MakeTarget()
		{
			if (Name.IsBlank())
				throw new Exception("Board must have a name before it can be added to the target list.");
			if (NoxicoGame.TravelTargets.ContainsKey(BoardNum))
				return; //throw new Exception("Board is already a travel target.");
			NoxicoGame.TravelTargets.Add(BoardNum, Name);
		}

		public static int FindTargetBoardByName(string name)
		{
			if (!NoxicoGame.TravelTargets.ContainsValue(name))
				return -1;
			var i = NoxicoGame.TravelTargets.First(b => b.Value == name);
			return i.Key;
		}

		public static Board PickBoard(BoardType boardType, int biome, int maxWater, object realm)
		{
			var r = Realms.Nox;
			if (realm == null)
			{
				if (NoxicoGame.Me.CurrentBoard != null)
					r = NoxicoGame.Me.CurrentBoard.Realm;
			}
			else
				r = (Realms)realm;
			var options = new List<Board>();
		tryAgain:
			foreach (var board in NoxicoGame.Me.Boards)
			{
				if (board == null)
					continue;
				if (board.Realm != r)
					continue;
				if (board.BoardType != boardType)
					continue;
				if (biome > 0 && board.GetToken("biome").Value != biome)
					continue;
				if (board.GetToken("biome").Value == 0 || board.GetToken("biome").Value == 9)
					continue;
				if (maxWater != -1)
				{
					var water = 0;
					for (var y = 0; y < 50; y++)
						for (var x = 0; x < 80; x++)
							if (board.Tilemap[x, y].Fluid != Fluids.Dry)
								water++;
					if (water > maxWater)
						continue;
				}
				options.Add(board);
			}
			if (options.Count == 0)
			{
				if (maxWater < 2000)
				{
					maxWater *= 2;
					goto tryAgain;
				}
				else
					return null;
			}
			var choice = options.PickOne();
			return choice;
		}

		public BoardChar PickBoardChar(Gender gender)
		{
			Func<BoardChar, bool> isOkay = (x) => { return true; };
			if (gender != Gender.Invisible)
				isOkay = (x) =>
					{
						return (x.Character.Gender == gender);
					};
			var options = this.Entities.OfType<BoardChar>().Where(e => !(e is Player) && isOkay(e)).ToList();
			if (options.Count == 0)
				return null;
			var choice = options[Random.Next(options.Count)];
			return choice;
		}

		public List<Entity> GetEntitiesWith(string ending, bool includeBoardChars)
		{
			var ret = new List<Entity>();
			foreach (var entity in this.Entities.Where(x => x.ID.EndsWith(ending)))
			{
				if (!includeBoardChars && entity is BoardChar)
					continue;
				ret.Add(entity);
			}
			return ret;
		}
		public BoardChar GetFirstBoardCharWith(string ending)
		{
			var ret = this.Entities.FirstOrDefault(x => x is BoardChar && x.ID.EndsWith(ending));
			return (BoardChar)ret;
		}

		public bool SectorContains(string sector, int x, int y)
		{
			if (sector.IsBlank() || Sectors.Count == 0)
				return true;
			if (!Sectors.ContainsKey(sector))
				return false;
			var s = Sectors[sector];
			if (s.Left >= x && s.Right <= x && s.Top >= y && s.Bottom <= y)
				return true;
			return false;
		}

		public void Connect(Direction dir, Board target)
		{
			if (target == null)
				return;
			switch (dir)
			{
				case Direction.North:
					this.ToNorth = target.BoardNum;
					target.ToSouth = this.BoardNum;
					break;
				case Direction.South:
					this.ToSouth = target.BoardNum;
					target.ToNorth = this.BoardNum;
					break;
				case Direction.East:
					this.ToEast = target.BoardNum;
					target.ToWest = this.BoardNum;
					break;
				case Direction.West:
					this.ToWest = target.BoardNum;
					target.ToEast = this.BoardNum;
					break;
			}
		}

		public void CheckCombatFinish()
		{
			if (!this.HasToken("combat"))
			{
				return;
			}
			foreach (var x in Entities.OfType<BoardChar>())
			{
				if (EntitiesToRemove.Contains(x))
					continue;
				if (x.Character.HasToken("hostile"))
					return; //leave the combat rolling
			}
			if (this.HasToken("combat"))
				this.AddToken("victorious");
			this.RemoveToken("combat");
			PlayMusic();
		}

		public void CheckCombatStart()
		{
			if (this.HasToken("combat"))
			{
				return;
			}
			foreach (var x in Entities.OfType<BoardChar>())
			{
				if (x.Character.HasToken("hostile"))
				{
					this.AddToken("combat");
					PlayMusic();
					return;
				}
			}
		}

		public void CreateHtmlScreenshot(StreamWriter stream, bool linked)
		{
			var waterGlyphs = new[] { 0, 0x157, 0x146, 0xDB, 0xDB, 0xDB, 0xDB, 0xDB };
			var waterColors = new[] { Color.Black, Color.Navy, Color.FromCSS("B22222"), Color.Black, Color.Red, Color.White, Color.Black, Color.Black };

			stream.WriteLine("<table style=\"font-family: Unifont, monospace; cursor: default;\" cellspacing=0 cellpadding=0>");
			for (int row = 0; row < 50; row++)
			{
				stream.WriteLine("\t<tr>");
				for (int col = 0; col < 80; col++)
				{
					var tile = Tilemap[col, row];
					var def = tile.Definition;
					var back = string.Format("rgb({0},{1},{2})", def.Background.R, def.Background.G, def.Background.B);
					var fore = string.Format("rgb({0},{1},{2})", def.Foreground.R, def.Foreground.G, def.Foreground.B);
					if (tile.InherentLight > 0)
					{
						var newBack = def.Background.LerpDarken(Tilemap[col, row].InherentLight / 12.0);
						var newFore = def.Background.LerpDarken(Tilemap[col, row].InherentLight / 12.0);
						back = string.Format("rgb({0},{1},{2})", newBack.R, newBack.G, newBack.B);
						fore = string.Format("rgb({0},{1},{2})", newFore.R, newFore.G, newFore.B);
					}

					var chr = string.Format("&#x{0:X};", (int)NoxicoGame.IngameToUnicode[def.Glyph]);
					var tag = string.Empty; //string.Format("{0}", Tilemap[col, row].InherentLight);
					var link = string.Empty;

					if (tile.Fluid != Fluids.Dry)
					{
						chr = string.Format("&#x{0:X};", (int)NoxicoGame.IngameToUnicode[waterGlyphs[(int)tile.Fluid]]);
						var newFore = waterColors[(int)tile.Fluid];
						if (tile.Fluid == Fluids.Slime)
							newFore = tile.SlimeColor;
						var newBack = newFore.Darken();
						back = string.Format("rgb({0},{1},{2})", newBack.R, newBack.G, newBack.B);
						fore = string.Format("rgb({0},{1},{2})", newFore.R, newFore.G, newFore.B);
					}

					if (!def.Description.IsBlank())
						tag = def.Description;

					if (chr == "&#x20;")
						chr = "&nbsp;";

					var ent = Entities.LastOrDefault(x => x.XPosition == col && x.YPosition == row);
					if (ent != null)
					{
						back = string.Format("rgb({0},{1},{2})", ent.BackgroundColor.R, ent.BackgroundColor.G, ent.BackgroundColor.B);
						fore = string.Format("rgb({0},{1},{2})", ent.ForegroundColor.R, ent.ForegroundColor.G, ent.ForegroundColor.B);
						chr = string.Format("&#x{0:X};", (int)NoxicoGame.IngameToUnicode[ent.Glyph]);
						tag = ent.ID;
						if (ent is BoardChar)
						{
							tag = ((BoardChar)ent).Character.Name.ToString(true);
							if (linked)
								link = string.Format("<a href=\"#{0}_{1}x{2}\" style=\"color: {3};\">", ent.ID, ent.XPosition, ent.YPosition, fore);
						}
						else if (ent is Container)
						{
							tag = ((Container)ent).Name;
							if (linked)
								link = string.Format("<a href=\"#{0}_{1}x{2}\" style=\"color: {3};\">", ent.ID, ent.XPosition, ent.YPosition, fore);
						}
						else if (ent is Clutter)
						{
							if (linked)
								link = string.Format("<a href=\"#{0}_{1}x{2}\" style=\"color: {3};\">", ent.ID, ent.XPosition, ent.YPosition, fore);
						}
						else if (ent is DroppedItem)
						{
							tag = ((DroppedItem)ent).Name;
							if (linked)
								link = string.Format("<a href=\"#{0}_{1}x{2}\" style=\"color: {3};\">", ent.ID, ent.XPosition, ent.YPosition, fore);
						}
					}
					if (!tag.IsBlank())
						tag = " title=\"" + tag + "\"";

					stream.WriteLine("\t\t<td style=\"background: {0}; color: {1};\"{3}>{4}{2}{5}</td>", back, fore, chr, tag, link, link.IsBlank(string.Empty, "</a>"));
					//DirtySpots.Add(new Location(col, row));
				}
				stream.WriteLine("</tr>");
			}
			stream.WriteLine("</table>");
		}

		public void PlayMusic()
		{
			if (this.HasToken("victorious"))
			{
				this.RemoveToken("victorious");
				NoxicoGame.Sound.PlayMusic("set://Victory", false);
			}
			else if (this.HasToken("combat"))
				NoxicoGame.Sound.PlayMusic("set://Combat", false);
			else
				NoxicoGame.Sound.PlayMusic(this.Music ?? "-");
		}

		public void LoadSurroundings()
		{
			var nox = NoxicoGame.Me;
			UpdateLightmap(nox.Player, true);
			if (this.ToNorth > -1 && nox.Boards[this.ToNorth] == null)
				nox.GetBoard(this.ToNorth);
			if (this.ToSouth > -1 && nox.Boards[this.ToSouth] == null)
				nox.GetBoard(this.ToSouth);
			if (this.ToEast > -1 && nox.Boards[this.ToEast] == null)
				nox.GetBoard(this.ToEast);
			if (this.ToWest > -1 && nox.Boards[this.ToWest] == null)
				nox.GetBoard(this.ToWest);
		}

		public void ResolveVariableWalls()
		{
			var newTile = new System.Text.StringBuilder();
			for (int row = 0; row < 50; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					var def = Tilemap[col, row].Definition;
					if (def.IsVariableWall)
					{
						var baseName = def.Name.Replace("Top", string.Empty).Replace("Bottom", string.Empty).Replace("Left", string.Empty).Replace("Right", string.Empty).Replace("Joiner", string.Empty);
						newTile.Clear();
						newTile.Append(baseName);
						if (row > 0 && (Tilemap[col, row - 1].Definition.Name.StartsWith(baseName) || Tilemap[col, row - 1].Definition.Name.StartsWith("doorway")))
							newTile.Append("Top");
						if (row < 49 && (Tilemap[col, row + 1].Definition.Name.StartsWith(baseName) || Tilemap[col, row + 1].Definition.Name.StartsWith("doorway")))
							newTile.Append("Bottom");
						if (col > 0 && (Tilemap[col - 1, row].Definition.Name.StartsWith(baseName) || Tilemap[col - 1, row].Definition.Name.StartsWith("doorway")))
							newTile.Append("Left");
						if (col < 79 && (Tilemap[col + 1, row].Definition.Name.StartsWith(baseName) || Tilemap[col + 1, row].Definition.Name.StartsWith("doorway")))
							newTile.Append("Right");
						var newDef = TileDefinition.Find(newTile.ToString());
						if (newDef.Index == 0)
							Console.WriteLine("Couldn't find \"{0}\".", newTile);
						Tilemap[col, row].Index = newDef.Index;
						//SetTile(row, col, newTile.ToString());
					}
				}
			}
		}

		public Entity PlaceEntity(Entity e, int x, int y)
		{
			if (e.ParentBoard != null)
			{
				e.ParentBoard.EntitiesToRemove.Add(e);
				e.ParentBoard = null;
			}
			e.XPosition = x;
			e.YPosition = y;
			e.ParentBoard = this;
			this.EntitiesToAdd.Add(e);
			return e;
		}

		public BoardChar PlaceCharacter(Character ch, int x, int y)
		{
			return (BoardChar)PlaceEntity(new BoardChar(ch), x, y);
		}
	}


}
