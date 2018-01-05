using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if DEBUG
using Keys = System.Windows.Forms.Keys;
#endif

namespace Noxico
{

	public partial class BoardChar : Entity
	{
		private static int blinkRate = 1000;

		public string Sector { get; set; }
		public string Pairing { get; set; }

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
		private Neo.IronLua.LuaGlobal env;
		private Scheduler scheduler;
		public Dijkstra GuardMap { get; private set; }
		public int Eyes { get; private set; }
		public int SightRadius { get; private set; }
		public char GlowGlyph { get; private set; }

		public BoardChar()
		{
			this.Glyph = (char)255;
			this.ForegroundColor = Color.White;
			this.BackgroundColor = Color.Gray;
			this.Blocking = true;

			this.DijkstraMap = new Dijkstra();
			this.DijkstraMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
		}

		public BoardChar(Character character) : this()
		{
			ID = character.Name.ToID();
			Character = character;
			Character.BoardChar = this;
			this.Blocking = true;
			RestockVendor();
		}

		public override string ToString()
		{
			if (Character == null)
				return "[Characterless BoardChar]";
			return Character.Name.ToString(true);
		}

		public virtual void AdjustView()
		{
			var skinColor = Character.Path("skin/color").Text;
			ForegroundColor = Color.FromName(skinColor);
			BackgroundColor = Toolkit.Darken(ForegroundColor);
			if (skinColor.Equals("black", StringComparison.OrdinalIgnoreCase))
				ForegroundColor = Color.FromArgb(34, 34, 34);

			Token forcedGlyph = Character.Path("glyph") ?? (Character.HasToken("beast") ? Character.Path("bestiary") : null) ?? null;
			if (forcedGlyph != null)
			{
				if (forcedGlyph.HasToken("char"))
					Glyph = (int)forcedGlyph.GetToken("char").Value;
				if (forcedGlyph.HasToken("fore"))
					ForegroundColor = Color.FromName(forcedGlyph.GetToken("fore").Text);
				if (forcedGlyph.HasToken("back"))
					BackgroundColor = Color.FromName(forcedGlyph.GetToken("back").Text);
			}
			else
			{
				var judgment = '\x160';
				if (Character.HasToken("tallness") && Character.GetToken("tallness").Value < 140)
					judgment = '\x165';
				if (Character.HasToken("wings") && !Character.GetToken("wings").HasToken("small"))
					judgment = (judgment == '\x165') ? '\x16A' : '\x166';
				else if (Character.HasToken("tail"))
					judgment = (judgment == '\x166') ? '\x168' : '\x167';
				if (Character.HasToken("snaketail"))
					judgment = (judgment == '\x166') ? '\x169' : '\x161';
				else if (Character.HasToken("slimeblob"))
					judgment = '\x164';
				else if (Character.HasToken("quadruped"))
					judgment = '\x163';
				else if (Character.HasToken("taur"))
					judgment = '\x162';
				else if (Character.GetToken("legs").Text == "bear" && Character.GetToken("ears").Text == "bear")
					judgment = '\x171';
				Glyph = judgment;
			}

			if (Character.HasToken("copier") && Character.GetToken("copier").Value == 1 && Character.GetToken("copier").HasToken("full"))
			{
				Glyph = '@'; //not sure about this one, what if they copy someone who's not the player?
			}

			Eyes = 0;
			SightRadius = 1;
			GlowGlyph = ' ';
			if (Character.HasToken("eyes"))
			{
				Eyes = 2;
				SightRadius = 10;
				GlowGlyph = '\"';
				var eyeToken = Character.GetToken("eyes").GetToken("count");
				if (eyeToken != null)
					Eyes = (int)eyeToken.Value;
				if (Eyes == 1)
				{
					SightRadius = 4;
					GlowGlyph = '\'';
				}
				else if (Eyes > 2)
				{
					SightRadius = 4 + (int)(Math.Log(Eyes + 3) * 4);
					GlowGlyph = '\xF8';
				}
			}
		}

