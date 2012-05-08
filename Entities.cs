using System;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Drawing;

namespace Noxico
{
    public enum Direction
    {
        North, East, South, West,
    }

	public enum Motor
	{
		Stand, Wander, WanderSector, Hunt, Sexytimes, //...
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

    public partial class Entity
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

		public string[] Script { get; set; }
		public int ScriptPointer { get; set; }
		public bool ScriptRunning { get; set; }
		public int ScriptDelay { get; set; }

		public Entity()
		{
			Script = new string[0];
			ID = "[null]";
		}

		public virtual void Draw()
		{
			NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, this.AsciiChar, this.ForegroundColor, this.BackgroundColor);
		}

		public virtual void Move(Direction targetDirection)
        {
            var touched = this.CanMove(targetDirection);
            if (touched != null)
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
        public virtual object CanMove(Direction targetDirection)
        {
            var newX = this.XPosition;
            var newY = this.YPosition;
			Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
            if (newX < 0 || newY < 0 || newX > 79 || newY > 24)
                return false;

			if (ParentBoard.IsSolid(newY, newX))
				return false;

            foreach (var entity in this.ParentBoard.Entities)
            {
                if (entity == this)
                    continue;
                if (entity.XPosition == newX && entity.YPosition == newY && entity.Blocking)
                    return entity;
            }
            return null;
        }
        public virtual void Update()
		{
			if (this.Script != null && this.Script.Length > 0)
				RunCycle();
        }

        public Direction Opposite(Direction current)
        {
            if (current == Direction.North)
                return Direction.South;
            else if (current == Direction.East)
                return Direction.West;
            else if (current == Direction.South)
                return Direction.North;
            else if (current == Direction.West)
                return Direction.East;
            return Direction.North;
        }

		public virtual void SaveToFile(BinaryWriter stream)
		{
			Console.WriteLine("   * Saving {0} {1}...", this.GetType(), ID ?? "????");
			stream.Write(ID ?? "<Null>");
			stream.Write(AsciiChar);
			BackgroundColor.SaveToFile(stream);
			ForegroundColor.SaveToFile(stream);
			stream.Write((byte)XPosition);
			stream.Write((byte)YPosition);
			stream.Write((byte)Flow);
			stream.Write(Blocking);
			stream.Write(ScriptPointer);
			stream.Write(ScriptRunning);
			stream.Write((Int16)ScriptDelay);
			stream.Write((Int16)Script.Length);
			foreach (var line in Script)
				stream.Write(line);
		}

		public static Entity LoadFromFile(BinaryReader stream)
		{
			var newEntity = new Entity();
			newEntity.ID = stream.ReadString();
			newEntity.AsciiChar = stream.ReadChar();
			newEntity.BackgroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.ForegroundColor = Toolkit.LoadColorFromFile(stream);
			newEntity.XPosition = stream.ReadByte();
			newEntity.YPosition = stream.ReadByte();
			newEntity.Flow = (Direction)stream.ReadByte();
			newEntity.Blocking = stream.ReadBoolean();
			newEntity.ScriptPointer = stream.ReadInt32();
			newEntity.ScriptRunning = stream.ReadBoolean();
			newEntity.ScriptDelay = stream.ReadInt16();
			var numLines = stream.ReadInt16();
			newEntity.Script = new string[numLines];
			for (int i = 0; i < numLines; i++)
				newEntity.Script[i] = stream.ReadString();
			Console.WriteLine("   * Loaded {0} {1}...", newEntity.GetType(), newEntity.ID ?? "????"); 
			return newEntity;
		}

		public int DistanceFrom(Entity other)
		{
			var dX = Math.Abs(this.XPosition - other.XPosition);
			var dY = Math.Abs(this.YPosition - other.YPosition);
			return (dX < dY) ? dY : dX;
		}

		public bool CanSee(Entity other)
		{
			foreach (var point in Toolkit.Line(XPosition, YPosition, other.XPosition, other.YPosition))
				if (ParentBoard.IsSolid(point.Y, point.X))
					return false;
			return true;
		}

		public void LoadScript(string script)
		{
			this.Script = script.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			this.ScriptPointer = 0;
			//this.ScriptRunning = true;
		}

		public void CallScript(string label)
		{
			NoxicoGame.HostForm.Text = ID + ": " + label;
			if (this.Script == null || this.Script.Length == 0)
				return;
			for (var i = 0; i < this.Script.Length; i++)
			{
				if (this.Script[i] == label + ":")
				{
					this.ScriptPointer = i;
					this.ScriptRunning = true;
					return;
				}
			}
		}

		partial void RunCycle();
	}

	public class Cursor : Entity
	{
		public enum Intents { Look, Take, Chat, Fuck };

		private static int BlinkRate = 500;
		public int Range { get; set; }
		public Intents Intent { get; set; }

