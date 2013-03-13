using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	public enum Gender
	{
		Random, Male, Female, Herm, Neuter
	}

	public enum MorphReportLevel
	{
		NoReports, PlayerOnly, Anyone
	}

	public enum Stat
	{
		Health, Charisma, Climax, Cunning, Carnality, Stimulation, Sensitivity, Speed, Strength
	}

	public class Character : TokenCarrier
	{
		private static XmlDocument bodyPlansDocument, uniquesDocument;
		public static StringBuilder MorphBuffer = new StringBuilder();

		public Name Name { get; set; }
		public string Species { get; set; }
		public string Title { get; set; }
		public bool IsProperNamed { get; set; }
		public string A { get; set; }
		public Culture Culture { get; set; }

		public float Capacity { get; private set; }
		public float Carried { get; private set; }

		public string ID
		{
			get
			{
				if (HasToken("player"))
					return "\xF4EF" + Name.ToID() + "#" + GetToken("player").Value;
				if (string.IsNullOrEmpty(Name.FirstName))
					return Title.ToID();
				return Name.ToID();
			}
		}

		/// <summary>
		/// Returns the full name of the character with title, or just the title.
		/// </summary>
		public override string ToString()
		{
			if (HasToken("title"))
				return GetToken("title").Text;
			var g = HasToken("invisiblegender") ? "" : GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == "female " && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")))
				g = "";
			if (IsProperNamed)
				return string.Format("{0}, {1} {3}", Name.ToString(true), A, g, Title);
			return string.Format("{0} {2}", A, g, Title);
		}

		/// <summary>
		/// Returns the short name of the character, or the title.
		/// </summary>
		public string GetNameOrTitle(bool fullName = false, bool the = false, bool initialCaps = false)
		{
			if (HasToken("title"))
				return GetToken("title").Text;
			if (HasToken("beast"))
				return string.Format("{0} {1}", initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A), Path("terms/generic").Text);
			var g = HasToken("invisiblegender") ? "" : GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || HasToken("malename"))) ||
				(g == "female " && (HasToken("femaleonly") || HasToken("femalename"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")))
				g = "";
			if (IsProperNamed)
				return Name.ToString(fullName);
			return string.Format("{0} {1}{2}", initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A), g, Species);
		}

		/// <summary>
		/// Returns the character's title.
		/// </summary>
		public string GetTheTitle()
		{
			if (HasToken("title"))
				return GetToken("title").Text;
			return string.Format("{0} {1}", A, Title);
		}

		/// <summary>
		/// Returns the character's gender -- male, female, herm, or gender-neutral.
		/// </summary>
		public string GetGender()
		{
			if (HasToken("penis") && HasToken("vagina"))
				return "hermaphrodite";
			else if (HasToken("penis"))
				return "male";
			else if (HasToken("vagina"))
				return "female";
			return "gender-neutral";
		}

		public Gender GetGenderEnum()
		{
			if (HasToken("penis") && HasToken("vagina"))
				return Gender.Herm;
			else if (HasToken("penis"))
				return Gender.Male;
			else if (HasToken("vagina"))
				return Gender.Female;
			return Gender.Neuter;
		}

		public void UpdateTitle()
		{
			var g = GetGender();
			Title = GetToken("terms").GetToken("generic").Text;
			if (HasToken("prefixes"))
			{
				foreach (var prefix in GetToken("prefixes").Tokens)
					Title = prefix.Name + " " + Title;
			}
			if (HasToken("invisiblegender"))
			{
				if (g == "male" && GetToken("terms").HasToken("male"))
					Title = GetToken("terms").GetToken("male").Text;
				else if (g == "female" && GetToken("terms").HasToken("female"))
					Title = GetToken("terms").GetToken("female").Text;
				else if (g == "hermaphrodite" && GetToken("terms").HasToken("herm"))
					Title = GetToken("terms").GetToken("herm").Text;
			}
			else if (HasToken("explicitgender"))
			{
				Title = g + " " + Title;
			}
			if (A == "a" && Title.StartsWithVowel())
				A = "an";
			else if (A == "an" && !Title.StartsWithVowel())
				A = "a";
		}

		public string HeSheIt(bool lower = false)
		{
			if (HasToken("penis") && HasToken("vagina"))
				return lower ? "shi" : "Shi";
			else if (HasToken("penis"))
				return lower ? "he" : "He";
			else if (HasToken("vagina"))
				return lower ? "she" : "She";
			return lower ? "it" : "It";
		}

		public string HisHerIts(bool lower = false)
		{
			if (HasToken("penis") && HasToken("vagina"))
				return lower ? "hir" : "Hir";
			else if (HasToken("penis"))
				return lower ? "his" : "His";
			else if (HasToken("vagina"))
				return lower ? "her" : "Her";
			return lower ? "its" : "Its";
		}

		public string HimHerIt()
		{
			if (HasToken("penis") && HasToken("vagina"))
				return "hir";
			else if (HasToken("penis"))
				return "him";
			else if (HasToken("vagina"))
				return "her";
			return "it";
		}

		public float GetMaximumHealth()
		{
			return GetToken("strength").Value * 2 + 50 + (HasToken("healthbonus") ? GetToken("healthbonus").Value : 0);
		}

		public Character()
		{
		}

		public static Character GetUnique(string id)
		{
			if (uniquesDocument == null)
				uniquesDocument = Mix.GetXMLDocument("uniques.xml");

			var newChar = new Character();
			var planSource = uniquesDocument.SelectSingleNode("//uniques/character[@id=\"" + id + "\"]") as XmlElement;
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokenize(plan);
			newChar.Name = new Name(planSource.GetAttribute("name"));
			newChar.A = "a";
			newChar.IsProperNamed = planSource.HasAttribute("proper");

			var prefabTokens = new[]
			{
				"items", "health", "perks", "skills",
				"charisma", "climax", "cunning", "carnality",
				"stimulation", "sensitivity", "speed", "strength",
				"money", "ships", "paragon", "renegade", "satiation",
			};
			var prefabTokenValues = new[]
			{
				0, 10, 0, 0,
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0, 50,
			};
			for (var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.AddToken(prefabTokens[i], prefabTokenValues[i]);
			if (!newChar.HasToken("culture"))
				newChar.AddToken("culture", 0, Culture.DefaultCulture.ID);
			newChar.GetToken("health").Value = newChar.GetMaximumHealth();

			var gender = Gender.Neuter;
			if (newChar.HasToken("penis") && !newChar.HasToken("vagina"))
				gender = Gender.Male;
			else if (!newChar.HasToken("penis") && newChar.HasToken("vagina"))
				gender = Gender.Female;
			else if (newChar.HasToken("penis") && newChar.HasToken("vagina"))
				gender = Gender.Herm;
			if (gender == Gender.Female)
				newChar.Name.Female = true;
			else if (gender == Gender.Herm || gender == Gender.Neuter)
				newChar.Name.Female = Random.NextDouble() > 0.5;

			var terms = newChar.GetToken("terms");
			newChar.Species = gender.ToString() + " " + terms.GetToken("generic").Text;
			if (gender == Gender.Male && terms.HasToken("male"))
				newChar.Species = terms.GetToken("male").Text;
			else if (gender == Gender.Female && terms.HasToken("female"))
				newChar.Species = terms.GetToken("female").Text;
			else if (gender == Gender.Herm && terms.HasToken("herm"))
				newChar.Species = terms.GetToken("herm").Text;

			newChar.ApplyCostume();
			newChar.UpdateTitle();
			newChar.StripInvalidItems();

			newChar.Culture = Culture.DefaultCulture;
			if (newChar.HasToken("culture"))
			{
				var culture = newChar.GetToken("culture").Text;
				if (Culture.Cultures.ContainsKey(culture))
					newChar.Culture = Culture.Cultures[culture];
			}

			newChar.RemoveMetaTokens();

			Console.WriteLine("Retrieved unique character {0}.", newChar);
			return newChar;
		}

		public static Character Generate(string bodyPlan, Gender gender)
		{
			if (bodyPlansDocument == null)
				bodyPlansDocument = Mix.GetXMLDocument("bodyplans.xml");

			var newChar = new Character();
			var planSource = bodyPlansDocument.SelectSingleNode("//bodyplans/bodyplan[@id=\"" + bodyPlan + "\"]") as XmlElement;
			//gToolkit.VerifyBodyplan(planSource); //by PillowShout
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokenize(plan);
			newChar.Name = new Name();
			newChar.A = "a";

			newChar.HandleSelectTokens(); //by PillowShout

			if (newChar.HasToken("femaleonly"))
				gender = Gender.Female;
			else if (newChar.HasToken("maleonly"))
				gender = Gender.Male;
			else if (newChar.HasToken("hermonly"))
				gender = Gender.Herm;
			else if (newChar.HasToken("neuteronly"))
				gender = Gender.Neuter;

			if (gender == Gender.Random)
			{
				var min = 1;
				var max = 4;
				if (newChar.HasToken("normalgenders"))
					max = 2;
				else if (newChar.HasToken("neverneuter"))
					max = 3;
				var g = Random.Next(min, max + 1);
				gender = (Gender)g;
			}

			if (gender != Gender.Female && newChar.HasToken("femaleonly"))
				throw new Exception(string.Format("Cannot generate a non-female {0}.", bodyPlan));
			if (gender != Gender.Male && newChar.HasToken("maleonly"))
				throw new Exception(string.Format("Cannot generate a non-male {0}.", bodyPlan));

			if (gender == Gender.Male || gender == Gender.Neuter)
			{
				newChar.RemoveToken("fertility");
				newChar.RemoveToken("milksource");
				newChar.RemoveToken("vagina");
				if (newChar.HasToken("breastrow"))
					newChar.GetToken("breastrow").GetToken("size").Value = 0f;
			}
			else if (gender == Gender.Female || gender == Gender.Neuter)
			{
				newChar.RemoveToken("penis");
				newChar.RemoveToken("balls");
			}

			if (!newChar.HasToken("beast"))
			{
				if (newChar.HasToken("namegen"))
				{
					var namegen = newChar.GetToken("namegen").Text;
					if (Culture.NameGens.Contains(namegen))
						newChar.Name.NameGen = namegen;
				}
				if (gender == Gender.Female)
					newChar.Name.Female = true;
				else if (gender == Gender.Herm || gender == Gender.Neuter)
					newChar.Name.Female = Random.NextDouble() > 0.5;
				newChar.Name.Regenerate();
				var patFather = new Name() { NameGen = newChar.Name.NameGen, Female = false };
				var patMother = new Name() { NameGen = newChar.Name.NameGen, Female = true };
				patFather.Regenerate();
				patMother.Regenerate();
				newChar.Name.ResolvePatronym(patFather, patMother);
				newChar.IsProperNamed = true;
			}

			var terms = newChar.GetToken("terms");
			newChar.Species = gender.ToString() + " " + terms.GetToken("generic").Text;
			if (gender == Gender.Male && terms.HasToken("male"))
				newChar.Species = terms.GetToken("male").Text;
			else if (gender == Gender.Female && terms.HasToken("female"))
				newChar.Species = terms.GetToken("female").Text;
			else if (gender == Gender.Herm && terms.HasToken("herm"))
				newChar.Species = terms.GetToken("herm").Text;

			newChar.UpdateTitle();
			newChar.StripInvalidItems();

			var prefabTokens = new[]
			{
				"items", "health", "perks", "skills",
				"charisma", "climax", "cunning", "carnality",
				"stimulation", "sensitivity", "speed", "strength",
				"money", "ships", "paragon", "renegade", "satiation",
			};
			var prefabTokenValues = new[]
			{
				0, 10, 0, 0,
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0, 50,
			};
			for (var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.AddToken(prefabTokens[i], prefabTokenValues[i]);
			newChar.GetToken("health").Value = newChar.GetMaximumHealth();

			newChar.ApplyCostume();

			newChar.Culture = Culture.DefaultCulture;
			if (newChar.HasToken("culture"))
			{
				var culture = newChar.GetToken("culture").Text;
				if (Culture.Cultures.ContainsKey(culture))
					newChar.Culture = Culture.Cultures[culture];
			}

			if (newChar.HasToken("beast") && !newChar.HasToken("neverprefix") && Random.NextDouble() > 0.5)
			{
				var prefixes = new[] { "vorpal", "poisonous", "infectious", "dire", "underfed" };
				var chosen = prefixes[Random.Next(prefixes.Length)];
				if (!newChar.HasToken("infectswith"))
					while (chosen == "infectious")
						chosen = prefixes[Random.Next(prefixes.Length)];
				newChar.UpdateTitle();
			}

			if (newChar.HasToken("femalesmaller"))
			{
				if (gender == Gender.Female)
					newChar.GetToken("tallness").Value -= Random.Next(5, 10);
				else if (gender == Gender.Herm)
					newChar.GetToken("tallness").Value -= Random.Next(1, 6);
			}

			while (newChar.HasToken("either"))
			{
				var either = newChar.GetToken("either");
				var eitherChoice = Random.Next(-1, either.Tokens.Count);
				if (eitherChoice > -1)
					newChar.AddToken(either.Tokens[eitherChoice]);
				newChar.RemoveToken(either);
			}

			newChar.RemoveMetaTokens();

			//Console.WriteLine("Generated {0}.", newChar);
			return newChar;
		}

		private void RemoveMetaTokens()
		{
			var metaTokens = new[] { "playable", "femalesmaller", "costume", "neverneuter", "hermonly", "maleonly", "femaleonly" };
			foreach (var t in metaTokens)
				this.RemoveAll(t);
			if (!this.HasToken("beast"))
				this.RemoveAll("bestiary");

			//While we're there, make sure asses have looseness and such.
			if (this.HasToken("ass"))
			{
				if (this.Path("ass/looseness") == null)
					this.GetToken("ass").AddToken("looseness");
				if (this.Path("ass/wetness") == null)
					this.GetToken("ass").AddToken("wetness");
			}
		}

		public void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "CHAR");
			Name.SaveToFile(stream);
			stream.Write(Species ?? "");
			stream.Write(Title ?? "");
			stream.Write(IsProperNamed);
			stream.Write(A ?? "a");
			stream.Write(Culture.ID);
			Toolkit.SaveExpectation(stream, "TOKS");
			stream.Write(Tokens.Count);
			Tokens.ForEach(x => x.SaveToFile(stream));
		}

		public static Character LoadFromFile(BinaryReader stream)
		{
			var newChar = new Character();
			Toolkit.ExpectFromFile(stream, "CHAR", "character");
			newChar.Name = Name.LoadFromFile(stream);
			newChar.Species = stream.ReadString();
			newChar.Title = stream.ReadString();
			newChar.IsProperNamed = stream.ReadBoolean();
			newChar.A = stream.ReadString();
			var culture = stream.ReadString();
			newChar.Culture = Culture.DefaultCulture;
			if (Culture.Cultures.ContainsKey(culture))
				newChar.Culture = Culture.Cultures[culture];
			Toolkit.ExpectFromFile(stream, "TOKS", "character token tree");
			var numTokens = stream.ReadInt32();
			for (var i = 0; i < numTokens; i++)
				newChar.Tokens.Add(Token.LoadFromFile(stream));
			return newChar;
		}

		public void ApplyCostume()
		{
			if (HasToken("costume"))
				RemoveToken("costume");
			if (HasToken("beast"))
				return;
			var filters = new Dictionary<string, string>();
			filters["gender"] = GetGender();
			filters["board"] = Board.HackishBoardTypeThing;
			filters["culture"] = this.GetToken("culture").Text;
			var inventory = this.GetToken("items");
			var armedOne = false;
			foreach (var item in WorldGen.GetRandomLoot("npc", "underwear", filters))
				inventory.AddToken(item).AddToken("equipped");
			foreach (var item in WorldGen.GetRandomLoot("npc", "clothing", filters))
				inventory.AddToken(item).AddToken("equipped");
			foreach (var item in WorldGen.GetRandomLoot("npc", "accessories", filters))
				inventory.AddToken(item).AddToken("equipped");
			foreach (var item in WorldGen.GetRandomLoot("npc", "arms", filters))
			{
				var arm = inventory.AddToken(item);
				if (!armedOne)
				{
					armedOne = true;
					arm.AddToken("equipped");
				}
			}
			foreach (var item in WorldGen.GetRandomLoot("npc", "food", filters))
				inventory.AddToken(item);

			/*
			if (HasToken("costume"))
			{
				if (itemsDocument == null)
					itemsDocument = Mix.GetXMLDocument("costumes.xml");

				var costumesToken = GetToken("costume");
				var costumeChoices = costumesToken.Tokens;
				var costume = new Token();
				var lives = 10;
				while (costume.Tokens.Count == 0 && lives > 0)
				{
					var pick = costumeChoices[Random.Next(costumeChoices.Count)].Name;
					var xElement = itemsDocument.SelectSingleNode("//costume[@id=\"" + pick + "\"]") as XmlElement;
					if (xElement != null)
					{
						var plan = xElement.ChildNodes[0].Value;
						if (plan == null)
						{
							lives--;
							continue;
						}
						costume.Tokenize(plan);
					}
					lives--;
				}
				if (HasToken("penis") && !HasToken("vagina"))
					costume = costume.GetToken("male");
				else if (!HasToken("penis") && HasToken("vagina"))
					costume = costume.GetToken("female");
				else
				{
					if (costume.HasToken("herm"))
						costume = costume.GetToken("herm");
					else if (costume.HasToken("neuter"))
						costume = costume.GetToken("neuter");
					else
						costume = costume.GetToken(Random.Next(100) > 50 ? "male" : "female");
				}

				if (costume == null)
					return;

				Toolkit.FoldCostumeRandoms(costume);
				Toolkit.FoldCostumeVariables(costume);

				var items = GetToken("items");
				var toEquip = new Dictionary<InventoryItem, Token>();
				foreach (var request in costume.Tokens)
				{
					//make sure it's a real item and actually clothing
					var find = NoxicoGame.KnownItems.Find(x => x.ID == request.Name);
					if (find == null)
						continue;
					if (!find.HasToken("equipable"))
						continue;
					var equipable = find.GetToken("equipable");
					if (!equipable.HasToken("undershirt") && !equipable.HasToken("underpants") && !equipable.HasToken("shirt") && !equipable.HasToken("pants") && !equipable.HasToken("jacket") && !equipable.HasToken("cloak"))
						continue;
					//if (equipable.Tokens.Count > 1 && !equipable.HasToken(costume.Name))
					//	continue;
					//check for pants and lack of suitable lower body, but watch out for bodysuits and dresses because those are fair game!
					//if ((HasToken("legs") && GetToken("legs").HasToken("quadruped")) || HasToken("snaketail") || HasToken("slimeblob"))
					if (HasToken("taur") || HasToken("quadruped") || HasToken("snaketail") || HasToken("slimeblob"))
					{
						if ((equipable.HasToken("underpants") && !equipable.HasToken("undershirt")) ||
							(equipable.HasToken("pants") && !equipable.HasToken("shirt")))
							continue;
					}
					//it's valid -- add it to <items>
					items.Tokens.Add(request);
					toEquip.Add(find, request);
				}

				var dressingOrder = new[] { "underpants", "undershirt", "shirt", "pants", "jacket", "cloak", "ring", "hand" };
				foreach (var order in dressingOrder)
					foreach (var thing in toEquip.Where(x => x.Key.GetToken("equipable").HasToken(order) && !x.Value.HasToken("equipped")))
						thing.Key.Equip(this, thing.Value);
			}
			*/
		}

		public void StripInvalidItems()
		{
			if (!HasToken("items"))
				return;
			var toDelete = new List<Token>();
			foreach (var carriedItem in GetToken("items").Tokens)
			{
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					toDelete.Add(carriedItem);
			}
			if (toDelete.Count > 0)
			{
				Console.WriteLine("Had to remove {0} inventory item(s) from {1}: {2}", toDelete.Count, GetNameOrTitle(), string.Join(", ", toDelete));
				GetToken("items").RemoveSet(toDelete);
			}
		}

		public void AddSet(List<Token> otherSet)
		{
			foreach (var toAdd in otherSet)
			{
				this.AddToken(toAdd.Name, toAdd.Value, toAdd.Text);
				if (toAdd.Tokens.Count > 0)
					this.GetToken(toAdd.Name).AddSet(toAdd.Tokens);
			}
		}

		public void IncreaseSkill(string skill)
		{
			var skills = GetToken("skills");
			if (!skills.HasToken(skill))
				skills.AddToken(skill);

			var s = skills.GetToken(skill);
			var l = (int)s.Value;
			var i = 0.0349 / (1 + (l / 2));
			s.Value += (float)i;
		}

		public float CumAmount
		{
			get
			{
				var ret = 0.0f;
				var size = HasToken("balls") && GetToken("balls").HasToken("size") ? GetToken("balls").GetToken("size").Value + 1 : 1.25f;
				var amount = HasToken("balls") && GetToken("balls").HasToken("amount") ? GetToken("balls").GetToken("amount").Value : 2f;
				var multiplier = HasToken("cummultiplier") ? GetToken("cummultiplier").Value : 1;
				var hours = 1;
				var stimulation = GetToken("stimulation").Value;
				ret = (size * amount * multiplier * 2 * (stimulation + 50) / 10 * (hours + 10) / 24) / 10;
				if (GetToken("perks").HasToken("messyorgasms"))
					ret *= 1.5f;
				return ret;
			}
		}

		private static void Columnize(Action<string> print, int pad, List<string> col1, List<string> col2, string header1, string header2)
		{
			var totalRows = Math.Max(col1.Count, col2.Count);
			print(header1.PadRight(pad) + header2 + "\n");
			for (var i = 0; i < totalRows; i++)
			{
				if (i < col1.Count)
					print(("| " + col1[i]).PadRight(pad));
				else
					print("".PadRight(pad));
				if (i < col2.Count)
					print("| " + col2[i]);
				print("\n");
			}
			print("\n");
		}

		#region LookAt submethods
		private void LookAtEquipment1(Entity pa, Action<string> print, ref List<InventoryItem> carried, ref List<string> worn, ref List<InventoryItem> hands, ref List<InventoryItem> fingers, ref bool breastsVisible, ref bool crotchVisible)
		{
			InventoryItem underpants = null;
			InventoryItem undershirt = null;
			InventoryItem shirt = null;
			InventoryItem pants = null;
			InventoryItem jacket = null;
			InventoryItem cloak = null;
			InventoryItem hat = null;
			InventoryItem goggles = null;
			InventoryItem mask = null;
			InventoryItem neck = null;
			var carriedItems = this.GetToken("items");
			for (var i = 0; i < carriedItems.Tokens.Count; i++)
			{
				var carriedItem = carriedItems.Item(i);
				var foundItem = NoxicoGame.KnownItems.Find(y => y.ID == carriedItem.Name);
				if (foundItem == null)
				{
					print("Can't handle " + carriedItem.Name + ".\n");
					continue;
				}

				if (foundItem.HasToken("equipable") && carriedItem.HasToken("equipped"))
				{
					var eq = foundItem.GetToken("equipable");
					if (eq.HasToken("underpants"))
					{
						underpants = foundItem;
						underpants.tempToken = carriedItem;
					}
					if (eq.HasToken("undershirt"))
					{
						undershirt = foundItem;
						undershirt.tempToken = carriedItem;
					}
					if (eq.HasToken("pants"))
					{
						pants = foundItem;
						pants.tempToken = carriedItem;
					}
					if (eq.HasToken("shirt"))
					{
						shirt = foundItem;
						shirt.tempToken = carriedItem;
					}
					if (eq.HasToken("jacket"))
					{
						jacket = foundItem;
						jacket.tempToken = carriedItem;
					}
					if (eq.HasToken("cloak"))
					{
						cloak = foundItem;
						cloak.tempToken = carriedItem;
					}
					if (eq.HasToken("hat"))
					{
						hat = foundItem;
						hat.tempToken = carriedItem;
					}
					if (eq.HasToken("goggles"))
					{
						goggles = foundItem;
						goggles.tempToken = carriedItem;
					}
					if (eq.HasToken("mask"))
					{
						mask = foundItem;
						mask.tempToken = carriedItem;
					}
					if (eq.HasToken("neck"))
					{
						neck = foundItem;
						neck.tempToken = carriedItem;
					}
					if (eq.HasToken("ring"))
					{
						foundItem.tempToken = carriedItem;
						fingers.Add(foundItem);
					}
					if (eq.HasToken("hand"))
					{
						foundItem.tempToken = carriedItem;
						hands.Add(foundItem);
					}
				}
				else
				{
					carried.Add(foundItem);
				}
			}

			if (hat != null)
				worn.Add(hat.ToLongString(hat.tempToken));
			if (goggles != null)
				worn.Add(goggles.ToLongString(goggles.tempToken));
			if (mask != null)
				worn.Add(mask.ToLongString(mask.tempToken));
			if (neck != null)
				worn.Add(neck.ToLongString(neck.tempToken));
			if (cloak != null)
				worn.Add(cloak.ToLongString(cloak.tempToken));
			if (jacket != null)
				worn.Add(jacket.ToLongString(jacket.tempToken));
			if (shirt != null)
				worn.Add(shirt.ToLongString(shirt.tempToken));
			if (pants != null && pants != shirt)
				worn.Add(pants.ToLongString(pants.tempToken));
			if (!(pa != null && pa is Player))
			{
				if (undershirt != null && (shirt == null || shirt.CanSeeThrough()))
				{
					breastsVisible = undershirt.CanSeeThrough();
					worn.Add(undershirt.ToLongString(undershirt.tempToken));
				}
				else
					breastsVisible = (shirt == null || shirt.CanSeeThrough());
				if (underpants != null && (pants == null || pants.CanSeeThrough()))
				{
					crotchVisible = underpants.CanSeeThrough();
					worn.Add(underpants.ToLongString(underpants.tempToken));
				}
				else
					crotchVisible = (pants == null || pants.CanSeeThrough());
			}
			else
			{
				if (undershirt != null)
					worn.Add(undershirt.ToLongString(undershirt.tempToken));
				if (underpants != null)
					worn.Add(underpants.ToLongString(underpants.tempToken));
				crotchVisible = breastsVisible = true;
			}
		}

		private void LookAtBodyFace(Entity pa, Action<string> print)
		{
			print("General\n-------\n");

			var bodyThings = new List<string>();
			var headThings = new List<string>();

			var legLength = this.GetToken("tallness").Value * 0.53f;
			var goggles = this.GetEquippedItemBySlot("goggles");
			var mask = this.GetEquippedItemBySlot("mask");

			if (this.HasToken("slimeblob"))
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value - legLength) + " tall");
			else
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value) + " tall");
			if (this.HasToken("snaketail"))
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value + legLength) + " long");

			if (this.Path("skin/type").Text == "slime")
				bodyThings.Add(Color.NameColor(this.Path("hair/color").Text) + " slime");
			else
				bodyThings.Add(Color.NameColor(this.Path("skin/color").Text) + " " + this.Path("skin/type").Text);
			if (this.Path("skin/pattern") != null)
				bodyThings.Add(Color.NameColor(this.Path("skin/pattern/color").Text) + " " + this.Path("skin/pattern").Text);

			if (this.HasToken("legs"))
			{
				var lt = this.GetToken("legs").Text;
				var legs = string.IsNullOrWhiteSpace(lt) ? "human" : lt;
				if (legs == "genbeast")
					legs = "digitigrade";
				else if (legs == "stiletto")
					legs = "stiletto-heeled";
				else if (legs == "claws")
					legs = "clawed";
				else if (legs == "insect")
					legs = "downy insectoid";
				bodyThings.Add(legs + " legs");
				if (this.HasToken("quadruped"))
					bodyThings.Add("quadruped");
				else if (this.HasToken("taur"))
					bodyThings.Add("taur");
			}

			if (this.HasToken("wings"))
			{
				var wt = this.GetToken("wings").Text + " wings";
				if (this.Path("wings/small") != null)
					wt = "small " + wt;
				bodyThings.Add(wt);
			}

			if (this.HasToken("tail"))
			{
				var tt = this.GetToken("tail").Text;
				var tail = string.IsNullOrWhiteSpace(tt) ? "genbeast" : tt;
				if (tail == "genbeast")
					tail = "beastly";
				if (tail == "demon")
					tail = "spaded demon";
				if (tail == "bunny")
					bodyThings.Add("bunny poofball");
				else if (tail == "tentacle")
				{
					var tentail = this.Path("tail/tip");
					if (tentail == null || tentail.Text == "tapered")
						bodyThings.Add("tapered tail tentacle");
					else if (tentail.Text == "penis")
						bodyThings.Add("cock-headed tail tentacle");
					else
						bodyThings.Add("tail tentacle");
				}
				else
					bodyThings.Add(tail + " tail");
			}

			//tone


			var faceType = this.GetToken("face").Text;
			if (faceType == "normal")
				faceType = "human";
			else if (faceType == "genbeast")
				faceType = "beastly";
			else if (faceType == "cow")
				faceType = "bovine";
			else if (faceType == "reptile")
				faceType = "reptilian";
			else
				faceType += "like";
			headThings.Add(faceType);

			if (this.HasToken("eyes"))
			{
				var eyes = Color.NameColor(this.GetToken("eyes").Text) + " eyes";
				var eyesHidden = false;
				if (goggles != null && !goggles.CanSeeThrough())
					eyesHidden = true;
				if (this.Path("eyes/glow") != null)
				{
					eyes = "glowing " + eyes;
					eyesHidden = false;
				}
				if (!eyesHidden)
					headThings.Add(eyes);
			}

			if (mask != null && mask.CanSeeThrough())
			{
				var teeth = this.Path("teeth");
				if (teeth != null && !string.IsNullOrWhiteSpace(teeth.Text) && teeth.Text != "normal")
				{
					var teethStyle = "";
					if (teeth.Text == "fangs")
						teethStyle = "fangs";
					else if (teeth.Text == "sharp")
						teethStyle = "sharp teeth";
					else if (teeth.Text == "osmond")
						teethStyle = "fearsome teeth";
					headThings.Add(teethStyle);
				}
				var tongue = this.Path("tongue");
				if (tongue != null && !string.IsNullOrWhiteSpace(tongue.Text) && tongue.Text != "normal")
					headThings.Add(tongue.Text + " tongue");
			}

			var ears = "human";
			if (this.HasToken("ears"))
				ears = this.GetToken("ears").Text;
			if (ears == "frill")
				headThings.Add("head frills");
			else
			{
				if (ears == "genbeast")
					ears = "animal";
				headThings.Add(ears + " ears");
			}

			if (this.HasToken("monoceros"))
				headThings.Add("unicorn horn");

			//femininity slider


			//Columnize it!
			Columnize(print, 34, bodyThings, headThings, "Body", "Head");
		}

		private void LookAtHairHips(Entity pa, Action<string> print)
		{
			//Because Descriptions.Hair() returns length and color + "hair".
			Func<float, string> hairLength = new Func<float, string>(i =>
			{
				if (i == 0)
					return "bald";
				if (i < 1)
					return "trim";
				if (i < 3)
					return "short";
				if (i < 6)
					return "shaggy";
				if (i < 10)
					return "moderately long";
				if (i < 16)
					return "shoulder-length";
				if (i < 26)
					return "very long";
				if (i < 40)
					return "ass-length";
				return "obscenely long";
			});
			//Same for these.
			Func<float, string> hips = new Func<float, string>(i =>
			{
				if (i < 1)
					return "tiny";
				if (i < 4)
					return "slender";
				if (i < 6)
					return "average";
				if (i < 10)
					return "ample";
				if (i < 15)
					return "curvy";
				if (i < 20)
					return "fertile";
				return "cow-like";
			});
			Func<float, string> waist = new Func<float, string>(i =>
			{
				if (i < 1)
					return "emaciated";
				if (i < 4)
					return "thin";
				if (i < 6)
					return "average";
				if (i < 8)
					return "soft";
				if (i < 11)
					return "chubby";
				if (i < 14)
					return "plump";
				if (i < 17)
					return "stout";
				if (i < 20)
					return "obese";
				return "morbid";
			});
			Func<float, string> ass = new Func<float, string>(i =>
			{
				if (i < 1)
					return "insignificant";
				if (i < 4)
					return "firm";
				if (i < 6)
					return "regular";
				if (i < 8)
					return "shapely";
				if (i < 11)
					return "large";
				if (i < 13)
					return "spacious";
				if (i < 16)
					return "voluminous";
				if (i < 20)
					return "huge";
				return "colossal";
			});

			var hairThings = new List<string>();
			var hipThings = new List<string>();
			if (this.HasToken("hair") && this.Path("hair/length").Value > 0)
			{
				hairThings.Add(hairLength(this.Path("hair/length").Value));
				hairThings.Add(Color.NameColor(this.Path("hair/color").Text));
				if (this.Path("hair/style") != null)
					hairThings.Add(this.Path("hair/style").Text);
				if (this.Path("skin/type").Text == "slime")
					hairThings.Add("goopy");
				if (this.Path("skin/type").Text == "rubber")
					hairThings.Add("stringy");
				if (this.Path("skin/type").Text == "metal")
					hairThings.Add("cord-like");
			}
			if (!(HasToken("quadruped") || HasToken("taur")))
			{
				hipThings.Add(hips(this.GetToken("hips").Value) + " hips");
				hipThings.Add(waist(this.GetToken("waist").Value) + " waist");
				hipThings.Add(ass(this.Path("ass/size").Value) + " ass");
			}
			else
			{
				//hipThings.Add("quadruped");
			}
			Columnize(print, 34, hairThings, hipThings, "Hair", "Hips and Waist");
		}

		private void LookAtSexual(Entity pa, Action<string> print, bool breastsVisible, bool crotchVisible)
		{
			print("Sexual characteristics\n----------------------\n");
			var cocks = new List<Token>(); var vaginas = new List<Token>(); var breastRows = new List<Token>();
			Token nuts;
			var ballCount = 0;
			var ballSize = 0.25f;
			//var slit = this.HasToken("snaketail");
			//var aroused = stimulation > 50;

			for (var i = 0; i < this.Tokens.Count; i++)
			{
				var gen = this.Item(i);
				if (gen.Name == "penis")
					cocks.Add(gen);
				else if (gen.Name == "vagina")
					vaginas.Add(gen);
				else if (gen.Name == "breastrow")
					breastRows.Add(gen);
				else if (gen.Name == "balls")
				{
					nuts = gen;
					ballCount = nuts.HasToken("amount") ? (int)nuts.GetToken("amount").Value : 2;
					ballSize = nuts.HasToken("size") ? nuts.GetToken("size").Value : 0.25f;
				}
			}

			Func<float, string> breast = new Func<float, string>(i =>
			{
				if (i == 0)
					return "flat";
				if (i < 0.5)
					return "tiny";
				if (i < 1)
					return "small";
				if (i < 2.5)
					return "fair";
				if (i < 3.5)
					return "appreciable";
				if (i < 4.5)
					return "ample";
				if (i < 6)
					return "pillowy";
				if (i < 7)
					return "large";
				if (i < 8)
					return "ridiculous";
				if (i < 9)
					return "huge";
				if (i < 10)
					return "spacious";
				if (i < 12)
					return "back-breaking";
				if (i < 13)
					return "mountainous";
				if (i < 14)
					return "ludicrous";
				if (i < 15)
					return "exploding";
				return "absurdly huge";
			});

			print("Breasts: ");
			if (breastRows.Count == 0)
				print("none\n");
			else
			{
				print(Toolkit.Count(breastRows.Count) + " row" + (breastRows.Count == 1 ? "" : "s") + "\n");
				for (var i = 0; i < breastRows.Count; i++)
				{
					var row = breastRows[i];
					print("| " + Toolkit.Count(row.GetToken("amount").Value) + " " + breast(/* row.GetToken("size").Value */ GetBreastRowSize(i)) + " breast");
					if (row.GetToken("amount").Value > 1)
						print("s");
					if (!breastsVisible)
					{
						print("\n");
						continue;
					}
					var nipSize = 0.5f;
					if (row.Path("nipples/size") != null)
						nipSize = row.Path("nipples/size").Value;
					var nipType = Descriptions.Length(nipSize) + " " + "nipple";
					if (row.Path("nipples/canfuck") != null)
						nipType = Descriptions.Length(nipSize) + " " + "dicknipple";
					else if (row.Path("nipples/fuckable") != null)
					{
						var loose = Descriptions.Looseness(row.Path("nipples/looseness"), false);
						var wet = Descriptions.Wetness(row.Path("nipples/wetness"));
						if (wet != null && loose != null)
							wet = " and " + wet;
						else if (wet == null && loose == null)
							loose = "";
						nipType = (loose + wet + " nipplecunt").Trim();
					}
					print(", " + Toolkit.Count(row.GetToken("nipples").Value) + " " + nipType);
					if (row.GetToken("nipples").Value > 1)
						print("s");
					print(" on each\n");
				}
			}
			print("\n");

			print("Vaginas: ");
			if (!crotchVisible)
			{
				if (this.GetGender() == "male")
					print("can't tell, none assumed\n");
				else if (this.GetGender() == "female")
					print("can't tell, one assumed\n");
				else
					print("can't tell\n");
			}
			else
			{
				if (vaginas.Count == 0)
					print("none\n");
				else
				{
					print(Toolkit.Count(vaginas.Count) + "\n");
					for (var i = 0; i < vaginas.Count; i++)
					{
						var vagina = vaginas[i];
						if (vagina == null)
							print("OH NO!");
						var loose = Descriptions.Looseness(vagina.GetToken("looseness"), false);
						var wet = Descriptions.Wetness(vagina.GetToken("wetness"));
						if (wet != null && loose != null)
							wet = " and " + wet;
						else if (wet == null && loose == null)
							loose = "regular";
						var clit = vagina.Path("clit");
						var clitSize = 0.25f;
						if (clit != null)
							clitSize = clit.Value;
						print("| " + loose + wet + ", with a " + Descriptions.Length(clitSize) + " clit\n");
					}
				}
			}
			print("\n");


			Func<float, string> balls = new Func<float, string>(i =>
			{
				if (i < 1)
					return "";
				if (i < 2)
					return "large";
				if (i < 3)
					return "baseball-sized";
				if (i < 4)
					return "apple-sized";
				if (i < 5)
					return "grapefruit-sized";
				if (i < 7)
					return "cantaloupe-sized";
				if (i < 9)
					return "soccer ball-sized";
				if (i < 12)
					return "basketball-sized";
				if (i < 15)
					return "watermelon-sized";
				if (i < 18)
					return "beach ball-sized";
				return "hideously swollen";
			});

			print("Cocks: ");
			if (!crotchVisible)
			{
				if (this.GetGender() == "male")
					print("can't tell, one assumed\n");
				else if (this.GetGender() == "female")
					print("can't tell, none assumed\n");
				else
					print("can't tell\n");
			}
			else
			{
				if (cocks.Count == 0)
					print("none\n");
				else
				{
					print(Toolkit.Count(cocks.Count) + "\n");
					for (var i = 0; i < cocks.Count; i++)
					{
						var cock = cocks[i];
						var cockType = cock.Text;
						if (string.IsNullOrWhiteSpace(cockType))
							cockType = "human-like";
						print("| " + cockType + ", " + Descriptions.Length(cock.GetToken("length").Value) + " long, ");
						print(Descriptions.Length(cock.GetToken("thickness").Value) + " thick\n");
					}
				}
				if (ballCount > 0)
				{
					print("| " + Toolkit.Count(ballCount) + " " + (ballSize < 1 ? "" : balls(ballSize) + " ") + "testicles\n");
				}
			}
			print("\n");
		}

		private void LookAtClothing(Entity pa, Action<string> print, List<string> worn)
		{
			print("Clothing\n");
			if (worn.Count == 0)
				print("| none\n");
			else
				for (var i = 0; i < worn.Count; i++)
					print("| " + worn[i] + "\n");
			print("\n");
		}

		private void LookAtEquipment2(Entity pa, Action<string> print, List<InventoryItem> hands, List<InventoryItem> fingers)
		{
			print("Equipment\n");
			var mono = HasToken("monoceros") ? 1 : 0;
			if (this.HasToken("noarms") && hands.Count > 1 + mono)
				print("NOTICE: dual wielding with mouth.\n");
			if (hands.Count > 2 + mono)
				print("NOTICE: Shiva called.\n");
			if (hands.Count + fingers.Count == 0)
				print("| none\n");
			else
			{
				for (var i = 0; i < hands.Count; i++)
					print("| " + hands[i].ToLongString(hands[i].tempToken) + "\n");
				for (var i = 0; i < fingers.Count; i++)
					print("| " + fingers[i].ToLongString(fingers[i].tempToken) + "\n");
			}
			print("\n");
		}
		#endregion

		public string LookAt(Entity pa, Action<string> print = null)
		{
			if (this.HasToken("beast"))
				return this.GetToken("bestiary").Text;

			var sb = new StringBuilder();

			//Things not listed: pregnancy, horns and wings.
			if (print == null)
				print = new Action<string>(x => sb.Append(x));

			//var stimulation = this.GetToken("stimulation").Value;

			print(("Name: " + this.Name.ToString(true)).PadRight(34) + "Type: " + this.Title + ((pa != null && pa is Player) ? " (player)" : "") + "\n");
			print("\n");

			bool breastsVisible = false, crotchVisible = false;
			var carried = new List<InventoryItem>();
			var worn = new List<string>();
			var hands = new List<InventoryItem>();
			var fingers = new List<InventoryItem>();
			LookAtEquipment1(pa, print, ref carried, ref worn, ref hands, ref fingers, ref breastsVisible, ref crotchVisible);
			LookAtBodyFace(pa, print);
			LookAtHairHips(pa, print);
			LookAtClothing(pa, print, worn);
			LookAtEquipment2(pa, print, hands, fingers);
			LookAtSexual(pa, print, breastsVisible, crotchVisible);

			#if DEBUG
			print("\n\n\n\n");
			print("<cGray>Debug\n<cGray>-----\n");
			print("<cGray>GetGender(): " + this.GetGender() + "\n");
			print("<cGray>Cum amount: " + this.CumAmount + "mLs.\n");
			print("<cGray>Biggest breast row: #" + this.BiggestBreastrowNumber + " @ " + this.GetBreastRowSize(this.BiggestBreastrowNumber) + "'\n");
			print("<cGray>Biggest penis (length only): #" + this.GetBiggestPenisNumber(false) + " @ " + this.GetPenisSize(this.GetBiggestPenisNumber(false), false) + "cm\n");
			print("<cGray>Biggest penis (l * t): #" + this.GetBiggestPenisNumber(true) + " @ " + this.GetPenisSize(this.GetBiggestPenisNumber(true), true) + "cm\n");
			print("<cGray>Highest capacity cooch: #" + this.LargestVaginaNumber + " @ " + this.GetVaginaCapacity(this.LargestVaginaNumber) + "\n");
			print("<cGray>Smallest breast row: #" + this.SmallestBreastrowNumber + " @ " + this.GetBreastRowSize(this.SmallestBreastrowNumber) + "'\n");
			print("<cGray>Smallest penis (l * t): #" + this.GetSmallestPenisNumber(true) + " @ " + this.GetPenisSize(this.GetSmallestPenisNumber(true), true) + "cm\n");
			print("<cGray>Lowest capacity cooch: #" + this.SmallestVaginaNumber + " @ " + this.GetVaginaCapacity(this.SmallestVaginaNumber) + "\n");
			#endif

			return sb.ToString();
		}

		public void CreateInfoDump()
		{
			var dump = new StreamWriter(Name + " info.html");
			var list = new List<string>();

			dump.WriteLine("<!DOCTYPE html>");
			dump.WriteLine("<html>");
			dump.WriteLine("<head>");
			dump.WriteLine("<title>Noxico - Infodump for {0}</title>", this.Name.ToString(true));
			dump.WriteLine("<meta http-equiv=\"Content-Type\" content=\"text/html; CHARSET=utf-8\" />");
			dump.WriteLine("</head>");
			dump.WriteLine("<body>");
			dump.WriteLine("<h1>Noxico - Infodump for {0}</h1>", this.Name.ToString(true));

			dump.WriteLine("<h2>Screenshot</h2>");
			NoxicoGame.HostForm.Noxico.CurrentBoard.CreateHTMLDump(dump, false);

			foreach (var carriedItem in GetToken("items").Tokens)
			{
				carriedItem.RemoveToken("equipped");
			}

			dump.WriteLine("<h2>About You</h2>");
			dump.WriteLine("<pre>");
			Action<string> print = new Action<string>(x =>
			{
				if (x.Contains("| none\n") || x.Contains("Clothing\n") || x.Contains("Equipment\n"))
					return;
				dump.Write(x);
			});
			var lookAt = LookAt(null, print);
			lookAt = lookAt.Replace("\n\n", "\n");
			dump.WriteLine(lookAt);
			dump.WriteLine("</pre>");

			dump.WriteLine("<h2>All of your items</h2>");
			dump.WriteLine("<ul>");
			if (GetToken("items").Tokens.Count == 0)
				dump.WriteLine("<li>You were carrying nothing.</li>");
			else
			{
				list.Clear();
				foreach (var carriedItem in GetToken("items").Tokens)
				{
					var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
					if (find == null)
						continue;
					find.RemoveToken("unidentified");
					find.RemoveToken("cursed");
					list.Add(find.ToString(carriedItem));
				}
				list.Sort();
				list.ForEach(x => dump.WriteLine("<li>{0}</li>", x));
			}
			dump.WriteLine("</ul>");

			dump.WriteLine("<h2>Relationships</h2>");
			dump.WriteLine("<ul>");
			var victims = 0;
			var lovers = 0;
			var deities = 0;
			if (GetToken("ships").Tokens.Count == 0)
				dump.WriteLine("<li>You were in no relationships.</li>");
			else
			{
				list.Clear();
				foreach (var person in GetToken("ships").Tokens)
				{
					var reparsed = person.Name.Replace('_', ' ');
					if (reparsed.StartsWith("\xF4EF"))
						reparsed = reparsed.Remove(reparsed.IndexOf('#')).Substring(1);
					list.Add(reparsed + " &mdash; " + string.Join(", ", person.Tokens.Select(x => x.Name)));
					if (person.HasToken("victim"))
						victims++;
					if (person.HasToken("lover"))
						lovers++;
					if (person.HasToken("prayer"))
						deities++;
				}
				list.Sort();
				list.ForEach(x => dump.WriteLine("<li>{0}</li>", x));
			}
			dump.WriteLine("</ul>");

			dump.WriteLine("<h2>Conduct</h2>");
			dump.WriteLine("<ul>");
			dump.WriteLine(HasToken("books") ? "<li>You were literate.</li>" : "<li>You were functionally illiterate.</li>");
			dump.WriteLine(lovers > 0 ? "<li>You were someone's lover.</li>" : "<li>You had no love to give.</li>");
			if (lovers == 1)
				dump.WriteLine("<li>You were faithful.</li>");
			if (victims > 0)
				dump.WriteLine(victims == 1 ? "<li>You had raped someone.</li>" : "<li>You had raped several people.</li>");
			if (deities == 0)
				dump.WriteLine("<li>You were an atheist.</li>");
			else
				dump.WriteLine(deities == 1 ? "<li>You were monotheistic.</li>" : "<li>You were a polytheist.</li>");
			dump.WriteLine("</ul>");

			dump.WriteLine("<h2>Books you've read</h2>");
			dump.WriteLine("<ul>");
			if (HasToken("books"))
			{
				foreach (var book in GetToken("books").Tokens)
					dump.WriteLine("<li>&ldquo;{0}&rdquo;</li>", book.Text);
			}
			else
				dump.WriteLine("<li>You did not read any books.</li>");
			dump.WriteLine("</ul>");

			dump.Flush();
			dump.Close();

			System.Diagnostics.Process.Start(Name + " info.html");
		}

		public bool HasPenis()
		{
			return HasToken("penis");
		}

		public bool HasVagina()
		{
			return HasToken("vagina");
		}

		public void CheckPants(MorphReportLevel reportLevel = MorphReportLevel.PlayerOnly, bool reportAsMessages = false)
		{
			var doReport = new Action<string>(s =>
			{
				if (reportLevel == MorphReportLevel.NoReports)
					return;
				if (reportLevel == MorphReportLevel.PlayerOnly && this != NoxicoGame.HostForm.Noxico.Player.Character)
					return;
				if (reportAsMessages)
					NoxicoGame.AddMessage(s);
				else
					Character.MorphBuffer.Append(s + ' ');
			});

			if (!HasToken("slimeblob") && !HasToken("snaketail") && !HasToken("quadruped") && !HasToken("taur"))
				return;
			var items = GetToken("items");
			foreach (var carriedItem in items.Tokens)
			{
				if (!carriedItem.HasToken("equipped"))
					continue;
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				var equip = find.GetToken("equipable");
				if (equip != null && (equip.HasToken("pants") || equip.HasToken("underpants")))
				{
					var originalname = find.ToString(carriedItem, false, false);
					if (HasToken("quadruped") || HasToken("taur"))
					{
						//Rip it apart!
						var slot = "pants";
						if (equip.HasToken("pants") && equip.HasToken("shirt"))
							slot = "over";
						else if (equip.HasToken("underpants"))
						{
							slot = "underpants";
							if (equip.HasToken("undershirt"))
								slot = "under";
						}
						carriedItem.Name = "tatteredshreds_" + slot;
						carriedItem.Tokens.Clear();

						if (this == NoxicoGame.HostForm.Noxico.Player.Character)
							doReport("You have torn out of your " + originalname + "!");
						else
							doReport(this.Name.ToString() + " has torn out of " + HisHerIts(true) + " " + originalname + ".");
					}
					else
					{
						if (this == NoxicoGame.HostForm.Noxico.Player.Character)
							doReport("You slip out of your " + originalname + ".");
						//else
						//mention it for others? It's hardly as... "radical" as tearing it up.
					}
				}
			}
		}

		public void Morph(string targetPlan, MorphReportLevel reportLevel = MorphReportLevel.PlayerOnly, bool reportAsMessages = false, int continueChance = 0)
		{
			if (bodyPlansDocument == null)
				bodyPlansDocument = Mix.GetXMLDocument("bodyplans.xml");

			var isPlayer = this == NoxicoGame.HostForm.Noxico.Player.Character;

			var doReport = new Action<string>(s =>
			{
				if (reportLevel == MorphReportLevel.NoReports)
					return;
				if (reportLevel == MorphReportLevel.PlayerOnly && !isPlayer)
					return;
				if (reportAsMessages)
					NoxicoGame.AddMessage(s);
				else
					Character.MorphBuffer.Append(s + ' ');
			});
			Action finish = () =>
			{
				var s = Character.MorphBuffer.ToString();
				if (string.IsNullOrWhiteSpace(s))
					return;
				var bc = NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => x.Character == this);
				if (bc != null)
					bc.AdjustView();
				var paused = true;
				MessageBox.ScriptPauseHandler = () =>
				{
					paused = false;
				};
				MessageBox.Notice(s, true);
				while (paused)
				{
					NoxicoGame.HostForm.Noxico.Update();
					System.Windows.Forms.Application.DoEvents();
				}
				Character.MorphBuffer.Clear();
			};

			var planSource = bodyPlansDocument.SelectSingleNode("//bodyplans/bodyplan[@id=\"" + targetPlan + "\"]") as XmlElement;
			if (planSource == null)
				throw new Exception(string.Format("Unknown target bodyplan \"{0}\".", targetPlan));
			var plan = planSource.ChildNodes[0].Value;
			var target = new TokenCarrier();
			target.Tokenize(plan);
			var source = this;

			var toChange = new List<Token>();
			var changeTo = new List<Token>();
			var report = new List<string>();
			var doNext = new List<bool>();

			//Remove it later if there's none.
			if (!source.HasToken("tail"))
				source.AddToken("tail");
			if (!target.HasToken("tail"))
				target.AddToken("tail");

			//Change tail type?
			if (target.GetToken("tail").Text != source.GetToken("tail").Text)
			{
				toChange.Add(source.Path("tail"));
				changeTo.Add(target.Path("tail"));
				doNext.Add(false);

				if (string.IsNullOrWhiteSpace(source.Path("tail").Text))
					report.Add((isPlayer ? "You have" : this.Name + " has") + " sprouted a " + Descriptions.Tail(target.Path("tail")) + ".");
				else
					report.Add((isPlayer ? "Your" : this.Name + "'s") + " tail has become a " + Descriptions.Tail(target.Path("tail")) + ".");
			}

			//Change entire skin type?
			if (target.Path("skin/type").Text != source.Path("skin/type").Text)
			{
				toChange.Add(source.Path("skin/type"));
				changeTo.Add(target.Path("skin/type"));
				doNext.Add(false);

				switch (target.Path("skin/type").Text)
				{
					case "skin":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Color.NameColor(target.Path("skin/color").Text) + " skin all over.");
						break;
					case "fur":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Color.NameColor(target.Path("skin/color").Text) + " fur all over.");
						break;
					case "rubber":
						report.Add((isPlayer ? "Your" : this.Name + "'s") + " skin has turned into " + Color.NameColor(target.Path("skin/color").Text) + " rubber.");
						break;
					case "scales":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown thick " + Color.NameColor(target.Path("skin/color").Text) + " scales.");
						break;
					case "slime":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " turned to " + Color.NameColor(target.Path("hair/color").Text) + " slime.");
						break;
					default:
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Color.NameColor(target.Path("skin/color").Text) + ' ' + target.Path("skin/type").Text + '.');
						break;
				}
			}

			var fooIneReports = new Dictionary<string, string>()
			{
				{ "normal", "human-like" },
				{ "genbeast", "beastly" },
				{ "horse", "equine" },
				{ "dog", "canine" },
				{ "cow", "bovine" },
				{ "cat", "feline" },
				{ "reptile", "reptilian" },
			};

			//Change facial structure?
			if (target.GetToken("face").Text != source.GetToken("face").Text)
			{
				toChange.Add(source.Path("face"));
				changeTo.Add(target.Path("face"));
				doNext.Add(false);

				var faceDescription = fooIneReports.ContainsKey(target.GetToken("face").Text) ? fooIneReports[target.GetToken("face").Text] : target.GetToken("face").Text;
				report.Add((isPlayer ? "Your" : this.Name + "'s") + " face has rearranged to a more " + faceDescription + " form.");
			}

			//Nothing less drastic to change? Great.
			if (toChange.Count == 0)
			{
				//TODO: handle wings, horns

				foreach (var lowerBody in new[] { "slimeblob", "snaketail" })
				{
					if (target.HasToken(lowerBody) && !source.HasToken(lowerBody))
					{
						doReport((isPlayer ? "Your" : this.Name + "'s") + " lower body has become " + (lowerBody == "slimeblob" ? "a mass of goop." : "a long, scaly snake tail"));
						RemoveToken("legs");
						AddToken(lowerBody);
						CheckPants();
						return;
					}
				}

				if (target.HasToken("legs"))
				{
					if (!source.HasToken("legs"))
						source.AddToken("legs");
					if (string.IsNullOrWhiteSpace(source.GetToken("legs").Text))
						source.GetToken("legs").Text = "human";

					if (source.GetToken("legs").Text != target.GetToken("legs").Text)
					{
						var legDescription = fooIneReports.ContainsKey(target.GetToken("legs").Text) ? fooIneReports[target.GetToken("legs").Text] : target.GetToken("legs").Text;
						doReport((isPlayer ? "You have" : this.Name + " has") + " grown " + legDescription + " legs.");
						source.GetToken("legs").Text  = target.GetToken("legs").Text;
					}
				}

				foreach (var taurQuad in new[] { "taur", "quadruped" })
				{
					if (target.HasToken(taurQuad) && !source.HasToken(taurQuad))
					{
						AddToken(taurQuad);
						CheckPants();
						if (target.HasToken("marshmallow") && !source.HasToken("marshmallow"))
							source.AddToken("marshmallow");
						doReport((isPlayer ? "You are" : this.Name + " is") + " now a " + taurQuad + ".");
						CheckPants();
						finish();
						return;
					}
					else if (!target.HasToken(taurQuad))
					{
						RemoveToken(taurQuad);
					}
				}

				{
					doReport("There was no further effect.");
					finish();
					return;
				}
			}

			//Nothing to do?
			if (toChange.Count == 0)
			{
				doReport("There was no further effect.");
				finish();
				return;
			}

			var choice = Random.Next(toChange.Count);
			var changeThis = toChange[choice];
			var toThis = changeTo[choice];
			doReport(report[choice]);

			#region Slime TO
			//Handle changing skin type to and from slime
			//TODO: make this handle any skin type transition?
			if (toThis.Name == "skin" && toThis.Path("type").Text == "slime")
			{
				//To slime
				var origName = Path("skin/type").Text;
				var originalSkin = Path("originalskins/" + origName);
				if (originalSkin == null)
				{
					AddToken("originalskins");
					originalSkin = GetToken("originalskins").AddToken(origName);
				}
				originalSkin.RemoveToken("color");
				originalSkin.RemoveToken("hair");
				originalSkin.AddToken("color", 0, Path("skin/color").Text);
				if (Path("hair") != null)
					originalSkin.AddToken("hair", 0, Path("hair/color").Text);
	
				//Now that that's done, grab a new hair color while you're there.
				var newHair = target.Path("hair/color").Text;
				Path("hair/color").Text = newHair;
			}
			#endregion

			changeThis.Text = toThis.Text;

			//CheckHands();
			CheckPants();

			#region Slime FROM
			if (toThis.Name == "skin" && this.HasToken("originalskins") && toThis.Path("type/slime") == null)
			{
				//From slime -- restore the original color for the target skin type if available.
				var thisSkin = Path("skin");
				//See if we have this skin type memorized
				var memorized = Path("originalskins/" + thisSkin.GetToken("type").Text);
				if (memorized != null)
				{
					if (!thisSkin.HasToken("color"))
						thisSkin.AddToken("color");
					Path("skin/color").Text = memorized.Text;
				}
				//Grab the hair too?
				if (Path("hair/color") != null && memorized.HasToken("hair"))
					Path("hair/color").Text = memorized.GetToken("hair").Text;
			}
			#endregion

			//remove any dummy tails
			if (string.IsNullOrWhiteSpace(source.Path("tail").Text))
				source.RemoveToken("tail");

			if (doNext[choice] || Random.Next(100) < continueChance)
			{
				Morph(targetPlan);
			}

			finish();
		}
		public void Morph(string targetPlan)
		{
			Morph(targetPlan, MorphReportLevel.PlayerOnly, false, 0);
		}

		public InventoryItem CanShoot()
		{
			foreach (var carriedItem in this.GetToken("items").Tokens)
			{
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					continue;
				if (find.HasToken("equipable") && find.HasToken("weapon") && carriedItem.HasToken("equipped"))
				{
					var eq = find.GetToken("equipable");
					var weap = find.GetToken("weapon");
					if (eq.HasToken("hand"))
					{
						//It's a wielded weapon at least. Now see if it's throwable or a firearm.
						var skill = weap.GetToken("skill");
						if (skill == null)
						{
							//No skill to determine weapon type by? Assume it's not something you'd throw.
							return null;
						}
						if (new[] { "throwing", "small_firearm", "large_firearm", "huge_firearm" }.Contains(skill.Text))
						{
							if (weap.HasToken("ammo"))
							{
								var ammoName = weap.GetToken("ammo").Text;
								var carriedAmmo = this.GetToken("items").Tokens.Find(ci => ci.Name == ammoName);
								if (carriedAmmo == null)
									return null;
								return find;
							}
							else
							{
								return find;
							}
						}
						return null;
					}
				}
			}
			return null;
		}

		public void SetRelation(Character target, string ship, bool mutual = false)
		{
			var shipToken = this.Path("ships/" + target.ID);
			if (shipToken == null)
			{
				shipToken = new Token(target.ID);
				this.Path("ships").Tokens.Add(shipToken);
				shipToken.AddToken(ship);
			}
			else
				shipToken.Tokens[0].Name = ship;
			if (mutual)
				target.SetRelation(this, ship, false);
		}

		public void RecalculateStatBonuses()
		{
			var statNames = Enum.GetNames(typeof(Stat)).Select(s => s.ToLower());
			foreach (var stat in statNames.Select(s => s + "bonus"))
				if (HasToken(stat))
					RemoveToken(stat);
			var bonuses = new Dictionary<string, float>();
			foreach (var stat in statNames)
				bonuses.Add(stat, 0);
			foreach (var carriedItem in GetToken("items").Tokens.Where(t => t.HasToken("equipped")))
			{
				var knownItem = NoxicoGame.KnownItems.FirstOrDefault(ki => ki.ID == carriedItem.Name);
				if (knownItem == null)
					continue;
				var sB = knownItem.Path("statbonus");
				if (sB == null)
					continue;
				foreach (var stat in sB.Tokens)
				{
					if (!bonuses.ContainsKey(stat.Name))
						continue;
					bonuses[stat.Name] += stat.Value;
				}
			}
			foreach (var stat in bonuses)
				AddToken(stat.Key + "bonus", stat.Value, string.Empty);
		}

		public float GetStat(Stat stat)
		{
			var statName = stat.ToString().ToLower();
			var statBonusName = statName + "bonus";
			if (!HasToken(statBonusName))
				RecalculateStatBonuses();
			var statBase = GetToken(statName);
			var statBonus = GetToken(statBonusName);
			return statBase.Value + statBonus.Value;
		}

		public int GetSkillLevel(string skillName)
		{
			var skillToken = this.Path("skills/" + skillName);
			if (skillToken == null)
				return 0;
			return (int)skillToken.Value;
		}

		public BoardChar GetBoardChar()
		{
			//Assume that our board is still in memory.
			foreach (var board in NoxicoGame.HostForm.Noxico.Boards.Where(x => x != null))
			{
				var me = board.Entities.OfType<BoardChar>().FirstOrDefault(x => x.Character == this);
				if (me != null)
					return me;
			}
			return null;
		}

		public Character Spouse
		{
			get
			{
				//Assume that our spouse is on the same board.
				foreach (var ship in GetToken("ships").Tokens)
				{
					if (ship.HasToken("spouse") || ship.HasToken("friend"))
					{
						var me = GetBoardChar();
						var them = me.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => x.Character.ID == ship.Name);
						if (them != null)
							return them.Character;
					}
				}
				return null;
			}
		}

		public void CheckHasteSlow()
		{
			var score = 0;
			this.RemoveToken("slow");
			this.RemoveToken("haste");

			//inherent
			if (this.Path("inherent/slow") != null)
				score--;
			else if (this.Path("inherent/haste") != null)
				score++;

			//item weight
			var weightClasses = new Dictionary<string, float>()
			{
				{ "feather", 0.1f },
 				{ "light", 1 },
				{ "medium", 2 },
				{ "heavy", 4 },
				{ "immense", 16 },
			};
			var strengthToCapacity = new Dictionary<int, int>()
			{
				{ 0, 1 }, //Boneless Chicken
				{ 10, 4 }, //Picked Last at P.E.
				{ 20, 8 }, //Average Joe
				{ 40, 16 }, //Heavy Delivery
				{ 60, 32 }, //Bench Press a Bunch
				{ 80, 56 }, //Olympic God
				{ 100, 64 }, //Demigod
			};
			var strength = GetStat(Stat.Strength);
			var capacity = 0;
			foreach (var s2c in strengthToCapacity)
			{
				capacity = s2c.Value;
				if (s2c.Key > strength)
					break;
			}
			Capacity = capacity;
			var totalWeight = 0f;
			foreach (var carriedItem in this.GetToken("items").Tokens)
			{
				var itemWeight = 1.0f; //assume Light
				var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
				if (knownItem != null && knownItem.HasToken("weight"))
				{
					if (!knownItem.HasToken("weapon") && carriedItem.HasToken("equipped"))
						continue;
					var weightToken = knownItem.GetToken("weight");
					if (string.IsNullOrWhiteSpace(weightToken.Text))
						itemWeight = weightToken.Value;
					else if (weightClasses.ContainsKey(weightToken.Text))
						itemWeight = weightClasses[weightToken.Text];
				}
				totalWeight += itemWeight;
			}
			Carried = totalWeight;
			if (totalWeight >= capacity - 1)
				score--;
			//TODO: if (totalWeight > capacity) become immobile

			//body weight

			//equips
			foreach (var carriedItem in this.GetToken("items").Tokens.Where(ci => ci.HasToken("equipped")))
			{
				var knownItem = NoxicoGame.KnownItems.Find(ki => ki.ID == carriedItem.Name);
				if (knownItem == null)
					continue;
				if (knownItem.Path("equipable/slow") != null)
					score--;
				if (knownItem.Path("equipable/haste") != null)
					score++;
			}

			if (this.GetToken("satiation").Value < 20)
				score--;

			//apply
			if (score < 0)
				this.AddToken("slow");
			else if (score > 0)
				this.AddToken("haste");

		}

		public void GiveRenegadePoints(int points)
		{
			var renegade = this.GetToken("renegade");
			renegade.Value += points;
			if (renegade.Value > 100)
				renegade.Value = 100;
		}

		public void GiveParagonPoints(int points)
		{
			var paragon = this.GetToken("paragon");
			paragon.Value += points;
			if (paragon.Value > 100)
				paragon.Value = 100;
		}

		public void SetTerms(string generic, string male, string female, string herm)
		{
			var g = this.Path("terms/generic");
			var m = this.Path("terms/male");
			var f = this.Path("terms/female");
			var h = this.Path("terms/herm");
			if (g == null)
				g = this.GetToken("terms").AddToken("generic");
			if (m == null)
				m = this.GetToken("terms").AddToken("male");
			if (f == null)
				f = this.GetToken("terms").AddToken("female");
			if (h == null)
				h = this.GetToken("terms").AddToken("herm");
			g.Text = generic;
			m.Text = male;
			f.Text = female;
			h.Text = herm;
		}

		public void EnsureColor(Token colorToken, string options)
		{
			var o = options.Split(',').Select(x => x.Trim()).ToArray();
			if (!o.Contains(colorToken.Text))
				colorToken.Text = Toolkit.PickOne(o);
		}



		public float[] GetBreastSizes()
		{
			var rows = this.Tokens.FindAll(x => x.Name == "breastrow").ToArray();
			var sizes = new float[rows.Length];
			if (rows.Length == 0)
				return sizes;
			var fromPrevious = false;
			var multiplier = 1f;
			sizes[0] = rows[0].GetToken("size").Value;
			for (var i = 1; i < rows.Length; i++)
			{
				if (rows[i].HasToken("size"))
				{
					fromPrevious = false;
					sizes[i] = rows[i].GetToken("size").Value;
				}
				else if (rows[i].HasToken("sizefromprevious") || fromPrevious)
				{
					fromPrevious = true;
					if (rows[i].HasToken("sizefromprevious"))
					{
						multiplier = rows[i].GetToken("sizefromprevious").Value;
						if (multiplier == 0f)
							multiplier = 1f;
					}
					sizes[i] = sizes[i - 1] * multiplier;
				}
			}
			return sizes;
		}

		public float GetBreastRowSize(Token row)
		{
			var sizes = GetBreastSizes();
			var rows = this.Tokens.FindAll(x => x.Name == "breastrow").ToArray();
			for (var i = 0; i < rows.Length; i++)
			{
				if (rows[i] == row)
					return sizes[i];
			}
			return -1f;
		}

		public float GetBreastRowSize(int row)
		{
			var sizes = GetBreastSizes();
			if (row >= sizes.Length || row < 0)
				return -1f;
			return sizes[row];
		}

		public Token GetBreastRowByNumber(int row)
		{
			var rows = this.Tokens.FindAll(x => x.Name == "breastrow").ToArray();
			if (row >= rows.Length || row < 0)
				return null;
			return rows[row];
		}

		public int BiggestBreastrowNumber
		{
			get
			{
				var sizes = GetBreastSizes();
				if (sizes.Length == 0)
					return -1;
				var biggest = -1f;
				var ret = -1;
				for (var i = 0; i < sizes.Length; i++)
				{
					if (sizes[i] > biggest)
					{
						biggest = sizes[i];
						ret = i;
					}
				}
				return ret;
			}
		}

		public int SmallestBreastrowNumber
		{
			get
			{
				var sizes = GetBreastSizes();
				if (sizes.Length == 0)
					return -1;
				var smallest = 99999f;
				var ret = -1;
				for (var i = 0; i < sizes.Length; i++)
				{
					if (sizes[i] < smallest)
					{
						smallest = sizes[i];
						ret = i;
					}
				}
				return ret;
			}
		}

		public float GetPenisSize(Token penis, bool withThickness)
		{
			var ret =  penis.GetToken("length").Value;
			if (withThickness)
				ret *= penis.GetToken("thickness").Value;
			return ret;
		}

		public float[] GetPenisSizes(bool withThickness)
		{
			var cocks = this.Tokens.FindAll(x => x.Name == "penis").ToArray();
			var sizes = new float[cocks.Length];
			for (var i = 0; i < cocks.Length; i++)
			{
				sizes[i] = GetPenisSize(cocks[i], withThickness);
			}
			return sizes;
		}

		public float GetPenisSize(int penis, bool withThickness)
		{
			var sizes = GetPenisSizes(withThickness);
			if (penis >= sizes.Length || penis < 0)
				return -1f;
			return sizes[penis];
		}

		public Token GetPenisByNumber(int penis)
		{
			var cocks = this.Tokens.FindAll(x => x.Name == "penis").ToArray();
			if (penis >= cocks.Length || penis < 0)
				return null;
			return cocks[penis];
		}

		public int GetBiggestPenisNumber(bool withThickness)
		{
			var sizes = GetPenisSizes(withThickness);
			if (sizes.Length == 0)
				return -1;
			var biggest = -1f;
			var ret = -1;
			for (var i = 0; i < sizes.Length; i++)
			{
				if (sizes[i] > biggest)
				{
					biggest = sizes[i];
					ret = i;
				}
			}
			return ret;
		}

		public int GetSmallestPenisNumber(bool withThickness)
		{
			var sizes = GetPenisSizes(withThickness);
			if (sizes.Length == 0)
				return -1;
			var smallest = 99999f;
			var ret = -1;
			for (var i = 0; i < sizes.Length; i++)
			{
				if (sizes[i] < smallest)
				{
					smallest = sizes[i];
					ret = i;
				}
			}
			return ret;
		}

		public float GetVaginaCapacity(Token vagina)
		{
			/* TODO: math stolen from CoC, which I think uses inches? Anyway, I get results like "4884.479" and I don't know if that's right.
			 * CoC's cockThatFits(x) takes a vaginal capacity and compares it against each cock's area, which according to cockArea(x) is thickness * length.
			 * For example, my testing character has a penis that is 24cm long and 2.5cm thick, giving a cockArea of 60, and a vagina that has wetness 4 ("drooling")
			 * and looseness 2 (undescribed), which this function (again adapted from CoC) gives a capacity of 4884.479...
			 * *sharp inhale*
			 * means that it is POSITIVELY CAVERNOUS!
			 * ...
			 * ...
			 * Okay, I tried again with a vagina that is wetness 0 ("dry") and looseness 0 ("virgin"), still no bonuses (did I forget to mention this is all without
			 * bonuses being applied?) and guess what? **576**
			 * We've got a cock that would make Ron Jeremy consider joining a monastery in shame, and a DRY VIRGIN COOCH that could take several of those!
			 * Changing "looseness * looseness" to just "looseness" turns 576 into 72, which is much better. But still, against such an inhuman cock...
			 */

			var loosenesses = new[] { 8, 16, 24, 36, 56, 100 };
			var looseness = 0f;
			if (vagina.HasToken("looseness"))
				looseness = vagina.GetToken("looseness").Value;
			if (looseness < loosenesses.Length)
				looseness = loosenesses[(int)looseness];
			else
				looseness = 10000;

			var wetnesses = new[] { 1.25f, 1, 0.8f, 0.7f, 0.6f, 0.5f };
			var wetness = 0f;
			if (vagina.HasToken("wetness"))
				wetness = vagina.GetToken("wetness").Value;
			if (wetness < wetnesses.Length)
				wetness = wetnesses[(int)wetness];
			else
				wetness = 0.5f;

			var bonus = 0f;
			if (this.HasToken("vcapbonus"))
				bonus = this.GetToken("vcapbonus").Value;

			var bodyBonus = 0f;

			var ret = (bodyBonus + bonus + 8 * looseness /* * looseness */) * (1 + wetness / 10);
			return ret;
		}
		//can double as GetNipplecuntCapacity yay

		public float[] GetVaginaCapacities()
		{
			var pussies = this.Tokens.FindAll(x => x.Name == "vagina").ToArray();
			var caps = new float[pussies.Length];
			for (var i = 0; i < pussies.Length; i++)
			{
				caps[i] = GetVaginaCapacity(pussies[i]);
			}
			return caps;
		}

		public float GetVaginaCapacity(int vagina)
		{
			var caps = GetVaginaCapacities();
			if (vagina >= caps.Length || vagina < 0)
				return -1f;
			return caps[vagina];
		}

		public Token GetVaginaByNumber(int vagina)
		{
			var pussies = this.Tokens.FindAll(x => x.Name == "vagina").ToArray();
			if (vagina >= pussies.Length || vagina < 0)
				return null;
			return pussies[vagina];
		}

		public int LargestVaginaNumber
		{
			get
			{
				var caps = GetVaginaCapacities();
				if (caps.Length == 0)
					return -1;
				var largest = -1f;
				var ret = -1;
				for (var i = 0; i < caps.Length; i++)
				{
					if (caps[i] > largest)
					{
						largest = caps[i];
						ret = i;
					}
				}
				return ret;
			}
		}

		public int SmallestVaginaNumber
		{
			get
			{
				var caps = GetVaginaCapacities();
				if (caps.Length == 0)
					return -1;
				var smallest = 99999f;
				var ret = -1;
				for (var i = 0; i < caps.Length; i++)
				{
					if (caps[i] < smallest)
					{
						smallest = caps[i];
						ret = i;
					}
				}
				return ret;
			}
		}


		public bool UpdatePregnancy()
		{
			if (!this.HasToken("vagina"))
				return false;
			if (this.HasToken("egglayer") && !this.HasToken("pregnancy"))
			{
				var boardChar = this.GetBoardChar();
				var eggToken = this.GetToken("egglayer");
				eggToken.Value++;
				if (eggToken.Value == 500)
				{
					eggToken.Value = 0;
					NoxicoGame.Sound.PlaySound("Put Item");
					var egg = new DroppedItem("egg")
					{
						XPosition = boardChar.XPosition,
						YPosition = boardChar.YPosition,
						ParentBoard = boardChar.ParentBoard,
					};
					egg.Take(this);
					if (boardChar is Player)
						NoxicoGame.AddMessage("You have laid an egg.");
					return false;
				}
			}
			else if (this.HasToken("pregnancy"))
			{
				var pregnancy = this.GetToken("pregnancy");
				var gestation = pregnancy.GetToken("gestation");
				gestation.Value++;
				if (gestation.Value >= gestation.GetToken("max").Value)
				{
					var child = pregnancy.Path("child");
					var location = this.Path("childlocation");
					var childName = new Name();

					Character childChar = null;

					if ((child != null && location == null) || (child == null))
					{
						//Invalidated location or no child definition.
						childName.Female = Random.NextDouble() > 0.5;
						childName.NameGen = this.GetToken("namegen").Text;
					}
					else
					{
						childName.Female = child.HasToken("vagina");
						childName.NameGen = child.GetToken("namegen").Text;
					}
					childName.Regenerate();
					if (childName.Surname.StartsWith("#patronym"))
						childName.ResolvePatronym(new Name(pregnancy.GetToken("father").Text), this.Name);
					
					if (location != null)
					{
						childChar = new Character();
						//childChar.Tokens = child.Tokens;
						childChar.Tokens.Clear();
						childChar.AddSet(child.Tokens);

						childChar.Culture = Culture.DefaultCulture;
						if (childChar.HasToken("culture"))
						{
							var culture = childChar.GetToken("culture").Text;
							if (Culture.Cultures.ContainsKey(culture))
								childChar.Culture = Culture.Cultures[culture];
						}

						var gender = childChar.GetGenderEnum();

						var terms = childChar.GetToken("terms");
						childChar.Species = gender.ToString() + " " + terms.GetToken("generic").Text;
						if (gender == Gender.Male && terms.HasToken("male"))
							childChar.Species = terms.GetToken("male").Text;
						else if (gender == Gender.Female && terms.HasToken("female"))
							childChar.Species = terms.GetToken("female").Text;
						else if (gender == Gender.Herm && terms.HasToken("herm"))
							childChar.Species = terms.GetToken("herm").Text;

						childChar.UpdateTitle();

						if (childChar.HasToken("femalesmaller"))
						{
							if (gender == Gender.Female)
								childChar.GetToken("tallness").Value -= Random.Next(5, 10);
							else if (gender == Gender.Herm)
								childChar.GetToken("tallness").Value -= Random.Next(1, 6);
						}

						childChar.GetToken("health").Value = childChar.GetMaximumHealth();
					}

					var ships = this.GetToken("ships");
					ships.AddToken(childName.ToID()).AddToken("child");
					if (childChar != null)
					{
						//also ship the child to the parent, can use SetRelation this time.
						childChar.SetRelation(this, "mother");
					}

					//Midwife Daemon goes here, using the location token.

					//Until then, we'll just message you.
					if (this.HasToken("player"))
						MessageBox.Notice("You have given birth to little " + childName.FirstName + ".", true, "Congratulations, mom.");

					this.RemoveToken("pregnancy");
					return true;
				}
			}
			return false;
		}

		public bool Fertilize(Character father)
		{
			var fertility = 0.0;
			if (this.HasToken("fertility"))
				fertility = this.GetToken("fertility").Value;
			//Simple version for now -- should involve the father, too.
			if (Random.Next() > fertility)
				return false;

			if (!this.HasToken("childlocation"))
				return true; //abstract pregnancy -- no need to actually mix up a baby
			
			//Now, let's play God.

			var pregnancy = this.Path("pregnancy");
			if (pregnancy == null)
				pregnancy = this.AddToken("pregnancy");
			var child = pregnancy.AddToken("child");
			var mother = this; //for clarity

			var fromMothersPlan = new[]
			{
				"normalgenders", "explicitgenders", "invisiblegender", "terms", "ascii", "femalesmaller",
			};
			var fromEitherPlan = new[]
			{
				"ass", "hips", "waist", "fertility", "breastrow", "vagina", "penis", "balls", "either",
			};
			var inheritable = new[]
			{
				"hair", "skin", "eyes", "face", "tongue", "teeth", "ears", "legs", "taur", "quadruped", "snaketail", "slimeblob", "wings", "horn", "monoceros",
			};
			var alwaysThere = new[]
			{
				"items", "health", "perks", "skills",
				"charisma", "climax", "cunning", "carnality",
				"stimulation", "sensitivity", "speed", "strength",
				"money", "ships", "paragon", "renegade"
			};
			var alwaysThereVals = new[]
			{
				0, 10, 0, 0,
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0,
			};

			//TODO: make fromMothersPlan and fromEitherPlan use FRESHLY ROLLED data from the closest bodyplan matches.

			foreach (var item in fromMothersPlan)
			{
				if (mother.HasToken(item))
					child.AddToken(item, mother.GetToken(item).Value, mother.GetToken(item).Text).AddSet(mother.GetToken(item).Tokens);
			}

			foreach (var item in fromEitherPlan)
			{
				var source = Random.NextDouble() > 0.5 ? father : mother;
				var other = source == father ? mother : father;
				if (source.HasToken(item))
					child.AddToken(item, source.GetToken(item).Value, source.GetToken(item).Text).AddSet(source.GetToken(item).Tokens);
				else if (other.HasToken(item))
					child.AddToken(item, other.GetToken(item).Value, other.GetToken(item).Text).AddSet(other.GetToken(item).Tokens);
			}

			foreach (var item in inheritable)
			{
				var source = Random.NextDouble() > 0.5 ? father : mother;
				var other = source == father ? mother : father;
				if (source.HasToken(item))
					child.AddToken(item, source.GetToken(item).Value, source.GetToken(item).Text).AddSet(source.GetToken(item).Tokens);
				else if (other.HasToken(item))
					child.AddToken(item, other.GetToken(item).Value, other.GetToken(item).Text).AddSet(other.GetToken(item).Tokens);
			}

			for (var i = 0; i < alwaysThere.Length; i++)
				child.AddToken(alwaysThere[i], alwaysThereVals[i]);

			var gender = Random.NextDouble() > 0.5 ? Gender.Male : Gender.Female;

			if (gender == Gender.Male)
			{
				child.RemoveToken("fertility");
				child.RemoveToken("milksource");
				child.RemoveToken("vagina");
				if (child.HasToken("breastrow"))
					child.GetToken("breastrow").GetToken("size").Value = 0f;
			}
			else if (gender == Gender.Female)
			{
				child.RemoveToken("penis");
				child.RemoveToken("balls");
			}

			return true;
		}



		public void GiveRapistPoints(Character bottom)
		{
			var points = Random.Next(4, 8);
			if (!bottom.HasToken("hostile"))
				points *= 2;
			ChangeStat("renegade", points);
			ChangeStat("carnality", Random.Next(5, 15));
			
			//TODO: more effects?
		}

		public void GiveRapeVictimPoints(Character top)
		{
			//TODO: change a couple stats that fit the bill
		}

		public void GiveConsentualPoints(Character bottom)
		{
			ChangeStat("paragon", Random.Next(1, 4));
			//TODO: more effects?
		}


		#region PillowShout's additions
		/// <summary>
        /// Checks the character's inventory to see if it contains at least one item with a matching ID.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
        /// <returns>True if an item with a matching ID is found or false if not.</returns>
        public bool HasItem(string itemID)
        {
            return (GetInventoryItems(itemID).Length > 0);
        }

        /// <summary>
        /// Checks the character's inventory to see if it contains an item with a matching ID,
        /// and if that item has been equipped by the character.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
        /// <returns>True if an item with a matching ID is equipped or or false if not.</returns>
        public bool HasItemEquipped(string itemID)
        {
            var itemList = GetInventoryItems(itemID);
            var item = itemList.FirstOrDefault(y => y.tempToken.HasToken("equipped"));

            return (item != null);
        }

        /// <summary>
        /// Checks the character's inventory to see if the character has an item equipped in a particular item slot.
        /// </summary>
        /// <param name="itemSlot">The name of the item slot to check. Valid options are:
        /// cloak, goggles, hand, hat, jacket, mask, neck, pants, ring, shirt, underpants, undershirt</param>
        /// <returns>True if the character has an item equipped to the specified slot, or false if not.</returns>
        public bool HasItemInSlot(string itemSlot)
        {
            return (GetEquippedItemBySlot(itemSlot) != null);
        }

        /// <summary>
        /// Checks if a character has an item with the specified ID in their inventory, and returns a list containing all matching items.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
		/// <returns>A list containing all the items in the character's inventory with matching IDs. Each element is an <see cref="InventoryItem"/> from
		/// <see cref="NoxicoGame.KnownItems"/> that matches the item held by the character. A reference to the character held item itself is stored in
		/// <see cref="InventoryItem.tempToken"/>. If no matching items are found, returns an empty list.</returns>
        public InventoryItem[] GetInventoryItems(string itemID)
        {
            // Code mostly taken from LookAt

            Func<string, InventoryItem> getKnownItem = new Func<string, InventoryItem>(x =>
                {
                    return NoxicoGame.KnownItems.Find(y => y.ID == x);
                }
            );

            var carried = new List<InventoryItem>();

            var carriedItems = this.GetToken("items");
            for (var i = 0; i < carriedItems.Tokens.Count; i++)
            {
                Token carriedItem = carriedItems.Item(i);
                InventoryItem foundItem = getKnownItem(carriedItem.Name);
                if (foundItem == null)
                {
                    continue;
                }

                if(foundItem.ID == itemID)
                {
                    foundItem.tempToken = carriedItem;
                    carried.Add(foundItem);
                }
            }

            return carried.ToArray();
        }

        /// <summary>
        /// Checks if a character has an item with the specified ID in their inventory, and returns the first encountered instance of that item.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
        /// <returns>The first matching item or if no matching items are found, then null.</returns>
        public InventoryItem GetFirstInventoryItem(string itemID)
        {
            var itemList = GetInventoryItems(itemID);
            return itemList.Length > 0 ? itemList[0] : null;
        }

        /// <summary>
        /// Checks if the character has an item equipped to the specified item slot and returns the item.
        /// </summary>
        /// <param name="itemSlot">The name of the item slot to check. Valid options are:
        /// cloak, goggles, hand, hat, jacket, mask, neck, pants, ring, shirt, underpants, undershirt</param>
        /// <returns>Returns an InventoryItem from <see cref="NoxicoGame.KnownItems"/> matching the item held by the character. A reference to the character
		/// held item itself is stored in <see cref="InventoryItem.tempToken"/>. If there is no item in the character slot, then null is returned. </returns>
        public InventoryItem GetEquippedItemBySlot(string itemSlot)
        {
            // Code mostly taken from LookAt
            
            Func<string, InventoryItem> getKnownItem = new Func<string, InventoryItem>(x =>
            {
                return NoxicoGame.KnownItems.Find(y => y.ID == x);
            }
            );

            var carriedItems = this.GetToken("items");

            for (var i = 0; i < carriedItems.Tokens.Count; i++)
            {
                var carriedItem = carriedItems.Item(i);
                var foundItem = getKnownItem(carriedItem.Name);
                if (foundItem == null)
                {
                    continue;
                }

                if (foundItem.HasToken("equipable") && carriedItem.HasToken("equipped"))
                {
                    var eq = foundItem.GetToken("equipable");
                    if (eq.HasToken(itemSlot))
                    {
                        foundItem.tempToken = carriedItem;
                        return foundItem;
                    }
                }
            }

            return null;
        }

		/// <summary>
		/// If the passed body part has the "virgin" token, it is removed.
		/// </summary>
		/// <param name="bodypart">The body part to remove the virginity of.</param>
		/// <returns>Returns true if the body part had the virgin token, or false if it did not.</returns>
		public bool RemoveVirgin(Token bodypart)
		{
			if (bodypart.HasToken("virgin"))
			{
				bodypart.RemoveToken("virgin");
				return true;
			}

			return false;
		}

		/// <summary>
		/// Increases the 'looseness' of a hole-type token to fit the thickness of the passed penis token. If the hole is already
		/// large enough, then the looseness of the hole is not increased.
		/// </summary>
		/// <param name="hole">A token for the bodily orifice to stretch. Its only requirement is that it has a looseness value.</param>
		/// <param name="penis">A token for the penis or penis-like object that the orifice is being stretched by. Its only requirement is that it has a thickness value.</param>
		/// <returns>Returns true if the hole has been stretched, or false if the hole has not been stretched.</returns>
		public bool StretchHole(Token hole, Token penis)
		{
			if (hole == null || penis == null)
				return false;

			var dickSize = 0f;
			var holeSize = 0f;

			if (penis.HasToken("thickness"))
				dickSize = penis.GetToken("thickness").Value;

			var holeSizes = new[] { 0, 2, 4, 6, 10, 16 };  // Penis thicknesses the hole will fit without being stretched
			var looseness = 0f;
			if (hole.HasToken("looseness"))
				looseness = hole.GetToken("looseness").Value;

			if (looseness < holeSizes.Length)
				holeSize = holeSizes[(int)looseness];
			else
				holeSize = 100;

			if (dickSize > holeSize)
			{
				// Stretch hole until penis fits
				for (int i = 1; i < holeSizes.Length; i++)
				{
					if (holeSizes[i] >= dickSize || i == holeSizes.Length - 1) // Hole either big enough or is the largest size.
					{
						hole.GetToken("looseness").Value = i;
						break;
					}
				}

				return true;
			}

			return false;
		}

		/// <summary>
		/// Resets the values of climax and stimulation.
		/// </summary>
		public void Orgasm()
		{
			GetToken("climax").Value = 0;
			GetToken("stimulation").Value = 10;
		}

		/// <summary>
		/// Changes the value of the passed stat by the amount passed. If the stat is increased beyond 100, it is set to 100.
		/// Likewise if the stat is reduced below 0, it is set to 0;
		/// </summary>
		/// <param name="statname">The name of the stat to change the value of.</param>
		/// <param name="amount">The amount to change the stat value by. Positive numbers increase the value, and negative numbers decrease the value.</param>
		/// <returns>The new value of the stat.</returns>
		public float ChangeStat(string statname, float amount)
		{
			var stat = GetToken(statname).Value;

			stat += amount;

			if (stat > 100)
				stat = 100;
			if (stat < 0)
				stat = 0;

			return GetToken(statname).Value = stat;
		}

		/// <summary>
		/// Deals with the select tokens found in a new character's bodyplan during character generation.
		/// </summary>
		private void HandleSelectTokens()
		{
			TraverseForSelectTokens(this);
		}

		/// <summary>
		/// Traverses the token tree and looks for select tokens, then uses ResolveSelectToken to deal with them.
		/// </summary>
		/// <param name="parent"></param>
		private static void TraverseForSelectTokens(TokenCarrier parent)
		{
			while (parent.HasToken("select"))
			{
				var select = parent.GetToken("select");
				ResolveSelectToken(select, parent);
				parent.RemoveToken(select);
			}

			foreach (var t in parent.Tokens)
			{
				if (t.Tokens.Count > 0)
					TraverseForSelectTokens(t);
			}
		}

		/// <summary>
		/// Resolves select token groups. Randomly chooses one of the sets of tokens inside select and then adds it to the parent.
		/// Also deals with 'addto' and 'overwrite' tokens inside sets.
		/// </summary>
		/// <param name="select">The token tree headed by the select token and containing the set tokens.</param>
		/// <param name="parent">The token tree of which select is an immediate child node.</param>
		private static void ResolveSelectToken(Token select, TokenCarrier parent)
		{
			var tokenSets = new List<Token>();
			var probs = new List<float>();

			// Extract token sets and probabilities
			while (select.HasToken("set"))
			{
				tokenSets.Add(select.GetToken("set"));
				probs.Add(select.GetToken("set").Value);
				select.RemoveToken("set");
			}

			// Get weighted probabilities
			var sum = 0f;

			foreach (var p in probs) { sum += p; }
			for (var i = 0; i < probs.Count; i++) { probs[i] /= sum; }

			// Select a set to add
			var r = Random.NextDouble();
			sum = 0f;
			int choice = 0;

			for (var i = 0; i < probs.Count; i++)
			{
				sum += probs[i];

				if (r <= sum) { choice = i; break; }
			}



			// Add the set to the TokenCarrier
			var tempCarrier = new TokenCarrier();
			foreach (var t in tokenSets[choice].Tokens)
			{
				if (t.Name == "addto")
				{
					tempCarrier.Tokenize(t.Text);
					var temp = tempCarrier.Tokens[0];
					var target = parent.Path(temp.Name);

					if (target != null)
					{
						target.Tokens.AddRange(t.Tokens);
					}
				}
				else if (t.Name == "overwrite")
				{
					tempCarrier.Tokenize(t.Text);
					var temp = tempCarrier.Tokens[0];
					var target = parent.Path(temp.Name);

					if (target != null)
					{
						target.Text = temp.Text;
						target.Value = temp.Value;
					}
				}
				else
					parent.AddToken(t);
			}
		}
		#endregion

		public void Hunger(float points)
		{
			this.GetToken("satiation").Value -= points;
			CheckHasteSlow();
		}
		public void Eat(Token item)
		{
			var points = item.Value > 0 ? item.Value : 5;
			this.GetToken("satiation").Value += points;
			if (item.HasToken("fat"))
			{
				var hwa = Random.Flip() ? "hips" : Random.Flip() ? "waist" : "ass/size";
				var change = Random.NextDouble() * 0.25;
				if (change > 0)
				{
					this.Path(hwa).Value += (float)change;
					if (hwa.Equals("ass/size"))
						hwa = Descriptions.ButtRandom();
					NoxicoGame.AddMessage("That went right to your " + hwa + "!");
				}
			}
		}
	}

	public class Name
	{
		public bool Female { get; set; }
		public string FirstName { get; set; }
		public string Surname { get; set; }
		public string Title { get; set; }
		public string NameGen { get; set; }
		public Name()
		{
			FirstName = "";
			Surname = "";
			Title = "";
			NameGen = "";
		}
		public Name(string name)
			: this()
		{
			var split = name.Split(' ');
			if (split.Length >= 1)
				FirstName = split[0];
			if (split.Length >= 2)
				Surname = split[1];
		}
		public void Regenerate()
		{
			FirstName = Culture.GetName(NameGen, Female ? Noxico.Culture.NameType.Female : Noxico.Culture.NameType.Male);
			Surname = Culture.GetName(NameGen, Noxico.Culture.NameType.Surname);
			Title = "";
		}
		public void ResolvePatronym(Name father, Name mother)
		{
			if (!Surname.StartsWith("#patronym"))
				return;
			var parts = Surname.Split('/');
			var male = parts[1];
			var female = parts[2];
			if (Female)
				Surname = mother.FirstName + female;
			else
				Surname = father.FirstName + male;
		}
		public override string ToString()
		{
			return FirstName;
		}
		public string ToString(bool full)
		{
			if (!full || string.IsNullOrWhiteSpace(Surname))
				return FirstName;
			return FirstName + ' ' + Surname;
		}
		public string ToID()
		{
			//had the silly thing in reverse ^_^;
			return FirstName + (string.IsNullOrWhiteSpace(Surname) ? string.Empty : '_' + Surname);
		}
		public void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "NGEN");
			stream.Write(FirstName);
			stream.Write(Surname);
			stream.Write(Title);
			stream.Write(NameGen);
		}
		public static Name LoadFromFile(BinaryReader stream)
		{
			var newName = new Name();
			Toolkit.ExpectFromFile(stream, "NGEN", "name");
			newName.FirstName = stream.ReadString();
			newName.Surname = stream.ReadString();
			newName.Title = stream.ReadString();
			var namegen = stream.ReadString();
			if (Culture.NameGens.Contains(namegen))
				newName.NameGen = namegen;
			return newName;
		}
	}
}
