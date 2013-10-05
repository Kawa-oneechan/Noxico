﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	public class InventoryItem : TokenCarrier
	{
		public string ID { get; private set; }
		public string Name { get; private set; }
		public string UnknownName { get; set; }
		public bool IsProperNamed { get; set; }
		public string A { get; set; }
		public string The { get; set; }
		public string OnUse { get; private set; }
		public string OnEquip { get; private set; }
		public string OnUnequip { get; private set; }
		public string OnTimer { get; private set; }

		public Token tempToken { get; set; }

		public override string ToString()
		{
			return ToString(null);
		}

		public string ToString(Token token, bool the = false, bool a = true)
		{
			if (ID == "book" && token != null && token.HasToken("id") && token.GetToken("id").Value < NoxicoGame.BookTitles.Count)
				return string.Format("\"{0}\"", NoxicoGame.BookTitles[(int)token.GetToken("id").Value]);

			var canBeIdentified = !string.IsNullOrWhiteSpace(UnknownName);
			var isIdentified = canBeIdentified ? !NoxicoGame.Identifications.Contains(ID) : true;

			var name = isIdentified ? Name : UnknownName;
			var color = (token != null && token.HasToken("color")) ? Color.NameColor(token.GetToken("color").Text) : string.Empty;
			var reps = new Dictionary<string, string>()
			{
				{ "[color]", color },
				{ "[, color]", ", " + color },
				{ "[color ]", color + " " },
				{ "[color, ]", color + ", " },
			};
			if (color.Length == 0)
			{
				foreach (var key in reps.Keys)
					name = name.Replace(key, "");
			}
			else
			{
				foreach (var item in reps)
					name = name.Replace(item.Key, item.Value);
			}

			var proper = IsProperNamed && isIdentified;
			if (proper || !a)
			{
				if (the && !string.IsNullOrEmpty(The))
					return The + ' ' + name;
				return name;
			}
			if (HasToken("charge") && token != null)
			{
				var charge = 0;
				var limit = "inf";
				if (GetToken("charge").HasToken("limit"))
					limit = Path("charge/limit").Value.ToString();
				if (token.HasToken("charge") && token.GetToken("charge").Value > 0)
				{
					charge = (int)token.GetToken("charge").Value;
					var collective = Path("charge/collectivename");
					if (collective != null)
						name = collective.Text;
				}
				return string.Format("{0} {1} ({2}/{3})", the ? The : (Toolkit.StartsWithVowel(name) ? "an" : "a"), name, charge, limit);
			}
			if (isIdentified)
				return string.Format("{0} {1}", the ? The : A, name).Trim();
			return string.Format("{0} {1}", the ? The : (Toolkit.StartsWithVowel(UnknownName) ? "an" : "a"), name).Trim();
		}

		//Added for Jint's sake.
		//HEADS UP: were it not for 'a' being true by default, we could remove this with the newest Jint update.
		public string ToString(Token token)
		{
			return this.ToString(token, false, true);
		}

		public string GetDescription(Token token)
		{
			// i.HasToken("description") && !t.HasToken("unidentified") ? i.GetToken("description").Text : "This is " + i.ToString() + ".";
			if (this.ID == "book" && token != null && token.HasToken("id") && token.GetToken("id").Value < NoxicoGame.BookTitles.Count)
				return string.Format("This is \"{0}\", by {1}.", NoxicoGame.BookTitles[(int)token.GetToken("id").Value], NoxicoGame.BookAuthors[(int)token.GetToken("id").Value]);
			var a = "";
			if (this.HasToken("description"))
			{
				var ret = GetToken("description").Text;
				var color = (token != null && token.HasToken("color")) ? Color.NameColor(token.GetToken("color").Text) : string.Empty;
				var reps = new Dictionary<string, string>()
				{
					{ "[color]", color },
					{ "[, color]", ", " + color },
					{ "[color ]", color + " " },
					{ "[color, ]", color + ", " },
				};
				if (color.Length == 0)
				{
					foreach (var key in reps.Keys)
						ret = ret.Replace(key, "");
				}
				else
				{
					foreach (var item in reps)
						ret = ret.Replace(item.Key, item.Value);
				}
				return ret;
			}
			else
			{
				a = Toolkit.StartsWithVowel(this.UnknownName) ? "an " : "a ";
			}
			return "This is " + this.ToString(token) + ".";
		}

		public static InventoryItem FromToken(Token item)
		{
			var ni = new InventoryItem();
			var nameParts = item.Text.Split(',');
			ni.ID = nameParts[0];
			if (nameParts.Length > 1)
				ni.Name = nameParts[1].Trim();
			else
				ni.Name = nameParts[0].Replace('_', ' ');
			if (item.HasToken("_u"))
				ni.UnknownName = item.GetToken("_u").Text;
			if (item.HasToken("_a"))
				ni.A = item.GetToken("_a").Text;
			else
				ni.A = ni.Name.StartsWithVowel() ? i18n.GetString("an") : i18n.GetString("a");
			ni.IsProperNamed = char.IsUpper(ni.Name[0]);
			if (item.HasToken("_t"))
				ni.The = item.GetToken("_t").Text;
			else
				ni.The = i18n.GetString("the");
			ni.OnUse = null;
			foreach (var script in item.Tokens.Where(t => t.Name == "script"))
			{
				switch (script.Text)
				{
					case "equip":
						ni.OnEquip = script.GetToken("#text").Text;
						break;
					case "unequip":
						ni.OnEquip = script.GetToken("#text").Text;
						break;
					case "timer":
						ni.OnTimer = script.GetToken("#text").Text;
						break;
					default:
						ni.OnUse = script.GetToken("#text").Text;
						break;
				}
			}
			ni.Tokens.Clear();
			ni.Tokens.AddRange(item.Tokens);
			ni.RemoveAll("_u");
			ni.RemoveAll("_a");
			ni.RemoveAll("_t");
			ni.RemoveAll("script");
			return ni;
		}

		public void CheckHands(Character character, string slot)
		{
			var max = slot == "ring" ? 8 : 2;
			if (character.HasToken("monoceros"))
				max = slot == "ring" ? 10 : 3;
			if (character.HasToken("quadruped"))
			{
				if (slot == "hand")
					max = character.HasToken("monoceros") ? 2 : 1;
				else
					max = character.HasToken("monoceros") ? 2 : 0;
			}
			/* therefore:
			 *           normal  pony  unicorn  humancorn
			 * rings     8       0     2        10
			 * weapons   2       1     2        3
			 */
			var worn = 0;
			var items = character.GetToken("items");
			foreach (var carriedItem in items.Tokens)
			{
				if (!carriedItem.HasToken("equipped"))
					continue;
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					continue;
				var equip = find.GetToken("equipable");
				if (equip.HasToken(slot))
					worn++;
			}
			if (worn >= max)
			{
				var error = "yourhandsarefull";
				if (max == 1 && slot == "hand")
					error = "yourmouthisfull";
				else if (max == 2 && slot == "ring")
					error = "yourhornisfull";
				throw new ItemException(i18n.GetString(error));
			}
		}

		public void CheckPants(Character character, Token item)
		{
			if (!(character.HasToken("taur") || (character.HasToken("quadruped"))))
				return;
			if ((item.HasToken("underpants") && item.HasToken("undershirt")) ||
				(item.HasToken("pants") && item.HasToken("shirt")))
				return; //allow bodysuits
			throw new ItemException("Your body is not made for this sort of clothing.");
		}

		public bool CanSeeThrough()
		{
			//return true;
			if (!HasToken("equipable"))
				throw new ItemException("Tried to check translucency on something not equipable.");
			return this.GetToken("equipable").HasToken("translucent");
		}

		public bool CanReachThrough()
		{
			if (!HasToken("equipable"))
				throw new ItemException("Tried to check reach on something not equipable.");
			return this.GetToken("equipable").HasToken("reach");
		}

		public bool Equip(Character character, Token item)
		{
			/*
			if rings and character is quadruped, error out.
			if required slots have covering slots
				check for target slot's reachability.
				if unreachable, try to temp-remove items in covering slots, recursively.
				if still unreachable, error out.
			if required slots are taken
				try to unequip the items in those slots, recursively.
				if required slots are still taken, error out;
				else, mark the item as equipped.
			replace each temp-removed item whose required slots are still free.
			*/
			var equip = this.GetToken("equipable");
			var tempRemove = new Stack<Token>();
			//var items = character.GetToken("items");

			//TODO: make full quadrupeds equip weapons in their mouth instead of the hands they don't have.
			//This means they can carry only ONE weapon at a time, and maybe not be able to converse until unequipped.
			if ((equip.HasToken("hands") || equip.HasToken("ring")) && (character.HasToken("quadruped")))
				throw new ItemException("[You] cannot put on the " + this.Name + " because [you] lack[s] hands.");

			if (equip.HasToken("hand"))
				CheckHands(character, "hand");
			else if (equip.HasToken("ring"))
				CheckHands(character, "ring");
			if (equip.HasToken("pants") || equip.HasToken("underpants"))
				CheckPants(character, equip);

			foreach (var t in equip.Tokens)
			{
				if (t.Name == "underpants" && (!TempRemove(character, tempRemove, "pants") || !TempRemove(character, tempRemove, "underpants")))
					return false;
				else if (t.Name == "undershirt" && (!TempRemove(character, tempRemove, "shirt") || !TempRemove(character, tempRemove, "undershirt")))
					return false;
				else if (t.Name == "shirt" && (!TempRemove(character, tempRemove, "shirt") || !TempRemove(character, tempRemove, "jacket")))
					return false;
				else if (t.Name == "jacket" && (!TempRemove(character, tempRemove, "cloak") || !TempRemove(character, tempRemove, "jacket")))
					return false;
				else if (t.Name == "socks" && (!TempRemove(character, tempRemove, "shoes") || !TempRemove(character, tempRemove, "socks")))
					return false;
			}

			var succeed = true;
			if (!string.IsNullOrWhiteSpace(this.OnEquip))
				succeed = (bool)RunScript(item, this.OnEquip, character, null, null);
			if (succeed)
				item.AddToken("equipped");

			if (this.HasToken("timer") && !string.IsNullOrWhiteSpace(this.OnTimer) && !item.HasToken("timer"))
			{
				item.AddToken("timer").Value = (this.GetToken("timer").Value == 0) ? 60 : this.GetToken("timer").Value;
				item.GetToken("timer").Text = NoxicoGame.InGameTime.ToBinary().ToString();
			}

			character.RecalculateStatBonuses();
			character.CheckHasteSlow();

			//Difficult bit: gotta re-equip tempremovals without removing the target item all over. THAT WOULD BE QUITE BAD.
			return succeed;
		}

		public bool Unequip(Character character, Token item)
		{
			/*
			if item's slots have covering slots
				check for target slot's reachability.
				if unreachable, try to temp-remove items in covering slots, recursively.
				if still unreachable, error out.
			if item is cursed, error out
			mark item as unequipped.
			*/
			if (item != null && item.HasToken("cursed") && item.GetToken("cursed").HasToken("known"))
				throw new ItemException(!string.IsNullOrWhiteSpace(item.GetToken("cursed").Text) ? item.GetToken("cursed").Text : "[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.");

			var equip = this.GetToken("equipable");
			var tempRemove = new Stack<Token>();
			var items = character.GetToken("items");
			foreach (var t in equip.Tokens)
			{
				if (t.Name == "underpants")
					TempRemove(character, tempRemove, "pants");
				else if (t.Name == "undershirt")
					TempRemove(character, tempRemove, "shirt");
				else if (t.Name == "shirt")
					TempRemove(character, tempRemove, "jacket");
				else if (t.Name == "jacket")
					TempRemove(character, tempRemove, "cloak");
				else if (t.Name == "socks")
					TempRemove(character, tempRemove, "shoes");
			}

			if (item == null)
				item = items.Tokens.Find(x => x.Name == this.ID);

			if (item.HasToken("cursed"))
			{
				item.GetToken("cursed").Tokens.Add(new Token() { Name = "known" });
				throw new ItemException("[You] tr[ies] to unequip " + this.ToString(item, true) + ", but find[s] " + (this.HasToken("plural") ? "them" : "it") + " stuck to [your] body!\n");
			}

			var succeed = true;
			if (!string.IsNullOrWhiteSpace(this.OnUnequip))
				succeed = (bool)RunScript(item, this.OnUnequip, character, null, null);
			if (succeed)
				item.RemoveToken("equipped");

			if (!string.IsNullOrWhiteSpace(this.OnTimer) && this.Path("timer/evenunequipped") == null)
				item.RemoveToken("timer");

			//Not sure about automatically putting pants back on after taking them off to take off underpants...
			//while (tempRemove.Count > 0)
			//	tempRemove.Pop().Tokens.Add(new Token() { Name = "equipped" });

			character.RecalculateStatBonuses();
			character.CheckHasteSlow();
			return succeed;
		}

		private bool TempRemove(Character character, Stack<Token> list, string slot)
		{
			foreach (var carriedItem in character.GetToken("items").Tokens)
			{
				if (!carriedItem.HasToken("equipped"))
					continue;
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				var equip = find.GetToken("equipable");
				if (equip == null)
				{
					System.Windows.Forms.MessageBox.Show("Item " + carriedItem.Name + " is marked as equipped, but " + find.Name + " is not equippable.");
					carriedItem.RemoveToken("equipped");
					continue;
				}
				if (equip.HasToken(slot))
				{
					if (equip.HasToken("reach"))
						return true;
					var success = find.Unequip(character, carriedItem);
					if (success)
						list.Push(carriedItem);
					return success;
				}
			}
			return true;
		}

		public void Drop(BoardChar boardChar, Token item)
		{
			//Find a spot to drop the item
			int lives = 1000, x = 0, y = 0;
			while (lives > 0)
			{
				x = boardChar.XPosition + Random.Next(-1, 2);
				y = boardChar.YPosition + Random.Next(-1, 2);
				if (!boardChar.ParentBoard.IsSolid(y, x) && !boardChar.ParentBoard.IsBurning(y, x))
					break;
				lives--;
			}
			if (lives == 0)
			{
				var tile = boardChar.ParentBoard.Tilemap[x, y];
				if (tile.Water || tile.Cliff)
					NoxicoGame.AddMessage((boardChar is Player ? "You" : boardChar.Character.GetKnownName(false, false, true, true)) + " dropped " + this.ToString(item, true, false) + " in the " + (tile.Cliff ? "depths" : "water") + "!");
				boardChar.Character.GetToken("items").Tokens.Remove(item);
				boardChar.Character.CheckHasteSlow();
				return;
			}
			var droppedItem = new DroppedItem(this, item)
			{
				XPosition = x,
				YPosition = y,
				ParentBoard = boardChar.ParentBoard,
			};
			droppedItem.AdjustView();
			droppedItem.ParentBoard.EntitiesToAdd.Add(droppedItem);
			boardChar.Character.GetToken("items").Tokens.Remove(item);
			boardChar.Character.CheckHasteSlow();
		}

		public void Use(Character character, Token item, bool noConfirm = false)
		{
			var boardchar = NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<BoardChar>().First(x => x.Character == character);
			var runningDesc = "";

			Action<string> showDesc = new Action<string>(d =>
			{
				if (d.Contains('\n'))
					MessageBox.Notice(runningDesc.Viewpoint(boardchar));
				else
					NoxicoGame.AddMessage(runningDesc.Viewpoint(boardchar));
			});

			#region Books
			if (this.ID == "book")
			{
				TextScroller.ReadBook((int)item.GetToken("id").Value);
				return;
			}
			#endregion

			#region Equipment
			if (this.HasToken("equipable"))
			{
				if (item == null)
				{
					var items = character.GetToken("items");
					item = items.Tokens.Find(x => x.Name == this.ID);
				}

				if (!item.HasToken("equipped"))
				{
					//TODO: only ask if it's the player?
					//Not wearing it
					MessageBox.Ask(runningDesc + "Equip " + this.ToString(item, true) + "?", () =>
					{
						try
						{
							if (this.Equip(character, item))
							{
								runningDesc += "[You] equip[s] " + this.ToString(item, true) + ".";
							}
						}
						catch (ItemException c)
						{
							runningDesc += c.Message;
						}
						if (!string.IsNullOrWhiteSpace(runningDesc))
							showDesc(runningDesc);
						return;
					},
						null);
				}
				else
				{
					//Wearing/wielding it
					if (item.HasToken("cursed") && item.GetToken("cursed").HasToken("known"))
					{
						runningDesc += !string.IsNullOrWhiteSpace(item.GetToken("cursed").Text) ? item.GetToken("cursed").Text : "[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.";
						showDesc(runningDesc.Viewpoint(boardchar));
						return;
					}
					MessageBox.Ask("Unequip " + this.ToString(item, true) + "?", () =>
					{
						try
						{
							if (this.Unequip(character, item))
							{
								runningDesc += "[You] unequip[s] " + this.ToString(item, true) + ".";
							}
						}
						catch (ItemException x)
						{
							runningDesc += x.Message;
						}
						if (!string.IsNullOrWhiteSpace(runningDesc))
							showDesc(runningDesc.Viewpoint(boardchar));
						return;
					},
						null);
				}
				return;
			}
			#endregion

			if (this.HasToken("ammo"))
			{
				MessageBox.Notice("This is ammo. To use it, fire the weapon that requires this kind of munition.");
				return;
			}

			if (this.HasToken("quest") || this.HasToken("nouse"))
			{
				if (this.HasToken("description"))
					runningDesc = this.GetToken("description").Text + "\n\n";
				showDesc(runningDesc + "This item has no effect.");
				return;
			}

			//Confirm use of potentially hazardous items
			if (!noConfirm)
			{
				var name = new StringBuilder();
				if (this.IsProperNamed)
					name.Append(string.IsNullOrWhiteSpace(this.The) ? "" : this.The + ' ');
				else
					name.Append(string.IsNullOrWhiteSpace(this.A) ? "" : this.A + ' ');
				name.Append(this.Name);
				if (item.HasToken("unidentified") && !string.IsNullOrWhiteSpace(this.UnknownName))
				{
					runningDesc = "You don't know what this does.\n\nDo you want to find out?";
				}
				else
				{
					if (this.HasToken("description"))
					{
						//No need to check for "worn" or "examined" here...
						runningDesc = this.GetDescription(item) + "\n\n"; //this.GetToken("description").Text + "\n\n";
					}
					runningDesc += "Do you want to use " + this.ToString(item, true) + "?";
				}
				MessageBox.Ask(runningDesc, () => { this.Use(character, item, true); }, null);
				return;
			}

			var statBonus = this.GetToken("statbonus");
			if (statBonus != null)
			{
				foreach (var bonus in statBonus.Tokens)
				{
					if (bonus.Name == "health")
					{
						character.Health += bonus.Value;
						if (character.Health > character.MaximumHealth)
							character.Health = character.MaximumHealth;
					}
				}
			}

			var food = this.GetToken("food");
			if (food != null)
				Eat(character, food);

			if (!string.IsNullOrWhiteSpace(this.OnUse))
				RunScript(item, this.OnUse, character, boardchar, (x => runningDesc += x));
			else
				this.Consume(character, item);

			if (!string.IsNullOrWhiteSpace(runningDesc))
				showDesc(runningDesc.Viewpoint(boardchar));
		}

		public void Consume(Character carrier, Token carriedItem)
		{
			if (this.HasToken("charge"))
			{
				var charge = carriedItem.Path("charge");
				if (charge == null && carriedItem.Name == "charge")
					charge = carriedItem;
				if (charge == null || charge.Value == 1)
				{
					if (HasToken("revert"))
					{
						carriedItem.Name = GetToken("revert").Text;
						carriedItem.Tokens.Clear();
					}
					else
					{
						carrier.GetToken("items").Tokens.Remove(carriedItem);
						carrier.CheckHasteSlow();
					}
				}
				else
					charge.Value--;
			}
			else
			{
				if (HasToken("revert"))
				{
					carriedItem.Name = GetToken("revert").Text;
					carriedItem.Tokens.Clear();
				}
				else
				{
					carrier.GetToken("items").Tokens.Remove(carriedItem);
					carrier.CheckHasteSlow();
				}
			}
		}

		public object RunScript(Token item, string script, Character character, BoardChar boardchar, Action<string> running)
		{
			var js = JavaScript.MainMachine;
			JavaScript.Ascertain(js);
			js.SetParameter("user", character);
			js.SetParameter("thisItem", this);
			js.SetParameter("thisToken", item);
			js.SetFunction("Consume", new Action<string>(x => this.Consume(character, item) /* character.GetToken("items").Tokens.Remove(item) */));
			js.SetFunction("print", new Action<string>(x =>
			{
				var paused = true;
				MessageBox.ScriptPauseHandler = () =>
				{
					paused = false;
				};
				MessageBox.Notice(x, true);
				while (paused)
				{
					NoxicoGame.HostForm.Noxico.Update();
					System.Windows.Forms.Application.DoEvents();
				}
			}));
			js.SetFunction("Identify", new Action<string>(x =>
			{
				if (character.GetToken("cunning").Value < 10)
				{
					//Dumb characters can't identify as well.
					if (Random.NextDouble() < 0.5)
						return;
				}

				//Random potion identification
				if (this.HasToken("randomized"))
				{
					var rid = (int)this.GetToken("randomized").Value;
					if (this.Path("equipable/ring") != null && rid < 128)
						rid += 128;
					var rdesc = NoxicoGame.HostForm.Noxico.Potions[rid];
					if (rdesc[0] != '!')
					{
						//Still unidentified. Let's rock.
						rdesc = '!' + rdesc;
						NoxicoGame.HostForm.Noxico.Potions[rid] = rdesc;
						this.UnknownName = null;
					}
					//Random potions and rings are un-unidentified by taking away their UnknownName, but we clear the unidentified state anyway.
					//item.RemoveToken("unidentified");
					//runningDesc += "You have identified this as " + this.ToString(item, true) + ".";
					//return;
				}

				//Regular item identification
				if (!string.IsNullOrWhiteSpace(this.UnknownName) && !NoxicoGame.Identifications.Contains(this.ID))
				{
					NoxicoGame.Identifications.Add(this.ID);
					if (running != null)
						running("You have identified this as " + this.ToString(item, true) + ".");
				}
			}));
#if DEBUG
			js.SetDebugMode(true);
			js.Step += (s, di) =>
			{
				Program.Write("JINT: {0}", di.CurrentStatement.Source.Code.ToString());
			};
#endif
			var ret = js.Run(script);
			if (!(ret is bool))
				ret = true;
			return ret;
		}

		#region PillowShout's additions
        public bool IsSuit()
        {
            var eq = this.GetToken("equipable");

            if (eq != null)
                return eq.HasToken("pants") && eq.HasToken("shirt");

            return false;
        }
		#endregion

		public string ToLongString(Token token)
		{
			var info = new List<string>();
			if (HasToken("equipable"))
			{
				if (HasToken("weapon"))
				{
					info.Add(Path("weapon/damage").Value + " dmg");
					if (new [] { "throwing", "small_firearm", "large_firearm", "huge_firearm" }.Contains(Path("weapon/skill").Text))
						info.Add("ranged");
					else
						info.Add("m\u00EAl\u00E9e");
				}
				if (HasToken("statbonus"))
				{
					foreach (var bonus in GetToken("statbonus").Tokens)
					{
						if (bonus.Name == "health")
							info.Add(bonus.Value + " HP");
						else
							info.Add(bonus.Value + " " + bonus.Name.Remove(3));
					}
				}
			}
			if (info.Count == 0)
				return ToString(token);
			return ToString(token) + " (" + string.Join(", ", info) + ")";
		}

		public void Eat(Character gourmand, Token item)
		{
			if (item.HasToken("fat"))
			{
				var hwa = Random.Flip() ? "hips" : Random.Flip() ? "waist" : "ass/size";
				var change = Random.NextDouble() * 0.25;
				if (change > 0)
				{
					gourmand.Path(hwa).Value += (float)change;
					if (!gourmand.HasToken("player"))
						return;
					if (hwa.Equals("ass/size"))
						hwa = Descriptions.ButtRandom();
					NoxicoGame.AddMessage("That went right to your " + hwa + "!");
				}
			}
		}
	}

	[Serializable]
	public class ItemException : Exception
	{
		public ItemException()
			: base()
		{
		}

		public ItemException(string message)
			: base(message)
		{
		}

		public ItemException(string message, Exception exception)
			: base(message, exception)
		{
		}

		protected ItemException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
			: base(info, context)
		{
		}
	}
}