		public override object CanMove(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var canMove = base.CanMove(targetDirection, check);
			if (canMove != null && canMove is bool && !(bool)canMove)
				return canMove;
			if (!Character.HasToken("hostile") && !ScriptPathing && (Character.HasToken("sectorlock") || Character.HasToken("sectoravoid")))
			{
				if (!ParentBoard.Sectors.ContainsKey(Sector))
					return canMove;
				var sect = ParentBoard.Sectors[Sector];
				var newX = this.XPosition;
				var newY = this.YPosition;
				Toolkit.PredictLocation(newX, newY, targetDirection, ref newX, ref newY);
				var inRect = (newX >= sect.Left && newX <= sect.Right && newY >= sect.Top && newY <= sect.Bottom);
				if (Character.HasToken("sectorlock") && !inRect)
					return false;
				if (Character.HasToken("sectoravoid") && inRect)
					return false;
			}
			return canMove;
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			if (Character.HasToken("slimeblob"))
				ParentBoard.TrailSlime(YPosition, XPosition, ForegroundColor);
			if (ParentBoard.IsWater(YPosition, XPosition))
			{
				if (Character.HasToken("aquatic"))
				{
					Energy -= 1250;
					if (!Character.HasToken("swimming"))
						Character.AddToken("swimming", -1);
				}
				else
				{
					var swimming = Character.GetToken("swimming");
					if (swimming == null)
						swimming = Character.AddToken("swimming", 20);
					swimming.Value -= 1;
					if (swimming.Value == 0)
					{
						Hurt(9999, "death_drowned", null, false);
					}
					Energy -= 1750;
				}
			}
			else
			{
				Character.RemoveToken("swimming");
				Energy -= 1000;
			}
			base.Move(targetDirection, check);
		}

		public override void Draw()
		{
			var localX = this.XPosition - NoxicoGame.CameraX;
			var localY = this.YPosition - NoxicoGame.CameraY;
			if (localX >= 80 || localY >= 20 || localX < 0 || localY < 0)
				return;
			var b = ((MainForm)NoxicoGame.HostForm).IsMultiColor ? TileDefinition.Find(this.ParentBoard.Tilemap[this.XPosition, this.YPosition].Index, true).Background : this.BackgroundColor;			
			if (ParentBoard.IsLit(this.YPosition, this.XPosition))
			{
				base.Draw();
				if (Environment.TickCount % blinkRate * 2 < blinkRate)
				{
					if (Character.HasToken("sleeping"))
						NoxicoGame.HostForm.SetCell(localY, localX, 'Z', this.ForegroundColor, b);
					else if (Character.HasToken("flying"))
						NoxicoGame.HostForm.SetCell(localY, localX, '^', this.ForegroundColor, b);
					else if (Character.Path("role/vendor") != null)
						NoxicoGame.HostForm.SetCell(localY, localX, '$', this.ForegroundColor, b);
				}
			}
			else if (Eyes > 0 && Character.Path("eyes/glow") != null && !Character.HasToken("sleeping"))
				NoxicoGame.HostForm.SetCell(localY, localX, GlowGlyph, Color.FromName(Character.Path("eyes").Text), ParentBoard.Tilemap[XPosition, YPosition].Definition.Background.Night());
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
			var player = NoxicoGame.Me.Player;
			if (player.ParentBoard != this.ParentBoard)
				player = null;
			//var ogled = false;
			foreach (var other in ParentBoard.Entities.OfType<BoardChar>().Where(e => e != this && e.DistanceFrom(this) < 3))
			{
				if (other.Character.HasToken("beast"))
					continue;
				if (!CanSee(other))
					continue;
				if (!this.Character.Likes(other.Character))
					return;
				if (other.Character.GetStat(Stat.Charisma) >= 10)
				{
					var stim = this.Character.GetToken("stimulation");
					var otherChar = other.Character.GetStat(Stat.Charisma);
					var distance = other.DistanceFrom(this);
					var increase = (otherChar / 20) * (distance * 0.25);
					stim.Value += (float)increase;
					if (distance < 2)
						stim.Value += 1;
					if (stim.Value > 100)
						stim.Value = 100;
					/*
					if (!ogled && this != player)
					{
						var oldStim = this.Character.HasToken("oglestim") ? this.Character.GetToken("oglestim").Value : 0;
						if (stim.Value >= oldStim + 20 && player != null && this != player && player.DistanceFrom(this) < 4 && player.CanSee(this))
						{
							//TODO: i18n - needs reworking to translate better.
							NoxicoGame.AddMessage(string.Format("{0} to {1}: \"{2}\"", this.Character.Name, (other == player ? "you" : other.Character.Name.ToString()), Ogle(other.Character)).SmartQuote(this.Character.GetSpeechFilter()), GetEffectiveColor());
							if (!this.Character.HasToken("oglestim"))
								this.Character.AddToken("oglestim");
							this.Character.GetToken("oglestim").Value = stim.Value;
							ogled = true;
						}
					}
					*/
				}
			}
		}

