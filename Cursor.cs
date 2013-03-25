using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if DEBUG
using Keys = System.Windows.Forms.Keys;
#endif

namespace Noxico
{
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
}