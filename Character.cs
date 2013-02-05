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
		private static XmlDocument bodyPlansDocument, uniquesDocument, itemsDocument;
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
				return Name.ToID();
			}
		}

		/// <summary>
		/// Returns the full name of the character with title, or just the title.
		/// </summary>
		public override string ToString()
		{
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
		public string GetName()
		{
			var g = HasToken("invisiblegender") ? "" : GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || HasToken("malename"))) ||
				(g == "female " && (HasToken("femaleonly") || HasToken("femalename"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")))
				g = "";
			if (IsProperNamed)
				return Name.ToString();
			return string.Format("{0} {1}{2}", A, g, Species);
		}

		/// <summary>
		/// Returns the character's title.
		/// </summary>
		public string GetTitle()
		{
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

		public bool HasNormalSkin()
		{
			//TODO: make these use the colors defined for the Human bodytype instead of hardcoding?
			var regularColors = new[] { "light", "pale", "dark", "olive" };
			if (Path("skin/type") == null || Path("skin/type").Text != "skin")
				return false;
			var color = Path("skin/color");
			if (color == null)
				return false;
			if (regularColors.Contains(color.Text))
				return true;
			return false;
		}

		public bool HasGreenSkin()
		{
			var color = Path("skin/color");
			if (color == null)
				return false;
			var raw = Toolkit.GetColor(color.Text);
			var hue = raw.GetHue();
			if (hue >= 98 && hue <= 148)
				return true;
			return false;
		}

		public float GetMaximumHealth()
		{
			return GetToken("strength").Value * 2 + 50 + (HasToken("healthbonus") ? GetToken("healthbonus").Value : 0);
		}

		public Character()
		{
			Tokens = new List<Token>();
		}

		public static Character GetUnique(string id)
		{
			if (uniquesDocument == null)
			{
				uniquesDocument = Mix.GetXMLDocument("uniques.xml");
				//uniquesDocument = new XmlDocument();
				//uniquesDocument.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Main, "noxico.xml"));
			}

			var newChar = new Character();
			var planSource = uniquesDocument.SelectSingleNode("//uniques/character[@id=\"" + id + "\"]") as XmlElement;
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokens = Token.Tokenize(plan);
			newChar.Name = new Name(planSource.GetAttribute("name"));
			newChar.A = "a";
			newChar.IsProperNamed = planSource.HasAttribute("proper");

			var prefabTokens = new[]
			{
				"items", "health", "perks", "skills",
				"charisma", "climax", "cunning", "carnality",
				"stimulation", "sensitivity", "speed", "strength",
				"money", "ships", "paragon", "renegade"
			};
			var prefabTokenValues = new[]
			{
				0, 10, 0, 0,
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0,
			};
			for (var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.Tokens.Add(new Token() { Name = prefabTokens[i], Value = prefabTokenValues[i] });
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
				newChar.Name.Female = Toolkit.Rand.NextDouble() > 0.5;

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
			{
				bodyPlansDocument = Mix.GetXMLDocument("bodyplans.xml");
				//bodyPlansDocument = new XmlDocument();
				//bodyPlansDocument.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BodyPlans, "bodyplans.xml"));
			}

			var newChar = new Character();
			var planSource = bodyPlansDocument.SelectSingleNode("//bodyplans/bodyplan[@id=\"" + bodyPlan + "\"]") as XmlElement;
			//gToolkit.VerifyBodyplan(planSource); //by PillowShout
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokens = Token.Tokenize(plan);
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
				var g = Toolkit.Rand.Next(min, max + 1);
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
					newChar.Name.Female = Toolkit.Rand.NextDouble() > 0.5;
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
				"money", "ships", "paragon", "renegade"
			};
			var prefabTokenValues = new[]
			{
				0, 10, 0, 0,
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0,
			};
			for (var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.Tokens.Add(new Token() { Name = prefabTokens[i], Value = prefabTokenValues[i] });
			newChar.GetToken("health").Value = newChar.GetMaximumHealth();

			newChar.ApplyCostume();

			newChar.Culture = Culture.DefaultCulture;
			if (newChar.HasToken("culture"))
			{
				var culture = newChar.GetToken("culture").Text;
				if (Culture.Cultures.ContainsKey(culture))
					newChar.Culture = Culture.Cultures[culture];
			}

			if (newChar.HasToken("beast") && !newChar.HasToken("neverprefix") && Toolkit.Rand.NextDouble() > 0.5)
			{
				var prefixes = new[] { "vorpal", "poisonous", "infectious", "dire", "underfed" };
				var prefix = newChar.AddToken("prefixes");
				var chosen = prefixes[Toolkit.Rand.Next(prefixes.Length)];
				if (!newChar.HasToken("infectswith"))
					while (chosen == "infectious")
						chosen = prefixes[Toolkit.Rand.Next(prefixes.Length)];
				var token = prefix.AddToken(chosen);
				newChar.UpdateTitle();
			}

			if (newChar.HasToken("femalesmaller"))
			{
				if (gender == Gender.Female)
					newChar.GetToken("tallness").Value -= Toolkit.Rand.Next(5, 10);
				else if (gender == Gender.Herm)
					newChar.GetToken("tallness").Value -= Toolkit.Rand.Next(1, 6);
			}

			while (newChar.HasToken("either"))
			{
				var either = newChar.GetToken("either");
				var eitherChoice = Toolkit.Rand.Next(-1, either.Tokens.Count);
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
			{
				if (itemsDocument == null)
				{
					itemsDocument = Mix.GetXMLDocument("costumes.xml");
					//itemsDocument = new XmlDocument();
					//itemsDocument.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.Items, "items.xml"));
				}
				var costumesToken = GetToken("costume");
				var costumeChoices = costumesToken.Tokens;
				var costume = new Token();
				var lives = 10;
				while (costume.Tokens.Count == 0 && lives > 0)
				{
					var pick = costumeChoices[Toolkit.Rand.Next(costumeChoices.Count)].Name;
					var xElement = itemsDocument.SelectSingleNode("//costume[@id=\"" + pick + "\"]") as XmlElement;
					if (xElement != null)
					{
						var plan = xElement.ChildNodes[0].Value;
						if (plan == null)
						{
							lives--;
							continue;
						}
						costume.Tokens = Token.Tokenize(plan);
						//var plan = xDoc.CreateElement("costume");
						//plan.InnerXml = xElement.InnerXml;
						//OneOf(plan);
						//costume.Tokens = Token.Tokenize(plan);
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
						costume = costume.GetToken(Toolkit.Rand.Next(100) > 50 ? "male" : "female");
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
				Console.WriteLine("Had to remove {0} inventory item(s) from {1}: {2}", toDelete.Count, GetName(), string.Join(", ", toDelete));
				GetToken("items").RemoveSet(toDelete);
			}
		}

		public void AddSet(List<Token> otherSet)
		{
			foreach (var toAdd in otherSet)
			{
				this.Tokens.Add(new Token() { Name = toAdd.Name, Text = toAdd.Text, Value = toAdd.Value });
				if (toAdd.Tokens.Count > 0)
					this.GetToken(toAdd.Name).AddSet(toAdd.Tokens);
			}
		}

		public void IncreaseSkill(string skill)
		{
			var skills = GetToken("skills");
			if (!skills.HasToken(skill))
				skills.Tokens.Add(new Token() { Name = skill });

			var s = skills.GetToken(skill);
			var l = (int)s.Value;
			var i = 0.0349 / (1 + (l / 2));
			s.Value += (float)i;
		}

		public float CumAmount()
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

		private void Columnize(Action<string> print, int pad, List<string> col1, List<string> col2, string header1, string header2)
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

		public string LookAt(Entity pa)
		{
			if (this.HasToken("beast"))
			{
				return this.GetToken("bestiary").Text;
			}

			var sb = new StringBuilder();

			//Backporting from .js mockup :D
			//Things not listed: pregnancy, horns and wings.
			#region Backport compatibility
			Action<string> print = new Action<string>(x => sb.Append(x));
			Func<string, InventoryItem> getKnownItem = new Func<string, InventoryItem>(x =>
			{
				return NoxicoGame.KnownItems.Find(y => y.ID == x);
			}
			);
			Func<string, string> nameColor = new Func<string, string>(x => Toolkit.NameColor(x));
			Func<float, string> count = new Func<float, string>(x => Toolkit.Count(x));
			var isPlayer = (pa != null && pa is Player);
			#endregion

			//Begin backport!

			var stimulation = this.GetToken("stimulation").Value;
			var legLength = this.GetToken("tallness").Value * 0.53f;

			print(("Name: " + this.Name.ToString(true)).PadRight(34) + "Type: " + this.Title + (isPlayer ? " (player)" : "") + "\n");
			print("\n");

			#region Equipment, phase 1
			bool breastsVisible = false, crotchVisible = false;
			var carried = new List<InventoryItem>();
			var worn = new List<string>();
			var hands = new List<InventoryItem>();
			var fingers = new List<InventoryItem>();
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
				var foundItem = getKnownItem(carriedItem.Name);
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
				worn.Add(hat.ToString(hat.tempToken));
			if (goggles != null)
				worn.Add(goggles.ToString(goggles.tempToken));
			if (mask != null)
				worn.Add(mask.ToString(mask.tempToken));
			if (neck != null)
				worn.Add(neck.ToString(neck.tempToken));
			if (cloak != null)
				worn.Add(cloak.ToString(cloak.tempToken));
			if (jacket != null)
				worn.Add(jacket.ToString(jacket.tempToken));
			if (shirt != null)
				worn.Add(shirt.ToString(shirt.tempToken));
			if (pants != null && pants != shirt)
				worn.Add(pants.ToString(pants.tempToken));
			if (!isPlayer)
			{
				if (undershirt != null && (shirt == null || shirt.CanSeeThrough()))
				{
					breastsVisible = undershirt.CanSeeThrough();
					worn.Add(undershirt.ToString(undershirt.tempToken));
				}
				else
					breastsVisible = (shirt == null || shirt.CanSeeThrough());
				if (underpants != null && (pants == null || pants.CanSeeThrough()))
				{
					crotchVisible = underpants.CanSeeThrough();
					worn.Add(underpants.ToString(underpants.tempToken));
				}
				else
					crotchVisible = (pants == null || pants.CanSeeThrough());
			}
			else
			{
				if (undershirt != null)
					worn.Add(undershirt.ToString(undershirt.tempToken));
				if (underpants != null)
					worn.Add(underpants.ToString(underpants.tempToken));
				crotchVisible = breastsVisible = true;
			}
			#endregion

			#region Body and Face
			print("General\n-------\n");

			var bodyThings = new List<string>();
			var headThings = new List<string>();

			if (this.HasToken("slimeblob"))
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value - legLength) + " tall");
			else
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value) + " tall");
			if (this.HasToken("snaketail"))
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value + legLength) + " long");

			if (this.Path("skin/type").Text == "slime")
				bodyThings.Add(nameColor(this.Path("hair/color").Text) + " slime");
			else
				bodyThings.Add(nameColor(this.Path("skin/color").Text) + " " + this.Path("skin/type").Text);
			if (this.Path("skin/pattern") != null)
				bodyThings.Add(nameColor(this.Path("skin/pattern/color").Text) + " " + this.Path("skin/pattern").Text);

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
				var eyes = nameColor(this.GetToken("eyes").Text) + " eyes";
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
			#endregion

			#region Hair and Hips
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
				hairThings.Add(nameColor(this.Path("hair/color").Text));
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
			#endregion

			#region Clothing
			print("Clothing\n");
			if (worn.Count == 0)
				print("| none\n");
			else
				for (var i = 0; i < worn.Count; i++)
					print("| " + worn[i] + "\n");
			print("\n");
			#endregion

			#region Equipment
			print("Equipment\n");
			var mono = HasToken("monoceros") ? 1 : 0;
			if (this.HasToken("noarms") && hands.Count > 1 + mono)
				print("<b>NOTICE<b>: dual wielding with mouth.\n");
			if (hands.Count > 2 + mono)
				print("<b>NOTICE<b>: Shiva called.\n");
			if (hands.Count == 0)
				print("| none\n");
			else
				for (var i = 0; i < hands.Count; i++)
					print("| " + hands[i] + "\n");
			//Extend from
			// | combat knife
			//to
			// | combat knife (30 dmg, m<g00EA>l<g00E9>e)
			print("\n");
			#endregion

			#region Sexual characteristics
			print("Sexual characteristics\n----------------------\n");
			var cocks = new List<Token>(); var vaginas = new List<Token>(); var breastRows = new List<Token>();
			Token nuts;
			var ballCount = 0;
			var ballSize = 0.25f;
			var slit = this.HasToken("snaketail");
			var aroused = stimulation > 50;

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
				print(count(breastRows.Count) + " row" + (breastRows.Count == 1 ? "" : "s") + "\n");
				for (var i = 0; i < breastRows.Count; i++)
				{
					var row = breastRows[i];
					print("| " + count(row.GetToken("amount").Value) + " " + breast(/* row.GetToken("size").Value */ GetBreastRowSize(i)) + " breast");
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
					print(", " + count(row.GetToken("nipples").Value) + " " + nipType);
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
					print(count(vaginas.Count) + "\n");
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
					print(count(cocks.Count) + "\n");
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
					print("| " + count(ballCount) + " " + (ballSize < 1 ? "" : balls(ballSize) + " ") + "testicles\n");
				}
			}
			print("\n");
			#endregion

//#if DEBUG
			print("\n\n\n\n");
			print("<cGray>Debug\n<cGray>-----\n");
			print("<cGray>GetGender(): " + this.GetGender() + "\n");
			print("<cGray>Cum amount: " + this.CumAmount() + "mLs.\n");
			print("<cGray>Biggest breast row: #" + this.GetBiggestBreastrowNumber() + " @ " + this.GetBreastRowSize(this.GetBiggestBreastrowNumber()) + "'\n");
			print("<cGray>Biggest penis (length only): #" + this.GetBiggestPenisNumber(false) + " @ " + this.GetPenisSize(this.GetBiggestPenisNumber(false), false) + "cm\n");
			print("<cGray>Biggest penis (l * t): #" + this.GetBiggestPenisNumber(true) + " @ " + this.GetPenisSize(this.GetBiggestPenisNumber(true), true) + "cm\n");
			print("<cGray>Highest capacity cooch: #" + this.GetLargestVaginaNumber() + " @ " + this.GetVaginaCapacity(this.GetLargestVaginaNumber()) + "\n");
			print("<cGray>Smallest breast row: #" + this.GetSmallestBreastrowNumber() + " @ " + this.GetBreastRowSize(this.GetSmallestBreastrowNumber()) + "'\n");
			print("<cGray>Smallest penis (l * t): #" + this.GetSmallestPenisNumber(true) + " @ " + this.GetPenisSize(this.GetSmallestPenisNumber(true), true) + "cm\n");
			print("<cGray>Lowest capacity cooch: #" + this.GetSmallestVaginaNumber() + " @ " + this.GetVaginaCapacity(this.GetSmallestVaginaNumber()) + "\n");
			//#endif

			//End backport!

			return sb.ToString();
		}

		private Dictionary<string, string> LoadDictionary(string p)
		{
			var dict = Mix.GetString("lookat\\" + p + ".txt").Split('\n');
			var ret = new Dictionary<string, string>();
			foreach (var line in dict)
			{
				var parts = line.Split('\t');
				ret.Add(parts[0], parts[1]);
			}
			return ret;
		}

		public void CreateInfoDump()
		{
			var dump = new StreamWriter(Name + " info.txt");
			var list = new List<string>();

			dump.WriteLine("DESCRIPTION");
			dump.WriteLine("-----------");
			dump.WriteLine(this.LookAt(null));

			dump.WriteLine("ITEMS");
			dump.WriteLine("-----");
			if (GetToken("items").Tokens.Count == 0)
				dump.WriteLine("You were carrying nothing.");
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
				list.ForEach(x => dump.WriteLine(x));
			}
			dump.WriteLine();

			dump.WriteLine("RELATIONSHIPS");
			dump.WriteLine("-------------");
			if (GetToken("ships").Tokens.Count == 0)
				dump.WriteLine("You were in no relationships.");
			else
			{
				list.Clear();
				foreach (var person in GetToken("ships").Tokens)
				{
					var reparsed = person.Name.Replace('_', ' ');
					if (reparsed.StartsWith("\xF4EF"))
						reparsed = reparsed.Remove(reparsed.IndexOf('#')).Substring(1);
					list.Add(reparsed + " (" + person.Name + ") -- " + string.Join(", ", person.Tokens.Select(x => x.Name)));
				}
				list.Sort();
				list.ForEach(x => dump.WriteLine(x));
			}

			dump.Flush();
			dump.Close();

			File.WriteAllText("info.html", Toolkit.HTMLize(File.ReadAllText(Name + " info.txt")));
			System.Diagnostics.Process.Start("info.html"); //Name + " info.txt");
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
			{
				bodyPlansDocument = Mix.GetXMLDocument("bodyplans.xml");
				//bodyPlansDocument = new XmlDocument();
				//bodyPlansDocument.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.BodyPlans, "bodyplans.xml"));
			}

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
				MessageBox.Message(s, true);
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
			var target = new TokenCarrier() { Tokens = Token.Tokenize(plan) };
			var source = this;

			var toChange = new List<Token>();
			var changeTo = new List<Token>();
			var report = new List<string>();
			var doNext = new List<bool>();

			//Remove it later if there's none.
			if (!source.HasToken("tail"))
				source.Tokens.Add(new Token() { Name = "tail" });
			if (!target.HasToken("tail"))
				target.Tokens.Add(new Token() { Name = "tail" });

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
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Toolkit.NameColor(target.Path("skin/color").Text) + " skin all over.");
						break;
					case "fur":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Toolkit.NameColor(target.Path("skin/color").Text) + " fur all over.");
						break;
					case "rubber":
						report.Add((isPlayer ? "Your" : this.Name + "'s") + " skin has turned into " + Toolkit.NameColor(target.Path("skin/color").Text) + " rubber.");
						break;
					case "scales":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown thick " + Toolkit.NameColor(target.Path("skin/color").Text) + " scales.");
						break;
					case "slime":
						report.Add((isPlayer ? "You have" : this.Name + " has") + " turned to " + Toolkit.NameColor(target.Path("hair/color").Text) + " slime.");
						break;
					default:
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Toolkit.NameColor(target.Path("skin/color").Text) + ' ' + target.Path("skin/type").Text + '.');
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
						Tokens.Add(new Token() { Name = lowerBody });
						CheckPants();
						return;
					}
				}

				if (target.HasToken("legs"))
				{
					if (!source.HasToken("legs"))
						source.Tokens.Add(new Token() { Name = "legs" });
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
						Tokens.Add(new Token() { Name = taurQuad });
						CheckPants();
						if (target.HasToken("marshmallow") && !source.HasToken("marshmallow"))
							source.Tokens.Add(new Token() { Name = "marshmallow" });
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

			var choice = Toolkit.Rand.Next(toChange.Count);
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
					Tokens.Add(new Token() { Name = "originalskins" });
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
						thisSkin.Tokens.Add(new Token() { Name = "color" });
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

			if (doNext[choice] || Toolkit.Rand.Next(100) < continueChance)
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
				shipToken = new Token() { Name = target.ID };
				this.Path("ships").Tokens.Add(shipToken);
				shipToken.Tokens.Add(new Token() { Name = ship });
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

		public Character GetSpouse()
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

		public int GetBiggestBreastrowNumber()
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

		public int GetSmallestBreastrowNumber()
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

		public int GetLargestVaginaNumber()
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
	
		public int GetSmallestVaginaNumber()
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
					egg.PickUp(this);
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
						childName.Female = Toolkit.Rand.NextDouble() > 0.5;
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
						childChar.Tokens = child.Tokens;

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
								childChar.GetToken("tallness").Value -= Toolkit.Rand.Next(5, 10);
							else if (gender == Gender.Herm)
								childChar.GetToken("tallness").Value -= Toolkit.Rand.Next(1, 6);
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
						MessageBox.Message("You have given birth to little " + childName.FirstName + ".", true, "Congratulations, mom.");

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
			if (Toolkit.Rand.Next() > fertility)
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
				var source = Toolkit.Rand.NextDouble() > 0.5 ? father : mother;
				var other = source == father ? mother : father;
				if (source.HasToken(item))
					child.AddToken(item, source.GetToken(item).Value, source.GetToken(item).Text).AddSet(source.GetToken(item).Tokens);
				else if (other.HasToken(item))
					child.AddToken(item, other.GetToken(item).Value, other.GetToken(item).Text).AddSet(other.GetToken(item).Tokens);
			}

			foreach (var item in inheritable)
			{
				var source = Toolkit.Rand.NextDouble() > 0.5 ? father : mother;
				var other = source == father ? mother : father;
				if (source.HasToken(item))
					child.AddToken(item, source.GetToken(item).Value, source.GetToken(item).Text).AddSet(source.GetToken(item).Tokens);
				else if (other.HasToken(item))
					child.AddToken(item, other.GetToken(item).Value, other.GetToken(item).Text).AddSet(other.GetToken(item).Tokens);
			}

			for (var i = 0; i < alwaysThere.Length; i++)
				child.AddToken(alwaysThere[i], alwaysThereVals[i]);

			var gender = Toolkit.Rand.NextDouble() > 0.5 ? Gender.Male : Gender.Female;

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


		#region PillowShout's additions
		/// <summary>
        /// Checks the character's inventory to see if it contains at least one item with a matching ID.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
        /// <returns>True if an item with a matching ID is found or false if not.</returns>
        public bool HasItem(string itemID)
        {
            return (GetInventoryItems(itemID).Count > 0);
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
            var item = itemList.Find(y => y.tempToken.HasToken("equipped"));

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
        public List<InventoryItem> GetInventoryItems(string itemID)
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

            return carried;
        }

        /// <summary>
        /// Checks if a character has an item with the specified ID in their inventory, and returns the first encountered instance of that item.
        /// </summary>
        /// <param name="itemID">The ID of the item to search for.</param>
        /// <returns>The first matching item or if no matching items are found, then null.</returns>
        public InventoryItem GetFirstInventoryItem(string itemID)
        {
            var itemList = GetInventoryItems(itemID);
            return itemList.Count > 0 ? itemList[0] : null;
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
			while (this.HasToken("select"))
			{
				var select = this.GetToken("select");
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
				var r = Toolkit.Rand.NextDouble();
				sum = 0f;
				int choice = 0;

				for (var i = 0; i < probs.Count; i++)
				{
					sum += probs[i];

					if (r <= sum) { choice = i; break; }
				}

				// Add the set to the character
				foreach (var t in tokenSets[choice].Tokens) { this.AddToken(t); }

				this.RemoveToken(select);
			}
		}
		#endregion
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

	public static class Descriptions
	{
		public static string Length(float cm)
		{
			if (cm >= 100)
			{
				var m = Math.Floor(cm / 100);
				cm %= 100;
				if (cm > 0)
					return m + "." + cm + "m";
				else
					return m + "m";
			}
			if (Math.Floor(cm) != cm)
				return cm.ToString("F1") + "cm";
			else
				return cm.ToString("F0") + "cm";
			//return Math.Floor(cm).ToString() + "cm";
		}

		public static string Hair(Token hair)
		{
			var hairDesc = "";
			var hairLength = hair.HasToken("length") ? hair.GetToken("length").Value : 0f;
			var hairColorToken = hair.GetToken("color");
			var hairColor = Toolkit.NameColor(hairColorToken.Text).ToLowerInvariant();
			if (hairLength == 0)
				return Toolkit.PickOne("bald", "shaved");
			else if (hairLength < 1)
				hairDesc = Toolkit.PickOne("trim ", "close-cropped ");
			else if (hairLength < 3)
				hairDesc = "short";
			else if (hairLength < 6)
				hairDesc = "shaggy";
			else if (hairLength < 10)
				hairDesc = "moderately long";
			else if (hairLength < 16)
				hairDesc = Toolkit.PickOne("shoulder-length", "long");
			else if (hairLength < 26)
				hairDesc = Toolkit.PickOne("flowing locks of", "very long");
			else if (hairLength < 40)
				hairDesc = "ass-length";
			else if (hairLength >= 40)
				hairDesc = "obscenely long";
			hairDesc += ", " + hairColor;
			//consider equine "mane"
			hairDesc += " hair";

			return hairDesc;
		}

		public static string Breasts(Token titrow, bool inCups = true)
		{
			if (titrow == null)
				return "glitch";
			var titDesc = "";
			var size = titrow.HasToken("size") ? titrow.GetToken("size").Value : 0f;
			if (size == 0)
				return "flat breasts";
			else if (size < 0.5)
				return (inCups ? "AA-cup" : "tiny") + " titties"; //the only alliteration we'll allow.
			else if (size < 1)
				titDesc += inCups ? "A-cup" : "small";
			else if (size < 2.5)
				titDesc += inCups ? "B-cup" : "fair";
			else if (size < 3.5)
				titDesc += inCups ? "C-cup" : "appreciable";
			else if (size < 4.5)
				titDesc += inCups ? "D-cup" : "ample";
			else if (size < 6)
				titDesc += inCups ? "E-cup" : "pillowy";
			else if (size < 7)
				titDesc += inCups ? "F-cup" : "large";
			else if (size < 8)
				titDesc += inCups ? "G-cup" : "ridiculously large";
			else if (size < 9)
				titDesc += inCups ? "H-cup" : "huge";
			else if (size < 10)
				titDesc += inCups ? "I-cup" : "spacious";
			else if (size < 12)
				titDesc += inCups ? "J-cup" : "back-breaking";
			else if (size < 13)
				titDesc += inCups ? "K-cup" : "mountainous";
			else if (size < 14)
				titDesc += inCups ? "L-cup" : "ludicrous";
			else if (size < 15)
				titDesc += inCups ? "M-cup" : "exploding";
			else
				titDesc += "absurdly huge";
			var words = new[] { "tits", "breasts", "mounds", "jugs", "titties", "boobs" };
			while (true)
			{
				var word = words[Toolkit.Rand.Next(words.Length)];
				if (titDesc[0] == word[0])
					continue;
				titDesc += ' ' + word;
				break;
			}
			return titDesc;
		}

		public static string Nipples(Token nipples)
		{
			if (nipples == null)
				return "glitch";
			var nipDesc = "";
			var adjective = false;
			var size = nipples.HasToken("size") ? nipples.GetToken("size").Value : 0.25f;
			if (size < 0.25f)
				nipDesc = "dainty";
			else if (size < 1)
				nipDesc = "prominent";
			else if (size < 2)
				nipDesc = "fleshy";
			else if (size < 3.2f)
				nipDesc = "hefty";
			else
				nipDesc = "bulky";
			if (nipples.HasToken("fuckable"))
			{
				//involve lactation somehow
				nipDesc += ", wet";
				adjective = true;
			}
			else if (nipples.HasToken("canfuck"))
			{
				//involve lactation somehow
				nipDesc += ", mutated";
				adjective = true;
			}
			else
			{
				//involve lactation somehow
			}
			if (!adjective)
			{
				//involve lust somehow
			}
			if (nipples.HasToken("color"))
				nipDesc += ", " + Toolkit.NameColor(nipples.GetToken("color").Tokens[0].Name);
			if (nipples.HasToken("fuckable"))
				nipDesc += " nipple-hole";
			else if (nipples.HasToken("canfuck"))
				nipDesc += " nipplecock";
			else
				nipDesc += " nipple";
			return nipDesc;
		}

		public static string Waist(Token waist)
		{
			var size = waist != null ? waist.Value : 5;
			if (size < 1)
				return Toolkit.PickOne("emaciated", "gaunt") + " physique";
			else if (size < 4)
				return Toolkit.PickOne("thin", "thin") + " physique";
			else if (size < 6)
				return null; //Toolkit.PickOne("normal", "average");
			else if (size < 8)
				return Toolkit.PickOne("soft", "spongy") + " belly";
			else if (size < 11)
				return Toolkit.PickOne("chubby", "gropeable" + " belly");
			else if (size < 14)
				return Toolkit.PickOne("plump", "meaty") + " stomach";
			else if (size < 17)
				return Toolkit.PickOne("corpulent", "stout") + " physique";
			else if (size < 20)
				return Toolkit.PickOne("obese", "rotund") + " physique";
			return Toolkit.PickOne("morbid", "immobilizing") + " physique";
		}

		public static string Hips(Token hips)
		{
			var size = hips != null ? hips.Value : 5;
			if (size < 1)
				return Toolkit.PickOne("tiny", "boyish") + " hips";
			else if (size < 4)
				return Toolkit.PickOne("slender", "narrow", "thin") + " hips";
			else if (size < 6)
				return Toolkit.PickOne("average", "normal", "plain") + " hips";
			else if (size < 10)
				return Toolkit.PickOne("ample", "noticeable", "girly") + " hips";
			else if (size < 15)
				return Toolkit.PickOne("flared", "curvy", "wide") + " hips";
			else if (size < 20)
				return Toolkit.PickOne("fertile", "child-bearing", "voluptuous") + " hips";
			return Toolkit.PickOne("broodmother-sized", "cow-like", "inhumanly wide") + " hips";
		}

		public static string Butt(Token butt, bool extended = false)
		{
			var size = butt != null && butt.HasToken("size") ? butt.GetToken("size").Value : 5;
			var ret = "";
			if (size < 1)
				ret = Toolkit.PickOne("very small", "insignificant");
			else if (size < 4)
				ret = Toolkit.PickOne("tight", "firm", "compact");
			else if (size < 6)
				ret = Toolkit.PickOne("regular", "unremarkable");
			else if (size < 8)
				ret = Toolkit.PickOne("handful of ass", "full", "shapely");
			else if (size < 10)
				ret = Toolkit.PickOne("squeezable", "large", "substantial");
			else if (size < 13)
				ret = Toolkit.PickOne("jiggling", "spacious", "heavy");
			else if (size < 16)
				ret = Toolkit.PickOne("expansive", "generous amount of ass", "voluminous");
			else if (size < 20)
				ret = Toolkit.PickOne("huge", "vast", "jiggling expanse of ass");
			else
				ret = Toolkit.PickOne("ginormous", "colossal", "tremendous");
			var looseness = "";
			if (extended && butt != null && butt.HasToken("looseness") && butt.GetToken("looseness").Value != 2)
				looseness = Looseness(butt.GetToken("looseness"), true);
			if (ret.EndsWith("ass"))
				ret = looseness + " " + ret;
			else
			{
				if (!string.IsNullOrWhiteSpace(looseness))
					ret = looseness + ", " + ret;
				ret += " " + Toolkit.PickOne("butt", "ass", "behind", "bum");
			}
			return ret;
		}

		public static string Looseness(Token looseness, bool forButts = false)
		{
			if (looseness == null)
				return null;
			var rets = new[] { "virgin", "tight", null, "loose", "very loose", "gaping", "gaping wide", "cavernous" };
			if (forButts)
				rets = new[] { "virgin", "tight", null, "loose", "stretched", "distented", "gaping", "cavernous" };
			var v = (int)Math.Floor(looseness.Value);
			if (v >= rets.Length)
				v = rets.Length - 1;
			return rets[v];
		}

		public static string Wetness(Token wetness)
		{
			if (wetness == null)
				return null;
			var rets = new[] { "dry", null, "wet", "slick", "drooling", "slavering" };
			var v = (int)Math.Floor(wetness.Value);
			if (v >= rets.Length)
				v = rets.Length - 1;
			return rets[v];
		}

		public static string Cock(Token cock)
		{
			if (cock == null)
				return "glitch";
			if (cock.HasToken("horse"))
				return "horse cock";
			else if (cock.HasToken("dog"))
				return "dog";
			//TODO
			return "cock";
		}

		public static string Tail(Token tail)
		{
			if (tail == null)
				return "glitch";
			var tails = new Dictionary<string, string>()
			{
				{ "stinger", "stinger" }, //needed to prevent "stinger tail"
				{ "genbeast", Toolkit.Rand.NextDouble() < 0.5 ? "ordinary tail" : "tail" }, //"Your (ordinary) tail"
			};
			var tailName = tail.Text;
			if (tails.ContainsKey(tailName))
				return tails[tailName];
			else
				return tailName + " tail";
		}

		public static string Tentacle(Token tentacle, float stimulation)
		{
			if (tentacle == null)
				return "glitch";
			var ret = "tapered tip";
			if (tentacle.HasToken("penis"))
				ret = "thick penis head";
			if (stimulation > 70)
				ret += ", half its length coated in a slick layer of lubricant";
			else if (stimulation > 50)
				ret += " shining with lubrication";
			else if (stimulation > 25)
				ret += ", a thick dollop of lubrication at tbe tip";
			return ret;
		}

		#region PillowShout's additions
		/// <summary>
		/// Returns a string describing the token 'cock's type.
		/// </summary>
		/// <param name="cock">The penis token to be evaluated.</param>
		/// <returns>A string containing only the 'cock's type.</returns>
		public static string CockType(Token cock)
		{
			if (cock == null)
				return "glitch";
			if (cock.Text == "horse")
				return Toolkit.PickOne("equine", "horse-like", "flared");
			else if (cock.Text == "dog")
				return Toolkit.PickOne("canine", "dog-like", "pointed", "knotted");
			else if (cock.Text == "bear")
				return Toolkit.PickOne("ursine", "bear-like", "tapered");
			else if (cock.Text == "lizard")
				return Toolkit.PickOne("reptilian", "lizard-like", "snake-like");
			else if (cock.Text == "cat")
				return Toolkit.PickOne("feline", "cat-like", "barbed");
			else if (cock.Text == "studded")
				return Toolkit.PickOne("studded", "bumpy", "stud covered");
			else if (cock.Text == "human")
				return Toolkit.PickOne("human", "human-like", "knobby");
			return Toolkit.PickOne("unusual", "indescribable", "oddly shaped");
		}

		/// <summary>
		/// Provides a description of a character's foot based on their leg type.
		/// </summary>
		/// <param name="leg">The leg token to be evaluated.</param>
		/// <param name="plural">If set to true, the returned description will be for both feet.</param>
		/// <returns>A string containing a description of the foot type.</returns>
		public static string Foot(Token leg, bool plural = false)
		{
			if (leg == null)
				return "glitch";
			if (leg.Text == "horse")
				return plural ? "hooves" : "hoof";
			else if (leg.Text == "dog" || leg.Text == "bear")
				return plural ? "paws" : "paw";
			else if (leg.Text == "insect")
				return plural ? "claws" : "claw";

			return plural ? "feet" : "foot";
		}

		/// <summary>
		/// Chooses a random euphemism for penis and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for penis.</returns>
		public static string CockRandom()
		{
			return Toolkit.PickOne("penis", "cock", "dick", "pecker", "prick", "rod", "shaft");
		}

		/// <summary>
		/// Chooses a random euphemism for vagina and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for vagina.</returns>
		public static string PussyRandom()
		{
			return Toolkit.PickOne("vagina", "pussy", "cunt", "cooter", "cooch", "cunny", "quim", "twat");
		}

		/// <summary>
		/// Chooses a random euphemism for anus and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for anus.</returns>
		public static string AnusRandom()
		{
			return Toolkit.PickOne("anus", "asshole", "butthole", "rosebud", "pucker");
		}
		
		/// <summary>
		/// Chooses a random euphemism for clitoris and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for clitoris.</returns>
		public static string ClitRandom()
		{
			return Toolkit.PickOne("clitoris", "clit", "fun button", "clitty", "love button");
		}

		/// <summary>
		/// Chooses a random euphemism for breast and returns it as a string.
		/// </summary>
		/// <param name="plural">Flag for returning a plural euphmism insead of a singular.</param>
		/// <returns>A string containing a euphemism for breast(s).</returns>
		public static string BreastRandom(bool plural = false)
		{
			if (plural)
				return Toolkit.PickOne("breasts", "boobs", "tits", "knockers", "mounds", "titties");
			else
				return Toolkit.PickOne("breast", "boob", "tit", "knocker", "mound");
		}

		/// <summary>
		/// Chooses a random euphemism for semen and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for semen.</returns>
		public static string CumRandom()
		{
			return Toolkit.PickOne("semen", "cum", "jizz", "spunk", "seed");
		}

		/// <summary>
		/// Returns a string describing a piece of equipment using its name, (optionally) color (if available), and (optionally) an appropriate article.
		/// </summary>
		/// <param name="item">An InventoryItem from <see cref="NoxicoGame.KnownItems"/>.</param>
		/// <param name="token">An item token from a <see cref="Character"/>'s inventory that matches the type of item in the first parameter.</param>
		/// <param name="article">Adds either the definite article (if "the" is passed), the indefinite article (if "a" is passed), or no article (anything else is passed)
		/// to the front of the descriptive string.</param>
		/// <param name="withColor">If set to true, the returned string will also describe the color of the item if it has one.</param>
		/// <returns>A string containing the description of the item as defined by the parameters. If 'item' is null, then null is returned instead.</returns>
		public static string Item(InventoryItem item, Token token, string article = "", bool withColor = false)
		{
			if (item == null)
				return null;
			var name = (token != null && token.HasToken("unidentified") && !string.IsNullOrWhiteSpace(item.UnknownName)) ? item.UnknownName : item.Name;
			var color = (token != null && token.HasToken("color")) ? Toolkit.NameColor(token.GetToken("color").Text) : "";
			var reps = new Dictionary<string, string>()
			{
				{ "[color]", color },
				{ "[, color]", ", " + color },
				{ "[color ]", color + " " },
				{ "[color, ]", color + ", " },
			};
			if (withColor && color != "")
			{
				foreach (var i in reps)
					name = name.Replace(i.Key, i.Value);
			}
			else
			{
				foreach (var key in reps.Keys)
					name = name.Replace(key, "");
			}

			if (article == "the")
				name = item.The + " " + name;
			else if (article == "a")
				name = item.A + " " + name;

			return name;
		}

		/// <summary>
		/// Returns a string containing a description of the parameter 'tongue's type.
		/// </summary>
		/// <param name="tongue">A tongue token from a character.</param>
		/// <returns>A string containing the description of the tongue based on its type.</returns>
		public static string TongueType(Token tongue)
		{
			if (tongue == null)
				return "glitch";

			var ret = "";
			var text = tongue.Text;
			switch (text)
			{
				case "normal":
					ret = Toolkit.PickOne("smooth", "nimble");
					break;

				case "barbed":
					ret = Toolkit.PickOne("rough", "barbed");
					break;

				case "long":
					ret = Toolkit.PickOne("prehensile", "flexible");
					break;

				case "forked":
					ret = Toolkit.PickOne("forked", "serpentine");
					break;

				default:
					ret = Toolkit.PickOne("indescribable", "unusual");
					break;
			}

			return ret;
		}

		/// <summary>
		/// Takes a tail type token from a character and returns a string describing that tail's type.
		/// </summary>
		/// <param name="tail">The tail token to be evaluated.</param>
		/// <returns>A string containing a description of the tail's type. Return's 'glitch' if 'tail is null.</returns>
		public static string TailType(Token tail)
		{
			if (tail == null)
				return "glitch";

			var ret = "";
			var text = tail.Text;
			switch (text)
			{
				case "genbeast":
					ret = Toolkit.PickOne("furry");
					break;

				case "fox":
					ret = Toolkit.PickOne("vulpine", "fluffy");
					break;

				case "stinger":
					ret = Toolkit.PickOne("barbed", "segmented");
					break;

				case "horse":
					ret = Toolkit.PickOne("long-haired", "equine");
					break;

				case "dog":
					ret = Toolkit.PickOne("canine", "furry");
					break;

				default:
					ret = Toolkit.PickOne("indescribable", "unusual");
					break;
			}

			return ret;
		}
		
		#endregion
	}

}
