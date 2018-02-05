using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if DEBUG
using Keys = System.Windows.Forms.Keys;
#endif

namespace Noxico
{
    public class Player : BoardChar
    {
		public bool AutoTravelling { get; set; }
		private Dijkstra AutoTravelMap;
		public Direction AutoTravelLeave { get; set; }
		public TimeSpan PlayingTime { get; set; }
		public int Lives
		{
			get
			{
				return Character.HasToken("lives") ? (int)Character.GetToken("lives").Value : 0;
			}
			set
			{
				var l = Character.Path("lives");
				if (l == null)
					l = Character.AddToken("lives");
				l.Value = value;
			}
		}

        public Player()
        {
			this.AutoTravelMap = new Dijkstra();
			this.AutoTravelMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
			this.Energy = 5000;
		}

		public Player(Character character) : base(character)
		{
			this.AutoTravelMap = new Dijkstra();
			this.AutoTravelMap.Hotspots.Add(new Point(this.XPosition, this.YPosition));
			this.Energy = 5000;
		}

		/*
		public override void AdjustView()
		{
			base.AdjustView();
			Glyph = '@';
		}
		*/

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
					DungeonGenerator.DungeonGeneratorEntranceBoardNum = ParentBoard.BoardNum;
					DungeonGenerator.DungeonGeneratorEntranceWarpID = warp.ID;
					DungeonGenerator.DungeonGeneratorBiome = (int)ParentBoard.GetToken("biome").Value;
					DungeonGenerator.CreateDungeon();
					return;
				}
				else if (warp.TargetBoard == -2) //unconnected dungeon
				{
					Travel.Open();
					return;
				}

				var game = NoxicoGame.Me;
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
			if (this.ParentBoard != null)
			{
				this.ParentBoard.EntitiesToRemove.Add(this);
				this.ParentBoard.SaveToFile(this.ParentBoard.BoardNum);
			}
			var n = NoxicoGame.Me;
			this.ParentBoard = n.GetBoard(index);
			n.CurrentBoard = this.ParentBoard;
			this.ParentBoard.Entities.Add(this);
			ParentBoard.CheckCombatStart();
			ParentBoard.CheckCombatFinish();
			ParentBoard.UpdateLightmap(this, true);
			ParentBoard.Redraw();
			ParentBoard.PlayMusic();
			NoxicoGame.Immediate = true;

			if (ParentBoard.BoardType == BoardType.Town)
			{
				var boardName = ParentBoard.Name;
				var known = NoxicoGame.TravelTargets.ContainsValue(boardName);
				if (!known)
				{
					NoxicoGame.TravelTargets.Add(index, boardName);
					Program.WriteLine("Registered {0} as a fast-travel target.", boardName);
				}
			}

			this.ParentBoard.AimCamera(XPosition, YPosition);

