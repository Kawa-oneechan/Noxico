using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/* A consideration.
 * 
 * Right now, mutamorph feedback is given pre-i18n'd, and one can gain a thing and then shortly if not immediately after lose it again.
 * Detecting such a thing would be pretty easy if we could somehow tag the feedback bits for recognition -- if we find a "gain dick"
 * and "lose dick" in the same list of feedbacks, we know we can probably remove it. Maybe not if we have an "alter dick" in there too.
 * The easiest way to do it, I think? Rewrite the feedback text to use the various placeholder tags! Instead of returning "you grew a
 * 3rd [?:breast]", return "morphpart_grewabreast", which would *later* resolve to "[view] grew a [ord:[v:breasts/amount]] [?:breast]",
 * and then finally be shown on screen as "you grew a 3rd bap". That way, we can count how many times "morphpart_grewabreast" appears
 * and compare it to how many times "morphpart_lostabreast" does.
 */

namespace Noxico
{
	public enum Mutations
	{
		Random = -1, AddPenis, AddVagina, AddOddLegs, RemoveOddLegs, AddBreast, RemoveBreast, AddTesticle, RemoveTesticle,
		GiveDicknipples, GiveNipplecunts, AddNipple, RemoveNipple, GiveRegularNipples, RemovePenis, RemoveVagina,
		AddTail
	}

	public partial class Character
	{
		public List<Token> GetTargetedMorphDeltas(string targetPlan, Gender targetGender)
		{
			//Token.NoRolls = true;
			var target = Character.Bodyplans.FirstOrDefault(x => x.Name == "bodyplan" && x.Text == targetPlan);
			if (target == null)
				throw new ArgumentException(string.Format("No such bodyplan \"{0}\".", targetPlan));

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
				//TODO: take these from stats.lua?
				"charisma", "mind", "vice", "libido", "speed", "body"
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
				if (!thenToken.Text.IsBlank())
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
				if (thenToken.Text.IsBlank())
					continue;
				var changeKind = string.Empty;
				if (thenToken.Text.StartsWith("oneof"))
				{
					var options = thenToken.Text.Substring(thenToken.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(now))
						changeKind = options.PickOne();
				}
				else if (thenToken.Text != now)
					changeKind = thenToken.Text;

				if (changeKind.IsBlank())
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
						changeColor = options.PickOne();
				}
				else if (thenToken.Text != now)
					changeColor = thenToken.Text;

