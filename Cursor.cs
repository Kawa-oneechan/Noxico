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
			this.Glyph = '\x7F';
			this.BackgroundColor = Color.Black;
			this.ForegroundColor = Color.White;
			this.Tabstops = new List<Point>();
		}

		public override void Draw()
		{
			if (NoxicoGame.Mode != UserMode.Aiming)
				return;
			NoxicoGame.HostForm.Cursor = new Point(XPosition - NoxicoGame.CameraX, YPosition - NoxicoGame.CameraY);
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			//this.ParentBoard.DirtySpots.Add(new Point(XPosition, YPosition));
			//this.ParentBoard.Draw(true);
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
			if (newX < 0 || newY < 0 || newX >= this.ParentBoard.Width || newY >= this.ParentBoard.Height)
				return false;
			return null;
		}

		public void Point()
		{
			this.ParentBoard.AimCamera(this.XPosition, this.YPosition);
			PointingAt = null;
			NoxicoGame.LookAt = i18n.GetString("pointatsomething");
			if (!this.ParentBoard.IsSeen(YPosition, XPosition))
			{
				NoxicoGame.LookAt = i18n.GetString("unexplored");
				return;
			}

			var tSD = this.ParentBoard.GetDescription(YPosition, XPosition);
			if (!tSD.IsBlank())
			{
				PointingAt = null;
				NoxicoGame.LookAt = tSD;
			}


			foreach (var entity in this.ParentBoard.Entities)
			{
				if (entity.XPosition == XPosition && entity.YPosition == YPosition)
				{
					if (!this.ParentBoard.IsLit(YPosition, XPosition) && NoxicoGame.Me.Player.CanSee(entity))
					{
						if (NoxicoGame.Me.Player.Character.Path("eyes/glow") == null)
						{
							//No darkvision
							if (entity is BoardChar && ((BoardChar)entity).Character.Path("eyes/glow") != null)
							{
								//Entity has glowing eyes, but we don't let the player actually interact with them.
								NoxicoGame.LookAt = "<c" + ((BoardChar)entity).Character.Path("eyes").Text + ">" + i18n.GetString("eyesinthedark");
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
						var color = ((BoardChar)entity).Character.Path("skin/color").Text;
						if (color.Equals("black", StringComparison.InvariantCultureIgnoreCase))
							color = "gray";
						NoxicoGame.LookAt = string.Format("<c{0}>{1}<c> ({2}/{3})", color,
							((BoardChar)PointingAt).Character.GetKnownName(true, true),
							((BoardChar)PointingAt).Character.Health, ((BoardChar)PointingAt).Character.MaximumHealth);
						//return;
					}
					else if (entity is DroppedItem)
					{
						NoxicoGame.LookAt = ((DroppedItem)PointingAt).Name;
						//return;
					}
					else if (entity is Clutter || entity is Container)
					{
						var desc = (entity is Container ? ((Container)PointingAt).Description : ((Clutter)PointingAt).Description);
						if (desc.Length() > 70 && desc.Contains('.'))
							desc = desc.Remove(desc.IndexOf('.')) + '.';
						if (desc.Length() > 70 && desc.Contains(','))
							desc = desc.Remove(desc.IndexOf(',')) + '.';
						NoxicoGame.LookAt = desc;
						//return;
					}
					else if (entity is Door)
					{
						NoxicoGame.LookAt = i18n.GetString("pointingatdoor");
						//return;
					}
				}
			}
		}

		public override void Update()
		{
			base.Update();
			NoxicoGame.ContextMessage = i18n.GetString("context_interactmode");
			if (this.ParentBoard == null)
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				return;
			}
			//this.ParentBoard.Draw(true);

			if (NoxicoGame.IsKeyDown(KeyBinding.Interact) || NoxicoGame.IsKeyDown(KeyBinding.Back) || Vista.Triggers == XInputButtons.B)
			{
				NoxicoGame.Mode = UserMode.Walkabout;
				NoxicoGame.ClearKeys();
				NoxicoGame.ContextMessage = string.Empty;
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
				var player = NoxicoGame.Me.Player;
#if DEBUG
				if (PointingAt == null)
				{
					ActionList.Show("Debug?", this.XPosition - NoxicoGame.CameraX, this.YPosition - NoxicoGame.CameraY,
						new Dictionary<object, string>()
						{
							{ "teleport", "Teleport" },
							{ "spawn", "Spawn character" },
						},
						() =>
						{
							Hide();
							if (ActionList.Answer is int && (int)ActionList.Answer == -1)
							{
								NoxicoGame.Mode = UserMode.Walkabout;
								Hide();
								return;
							}
							switch (ActionList.Answer as string)
							{
								case "teleport":
									player.XPosition = this.XPosition;
									player.YPosition = this.YPosition;
									ParentBoard.AimCamera();
									ParentBoard.Redraw();
									NoxicoGame.Mode = UserMode.Aiming;
									Point();
									return;
								case "spawn":
									var spawnOptions = new Dictionary<object, string>();
									foreach (var bp in Character.Bodyplans)
										spawnOptions[bp.Text] = bp.Text;
									var uniques = Mix.GetTokenTree("uniques.tml", true);
									foreach (var bp in uniques)
										spawnOptions['!' + bp.Text] = bp.Text;
									ActionList.Show("Debug?", this.XPosition - NoxicoGame.CameraX, this.YPosition - NoxicoGame.CameraY, spawnOptions,
										() =>
										{
											if (ActionList.Answer is int && (int)ActionList.Answer == -1)
											{
												NoxicoGame.Mode = UserMode.Aiming;
												Point();
												return;
											}
											var spawnId = (string)ActionList.Answer;
											var isUnique = spawnId.StartsWith('!');
											if (isUnique)
												spawnId = spawnId.Substring(1);
											Character newChar = null;
											if (isUnique)
												newChar = Character.GetUnique(spawnId);
											else
												newChar = Character.Generate(spawnId, Gender.RollDice);
											var newBoardChar = new BoardChar(newChar)
											{
												XPosition = this.XPosition,
												YPosition = this.YPosition,
												ParentBoard = this.ParentBoard
											};
											newBoardChar.AdjustView();
											newBoardChar.AssignScripts(spawnId);
											ParentBoard.EntitiesToAdd.Add(newBoardChar);
											//ParentBoard.Redraw();
											NoxicoGame.Mode = UserMode.Walkabout;
											Hide();
											return;
										}
									);
									break;
							}
						}
					);
				}
#endif
				if (PointingAt != null)
				{
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
						options["talk"] = i18n.GetString("action_talktoyou");
						if (player.Character.GetStat("excitement") >= 30)
							options["fuck"] = i18n.GetString("action_masturbate");

						if (player.Character.HasToken("copier") && player.Character.GetToken("copier").IntValue == 1)
						{
							if (player.Character.Path("copier/backup") != null || player.Character.Path("copier/full") == null)
								options["revert"] = i18n.GetString("action_revert");
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
					else if (PointingAt is Container)
					{
						var container = PointingAt as Container;
						description = container.Name ?? "container";
					}
					else if (PointingAt is Clutter)
					{
						var clutter = PointingAt as Clutter;
						description = clutter.Name ?? "something";
						if (clutter.ID == "craftstation")
							options["craft"] = i18n.GetString("action_craft");
					}
					else if (PointingAt is BoardChar)
					{
						var boardChar = PointingAt as BoardChar;
						description = boardChar.Character.GetKnownName(true);
						options["look"] = i18n.Format("action_lookathim", boardChar.Character.HimHerIt(true));

						if (canSee && distance <= 2 && !boardChar.Character.HasToken("beast") && !boardChar.Character.HasToken("sleeping"))
						{
							options["talk"] = i18n.Format("action_talktohim", boardChar.Character.HimHerIt(true));
							if (boardChar.Character.Path("role/vendor") != null && boardChar.Character.Path("role/vendor/class").Text != "carpenter")
								options["trade"] = i18n.Format("action_trade", boardChar.Character.HimHerIt(true));
						}

						if (canSee && player.Character.GetStat("excitement") >= 30 && distance <= 1)
						{
							var mayFuck = boardChar.Character.HasToken("willing");
							var willRape = boardChar.Character.HasToken("helpless");

							if (!IniFile.GetValue("misc", "allowrape", false) && willRape)
								mayFuck = false;
							//but DO allow it if they're helpless but willing
							if (boardChar.Character.HasToken("willing") && willRape)
							{
								mayFuck = true;
								willRape = false;
							}
							if (boardChar.Character.HasToken("beast"))
								mayFuck = false;

							if (mayFuck)
								options["fuck"] = i18n.Format(willRape ? "action_rapehim" : "action_fuckhim", boardChar.Character.HimHerIt(true));
						}

						//TODO: This needs to be updated to work with Powers.
						if (canSee && !boardChar.Character.HasToken("beast") && player.Character.HasToken("copier") && player.Character.Path("copier/timeout") == null)
						{
							//if (player.Character.UpdateCopier())
							if (player.Character.HasToken("fullCopy") || player.Character.HasToken("sexCopy"))
								options["copy"] = i18n.Format("action_copyhim", boardChar.Character.HimHerIt(true));
						}

						if (canSee && player.Character.CanShoot() != null && player.ParentBoard.HasToken("combat"))
						{
							options["shoot"] = i18n.Format("action_shoothim", boardChar.Character.HimHerIt(true));
						}
					}

#if DEBUG
					if (PointingAt is BoardChar)
					{
						options["mutate"] = "(debug) Random mutate";
						options["turbomutate"] = "(debug) Apply LOTS of mutations!";
					}
#endif

					ActionList.Show(description, PointingAt.XPosition - NoxicoGame.CameraX, PointingAt.YPosition - NoxicoGame.CameraY, options,
						() =>
						{
							Hide();
							if (ActionList.Answer is int && (int)ActionList.Answer == -1)
							{
								//NoxicoGame.Messages.Add("[Aim message]");
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
									else if (PointingAt is Clutter && !((Clutter)PointingAt).Description.IsBlank())
									{
										MessageBox.Notice(((Clutter)PointingAt).Description.Trim(), true, ((Clutter)PointingAt).Name ?? "something");
									}
									else if (PointingAt is Container)
									{
										MessageBox.Notice(((Container)PointingAt).Description.Trim(), true, ((Container)PointingAt).Name ?? "container");
									}
									else if (PointingAt is BoardChar)
									{
										if (((BoardChar)PointingAt).Character.HasToken("beast"))
											MessageBox.Notice(((BoardChar)PointingAt).Character.LookAt(PointingAt), true, ((BoardChar)PointingAt).Character.GetKnownName(true));
										else
											TextScroller.LookAt((BoardChar)PointingAt);
									}
									break;

								case "talk":
									if (PointingAt is Player)
									{
										Lua.Environment.TalkToSelf(((Player)PointingAt).Character);
									}
									else if (PointingAt is BoardChar)
									{
										var boardChar = PointingAt as BoardChar;
										if (boardChar.Character.HasToken("hostile") && !boardChar.Character.HasToken("helpless"))
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
										SexManager.Engage(player.Character, ((BoardChar)PointingAt).Character);
									break;

								case "shoot":
									player.AimShot(PointingAt);
									break;

								case "copy":
									player.Character.Copy(((BoardChar)PointingAt).Character);
									player.AdjustView();
									//NoxicoGame.AddMessage(i18n.Format((player.Character.Path("copier/full") == null) ? "youimitate_x" : "become_x", ((BoardChar)PointingAt).Character.GetKnownName(false, false, true)));
									NoxicoGame.AddMessage(i18n.Format(player.Character.HasToken("fullCopy") ? "x_becomes_y" : "x_imitates_y").Viewpoint(player.Character, ((BoardChar)PointingAt).Character));
									player.Energy -= 2000;
									break;

								case "revert":
									player.Character.Copy(null);
									player.AdjustView();
									NoxicoGame.AddMessage(i18n.GetString((player.Character.HasToken("fullCopy")) ? "youmelt" : "yourevert"));
									player.Energy -= 1000;
									break;

								case "take":
									if (PointingAt is DroppedItem)
									{
										var drop = PointingAt as DroppedItem;
										var item = drop.Item;
										var token = drop.Token;
										drop.Take(player.Character, ParentBoard);
										player.Energy -= 1000;
										NoxicoGame.AddMessage(i18n.Format("youpickup_x", item.ToString(token, true)), drop.ForegroundColor);
										NoxicoGame.Sound.PlaySound("set://GetItem");
										//ParentBoard.Redraw();
									}
									break;

								case "craft":
									Crafting.Open(player.Character);
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
									((BoardChar)PointingAt).AdjustView();
									((BoardChar)PointingAt).Character.RecalculateStatBonuses();
									((BoardChar)PointingAt).Character.CheckHasteSlow();
									break;

								case "mutate":
									var result = ((BoardChar)PointingAt).Character.Mutate(1, 30);
									NoxicoGame.AddMessage(result);
									break;

								case "turbomutate":
									result = ((BoardChar)PointingAt).Character.Mutate(2500, 30);
									NoxicoGame.AddMessage(result);
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
					/*
					var tSD = this.ParentBoard.GetDescription(YPosition, XPosition);
					if (!string.IsNullOrWhiteSpace(tSD))
					{
						PointingAt = null;
						MessageBox.ScriptPauseHandler = () =>
						{
							NoxicoGame.Mode = UserMode.Aiming;
							Point();
						};
						MessageBox.Notice(tSD, true);
						return;
					}
					*/
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
			var player = NoxicoGame.Me.Player;
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
			//if (NoxicoGame.Messages.Count > 1)
			//	NoxicoGame.Messages.RemoveAt(NoxicoGame.Messages.Count - 1);
			NoxicoGame.LookAt = null;
			NoxicoGame.HostForm.Cursor = new Point(-1, -1);
			this.ParentBoard.AimCamera();
			this.ParentBoard.DirtySpots.Add(new Point(XPosition, YPosition));
			//this.ParentBoard.Draw(true);
		}
	}
}
