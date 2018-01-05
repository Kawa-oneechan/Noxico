using System;
using System.Collections.Generic;
using System.Linq;

namespace Noxico
{
	public static class Descriptions
	{
		public static TokenCarrier descTable;

		static Descriptions()
		{
			descTable = new TokenCarrier();
			descTable.Tokens.AddRange(Mix.GetTokenTree("bodyparts.tml"));
		}

		public static string Length(float cm)
		{
			if (!IniFile.GetValue("misc", "imperial", false))
			{
				if (cm >= 100)
				{
					var m = Math.Floor(cm / 100);
					cm %= 100;
					if (cm > 0)
						return m + "." + Math.Floor(cm) + "m";
					else
						return m + "m";
				}
				if (Math.Floor(cm) != cm)
					return cm.ToString("F1") + "cm";
				else
					return cm.ToString("F0") + "cm";
			}
			else
			{
				var i = cm * 0.3937;
				if (i > 12)
				{
					var f = Math.Floor(i / 12);
					i %= 12;
					if (i > 0)
						return f + "\x15E" + i.ToString("F0") + "\x15F";
					else
						return f + "\x15E";
				}
				if (Math.Floor(i) != i)
					return i.ToString("F1") + "\x15F";
				else
					return i.ToString("F0") + "\x15F";
			}
		}

		public static string GetSizeDescription(string path, float upTo)
		{
			var set = descTable.Path(path);
			if (set == null)
				throw new Exception("Could not find bodyparts.tml item \"" + path + "\".");
			var ret = set.Tokens[0].Tokens[Random.Next(set.Tokens[0].Tokens.Count)].Name;
			foreach (var item in set.Tokens)
			{
				if (item.Value <= upTo)
					ret = item.Tokens[Random.Next(item.Tokens.Count)].Name;
				else
					return ret;
			}
			return ret;
		}

		public static string GetPartDescription(string path)
		{
			var set = descTable.Path(path);
			if (set == null)
				throw new Exception("Could not find bodyparts.tml item \"" + path + "\".");
			return set.Tokens[Random.Next(set.Tokens.Count)].Name;
		}
		public static string GetPartDescription(string path, params string[] alternatives)
		{
			var set = descTable.Path(path);
			if (set == null)
				return Toolkit.PickOne(alternatives);
			return set.Tokens[Random.Next(set.Tokens.Count)].Name;
		}

		public static string BreastSize(Token breastToken, bool inCups = false)
		{
			if (breastToken == null)
				return "glitch";
			var size = breastToken.HasToken("size") ? breastToken.GetToken("size").Value : 0f;
			return GetSizeDescription(inCups ? "breasts/cupsize" : "breasts/size", size);
		}

		public static string Looseness(Token loosenessToken, bool forButts = false)
		{
			if (loosenessToken == null)
				return null;
			return GetSizeDescription(forButts ? "ass/size" : "vagina/looseness", loosenessToken.Value);
		}

		public static string Wetness(Token wetnessToken)
		{
			if (wetnessToken == null)
				return null;
			return GetSizeDescription("vagina/wetness", wetnessToken.Value);
		}

		public static string Tail(Token tailToken)
		{
			if (tailToken == null)
				return "glitch";
			var tails = new Dictionary<string, string>()
			{
				{ "stinger", "stinger" }, //needed to prevent "stinger tail"
				{ "webber", "webber" },
				{ "genbeast", Random.NextDouble() < 0.5 ? "ordinary tail" : "tail" }, //"Your (ordinary) tail"
			};
			var tailName = tailToken.Text;
			if (tails.ContainsKey(tailName))
				return tails[tailName];
			else
				return tailName + " tail";
		}

