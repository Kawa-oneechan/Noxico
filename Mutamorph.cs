using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public partial class Character
	{
		public List<Token> GetMorphDeltas(string targetPlan)
		{
			Token.NoRolls = true;
			var rawPlans = Mix.GetTokenTree("bodyplans.tml");
			var target = rawPlans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == targetPlan);
			if (target == null)
				throw new ArgumentException("No such bodyplan \"" + targetPlan + "\".");

			var meta = new[] { "playable", "culture", "namegen", "bestiary", "femalesmaller", "costume", "_either", "items" };
			var simpleTraits = new[] { "fireproof", "aquatic" };
			var trivialSizes = new[]
			{
				"tallness", "hips", "waist", "fertility", "ass/size",
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
					var description = "[view] grows a " + tailThen.Text + "-like tail";
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

			//TODO: Wings

			//TODO: Breastrows

			//TODO: Vaginas

			//TODO: Penises

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
				var token = this.Path(change.Name) ?? this.AddToken(change.Name);
				if (!string.IsNullOrWhiteSpace(change.Text))
					token.Text = change.Text;
				else
					token.Value = change.Value;
			}
		}

		public void Morph(string targetPlan)
		{
			var possibilities = GetMorphDeltas(targetPlan);
			var feedback = string.Empty;
			ApplyMutamorphDeltas(possibilities, 4, out feedback);
		}
	}
}
