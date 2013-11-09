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
			this.AsciiChar = '\x7F';
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
			if (newX < 0 || newY < 0 || newX > 79 || newY > 49)
				return false;
			return null;
		}

		public void Point()
		{
			PointingAt = null;
			if (NoxicoGame.Messages.Count == 0) //fixes range error found while explaining controls
				NoxicoGame.Messages.Add(string.Empty);
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
						NoxicoGame.Messages[NoxicoGame.Messages.Count - 1] = "<c" + ((BoardChar)entity).Character.Path("skin/color").Text + ">" + ((BoardChar)PointingAt).Character.GetKnownName(true, true); 
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
						description = i18n.Format("action_descyou", player.Character.Name);
						options["look"] = i18n.GetString("action_lookatyou");
						if (player.Character.GetStat(Stat.Stimulation) >= 30)
							options["fuck"] = i18n.GetString("action_masturbate");

						if (player.Character.HasToken("copier") && player.Character.GetToken("copier").Value == 1)
						{
							if (player.Character.Path("copier/backup") != null || player.Character.Path("copier/full") == null)
								options["revert"] = i18n.GetString("action_revert");
						}
					}
					else if (PointingAt is BoardChar)
					{
						var boardChar = PointingAt as BoardChar;
						description = boardChar.Character.GetKnownName(true);
						options["look"] = i18n.Format("action_lookathim", boardChar.Character.HimHerIt(true));

						if (canSee && distance <= 2 && !boardChar.Character.HasToken("beast") && !boardChar.Character.HasToken("sleeping"))
						{
							options["talk"] = i18n.Format("action_talktohim", boardChar.Character.HimHerIt(true));
							if (boardChar.Character.Path("role/vendor") != null)
								options["trade"] = i18n.Format("action_trade", boardChar.Character.HimHerIt(true));
						}

						if (canSee && player.Character.GetStat(Stat.Stimulation) >= 30 && distance <= 1)
						{
							if (!boardChar.Character.HasToken("beast"))
							{
								if ((boardChar.Character.HasToken("hostile") && boardChar.Character.HasToken("helpless")))
									options["fuck"] = i18n.Format("action_rapehim", boardChar.Character.HimHerIt(true));
								else
									options["fuck"] = i18n.Format("action_fuckhim", boardChar.Character.HimHerIt(true));
							}
						}

						if (canSee && !boardChar.Character.HasToken("beast") && player.Character.HasToken("copier") && player.Character.Path("copier/timeout") == null)
						{
							if (player.Character.UpdateCopier())
								options["copy"] = i18n.Format("action_copyhim", boardChar.Character.HimHerIt(true));
						}

						if (canSee && player.Character.CanShoot() != null && player.ParentBoard.HasToken("combat"))
						{
							options["shoot"] = i18n.Format("action_shoothim", boardChar.Character.HimHerIt(true));
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

#if DEBUG
					if (PointingAt is DroppedItem || PointingAt is BoardChar)
						options["edit"] = "Edit tokens";
                    if (PointingAt is BoardChar)
                        options["mutate"] = "Random mutate";
#endif

					//MessageBox.List("This is " + description + ". What would you do?", options,
					ActionList.Show(description, PointingAt.XPosition, PointingAt.YPosition, options,
						() =>
						{
							Hide();
							if (ActionList.Answer is int && (int)ActionList.Answer == -1)
							{
								NoxicoGame.Messages.Add("[Aim message]");
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
											MessageBox.Notice(i18n.Format("nothingtosay", boardChar.Character.GetKnownName(false, false, true, true)), true);
										else
											SceneSystem.Engage(player.Character, boardChar.Character);
									}
									break;

								case "trade":
									ContainerMan.Setup(((BoardChar)PointingAt).Character);
									break;

								case "fuck":
									if (PointingAt is BoardChar)
										SexManager.Engage(player, (BoardChar)PointingAt);
									break;

								case "shoot":
									player.AimShot(PointingAt);
									break;

								case "copy":
									player.Character.Copy(((BoardChar)PointingAt).Character);
									player.AdjustView();
									NoxicoGame.AddMessage(i18n.Format((player.Character.Path("copier/full") == null) ? "youimitate_x" : "youbecome_x", ((BoardChar)PointingAt).Character.GetKnownName(false, false, true)));
									player.Energy -= 2000;
									break;

								case "revert":
									player.Character.Copy(null);
									player.AdjustView();
									NoxicoGame.AddMessage(i18n.GetString((player.Character.Path("copier/full") == null) ? "youmelt" : "yourevert"));
									player.Energy -= 1000;
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
										ParentBoard.Redraw();
									}
									break;

#if DEBUG
								case "edit":
									TokenCarrier tc = null;
									if (PointingAt is DroppedItem)
										tc = ((DroppedItem)PointingAt).Token;
									else if (PointingAt is BoardChar)
										tc = ((BoardChar)PointingAt).Character;

									NoxicoGame.HostForm.Write("TOKEN EDIT ENGAGED. Waiting for editor process to exit.", Color.Black, Color.White, 0, 0);
									NoxicoGame.HostForm.Draw();
									((MainForm)NoxicoGame.HostForm).timer.Enabled = false;
									var dump = "-- WARNING! Many things may cause strange behavior or crashes. WATCH YOUR FUCKING STEP.\r\n" + tc.DumpTokens(tc.Tokens, 0);
									var temp = Path.Combine(Path.GetTempPath(), DateTime.Now.Ticks.ToString() + ".txt");
									File.WriteAllText(temp, dump);
									var process = System.Diagnostics.Process.Start(temp);
									process.WaitForExit();
									var newDump = File.ReadAllText(temp);
									File.Delete(temp);
									((MainForm)NoxicoGame.HostForm).timer.Enabled = true;
									ParentBoard.Redraw();
									if (newDump == dump)
										break;
									tc.Tokenize(newDump);
									break;
                                
                                case "mutate":
                                    ((BoardChar)PointingAt).Character.Mutate(1, 30);
                                    break;
#endif

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
			if (NoxicoGame.KeyMap[Keys.D])
			{
				NoxicoGame.ClearKeys();
				if (PointingAt != null && PointingAt is BoardChar)
				{
					((BoardChar)PointingAt).Character.CreateInfoDump();
					NoxicoGame.AddMessage("Info for " + ((BoardChar)PointingAt).Character.GetKnownName(true, true, true) + " dumped.", Color.Red);
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