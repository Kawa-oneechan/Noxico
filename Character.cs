using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Noxico
{
	public enum Gender
	{
		RollDice, Male, Female, Herm, Neuter, Invisible
	}

	public enum MorphReportLevel
	{
		NoReports, PlayerOnly, Anyone
	}

	public enum Stat
	{
		Health, Charisma, Climax, Cunning, Carnality, Stimulation, Sensitivity, Speed, Strength
	}

	public enum TeamBehaviorClass
	{
		Attacking, Flocking
	}
	public enum TeamBehaviorAction
	{
		Nothing, Attack, PreferentialAttack, Avoid, Flock, FlockAlike,
		CloseByAttack = 8, ThiefingPlayer
	}


	public partial class Character : TokenCarrier
	{
		public static List<Token> Bodyplans;
		public static StringBuilder MorphBuffer = new StringBuilder();

		public Name Name { get; set; }
		public BoardChar BoardChar { get; set; }
		public SpeechFilter SpeechFilter { get; set; }

		public Culture Culture
		{
			get
			{
				return (!HasToken("culture")) ? 
					Culture.DefaultCulture : 
					Culture.FindCultureByName(GetToken("culture").Text);
			}
			set
			{
				if (!HasToken("culture")) AddToken("culture");
				GetToken("culture").Text = value.ToString();
			}
		}

		public string Title
		{
			get
			{
				if (!HasToken("title")) AddToken("title").Text = "a";
				return GetToken("title").Text;
			}
			set
			{
				if (!HasToken("title")) AddToken("title").Text = "a";
				GetToken("title").Text = value;
			}
		}

		public bool IsProperNamed
		{
			get
			{
				if (!HasToken("ispropernamed"))
					AddToken("ispropernamed").Text = "false";
				return (GetToken("ispropernamed").Text == "true") ? true : false;
			}
			set
			{
				if (!HasToken("ispropernamed"))
					AddToken("ispropernamed").Text = "false";
				GetToken("ispropernamed").Text = value.ToString().ToLower();
			}
		}

		public string A
		{
			get
			{
				if (!HasToken("a")) AddToken("a").Text = "a";
				return GetToken("a").Text;
			}
			set
			{
				if (!HasToken("a")) AddToken("a").Text = "a";
				GetToken("a").Text = value;
			}
		}

		public float Capacity
		{
			get
			{
				if (!HasToken("capacity")) AddToken("capacity").Value = 0;
				return GetToken("capacity").Value;
			}
			private set
			{
				if (!HasToken("capacity")) AddToken("capacity").Value = 0;
				GetToken("capacity").Value = value;
			}
		}

		public float Carried
		{
			get
			{
				if (!HasToken("carried")) AddToken("carried").Value = 0;
				return GetToken("carried").Value;
			}
			private set
			{
				if (!HasToken("carried")) AddToken("carried").Value = 0;
				GetToken("carried").Value = value;
			}
		}

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
			var g = HasToken("invisiblegender") ? Gender.Invisible : Gender;
			if ((g == Gender.Male && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == Gender.Female && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == Gender.Herm && HasToken("hermonly")))
				g = Gender.Invisible;
			if (IsProperNamed)
				return string.Format("{0}, {1} {3}", Name.ToString(true), A, (g == Gender.Invisible) ? string.Empty : g.ToString().ToLowerInvariant() + ' ', Title);
			return string.Format("{0} {1}", A, Title);
		}

		public string GetKnownName(bool fullName = false, bool appendTitle = false, bool the = false, bool initialCaps = false)
		{
			//TODO: i18n
			if (HasToken("player") || HasToken("special"))
				return Name.ToString(fullName);

			if (HasToken("beast"))
				return string.Format("{0} {1}", initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A), Path("terms/generic").Text);

			var player = NoxicoGame.Me.Player.Character;
			var g = HasToken("invisiblegender") ? Gender.Invisible : Gender;

			//TODO: logic duplicated from UpdateTitle()
			if ((g == Gender.Male && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == Gender.Female && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == Gender.Herm && HasToken("hermonly")))
				g = Gender.Invisible;

			if (player != null && player.Path("ships/" + ID) != null)
			{
				if (appendTitle)
					return string.Format("{0}, {1} {2}{3}",
						Name.ToString(fullName), (the ? "the" : A),
						(g == Gender.Invisible) ? string.Empty : g.ToString().ToLowerInvariant() + ' ',
						Title);
				return Name.ToString(fullName);
			}

			return string.Format("{0} {1}{2}",
				initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A),
				(g == Gender.Invisible) ? string.Empty : g.ToString().ToLowerInvariant() + ' ',
				Title);
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

		public Gender Gender
		{
			get
			{
				if (HasToken("penis") && HasToken("vagina"))
					return Gender.Herm;
				else if (HasToken("penis"))
					return Gender.Male;
				else if (HasToken("vagina"))
					return Gender.Female;
				return Gender.Neuter;
			}
		}

		public Gender BiologicalGender
		{
			get
			{
				return Gender;
			}
		}

		/// <summary>
		/// Returns the character's visible gender according to body parts.
		/// </summary>
		public Gender PercievedGender
		{
			get
			{
				if (HasToken("beast"))
					return Gender.Neuter;

				if (HasToken("player"))
					return (Gender)(PreferredGender + 1);
				//TODO: detect a relationship token and return the preferred gender if known.

				var pants = GetEquippedItemBySlot("pants");
				var underpants = GetEquippedItemBySlot("underpants");
				var pantsCT = (pants == null) ? true : pants.CanSeeThrough(this);
				var underpantsCT = (underpants == null) ? true : underpants.CanSeeThrough(this);
				var crotchVisible = (pantsCT && underpantsCT);

				var dickSize = GetPenisSize(false);
				if (dickSize < 4 && !crotchVisible)
					dickSize = 0; //hide tiny dicks with clothing on.

				var scoreM = 0.0f; // note scores not capped at 1.0
				var scoreF = 0.0f;

				// calibrated using felin min. penis size
				// size 13 or more guarentees masculine looks
				scoreM += dickSize * 0.0425f;

				// calibrated using human min. breast size
				// size 2 or more breaks the feminine looks threshold
				scoreF += GetBreastSize() * 0.51f;

				// visible vagina implies feminine looks
				if (HasToken("vagina") && crotchVisible)
					scoreF += 0.51f;

				// hair > 11 makes for feminine looks - even if you got nothing else
				if (HasToken("hair"))
					scoreF += this.Path("hair/length").Value * 0.046f;

				// TODO: consider hips & waist

				// decide what to return based on quadrants
				// currently not good with flat chested fela and long haired male naga, however,
				// since nagas are not invisible or explit gender, they'll just get called "naga", so that's ok for them

				var decision = Noxico.Gender.Female; // default. never trust floating point
				if (scoreM > 0.5f && scoreF > 0.5f) decision = Gender.Herm;
				if (scoreM < 0.5f && scoreF < 0.5f) decision = Gender.Neuter;
				if (scoreM > 0.5f && scoreF < 0.5f) decision = Gender.Male;
				if (scoreM < 0.5f && scoreF > 0.5f) decision = Gender.Female;

				// felin are invisiblegender and more of a problem.
				// fela get an exeption because I can't think of a better way to do it
				if (decision == Gender.Neuter && HasToken("culture") && GetToken("culture").Text == "felin")
					decision = Gender.Female;

				return decision;
			}
		}

		public Gender PreferredGender
		{
			get
			{
				if (HasToken("preferredgender"))
					return (Gender)((int)GetToken("preferredgender").Value);
				return BiologicalGender; //stopgap
			}
		}

		public void UpdateTitle()
		{
			//TODO: clean up
			//TODO: i18n the lot of this. Could take rewrite cleanup to handle.

			// enums (being ints in disguise) compare better than strings. -- K
			// yeah, I know that, it was like that when I got here -- sparks

			//The ORIGINAL plan:
			// Neither: "felin"
			// Explicit: "male felin"
			// Invisible: "felir"
			// -- K

			var pg = PercievedGender; //cache the value -- K
			Title = null; // start fresh
			if (HasToken("invisiblegender")) // attempt to find a custom term for this race & gender combo eg "felir"
			{
				if (pg == Gender.Male && GetToken("terms").HasToken("male"))
					Title = GetToken("terms").GetToken("male").Text;

				if (pg == Gender.Female && GetToken("terms").HasToken("female"))
					Title = GetToken("terms").GetToken("female").Text;

				if (pg == Gender.Herm && GetToken("terms").HasToken("herm"))
					Title = GetToken("terms").GetToken("herm").Text;

				if (pg == Gender.Neuter && GetToken("terms").HasToken("neuter"))
					Title = GetToken("terms").GetToken("neuter").Text;

				if (Title == null) // fix to catch missing invisiblegenders
					Title = pg.ToString().ToLowerInvariant() + " " + GetToken("terms").GetToken("generic").Text;
			}
			else if (HasToken("explicitgender") || Title == null) // eg "male felin"
			{
				Title = pg.ToString().ToLowerInvariant() + " " + GetToken("terms").GetToken("generic").Text;
			}
			else // just "felin"
			{
				Title = GetToken("terms").GetToken("generic").Text;
			}

			if (HasToken("prefixes")) // add prefixes, 'vorpal', 'dire' etc
			{
				foreach (var prefix in GetToken("prefixes").Tokens)
					Title = prefix.Name + " " + Title;
			}

			A = HasToken("_a") ? GetToken("_a").Text : Title.GetArticle();
		}

		public string HeSheIt(bool lower = false)
		{
			var rets = i18n.GetArray("hesheshiit");
			return lower ? rets[(int)PercievedGender].ToLowerInvariant() : rets[(int)PercievedGender];
		}

		public string HisHerIts(bool lower = false)
		{
			var rets = i18n.GetArray("hisherhirits");
			return lower ? rets[(int)PercievedGender].ToLowerInvariant() : rets[(int)PercievedGender];
		}

		public string HimHerIt(bool lower = false)
		{
			var rets = i18n.GetArray("himherhirit");
			return lower ? rets[(int)PercievedGender].ToLowerInvariant() : rets[(int)PercievedGender];
		}

		public float MaximumHealth
		{
			get
			{
				return GetToken("strength").Value * 2 + 50 + (HasToken("healthbonus") ? GetToken("healthbonus").Value : 0);
			}
		}

		public float Health
		{
			get
			{
				return GetToken("health").Value;
			}
			set
			{
				GetToken("health").Value = Math.Min(MaximumHealth, value);
			}
		}

		public void Heal(float amount)
		{
			Health += amount;
		}

		public Character()
		{
		}

		public void FixBoobs()
		{
			//moved from FixBroken() since FixBoobs() may need to be called from within FixBroken() and it's best to avoid an infinite loop.
			if (!this.HasToken("breasts"))
			{
				var boob = this.AddToken("breasts");
				boob.AddToken("amount", 2);
				boob.AddToken("size", 0);
			}
		}

		public void FixBroken()
		{
			//Fix legs
			if (!this.HasToken("legs") && !this.HasToken("snaketail") && !this.HasToken("slimeblob"))
			{
				if (this.HasToken("oldlegs"))
					this.GetToken("oldlegs").Name = "legs";
				else
					//TODO: Make this determine the proper sort of legs for the character to have and add them.
					/* KAWA SEZ: how 'bout a small lookup mapping faces to legs? If the current face is
					 * not in the list, do the same with skintypes. If that fails, use human legs.
					 */
					//throw new NotImplementedException();
					// for now, you get bestial legs, mutant! :-) -sparks
					this.AddToken("legs").AddToken("genbeast");
			}
			else if ((this.HasToken("snaketail") || this.HasToken("slimeblob")) && this.HasToken("legs"))
			{
				this.GetToken("legs").Name = "oldlegs";
			}
			//Fix hips and waist
			if (!this.HasToken("taur") && !this.HasToken("quadruped"))
			{
				if (!this.HasToken("hips"))
				{
					if (this.HasToken("oldhips"))
						this.GetToken("oldhips").Name = "hips";
					else
						//TODO: Make this add reasonably-sized hips.
						//KAWA SEZ: I have nothing to say about this at this time.
						throw new NotImplementedException();
				}
				if (!this.HasToken("waist"))
				{
					if (this.HasToken("oldwaist"))
						this.GetToken("oldwaist").Name = "waist";
					else
						//TODO: see above.
						throw new NotImplementedException();
				}
			}
			else
			{
				//character does have "taur" or "quadruped" token
				if (this.HasToken("hips"))
					this.GetToken("hips").Name = "oldhips";
				if (this.HasToken("waist"))
					this.GetToken("waist").Name = "oldwaist";
			}

			//fix negative-sized or negative-valued tokens
			List<Token> toRemove = new List<Token>();
			foreach (Token toFix in this.Tokens)
			{
				if ((toFix.HasToken("count") && toFix.GetToken("count").Value <= 0) ||
					(toFix.HasToken("amount") && toFix.GetToken("amount").Value <= 0) ||
					(toFix.HasToken("size") && toFix.GetToken("size").Value < 0) ||
					(toFix.HasToken("sizefromprevious")))
					toRemove.Add(toFix);
			}
			foreach (var t in this.Tokens.Where(t => t.Name == "breastrow"))
			{
				t.Name = "breasts";
			}
			//Remove superfluous genitalia
			if (this.Tokens.Count(t => t.Name == "penis") > 2)
			{
				this.GetToken("penis").AddToken("dual");
				toRemove.AddRange(this.Tokens.Where(t => t.Name == "penis").Skip(1));
			}
			if (this.Tokens.Count(t => t.Name == "vagina") > 2)
			{
				this.GetToken("vagina").AddToken("dual");
				toRemove.AddRange(this.Tokens.Where(t => t.Name == "vagina").Skip(1));
			}
			if (this.Tokens.Count(t => t.Name == "breasts") > 1)
			{
				toRemove.AddRange(this.Tokens.Where(t => t.Name == "breasts").Skip(1));
			}
			foreach (Token removeMe in toRemove)
			{
				this.Tokens.Remove(removeMe);
			}
			this.FixBoobs();
		}

		public static Character GetUnique(string id)
		{
			var uniques = Mix.GetTokenTree("uniques.tml", true);

			var newChar = new Character();
			var planSource = uniques.FirstOrDefault(t => t.Name == "character" && (t.Text == id));
			if (planSource == null)
				throw new FileNotFoundException(string.Format("Could not find a unique bodyplan with id \"{0}\" to generate.", id));
			newChar.AddSet(planSource.Tokens);
			newChar.AddToken("lootset_id", 0, id);
			if (newChar.HasToken("_n"))
				newChar.Name = new Name(newChar.GetToken("_n").Text);
			else
				newChar.Name = new Name(id.Replace('_', ' ').Titlecase());
			newChar.RemoveToken("_n");
			newChar.IsProperNamed = char.IsUpper(newChar.Name.ToString()[0]);

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

			newChar.ResolveMetaTokens();
			newChar.EnsureDefaultTokens();
			newChar.StripInvalidItems();
			newChar.CheckHasteSlow();
			newChar.UpdateTitle();
			newChar.ApplyCostume();
			foreach (var item in newChar.GetToken("items").Tokens)
				item.AddToken("owner", 0, newChar.ID);

			newChar.Culture = Culture.DefaultCulture;
			if (newChar.HasToken("culture"))
			{
				var culture = newChar.GetToken("culture").Text;
				if (Culture.Cultures.ContainsKey(culture))
					newChar.Culture = Culture.Cultures[culture];
			}

			Program.WriteLine("Retrieved unique character {0}.", newChar);
			return newChar;
		}

		private void ResolveMetaTokens()
		{
			while (HasToken("_either"))
			{
				var either = GetToken("_either");
				var eitherChoice = Random.Next(-1, either.Tokens.Count);
				if (eitherChoice > -1)
					AddToken(either.Tokens[eitherChoice]);
				RemoveToken(either);
			}

			var removeThese = new List<Token>();

			foreach (Token token in Tokens)
			{
				if (token.HasToken("_maybe"))
				{
					float value = token.GetToken("_maybe").Value;
					if (value == 0.0f)
						value = 0.5f;
					if (Random.NextDouble() >= value)
						removeThese.Add(token);
					token.RemoveToken("_maybe");
				}
			}

			foreach (Token token in removeThese)
				RemoveToken(token);

			while (HasToken("_copy"))
			{
				string path = GetToken("_copy").Text;
				RemoveToken("_copy");
				var source = Path(path);
				if (source == null)
					continue;
				AddToken(source.Clone(true));
			}

		}

        public static Character Generate(string bodyPlan, Gender bioGender, Gender idGender = Gender.RollDice, Realms world = Realms.Nox)
		{
			var newChar = new Character();
			var planSource = Bodyplans.FirstOrDefault(t => t.Name == "bodyplan" && t.Text == bodyPlan);
			if (planSource == null)
				throw new ArgumentOutOfRangeException(string.Format("Could not find a bodyplan with id \"{0}\" to generate.", bodyPlan));

			newChar.AddSet(planSource.Tokens);
			newChar.Name = new Name();
			
			if (newChar.HasToken("editable"))
				newChar.RemoveToken("editable");

			newChar.HandleSelectTokens(); //by PillowShout
			newChar.ResolveRolls(); // moved rolls to after select, that way we can do rolls within selects

			if (newChar.HasToken("femaleonly"))
				bioGender = Gender.Female;
			else if (newChar.HasToken("maleonly"))
				bioGender = Gender.Male;
			else if (newChar.HasToken("hermonly"))
				bioGender = Gender.Herm;
			else if (newChar.HasToken("neuteronly"))
				bioGender = Gender.Neuter;

			if (bioGender == Gender.RollDice)
			{
				var min = 1;
				var max = 4;
				if (newChar.HasToken("normalgenders"))
					max = 2;
				else if (newChar.HasToken("neverneuter"))
					max = 3;
				var g = Random.Next(min, max + 1);
				bioGender = (Gender)g;
			}

			if (idGender == Gender.RollDice)
				idGender = bioGender;

			if (bioGender != Gender.Female && newChar.HasToken("femaleonly"))
				throw new Exception(string.Format("Cannot generate a non-female {0}.", bodyPlan));
			if (bioGender != Gender.Male && newChar.HasToken("maleonly"))
				throw new Exception(string.Format("Cannot generate a non-male {0}.", bodyPlan));

			if (bioGender == Gender.Male || bioGender == Gender.Neuter)
			{
				newChar.RemoveToken("womb");
				while (newChar.HasToken("vagina"))
					newChar.RemoveToken("vagina");
				foreach (var boob in newChar.Tokens.Where(t => t.Name == "breasts" && t.HasToken("size")))
					boob.GetToken("size").Value = 0;
			}
			if (bioGender == Gender.Female || bioGender == Gender.Neuter)
			{
				while (newChar.HasToken("penis"))
					newChar.RemoveToken("penis");
				newChar.RemoveToken("balls");
			}

			if (newChar.HasToken("snaketail") && newChar.HasToken("legs"))
				newChar.RemoveToken("legs");

			if (!newChar.HasToken("beast"))
			{
				if (newChar.HasToken("namegen"))
				{
					var namegen = newChar.GetToken("namegen").Text;
					if (Culture.NameGens.ContainsKey(namegen))
						newChar.Name.NameGen = namegen;
				}
				if (idGender == Gender.Female)
					newChar.Name.Female = true;
				newChar.Name.Regenerate();
				var patFather = new Name() { NameGen = newChar.Name.NameGen, Female = false };
				var patMother = new Name() { NameGen = newChar.Name.NameGen, Female = true };
				patFather.Regenerate();
				patMother.Regenerate();
				newChar.Name.ResolvePatronym(patFather, patMother);
				newChar.IsProperNamed = true;
			}

            newChar.AddToken("preferredgender", 0, idGender.ToString());
            newChar.EnsureDefaultTokens();
            newChar.UpdateTitle();
            newChar.ApplyCostume();
			foreach (var item in newChar.GetToken("items").Tokens)
				item.AddToken("owner", 0, newChar.ID);

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
				var chosen = prefixes.PickOne();
				if (!newChar.HasToken("infectswith"))
					while (chosen == "infectious")
						chosen = prefixes.PickOne();
				newChar.UpdateTitle();
			}

			if (newChar.HasToken("femalesmaller"))
			{
				if (bioGender == Gender.Female)
					newChar.GetToken("tallness").Value -= Random.Next(5, 10);
				else if (bioGender == Gender.Herm)
					newChar.GetToken("tallness").Value -= Random.Next(1, 6);
			}

			//Prevent a semi-common generation bug from triggering in LookAt.
			if (newChar.Path("skin/pattern") != null && newChar.Path("skin/pattern").Text.IsBlank())
				newChar.GetToken("skin").RemoveToken("pattern");

			newChar.ResolveMetaTokens();
            newChar.StripInvalidItems();
			newChar.CheckHasteSlow();

