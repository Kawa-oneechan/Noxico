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

	public class Cursor : Entity
	{
		public static Entity LastTarget { get; set; }
		public Entity PointingAt { get; private set; }
		public List<Point> Tabstops { get; private set; }
		public int Tabstop { get; set; }

		public Cursor()
		{
			this.AsciiChar = '\u25CA';
			this.BackgroundColor = Color.Black;
			this.ForegroundColor = Color.White;
			this.Tabstops = new List<Point>();
		}

		public override void Draw()
		{
			//if (Environment.TickCount % blinkRate * 2 < blinkRate)
			//	base.Draw();
			NoxicoGame.HostForm.Cursor = new Point(XPosition, YPosition);
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			this.ParentBoard.DirtySpots.Add(new Location(XPosition, YPosition));
			if (CanMove(targetDirection, check) != null)
				return;
			var newX = 0;
			var newY = 0;
			Toolkit.PredictLocation(XPosition, YPosition, targetDirection, ref newX, ref newY);
			XPosition = newX;
			YPosition = newY;
			Point();
		}

		public override object CanMove(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var newX = this.XPosition;
			var newY = this.YPosition;
			Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
			if (newX < 0 || newY < 0 || newX > 79 || newY > 24)
				return false;
			return null;
		}

		public void Point()
		{
			PointingAt = null;
			if (NoxicoGame.Messages.Count == 0)
				NoxicoGame.AddMessage("<...>");
			NoxicoGame.Messages.Last().Message = "Point at an object or character.";
			NoxicoGame.Messages.Last().Color = Color.Gray;
			foreach (var entity in this.ParentBoard.Entities)
			{
				if (entity.XPosition == XPosition && entity.YPosition == YPosition)
				{
					if (!this.ParentBoard.IsLit(YPosition, XPosition) && NoxicoGame.HostForm.Noxico.Player.CanSee(entity))
					{
						if (NoxicoGame.HostForm.Noxico.Player.Character.Path("eyes/glow") == null)
						{
							//No darkvision
							if (entity is BoardChar && ((BoardChar)entity).Character.Path("eyes/glow") != null)
							{
								//Entity has glowing eyes, but we don't let the player actually interact with them.
								NoxicoGame.Messages.Last().Message = "Eyes in the darkness";
								NoxicoGame.Messages.Last().Color = Color.FromName(((BoardChar)entity).Character.Path("eyes").Text);
							}
							return;
						}
						else
						{
							//Player does have darkvision, ignore all this.
						}
					}

					PointingAt = entity;
					NoxicoGame.Messages.Last().Color = PointingAt.ForegroundColor;
					if (entity is BoardChar)
					{
						NoxicoGame.Messages.Last().Message = ((BoardChar)PointingAt).Character.ToString(); 
						return;
					}
					else if (entity is DroppedItem)
					{
						NoxicoGame.Messages.Last().Message = ((DroppedItem)PointingAt).Name;
						return;
					}
					else if (entity is Clutter || entity is Container)
					{
						NoxicoGame.Messages.Last().Message = entity is Container ? ((Container)PointingAt).Name : ((Clutter)PointingAt).Name;
						return;
					}
					else if (entity is Door)
					{
						NoxicoGame.Messages.Last().Message = "Door";
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
			NoxicoGame.Messages.Last().Renew();
			NoxicoGame.UpdateMessages();

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.Messages.Remove(NoxicoGame.Messages.Last());
				ParentBoard.Redraw();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.TabFocus) || Vista.Triggers == XInputButtons.RightShoulder)
			{
				NoxicoGame.ClearKeys();
				Tabstop++;
				if (Tabstop >= Tabstops.Count)
					Tabstop = 0;
				XPosition = Tabstops[Tabstop].X;
				YPosition = Tabstops[Tabstop].Y;
				Point();
			}

			if (NoxicoGame.IsKeyDown(KeyBinding.Accept) || Vista.Triggers == XInputButtons.A)
			{
				Subscreens.PreviousScreen.Clear();
				NoxicoGame.ClearKeys();
				var player = NoxicoGame.HostForm.Noxico.Player;
				if (PointingAt != null)
				{
					if (PointingAt is Clutter || PointingAt is Door || PointingAt is Container)
						return;

					LastTarget = PointingAt;
					var distance = player.DistanceFrom(PointingAt);
					var canSee = player.CanSee(PointingAt);

					var options = new Dictionary<object, string>();
					var description = "something";

					options["look"] = "Look at it";

					if (PointingAt is Player)
					{
						description = "you, " + player.Character.GetNameOrTitle();
						options["look"] = "Check yourself";
						if (player.Character.GetStat(Stat.Stimulation) >= 30)
							options["fuck"] = "Masturbate";
					}
					else if (PointingAt is BoardChar)
					{
						var boardChar = PointingAt as BoardChar;
						description = boardChar.Character.GetNameOrTitle(true);
						options["look"] = "Look at " + boardChar.Character.HimHerIt();

						if (canSee && distance <= 2 && !boardChar.Character.HasToken("beast"))
						{
							options["talk"] = "Talk to " + boardChar.Character.HimHerIt();
						}

						if (canSee && player.Character.GetStat(Stat.Stimulation) >= 30 && distance <= 1)
						{
							if (!boardChar.Character.HasToken("beast"))
							{
								if ((boardChar.Character.HasToken("hostile") && boardChar.Character.HasToken("helpless")))
									options["rape"] = "Rape " + boardChar.Character.HimHerIt();
								else
									options["fuck"] = "Fuck " + boardChar.Character.HimHerIt();
							}
						}

						if (canSee && player.Character.CanShoot() != null && player.ParentBoard.HasToken("combat"))
						{
							options["shoot"] = "Shoot at " + boardChar.Character.HimHerIt();
						}
					}
					else if (PointingAt is DroppedItem)
					{
						var drop = PointingAt as DroppedItem;
						var item = drop.Item;
						var token = drop.Token;
						description = item.ToString(token);
						if (distance <= 1)
							options["take"] = "Pick it up";
					}

					//MessageBox.List("This is " + description + ". What would you do?", options,
					ActionList.Show(description, PointingAt.XPosition, PointingAt.YPosition, options,
						() =>
						{
							if (ActionList.Answer is int && (int)ActionList.Answer == -1)
								return;
							switch (ActionList.Answer as string)
							{
								case "look":
									if (PointingAt is DroppedItem)
									{
										var drop = PointingAt as DroppedItem;
										var item = drop.Item;
										var token = drop.Token;
										var text = (item.HasToken("description") && !token.HasToken("unidentified") ? item.GetToken("description").Text : "This is " + item.ToString(token) + ".").Trim();
										MessageBox.Notice(text, true);
									}
									else if (PointingAt is Clutter && !string.IsNullOrWhiteSpace(((Clutter)PointingAt).Description))
									{
										MessageBox.Notice(((Clutter)PointingAt).Description.Trim(), true);
									}
									else if (PointingAt is BoardChar)
									{
										TextScroller.LookAt((BoardChar)PointingAt);
									}
									break;

								case "talk":
									if (PointingAt is Player)
									{
										if (Culture.CheckSummoningDay())
											return;
										if (player.Character.Path("cunning").Value >= 10)
											MessageBox.Notice("Talking to yourself is the first sign of insanity.", true);
										else
											MessageBox.Notice("You spend a short while enjoying some pleasant but odd conversation with yourself.", true);
									}
									else if (PointingAt is BoardChar)
									{
										var boardChar = PointingAt as BoardChar;
										if (boardChar.Character.HasToken("hostile"))
											MessageBox.Notice((boardChar.Character.IsProperNamed ? boardChar.Character.GetNameOrTitle() : "the " + boardChar.Character.Title) + " has nothing to say to you.", true);
										else
											SceneSystem.Engage(player.Character, boardChar.Character, true);
										//MessageBox.Ask("Strike up a conversation with " + boardChar.Character.GetNameOrTitle() + "?", () => { SceneSystem.Engage(player.Character, boardChar.Character, true); }, null, true);
									}
									break;

								case "fuck":
									if (PointingAt is Player)
										SceneSystem.Engage(player.Character, ((BoardChar)PointingAt).Character, "(masturbate)");
									else if (PointingAt is BoardChar)
										SceneSystem.Engage(player.Character, ((BoardChar)PointingAt).Character);
									break;

								case "rape":
									SceneSystem.Engage(player.Character, ((BoardChar)PointingAt).Character, "(rape start)");
									break;

								case "shoot":
									player.AimShot(PointingAt);
									break;

								case "take":
									if (PointingAt is DroppedItem)
									{
										var drop = PointingAt as DroppedItem;
										var item = drop.Item;
										var token = drop.Token;
										drop.Take(player.Character);
										NoxicoGame.AddMessage("You pick up " + item.ToString(token, true) + ".", drop.ForegroundColor);
										NoxicoGame.Sound.PlaySound("Get Item");
										ParentBoard.Redraw();
									}
									break;

								default:
									MessageBox.Notice("Unknown action handler \"" + ActionList.Answer.ToString() + "\".", true);
									break;
							}
						}
						); //	true, true);
					return;
				}
				else
				{
					var tSD = this.ParentBoard.GetSpecialDescription(YPosition, XPosition);
					if (tSD.HasValue)
					{
						PointingAt = null;
						MessageBox.Notice(tSD.Value.Description, true); 
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
					NoxicoGame.AddMessage("Info for " + ((BoardChar)PointingAt).Character.GetNameOrTitle() + " dumped.", Color.Red);
				}
			}
#endif

			if (NoxicoGame.IsKeyDown(KeyBinding.Left) || Vista.DPad == XInputButtons.Left)
				this.Move(Direction.West);
			else if (NoxicoGame.IsKeyDown(KeyBinding.Right) || Vista.DPad == XInputButtons.Right)
				this.Move(Direction.East);
			else if (NoxicoGame.IsKeyDown(KeyBinding.Up) || Vista.DPad == XInputButtons.Up)
				this.Move(Direction.North);
			else if (NoxicoGame.IsKeyDown(KeyBinding.Down) || Vista.DPad == XInputButtons.Down)
				this.Move(Direction.South);
		}

		public void PopulateTabstops()
		{
			var player = NoxicoGame.HostForm.Noxico.Player;
			Tabstops.Clear();
			foreach (var e in ParentBoard.Entities)
			{
				if (e is Door)
					continue;
				if (e is Clutter)
					continue;
				Tabstops.Add(new Point(e.XPosition, e.YPosition));
			}

			//Tabstops.Sort();
			if (LastTarget != null)
			{
				var ltp = Tabstops.FirstOrDefault(p => p.X == LastTarget.XPosition && p.Y == LastTarget.YPosition);
				if (ltp.X + ltp.Y == 0)
					LastTarget = null;
				if (LastTarget != null)
				{
					if (!player.CanSee(LastTarget))
						LastTarget = null;
					else
					{
						this.XPosition = ltp.X;
						this.YPosition = ltp.Y;
						Tabstop = Tabstops.IndexOf(ltp);
					}
				}
			}
		}
	}

	public class BoardChar : Entity
	{
		private static int blinkRate = 1000;

		public Motor Movement { get; set; }
		public string Sector { get; set; }
		public string Pairing { get; set; }
		private int MoveTimer;
		public int MoveSpeed { get; set; }

		public Dijkstra DijkstraMap { get; private set; }
		public Character Character { get; set; }

		public string OnTick { get; set; }
		public string OnLoad { get; set; }
		public string OnPlayerBump { get; set; }
		public string OnHurt { get; set; }
		public string OnPathFinish { get; set; }
		public bool ScriptPathing { get; set; }
		public Dijkstra ScriptPathTarget { get; private set; }
		public int ScriptPathTargetX { get; private set; }
		public int ScriptPathTargetY { get; private set; }
		public string ScriptPathID { get; set; }
		private Jint.JintEngine js;
		private Scheduler scheduler;

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
			RestockVendor();
		}

		public virtual void AdjustView()
		{
			var skinColor = Character.Path((Character.Path("skin/type").Text == "slime" ? "hair" : "skin") + "/color").Text;
			ForegroundColor = Color.FromName(skinColor);
			BackgroundColor = Toolkit.Darken(ForegroundColor);
			if (skinColor.Equals("black", StringComparison.OrdinalIgnoreCase))
				ForegroundColor = Color.FromArgb(34, 34, 34);

			if (Character.HasToken("ascii"))
			{
				var a = Character.GetToken("ascii");
				if (a.HasToken("char"))
					AsciiChar = (char)a.GetToken("char").Value;
				if (a.HasToken("fore"))
					ForegroundColor = Color.FromName(a.GetToken("fore").Text);
				if (a.HasToken("back"))
					BackgroundColor = Color.FromName(a.GetToken("back").Text);
			}
		}

		public override object CanMove(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var canMove = base.CanMove(targetDirection, check);
			if (canMove != null && canMove is bool && !(bool)canMove)
				return canMove;
			if (Movement == Motor.WanderSector && !ScriptPathing)
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

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			if (Character.HasToken("slimeblob"))
				ParentBoard.TrailSlime(YPosition, XPosition, ForegroundColor);
			check = SolidityCheck.Walker;
			if (Character.HasToken("flying"))
				check = SolidityCheck.Flyer;
			base.Move(targetDirection, check);
		}

		public override void Draw()
		{
			if (ParentBoard.IsLit(this.YPosition, this.XPosition))
			{
				base.Draw();
				if (Environment.TickCount % blinkRate * 2 < blinkRate)
				{
					if (Character.HasToken("sleeping"))
						NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, 'Z', this.ForegroundColor, this.BackgroundColor);
					else if (Character.HasToken("flying"))
						NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, '^', this.ForegroundColor, this.BackgroundColor);
					else if (Character.Path("role/vendor") != null)
						NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, '$', this.ForegroundColor, this.BackgroundColor);
				}
			}
			else if (Character.Path("eyes/glow") != null && !Character.HasToken("sleeping"))
				NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, '\"', Color.FromName(Character.Path("eyes").Text), ParentBoard.Tilemap[XPosition, YPosition].Background.Darken(1.5));
		}

		public override bool CanSee(Entity other)
		{
			if (Character.Path("eyes/glow") == null)
				return base.CanSee(other);
			//But if we do have glowing eyes, ignore illumination.
			foreach (var point in Toolkit.Line(XPosition, YPosition, other.XPosition, other.YPosition))
				if (ParentBoard.IsSolid(point.Y, point.X))
					return false;
			return true;
		}

		public void Excite()
		{
			if (this.Character.HasToken("beast") || this.Character.HasToken("sleeping"))
				return;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (player.ParentBoard != this.ParentBoard)
				player = null;
			var ogled = false;
			foreach (var other in ParentBoard.Entities.OfType<BoardChar>().Where(e => e != this && e.DistanceFrom(this) < 3))
			{
				if (other.Character.HasToken("beast"))
					continue;
				if (other.Character.GetStat(Stat.Charisma) >= 10)
				{
					var stim = this.Character.GetToken("stimulation");
					var otherChar = other.Character.GetStat(Stat.Charisma);
					var distance = other.DistanceFrom(this);
					var increase = (otherChar / 10) * (distance * 0.25);
					stim.Value += (float)increase;
					if (distance < 2)
						stim.Value += 2;
					if (stim.Value > 100)
						stim.Value = 100;
					if (!ogled && this != player)
					{
						var oldStim = this.Character.HasToken("oglestim") ? this.Character.GetToken("oglestim").Value : 0;
						if (stim.Value >= oldStim + 20 && player != null && this != player && player.DistanceFrom(this) < 4 && player.CanSee(this))
						{
							NoxicoGame.AddMessage(string.Format("{0} to {1}: {2}", this.Character.Name, (other == player ? "you" : other.Character.Name.ToString()), Ogle(other.Character)), this.ForegroundColor);
							if (!this.Character.HasToken("oglestim"))
								this.Character.AddToken("oglestim");
							this.Character.GetToken("oglestim").Value = stim.Value;
							ogled = true;
						}
					}
				}
			}
		}

		public string Ogle(Character otherChar)
		{
			if (this.Character.HasToken("sleeping"))
				return null;
			var stim = this.Character.GetStat(Stat.Stimulation);
			var carn = this.Character.GetStat(Stat.Carnality);
			var r = Random.Next(4);
			if (r == 0)
			{
				if (otherChar.BiggestBreastrowNumber == -1 || otherChar.GetBreastRowSize(otherChar.BiggestBreastrowNumber) < 3.5)
					r = Random.Next(1, 4);
				else
				{
					var breastSize = otherChar.GetBreastRowSize(otherChar.BiggestBreastrowNumber);
					if (breastSize < 5)
						return "Nice " + Descriptions.BreastRandom(true) + ".";
					else if (breastSize < 10)
						return "Look at those " + Descriptions.BreastRandom(true) + "...";
					else
						return "Woah, momma.";
				}
			}
			//TODO: add more reactions.
			{
				var cha = otherChar.GetStat(Stat.Charisma);
				if (cha > 0)
				{
					if (cha < 30)
						return "Well hello, " + (otherChar.GetGenderEnum() == Gender.Male ? "handsome." : "beautiful.");
					else if (cha < 60)
						return "Oh my.";
					else
						return "Woah.";
				}
			}
			return "There are no words.";
		}

		public void CheckForCriminalScum()
		{
			if (Character.HasToken("hostile") || Character.HasToken("sleeping"))
				return;
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (CanSee(player) && DistanceFrom(player) < 10)
			{
				var myID = this.Character.ID;
				var items = player.Character.GetToken("items").Tokens;
				foreach (var item in items)
				{
					var owner = item.Path("owner");
					if (owner != null && owner.Text == myID)
					{
						if (!this.ParentBoard.HasToken("combat"))
							this.ParentBoard.AddToken("combat");
						SceneSystem.Engage(player.Character, this.Character, "(criminalscum)", true);
					}
				}
			}
		}

		public override void Update()
		{
			if (Character.GetToken("health").Value <= 0)
				return;

			if (NoxicoGame.HostForm.Noxico.Player.Character.HasToken("haste") && !(this is Player))
			{
				if (NoxicoGame.HostForm.Noxico.Player.Character.GetToken("haste").Value == 1)
					return; //skip a turn
			}
			if (this.Character.HasToken("slow"))
			{
				var slow = this.Character.GetToken("slow");
				slow.Value = (int)slow.Value ^ 1;
				if (slow.Value == 1)
					return; //skip a turn
			}
			if (this.Character.HasToken("justmeleed"))
			{
				this.Character.RemoveToken("justmeleed");
				return; //guess what
			}

			if (Character.HasToken("helpless"))
			{
				if (Random.NextDouble() < 0.05)
				{
					Character.GetToken("health").Value += 2;
					NoxicoGame.AddMessage((this is Player ? "You get" : Character.GetNameOrTitle() + " gets") + " back up.");
					Character.RemoveToken("helpless");
					//TODO: Remove hostility? Replace with fear?
				}
				else
					return;
			}
			if (Character.HasToken("waitforplayer") && !(this is Player))
			{
				if (!NoxicoGame.HostForm.Noxico.Player.Character.HasToken("helpless"))
				{
					Character.RemoveToken("waitforplayer");
					Character.AddToken("cooldown", 5, "");
				}
				return;
			}
			if (Character.HasToken("cooldown"))
			{
				Character.GetToken("cooldown").Value--;
				if (Character.GetToken("cooldown").Value == 0)
					Character.RemoveToken("cooldown");
				else
					return;
			}

			if (!RunScript(OnTick))
				return;

			CheckForCriminalScum();

			base.Update();
			Excite();
			Character.UpdatePregnancy();

			if (!Character.HasToken("fireproof") && ParentBoard.IsBurning(YPosition, XPosition))
				if (Hurt(10, "burning to death", null))
					return;

			//Pillowshout added this.
			if (!(this is Player) && !this.Character.HasToken("hostile") && this.ParentBoard.BoardType == BoardType.Town)
			{
				if (scheduler == null)
					scheduler = new Scheduler(this);

				scheduler.RunSchedule();
			}

			if (this.Character.HasToken("sleeping"))
				return;

			if (MoveTimer > MoveSpeed)
				MoveTimer = 0;
			else if (MoveSpeed > 0)
				MoveTimer++;

			ActuallyMove();
			if (Character.HasToken("haste"))
					ActuallyMove();
		}

		private void ActuallyMove()
		{
			if (MoveTimer == 0)
			{
				if (ScriptPathing)
				{
					var dir = Direction.North;
					ScriptPathTarget.Ignore = DijkstraIgnore.Type;
					ScriptPathTarget.IgnoreType = typeof(BoardChar);
					if (ScriptPathTarget.RollDown(this.YPosition, this.XPosition, ref dir))
						Move(dir);
					if (this.XPosition == ScriptPathTargetX && this.YPosition == ScriptPathTargetY)
					{
						ScriptPathing = false;
						RunScript(OnPathFinish);
					}
					return;
				}

				switch (Movement)
				{
					default:
					case Motor.Stand:
						//Do nothing
						break;
					case Motor.Wander:
					case Motor.WanderSector:
						this.Move((Direction)Random.Next(4));
						break;
					case Motor.Hunt:
						Hunt();
						break;
				}
			}

			var hostile = Character.HasToken("hostile");
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (ParentBoard == player.ParentBoard && hostile && Movement != Motor.Hunt)
			{
				if (DistanceFrom(player) > 10) //TODO: determine better range
					return;
				if (!CanSee(player))
					return;
				NoxicoGame.Sound.PlaySound("Alert"); //Test things with an MGS Alert -- would normally be done in Noxicobotic, I guess...
				MoveSpeed = 0;
				Movement = Motor.Hunt;
				
				//If we're gonna rape the target, we'd want them for ourself. Otherwise...
				if (Character.GetStat(Stat.Stimulation) < 30)
				{
					//...we call out to nearby hostiles
					var called = 0;
					foreach (var other in ParentBoard.Entities.OfType<BoardChar>().Where(x => !(x is Player) && x != this && DistanceFrom(x) < 10))
					{
						called++;
						other.CallTo(player);
					}
					if (called > 0)
					{
						if (!Character.HasToken("beast"))
							NoxicoGame.AddMessage(Character.Name.ToString(false) + ", " + Character.Title + ": \"There " + player.Character.HeSheIt(true) + " is!\"", this.ForegroundColor);
						else
							NoxicoGame.AddMessage("The " + Character.Title + " vocalizes an alert!", this.ForegroundColor);
						Program.WriteLine("{0} called {1} others to player's location.", this.Character.Name, called);
					}
				}
			}
			if (Movement == Motor.Hunt && !hostile)
				Movement = Motor.Wander;
		}

		private void Hunt()
		{
			if (Character.HasToken("helpless"))
				return;

			if (Character.HasToken("beast"))
				Character.GetToken("stimulation").Value = 0;

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

			MoveSpeed = 0;

			var distance = DistanceFrom(target);
			//var weapon = Character.CanShoot();
			var weapon = this.Character.GetEquippedItemBySlot("hand");
			if (weapon != null && !weapon.HasToken("weapon"))
				weapon = null;
			var range = (weapon == null || weapon.Path("weapon/range") == null) ? 1 : (int)weapon.Path("weapon/range").Value;

			//Determine best weapon for the job.
			if ((distance <= 2 && range > 2) || weapon == null)
			{
				//Close by, could be better to use short-range weapon, or unarmed.
				foreach (var carriedItem in this.Character.GetToken("items").Tokens)
				{
					if (carriedItem.HasToken("equipped"))
						continue;
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					if (find.HasToken("equipable") && find.HasToken("weapon"))
					{
						var r = find.Path("weapon/range");
						if (r == null || r.Value == 1)
						{
							try
							{
								if (find.Equip(this.Character, carriedItem))
								{
									Program.WriteLine("{0} switches to {1} (SR)", this.Character.Name, find);
									return; //end turn
								}
							}
							catch (ItemException)
							{ }
						}
					}
				}
			}
			if ((distance > 2 && range == 1) || weapon == /* still */ null)
			{
				//Far away, could be better to use long-range weapon, or unarmed
				foreach (var carriedItem in this.Character.GetToken("items").Tokens)
				{
					if (carriedItem.HasToken("equipped"))
						continue;
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					if (find.HasToken("equipable") && find.HasToken("weapon"))
					{
						var r = find.Path("weapon/range");
						if (r != null && r.Value > 3)
						{
							try
							{
								if (find.Equip(this.Character, carriedItem))
								{
									Program.WriteLine("{0} switches to {1} (LR)", this.Character.Name, find);
									return; //end turn
								}
							}
							catch (ItemException)
							{ }
						}
					}
				}
			}

			if (distance <= range && CanSee(target))
			{
				//Within attacking range.
				if (target.Character.HasToken("helpless") && Character.GetToken("stimulation").Value > 30 && distance == 1)
				{
					//WRONG KIND OF ATTACK! ABANDON SHIP!!
					Character.AddToken("waitforplayer");
					SceneSystem.Engage(this.Character, target.Character, "(loss rape start)");
					return;
				}
				if (range == 1 && (target.XPosition == this.XPosition || target.YPosition == this.YPosition))
				{
					//Melee attacks can only be orthogonal.
					MeleeAttack(target);
					if (Character.Path("prefixes/infectious") != null && Random.NextDouble() > 0.25)
						target.Character.Morph(Character.GetToken("infectswith").Text, MorphReportLevel.PlayerOnly, true, 0);
					return;
				}
				else if (weapon != null)
				{
					AimShot(target);
				}
			}

			if (!CanSee(target) && Character.HasToken("targetlastpos"))
			{
				if (ScriptPathTarget == null)
				{
					var lastPos = Character.GetToken("targetlastpos");
					ScriptPathTarget = new Dijkstra();
					ScriptPathTarget.Hotspots.Add(new Point((int)lastPos.GetToken("x").Value, (int)lastPos.GetToken("y").Value));
					ScriptPathTarget.Update();
				}
				Program.WriteLine("{0} can't see, looks for {1}", this.ID, ScriptPathTarget.Hotspots[0].ToString());
				var map = ScriptPathTarget;
				var dir = Direction.North;
				map.Ignore = DijkstraIgnore.Type;
				map.IgnoreType = typeof(BoardChar);
				if (map.RollDown(this.YPosition, this.XPosition, ref dir))
					Move(dir);
				else
				{
					Program.WriteLine("{0} couldn't find target at LKP {1}, wandering...", this.ID, ScriptPathTarget.Hotspots[0].ToString());
					MoveSpeed = 2;
					Movement = Motor.Wander;
				}
				if (CanSee(target))
				{
					var lastPos = Character.Path("targetlastpos");
					lastPos.GetToken("x").Value = target.XPosition;
					lastPos.GetToken("y").Value = target.YPosition;
				}
			}
			else if (distance <= 20 && CanSee(target))
			{
				var lastPos = Character.Path("targetlastpos");
				if (lastPos == null)
				{
					lastPos = Character.AddToken("targetlastpos");
					lastPos.AddToken("x");
					lastPos.AddToken("y");
				}
				lastPos.GetToken("x").Value = target.XPosition;
				lastPos.GetToken("y").Value = target.YPosition;
				if (ScriptPathTarget == null)
				{
					ScriptPathTarget = new Dijkstra();
				}
				ScriptPathTarget.Hotspots.Clear();
				ScriptPathTarget.Hotspots.Add(new Point(target.XPosition, target.YPosition));
				ScriptPathTarget.Update();
				Program.WriteLine("{0} updates LKP to {1} (can see)", this.ID, ScriptPathTarget.Hotspots[0].ToString());

				//Try to move closer. I WANT TO HIT THEM WITH MY SWORD!
				var map = ScriptPathTarget; //target.DijkstraMap;
				var dir = Direction.North;
				map.Ignore = DijkstraIgnore.Type;
				map.IgnoreType = typeof(BoardChar);
				if (map.RollDown(this.YPosition, this.XPosition, ref dir))
					Move(dir);
			}
			else
			{
				/*
				//If we're out of range, switch back to wandering.
				MoveSpeed = 2;
				Movement = Motor.Wander;
				if (!string.IsNullOrWhiteSpace(Sector) && Sector != "<null>")
					Movement = Motor.WanderSector;
				//TODO: go back to assigned sector.
				return;
				*/
			}
		}

		public void CallTo(BoardChar target)
		{
			MoveSpeed = 0;
			Movement = Motor.Hunt;
			var lastPos = Character.Path("targetlastpos");
			if (lastPos == null)
			{
				lastPos = Character.AddToken("targetlastpos");
				lastPos.AddToken("x");
				lastPos.AddToken("y");
			}
			lastPos.GetToken("x").Value = target.XPosition;
			lastPos.GetToken("y").Value = target.YPosition;
			Program.WriteLine("{0} called to action.", this.Character.Name);
		}

		public virtual bool MeleeAttack(BoardChar target)
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
			var obituary = "died from being struck down";
			var attackerName = this.Character.GetNameOrTitle(false, true);
			var attackerFullName = this.Character.GetNameOrTitle(true, true);
			var targetName = target.Character.GetNameOrTitle(false, true);
			var targetFullName = target.Character.GetNameOrTitle(true, true);
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
				damage = (float)Random.Next((int)minimalDamage, (int)baseDamage);
			}
			else
			{
				//Just use baseDamage until later.
				damage = baseDamage;
			}

			if (this.Character.Path("prefixes/vorpal") != null)
				damage *= 1.5f;
			if (this.Character.Path("prefixes/underfed") != null)
				damage *= 0.25f;

			//Account for armor and such

			damage *= GetDefenseFactor(weaponData, target.Character);

			//Add some randomization
			//Determine dodges

			if (target.Character.HasToken("helpless"))
			{
				damage = target.Character.GetToken("health").Value + 1;
				dodged = false;
			}

			if (dodged)
			{
				NoxicoGame.AddMessage((target is Player ? targetName.InitialCase() : "You") + " dodged " + (target is Player ? attackerName + "'s" : "your") + " attack.");
				return false;
			}

			if (damage > 0)
			{
				NoxicoGame.AddMessage((target is Player ? attackerName.InitialCase() : "You") + ' ' + verb + ' ' + (target is Player ? "you" : targetName) + " for " + damage + " point" + (damage > 1 ? "s" : "") + ".");
				Character.IncreaseSkill(skill);
			}
			if (target.Hurt(damage, obituary + " by " + attackerFullName, this, true))
			{
				//Gain a bonus from killing the target?
				return true;
			}
			return false;
		}

		public float GetDefenseFactor(Token weaponToken, Noxico.Character target)
		{
			var ret = 0f;
			var attackType = 0; //punch
			var skinType = 0; //skin
			if (weaponToken == null)
			{
				if (this.Character.Path("skin/type").Text == "fur" /* or the character has nails? */)
					attackType = 1; //tear
				else if (this.Character.HasToken("snaketail"))
					attackType = 2; //strike
				else if (this.Character.HasToken("quadruped") || this.Character.HasToken("taur"))
					attackType = 3; //kick
				//monoceros check?
			}
			else
			{
				var wat = weaponToken.HasToken("attacktype") ? weaponToken.GetToken("attacktype").Text : "strike";
				var types = new[] { "punch", "tear", "strike", "kick", "stab", "pierce", "crush" };
				for (var i = 0; i < types.Length; i++)
				{
					if (wat == types[i])
					{
						attackType = i;
						break;
					}
				}
			}

			var skinTypes = new[] { "skin", "fur", "scales", "metal", "slime", "rubber" };
			var tst = target.Path("skin/type").Text;
			if (tst == "carapace")
				tst = "scales";
			for (var i = 0; i < skinTypes.Length; i++)
			{
				if (tst == skinTypes[i])
				{
					skinType = i;
					break;
				}
			}

			var factors = new[,]
			{ //skin   fur    scales metal slime  rubber
			  { 1,     1,     1,     0.5f, 0.75f, 0.5f }, //punch
 			  { 1,     1,     0.5f,  0.5f, 0.75f, 2    }, //tear
			  { 1,     1,     1,     0.5f, 0.75f, 0.5f }, //strike
			  { 1.1f,  1.1f,  1.1f,  0.6f, 0.78f, 0.6f }, //kick (punch harder)
 			  { 1.5f,  1.5f,  1.2f,  0.5f, 0.75f, 1    }, //stab
			  { 1.25f, 1.25f, 1.5f,  1,    0.75f, 1    }, //pierce
			  { 1.5f,  1.5f,  1,     1,    2,     0.5f }, //crush
			};

			ret = factors[attackType, skinType];
			//TODO: do something like the above for any armor or shield being carried by the defender

			return ret;
		}

		public virtual bool Hurt(float damage, string obituary, BoardChar aggressor, bool finishable = false, bool leaveCorpse = true)
		{
			RunScript(OnHurt, "damage", damage);
			var health = Character.GetToken("health").Value;
			if (health - damage <= 0)
			{
				if (finishable && !Character.HasToken("beast"))
				{
					if (!Character.HasToken("helpless"))
					{
						NoxicoGame.AddMessage((this is Player ? "You are" : Character.GetNameOrTitle() + " is") + " helpless!");
						Character.Tokens.Add(new Token() { Name = "helpless" } );
						return false;
					}
				}
				//Dead, but how?
				Character.GetToken("health").Value = 0;
				if (aggressor != null)
				{
					if (Character.HasToken("stolenfrom") && aggressor is Player)
					{
						aggressor.Character.GiveRenegadePoints(10);
					}
				}
				if (leaveCorpse)
					LeaveCorpse(obituary);
				this.ParentBoard.CheckCombatFinish();
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
				Name = Character.Name.ToString(true) + "'s remains",
				Description = "These are the remains of " + Character.Name.ToString(true) + " the " + Character.Title + ", who " + obituary + ".",
				XPosition = XPosition,
				YPosition = YPosition,
			};
			if (!Character.IsProperNamed)
			{
				corpse.Name = Character.GetTheTitle() + "'s remains";
				corpse.Description = "These are the remains of " + Character.GetTheTitle() + ", who " + obituary + ".";
			}

			//Scatter belongings, if any -- BUT NOT FOR THE PLAYER so the infodump'll have items to list (thanks jAvel!)
			var items = Character.GetToken("items");
			if (items != null && items.Tokens.Count > 0 && !(this is Player))
			{
				while (items.Tokens.Count > 0)
				{
					var itemToken = items.Tokens[0];
					var knownItem = NoxicoGame.KnownItems.First(i => i.ID == itemToken.Name);
					if (knownItem == null)
						continue;
					itemToken.RemoveToken("equipped");
					knownItem.Drop(this, itemToken);
				}
			}

			ParentBoard.EntitiesToRemove.Add(this);
			ParentBoard.EntitiesToAdd.Add(corpse);
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "BCHR");
			base.SaveToFile(stream);
			stream.Write((byte)Movement);
			stream.Write(Sector ?? "<null>");
			stream.Write(Pairing ?? "<null>");
			stream.Write((byte)MoveTimer);
			Character.SaveToFile(stream);
		}

		public static new BoardChar LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "BCHR", "boardchar entity");
			var e = Entity.LoadFromFile(stream);
			var newChar = new BoardChar()
			{
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Flow = e.Flow, Blocking = e.Blocking,
			};
			newChar.Movement = (Motor)stream.ReadByte();
			newChar.Sector = stream.ReadString();
			newChar.Pairing = stream.ReadString();
			newChar.MoveTimer = stream.ReadByte();
			newChar.Character = Character.LoadFromFile(stream);
			newChar.AdjustView();
			newChar.ReassignScripts();
			newChar.RestockVendor();
			return newChar;
		}

		public void ReassignStuff(Name oldName)
		{
			var oldID = oldName.FirstName;
			var newID = this.Character.Name.FirstName;
			foreach (var thing in this.ParentBoard.Entities.OfType<Clutter>().Where(x => x.ID.Contains(oldID)))
			{
				thing.ID = thing.ID.Replace(oldID, newID);
				thing.Description = thing.Description.Replace(oldName.ToString(true), this.Character.Name.ToString(true));
				thing.Description = thing.Description.Replace(oldName.ToString(), this.Character.Name.ToString());
			}
		}

		private void SetupJint()
		{
			js = JavaScript.Create();

			JavaScript.Ascertain(js);
			js.SetParameter("this", this.Character);
			js.SetParameter("thisEntity", this);
			js.SetParameter("playerEntity", NoxicoGame.HostForm.Noxico.Player);
			js.SetParameter("target", ScriptPathID);
			js.SetParameter("Random", typeof(Random));
			js.SetParameter("BoardType", typeof(BoardType));
			js.SetParameter("Character", typeof(Character));
			js.SetParameter("BoardChar", typeof(BoardChar));
			js.SetParameter("InventoryItem", typeof(InventoryItem));
			js.SetParameter("Tile", typeof(Tile));
			js.SetParameter("Color", typeof(Color));
			js.SetFunction("sound", new Action<string>(x => NoxicoGame.Sound.PlaySound(x)));
			js.SetFunction("corner", new Action<string>(x => NoxicoGame.AddMessage(x)));
			js.SetFunction("print", new Action<string>(x =>
			{
				var paused = true;
				MessageBox.ScriptPauseHandler = () =>
				{
					paused = false;
				};
				MessageBox.Notice(x, true, this.Character.Name.ToString(true));
				while (paused)
				{
					NoxicoGame.HostForm.Noxico.Update();
					System.Windows.Forms.Application.DoEvents();
				}
			}));
			js.SetFunction("FindTargetBoardByName", new Func<string, int>(x =>
			{
				if (!NoxicoGame.TargetNames.ContainsValue(x))
					return -1;
				var i = NoxicoGame.TargetNames.First(b => b.Value == x);
				return i.Key;
			}));

			var makeBoardTarget = new Action<Board>(board =>
			{
				if (string.IsNullOrWhiteSpace(board.Name))
					throw new Exception("Board must have a name before it can be added to the target list.");
				if (NoxicoGame.TargetNames.ContainsKey(board.BoardNum))
					return; //throw new Exception("Board is already a travel target.");
				NoxicoGame.TargetNames.Add(board.BoardNum, board.Name);
			});
			var makeBoardKnown = new Action<Board>(board =>
			{
				if (!NoxicoGame.TargetNames.ContainsKey(board.BoardNum))
					throw new Exception("Board must be in the travel targets list before it can be known.");
				if (NoxicoGame.KnownTargets.Contains(board.BoardNum))
					return;
				NoxicoGame.KnownTargets.Add(board.BoardNum);
			});

			js.SetFunction("MakeBoardTarget", makeBoardTarget);
			js.SetFunction("MakeBoardKnown", makeBoardKnown);
			js.SetFunction("GetBoard", new Func<int, Board>(x => NoxicoGame.HostForm.Noxico.GetBoard(x)));
			js.SetFunction("GetBiomeByName", new Func<string, int>(BiomeData.ByName));
			js.SetFunction("CreateTown", new Func<int, string, string, bool, Board>(WorldGen.CreateTown));
			js.SetFunction("ExpectTown", new Func<string, int, Expectation>(Expectation.ExpectTown));
			js.SetParameter("Expectations", NoxicoGame.Expectations);
			js.SetParameter("scheduler", this.scheduler);
			js.SetParameter("Task", typeof(Task));
			js.SetParameter("TaskType", typeof(TaskType));
			js.SetParameter("Token", typeof(Token));
		}

		public bool RunScript(string script, string extraParm = "", float extraVal = 0)
		{
			if (string.IsNullOrWhiteSpace(script))
				return true;
			if (js == null)
				SetupJint();

			Board.DrawJS = js;
			if (!string.IsNullOrEmpty(extraParm))
				js.SetParameter(extraParm, extraVal);
			var r = js.Run(script);
			if (r is bool)
				return (bool)r;
			return true;
		}

		[ForJS(ForJSUsage.Either)]
		public void MoveTo(int x, int y, string target)
		{
			JavaScript.Assert();

			ScriptPathTarget = new Dijkstra();
			ScriptPathTarget.Hotspots.Add(new Point(x, y));
			ScriptPathTarget.Update();
			ScriptPathID = target;
			ScriptPathTargetX = x;
			ScriptPathTargetY = y;
			ScriptPathing = true;
		}

		[ForJS(ForJSUsage.Either)]
		public void AssignScripts(string id)
		{
			var xml = Mix.GetXMLDocument("uniques.xml");
			var planSource = xml.SelectSingleNode("//uniques/character[@id=\"" + id + "\"]") as System.Xml.XmlElement;
			var scripts = planSource.SelectNodes("script").OfType<System.Xml.XmlElement>();
			foreach (var script in scripts)
			{
				var target = script.GetAttribute("target");
				switch (target)
				{
					case "tick":
						OnTick = script.InnerText;
						break;
					case "load":
						OnLoad = script.InnerText;
						break;
					case "bump":
					case "playerbump":
						OnPlayerBump = script.InnerText;
						break;
					case "hurt":
						OnHurt = script.InnerText;
						break;
					case "path":
					case "pathfinish":
						OnPathFinish = script.InnerText;
						break;
				}
			}
			this.Character.RemoveToken("script");
			this.Character.AddToken("script", 0, id);
		}
		public void ReassignScripts()
		{
			var scriptSource = this.Character.Path("script");
			if (scriptSource == null)
				return;
			AssignScripts(scriptSource.Text);
		}

		public void AimShot(Entity target)
		{
			var weapon = Character.CanShoot();
			if (weapon == null)
				return;
			var weap = weapon.GetToken("weapon");
			var skill = weap.GetToken("skill");
			if (new[] { "throwing", "small_firearm", "large_firearm", "huge_firearm" }.Contains(skill.Text))
			{
				if (weap.HasToken("ammo"))
				{
					var ammoName = weap.GetToken("ammo").Text;
					var carriedAmmo = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == ammoName);
					if (carriedAmmo == null)
						return;
					var knownAmmo = NoxicoGame.KnownItems.Find(ki => ki.ID == ammoName);
					if (knownAmmo == null)
						return;
					knownAmmo.Consume(Character, carriedAmmo);
				}
				else if (weapon.HasToken("charge"))
				{
					var carriedGun = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == weapon.ID && ci.HasToken("equipped"));
					weapon.Consume(Character, carriedGun);
				}
				if (weapon != null)
					FireLine(weapon.Path("effect"), target);
			}
			else
			{
				Program.WriteLine("{0} tried to throw a weapon.", this.Character.GetNameOrTitle(false, true, true));
				return;
			}
			var aimSuccess = true; //TODO: make this skill-relevant.
			if (aimSuccess)
			{
				var damage = weap.Path("damage").Value;
				this.Character.IncreaseSkill(skill.Text);
				if (target is Player)
				{
					var hit = target as Player;
					NoxicoGame.AddMessage(string.Format("{0} hit you for {1} point{2}.", this.Character.GetNameOrTitle(false, true, true), damage, damage > 1 ? "s" : ""));
					hit.Hurt(damage, "being shot down by " + this.Character.Name.ToString(true), this, false);
					return;
				}
			}
			NoxicoGame.Mode = UserMode.Walkabout;
		}

		public void FireLine(Token effect, int x, int y)
		{
			if (effect == null)
				return;
			foreach (var point in Toolkit.Line(XPosition, YPosition, x, y))
			{
				var particle = new Clutter()
				{
					ParentBoard = this.ParentBoard,
					ForegroundColor = Color.FromName(effect.GetToken("fore").Text),
					BackgroundColor = Color.FromName(effect.GetToken("back").Text),
					AsciiChar = (char)effect.GetToken("char").Value,
					Blocking = false,
					XPosition = point.X,
					YPosition = point.Y,
					Life = 2 + Random.Next(2),
				};
				this.ParentBoard.EntitiesToAdd.Add(particle);
			}
		}

		public void FireLine(Token effect, Entity target)
		{
			if (effect != null)
				FireLine(effect, target.XPosition, target.YPosition);
		}
		
		public void RestockVendor()
		{
			var vendor = Character.Path("role/vendor");
			if (vendor == null)
				return;
			if (!vendor.HasToken("lastrestockday"))
				vendor.AddToken("lastrestockday", NoxicoGame.InGameTime.DayOfYear - 1);
			var lastRestockDay = vendor.GetToken("lastrestockday").Value;
			var today = NoxicoGame.InGameTime.DayOfYear;
			if (lastRestockDay >= today)
				return;
			Program.WriteLine("{0} ({1}) restocking...", Character.Name, vendor.GetToken("class").Text);
			vendor.GetToken("lastrestockday").Value = today;
			var items = Character.Path("items");
			var diff = 20 - items.Tokens.Count;
			if (diff > 0)
				Character.GetToken("money").Value += diff * 50; //?
			var filters = new Dictionary<string, string>();
			filters["vendorclass"] = vendor.GetToken("class").Text;
			while (items.Tokens.Count < 20)
			{
				var newstock = WorldGen.GetRandomLoot("vendor", "stock", filters);
				if (newstock.Count == 0)
					break;
				items.AddSet(newstock);
			}
		}
	}

    public class Player : BoardChar
    {
		public bool AutoTravelling { get; set; }
		private Dijkstra AutoTravelMap;
		public TimeSpan PlayingTime { get; set; }
		public int CurrentRealm { get; private set; }

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

		public override void Draw()
		{
			base.Draw();
			if (Character.HasToken("flying"))
			{
				var flightTimer = string.Format(" - Flight: {0:00}% - ", Math.Floor((Character.GetToken("flying").Value / 100) * 100));
				NoxicoGame.HostForm.Write(flightTimer, Color.FromName("CornflowerBlue"), Color.Black, 40 - (flightTimer.Length / 2), 0);
			}
		}

		public bool OnWarp()
		{
			var warp = ParentBoard.Warps.Find(w => w.XPosition == XPosition && w.YPosition == YPosition);
			return warp != null;
		}

		public void CheckWarps()
		{
			var warp = ParentBoard.Warps.Find(w => /* !String.IsNullOrEmpty(w.TargetBoard) && */ w.XPosition == XPosition && w.YPosition == YPosition);
			if (warp != null)
			{
				if (warp.TargetBoard == -1) //ungenerated dungeon
				{
					/*
					NoxicoGame.Mode = UserMode.Subscreen;
					NoxicoGame.Subscreen = UnsortedSubscreens.CreateDungeon;
					UnsortedSubscreens.DungeonGeneratorEntranceBoardNum = ParentBoard.BoardNum;
					UnsortedSubscreens.DungeonGeneratorEntranceWarpID = warp.ID;
					UnsortedSubscreens.DungeonGeneratorBiome = (int)ParentBoard.GetToken("biome").Value;
					Subscreens.FirstDraw = true;
					*/
					WorldGen.DungeonGeneratorEntranceBoardNum = ParentBoard.BoardNum;
					WorldGen.DungeonGeneratorEntranceWarpID = warp.ID;
					WorldGen.DungeonGeneratorBiome = (int)ParentBoard.GetToken("biome").Value;
					WorldGen.CreateDungeon();
					return;
				}
				else if (warp.TargetBoard == -2) //unconnected dungeon
				{
					Travel.Open();
					return;
				}

				var game = NoxicoGame.HostForm.Noxico;
				var targetBoard = game.GetBoard(warp.TargetBoard); //game.Boards[warp.TargetBoard]; //.Find(b => b.ID == warp.TargetBoard);

				var sourceBoard = ParentBoard;

				ParentBoard.EntitiesToRemove.Add(this);
				game.CurrentBoard = targetBoard;
				ParentBoard = targetBoard;
				ParentBoard.Entities.Add(this);
				var twarp = targetBoard.Warps.Find(w => w.ID == warp.TargetWarpID);
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
				ParentBoard.UpdateLightmap(this, true);
				ParentBoard.Redraw();
				ParentBoard.PlayMusic();
				NoxicoGame.Immediate = true;

				//Going from a dungeon to a wild board?
				if (targetBoard.GetToken("type").Value == 0 && sourceBoard.GetToken("type").Value == 2)
					game.FlushDungeons();

			}
		}

		public void OpenBoard(int index)
		{
			var n = NoxicoGame.HostForm.Noxico;
			this.ParentBoard.EntitiesToRemove.Add(this);
			this.ParentBoard.SaveToFile(this.ParentBoard.BoardNum);
			this.ParentBoard = n.GetBoard(index);
			n.CurrentBoard = this.ParentBoard;
			this.ParentBoard.Entities.Add(this);
			ParentBoard.CheckCombatStart();
			ParentBoard.CheckCombatFinish();
			ParentBoard.UpdateLightmap(this, true);
			ParentBoard.Redraw();
			ParentBoard.PlayMusic();
			NoxicoGame.Immediate = true;

			this.DijkstraMap.UpdateWalls(ParentBoard);
			this.DijkstraMap.Update();
			this.AutoTravelMap.UpdateWalls(ParentBoard);
		}

		public override bool MeleeAttack(BoardChar target)
		{
			var mySpeed = this.Character.GetStat(Stat.Speed);
			var theirSpeed = target.Character.GetStat(Stat.Speed);
			var meFirst = false;
			
			if (mySpeed > theirSpeed)
				meFirst = true;
			else if (mySpeed == theirSpeed)
				meFirst = Random.NextDouble() > 0.5;

			if (meFirst)
			{
				var killedThem = base.MeleeAttack(target);
				if (!killedThem && !target.Character.HasToken("helpless"))
				{
					target.Character.AddToken("justmeleed");
					target.MeleeAttack(this);
				}
				return killedThem;
			}
			else
			{
				var killedMe = target.MeleeAttack(this);
				target.Character.AddToken("justmeleed");
				if (!killedMe && !this.Character.HasToken("helpless"))
					return base.MeleeAttack(target);
				return false;
			}
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var lx = XPosition;
			var ly = YPosition;

			check = SolidityCheck.Walker;
			if (Character.HasToken("flying"))
				check = SolidityCheck.Flyer;

			#region Inter-board travel
			var n = NoxicoGame.HostForm.Noxico;
			Board otherBoard = null;
			if (ly == 0 && targetDirection == Direction.North && this.ParentBoard.ToNorth > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToNorth);
				if (this.CanMove(otherBoard, lx, 24, check) != null)
					return;
				this.YPosition = 24;
				OpenBoard(this.ParentBoard.ToNorth);
			}
			else if (ly == 24 && targetDirection == Direction.South && this.ParentBoard.ToSouth > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToSouth);
				if (this.CanMove(otherBoard, lx, 0, check) != null)
					return;
				this.YPosition = 0;
				OpenBoard(this.ParentBoard.ToSouth);
			}
			else if (lx == 0 && targetDirection == Direction.West && this.ParentBoard.ToWest > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToWest);
				if (this.CanMove(otherBoard, 79, ly, check) != null)
					return;
				this.XPosition = 79;
				OpenBoard(this.ParentBoard.ToWest);
			}
			else if (lx == 79 && targetDirection == Direction.East && this.ParentBoard.ToEast > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToEast);
				if (this.CanMove(otherBoard, 0, ly, check) != null)
					return;
				this.XPosition = 0;
				OpenBoard(this.ParentBoard.ToEast);
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
					if (entity is BoardChar)
					{
						var bc = (BoardChar)entity;
						if (bc.Character.HasToken("hostile"))
						{
							//Strike at your foes!
							AutoTravelling = false;
							MeleeAttack(bc);
							EndTurn();
							return;
						}
						if (!string.IsNullOrWhiteSpace(bc.OnPlayerBump))
						{
							bc.RunScript(bc.OnPlayerBump);
							return;
						}
						//Displace!
						NoxicoGame.AddMessage("You displace " + bc.Character.GetNameOrTitle(false, true) + ".");
						bc.XPosition = this.XPosition;
						bc.YPosition = this.YPosition;
					}
				}
			}
			base.Move(targetDirection, check);

			EndTurn();
			if (this.Character.HasToken("slow"))
				EndTurn();

			if (this.Character.HasToken("haste"))
			{
				var haste = this.Character.GetToken("haste");
				haste.Value = (int)haste.Value ^ 1;
			}

			NoxicoGame.Sound.PlaySound(Character.HasToken("squishy") || Character.Path("skin/type/slime") != null ? "Splorch" : "Step");

			if (lx != XPosition || ly != YPosition)
			{
				this.DijkstraMap.Hotspots[0] = new Point(XPosition, YPosition);
				this.DijkstraMap.Update();
				//this.DijkstraMap.SaveToPNG();
			}
			else if (AutoTravelling)
			{
				AutoTravelling = false;
				NoxicoGame.AddMessage("* TEST: couldn't go any further. *");
			}

			NoxicoGame.ContextMessage = null;
			if (OnWarp())
				NoxicoGame.ContextMessage = "take exit";
			else if (ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
				NoxicoGame.ContextMessage = "pick up";
			else if (ParentBoard.Entities.OfType<Container>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
				NoxicoGame.ContextMessage = "see contents";
			else if (/* Character.GetToken("health").Value < Character.GetMaximumHealth() && */ ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.AsciiChar == '\x0398') != null)
				NoxicoGame.ContextMessage = "sleep";
			if (NoxicoGame.ContextMessage != null)
				NoxicoGame.ContextMessage = Toolkit.TranslateKey(KeyBinding.Activate, false, false) + " - " + NoxicoGame.ContextMessage;
		}

		public void QuickFire(Direction targetDirection)
		{
			NoxicoGame.Modifiers[0] = false;
			if (this.ParentBoard.BoardType == BoardType.Town && !this.ParentBoard.HasToken("combat"))
				return;
			var weapon = Character.CanShoot();
			if (weapon == null)
				return; //Don't whine about it.

			var weap = weapon.GetToken("weapon");
			if (weap.HasToken("ammo"))
			{
				var ammoName = weap.GetToken("ammo").Text;
				var carriedAmmo = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == ammoName);
				if (carriedAmmo == null)
					return;
				var knownAmmo = NoxicoGame.KnownItems.Find(ki => ki.ID == ammoName);
				if (knownAmmo == null)
					return;
				knownAmmo.Consume(Character, carriedAmmo);
			}
			else if (weapon.HasToken("charge"))
			{
				var carriedGun = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == weapon.ID && ci.HasToken("equipped"));
				weapon.Consume(Character, carriedGun);
			}

			if (weapon == null)
				return;

			var x = this.XPosition;
			var y = this.YPosition;
			var distance = 0;
			var range = (int)weapon.Path("weapon/range").Value;
			var damage = (int)weapon.Path("weapon/damage").Value;
			var skill = weap.GetToken("skill").Text;
			Func<int, int, bool> gotHit = (xPos, yPos) =>
			{
				if (this.ParentBoard.IsSolid(y, x, SolidityCheck.Projectile))
				{
					FireLine(weapon.Path("effect"), x, y);
					return true;
				}
				var hit = this.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(e => e.XPosition == x && e.YPosition == y);
				if (hit != null)
				{
					FireLine(weapon.Path("effect"), x, y);
					NoxicoGame.AddMessage(string.Format("You hit {0} for {1} point{2}.", hit.Character.GetNameOrTitle(false, true), damage, damage > 1 ? "s" : ""));
					hit.Hurt(damage, "being shot down by " + this.Character.Name.ToString(true), this, false);
					this.Character.IncreaseSkill(skill);
					return true;
				}
				return false;
			};

			if (targetDirection == Direction.East)
			{
				for (x++; x < 80 && distance < range; x++, distance++)
					if (gotHit(x, y))
						break;
			}
			else if (targetDirection == Direction.West)
			{
				for (x--; x >= 0 && distance < range; x--, distance++)
					if (gotHit(x, y))
						break;
			}
			else if (targetDirection == Direction.South)
			{
				for (y++; x < 80 && distance < range; y++, distance++)
					if (gotHit(x, y))
						break;
			}
			else if (targetDirection == Direction.North)
			{
				for (y--; y >= 0 && distance < range; y--, distance++)
					if (gotHit(x, y))
						break;
			}
		}

		public override void Update()
        {
            //base.Update();
			if (NoxicoGame.Mode != UserMode.Walkabout)
				return;

			var sleeping = Character.Path("sleeping");
			if (sleeping != null)
			{
				Character.GetToken("health").Value += 2;
				sleeping.Value--;
				if (sleeping.Value <= 0)
				{
					Character.RemoveToken("sleeping");
					Character.RemoveToken("helpless");
					NoxicoGame.AddMessage("You get back up.");
					if (Character.GetToken("health").Value > Character.GetMaximumHealth())
						Character.GetToken("health").Value = Character.GetMaximumHealth();
				}
				NoxicoGame.InGameTime.AddMinutes(5);
				EndTurn();
			}

			var helpless = Character.HasToken("helpless");
			if (helpless)
			{
				if (Random.NextDouble() < 0.05)
				{
					Character.GetToken("health").Value += 2;
					NoxicoGame.AddMessage("You get back up.");
					Character.RemoveToken("helpless");
					helpless = false;
				}
			}
			var flying = Character.HasToken("flying");

#if DEBUG
			if (NoxicoGame.KeyMap[(int)Keys.Z])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.InGameTime.AddMinutes(30);
			}