			this.DijkstraMap.UpdateWalls(!Character.IsSlime, ParentBoard);
			this.DijkstraMap.Update();
			this.AutoTravelMap.UpdateWalls(!Character.IsSlime, ParentBoard);
		}

		public override bool MeleeAttack(BoardChar target)
		{
			var killedThem = base.MeleeAttack(target);
			if (!killedThem && !target.Character.HasToken("helpless"))
			{
				target.Character.AddToken("justmeleed");
				target.MeleeAttack(this);
			}
			return killedThem;
		}

		public override void Move(Direction targetDirection, SolidityCheck check = SolidityCheck.Walker)
		{
			var lx = XPosition;
			var ly = YPosition;

			check = SolidityCheck.Walker;
			if (Character.IsSlime)
				check = SolidityCheck.DryWalker;
			if (Character.HasToken("flying"))
				check = SolidityCheck.Flyer;

			#region Inter-board travel
			var n = NoxicoGame.Me;
			Board otherBoard = null;
			if (ly == 0 && targetDirection == Direction.North && this.ParentBoard.ToNorth > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToNorth);
				if (this.CanMove(otherBoard, lx, 49, check) != null)
					return;
				this.YPosition = 50;
				OpenBoard(this.ParentBoard.ToNorth);
			}
			else if (ly == 49 && targetDirection == Direction.South && this.ParentBoard.ToSouth > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToSouth);
				if (this.CanMove(otherBoard, lx, 0, check) != null)
					return;
				this.YPosition = -1;
				OpenBoard(this.ParentBoard.ToSouth);
			}
			else if (lx == 0 && targetDirection == Direction.West && this.ParentBoard.ToWest > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToWest);
				if (this.CanMove(otherBoard, 79, ly, check) != null)
					return;
				this.XPosition = 80;
				OpenBoard(this.ParentBoard.ToWest);
			}
			else if (lx == 79 && targetDirection == Direction.East && this.ParentBoard.ToEast > -1)
			{
				otherBoard = n.GetBoard(this.ParentBoard.ToEast);
				if (this.CanMove(otherBoard, 0, ly, check) != null)
					return;
				this.XPosition = -1;
				OpenBoard(this.ParentBoard.ToEast);
			}
			#endregion

			if (Character.HasToken("tutorial"))
				Character.GetToken("tutorial").Value++;

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
						if (!bc.OnPlayerBump.IsBlank())
						{
							bc.RunScript(bc.OnPlayerBump);
							return;
						}
						//Displace!
						NoxicoGame.AddMessage(i18n.Format("youdisplacex", bc.Character.GetKnownName(false, false, true)), bc.GetEffectiveColor());
						bc.XPosition = this.XPosition;
						bc.YPosition = this.YPosition;
					}
				}
			}
			base.Move(targetDirection, check);
			ParentBoard.AimCamera(XPosition, YPosition);

			EndTurn();

			if (Character.HasToken("squishy") || (Character.Path("skin/type") != null && Character.Path("skin/type").Text == "slime"))
				NoxicoGame.Sound.PlaySound("set://Squish");
			else
				NoxicoGame.Sound.PlaySound("set://Step");
			
			if (lx != XPosition || ly != YPosition)
			{
				ParentBoard.UpdateLightmap(this, true);
				this.DijkstraMap.Hotspots[0] = new Point(XPosition, YPosition);
				this.DijkstraMap.Update();
			}
			else if (AutoTravelling)
			{
				AutoTravelling = false;
#if DEBUG
				NoxicoGame.AddMessage("* TEST: couldn't go any further. *");
#endif
			}

			NoxicoGame.ContextMessage = null;
			if (OnWarp())
				NoxicoGame.ContextMessage = i18n.GetString("context_warp");
			else if (ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
				NoxicoGame.ContextMessage = i18n.GetString("context_droppeditem");
			else if (ParentBoard.Entities.OfType<Container>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
			{
				if (ParentBoard.Entities.OfType<Container>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition).Token.HasToken("corpse"))
					NoxicoGame.ContextMessage = i18n.GetString("context_corpse");
				else
					NoxicoGame.ContextMessage = i18n.GetString("context_container");
			}
			//else if (ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.Glyph == 0x147) != null)
			else if (ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.DBRole == "bed") != null)
				NoxicoGame.ContextMessage = i18n.GetString("context_bed");
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

			Energy -= 500;

			var x = this.XPosition;
			var y = this.YPosition;
			var distance = 0;
			var range = (int)weapon.Path("weapon/range").Value;
			//var damage = (int)weapon.Path("weapon/damage").Value;
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
					var damage = weap.Path("damage").Value * GetDefenseFactor(weap, hit.Character);
					var overallArmor = 0f;
					foreach (var targetArmor in hit.Character.GetToken("items").Tokens.Where(t => t.HasToken("equipped")))
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

					FireLine(weapon.Path("effect"), x, y);
					NoxicoGame.AddMessage(i18n.Format("youhitxfory", hit.Character.GetKnownName(false, false, true), damage, i18n.Pluralize("point", (int)Math.Ceiling(damage))));
					hit.Hurt(damage, "death_shot", this, false);
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
				for (y++; y < 50 && distance < range; y++, distance++)
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

			//START
			if (NoxicoGame.IsKeyDown(KeyBinding.Pause) || Vista.Triggers == XInputButtons.Start)
			{
				NoxicoGame.ClearKeys();
				Pause.Open();
				return;
			}

			var increase = 200 + (int)Character.GetStat(Stat.Speed);
			if (Character.HasToken("haste"))
				increase *= 2;
			else if (Character.HasToken("slow"))
				increase /= 2;
			Energy += increase;
			if (Energy < 5000)
			{
				var wasNight = Toolkit.IsNight();

				// kawa, DateTime is immutable now, you have to make a new one - sparks
				DateTime newTime = NoxicoGame.InGameTime.AddMilliseconds(increase);
				NoxicoGame.InGameTime = newTime;

				if (wasNight && !Toolkit.IsNight())
				{
					ParentBoard.UpdateLightmap(this, true);
					ParentBoard.Redraw();
				}
				EndTurn();
				return;
			}
			else
			{
				NoxicoGame.PlayerReady = true;
				Energy = 5000;
			}

			CheckForTimedItems();
			CheckForCopiers();
			if (Character.UpdateSex())
				return;

			var sleeping = Character.Path("sleeping");
			if (sleeping != null)
			{
				var hp = Character.GetToken("health");
				var hpMax = Character.MaximumHealth;
				hp.Value += 2;
				if (hp.Value > hpMax)
					hp.Value = hpMax;
				sleeping.Value--;
				if (sleeping.Value <= 0)
				{
					Character.RemoveToken("sleeping");
					Character.RemoveToken("helpless");
					NoxicoGame.AddMessage(i18n.GetString("yougetup"));
					if (Character.Health > Character.MaximumHealth)
						Character.Health = Character.MaximumHealth;
				}
				NoxicoGame.InGameTime.AddMinutes(5);
				EndTurn();
				return; //07-04-13 no more sleepwalking
			}

			var helpless = Character.HasToken("helpless");
			if (helpless)
			{
				if (Random.NextDouble() < 0.05)
				{
					Character.Health += 2;
					NoxicoGame.AddMessage(i18n.GetString("yougetup"));
					Character.RemoveToken("helpless");
					helpless = false;
				}
			}
			var flying = Character.HasToken("flying");