		public Entity PointingAt { get; private set; }

		public Cursor()
		{
			this.AsciiChar = '\u25CA';
			this.BackgroundColor = Color.Black;
			this.ForegroundColor = Color.White;
			this.Range = 0;
			this.Intent = Intents.Look;
		}

		public override void Draw()
		{
			if (Environment.TickCount % BlinkRate * 2 < BlinkRate)
				base.Draw();
		}

		public override void Move(Direction targetDirection)
		{
			this.ParentBoard.DirtySpots.Add(new Location(XPosition, YPosition));
			if (CanMove(targetDirection) != null)
				return;
			var newX = 0;
			var newY = 0;
			Toolkit.PredictLocation(XPosition, YPosition, targetDirection, ref newX, ref newY);
			XPosition = newX;
			YPosition = newY;
			Point();
		}

		public override object CanMove(Direction targetDirection)
		{
			var newX = this.XPosition;
			var newY = this.YPosition;
			Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
			if (newX < 0 || newY < 0 || newX > 79 || newY > 24)
				return false;
			if (Range > 0)
			{
				var player = NoxicoGame.HostForm.Noxico.Player;
				if (Toolkit.Distance(newX, newY, player.XPosition, player.YPosition) >= Range)
					return false;
			}
			return null;
		}

		public void Point()
		{
			PointingAt = null;
			NoxicoGame.Messages.Last().Message = "Point at " + ((Intent == Intents.Look) ? "an object or character." : (Intent == Intents.Take) ? "an object." : "a character.");
			NoxicoGame.Messages.Last().Color = Color.Gray;
			foreach (var entity in this.ParentBoard.Entities)
			{
				if (entity.XPosition == XPosition && entity.YPosition == YPosition)
				{
					if (entity is BoardChar && Intent != Intents.Take)
					{
						PointingAt = entity;
						if (((BoardChar)PointingAt).Character.IsProperNamed)
							NoxicoGame.Messages.Last().Message = ((BoardChar)PointingAt).Character.GetName() + ", " + ((BoardChar)PointingAt).Character.GetTitle();
						else
							NoxicoGame.Messages.Last().Message = ((BoardChar)PointingAt).Character.GetTitle();
						NoxicoGame.Messages.Last().Color = PointingAt.ForegroundColor;
						return;
					}
					else if (entity is Clutter && Intent == Intents.Look)
					{
						PointingAt = entity;
						NoxicoGame.Messages.Last().Message = ((Clutter)PointingAt).Name;
						NoxicoGame.Messages.Last().Color = PointingAt.ForegroundColor;
						return;
					}
					else if (entity is DroppedItem && (Intent == Intents.Look || Intent == Intents.Take))
					{
						PointingAt = entity;
						NoxicoGame.Messages.Last().Message = ((DroppedItem)PointingAt).Name;
						NoxicoGame.Messages.Last().Color = PointingAt.ForegroundColor;
						return;
					}
				}
			}
			var tSD = this.ParentBoard.GetSpecialDescription(YPosition, XPosition);
			if (tSD.HasValue)
			{
				PointingAt = null;
				NoxicoGame.Messages.Last().Message = tSD.Value.Name;
				NoxicoGame.Messages.Last().Color = tSD.Value.Color;
				return;
			}
		}

		public override void Update()
		{
			base.Update();
			ParentBoard.Redraw();
			NoxicoGame.Messages.Last().New = true;
			NoxicoGame.UpdateMessages();

			if (NoxicoGame.KeyMap[(int)Keys.Escape])
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.Messages.Remove(NoxicoGame.Messages.Last());
				ParentBoard.Redraw();
			}