#endif

			//START
			if (NoxicoGame.IsKeyDown(KeyBinding.Pause) || Vista.Triggers == XInputButtons.Start)
			{
				NoxicoGame.ClearKeys();
				Pause.Open();
				return;
			}

			//RIGHT
			if ((NoxicoGame.IsKeyDown(KeyBinding.Travel) || Vista.Triggers == XInputButtons.RightShoulder) && this.ParentBoard.AllowTravel)
			{
				NoxicoGame.ClearKeys();
				Travel.Open();
				return;
			}

			//LEFT
			if (NoxicoGame.IsKeyDown(KeyBinding.Rest) || Vista.Triggers == XInputButtons.LeftShoulder)
			{
				NoxicoGame.ClearKeys();
				if (this.Character.HasToken("haste"))
					this.Character.GetToken("haste").Value = 0;
					EndTurn();
				return;
			}

			//GREEN
			if (NoxicoGame.IsKeyDown(KeyBinding.Interact) || Vista.Triggers == XInputButtons.A)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.AddMessage("\uE080[Aim message]");
				NoxicoGame.Mode = UserMode.Aiming;
				NoxicoGame.Cursor.ParentBoard = this.ParentBoard;
				NoxicoGame.Cursor.XPosition = this.XPosition;
				NoxicoGame.Cursor.YPosition = this.YPosition;
				NoxicoGame.Cursor.PopulateTabstops();
				NoxicoGame.Cursor.Point();
				return;
			}
			
			//BLUE
			if (NoxicoGame.IsKeyDown(KeyBinding.Items) || Vista.Triggers == XInputButtons.X)
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.Mode = UserMode.Subscreen;
				NoxicoGame.Subscreen = Inventory.Handler;
				Subscreens.FirstDraw = true;
				return;
			}

			//YELLOW
			if ((NoxicoGame.IsKeyDown(KeyBinding.Fly) || Vista.Triggers == XInputButtons.Y) && !helpless)
			{
				NoxicoGame.ClearKeys();
				if (Character.HasToken("flying"))
				{
					//Land
					NoxicoGame.AddMessage("You land.");
					Character.RemoveToken("flying");
					//add swim capability?
					var tile = ParentBoard.Tilemap[XPosition, YPosition];
					if (tile.Water)
						Hurt(9999, "dove into the water and drowned", null, false);
					else if (tile.Cliff)
						Hurt(9999, "dove into the depths", null, false, false);
				}
				else
				{
					if (Character.HasToken("wings"))
					{
						if (Character.GetToken("wings").HasToken("small"))
						{
							NoxicoGame.AddMessage("Your wings are too small.");
							return;
						}
						var tile = ParentBoard.Tilemap[XPosition, YPosition];
						if (tile.Ceiling)
						{
							if (Character.GetStat(Stat.Cunning) < 10 ||
								(Character.GetStat(Stat.Cunning) < 20 && Random.NextDouble() < 0.5))
							{
								Hurt(2, "hit the ceiling when trying to fly", null, false);
								NoxicoGame.AddMessage("You hit your head on the ceiling.");
							}
							else
								NoxicoGame.AddMessage("You can't fly here - there's a ceiling in the way.");
							return;
						}
						//Take off
						Character.AddToken("flying").Value = 100;
						NoxicoGame.AddMessage("You take off.");
						return;
					}
					NoxicoGame.AddMessage("You have no wings.");
				}
				return;
			}

			//RED
			if ((NoxicoGame.IsKeyDown(KeyBinding.Activate) || Vista.Triggers == XInputButtons.B) && !helpless && !flying)
			{
				NoxicoGame.ClearKeys();

				if (OnWarp())
					CheckWarps();

				var container = ParentBoard.Entities.OfType<Container>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition);
				if (container != null)
				{
					NoxicoGame.ClearKeys();
					ContainerMan.Setup(container);
					return;
				}

				//Find dropped items
				var drop = ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition);
				if (drop != null)
				{
					drop.Take(this.Character);
					NoxicoGame.AddMessage("You pick up " + drop.Item.ToString(drop.Token, true) + ".", drop.ForegroundColor);
					NoxicoGame.Sound.PlaySound("Get Item");
					ParentBoard.Redraw();
					return;
				}

				//Find bed
				var bed = ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.AsciiChar == '\x0398');
				if (bed != null)
				{
					var prompt = "It's " + NoxicoGame.InGameTime.ToShortTimeString() + ", " + NoxicoGame.InGameTime.ToLongDateString() + ". Sleep for how long?";
					var options = new Dictionary<object, string>();
					foreach (var interval in new[] { 1, 2, 4, 8, 12 })
						options[interval] = Toolkit.Count(interval).Titlecase() + (interval == 1 ? " hour" : " hours");
					options[-1] = "Cancel";
					MessageBox.List(prompt, options, () =>
					{
						if ((int)MessageBox.Answer != -1)
						{
							Character.AddToken("helpless");
							Character.AddToken("sleeping").Value = (int)MessageBox.Answer * 12;
						}
					}, true, true, "Bed");
				}
				return;
			}

