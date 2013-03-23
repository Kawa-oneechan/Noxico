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

    public class Location
    {
        public int X, Y;
        public Location(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    public class Entity
    {
        public Board ParentBoard { get; set; }
        public string ID { get; set; }
        public char AsciiChar { get; set; }
        public Color ForegroundColor { get; set; }
        public Color BackgroundColor { get; set; }
        public int XPosition { get; set; }
        public int YPosition { get; set; }
        public Direction Flow { get; set; }
		public bool Blocking { get; set; }
		public bool Passive { get; set; }

		public Entity()
		{
			ID = "[null]";
		}

		public virtual void Draw()
		{
			if (ParentBoard.IsLit(this.YPosition, this.XPosition))
				NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, this.AsciiChar, this.ForegroundColor, this.BackgroundColor);
			else
				NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, this.AsciiChar, this.ForegroundColor.Darken(), this.BackgroundColor.Darken());
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
						NoxicoGame.Sound.PlaySound("Open Gate");
					door.Closed = false;
				}
			}
			else if (touched != null)
            {
                return;
            }
            this.ParentBoard.DirtySpots.Add(new Location(XPosition, YPosition));
			var newX = 0;
			var newY = 0;
			Toolkit.PredictLocation(XPosition, YPosition, targetDirection, ref newX, ref newY);
			XPosition = newX;
			YPosition = newY;
			Flow = targetDirection;
        }

		public virtual object CanMove(Board board, int x, int y, SolidityCheck check = SolidityCheck.Walker)
		{
			if (x < 0 || y < 0 || x > 79 || y > 24)
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
			// TODO: make asynchronous scripts unblock if needed.
			//if (this.Script != null && this.Script.Length > 0)
			//	RunCycle();
        }

		public virtual void SaveToFile(BinaryWriter stream)
		{
			//Program.WriteLine("   * Saving {0} {1}...", this.GetType(), ID ?? "????");
			Toolkit.SaveExpectation(stream, "ENTT");
			stream.Write(ID ?? "<Null>");
			stream.Write(AsciiChar);
			BackgroundColor.SaveToFile(stream);
			ForegroundColor.SaveToFile(stream);
			stream.Write((byte)XPosition);
			stream.Write((byte)YPosition);
			stream.Write((byte)Flow);
			stream.Write(Blocking);
		}

		public static Entity LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "ENTT", "entity");
			var newEntity = new Entity();
			newEntity.ID = stream.ReadString();
			newEntity.AsciiChar = stream.ReadChar();
			newEntity.BackgroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.ForegroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.XPosition = stream.ReadByte();
			newEntity.YPosition = stream.ReadByte();
			newEntity.Flow = (Direction)stream.ReadByte();
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
		public string Name { get; set; }
		public string Description { get; set; }
		public int Life { get; set; }
		public bool CanBurn { get; set; }

		public Clutter()
		{
			this.AsciiChar = '?';
			this.ForegroundColor = Color.Silver;
			this.BackgroundColor = Color.Black;
		}

		public Clutter(char character, Color foreColor, Color backColor, bool blocking = false, string name = "thing", string description = "This is a thing.")
		{
			this.AsciiChar = character;
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

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "CLUT");
			base.SaveToFile(stream);
			stream.Write(Name ?? "");
			stream.Write(Description ?? "");
			stream.Write(CanBurn);
			stream.Write(Life);
		}

		public static new Clutter LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "CLUT", "clutter entity");
			var e = Entity.LoadFromFile(stream);
			var newDress = new Clutter()
			{
				ID = e.ID,
				AsciiChar = e.AsciiChar,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
				Blocking = e.Blocking
			};
			newDress.Name = stream.ReadString();
			newDress.Description = stream.ReadString();
			newDress.CanBurn = stream.ReadBoolean();
			newDress.Life = stream.ReadInt32();
			return newDress;
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

			this.AsciiChar = '?';
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
					this.AsciiChar = (char)ascii.GetToken("char").Value;
				if (ascii.HasToken("fore"))
					this.ForegroundColor = Color.FromName(ascii.GetToken("fore").Tokens[0]);
				else if (Item.ID == "book" && Token.Tokens.Count > 0)
				{
					var cga = new[] { "Black", "DarkBlue", "DarkGreen", "DarkCyan", "DarkRed", "Purple", "Brown", "Silver", "Gray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White" };
					this.ForegroundColor = Color.FromName(cga[(int)Token.GetToken("id").Value % cga.Length]);
				}
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
			var newItem = new DroppedItem(stream.ReadString())
			{
				ID = e.ID,
				AsciiChar = e.AsciiChar,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
			};
			newItem.Token = Token.LoadFromFile(stream);
			return newItem;
		}

		public void Take(Character taker)
		{
			if (!taker.HasToken("items"))
				taker.Tokens.Add(new Noxico.Token() { Name = "items" });
			taker.GetToken("items").Tokens.Add(Token);
			ParentBoard.EntitiesToRemove.Add(this);
			taker.CheckHasteSlow();
		}
	}

	public class Container : Entity
	{
		public Token Token { get; set; }

		public string Name
		{
			get
			{
				if (string.IsNullOrWhiteSpace(Token.Text))
					return "container";
				else
					return Token.Text;
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
				AsciiChar = e.AsciiChar,
				ForegroundColor = e.ForegroundColor,
				BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition,
				YPosition = e.YPosition,
			};
			newContainer.Token = Token.LoadFromFile(stream);
			return newContainer;
		}

		public void AdjustView()
		{
			if (Token.HasToken("ascii"))
			{
				var ascii = Token.GetToken("ascii");
				if (ascii.HasToken("char"))
					this.AsciiChar = (char)ascii.GetToken("char").Value;
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
			horizontal = ParentBoard.IsSolid(YPosition - 1, XPosition) && ParentBoard.IsSolid(YPosition + 1, XPosition);
			dirInited = true;
		}

		public override void Draw()
		{
			if (!dirInited)
				FindDirection();
			if (closed)
				AsciiChar = '+';
			else
				AsciiChar = horizontal ? '|' : '-';
			base.Draw();
		}

		public void UpdateMapSolidity()
		{
			if (ParentBoard == null)
				return;
			ParentBoard.Tilemap[XPosition, YPosition].Wall = closed;
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
				AsciiChar = e.AsciiChar,
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