#if DEBUG
			if (NoxicoGame.KeyMap[Keys.Z])
			{
				NoxicoGame.ClearKeys();
				NoxicoGame.InGameTime.AddMinutes(30);
			}
#endif
			//Pause menu moved up so you can pause while <5000.

			//RIGHT
			if ((NoxicoGame.IsKeyDown(KeyBinding.Travel) || Vista.Triggers == XInputButtons.RightShoulder))
			{
				NoxicoGame.ClearKeys();
				if (!this.ParentBoard.AllowTravel)
				{
					if (this.ParentBoard.BoardType == BoardType.Dungeon)
						NoxicoGame.AddMessage(i18n.GetString("travel_notfromdungeon"));
					else
						NoxicoGame.AddMessage(i18n.GetString("travel_notfromwilds"));
					return;
				}
				Travel.Open();
				return;
			}

			//LEFT
			if (NoxicoGame.IsKeyDown(KeyBinding.Rest) || Vista.Triggers == XInputButtons.LeftShoulder)
			{
				NoxicoGame.ClearKeys();
				Energy -= 1000;
				EndTurn();
				return;
			}

			//GREEN
			if (NoxicoGame.IsKeyDown(KeyBinding.Interact) || Vista.Triggers == XInputButtons.A)
			{
				NoxicoGame.ClearKeys();
				//NoxicoGame.Messages.Add("[Aim message]");
				NoxicoGame.Mode = UserMode.Aiming;
				NoxicoGame.Cursor.ParentBoard = this.ParentBoard;
				NoxicoGame.Cursor.XPosition = this.XPosition;
				NoxicoGame.Cursor.YPosition = this.YPosition;
				NoxicoGame.Cursor.PopulateTabstops();
				NoxicoGame.Cursor.Point();
				if (Character.HasToken("tutorial") && !Character.GetToken("tutorial").HasToken("interactmode"))
				{
					Character.GetToken("tutorial").AddToken("dointeractmode");
					NoxicoGame.CheckForTutorialStuff();
				}
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
					NoxicoGame.AddMessage(i18n.GetString("youland"));
					Character.RemoveToken("flying");
					//add swim capability?
					var tile = ParentBoard.Tilemap[XPosition, YPosition];
					if (tile.Fluid != Fluids.Dry && !tile.Shallow && Character.IsSlime)
						Hurt(9999, "death_doveinanddrowned", null, false);
					else if (tile.Definition.Cliff)
						Hurt(9999, "death_doveintodepths", null, false, false);
					else if (tile.Definition.Fence)
					{
						//I guess I'm still a little... on the fence.
						/*
						var tileDesc = tile.GetDescription();
						if (!tileDesc.HasValue)
							tileDesc = new TileDescription() { Color = Color.Silver, Name = "obstacle" };
						NoxicoGame.AddMessage("You fall off the " + tileDesc.Value.Name + ".", tileDesc.Value.Color);
						Hurt(5, "landed on " + (tileDesc.Value.Name.StartsWithVowel() ? "an" : "a") + ' ' + tileDesc.Value.Name, null, false, true);
						*/
						//YEEEEAAAAH!!!!!!!!
					}
				}
				else
				{
					if (Character.HasToken("wings"))
					{
						if (Character.GetToken("wings").HasToken("small"))
						{
							NoxicoGame.AddMessage(i18n.GetString("wingsaretoosmall"));
							return;
						}
						var tile = ParentBoard.Tilemap[XPosition, YPosition];
						if (tile.Definition.Ceiling)
						{
							if (Character.GetStat(Stat.Cunning) < 10 ||
								(Character.GetStat(Stat.Cunning) < 20 && Random.NextDouble() < 0.5))
							{
								Hurt(2, "death_crackedagainstceiling", null, false);
								NoxicoGame.AddMessage(i18n.GetString("hittheceiling"));
							}
							else
								NoxicoGame.AddMessage(i18n.GetString("cantflyinside"));
							return;
						}
						//Take off
						Character.AddToken("flying").Value = 100;
						NoxicoGame.AddMessage(i18n.GetString("youfly"));
						return;
					}
					NoxicoGame.AddMessage(i18n.GetString("flyneedswings"));
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
				var itemsHere = DroppedItem.GetItemsAt(ParentBoard, XPosition, YPosition);
				if (itemsHere.Count == 1)
				{
					var drop = itemsHere[0];
					if (drop != null)
					{
						drop.Take(this.Character, ParentBoard);
						NoxicoGame.Me.Player.Energy -= 1000;
						NoxicoGame.AddMessage(i18n.Format("youpickup_x", drop.Item.ToString(drop.Token, true)));
						NoxicoGame.Sound.PlaySound("set://GetItem"); 
						ParentBoard.Redraw();
						return;
					}
				}
				else if (itemsHere.Count > 1)
				{
					DroppedItem.PickItemsFrom(itemsHere);
					return;
				}

				//Find bed
				//var bed = ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.Glyph == 0x147);
				var bed = ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.DBRole == "bed");
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
					}, true, true, i18n.GetString("Bed"));
				}
				return;
			}