				if (changeColor.IsBlank())
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
						skinTypeThen = options.PickOne();
					else
						skinTypeThen = skinTypeNow;
				}
				if (skinColorThen.StartsWith("oneof"))
				{
					var options = skinColorThen.Substring(skinColorThen.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(skinColorNow))
						skinColorThen = options.PickOne();
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
							legsThen = options.PickOne();
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
							hairColorThen = options.PickOne();
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
				else if (hairThen == null || hairThen.GetToken("length").Value == 0)
				{
					var hairLengthNow = hairNow.GetToken("length").Value;
					if (hairLengthNow > 0.25f)
					{
						var change = new Token("hair/length", hairLengthNow - (hairLengthNow / 2));
						change.AddToken("$", 0, i18n.GetString("morphpart_hair_shorten"));
					}
					else
					{
						var change = new Token("_remove", "hair");
						change.AddToken("$", 0, i18n.GetString("morphpart_hair_embalden"));
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
						tailThen.Text = options.PickOne();
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

			#region Wings
			{
				var wingsNow = this.GetToken("wings");
				var wingsThen = target.GetToken("wings");
				if (wingsThen != null && wingsThen.Text.StartsWith("oneof"))
				{
					var options = wingsThen.Text.Substring(wingsThen.Text.IndexOf("of ") + 3).Split(',').Select(x => x.Trim()).ToArray();
					if (!options.Contains(wingsNow.Text))
						wingsThen.Text = options.PickOne();
					else
						wingsThen.Text = wingsNow.Text;
				}
				if (wingsNow == null && wingsThen != null)
				{
					var change = new Token("wings", wingsThen.Text);
					if (wingsThen.HasToken("small"))
					{
						change.AddToken("_addto/wings", "small");
						change.AddToken("$", i18n.Format("morphpart_getsmallwings", wingsThen.Text));
					}
					else
						change.AddToken("$", i18n.Format("morphpart_getwings", wingsThen.Text));
					possibleChanges.Add(change);
				}
				else if (wingsNow != null && wingsThen == null)
				{
					if (wingsNow.HasToken("small"))
					{
						var change = new Token("_remove", "wings");
						change.AddToken("$", i18n.GetString("morphpart_losewings"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_addto/wings", "small");
						change.AddToken("$", i18n.GetString("morphpart_shrinkwings"));
						possibleChanges.Add(change);
					}
				}
				else if (wingsNow != null && wingsThen != null)
				{
					if (wingsNow.Text != wingsThen.Text)
					{
						var change = new Token("wings", wingsThen.Text);
						change.AddToken("$", i18n.GetString("morphpart_gettail_" + wingsThen.Text));
						possibleChanges.Add(change);
					}
					else if (wingsNow.HasToken("small") && !wingsThen.HasToken("small"))
					{
						var change = new Token("_remove", "wings/small");
						change.AddToken("$", i18n.GetString("morphpart_growwings"));
						possibleChanges.Add(change);
					}
					else if (!wingsNow.HasToken("small") && wingsThen.HasToken("small"))
					{
						var change = new Token("_addto/wings", "small");
						change.AddToken("$", i18n.GetString("morphpart_shrinkwings"));
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
					changeKind = options.PickOne();
				}
				else
					changeKind = thenToken.Text;
				change.AddToken("legs", changeKind);
				foreach (var specialShit in new[] { "snaketail", "slimeblob" })
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
						case "slimeblob": change.AddToken("$", "[views] entire lower body dissolves into a mass of goop"); break;
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

			#region Breasts
			//No need to *add* breasts -- you always have them even if flat.
			{
				//Change breasts.

				var sourceBreasts = this.GetToken("breasts");
				var targetBreasts = target.GetToken("breasts");
				var targetAmount = targetBreasts.GetToken("amount");
				var targetSize = targetBreasts.GetToken("size");
				var targetNipples = targetBreasts.GetToken("nipples");
				var targetNippleSize = targetBreasts.GetToken("nipples").GetToken("size");
				var addOrRemove = 0;
				var growOrShrink = 0;
				var nipplesAddOrRemove = 0;
				var nipplesGrowOrShrink = 0;

				var now = sourceBreasts.GetToken("amount").Value;
				if (!targetAmount.Text.IsBlank() && targetAmount.Text.StartsWith("roll"))
				{
					var xDyPz = targetAmount.Text;
					var range = 0;
					var plus = 0;
					xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
					ParseRoll(xDyPz, out range, out plus);
					var min = plus;
					var max = min + range;
					if (now < min)
						addOrRemove = 1;
					else if (now > max)
						addOrRemove = -1;
				}
				else
				{
					var then = targetAmount.Value;
					if (now < then)
						addOrRemove = 1;
					else if (now > then)
						addOrRemove = -1;
				}

				now = sourceBreasts.GetToken("size").Value;
				if (!targetSize.Text.IsBlank() && targetSize.Text.StartsWith("roll"))
				{
					var xDyPz = targetSize.Text;
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
					var then = targetSize.Value;
					if (now < then)
						growOrShrink = 1;
					else if (now > then)
						growOrShrink = -1;
				}
				if (this.Gender == Noxico.Gender.Male)
					growOrShrink = -1;

				now = sourceBreasts.GetToken("nipples").Value;
				if (!targetNipples.Text.IsBlank() && targetNipples.Text.StartsWith("roll"))
				{
					var xDyPz = targetNipples.Text;
					var range = 0;
					var plus = 0;
					xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
					ParseRoll(xDyPz, out range, out plus);
					var min = plus;
					var max = min + range;
					if (now < min)
						nipplesAddOrRemove = 1;
					else if (now > max)
						nipplesAddOrRemove = -1;
				}
				else
				{
					var then = targetNipples.Value;
					if (now < then)
						nipplesAddOrRemove = 1;
					else if (now > then)
						nipplesAddOrRemove = -1;
				}

				now = sourceBreasts.GetToken("nipples").GetToken("size").Value;
				if (!targetNippleSize.Text.IsBlank() && targetNippleSize.Text.StartsWith("roll"))
				{
					var xDyPz = targetNippleSize.Text;
					var range = 0;
					var plus = 0;
					xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
					ParseRoll(xDyPz, out range, out plus);
					var min = plus;
					var max = min + range;
					if (now < min)
						nipplesGrowOrShrink = 1;
					else if (now > max)
						nipplesGrowOrShrink = -1;
				}
				else
				{
					var then = targetNippleSize.Value;
					if (now < then)
						nipplesGrowOrShrink = 1;
					else if (now > then)
						nipplesGrowOrShrink = -1;
				}

				Token change = null;

				if (addOrRemove != 0 && Random.Flip())
				{
					change = new Token("breasts/amount", sourceBreasts.GetToken("amount").Value + addOrRemove);
					change.AddToken("$", i18n.GetString("morphpart_breasts_" + addOrRemove));
					possibleChanges.Add(change);
				}
				else if (growOrShrink != 0 && Random.Flip())
				{
					change = new Token("breasts/size", sourceBreasts.GetToken("size").Value + (growOrShrink * 0.25f));
					//I18N this with morphpart_breasts_-1 etc
					change.AddToken("$", i18n.GetString("morphpart_breastsize_" + growOrShrink));
					possibleChanges.Add(change);
				}
				else if (nipplesGrowOrShrink != 0 && Random.Flip())
				{
					change = new Token("breasts/nipples/size", sourceBreasts.GetToken("nipples").GetToken("size").Value + (nipplesGrowOrShrink * 0.25f));
					//I18N this with morphpart_nipplesize_-1 etc
					change.AddToken("$", i18n.GetString("morphpart_nipplesize_" + growOrShrink));
					possibleChanges.Add(change);
				}
				else if (nipplesAddOrRemove != 0 && Random.Flip() && Random.Flip())
				{
					change = new Token("breasts/nipples", sourceBreasts.GetToken("nipples").Value + nipplesAddOrRemove);
					//I18N this with morphpart_nipples_-1 etc
					change.AddToken("$", i18n.GetString("morphpart_nipples_" + nipplesAddOrRemove));
					possibleChanges.Add(change);
				}
			}
			#endregion

			#region Vagina
			if (targetGender == Noxico.Gender.Female || targetGender == Noxico.Gender.Herm)
			{
				if (target.HasToken("vagina") && !this.HasToken("vagina"))
				{
					//Grow a dick, since we have none.

					var targetVagina = target.GetToken("vagina");
					var change = new Token("_add", "vagina");
					change.AddToken("vagina", targetVagina.Text);
					change.AddToken("_addto/vagina", "virgin");
					change.AddToken("_addto/vagina", "looseness");
					var targetLooseness = targetVagina.GetToken("looseness");
					if (targetLooseness.Text.StartsWith("roll"))
					{
						var xDyPz = targetLooseness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetLooseness.Value = min;
					}
					change.AddToken("vagina/looseness", targetLooseness.Value);
					var targetWetness = targetVagina.GetToken("wetness");
					if (targetWetness.Text.StartsWith("roll"))
					{
						var xDyPz = targetWetness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetWetness.Value = min;
					}
					change.AddToken("_addto/vagina", "wetness");
					change.AddToken("vagina/wetness", targetWetness.Value / 2);
					change.AddToken("_addto/vagina", "clit");
					var targetClit = targetVagina.GetToken("clit");
					if (targetClit.Text.StartsWith("roll"))
					{
						var xDyPz = targetClit.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetClit.Value = min;
					}
					change.AddToken("vagina/clit", targetClit.Value);
					change.AddToken("$", i18n.Format("morphpart_grow_pussy", targetVagina.Text));
					possibleChanges.Add(change);
				}
				else
				{
					//Change a pussy.

					var sourceVagina = this.GetToken("vagina");
					var targetVagina = target.GetToken("vagina");
					var targetLooseness = targetVagina.GetToken("looseness");
					var targetWetness = targetVagina.GetToken("wetness");
					var targetClit = targetVagina.GetToken("clit");
					var splitOrJoin = 0;
					var loosenOrTighten = 0;
					var wettenOrDry = 0;
					var growOrShrink = 0;

					if (targetVagina.HasToken("dual") && !sourceVagina.HasToken("dual"))
						splitOrJoin = 1;
					else if (!targetVagina.HasToken("dual") && sourceVagina.HasToken("dual"))
						splitOrJoin = -1;

					var now = sourceVagina.GetToken("looseness").Value;
					if (!targetLooseness.Text.IsBlank() && targetLooseness.Text.StartsWith("roll"))
					{
						var xDyPz = targetLooseness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = min + range;
						if (now < min)
							loosenOrTighten = 1;
						else if (now > max)
							loosenOrTighten = -1;
					}
					else
					{
						var then = targetLooseness.Value;
						if (now < then)
							loosenOrTighten = 1;
						else if (now > then)
							loosenOrTighten = -1;
					}
					if (targetGender == Noxico.Gender.Male || targetGender == Noxico.Gender.Neuter)
						loosenOrTighten = -1;

					now = sourceVagina.GetToken("wetness").Value;
					if (!targetWetness.Text.IsBlank() && targetWetness.Text.StartsWith("roll"))
					{
						var xDyPz = targetWetness.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = min + range;
						if (now < min)
							wettenOrDry = 1;
						else if (now > max)
							wettenOrDry = -1;
					}
					else
					{
						var then = targetWetness.Value;
						if (now < then)
							wettenOrDry = 1;
						else if (now > then)
							wettenOrDry = -1;
					}

					now = sourceVagina.GetToken("clit").Value;
					if (!targetClit.Text.IsBlank() && targetClit.Text.StartsWith("roll"))
					{
						var xDyPz = targetClit.Text;
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
						var then = targetClit.Value;
						if (now < then)
							growOrShrink = 1;
						else if (now > then)
							growOrShrink = -1;
					}

					Token change = null;

					if (splitOrJoin != 0 && Random.Flip())
					{
						if (splitOrJoin == 1)
						{
							change = new Token("_addto/vagina", "dual");
							change.AddToken("$", i18n.GetString("morphpart_pussy_split"));
						}
						else
						{
							if (splitOrJoin == -1)
							{
								change = new Token("_removefrom/vagina", "dual");
								change.AddToken("$", i18n.GetString("morphpart_pussy_join"));
							}
						}
					}
					else
					{
						if (loosenOrTighten != 0)
							change = new Token("vagina/looseness", sourceVagina.GetToken("looseness").Value + loosenOrTighten);
						if (wettenOrDry != 0)
						{
							if (change == null)
								change = new Token("vagina/wetness", sourceVagina.GetToken("wetness").Value + (wettenOrDry * 0.25f));
							else
								change.AddToken("vagina/wetness", sourceVagina.GetToken("wetness").Value + (wettenOrDry * 0.25f));
						}

						//I18N this with morphpart_pussy_-1_-1 etc
						change.AddToken("$", i18n.GetString("morphpart_pussy_" + loosenOrTighten + "_" + wettenOrDry));
					}

					if (change != null)
						possibleChanges.Add(change);


					if (growOrShrink != 0)
					{
						change = new Token("vagina/clit", sourceVagina.GetToken("clit").Value + (growOrShrink * 0.25f));
						change.AddToken("$", i18n.GetString("morphpart_clit_" + growOrShrink));
						possibleChanges.Add(change);
					}
				}
			}
			else if (target.HasToken("maleonly") || targetGender == Gender.Male)
			{
				if (this.HasToken("vagina"))
				{
					var sourceVagina = this.GetToken("vagina");
					if (sourceVagina.HasToken("dual"))
					{
						var change = new Token("_removefrom/vagina", "dual");
						change.AddToken("$", i18n.GetString("morphpart_pussy_join"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_remove", "vagina");
						change.AddToken("$", i18n.GetString("morphpart_lose_pussy"));
					}
				}
			}
			#endregion

			#region Penis
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
					if (!targetLength.Text.IsBlank() && targetLength.Text.StartsWith("roll"))
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
					if (!targetThickness.Text.IsBlank() && targetThickness.Text.StartsWith("roll"))
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

			#region Balls
			if (targetGender == Noxico.Gender.Male || targetGender == Noxico.Gender.Herm)
			{
				if (target.HasToken("balls") && !this.HasToken("balls"))
				{
					//Grow a pair, since we have none.

					var targetBalls = target.GetToken("balls");
					var change = new Token("_add", "balls");
					change.AddToken("_addto/balls", "amount");
					var targetAmount = targetBalls.GetToken("amount");
					if (targetAmount.Text.StartsWith("roll"))
					{
						var xDyPz = targetAmount.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetAmount.Value = min;
					}
					change.AddToken("balls/amount", targetAmount.Value);
					var targetSize = targetBalls.GetToken("size");
					if (targetSize.Text.StartsWith("roll"))
					{
						var xDyPz = targetSize.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						targetSize.Value = min;
					}
					change.AddToken("_addto/balls", "size");
					change.AddToken("balls/size", targetSize.Value / 2);
					change.AddToken("$", i18n.Format("morphpart_grow_balls", targetBalls.Text));
					possibleChanges.Add(change);
				}
				else
				{
					//Change a pair.

					var sourceBalls = this.GetToken("penis");
					var targetBalls = target.GetToken("penis");
					var targetAmount = targetBalls.GetToken("amount");
					var targetSize = targetBalls.GetToken("size");
					var addOrRemove = 0;
					var growOrShrink = 0;

					var now = sourceBalls.GetToken("amount").Value;
					if (!targetAmount.Text.IsBlank() && targetAmount.Text.StartsWith("roll"))
					{
						var xDyPz = targetAmount.Text;
						var range = 0;
						var plus = 0;
						xDyPz = xDyPz.Substring(xDyPz.LastIndexOf(' ') + 1);
						ParseRoll(xDyPz, out range, out plus);
						var min = plus;
						var max = min + range;
						if (now < min)
							addOrRemove = 1;
						else if (now > max)
							addOrRemove = -1;
					}
					else
					{
						var then = targetAmount.Value;
						if (now < then)
							addOrRemove = 1;
						else if (now > then)
							addOrRemove = -1;
					}
					if (targetGender == Noxico.Gender.Female || targetGender == Noxico.Gender.Neuter)
						addOrRemove = -1;

					now = sourceBalls.GetToken("size").Value;
					if (!targetSize.Text.IsBlank() && targetSize.Text.StartsWith("roll"))
					{
						var xDyPz = targetSize.Text;
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
						var then = targetSize.Value;
						if (now < then)
							growOrShrink = 1;
						else if (now > then)
							growOrShrink = -1;
					}

					Token change = null;

					var newBalls = sourceBalls.GetToken("amount").Value + addOrRemove;
					if (addOrRemove != 0)
					{
						change = new Token("balls/amount", newBalls);
					}
					if (growOrShrink != 0)
					{
						if (change == null)
							change = new Token("balls/size", sourceBalls.GetToken("size").Value + (growOrShrink * 0.25f));
						else
							change.AddToken("balls/size", sourceBalls.GetToken("size").Value + (growOrShrink * 0.25f));
					}
					if (newBalls == 0)
					{
						change = new Token("_remove", "balls");
						change.AddToken("$", i18n.GetString("morphpart_lose_balls"));
					}
					else
					{
						//I18N this with morphpart_balls_-1_-1 etc
						change.AddToken("$", i18n.GetString("morphpart_balls_" + addOrRemove + "_" + growOrShrink));
					}

					if (change != null)
						possibleChanges.Add(change);
				}
			}
			else if (target.HasToken("femaleonly") || targetGender == Gender.Female)
			{
				if (this.HasToken("balls"))
				{
					var sourceBalls = this.GetToken("balls");
					if (sourceBalls.GetToken("amount").Value > 1)
					{
						var change = new Token("balls/amount", sourceBalls.GetToken("amount").Value - 1);
						change.AddToken("$", i18n.GetString("morphpart_balls_-1_0"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_remove", "balls");
						change.AddToken(new Token("_remove", "balls"));
						change.AddToken("$", i18n.GetString("morphpart_lose_balls"));
					}
				}
			}
			#endregion

			return possibleChanges;
		}

		public List<Token> GetWildMorphDeltas(float intensity, Mutations mutation)
		{
			if (mutation == Mutations.Random)
				mutation = (Mutations)Random.Next(Enum.GetNames(typeof(Mutations)).Length - 1); //subtract one from the length to account for Random = -1
			var closestBodyplan = Bodyplans.First(t => t.Text == GetClosestBodyplanMatch());
			var possibleChanges = new List<Token>();
			var funkyLegs = new[] { "taur", "quadruped", "snaketail", "slimeblob" };
			var tailTypes = Descriptions.descTable.GetToken("tail").Tokens.Select(t => t.Name).ToArray();
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
						if (this.Path("breasts/nipples/canfuck") != null)
							change.AddToken("$", i18n.GetString("morph_gaindicknipple"));
						else if (this.Path("breasts/nipples/fuckable") != null)
							change.AddToken("$", i18n.GetString("morph_gainnipplecunt"));
						else
							change.AddToken("$", i18n.GetString("morph_gainnipple"));
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
					break; //throw new NotImplementedException();
				#endregion
				#region Odd lower bodies
				case Mutations.AddOddLegs:
					if (funkyLegs.All(x => !this.HasToken(x)))
					{
						var choice = funkyLegs.PickOne();
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
								var newLegs = "human";
								if (closestBodyplan.HasToken("legs"))
									newLegs = closestBodyplan.GetToken("legs").Text;
								change.AddToken("$", i18n.Format("morph_lose_x_and_gain_y_legs", i18n.GetString("morph_lose" + snakeSlime), newLegs));
								change.AddToken("_add", "legs");
								change.AddToken("legs", newLegs);
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
						//var fail = new Token("_dummy");
						//fail.AddToken("$", i18n.GetString("morph_legsfail"));
						//possibleChanges.Add(fail);
						break;
					}
				#endregion
				#region Tails and Wings
				case Mutations.AddTail:
					if (funkyLegs.Any(x => this.HasToken(x)))
						break;
					if (this.HasToken("tail"))
					{
						var change = new Token("tail", tailTypes.PickOne());
						change.AddToken("$", i18n.GetString("morph_altertail"));
						possibleChanges.Add(change);
					}
					else
					{
						var change = new Token("_add", "tail");
						change.AddToken("tail", tailTypes.PickOne());
						change.AddToken("$", i18n.GetString("morph_growtail"));
						possibleChanges.Add(change);
					}
					break;
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
					else
					{
						if (Random.Flip() && !this.GetToken("penis").HasToken("dual"))
						{
							var change = new Token("_addto/penis", "dual");
							change.AddToken("$", i18n.GetString("morph_splitcock"));
							possibleChanges.Add(change);
						}
						else
						{
							var change = new Token("penis/length", this.Path("penis/length").Value + (float)Random.NextDouble() * intensity + 2f);
							change.AddToken("penis/thickness", this.Path("penis/thickness").Value + (float)Random.NextDouble() * (intensity / 3));
							change.AddToken("$", i18n.GetString("morphpart_penis_1_0"));
							possibleChanges.Add(change);
						}
					}
					break;
				case Mutations.AddVagina:
					if (!this.HasToken("vagina"))
					{
						var change = new Token("_add", "vagina");
						change.AddToken("_addto/vagina", "virgin");
						change.AddToken("_addto/vagina", "looseness");
						change.AddToken("vagina/looseness", (float)Random.NextDouble() * intensity);
						change.AddToken("_addto/vagina", "wetness");
						change.AddToken("vagina/wetness", (float)Random.NextDouble() * intensity);
						change.AddToken("$", i18n.GetString("morph_growpussy"));
						possibleChanges.Add(change);
					}
					else
					{
						if (Random.Flip() && !this.GetToken("vagina").HasToken("dual"))
						{
							var change = new Token("_addto/vagina", "dual");
							change.AddToken("$", i18n.GetString("morph_splitpussy"));
							possibleChanges.Add(change);
						}
						else
						{
							var change = new Token("vagina/looseness", this.Path("vagina/looseness").Value + (float)Random.NextDouble() * intensity);
							change.AddToken("vagina/wetness", this.Path("vagina/wetness").Value + (float)Random.NextDouble() * intensity);
							change.AddToken("$", i18n.GetString("morphpart_pussy_1_1"));
							possibleChanges.Add(change);
						}
					}
					break;
				case Mutations.AddTesticle:
					break; //throw new NotImplementedException();
				#endregion
				#region Genitalia -- changing
				//case Mutations.AlterPenis:
				//case Mutations.GrowTesticles:
				#endregion
				#region Genitalia -- removing
				case Mutations.RemovePenis:
					{
						if (!this.HasToken("penis"))
							break;
						if (this.GetToken("penis").HasToken("dual"))
						{
							var change = new Token("_removefrom/penis", "dual");
							change.AddToken("$", i18n.GetString("morphpart_cock_join"));
							possibleChanges.Add(change);
						}
						else
						{
							var change = new Token("_remove", "penis");
							change.AddToken("$", i18n.GetString("morphpart_lose_dick"));
							possibleChanges.Add(change);
						}
						break;
					}
				case Mutations.RemoveVagina:
					{
						if (!this.HasToken("vagina"))
							break;
						if (this.GetToken("vagina").HasToken("dual"))
						{
							var change = new Token("_removefrom/vagina", "dual");
							change.AddToken("$", i18n.GetString("morphpart_pussy_join"));
							possibleChanges.Add(change);
						}
						else
						{
							var change = new Token("_remove", "vagina");
							change.AddToken("$", i18n.GetString("morphpart_lose_pussy"));
							possibleChanges.Add(change);
						}
						break;
					}
				case Mutations.RemoveTesticle:
					{
						var balls = this.GetToken("balls");
						if (balls != null)
						{
							var amount = balls.GetToken("amount");
							if (amount.Value > 1)
							{
								var change = new Token("balls/amount", amount.Value - 1);
								change.AddToken("$", i18n.Format("morph_loseonenut", amount.Value - 1));
								possibleChanges.Add(change);
							}
							else
							{
								var change = new Token("_remove", "balls");
								change.AddToken("$", i18n.GetString("morph_loselastnut"));
								possibleChanges.Add(change);
							}
						}
					}
					break;
				#endregion
			}

			return possibleChanges;
		}

		public void ApplyMorphDeltas(List<Token> possibilities, int maxChanges, List<string> feedbacks)
		{
			var numChanges = Math.Min(4, possibilities.Count);
			var changes = new List<Token>();
			for (var i = 0; i < numChanges; i++)
			{
				var possibility = possibilities.PickOne();
				possibilities.Remove(possibility);
				changes.Add(possibility);
				foreach (var subChange in possibility.Tokens)
				{
					if (subChange.Name == "$")
					{
						if (!feedbacks.Contains(subChange.Text))
							feedbacks.Add(subChange.Text);
						continue;
					}
					changes.Add(subChange);
				}
			}

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
				if (!change.Text.IsBlank())
					token.Text = change.Text;
				else
					token.Value = change.Value;
			}
		}

		public string GetMorphFeedback(List<string> feedbacks)
		{
			if (feedbacks.Count == 0)
				return i18n.GetString("morphfinal_nothing");

			var isStart = new bool[feedbacks.Count];
			isStart[0] = true;
			for (var i = 1; i < feedbacks.Count; i++)
				if (feedbacks[i][0] != '[')
					isStart[i] = true;

			var fragmentEnd = new int[feedbacks.Count];
			fragmentEnd[feedbacks.Count-1] = 4; //"."
			var fragmentLength = 0;
			for (var i = 0; i < feedbacks.Count - 1; i++)
			{
				if (isStart[i + 1])
				{
					fragmentEnd[i] = 3; //". "
					if (fragmentLength > 0)
					{
						if (fragmentLength == 1)
							fragmentEnd[i - 1] = 1; //" and "
						else
							fragmentEnd[i - 1] = 2; //", and "
					}
					fragmentLength = 0;
				}
				else
				{
					fragmentLength++;
					if (fragmentLength == 4)
					{
						//Force an end.
						fragmentLength = 0;
						fragmentEnd[i] = 3; //". "
						if (i < feedbacks.Count - 1)
							isStart[i + 1] = true;
						if (i > 0)
						{
							if (fragmentLength == 1)
								fragmentEnd[i - 1] = 1; //" and "
							else
								fragmentEnd[i - 1] = 2; //", and "
						}
					}
				}
			}
			//fix final part
			if (fragmentLength > 0)
			{
				if (fragmentLength == 1)
					fragmentEnd[feedbacks.Count - 2] = 1; //" and "
				else
					fragmentEnd[feedbacks.Count - 2] = 2; //", and "
			}

			var feedbackBuilder = new StringBuilder();

			for (var i = 0; i < feedbacks.Count; i++)
			{
				if (isStart[i])
					feedbackBuilder.Append(feedbacks[i].Replace("[views]", "[Yourornames]").Replace("[view]", "[Youorname]"));
				else
					feedbackBuilder.Append(feedbacks[i].Replace("[views]", "[his]").Replace("[view]", "[he]"));
				feedbackBuilder.Append(i18n.GetString("morphfinal_" + fragmentEnd[i]));
			}
			
			//Perhaps have a case for extreme amounts where it splits up into various sentences and ends with a "finally"?
			return feedbackBuilder.ToString().Viewpoint(this);
		}

		public string Morph(string targetPlan, Gender targetGender = Gender.Invisible)
		{
			if (GetToken("perks").HasToken("formlock"))
				return i18n.GetString("formlock").Viewpoint(this);
			var possibilities = GetTargetedMorphDeltas(targetPlan, targetGender);
			var feedbacks = new List<string>();
			ApplyMorphDeltas(possibilities, 4, feedbacks);

			var feedback = GetMorphFeedback(feedbacks);

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
			UpdatePowers();

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
			var feedbacks = new List<string>();
			for (var i = 0; i < number; i++)
			{
				var possibilities = GetWildMorphDeltas(intensity, mutation);
				var feedback = string.Empty;
				ApplyMorphDeltas(possibilities, 4, feedbacks);
			}

			var fb = GetMorphFeedback(feedbacks);

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
			UpdatePowers();

			SpeechFilter = null; //invalidate here -- we don't necessarily have this character speak right away and need to adjust for new impediments.

			return fb.Trim();
		}

		/// <summary>
		/// Creates an encoded textual description of a character's body to use in comparisons.
		/// </summary>
		public static string GetBodyComparisonHash(TokenCarrier token)
		{
			var ret = new StringBuilder();
			ret.Append('[');
			var parts = new[] { "skin", "ears", "face", "teeth", "tongue", "wings", "legs", "penis", "tail" };
			foreach (var part in parts)
			{
				var pt = token.Path(part + "/type");
				if (pt == null)
					pt = token.GetToken(part);
				if (pt == null)
				{
					if (part == "tail")
					{
						if (token.HasToken("snaketail"))
							ret.Append('§');
						else if (token.HasToken("slimeblob"))
							ret.Append('ß');
						else
							ret.Append(' ');
					}
					else
						ret.Append(' ');
					continue;
				}
				var letter = Descriptions.descTable.Path(part + '/' + pt.Text + "/_hash");
				if (letter == null)
					ret.Append(' ');
				else
				{
					if (part == "wings" && !pt.HasToken("small"))
						ret.Append(letter.Text.ToUpperInvariant());
					else
						ret.Append(letter.Text);
				}
			}
			if (token.Path("hair") != null)
				ret.Append('h');
			else
				ret.Append(' ');
			if (token.Path("antennae") == null)
				ret.Append(' ');
			else
				ret.Append('!');
			var tallness = token.Path("tallness");
			if (tallness == null)
				ret.Append(' ');
			else
			{
				var t = tallness.Value;
				if (t == 0 && tallness.Text.StartsWith("roll"))
					t = float.Parse(tallness.Text.Substring(tallness.Text.IndexOf('+') + 1));
				if (t < 140)
					ret.Append('_');
				else if (t > 180)
					ret.Append('!');
				else
					ret.Append(' ');
			}
			ret.Append(']');
			return ret.ToString();
		}

		public string GetBodyComparisonHash()
		{
			return GetBodyComparisonHash(this);
		}

		public string GetClosestBodyplanMatch()
		{
			var thisHash = this.GetBodyComparisonHash();
			var ret = string.Empty;
			var score = 999;
			foreach (var hash in NoxicoGame.BodyplanHashes)
			{
				var distance = thisHash.GetHammingDistance(hash.Value);
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
	}
}
