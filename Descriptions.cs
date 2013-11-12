using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Noxico
{
	public static class Descriptions
	{
		public static XmlDocument descTable;

		static Descriptions()
		{
			descTable = Mix.GetXmlDocument("bodyparts.xml");
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

		public static string BreastSize(Token breastRowToken, bool inCups = false)
		{
			if (breastRowToken == null)
				return "glitch";

			var size = breastRowToken.HasToken("size") ? breastRowToken.GetToken("size").Value : 0f;

			var descriptions = inCups ? GetSizeDescriptions(size, "//upperbody/breasts/cupsizes") : GetSizeDescriptions(size, "//upperbody/breasts/sizes");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		public static string Looseness(Token loosenessToken, bool forButts = false)
		{
			if (loosenessToken == null)
				return null;

			var size = loosenessToken.Value;

			var descriptions = GetSizeDescriptions(size, forButts ?  "//lowerbody/ass/loosenesses" : "//lowerbody/sexorgans/vaginas/loosenesses");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		public static string Wetness(Token wetnessToken)
		{
			if (wetnessToken == null)
				return null;

			var wetness = wetnessToken.Value;

			var descriptions = GetSizeDescriptions(wetness, "//lowerbody/sexorgans/vaginas/wetnesses");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		public static string Tail(Token tailToken)
		{
			if (tailToken == null)
				return "glitch";
			var tails = new Dictionary<string, string>()
			{
				{ "stinger", "stinger" }, //needed to prevent "stinger tail"
				{ "genbeast", Random.NextDouble() < 0.5 ? "ordinary tail" : "tail" }, //"Your (ordinary) tail"
			};
			var tailName = tailToken.Text;
			if (tails.ContainsKey(tailName))
				return tails[tailName];
			else
				return tailName + " tail";
		}

		#region PillowShout's additions

		/// <summary>
		/// Returns a set of descriptions based on the name of the desired bodypart type and the path to that set of part descriptions.
		/// </summary>
		/// <param name="name">The name of the descriptive element to return.</param>
		/// <param name="Xpath">The XML path to the desired set of description elements.</param>
		/// <returns>Returns the set of comma delimited descriptions as a string.</returns>
		public static string GetPartDescriptions(string name, string Xpath)
		{
			var text = "";

			foreach (var descEntry in descTable.DocumentElement.SelectNodes(Xpath + "/partdesc").OfType<XmlElement>())
			{
				if (descEntry.GetAttribute("name").Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					text = descEntry.InnerText;
					break;
				}
			}

			return text;
		}

		/// <summary>
		/// Returns a set of descriptions based on the size of the desired bodypart type and the path to that set of part descriptions.
		/// </summary>
		/// <param name="name">The size of the descriptive element to return.</param>
		/// <param name="Xpath">The XML path to the desired set of description elements.</param>
		/// <returns>Returns the set of comma delimited descriptions as a string.</returns>
		public static string GetSizeDescriptions(float size, string Xpath)
		{
			var text = "";

			foreach (var descEntry in descTable.DocumentElement.SelectNodes(Xpath + "/size").OfType<XmlElement>())
			{
				if (size <= float.Parse(descEntry.GetAttribute("upto")))
				{
					text = descEntry.InnerText;
					break;
				}
			}

			return text;
		}

		#region Random descriptions
		/// <summary>
		/// Chooses a random euphemism for penis and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for penis.</returns>
		public static string CockRandom()
		{
			return Toolkit.PickOne("penis", "cock", "dick", "pecker", "prick", "rod", "shaft", "dong");
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
		/// Chooses a random euphemism for ass and returns it as a string.
		/// </summary>
		/// <returns>A string containing a euphemism for ass.</returns>
		public static string ButtRandom()
		{
			return Toolkit.PickOne("butt", "ass", "rear", "backside", "behind", "bum");
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

			var type = earToken.Text;

			var descriptions = GetPartDescriptions(type, "//head/ears");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
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

			var type = faceToken.Text;

			var descriptions = GetPartDescriptions(type, "//head/faces");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "strange");
		}

		/// <summary>
		/// Returns a string describing the color of the passed hair token.
		/// </summary>
		/// <param name="hairToken">A character's 'hair' token.</param>
		/// <returns>A string that describes the hair's color. Result is lower case.</returns>
		public static string HairColor(Token hairToken)
		{
			var hairColorToken = hairToken.GetToken("color");
			return Color.NameColor(hairColorToken.Text).ToLowerInvariant();
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

			var hairLength = hairToken.HasToken("length") ? hairToken.GetToken("length").Value : 0f;

			var descriptions = GetSizeDescriptions(hairLength, "//head/hairs/lengths");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		public static string Hair(Token hairToken)
		{
			//TODO finish hair

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

			var type = teethToken.Text;

			var descriptions = GetPartDescriptions(type, "//head/teeth");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
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

			var type = tongueToken.Text;

			var descriptions = GetPartDescriptions(type, "//head/tongues");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
		}

		#endregion

		#region Upperbody descriptions

		/// <summary>
		/// Returns a string containing a description of the passed 'nipple' token's size. Returned values are determined from "bodyparts.xml".
		/// </summary>
		/// <param name="ballsToken">The 'nipple' token of a character.</param>
		/// <returns>A string containging the description of the 'nipple' token's size.</returns>
		public static string NippleSize(Token nipplesToken)
		{
			if (nipplesToken == null)
				return "glitch";

			var size = nipplesToken.HasToken("size") ? nipplesToken.GetToken("size").Value : 0.25f;

			var descriptions = GetSizeDescriptions(size, "//upperbody/breasts/nipplesizes");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'waist' token's size. Returned values are determined from "bodyparts.xml".
		/// </summary>
		/// <param name="ballsToken">The 'waist' token of a character.</param>
		/// <returns>A string containging the description of the 'waist' token's size.</returns>
		public static string WaistSize(Token waistToken)
		{
			var size = waistToken != null ? waistToken.Value : 5;

			var descriptions = GetSizeDescriptions(size, "//upperbody/waist");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		/// <summary>
		/// Takes a wing type token from a character and returns a string describing that wing's type.
		/// </summary>
		/// <param name="tailToken">The 'wings' token to be evaluated.</param>
		/// <returns>A string containing a description of the wings' type. Return's 'glitch' if 'tail is null.</returns>
		public static string WingType(Token wingToken)
		{
			if (wingToken == null)
				return "glitch";

			var type = wingToken.Text;

			var descriptions = GetPartDescriptions(type, "//upperbody/wings");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
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

			var type = cockToken.Text;

			var descriptions = GetPartDescriptions(type, "//lowerbody/sexorgans/penises/types");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("unusual", "indescribable", "oddly shaped");
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'balls' token size. Returned values are determined from "bodyparts.xml".
		/// </summary>
		/// <param name="ballsToken">The 'balls' token of a character.</param>
		/// <returns>A string containging the description of the 'balls' token's size.</returns>
		public static string BallSize(Token ballsToken)
		{
			if (ballsToken == null)
				return "glitch";

			var size = ballsToken.HasToken("size") ? ballsToken.GetToken("size").Value : 1f;

			var descriptions = GetSizeDescriptions(size, "//lowerbody/sexorgans/balls/sizes");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'ass' token size. Returned values are determined from "bodyparts.xml".
		/// </summary>
		/// <param name="ballsToken">The 'ass' token of a character.</param>
		/// <returns>A string containging the description of the 'ass' token's size.</returns>
		public static string ButtSize(Token buttToken)
		{
			if (buttToken == null)
				return "glitch";

			var size = buttToken.HasToken("size") ? buttToken.GetToken("size").Value : 5;

			var descriptions = GetSizeDescriptions(size, "//lowerbody/ass/sizes");

			return Toolkit.PickOne(descriptions.Split(',')).Trim();
		}

		/// <summary>
		/// Provides a description of a character's foot based on their leg type.
		/// </summary>
		/// <param name="legsToken">The leg token to be evaluated.</param>
		/// <param name="plural">If set to true, the returned description will be for both feet.</param>
		/// <returns>A string containing a description of the foot type.</returns>
		public static string Foot(Token legsToken, bool plural = false)
		{
			if (legsToken == null)
				return "glitch";
			if (legsToken.Text == "horse")
				return plural ? "hooves" : "hoof";
			else if (legsToken.Text == "dog" || legsToken.Text == "bear")
				return plural ? "paws" : "paw";
			else if (legsToken.Text == "insect")
				return plural ? "claws" : "claw";

			return plural ? "feet" : "foot";
		}

		/// <summary>
		/// Returns a string containing a description of the passed 'hips' token size. Returned values are determined from "bodyparts.xml".
		/// </summary>
		/// <param name="hipToken">The 'hips' token of a character.</param>
		/// <returns>A string containging a description based on the hipToken's value.</returns>
		public static string HipSize(Token hipToken)
		{
			if (hipToken == null)
				return "glitch";

			var hipSize = hipToken.Value;

			var descriptions = GetSizeDescriptions(hipSize, "//lowerbody/hips");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
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

			var type = tailToken.Text;

			var descriptions = GetPartDescriptions(type, "//lowerbody/tails");

			if (!string.IsNullOrWhiteSpace(descriptions))
				return Toolkit.PickOne(descriptions.Split(',')).Trim();
			else
				return Toolkit.PickOne("indescribable", "unusual");
		}

		#endregion

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
			return plural ? "hands" : "hand";
		}

	}
}