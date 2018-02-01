﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Character
	{
		public List<Token> GetTargetedMorphDeltas(string targetPlan, Gender targetGender)
		{
			//Token.NoRolls = true;
			var target = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == targetPlan);
			if (target == null)
				throw new ArgumentException("No such bodyplan \"" + targetPlan + "\".");

			// fix: added a default penis to prevent null exceptions when not having suitable TargetPenis 
			if (!target.HasToken("penis"))
			{
				target.AddToken("penis");
				target.GetToken("penis").AddToken("thickness").Value = 2;
				target.GetToken("penis").AddToken("length").Value = 15;
			}

			if (target.HasToken("femaleonly"))
				targetGender = Gender.Female;
			else if (target.HasToken("maleonly"))
				targetGender = Gender.Male;
			else if (target.HasToken("hermonly"))
				targetGender = Gender.Herm;
			else if (target.HasToken("neuteronly"))
				targetGender = Gender.Neuter;

			if (targetGender == Gender.RollDice)
			{
				var min = 1;
				var max = 4;
				if (target.HasToken("normalgenders"))
					max = 2;
				else if (target.HasToken("neverneuter"))
					max = 3;
				var g = Random.Next(min, max + 1);
				targetGender = (Gender)g;
			}

			var meta = new[] { "playable", "culture", "namegen", "bestiary", "femalesmaller", "costume", "_either", "items" };
			var simpleTraits = new[] { "fireproof", "aquatic" };
			var trivialSizes = new[]
			{
				"tallness", "hips", "waist", "ass/size",
				"charisma", "cunning", "carnality", "sensitivity", "speed", "strength"
			};
			var trivialKinds = new[] { "face", "teeth", "tongue", "ears", "legs" };
			var trivialColors = new[] { "eyes" };
			var possibleChanges = new List<Token>();

			//Trivial changes are on the root level only. That makes them fairly easy to handle.

			#region Trivial size and rating changes
			foreach (var trivialSize in trivialSizes)
			{
				if (this.Path(trivialSize) == null || target.Path(trivialSize) == null)
					continue;
				var now = this.Path(trivialSize).Value;
				var thenToken = target.Path(trivialSize);
				var growOrShrink = 0;
				if (!string.IsNullOrWhiteSpace(thenToken.Text))
				{
					if (thenToken.Text.StartsWith("roll"))
					{
						var xDyPz = thenToken.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = range + plus;
						if (now < min)
							growOrShrink = 1; //Need to grow.
						else if (now > max)
							growOrShrink = -1; //Need to shrink.
					}
				}
				else
				{
					var then = thenToken.Value;
					if (now < then)
						growOrShrink = 1; //Need to grow.
					else if (now > then)
						growOrShrink = -1; //Need to shrink.
				}

				if (growOrShrink == 0)
					continue;

				var change = new Token(trivialSize, now + (growOrShrink * Random.Next(1, 3)));
				change.AddToken("$", i18n.GetString("morphpart_" + trivialSize.Replace('/', '_') + '_' + growOrShrink));
				possibleChanges.Add(change);
			}
			#endregion

			#region Trivial kind changes
			foreach (var trivialKind in trivialKinds)
			{
				if (this.Path(trivialKind) == null || target.Path(trivialKind) == null)
					continue;
				var now = this.Path(trivialKind).Text;
				var thenToken = target.Path(trivialKind);
				if (string.IsNullOrWhiteSpace(thenToken.Text))
					continue;
				var changeKind = string.Empty;
				if (thenToken.Text.StartsWith("oneof"))
				{
					var options = thenToken.Text.Substring(thenToken.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(now))
						changeKind = Toolkit.PickOne(options);
				}
				else if (thenToken.Text != now)
					changeKind = thenToken.Text;

				if (string.IsNullOrWhiteSpace(changeKind))
					continue;

				var change = new Token(trivialKind, changeKind);
				change.AddToken("$", i18n.GetString("morphpart_" + trivialKind + '_' + changeKind));
				possibleChanges.Add(change);
			}
			#endregion

			#region Trivial color changes
			//Yes, almost the same as trivialKinds. I'm keeping this separate for Reasons.
			foreach (var trivialColor in trivialColors)
			{
				if (this.Path(trivialColor) == null || target.Path(trivialColor) == null)
					continue;
				var now = this.Path(trivialColor).Text;
				var thenToken = target.Path(trivialColor);
				var changeColor = string.Empty;
				if (thenToken.Text.StartsWith("oneof"))
				{
					var options = thenToken.Text.Substring(thenToken.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(now))
						changeColor = Toolkit.PickOne(options);
				}
				else if (thenToken.Text != now)
					changeColor = thenToken.Text;

				if (string.IsNullOrWhiteSpace(changeColor))
					continue;

				var change = new Token(trivialColor, changeColor);
				change.AddToken("$", i18n.Format("morphpart_eyes_x", changeColor));
				possibleChanges.Add(change);
			}
			#endregion

			#region Skin
			{
				//RULE: a slime does NOT NEED to have a slimeblob, but a nonslime MAY NEVER have one.
				var skinNow = this.GetToken("skin");
				var skinThen = target.GetToken("skin");
				var skinTypeNow = skinNow.GetToken("type").Text;
				var skinColorNow = skinNow.GetToken("color").Text;
				var skinTypeThen = skinThen.GetToken("type").Text;
				var skinColorThen = skinThen.GetToken("color").Text;
				if (skinTypeThen.StartsWith("oneof"))
				{
					var options = skinTypeThen.Substring(skinTypeThen.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(skinTypeNow))
						skinTypeThen = Toolkit.PickOne(options);
					else
						skinTypeThen = skinTypeNow;
				}
				if (skinColorThen.StartsWith("oneof"))
				{
					var options = skinColorThen.Substring(skinColorThen.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(skinColorNow))
						skinColorThen = Toolkit.PickOne(options);
					else
						skinColorThen = skinColorNow;
				}
				if (skinTypeNow != skinTypeThen)
				{
					var change = new Token("skin/type", skinTypeThen);
					change.AddToken("$", 0, i18n.Format("morphpart_skin_" + skinTypeThen, skinColorThen));
					change.AddToken("skin/color", skinColorThen);
					possibleChanges.Add(change);

					if (skinTypeThen != "slime")
					{
						change = new Token("_remove", "slimeblob");
						//Force a leg change
						var legsThen = target.HasToken("legs") ? target.GetToken("legs").Text : "human";
						if (legsThen.StartsWith("oneof"))
						{
							var options = legsThen.Substring(legsThen.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
							legsThen = Toolkit.PickOne(options);
						}
						var newChange = change.AddToken("$", i18n.GetString("morphpart_slime_" + legsThen));
						newChange.AddToken("legs", legsThen);
					}
					else if (skinTypeThen == "slime")
					{
						var newChange = new Token("_add", "slimeblob");
						newChange.AddToken("$", 0, i18n.GetString("morphpart_slime_blob"));
						newChange.AddToken("_remove", "legs");
						change.AddToken(newChange);
					}
				}
				else if (skinColorNow != skinColorThen)
				{
					var change = new Token("skin/color", skinColorThen);
					change.AddToken("$", i18n.Format("morphpart_skin_color", skinColorThen));
					possibleChanges.Add(change);
				}
			}
			#endregion

			#region Hair
			{
				//TODO: If the target has no hair token or zero length, decrease the length. When near-zero, remove it entirely.
				//Leave the style alone, even if the target's styles don't include it!
				var hairNow = this.GetToken("hair");
				var hairThen = target.GetToken("hair");
				if (hairThen != null && hairThen.GetToken("length").Value > 0)
				{
					var hairColorNow = hairNow.GetToken("color").Text;
					var hairColorThen = hairThen.GetToken("color").Text;
					if (hairColorThen.StartsWith("oneof"))
					{
						var options = hairColorThen.Substring(hairColorThen.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
						if (!options.Contains(hairColorNow))
							hairColorThen = Toolkit.PickOne(options);
						else
							hairColorThen = hairColorNow;
					}
					if (hairColorNow != hairColorThen)
					{
						var change = new Token("hair/color", hairColorThen);
						change.AddToken("$", 0, i18n.Format("morphpart_hair_color", hairColorThen));
						possibleChanges.Add(change);
					}
				}
			}
			#endregion

			#region Tail
			{
				var tailNow = this.GetToken("tail");
				var tailThen = target.GetToken("tail");
				if (tailThen != null && tailThen.Text.StartsWith("oneof"))
				{
					var options = tailThen.Text.Substring(tailThen.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(tailNow.Text))
						tailThen.Text = Toolkit.PickOne(options);
					else
						tailThen.Text = tailNow.Text;
				}
				if (tailNow != null && tailThen != null)
				{
					//Change tails
					if (tailThen.Text != tailNow.Text)
					{
						var change = new Token("tail", tailThen.Text);
						change.AddToken("$", i18n.GetString("morphpart_tail_" + tailThen.Text));
						possibleChanges.Add(change);
					}
				}
				else if (tailNow == null && tailThen != null)
				{
					//Grow a tail
					var change = new Token("tail", tailThen.Text);
					change.AddToken("$", i18n.GetString("morphpart_gettail_" + tailThen.Text));
					possibleChanges.Add(change);
				}
				else if (tailNow != null && tailThen == null)
				{
					//Lose a tail
					var change = new Token("_remove", "tail");
					change.AddToken("$", i18n.GetString("morphpart_tail_lose"));
					possibleChanges.Add(change);
				}
			}
			#endregion

			//TODO: i18n this.
			#region Wings
			{
				var wingsNow = this.GetToken("wings");
				var wingsThen = target.GetToken("wings");
				if (wingsThen != null && wingsThen.Text.StartsWith("oneof"))
				{
					var options = wingsThen.Text.Substring(wingsThen.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(wingsNow.Text))
						wingsThen.Text = Toolkit.PickOne(options);
					else
						wingsThen.Text = wingsNow.Text;
				}
				if (wingsNow == null && wingsThen != null)
				{
					var change = new Token("wings", wingsThen.Text);
					if (wingsThen.HasToken("small"))
					{
						change.AddToken("_addto/wings", "small");
						change.AddToken("$", "[view] grow{s} a pair of small " + wingsThen.Text + " wings");
					}
					else
						change.AddToken("$", "[view] grow{s} a pair of " + wingsThen.Text + " wings");
					possibleChanges.Add(change);
				}
				else if (wingsNow != null && wingsThen == null)
				{
					if (wingsNow.HasToken("small"))
					{
						var change = new Token("_remove", "wings");
						change.AddToken("$", "[view] lose{s} [his] wings");
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_addto/wings", "small");
						change.AddToken("$", "[views] wings shrink");
						possibleChanges.Add(change);
					}
				}
				else if (wingsNow != null && wingsThen != null)
				{
					if (wingsNow.Text != wingsThen.Text)
					{
						var change = new Token("wings", wingsThen.Text);
						change.AddToken("$", "[views] wings become " + wingsThen.Text + "-like");
						possibleChanges.Add(change);
					}
					else if (wingsNow.HasToken("small") && !wingsThen.HasToken("small"))
					{
						var change = new Token("_remove", "wings/small");
						change.AddToken("$", "[views] wings grow larger");
						possibleChanges.Add(change);
					}
					else if (!wingsNow.HasToken("small") && wingsThen.HasToken("small"))
					{
						var change = new Token("_addto/wings", "small");
						change.AddToken("$", "[views] wings shrink");
						possibleChanges.Add(change);
					}
				}
			}
			#endregion

			//TODO: Needs more testing. Also, i18n this after.
			#region Legs
			if (target.HasToken("legs") && !this.HasToken("legs"))
			{
				var change = new Token("_add", "legs");
				var thenToken = target.Path("legs");
				var changeKind = string.Empty;
				if (thenToken.Text.StartsWith("oneof"))
				{
					var options = thenToken.Text.Substring(thenToken.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					changeKind = Toolkit.PickOne(options);
				}
				else
					changeKind = thenToken.Text;
				change.AddToken("legs", changeKind);
				foreach (var specialShit in new [] { "snaketail", "slimeblob" })
					if (this.HasToken(specialShit))
						change.AddToken("_remove", specialShit);
				change.AddToken("$", "[view] grow{s} " + changeKind + " legs");
				possibleChanges.Add(change);
			}
			else if (!target.HasToken("legs")) //&& this.HasToken("legs"))
			{
				var actuallyChanging = true;
				var targetLegs = string.Empty;
				var change = new Token("_remove", "legs");
				foreach (var specialShit in new[] { "snaketail", "slimeblob" })
				{
					if (target.HasToken(specialShit))
					{
						targetLegs = specialShit;
						break;
					}
				}
				foreach (var specialShit in new[] { "snaketail", "slimeblob" })
				{
					if (this.HasToken(specialShit))
					{
						if (specialShit != targetLegs)
						{
							change.Text = specialShit;
							//break;
						}
						else
						{
							actuallyChanging = false;
						}
					}
				}
				if (actuallyChanging)
				{
					change.AddToken("_add", targetLegs);
					switch (targetLegs)
					{
						case "snaketail": change.AddToken("$", "[views] lower body turns into a long, coiling snake tail"); break;
						//case "taur": change.AddToken("$", "[views] lower body turns into a quadrupedal, centauroid configuration"); break;
						//case "quad": change.AddToken("$", "[views] entire body turns into a quadrupedal setup"); break;
						case "slimeblob":	change.AddToken("$", "[views] entire lower body dissolves into a mass of goop"); break;
					}
				}
				possibleChanges.Add(change);
			}

			if (target.HasToken("legs"))
			{
				var multiLegs = new[] { "taur", "quadruped" };
				var targetHasMultiLegs = false;
				foreach (var multiLeg in multiLegs)
				{
					if (target.HasToken(multiLeg) && !this.HasToken(multiLeg))
					{
						targetHasMultiLegs = true;
						var change = new Token("_add", multiLeg);
						foreach (var m in multiLegs)
						{
							if (m != multiLeg)
								change.AddToken("_remove", m);
						}
						switch (multiLeg)
						{
							case "taur": change.AddToken("$", "[views] lower body turns into a quadrupedal, centauroid configuration"); break;
							case "quadruped": change.AddToken("$", "[views] entire body turns into a quadrupedal setup"); break;
						}
						possibleChanges.Add(change);
						break;
					}
				}
				if (!targetHasMultiLegs)
				{
					foreach (var m in multiLegs)
					{
						if (this.HasToken(m))
						{
							var change = new Token("_remove", m);
							if (target.HasToken("slimeblob"))
								change.AddToken("$", "[views] entire lower body dissolves into a mass of goop.");
							if (target.HasToken("snaketail"))
								change.AddToken("$", "[views] lower body turns into a long, coiling snake tail.");
							else
								change.AddToken("$", "[views] lower body reconfigures into a bipedal stance.");
							possibleChanges.Add(change);
						}
					}
				}
			}
			#endregion

			//TODO: Breasts

			//TODO: Vaginas

			//TODO: Rewrite this to simplify into a single penis token, assuming a dualcock has two identical ones.
			#region Penis
			//if (target.HasToken("maleonly") || (this.ActualGender == Gender.Male || this.ActualGender == Gender.Herm) || (!target.HasToken("femaleonly") && targetGender == Gender.Male) || targetGender == Gender.Herm)
			if (targetGender == Noxico.Gender.Male || targetGender == Noxico.Gender.Herm)
			{
				if (target.HasToken("penis") && !this.HasToken("penis"))
				{
					//Grow a dick, since we have none.

					var targetDick = target.GetToken("penis");
					var change = new Token("_add", "penis");
					change.AddToken("penis", targetDick.Text);
					change.AddToken("_addto/penis", "thickness");
					var targetThickness = targetDick.GetToken("thickness");
					if (targetThickness.Text.StartsWith("roll"))
					{
						var xDyPz = targetThickness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetThickness.Value = min;
					}
					change.AddToken("penis/thickness", targetThickness.Value);
					var targetLength = targetDick.GetToken("length");
					if (targetLength.Text.StartsWith("roll"))
					{
						var xDyPz = targetLength.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetLength.Value = min;
					}
					change.AddToken("_addto/penis", "length");
					change.AddToken("penis/length", targetLength.Value / 2);
					change.AddToken("$", i18n.Format("morphpart_grow_dick", targetDick.Text));
					possibleChanges.Add(change);
				}
				else
				{
					//Change a dick.

					var sourceDick = this.GetToken("penis");
					var targetDick = target.GetToken("penis");
					var targetLength = targetDick.GetToken("length");
					var targetThickness = targetDick.GetToken("thickness");
					var splitOrJoin = 0;
					var growOrShrink = 0;
					var thickOrThin = 0;

					if (targetDick.HasToken("dual") && !sourceDick.HasToken("dual"))
						splitOrJoin = 1;
					else if (!targetDick.HasToken("dual") && sourceDick.HasToken("dual"))
						splitOrJoin = -1;

					var now = sourceDick.GetToken("length").Value;
					if (!string.IsNullOrWhiteSpace(targetLength.Text) && targetLength.Text.StartsWith("roll"))
					{
						var xDyPz = targetLength.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = min + range;
						if (now < min)
							growOrShrink = 1;
						else if (now > max)
							growOrShrink = -1;
					}
					else
					{
						var then = targetLength.Value;
						if (now < then)
							growOrShrink = 1;
						else if (now > then)
							growOrShrink = -1;
					}
					if (targetGender == Noxico.Gender.Female || targetGender == Noxico.Gender.Neuter)
						growOrShrink = -1;

					now = sourceDick.GetToken("thickness").Value;
					if (!string.IsNullOrWhiteSpace(targetThickness.Text) && targetThickness.Text.StartsWith("roll"))
					{
						var xDyPz = targetThickness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = min + range;
						if (now < min)
							thickOrThin = 1;
						else if (now > max)
							thickOrThin = -1;
					}
					else
					{
						var then = targetThickness.Value;
						if (now < then)
							thickOrThin = 1;
						else if (now > then)
							thickOrThin = -1;
					}

					Token change = null;

					if (splitOrJoin != 0 && Random.Flip())
					{
						if (splitOrJoin == 1)
						{
							change = new Token("_addto/penis", "dual");
							change.AddToken("$", i18n.GetString("morphpart_cock_split"));
						}
						else
						{
							if (splitOrJoin == -1)
							{
								change = new Token("_removefrom/penis", "dual");
								change.AddToken("$", i18n.GetString("morphpart_cock_join"));
							}
						}
					}
					else
					{
						if (growOrShrink != 0)
							change = new Token("penis/length", sourceDick.GetToken("length").Value + growOrShrink);
						if (thickOrThin != 0)
						{
							if (change == null)
								change = new Token("penis/thickness", sourceDick.GetToken("thickness").Value + (thickOrThin * 0.25f));
							else
								change.AddToken("penis/thickness", sourceDick.GetToken("thickness").Value + (thickOrThin * 0.25f));
						}

						//I18N this with morphpart_penis_-1_-1 etc
						change.AddToken("$", i18n.GetString("morphpart_penis_" + growOrShrink + "_" + thickOrThin));
					}

					if (change != null)
						possibleChanges.Add(change);
				}
			}
			else if (target.HasToken("femaleonly") || targetGender == Gender.Female)
			{
				if (this.HasToken("penis"))
				{
					var sourceDick = this.GetToken("penis");
					if (sourceDick.GetToken("length").Value > 2)
					{
						var change = new Token("penis/length", sourceDick.GetToken("length").Value * 0.25f);
						change.AddToken("$", i18n.GetString("morphpart_penis_-1_0"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_remove", "penis");
						change.AddToken(new Token("_remove", "balls"));
						change.AddToken("$", i18n.GetString("morphpart_lose_dick"));
					}
					if (sourceDick.HasToken("dual") && Random.Flip())
					{
						var change = new Token("_removefrom/penis", "dual");
						change.AddToken("$", i18n.GetString("morphpart_cock_join"));
						possibleChanges.Add(change);
					}
				}
			}
			#endregion

			//TODO: Balls

			return possibleChanges;
		}

		public List<Token> GetWildMorphDeltas(float intensity, Mutations mutation)
		{
			if (mutation == Mutations.Random)
				mutation = (Mutations)Random.Next(Enum.GetNames(typeof(Mutations)).Length - 1); //subtract one from the length to account for Random = -1
			var closestBodyplan = Bodyplans.First(t => t.Text == GetClosestBodyplanMatch());
			var possibleChanges = new List<Token>();
			switch (mutation)
			{
				#region Breasts and nipples -- adding
				case Mutations.AddBreast:
					if (!this.HasToken("breasts"))
					{
						//gotta add breasts here and now goddamn
						var change = new Token("_add", "breasts");
						change.AddToken("_addto/breasts", "amount");
						change.AddToken("breasts/amount", 1);
						change.AddToken("_addto/breasts", "size");
						change.AddToken("breasts/size", 1);
						var nips = closestBodyplan.Path("breasts/nipples");
						if (nips != null)
						{
							//copy over the default nipples
							change.AddToken("_addto/breasts", "nipples");
							change.AddToken("breasts/nipples", 1);
							foreach (var nipTok in nips.Tokens)
							{
								change.AddToken("_addto/breasts/nipples", nipTok.Name);
								if (nipTok.Value != 0) change.AddToken("breasts/nipples/" + nipTok.Name, nipTok.Value);
								if (!nipTok.Text.IsBlank()) change.AddToken("breasts/nipples/" + nipTok.Name, nipTok.Text);
							}
						}
						change.AddToken("$", i18n.GetString("morph_gainbreast"));
						possibleChanges.Add(change);
					}
					else
					{
						//thank god we already have breasts!
						var change = new Token("breasts/amount", this.Path("breasts/amount").Value + 1);
						change.AddToken("$", i18n.Format("morph_gainmorebreasts", (this.Path("breasts/amount").Value + 1).Ordinal()));
						possibleChanges.Add(change);
					}
					break;
				case Mutations.AddNipple:
					var nipples = this.Path("breasts/nipples");
					if (nipples == null)
					{
						if (!this.HasToken("breasts"))
						{
							//Just give up here, we're not doin' this.
						}
						var nips = closestBodyplan.Path("breasts/nipples");
						if (nips != null)
						{
							//copy over the default nipples
							var change = new Token("_addto/breasts", "nipples");
							change.AddToken("breasts/nipples", 1);
							foreach (var nipTok in nips.Tokens)
							{
								change.AddToken("_addto/breasts/nipples", nipTok.Name);
								if (nipTok.Value != 0) change.AddToken("breasts/nipples/" + nipTok.Name, nipTok.Value);
								if (!nipTok.Text.IsBlank()) change.AddToken("breasts/nipples/" + nipTok.Name, nipTok.Text);
							}
							change.AddToken("$", i18n.GetString("morph_gainonenipple"));
							possibleChanges.Add(change);
						}
						else
						{
							//give 'em a hardcoded one, fuck it.
							var change = new Token("_addto/breasts", "nipples");
							change.AddToken("breasts/nipples", 1);
							change.AddToken("$", i18n.GetString("morph_gainonenipple"));
							possibleChanges.Add(change);
						}
					}
					else
					{
						//already have at least one! YAY!
						var change = new Token("breasts/nipples", this.Path("breasts/nipples").Value + 1);
						change.AddToken("$", i18n.GetString("morph_gainnipple")); //TODO: use morph_gaindicknipple and morph_gainnipplecunt.
						possibleChanges.Add(change);

					}
					break;
				#endregion
				#region Breasts and nipples -- changing
				case Mutations.GiveDicknipples:
					{
						var nips = this.Path("breasts/nipples");
						var change = new Token("_addto/breasts/nipples", "canfuck");
						if (nips != null && !nips.HasToken("fuckable"))
							change.AddToken("$", i18n.GetString("morph_gaindicknipples"));
						else
							change.AddToken("$", i18n.GetString("morph_nipplefail"));
						possibleChanges.Add(change);
						break;
					}
				case Mutations.GiveNipplecunts:
					{
						var nips = this.Path("breasts/nipples");
						var change = new Token("_addto/breasts/nipples", "fuckable");
						if (nips != null && !nips.HasToken("fuckable"))
							change.AddToken("$", i18n.GetString("morph_gainnipplecunts"));
						else
							change.AddToken("$", i18n.GetString("morph_nipplefail"));
						possibleChanges.Add(change);
						break;
					}
				case Mutations.GiveRegularNipples:
					{
						var nips = this.Path("breasts/nipples");
						var change = new Token("_dummy");
						change.AddToken("$", i18n.GetString("morph_nipplefail"));
						if (nips != null && nips.HasToken("canfuck"))
						{
							change = new Token("_removefrom/breasts/nipples", "canfuck");
							change.AddToken("$", i18n.GetString("morph_revertdicknipples"));
						}
						else if (nips != null && nips.HasToken("fuckable"))
						{
							change = new Token("_removefrom/breasts/nipples", "fuckable");
							change.AddToken("$", i18n.GetString("morph_revertnipplecunts"));
						}

						possibleChanges.Add(change);
						break;
					}

				#endregion
				#region Breasts and nipples -- removing
				case Mutations.RemoveBreast:
				case Mutations.RemoveNipple:
					throw new NotImplementedException();
				#endregion
				#region Odd lower bodies
				case Mutations.AddOddLegs:
					var funkyLegs = new[] { "taur", "quadruped", "snaketail", "slimeblob" };
					if (funkyLegs.All(x => !HasToken(x)))
					{
						var choice = "taur"; // Toolkit.PickOne(funkyLegs);
						var change = new Token("_add", choice);
						switch (choice)
						{
							case "snaketail":
							case "slimeblob":
								foreach (var funk in funkyLegs)
									if (funk != choice && this.HasToken(funk))
										change.AddToken("_remove", funk);
								if (this.HasToken("legs"))
									change.AddToken("_remove", "legs");
								if (this.HasToken("tail"))
									change.AddToken("_remove", "tail");
								change.AddToken("$", i18n.GetString("morph_grow" + choice));
								possibleChanges.Add(change);
								break;
							case "quadruped":
								foreach (var funk in funkyLegs)
									if (funk != choice && this.HasToken(funk))
										change.AddToken("_remove", funk);
								if (this.HasToken("taur"))
									change.AddToken("_remove", "taur");
								change.AddToken("$", i18n.GetString("morph_quadrify"));
								possibleChanges.Add(change);
								break;
							case "taur":
								foreach (var funk in funkyLegs)
									if (funk != choice && this.HasToken(funk))
										change.AddToken("_remove", funk);
								if (this.HasToken("quadruped"))
									change.AddToken("_remove", "quadruped");
								change.AddToken("$", i18n.GetString("morph_taurify"));
								possibleChanges.Add(change);
								break;
						}
					}
					else if (this.HasToken("taur"))
					{
						//choo-choo
						var change = new Token("taur", this.GetToken("taur").Value + 1);
						change.AddToken("$", i18n.GetString("morph_taurtrain"));
						possibleChanges.Add(change);
						break;
					}
					break;
				case Mutations.RemoveOddLegs:
					{
						foreach (var snakeSlime in new[] { "slimeblob", "snaketail" })
						{
							if (this.HasToken(snakeSlime))
							{
								var change = new Token("_remove", snakeSlime);
								change.AddToken("$", i18n.Format("morph_lose_x_and_gain_y_legs", i18n.GetString("morph_lose" + snakeSlime)));
								change.AddToken("_add", "legs");
								if (closestBodyplan.HasToken("legs"))
									change.AddToken("legs", closestBodyplan.GetToken("legs").Text);
								else
									change.AddToken("legs", "human");
								possibleChanges.Add(change);
								return possibleChanges;
							}
						}
						if (this.HasToken("taur") && this.GetToken("taur").Value > 0)
						{
							//oohc-ooch
							var change = new Token("taur", this.GetToken("taur").Value - 1);
							change.AddToken("$", i18n.GetString("morph_untaurtrain"));
							possibleChanges.Add(change);
							return possibleChanges;
						}
						foreach (var taurQuad in new[] { "taur", "quadruped" })
						{
							if (this.HasToken(taurQuad))
							{
								var change = new Token("_remove", taurQuad);
								change.AddToken("$", i18n.GetString("morph_bipedify"));
								possibleChanges.Add(change);
								return possibleChanges;
							}
						}
						var fail = new Token("_dummy");
						fail.AddToken("$", i18n.GetString("morph_legsfail"));
						possibleChanges.Add(fail);
						break;
					}
				#endregion
				#region Tails and Wings
				//case Mutations.AddTail:
				//case Mutations.AlterTail:
				#endregion
				#region Horns and Antennae
				//case Mutations.AddHorns:
				//case Mutations.AddAntennae:
				#endregion
				#region Skin and Hair
				//case Mutations.AlterSkin:
				//case Mutations.GrowHair:
				#endregion
				#region Genitalia -- adding
				case Mutations.AddPenis:
					if (!this.HasToken("penis"))
					{
						var change = new Token("_add", "penis");
						change.AddToken("_addto/penis", "length");
						change.AddToken("penis/length", (float)Random.NextDouble() * intensity + 12f);
						change.AddToken("_addto/penis", "thickness");
						change.AddToken("penis/thickness", (float)Random.NextDouble() * intensity / 4 + 4f);
						change.AddToken("$", i18n.GetString("morph_growcock"));
						possibleChanges.Add(change);
					}
					else if (!this.GetToken("penis").HasToken("dual"))
					{
						var change = new Token("_addto/penis", "dual");
						change.AddToken("$", i18n.GetString("morph_splitcock"));
						possibleChanges.Add(change);
					}
					else
					{
						//Instead of failing, let's just grow 'em!
						var change = new Token("penis/length", this.Path("penis/length").Value + (float)Random.NextDouble() * intensity + 2f);
						change.AddToken("penis/thickness", this.Path("penis/thickness").Value + (float)Random.NextDouble() * (intensity / 3));
						change.AddToken("$", i18n.GetString("morphpart_penis_1_0"));
						possibleChanges.Add(change);
					}
					break;
				case Mutations.AddVagina:
				case Mutations.AddTesticle:
					throw new NotImplementedException();
				#endregion
				#region Genitalia -- changing
				//case Mutations.AlterPenis:
				case Mutations.GrowPenis:
					if (!this.HasToken("penis"))
					{
						//Don't got one? Get one!
						var change = new Token("_add", "penis");
						change.AddToken("_addto/penis", "length");
						change.AddToken("penis/length", (float)Random.NextDouble() * intensity + 12f);
						change.AddToken("_addto/penis", "thickness");
						change.AddToken("penis/thickness", (float)Random.NextDouble() * intensity / 4 + 4f);
						change.AddToken("$", i18n.GetString("morph_growcock"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("penis/length", this.Path("penis/length").Value + (float)Random.NextDouble() * intensity + 2f);
						change.AddToken("penis/thickness", this.Path("penis/thickness").Value + (float)Random.NextDouble() * (intensity / 3));
						change.AddToken("$", i18n.GetString("morphpart_penis_1_0"));
						possibleChanges.Add(change);
					}
					break;
				//case Mutations.GrowVagina:
				//case Mutations.GrowTesticles:
				#endregion
				#region Genitalia -- removing
				//case Mutations.RemovePenis:
				//case Mutations.RemoveVagina:
				case Mutations.RemoveTesticle:
					throw new NotImplementedException();
				#endregion
			}

			return possibleChanges;
		}

		/*
		/// <summary>
		/// Applies a few random mutations to the Character. Contrast with Morph, which is more targeted.
		/// </summary>
		/// <param name="number">Amount of mutations to apply.</param>
		/// <param name="intensity">How much impact each mutation can have.</param>
		/// <param name="mutation">What kind of mutation.</param>
		/// <returns>Returns a list of report strings.</returns>
		public List<string> Mutate(int number, float intensity, Mutations mutation = Mutations.Random)
		{
			if (GetToken("perks").HasToken("formlock"))
				return new List<string>() { i18n.GetString("formlock").Viewpoint(this) };

			//TODO: use Morph Deltas, like GetTargetedMorphDeltas returns.
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
						throw new Exception("Something went wrong, and the mutation was not randomized properly.  Pester Kawa to fix it.");
					case Mutations.AddPenis:
						var cock = this.GetToken("penis");
						if (cock == null)
						{
							cock = this.AddToken("vagina");
							var length = (float)Random.NextDouble() * intensity + 12f;
							var thick = (float)Random.NextDouble() * intensity / 4 + 4f;
							cock.AddToken("length", length);
							cock.AddToken("thickness", thick);
							cock.AddToken("cumsource");
							report += i18n.GetString("morph_growcock");
						}
						else if (cock != null && !cock.HasToken("dual"))
						{
							cock.AddToken("dual");
							report += i18n.GetString("morph_splitcock");
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.AddVagina:
						var vagina = this.GetToken("vagina");
						if (vagina == null)
						{
							vagina = this.AddToken("vagina");
							vagina.AddToken("wetness", Random.Next((int)(intensity / 2)));
							vagina.AddToken("looseness", Random.Next((int)(intensity / 2)));
							report += i18n.GetString("morph_growpussy");
						}
						else if (vagina != null && !vagina.HasToken("dual"))
						{
							vagina.AddToken("dual");
							report += i18n.GetString("morph_splitpussy");
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.RemoveBreast:
						if (this.HasToken("breasts"))
						{
							var boob = this.GetToken("breasts");
							if (boob.GetToken("amount").Value > 1)
							{
								boob.GetToken("amount").Value--;
								report += i18n.GetString("morph_losebreast");
							}

							if (boob.GetToken("amount").Value == 0)
							{
								this.RemoveToken(boob);
								report += i18n.GetString("morph_loselastbreast");
							}

							if (!this.HasToken("breasts"))
							{
								this.AddToken("breasts").AddToken("size", 0);
								this.GetToken("breasts").AddToken("amount", 2);
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
							report += i18n.GetString("morph_gaintesticle");
						}
						else
						{
							var num = Random.Next((int)(intensity / 4)) + 1;
							var size = (float)Random.NextDouble() * intensity / 4 + 3f;
							this.AddToken("balls").AddToken("amount", num);
							this.GetToken("balls").AddToken("size", size);
							report += num > 1 ? i18n.Format("morph_gainballs", i18n.GetArray("counts")[num], Descriptions.BallSize(this.GetToken("balls")))
											  : i18n.GetString("morph_gainonenut");
						}
						break;
					case Mutations.RemoveTesticle:
						if (this.HasToken("balls"))
						{
							this.GetToken("balls").GetToken("amount").Value--;
							if (this.GetToken("balls").GetToken("amount").Value <= 0)
							{
								this.RemoveToken("balls");
								report += i18n.GetString("morph_loselastnut");
							}
							else
								report += i18n.Format("morph_loseonenut", this.GetToken("balls").GetToken("amount").Value.Count());
						}
						else
							report += "\uE2FC";
						break;
					case Mutations.RemoveNipple:
						if (this.Path("breasts/nipples") != null)
						{
							var boob = this.GetToken("breasts");
							boob.GetToken("nipples").Value--;
							var nippleName = (boob.GetToken("nipples").HasToken("canfuck") ? "dick" : "") +
								"nipple" + (boob.GetToken("nipples").HasToken("fuckable") ? "cunt" : "");
							if (boob.GetToken("nipples").Value == 0)
							{
								boob.RemoveToken("nipples");
								report += i18n.GetString("morph_lose" + nippleName + "s");
							}
							else
								report += i18n.GetString("morph_lose" + nippleName);
						}
						else
							report += "\uE2FC";
						break;
				}
				reports.Add(report);
			}
			FixBroken(); // very important
			return reports;
		}
		*/

		public void ApplyMorphDeltas(List<Token> possibilities, int maxChanges, out string feedback)
		{
			var numChanges = Math.Min(4, possibilities.Count);
			var changes = new List<Token>();
			var feedbacks = new List<string>();
			for (var i = 0; i < numChanges; i++)
			{
				var possibility = possibilities[Random.Next(possibilities.Count)];
				possibilities.Remove(possibility);
				changes.Add(possibility);
				foreach (var subChange in possibility.Tokens)
				{
					if (subChange.Name == "$")
					{
						feedbacks.Add(subChange.Text);
						continue;
					}
					changes.Add(subChange);
				}
			}
			if (changes.Count == 0)
			{
				feedback = i18n.GetString("morphfinal_nothing");
				return;
			};

			var feedbackBuilder = new StringBuilder();
			if (feedbacks.Count == 1)
			{
				feedbackBuilder.Append(i18n.Format("morphfinal_1", feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]")));
			}
			else if (feedbacks.Count == 2)
			{
				feedbackBuilder.Append(i18n.Format("morphfinal_2",
					feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"),
					feedbacks[1].Replace("[views]", "[his]").Replace("[view]", "[he]")));
			}
			else if (feedbacks.Count == 3)
			{
				feedbackBuilder.Append(i18n.Format("morphfinal_3",
					feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"),
					feedbacks[1].Replace("[views]", "[his]").Replace("[view]", "[he]"),
					feedbacks[2].Replace("[views]", "[his]").Replace("[view]", "[he]")));
			}
			else
			{
				feedbackBuilder.Append(i18n.Format("morphfinal_n1",
					feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"),
					feedbacks[1].Replace("[views]", "[His]").Replace("[view]", "[He]")));
				for (var i = 2; i < feedbacks.Count - 1; i++)
				{
					feedbackBuilder.Append(i18n.Format("morphfinal_n2", feedbacks[i].Replace("[views]", "[yourornames]").Replace("[view]", "[youorname]")));
				}
				feedbackBuilder.Append(i18n.Format("morphfinal_n3", feedbacks[feedbacks.Count - 1].Replace("[views]", "[he]").Replace("[view]", "[he]")));
			}
			//Perhaps have a case for extreme amounts where it splits up into various sentences and ends with a "finally"?
			feedback = feedbackBuilder.ToString().Viewpoint(this);

			foreach (var change in changes)
			{
				if (change.Name == "_dummy")
					continue;
				if (change.Name == "_remove")
				{
					this.RemoveToken(change.Text);
					continue;
				}
				if (change.Name == "_removefrom/")
				{
					var path = this.Path(change.Name.Substring(12));
					if (path != null)
						path.RemoveToken(change.Text);
					continue;
				}
				if (change.Name == "_add")
				{
					this.AddToken(change.Text);
					continue;
				}
				if (change.Name.StartsWith("_addto/"))
				{
					var path = this.Path(change.Name.Substring(7));
					if (path != null)
						path.AddToken(change.Text);
					continue;
				}
				var token = this.Path(change.Name) ?? this.AddToken(change.Name);
				if (!string.IsNullOrWhiteSpace(change.Text))
					token.Text = change.Text;
				else
					token.Value = change.Value;
			}
		}

		public string Morph(string targetPlan, Gender targetGender = Gender.Invisible)
		{
			if (GetToken("perks").HasToken("formlock"))
				return i18n.GetString("formlock").Viewpoint(this);
			var possibilities = GetTargetedMorphDeltas(targetPlan, targetGender);
			var feedback = string.Empty;
			ApplyMorphDeltas(possibilities, 4, out feedback);
			
			var closestMatch = GetClosestBodyplanMatch();
			var target = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == closestMatch);
			var myTerms = this.GetToken("terms");
			var closestTerms = target.GetToken("terms");
			if (myTerms.Tokens[0].Text != closestTerms.Tokens[0].Text)
			{
				for (var i = 0; i < myTerms.Tokens.Count && i < closestTerms.Tokens.Count; i++)
					myTerms.Tokens[i].Text = closestTerms.Tokens[i].Text;
				UpdateTitle();
			}

			SpeechFilter = null; //invalidate here -- we don't necessarily have this character speak right away and need to adjust for new impediments.

			return feedback;
		}

		/// <summary>
		/// Applies a few random mutations to the Character. Contrast with Morph, which is more targeted.
		/// </summary>
		/// <param name="number">Amount of mutations to apply.</param>
		/// <param name="intensity">How much impact each mutation can have.</param>
		/// <param name="mutation">What kind of mutation.</param>
		/// <returns>Returns a list of report strings.</returns>
		public string Mutate(int number, float intensity, Mutations mutation = Mutations.Random)
		{
			if (GetToken("perks").HasToken("formlock"))
				return i18n.GetString("formlock").Viewpoint(this);
			var fb = new StringBuilder();
			for (var i = 0; i < number; i++)
			{
				var possibilities = GetWildMorphDeltas(intensity, mutation);
				var feedback = string.Empty;
				ApplyMorphDeltas(possibilities, 4, out feedback);
				fb.Append(feedback);
				fb.Append(' ');
			}

			var closestMatch = GetClosestBodyplanMatch();
			var target = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == closestMatch);
			var myTerms = this.GetToken("terms");
			var closestTerms = target.GetToken("terms");
			if (myTerms.Tokens[0].Text != closestTerms.Tokens[0].Text)
			{
				for (var i = 0; i < myTerms.Tokens.Count && i < closestTerms.Tokens.Count; i++)
					myTerms.Tokens[i].Text = closestTerms.Tokens[i].Text;
				UpdateTitle();
			}

			SpeechFilter = null; //invalidate here -- we don't necessarily have this character speak right away and need to adjust for new impediments.

			return fb.ToString().Trim();
		}
	}
}
