using System;
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
	/// The poor man's System.Drawing.Rectangle.
	/// </summary>
	public struct Rectangle
	{
		public int Left { get; set; }
		public int Top { get; set; }
		public int Right { get; set; }
		public int Bottom { get; set; }
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
		Walker, Flyer, Projectile, Swimmer
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
	public partial class Tile
	{
		public char Character { get; set; }
		public Color Foreground { get; set; }
		public Color Background { get; set; }
		public bool Wall { get; set; }
		public bool Water { get; set; }
		public bool Ceiling { get; set; }
		public bool Cliff { get; set; }
		public bool Fence { get; set; }
		public bool Grate { get; set; }
		public bool CanBurn { get; set; }
		public int BurnTimer { get; set; }
		public int SpecialDescription { get; set; }

		public bool SolidToWalker { get { return Wall || Water || Fence || Cliff; } }
		public bool SolidToFlyer { get { return Ceiling || Wall; } }
		public bool SolidToProjectile { get { return (Wall && !Grate); } }
		public bool SolidToSwimmer { get { return Wall || Fence || Cliff; } }

		/// <summary>
		/// Returns a TileDescription if this tile has one.
		/// </summary>
		/// <returns></returns>
		public TileDescription? GetDescription()
		{
			if (SpecialDescription == 0)
				return null;
			if (SpecialDescription > NoxicoGame.TileDescriptions.Length)
				return null;
			var tsd = NoxicoGame.TileDescriptions[SpecialDescription];
			var name = tsd.Substring(0, tsd.IndexOf(':'));
			var desc = tsd.Substring(tsd.IndexOf(':') + 1).Trim();
			return new TileDescription() { Name = name, Description = desc, Color = Foreground.Lightness < 0.5 ? Background : Foreground  };
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Character);
			Foreground.SaveToFile(stream);
			Background.SaveToFile(stream);

			var bits = new BitVector32();
			bits[1] = CanBurn;
			bits[2] = Wall;
			bits[4] = Water;
			bits[8] = Ceiling;
			bits[16] = Cliff;
			bits[32] = (BurnTimer > 0);
			bits[64] = (SpecialDescription > 0);
			bits[128] = (Fence || Grate); //Has more settings
			stream.Write((byte)bits.Data);
			if (bits[128])
			{
				bits = new BitVector32();
				bits[1] = Fence;
				bits[2] = Grate;
				//rest reserved.
				stream.Write((byte)bits.Data);
			}
			if (BurnTimer > 0)
				stream.Write((byte)BurnTimer);
			if (SpecialDescription > 0)
				stream.Write((Int16)SpecialDescription);
		}

		public void LoadFromFile(BinaryReader stream)
		{
			Character = stream.ReadChar();
			Foreground = Toolkit.LoadColorFromFile(stream);
			Background = Toolkit.LoadColorFromFile(stream);

			var set = stream.ReadByte();
			var bits = new BitVector32(set);
			CanBurn = bits[1];
			Wall = bits[2];
			Water = bits[4];
			Ceiling = bits[8];
			Cliff = bits[16];
			var HasBurn = bits[32];
			var HasSpecialDescription = bits[64];
			var HasMoreSettings = bits[128];
			if (HasMoreSettings)
			{
				set = stream.ReadByte();
				bits = new BitVector32(set);
				Fence = bits[1];
				Grate = bits[2];
			}
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

	public partial class Board : TokenCarrier
	{
		public static int GeneratorCount = 0;

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
		public List<Entity> Entities { get; private set; }
		public List<Warp> Warps { get; private set; }
		public List<Location> DirtySpots { get; private set; }
		public List<Entity> EntitiesToRemove { get; private set; }
		public List<Entity> EntitiesToAdd { get; private set; }

		public Tile[,] Tilemap = new Tile[80, 25];
		public bool[,] Lightmap = new bool[25, 80];

		public Dictionary<string, Rectangle> Sectors { get; private set; }
		public List<Location> ExitPossibilities { get; private set; }

		public override string ToString()
		{
			return string.Format("#{0} {1} - \"{2}\"", BoardNum, ID, Name);
		}

		public Board()
		{
			foreach (var t in new[] { "name", "id", "music", "type", "biome", "encounters" })
				this.AddToken(t);
			foreach (var t in new[] { "north", "south", "east", "west" })
				this.AddToken(t, -1, string.Empty);
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
			var realm = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "boards");
			if (!Directory.Exists(realm))
				Directory.CreateDirectory(realm);
			//Console.WriteLine(" * Saving board {0}...", Name);
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
				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						Tilemap[col, row].SaveToFile(stream);

				Toolkit.SaveExpectation(stream, "SECT");
				foreach (var sector in Sectors)
				{
					//TODO: give sectors their own serialization function. For readability.
					stream.Write(sector.Key);
					stream.Write(sector.Value.Left);
					stream.Write(sector.Value.Top);
					stream.Write(sector.Value.Right);
					stream.Write(sector.Value.Bottom);
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
				newBoard.ID = newBoard.GetToken("id").Text;
				newBoard.Music = newBoard.GetToken("music").Text;
				newBoard.BoardType = (BoardType)newBoard.GetToken("type").Value;

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
				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						newBoard.Tilemap[col, row].LoadFromFile(stream);

				Toolkit.ExpectFromFile(stream, "SECT", "sector");
				for (int i = 0; i < secCt; i++)
				{
					var secName = stream.ReadString();
					var l = stream.ReadInt32();
					var t = stream.ReadInt32();
					var r = stream.ReadInt32();
					var b = stream.ReadInt32();
					newBoard.Sectors.Add(secName, new Rectangle() { Left = l, Top = t, Right = r, Bottom = b });
				}

				Toolkit.ExpectFromFile(stream, "ENTT", "board entity");
				//Unlike in SaveToFile, there's no need to worry about the player because that one's handled on the world level.
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

				//Console.WriteLine(" * Loaded board {0}...", newBoard.Name);
			}
			return newBoard;
		}

		private void CleanUpCorpses()
		{
			foreach (var corpse in Entities.OfType<Clutter>().Where(x => x.Name.EndsWith("'s remains")))
				if (Random.NextDouble() > 0.7)
					this.EntitiesToRemove.Add(corpse);
		}

		public bool IsSolid(int row, int col, SolidityCheck check = SolidityCheck.Walker)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return true;
			if (check == SolidityCheck.Walker && Tilemap[col, row].SolidToWalker)
				return true;
			else if (check == SolidityCheck.Flyer && Tilemap[col, row].SolidToFlyer)
				return true;
			else if (check == SolidityCheck.Projectile && Tilemap[col, row].SolidToProjectile)
				return true;
			else if (check == SolidityCheck.Swimmer && Tilemap[col, row].SolidToSwimmer)
				return true;
			return Tilemap[col, row].Wall;
		}

		public bool IsBurning(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return false;
			return Tilemap[col, row].BurnTimer > 0 && Tilemap[col, row].CanBurn;
		}

		public bool IsWater(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return false;
			return Tilemap[col, row].Water;
		}

		public bool IsLit(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return false;
			return Lightmap[row, col];
		}

		public TileDescription? GetSpecialDescription(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return null;
			return Tilemap[col, row].GetDescription();
		}

		public void SetTile(int row, int col, char tile, Color foreColor, Color backColor, bool wall = false, bool burn = false, bool water = false, bool cliff = false)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			var t = new Tile()
			{
				Character = tile,
				Foreground = foreColor,
				Background = backColor,
				Wall = wall,
				Water = water,
				Cliff = cliff,
				CanBurn = burn,
			};
			Tilemap[col, row] = t;
			DirtySpots.Add(new Location(col, row));
		}

		public void Immolate(int row, int col)
		{
			if (col >= 80 || row >= 25 || col < 0 || row < 0)
				return;
			var tile = Tilemap[col, row];
			if (tile.CanBurn && !tile.Water)
			{
				tile.BurnTimer = Random.Next(20, 23);
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
				ForegroundColor = color.Darken(2 + Random.NextDouble()).Darken(),
				BackgroundColor = color.Darken(1.4),
				AsciiChar = (char)Random.Next(0xB0, 0xB3),
				Blocking = false,
				XPosition = col,
				YPosition = row,
				Life = 5 + Random.Next(10),
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
						Tilemap[col, row].Foreground = Color.FromArgb(Random.Next(20, 25) * 10, Random.Next(5, 25) * 10, 0); //flameColors[Randomizer.Next(flameColors.Length)];
						Tilemap[col, row].Background = Color.FromArgb(Random.Next(20, 25) * 10, Random.Next(5, 25) * 10, 0);//flameColors[Randomizer.Next(flameColors.Length)];
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
							if (Random.Next(100) > 50)
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
				if (!surrounding && BoardType != BoardType.Dungeon)
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
			if (this.ToNorth > -1)
				nox.GetBoard(this.ToNorth).Update(true, true);
			if (this.ToSouth > -1)
				nox.GetBoard(this.ToNorth).Update(true, true);
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

		public void Redraw()
		{
			//HACK!
			if (SceneSystem.LeavingDream)
			{
				SceneSystem.LeavingDream = false;
				SceneSystem.Dreaming = false;
				NoxicoGame.Sound.PlayMusic(this.Music);
			}

			for (int row = 0; row < 25; row++)
				for (int col = 0; col < 80; col++)
					DirtySpots.Add(new Location(col, row));
		}

		public void Draw(bool force = false)
		{
			foreach (var l in this.DirtySpots)
			{
				var t = this.Tilemap[l.X, l.Y];
				if (Lightmap[l.Y, l.X])
					NoxicoGame.HostForm.SetCell(l.Y, l.X, t.Character, t.Foreground, t.Background, force);
				else
					NoxicoGame.HostForm.SetCell(l.Y, l.X, t.Character, t.Foreground.Darken(), t.Background.Darken(), force);
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
				for (int row = 0; row < 25; row++)
					for (int col = 0; col < 80; col++)
						Lightmap[row, col] = true;
				return;
			}

			var previousMap = new bool[25, 80];
			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					previousMap[row, col] = Lightmap[row, col];
					Lightmap[row, col] = false;
				}
			}

			Func<int, int, bool> f = (x1, y1) =>
			{
				if (y1 < 0 || y1 >= 25 | x1 < 0 || x1 >= 80)
					return true;
				return Tilemap[x1, y1].SolidToProjectile;
				//return !Tilemap[x1, y1].IsWater && Tilemap[x1, y1].Solid;
			};
			Action<int, int> a = (x2, y2) =>
			{
				if (y2 < 0 || y2 >= 25 | x2 < 0 || x2 >= 80)
					return;
				Lightmap[y2, x2] = true;
			};

			if (source != null)
				SilverlightShadowCasting.ShadowCaster.ComputeFieldOfViewWithShadowCasting(source.XPosition, source.YPosition, 10, f, a);

			if (torches)
			{
				foreach (var light in Entities.OfType<Clutter>().Where(x => x.ID.Contains("Torch")))
				{
					SilverlightShadowCasting.ShadowCaster.ComputeFieldOfViewWithShadowCasting(light.XPosition, light.YPosition, 5, f, a);
				}
			}

			for (int row = 0; row < 25; row++)
			{
				for (int col = 0; col < 80; col++)
				{
					if (Lightmap[row, col] != previousMap[row, col])
						DirtySpots.Add(new Location(col, row));
				}
			}
		}

		public static Board CreateBasicOverworldBoard(int biomeID, string id, string name, string music)
		{
			var newBoard = new Board();
			newBoard.Clear(biomeID);
			newBoard.Tokenize("name: \"" + name + "\"\nid: \"" + id + "\"\nmusic: \"" + music + "\"\ntype: 3\nbiome: " + biomeID + "\nencounters: 0\n");
			newBoard.ID = id;
			newBoard.Name = name;
			newBoard.Music = music;
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
			file.WriteLine("\tType: {0}<br />", BoardType);
			file.WriteLine("\tBiome: {0}<br />", BiomeData.Biomes[(int)GetToken("biome").Value].Name);
			file.WriteLine("\tCulture: {0}<br />", HasToken("culture") ? GetToken("culture").Text : "&lt;none&gt;");
			file.WriteLine("</p>");
			file.WriteLine("<pre>");
			file.WriteLine(DumpTokens(Tokens, 0));
			file.WriteLine("</pre>");
			file.WriteLine("<h2>Screendump</h2>");
			CreateHTMLDump(file, true);

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
			if (GetToken("encounters").Value == 0 || BoardType != BoardType.Dungeon && BoardType != BoardType.Wild)
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
				newb.XPosition = Random.Next(2, 78);
				newb.YPosition = Random.Next(2, 23);
				var lives = 100;
				while (IsSolid(newb.YPosition, newb.XPosition) && lives > 0)
				{
					lives--;
					newb.XPosition = Random.Next(2, 78);
					newb.YPosition = Random.Next(2, 23);
				}
				if (lives == 0)
					continue;
				newb.Character.GetToken("health").Value = 12 * Random.Next(3);
				newb.Character.AddToken("hostile");
				//arm them
				if (!newb.Character.HasToken("beast"))
				{
					var items = newb.Character.Path("items");
					if (items == null)
						items = newb.Character.AddToken("items");
					var weapons = new[] { "dagger", "shortsword", "whip", "baseballbat" }; //TODO: make this cultural
					var w = Random.Next(1, 2);
					while (w > 0)
					{
						var weapon = Toolkit.PickOne(weapons);
						if (items.HasToken(weapon))
							continue;
						items.AddToken(weapon);
						w--;
					}
				}
				newb.AdjustView();
				this.Entities.Add(newb);
			}
		}

		private void CleanUpSlimeTrails()
		{
			foreach (var c in this.Entities.OfType<Clutter>().Where(c => c.Life > 0))
				this.EntitiesToRemove.Add(c);
		}
	
		[ForJS]
		public BoardChar PickBoardChar(Gender gender)
		{
			Func<BoardChar, bool> isOkay = (x) => { return true; };
			if (gender != Gender.Random)
				isOkay = (x) =>
					{
						var g = x.Character.GetGender();
						if (g == "male" && gender == Gender.Male ||
							g == "female" && gender == Gender.Female ||
							g == "hermaphrodite" && gender == Gender.Herm)
							return true;
						return false;
					};
			var options = this.Entities.OfType<BoardChar>().Where(e => !(e is Player) && isOkay(e)).ToList();
			if (options.Count == 0)
				return null;
			var choice = options[Random.Next(options.Count)];
			return choice;
		}

		[ForJS]
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

		public bool SectorContains(string sector, int x, int y)
		{
			if (string.IsNullOrWhiteSpace(sector) || Sectors.Count == 0)
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
				if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
					NoxicoGame.AutoRestSpeed = NoxicoGame.AutoRestExploreSpeed; 
				return;
			}
			foreach (var x in Entities.OfType<BoardChar>())
				if (x.Character.HasToken("hostile"))
					return; //leave the combat rolling
			this.RemoveToken("combat");
			if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
				NoxicoGame.AutoRestSpeed = NoxicoGame.AutoRestExploreSpeed;
		}

		public void CheckCombatStart()
		{
			if (this.HasToken("combat"))
			{
				if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
					NoxicoGame.AutoRestSpeed = NoxicoGame.AutoRestCombatSpeed;
				return;
			}
			foreach (var x in Entities.OfType<BoardChar>())
			{
				if (x.Character.HasToken("hostile"))
				{
					this.AddToken("combat");
					if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
						NoxicoGame.AutoRestSpeed = NoxicoGame.AutoRestCombatSpeed;
					return;
				}
			}
			if (NoxicoGame.HostForm.Noxico.CurrentBoard == this)
				NoxicoGame.AutoRestSpeed = NoxicoGame.AutoRestExploreSpeed;
		}

		public void CreateHTMLDump(StreamWriter stream, bool linked)
		{
			stream.WriteLine("<table style=\"font-family: monospace;\" cellspacing=0 cellpadding=0>");
			for (int row = 0; row < 25; row++)
			{
				stream.WriteLine("\t<tr>");
				for (int col = 0; col < 80; col++)
				{
					var tile = Tilemap[col, row];
					var back = string.Format("rgb({0},{1},{2})", tile.Background.R, tile.Background.G, tile.Background.B);
					var fore = string.Format("rgb({0},{1},{2})", tile.Foreground.R, tile.Foreground.G, tile.Foreground.B);
					var chr = string.Format("&#x{0:X};", (int)tile.Character);
					var tag = "";
					var link = "";

					if (chr == "&#x20;")
						chr = "&nbsp;";

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
							if (linked)
								link = "<a href=\"#" + ent.ID + "\" style=\"color: " + fore + ";\">";
						}
					}
					if (!string.IsNullOrWhiteSpace(tag))
						tag = " title=\"" + tag + "\"";

					stream.WriteLine("\t\t<td style=\"background: {0}; color: {1};\"{3}>{4}{2}{5}</td>", back, fore, chr, tag, link, string.IsNullOrWhiteSpace(link) ? "" : "</a>");
					//DirtySpots.Add(new Location(col, row));
				}
				stream.WriteLine("</tr>");
			}
			stream.WriteLine("</table>");
		}
	}


}