		#region Random descriptions
		/// <summary>
		/// Chooses a random euphemism for penis and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for penis.</returns>
		public static string CockRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("cockrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for vagina and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for vagina.</returns>
		public static string PussyRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("pussyrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for anus and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for anus.</returns>
		public static string AnusRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("anusrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for ass and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for ass.</returns>
		public static string ButtRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("buttrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for clitoris and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for clitoris.</returns>
		public static string ClitRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("clitrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for breast and returns it as a string.
		/// </summary>
		/// <param name="plural">Flag for returning a plural euphmism insead of a singular.</param>
		/// <returns>A string containing a euphemism for breast(s).</returns>
		public static string BreastRandom(bool plural = false)
		{
			throw new Exception("This method is deprecated.");
			//if (plural)
			//	return Toolkit.PickOne(i18n.GetArray("breastsrandom"));
			//else
			//	return Toolkit.PickOne(i18n.GetArray("breastrandom"));
		}

		/// <summary>
		/// Chooses a random euphemism for semen and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for semen.</returns>
		public static string CumRandom()
		{
			throw new Exception("This method is deprecated.");
			//return Toolkit.PickOne(i18n.GetArray("cumrandom"));
		}
		#endregion

		/// <summary>
		/// Returns a string describing a piece of equipment using its name, (optionally) color (if available), and (optionally) an appropriate article.
		/// </summary>
		/// <param name="knownItem">An InventoryItem from <see cref="NoxicoGame.KnownItems"/>.</param>
		/// <param name="token">An item token from a <see cref="Character"/>'s inventory that matches the type of item in the first parameter.</param>
		/// <param name="article">Adds either the definite article (if "the" is passed), the indefinite article (if "a" is passed), or no article (anything else is passed)
		/// to the front of the descriptive string.</param>
		/// <param name="withColor">If set to true, the returned string will also describe the color of the item if it has one.</param>
		/// <returns>A string containing the description of the item as defined by the parameters. If 'item' is null, then null is returned instead.</returns>
		public static string Item(InventoryItem knownItem, Token token, string article = "", bool withColor = false)
		{
			if (knownItem == null)
				return null;
			var name = (token != null && token.HasToken("unidentified") && !string.IsNullOrWhiteSpace(knownItem.UnknownName)) ? knownItem.UnknownName : knownItem.Name;
			var color = (token != null && token.HasToken("color")) ? Color.NameColor(token.GetToken("color").Text) : "";
			var reps = new Dictionary<string, string>()
			{
				{ "[color]", color },
				{ "[, color]", ", " + color },
				{ "[color ]", color + " " },
				{ "[color, ]", color + ", " },
			};
			if (withColor && !string.IsNullOrEmpty(color))
			{
				foreach (var i in reps)
					name = name.Replace(i.Key, i.Value);
			}
			else
			{
				foreach (var key in reps.Keys)
					name = name.Replace(key, "");
			}

			//TODO: see Mutamorph/GetMorphDeltas about scripted articles.
			if (article == "the")
				name = knownItem.Definite + " " + name;
			else if (article == "a")
				name = knownItem.Indefinite + " " + name;

			return name;
		}

		#region Head descriptions

		/// <summary>
		/// Returns a string describing the ear token passed to the function.
		/// </summary>
		/// <param name="earToken">The 'ears' token of a character.</param>
		/// <returns>A string containing the description of the ear type.</returns>
		public static string EarType(Token earToken)
		{
			if (earToken == null)
				return "glitch";
			return GetPartDescription("ears/" + earToken.Text, "indescribable", "unusual");
		}

		/// <summary>
		/// Returns a string describing the face token passed to the function.
		/// </summary>
		/// <param name="faceToken">The 'face' token of a character.</param>
		/// <returns>A string containing the description of the face type.</returns>
		public static string FaceType(Token faceToken)
		{
			if (faceToken == null)
				return "glitch";
			return GetPartDescription("face/" + faceToken.Text, "indescribable", "strange");
		}

		/// <summary>
		/// Returns a string describing the color of the passed hair token.
		/// </summary>
		/// <param name="hairToken">A character's 'hair' token.</param>
		/// <returns>A string that describes the hair's color.</returns>
		public static string HairColor(Token hairToken)
		{
			return Color.NameColor(hairToken.GetToken("color").Text);
		}

		/// <summary>
		/// Returns a string describing the length of a the passed hair token in plain english.
		/// </summary>
		/// <param name="hairToken">A character's hair token.</param>
		/// <returns>An all lower case string describing the hair's length.</returns>
		public static string HairLength(Token hairToken)
		{
			if (hairToken == null)
				return "glitch";
			return GetSizeDescription("hair/length", hairToken.HasToken("length") ? hairToken.GetToken("length").Value : 0f);
		}

		public static string Hair(Token hairToken)
		{
			//TODO: finish hair

			return null;
		}

		/// <summary>
		/// Returns a string describing the teeth token passed to the function.
		/// </summary>
		/// <param name="teethToken">The 'teeth' token to be evaluated.</param>
		/// <returns>A string containing a description of the teeth type. Return's 'glitch' if 'teeth' is null.</returns>
		public static string TeethType(Token teethToken)
		{
			if (teethToken == null)
				return "glitch";
			return GetPartDescription("teeth/" + teethToken.Text, "indescribable", "unusual");
		}

		/// <summary>
		/// Returns a string containing a description of the parameter 'tongue's type.
		/// </summary>
		/// <param name="tongueToken">A tongue token from a character.</param>
		/// <returns>A string containing the description of the tongue based on its type.</returns>
		public static string TongueType(Token tongueToken)
		{
			if (tongueToken == null)
				return "glitch";
			return GetPartDescription("tongue/" + tongueToken.Text, "indescribable", "unusual");
		}

		#endregion

		#region Upperbody descriptions

		/// <summary>
		/// Returns a string containing a description of the passed 'nipple' token's size.
		/// </summary>
		/// <param name="nipplesToken">The 'nipple' token of a character.</param>
		/// <returns>A string containging the description of the 'nipple' token's size.</returns>
		public static string NippleSize(Token nipplesToken)
		{
			if (nipplesToken == null)
				return "glitch";
			return GetSizeDescription("breasts/nipples", nipplesToken.HasToken("size") ? nipplesToken.GetToken("size").Value : 0.25f);
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'waist' token's size.
		/// </summary>
		/// <param name="waistToken">The 'waist' token of a character.</param>
		/// <returns>A string containging the description of the 'waist' token's size.</returns>
		public static string WaistSize(Token waistToken)
		{
			return GetSizeDescription("waist", waistToken != null ? waistToken.Value : 5);
		}

		/// <summary>
		/// Takes a wing type token from a character and returns a string describing that wing's type.
		/// </summary>
		/// <param name="wingToken">The 'wings' token to be evaluated.</param>
		/// <returns>A string containing a description of the wings' type. Return's 'glitch' if 'tail is null.</returns>
		public static string WingType(Token wingToken)
		{
			if (wingToken == null)
				return "glitch";
			return GetPartDescription("wings/" + wingToken.Text, "indescribable", "unusual");
		}

		#endregion

		#region Lowerbody descriptions

		/// <summary>
		/// Returns a string describing the token 'cock's type.
		/// </summary>
		/// <param name="cockToken">The penis token to be evaluated.</param>
		/// <returns>A string containing only the 'cock's type.</returns>
		public static string CockType(Token cockToken)
		{
			if (cockToken == null)
				return "glitch";
			return GetPartDescription("penis/" + cockToken.Text, "indescribable", "unusual", "oddly shaped");
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'balls' token size.
		/// </summary>
		/// <param name="ballsToken">The 'balls' token of a character.</param>
		/// <returns>A string containging the description of the 'balls' token's size.</returns>
		public static string BallSize(Token ballsToken)
		{
			if (ballsToken == null)
				return "glitch";
			return GetSizeDescription("balls", ballsToken.HasToken("size") ? ballsToken.GetToken("size").Value : 1f);
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'ass' token size.
		/// </summary>
		/// <param name="buttToken">The 'ass' token of a character.</param>
		/// <returns>A string containging the description of the 'ass' token's size.</returns>
		public static string ButtSize(Token buttToken)
		{
			if (buttToken == null)
				return "glitch";
			return GetSizeDescription("ass/size", buttToken.HasToken("size") ? buttToken.GetToken("size").Value : 5);
		}

		/// <summary>
		/// Provides a description of a character's foot based on their leg type.
		/// </summary>
		/// <param name="legsToken">The leg token to be evaluated.</param>
		/// <param name="plural">If set to true, the returned description will be for both feet.</param>
		/// <returns>A string containing a description of the foot type.</returns>
		public static string Foot(Token legsToken, bool plural = false)
		{
			var request = descTable.Path("feet/" + (legsToken == null ? "default" : legsToken.Text)) ?? descTable.Path("feet/default");
			return request.Text.Pluralize(plural ? 2 : 1);
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'hips' token size.
		/// </summary>
		/// <param name="hipToken">The 'hips' token of a character.</param>
		/// <returns>A string containging a description based on the hipToken's value.</returns>
		public static string HipSize(Token hipToken)
		{
			if (hipToken == null)
				return "glitch";
			return GetSizeDescription("hips", hipToken.Value);
		}

		/// <summary>
		/// Takes a tail type token from a character and returns a string describing that tail's type.
		/// </summary>
		/// <param name="tailToken">The tail token to be evaluated.</param>
		/// <returns>A string containing a description of the tail's type. Return's 'glitch' if 'tail is null.</returns>
		public static string TailType(Token tailToken)
		{
			if (tailToken == null)
				return "glitch";
			return GetPartDescription("tail", tailToken.Text, "indescribable", "unusual");
		}

		#endregion

		/// <summary>
		/// Provides a description of a character's hand based on their body type.
		/// </summary>
		/// <param name="character">The character to be evaluated.</param>
		/// <param name="plural">If set to true, the returned description will be for both hands.</param>
		/// <returns>A string containing a description of the hand type.</returns>
		public static string Hand(Character character, bool plural = false)
		{
			if (character.HasToken("quadruped"))
				return Foot(character.GetToken("legs"), plural);
			//Clawed hands and such can go here.
			var request = descTable.Path("hand/default");
			return request.Text.Pluralize(plural ? 2 : 1);
		}
	}
}