using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Noxico
{
	public static class Toolkit
	{
		public static TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
		private static XmlDocument colorTable;

		public static Random Rand { get; private set; }

		static Toolkit()
		{
			Rand = new Random();
		}

		/// <summary>
		/// Returns the amount of change between two strings.
		/// </summary>
		public static int Levenshtein(string s, string t)
		{
			var n = s.Length;
			var m = t.Length;
			var d = new int[n + 1, m + 1];
			var cost = 0;

			if (n == 0)
				return m;
			if (m == 0)
				return n;

			for (int i = 0; i <= n; d[i, 0] = i++) ;
			for (int j = 0; j <= m; d[0, j] = j++) ;

			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
					d[i, j] = System.Math.Min(System.Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
					d[i - 1, j - 1] + cost);
				}
			}

			return d[n, m];
		}

		/// <summary>
		/// Creates an encoded textual description of a character's body to use in Levenshtein comparisons.
		/// </summary>
		public static string GetLevenshteinString(TokenCarrier token)
		{
			var ret = new StringBuilder();
			if (token.Path("hair") != null)
				ret.Append('h');
			else
				ret.Append(' ');

			if (token.Path("skin") == null)
				ret.Append('s');
			else
			{
				var skinTypeToken = token.Path("skin/type");
				if (skinTypeToken == null)
					ret.Append('s');
				else
				{
					var skinTypes = new Dictionary<string, char>()
					{
						{ "skin", 's' },
						{ "fur", 'f' },
						{ "scales", 'c' },
						{ "slime", 'j' },
						{ "rubber", 'r' },
						{ "metal", 'm' },
					};
					if (skinTypes.ContainsKey(skinTypeToken.Text))
						ret.Append(skinTypes[skinTypeToken.Text]);
					else
						ret.Append('s');
				}
			}

			if (token.Path("face") == null)
				ret.Append(' ');
			else
			{
				var faceToken = token.Path("face");
				if (faceToken == null)
					ret.Append(' ');
				else
				{
					var faceTypes = new Dictionary<string, char>()
					{
						{ "normal", ' ' },
						{ "genbeast", 'b' },
						{ "horse", 'h' },
						{ "dog", 'd' },
						{ "cow", 'm' },
						{ "cat", 'c' },
						{ "reptile", 'r' },
					};
					if (faceTypes.ContainsKey(faceToken.Text))
						ret.Append(faceTypes[faceToken.Text]);
					else
						ret.Append(' ');
				}
			}

			if (token.Path("ears") == null)
				ret.Append(' ');
			else
			{
				var earsToken = token.Path("face");
				if (earsToken == null)
					ret.Append(' ');
				else
				{
					var earTypes = new Dictionary<string, char>()
					{
						{ "human", ' ' },
						{ "elfin", 'e' },
						{ "genbeast", 'b' },
						{ "horse", 'h' },
						{ "dog", 'd' },
						{ "cat", 'c' },
						{ "cow", 'm' },
						{ "frill", 'f' },
						{ "bear", 'u' },
					};
					if (earTypes.ContainsKey(earsToken.Text))
						ret.Append(earTypes[earsToken.Text]);
					else
						ret.Append(' ');
				}
			}

			if (token.Path("antennae") == null)
				ret.Append(' ');
			else
				ret.Append('!');

			if (token.Path("snaketail") != null)
				ret.Append('S');
			else if (token.Path("tail") == null)
				ret.Append(' ');
			else
			{
				var tailToken = token.Path("tail");
				if (tailToken == null)
					ret.Append(' ');
				else
				{
					var tailTypes = new Dictionary<string, char>()
					{
						{ "genbeast", 'b' },
						{ "horse", 'h' },
						{ "dog", 't' },
						{ "fox", 'T' },
						{ "squirrel", 'T' },
						{ "cow", 'c' },
						{ "tentacle", '!' },
						{ "stinger", 'v' },
						{ "spider", 'S' },
					};
					if (tailTypes.ContainsKey(tailToken.Text))
						ret.Append(tailTypes[tailToken.Text]);
					else
						ret.Append('t');
				}
			}

			return ret.ToString();
		}

		/// <summary>
		/// Grabs the content for a token from a raw textual token tree, for analysis outside of character creation.
		/// </summary>
		public static string GrabToken(string input, string token)
		{
			var start = input.IndexOf(token);
			if (start == 0)
				return null;
			start += token.Length + 1;
			if (input[start] != '\t')
				return null;
			for (var i = start; i < input.Length; i++)
			{
				if (input[i] == '\n' && input[i + 1] != '\t')
				{
					var ret = input.Substring(start, i - start);
					return ret + "\n<end>";
				}
			}
			return null;
		}

		/// <summary>
		/// Picks a single item from a string array, at random.
		/// </summary>
		public static string PickOne(params string[] options)
		{
			return options[Toolkit.Rand.Next(options.Length)];
		}

		/// <summary>
		/// Returns the given number as a word in English, from "one" up to "twelve". 13 and higher are returned as-is.
		/// </summary>
		public static string Count(this float num)
		{
			var words = new[] { "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve" };
			var i = (int)Math.Floor(num);
			if (i < words.Length)
				return words[i];
			return i.ToString();
		}

		/// <summary>
		/// Returns the proper name for a color -- "darkslategray", "dark_slate_gray", or "DarkSlateGray" becomes "dark slate gray".
		/// </summary>
		public static string NameColor(string color)
		{
			var req = color.Trim().ToLower().Replace("_", "").Replace(" ", "");
			var colorName = "";
			if (colorTable == null)
			{
				colorTable = Mix.GetXMLDocument("knowncolors.xml");
				//colorTable = new XmlDocument();
				//colorTable.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.KnownColors, "knowncolors.xml"));
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

		/// <summary>
		/// Returns a Color by name -- "DarkSlateGray" returns a Color with RGB values { 47, 79, 79 }.
		/// </summary>
		public static Color GetColor(string color)
		{
			if (string.IsNullOrEmpty(color))
				return Color.Silver;
			if (colorTable == null)
			{
				colorTable = Mix.GetXMLDocument("knowncolors.xml");
				//colorTable = new XmlDocument();
				//colorTable.LoadXml(Toolkit.ResOrFile(global::Noxico.Properties.Resources.KnownColors, "knowncolors.xml"));
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
		/// <summary>
		/// Returns a Color by name -- "DarkSlateGray" returns a Color with RGB values { 47, 79, 79 }.
		/// </summary>
		public static Color GetColor(Token color)
		{
			if (color == null)
				return Color.Silver;
			return GetColor(color.Name);
		}

		/// <summary>
		/// Darkens a color in some stupid way.
		/// </summary>
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

		/// <summary>
		/// Applies [grammar replacement] from a given character's point of view.
		/// </summary>
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

		public static string Disemvowel(this string text)
		{
			var vowels = "AEIOUaeiou".ToCharArray();
			var ret = new StringBuilder();
			foreach (var c in text)
			{
				if (vowels.Contains(c))
					continue;
				ret.Append(c);
			}
			return ret.ToString().Trim();
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

		public static string SmartQuote(this string text)
		{
			var ret = new StringBuilder();
			var open = false;
			foreach (var ch in text)
			{
				if (ch == '\"')
				{
					ret.Append(open ? '\u201D' : '\u201C');
					open = !open;
				}
				else
					ret.Append(ch);
			}
			return ret.ToString();
		}

		public static void DrawWindow(int left, int top, int width, int height, string title, Color fgColor, Color bgColor, Color titleColor, bool single = false)
		{
			var host = NoxicoGame.HostForm;
			host.SetCell(top, left, (char)(single ? 0x250C : 0x2554), fgColor, bgColor);
			host.SetCell(top, left + width, (char)(single ? 0x2510 : 0x2557), fgColor, bgColor);
			host.SetCell(top + height, left, (char)(single ? 0x2514 : 0x255A), fgColor, bgColor);
			host.SetCell(top + height, left + width, (char)(single ? 0x2518 : 0x255D), fgColor, bgColor);
			for (int i = left + 1; i < left + width; i++)
			{
				host.SetCell(top, i, (char)(single ? 0x2500 : 0x2550), fgColor, bgColor);
				host.SetCell(top + height, i, (char)(single ? 0x2500 : 0x2550), fgColor, bgColor);
				for (int j = top + 1; j < top + height; j++)
					host.SetCell(j, i, ' ', fgColor, bgColor);
			}
			for (int i = top + 1; i < top + height; i++)
			{
				host.SetCell(i, left, (char)(single ? 0x2502 : 0x2551), fgColor, bgColor);
				host.SetCell(i, left + width, (char)(single ? 0x2502 : 0x2551), fgColor, bgColor);
			}
			if (!string.IsNullOrWhiteSpace(title))
			{
				var captionWidth = title.Length;
				var captionPos = left + (int)Math.Ceiling(width / 2.0) - (int)Math.Ceiling(captionWidth / 2.0) - 1;
				host.Write("<g" + (single ? "2524" : "2561") + "><c" + titleColor.Name + "> " + title + " <c" + fgColor.Name + "><g" + (single ? "251C" : "255E") + ">", fgColor, bgColor, captionPos - 1, top);
				//host.SetCell(top, captionPos - 2, (char)(single ? 0xB4 : 0xB5), fgColor, bgColor);
				//host.SetCell(top, captionPos + captionWidth, (char)(single ? 0xC3 : 0xC6), fgColor, bgColor);
			}

			//for (var i = top + 1; i <= top + height; i++)
			//	NoxicoGame.HostForm.DarkenCell(i, left + width + 1);
			//for (var i = left + 1; i <= left + width + 1; i++)
			//	NoxicoGame.HostForm.DarkenCell(top + height + 1, i);
		}

		/// <summary>
		/// Use in a ForEach loop.
		/// </summary>
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

		/// <summary>
		/// Converts a NoxML string to HTML. Badly.
		/// </summary>
		public static string HTMLize(string text)
		{
			var html = new StringBuilder();
			var lines = text.Split('\n');
			var glyph = @"\<g([0-9a-fA-F]{4})\>";
			var color = @"<c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)>";
			html.Append("<pre>");
			foreach (var line in lines)
			{
				var s = line;
				if (s.Equals(lines[0]))
					s = "<h3>" + s + "</h3>";
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
				while (Regex.IsMatch(s, glyph))
				{
					s = Regex.Replace(s, glyph, @"&#x$1;");
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

		/// <summary>
		/// Converts a fragment of XML to NoxML, specifically the B, BR, and P elements.
		/// </summary>
		/// <param name="element"></param>
		/// <returns></returns>
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

		/// <summary>
		/// Stolen from XNA. Linearly interpolates between two colors.
		/// </summary>
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

		/// <summary>
		/// Darkens a color in a more sane manner.
		/// </summary>
		public static Color LerpDarken(this Color color, double amount)
		{
			return Lerp(color, Color.Black, amount);
		}

		public static int Distance(int fromX, int fromY, int toX, int toY)
		{
			var dX = Math.Abs(fromX - toX);
			var dY = Math.Abs(fromY - toY);
			return (dX < dY) ? dY : dX;
		}

		/// <summary>
		/// Returns a string from the project's resources, but from a specific file if it exists otherwise.
		/// </summary>
		[Obsolete("Use the Mix system.", true)]
		public static string ResOrFile(string resource, string filename)
		{
			if (File.Exists(filename))
				return File.ReadAllText(filename);
			else
				return resource;
		}

		/// <summary>
		/// Returns a Bitmap from the project's resources, but from a specific file if it exists otherwise.
		/// </summary>
		[Obsolete("Use the Mix system.", true)]
		public static Bitmap ResOrFile(Bitmap resource, string filename)
		{
			if (File.Exists(filename))
				return (Bitmap)Bitmap.FromFile(filename);
			else
				return resource;
		}

		/// <summary>
		/// From Nethack. True if it's Friday the 13th.
		/// </summary>
		public static bool IsFriday13()
		{
			return (DateTime.Now.DayOfWeek == DayOfWeek.Friday && DateTime.Now.Day == 13);
		}

		/// <summary>
		/// Returns the phase of the moon. Taken straight from Nethack's hacklib.c.
		/// </summary>
		/// <returns>An integer from 0-7, where 0 is new moon and 4 is full moon.</returns>
		public static int MoonPhase()
		{
			/*
			 * moon period = 29.53058 days ~= 30, year = 365.2422 days
			 * days moon phase advances on first day of year compared to preceding year
			 *	= 365.2422 - 12*29.53058 ~= 11
			 * years in Metonic cycle (time until same phases fall on the same days of
			 *	the month) = 18.6 ~= 19
			 * moon phase on first day of year (epact) ~= (11*(year%19) + 29) % 30
			 *	(29 as initial condition)
			 * current phase in days = first day phase + days elapsed in year
			 * 6 moons ~= 177 days
			 * 177 ~= 8 reported phases * 22
			 * + 11/22 for rounding
			 */
			var diy = DateTime.Now.DayOfYear;
			var goldn = (DateTime.Now.Year % 19) + 1;
			var epact = (11 * goldn + 18) % 30;
			if ((epact == 25 && goldn > 11) || epact == 24)
				epact++;
			return ((((((diy + epact) * 6) + 11) % 177) / 22) & 7);
		}

		public static bool IsNight()
		{
			return NoxicoGame.InGameTime.Hour < 6 || NoxicoGame.InGameTime.Hour > 21;
		}

		/// <summary>
		/// From Nethack. Returns the ordinal suffix for the given number -- insert 4, get "th" as in "4th".
		/// </summary>
		public static string Ordinal(this int number)
		{
			var i = number;
			var dd = i % 10;
			return (dd == 0 || dd > 3 || (i % 100) / 10 == 1) ? "th" : (dd == 1) ? "st" : (dd == 2) ? "nd" : "rd";
		}
		public static string Ordinal(this float number)
		{
			return ((int)Math.Floor(number)).Ordinal();
		}

		/// <summary>
		///	From Nethack. Returns the possessive suffix -- "Kawa" > "Kawa's", "Chris" > "Chris'", etc...
		/// </summary>
		public static string Possessive(this string subject)
		{
			if (!subject.Equals("it", StringComparison.InvariantCultureIgnoreCase))
				return subject + "s";
			else if (subject.EndsWith("s"))
				return subject + "'";
			else
				return subject + "'s";
		}

		public static void FoldCostumeRandoms(Token token)
		{
			if (token == null)
				return;
			while (token.HasToken("random"))
			{
				var rnd = token.GetToken("random");
				var pick = rnd.Tokens[Toolkit.Rand.Next(rnd.Tokens.Count)];
				token.Tokens.Remove(rnd);
				foreach (var t in pick.Tokens)
					token.Tokens.Add(t);
				//rnd = pick;
			}
		}

		public static void FoldCostumeVariables(Token token, string[] vars = null)
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
				FoldCostumeVariables(child, vars);
		}

		public static string TranslateKey(KeyBinding binding)
		{
			return TranslateKey((System.Windows.Forms.Keys)NoxicoGame.KeyBindings[binding]);
		}
		public static string TranslateKey(System.Windows.Forms.Keys key)
		{
			var keyName = key.ToString();
			var specials = new Dictionary<string, string>()
			{
				{ "Left", "\u2190" },
				{ "Up", "\u2191" },
				{ "Right", "\u2192" },
				{ "Down", "\u2193" },
				{ "Return", "\u21B2" },
				{ "OemQuestion", "/" },
				{ "Oemcomma", "," },
				{ "Escape", "Esc." },
			};
			if (specials.ContainsKey(keyName))
				return specials[keyName];
			if (keyName.StartsWith("Oem"))
				return keyName.Substring(3);
			return keyName;
		}
	}
}

