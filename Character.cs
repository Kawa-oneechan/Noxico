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

		public void UpdateTitle()
		{
			var g = GetGender();
			Title = GetToken("terms").GetToken("generic").Text;
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
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokens = Token.Tokenize(plan);
			newChar.Name = new Name();
			newChar.A = "a";

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

			//Console.WriteLine("Generated {0}.", newChar);
			return newChar;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			Name.SaveToFile(stream);
			stream.Write(Species ?? "");
			stream.Write(Title ?? "");
			stream.Write(IsProperNamed);
			stream.Write(A ?? "a");
			stream.Write(Culture.ID);
			stream.Write(Tokens.Count);
			Tokens.ForEach(x => x.SaveToFile(stream));
		}

		public static Character LoadFromFile(BinaryReader stream)
		{
			var newChar = new Character();
			newChar.Name = Name.LoadFromFile(stream);
			newChar.Species = stream.ReadString();
			newChar.Title = stream.ReadString();
			newChar.IsProperNamed = stream.ReadBoolean();
			newChar.A = stream.ReadString();
			var culture = stream.ReadString();
			newChar.Culture = Culture.DefaultCulture;
			if (Culture.Cultures.ContainsKey(culture))
				newChar.Culture = Culture.Cultures[culture];
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
					if ((HasToken("legs") && GetToken("legs").HasToken("quadruped")) || HasToken("snaketail") || HasToken("slimeblob"))
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
				this.Tokens.Add(new Token() { Name = toAdd.Name });
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

		public string LookAt(Entity pa)
		{
			if (this.HasToken("beast"))
			{
				return this.GetToken("bestiary").Text;
			}

			var sb = new StringBuilder();

			//TODO: account for slimeblobs -- "[He] [is] {0} tall upright, but effectively only {1}."
			//var legLength = this.GetToken("tallness").Value * 0.53;
			sb.AppendFormat("[He] [is] {0} tall. ", Descriptions.Length(this.GetToken("tallness").Value));

			var stimulation = this.GetToken("stimulation").Value;

			#region Equipment phase 1
			var breastsVisible = false;
			var crotchVisible = false;
			var carried = new List<InventoryItem>();
			var hands = new List<InventoryItem>();
			var fingers = new List<InventoryItem>();
			InventoryItem underpants = null;
			InventoryItem undershirt = null;
			InventoryItem pants = null;
			InventoryItem shirt = null;
			InventoryItem jacket = null;
			InventoryItem cloak = null;
			InventoryItem hat = null;
			InventoryItem goggles = null;
			InventoryItem mask = null;
			InventoryItem neck = null;
			foreach (var carriedItem in this.GetToken("items").Tokens)
			{
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				if (find == null)
					continue;
				if (find.HasToken("equipable") && carriedItem.HasToken("equipped"))
				{
					var eq = find.GetToken("equipable");
					if (eq.HasToken("underpants"))
					{
						underpants = find;
						underpants.tempToken = carriedItem;
					}
					if (eq.HasToken("undershirt"))
					{
						undershirt = find;
						undershirt.tempToken = carriedItem;
					}
					if (eq.HasToken("pants"))
					{
						pants = find;
						pants.tempToken = carriedItem;
					}
					if (eq.HasToken("shirt"))
					{
						shirt = find;
						shirt.tempToken = carriedItem;
					}
					if (eq.HasToken("jacket"))
					{
						jacket = find;
						jacket.tempToken = carriedItem;
					}
					if (eq.HasToken("cloak"))
					{
						cloak = find;
						cloak.tempToken = carriedItem;
					}
					if (eq.HasToken("hat"))
					{
						hat = find;
						hat.tempToken = carriedItem;
					}
					if (eq.HasToken("goggles"))
					{
						goggles = find;
						goggles.tempToken = carriedItem;
					}
					if (eq.HasToken("mask"))
					{
						mask = find;
						mask.tempToken = carriedItem;
					}
					if (eq.HasToken("neck"))
					{
						neck = find;
						neck.tempToken = carriedItem;
					}
					if (eq.HasToken("ring"))
					{
						find.tempToken = carriedItem;
						fingers.Add(find);
					}
					if (eq.HasToken("hand"))
					{
						find.tempToken = carriedItem;
						hands.Add(find);
					}
				}
				else
					carried.Add(find);
			}
			var visible = new List<string>();
			if (hat != null)
				visible.Add(hat.ToString(hat.tempToken));
			if (goggles != null)
				visible.Add(goggles.ToString(goggles.tempToken));
			if (mask != null)
				visible.Add(mask.ToString(mask.tempToken));
			if (neck != null)
				visible.Add(neck.ToString(neck.tempToken));
			if (cloak != null)
				visible.Add(cloak.ToString(cloak.tempToken));
			if (jacket != null)
				visible.Add(jacket.ToString(jacket.tempToken));
			if (shirt != null)
				visible.Add(shirt.ToString(shirt.tempToken));
			if (pants != null && pants != shirt)
				visible.Add(pants.ToString(pants.tempToken));
			if (undershirt != null && (shirt == null || shirt.CanSeeThrough()))
			{
				breastsVisible = undershirt.CanSeeThrough();
				visible.Add(undershirt.ToString(undershirt.tempToken));
			}
			else
				breastsVisible = (shirt == null || shirt.CanSeeThrough());
			if (underpants != null && (pants == null || pants.CanSeeThrough()))
			{
				crotchVisible = underpants.CanSeeThrough();
				visible.Add(underpants.ToString(underpants.tempToken));
			}
			else
				crotchVisible = (pants == null || pants.CanSeeThrough());
			#endregion

			#region Face and Skin
			var skinDescriptions = new Dictionary<string, Dictionary<string, string>>()
			{
				{ "skin",
					new Dictionary<string, string>()
					{
						{ "normal", "[He] [has] a fairly normal face, with {0} skin." },
						{ "genbeast", "[He] [has] an animalistic face, though it's difficult to tell exactly what kind of animal. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
						{ "horse", "[His] face is equine in shape and structure. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
						{ "dog", "[He] [has] a dog-like face, complete with a wet nose. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
						{ "cow", "[He] [has] a face resembling that of a minotaur, with cow-like features, particularly a squared off wet nose. Despite [his] lack of fur elsewhere, [his] head does have a short layer of {1} fuzz." },
						{ "cat", "[He] [has] a cat-like face, complete with a cute, moist nose, whiskers, and slitted eyes. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
						{ "reptile", "[He] [has] a face resembling that of a lizard, and with [his] toothy maw, [he] [has] quite a fearsome visage. [He] [does] look a little odd as a lizard without scales." },
					}
				},
				{
					"fur",
					new Dictionary<string, string>()
					{
						{ "normal", "Under [his] {0} fur [he] [has] a human-shaped head." },
						{ "genbeast", "[He] [has] a face like an animal, but still recognizably humanoid. [His] fur is {0}." },
						{ "horse", "[His] face is almost entirely equine in appearance, even having {0} fur." },
						{ "dog", "[He] [has] a dog's face, complete with wet nose and panting tongue. [His] fur is {0}." },
						{ "cow", "[He] [has] a face resembling that of a minotaur, with cow-like features, particularly a squared off wet nose. [His] {0} fur thickens noticably on [his] head, looking shaggy and monstrous." },
						{ "cat", "[He] [has] a cat's face, complete with moist nose, whiskers, and slitted eyes. [His] fur is {0}." },
						{ "reptile", "[He] [has] a face resembling that of a lizard. Between the toothy maw, pointed snout, and the layer of {0} fur covering [his] face, [he] [has] quite the fearsome visage." },
					}
				},
				{
					"rubber",
					new Dictionary<string, string>
					{
						{ "normal", "[His] face is fairly human in shape, but is covered in {0} rubber." },
						{ "genbeast", "[He] [has] a face like an animal, but overlaid with glittering {0} rubber instead of fur. The look is very strange, but not unpleasant." },
						{ "horse", "[He] [has] the face and head structure of a horse, overlaid with glittering {0} rubber. The look is strange, but not unpleasant." },
						{ "dog", "[He] [has] the face and head structure of a dog, wet nose and all, but overlaid with glittering {0} rubber. The look is strange, but not unpleasant." },
						{ "cow", "[His] face resembles a minotaur's, though strangely it is covered in shimmering {0} scales, right up to the flat cow-like noise that protrudes from [his] face." },
						{ "cat", "[He] [has] the facial structure of a cat, moist nose, whisker, and slitted eyes included, but overlaid with glittering {0} rubber. The look is strange, but not unpleasant." },
						{ "reptile", "[His] face is that of a lizard, complete with a toothy maw and pointed snout. Reflective {0} rubber completes the look, making [him] look quite fearsome." },
					}
				},
				{
					"scales",
					new Dictionary<string, string>
					{
						{ "normal", "[He] [has] a fairly normal face, with {0} scales." },
						{ "genbeast", "[He] [has] an animalistic face, though it's difficult to tell exactly what kind of animal. It looks somewhat odd as [he] [has] no fur, only {0} scales." },
						{ "horse", "[His] face is equine in shape and structure. It looks somewhat odd as [he] [has] no fur, only {0} scales." },
						{ "dog", "[He] [has] a dog-like face, complete with a wet nose. It looks somewhat odd as [he] [has] no fur, only {0} scales." },
						{ "cow", "[He] [has] a face resembling that of a minotaur, with cow-like features, particularly a squared off wet nose. Despite [his] lack of fur elsewhere, [his] head does have a short layer of {1} fuzz." },
						{ "cat", "[He] [has] a cat-like face, complete with a cute, moist nose, whiskers, and slitted eyes. It looks somewhat odd as [he] [has] no fur, only {0} scales." },
						{ "reptile", "[His] face is that of a lizard, complete with a toothy maw and pointed snout. Reflective {0} scales complete the look, making [him] look quite fearsome." },
					}
				},
				{
					"slime",
					new Dictionary<string, string>
					{
						{ "normal", "[He] [has] a fairly normal face, made of translucent {0} slime." },
						{ "genbeast", "[He] [has] an animalistic face, though it's difficult to tell exactly what kind of animal. It looks somewhat odd as [his] face is made of translucent {0} slime." },
						{ "horse", "[His] face is equine in shape and structure. It looks somewhat odd as [his] face is made of translucent {0} slime." },
						{ "dog", "[He] [has] a dog-like face, complete with a wet nose. It looks somewhat odd as [his] face is made of translucent {0} slime." },
						{ "cow", "[He] [has] a face resembling that of a minotaur, with cow-like features, particularly a squared off wet nose. It looks somewhat odd as [his] face is made of translucent {0} slime." },
						{ "cat", "[He] [has] a cat-like face, complete with a cute, moist nose, whiskers, and slitted eyes. It looks somewhat odd as [his] face is made of translucent {0} slime." },
						{ "reptile", "[His] face is that of a lizard, complete with a toothy maw and pointed snout. Translucent {0} slime completes the look, making [him] look quite fearsome." },
					}
				},
				{
					"metal",
					new Dictionary<string, string>
					{
						{ "normal", "[His] face is fairly human in basic shape, but made of a rigid-looking metallic substance, {0} in color." },
						{ "genbeast", "[His] face looks metallic and bestial, made of small slivers of {0} metal." },
						{ "horse", "[His] face is equine in appearance, with a smooth, metallic {0} surface." },
						{ "dog", "[He] [has] a dog-like face, made of smooth metal plates, {0} in color." },
						{ "cow", "[He] [has] a bovine face, made of a rigid-looking {0} metal." },
						{ "cat", "[His] face is cat-like, with the outer surface made of smooth little metal plates, {0} in color." },
						{ "reptile", "[His] face is lizard-like, made of smoothly laid out slivers of {0} metal." },
					}
				},
			};
			var skinName = this.Path("skin/type") != null ? this.Path("skin/type").Text : "skin";
			var faceType = this.HasToken("face") ? this.GetToken("face").Text : "normal";
			var hairColor = this.Path("hair/color") != null ? Toolkit.NameColor(this.Path("hair/color").Text).ToLowerInvariant() : "<null>";
			var skinColor = skinName == "slime" ? hairColor : Toolkit.NameColor(this.Path("skin/color").Text).ToLowerInvariant();
			//TODO: hide some info if wearing a mask.
			//if (mask != null && !mask.CanSeeThrough())
			sb.AppendFormat(skinDescriptions[skinName][faceType], skinColor, hairColor);
			//Simple two eyes setup for everybody, can expand to more eyes or special kinds later.
			var eyeColor = Toolkit.NameColor(this.GetToken("eyes").Text);
			sb.AppendFormat(" [He] has {0} eyes", eyeColor);
			if (this.Path("skin/pattern") != null)
			{
				var pattern = this.Path("skin/pattern");
				sb.AppendFormat(" and [his] body is covered in {0} {1}", pattern.GetToken("color").Text, pattern.Text);
			}
			sb.Append('.');
			sb.AppendLine();
			#endregion

			#region Hair and Ears
			if (this.HasToken("hair") && this.GetToken("hair").GetToken("length").Value > 0)
			{
				var hairDesc = Descriptions.Hair(this.GetToken("hair"));

				if (skinName == "slime")
					hairDesc = "goopy, " + hairDesc;
				else if (skinName == "rubber")
					hairDesc = "thick, " + hairDesc;
				else if (skinName == "metal")
					hairDesc = "plastic-like, " + hairDesc;

				var earDescriptions = new Dictionary<string, string>()
				{
					{ "human", "[His] {0} looks good, accentuating [his] features well." },
					{ "elfin", "[His] {0} is parted by a pair of long, pointed ears." },
					{ "genbeast", "[His] {0} is parted by a pair of sizable, triangular ears." },
					{ "horse", "[His] {0} parts around a pair of very horse-like ears that grow up from [his] head." },
					{ "dog", "[His] {0} is parted by a pair of pointed dog-ears." },
					{ "cat", "[His] {0} is parted by a pair of cat ears." },
					{ "cow", "[His] {0} is parted by a pair of rounded cow-ears that stick out sideways." },
					{ "frill", "[His] {0} is parted by a pair of draconic frills." },
					{ "bunny", "[His] {0} is parted by a pair of long, floppy bunny ears." },
				};
				if (this.HasToken("ears"))
				{
					var earType = "human";
					if (!string.IsNullOrWhiteSpace(this.GetToken("ears").Text))
						earType = this.GetToken("ears").Text;
					if (earDescriptions.ContainsKey(earType))
						sb.AppendFormat(earDescriptions[earType], hairDesc);
				}
				if (this.HasToken("antennae"))
					sb.AppendFormat(" Floppy antennae grow from just behind [his] hairline, bouncing and swaying in the breeze.");
				sb.AppendLine();
			}
			else
			{
				if (skinName != "skin")
					sb.AppendFormat("[He] [is] totally bald, showing only shiny {0} {1} where [his] hair should be.", skinColor, skinName);
				else
					sb.AppendFormat("[He] [has] no hair, only a thin layer of fur atop of [his] head.");

				var earDescriptions = new Dictionary<string, string>()
				{
					{ "elfin", " A pair of large pointy ears stick out from [his] skull." },
					{ "genbeast", " A pair of large-ish animal ears have sprouted from the top of [his] head." },
					{ "horse", " A pair of horse-like ears rise up from the top of [his] head." },
					{ "dog", " A pair of dog ears protrude from [his] skull, flopping down adorably." },
					{ "cat", " A pair of cute, fuzzy cat-ears have sprouted from the top of [his] head." },
					{ "cow", " A pair of round, floppy cow ears protrude from the sides of [his] skull." },
					{ "frill", " A set of draconic frills extend from the sides of [his] skull." },
					{ "bunny", " A pair of long bunny ears stand up from [his] skull, flopping down adorably." },
				};
				if (this.HasToken("ears"))
				{
					var earType = "";
					if (!string.IsNullOrWhiteSpace(this.GetToken("ears").Text))
						earType = this.GetToken("ears").Text;
					if (earDescriptions.ContainsKey(earType))
						sb.AppendFormat(earDescriptions[earType]);
				}
				if (this.HasToken("antennae"))
					sb.AppendFormat(" Floppy antennae also appear on [his] skull, bouncing and swaying in the breeze.");
			}
			#endregion

			#region Horns
			if (this.HasToken("horns"))
			{
				if (this.GetToken("horns").HasToken("cow"))
				{
					var hornSize = this.GetToken("horns").Value;
					if (hornSize <= 3)
						sb.AppendFormat("Two tiny horn-like nubs protrude from [his] forehead, resembling the horns of young livestock.");
					else if (hornSize <= 6)
						sb.AppendFormat("Two moderately sized horns grow from [his] forehead, similar in size to those on a young bovine.");
					else if (hornSize <= 12)
						sb.AppendFormat("Two large horns sprout from [his] forehead, curving forwards like those of a bull.");
					else if (hornSize <= 20)
						sb.AppendFormat("Two very large and dangerous looking horns sprout from [his] head, curving forward and over a foot long. They have dangerous looking points.");
					else
						sb.AppendFormat("Two huge horns erupt from [his] forehead, curving outward at first, then forwards. The weight of them is heavy, and they end in dangerous looking points.");
				}
				else
				{
					var numHorns = this.GetToken("horns").Value;
					if (numHorns == 2)
						sb.AppendFormat("A small pair of pointed horns has broken through the [skin] on [his] forehead, proclaiming some demonic taint to any who see them.");
					else if (numHorns == 4)
						sb.AppendFormat("A quartet of prominant horns has broken through [his] [skin]. The back pair are longer, and curve back along [his] head. The front pair protrude forward demonically.");
					else if (numHorns == 6)
						sb.AppendFormat("Six horns have sprouted through [his] [skin], the back two pairs curve backwards over [his] head and down towards [his] neck, while the front two horns stand almost eight inches long upwards and a little forward.");
					else
						sb.AppendFormat("A large number of thick demonic horns sprout through [his] [skin], each pair sprouting behind the ones before.  The front jut forwards nearly ten inches while the rest curve back over [his] head, some of the points ending just below [his] ears.  You estimate [he] [has] a total of {0} horns.", numHorns);
				}
				sb.AppendLine();
			}
			#endregion

			#region Wings
			if (this.HasToken("wings"))
			{
				var wingTypes = new Dictionary<string, string[]>()
					{
						{
							"invalid", new[]
							{
								"A pair of impressive but ill-defined wings sprouts from [his] back. They defy all description because <b>something, somewhere went very wrong.<b>",
								"A pair of small but ill-defined wings sprouts from [his] back, <b>pretty glaringly in error.<b>"
							}
						},
						{
							"insect", new[]
							{
								"A pair of large bee-wings sprouts from [his] back, reflecting the light through their clear membranes beautifully. They flap quickly, allowing [him] to easily hover in place or fly.",
								"A pair of tiny yet beautiful bee-wings sprouts from [his] back, too small to allow [him] to fly."
							}
						},
						{
							"bat", new[]
							{
								"A pair of large demonic bat wings folds behind [his] shoulders.  With a muscle-twitch, [he] can extend them, and use them to handily fly through the air.",
								"A pair of tiny bat-like wings sprouts from [his] back, flapping cutely, but otherwise being of little use."
							}
						},
						{
							"dragon", new[]
							{
								"A pair of large draconic wings extends from behind [his] shoulders, drooping over them like some sort of cape. When a muscle-twitch, [he] can extend them, and use them to soar gracefully through the air.",
								"A pair of tiny dragon-y wings sprouts from [his] back, only there to look cute."
							}
						},
					};
				var wingTypeT = this.GetToken("wings").Tokens.Find(x => x.Name != "small");
				var wingType = wingTypeT == null ? "feather" : wingTypeT.Name;
				if (!wingTypes.ContainsKey(wingType))
					wingType = "invalid";
				sb.AppendFormat(wingTypes[wingType][this.GetToken("wings").HasToken("small") ? 1 : 0]);
				sb.AppendLine();
			}
			#endregion

			//TODO: support certain skintype limitations
			//Possibility: allow skin and fur to count as one.
			#region Tails
			if (this.HasToken("tail"))
			{
				//TODO: buttdescript
				var tail = this.GetToken("tail");
				//var hairColor = Color(this.GetToken("hair").Tokens.Find(x => x.Name != "length").Name);
				var buttDesc = Toolkit.PickOne("butt", "ass", Descriptions.Butt(this.GetToken("ass")));
				if (tail.Text == "spider")
				{
					if (!tail.HasToken("venom"))
						tail.Tokens.Add(new Token() { Name = "venom", Value = 10 });
					sb.AppendFormat("A large spherical spider-abdomen grows out from [his] backside{0}. Though it weighs heavy and bobs with every motion, it doesn't seem to slow [him] down.", "" /* TODO: ", covered in shiny red and black chitin" should be skintyped */);
					if (pa is Player)
					{
						if (tail.GetToken("venom").Value > 50 && tail.GetToken("venom").Value < 80)
							sb.AppendFormat(" [His] bulging arachnid posterior feels fairly full of webbing.");
						else if (tail.GetToken("venom").Value >= 80 && tail.GetToken("venom").Value < 100)
							sb.AppendFormat(" [His] bulbous arachnid rear bulges and feels very full of webbing.");
						else if (tail.GetToken("venom").Value == 100)
							sb.AppendFormat(" [His] swollen spider-butt is distended with the sheer amount of webbing it's holding.");
					}
					else
					{
						if (tail.GetToken("venom").Value >= 80 && tail.GetToken("venom").Value < 100)
							sb.AppendFormat(" [His] bulbous arachnid rear bulges as if full of webbing.");
						else if (tail.GetToken("venom").Value == 100)
							sb.AppendFormat(" [His] swollen spider-butt is distended with the sheer amount of webbing it's holding.");
					}
				}
				else if (tail.Text == "stinger")
				{
					if (!tail.HasToken("venom"))
						tail.Tokens.Add(new Token() { Name = "venom", Value = 10 });
					sb.AppendFormat("A large insectile bee-abdomen dangles from just above [his] backside, bobbing with its own weight. It is {0}tipped with a dagger-like stinger.", "" /* TODO: as above, "covered in hard chitin with black and yellow stripes, " */);
					if (tail.GetToken("venom").Value > 50 && tail.GetToken("venom").Value < 80)
						sb.AppendFormat(" A single drop of poison hangs from [his] exposed stinger.");
					else if (tail.GetToken("venom").Value >= 80 && tail.GetToken("venom").Value < 100)
						sb.AppendFormat(" Poisonous bee venom coats [his] stinger completely.");
					else if (tail.GetToken("venom").Value == 100)
						sb.AppendFormat(" Venom drips from [his] poisoned stinger regularly.");
				}
				else
				{
					var tails = new Dictionary<string, string>()
						{
							//TODO: skintype these, like faces are.
							{ "invalid", "A {0} tail hangs from [his] {2}, <b>but that's all I can tell ya.<b>" },
							{ "stinger", "????" },
							{ "spider", "????" },
							{ "genbeast", "A long {0}-furred tail hangs from [his] {2}." },
							{ "horse", "A long {1} horsetail hangs from [his] {2}, smooth and shiny." }, //use hair color instead
							{ "dog", "A fuzzy {0} dogtail sprouts just above [his] {2}, wagging to and fro whenever [he] [is] happy." },
							{ "squirrel", "A bushy {0} squirrel tail juts out from above [his] {2}, almost as tall as [he] [is], and just as wide." },
							{ "fox", "A fluffy, thick {0} foxtail extends from [his] {2}, tipped white on the end." },
							{ "demon", "A narrow tail ending in a spaded tip curls down from [his] {2}, wrapping around [his] leg sensually at every opportunity." },
							{ "cow", "A long cowtail with a puffy tip swishes back and forth as if swatting at flies." },
							{ "bunny", "A adorable puffball sprouts just above [his] {2}." },
							{ "tentacle", "A thick tentacle extends from [his] {2}, ending in a {3}." },
						};
					var tailT = string.IsNullOrWhiteSpace(tail.Text) ? "genbeast" : tail.Text;
					var tentacleEnd = string.Empty;
					if (tailT == "tentacle")
						tentacleEnd = Descriptions.Tentacle(tail.Tokens[0], stimulation);
					sb.AppendFormat(tails[tailT], skinColor, hairColor, buttDesc, tentacleEnd);
				}
				sb.AppendLine();
			}
			#endregion

			#region Hips, Waist and Butt
			var waist = Descriptions.Waist(this.GetToken("waist"));
			var butt = Descriptions.Butt(this.GetToken("ass"), true);
			sb.AppendLine();
			sb.AppendFormat("[He] [has] {0}{1} and {3}{2}.",
				Descriptions.Hips(this.GetToken("hips")),
				waist != null ? "," + (waist.StartsWithVowel() ? " an " : " a ") + waist + "," : "",
				butt,
				butt.StartsWithVowel() ? "an " : "a ");
			sb.AppendLine();
			#endregion

			#region Legs
			if (this.HasToken("legs"))
			{
				var legs = this.GetToken("legs");
				if (skinName == "slime")
				{
					//Can't have sharp feet as a slime. Is this a thing? Slimes don't have legs!
					if (legs.Text == "stilleto" || legs.Text == "claws" || legs.Text == "insect")
					{
						sb.AppendFormat("(<b>NOTICE<b>: silly leg type specified. Changing to human.) ");
						legs.Text = null;
					}
				}
				if (this.HasToken("quadruped"))
				{
					//Assume horse, accept genbeast, cow or dog legs, mock and reject others.
					if (string.IsNullOrWhiteSpace(legs.Text))
					{
						sb.AppendFormat("(<b>NOTICE<b>: no leg type specified. Assuming horse legs.) ");
						legs.Text = "horse";
					}
					if (legs.Text == "stilleto" || legs.Text == "claws" || legs.Text == "insect")
					{
						sb.AppendFormat("(<b>NOTICE<b>: silly leg type specified. Changing to genbeast.) ");
						legs.Text = "genbest";
					}
					if (legs.Text == "horse" || legs.Text == "cow")
						sb.AppendFormat("Four perfectly reasonable horse legs extend from [his] chest and waist, ending in {0} hooves.", this.HasToken("marshmallow") ? "soft" : "sturdy");
					else if (legs.Text == "genbeast")
						sb.AppendFormat("[He] [has] four digitigrade legs growing downwards from [his] body, ending in beastly paws.");
					else if (legs.Text == "dog")
						sb.AppendFormat("[He] [has] four digitigrade legs growing downwards from [his] chest and waist, ending in dog-like paws.");
				}
				else
				{
					var legTypes = new Dictionary<string, string>()
					{
						{ "human", "Two normal human legs grow down from [his] waist, ending in normal human feet." },
						{ "genbeast", "Two digitigrade legs grow downwards from [his] waist, ending in beastlike hind-paws." },
						{ "cow", "[His] legs are muscled and jointed oddly and end in a pair of {0} hooves." },
						{ "horse", string.Format("[His] legs are muscled and jointed oddly and end in a pair of {0} hooves.", this.HasToken("marshmallow") ? "soft" : "bestial") },
						{ "dog", "Two digitigrade legs grow downwards from [his] waist, ending in dog-like hind-paws." },
						{ "stiletto", "[His] perfect lissom legs end in mostly human feet, apart from the horn protruding straight down from the heel that forces [him] to walk with a sexy, swaying gait." },
						{ "claws", "[His] lithe legs are capped with flexible clawed feet. Sharp black nails grow from the toes, giving [him] fantastic grip." },
						{ "insect", "[His] legs are covered in a shimmering insectile carapace up to mid-thigh, looking more like a pair of 'fuck me' boots than exoskeleton. A bit of downy yellow and black fur fuzzes [his] upper thighs." },
					};
					var legType = string.IsNullOrWhiteSpace(legs.Text) ? "human" : legs.Text;
					if (!legTypes.ContainsKey(legType))
						legType = "human";
					sb.AppendFormat(legTypes[legType]);
					sb.AppendLine();
				}
			}
			else if (this.HasToken("snaketail"))
				sb.AppendFormat("Below [his] waist [his] flesh is fused together into an a very long snake-like tail.");
			else if (this.HasToken("slimeblob"))
				sb.AppendFormat("Below [his] waist is nothing but a shapeless mass of goo, the very top of leg-like shapes just barely recognizable.");
			sb.AppendLine();
			#endregion

			//Pregnancy

			#region Equipment phase 2
			if (visible.Count == 0)
			{
				crotchVisible = true;
				breastsVisible = true;
				sb.AppendLine();
				sb.AppendFormat("[He] [is] wearing nothing at all.");
			}
			else if (visible.Count == 1)
			{
				sb.AppendLine();
				sb.AppendFormat("[He] [is] wearing {0}.", visible[0]);
			}
			else if (visible.Count == 2)
			{
				sb.AppendLine();
				sb.AppendFormat("[He] [is] wearing {0} and {1}.", visible[0], visible[1]);
			}
			else
			{
				sb.AppendLine();
				sb.AppendFormat("[He] [is] wearing {0}", visible[0]);
				for (var i = 1; i < visible.Count; i++)
					sb.AppendFormat("{1}{0}", visible[i], i == visible.Count - 1 ? ", and " : ", ");
				sb.AppendFormat(".");
			}
			sb.Append(' ');
			if (HasToken("noarms") && hands.Count > 0)
			{
				if (hands.Count == 2)
					sb.AppendFormat("(<b>NOTICE<b>: dual wielding with mouth?) ");
				sb.AppendFormat("[He] [has] {0} held between [his] teeth.", hands[0]);
			}
			else
			{
				if (hands.Count == 1)
					sb.AppendFormat("[He] [has] {0} in [his] hands.", hands[0]);
				else if (hands.Count == 1)
					sb.AppendFormat("[He] [has] {0} in [his] hands.", hands[0]);
			}
			sb.AppendLine();
			#endregion

			#region Breasts
			if (this.HasToken("breastrow") && (this.HasToken("noarms") && this.HasToken("legs") && this.GetToken("legs").HasToken("quadruped")))
			{
				sb.AppendFormat("(<b>NOTICE<b>: character has tits but is a full quadruped.)");
				this.Tokens.RemoveAll(x => x.Name == "breastrow");
			}
			if (this.HasToken("breastrow") && breastsVisible)
			{
				sb.AppendLine();
				var numRows = this.Tokens.Count(x => x.Name == "breastrow");
				var totalTits = 0f;
				var totalNipples = 0f;
				var averageTitSize = 0f;
				var averageNippleSize = 0f;
				foreach (var row in this.Tokens.FindAll(x => x.Name == "breastrow"))
				{
					totalTits += row.GetToken("amount").Value;
					totalNipples += row.GetToken("nipples").Value * row.GetToken("amount").Value;
					averageTitSize += row.GetToken("size").Value;
					var nipSize = row.GetToken("nipples").HasToken("size") ? row.GetToken("nipples").GetToken("size").Value : 0.25f;
					averageNippleSize += row.GetToken("nipples").Value * row.GetToken("amount").Value * (row.GetToken("nipples").HasToken("size") ? row.GetToken("nipples").GetToken("size").Value : 1);
				}
				averageTitSize /= totalTits;
				averageNippleSize /= totalNipples;
				if (numRows == 1)
				{
					var nipsPerTit = this.GetToken("breastrow").GetToken("nipples").Value;
					sb.AppendFormat("[He] [has] {0} {1}, each supporting {2} {3} {4}{5}.", Toolkit.Count(totalTits), Descriptions.Breasts(this.GetToken("breastrow")), Toolkit.Count(nipsPerTit), Descriptions.Length(averageNippleSize), Descriptions.Nipples(this.GetToken("breastrow").GetToken("nipples")), nipsPerTit == 1 ? "" : "s");
				}
				else
				{
					//TODO: rewrite this to produce concise info instead of repeating most stuff
					sb.AppendFormat("[He] [has] {0} rows of tits.", Toolkit.Count(numRows));
					var theNth = "The first";
					var rowNum = 0;
					foreach (var row in this.Tokens.FindAll(x => x.Name == "breastrow"))
					{
						rowNum++;
						var nipSize = row.GetToken("nipples").Value * (row.GetToken("nipples").HasToken("size") ? row.GetToken("nipples").GetToken("size").Value : 1);
						sb.AppendFormat(" {0} row has {1} {2}, each with {3} {4}\" {5}{6}.", theNth, Toolkit.Count(row.GetToken("amount").Value), Descriptions.Breasts(row), Toolkit.Count(row.GetToken("nipples").Value), nipSize, Descriptions.Nipples(row.GetToken("nipples")), row.GetToken("nipples").Value == 1 ? "" : "s");
						theNth = (rowNum < numRows - 1) ? "The next" : "The last";
					}
				}
				sb.AppendLine();
			}
			#endregion

			#region Genitalia
			var hasGens = this.HasToken("penis") || this.HasToken("vagina");
			sb.AppendLine();
			if (!hasGens)
				sb.AppendFormat("[He] [has] a curious, total lack of sexual endowments.");
			else if (!crotchVisible)
			{
				//Can't hide a big one, no matter what.
				if (PenisArea() > 50)
					sb.AppendFormat("A large dick is plainly visible beneath [his] clothes.");
				else if (PenisArea() > 20)
				{
					if (stimulation > 50)
						sb.AppendFormat("A large bulge is plainly visible beneath [his] clothes.");
					else if (stimulation > 20)
						sb.AppendFormat("There is a noticable bump beneath [his] clothes.");
				}
				//else, nothing is visible.
			}
			else
			{
				if (this.HasToken("legs") && this.GetToken("legs").HasToken("quadruped"))
					sb.AppendFormat("Between [his] back legs [he] [has] ");
				else if (this.HasToken("snaketail"))
					sb.AppendFormat("[His] crotch is almost featureless, except for a handy, hidden slit wherein [he] [has] hidden ");
				else if (this.HasToken("slimeblob"))
					sb.AppendFormat("Just above the point where [his] body ends and becomes formless, [he] [has] ");
				else
					sb.AppendFormat("Between [his] legs, [he] [has] ");
				if (this.HasToken("penis"))
				{
					//TODO: multicock and different kinds.
					//Don't forget to allow tentacle cocks.
					var cockCount = this.Tokens.Count(x => x.Name == "penis");
					if (cockCount == 1)
					{
						var cock = this.GetToken("penis");
						sb.AppendFormat("a {0} {2}, {1} thick", Descriptions.Length(cock.GetToken("length").Value), Descriptions.Length(cock.GetToken("thickness").Value), Descriptions.Cock(cock));
						if (stimulation > 50)
							sb.AppendFormat(", sticking out and throbbing");
						else if (stimulation > 20)
							sb.AppendFormat(", eagerly standing at attention");
					}
					else
						sb.AppendFormat("a bunch of dicks I'm not gonna try to describe just yet");
				}
				if (this.HasToken("vagina"))
				{
					//TODO: vaginal descriptions.
					//Allow cock-clits?
					if (this.HasToken("penis"))
						sb.AppendFormat(", and ");
					var pussy = this.GetToken("vagina");
					var pussyLoose = Descriptions.Looseness(pussy.GetToken("looseness"));
					var pussyWet = Descriptions.Wetness(pussy.GetToken("wetness"));
					sb.AppendFormat("a{0}{1}{2}vagina",
						(pussyLoose != null ? " " + pussyLoose : ""),
						(pussyLoose != null && pussyWet != null ? ", " : " "),
						(pussyWet != null ? pussyWet + " " : ""));
				}
				sb.AppendFormat(".");
			}
			sb.AppendLine();
			#endregion

#if DEBUG
			#region Debug
			sb.AppendLine();
			sb.AppendLine("<cGray>- Debug -");
			sb.AppendLine("<cGray>Cum amount: " + this.CumAmount() + "mLs.");
			#endregion
#endif

			sb.Replace("[This is]", pa is Player ? "You are" : "This is");
			sb.Replace("[His]", pa is Player ? "Your" : this.HisHerIts());
			sb.Replace("[He]", pa is Player ? "You" : this.HeSheIt());
			sb.Replace("[his]", pa is Player ? "your" : this.HisHerIts(true));
			sb.Replace("[he]", pa is Player ? "you" : this.HeSheIt(true));
			sb.Replace("[him]", pa is Player ? "you" : this.HimHerIt());
			sb.Replace("[is]", pa is Player ? "are" : "is");
			sb.Replace("[has]", pa is Player ? "have" : "has");
			sb.Replace("[does]", pa is Player ? "do" : "does");

			sb.Replace("[skin]", skinName);
			return sb.ToString();
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

		public float PenisArea()
		{
			if (!HasToken("penis"))
				return 0f;
			var area = 0f;
			foreach (var p in Tokens.Where(t => t.Name == "penis"))
				area += PenisArea(p);
			return area;
		}

		public float PenisArea(Token p)
		{
			var area = p.GetToken("thickness").Value * p.GetToken("length").Value;
			if (GetToken("stimulation").Value < 20)
				area /= 2;
			return area;
		}

		public float VaginalCapacity()
		{
			if (!HasVagina())
				return 0f;
			return VaginalCapacity(GetToken("vagina"));
		}

		public float VaginalCapacity(Token v)
		{
			var lut = new[] { 8f, 16f, 24f, 36f, 56f, 100f };
			var l = (int)Math.Ceiling(v.GetToken("looseness").Value);
			if (l > lut.Length)
				l = lut.Length - 1;
			return lut[l];
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

			if (!HasToken("slimeblob") && !HasToken("snaketail") && !HasToken("quadruped"))
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
					if (HasToken("quadruped"))
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
				toChange.Add(source.Path("skin"));
				changeTo.Add(target.Path("skin"));
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

				if (target.HasToken("quadruped") && !source.HasToken("quadruped"))
				{
					Tokens.Add(new Token() { Name = "quadruped" });
					CheckPants();
					if (target.HasToken("marshmallow") && !source.HasToken("marshmallow"))
						source.Tokens.Add(new Token() { Name = "marshmallow" });
					if (target.HasToken("noarms") && !source.HasToken("noarms"))
					{
						doReport((isPlayer ? "You are" : this.Name + " is") + " now a full quadruped.");
						source.Tokens.Add(new Token() { Name = "noarms" });
						//CheckHands();
					}
					else
					{
						doReport((isPlayer ? "You are" : this.Name + " is") + " now a centaur.");
					}
					finish();
					return;
				}
				else if (!target.HasToken("quadruped"))
				{
					RemoveToken("quadruped");
					//Always return arms
					if (source.HasToken("noarms"))
						source.RemoveToken("noarms");
					if (source.HasToken("marshmallow"))
						source.RemoveToken("marshmallow");
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

			changeThis.Name = toThis.Name;
			changeThis.Tokens.Clear();
			changeThis.AddSet(toThis.Tokens);

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
			stream.Write(FirstName);
			stream.Write(Surname);
			stream.Write(Title);
			stream.Write(NameGen);
		}
		public static Name LoadFromFile(BinaryReader stream)
		{
			var newName = new Name();
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
			var tailName = tail.Tokens[0].Name;
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
	}

}
