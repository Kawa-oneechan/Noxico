using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.IO;
using System.Globalization;
using System.Drawing;

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
	
	public static class Toolkit
	{
		public static TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
		private static XmlDocument colorTable;

		public static Random Rand { get; private set; }

		static Toolkit()
		{
			Rand = new Random();
		}

		public static string PickOne(params string[] options)
		{
			return options[Toolkit.Rand.Next(options.Length)];
		}

		public static string Count(float num)
		{
			var words = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
			var i = (int)Math.Floor(num);
			if (i < words.Length)
				return words[i];
			return i.ToString();
		}

		public static string NameColor(string color)
		{
			var req = color.Trim().ToLower().Replace("_", "").Replace(" ", "");
			var colorName = "";
			if (colorTable == null)
			{
				colorTable = new XmlDocument();
				colorTable.LoadXml(global::Noxico.Properties.Resources.KnownColors);
			}
			foreach (var colorEntry in colorTable.DocumentElement.SelectNodes("//color").OfType<XmlElement>())
			{
				if (colorEntry.GetAttribute("name").Equals(req, StringComparison.InvariantCultureIgnoreCase))
				{
					colorName = colorEntry.GetAttribute("name");
					break;
				}
			}
			if (colorName == "")
				return color;
			var ret = new StringBuilder();
			foreach (var c in colorName)
			{
				if (char.IsUpper(c))
					ret.Append(' ');
				ret.Append(c);
			}
			return ret.ToString().Trim().ToLowerInvariant();
		}

		public static Color GetColor(string color)
		{
			if (string.IsNullOrEmpty(color))
				return Color.Silver;
			if (colorTable == null)
			{
				colorTable = new XmlDocument();
				colorTable.LoadXml(global::Noxico.Properties.Resources.KnownColors);
			}
			var req = color.ToLower().Replace("_", "").Replace(" ", "");
			//var entry = colorTable.DocumentElement.SelectSingleNode("//color[@name=\"" + req + "\"]") as XmlElement;
			XmlElement entry = null;
			var entries = colorTable.DocumentElement.SelectNodes("//color").OfType<XmlElement>();
			foreach (var e in entries)
			{
				if (e.GetAttribute("name").Equals(req, StringComparison.InvariantCultureIgnoreCase))
				{
					entry = e;
					break;
				}
			}
			if (entry == null)
				return Color.Silver;
			if (String.IsNullOrEmpty(entry.GetAttribute("rgb")))
				return Color.Silver;
			var rgb = entry.GetAttribute("rgb").Split(',');
			return Color.FromArgb(int.Parse(rgb[0]), int.Parse(rgb[1]), int.Parse(rgb[2]));
		}
		public static Color GetColor(Token color)
		{
			if (color == null)
				return Color.Silver;
			return GetColor(color.Name);
		}

		public static Color Darken(this Color color, double divisor = 2)
		{
			if (divisor == 0)
				divisor = 1;
			var rD = color.R / divisor;
			var gD = color.G / divisor;
			var bD = color.B / divisor;
			var r = color.R - rD;
			var g = color.G - gD;
			var b = color.B - bD;
			if (r < 0)
				r = 0;
			if (g < 0)
				g = 0;
			if (b < 0)
				b = 0;
			return Color.FromArgb((int)r, (int)g, (int)b);
		}

		public static string Viewpoint(this string text, BoardChar point)
		{
			if (point != null && point is Player)
			{
				text = text.Replace("[Your]", "Your");
				text = text.Replace("[your]", "your");
				text = text.Replace("[You]", "You");
				text = text.Replace("[you]", "you");
				text = text.Replace("[have]", "have");
				text = text.Replace("[s]", "");
				text = text.Replace("[ies]", "y");
			}
			else
			{
				//For third-person descriptions...
				text = text.Replace("[Your]", point.Character.HisHerIts(false));
				text = text.Replace("[your]", point.Character.HisHerIts(true));
				text = text.Replace("[You]", point.Character.HeSheIt(false));
				text = text.Replace("[you]", point.Character.HeSheIt(true));
				text = text.Replace("[have]", "has");
				text = text.Replace("[s]", "s");
				text = text.Replace("[ies]", "ies");
			}
			/*
				sb.Replace("[This is]", pa is Player ? "You are" : "This is");
				sb.Replace("[His]", pa is Player ? "Your" : chr.HisHerIts());
				sb.Replace("[He]", pa is Player ? "You" : chr.HeSheIt());
				sb.Replace("[his]", pa is Player ? "your" : chr.HisHerIts(true));
				sb.Replace("[he]", pa is Player ? "you" : chr.HeSheIt(true));
				sb.Replace("[him]", pa is Player ? "you" : chr.HimHerIt());
				sb.Replace("[is]", pa is Player ? "are" : "is");
				sb.Replace("[has]", pa is Player ? "have" : "has");
			*/
			return text;
		}

		public static bool StartsWithVowel(this string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return false;
			var vowels = "AEIOUaeiou".ToCharArray();
			if (vowels.Contains(text[0]))
				return true;
			return false;
		}

		public static string Titlecase(this string text)
		{
			return ti.ToTitleCase(text);
		}

		public static string Wordwrap(this string text, int length = 80)
		{
			var lines = text.Replace("\r", "").Split('\n');
			var sb = new System.Text.StringBuilder();
			foreach (var line in lines)
			{
				var words = line.Split(new[] { ' ' });
				var lineWidth = 0;
				for (var i = 0; i < words.Length; i++)
				{
					sb.Append(words[i]);
					var len = words[i].Length;
					if (words[i].Contains('<'))
					{
						var newWord = Regex.Replace(words[i], @"\<(.*?)\>", "");
						len = newWord.Length;
					}

					lineWidth += len + 1;
					if (i < words.Length - 1 && lineWidth + words[i + 1].Length > length - 2)
					{
						sb.AppendLine();
						lineWidth = 0;
					}
					else
						sb.Append(" ");
				}
				sb.Append("\n");
			}
			return sb.ToString();
		}

		public static void DrawWindow(int left, int top, int width, int height, string title, Color fgColor, Color bgColor, Color titleColor, bool single = false)
		{
			var host = NoxicoGame.HostForm;
#if USE_EXTENDED_TILES
			host.SetCell(top, left, (char)(single ? 0xDA : 0x120), fgColor, bgColor);
			host.SetCell(top, left + width, (char)(single ? 0xBF : 0x121), fgColor, bgColor);
			host.SetCell(top + height, left, (char)(single ? 0xC0 : 0x122), fgColor, bgColor);
			host.SetCell(top + height, left + width, (char)(single ? 0xD9 : 0x123), fgColor, bgColor);
			for (int i = left + 1; i < left + width; i++)
			{
				host.SetCell(top, i, (char)(single ? 0xC4 : 0x124), fgColor, bgColor);
				host.SetCell(top + height, i, (char)(single ? 0xC4 : 0x124), fgColor, bgColor);
				for (int j = top + 1; j < top + height; j++)
					host.SetCell(j, i, ' ', fgColor, bgColor);
			}
			for (int i = top + 1; i < top + height; i++)
			{
				host.SetCell(i, left, (char)(single ? 0xB3 : 0x125), fgColor, bgColor);
				host.SetCell(i, left + width, (char)(single ? 0xB3 : 0x125), fgColor, bgColor);
			}
			if (!string.IsNullOrWhiteSpace(title))
			{
				var captionWidth = title.Length;
				var captionPos = left + (int)Math.Ceiling(width / 2.0) - (int)Math.Ceiling(captionWidth / 2.0) - 1;
				host.Write("<g" + (single ? "B4" : "B5,126") + "><c" + titleColor.Name + "> " + title + " <c" + fgColor.Name + "><g" + (single ? "C3" : "C6,127") + ">", fgColor, bgColor, captionPos - 1, top);
				//host.SetCell(top, captionPos - 2, (char)(single ? 0xB4 : 0xB5), fgColor, bgColor);
				//host.SetCell(top, captionPos + captionWidth, (char)(single ? 0xC3 : 0xC6), fgColor, bgColor);
			}
#else
			host.SetCell(top, left, (char)(single ? 0xDA : 0xC9), fgColor, bgColor);
			host.SetCell(top, left + width, (char)(single ? 0xBF : 0xBB), fgColor, bgColor);
			host.SetCell(top + height, left, (char)(single ? 0xC0 : 0xC8), fgColor, bgColor);
			host.SetCell(top + height, left + width, (char)(single ? 0xD9 : 0xBC), fgColor, bgColor);
			for (int i = left + 1; i < left + width; i++)
			{
				host.SetCell(top, i, (char)(single ? 0xC4 : 0xCD), fgColor, bgColor);
				host.SetCell(top + height, i, (char)(single ? 0xC4 : 0xCD), fgColor, bgColor);
				for (int j = top + 1; j < top + height; j++)
					host.SetCell(j, i, ' ', fgColor, bgColor);
			}
			for (int i = top + 1; i < top + height; i++)
			{
				host.SetCell(i, left, (char)(single ? 0xB3 : 0xBA), fgColor, bgColor);
				host.SetCell(i, left + width, (char)(single ? 0xB3 : 0xBA), fgColor, bgColor);
			}
			if (!string.IsNullOrWhiteSpace(title))
			{
				var captionWidth = title.Length;
				var captionPos = left + (int)Math.Ceiling(width / 2.0) - (int)Math.Ceiling(captionWidth / 2.0) - 1;
				host.Write("<g" + (single ? "B4" : "B5") + "><c" + titleColor.Name + "> " + title + " <c" + fgColor.Name + "><g" + (single ? "C3" : "C6") + ">", fgColor, bgColor, captionPos - 1, top);
				//host.SetCell(top, captionPos - 2, (char)(single ? 0xB4 : 0xB5), fgColor, bgColor);
				//host.SetCell(top, captionPos + captionWidth, (char)(single ? 0xC3 : 0xC6), fgColor, bgColor);
			}
#endif
		}

		public static IEnumerable<Point> Line(int x0, int y0, int x1, int y1)
		{
			bool steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);
			if (steep)
			{
				int t;
				t = x0; // swap x0 and y0
				x0 = y0;
				y0 = t;
				t = x1; // swap x1 and y1
				x1 = y1;
				y1 = t;
			}
			if (x0 > x1)
			{
				int t;
				t = x0; // swap x0 and x1
				x0 = x1;
				x1 = t;
				t = y0; // swap y0 and y1
				y0 = y1;
				y1 = t;
			}
			int dx = x1 - x0;
			int dy = Math.Abs(y1 - y0);
			int error = dx / 2;
			int ystep = (y0 < y1) ? 1 : -1;
			int y = y0;
			for (int x = x0; x <= x1; x++)
			{
				yield return new Point((steep ? y : x), (steep ? x : y));
				error = error - dy;
				if (error < 0)
				{
					y += ystep;
					error += dx;
				}
			}
			yield break;
		}

		public static void PredictLocation(int oldX, int oldY, Direction targetDirection, ref int newX, ref int newY)
		{
			newX = oldX;
			newY = oldY;
			switch (targetDirection)
			{
				case Direction.North:
					newY--;
					break;
				case Direction.East:
					newX++;
					break;
				case Direction.South:
					newY++;
					break;
				case Direction.West:
					newX--;
					break;
			}
		}

		public static Color LoadColorFromFile(BinaryReader stream)
		{
			var r = stream.ReadByte();
			var g = stream.ReadByte();
			var b = stream.ReadByte();
			return Color.FromArgb(r, g, b);
		}
		public static void SaveToFile(this Color color, BinaryWriter stream)
		{
			stream.Write((byte)color.R);
			stream.Write((byte)color.G);
			stream.Write((byte)color.B);
		}

		public static string HTMLize(string text)
		{
			var html = new StringBuilder();
			var lines = text.Split('\n');
			var color = @"<c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)>";
			html.Append("<pre>");
			foreach (var line in lines)
			{
				var s = line;
				var colorClosers = 0;
				while (Regex.IsMatch(s, color))
				{
					var match = Regex.Match(s, color);
					if (match.Groups["fore"] == null)
					{
						s = s.Substring(0, match.Index) + "</span>" + s.Substring(match.Index + match.Length);
						colorClosers--;
					}
					else
					{
						var col = Toolkit.GetColor(match.Groups["fore"].ToString());
						s = s.Substring(0, match.Index) + "<span style=\"color: rgb(" + col.R + "," + col.G + "," + col.B + ");\">" + s.Substring(match.Index + match.Length);
						colorClosers++;
					}
				}
				html.Append(s);
				while (colorClosers > 0)
				{
					html.Append("</span>");
					colorClosers--;
				}
			}
			html.Append("</pre>");
			return html.ToString();
		}

		public static string Noxicize(this XmlElement element)
		{
			var r = "";
			foreach (var n in element.ChildNodes)
			{
				if (n is XmlText)
					r += ((XmlText)n).Value.Trim();
				else if (n is XmlElement)
				{
					var e = n as XmlElement;
					if (e.Name == "b")
						r += "<cWhite>";

					if (e.Name == "br")
						r += "\n";

					r += e.Noxicize();
		
					if (e.Name == "b")
						r += "<c>";

					if (e.Name == "p")
						r += "\n\n";
				}
			}
			return r;
		}

		public static Color Lerp(int sR, int sG, int sB, int dR, int dG, int dB, double amount)
		{
			if (amount < 0)
				amount = 0;
			else if (amount > 1)
				amount = 1;
			double iR = (dR - sR) / 100.0, iG = (dG - sG) / 100.0, iB = (dB - sB) / 100.0;
			var a = (int)(amount * 100);
			int tR = sR + (int)(iR * a);
			int tG = sG + (int)(iG * a);
			int tB = sB + (int)(iB * a);
			return Color.FromArgb(tR, tG, tB);
		}

		public static Color Lerp(Color source, Color dest, double amount)
		{
			return Lerp(source.R, source.G, source.B, dest.R, dest.G, dest.B, amount);
		}

		public static Color LerpDarken(this Color color, double amount)
		{
			return Lerp(color, Color.Black, amount);
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
				if (Math.Floor(cm) > 0)
					return m + "." + cm + "m";
				else
					return m + "m";
			}
			return cm.ToString("F2") + "cm";
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
			if (cock.HasToken("horse"))
				return "horse cock";
			else if (cock.HasToken("dog"))
				return "dog";
			//TODO
			return "cock";
		}

		public static string Tail(Token tail)
		{
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
	}

	public class TokenCarrier
	{
		public List<Token> Tokens;

		public bool HasToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t != null;
		}

		public Token GetToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t;
		}

		public void RemoveToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			if (t != null)
				Tokens.Remove(t);
		}

		public Token Path(string path)
		{
			var parts = path.Split('/');
			var point = this;
			var final = parts.Last();
			foreach (var p in parts)
			{
				var target = point.Tokens.Find(t => t.Name.Equals(p, StringComparison.InvariantCultureIgnoreCase));
				if (target == null)
					return null;
				if (target.Name.Equals(final, StringComparison.InvariantCultureIgnoreCase))
					return target;
				point = target;
			}
			return null;
		}