			if (NoxicoGame.KeyMap[(int)Keys.Enter])
			{
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.ClearKeys();
				var player = NoxicoGame.HostForm.Noxico.Player;
				if (PointingAt != null)
				{
					if (PointingAt is DroppedItem && (Intent == Intents.Look || Intent == Intents.Take))
					{
						var item = ((DroppedItem)PointingAt).Item;
						var token = ((DroppedItem)PointingAt).Token;
						if (Intent == Intents.Look)
						{
							var text = item.HasToken("description") && !token.HasToken("unidentified") ? item.GetToken("description").Text : "This is " + item.ToString() + ".";
							text = text.Trim();
							var lines = text.Split('\n').Length;
							MessageBox.Message(text, true);
						}
						else
						{
							((DroppedItem)PointingAt).PickUp(player.Character);
							NoxicoGame.AddMessage("You pick up " + item.ToString(token, true) + ".", ((DroppedItem)PointingAt).ForegroundColor);
							ParentBoard.Redraw();
							NoxicoGame.Mode = UserMode.Walkabout;
						}
					}
					else if (PointingAt is Clutter && Intent == Intents.Look)
					{
						var text = ((Clutter)PointingAt).Description;
						text = text.Trim();
						//var lines = text.Split('\n').Length;
						MessageBox.Message(text, true);
					}
					else if (PointingAt is BoardChar)
					{
						if (Intent == Intents.Look)
							TextScroller.LookAt((BoardChar)PointingAt);
						else if (Intent == Intents.Chat && player.CanSee(PointingAt))
							MessageBox.Ask("Strike a conversation with " + ((BoardChar)PointingAt).Character.GetName() + "?", () => { Dialogue.Engage(player.Character, ((BoardChar)PointingAt).Character); }, null, true);
						else if (Intent == Intents.Fuck && player.CanSee(PointingAt))
							MessageBox.Message("Can't fuck yet, sorry.", true);
					}
				}
				else if (Intent == Intents.Look)
				{
					var tSD = this.ParentBoard.GetSpecialDescription(YPosition, XPosition);
					if (tSD.HasValue)
					{
						PointingAt = null;
						MessageBox.Message(tSD.Value.Description, true); 
						return;
					}
				}
			}

#if DEBUG
			if (NoxicoGame.KeyMap[(int)Keys.D])
			{
				NoxicoGame.ClearKeys();
				if (PointingAt != null && PointingAt is BoardChar)
				{
					((BoardChar)PointingAt).Character.CreateInfoDump();
					NoxicoGame.AddMessage("Info for " + ((BoardChar)PointingAt).Character.GetName() + " dumped.", Color.Red);
				}
			}
#endif

