using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if DEBUG
using Keys = System.Windows.Forms.Keys;
#endif

namespace Noxico
{
    public enum Direction
    {
        North, East, South, West,
    }

	public enum Motor
	{
		Stand, Wander, WanderSector, Hunt, //...
	}

	/*
    public class Location
    {
        public int X, Y;
        public Location(int x, int y)
        {
            X = x;
            Y = y;
        }
    }
	*/

    public class Entity
    {
        public Board ParentBoard { get; set; }
       public string ID { get; set; }
#if DEBUG
		[System.ComponentModel.Editor(typeof(GlyphSelector), typeof(System.Drawing.Design.UITypeEditor))]
#endif
		public int Glyph { get; set; }
		public Color ForegroundColor { get; set; }
        public Color BackgroundColor { get; set; }
        public int XPosition { get; set; }
        public int YPosition { get; set; }
		///<summary>For Entities, indicates whether a BoardChar can walk into this Entity's Tile. For BoardChars, indicates whether other BoardChars can switch positions into this Tile.</summary>
		public bool Blocking { get; set; }
		public bool Passive { get; set; }
		public int Energy { get; set; }

		public Entity()
		{
			ID = "[null]";
			Glyph = '?';
			this.Energy = Random.Next(4000, 5000);
		}

		public virtual void Draw()
		{
			var localX = this.XPosition - NoxicoGame.CameraX;
			var localY = this.YPosition - NoxicoGame.CameraY;
			if (localX >= 80 || localY >= 20 || localX < 0 || localY < 0)
				return;
			var b = ((MainForm)NoxicoGame.HostForm).IsMultiColor ? TileDefinition.Find(this.ParentBoard.Tilemap[this.XPosition, this.YPosition].Index, true).Background : this.BackgroundColor;
			if (ParentBoard.IsLit(this.YPosition, this.XPosition))
				NoxicoGame.HostForm.SetCell(localY, localX, this.Glyph, this.ForegroundColor, b);
			//else
			//	NoxicoGame.HostForm.SetCell(localY, localX, this.Glyph, this.ForegroundColor.Night(), b.Night());
		}

		public virtual void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
        {
            var touched = this.CanMove(targetDirection, check);
			if (touched is Door)
			{
				var door = touched as Door;
				if (door.Locked)
					return;
				if (door.Closed)
				{
					if (this is Player)
						NoxicoGame.Sound.PlaySound("set://DoorOpen");
					Energy -= 500;
					door.Closed = false;
				}
			}
			else if (touched != null)
            {
                return;
            }
			if (XPosition >= 0 && YPosition >= 0 && XPosition < 80 && YPosition < 50)
	            this.ParentBoard.DirtySpots.Add(new Point(XPosition, YPosition));
			var newX = 0;
			var newY = 0;
			Toolkit.PredictLocation(XPosition, YPosition, targetDirection, ref newX, ref newY);
			XPosition = newX;
			YPosition = newY;
        }

		public virtual object CanMove(Board board, int x, int y, SolidityCheck check = SolidityCheck.Walker)
		{
			if (x < 0 || y < 0 || x > 79 || y > 49)
				return false;

			foreach (var entity in board.Entities)
			{
				if (entity == this)
					continue;
				if (entity.XPosition == x && entity.YPosition == y)
				{
					if (entity is Door && ((Door)entity).Closed)
						return entity as Door;
					if (entity.Blocking)
						return entity;
				}
			}

			if (board.IsSolid(y, x, check))
				return false;

			return null;
		}

		public virtual object CanMove(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
        {
            var newX = this.XPosition;
            var newY = this.YPosition;
			Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
			return CanMove(this.ParentBoard, newX, newY, check);
        }
 
		public virtual void Update()
		{
        }

		public virtual void SaveToFile(BinaryWriter stream)
		{
			//Program.WriteLine("   * Saving {0} {1}...", this.GetType(), ID ?? "????");
			Toolkit.SaveExpectation(stream, "ENTT");
			stream.Write(ID ?? "<Null>");
			stream.Write((char)Glyph);
			BackgroundColor.SaveToFile(stream);
			ForegroundColor.SaveToFile(stream);
			stream.Write((byte)XPosition);
			stream.Write((byte)YPosition);
			stream.Write((byte)0); //was Flow
			stream.Write(Blocking);
		}

		public static Entity LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "ENTT", "entity");
			var newEntity = new Entity();
			newEntity.ID = stream.ReadString();
			newEntity.Glyph = stream.ReadChar();
			newEntity.BackgroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.ForegroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.XPosition = stream.ReadByte();
			newEntity.YPosition = stream.ReadByte();
			stream.ReadByte(); //was Flow
			newEntity.Blocking = stream.ReadBoolean();
			//Program.WriteLine("   * Loaded {0} {1}...", newEntity.GetType(), newEntity.ID ?? "????"); 
			return newEntity;
		}