#if DEBUG
			if (NoxicoGame.KeyMap[(int)Keys.F3])
			{
				NoxicoGame.ClearKeys();
				ParentBoard.DumpToHTML();
				NoxicoGame.AddMessage("Board dumped.");
				return;
			}
#endif
			if (helpless)
			{
				EndTurn();
				return;
			}

			if (!AutoTravelling)
			{
				if (!NoxicoGame.Modifiers[0])
				{
					if (NoxicoGame.IsKeyDown(KeyBinding.Left) || Vista.DPad == XInputButtons.Left)
						this.Move(Direction.West);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Right) || Vista.DPad == XInputButtons.Right)
						this.Move(Direction.East);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Up) || Vista.DPad == XInputButtons.Up)
						this.Move(Direction.North);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Down) || Vista.DPad == XInputButtons.Down)
						this.Move(Direction.South);
				}
				else if(NoxicoGame.Modifiers[0])
				{
					//Program.WriteLine("shift");
					if (NoxicoGame.IsKeyDown(KeyBinding.Left))
						this.QuickFire(Direction.West);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Right))
						this.QuickFire(Direction.East);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Up))
						this.QuickFire(Direction.North);
					else if (NoxicoGame.IsKeyDown(KeyBinding.Down))
						this.QuickFire(Direction.South);
				}
			}
			else
			{
				if (NoxicoGame.IsKeyDown(KeyBinding.Left) || NoxicoGame.IsKeyDown(KeyBinding.Right) || NoxicoGame.IsKeyDown(KeyBinding.Up) || NoxicoGame.IsKeyDown(KeyBinding.Down))//(NoxicoGame.KeyMap[(int)Keys.Left] || NoxicoGame.KeyMap[(int)Keys.Right] || NoxicoGame.KeyMap[(int)Keys.Up] || NoxicoGame.KeyMap[(int)Keys.Down])
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
			Excite();
			Hunger();
			if (Character.UpdatePregnancy())
				return;

			var five = new TimeSpan(0,0,5);
			PlayingTime = PlayingTime.Add(five);
			if (!(this.Character.HasToken("haste") && this.Character.GetToken("haste").Value == 0))
			{
				var wasNight = Toolkit.IsNight();
				NoxicoGame.InGameTime.Add(five);
				ParentBoard.UpdateLightmap(this, true);
				//stupid bug, using != instead of && caused Redraw to not be caled for too long.
				if (wasNight && !Toolkit.IsNight())
					ParentBoard.Redraw();
			}

			if (Character.HasToken("flying"))
			{
				var f = Character.GetToken("flying");
				f.Value--;
				if (!Character.HasToken("wings") || Character.GetToken("wings").HasToken("small"))
				{
					NoxicoGame.AddMessage("You lose your ability to fly!");
					f.Value = -10;
				}
				if (f.Value <= 0)
					NoxicoGame.KeyMap[(int)NoxicoGame.KeyBindings[KeyBinding.Fly]] = true; //force a landing
			}


			NoxicoGame.AutoRestTimer = NoxicoGame.AutoRestSpeed;
			if (ParentBoard == null)
			{
				return;
			}
			ParentBoard.Update(true);
			if (ParentBoard.IsBurning(YPosition, XPosition))
				Hurt(10, "burned to death", null, false, false);
			//Leave EntitiesToAdd/Remove to Board.Update next passive cycle.


#if DEBUG
			NoxicoGame.HostForm.Text = string.Format("Noxico - {0} ({1}x{2}) @ {3} {4}", ParentBoard.Name, XPosition, YPosition, NoxicoGame.InGameTime.ToLongDateString(), NoxicoGame.InGameTime.ToShortTimeString());
#endif
			//NoxicoGame.UpdateMessages();
		}

		public override bool Hurt(float damage, string obituary, BoardChar aggressor, bool finishable = false, bool leaveCorpse = true)
		{
			if (AutoTravelling)
			{
				NoxicoGame.AddMessage("Autotravel interrupted.");
				AutoTravelling = false;
			}
			var dead = base.Hurt(damage, obituary, aggressor, finishable);
			NoxicoGame.HealthMessage();
			if (dead)
			{
				if (aggressor != null)
				{
					var relation = Character.Path("ships/" + aggressor.Character.ID);
					if (relation == null)
					{
						relation = new Token(aggressor.Character.ID);
						Character.Path("ships").Tokens.Add(relation);
					}
					relation.AddToken("killer");
				}
				Character.AddToken("gameover");

				NoxicoGame.AddMessage("GAME OVER", Color.Red);
				var playerFile = Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "player.bin");
				File.Delete(playerFile);
				NoxicoGame.Sound.PlayMusic("set://Death");
				MessageBox.Ask(
					"You " + obituary + ".\n\nWould you like an infodump on the way out?",
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

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "PLAY");
			base.SaveToFile(stream);
			stream.Write(PlayingTime.Ticks);
			stream.Write(CurrentRealm);
		}

		public static new Player LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "PLAY", "player entity");
			var e = BoardChar.LoadFromFile(stream);
			var newChar = new Player()
			{
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Flow = e.Flow, Blocking = e.Blocking,
				Character = e.Character,
			};
			newChar.PlayingTime = new TimeSpan(stream.ReadInt64());
			newChar.CurrentRealm = stream.ReadInt32();
			return newChar;
		}

		public new void AimShot(Entity target)
		{
			//TODO: throw whatever is being held by the player at the target, according to their Throwing skill and the total distance.
			//If it's a gun they're holding, fire it instead, according to their Shooting skill.
			//MessageBox.Message("Can't shoot yet, sorry.", true);

			if (target is Player)
			{
				MessageBox.Notice("Dont shoot yourself in the foot!", true);
				return;
			}

			var weapon = Character.CanShoot();
			var weap = weapon.GetToken("weapon");
			var skill = weap.GetToken("skill");
			if (new[] { "throwing", "small_firearm", "large_firearm", "huge_firearm" }.Contains(skill.Text))
			{
				if (weap.HasToken("ammo"))
				{
					var ammoName = weap.GetToken("ammo").Text;
					var carriedAmmo = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == ammoName);
					if (carriedAmmo == null)
						return;
					var knownAmmo = NoxicoGame.KnownItems.Find(ki => ki.ID == ammoName);
					if (knownAmmo == null)
						return;
					knownAmmo.Consume(Character, carriedAmmo);
				}
				else if (weapon.HasToken("charge"))
				{
					var carriedGun = this.Character.GetToken("items").Tokens.Find(ci => ci.Name == weapon.ID && ci.HasToken("equipped"));
					weapon.Consume(Character, carriedGun);
				}
				if (weapon != null)
					FireLine(weapon.Path("effect"), target);
			}
			else
			{
				MessageBox.Notice("Can't throw yet, sorry.", true);
				return;
			}
			var aimSuccess = true; //TODO: make this skill-relevant.
			if (aimSuccess)
			{
				if (target is BoardChar)
				{
					var hit = target as BoardChar;
					var damage = weap.Path("damage").Value * GetDefenseFactor(weap, hit.Character);
					NoxicoGame.AddMessage(string.Format("You hit {0} for {1} point{2}.", hit.Character.GetNameOrTitle(false, true), damage, damage > 1 ? "s" : ""));
					hit.Hurt(damage, "being shot down by " + this.Character.Name.ToString(true), this, false);
				}
				this.Character.IncreaseSkill(skill.Text);
			}

			NoxicoGame.Mode = UserMode.Walkabout;
			EndTurn();
		}

		public void Hunger()
		{
			var lastSatiationChange = Character.Path("satiation/lastchange");
			if (lastSatiationChange == null)
			{
				lastSatiationChange = Character.GetToken("satiation").AddToken("lastchange");
				lastSatiationChange.AddToken("dayoftheyear", NoxicoGame.InGameTime.DayOfYear);
				lastSatiationChange.AddToken("hour", NoxicoGame.InGameTime.Hour);
				lastSatiationChange.AddToken("minute", NoxicoGame.InGameTime.Minute);
			}
			var lastSatiation = Character.GetToken("satiation").Value;
			if (lastSatiationChange.GetToken("dayoftheyear").Value < NoxicoGame.InGameTime.DayOfYear)
			{
				var days = NoxicoGame.InGameTime.DayOfYear - (int)lastSatiationChange.GetToken("dayoftheyear").Value;
				Character.Hunger(days * 2);
			}
			if (lastSatiationChange.GetToken("hour").Value < NoxicoGame.InGameTime.Hour)
			{
				var hours = NoxicoGame.InGameTime.Hour - (int)lastSatiationChange.GetToken("hour").Value;
				Character.Hunger(hours * 0.5f);
			}
			if (lastSatiationChange.GetToken("minute").Value < NoxicoGame.InGameTime.Minute)
			{
				var minutes = NoxicoGame.InGameTime.Minute - (int)lastSatiationChange.GetToken("minute").Value;
				Character.Hunger(minutes * 0.1f);
			}
			lastSatiationChange.GetToken("dayoftheyear").Value = NoxicoGame.InGameTime.DayOfYear;
			lastSatiationChange.GetToken("hour").Value = NoxicoGame.InGameTime.Hour;
			lastSatiationChange.GetToken("minute").Value = NoxicoGame.InGameTime.Minute;
			var newSatiation = Character.GetToken("satiation").Value;
			if (lastSatiation >= 20 && newSatiation < 20)
				NoxicoGame.AddMessage("You have become very hungry.");
			else if (lastSatiation > 0 && newSatiation == 0)
				NoxicoGame.AddMessage("You are starving.", Color.Red);
			else if (lastSatiation > newSatiation && newSatiation < 0)
			{
				Character.GetToken("satiation").Value = -1;
				//The hungry body turns against the stubborn mind...
				Hurt(2, "starved to death", null, false, true);
			}
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
			Program.WriteLine("Trying to move clutter.");
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
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Blocking = e.Blocking
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

		public DroppedItem(string item) : this(NoxicoGame.KnownItems.First(i => i.ID == item), null)
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
					var cga = new [] { "Black", "DarkBlue", "DarkGreen", "DarkCyan", "DarkRed", "Purple", "Brown", "Silver", "Gray", "Blue", "Green", "Cyan", "Red", "Magenta", "Yellow", "White" };
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
			Program.WriteLine("Trying to move dropped item.");
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