#if DEBUG
		public string DumpTokens(List<Token> list, int tabs)
		{
			var ret = new StringBuilder();
			foreach (var item in list)
			{
				ret.AppendFormat("{0}{1}", new string('\t', tabs), item.Name);
				if (item.Value != 0 || !string.IsNullOrWhiteSpace(item.Text))
				{
					ret.Append(": ");
					if (item.Value != 0)
						ret.Append(item.Value);
					else
						ret.AppendFormat("\"{0}\"", item.Text);
					ret.AppendLine();
				}
				else
					ret.AppendLine();
				if (item.Tokens.Count > 0)
					ret.Append(DumpTokens(item.Tokens, tabs + 1));
			}
			return ret.ToString();
		}
#endif
	}

	public class Name
	{
		public bool Female { get; set; }
		public string FirstName { get; set; }
		public string Surname { get; set; }
		public string Title { get; set; }
		public Culture Culture { get; set; }
		public Name()
		{
			FirstName = "";
			Surname = "";
			Title = "";
			Culture = Culture.DefaultCulture;
		}
		public Name(string name) : this()
		{
			var split = name.Split(' ');
			if (split.Length >= 1)
				FirstName = split[0];
			if (split.Length >= 2)
				Surname = split[1];
		}
		public void Regenerate()
		{
			FirstName = this.Culture.GetName(Female ? Noxico.Culture.NameType.Female : Noxico.Culture.NameType.Male);
			Surname = this.Culture.GetName(Noxico.Culture.NameType.Surname);
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
			return FirstName + (string.IsNullOrWhiteSpace(Surname) ? '_' + Surname : string.Empty);
		}
		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(FirstName);
			stream.Write(Surname);
			stream.Write(Title);
			stream.Write(Culture.ID);
		}
		public static Name LoadFromFile(BinaryReader stream)
		{
			var newName = new Name();
			newName.FirstName = stream.ReadString();
			newName.Surname = stream.ReadString();
			newName.Title = stream.ReadString();
			var cultureName = stream.ReadString();
			if (Culture.Cultures.ContainsKey(cultureName))
				newName.Culture = Culture.Cultures[cultureName];
			return newName;
		}
	}

	public class Character : TokenCarrier
	{
		private static XmlDocument xDoc;
		public static StringBuilder MorphBuffer = new StringBuilder();

		public Name Name { get; set; }
		public string Species { get; set; }
		public string Title { get; set; }
		public bool IsProperNamed { get; set; }
		public string A { get; set; }

		public override string ToString()
		{
			var g = GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == "female " && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")))
				g = "";
			if (IsProperNamed)
				return string.Format("{0}, {1} {3}", Name, A, g, Title);
			return string.Format("{0} {2}", A, g, Title);
		}

		public string GetName()
		{
			var g = GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || HasToken("malename"))) ||
				(g == "female " && (HasToken("femaleonly") || HasToken("femalename"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")))
				g = "";
			if (IsProperNamed)
				return Name.ToString();
			return string.Format("{0} {1}{2}", A, g, Species);
		}

		public string GetTitle(bool gendered = true)
		{
			if (HasToken("invisiblegender"))
				gendered = false;
			var g = GetGender() + " ";
			if ((g == "male " && (HasToken("maleonly") || GetToken("terms").HasToken("male"))) ||
				(g == "female " && (HasToken("femaleonly") || GetToken("terms").HasToken("female"))) ||
				(g == "hermaphrodite " && HasToken("hermonly")) ||
				!gendered)
				g = "";
			return string.Format("{0} {2}", A, g, Title);
		}

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
			Title = Species.ToLowerInvariant();
			if (g == "male" && GetToken("terms").HasToken("male"))
				Title = GetToken("terms").GetToken("male").Text;
			else if (g == "female" && GetToken("terms").HasToken("female"))
				Title = GetToken("terms").GetToken("female").Text;
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

		public float GetHumanScore()
		{
			var i = 0f;
			if (HasToken("penis") && !GetToken("penis").HasToken("ridged") && !GetToken("penis").HasToken("bumpy"))
				i += 0.20f;
			if (!HasToken("tail"))
				i += 0.18f;
			if (HasToken("ears") && GetToken("ears").HasToken("human"))
				i += 0.21f;
			if (HasToken("legs"))
				i += 0.12f;
			if (HasToken("feet"))
				i += 0.11f;
			if (!HasToken("antennae") && !HasToken("horns"))
				i += 0.18f;
			return i;
		}

		public float GetDemonScore()
		{
			var i = 0f;
			if (HasToken("penis") && (GetToken("penis").HasToken("ridged") || GetToken("penis").HasToken("bumpy")))
				i += 0.17f;
			if (HasToken("horns"))
				i += 0.22f;
			if (HasToken("skin") && (GetToken("skin").HasToken("blue") || GetToken("skin").HasToken("purple") || GetToken("skin").HasToken("red")))
				i += 0.19f;
			if (HasToken("hooves"))
				i += 0.21f;
			if (HasToken("tail") && GetToken("tail").HasToken("spaded"))
				i += 0.23f;
			return i;
		}

		public float GetGoblinScore()
		{
			var i = 0f;
			if (HasToken("skin") && (GetToken("skin").HasToken("green") || GetToken("skin").HasToken("darkgreen")))
				i += 0.40f;
			if (HasToken("ears") && GetToken("ears").HasToken("elfin"))
				i += 0.60f;
			return i;
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
			if (xDoc == null)
			{
				xDoc = new XmlDocument();
				xDoc.Load("Noxico.xml");
			}

			var newChar = new Character();
			var planSource = xDoc.SelectSingleNode("//uniques/character[@id=\"" + id + "\"]") as XmlElement;
			var plan = planSource.ChildNodes[0].Value;
			newChar.Tokens = Token.Tokenize(plan);
			newChar.Name = new Name(planSource.GetAttribute("name"));
			newChar.A = "a";
			newChar.IsProperNamed = planSource.HasAttribute("proper");
			/*
			var newChar = new Character();
			var planSource = xDoc.SelectSingleNode("//uniques/character[@name=\"" + name + "\"]") as XmlElement;
			var plan = xDoc.CreateElement("character");
			plan.InnerXml = planSource.InnerXml;
			newChar.IsProperNamed = planSource.HasAttribute("proper");
			newChar.Name = new Name(planSource.GetAttribute("name"));
			newChar.Species = planSource.GetAttribute("species");
			newChar.A = planSource.GetAttribute("a");
			Roll(plan);
			OneOf(plan);
			newChar.Tokens = Token.Tokenize(plan);
			*/

			var prefabTokens = new[] { "items", "health", "perks", "skills", "charisma", "climax", "cunning", "carnality", "stimulation", "sensitivity", "speed", "strength", "money", "ships" };
			var prefabTokenValues = new[] { 0, 10, 0, 0, 10, 0, 10, 0, 10, 10, 10, 15, 100, 0 };
			for (var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.Tokens.Add(new Token() { Name = prefabTokens[i], Value = prefabTokenValues[i] });
			if (!newChar.HasToken("culture"))
			{
				newChar.Tokens.Add(new Token() { Name = "culture" });
				newChar.GetToken("culture").Tokens.Add(new Token() { Name = "human" });
			}
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

			Console.WriteLine("Retrieved unique character {0}.", newChar);
			return newChar;
		}

		public static Character Generate(string bodyPlan, Gender gender)
		{
			if (xDoc == null)
			{
				xDoc = new XmlDocument();
				xDoc.Load("Noxico.xml");
			}

			var newChar = new Character();
			var planSource = xDoc.SelectSingleNode("//bodyplans/bodyplan[@id=\"" + bodyPlan + "\"]") as XmlElement;
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

			/*
			var skinTypes = new[] { "fur", "scales", "rubber", "slime" };
			foreach (var skinType in skinTypes)
			{
				if (newChar.HasToken(skinType) && newChar.GetToken(skinType).HasToken("copyhair"))
				{
					newChar.GetToken(skinType).Tokens.Clear();
					newChar.GetToken(skinType).Tokens.Add(newChar.GetToken("hair").GetToken("color").Tokens[0]);
					newChar.GetToken(skinType).Tokens.Add(new Token() { Name = "copyhair" }); //replace it so that NoxicoGame.CreatePlayerCharacter() can overrule.
					break;
				}
			}
			*/

			if (newChar.HasToken("culture") && newChar.GetToken("culture").Tokens.Count > 0)
			{
				var culture = newChar.GetToken("culture").Tokens[0].Name;
				if (Culture.Cultures.ContainsKey(culture))
					newChar.Name.Culture = Culture.Cultures[culture];
			}
			if (gender == Gender.Female)
				newChar.Name.Female = true;
			else if (gender == Gender.Herm || gender == Gender.Neuter)
				newChar.Name.Female = Toolkit.Rand.NextDouble() > 0.5;
			newChar.Name.Regenerate();
			var patFather = new Name() { Culture = newChar.Name.Culture, Female = false };
			var patMother = new Name() { Culture = newChar.Name.Culture, Female = true };
			patFather.Regenerate();
			patMother.Regenerate();
			newChar.Name.ResolvePatronym(patFather, patMother);
			newChar.IsProperNamed = true;

			var terms = newChar.GetToken("terms");
			newChar.Species = gender.ToString() + " " +  terms.GetToken("generic").Text;
			if (gender == Gender.Male && terms.HasToken("male"))
				newChar.Species = terms.GetToken("male").Text;
			else if (gender == Gender.Female && terms.HasToken("female"))
				newChar.Species = terms.GetToken("female").Text;
			else if (gender == Gender.Herm && terms.HasToken("herm"))
				newChar.Species = terms.GetToken("herm").Text;

			newChar.UpdateTitle();
			newChar.StripInvalidItems();

			var prefabTokens = new[] { "items", "health", "perks", "skills", "charisma", "climax", "cunning", "stimulation", "carnality", "sensitivity", "speed", "strength", "money", "ships" };
			var prefabTokenValues = new[] { 0, 20, 0, 0, 10, 0, 10, 0, 10, 10, 10, 15, 100, 0 }; 
			for(var i = 0; i < prefabTokens.Length; i++)
				if (!newChar.HasToken(prefabTokens[i]))
					newChar.Tokens.Add(new Token() { Name = prefabTokens[i], Value = prefabTokenValues[i] });
			if (!newChar.HasToken("culture"))
			{
				newChar.Tokens.Add(new Token() { Name = "culture" });
				newChar.GetToken("culture").Tokens.Add(new Token() { Name = "human" });
			}
			newChar.GetToken("health").Value = newChar.GetMaximumHealth();

			newChar.ApplyCostume();

			Console.WriteLine("Generated {0}.", newChar);
			return newChar;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			//stream.Write(Name ?? "");
			Name.SaveToFile(stream);
			stream.Write(Species ?? "");
			stream.Write(Title ?? "");
			stream.Write(IsProperNamed);
			stream.Write(A ?? "a");
			stream.Write(Tokens.Count);
			Tokens.ForEach(x => x.SaveToFile(stream));
		}

		public static Character LoadFromFile(BinaryReader stream)
		{
			var newChar = new Character();
			newChar.Name = Name.LoadFromFile(stream); //stream.ReadString();
			newChar.Species = stream.ReadString();
			newChar.Title = stream.ReadString();
			newChar.IsProperNamed = stream.ReadBoolean();
			newChar.A = stream.ReadString();
			var numTokens = stream.ReadInt32();
			for (var i = 0; i < numTokens; i++)
				newChar.Tokens.Add(Token.LoadFromFile(stream));
			return newChar;
		}

		public void ApplyCostume()
		{
			if (HasToken("costume"))
			{

				var costumesToken = GetToken("costume");
				var costumeChoices = costumesToken.Tokens;
				var costume = new Token();
				var lives = 10;
				while (costume.Tokens.Count == 0 && lives > 0)
				{
					var pick = costumeChoices[Toolkit.Rand.Next(costumeChoices.Count)].Name;
					var xElement = xDoc.SelectSingleNode("//costume[@id=\"" + pick + "\"]") as XmlElement;
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

				FoldRandom(costume);
				FoldVariables(costume);

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
					if (!equipable.HasToken("undershirt") && !equipable.HasToken("underpants") && !equipable.HasToken("shirt") && !equipable.HasToken("pants") && !equipable.HasToken("jacket") && !equipable.HasToken("coat"))
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

		private void FoldRandom(Token token)
		{
			if (token == null)
				return;
			while(token.HasToken("random"))
			{
				var rnd = token.GetToken("random");
				var pick = rnd.Tokens[Toolkit.Rand.Next(rnd.Tokens.Count)];
				token.Tokens.Remove(rnd);
				foreach (var t in pick.Tokens)
					token.Tokens.Add(t);
				//rnd = pick;
			}
		}

		private void FoldVariables(Token token, string[] vars = null)
		{
			if (token == null)
				return;
			if (vars == null)
				vars = new string[100];
			while (token.HasToken("setvar"))
			{
				var setvar = token.GetToken("setvar");
				var id = (int)setvar.GetToken("id").Value;
				var value = setvar.GetToken("value");
				vars[id] = value.Text;
				token.RemoveToken("setvar");
			}
			while (token.HasToken("var"))
			{
				var getvar = token.GetToken("var");
				var id = (int)getvar.Value;
				if (string.IsNullOrWhiteSpace(vars[id]))
					token.RemoveToken("var");
				else
					getvar.Name = vars[id];
			}
			if (!string.IsNullOrWhiteSpace(token.Text) && token.Text.Trim().StartsWith("var "))
			{
				var id = int.Parse(token.Text.Trim().Substring(4));
				if (string.IsNullOrWhiteSpace(vars[id]))
					token.Text = "<invalid token>";
				else
					token.Text = vars[id];
			}
			foreach (var child in token.Tokens)
				FoldVariables(child, vars);
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
			var i = 0.20 / (1 + (l / 2));
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
			var sb = new StringBuilder();

			sb.AppendFormat("[He] [is] {0} tall. ", Descriptions.Length(this.GetToken("tallness").Value));

			var stimulation = this.GetToken("stimulation").Value;

			#region Face and Skin
			var skinDescriptions = new Dictionary<string, Dictionary<string, string>>()
				{
					{ "skin",
						new Dictionary<string, string>()
						{
							{ "normal", "[He] [has] a fairly normal face, with {0} skin." },
							{ "genbeast", "[He] [has] an animalistic face, though it's difficult to tell exactly what kind of animal. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
							{ "horse", "[His] is equine in shape and structure. It looks somewhat odd as [he] [has] no fur, only {0} skin." },
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
							{ "horse", "[His] is equine in shape and structure. It looks somewhat odd as [he] [has] no fur, only {0} scales." },
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
							{ "horse", "[His] is equine in shape and structure. It looks somewhat odd as [his] face is made of translucent {0} slime." },
							{ "dog", "[He] [has] a dog-like face, complete with a wet nose. It looks somewhat odd as [his] face is made of translucent {0} slime." },
							{ "cow", "[He] [has] a face resembling that of a minotaur, with cow-like features, particularly a squared off wet nose. It looks somewhat odd as [his] face is made of translucent {0} slime." },
							{ "cat", "[He] [has] a cat-like face, complete with a cute, moist nose, whiskers, and slitted eyes. It looks somewhat odd as [his] face is made of translucent {0} slime." },
							{ "reptile", "[His] face is that of a lizard, complete with a toothy maw and pointed snout. Translucent {0} slime completes the look, making [him] look quite fearsome." },
						}
					},
				};
			var skinName = this.Path("skin/type") != null ? this.Path("skin/type").Tokens[0].Name : "skin";
			var faceType = this.HasToken("face") ? this.GetToken("face").Tokens[0].Name : "normal";
			var hairColor = this.Path("hair/color") != null ? Toolkit.NameColor(this.Path("hair/color").Text).ToLowerInvariant() : "<null>";
			var skinColor = skinName == "slime" ? hairColor : Toolkit.NameColor(this.Path("skin/color").Text).ToLowerInvariant();
			sb.AppendFormat(skinDescriptions[skinName][faceType], skinColor, hairColor);
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

				if (!this.HasToken("ears") || this.GetToken("ears").HasToken("human"))
					sb.AppendFormat("[His] {0} looks good, accentuating [his] features well.", hairDesc);
				else if (this.GetToken("ears").HasToken("elfin"))
					sb.AppendFormat("[His] {0} is parted by a pair of long, pointed ears.", hairDesc);
				else if (this.GetToken("ears").HasToken("genbeast"))
					sb.AppendFormat("[His] {0} is parted by a pair of sizable, triangular ears.", hairDesc);
				else if (this.GetToken("ears").HasToken("horse"))
					sb.AppendFormat("[His] {0} parts around a pair of very horse-like ears that grow up from [his] head.", hairDesc);
				else if (this.GetToken("ears").HasToken("dog"))
					sb.AppendFormat("[His] {0} is parted by a pair of pointed dog-ears.", hairDesc);
				else if (this.GetToken("ears").HasToken("cat"))
					sb.AppendFormat("[His] {0} is parted by a pair of cat ears.", hairDesc);
				else if (this.GetToken("ears").HasToken("cow"))
					sb.AppendFormat("[His] {0} is parted by a pair of rounded cow-ears that stick out sideways.", hairDesc);
				else if (this.GetToken("ears").HasToken("frill"))
					sb.AppendFormat("[His] {0} is parted by a pair of draconic frills.", hairDesc);

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

				if (this.GetToken("ears").HasToken("elfin"))
					sb.AppendFormat("  A pair of large pointy ears stick out from [his] skull.");
				else if (this.GetToken("ears").HasToken("genbeast"))
					sb.AppendFormat(" A pair of large-ish animal ears have sprouted from the top of [his] head.");
				else if (this.GetToken("ears").HasToken("horse"))
					sb.AppendFormat(" A pair of horse-like ears rise up from the top of [his] head.");
				else if (this.GetToken("ears").HasToken("dog"))
					sb.AppendFormat(" A pair of dog ears protrude from [his] skull, flopping down adorably.");
				else if (this.GetToken("ears").HasToken("cat"))
					sb.AppendFormat(" A pair of cute, fuzzy cat-ears have sprouted from the top of [his] head.");
				else if (this.GetToken("ears").HasToken("cow"))
					sb.AppendFormat(" A pair of round, floppy cow ears protrude from the sides of [his] skull.");
				else if (this.GetToken("ears").HasToken("frill"))
					sb.AppendFormat(" A set of draconic frills extend from the sides of [his] skull.");
				else if (this.GetToken("ears").HasToken("bunny"))
					sb.AppendFormat(" A pair of long bunny ears [his] skull, flopping down adorably.");

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

			#region Tails
			if (this.HasToken("tail"))
			{
				//TODO: buttdescript
				var tail = this.GetToken("tail");
				//var hairColor = Color(this.GetToken("hair").Tokens.Find(x => x.Name != "length").Name);
				var buttDesc = Toolkit.PickOne("butt", "ass", Descriptions.Butt(this.GetToken("ass")));
				if (tail.HasToken("spider"))
				{
					var spider = tail.GetToken("spider");
					if (!spider.HasToken("venom"))
						spider.Tokens.Add(new Token() { Name = "venom", Value = 10 });
					sb.AppendFormat("A large spherical spider-abdomen grows out from [his] backside, covered in shiny red and black chitin. Though it weighs heavy and bobs with every motion, it doesn't seem to slow [him] down.");
					if (pa is Player)
					{
						if (spider.GetToken("venom").Value > 50 && spider.GetToken("venom").Value < 80)
							sb.AppendFormat(" [His] bulging arachnid posterior feels fairly full of webbing.");
						else if (spider.GetToken("venom").Value >= 80 && spider.GetToken("venom").Value < 100)
							sb.AppendFormat(" [His] bulbous arachnid rear bulges and feels very full of webbing.");
						else if (spider.GetToken("venom").Value == 100)
							sb.AppendFormat(" [His] swollen spider-butt is distended with the sheer amount of webbing it's holding.");
					}
					else
					{
						if (spider.GetToken("venom").Value >= 80 && spider.GetToken("venom").Value < 100)
							sb.AppendFormat(" [His] bulbous arachnid rear bulges as if full of webbing.");
						else if (spider.GetToken("venom").Value == 100)
							sb.AppendFormat(" [His] swollen spider-butt is distended with the sheer amount of webbing it's holding.");
					}
				}
				else if (tail.HasToken("stinger"))
				{
					var stinger = tail.GetToken("stinger");
					if (!stinger.HasToken("venom"))
						stinger.Tokens.Add(new Token() { Name = "venom", Value = 10 });
					sb.AppendFormat("A large insectile bee-abdomen dangles from just above [his] backside, bobbing with its own weight. It is covered in hard chitin with black and yellow stripes, tipped with a dagger-like stinger.");
					if (stinger.GetToken("venom").Value > 50 && stinger.GetToken("venom").Value < 80)
						sb.AppendFormat(" A single drop of poison hangs from [his] exposed stinger.");
					else if (stinger.GetToken("venom").Value >= 80 && stinger.GetToken("venom").Value < 100)
						sb.AppendFormat(" Poisonous bee venom coats [his] stinger completely.");
					else if (stinger.GetToken("venom").Value == 100)
						sb.AppendFormat(" Venom drips from [his] poisoned stinger regularly.");
				}
				else
				{
					var tails = new Dictionary<string, string>()
						{
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
						};
					var tailT = tail.Tokens.Count > 0 ? tail.Tokens[0].Name : "genbeast";
					if (!tail.HasToken(tailT))
						tailT = "invalid";
					sb.AppendFormat(tails[tailT], skinColor, hairColor, buttDesc);
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
				if (this.HasToken("quadruped"))
				{
					//Assume horse, accept genbeast, cow or dog legs, mock and reject others.
					if (legs.Tokens.Count == 0)
					{
						sb.AppendFormat("(<b>NOTICE<b>: no leg type specified. Assuming horse legs.) ");
						legs.Tokens.Add(new Token() { Name = "horse" });
					}
					if (legs.HasToken("stilleto") || legs.HasToken("claws") || legs.HasToken("insect"))
					{
						sb.AppendFormat("(<b>NOTICE<b>: silly leg type specified. Changing to genbeast.) ");
						//Clear out everything, then re-add quadruped and genbeast.
						legs.Tokens.Clear();
						legs.Tokens.Add(new Token() { Name = "genbeast" });
					}
					if (legs.HasToken("horse") || legs.HasToken("cow"))
						sb.AppendFormat("Four perfectly reasonable horse legs extend from [his] chest and waist, ending in {0} hooves.", this.HasToken("marshmallow") ? "soft" : "sturdy");
					else if (legs.HasToken("genbeast"))
						sb.AppendFormat("[He] [has] four digitigrade legs growing downwards from [his] body, ending in beastly paws.");
					else if (legs.HasToken("dog"))
						sb.AppendFormat("[He] [has] four digitigrade legs growing downwards from [his] chest and waist, ending in dog-like paws.");
				}
				else
				{
					if (legs.Tokens.Count == 0 || legs.HasToken("human"))
						sb.AppendFormat("Two normal human legs grow down from [his] waist, ending in normal human feet.");
					else if (legs.HasToken("genbeast"))
						sb.AppendFormat("Two digitigrade legs grow downwards from [his] waist, ending in beastlike hind-paws.");
					else if (legs.HasToken("cow") || legs.HasToken("horse"))
						sb.AppendFormat("[His] legs are muscled and jointed oddly, covered in fur, and end in a pair of {0} hooves.", this.HasToken("marshmallow") ? "soft" : "bestial");
					else if (legs.HasToken("dog"))
						sb.AppendFormat("Two digitigrade legs grow downwards from [his] waist, ending in dog-like hind-paws.");
					else if (legs.HasToken("stilleto"))
						sb.AppendFormat("[His] perfect lissom legs end in mostly human feet, apart from the horn protruding straight down from the heel that forces [him] to walk with a sexy, swaying gait.");
					else if (legs.HasToken("claws"))
						sb.AppendFormat("[His] lithe legs are capped with flexible clawed feet. Sharp black nails grow from the toes, giving [him] fantastic grip.");
					else if (legs.HasToken("insect"))
						sb.AppendFormat("[His] legs are covered in a shimmering insectile carapace up to mid-thigh, looking more like a pair of 'fuck me' boots than exoskeleton. A bit of downy yellow and black fur fuzzes [his] upper thighs.");
				}
			}
			else if (this.HasToken("snaketail"))
				sb.AppendFormat("Below [his] waist [his] flesh is fused together into an a very long snake-like tail.");
			else if (this.HasToken("slimeblob"))
				sb.AppendFormat("Below [his] waist is nothing but a shapeless mass of goo, the very top of leg-like shapes just barely recognizable.");
			sb.AppendLine();
			#endregion

			//Pregnancy

			var breastsVisible = false;
			var crotchVisible = false;
			#region Equipment
			if (this.HasToken("items"))
			{
				var carried = new List<InventoryItem>();
				var hands = new List<InventoryItem>();
				var fingers = new List<InventoryItem>();
				InventoryItem underpants = null;
				InventoryItem undershirt = null;
				InventoryItem pants = null;
				InventoryItem shirt = null;
				InventoryItem jacket = null;
				InventoryItem cloak = null;
				InventoryItem head = null;
#region Collection
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
						{jacket = find;
							jacket.tempToken = carriedItem;
						}
						if (eq.HasToken("cloak"))
						{	cloak = find;
							cloak.tempToken = carriedItem;
						}
						if (eq.HasToken("head"))
						{	head = find;
							head.tempToken = carriedItem;
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
						/*
						sb.AppendLine();
						if (find.GetToken("equipable").HasToken("description") && find.GetToken("equipable").GetToken("description").HasToken("equipped"))
							sb.AppendFormat(find.GetToken("equipable").GetToken("description").GetToken("equipped").Text);
						else
							sb.AppendFormat("[He] [is] {0} {1}.", "wearing", find.ToString(carriedItem));
						*/
					}
					else
						carried.Add(find);
				}
#endregion
				var visible = new List<string>();
				if (head != null)
					visible.Add(head.ToString(head.tempToken));
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
				if (HasToken("noarms"))
				{
					if (hands.Count == 2)
						sb.AppendFormat("(<b>NOTICE<b>: dual wielding with mouth?) ");
					sb.AppendFormat("[He] [has] {0} held between [his] teeth.", hands[0]);
				}
				else
				{
					if (hands.Count == 1)
						sb.AppendFormat("[He] has {0} in [his] hands.", hands[0]);
					else if (hands.Count == 1)
						sb.AppendFormat("[He] has {0} in [his] hands.", hands[0]);
				}
				sb.AppendLine();
			}
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
					//TODO: multicock
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
			sb.Replace("[is]", pa is Player ? "are" : "is");

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
					list.Add(person.Name + " -- " + string.Join(", ", person.Tokens.Select(x => x.Name)));
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
				if (equip.HasToken("pants") || equip.HasToken("underpants"))
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
			if (xDoc == null)
			{
				xDoc = new XmlDocument();
				xDoc.Load("Noxico.xml");
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

			var planSource = xDoc.SelectSingleNode("//bodyplans/bodyplan[@id=\"" + targetPlan + "\"]") as XmlElement;
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
			if (source.GetToken("tail").Tokens.Count == 0)
				source.GetToken("tail").Tokens.Add(new Token() { Name = "<none>" });
			if (target.GetToken("tail").Tokens.Count == 0)
				target.GetToken("tail").Tokens.Add(new Token() { Name = "<none>" });

			//Change tail type?
			if (target.GetToken("tail").Tokens[0].Name != source.GetToken("tail").Tokens[0].Name)
			{
				toChange.Add(source.Path("tail"));
				changeTo.Add(target.Path("tail"));
				doNext.Add(false);

				if (source.Path("tail/<none>") != null)
					report.Add((isPlayer ? "You have" : this.Name + " has") + " sprouted a " + Descriptions.Tail(target.Path("tail")) + ".");
				else
					report.Add((isPlayer ? "Your" : this.Name + "'s") + " tail has become a " + Descriptions.Tail(target.Path("tail")) + ".");
			}

			//Change entire skin type?
			if (target.Path("skin/type").Tokens[0].Name != source.Path("skin/type").Tokens[0].Name)
			{
				toChange.Add(source.Path("skin"));
				changeTo.Add(target.Path("skin"));
				doNext.Add(false);

				switch (target.Path("skin/type").Tokens[0].Name)
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
						report.Add((isPlayer ? "You have" : this.Name + " has") + " grown " + Toolkit.NameColor(target.Path("skin/color").Text) + ' ' + target.Path("skin/type").Tokens[0].Name + '.');
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
			if (target.GetToken("face").Tokens[0].Name != source.GetToken("face").Tokens[0].Name)
			{
				toChange.Add(source.Path("face"));
				changeTo.Add(target.Path("face"));
				doNext.Add(false);

				var faceDescription = fooIneReports.ContainsKey(target.GetToken("face").Tokens[0].Name) ? fooIneReports[target.GetToken("face").Tokens[0].Name] : target.GetToken("face").Tokens[0].Name;
				report.Add((isPlayer ? "Your" : this.Name + "'s") + " face has rearranged to a more " + faceDescription + " form");
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
					if (source.GetToken("legs").Tokens.Count == 0)
						source.GetToken("legs").Tokens.Add(new Token() { Name = "human" });

					if (source.GetToken("legs").Tokens[0].Name != target.GetToken("legs").Tokens[0].Name)
					{
						var legDescription = fooIneReports.ContainsKey(target.GetToken("legs").Tokens[0].Name) ? fooIneReports[target.GetToken("legs").Tokens[0].Name] : target.GetToken("legs").Tokens[0].Name;
						doReport((isPlayer ? "You have" : this.Name + " has") + " grown " + legDescription + " legs.");
						source.GetToken("legs").Tokens[0].Name = target.GetToken("legs").Tokens[0].Name;
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
					return;
				}
			}

			//Nothing to do?
			if (toChange.Count == 0)
			{
				doReport("There was no further effect.");
				return;
			}

			var choice = Toolkit.Rand.Next(toChange.Count);
			var changeThis = toChange[choice];
			var toThis = changeTo[choice];
			doReport(report[choice]);

			#region Slime TO
			//Handle changing skin type to and from slime
			//TODO: make this handle any skin type transition?
			if (toThis.Name == "skin" && toThis.Path("type/slime") != null)
			{
				//To slime
				var origName = Path("skin/type").Tokens[0].Name;
				var originalSkin = Path("originalskins");
				if (originalSkin == null)
				{
					Tokens.Add(new Token() { Name = "originalskins" });
					originalSkin = GetToken("originalskins");
				}
				var thisSkin = originalSkin.Path(origName);
				if (thisSkin == null)
				{
					originalSkin.Tokens.Add(new Token() { Name = origName });
					thisSkin = originalSkin.GetToken(origName);
				}
				thisSkin.Text = Path("skin/color").Text;

				if (!originalSkin.HasToken("hair"))
					originalSkin.Tokens.Add(new Token() { Name = "hair" });
				originalSkin.GetToken("hair").Text = Path("hair/color").Text;

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
				var memorized = Path("originalskins/" + thisSkin.GetToken("type").Tokens[0].Name);
				if (memorized != null)
				{
					if (!thisSkin.HasToken("color"))
						thisSkin.Tokens.Add(new Token() { Name = "color" });
					Path("skin/color").Text = memorized.Text;
				}
				//Grab the hair too?
				if (Path("hair/color") != null)
					Path("hair/color").Text = Path("originalskins/hair").Text;
			}
			#endregion

			//remove any dummy tails
			if (source.Path("tail/<none>") != null)
				source.RemoveToken("tail");
			
			if (doNext[choice] || Toolkit.Rand.Next(100) < continueChance)
			{
				Morph(targetPlan);
			}
		}
	}

	public class InventoryItem : TokenCarrier
	{
		public string ID { get; private set; }
		public string Name { get; private set; }
		public string UnknownName { get; private set; }
		public bool IsProperNamed { get; set; }
		public string A { get; set; }
		public string The { get; set; }
		public string Script { get; private set; }

		public Token tempToken { get; set; }

		/*
		public override string ToString()
		{
			if (IsProperNamed)
				return Name;
			return string.Format("{0} {1}", A, Name);
		}
		*/
		public override string ToString()
		{
			return ToString(null);
		}

		public string ToString(Token token, bool the = false, bool a = true)
		{
			if (ID == "book" && token != null && token.HasToken("id") && token.GetToken("id").Value < NoxicoGame.BookTitles.Count)
				return string.Format("\"{0}\"", NoxicoGame.BookTitles[(int)token.GetToken("id").Value]);

			var name = (token != null && token.HasToken("unidentified") && !string.IsNullOrWhiteSpace(UnknownName)) ? UnknownName : Name;
			var color = (token != null && token.HasToken("color")) ? Toolkit.NameColor(token.GetToken("color").Text) : "";
			var reps = new Dictionary<string, string>()
			{
				{ "[color]", color },
				{ "[, color]", ", " + color },
				{ "[color ]", color + " " },
				{ "[color, ]", color + ", " },
			};
			if (color == "")
			{
				foreach (var key in reps.Keys)
					name = name.Replace(key, "");
			}
			else
			{
				foreach (var item in reps)
					name = name.Replace(item.Key, item.Value);
			}

			if (IsProperNamed || !a)
				return name;
			return string.Format("{0} {1}", the ? The : A, name);
		}

		public static InventoryItem FromXML(XmlElement x)
		{
			var ni = new InventoryItem();
			ni.ID = x.GetAttribute("id");
			ni.Name = x.GetAttribute("name");
			ni.UnknownName = x.GetAttribute("unknown");
			ni.A = x.GetAttribute("a");
			ni.The = x.GetAttribute("the");
			ni.IsProperNamed = x.GetAttribute("proper") == "true";

			var t = x.ChildNodes.OfType<XmlCDataSection>().FirstOrDefault();
			if (t != null)
				ni.Tokens = Token.Tokenize(t.Value);
#if DEBUG
			else
			{
				ni.Tokens = Token.Tokenize(x);
				Console.WriteLine("Item {0} conversion:", ni.ID);
				Console.WriteLine("\t\t\t<![CDATA[");
				Console.Write(ni.DumpTokens(ni.Tokens, 0));
				Console.WriteLine("\t\t\t]]>");
				Console.WriteLine();
			}
#endif

			ni.Script = null;
			var ses = x.SelectNodes("script");
			if (ses.Count == 0)
				return ni;
			var s = ses[0].ChildNodes.OfType<XmlCDataSection>().FirstOrDefault();
			if (s != null)
				ni.Script = s.Value;
			return ni;
		}

		public void CheckHands(Character character, string slot)
		{
			var max = slot == "ring" ? 8 : 2;
			var worn = 0;
			var items = character.GetToken("items");
			foreach (var carriedItem in items.Tokens)
			{
				if (!carriedItem.HasToken("equipped"))
					continue;
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				var equip = find.GetToken("equipable");
				if (equip.HasToken(slot))
					worn++;
			}
			if (worn >= max)
				throw new ItemException("Your hands are already full.");
		}

		public bool CanSeeThrough()
		{
			//return true;
			if(!HasToken("equipable"))
				throw new ItemException("Tried to check translucency on something not equipable.");
			return this.GetToken("equipable").HasToken("translucent");
		}

		public bool CanReachThrough()
		{
			if (!HasToken("equipable"))
				throw new ItemException("Tried to check reach on something not equipable.");
			return this.GetToken("equipable").HasToken("reach");
		}

		public bool Equip(Character character, Token item)
		{
			/*
			if rings and character is quadruped, error out.
			if required slots have covering slots
				check for target slot's reachability.
				if unreachable, try to temp-remove items in covering slots, recursively.
				if still unreachable, error out.
			if required slots are taken
				try to unequip the items in those slots, recursively.
				if required slots are still taken, error out;
				else, mark the item as equipped.
			replace each temp-removed item whose required slots are still free.
			*/
			var equip = this.GetToken("equipable");
			var tempRemove = new Stack<Token>();
			var items = character.GetToken("items");

			//TODO: make full quadrupeds equip weapons in their mouth instead of the hands they don't have.
			//This means they can carry only ONE weapon at a time, and maybe not be able to converse until unequipped.
			if ((equip.HasToken("hands") || equip.HasToken("ring")) && (character.HasToken("noarms")))
				throw new ItemException("[You] cannot put on the " + this.Name + " because [you] lack[s] hands.");

			if (equip.HasToken("hand"))
				CheckHands(character, "hand");
			else if (equip.HasToken("ring"))
				CheckHands(character, "ring");

			foreach (var t in equip.Tokens)
			{
				if (t.Name == "underpants" && !TempRemove(character, tempRemove, "pants"))
					return false;
				else if (t.Name == "undershirt" && !TempRemove(character, tempRemove, "shirt"))
					return false;
				else if (t.Name == "shirt" && !TempRemove(character, tempRemove, "jacket"))
					return false;
				else if (t.Name == "jacket" && !TempRemove(character, tempRemove, "cloak"))
					return false;
			}

			item.Tokens.Add(new Token() { Name = "equipped" });

			//Difficult bit: gotta re-equip tempremovals without removing the target item all over. THAT WOULD BE QUITE BAD.
			return true;
		}

		public bool Unequip(Character character, Token item)
		{
			/*
			if item's slots have covering slots
				check for target slot's reachability.
				if unreachable, try to temp-remove items in covering slots, recursively.
				if still unreachable, error out.
			if item is cursed, error out
			mark item as unequipped.
			*/
			if (item != null && item.HasToken("cursed") && item.GetToken("cursed").HasToken("known"))
				throw new ItemException("[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.");

			var equip = this.GetToken("equipable");
			var tempRemove = new Stack<Token>();
			var items = character.GetToken("items");
			foreach (var t in equip.Tokens)
			{
				if (t.Name == "underpants")
					TempRemove(character, tempRemove, "pants");
				else if (t.Name == "undershirt")
					TempRemove(character, tempRemove, "shirt");
				else if (t.Name == "shirt")
					TempRemove(character, tempRemove, "jacket");
				else if (t.Name == "jacket")
					TempRemove(character, tempRemove, "cloak");
			}

			if (item == null)
				item = items.Tokens.Find(x => x.Name == this.ID);

			if (item.HasToken("cursed"))
			{
				item.GetToken("cursed").Tokens.Add(new Token() { Name = "known" });
				throw new ItemException("[You] tr[ies] to unequip " + this.ToString(item, true) + ", but find[s] " + (this.HasToken("plural") ? "them" : "it") + " stuck to [your] body!\n");
			}

			item.Tokens.Remove(item.GetToken("equipped"));
			
			//Not sure about automatically putting pants back on after taking them off to take off underpants...
			//while (tempRemove.Count > 0)
			//	tempRemove.Pop().Tokens.Add(new Token() { Name = "equipped" });
			
			return true;
		}

		private bool TempRemove(Character character, Stack<Token> list, string slot)
		{
			foreach (var carriedItem in character.GetToken("items").Tokens)
			{
				if (!carriedItem.HasToken("equipped"))
					continue;
				var find = NoxicoGame.KnownItems.Find(x => x.ID == carriedItem.Name);
				var equip = find.GetToken("equipable");
				if (equip == null)
				{
					System.Windows.Forms.MessageBox.Show("Item " + carriedItem.Name + " is marked as equipped, but " + find.Name + " is not equippable.");
					carriedItem.RemoveToken("equipped");
					continue;
				}
				if (equip.HasToken(slot))
				{
					if (equip.HasToken("reach"))
						return true;
					var success = find.Unequip(character, null);
					if (success)
						list.Push(carriedItem);
					return success;
				}
			}
			return true;
		}

		public void Use(Character character, Token item, bool noConfirm = false)
		{
			var boardchar = NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<BoardChar>().First(x => x.Character == character);
			var runningDesc = "";

			#region Books
			if (this.ID == "book")
			{
				TextScroller.ReadBook((int)item.GetToken("id").Value);
				return;
			}
			#endregion

			#region Equipment
			if (this.HasToken("equipable"))
			{
				if (item == null)
				{
					var items = character.GetToken("items");
					item = items.Tokens.Find(x => x.Name == this.ID);
				}

				if (!item.HasToken("equipped"))
				{
					//TODO: only ask if it's the player?
					//Not wearing it
					MessageBox.Ask(runningDesc + "Equip " + this.ToString(item, true) + "?", () =>
						{
							try
							{
								if (this.Equip(character, item))
								{
									runningDesc += "[You] equip[s] " + this.ToString(item, true) + ".";
								}
							}
							catch (ItemException c)
							{
								runningDesc += c.Message;
							}
							MessageBox.Message(runningDesc.Viewpoint(boardchar));
							return;
						},
						null);
				}
				else
				{
					//Wearing/wielding it
					if (item.HasToken("cursed") && item.GetToken("cursed").HasToken("known"))
					{
						runningDesc += "[You] can't unequip " + this.ToString(item, true) + "; " + (this.HasToken("plural") ? "they are" : "it is") + " cursed.";
						MessageBox.Message(runningDesc.Viewpoint(boardchar));
						return;
					}
					MessageBox.Ask("Unequip " + this.ToString(item, true) + "?", () =>
						{
							try
							{
								if (this.Unequip(character, item))
								{
									runningDesc += "[You] unequip[s] " + this.ToString(item, true) + ".";
								}
							}
							catch (ItemException x)
							{
								runningDesc += x.Message;
							}
							MessageBox.Message(runningDesc.Viewpoint(boardchar));
							return;
						},
						null);
				}
				return;
			}
			#endregion

			if (this.HasToken("quest"))
			{
				if (this.HasToken("description"))
					runningDesc = this.GetToken("description").Text + "\n\n";
				MessageBox.Message(runningDesc + "This item has no effect.");
				return;
			}

			//Confirm use of potentially hazardous items
			if (!noConfirm)
			{
				var name = new StringBuilder();
				if (this.IsProperNamed)
					name.Append(string.IsNullOrWhiteSpace(this.The) ? "" : this.The + ' ');
				else
					name.Append(string.IsNullOrWhiteSpace(this.A) ? "" : this.A + ' ');
				name.Append(this.Name);
				if(this.HasToken("description"))
				{
					//No need to check for "worn" or "examined" here...
					runningDesc = this.GetToken("description").Text + "\n\n";
				}
				MessageBox.Ask(runningDesc + "Do you want to use " + this.ToString(item, true) + "?", () => { this.Use(character, item, true); }, null);
				return;
			}

			if (!string.IsNullOrWhiteSpace(this.Script))
			{
				Console.WriteLine("------\nSCRIPT\n------");
				var script = this.Script.Split('\n'); //this.GetToken("script").Text.Split('\n');
				boardchar.ScriptRunning = true;
				boardchar.ScriptPointer = 0;
				Noxicobotic.Run(boardchar, script);
				boardchar.ScriptRunning = false;
				return;
			}

			#region Penis play
			if (this.HasToken("growpenis"))
			{
				var cock = this.GetToken("growpenis");
				var type = "";
				var initial = 4f;
				var increase = 1f;
				var multichance = 0;
				if (cock.HasToken("type") && cock.GetToken("type").Tokens.Count > 0)
				{
					if (cock.GetToken("type").HasToken("byscore"))
					{
						var gS = character.GetGoblinScore();
						var dS = character.GetDemonScore();
						var hS = character.GetHumanScore();
						if (dS > hS && dS > gS)
							type = "studded";
						else if (gS > hS)
							type = "";
					}
					else
						type = cock.GetToken("type").Tokens[0].Name;
				}
				if (cock.HasToken("initial"))
					initial = cock.GetToken("initial").Value;
				if (cock.HasToken("increase"))
					increase = cock.GetToken("increase").Value;
				if (cock.HasToken("fudge"))
				{
					initial += ((float)Toolkit.Rand.NextDouble() * cock.GetToken("fudge").Value);
					increase += ((float)Toolkit.Rand.NextDouble() * cock.GetToken("fudge").Value);
				}
				if (cock.HasToken("multichance"))
					multichance = (int)cock.GetToken("multichance").Value;

				Token newCock = null;
				if (!character.HasToken("penis") || (multichance > 0 && Toolkit.Rand.Next(100) > multichance))
				{
					newCock = new Token() { Name = "penis" };
					newCock.Tokens.Add(new Token() { Name = "thickness", Value = 1 });
					newCock.Tokens.Add(new Token() { Name = "length", Value = initial });
					newCock.Tokens.Add(new Token() { Name = "cumsource" });
					newCock.Tokens.Add(new Token() { Name = "canfuck" });
					character.Tokens.Add(newCock);
					runningDesc += "[You] [have] grown " + (character.Tokens.Count(x => x.Name == "penis") > 1 ? "an additional " : "a ") + Descriptions.Length(increase) + " penis!\n";
				}
				else if (character.HasToken("penis"))
				{
					var cocks = character.Tokens.FindAll(x => x.Name == "penis").ToArray();
					if (cocks.Length == 1)
						newCock = cocks[0];
					else
						newCock = cocks[Toolkit.Rand.Next(cocks.Length)];
					newCock.GetToken("length").Value += increase;
					runningDesc += (cocks.Length > 1 ? "One of [your] cocks" : "[Your] cock") + " has grown by " + Descriptions.Length(increase) + ".";
				}
				character.UpdateTitle();
			}
			if (this.HasToken("shrinkpenis"))
			{
				if (character.HasToken("penis"))
				{
					var cock = this.GetToken("shrinkpenis");
					var decrease = 4f;
					var result = "";
					var removals = 0;
					if (cock.HasToken("decrease"))
						decrease = cock.GetToken("decrease").Value;
					if (cock.HasToken("fudge"))
						decrease += ((float)Toolkit.Rand.NextDouble() * cock.GetToken("fudge").Value);
					var cocks = character.Tokens.FindAll(x => x.Name == "penis").ToArray();
					if (cock.HasToken("all"))
					{
						foreach (var target in cocks)
						{
							target.GetToken("length").Value -= decrease;
							if (target.GetToken("length").Value <= 0)
							{
								character.Tokens.Remove(target);
								removals++;
							}
						}
						if (removals == cocks.Length)
							result = (cocks.Length > 1 ? "All of [your] cocks have" : "[Your] cock has") + " completely receded into nothingness!";
						else if (removals == 0)
							result = (cocks.Length > 1 ? "All of [your] cocks have" : "[Your] cock has") + " shrunk by " + Descriptions.Length(decrease) + ".";
						else if (cocks.Length > 1 && removals > 0)
							result = "Some of [your] cocks have shrunk by " + Descriptions.Length(decrease) + ", and " + removals + (removals > 1 ? " have" : " has") + " disappeared entirely.";
					}
					else
					{
						var targetCock = cocks[0];
						if (cocks.Length > 1)
							targetCock = cocks[Toolkit.Rand.Next(cocks.Length)];
						targetCock.GetToken("length").Value -= decrease;
						if (targetCock.GetToken("length").Value <= 0)
						{
							character.Tokens.Remove(targetCock);
							result = (cocks.Length > 1 ? "One of [your] cocks" : "[Your] cock") + " has completely disappeared into nothingness!";
						}
						else
							result = (cocks.Length > 1 ? "One of [your] cocks" : "[Your] cock") + " has shrunk by " + Descriptions.Length(decrease) + ".";
					}
					runningDesc += result + "\n";
				}
				character.UpdateTitle();
			}
			#endregion

			if (boardchar != null)
				boardchar.AdjustView();
			if (!string.IsNullOrEmpty(runningDesc))
			{
				if (!this.HasToken("multiuse") && !this.HasToken("wearable"))
				{
					var toRemove = character.GetToken("items").GetToken(this.ID);
					character.GetToken("items").Tokens.Remove(toRemove);
				}
			}
			else
				runningDesc = "It had no effect...";

			MessageBox.Message(runningDesc.Viewpoint(boardchar));
		}
	}

	public class Token : TokenCarrier
	{
		public string Name { get; set; }
		public float Value { get; set; }
		public string Text { get; set; }

		public override string ToString()
		{
			return string.Format("{0} ({1}, {2})", Name, Value, Tokens.Count);
		}

		public static List<Token> Tokenize(XmlElement e)
		{
			var t = new List<Token>();
			foreach (var x in e.OfType<XmlElement>())
			{
				var nV = 0f;
				string nT = null;
				if (x.InnerText.Trim() != "")
				{
					if (x.HasAttribute("text"))
					{
						nT = x.InnerText.Trim();
					}
					else
					{
						float v;
						var it = x.ChildNodes.OfType<XmlText>().Count() > 0 ? x.ChildNodes.OfType<XmlText>().First().InnerText : "0.0";
						if (float.TryParse(it, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
							nV = v;
					}
				}
				var newToken = new Token() { Name = x.Name, Value = nV, Text = nT };
				if (x.ChildNodes.OfType<XmlElement>().Count() > 0)
					newToken.Tokens = Tokenize(x);
				t.Add(newToken);
			}
			return t;
		}

		public static List<Token> Tokenize(string a)
		{
			var t = new List<Token>();
			var lines = a.Split('\n');
			var nodes = new List<Token>();
			var prevTabs = 0;
			foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("--")))
			{
				var l = line.TrimEnd();
				//count number of tabs in front
				var tabs = 0;
				for (; tabs < l.Length - 1; tabs++)
					if (l[tabs] != '\t')
						break;
				l = l.TrimStart();
				var newOne = new Token();
				var tokenName = l;
				if (tokenName.StartsWith("oneof "))
				{
					var options = l.Substring(l.IndexOf(' ') + 1).Split(',');
					var choice = options[Toolkit.Rand.Next(options.Length)].Trim();
					tokenName = choice;
				}
				else if (l.Contains(": "))
				{
					//Token has a value
					if (l.Contains(": \""))
					{
						var text = l.Substring(l.IndexOf('\"') + 1);
						newOne.Text = text.Remove(text.LastIndexOf('\"'));
					}
					else if (l.Contains(": oneof "))
					{
						var options = l.Substring(l.IndexOf("of ") + 3).Split(',');
						var choice = options[Toolkit.Rand.Next(options.Length)].Trim();
						newOne.Text = choice;
					}
					else if (l.Contains(": roll "))
					{
						var xDyPz = l.Substring(l.LastIndexOf(' ') + 1);
						int y = 0, z = 0;
						var m = Regex.Match(xDyPz, @"1d(\d+)\+(\d+)");
						if (!m.Success)
						{
							m = Regex.Match(xDyPz, @"1d(\d+)");
							if (!m.Success)
								throw new Exception(string.Format("Roll() can't parse \"{0}\".", xDyPz));
						}
						y = int.Parse(m.Groups[1].Value);
						if (m.Groups.Count == 3)
							z = int.Parse(m.Groups[2].Value);
						var roll = Toolkit.Rand.Next(y) + z;
						newOne.Value = roll;
					}
					else
					{
						float v;
						var value = l.Substring(l.IndexOf(' '));
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
							newOne.Value = v;
						else
							newOne.Text = value;
					}
					tokenName = tokenName.Remove(tokenName.IndexOf(':'));
#if DEBUG
					if (tokenName.Contains(' '))
						throw new Exception(string.Format("Found a token \"{0}\", probably a typo.", tokenName));
#endif
				}
				newOne.Name = tokenName;

				if (tabs == 0)
				{
					//New one here
					t.Add(newOne);
					nodes.Clear();
					nodes.Add(newOne);
				}
				else if (tabs == prevTabs + 1)
				{
					var hook = nodes[prevTabs];
					hook.Tokens.Add(newOne);
					nodes.Add(newOne);
				}
				else if (tabs < prevTabs)
				{
					var hook = nodes[tabs - 1];
					hook.Tokens.Add(newOne);
					nodes.RemoveRange(tabs, nodes.Count - tabs);
					nodes.Add(newOne);
				}
				else if (tabs == prevTabs)
				{
					var hook = nodes[tabs - 1];
					hook.Tokens.Add(newOne);
					nodes[tabs] = newOne;
				}
				else
				{
					throw new Exception("Skipping a branch.");
				}
				prevTabs = tabs;
			}
			return t;
		}

		public Token()
		{
			Tokens = new List<Token>();
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Name ?? "Blank");
			stream.Write((Single)Value);
			stream.Write(Text ?? "");
			stream.Write(Tokens.Count);
			Tokens.ForEach(x => x.SaveToFile(stream));
		}

		public static Token LoadFromFile(BinaryReader stream)
		{
			var newToken = new Token();
			newToken.Name = stream.ReadString();
			newToken.Value = (float)stream.ReadSingle();
			newToken.Text = stream.ReadString();
			var numTokens = stream.ReadInt32();
			for (var i = 0; i < numTokens; i++)
				newToken.Tokens.Add(Token.LoadFromFile(stream));
			return newToken;
		}

		public bool IsMatch(List<Token> otherSet)
		{
			foreach (var toFind in this.Tokens)
			{
				var f = otherSet.Find(x => x.Name == toFind.Name);
				if (f == null)
					return false;
				if (toFind.Tokens.Count > 0)
				{
					var contentMatch = toFind.IsMatch(f.Tokens);
					if (!contentMatch)
						return false;
				}
			}
			return true;
		}

		public void RemoveSet(List<Token> otherSet)
		{
			//throw new NotImplementedException();
			foreach (var t in otherSet)
				this.Tokens.Remove(t);
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
	}

	public class ItemException : Exception
	{
		public ItemException(string message)
			: base(message)
		{
		}
	}
}