		//TODO: rework ogling.
		/*
		public string Ogle(Character otherChar)
		{
			//TODO: i18n - all reactions should be in i18n.tml, and more should be added.
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
						return "Well hello, " + (otherChar.PercievedGender == Gender.Male ? "handsome." : "beautiful.");
					else if (cha < 60)
						return "Oh my.";
					else
						return "Woah.";
				}
			}
			return "There are no words.";
		}
		*/

		public void CheckForCriminalScum()
		{
			if (Character.HasToken("hostile") || Character.HasToken("sleeping"))
				return;
			var player = NoxicoGame.Me.Player;
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
						SceneSystem.Engage(player.Character, this.Character, "(criminalscum)");
					}
				}
			}
		}

		public void CheckForTimedItems()
		{
			foreach (var carriedItem in this.Character.GetToken("items").Tokens)
			{
				var timer = carriedItem.Path("timer");
				if (timer == null)
					continue;
				if (string.IsNullOrWhiteSpace(timer.Text))
					continue;
				var knownItem = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (knownItem == null)
					continue;
				if (knownItem.Path("timer/evenunequipped") == null && !carriedItem.HasToken("equipped"))
					continue;
				var time = new DateTime(long.Parse(timer.Text));
				if (NoxicoGame.InGameTime.Minute == time.Minute)
					continue;
				if (timer.Value > 0)
				{
					timer.Value--;
					timer.Text = NoxicoGame.InGameTime.ToBinary().ToString();
				}
				if (timer.Value <= 0)
				{
					timer.Value = (knownItem.GetToken("timer").Value == 0) ? 60 : knownItem.GetToken("timer").Value;
					if (string.IsNullOrWhiteSpace(knownItem.OnTimer))
					{
						Program.WriteLine("Warning: {0} has a timer, but no OnTimer script! Timer token removed.", carriedItem.Name);
						carriedItem.RemoveToken("timer");
						continue;
					}
					knownItem.RunScript(carriedItem, knownItem.OnTimer, this.Character, this, null);
				}
			}
		}

		public void CheckForCopiers()
		{
			if (Character.HasToken("copier"))
			{
				var copier = Character.GetToken("copier");
				var timeout = copier.GetToken("timeout");
				if (timeout != null && timeout.Value > 0)
				{
					if (!timeout.HasToken("minute"))
						timeout.AddToken("minute", NoxicoGame.InGameTime.Minute);
					if (timeout.GetToken("minute").Value == NoxicoGame.InGameTime.Minute)
						return;
					timeout.GetToken("minute").Value = NoxicoGame.InGameTime.Minute;
					timeout.Value--;
					if (timeout.Value == 0)
					{
						copier.RemoveToken(timeout);
						if (copier.HasToken("full") && copier.HasToken("backup"))
						{
							Character.Copy(null); //force revert
							AdjustView();
							NoxicoGame.AddMessage(i18n.GetString("x_reverts").Viewpoint(Character));
						}
					}
				}
			}
		}

		public override void Update()
		{
			if (Character.Health <= 0)
				return;

			var increase = 200 + (int)Character.GetStat(Stat.Speed);
			if (Character.HasToken("haste"))
				increase *= 2;
			else if (Character.HasToken("slow"))
				increase /= 2;
			Energy += increase;
			if (Energy < 5000)
				return;
			else
				Energy = 5000;

			if (Character.HasToken("helpless"))
			{
				if (Random.NextDouble() < 0.05)
				{
					Character.Health += 2;
					NoxicoGame.AddMessage(i18n.GetString("x_getsbackup").Viewpoint(Character));
					Character.RemoveToken("helpless");
					//TODO: Remove hostility? Replace with fear?
					//If the team system is used, perhaps switch to a Routed Hostile team.
				}
				else
					return;
			}
			if (Character.HasToken("waitforplayer") && !(this is Player))
			{
				if (!NoxicoGame.Me.Player.Character.HasToken("helpless"))
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
			
			CheckForTimedItems();
			CheckForCriminalScum();
			CheckForCopiers();
			if (Character.UpdateSex())
				return;

			base.Update();
			Excite();
			Character.UpdateOviposition();

			if (!Character.HasToken("fireproof") && ParentBoard.IsBurning(YPosition, XPosition))
				if (Hurt(10, "death_burned", null))
					return;

			//Pillowshout added this.
			if (!(this is Player) && !this.Character.HasToken("hostile") && this.ParentBoard.BoardType == BoardType.Town)
			{
				if (scheduler == null)
					scheduler = new Scheduler(this);

				scheduler.RunSchedule();
			}

			if (this.Character.HasToken("sleeping") || Character.HasToken("anchored"))
				return;

			ActuallyMove();
		}

		private void ActuallyMove()
		{
			var solidity = SolidityCheck.Walker;
			//if (Character.IsSlime)
				solidity = SolidityCheck.DryWalker;
			if (Character.HasToken("flying"))
				solidity = SolidityCheck.Flyer;

			if (ScriptPathing)
			{
				var dir = Direction.North;
				ScriptPathTarget.Ignore = DijkstraIgnore.Type;
				ScriptPathTarget.IgnoreType = typeof(BoardChar);
				if (ScriptPathTarget.RollDown(this.YPosition, this.XPosition, ref dir))
					Move(dir, solidity);
				if (this.XPosition == ScriptPathTargetX && this.YPosition == ScriptPathTargetY)
				{
					ScriptPathing = false;
					RunScript(OnPathFinish);
				}
				return;
			}

			var ally = Character.HasToken("ally");
			var hostile = ally ? Character.GetToken("ally") : Character.GetToken("hostile");
			var player = NoxicoGame.Me.Player;
			if (ParentBoard == player.ParentBoard && hostile != null)
			{
				var target = (BoardChar)player;
				if (ally)
					target = ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => !(x is Player) && x != this && x.Character.HasToken("hostile"));

				if (hostile.Value == 0) //Not actively hunting, but on the lookout.
				{
					if (target != null && DistanceFrom(target) <= SightRadius && CanSee(target))
					{
						NoxicoGame.Sound.PlaySound("set://Alert"); 
						hostile.Value = 1; //Switch to active hunting.
						Energy -= 500;

						if (!ally)
						{
							if (Character.HasToken("copier"))
							{
								var copier = Character.GetToken("copier");
								if (copier.Value == 0 && !copier.HasToken("timeout"))
								{
									Character.Copy(target.Character);
									AdjustView();
									NoxicoGame.AddMessage(i18n.Format(copier.HasToken("full") ? "x_becomes_y" : "x_imitates_y").Viewpoint(Character, target.Character));
									Energy -= 2000;
									return;
								}
							}

							//If we're gonna rape the target, we'd want them for ourself. Otherwise...
							if (Character.GetStat(Stat.Stimulation) < 30)
							{
								//...we call out to nearby hostiles
								var called = 0;
								foreach (var other in ParentBoard.Entities.OfType<BoardChar>().Where(x => !(x is Player) && x != this && DistanceFrom(x) < 10 && x.Character.HasToken("hostile")))
								{
									called++;
									other.CallTo(player);
								}
								if (called > 0)
								{
									if (!Character.HasToken("beast"))
										NoxicoGame.AddMessage(i18n.Format("call_out", Character.GetKnownName(true, true, true, true)).SmartQuote().Viewpoint(this.Character, target.Character), GetEffectiveColor());
									else
										NoxicoGame.AddMessage(i18n.Format("call_out_animal").Viewpoint(this.Character), GetEffectiveColor());
									Program.WriteLine("{0} called {1} others to player's location.", this.Character.Name, called);
									Energy -= 2000;
								}
							}
						}
						return;
					}
				}
				else if (hostile.Value == 1)
				{
					Hunt();
					return;
				}
			}

			if (Character.HasToken("guardspot"))
			{
				var guardX = this.XPosition;
				var guardY = this.YPosition;
				if (Character.GetToken("guardspot").Tokens.Count > 0)
				{
					if (this.GuardMap == null)
					{
						GuardMap = new Dijkstra(!Character.IsSlime, ParentBoard);
						GuardMap.Hotspots.Add(new Point(guardX, guardY));
						GuardMap.Update();
						GuardMap.Ignore = DijkstraIgnore.Type;
						GuardMap.IgnoreType = typeof(BoardChar);
					}
				}
				var dir = Direction.North;
				if (this.XPosition != guardX && this.YPosition != guardY)
					if (GuardMap.RollDown(this.YPosition, this.XPosition, ref dir))
						Move(dir, solidity);
				return;
			}

			if (Random.Flip())
				this.Move((Direction)Random.Next(4), solidity);
		}

		private void Hunt()
		{
			if (Character.HasToken("helpless"))
				return;

			if (Character.HasToken("beast"))
				Character.GetToken("stimulation").Value = 0;

			var ally = Character.HasToken("ally");
			var hostile = ally ? Character.GetToken("ally") : Character.GetToken("hostile");
			if (hostile == null)
				return;

			Entity target = null;
			//If no target is given, assume the player.
			if (Character.HasToken("huntingtarget"))
				target = ParentBoard.Entities.OfType<BoardChar>().First(x => x.ID == Character.GetToken("huntingtarget").Text);
			else if (!ally && NoxicoGame.Me.Player.ParentBoard == this.ParentBoard)
				target = NoxicoGame.Me.Player;

			if (Character.HasToken("stolenfrom"))
			{
				var newTarget = ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(x => x.Token.HasToken("owner") && x.Token.GetToken("owner").Text == Character.ID);
				if (newTarget != null)
					target = newTarget;
			}

			if (target == null)
			{
				//Intended target isn't on the board. Break off the hunt?
				hostile.Value = 0;
				return;
			}

			var distance = DistanceFrom(target);

			if (target is BoardChar)
			{
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
										Energy -= 1000;
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
										Energy -= 1000;
										return; //end turn
									}
								}
								catch (ItemException)
								{ }
							}
						}
					}
				}

				var bcTarget = target as BoardChar;
				if (distance <= range && CanSee(bcTarget))
				{
					//Within attacking range.
					if (IniFile.GetValue("misc", "allowrape", false) && distance == 1 && bcTarget.Character.HasToken("helpless") && Character.GetToken("stimulation").Value > 30 && Character.Likes(bcTarget.Character))
					{
						//WRONG KIND OF ATTACK! ABANDON SHIP!!
						Character.AddToken("waitforplayer");
						SexManager.Engage(this.Character, bcTarget.Character);
						return;
					}
					if (range == 1 && (target.XPosition == this.XPosition || target.YPosition == this.YPosition))
					{
						//Melee attacks can only be orthogonal.
						MeleeAttack(bcTarget);
#if MUTAMORPH
						if (Character.Path("prefixes/infectious") != null && Random.NextDouble() > 0.25)
							bcTarget.Character.Morph(Character.GetToken("infectswith").Text);
#endif
						return;
					}
					else if (weapon != null)
					{
						AimShot(target);
					}
				}
			}
			else if (target is DroppedItem)
			{
				var diTarget = target as DroppedItem;
				if (distance <= 1 && CanSee(diTarget))
				{
					diTarget.Take(this.Character, ParentBoard);
					this.Character.GetToken("stolenfrom").Name = "wasstolenfrom";
					this.Character.RemoveToken("hostile");
					this.Energy -= 1000;
					ParentBoard.Redraw();
					return;
				}
			}

			if (!CanSee(target) && Character.HasToken("targetlastpos"))
			{
				if (ScriptPathTarget == null)
				{
					var lastPos = Character.GetToken("targetlastpos");
					ScriptPathTarget = new Dijkstra(!Character.IsSlime, this.ParentBoard);
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
					hostile.Value = 0; //Switch off hunting mode
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
					ScriptPathTarget = new Dijkstra(!Character.IsSlime, this.ParentBoard);
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
			var hostile = Character.GetToken("hostile");
			if (hostile == null)
			{
				Program.WriteLine("{0} called to action, but is nonhostile.", this.Character.Name);
				return;
			}
			hostile.Value = 1; //engage hunt mode!
			Energy -= 800; //surprised, so not 500.
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
			Token carriedWeapon = null;
			foreach (var carriedItem in this.Character.GetToken("items").Tokens)
			{
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					continue;
				if (find.HasToken("equipable") && carriedItem.HasToken("equipped") && find.HasToken("weapon"))
				{
					weaponData = find.GetToken("weapon");
					carriedWeapon = carriedItem;
					break;
				}
			}

			Energy -= 500;

			var damage = 0.0f;
			var baseDamage = 0.0f;
			var dodged = false;
			var skill = "unarmed_combat";
			var verb = "strike{s}"; //TODO: i18n
			var cause = "death_struckdown";
			var attackerName = this.Character.GetKnownName(false, false, true);
			var attackerFullName = this.Character.GetKnownName(true, true, true);
			var targetName = target.Character.GetKnownName(false, false, true);
			var targetFullName = target.Character.GetKnownName(true, true, true);
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
				if (carriedWeapon.HasToken("bonus"))
					baseDamage = (float)Math.Ceiling(baseDamage * ((carriedWeapon.GetToken("bonus").Value + 1) * 0.75f));
				//TODO: if it's a crushing weapon, use strength stat.
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

			damage *= GetDefenseFactor(weaponData, target.Character);

			//Find the best overall "generic" armor defense and apply it.
			var overallArmor = 0f;
			foreach (var targetArmor in target.Character.GetToken("items").Tokens.Where(t => t.HasToken("equipped")))
			{
				var targetArmorItem = NoxicoGame.KnownItems.FirstOrDefault(i => i.Name == targetArmor.Name);
				if (targetArmorItem == null)
					continue;
				if (!targetArmorItem.HasToken("armor"))
					continue;
				if (targetArmorItem.GetToken("armor").Value > overallArmor)
					overallArmor = Math.Max(1.5f, targetArmorItem.GetToken("armor").Value);
			}
			if (overallArmor != 0)
				damage /= overallArmor;
			//Account for armor materials?

			//Add some randomization
			//Determine dodges

			if (target.Character.HasToken("helpless"))
			{
				damage = target.Character.Health + 1;
				dodged = false;
			}

			if (dodged)
			{
				NoxicoGame.AddMessage(i18n.Format("x_dodges_ys_attack").Viewpoint(this.Character, target.Character), target.GetEffectiveColor());
				return false;
			}

			if (damage > 0)
			{
				NoxicoGame.AddMessage(i18n.Format("x_verbs_y_for_z", verb, (int)damage).Viewpoint(this.Character, target.Character), target.GetEffectiveColor());
				Character.IncreaseSkill(skill);
			}
			if (target.Hurt(damage, cause, this, true)) //TODO: i18n - may need reworking
			{
				//Gain a bonus from killing the target?
				return true;
			}
			return false;
		}

		public float GetDefenseFactor(Token weaponToken, Noxico.Character target)
		{
			if (env == null)
				SetupLua();
			env.SetValue("weapon", weaponToken);
			env.SetValue("target", target);
			var r = Lua.RunFile("defense.lua", env);
			return (float)(r.ToDouble());
		}

		public virtual bool Hurt(float damage, string cause, object aggressor, bool finishable = false, bool leaveCorpse = true)
		{
			RunScript(OnHurt, "damage", damage);
			var health = Character.Health;
			if (health - damage <= 0)
			{
				if (finishable && !Character.HasToken("beast"))
				{
					if (!Character.HasToken("helpless"))
					{
						NoxicoGame.AddMessage(i18n.Format("x_is_helpless").Viewpoint(Character), this.GetEffectiveColor());
						Character.Tokens.Add(new Token() { Name = "helpless" } );
						return false;
					}
				}
				//Dead, but how?
				Character.Health = 0;
				if (aggressor != null && aggressor is BoardChar)
				{
					if (Character.HasToken("stolenfrom") && aggressor is Player)
					{
						((BoardChar)aggressor).Character.GiveRenegadePoints(10);
					}
				}
				if (leaveCorpse)
					LeaveCorpse(cause, aggressor);
				this.ParentBoard.CheckCombatFinish();
				return true;
			}
			Character.Health -= damage;
			return false;
		}

		private void LeaveCorpse(string cause, object aggressor)
		{
			if (Character.HasToken("copier") && Character.GetToken("copier").Value > 0 && Character.GetToken("copier").HasToken("full"))
			{
				//Revert changelings to their true form first.
				Character.Copy(null);
			}
			var name = i18n.Format("corpse_name", (Character.IsProperNamed ? Character.Name.ToString(true) : Character.GetKnownName(true, false, false, true)));
			var corpse = new Container(name, new List<Token>())
			{
				ParentBoard = ParentBoard,
				Glyph = Glyph,
				ForegroundColor = ForegroundColor.Darken(),
				BackgroundColor = BackgroundColor.Darken(),
				Blocking = false,
				XPosition = XPosition,
				YPosition = YPosition,
			};
			corpse.Token.AddToken("corpse");
			cause = i18n.GetString(cause);
			var obituary = cause;
			if (aggressor != null)
			{
				if (aggressor is BoardChar)
					obituary = i18n.Format("corpse_xed_by_y", cause, ((BoardChar)aggressor).Character.IsProperNamed ? ((BoardChar)aggressor).Character.Name.ToString(true) : ((BoardChar)aggressor).Character.GetKnownName(true, true, false));
				else
					obituary = i18n.Format("corpse_xed_by_y", cause, i18n.GetString((string)aggressor));
			}
			corpse.Token.AddToken("description", i18n.Format("corpse_description", Character.IsProperNamed ? Character.Name.ToString(true) : Character.GetKnownName(true, true, false), obituary));
			if (!(this is Player))
			{
				foreach (var item in Character.GetToken("items").Tokens)
					item.RemoveToken("owner");
				corpse.Token.GetToken("contents").AddSet(Character.GetToken("items").Tokens);
			}
			ParentBoard.EntitiesToRemove.Add(this);
			if (Character.HasToken("beast") && Character.HasToken("drops"))
			{
				foreach (var drop in Character.GetToken("drops").Tokens)
				{
					var droppedItem = new DroppedItem(drop.Name)
					{
						XPosition = XPosition,
						YPosition = YPosition,
						ParentBoard = ParentBoard,
					};
					droppedItem.AdjustView();
					droppedItem.ParentBoard.EntitiesToAdd.Add(droppedItem);
				}
			}
			else
				ParentBoard.EntitiesToAdd.Add(corpse);
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "BCHR");
			base.SaveToFile(stream);
			stream.Write(Sector ?? "<null>");
			stream.Write(Pairing ?? "<null>");
			Character.SaveToFile(stream);
		}

		public static new BoardChar LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "BCHR", "boardchar entity");
			var e = Entity.LoadFromFile(stream);
			var newChar = new BoardChar()
			{
				ID = e.ID, Glyph = e.Glyph, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Blocking = e.Blocking,
			};
			newChar.Sector = stream.ReadString();
			newChar.Pairing = stream.ReadString();
			newChar.Character = Character.LoadFromFile(stream);
			newChar.Character.BoardChar = newChar;
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

		private void SetupLua()
		{
			env = Lua.IronLua.CreateEnvironment();
			Lua.Ascertain(env);
			env.SetValue("this", this.Character);
			env.SetValue("thisEntity", this);
			env.SetValue("playerEntity", NoxicoGame.Me.Player);
			env.SetValue("target", ScriptPathID);
			env.RegisterPackage("Random", typeof(Random));
			env.RegisterPackage("BoardType", typeof(BoardType));
			env.RegisterPackage("Character", typeof(Character));
			env.RegisterPackage("BoardChar", typeof(BoardChar));
			env.RegisterPackage("InventoryItem", typeof(InventoryItem));
			env.RegisterPackage("Tile", typeof(Tile));
			env.RegisterPackage("Color", typeof(Color));
			env.SetValue("sound", new Action<string>(x => NoxicoGame.Sound.PlaySound(x)));
			env.SetValue("corner", new Action<string>(x => NoxicoGame.AddMessage(x)));
			env.SetValue("print", new Action<string>(x =>
			{
				var paused = true;
				MessageBox.ScriptPauseHandler = () =>
				{
					paused = false;
				};
				MessageBox.Notice(x, true, this.Character.Name.ToString(true));
				while (paused)
				{
					NoxicoGame.Me.Update();
					System.Windows.Forms.Application.DoEvents();
				}
			}));
			env.SetValue("FindTargetBoardByName", new Func<string, int>(x =>
			{
				if (!NoxicoGame.TravelTargets.ContainsValue(x))
					return -1;
				var i = NoxicoGame.TravelTargets.First(b => b.Value == x);
				return i.Key;
			}));

			var makeBoardTarget = new Action<Board>(board =>
			{
				if (string.IsNullOrWhiteSpace(board.Name))
					throw new Exception("Board must have a name before it can be added to the target list.");
				if (NoxicoGame.TravelTargets.ContainsKey(board.BoardNum))
					return; //throw new Exception("Board is already a travel target.");
				NoxicoGame.TravelTargets.Add(board.BoardNum, board.Name);
			});

			env.SetValue("MakeBoardTarget", makeBoardTarget);
			env.SetValue("GetBoard", new Func<int, Board>(x => NoxicoGame.Me.GetBoard(x)));
			env.SetValue("GetBiomeByName", new Func<string, int>(BiomeData.ByName));
			env.SetValue("scheduler", this.scheduler);
			env.RegisterPackage("Task", typeof(Task));
			env.RegisterPackage("TaskType", typeof(TaskType));
			env.RegisterPackage("Token", typeof(Token));
		}

		public bool RunScript(string script, string extraParm = "", float extraVal = 0)
		{
			if (string.IsNullOrWhiteSpace(script))
				return true;
			if (env == null)
				SetupLua();

			Board.DrawEnv = env;
			if (!string.IsNullOrEmpty(extraParm))
				env.SetValue(extraParm, extraVal);
			var r = Lua.Run(script, env);
			if (r.ToBoolean())
				return r.ToBoolean();
			return true;
		}

		public void MoveTo(int x, int y, string target)
		{
			ScriptPathTarget = new Dijkstra(!Character.IsSlime, this.ParentBoard);
			ScriptPathTarget.Hotspots.Add(new Point(x, y));
			ScriptPathTarget.Update();
			ScriptPathID = target;
			ScriptPathTargetX = x;
			ScriptPathTargetY = y;
			ScriptPathing = true;
		}

		public void AssignScripts(string id)
		{
			var uniques = Mix.GetTokenTree("uniques.tml", true);
			var planSource = uniques.FirstOrDefault(t => t.Name == "character" && (t.Text == id));
			var scripts = planSource.Tokens.Where(t => t.Name == "script");
			foreach (var script in scripts)
			{
				var target = script.Text;
				switch (target)
				{
					case "tick":
						OnTick = script.GetToken("#text").Text;
						break;
					case "load":
						OnLoad = script.GetToken("#text").Text;
						break;
					case "bump":
					case "playerbump":
						OnPlayerBump = script.GetToken("#text").Text;
						break;
					case "hurt":
						OnHurt = script.GetToken("#text").Text;
						break;
					case "path":
					case "pathfinish":
						OnPathFinish = script.GetToken("#text").Text;
						break;
				}
			}
			this.Character.RemoveAll("script");
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
				if (weap.HasToken("splash"))
				{
					var splashRadius = (int)weap.GetToken("splash").Value;
					//TODO: apply splash damage to like half the points around the target or sumth
				}
			}
			else
			{
				Program.WriteLine("{0} tried to throw a weapon.", this.Character.Name);
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
					NoxicoGame.AddMessage(i18n.Format("x_verbs_y_for_z", "hit", damage).Viewpoint(this.Character, hit.Character), this.GetEffectiveColor());
					hit.Hurt(damage, "death_shot", this, false);
					Energy -= 500; //fixed: succesful shots didn't take time
					return;
				}
			}
			Energy -= 500;
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
					Glyph = (char)effect.GetToken("char").Value,
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
			if (vendor == null || vendor.GetToken("class").Text == "carpenter")
				return;
			if (!vendor.HasToken("lastrestockday"))
				vendor.AddToken("lastrestockday", NoxicoGame.InGameTime.DayOfYear - 1);
			var lastRestockDay = vendor.GetToken("lastrestockday").Value;
			var today = NoxicoGame.InGameTime.DayOfYear;
			if (lastRestockDay >= today)
				return;
			vendor.GetToken("lastrestockday").Value = today;
			var items = Character.Path("items");
			var diff = 20 - items.Tokens.Count;
			if (diff > 0)
				Character.GetToken("money").Value += diff * 50; //TODO: explain this
			var filters = new Dictionary<string, string>();
			filters["vendorclass"] = vendor.GetToken("class").Text;
			while (items.Tokens.Count < 20)
			{
				var newstock = DungeonGenerator.GetRandomLoot("vendor", "stock", filters);
				if (newstock.Count == 0)
					break;
				foreach (var item in newstock)
					item.AddToken("for_sale");
				items.AddSet(newstock);
			}
		}

		public Color GetEffectiveColor()
		{
			return Color.FromName(Character.Path("skin/color"));
		}
	}
}