/* Disabled for now pending Mutate rewrite.
			// because: "why the hell did I pick a male human and get herm centaur?"
			if (!newChar.HasToken("beast") && !newChar.HasToken("player") && world == Realms.Seradevari) 
                newChar.Mutate(2, 20);
*/

			return newChar;
		}

		private void EnsureDefaultTokens()
		{
			var metaTokens = new[] { "playable", "femalesmaller", "costume", "neverneuter", "hermonly", "maleonly", "femaleonly" };
			foreach (var t in metaTokens)
				this.RemoveAll(t);
			if (!this.HasToken("beast"))
				this.RemoveAll("bestiary");

			if (this.HasToken("ass"))
			{
				if (this.Path("ass/looseness") == null)
					this.GetToken("ass").AddToken("looseness");
				if (this.Path("ass/wetness") == null)
					this.GetToken("ass").AddToken("wetness");
			}

			var prefabTokens = new[]
			{
				"items", "health", "perks", "skills", "sexpreference",
				"charisma", "climax", "cunning", "carnality",
				"stimulation", "sensitivity", "speed", "strength",
				"money", "ships", "paragon", "renegade", 
				"charismabonus", "climaxbonus", "cunningbonus", "carnalitybonus",
				"stimulationbonus", "sensitivitybonus", "speedbonus", "strengthbonus",
			};
			var prefabTokenValues = new[]
			{
				0, 10, 0, 0, (Random.Flip() ? 2 : Random.Next(0, 3)),
				10, 0, 10, 0,
				10, 10, 10, 15,
				100, 0, 0, 0,
				0, 0, 0, 0,
				0, 0, 0, 0,
			};

			for (var i = 0; i < prefabTokens.Length; i++)
				if (!HasToken(prefabTokens[i]))
					AddToken(prefabTokens[i], prefabTokenValues[i]);

			Health = MaximumHealth;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			Toolkit.SaveExpectation(stream, "CHAR");
			Name.SaveToFile(stream);
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
			/* newChar.IsProperNamed = */ stream.ReadBoolean();
			/* newChar.A = */ stream.ReadString();
			/* var culture = */ stream.ReadString();
			/* newChar.Culture = Culture.DefaultCulture;
			if (Culture.Cultures.ContainsKey(culture))
				newChar.Culture = Culture.Cultures[culture]; */
			Toolkit.ExpectFromFile(stream, "TOKS", "character token tree");
			var numTokens = stream.ReadInt32();
			for (var i = 0; i < numTokens; i++)
				newChar.Tokens.Add(Token.LoadFromFile(stream));
			newChar.UpdateTitle();

			//Fix the results of a bug that caused multiple a, ispropernamed, and culture tokens to appear, namely the above-commented.
			//TODO: bump world version, remove that shit.
			var a = false;
			var p = false;
			var c = false;
			foreach (var token in newChar.Tokens)
			{
				if (token.Name == "a")
				{
					if (a)
						token.Name = "__kill";
					a = true;
				}
				else if (token.Name == "ispropernamed")
				{
					if (p)
						token.Name = "__kill";
					p = true;
				}
				else if (token.Name == "culture")
				{
					if (c)
						token.Name = "__kill";
					c = true;
				}
			}
			newChar.Tokens.RemoveAll(t => t.Name == "__kill");

			return newChar;
		}

		public void ApplyCostume()
		{
			if (HasToken("costume"))
				RemoveToken("costume");
			if (HasToken("beast"))
				return;
			if (!HasToken("lootset_id"))
				AddToken("lootset_id", 0, ID.ToLowerInvariant());
			var filters = new Dictionary<string, string>
			{
				{ "gender", PreferredGender.ToString().ToLowerInvariant() },
				{ "board", Board.HackishBoardTypeThing },
				{ "culture", this.HasToken("culture") ? this.GetToken("culture").Text : string.Empty },
				{ "name", this.Name.ToString(true) },
				{ "id", this.GetToken("lootset_id").Text },
				{ "bodymatch", this.GetClosestBodyplanMatch() },
				{ "biome", BiomeData.Biomes[DungeonGenerator.DungeonGeneratorBiome].Name.ToLowerInvariant() } //AcetheSuperVillain suggests a biome key.
			};
			var inventory = this.GetToken("items");
			var clothing = new List<Token>();
			clothing.AddRange(DungeonGenerator.GetRandomLoot("npc", "underwear", filters));
			clothing.AddRange(DungeonGenerator.GetRandomLoot("npc", "clothing", filters));
			clothing.AddRange(DungeonGenerator.GetRandomLoot("npc", "accessories", filters));
			var check = new Func<Token, bool>(x =>
			{
				var ki = NoxicoGame.KnownItems.FirstOrDefault(i => i.ID == x.Name);
				return ki != null;
			});
			if (HasToken("taur") || HasToken("quadruped"))
				check = new Func<Token, bool>(x =>
				{
					var ki = NoxicoGame.KnownItems.FirstOrDefault(i => i.ID == x.Name);
					if (ki == null)
						return false;
					if (ki.Path("equipable/underpants") != null)
						return ki.Path("equipable/undershirt") != null;
					if (ki.Path("equipable/pants") != null)
						return ki.Path("equipable/shirt") != null;
					return true;
				});
			if (HasToken("snaketail"))
				check = new Func<Token, bool>(x =>
				{
					var ki = NoxicoGame.KnownItems.FirstOrDefault(i => i.ID == x.Name);
					if (ki == null)
						return false;
					if (ki.Path("equipable/socks") != null || ki.Path("equipable/shoes") != null)
						return false;
					if ((ki.Path("equipable/pants") != null || ki.Path("equipable/underpants") != null) && !ki.HasToken("nolegs"))
						return false;
					return true;
				});
			foreach (var item in clothing)
			{
				if (check(item))
					inventory.AddToken(item).AddToken("equipped");
			}
			var armedOne = false;
			foreach (var item in DungeonGenerator.GetRandomLoot("npc", "arms", filters))
			{
				var arm = inventory.AddToken(item);
				if (!armedOne)
				{
					armedOne = true;
					arm.AddToken("equipped");
				}
			}
			foreach (var item in DungeonGenerator.GetRandomLoot("npc", "food", filters))
				inventory.AddToken(item);

			this.RemoveToken("lootset_id");
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
				Program.WriteLine("Had to remove {0} inventory item(s) from {1}: {2}", toDelete.Count, Name, toDelete.Join());
				GetToken("items").RemoveSet(toDelete);
			}
		}

		public void AddSet(List<Token> otherSet)
		{
			foreach (var toAdd in otherSet)
			{
				var newToken = new Token(toAdd.Name, toAdd.Value, toAdd.Text);
				if (toAdd.Tokens.Count > 0)
					newToken.AddSet(toAdd.Tokens);
				this.Tokens.Add(newToken);
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

		public float MilkAmount
		{
			get
			{
				var size = GetBreastSize();
				var amount = GetBreastAmount();
				if (amount == 0)
					return 0;
				var effectiveAmount = size * amount;
				if (this.GetToken("breasts").HasToken("lactation"))
					effectiveAmount *= 5;
				if (GetToken("perks").HasToken("messyorgasms"))
					effectiveAmount *= 1.5f;
				return effectiveAmount;
			}
		}

		private static void Columnize(Action<string> print, List<string> col1, List<string> col2, string header1, string header2)
		{
			var pad = 36;
			var totalRows = Math.Max(col1.Count, col2.Count);
			print(i18n.GetString(header1).PadEffective(pad) + i18n.GetString(header2) + "\n");
			for (var i = 0; i < totalRows; i++)
			{
				if (i < col1.Count)
					print(((i < col1.Count - 1 ? "\xC3 " : "\xC0 ") + (i18n.GetString(col1[i], false)).ToLowerInvariant()).PadEffective(pad));
				else
					print(string.Empty.PadEffective(pad));
				if (i < col2.Count)
					print((i < col2.Count - 1 ? "\xC3 " : "\xC0 ") + (i18n.GetString(col2[i], false).ToLowerInvariant()));
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
			InventoryItem socks = null;
			InventoryItem jacket = null;
			InventoryItem cloak = null;
			InventoryItem shoes = null;
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
					print("Can't handle " + carriedItem.Name + ".\n"); //DO NOT TRANSLATE
					continue;
				}

				if (foundItem.HasToken("equipable") && carriedItem.HasToken("equipped"))
				{
					var eq = foundItem.GetToken("equipable");
					if (eq.HasToken("underpants"))
					{
						underpants = foundItem;
						underpants.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("undershirt"))
					{
						undershirt = foundItem;
						undershirt.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("socks"))
					{
						socks = foundItem;
						socks.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("pants"))
					{
						pants = foundItem;
						pants.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("shirt"))
					{
						shirt = foundItem;
						shirt.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("jacket"))
					{
						jacket = foundItem;
						jacket.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("cloak"))
					{
						cloak = foundItem;
						cloak.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("shoes"))
					{
						shoes = foundItem;
						shoes.tempToken[this.ID] = carriedItem;
					} 
					if (eq.HasToken("hat"))
					{
						hat = foundItem;
						hat.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("goggles"))
					{
						goggles = foundItem;
						goggles.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("mask"))
					{
						mask = foundItem;
						mask.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("neck"))
					{
						neck = foundItem;
						neck.tempToken[this.ID] = carriedItem;
					}
					if (eq.HasToken("ring"))
					{
						foundItem.tempToken[this.ID] = carriedItem;
						fingers.Add(foundItem);
					}
					if (eq.HasToken("hand"))
					{
						foundItem.tempToken[this.ID] = carriedItem;
						hands.Add(foundItem);
					}
				}
				else
				{
					carried.Add(foundItem);
				}
			}

			if (hat != null)
				worn.Add(hat.ToLongString(hat.tempToken[this.ID]));
			if (goggles != null)
				worn.Add(goggles.ToLongString(goggles.tempToken[this.ID]));
			if (mask != null)
				worn.Add(mask.ToLongString(mask.tempToken[this.ID]));
			if (neck != null)
				worn.Add(neck.ToLongString(neck.tempToken[this.ID]));
			if (cloak != null)
				worn.Add(cloak.ToLongString(cloak.tempToken[this.ID]));
			if (jacket != null)
				worn.Add(jacket.ToLongString(jacket.tempToken[this.ID]));
			if (shirt != null)
				worn.Add(shirt.ToLongString(shirt.tempToken[this.ID]));
			if (pants != null && pants != shirt)
				worn.Add(pants.ToLongString(pants.tempToken[this.ID]));
			if (shoes != null)
				worn.Add(shoes.ToLongString(shoes.tempToken[this.ID]));
			if (!(pa != null && pa is Player))
			{
				if (undershirt != null && (shirt == null || shirt.CanSeeThrough(this)))
				{
					breastsVisible = undershirt.CanSeeThrough(this);
					worn.Add(undershirt.ToLongString(undershirt.tempToken[this.ID]));
				}
				else
					breastsVisible = (shirt == null || shirt.CanSeeThrough(this));
				if (underpants != null && underpants != undershirt && (pants == null || pants.CanSeeThrough(this)))
				{
					crotchVisible = underpants.CanSeeThrough(this);
					worn.Add(underpants.ToLongString(underpants.tempToken[this.ID]));
				}
				else
					crotchVisible = (pants == null || pants.CanSeeThrough(this));
				if (socks != null && (pants == null || pants.CanSeeThrough(this)))
					worn.Add(socks.ToLongString(socks.tempToken[this.ID]));
			}
			else
			{
				if (undershirt != null)
					worn.Add(undershirt.ToLongString(undershirt.tempToken[this.ID]));
				if (underpants != null && underpants != undershirt)
					worn.Add(underpants.ToLongString(underpants.tempToken[this.ID]));
				if (socks != null)
					worn.Add(socks.ToLongString(socks.tempToken[this.ID]));
				crotchVisible = breastsVisible = true;
			}
		}

		private void LookAtBodyFace(Entity pa, Action<string> print)
		{
			print(i18n.GetString("lookat_header_general"));

			var bodyThings = new List<string>();
			var headThings = new List<string>();

			var legLength = this.GetToken("tallness").Value * 0.53f;
			var goggles = this.GetEquippedItemBySlot("goggles");
			var mask = this.GetEquippedItemBySlot("mask");

			if (this.HasToken("slimeblob"))
			{
				bodyThings.Add("amorphous blob");
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value - (legLength * 0.75f)) + " tall");
			}
			else if (this.HasToken("snaketail"))
			{
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value) + " tall");
				bodyThings.Add("snake tail");
				//add legLength over again to increase length; nagas are longer than most!
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value + (legLength * 2)) + " long");
			}
			else
				bodyThings.Add(Descriptions.Length(this.GetToken("tallness").Value) + " tall");

			bodyThings.Add(i18n.Format("x_skin", Color.Translate(Color.NameColor(this.Path("skin/color").Text)), i18n.GetString(this.Path("skin/type").Text, false)));
			if (this.Path("skin/pattern") != null)
				bodyThings.Add(i18n.Format("x_pattern", Color.Translate(Color.NameColor(this.Path("skin/pattern/color").Text)), i18n.GetString(this.Path("skin/pattern").Text, false)));

			if (this.HasToken("legs"))
			{
				var lt = this.GetToken("legs").Text;
				var legs = lt.IsBlank("human", lt);
				var count = 0;
				var numberOrPair = "counts";
				if (this.GetToken("legs").GetToken("amount") != null)
					count = (int)this.GetToken("legs").GetToken("amount").Value;
				else
					count = 2;
				if (this.HasToken("quadruped") || this.HasToken("taur"))
				{
					count = 4;
					if (this.HasToken("taur"))
					{
						var taur = (int)this.GetToken("taur").Value;
						if (taur == 0)
							taur = 1;
						else if (taur > 1)
							count = 2 + (taur * 2);
					}
				}
				if (count < 6)
					numberOrPair = "setbymeasure";
				bodyThings.Add(i18n.GetArray(numberOrPair)[count] + " " + i18n.Format("x_legs", i18n.GetString("legtype_" + legs)));
				if (this.HasToken("quadruped"))
					bodyThings.Add("quadruped");
				else if (this.HasToken("taur"))
				{
					var taur = (int)this.GetToken("taur").Value + 1;
					if (taur < 2)
						bodyThings.Add(i18n.GetString("single_taur"));
					else if (taur > 1)
						bodyThings.Add(i18n.Format("multi_taur", taur + 1));
				}
			}

			if (this.HasToken("wings"))
			{
				var wingType = this.GetToken("wings").Text;
				if (wingType.IsBlank())
					wingType = "feather"; //TODO: different "undefined" fallback?
				var wt = i18n.Format("x_wings", i18n.GetString("wingtype_" + wingType));
				if (this.Path("wings/small") != null)
					wt = i18n.Format("small_wings", wt);
				bodyThings.Add(wt);
			}

			//tone


			var faceType = this.GetToken("face").Text;
			if (new[] { "normal", "genbeast", "cow", "reptile" }.Contains(faceType))
				faceType = i18n.GetString("facetype_" + faceType);
			else if (faceType == "normal")
				faceType = "human";
			else if (faceType == "genbeast")
				faceType = "beastly";
			else if (faceType == "cow")
				faceType = "bovine";
			else if (faceType == "reptile")
				faceType = "reptilian";
			else
				faceType = i18n.Format("face_xlike", faceType);
			headThings.Add(i18n.Format("x_face", faceType));

			if (this.HasToken("eyes"))
			{
				var eyes = i18n.Format("color_eyes", Color.Translate(Color.NameColor(this.GetToken("eyes").Text)), "eyes");
				var eyesHidden = false;
				var count = 2;
				if (this.Path("eyes/count") != null)
				{
					count = (int)this.GetToken("eyes").GetToken("count").Value;
					eyes = i18n.Format("color_eyes", Color.Translate(Color.NameColor(this.GetToken("eyes").Text)), i18n.Pluralize("eye", count));
				}
				if (goggles != null && !goggles.CanSeeThrough(this))
					eyesHidden = true;
				if (this.Path("eyes/glow") != null)
				{
					eyes = i18n.Format("glowing_x", eyes);
					eyesHidden = false;
				}
				eyes = (count <= 12 ? i18n.GetArray("setbymeasure")[count] : count.ToString()) + " " + eyes;
				if (!eyesHidden)
					headThings.Add(eyes);
			}

			if (mask != null && mask.CanSeeThrough(this))
			{
				var teeth = this.Path("teeth");
				if (teeth != null && !teeth.Text.IsBlank() && teeth.Text != "normal")
					headThings.Add(i18n.GetString("teethtype_" + teeth.Text));
				var tongue = this.Path("tongue");
				if (tongue != null && !tongue.Text.IsBlank() && tongue.Text != "normal")
					headThings.Add(i18n.GetString("tonguetype_" + teeth.Text));
			}

			var ears = "human";
			if (this.HasToken("ears"))
				ears = this.GetToken("ears").Text;
			if (ears != "human")
				headThings.Add(i18n.GetString("eartype_" + ears));

			//femininity slider

			//Columnize it!
			Columnize(print, bodyThings, headThings, "lookat_column_body", "lookat_column_head");
		}

		private void LookAtHairHips(Entity pa, Action<string> print)
		{
			//TODO: i18n
			var hairThings = new List<string>();
			var hipThings = new List<string>();
			if (this.HasToken("hair") && this.Path("hair/length").Value > 0)
			{
				var hair = this.GetToken("hair");
				hairThings.Add(Descriptions.HairLength(hair));
				if (this.Path("skin/type").Text != "slime")
					hairThings.Add(Descriptions.HairColor(hair));
				if (this.Path("hair/style") != null)
					hairThings.Add(Descriptions.HairStyle(hair));
				if (this.Path("skin/type").Text == "slime")
					hairThings.Add("goopy");
				if (this.Path("skin/type").Text == "rubber")
					hairThings.Add("rubbery");
				if (this.Path("skin/type").Text == "metal")
					hairThings.Add("cord-like");
			}

			if (this.HasToken("monoceros"))
				hairThings.Add(i18n.GetString("horntype_monoceros"));			
			if (this.HasToken("horns") && this.Path("horns").Value > 0)
			{
				var count = GetToken("horns").Value;
				Token horns = GetToken("horns");
				string size = horns.HasToken("big") ? "big" : "small";
				string style = horns.HasToken("curled") ? "curled" : "straight";
				string horntype = "x_horntype_" + size + "_" + style;
				string pairs = (count == 1) ? "pair" : "pairs"; // there is probably a better way to do this - sparks
				hairThings.Add(i18n.Format(horntype, count, pairs));
			}

			if (!(HasToken("quadruped") || HasToken("taur")))
			{
				hipThings.Add(Descriptions.HipSize(this.GetToken("hips")) + " hips");
				hipThings.Add(Descriptions.WaistSize(this.GetToken("waist")) + " waist");
				hipThings.Add(Descriptions.ButtSize(this.GetToken("ass")) + " ass");
			}
			else
			{
				//hipThings.Add("quadruped");
			}

			if (this.HasToken("tail"))
			{
				var tt = this.GetToken("tail").Text;
				var tail = tt.IsBlank("genbeast", tt);
				if (tail == "bunny")
					hipThings.Add(i18n.GetString("tailtype_bunny"));
				else if (tail == "webber")
					hipThings.Add(i18n.GetString("tailtyle_webber"));
				else if (tail == "tentacle")
				{
					var tentail = this.Path("tail/tip");
					if (tentail == null || tentail.Text == "tapered")
						hipThings.Add(i18n.GetString("tailtype_tapered_tentacle"));
					else if (tentail.Text == "penis")
						hipThings.Add(i18n.GetString("tailtype_cocktacle"));
					else
						hipThings.Add(i18n.GetString("tailtype_tentacle"));
				}
				else
					hipThings.Add(i18n.Format("x_tail", i18n.GetString("tailtype_" + tail)));
			}

			//cutie mark crusaders YAY!!!
			foreach (var tat in new[] { "hip", "smallofback", "buttcheek" })
			{
				var tatTok = this.Path("tattoos/" + tat);
				if (tatTok != null)
					hipThings.Add(i18n.Format("tat_x_on_y", tatTok.Text, i18n.GetString("tatlocation_" + tat)));
			}

			Columnize(print, hairThings, hipThings, "lookat_column_hair", "lookat_column_hips");
		}

		private void LookAtSexual(Entity pa, Action<string> print, bool breastsVisible, bool crotchVisible)
		{
			print(i18n.GetString("lookat_header_sexual"));
			//TODO: i18n
			Token cock = this.GetToken("penis");
			Token vagina = this.GetToken("vagina");
			Token breasts = this.GetToken("breasts");
			Token nuts = this.GetToken("balls");
			var ballCount = 0;
			var ballSize = 0.25f;
			//var slit = this.HasToken("snaketail");
			//var aroused = stimulation > 50;
			if (nuts != null)
			{
				ballCount = nuts.HasToken("amount") ? (int)nuts.GetToken("amount").Value : 2;
				ballSize = nuts.HasToken("size") ? nuts.GetToken("size").Value : 0.25f;
			}
			
			print("Breasts: ");
			if (breasts == null)
				print("none\n");
			else
			{
				print("\n");
				var boob = GetToken("breasts");
				//if (HasToken("quadruped") && GetBreastRowSize(i) < 0.5)
				//	continue;
				print("\xC0 " + Toolkit.Count(boob.GetToken("amount").Value) + " " + Descriptions.GetSizeDescription("breasts/size", GetBreastSize()));
				if (breastsVisible && (boob.Path("nipples") == null || boob.Path("nipples").Value == 0))
					print(" nippleless");
				print(" breast");
				if (boob.GetToken("amount").Value > 1)
					print("s");
				if (!breastsVisible || (boob.Path("nipples") == null || boob.Path("nipples").Value == 0))
					print("\n");

				if (!(boob.Path("nipples") == null) && !(boob.Path("nipples").Value == 0))
				{
					var nipSize = 0.5f;
					if (boob.Path("nipples/size") != null)
						nipSize = boob.Path("nipples/size").Value;
					var nipType = Descriptions.NippleSize(boob.GetToken("nipples")) + " " + Descriptions.Length(nipSize);
					if (boob.Path("nipples/canfuck") != null)
						nipType += " " + "dicknipple".Pluralize((int)boob.GetToken("nipples").Value);
					else if (boob.Path("nipples/fuckable") != null)
					{
						var loose = Descriptions.Looseness(boob.Path("nipples/looseness"), false);
						var wet = Descriptions.Wetness(boob.Path("nipples/wetness"));
						if (wet != null && loose != null)
							wet = " and " + wet;
						else if (wet == null && loose == null)
							loose = "";
						nipType += (" " + loose + wet + " " + "nipplecunt".Pluralize((int)boob.GetToken("nipples").Value)).Trim();
					}
					else
						nipType += " [?:" + "nipple".Pluralize((int)boob.GetToken("nipples").Value) + "]";
					print(", " + Toolkit.Count(boob.GetToken("nipples").Value) + " " + nipType);
					print(" on each\n");
				}
			}
			print("\n");

			print("Genitals: ");
			if (!crotchVisible)
			{
				if (this.PercievedGender == Gender.Male)
					print("a [?:cock]?\n");
				else if (this.PercievedGender == Gender.Female)
					print("a [?:pussy]?\n");
				else
					print("can't tell!\n");
			}
			else
			{
				print("\n");
				if (vagina != null)
				{
					//TODO: allow dual vaginas and cocks
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
					print((cock == null && ballCount == 0 ? "\xC0 " : "\xC3 ") + (vagina.HasToken("dual") ? "a split, " : "a ") + loose + wet + " [?:pussy], with a " + Descriptions.Length(clitSize) + " [?:clit]\n");
				}

				if (cock != null)
				{
					var cockType = cock.Text.IsBlank("human", cock.Text);
					print((ballCount == 0 ? "\xC0 " : "\xC3 ") + (cock.HasToken("dual") ? "a split, " : "a ") + cockType + " [?:cock], " + Descriptions.Length(cock.GetToken("length").Value) + " long, ");
					print(Descriptions.Length(cock.GetToken("thickness").Value) + " thick\n");
				}
				if (ballCount > 0)
				{
					print("\xC0 " + Toolkit.Count(ballCount) + " " + (ballSize < 1 ? "" : Descriptions.BallSize(nuts) + " ") + "testicle".Pluralize(ballCount) + "\n");
				}
				//TODO: handle assholes?
			}
			print("\n");
		}

		private void LookAtClothing(Entity pa, Action<string> print, List<string> worn)
		{
			print(i18n.GetString("lookat_header_items")); 
			print(i18n.GetString("lookat_column_clothing"));
			if (worn.Count == 0)
				print("\xC0 " + i18n.GetString("none") + "\n");
			else
				for (var i = 0; i < worn.Count; i++)
					print((i < worn.Count - 1 ? "\xC3 " : "\xC0 ") + worn[i] + "\n");
			print("\n");
		}

		private void LookAtEquipment2(Entity pa, Action<string> print, List<InventoryItem> hands, List<InventoryItem> fingers)
		{
			print(i18n.GetString("lookat_column_equipment"));
			var mono = HasToken("monoceros") ? 1 : 0;
			if (this.HasToken("noarms") && hands.Count > 1 + mono)
				print("NOTICE: dual wielding with mouth.\n");
			if (hands.Count > 2 + mono)
				print("NOTICE: Shiva called.\n");
			if (hands.Count + fingers.Count == 0)
				print("\xC0 " + i18n.GetString("none") + "\n");
			else
			{
				var handsAndFingers = new List<string>();
				handsAndFingers.AddRange(hands.Select(x => x.ToLongString(x.tempToken[this.ID])));
				handsAndFingers.AddRange(fingers.Select(x => x.ToLongString(x.tempToken[this.ID])));
				for (var i = 0; i < handsAndFingers.Count; i++)
					print((i < handsAndFingers.Count - 1 ? "\xC3 " : "\xC0 ") + handsAndFingers[i] + "\n");
			}
			print("\n");
		}
		#endregion

		public string LookAt(Entity pa, Action<string> print = null)
		{
			if (this.HasToken("beast"))
				return this.GetToken("bestiary").Text;

			var sb = new StringBuilder();

			//Things not listed: horns and wings.
			if (print == null)
				print = new Action<string>(x => sb.Append(x.Viewpoint(null)));

			//var stimulation = this.GetToken("stimulation").Value;

			var player = NoxicoGame.Me.Player == null ? null : NoxicoGame.Me.Player.Character;
			if (pa is Player || (player != null && player.Path("ships/" + ID) != null))
				print(this.GetKnownName(true) + ", " + this.Title + "\n\n");
			else
				print(this.Title + "\n\n");

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
			print(string.Format(@"
Debug
-------
Hash: {0} (closest match: {1})
Article: {2}
Biological gender: {3} (mirror of gender property)
Breasts: {24} @ {25}
Carry capacity: {5} of {4}
Culture: {6}
Cum amount: {7}
Gender: {8} (determined from genitalia)
Health: {9} of {15}
ID: {10}
Is proper named: {11}
Is slime: {12}
Likes boys: {13} (derived from sexpreference token)
Likes girls: {14} (derived from sexpreference token)
Milk amount: {16}
Name: {17}
Penis (length only): {26}
Penis (length * thickness): {27}
Percieved gender: {18} (what they seem to be)
Preferred gender: {19} (what to call them)
Spouse: {20}
Team: {21}
Title: {22}
Vaginal capacity: {28}

Tokens:
{23}",
				   this.GetBodyComparisonHash(),
				   GetClosestBodyplanMatch(),
				   this.A, this.BiologicalGender, this.Capacity, this.Carried, this.Culture,
				   this.CumAmount, this.Gender, this.Health, this.ID, this.IsProperNamed,
				   this.IsSlime, this.LikesBoys, this.LikesGirls, this.MaximumHealth,
				   this.MilkAmount, this.Name, this.PercievedGender, this.PreferredGender,
				   this.Spouse == null ? "noone" : this.Spouse.ToString(),
				   this.Team, this.Title, DumpTokens(this.Tokens, 1).Replace("\t", "  "),
				   this.GetBreastAmount(), this.GetBreastSize(),
				   this.GetPenisSize(false), this.GetPenisSize(true),
				   this.GetVaginaCapacity()
				   ));
			#endif
			#if LOLDEBUG
			print("\n\n\n\n");
			print("<cGray>Debug\n<cGray>\xc4\xc4\xc4\xc4\xc4\n");
			print("<cGray>Percieved gender: " + this.PercievedGender.ToString() + "\n");
			print("<cGray>Actual gender: " + this.ActualGender.ToString() + "\n");
			print("<cGray>Self-preferred gender: " + this.PreferredGender.ToString() + "\n");
			print("<cGray>Cum amount: " + this.CumAmount + "mLs.\n");
			print("<cGray>Breasts: " + this.GetBreastAmount() + " @ " + this.GetBreastSize() + "'\n");
			print("<cGray>Penis (length only): " + this.GetPenisSize(false) + "cm\n");
			print("<cGray>Penis (l * t): " + this.GetPenisSize(true) + "cm\n");
			print("<cGray>Vagina capacity: " + this.GetVaginaCapacity() + "\n");
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
			dump.WriteLine("<title>{0}</title>", i18n.Format("infodump_title", this.Name.ToString(true)));
			dump.WriteLine("<meta http-equiv=\"Content-Type\" content=\"text/html; CHARSET=utf-8\" />");
			dump.WriteLine("</head>");
			dump.WriteLine("<body>");
			dump.WriteLine("<h1>{0}</h1>", i18n.Format("infodump_title", this.Name.ToString(true)));

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_screenshot"));
			NoxicoGame.Me.CurrentBoard.CreateHtmlScreenshot(dump, false);

			foreach (var carriedItem in GetToken("items").Tokens)
			{
				carriedItem.RemoveToken("equipped");
			}

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_about"));
			dump.WriteLine("<pre>");
			Action<string> print = new Action<string>(x =>
			{
				if (x.Contains(' ' + i18n.GetString("none") + '\n') || x.Contains(i18n.GetString("lookat_column_clothing")) || x.Contains(i18n.GetString("lookat_header_items")))
					return;
				x = x.Replace("\xC3", "&#x251C;").Replace("\xC0", "&#x2514;").Replace("&#xc4;", "&#x2500;");
				dump.Write(x.Viewpoint(null));
			});
			var lookAt = LookAt(null, print);
			lookAt = lookAt.Replace("\n\n", "\n");
			dump.WriteLine(lookAt);
			dump.WriteLine("</pre>");

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_items"));
			dump.WriteLine("<ul>");
			if (GetToken("items").Tokens.Count == 0)
				dump.WriteLine("<li>{0}</li>", i18n.GetString("infodump_no_items"));
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

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_relationships"));
			dump.WriteLine("<ul>");
			var victims = 0;
			var lovers = 0;
			//var deities = 0;
			if (GetToken("ships").Tokens.Where(t => !t.HasToken("player")).Count() == 0)
				dump.WriteLine("<li>{0}</li>", i18n.GetString("infodump_no_ships"));
			else
			{
				list.Clear();
				foreach (var person in GetToken("ships").Tokens.Where(t => !t.HasToken("player")))
				{
					var reparsed = person.Name.Replace('_', ' ');
					if (reparsed.StartsWith('\xF4EF'))
						reparsed = reparsed.Remove(reparsed.IndexOf('#')).Substring(1);
					list.Add(reparsed + " &mdash; " + person.Tokens.Select(x => x.Name).Join());
					if (person.HasToken("victim"))
						victims++;
					if (person.HasToken("lover"))
						lovers++;
					//if (person.HasToken("prayer"))
					//	deities++;
				}
				list.Sort();
				list.ForEach(x => dump.WriteLine("<li>{0}</li>", x));
			}
			dump.WriteLine("</ul>");

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_conduct"));
			dump.WriteLine("<ul>");
			if (HasToken("easymode"))
				dump.WriteLine("<li><strong>{0}</strong></li>", i18n.GetString("infodump_easymode"));
#if DEBUG
			else if (HasToken("wizard"))
				dump.WriteLine("<li>{0}</li>", i18n.GetString("infodump_wizard"));
#endif
			dump.WriteLine("<li>{0}</li>", i18n.GetString(HasToken("books") ? "infodump_literate" : "infodump_illiterate"));
			dump.WriteLine("<li>{0}</li>", i18n.GetString(lovers > 0 ? "infodump_had_lovers" : "infodump_no_lovers"));
			if (lovers == 1)
				dump.WriteLine("<li>{0}</li>", i18n.GetString("infodump_one_lover"));
			if (victims > 0)
				dump.WriteLine("<li>{0}</li>", i18n.GetString(victims == 1 ? "infodump_rapist" : "infodump_serial_rapist"));
			//if (deities == 0)
			//	dump.WriteLine("<li>You were an atheist.</li>");
			//else
			//	dump.WriteLine(deities == 1 ? "<li>You were monotheistic.</li>" : "<li>You were a polytheist.</li>");
			dump.WriteLine("</ul>");

			dump.WriteLine("<h2>{0}</h2>", i18n.GetString("infodump_books"));
			dump.WriteLine("<ul>");
			if (HasToken("books"))
			{
				foreach (var book in GetToken("books").Tokens)
					dump.WriteLine("<li>&ldquo;{0}&rdquo;</li>", book.Text);
			}
			else
				dump.WriteLine("<li>{0}</li>", i18n.GetString("infodump_no_books"));
			dump.WriteLine("</ul>");

			dump.Flush();
			dump.Close();

			System.Diagnostics.Process.Start(Name + " info.html");
		}

		// stuff useful for sex.tml starts here 

		public bool HasPenis()
		{
			return HasToken("penis");
		}

		public bool HasVagina()
		{
			return HasToken("vagina");
		}

		public bool HasClit()
		{
			return (HasToken("vagina") && GetToken("vagina").HasToken("clit"));
		}

		public bool CanReachBreasts()
		{
			var undershirt = GetEquippedItemBySlot("undershirt");
			var shirt = GetEquippedItemBySlot("shirt");
			var jacket = GetEquippedItemBySlot("jacket");
			var cloak = GetEquippedItemBySlot("cloak");
			return ((cloak == null || cloak.CanReachThrough(this)) &&
				(jacket == null || jacket.CanReachThrough(this)) &&
				(shirt == null || shirt.CanReachThrough(this)) &&
				(undershirt == null || undershirt.CanReachThrough(this)));
		}

		public bool CanReachCrotch(string part = null)
		{
			var underpants = GetEquippedItemBySlot("underpants");
			var pants = GetEquippedItemBySlot("pants");
			var socks = GetEquippedItemBySlot("socks");
			return ((pants == null || pants.CanReachThrough(this, part)) &&
				(underpants == null || underpants.CanReachThrough(this, part)) &&
				(socks == null || socks.CanReachThrough(this, part)));
		}

		//// sparks sex.tml helper functions
		public bool VaginalPlug()
		{
			return (GetEquippedItemBySlot("vagina") != null);
		}

		public bool AnalPlug()
		{
			return (GetEquippedItemBySlot("anus") != null);
		}

		public void CheckPants(MorphReportLevel reportLevel = MorphReportLevel.PlayerOnly, bool reportAsMessages = false)
		{
			var doReport = new Action<string>(s =>
			{
				if (reportLevel == MorphReportLevel.NoReports)
					return;
				if (reportLevel == MorphReportLevel.PlayerOnly && this != NoxicoGame.Me.Player.Character)
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
						InventoryItem.TearApart(find, this, true);
						doReport(string.Format("[Youorname] [has] torn out of [his] {0}!", originalname).Viewpoint(this));
					}
					else
					{
                        if (this == NoxicoGame.Me.Player.Character)
                            doReport(string.Format("[Youorname] slip{{s}} out of [his] {0}.", originalname).Viewpoint(this));
                        //mention for others?  Less dramatic than tearing out
                        //else
                        //    doReport(this.Name.ToString() + " slips out of " + HisHerIts(true) + " " + originalname + ".");
					}
				}
			}
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

		public float ChangeLiking(Character target, float delta)
		{
			var shipToken = this.Path("ships/" + target.ID);
			if (shipToken == null)
			{
				//Error?
				return 0;
			}
			shipToken.Value += delta;
			return shipToken.Value;
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

		public Character Spouse
		{
			get
			{
				if (BoardChar == null)
					return null;
				if (BoardChar.ParentBoard == null)
					return null;
				//Assume that our spouse is on the same board.
				foreach (var ship in GetToken("ships").Tokens)
				{
					if (ship.HasToken("spouse") || ship.HasToken("friend"))
					{
						var them = BoardChar.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => x.Character != null && x.Character.ID == ship.Name);
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
			var capacity = 0f;
			foreach (var s2c in strengthToCapacity)
			{
				capacity = s2c.Value;
				if (s2c.Key > strength)
					break;
			}

			//Taurs and taurtrains get a bonus!
			if (HasToken("taur"))
			{
				var taur = (int)GetToken("taur").Value;
				if (taur == 0)
					taur = 1;
				capacity += taur * 0.5f * capacity;
			}
			else if (HasToken("quadruped"))
				capacity += 0.5f * capacity;
			//And so do quadrupeds, dammit!

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
					if (weightToken.Text.IsBlank())
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
			/*
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
			*/

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
				colorToken.Text = o.PickOne();
		}
		
		public float GetBreastSize()
		{
			if (!this.HasToken("breasts"))
				return 0;
			var boob = this.GetToken("breasts");
			var size = boob.GetToken("size").Value;
			if (boob.HasToken("lactation"))
				size += 0.25f * boob.GetToken("lactation").Value;
			return size;
		}

		public float GetBreastAmount()
		{
			if (!this.HasToken("breasts"))
				return 0;
			var boob = this.GetToken("breasts");
			return boob.GetToken("amount").Value;
		}

		public float GetPenisSize(bool withThickness)
		{
			if (!this.HasToken("penis"))
				return -1;
			var penis = this.GetToken("penis");
			var ret =  penis.GetToken("length").Value;
			if (withThickness)
				ret *= penis.GetToken("thickness").Value;
			return ret;
		}

		public float GetVaginaCapacity()
		{
			return GetVaginaCapacity(this.GetToken("vagina"));
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
			if (vagina == null)
				return -1;

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

		public void UpdateOviposition()
		{
			if (BoardChar == null)
				return;
			if (this.HasToken("egglayer") && this.HasToken("vagina"))
			{
				var eggToken = this.GetToken("egglayer");
				eggToken.Value++;
				if (eggToken.Value == 500)
				{
					eggToken.Value = 0;
					var egg = new DroppedItem("egg")
					{
						XPosition = BoardChar.XPosition,
						YPosition = BoardChar.YPosition,
						ParentBoard = BoardChar.ParentBoard,
					};
					egg.Take(this, BoardChar.ParentBoard);
					if (BoardChar is Player)
						NoxicoGame.AddMessage(i18n.GetString("youareachicken").Viewpoint(this));
					return;
				}
			}
			return;
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
            var item = itemList.FirstOrDefault(y => y.tempToken.ContainsKey(this.ID) && y.tempToken[this.ID].HasToken("equipped"));

            return (item != null);
        }

		/// <summary>
		/// Checks the character's inventory to see if the character has an item equipped in a particular item slot.
		/// </summary>
		/// <param name="itemSlot">The name of the item slot to check. Valid options are:
		/// cloak, goggles, hand, hat, jacket, mask, neck, pants, ring, shirt, underpants, undershirt
		/// nipple, clit, labia, vagina, anus, cockring, frenulum</param>
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
			var carriedItems = this.GetToken("items");
			var carried = new List<InventoryItem>();
			foreach (var carriedItem in carriedItems.Tokens)
			{
				var foundItem = NoxicoGame.KnownItems.Find(y => y.ID == carriedItem.Name);
				if (foundItem == null)
					continue;
				if (foundItem.ID == itemID)
                {
                    foundItem.tempToken[this.ID] = carriedItem;
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
		/// cloak, goggles, hand, hat, jacket, mask, neck, pants, ring, shirt, underpants, undershirt
		/// nipple, clit, labia, vagina, anus, cockring, frenulum</param>
		/// <returns>Returns an InventoryItem from <see cref="NoxicoGame.KnownItems"/> matching the item held by the character. A reference to the character
		/// held item itself is stored in <see cref="InventoryItem.tempToken"/>. If there is no item in the character slot, then null is returned. </returns>
		public InventoryItem GetEquippedItemBySlot(string itemSlot)
        {
			var carriedItems = this.GetToken("items");
			foreach (var carriedItem in carriedItems.Tokens)
			{
                var foundItem = NoxicoGame.KnownItems.Find(y => y.ID == carriedItem.Name);
                if (foundItem == null)
                    continue;
                if (foundItem.HasToken("equipable") && carriedItem.HasToken("equipped"))
                {
                    var eq = foundItem.GetToken("equipable");
                    if (eq.HasToken(itemSlot))
                    {
                        foundItem.tempToken[this.ID] = carriedItem;
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
			if (bodypart == null)
				return false;
			if (bodypart.HasToken("virgin"))
			{
				bodypart.RemoveToken("virgin");
				return true;
			}

			return false;
		}

		public bool RemoveVirgin()
		{
			return RemoveVirgin(this.GetToken("vagina"));
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
			{
				dickSize = penis.GetToken("thickness").Value;
			}
			else // might be an inventory item
			{
				InventoryItem toy = GetFirstInventoryItem(penis.Name);
				if (toy != null && toy.HasToken("thickness"))
					dickSize = toy.GetToken("thickness").Value;
			}

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

		public bool StretchHole(Token penis)
		{
			return StretchHole(this.GetToken("vagina"), penis);
		}

		/// <summary>
		/// Resets the values of climax and stimulation.
		/// </summary>
		public void Orgasm()
		{
			GetToken("climax").Value = 0;
			GetToken("stimulation").Value = 10;
			if (HasToken("hostile"))
				RemoveToken("hostile");
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

			if (statname == "climax")
			{
				// 0 stim gives only 50% of climax increase
				// 50 stim gives 125% climax increase
				// 100 stim gives 200% climax increase	 
				var stimBonus = 0.5f + (GetStat(Stat.Stimulation) * 0.015f);

				// same
				var carnBonus = 0.5f + (GetStat(Stat.Carnality) * 0.015f);

				stat += (amount * stimBonus * carnBonus);
			}
			else
			{
				stat += amount;
			}

			if (stat > 100)
				stat = 100;
			if (stat < 0)
				stat = 0;

			return GetToken(statname).Value = stat;
		}

		public bool ChangeMoney(float amount)
		{
			var money = GetToken("money").Value;
			var fail = false;

			money += amount;
			if (money < 0)
			{
				// note that if you get here, probably the person charging
				//  you money didn't check that you had enough
				money = 0;
				fail = true;
			}
			GetToken("money").Value = money;
			return fail;
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

		public bool LikesBoys
		{
			get
			{
				if (!HasToken("sexpreference"))
					return true;
				var pref = GetToken("sexpreference").Value;
				return pref == 0 || pref == 2;
			}
		}

		public bool LikesGirls
		{
			get
			{
				if (!HasToken("sexpreference"))
					return true;
				var pref = GetToken("sexpreference").Value;
				return pref == 1 || pref == 2;
			}
		}

		public bool Likes(Character other)
		{
			if (!HasToken("sexpreference"))
				return true;
			var pref = GetToken("sexpreference").Value;
			if (other.PercievedGender == Noxico.Gender.Herm)
				return true;
			if (other.PercievedGender == Noxico.Gender.Neuter)
				return false;
			return pref == 2 || pref == ((int)other.PercievedGender - 1);
		}

		public void Copy(Character source)
		{
			var copier = GetToken("copier");
			if (copier == null)
				throw new InvalidOperationException("Tried to copy, but is not a copier.");
			var full = copier.HasToken("full");
			var toCopyForFull = new[]
			{
				"balls", "penis", "breasts", "ass", "hips", "waist", "vagina",
				"legs", "skin", "ascii", "tallness", "hair", "face", "eyes",
				"teeth", "tongue", "legs", "quadruped", "monoceros", "horns",
				"tail", "ears", "slimeblob", "snaketail",
				"hostile", //lol that oughta be fun
			};
			var toCopyForSlimes = new[]
			{
				"balls", "penis", "breasts", "vagina", /* "ass", "hips", "waist", */
			};
			if (source == null)
			{
				//Revert: For slimes, remove all sexual characteristics. For changelings, copy back original form from copier/backup.
				if (full)
				{
					var backup = copier.GetToken("backup");
					if (backup == null)
						throw new InvalidOperationException("Tried to revert to true form, but true form is missing.");
					foreach (var token in toCopyForFull)
						RemoveAll(token);
					foreach (var token in backup.Tokens)
						AddToken(token);
					var items = this.GetToken("items");
					var toRemove = new List<Token>(); //can't delete in a foreach
					foreach (var token in items.Tokens.Where(x => x.HasToken("disguise")))
						toRemove.Add(token);
					foreach (var token in toRemove)
						items.RemoveToken(token);
					foreach (var token in backup.GetToken("items").Tokens)
						items.AddToken(token);
					copier.RemoveToken("backup");
				}
				else
				{
					foreach (var token in toCopyForSlimes)
						RemoveAll(token);
				}
				copier.Value = 0;
			}
			else
			{
				if (full)
				{
					var backup = copier.AddToken("backup");
					foreach (var token in this.Tokens.Where(x => toCopyForFull.Contains(x.Name)))
						backup.AddToken(token);
					foreach (var token in toCopyForFull)
						RemoveAll(token);
					foreach (var token in source.Tokens.Where(x => toCopyForFull.Contains(x.Name)))
						AddToken(token.Clone());
					var backupItems = backup.AddToken("items");
					var items = this.GetToken("items");
					var toRemove = new List<Token>();
					foreach (var token in this.GetToken("items").Tokens.Where(x => x.HasToken("equipped")))
					{
						backupItems.AddToken(token);
						toRemove.Add(token);
					}
					foreach (var token in toRemove)
						items.RemoveToken(token);
					foreach (var token in source.GetToken("items").Tokens.Where(x => x.HasToken("equipped")))
					{
						var newToken = items.AddToken(token.Clone());
						newToken.AddToken("disguise");
						var cursed = newToken.GetToken("cursed");
						if (cursed == null)
							cursed = newToken.AddToken("cursed");
						cursed.Text = i18n.GetString("item_changeling_disguise");
						cursed.AddToken("hidden");
						cursed.AddToken("known");
					}
					//TODO: copy all stats but health at 75%.
				}
				else
				{
					foreach (var token in toCopyForSlimes)
						RemoveAll(token);
					foreach (var token in source.Tokens.Where(x => toCopyForSlimes.Contains(x.Name)))
						AddToken(token);
				}
				copier.Value = 1;
				copier.AddToken("timeout", 5 * (full ? 1 : 3)).AddToken("minute", NoxicoGame.InGameTime.Minute);
			}
		}

		public bool UpdateCopier()
		{
			if (!HasToken("copier"))
				return false;
			if (Path("copier/full") != null && GetToken("copier").Value == 0)
			{
				//Should be a Changeling. Distance should be < 2.
				//TODO: make this more generic, uncoupling from what should be a modpak's bodyplan.
				var myHash = this.GetBodyComparisonHash();
				var changeling = NoxicoGame.BodyplanHashes["mlp_changeling"];
				if (myHash.GetHammingDistance(changeling) >= 2)
					return false;
			}
			else
			{
				//Should be a Slime, defined as simply having a slimeblob and slime skin.
				if (Path("skin/type").Text != "slime" || !HasToken("slimeblob"))
					return false;
			}
			//Regain copying power by TF scripts or Morph().
			return true;
		}

		public bool IsSlime
		{
			get
			{
				return (Path("skin/type").Text == "slime");
			}
		}

		public void ResetEquipmentTempTokens()
		{
			var carriedItems = this.GetToken("items");
			foreach (var carriedItem in carriedItems.Tokens)
			{
				var foundItem = NoxicoGame.KnownItems.Find(y => y.ID == carriedItem.Name);
				if (foundItem == null)
					continue;
				if (foundItem.tempToken.ContainsKey(this.ID))
					foundItem.tempToken.Remove(this.ID);
			}
		}

		/* TEAMS IDEA
		 * ----------
		 * Each character is assigned to one of the following:
		 * 
		 * 0 - Neutrals
		 * 1 - The player
		 * 2 - Hostiles
		 * 3 - The player's posse
		 * 4 - Guards
		 * 5 - Predatory beasts
		 * 6 - Prey beasts
		 * 7 - Angry neutrals
		 * 8 - Routed hostile
		 * 
		 * This would of course mean that the Hostile token will be deprecated.
		 * 
		 * ATTACK GRID -- members of one team will hunt down and attack members of the other when spotted.
		 *   0 1 2 3 4 5 6 7 8 <-- the other
		 * 0 - - - - - - - - - <-- neutrals don't attack
		 * 1 - - - - - - - - - <-- the player is not automatically controlled at all
		 * 2 Y P - Y Y - - Y - <-- hostiles attack neutrals, players, their possse and guards, but prefer the player
		 * 3 - - Y - - Y - Y - <-- posse attacks hostiles, predators, and angry neutrals (allow tactics control?)
		 * 4 - T Y - - C - - - <-- guards attack hostiles, thiefing players, and any predators that come too close
		 * 5 ? Y Y Y Y ? P Y P <-- predators attack basically everyone, but prefer prey
		 * 6 - - - - - C - - - <-- prey tries to bite back at predators
		 * 7 - Y - - - - - - - <-- neutrals only get angry at the player, for stealing their crap
		 * 8 - - - - - - - - - <-- routed hostiles don't attack anyone, too busy getting the hell away
		 * 
		 * FLOCKING GRID -- stay close to me... or don't?
		 * (When you see a member of the other team, what do you do?)
		 *   0 1 2 3 4 5 6 7 8
		 * 0 - - A - - - - - - <-- neutrals avoid hostiles
		 * 1 - - - - - - - - -
		 * 2 - - S - - - - - S <-- hostiles of a feather flock together (s for same)
		 * 3 - Y - - - - - - -
		 * 4 - - - - ? - - - -
		 * 5 - - - - - S - - -
		 * 6 - ? A ? - A S - - <-- prey avoids any visible predators
		 * 7 - - A - - - - - -
		 * 8 - A - A A A - A Y
		 */
		public int Team
		{
			get
			{
				if (HasToken("team"))
					return (int)GetToken("team").Value;
				else
				{
					if (HasToken("player"))
						return 1;
					return 0; //Assume neutral
				}
			}
			set
			{
				if (!HasToken("team"))
					AddToken("team");
				if (HasToken("player"))
					value = 1; //Do not allow the player to be off the Player team.
				GetToken("team").Value = value;
			}
		}

		public TeamBehaviorAction DecideTeamBehavior(Character other, TeamBehaviorClass whatFor)
		{
			var myTeam = this.Team;
			var theirTeam = other.Team;
			if (myTeam == 1) //We are the player
				return TeamBehaviorAction.Nothing;
			//TODO: handle team 3 with player-decided tactics?
			/* ACTIONS -- using ints instead of enums to make it shorter
			 * 0 - do nothing
			 * 1 - attack
			 * 2 - preferential attack
			 * 3 - avoid
			 * 4 - flock
			 * 5 - flock to similar
			 * ...
			 * 8 - close-by attack -- collapses to "nothing" or "preferential". IF RETURNED, SHIT'S FUCKED.
			 * 9 - thiefing player check -- collapses like #8.
			 */
			if (whatFor == TeamBehaviorClass.Attacking)
			{
				var grid = new[]
				{
					0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 0, 0, 0, 0,
					1, 2, 0, 1, 1, 0, 0, 1, 0,
					0, 0, 1, 0, 0, 1, 0, 1, 0,
					0, 9, 1, 0, 0, 8, 0, 0, 0,
					0, 1, 1, 1, 1, 0, 2, 1, 2,
					0, 0, 0, 0, 0, 8, 0, 0, 0,
					0, 1, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 0, 0, 0, 0,
				};
				var action = (TeamBehaviorAction)grid[(myTeam * 9) + theirTeam];
				if (action == TeamBehaviorAction.CloseByAttack)
					action = (BoardChar.DistanceFrom(other.BoardChar) > 4) ? TeamBehaviorAction.Nothing : TeamBehaviorAction.Attack;
				//TODO: check for thieving players
				//TODO: consider running away at low health, returning TBA.Avoid.
				return action;
			}
			else
			{
				var grid = new[]
				{
					0, 0, 3, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 5, 0, 0, 0, 0, 0, 5,
					0, 4, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 0, 0, 0, 0,
					0, 0, 0, 0, 0, 5, 0, 0, 0,
					0, 0, 3, 0, 0, 3, 5, 0, 0,
					0, 0, 3, 0, 0, 0, 0, 0, 0,
					0, 3, 0, 3, 3, 3, 0, 3, 4,
				};
				var action = (TeamBehaviorAction)grid[(myTeam * 9) + theirTeam];
				if (action == TeamBehaviorAction.FlockAlike) //TODO: check if >= 3 is any good.
					action = (this.GetClosestBodyplanMatch().GetHammingDistance(other.GetClosestBodyplanMatch()) >= 3) ? TeamBehaviorAction.Nothing : TeamBehaviorAction.Flock;
				return action;
			}
		}
	}
}
