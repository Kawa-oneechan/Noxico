using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if DEBUG
using Keys = System.Windows.Forms.Keys;
#endif

namespace Noxico
{

	public class BoardChar : Entity
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
		private Jint.JintEngine js;
		private Scheduler scheduler;
		public Dijkstra GuardMap { get; private set; }

		public BoardChar()
		{
			this.AsciiChar = (char)255;
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
			this.Blocking = true;
			RestockVendor();
		}

		public virtual void AdjustView()
		{
			var skinColor = Character.Path("skin/color").Text;
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

			if (Character.HasToken("copier") && Character.GetToken("copier").Value == 1 && Character.GetToken("copier").HasToken("full"))
			{
				AsciiChar = '@';
			}
		}

		public override object CanMove(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var canMove = base.CanMove(targetDirection, check);
			if (canMove != null && canMove is bool && !(bool)canMove)
				return canMove;
			if (!ScriptPathing && (Character.HasToken("sectorlock") || Character.HasToken("sectoravoid")))
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
			check = SolidityCheck.Walker;
			if (Character.HasToken("flying"))
				check = SolidityCheck.Flyer;
			Energy -= 1000;
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
				NoxicoGame.HostForm.SetCell(this.YPosition, this.XPosition, '\"', Color.FromName(Character.Path("eyes").Text), ParentBoard.Tilemap[XPosition, YPosition].Background.Night());
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
				if (!CanSee(other))
					continue;
				if (!this.Character.Likes(other.Character))
					return;
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
							NoxicoGame.AddMessage(string.Format("{0} to {1}: \"{2}\"", this.Character.Name, (other == player ? "you" : other.Character.Name.ToString()), Ogle(other.Character)).SmartQuote(this.Character.GetSpeechFilter()), GetEffectiveColor());
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
						return "Well hello, " + (otherChar.Gender == Gender.Male ? "handsome." : "beautiful.");
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
				var time = new NoxicanDate(long.Parse(timer.Text));
				if (NoxicoGame.InGameTime.Minute <= time.Minute)
					continue;
				timer.Value--;
				if (timer.Value > 0)
				{
					timer.Text = NoxicoGame.InGameTime.ToBinary().ToString();
					continue;
				}
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

		public void CheckForCopiers()
		{
			if (Character.HasToken("copier"))
			{
				var copier = Character.GetToken("copier");
				var timeout = copier.GetToken("timeout");
				if (timeout != null && timeout.Value > 0)
				{
					if (int.Parse(timeout.Text) == NoxicoGame.InGameTime.Minute)
						return;
					timeout.Text = NoxicoGame.InGameTime.Minute.ToString();
					timeout.Value--;
					if (timeout.Value == 0)
					{
						copier.RemoveToken(timeout);
						if (copier.HasToken("full") && copier.HasToken("backup"))
						{
							Character.Copy(null); //force revert
							AdjustView();
							if (this is Player)
								NoxicoGame.AddMessage(i18n.GetString("yourevert"));
							else
								NoxicoGame.AddMessage(i18n.Format("x_reverts", Character.Name, Character.HisHerIts(true)));
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
					NoxicoGame.AddMessage((this is Player ? "You get" : Character.GetKnownName(false, false, true, true) + " gets") + " back up.");
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
			
			CheckForTimedItems();
			CheckForCriminalScum();
			CheckForCopiers();

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

			ActuallyMove();
		}

		private void ActuallyMove()
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

			var ally = Character.HasToken("ally");
			var hostile = ally ? Character.GetToken("ally") : Character.GetToken("hostile");
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (ParentBoard == player.ParentBoard && hostile != null)
			{
				var target = (BoardChar)player;
				if (ally)
					target = ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => !(x is Player) && x != this && x.Character.HasToken("hostile"));

				if (hostile.Value == 0) //Not actively hunting, but on the lookout.
				{
					if (target != null && DistanceFrom(target) < 10 && CanSee(target))
					{
						NoxicoGame.Sound.PlaySound("Alert"); //Test things with an MGS Alert -- would normally be done in Noxicobotic, I guess...
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
									if (target is Player)
										NoxicoGame.AddMessage(i18n.Format(copier.HasToken("full") ? "x_becomesyou" : "x_imitatesyou", Character.GetKnownName(false, false, true, true)));
									else
										NoxicoGame.AddMessage(i18n.Format(copier.HasToken("full") ? "x_becomes_y" : "x_imitates_y", Character.GetKnownName(false, false, true, true), target.Character.GetKnownName(false, false, true)));
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
										NoxicoGame.AddMessage((Character.GetKnownName(false, false, true, true) + ", " + Character.Title + ": \"There " + player.Character.HeSheIt(true) + " is!\"").SmartQuote(this.Character.GetSpeechFilter()), GetEffectiveColor());
									else
										NoxicoGame.AddMessage("The " + Character.Title + " vocalizes an alert!", GetEffectiveColor());
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
						GuardMap = new Dijkstra(ParentBoard);
						GuardMap.Hotspots.Add(new Point(guardX, guardY));
						GuardMap.Update();
						GuardMap.Ignore = DijkstraIgnore.Type;
						GuardMap.IgnoreType = typeof(BoardChar);
					}
				}
				var dir = Direction.North;
				if (this.XPosition != guardX && this.YPosition != guardY)
					if (GuardMap.RollDown(this.YPosition, this.XPosition, ref dir))
						Move(dir);
				return;
			}

			if (Random.Flip())
				this.Move((Direction)Random.Next(4));
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

			BoardChar target = null;
			//If no target is given, assume the player.
			if (Character.HasToken("huntingtarget"))
				target = ParentBoard.Entities.OfType<BoardChar>().First(x => x.ID == Character.GetToken("huntingtarget").Text);
			else if (!ally && NoxicoGame.HostForm.Noxico.Player.ParentBoard == this.ParentBoard)
				target = NoxicoGame.HostForm.Noxico.Player;

			if (target == null)
			{
				//Intended target isn't on the board. Break off the hunt?
				hostile.Value = 0;
				return;
			}

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
					ScriptPathTarget = new Dijkstra(this.ParentBoard);
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
					ScriptPathTarget = new Dijkstra(this.ParentBoard);
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

			Energy -= 500;

			var damage = 0.0f;
			var baseDamage = 0.0f;
			var dodged = false;
			var skill = "unarmed_combat";
			var verb = "strikes";
			var obituary = "died from being struck down";
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

			//Account for armor and such

			damage *= GetDefenseFactor(weaponData, target.Character);

			//Add some randomization
			//Determine dodges

			if (target.Character.HasToken("helpless"))
			{
				damage = target.Character.Health + 1;
				dodged = false;
			}

			if (dodged)
			{
				NoxicoGame.AddMessage((target is Player ? targetName.InitialCase() : "You") + " dodge " + (target is Player ? attackerName + "'s" : "your") + " attack.", target.GetEffectiveColor());
				return false;
			}

			if (damage > 0)
			{
				NoxicoGame.AddMessage((target is Player ? attackerName.InitialCase() : "You") + ' ' + verb + ' ' + (target is Player ? "you" : targetName) + " for " + damage + " point" + (damage > 1 ? "s" : "") + ".", target.GetEffectiveColor());
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
			var health = Character.Health;
			if (health - damage <= 0)
			{
				if (finishable && !Character.HasToken("beast"))
				{
					if (!Character.HasToken("helpless"))
					{
						NoxicoGame.AddMessage((this is Player ? "You are" : Character.GetKnownName(false, false, true, true) + " is") + " helpless!", Color.FromName(this.Character.Path("skin/color")));
						Character.Tokens.Add(new Token() { Name = "helpless" } );
						return false;
					}
				}
				//Dead, but how?
				Character.Health = 0;
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
			Character.Health -= damage;
			return false;
		}

		private void LeaveCorpse(string obituary)
		{
			if (Character.HasToken("copier") && Character.GetToken("copier").Value > 0 && Character.GetToken("copier").HasToken("full"))
			{
				//Revert changelings to their true form first.
				Character.Copy(null);
			}
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
				corpse.Name = Character.GetKnownName(true, false, false, true) + "'s remains";
				corpse.Description = "These are the remains of " + Character.GetKnownName(true, true, false) + ", who " + obituary + ".";
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
				ID = e.ID, AsciiChar = e.AsciiChar, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Flow = e.Flow, Blocking = e.Blocking,
			};
			newChar.Sector = stream.ReadString();
			newChar.Pairing = stream.ReadString();
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

			ScriptPathTarget = new Dijkstra(this.ParentBoard);
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
			var xml = Mix.GetXmlDocument("uniques.xml");
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
					NoxicoGame.AddMessage(string.Format("{0} hit you for {1} point{2}.", this.Character.GetKnownName(false, false, true, true), damage, damage > 1 ? "s" : ""));
					hit.Hurt(damage, "being shot down by " + this.Character.GetKnownName(true, true, true), this, false);
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

		public Color GetEffectiveColor()
		{
			return Color.FromName(Character.Path("skin/color"));
		}
	}
}