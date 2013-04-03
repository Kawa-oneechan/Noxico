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
			if (NoxicoGame.Mode != UserMode.Aiming)
				return;
			NoxicoGame.HostForm.Cursor = new Point(XPosition, YPosition);
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			this.ParentBoard.DirtySpots.Add(new Location(XPosition, YPosition));
			this.ParentBoard.Draw(true);
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
			NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<cSilver>" + i18n.GetString("pointatsomething");
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
								NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<c" + ((BoardChar)entity).Character.Path("eyes").Text + ">" + i18n.GetString("eyesinthedark");
							}
							return;
						}
						else
						{
							//Player does have darkvision, ignore all this.
						}
					}

					PointingAt = entity;
					if (entity is BoardChar)
					{
						NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<c" + ((BoardChar)entity).Character.Path("eyes").Text + ">" + ((BoardChar)PointingAt).Character.ToString(); 
						return;
					}
					else if (entity is DroppedItem)
					{
						NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<cSilver>" + ((DroppedItem)PointingAt).Name;
						return;
					}
					else if (entity is Clutter || entity is Container)
					{
						NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<cSilver>" + (entity is Container ? ((Container)PointingAt).Name : ((Clutter)PointingAt).Name);
						return;
					}
					else if (entity is Door)
					{
						NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<cSilver>" + i18n.GetString("pointingatdoor");
						return;
					}
				}
			}
			var tSD = this.ParentBoard.GetSpecialDescription(YPosition, XPosition);
			if (tSD.HasValue)
			{
				PointingAt = null;
				NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<c" + tSD.Value.Color.Name + ">" + tSD.Value.Name;
				return;
			}
		}

		public override void Update()
		{
			base.Update();
			this.ParentBoard.Draw(true);

			if (NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				Hide();
				return;
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

					options["look"] = i18n.GetString("action_lookatit");

					if (PointingAt is Player)
					{
						description = i18n.Format("action_descyou", player.Character.GetNameOrTitle());
						options["look"] = i18n.Format("action_lookatyou");
						if (player.Character.GetStat(Stat.Stimulation) >= 30)
							options["fuck"] = i18n.Format("action_masturbate");
					}
					else if (PointingAt is BoardChar)
					{
						var boardChar = PointingAt as BoardChar;
						description = boardChar.Character.GetNameOrTitle(true);
						options["look"] = i18n.Format("action_lookathim", boardChar.Character.HimHerIt());

						if (canSee && distance <= 2 && !boardChar.Character.HasToken("beast") && !boardChar.Character.HasToken("sleeping"))
						{
							options["talk"] = i18n.Format("action_talktohim", boardChar.Character.HimHerIt());
							if (boardChar.Character.Path("role/vendor") != null)
								options["trade"] = i18n.Format("action_trade", boardChar.Character.HimHerIt());
						}

						if (canSee && player.Character.GetStat(Stat.Stimulation) >= 30 && distance <= 1)
						{
							if (!boardChar.Character.HasToken("beast"))
							{
								if ((boardChar.Character.HasToken("hostile") && boardChar.Character.HasToken("helpless")))
									options["rape"] = i18n.Format("action_rapehim", boardChar.Character.HimHerIt());
								else
									options["fuck"] = i18n.Format("action_fuckhim", boardChar.Character.HimHerIt());
							}
						}

						if (canSee && player.Character.CanShoot() != null && player.ParentBoard.HasToken("combat"))
						{
							options["shoot"] = i18n.Format("action_shoothim", boardChar.Character.HimHerIt());
						}
					}
					else if (PointingAt is DroppedItem)
					{
						var drop = PointingAt as DroppedItem;
						var item = drop.Item;
						var token = drop.Token;
						description = item.ToString(token);
						if (distance <= 1)
							options["take"] = i18n.GetString("action_pickup");
					}

					//MessageBox.List("This is " + description + ". What would you do?", options,
					ActionList.Show(description, PointingAt.XPosition, PointingAt.YPosition, options,
						() =>
						{
							Hide();
							if (ActionList.Answer is int && (int)ActionList.Answer == -1)
							{
								NoxicoGame.Messages.Add("\uE080[Aim message]");
								NoxicoGame.Mode = UserMode.Aiming;
								Point();
								return;
							}
							switch (ActionList.Answer as string)
							{
								case "look":
									if (PointingAt is DroppedItem)
									{
										var drop = PointingAt as DroppedItem;
										var item = drop.Item;
										var token = drop.Token;
										var text = (item.HasToken("description") && !token.HasToken("unidentified") ? item.GetToken("description").Text : i18n.Format("thisis_x", item.ToString(token))).Trim();
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
											MessageBox.Notice(i18n.GetString("talkingotyourself"), true);
										else
											MessageBox.Notice(i18n.GetString("talkingtoyourself_nutso"), true);
									}
									else if (PointingAt is BoardChar)
									{
										var boardChar = PointingAt as BoardChar;
										if (boardChar.Character.HasToken("hostile"))
											MessageBox.Notice(i18n.Format(boardChar.Character.IsProperNamed ? "nothingtosay_it" : "nothingtosay_he", boardChar.Character.GetNameOrTitle()), true);
										else
											SceneSystem.Engage(player.Character, boardChar.Character, true);
									}
									break;

								case "trade":
									ContainerMan.Setup(((BoardChar)PointingAt).Character);
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
										player.Energy -= 1000;
										NoxicoGame.AddMessage(i18n.Format("youpickup_x", item.ToString(token, true)), drop.ForegroundColor);
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
						MessageBox.ScriptPauseHandler = () =>
						{
							NoxicoGame.Mode = UserMode.Aiming;
							Point();
						};
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

		public void Hide()
		{
			NoxicoGame.Messages.RemoveAt(NoxicoGame.Messages.Count - 1);
			NoxicoGame.HostForm.Cursor = new Point(-1, -1);
			this.ParentBoard.DirtySpots.Add(new Location(XPosition, YPosition));
			this.ParentBoard.Draw(true);
		}
	}
}