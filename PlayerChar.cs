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
		public int CurrentRealm { get; private set; }

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

		public override void AdjustView()
		{
			base.AdjustView();
			AsciiChar = '@';
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
						NoxicoGame.AddMessage(i18n.Format("youdisplacex", bc.Character.GetKnownName(false, false, true)), bc.GetEffectiveColor());
						bc.XPosition = this.XPosition;
						bc.YPosition = this.YPosition;
					}
				}
			}
			base.Move(targetDirection, check);

			EndTurn();
			
			NoxicoGame.Sound.PlaySound(Character.HasToken("squishy") || Character.Path("skin/type/slime") != null ? "Splorch" : "Step");

			if (lx != XPosition || ly != YPosition)
			{
				ParentBoard.UpdateLightmap(this, true);
				this.DijkstraMap.Hotspots[0] = new Point(XPosition, YPosition);
				this.DijkstraMap.Update();
			}
			else if (AutoTravelling)
			{
				AutoTravelling = false;
				NoxicoGame.AddMessage("* TEST: couldn't go any further. *");
			}

			NoxicoGame.ContextMessage = null;
			if (OnWarp())
				NoxicoGame.ContextMessage = i18n.GetString("context_warp");
			else if (ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
				NoxicoGame.ContextMessage = i18n.GetString("context_droppeditem");
			else if (ParentBoard.Entities.OfType<Container>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition) != null)
				NoxicoGame.ContextMessage = i18n.GetString("context_container");
			else if (ParentBoard.Entities.OfType<Clutter>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition && c.AsciiChar == '\x0398') != null)
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
					NoxicoGame.AddMessage(i18n.Format("youhitxfory", hit.Character.GetKnownName(false, false, true), damage, i18n.Pluralize("point", damage)));
					hit.Hurt(damage, i18n.Format("death_shotbyx", this.Character.GetKnownName(true, true)), this, false);
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
				NoxicoGame.InGameTime.AddMilliseconds(increase);
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
			if (UpdateSex())
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
			if (NoxicoGame.KeyMap[(int)Keys.Z])
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
				NoxicoGame.Messages.Add("[Aim message]");
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
					NoxicoGame.AddMessage(i18n.GetString("youland"));
					Character.RemoveToken("flying");
					//add swim capability?
					var tile = ParentBoard.Tilemap[XPosition, YPosition];
					if (tile.Water && Character.IsSlime)
						Hurt(9999, i18n.GetString("death_doveinanddrowned"), null, false);
					else if (tile.Cliff)
						Hurt(9999, i18n.GetString("death_doveintodepths"), null, false, false);
					else if (tile.Fence)
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
						if (tile.Ceiling)
						{
							if (Character.GetStat(Stat.Cunning) < 10 ||
								(Character.GetStat(Stat.Cunning) < 20 && Random.NextDouble() < 0.5))
							{
								Hurt(2, i18n.GetString("death_crackedagainstceiling"), null, false);
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
				var drop = ParentBoard.Entities.OfType<DroppedItem>().FirstOrDefault(c => c.XPosition == XPosition && c.YPosition == YPosition);
				if (drop != null)
				{
					drop.Take(this.Character);
					NoxicoGame.HostForm.Noxico.Player.Energy -= 1000;
					NoxicoGame.AddMessage(i18n.Format("youpickup_x", drop.Item.ToString(drop.Token, true)));
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
					}, true, true, i18n.GetString("Bed"));
				}
				return;
			}

#if DEBUG
			if (NoxicoGame.KeyMap[(int)Keys.F3])
			{
				NoxicoGame.ClearKeys();
				ParentBoard.DumpToHtml();
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
			if (Character.UpdatePregnancy())
				return;

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
					NoxicoGame.KeyMap[(int)NoxicoGame.KeyBindings[KeyBinding.Fly]] = true; //force a landing
			}

			if (ParentBoard == null)
			{
				return;
			}
			ParentBoard.Update(true);
			if (ParentBoard.IsBurning(YPosition, XPosition))
				Hurt(10, i18n.GetString("death_burned"), null, false, false);
			//Leave EntitiesToAdd/Remove to Board.Update next passive cycle.
		}

		public override bool Hurt(float damage, string obituary, BoardChar aggressor, bool finishable = false, bool leaveCorpse = true)
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

			var dead = base.Hurt(damage, obituary, aggressor, finishable);
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

				NoxicoGame.AddMessage(i18n.GetString("gameover_title"), Color.Red);
				var playerFile = Path.Combine(NoxicoGame.SavePath, NoxicoGame.WorldName, "player.bin");
				File.Delete(playerFile);
				NoxicoGame.Sound.PlayMusic("set://Death");
				MessageBox.Ask(
					i18n.Format("youdied", obituary),
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
					NoxicoGame.AddMessage(string.Format("You hit {0} for {1} point{2}.", hit.Character.GetKnownName(false, false, true), damage, damage > 1 ? "s" : ""));
					hit.Hurt(damage, "being shot down by " + this.Character.GetKnownName(true, true, true), this, false);
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
				if (!(tile.SolidToWalker || tile.Ceiling))
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
	}
}