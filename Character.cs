using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

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

	public enum Mutations
	{
		Random = -1, AddBreastRow, AddPenis, AddVagina, AddOddLegs, RemoveOddLegs, AddBreast, RemoveBreast, AddTesticle, RemoveTesticle, 
		GiveDicknipples, GiveNipplecunts, AddNipple, RemoveNipple, GiveRegularNipples
	}

	public partial class Character : TokenCarrier
	{
		public static StringBuilder MorphBuffer = new StringBuilder();

		public Name Name { get; set; }
		//public string Species { get; set; }
		public string Title { get; set; }
		public bool IsProperNamed { get; set; }
		public string A { get; set; }
		public Culture Culture { get; set; }
		public BoardChar BoardChar { get; set; }

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
			var g = HasToken("invisiblegender") ? Gender.Random : Gender;
			if ((g == Gender.Male && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == Gender.Female && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == Gender.Herm && HasToken("hermonly")))
				g = Gender.Random;
			if (IsProperNamed)
				return string.Format("{0}, {1} {3}", Name.ToString(true), A, (g == Gender.Random) ? "" : g.ToString().ToLowerInvariant() + ' ', Title);
			return string.Format("{0} {1}", A, Title);
		}

		public string GetKnownName(bool fullName = false, bool appendTitle = false, bool the = false, bool initialCaps = false)
		{
			if (HasToken("player") || HasToken("special"))
				return Name.ToString(fullName);
			if (HasToken("beast"))
				return string.Format("{0} {1}", initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A), Path("terms/generic").Text);
			var player = NoxicoGame.HostForm.Noxico.Player.Character;
			var g = HasToken("invisiblegender") ? Gender.Random : Gender;
			if ((g == Gender.Male && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == Gender.Female && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == Gender.Herm && HasToken("hermonly")))
				g = Gender.Random;
			if (player != null && player.Path("ships/" + ID) != null)
			{
				if (appendTitle)
					return string.Format("{0}, {1} {2}{3}", Name.ToString(fullName), (the ? "the" : A), (g == Gender.Random) ? "" : g.ToString().ToLowerInvariant() + ' ', Title);
				return Name.ToString(fullName);
			}
			return string.Format("{0} {1}{2}", initialCaps ? (the ? "The" : A.ToUpperInvariant()) : (the ? "the" : A), (g == Gender.Random) ? "" : g.ToString().ToLowerInvariant() + ' ', Title);
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

		public Gender ActualGender
		{
			get
			{
				return Gender;
			}
		}

		public Gender PercievedGender
		{
			get
			{
				if (HasToken("beast"))
					return Noxico.Gender.Neuter;

				if (HasToken("player"))
					return PreferredGender;
				//TODO: detect a relationship token and return the preferred gender if known.

				var crotchVisible = false;
				var pants = GetEquippedItemBySlot("pants");
				var underpants = GetEquippedItemBySlot("underpants");
				var pantsCT = (pants == null) ? true : pants.CanSeeThrough();
				var underpantsCT = (underpants == null) ? true : underpants.CanSeeThrough();
				if (pantsCT && underpantsCT)
					crotchVisible = true;
				var biggestDick =  (GetBiggestPenisNumber(false) == -1) ? 0 : GetPenisSize(GetPenisByNumber(GetBiggestPenisNumber(false)), false);
				if (biggestDick < 4 && !crotchVisible)
					biggestDick = 0; //hide tiny dicks with clothing on.

				var score = 0.5f;
				score -= biggestDick * 0.02f;
				if (BiggestBreastrowNumber != -1)
					score += GetBreastRowSize(BiggestBreastrowNumber) * 0.1f;
				if (HasToken("vagina") && crotchVisible)
					score += 0.5f;
				if (HasToken("hair"))
					score += this.Path("hair/length").Value * 0.01f;
				//TODO: apply femininity score

				if (score < 0.4f)
					return Noxico.Gender.Male;
				else if (score > 0.6f)
					return Noxico.Gender.Female;
				return Noxico.Gender.Herm;
			}
		}

		public Gender PreferredGender
		{
			get
			{
				if (HasToken("preferredgender"))
					return (Gender)Enum.Parse(typeof(Gender), GetToken("preferredgender").Text, true);
				return ActualGender; //stopgap
			}
		}

		public void UpdateTitle()
		{
			//TODO: clean up
			//TRANSLATE the lot of this. Could take rewrite cleanup to handle.
			var g = PercievedGender.ToString().ToLowerInvariant();
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

		public Character()
		{
		}

		public void FixBoobs()
		{
			//moved from FixBroken() since FixBoobs() may need to be called from within FixBroken() and it's best to avoid an infinite loop.
			if (!this.HasToken("breastrow"))
			{
				var breastRow = this.AddToken("breastrow");
				breastRow.AddToken("amount", 2);
				breastRow.AddToken("size", 0);
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
					throw new NotImplementedException();
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
					(toFix.HasToken("sizefromprevious") && toFix.GetToken("sizefromprevious").Value < 0))
						toRemove.Add(toFix);
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
				throw new ArgumentOutOfRangeException(string.Format("Could not find a unique bodyplan with id \"{0}\" to generate.", id));
			newChar.AddSet(planSource.Tokens);
			newChar.AddToken("lootset_id", 0, id);
			if (newChar.HasToken("_n"))
				newChar.Name = new Name(newChar.GetToken("_n").Text);
			else
				newChar.Name = new Name(id.Replace('_', ' ').Titlecase());
			newChar.RemoveToken("_n");
			if (planSource.HasToken("_a"))
				newChar.A = planSource.GetToken("_a").Text;
			else
				newChar.A = newChar.Name.ToString().StartsWithVowel() ? i18n.GetString("an") : i18n.GetString("a");
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

			newChar.EnsureDefaultTokens();
			newChar.StripInvalidItems();
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

		private Token PickATit()
		{
			var allTits = Tokens.Where(x => x.Name == "breastrow").ToList();
			return allTits[Random.Next(allTits.Count)];
		}

		private int PickATitNum()
		{
			var allTits = Tokens.Where(x => x.Name == "breastrow").ToList();
			return Random.Next(allTits.Count);
		}

#if MUTAMORPH
        public List<string> Mutate(int number, float intensity, Mutations mutation = Mutations.Random)
        {
			//TODO: return a summary of what was done, coded for i18n

            //Applies a few random mutations to the calling Character.  Intensity determines sizes and numbers of added objects, number determines how many mutations to apply.
			var randomize = false;
			if (mutation == Mutations.Random)
				randomize = true;
			List<string> reports = new List<string>();
			for (var i = 0; i < number; i++)
            {
				string report = "";
				if (randomize)
					mutation = (Mutations)Random.Next(Enum.GetNames(typeof(Mutations)).Length - 1); //subtract one from the length to account for Random = -1
                switch (mutation)
                {
					case Mutations.Random:
						throw new Exception("Something went wrong, and the mutation was not randomized properly.  Pester Xolroc to fix it.");
                    case Mutations.AddBreastRow:
						var newboobs = new Token("breastrow");
                        if (this.Tokens.Count(t => t.Name == "breastrow") < 4)
                        {
							if (this.HasToken("breastrow") && this.GetToken("breastrow").GetToken("size").Value == 0)
								this.RemoveToken("breastrow");
							
							var amount = 2;
                            newboobs.AddToken("amount", amount);

							report += "[Youorname] grow{s} " + i18n.GetArray("setbymeasure")[amount] + " ";

							var size = 0f;
							var fromprevious = false;
                           
							if (this.HasToken("breastrow"))
							{
								size = (float)Random.NextDouble() * intensity / 10 + 0.5f;
								newboobs.AddToken("sizefromprevious", size);
								fromprevious = true;
							}
							else
							{
								size = (float)Random.NextDouble() * intensity / 2 + 4f;
								newboobs.AddToken("size", size);
							}

							report += fromprevious ? " " + i18n.Pluralize("breast", amount) + Math.Round((double)size * 100).ToString() + "% the size of the " + 
													i18n.Pluralize("one", (int)this.GetBreastRowByNumber(this.Tokens.Count(t => t.Name == "breastrow") - 1).
													GetToken("amount").Value) + " above " + (amount == 1 ? "it, with " : "them, each with")
												   : " " + Descriptions.GetSizeDescriptions(size, "//upperbody/breasts/sizes") + i18n.Pluralize("breast", amount) + 
												    (amount == 1 ? ", with " : ", each with ");

							var nipnum = Random.Next(5);
							var nipsize = (float)Random.NextDouble() * intensity / 4 + 0.7f;
							newboobs.AddToken("nipples", nipnum);
                            newboobs.GetToken("nipples").AddToken("size", nipsize);

							var dickniplength = -1f;
							var dicknipthick = -1f;
							var nipcuntwet = -1;
							var nipcuntloose = -1;

                            switch (Random.Next(3))
                            {
                                case 0:
                                    newboobs.GetToken("nipples").AddToken("canfuck");
									dickniplength = (float)Random.NextDouble() * intensity / 2 + 5f;
									dicknipthick = (float)Random.NextDouble() * intensity / 4 + 2f;
                                    newboobs.GetToken("nipples").AddToken("length", dickniplength);
                                    newboobs.GetToken("nipples").AddToken("thickness", dicknipthick);
									
									report += i18n.GetArray("setbymeasure")[nipnum] + Descriptions.Length(dickniplength) + " [p:" + 
												nipnum.ToString() + ":dicknipple].";
                                    
									break;
                                case 1:
                                    newboobs.GetToken("nipples").AddToken("fuckable");
									nipcuntwet = Random.Next((int)(intensity / 2));
									nipcuntloose = Random.Next((int)(intensity / 2));
                                    newboobs.GetToken("nipples").AddToken("wetness", nipcuntwet);
                                    newboobs.GetToken("nipples").AddToken("looseness", nipcuntloose);

									report += i18n.GetString("counts")[nipnum] + " " + Descriptions.GetSizeDescriptions(nipcuntloose,
												"//lowerbody/sexorgans/vaginas/loosenesses") + " and " + Descriptions.GetSizeDescriptions(nipcuntwet,
												"//lowerbody/sexorgans/vaginas/wetnesses") + " [p:" + nipnum.ToString() + ":nipplecunt].";

                                    break;
                                case 2:
									report += i18n.GetArray("counts")[nipnum] + " " + Descriptions.Length(nipsize) + " [p:" + nipnum.ToString() + ":nipple].";
                                    break;
                            }

                            this.AddToken(newboobs);
                        }
						else
							report += "\uE2FC";
                        break;
                    case Mutations.AddPenis:
						if (this.Tokens.Count(t => t.Name == "penis") < 8)
						{
							string[] dicktypes = { "human", "horse", "dragon", "cat", "dog", "bear", "lizard", "studded" };
							var type = dicktypes[Random.Next(dicktypes.Length)];
							var newdick = new Token("penis", 0f, type);
							var length = (float)Random.NextDouble() * intensity + 12f;
							var thick = (float)Random.NextDouble() * intensity / 4 + 4f;
							newdick.AddToken("length", length);
							newdick.AddToken("thickness", thick);
							newdick.AddToken("cumsource");
							report += "[Youorname] [has] grown a new " + Descriptions.Length(length) + " long, " + Descriptions.Length(thick)
										+ " thick " + type + " " + Descriptions.CockRandom();
							this.AddToken(newdick);
						}
						else
							report += "\uE2FC";
                        break;
                    case Mutations.AddVagina:
						if (this.Tokens.Count(t => t.Name == "vagina") < 5)
						{
							Token newpussy = new Token("vagina");
							newpussy.AddToken("wetness", Random.Next((int)(intensity / 2)));
							newpussy.AddToken("looseness", Random.Next((int)(intensity / 2)));
							this.AddToken(newpussy);
						}
						else
							report += "\uE2FC";
                        break;
                    case Mutations.AddOddLegs:
                        var funkyLegs = new[] { "taur", "quadruped", "snaketail", "slimeblob" };
						if (funkyLegs.All(x => !HasToken(x)))
						{
							var choice = Random.Next(funkyLegs.Length);
							if (choice < funkyLegs.Length - 1)
								if (this.GetClosestBodyplanMatch() != "human" || funkyLegs[choice] != "quadruped")
									this.AddToken(funkyLegs[choice]);
							if (choice == 2 || choice == 3)
							{
								this.RemoveToken("legs");
								this.RemoveToken("tail");
								report += choice == 2 ? "[Youorname] [has] lost [his] legs and gained a long, serpentine tail."
													  : "[Youorname] become{s} a slime, and [his] legs melt into a blob of goo.";
							}
							else
								report += choice == 0 ? "[Youorname] grow{s} a second pair of legs as a taurbody extends from [his] rear." 
													  : "[Youorname] fall{s} down on all fours as [his] body rearranges itself to a quadrupedal form.";
						}
						else if (this.HasToken("taur"))
						{
							this.GetToken("taur").Value = Math.Max(2, this.GetToken("taur").Value + 1);
							report += "Another taurbody extends out behind [yourornames] [buttrand] as [he] grow{s} another pair of legs.";
						}
						else
							report += "\uE2FC";
                        break;
                    case Mutations.RemoveOddLegs:
						if ((this.HasToken("taur") && this.GetToken("taur").Value < 2) || this.HasToken("quadruped") 
							|| this.HasToken("snaketail") || this.HasToken("slimeblob"))
						{
							//left out stiletto and insect, as those are meant for particular characters/species only and would look off on the wrong body
							var legtypes = new[] { "human", "horse", "claws", "genbeast", "bear", "dog" };
							var legnames = new[] { "human", "equine", "clawed", "digitigrade", "ursine", "digitigrade" };
							var type = Random.Next(legtypes.Length);
							this.RemoveToken("taur");
							this.RemoveToken("quadruped");
							if (!this.HasToken("legs"))
							{
								this.AddToken("legs", 0f, legtypes[type]);
								if (!this.HasToken("hips"))
									this.AddToken("hips", (float)Random.NextDouble() * intensity / 4 + 2f);
								if (!this.HasToken("waist"))
									this.AddToken("waist", (float)Random.NextDouble() * intensity / 4 + 2f);
								if (!this.HasToken("ass"))
									this.AddToken("ass", (float)Random.NextDouble() * intensity / 4 + 2f);
								if (this.HasToken("snaketail"))
									report += "[Youorname] [has] lost [his] snaketail ";
								else if (this.HasToken("slimeblob"))
									report += "[Youorname] [has] solidified ";
								report += "and gained a pair of " + legnames[type] + " legs.";
							}
							else
								report += "[Yourornames] body has returned to a normal bipedal shape.";
							this.RemoveToken("snaketail");
							this.RemoveToken("slimeblob");
						}
						else if (this.HasToken("taur") && this.GetToken("taur").Value >= 2)
						{
							this.GetToken("taur").Value--;
							report += "[Youorname] lose{s} a pair of legs as one of [his] taurbodies shrinks into [his] [buttrand].";
						}
						else
							report += "\uE2FC";
                        break;
                    case Mutations.AddBreast:
                        if (this.HasToken("breastrow"))
                        {
							var boob = PickATitNum();
                            if (this.GetBreastRowByNumber(boob).GetToken("amount").Value < 5)
                                this.GetBreastRowByNumber(boob).GetToken("amount").Value++;
							if (this.Tokens.Count(t => t.Name == "breastrow") > 1)
								report += "[Youorname] [has] gained a " + this.GetBreastRowByNumber(boob).GetToken("amount").Value.CountOrdinal() +
										  " [breastrand] in [his] " + (boob + 1).CountOrdinal() + " row.";
                        }
						else
							report += "\uE2FC";
                        break;
                    case Mutations.RemoveBreast:
                        if (this.HasToken("breastrow"))
                        {
                            var allTits = Tokens.Where(x => x.Name == "breastrow").ToList();
                            var rand = Random.Next(allTits.Count);
							var boob = allTits[rand];
                            if (boob.GetToken("amount").Value > 1)
							{
                                boob.GetToken("amount").Value--;
								report += "[Youorname] [has] lost a [breastrand] from [his] " + (rand + 1).CountOrdinal() + " row.";
                            }
							else
                            {
                                if (boob.HasToken("size") && allTits.Count - 1 > rand)
                                {
                                    allTits[rand + 1].AddToken("size", boob.GetToken("size").Value * allTits[rand + 1].GetToken("sizefromprevious").Value);
                                    allTits[rand + 1].RemoveToken("sizefromprevious");
                                }
                                this.RemoveToken(boob);
								report += "[Youorname] [has] lost a row of [breastsrand].";
                            }
                            if (!this.HasToken("breastrow"))
                            {
                                this.AddToken("breastrow").AddToken("size", 0);
                                this.GetToken("breastrow").AddToken("amount", 2);
                            }
                        }
						else
							report += "\uE2FC";
                        break;
                    case Mutations.AddTesticle:
                        var balls = GetToken("balls");
                        if (balls != null)
                        {
                            balls.GetToken("amount").Value++;
							report += "[Youorname] [has] gained another testicle.";
                        }
                        else
                        {
							var num = Random.Next((int)(intensity / 4)) + 1;
							var size = (float)Random.NextDouble() * intensity / 4 + 3f;
                            this.AddToken("balls").AddToken("amount", num);
                            this.GetToken("balls").AddToken("size", size);
							report += num > 1 ? "[Youorname] [has] gained a set of " + i18n.GetArray("counts")[num] + " " + Descriptions.BallSize(this.GetToken("balls")) +
												" balls." 
											  : "[Youorname] [has] grown a single testicle.";
                        }
                        break;
                    case Mutations.RemoveTesticle:
						if (this.HasToken("balls"))
						{
							this.GetToken("balls").GetToken("amount").Value--;
							if (this.GetToken("balls").GetToken("amount").Value <= 0)
							{
								this.RemoveToken("balls");
								report += "[Youorname] [has] lost [his] last remaining testicle.";
							}
							else
								report += "[Youorname] [has] lost one of [his] balls, bringing [him] down to only " + 
										  this.GetToken("balls").GetToken("amount").Value.Count() + ".";
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.GiveDicknipples:
						if (this.Path("breastrow/nipples") != null && this.Path("breastrow/nipples/canfuck") == null)
						{
							var boob = PickATitNum();
							while (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("canfuck"))
								boob = PickATitNum();
							this.GetBreastRowByNumber(boob).GetToken("nipples").AddToken("canfuck");
							report += "The nipples on [yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
									  " have grown out and become phallic.";
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.GiveNipplecunts:
						if (this.Path("breastrow/nipples") != null && this.Path("breastrow/nipples/fuckable") == null)
						{
							var boob = PickATitNum();
							while (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("fuckable"))
								boob = PickATitNum();
							this.GetBreastRowByNumber(boob).GetToken("nipples").AddToken("fuckable");
							//TODO: add wetness/looseness attributes.
							report += "The nipples on [yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
									  " have inverted and taken on a distinctly vaginal appearance.";
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.AddNipple:
						if (this.HasToken("breastrow"))
						{
							var boob = PickATitNum();
							if (!this.GetBreastRowByNumber(boob).HasToken("nipples"))
								this.GetBreastRowByNumber(boob).AddToken("nipples", 1);
							else
								this.GetBreastRowByNumber(boob).GetToken("nipples").Value++;
							report += "[Yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
								" have each gained another " + (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("canfuck") ? "dick" : "") + 
								"nipple" + (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("fuckable") ? "cunt" : "") + ".";
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.RemoveNipple:
						if (this.Path("breastrow/nipples") != null)
						{
							var boob = PickATitNum();
							while (!this.GetBreastRowByNumber(boob).HasToken("nipples"))
								boob = PickATitNum();
							this.GetBreastRowByNumber(boob).GetToken("nipples").Value--;
							var nippleName = (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("canfuck") ? "dick" : "") + 
								"nipple" + (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("fuckable") ? "cunt" : "");
							if (this.GetBreastRowByNumber(boob).GetToken("nipples").Value == 0)
							{
								this.GetBreastRowByNumber(boob).RemoveToken("nipples");
								report += "[Yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
										  " have lost their " + nippleName.Pluralize() + ".";
							}
							else
								report += "[Yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
										  " have each lost a " + nippleName + ".";
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.GiveRegularNipples:
						if (this.Path("breastrow/nipples/fuckable") != null || this.Path("breastrow/nipples/canfuck") != null)
						{
							var boob = PickATitNum();
							while (!this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("fuckable") && 
									!this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("canfuck"))
								boob = PickATitNum();
							if (this.GetBreastRowByNumber(boob).GetToken("nipples").HasToken("fuckable"))
							{
								this.GetBreastRowByNumber(boob).GetToken("nipples").RemoveToken("fuckable");
								report += "The nipplecunts on [yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
										  " have become normal nipples.";
							}
							else
							{
								this.GetBreastRowByNumber(boob).GetToken("nipples").RemoveToken("canfuck");
								report += "The dicknipples on [yourornames] " + (boob + 1).CountOrdinal() + " row of " + Descriptions.BreastRandom(true) +
										  " have become normal nipples.";
							}
						}
						else
							report += "\uE2FC";
						break;
                }
				reports.Add(report);
            }
			return reports;
        }
#else
		public List<string> Mutate(int number, float intensity, Mutations mutation = Mutations.Random)
		{
			throw new InvalidOperationException("Mutate() has been disabled in this build.");
		}
#endif

        public static Character Generate(string bodyPlan, Gender gender, Gender idGender = Gender.Random)
		{
			var bodyPlans = Mix.GetTokenTree("bodyplans.tml", true);

			var newChar = new Character();
			var planSource = bodyPlans.FirstOrDefault(t => t.Name == "bodyplan" && t.Text == bodyPlan);
			if (planSource == null)
				throw new ArgumentOutOfRangeException(string.Format("Could not find a bodyplan with id \"{0}\" to generate.", bodyPlan));

			newChar.AddSet(planSource.Tokens);
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

			if (idGender == Gender.Random)
				idGender = gender;

			if (gender != Gender.Female && newChar.HasToken("femaleonly"))
				throw new Exception(string.Format("Cannot generate a non-female {0}.", bodyPlan));
			if (gender != Gender.Male && newChar.HasToken("maleonly"))
				throw new Exception(string.Format("Cannot generate a non-male {0}.", bodyPlan));

			if (gender == Gender.Male || gender == Gender.Neuter)
			{
				newChar.RemoveToken("fertility");
				newChar.RemoveToken("womb");
				while (newChar.HasToken("vagina"))
					newChar.RemoveToken("vagina");
				foreach (var breastRow in newChar.Tokens.Where(t => t.Name == "breastrow" && t.HasToken("size")))
					breastRow.GetToken("size").Value = 0;
			}
			if (gender == Gender.Female || gender == Gender.Neuter)
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

			while (newChar.HasToken("_either"))
			{
				var either = newChar.GetToken("_either");
				var eitherChoice = Random.Next(-1, either.Tokens.Count);
				if (eitherChoice > -1)
					newChar.AddToken(either.Tokens[eitherChoice]);
				newChar.RemoveToken(either);
            }

			var removeThese = new List<Token>();

			foreach (Token token in newChar.Tokens)
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
				newChar.RemoveToken(token);

			while (newChar.HasToken("_copy"))
			{
				string path = newChar.GetToken("_copy").Text;
				newChar.RemoveToken("_copy");
				var source = newChar.Path(path);
				if (source == null)
					continue;
				newChar.AddToken(source.Clone(true));
			}

            newChar.StripInvalidItems();

#if MUTAMORPH
            if (!newChar.HasToken("beast"))
                newChar.Mutate(2, 20);
#endif

			//Program.WriteLine("Generated {0}.", newChar);
			return newChar;
		}

		public static Character GenerateQuick(string bodyPlan, Gender gender)
		{
			var bodyPlans = Mix.GetTokenTree("bodyplans.tml", true);

			var newChar = new Character();
			var planSource = bodyPlans.FirstOrDefault(t => t.Name == "bodyplan" && t.Text == bodyPlan);
			if (planSource == null)
				throw new ArgumentOutOfRangeException(string.Format("Could not find a bodyplan with id \"{0}\" to generate.", bodyPlan));

			newChar.AddSet(planSource.Tokens);
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
				newChar.RemoveToken("womb");
				newChar.RemoveToken("vagina");
				foreach (var breastRow in newChar.Tokens.Where(t => t.Name == "breastrow" && t.HasToken("size")))
					breastRow.GetToken("size").Value = 0;
			}
			else if (gender == Gender.Female || gender == Gender.Neuter)
			{
				newChar.RemoveToken("penis");
				newChar.RemoveToken("balls");
			}

			newChar.EnsureDefaultTokens();

			if (newChar.HasToken("femalesmaller"))
			{
				if (gender == Gender.Female)
					newChar.GetToken("tallness").Value -= Random.Next(5, 10);
				else if (gender == Gender.Herm)
					newChar.GetToken("tallness").Value -= Random.Next(1, 6);
			}

			while (newChar.HasToken("_either"))
			{
				var either = newChar.GetToken("_either");
				var eitherChoice = Random.Next(-1, either.Tokens.Count);
				if (eitherChoice > -1)
					newChar.AddToken(either.Tokens[eitherChoice]);
				newChar.RemoveToken(either);
			}

			var removeThese = new List<Token>();

			foreach (Token token in newChar.Tokens)
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
				newChar.RemoveToken(token);

			while (newChar.HasToken("_copy"))
			{
				string path = newChar.GetToken("_copy").Text;
				newChar.RemoveToken("_copy");
				var source = newChar.Path(path);
				if (source == null)
					continue;
				newChar.AddToken(source.Clone(true));
			}

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
			newChar.UpdateTitle();
			return newChar;
		}

		public void ApplyCostume()
		{
			if (HasToken("costume"))
				RemoveToken("costume");
			if (HasToken("beast"))
				return;
			if (!HasToken("lootset_id"))
				AddToken("lootset_id", 0, ID);
			var filters = new Dictionary<string, string>();
			filters["gender"] = PreferredGender.ToString().ToLowerInvariant();
			filters["board"] = Board.HackishBoardTypeThing;
			filters["culture"] = this.HasToken("culture") ? this.GetToken("culture").Text : "";
			filters["name"] = this.Name.ToString(true);
			filters["id"] = this.GetToken("lootset_id").Text;
			filters["bodymatch"] = this.GetClosestBodyplanMatch();
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
				Program.WriteLine("Had to remove {0} inventory item(s) from {1}: {2}", toDelete.Count, Name, string.Join(", ", toDelete));
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
			print(i18n.GetString(header1).PadEffective(pad) + i18n.GetString(header2) + "\n");
			for (var i = 0; i < totalRows; i++)
			{
				if (i < col1.Count)
					print(((i < col1.Count - 1 ? "\xC3 " : "\xC0 ") + i18n.GetString(col1[i], false)).PadEffective(pad));
				else
					print("".PadEffective(pad));
				if (i < col2.Count)
					print((i < col2.Count - 1 ? "\xC3 " : "\xC0 ") + i18n.GetString(col2[i], false));
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
						underpants.tempToken = carriedItem;
					}
					if (eq.HasToken("undershirt"))
					{
						undershirt = foundItem;
						undershirt.tempToken = carriedItem;
					}
					if (eq.HasToken("socks"))
					{
						socks = foundItem;
						socks.tempToken = carriedItem;
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
					if (eq.HasToken("shoes"))
					{
						shoes = foundItem;
						shoes.tempToken = carriedItem;
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
			if (shoes != null)
				worn.Add(shoes.ToLongString(shoes.tempToken));
			if (!(pa != null && pa is Player))
			{
				if (undershirt != null && (shirt == null || shirt.CanSeeThrough()))
				{
					breastsVisible = undershirt.CanSeeThrough();
					worn.Add(undershirt.ToLongString(undershirt.tempToken));
				}
				else
					breastsVisible = (shirt == null || shirt.CanSeeThrough());
				if (underpants != null && underpants != undershirt && (pants == null || pants.CanSeeThrough()))
				{
					crotchVisible = underpants.CanSeeThrough();
					worn.Add(underpants.ToLongString(underpants.tempToken));
				}
				else
					crotchVisible = (pants == null || pants.CanSeeThrough());
				if (socks != null && (pants == null || pants.CanSeeThrough()))
					worn.Add(socks.ToLongString(socks.tempToken));
			}
			else
			{
				if (undershirt != null)
					worn.Add(undershirt.ToLongString(undershirt.tempToken));
				if (underpants != null && underpants != undershirt)
					worn.Add(underpants.ToLongString(underpants.tempToken));
				if (socks != null)
					worn.Add(socks.ToLongString(socks.tempToken));
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
				var legs = string.IsNullOrWhiteSpace(lt) ? "human" : lt;
				int count;
				string number_or_pair = "counts";
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
					number_or_pair = "setbymeasure";
				bodyThings.Add(i18n.GetArray(number_or_pair)[count] + " " + i18n.Format("x_legs", i18n.GetString("legtype_" + legs)));
				if (this.HasToken("quadruped"))
					bodyThings.Add("quadruped");
				else if (this.HasToken("taur"))
				{
					var taur = (int)this.GetToken("taur").Value;
					if (taur < 2)
						bodyThings.Add(i18n.GetString("single_taur"));
					else if (taur > 1)
						bodyThings.Add(i18n.Format("multi_taur", taur + 1));
				}
			}

			if (this.HasToken("wings"))
			{
				var wt = i18n.Format("x_wings", i18n.GetString("wingtype_" + this.GetToken("wings").Text));
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
				if (goggles != null && !goggles.CanSeeThrough())
					eyesHidden = true;
				if (this.Path("eyes/glow") != null)
				{
					eyes = i18n.Format("glowing_x", eyes);
					eyesHidden = false;
				}
				eyes = i18n.GetArray("setbymeasure")[count] + " " + eyes;
				if (!eyesHidden)
					headThings.Add(eyes);
			}

			if (mask != null && mask.CanSeeThrough())
			{
				var teeth = this.Path("teeth");
				if (teeth != null && !string.IsNullOrWhiteSpace(teeth.Text) && teeth.Text != "normal")
					headThings.Add(i18n.GetString("teethtype_" + teeth.Text));
				var tongue = this.Path("tongue");
				//TRANSLATE
				if (tongue != null && !string.IsNullOrWhiteSpace(tongue.Text) && tongue.Text != "normal")
					headThings.Add(tongue.Text + " tongue");
			}

			//TRANSLATE - finish this block
			var ears = "human";
			if (this.HasToken("ears"))
				ears = this.GetToken("ears").Text;
			if (ears == "frill")
				headThings.Add("head frills");
			else
			{
				if (ears == "genbeast")
					ears = "animal";
				headThings.Add(i18n.Format("x_ears",  ears));
			}

			if (this.HasToken("monoceros"))
				headThings.Add("unicorn horn");

			//femininity slider


			//Columnize it!
			Columnize(print, 34, bodyThings, headThings, "lookat_column_body", "lookat_column_head");
		}

		private void LookAtHairHips(Entity pa, Action<string> print)
		{
			//TRANSLATE
			var hairThings = new List<string>();
			var hipThings = new List<string>();
			if (this.HasToken("hair") && this.Path("hair/length").Value > 0)
			{
				var hair = this.GetToken("hair");
				hairThings.Add(Descriptions.HairLength(hair));
				if (this.Path("skin/type").Text != "slime")
					hairThings.Add(Descriptions.HairColor(hair));
				if (this.Path("hair/style") != null)
					hairThings.Add(this.Path("hair/style").Text);
				if (this.Path("skin/type").Text == "slime")
					hairThings.Add("goopy");
				if (this.Path("skin/type").Text == "rubber")
					hairThings.Add("stringy");
				if (this.Path("skin/type").Text == "metal")
					hairThings.Add("cord-like");
			}

			if (this.HasToken("monoceros"))
				hairThings.Add(i18n.GetString("horntype_monoceros"));
			if (this.HasToken("horns"))
				hairThings.Add(i18n.Format("x_horntype_small_straight", this.GetToken("horns").Value).Viewpoint(this));

			if (!(HasToken("quadruped") || HasToken("taur")))
			{
				hipThings.Add(Descriptions.HipSize(this.GetToken("hips")) + " hips");
				hipThings.Add(Descriptions.WaistSize(this.GetToken("waist")) + " waist");
				hipThings.Add(Descriptions.ButtSize(this.Path("ass/size")) + " ass");
			}
			else
			{
				//hipThings.Add("quadruped");
			}

			if (this.HasToken("tail"))
			{
				var tt = this.GetToken("tail").Text;
				var tail = string.IsNullOrWhiteSpace(tt) ? "genbeast" : tt;
				if (tail == "bunny")
					hipThings.Add(i18n.GetString("tailtype_bunny"));
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

			Columnize(print, 34, hairThings, hipThings, "lookat_column_hair", "lookat_column_hips");
		}

		private void LookAtSexual(Entity pa, Action<string> print, bool breastsVisible, bool crotchVisible)
		{
			print(i18n.GetString("lookat_header_sexual"));
			//TRANSLATE
			var cocks = new List<Token>(); var vaginas = new List<Token>(); var breastRows = new List<Token>();
			Token nuts = null;
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


			print("Breasts: ");
			if (breastRows.Count == 0)
				print("none\n");
			else
			{
				print(Toolkit.Count(breastRows.Count) + " row" + (breastRows.Count == 1 ? "" : "s") + "\n");
				for (var i = 0; i < breastRows.Count; i++)
				{
					var row = breastRows[i];
					//if (HasToken("quadruped") && GetBreastRowSize(i) < 0.5)
					//	continue;
					print((i < breastRows.Count - 1 ? "\xC3 " : "\xC0 ") + Toolkit.Count(row.GetToken("amount").Value) + " " + Descriptions.GetSizeDescription("breasts/size", GetBreastRowSize(i)));
					if (breastsVisible && (row.Path("nipples") == null || row.Path("nipples").Value == 0))
						print(" nippleless");
					print(" breast");
					if (row.GetToken("amount").Value > 1)
						print("s");
					if (!breastsVisible || (row.Path("nipples") == null || row.Path("nipples").Value == 0))
					{
						print("\n");
						continue;
					}

					if (!(row.Path("nipples") == null) && !(row.Path("nipples").Value == 0))
					{
						var nipSize = 0.5f;
						if (row.Path("nipples/size") != null)
							nipSize = row.Path("nipples/size").Value;
						var nipType = Descriptions.NippleSize(row.GetToken("nipples")) + " " + Descriptions.Length(nipSize);
						if (row.Path("nipples/canfuck") != null)
							nipType += " dicknipple";
						else if (row.Path("nipples/fuckable") != null)
						{
							var loose = Descriptions.Looseness(row.Path("nipples/looseness"), false);
							var wet = Descriptions.Wetness(row.Path("nipples/wetness"));
							if (wet != null && loose != null)
								wet = " and " + wet;
							else if (wet == null && loose == null)
								loose = "";
							nipType += (" " + loose + wet + " nipplecunt").Trim();
						}
						else
							nipType += " nipple";
						print(", " + Toolkit.Count(row.GetToken("nipples").Value) + " " + nipType);
						if (row.GetToken("nipples").Value > 1)
							print("s");
						print(" on each\n");
					}
				}
			}
			print("\n");

			print("Vaginas: ");
			if (!crotchVisible)
			{
				if (this.PercievedGender == Gender.Male)
					print("can't tell, none assumed\n");
				else if (this.PercievedGender == Gender.Female)
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
						print((i < vaginas.Count - 1 ? "\xC3 " : "\xC0 ") + loose + wet + ", with a " + Descriptions.Length(clitSize) + " clit\n");
					}
				}
			}
			print("\n");

			print("Cocks: ");
			if (!crotchVisible)
			{
				if (this.PercievedGender == Gender.Male)
					print("can't tell, one assumed\n");
				else if (this.PercievedGender == Gender.Female)
					print("can't tell, none assumed\n");
				else
					print("can't tell\n");
			}
			else
			{
				var cocksAndBalls = cocks.Count + ballCount;
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
							cockType = "human";
						print((i < cocksAndBalls - 1 ? "\xC3 " : "\xC0 ") + cockType + ", " + Descriptions.Length(cock.GetToken("length").Value) + " long, ");
						print(Descriptions.Length(cock.GetToken("thickness").Value) + " thick\n");
					}
				}
				if (ballCount > 0)
				{
					print("\xC0 " + Toolkit.Count(ballCount) + " " + (ballSize < 1 ? "" : Descriptions.BallSize(nuts) + " ") + "testicles\n");
				}
			}
			print("\n");
		}

		private void LookAtClothing(Entity pa, Action<string> print, List<string> worn)
		{
			print("Clothing\n");
			if (worn.Count == 0)
				print("\xC0 none\n");
			else
				for (var i = 0; i < worn.Count; i++)
					print((i < worn.Count - 1 ? "\xC3 " : "\xC0 ") + worn[i] + "\n");
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
				print("\xC0 none\n");
			else
			{
				var handsAndFingers = new List<string>();
				handsAndFingers.AddRange(hands.Select(x => x.ToLongString(x.tempToken)));
				handsAndFingers.AddRange(fingers.Select(x => x.ToLongString(x.tempToken)));
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

			//Things not listed: pregnancy, horns and wings.
			if (print == null)
				print = new Action<string>(x => sb.Append(x));

			//var stimulation = this.GetToken("stimulation").Value;

			print(("Name: " + this.Name.ToString(true)).PadEffective(34) + "Type: " + this.Title + ((pa != null && pa is Player) ? " (player)" : "") + "\n");
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
			print("<cGray>Percieved gender: " + this.PercievedGender.ToString() + "\n");
			print("<cGray>Actual gender: " + this.ActualGender.ToString() + "\n");
			print("<cGray>Self-preferred gender: " + this.PreferredGender.ToString() + "\n");
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
			//TRANSLATE _ALL_ OF THIS
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

			/*
			if (isWinner)
				dump.WriteLine("<p><strong>Final result: Victory!</strong></p>");
			else
				dump.WriteLine("<p><strong>Final result: Death.</strong></p>");
			*/

			dump.WriteLine("<h2>Screenshot</h2>");
			NoxicoGame.HostForm.Noxico.CurrentBoard.CreateHtmlScreenshot(dump, false);

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
			if (HasToken("easymode"))
				dump.WriteLine("<li><strong>You were a total scrub.</strong></li>");
#if DEBUG
			else if (HasToken("wizard"))
				dump.WriteLine("<li>You were playing a debug build with the infinite lives cheat enabled.</li>");
#endif
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
						InventoryItem.TearApart(find, carriedItem);
						doReport(string.Format("[Youorname] [has] torn out of [his] {0}!", originalname).Viewpoint(this));
					}
					else
					{
                        if (this == NoxicoGame.HostForm.Noxico.Player.Character)
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
				//Assume that our spouse is on the same board.
				foreach (var ship in GetToken("ships").Tokens)
				{
					if (ship.HasToken("spouse") || ship.HasToken("friend"))
					{
						var them = BoardChar.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(x => x.Character.ID == ship.Name);
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
			var gestation = this.Path("pregnancy/gestation");
			if (gestation != null && gestation.Value == gestation.GetToken("max").Value / 2)
				score--;

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
					if (sizes[i] < 0) //just to be sure.
						sizes[i] = 0;
				}
				if (rows[i].HasToken("lactation"))
					sizes[i] += 0.25f * rows[i].GetToken("lactation").Value;
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
				var smallest = sizes[0];
				var ret = 0;
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
			var smallest = sizes[0];
			var ret = 0;
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
				var smallest = caps[0];
				var ret = 0;
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
			if (BoardChar == null)
				return false; //abandon pregnanship!
			//Disabled egglaying for now.
			/*
			if (this.HasToken("egglayer") && this.HasToken("vagina") && !this.HasToken("pregnancy"))
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
					egg.Take(this);
					if (BoardChar is Player)
						NoxicoGame.AddMessage(i18n.GetString("youareachicken").Viewpoint(this));
					return false;
				}
			}
			else
			*/
			if (this.HasToken("pregnancy"))
			{
				var pregnancy = this.GetToken("pregnancy");
				var gestation = pregnancy.GetToken("gestation");
				gestation.Value++;
				if (gestation.Value >= gestation.GetToken("max").Value)
				{
					var childName = new Name();
					childName.Female = Random.NextDouble() > 0.5;
					childName.NameGen = this.GetToken("namegen").Text;
					childName.Regenerate();
					if (childName.Surname.StartsWith("#patronym"))
						childName.ResolvePatronym(new Name(pregnancy.GetToken("father").Text), this.Name);

					var ships = this.GetToken("ships");
					ships.AddToken(childName.ToID()).AddToken("child");

					//Gotta grow a vagina if we don't have one right now.
					//if (!this.HasToken("vagina"))

					if (this.HasToken("player"))
					{
						var children = 0;
						foreach (var ship in ships.Tokens)
						{
							if (ship.HasToken("child"))
								children++;
						}
						if (children == 1)
						{
							//First time!
							MessageBox.Notice(i18n.Format("you_bear1stchild", childName.FirstName), true, i18n.GetString("congrats_mom"));
						}
						else
							MessageBox.Notice(i18n.Format("you_bearNthchild", childName.FirstName), true, i18n.GetString("congrats_mom"));
					}
					else if (pregnancy.GetToken("father").Text == NoxicoGame.HostForm.Noxico.Player.Character.Name.ToString(true))
					{
						NoxicoGame.AddMessage(i18n.Format("x_bearschild", this.Name, childName.FirstName));
					}

					this.RemoveToken("pregnancy");
					return true;
				}
				else if (gestation.Value == gestation.GetToken("max").Value / 2)
					CheckHasteSlow();
			}
			return false;
		}

		public bool Fertilize(Character father)
		{
			if (!this.HasToken("womb"))
				return false;
			var fertility = 0.0;
			if (this.HasToken("fertility"))
				fertility = this.GetToken("fertility").Value;
			//Simple version for now -- should involve the father, too.
			if (Random.Next() > fertility)
				return false;

			var pregnancy = AddToken("pregnancy");
			pregnancy.AddToken("gestation").AddToken("max", 1000); //TODO: tweak gestation time. Has to be high, though; gestation is in time, not turns!
			pregnancy.AddToken("father", 0, father.Name.ToString(true));
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

		public Func<string, string> GetSpeechFilter(Func<string, string> original = null)
		{
			if (i18n.GetString("meta_nospeechfilters")[0] == '[')
				return original;
			if (original == null)
				original = new Func<string, string>(x => x);
			if (this.GetToken("face").Text == "reptile")
				return new Func<string, string>(x => original(x.Replace("s", "sh").Replace("S", "Sh")));
			var match = this.GetClosestBodyplanMatch();
			if (match == "felinoid")
				return new Func<string, string>(x => original(x.Replace("r", "rr")));
			return original;
		}

		public string GetClosestBodyplanMatch()
		{
			var thisHash = Toolkit.GetBodyComparisonHash(this);
			var ret = "";
			var score = 999;
			foreach (var hash in NoxicoGame.BodyplanHashes)
			{
				var distance = Toolkit.GetHammingDistance(thisHash, hash.Value);
				if (distance < score)
				{
					score = distance;
					ret = hash.Key;
				}
			}
			if (score == 999)
				return "human";
			return ret;
		}

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
				"balls", "penis", "breastrow", "ass", "hips", "waist", "vagina",
				"legs", "skin", "ascii", "tallness", "hair", "face", "eyes",
				"teeth", "tongue", "legs", "quadruped", "monoceros", "horns",
				"tail", "ears", "slimeblob", "snaketail",
				"hostile", //lol that oughta be fun
			};
			var toCopyForSlimes = new[]
			{
				"balls", "penis", "breastrow", "vagina", /* "ass", "hips", "waist", */
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
						cursed.Text = i18n.GetString("item_changeling_disguise"); //"This is part of your disguise.";
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
				var myHash = Toolkit.GetBodyComparisonHash(this);
				var changeling = NoxicoGame.BodyplanHashes["changeling"];
				if (Toolkit.GetHammingDistance(myHash, changeling) >= 2)
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

		public bool CanReachBreasts()
		{
			var undershirt = GetEquippedItemBySlot("undershirt");
			var shirt = GetEquippedItemBySlot("shirt");
			var jacket = GetEquippedItemBySlot("jacket");
			var cloak = GetEquippedItemBySlot("cloak");
			return ((cloak == null || cloak.CanReachThrough()) &&
				(jacket == null || jacket.CanReachThrough()) &&
				(shirt == null || shirt.CanReachThrough()) &&
				(undershirt == null || undershirt.CanReachThrough()));
		}

		public bool CanReachCrotch()
		{
			var underpants = GetEquippedItemBySlot("underpants");
			var pants = GetEquippedItemBySlot("pants");
			var socks = GetEquippedItemBySlot("socks");
			return ((pants == null || pants.CanReachThrough()) &&
				(underpants == null || underpants.CanReachThrough()) &&
				(socks == null || socks.CanReachThrough()));
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
			if (Culture.NameGens.ContainsKey(namegen))
				newName.NameGen = namegen;
			return newName;
		}
	}
}