#if DEBUG
			if (NoxicoGame.KeyMap[Keys.F3])
			{
				NoxicoGame.ClearKeys();
				ParentBoard.DumpToHtml(string.Empty);
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
				{
					AutoTravelling = false;
					if ((int)AutoTravelLeave > -1)
						this.Move(AutoTravelLeave);
				}
			}
        }

		public void AutoTravelTo(int x, int y)
		{
			AutoTravelMap.Hotspots[0] = new Point(x, y);
			AutoTravelMap.UpdateWalls(!Character.IsSlime);
			AutoTravelMap.Update();
			AutoTravelling = true;
			AutoTravelLeave = (Direction)(-1);
		}

		public void EndTurn()
		{
			Excite();

			NoxicoGame.PlayerReady = false;

			if (Character.HasToken("flying"))
			{
				var f = Character.GetToken("flying");
				f.Value--;
				if (!Character.HasToken("wings") || Character.GetToken("wings").HasToken("small"))
				{
					NoxicoGame.AddMessage(i18n.GetString("losewings"));
					f.Value = -10;
				}
				if (f.Value <= 0)
					NoxicoGame.KeyMap[NoxicoGame.KeyBindings[KeyBinding.Fly]] = true; //force a landing
			}

			if (ParentBoard == null)
			{
				return;
			}
			ParentBoard.Update(true);
			if (ParentBoard.IsBurning(YPosition, XPosition))
				Hurt(10, "death_burned", null, false, false);
			//Leave EntitiesToAdd/Remove to Board.Update next passive cycle.
		}

		public override bool Hurt(float damage, string cause, object aggressor, bool finishable = false, bool leaveCorpse = true)
		{
			if (AutoTravelling)
			{
				NoxicoGame.AddMessage(i18n.Format("autotravelstop"));
				AutoTravelling = false;
			}

			if (Character.HasItemEquipped("eternitybrooch"))
			{
				var brooch = Character.GetEquippedItemBySlot("neck"); //can assume the neck slot has the brooch.
				var today = NoxicoGame.InGameTime.DayOfYear;
				if (!brooch.HasToken("lastTrigger"))
					brooch.AddToken("lastTrigger", today - 2);
				if (Math.Abs(brooch.GetToken("lastTrigger").Value - today) >= 2 && Character.Health - damage <= 0) 
				{
					brooch.GetToken("lastTrigger").Value = today;
					NoxicoGame.AddMessage(i18n.GetString("eternitybrooched"));
					Character.Health = Character.MaximumHealth;
					Reposition();
					return false;
				}
			}

			var dead = base.Hurt(damage, cause, aggressor, finishable);
			if (dead)
			{
				if (aggressor != null && aggressor is BoardChar)
				{
					var aggChar = ((BoardChar)aggressor).Character;
					var relation = Character.Path("ships/" + aggChar.ID);
					if (relation == null)
					{
						relation = new Token(aggChar.ID);
						Character.Path("ships").Tokens.Add(relation);
					}
					relation.AddToken("killer");
					if (aggChar.HasToken("stolenfrom"))
					{
						var myItems = Character.GetToken("items").Tokens;
						var hisItems = aggChar.GetToken("items").Tokens;
						var stolenGoods = myItems.Where(t => t.HasToken("owner") && t.GetToken("owner").Text == aggChar.ID).ToList();
						foreach (var item in stolenGoods)
						{
							hisItems.Add(item);
							myItems.Remove(item);
						}
						aggChar.GetToken("stolenfrom").Name = "wasstolenfrom";
						aggChar.RemoveToken("hostile");
					}
				}

				if (Lives == 0)
				{
					Character.AddToken("gameover");
					NoxicoGame.AddMessage(i18n.GetString("gameover_title"), Color.Red);
					//var playerFile = Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "player.bin");
					//File.Delete(playerFile);
					var world = System.IO.Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName);
					NoxicoGame.Sound.PlayMusic("set://Death");
					NoxicoGame.InGame = false;
					var c = i18n.GetString(cause + "_player");
					if (c[0] == '[')
						c = i18n.GetString(cause);
					MessageBox.Ask(
						i18n.Format("youdied", c),
						() =>
						{
							Character.CreateInfoDump();
							Directory.Delete(world, true);
							NoxicoGame.HostForm.Close();
						},
						() =>
						{
							Directory.Delete(world, true);
							NoxicoGame.HostForm.Close();
						}
						);
				}
				else
				{
					Respawn();
				}
			}
			return dead;
		}

		public override void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "PLAY");
			base.SaveToFile(stream);
			stream.Write(PlayingTime.Ticks);
		}

		public static new Player LoadFromFile(BinaryReader stream)
		{
			Toolkit.ExpectFromFile(stream, "PLAY", "player entity");
			var e = BoardChar.LoadFromFile(stream);
			var newChar = new Player()
			{
				ID = e.ID, Glyph = e.Glyph, ForegroundColor = e.ForegroundColor, BackgroundColor = e.BackgroundColor,
				XPosition = e.XPosition, YPosition = e.YPosition, Blocking = e.Blocking,
				Character = e.Character,
			};
			newChar.PlayingTime = new TimeSpan(stream.ReadInt64());
			return newChar;
		}

		public new void AimShot(Entity target)
		{
			//TODO: throw whatever is being held by the player at the target, according to their Throwing skill and the total distance.
			//If it's a gun they're holding, fire it instead, according to their Shooting skill.
			//MessageBox.Message("Can't shoot yet, sorry.", true);

			if (target is Player)
			{
				MessageBox.Notice("Don't shoot yourself in the foot!", true);
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
					
					var overallArmor = 0f;
					foreach (var targetArmor in hit.Character.GetToken("items").Tokens.Where(t => t.HasToken("equipped")))
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

					NoxicoGame.AddMessage(i18n.Format("youhitxfory", hit.Character.GetKnownName(false, false, true), damage, i18n.Pluralize("point", (int)damage)));
					hit.Hurt(damage, "death_shot", this, false);
				}
				this.Character.IncreaseSkill(skill.Text);
			}

			NoxicoGame.Mode = UserMode.Walkabout;
			Energy -= 500;
			EndTurn();
		}

		public void Reposition()
		{
			var range = 10;
			var tries = 10;
			while (true)
			{
				var x = Random.Next(40 - range, 40 + range);
				var y = Random.Next(12 - (range / 2), 12 + (range / 2));
				var tile = ParentBoard.Tilemap[x, y];
				if (!(tile.SolidToWalker || tile.Definition.Ceiling))
				{
					XPosition = x;
					YPosition = y;
					break;
				}
				tries--;
				if (tries == 0)
				{
					range += 5;
					if (range >= 30)
					{
						Program.WriteLine("Player.Reposition() giving up.");
						XPosition = 40;
						YPosition = 12;
						break;
					}
				}
			}
		}

		public void Respawn()
		{
			var game = NoxicoGame.Me;
			var homeBoard = game.GetBoard((int)Character.GetToken("homeboard").Value);
			var bed = homeBoard.Entities.First(e => e is Clutter && e.ID == "Bed_playerRespawn");
			if (ParentBoard != homeBoard)
				OpenBoard(homeBoard.BoardNum);
			XPosition = bed.XPosition;
			YPosition = bed.YPosition;
#if DEBUG
			if (!Character.HasToken("easymode") && !Character.HasToken("wizard"))
#else
			if (!Character.HasToken("easymode"))
#endif
				Lives--;
			Character.Health = Character.MaximumHealth * 0.75f;
			UpdateCandle();
		}

		public void UpdateCandle()
		{
			var game = NoxicoGame.Me;
			var homeBoard = game.GetBoard((int)Character.GetToken("homeboard").Value);
			var candle = (Clutter)homeBoard.Entities.First(e => e is Clutter && e.ID == "lifeCandle");
			if (Character.HasToken("easymode"))
				candle.Description = i18n.GetString("candle_easymode");
			else if (Lives == 0)
				candle.Description = i18n.GetString("candle_0");
			else if (Lives == 1)
				candle.Description = i18n.GetString("candle_1");
			else if (Lives < 4)
				candle.Description = i18n.GetString("candle_3");
			else if (Lives < 7)
				candle.Description = i18n.GetString("candle_6");
			else
				candle.Description = i18n.GetString("candle_X");
		}
	}
}