		public int DistanceFrom(Entity other)
		{
			var dX = Math.Abs(this.XPosition - other.XPosition);
			var dY = Math.Abs(this.YPosition - other.YPosition);
			return (dX < dY) ? dY : dX;
		}

		public virtual bool CanSee(Entity other)
		{
			foreach (var point in Toolkit.Line(XPosition, YPosition, other.XPosition, other.YPosition))
				//if ((ParentBoard.IsSolid(point.Y, point.X) && !ParentBoard.IsWater(point.Y, point.X)) && ParentBoard.IsLit(point.Y, point.X))
				if (ParentBoard.IsSolid(point.Y, point.X, SolidityCheck.Projectile) && ParentBoard.IsLit(point.Y, point.X))
					return false;
			return true;
		}
	}

	public class Clutter : Entity
	{
		private static List<Token> clutterDB;
		public static Board ParentBoardHack { get; set; }

		public string Name { get; set; }
		public string Description { get; set; }
		public int Life { get; set; }
		public bool CanBurn { get; set; }
		public string DBRole { get; private set; }
		private bool descriptionFromDB;

		public Clutter()
		{
			this.Glyph = '?';
			this.ForegroundColor = Color.Silver;
			this.BackgroundColor = Color.Black;
		}

		public Clutter(char character, Color foreColor, Color backColor, bool blocking = false, string name = "thing", string description = "This is a thing.")
		{
			this.Glyph = character;
			this.ForegroundColor = foreColor;
			this.BackgroundColor = backColor;
			this.Blocking = blocking;
			this.Name = name;
			this.Description = description;
		}

		public override void Update()
		{
			base.Update();
			if (CanBurn && ParentBoard.IsBurning(YPosition, XPosition))
			{
				ParentBoard.EntitiesToRemove.Add(this);
				return;
			}
			if (Life > 0)
			{
				Life--;
				if (Life == 0)
				{
					ParentBoard.EntitiesToRemove.Add(this);
					return;
				}
			}
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			Console.WriteLine("Trying to move clutter.");
		}

		public static bool ResetToKnown(Entity thing)
		{
			if (!(thing is Clutter) && !(thing is Container))
				throw new InvalidCastException("ResetToKnown only takes Clutter and Container objects.");

			var name = string.Empty;
			if (thing is Clutter) name = ((Clutter)thing).Name;
			else if (thing is Container) name = ((Container)thing).Name;
			if (clutterDB == null)
				clutterDB = Mix.GetTokenTree("clutter.tml", true);
			var knownThing = clutterDB.FirstOrDefault(kc =>
				thing.Glyph == kc.GetToken("char").Value ||
				(!name.IsBlank() && name.Equals(kc.Text, StringComparison.InvariantCultureIgnoreCase)) ||
				thing.ID.StartsWith(kc.Text, StringComparison.InvariantCultureIgnoreCase));
			if (knownThing != null)
			{
				thing.Glyph = (int)knownThing.GetToken("char").Value;
				if (knownThing.HasToken("color"))
				{
					thing.ForegroundColor = Color.FromName(knownThing.GetToken("color").Text);
					thing.BackgroundColor = thing.ForegroundColor.Darken(); //TileDefinition.Find(parentBoardHack.Tilemap[e.XPosition, e.YPosition].Index).Background;
				}
				if (knownThing.HasToken("background"))
				{
					if (knownThing.GetToken("background").Text == "inherit")
						thing.BackgroundColor = TileDefinition.Find((thing.ParentBoard ?? ParentBoardHack).Tilemap[thing.XPosition, thing.YPosition].Index).Background;
					else
						thing.BackgroundColor = Color.FromName(knownThing.GetToken("background").Text);
				}
				if (thing is Clutter) ((Clutter)thing).CanBurn = knownThing.HasToken("canburn");
				//else if (thing is Container) ((Container)thing).CanBurn = knownClutter.HasToken("canburn");
				thing.Blocking = knownThing.HasToken("blocking");
				if (knownThing.HasToken("description"))
				{
					if (thing is Clutter)
					{
						((Clutter)thing).Description = knownThing.GetToken("description").Text;
						((Clutter)thing).descriptionFromDB = true;
					}
					else if (thing is Container && !((Container)thing).Token.HasToken("description"))
						((Container)thing).Token.AddToken("description", knownThing.GetToken("description").Text);
				}
				if (knownThing.HasToken("name"))
				{
					if (thing is Clutter) ((Clutter)thing).Name = knownThing.GetToken("name").Text;
					if (thing is Container) ((Container)thing).Name = knownThing.GetToken("name").Text;
				}
				if (knownThing.HasToken("role") && thing is Clutter) ((Clutter)thing).DBRole = knownThing.GetToken("role").Text;
				return true;
			}
			return false;
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "CLUT");
			base.SaveToFile(stream);
			stream.Write(Name ?? "");
			stream.Write(descriptionFromDB ? "" : (Description ?? ""));
			stream.Write(CanBurn);
			stream.Write(Life);
		}

		public static new Clutter LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "CLUT", "clutter entity");
			var e = Entity.LoadFromFile(stream);
			var newClutter = new Clutter()
			{
				ID = e.ID,
				Glyph = e.Glyph,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
				Blocking = e.Blocking
			};
			newClutter.Name = stream.ReadString();
			newClutter.Description = stream.ReadString();
			newClutter.CanBurn = stream.ReadBoolean();
			newClutter.Life = stream.ReadInt32();
			Clutter.ResetToKnown(newClutter);
			return newClutter;
		}

	}

	public class DroppedItem : Entity
	{
		public InventoryItem Item { get; set; }
		public Token Token { get; set; }
		public string Name
		{
			get
			{
				if (Item == null)
					return "???";
				return Item.ToString(Token);
			}
		}

		public DroppedItem(string item)
			: this(NoxicoGame.KnownItems.First(i => i.ID == item), null)
		{
		}

		public DroppedItem(InventoryItem item, Token carriedItem)
		{
			Item = item;
			Token = new Token(item.ID);
			if (carriedItem != null)
				Token.AddSet(carriedItem.Tokens);

			this.Glyph = '?';
			this.ForegroundColor = Color.Silver;
			this.BackgroundColor = Color.Black;
			this.Blocking = false;
			AdjustView();
		}

		public void AdjustView()
		{
			if (Item.HasToken("ascii"))
			{
				var ascii = Item.GetToken("ascii");
				if (ascii.HasToken("char"))
					this.Glyph = (char)ascii.GetToken("char").Value;
				if (Item.HasToken("colored") && Token.HasToken("color"))
					this.ForegroundColor = Color.FromName(Token.GetToken("color").Text);
				else if (ascii.HasToken("fore"))
					this.ForegroundColor = Color.FromName(ascii.GetToken("fore").Text);
				else if (Item.ID == "book" && Token.Tokens.Count > 0)
					this.ForegroundColor = Color.FromCGA(Token.GetToken("id").Text.GetHashCode() % 16);
				if (ascii.HasToken("back"))
					this.BackgroundColor = Color.FromName(ascii.GetToken("back").Tokens[0]);
				else
					this.BackgroundColor = this.ForegroundColor.Darken();
			}
		}

		public override void Update()
		{
			if (!Item.HasToken("fireproof") && ParentBoard.IsBurning(YPosition, XPosition))
				ParentBoard.EntitiesToRemove.Add(this);
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			Console.WriteLine("Trying to move dropped item.");
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "DROP");
			base.SaveToFile(stream);
			stream.Write(Item.ID);
			Token.SaveToFile(stream);
		}

		public static new DroppedItem LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "DROP", "dropped item entity");
			var e = Entity.LoadFromFile(stream);

			//Handle broken references (warhammer > war_hammer)
			var id = stream.ReadString();
			var exists = NoxicoGame.KnownItems.Any(ki => ki.ID == id);
			if (!exists)
			{
				var attempt = NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID.Replace("_", "") == id);
				if (attempt != null)
					id = attempt.ID;
				else
					id = NoxicoGame.KnownItems[0].ID; //Fallback.
			}
				

			var newItem = new DroppedItem(id)
			{
				ID = e.ID,
				Glyph = e.Glyph,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
			};
			newItem.Token = Token.LoadFromFile(stream);
			return newItem;
		}

		public void Take(Character taker, Board ParentBoard)
		{
			if (!taker.HasToken("items"))
				taker.Tokens.Add(new Noxico.Token() { Name = "items" });
			taker.GetToken("items").Tokens.Add(Token);
			taker.CheckHasteSlow();
            ParentBoard.EntitiesToRemove.Add(this);
        }

		public static List<DroppedItem> GetItemsAt(Board board, int x, int y)
		{
			return new List<DroppedItem>(board.Entities.OfType<DroppedItem>().Where(drop => drop.XPosition == x && drop.YPosition == y));
		}

		public static void PickItemsFrom(List<DroppedItem> items)
		{
			var itemDict = new Dictionary<object, string>();
			foreach (var item in items)
			{
				itemDict.Add(item, item.Name);
			}
			itemDict.Add(-1, i18n.GetString("action_pickup_cancel")); //"...nothing"
			ActionList.Show(i18n.GetString("action_pickup_window") /* "Pick up..." */, items[0].XPosition, items[0].YPosition, itemDict,
				() =>
				{
					if (ActionList.Answer is int && (int)ActionList.Answer == -1)
					{
						//Cancelled.
						return;
					}
					var drop = ActionList.Answer as DroppedItem;
					var player = NoxicoGame.Me.Player;
					var item = drop.Item;
					var token = drop.Token;
					drop.Take(player.Character, player.ParentBoard);
					player.Energy -= 1000;
					NoxicoGame.AddMessage(i18n.Format("youpickup_x", item.ToString(token, true)), drop.ForegroundColor);
					NoxicoGame.Sound.PlaySound("set://GetItem"); 
					player.ParentBoard.Redraw();
				}
			);
		}
	}

	public class Container : Entity
	{
		public string Description
		{
			get
			{
				if (this.Token.HasToken("description") && !this.Token.GetToken("description").Text.IsBlank())
					return this.Token.GetToken("description").Text;
				return i18n.GetString("generic_description");
			}
		}

		public Token Token { get; set; }

		public string Name
		{
			get
			{
				return Token.Text.IsBlank("container", Token.Text);
			}
			set
			{
				Token.Text = value;
			}
		}

		public Container(string name, List<Token> contents)
		{
			Token = new Token();
			Token.Text = name;
			var c = new Token("contents");
			if (contents != null)
				c.AddSet(contents);
			Token.Tokens.Add(c);
			Blocking = false;
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "CONT");
			base.SaveToFile(stream);
			Token.SaveToFile(stream);
		}

		public static new Container LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "CONT", "container entity");
			var e = Entity.LoadFromFile(stream);
			var newContainer = new Container("", null)
			{
				ID = e.ID,
				Glyph = e.Glyph,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
			};
			newContainer.Token = Token.LoadFromFile(stream);
			Clutter.ResetToKnown(newContainer);
			return newContainer;
		}

		public void AdjustView()
		{
			if (Token.HasToken("ascii"))
			{
				var ascii = Token.GetToken("ascii");
				if (ascii.HasToken("char"))
					this.Glyph = (char)ascii.GetToken("char").Value;
				if (ascii.HasToken("fore"))
					this.ForegroundColor = Color.FromName(ascii.GetToken("fore").Tokens[0]);
				if (ascii.HasToken("back"))
					this.BackgroundColor = Color.FromName(ascii.GetToken("back").Tokens[0]);
				else
					this.BackgroundColor = this.ForegroundColor.Darken();
			}
		}
	}

	public class Door : Entity
	{

		public int KeyIndex { get; set; }
		public bool Locked { get; set; }
		public bool Closed
		{
			get { return closed; }
			set { closed = value; UpdateMapSolidity(); }
		}
		private bool closed;
		private bool horizontal;
		private bool dirInited;
		private int closeTimer;

		private void FindDirection()
		{
			horizontal = ParentBoard.IsSolid(YPosition, XPosition - 1) && ParentBoard.IsSolid(YPosition, XPosition + 1);
			dirInited = true;
		}

		public override void Draw()
		{
			if (!dirInited)
				FindDirection();
			if (closed)
				Glyph = horizontal ? 0x152 : 0x154;
			else
				Glyph = horizontal ? 0x153 : 0x155;
			base.Draw();
		}

		public void UpdateMapSolidity()
		{
			if (ParentBoard == null)
				return;
			ParentBoard.Tilemap[XPosition, YPosition].Definition = TileDefinition.Find(closed ? "doorwayClosed" : "doorwayOpened");
		}

		public override void Update()
		{
			if (!closed)
			{
				closeTimer++;
				if (closeTimer > 20)
				{
					Closed = true;
					closeTimer = 0;
				}
			}
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "DOOR");
			base.SaveToFile(stream);
			stream.Write(KeyIndex);
			stream.Write(true /* Closed */);
			stream.Write(Locked);
		}

		public static new Door LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "DOOR", "door entity");
			var e = Entity.LoadFromFile(stream);
			var newDoor = new Door()
			{
				ID = e.ID,
				Glyph = e.Glyph,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
				Blocking = e.Blocking
			};
			newDoor.KeyIndex = stream.ReadInt32();
			newDoor.Closed = stream.ReadBoolean();
			newDoor.Locked = stream.ReadBoolean();
			return newDoor;
		}
	}
}