			if (NoxicoGame.KeyMap[(int)Keys.Left])
				this.Move(Direction.West);
			else if (NoxicoGame.KeyMap[(int)Keys.Right])
				this.Move(Direction.East);
			else if (NoxicoGame.KeyMap[(int)Keys.Up])
				this.Move(Direction.North);
			else if (NoxicoGame.KeyMap[(int)Keys.Down])
				this.Move(Direction.South);
		}
	}

	public class BoardChar : Entity
	{
		public Motor Movement { get; set; }
		public string Sector { get; set; }
		public string Pairing { get; set; }
		private int MoveTimer;
		public int MoveSpeed { get; set; }

		public Dijkstra DijkstraMap { get; private set; }
		public Character Character { get; set; }

		public BoardChar()
		{
			this.AsciiChar = (char)255;
			this.ForegroundColor = Color.White;
			this.BackgroundColor = Color.Gray;
			this.Blocking = true;
			this.MoveSpeed = 2;

			this.DijkstraMap = new Dijkstra();
			this.DijkstraMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
		}

		public BoardChar(Character character) : this()
		{
			ID = character.Name.ToID();
			Character = character;
			this.Blocking = true;
			AdjustView();
		}

		public virtual void AdjustView()
		{
			var hS = Character.GetHumanScore();
			var gS = Character.GetGoblinScore();
			var dS = Character.GetDemonScore();
			AsciiChar = NoxicoGame.Views["human"];
			if (dS > hS && dS > gS)
				AsciiChar = NoxicoGame.Views["foocubus"];
			else if (gS > hS)
				AsciiChar = NoxicoGame.Views["goblin"];
			
			var skinColor = Character.Path((Character.Path("skin/type").Tokens[0].Name == "slime" ? "hair" : "skin") + "/color").Text;
			ForegroundColor = Toolkit.GetColor(skinColor);
			BackgroundColor = Toolkit.Darken(ForegroundColor);

			if (Character.HasToken("ascii"))
			{
				var a = Character.GetToken("ascii");
				if (a.HasToken("char"))
				{
					AsciiChar = (char)a.GetToken("char").Value;
				}
				if (a.HasToken("fore"))
					ForegroundColor = Toolkit.GetColor(a.GetToken("fore").Text); //(int)a.GetToken("fore").Value;
				if (a.HasToken("back"))
					BackgroundColor = Toolkit.GetColor(a.GetToken("back").Text); //(int)a.GetToken("back").Value;
			}
		}

		public override object CanMove(Direction targetDirection)
		{
			var canMove = base.CanMove(targetDirection);
			if (canMove != null && canMove is bool && !(bool)canMove)
				return canMove;
			if (Movement == Motor.WanderSector)
			{
				if (!ParentBoard.Sectors.ContainsKey(Sector))
					return canMove;
				var sect = ParentBoard.Sectors[Sector];
				var newX = this.XPosition;
				var newY = this.YPosition;
				Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
				if (newX < sect.Left || newX > sect.Right || newY < sect.Top || newY > sect.Bottom)
					canMove = false;
			}
			return canMove;
		}

		public override void Move(Direction targetDirection)
		{
			if (Character.HasToken("slimeblob"))
				ParentBoard.TrailSlime(YPosition, XPosition, ForegroundColor);
			base.Move(targetDirection);
		}

		public override void Update()
		{
			if (Character.GetToken("health").Value <= 0)
				return;

			base.Update();

			if (ParentBoard.IsBurning(YPosition, XPosition))
				if (Hurt(10, "burning to death", null))
					return;
			
			if (MoveTimer > MoveSpeed)
				MoveTimer = 0;
			else if (MoveSpeed > 0)
				MoveTimer++;

			if (ScriptRunning)
				return;

			if (MoveTimer == 0)
			{
				switch (Movement)
				{
					default:
					case Motor.Stand:
						//Do nothing
						break;
					case Motor.Wander:
					case Motor.WanderSector:
						this.Move((Direction)Toolkit.Rand.Next(4));
						break;
					case Motor.Hunt:
						Hunt();
						break;
					case Motor.Sexytimes:
						//TODO: Make sweet love -- definately split off into another method.
						break;
				}
			}

			var hostile = Character.HasToken("hostile"); //TODO: determine otherwise, probably from tokens
			if (hostile && Movement != Motor.Hunt)
			{
				var player = NoxicoGame.HostForm.Noxico.Player;
				if (DistanceFrom(player) > 10) //TODO: determine better range
					return;
				if (!CanSee(player))
					return;
				NoxicoGame.Sound.PlaySound("Alert"); //Test things with an MSG Alert -- would normally be done in Noxicobotic, I guess...
				CallScript("alert");
				MoveSpeed = 0;
				Movement = Motor.Hunt;
			}
		}

		private void Hunt()
		{
			//TODO: Hunt down the target, probably the player.
			BoardChar target = null;
			//If no target is given, assume the player.
			if (Character.HasToken("huntingtarget"))
				target = ParentBoard.Entities.OfType<BoardChar>().First(x => x.ID == Character.GetToken("huntingtarget").Text);
			else if (NoxicoGame.HostForm.Noxico.Player.ParentBoard == this.ParentBoard)
				target = NoxicoGame.HostForm.Noxico.Player;

			if (target == null)
			{
				//Intended target isn't on the board. Break off the hunt?
				MoveSpeed = 2;
				Movement = Motor.Wander;
				if (!string.IsNullOrWhiteSpace(Sector) && Sector != "<null>")
					Movement = Motor.WanderSector;
				//TODO: use pathfinder to go back to assigned sector.
				return;
			}

			var range = 1; //TODO: set to applicable range for ranged weapons. Melee gets 1 for now.
			if (DistanceFrom(target) <= range && CanSee(target))
			{
				//Within attacking range.
				if (range == 1 && (target.XPosition == this.XPosition || target.YPosition == this.YPosition))
				{
					//Melee attacks can only be orthogonal.
					MeleeAttack(target);
					return;
				}
			}
			if (DistanceFrom(target) <= 20 && CanSee(target))
			{
				//Try to move closer. I WANT TO HIT THEM WITH MY SWORD!
				var map = target.DijkstraMap;
				var dir = Direction.North;
				map.Ignore = DijkstraIgnores.Type;
				map.IgnoreType = typeof(BoardChar);
				if (map.RollDown(this.YPosition, this.XPosition, ref dir))
					Move(dir);
			}
			else
			{
				//If we're out of range, switch back to wandering.
				MoveSpeed = 2;
				Movement = Motor.Wander;
				if (!string.IsNullOrWhiteSpace(Sector) && Sector != "<null>")
					Movement = Motor.WanderSector;
				//TODO: go back to assigned sector.
				return;
			}
		}

		public void MeleeAttack(BoardChar target)
		{
			//First we need to figure out if we're armed.
			Token weaponData = null;
			foreach (var carriedItem in this.Character.GetToken("items").Tokens)
			{
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					continue;
				if (find.HasToken("equipable") && carriedItem.HasToken("equipped") && find.HasToken("weapon"))
				{
					weaponData = find.GetToken("weapon");
					break;
				}
			}

			var damage = 0.0f;
			var baseDamage = 0.0f;
			var dodged = false;
			var skill = "unarmed_combat";
			var verb = "struck";
			var obituary = "being struck down";
			if (weaponData == null)
			{
				//Unarmed combat by default.
				baseDamage = (float)Math.Floor(this.Character.GetToken("strength").Value);
			}
			else
			{
				//Armed combat, yeah!
				skill = weaponData.GetToken("skill").Text;
				baseDamage = weaponData.GetToken("damage").Value;
			}

			var level = (this.Character.Path("skills/" + skill) == null) ? 0 : (int)this.Character.Path("skills/" + skill).Value;

			if (level == 5)
				damage = baseDamage;
			else if (level < 5)
			{
				var gradient = (baseDamage - 1) / 5;
				var minimalDamage = (gradient * level + 1) + 1;
				damage = (float)Toolkit.Rand.Next((int)minimalDamage, (int)baseDamage);
			}
			else
			{
				//Just use baseDamage until later.
				damage = baseDamage;
			}

			//Account for armor and such
			//Add some randomization
			//Determine dodges
			if (dodged)
			{
				NoxicoGame.AddMessage((target is Player ? this.Character.Name.ToString() : "You") + " dodged " + (target is Player ? this.Character.Name.ToString() + "'s" : "your") + " attack.");
				return;
			}

			if (damage > 0)
			{
				NoxicoGame.AddMessage((target is Player ? this.Character.Name.ToString() : "You") + ' ' + verb + ' ' + (target is Player ? "you" : target.Character.Name.ToString()) + " for " + damage + " points.");
				Character.IncreaseSkill(skill);
			}
			if (target.Hurt(damage, obituary + " by " + this.Character.Name.ToString(true), this))
			{
				//Gain a bonus from killing the target?
			}
		}

		public virtual bool Hurt(float damage, string obituary, BoardChar aggressor)
		{
			var health = Character.GetToken("health").Value;
			if (health - damage <= 0)
			{
				//Dead, but how?
				Character.GetToken("health").Value = 0;
				LeaveCorpse(obituary);
				return true;
			}
			Character.GetToken("health").Value -= damage;
			return false;
		}

		private void LeaveCorpse(string obituary)
		{
			var corpse = new Clutter()
			{
				ParentBoard = ParentBoard,
				AsciiChar = AsciiChar,
				ForegroundColor = ForegroundColor.Darken(),
				BackgroundColor = BackgroundColor.Darken(),
				Blocking = false,
				Name = Character.Name + "'s remains",
				Description = "These are the remains of " + Character.Name + ", who died from " + obituary + ".",
				XPosition = XPosition,
				YPosition = YPosition,
			};
			ParentBoard.EntitiesToRemove.Add(this);
			ParentBoard.EntitiesToAdd.Add(corpse);
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			base.SaveToFile(stream);
			stream.Write((byte)Movement);
			stream.Write(Sector ?? "<null>");
			stream.Write(Pairing ?? "<null>");
			stream.Write((byte)MoveTimer);
			/*
			stream.Write(OnPathfinder);
			if (OnPathfinder)
			{
				stream.Write((Int16)Path.Count);
				foreach (var step in Path)
					stream.Write((byte)step);
			}
			*/
			Character.SaveToFile(stream);
		}

		public static new BoardChar LoadFromFile(BinaryReader stream)
		{
			var e = Entity.LoadFromFile(stream);
			var newChar = new BoardChar()
			{
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Flow = e.Flow, Blocking = e.Blocking,
				Script = e.Script, ScriptPointer = e.ScriptPointer, ScriptRunning = e.ScriptRunning, ScriptDelay = e.ScriptDelay,
			}; 
			newChar.Movement = (Motor)stream.ReadByte();
			newChar.Sector = stream.ReadString();
			newChar.Pairing = stream.ReadString();
			newChar.MoveTimer = stream.ReadByte();
			/*
			newChar.OnPathfinder = stream.ReadBoolean();
			if (newChar.OnPathfinder)
			{
				newChar.Path = new List<Direction>();
				var steps = stream.ReadInt16();
				for (var i = 0; i < steps; i++)
					newChar.Path.Add((Direction)stream.ReadByte());
			}
			*/
			newChar.Character = Character.LoadFromFile(stream);
			return newChar;
		}
	}

    public class Player : BoardChar
    {
		public bool AutoTravelling { get; set; }
		private Dijkstra AutoTravelMap;

        public Player()
        {
			this.AutoTravelMap = new Dijkstra();
			this.AutoTravelMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
		}

		public Player(Character character) : base(character)
		{
			this.AutoTravelMap = new Dijkstra();
			this.AutoTravelMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
		}

		public override void AdjustView()
		{
			base.AdjustView();
			AsciiChar = '@';
		}

		public void CheckWarps()
		{
			var warp = ParentBoard.Warps.Find(w => !String.IsNullOrEmpty(w.TargetBoard) && w.XPosition == XPosition && w.YPosition == YPosition);
			if (warp != null)
			{
				var game = NoxicoGame.HostForm.Noxico;
				var targetBoard = game.Boards.Find(b => b.ID == warp.TargetBoard);
				/*
				if (targetBoard == null)
				{
					targetBoard = Board.Load(warp.TargetBoard);
					game.Boards.Add(targetBoard);
				}
				*/
				ParentBoard.EntitiesToRemove.Add(this);
				game.CurrentBoard = targetBoard;
				ParentBoard = targetBoard;
				ParentBoard.Entities.Add(this);
				var twarp = targetBoard.Warps.Find(w => w.ID == warp.TargetWarp);
				if (twarp == null)
				{
					XPosition = 0;
					YPosition = 0;
				}
				else
				{
					XPosition = twarp.XPosition;
					YPosition = twarp.YPosition;
				}
				ParentBoard.Redraw();
				NoxicoGame.Sound.PlayMusic(ParentBoard.Music);
				NoxicoGame.Immediate = true;
			}
		}

		public void OpenBoard(int index)
		{
			var n = NoxicoGame.HostForm.Noxico;
			this.ParentBoard.EntitiesToRemove.Add(this);
			this.ParentBoard = n.GetBoard(index);
			n.CurrentBoard = this.ParentBoard;
			this.ParentBoard.Entities.Add(this);
			ParentBoard.Redraw();
			NoxicoGame.Sound.PlayMusic(ParentBoard.Music);
			NoxicoGame.Immediate = true;

			this.DijkstraMap.UpdateWalls();
			this.DijkstraMap.Update();
			this.AutoTravelMap.UpdateWalls();
		}

		public override void Move(Direction targetDirection)
		{
			var lx = XPosition;
			var ly = YPosition;

			#region Inter-board travel
			//TODO: Hoist this up to BoardChar?
			var n = NoxicoGame.HostForm.Noxico;
			var owI = NoxicoGame.GetOverworldIndex(ParentBoard);
			var reach = n.Overworld.GetLength(0);
			if (owI < reach * reach)
			{
				var owX = owI % reach;
				var owY = owI / reach;
				if (lx == 79 && targetDirection == Direction.East && owX < reach - 1)
				{
					this.XPosition = 0;
					OpenBoard(owI + 1);
					return;
				}
				else if (lx == 0 && targetDirection == Direction.West && owX > 0)
				{
					this.XPosition = 79;
					OpenBoard(owI - 1);
					return;
				}
				else if (ly == 24 && targetDirection == Direction.South && owY < reach - 1)
				{
					this.YPosition = 0;
					OpenBoard(owI + reach);
					return;
				}
				else if (ly == 0 && targetDirection == Direction.North && owY > 0)
				{
					this.YPosition = 24;
					OpenBoard(owI - reach);
					return;
				}
			}
			#endregion

			var newX = this.XPosition;
			var newY = this.YPosition;
			Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
			foreach (var entity in ParentBoard.Entities.Where(x => x.XPosition == newX && x.YPosition == newY))
			{
				if (entity.Blocking)
				{
					NoxicoGame.ClearKeys();
					entity.CallScript("playerbump");
					if (entity is BoardChar && ((BoardChar)entity).Character.HasToken("hostile"))
					{
						//Strike at your foes!
						AutoTravelling = false;
						MeleeAttack((BoardChar)entity);
						EndTurn();
					}
					return;
				}
				else
					entity.CallScript("playerstep");
			}
			base.Move(targetDirection);

			EndTurn();
			NoxicoGame.Sound.PlaySound("Push");

			if (lx != XPosition || ly != YPosition)
			{
				this.DijkstraMap.Hotspots[0] = new Point(XPosition, YPosition);
				this.DijkstraMap.Update();
				//this.DijkstraMap.SaveToPNG();
				CheckWarps();
			}

			NoxicoGame.HostForm.Text = string.Format("Noxico - {0} ({1}x{2})", ParentBoard.Name, XPosition, YPosition);
		}

		public override void Update()
        {
            //base.Update();
			if (NoxicoGame.Mode != UserMode.Walkabout)
				return;

			if (NoxicoGame.KeyMap[(int)Keys.F1])
			{
				Pause.Open();
				return;
			}

			if (NoxicoGame.KeyMap[(int)Keys.OemPeriod])
			{
				NoxicoGame.ClearKeys();
				EndTurn();
				return;
			}

			if (NoxicoGame.KeyMap[(int)Keys.C])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.AddMessage("[Chat message]");
				NoxicoGame.Mode = UserMode.LookAt;
				NoxicoGame.Cursor.ParentBoard = this.ParentBoard;
				NoxicoGame.Cursor.XPosition = this.XPosition;
				NoxicoGame.Cursor.YPosition = this.YPosition;
				NoxicoGame.Cursor.Range = 3;
				NoxicoGame.Cursor.Intent = Cursor.Intents.Chat;
				NoxicoGame.Cursor.Point();
				return;
			}

			if (NoxicoGame.KeyMap[(int)Keys.L] || NoxicoGame.KeyMap[(int)Keys.OemQuestion])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.AddMessage("[Lookat message]");
				NoxicoGame.Mode = UserMode.LookAt;
				NoxicoGame.Cursor.ParentBoard = this.ParentBoard;
				NoxicoGame.Cursor.XPosition = this.XPosition;
				NoxicoGame.Cursor.YPosition = this.YPosition;
				NoxicoGame.Cursor.Range = 0;
				NoxicoGame.Cursor.Intent = Cursor.Intents.Look;
				NoxicoGame.Cursor.Point();
				return;
			}

			if (NoxicoGame.KeyMap[(int)Keys.P] || NoxicoGame.KeyMap[(int)Keys.Oemcomma])
			{
				NoxicoGame.ClearKeys();
				var itemsHere = ParentBoard.Entities.FindAll(e => e.XPosition == this.XPosition && e.YPosition == this.YPosition && e is DroppedItem);
				if (itemsHere.Count == 0)
				{
					NoxicoGame.AddMessage("[Pickup message]");
					NoxicoGame.Mode = UserMode.LookAt;
					NoxicoGame.Cursor.ParentBoard = this.ParentBoard;
					NoxicoGame.Cursor.XPosition = this.XPosition;
					NoxicoGame.Cursor.YPosition = this.YPosition;
					NoxicoGame.Cursor.Range = 2;
					NoxicoGame.Cursor.Intent = Cursor.Intents.Take;
					NoxicoGame.Cursor.Point();
					return;
				}
				else
				{
					var item = (DroppedItem)itemsHere[0];
					item.PickUp(this.Character);
					NoxicoGame.AddMessage("You pick up " + item.Item.ToString(item.Token, true) + ".", item.ForegroundColor);
					return;
				}
			}

			if (NoxicoGame.KeyMap[(int)Keys.I])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Mode = UserMode.Subscreen;
				NoxicoGame.Subscreen = Inventory.Handler;
				Subscreens.FirstDraw = true;
				return;
			}

			if (!AutoTravelling)
			{
				if (NoxicoGame.KeyMap[(int)Keys.Left])
					this.Move(Direction.West);
				else if (NoxicoGame.KeyMap[(int)Keys.Right])
					this.Move(Direction.East);
				else if (NoxicoGame.KeyMap[(int)Keys.Up])
					this.Move(Direction.North);
				else if (NoxicoGame.KeyMap[(int)Keys.Down])
					this.Move(Direction.South);
			}
			else
			{
				if (NoxicoGame.KeyMap[(int)Keys.Left] || NoxicoGame.KeyMap[(int)Keys.Right] || NoxicoGame.KeyMap[(int)Keys.Up] || NoxicoGame.KeyMap[(int)Keys.Down])
				{
					AutoTravelling = false;
					return;
				}
				var x = XPosition;
				var y = YPosition;
				var dir = Direction.North;
				if (AutoTravelMap.RollDown(y, x, ref dir))
					Move(dir);
				else
					AutoTravelling = false;
			}
        }

		public void AutoTravelTo(int x, int y)
		{
			AutoTravelMap.Hotspots[0] = new Point(x, y);
			AutoTravelMap.UpdateWalls();
			AutoTravelMap.Update();
			AutoTravelling = true;
		}

		public void EndTurn()
		{
			NoxicoGame.AutoRestTimer = NoxicoGame.AutoRestSpeed;
			ParentBoard.Update(true);
			if (ParentBoard.IsBurning(YPosition, XPosition))
			{
				if (Hurt(10, "burned to death", null))
				{
					NoxicoGame.AddMessage("GAME OVER", Color.Red);
					MessageBox.Ask(
						"You have burned to death.\n\nWould you like an infodump on the way out?",
						() =>
						{
							Character.CreateInfoDump();
							NoxicoGame.HostForm.Close();
						},
						() =>
						{
							NoxicoGame.HostForm.Close();
						}
						);
					return;
				}
			}
			//Leave EntitiesToAdd/Remove to Board.Update next passive cycle.

			NoxicoGame.UpdateMessages();
		}

		public override bool Hurt(float damage, string obituary, BoardChar aggressor)
		{
			if (AutoTravelling)
			{
				NoxicoGame.AddMessage("Autotravel interrupted.");
				AutoTravelling = false;
			}
			var dead = base.Hurt(damage, obituary, aggressor);
			if (dead)
			{
				var relation = Character.Path("ships/" + aggressor.Character.Name.ToString(true));
				if (relation == null)
				{
					relation = new Token() { Name = aggressor.Character.Name.ToString(true) };
					Character.Path("ships").Tokens.Add(relation);
				}
				relation.Tokens.Add(new Token() { Name = "killer" });

				NoxicoGame.AddMessage("GAME OVER", Color.Red);
				MessageBox.Ask(
					"You have been slain.\n\nWould you like an infodump on the way out?",
					() =>
					{
						Character.CreateInfoDump();
						NoxicoGame.HostForm.Close();
					},
					() =>
					{
						NoxicoGame.HostForm.Close();
					}
					);
			}
			return dead;
		}

		public static new Player LoadFromFile(BinaryReader stream)
		{
			var e = Entity.LoadFromFile(stream);
			var newChar = new Player()
			{
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Flow = e.Flow, Blocking = e.Blocking,
				//Script = e.Script, ScriptPointer = e.ScriptPointer, ScriptRunning = e.ScriptRunning, ScriptWaitTime = e.ScriptWaitTime, //Don't transfer any script data that might be there. Why would it!?
			};
			//Skip the unused bits. Players aren't bound to sectors and such!
			stream.ReadByte();
			stream.ReadString();
			stream.ReadString();
			stream.ReadByte();
			//----
			/*
			newChar.OnPathfinder = stream.ReadBoolean();
			if (newChar.OnPathfinder)
			{
				newChar.Path = new List<Direction>();
				var steps = stream.ReadInt16();
				for (var i = 0; i < steps; i++)
					newChar.Path.Add((Direction)stream.ReadByte());
			}
			*/
			newChar.Character = Character.LoadFromFile(stream);

			return newChar;
		}
	}

	public class Clutter : Entity
	{
		public string Name { get; set; }
		public string Description { get; set; }
		public int Life { get; set; }

		public Clutter()
		{
			this.AsciiChar = '?';
			this.ForegroundColor = Color.Silver;
			this.BackgroundColor = Color.Black;
		}

		public Clutter(char asciiChar, Color foreColor, Color backColor, bool blocking = false, string name = "thing", string description = "This is a thing.")
		{
			this.AsciiChar = asciiChar;
			this.ForegroundColor = foreColor;
			this.BackgroundColor = backColor;
			this.Blocking = blocking;
			this.Name = name;
			this.Description = description;
		}

		public override void Update()
		{
			base.Update();
			if (Life > 0)
			{
				Life--;
				if (Life == 0)
					ParentBoard.EntitiesToRemove.Add(this);
			}
		}

		public override void Move(Direction targetDirection)
		{
			//base.Move(targetDirection);
			Console.WriteLine("Trying to move clutter.");
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			base.SaveToFile(stream);
			stream.Write(Name ?? "");
			stream.Write(Description ?? "");
		}

		public static new Clutter LoadFromFile(BinaryReader stream)
		{
			var e = Entity.LoadFromFile(stream);
			var newDress = new Clutter()
			{
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Blocking = e.Blocking
			};
			newDress.Name = stream.ReadString();
			newDress.Description = stream.ReadString();
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

		public DroppedItem(string item) : this(NoxicoGame.KnownItems.First(i => i.ID == item))
		{
		}

		public DroppedItem(InventoryItem item)
		{
			Item = item;
			Token = new Token() { Name = item.ID };

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
					this.ForegroundColor = Toolkit.GetColor(ascii.GetToken("fore").Tokens[0]);
				else if (Item.ID == "book" && Token.Tokens.Count > 0)
				{
					var cga = new [] { Color.Black, Color.DarkBlue, Color.DarkGreen, Color.DarkCyan, Color.DarkRed, Color.Purple, Color.Brown, Color.Silver, Color.Gray, Color.Blue, Color.Green, Color.Cyan, Color.Red, Color.Magenta, Color.Yellow, Color.White };
					this.ForegroundColor = cga[(int)Token.GetToken("id").Value % cga.Length]; //Toolkit.GetColor(cga[(int)Token.GetToken("id").Value % cga.Length]);
				}
				if (ascii.HasToken("back"))
					this.BackgroundColor = Toolkit.GetColor(ascii.GetToken("back").Tokens[0]);
				else
					this.BackgroundColor = this.ForegroundColor.Darken();
			}
		}

		public override void Update()
		{
		}

		public override void Move(Direction targetDirection)
		{
			Console.WriteLine("Trying to move dropped item.");
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			base.SaveToFile(stream);
			stream.Write(Item.ID);
			Token.SaveToFile(stream);
		}

		public static new DroppedItem LoadFromFile(BinaryReader stream)
		{
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

		public void PickUp(Character taker)
		{
			if (!taker.HasToken("items"))
				taker.Tokens.Add(new Noxico.Token() { Name = "items" });
			taker.GetToken("items").Tokens.Add(Token);
			ParentBoard.EntitiesToRemove.Add(this);
		}
	}

	[Obsolete("This is for testing only.")]
	public class LOSTester : BoardChar
	{
		public override void Update()
		{
			base.Update();
			var player = NoxicoGame.HostForm.Noxico.Player;
			var canSee = true;
			foreach (var point in Toolkit.Line(XPosition, YPosition, player.XPosition, player.YPosition))
			{
				if (ParentBoard.IsSolid(point.Y, point.X))
				{
					canSee = false;
					break;
				}
				else
					NoxicoGame.HostForm.SetCell(point.Y, point.X, 'x', Color.White, Color.Navy, true);

			}
			AsciiChar = canSee ? '!' : '.';
		}
	}

	public class LightSource : Entity
	{
		public int Brightness { get; set; }
		public LightSource()
		{
			this.AsciiChar = '*';
			this.ForegroundColor = Color.Yellow;
			this.Brightness = 64;
		}
	}
}