using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Character
	{
		public List<Token> GetMorphDeltas(string targetPlan, Gender targetGender)
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

				var description = "[views] " + trivialSize + (growOrShrink > 0 ? " increases" : " decreases");
				//TODO: replace with a better i18n-based way to work.
				//Perhaps a key like "morph_tallness_1" or "morph_hips_-1".
				//For now, these'll do.
				switch (trivialSize)
				{
					case "tallness":
						description = "[view] grow{s} a bit " + (growOrShrink > 0 ? "taller" : "shorter");
						break;
					case "hips":
						description = "[views] hips " + (growOrShrink > 0 ? "flare out some more" : "become smaller");
						break;
					case "waist":
						description = "[views] waist grows " + (growOrShrink > 0 ? "a bit wider" : "a bit less wide");
						break;
				}

				var change = new Token(trivialSize, now + (growOrShrink * Random.Next(1, 3)));
				change.AddToken("$", description);
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

				var description = "[views] " + trivialKind + " turns " + changeKind;
				//TODO: similar as trivialSize above.

				var change = new Token(trivialKind, changeKind);
				change.AddToken("$", description);
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

				var description = "[views] " + trivialColor + " turns " + changeColor;
				//TODO: similar as trivialSize above.

				var change = new Token(trivialColor, changeColor);
				change.AddToken("$", description);
				possibleChanges.Add(change);
			}
			#endregion

			#region Skin
			{
				//TODO: RULE: a slime does NOT NEED to have a slimeblob, but a nonslime MAY NEVER have one.
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
					var description = "[views] body turns to " + skinColorThen + " " + skinTypeThen;
					var change = new Token("skin/type", skinTypeThen);
					change.AddToken("$", 0, description);
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
						var newChange = change.AddToken("$", "[views] lower body reforms into a pair of " + legsThen + " legs.");
						newChange.AddToken("legs", legsThen);
					}
					else if (skinTypeThen == "slime")
					{
						var newChange = new Token("_add", "slimeblob");
						newChange.AddToken("$", 0, "[views] legs dissolve into a puddle of goop.");
						newChange.AddToken("_remove", "legs");
						change.AddToken(newChange);
					}
				}
				else if (skinColorNow != skinColorThen)
				{
					var description = "[views] body turns " + skinColorThen;
					var change = new Token("skin/color", skinColorThen);
					change.AddToken("$", description);
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
						var description = "[views] hair turns " + hairColorThen;
						var change = new Token("hair/color", hairColorThen);
						change.AddToken("$", 0, description);
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
						var description = "[views] tail becomes " + tailThen.Text + "-like";
						var change = new Token("tail", tailThen.Text);
						change.AddToken("$", description);
						possibleChanges.Add(change);
					}
				}
				else if (tailNow == null && tailThen != null)
				{
					//Grow a tail
					var description = "[view] grow{s} a " + tailThen.Text + "-like tail";
					var change = new Token("tail", tailThen.Text);
					change.AddToken("$", description);
					possibleChanges.Add(change);
				}
				else if (tailNow != null && tailThen == null)
				{
					//Lose a tail
					var description = "[views] tail disappears";
					var change = new Token("_remove", "tail");
					change.AddToken("$", description);
					possibleChanges.Add(change);
				}
			}
			#endregion

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

			//TODO: Needs more testing.
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

			#region Penis
			{
				if (target.HasToken("maleonly") || (this.ActualGender == Gender.Male || this.ActualGender == Gender.Herm) || (!target.HasToken("femaleonly") && targetGender == Gender.Male) || targetGender == Gender.Herm)
				{
					//Grow at least one dick or change one
					var targetDicks = target.Tokens.Where(t => t.Name == "penis").ToList();
					var sourceDicks = this.Tokens.Where(t => t.Name == "penis").ToList();
					if (sourceDicks.Count < targetDicks.Count)
					{
						//Not enough dicks right now.
						var change = new Token("_add", "penis");
						var targetDick = targetDicks[sourceDicks.Count];
						change.AddToken("penis[" + sourceDicks.Count + "]", targetDick.Text);
						change.AddToken("_addto/penis[" + sourceDicks.Count + "]", "thickness");
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
						change.AddToken("penis[" + sourceDicks.Count + "]/thickness", targetThickness.Value);
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
						change.AddToken("_addto/penis[" + sourceDicks.Count + "]", "length");
						change.AddToken("penis[" + sourceDicks.Count + "]/length", targetLength.Value / 2);
						change.AddToken("$", "[view] grow{s} a " + targetDick.Text + " penis");
						possibleChanges.Add(change);
					}
					//else
					{
						//Compare sizes
						for (var i = 0; i < sourceDicks.Count; i++)
						{
							var sourceDick = sourceDicks[i];
							var targetDick = targetDicks[i < targetDicks.Count ? i : targetDicks.Count - 1];
							var targetLength = targetDick.GetToken("length");
							var targetThickness = targetDick.GetToken("thickness");
							var growOrShrink = 0;
							var thickOrThin = 0;
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
							if (growOrShrink == 1 && thickOrThin == 0)
								change = new Token("penis[" + i + "]/length", sourceDick.GetToken("length").Value + growOrShrink);
							else if (growOrShrink == 1 && thickOrThin == 1)
							{
								change = new Token("penis[" + i + "]/length", sourceDick.GetToken("length").Value + growOrShrink);
								change.AddToken("penis[" + i + "]/thickness", sourceDick.GetToken("thickness").Value + (thickOrThin * 0.25f));
							}
							else if (growOrShrink == 0 && thickOrThin == 1)
								change = new Token("penis[" + i + "]/thickness", sourceDick.GetToken("thickness").Value + (thickOrThin * 0.25f));

							if (change != null)
							{
								if (sourceDicks.Count > 1)
								{
									if (growOrShrink == 1 && thickOrThin == 0)
										change.AddToken("$", "[views] " + (i + 1).Ordinal() + " [?:cock] grows " + (growOrShrink > 0 ? "longer" : "shorter"));
									else if (growOrShrink == 1 && thickOrThin == 1)
										change.AddToken("$", "[views] " + (i + 1).Ordinal() + " [?:cock] grows " + (growOrShrink > 0 ? "longer" : "shorter") + " and " + (thickOrThin > 0 ? "thicker" : "thinner"));
									else if (growOrShrink == 0 && thickOrThin == 1)
										change.AddToken("$", "[views] " + (i + 1).Ordinal() + " [?:cock] grows " + (growOrShrink > 0 ? "thicker" : "thinner"));
								}
								else
								{
									if (growOrShrink == 1 && thickOrThin == 0)
										change.AddToken("$", "[views] [?:cock] grows " + (growOrShrink > 0 ? "longer" : "shorter"));
									else if (growOrShrink == 1 && thickOrThin == 1)
										change.AddToken("$", "[views] [?:cock] grows " + (growOrShrink > 0 ? "longer" : "shorter") + " and " + (thickOrThin > 0 ? "thicker" : "thinner"));
									else if (growOrShrink == 0 && thickOrThin == 1)
										change.AddToken("$", "[views] [?:cock] grows " + (growOrShrink > 0 ? "thicker" : "thinner"));
								}
								possibleChanges.Add(change);
							}

							//TODO: allow oneof
							if (sourceDick.Text != targetDick.Text)
							{
								change = new Token("penis[" + i + "]", targetDick.Text);
								if (sourceDicks.Count > 1)
									change.AddToken("$", "[views] " + (i + 1).Ordinal() + " [?:cock] becomes " + targetDick.Text);
								else
									change.AddToken("$", "[views] [?:cock] becomes " + targetDick.Text);
								possibleChanges.Add(change);
							}
						}
					}
				}
				else if (target.HasToken("femaleonly") || targetGender == Gender.Female)
				{
					//TODO: Shrink/remove dicks

				}
			}
			#endregion

			//TODO: Balls

			return possibleChanges;
		}

		public void ApplyMutamorphDeltas(List<Token> possibilities, int maxChanges, out string feedback)
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
				feedback = "Nothing happens.";
				return;
			};

			var feedbackBuilder = new StringBuilder();
			if (feedbacks.Count == 1)
			{
				feedbackBuilder.Append(feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"));
				feedbackBuilder.Append(".");
			}
			else if (feedbacks.Count == 2)
			{
				feedbackBuilder.Append(feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"));
				feedbackBuilder.Append(" and ");
				feedbackBuilder.Append(feedbacks[1].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				feedbackBuilder.Append(".");
			}
			else if (feedbacks.Count == 3)
			{
				feedbackBuilder.Append(feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"));
				feedbackBuilder.Append(", ");
				feedbackBuilder.Append(feedbacks[1].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				feedbackBuilder.Append(", and ");
				feedbackBuilder.Append(feedbacks[2].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				feedbackBuilder.Append(".");
			}
			else
			{
				feedbackBuilder.Append(feedbacks[0].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"));
				feedbackBuilder.Append(". ");
				feedbackBuilder.Append(feedbacks[1].Replace("[views]", "[His]").Replace("[view]", "[He]"));
				for (var i = 2; i < feedbacks.Count - 1; i++)
				{
					feedbackBuilder.Append(", ");
					feedbackBuilder.Append(feedbacks[i].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				}
				feedbackBuilder.Append(", and ");
				feedbackBuilder.Append(feedbacks[feedbacks.Count - 1].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				feedbackBuilder.Append(".");
			}
			//Perhaps have a case for extreme amounts where it splits up into various sentences and ends with a "finally"?
			feedback = feedbackBuilder.ToString().Viewpoint(this);

			foreach (var change in changes)
			{
				if (change.Name == "_remove")
				{
					this.RemoveToken(change.Text);
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
			if (this.HasToken("formlock"))
				return i18n.GetString("formlock").Viewpoint(this);
			var possibilities = GetMorphDeltas(targetPlan, targetGender);
			var feedback = string.Empty;
			ApplyMutamorphDeltas(possibilities, 4, out feedback);
			
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

			return feedback;
		}
	}
}
