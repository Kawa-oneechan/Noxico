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
		public string Script { get; private set; }

		public Token tempToken { get; set; }
		private static XmlDocument itemDoc, costumeDoc;

		/*
		public override string ToString()
		{
			if (IsProperNamed)
				return Name;
			return string.Format("{0} {1}", A, Name);
		}
		*/
		public override string ToString()
		{
			return ToString(null);
		}

		public string ToString(Token token, bool the = false, bool a = true)
		{
			if (ID == "book" && token != null && token.HasToken("id") && token.GetToken("id").Value < NoxicoGame.BookTitles.Count)
				return string.Format("\"{0}\"", NoxicoGame.BookTitles[(int)token.GetToken("id").Value]);

			var name = (token != null && token.HasToken("unidentified") && !string.IsNullOrWhiteSpace(UnknownName)) ? UnknownName : Name;
			var color = (token != null && token.HasToken("color")) ? Toolkit.NameColor(token.GetToken("color").Text) : "";
			var reps = new Dictionary<string, string>()
			{
				{ "[color]", color },
				{ "[, color]", ", " + color },
				{ "[color ]", color + " " },
				{ "[color, ]", color + ", " },
			};
			if (color == "")
			{
				foreach (var key in reps.Keys)
					name = name.Replace(key, "");
			}
			else
			{
				foreach (var item in reps)
					name = name.Replace(item.Key, item.Value);
			}

			var proper = IsProperNamed && (token != null && !token.HasToken("unidentified"));
			if (proper || !a)
			{
				if (the && The != "")
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
			return string.Format("{0} {1}", the ? The : (token != null && token.HasToken("unidentified") ? (Toolkit.StartsWithVowel(UnknownName) ? "an" : "a") : A), name).Trim();
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
				var color = (token != null && token.HasToken("color")) ? Toolkit.NameColor(token.GetToken("color").Text) : "";
				var reps = new Dictionary<string, string>()
				{
					{ "[color]", color },
					{ "[, color]", ", " + color },
					{ "[color ]", color + " " },
					{ "[color, ]", color + ", " },
				};
				if (color == "")
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
			return "This is " + a + this.ToString(token) + ".";
		}

		public static InventoryItem FromXML(XmlElement x)
		{
			var ni = new InventoryItem();
			ni.ID = x.GetAttribute("id");
			ni.Name = x.GetAttribute("name");
			ni.UnknownName = x.GetAttribute("unknown");
			ni.A = x.GetAttribute("a");
			ni.The = x.GetAttribute("the");
			ni.IsProperNamed = x.GetAttribute("proper") == "true";

			var t = x.ChildNodes.OfType<XmlCDataSection>().FirstOrDefault();
			if (t != null)
				ni.Tokens = Token.Tokenize(t.Value);
#if DEBUG
			else
			{
				ni.Tokens = Token.Tokenize(x);
				Console.WriteLine("Item {0} conversion:", ni.ID);
				Console.WriteLine("\t\t\t<![CDATA[");
				Console.Write(ni.DumpTokens(ni.Tokens, 0));
				Console.WriteLine("\t\t\t]]>");
				Console.WriteLine();
			}
#endif

			ni.Script = null;
			if (ni.ID == "catmorph")
				ni.ID = "catmorph";
			var ses = x.SelectNodes("script").OfType<XmlElement>().ToList();
			if (ses.Count == 0)
				return ni;
			if (ses[0] != null && ses[0].GetAttribute("type") == "text/javascript")
				ni.Script = ses[0].InnerText;
			return ni;
		}

		public void CheckHands(Character character, string slot)
		{
			var max = slot == "ring" ? 8 : 2;
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
				throw new ItemException("Your hands are already full.");
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
			var items = character.GetToken("items");

			//TODO: make full quadrupeds equip weapons in their mouth instead of the hands they don't have.
			//This means they can carry only ONE weapon at a time, and maybe not be able to converse until unequipped.
			if ((equip.HasToken("hands") || equip.HasToken("ring")) && (character.HasToken("noarms")))
				throw new ItemException("[You] cannot put on the " + this.Name + " because [you] lack[s] hands.");

			if (equip.HasToken("hand"))
				CheckHands(character, "hand");
			else if (equip.HasToken("ring"))
				CheckHands(character, "ring");

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
			}

			item.Tokens.Add(new Token() { Name = "equipped" });
			character.RecalculateStatBonuses();
			character.CheckHasteSlow();

			//Difficult bit: gotta re-equip tempremovals without removing the target item all over. THAT WOULD BE QUITE BAD.
			return true;
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
				throw new ItemException("[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.");

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
			}

			if (item == null)
				item = items.Tokens.Find(x => x.Name == this.ID);

			if (item.HasToken("cursed"))
			{
				item.GetToken("cursed").Tokens.Add(new Token() { Name = "known" });
				throw new ItemException("[You] tr[ies] to unequip " + this.ToString(item, true) + ", but find[s] " + (this.HasToken("plural") ? "them" : "it") + " stuck to [your] body!\n");
			}

			item.Tokens.Remove(item.GetToken("equipped"));

			//Not sure about automatically putting pants back on after taking them off to take off underpants...
			//while (tempRemove.Count > 0)
			//	tempRemove.Pop().Tokens.Add(new Token() { Name = "equipped" });

			character.RecalculateStatBonuses();
			character.CheckHasteSlow();
			return true;
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
				x = boardChar.XPosition + Toolkit.Rand.Next(-1, 2);
				y = boardChar.YPosition + Toolkit.Rand.Next(-1, 2);
				if (!boardChar.ParentBoard.IsSolid(y, x) && !boardChar.ParentBoard.IsBurning(y, x))
					break;
				lives--;
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
						MessageBox.Message(runningDesc.Viewpoint(boardchar));
						return;
					},
						null);
				}
				else
				{
					//Wearing/wielding it
					if (item.HasToken("cursed") && item.GetToken("cursed").HasToken("known"))
					{
						runningDesc += "[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.";
						MessageBox.Message(runningDesc.Viewpoint(boardchar));
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
						MessageBox.Message(runningDesc.Viewpoint(boardchar));
						return;
					},
						null);
				}
				return;
			}
			#endregion

			if (this.HasToken("ammo"))
			{
				MessageBox.Message("This is ammo. To use it, fire the weapon that requires this kind of munition.");
				return;
			}

			if (this.HasToken("quest") || this.HasToken("nouse"))
			{
				if (this.HasToken("description"))
					runningDesc = this.GetToken("description").Text + "\n\n";
				MessageBox.Message(runningDesc + "This item has no effect.");
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
						runningDesc = this.GetToken("description").Text + "\n\n";
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
						character.GetToken("health").Value += bonus.Value;
						if (character.GetToken("health").Value > character.GetMaximumHealth())
							character.GetToken("health").Value = character.GetMaximumHealth();
					}
				}
			}

			if (!string.IsNullOrWhiteSpace(this.Script))
			{
				var js = Javascript.MainMachine;
				Javascript.Ascertain(js, true);
				js.SetParameter("user", character);
				js.SetParameter("userbc", boardchar);
				js.SetFunction("consume", new Action<string>(x => this.Consume(character, item) /* character.GetToken("items").Tokens.Remove(item) */));
				js.SetFunction("message", new Action<string>(x =>
				{
					var paused = true;
					MessageBox.ScriptPauseHandler = () =>
					{
						paused = false;
					};
					MessageBox.Message(x);
					while (paused)
					{
						NoxicoGame.HostForm.Noxico.Update();
						System.Windows.Forms.Application.DoEvents();
					}
				}));
				js.SetFunction("identify", new Action<string>(x =>
				{
					if (character.GetToken("cunning").Value < 10)
					{
						//Dumb characters can't identify as well.
						if (Toolkit.Rand.NextDouble() < 0.5)
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
					if (item.HasToken("unidentified"))// && !string.IsNullOrWhiteSpace(this.UnknownName))
					{
						item.RemoveToken("unidentified");
						runningDesc += "You have identified this as " + this.ToString(item, true) + ".";
					}
				}));
#if DEBUG
				js.SetDebugMode(true);
				js.Step += (s, di) =>
				{
					Console.Write("JINT: {0}", di.CurrentStatement.Source.Code.ToString());
				};
#endif
				js.Run(this.Script);
			}
			else
				this.Consume(character, item);
			/*
			else if(this.HasToken("singleuse"))
			{
				character.GetToken("items").Tokens.Remove(item);
			}
			*/

			if (!string.IsNullOrWhiteSpace(runningDesc))
				MessageBox.Message(runningDesc.Viewpoint(boardchar));
		}

		public void Consume(Character carrier, Token carriedItem)
		{
			if (this.HasToken("charge"))
			{
				var charge = carriedItem.Path("charge");
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

		public static List<Token> RollContainer(Character owner, string type)
		{
			if (costumeDoc == null)
			{
				costumeDoc = Mix.GetXMLDocument("costumes.xml");
				itemDoc = Mix.GetXMLDocument("items.xml");
				//itemDoc = new XmlDocument();
				//itemDoc.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Items, "items.xml"));
			}
			var ret = new List<Token>();
			var gender = owner == null ? Gender.Random : owner.Name.Female ? Gender.Female : Gender.Male;
			switch (type)
			{
				case "wardrobe":
					var costumes = costumeDoc.SelectNodes("costumes/costume").OfType<XmlElement>().ToList();
					var amount = Toolkit.Rand.Next(2, 7);
					for (var i = 0; i < amount; i++)
					{
						XmlElement x = null;
						var carrier = new TokenCarrier();
						Token costume = null;
						var lives = 20;
						while (costume == null && lives > 0)
						{
							lives--;
							x = costumes[Toolkit.Rand.Next(costumes.Count)];
							carrier.Tokens = Token.Tokenize(x.InnerText);
							if (carrier.HasToken("rare") && Toolkit.Rand.NextDouble() > 0.5)
								continue;
							if (gender == Gender.Male && carrier.HasToken("male"))
								costume = carrier.GetToken("male");
							if (gender == Gender.Female && carrier.HasToken("female"))
								costume = carrier.GetToken("female");
						}
						if (carrier.HasToken("singleton"))
							costumes.Remove(x);
						if (costume == null)
							break;
						Toolkit.FoldCostumeRandoms(costume);
						Toolkit.FoldCostumeVariables(costume);
						foreach (var request in costume.Tokens)
						{
							var find = NoxicoGame.KnownItems.Find(item => item.ID == request.Name);
							if (find == null)
								continue;
							request.AddToken("owner", 0, owner.ID);
							ret.Add(request);
						}
					}
					break;
				case "chest":
					//TODO: fill chests.
					break;
				case "cabinet":
					//TODO: fill cabinets.
					break;
				case "dungeontreasure":
					//TODO: allow more special shit to appear in dungeon treasure chests.
					//For now, let's go with weapons.
					var playerItems = NoxicoGame.HostForm.Noxico.Player.Character.GetToken("items");
					var weapons = NoxicoGame.KnownItems.Where(ki => ki.HasToken("weapon") && !playerItems.HasToken(ki.ID)).ToList();
					//We should now have a list of all weapons, excluding the ones carried by the player.
					if (weapons.Count > 0)
					{
						var choice = weapons[Toolkit.Rand.Next(weapons.Count)];
						ret.Add(new Token() { Name = choice.ID });
					}
					//Now add some healing items.
					var healers = NoxicoGame.KnownItems.Where(ki => ki.Path("statbonus/health") != null).ToList();
					amount = Toolkit.Rand.Next(2, 6);
					while (amount > 0)
					{
						var choice = healers[Toolkit.Rand.Next(healers.Count)];
						ret.Add(new Token() { Name = choice.ID });
						amount--;
					}
					break;
			}
			return ret;
		}
	}

	public class ItemException : Exception
	{
		public ItemException(string message)
			: base(message)
		{
		}
	}
}