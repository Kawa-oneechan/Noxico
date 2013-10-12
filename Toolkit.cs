using System;
using System.Collections.Generic;
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
		private static List<Tuple<Regex, int>> hyphenationRules;

		/// <summary>
		/// Returns the amount of change between two strings according to the Levenshtein method.
		/// </summary>
		public static int GetLevenshteinDistance(string s, string t)
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
		/// Returns the amount of change between two strings according to the Hamming method.
		/// </summary>
		public static int GetHammingDistance(string s, string t)
		{
			if (s.Length != t.Length)
				throw new ArgumentException("Subject strings in a Hamming distance calculation should be of equal length.");
			return s.Zip(t, (c1, c2) => c1 == c2 ? 0 : 1).Sum();
		}

		/// <summary>
		/// Creates an encoded textual description of a character's body to use in comparisons.
		/// </summary>
		public static string GetBodyComparisonHash(TokenCarrier token)
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
						{ "carapace", 'C' },
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
				var earsToken = token.Path("ears");
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
						{ "spider", 'A' },
					};
					if (tailTypes.ContainsKey(tailToken.Text))
						ret.Append(tailTypes[tailToken.Text]);
					else
						ret.Append('t');
				}
			}

			if (token.Path("wings") == null)
				ret.Append(' ');
			else
			{
				var wingsToken = token.Path("wings");
				if (wingsToken == null)
					ret.Append(' ');
				else
				{
					var wingTypes = new Dictionary<string, char>()
					{
						{ "bat", 'b' },
						{ "dragon", 'd' },
						{ "feather", 'f' },
					};
					if (wingTypes.ContainsKey(wingsToken.Text))
						ret.Append(wingsToken.HasToken("small") ? wingTypes[wingsToken.Text] : wingTypes[wingsToken.Text].ToString().ToUpperInvariant()[0]);
					else
						ret.Append(' ');
				}
			}

			var tallness = token.Path("tallness");
			if (tallness == null)
				ret.Append(' ');
			else if (tallness.Value < 140)
				ret.Append('_');
			else if (tallness.Value > 180)
				ret.Append('!');
			else
				ret.Append(' ');

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
			return options[Random.Next(options.Length)];
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
			var words = new List<string>();
			var lines = new List<string>();

			text = text.Normalize();

			var currentWord = new StringBuilder();
			foreach (var ch in text)
			{
				var breakIt = false;
				if (char.IsWhiteSpace(ch) && ch != '\u00A0')
					breakIt = true;
				else if (char.IsPunctuation(ch) && !(ch == '(' || ch == ')'))
					breakIt = true;

				currentWord.Append(ch);
				if (breakIt)
				{
					words.Add(currentWord.ToString());
					currentWord.Clear();
				}
			}
			if (currentWord.ToString() != "")
				words.Add(currentWord.ToString());

			if (hyphenationRules == null)
			{
				var wordsXml = Mix.GetXmlDocument("words.xml");
				var ruleNodes = wordsXml.SelectNodes("//hyphenation/rule").OfType<XmlElement>();
				hyphenationRules = new List<Tuple<Regex, int>>();
				foreach (var rule in ruleNodes)
				{
					var newTuple = Tuple.Create(new Regex(rule.InnerText.Trim()), int.Parse(rule.GetAttribute("cutoff")));
					hyphenationRules.Add(newTuple);
				}
			}

			//TODO: make these rules part of words.xml?
			for (var i = 0; i < words.Count; i++)
			{
				var word = words[i];
				if (word.Length < 5)
					continue;
				if (word.IndexOf('\u00AD') > 0 || i > 1 && words[i - 1].IndexOf('\u00AD') > 0)
					continue;

				foreach (var rule in hyphenationRules)
				{
					if (rule.Item2 == -1 && rule.Item1.IsMatch(word))
						break;
					while (rule.Item1.IsMatch(word))
					{
						var match = rule.Item1.Match(word);
						var replacement = '\u00AD';
						if (match.ToString().Contains(' ') || match.ToString().Contains('\u00AD'))
							replacement = '\uFFFE'; //prevent this match from retriggering
						word = word.Substring(0, match.Index + rule.Item2) + replacement + word.Substring(match.Index + rule.Item2);
					}
				}
				word = word.Replace("\uFFFE", ""); //cleanup in aisle -2!
				while (word.IndexOf('\u00AD') > 0 && word.IndexOf('\u00AD') < word.Length - 1)
				{
					var natch = word.Substring(0, word.IndexOf('\u00AD') + 1);
					words.Insert(i, natch);
					i++;
					word = word.Substring(word.IndexOf('\u00AD') + 1);
				}
				words[i] = word;
			}

			var line = new StringBuilder();
			var spaceLeft = length;
			for (var i = 0; i < words.Count; i++)
			{
				var word = words[i];
				var next = (i < words.Count - 1) ? words[i + 1] : null;

				//Check for words longer than length? Should not happen with autohyphenator.

				if (word == "\n")
				{
					lines.Add(line.ToString().Trim());
					line.Clear();
					spaceLeft = length;
					continue;
				}
				else if (word == "\u2029")
				{
					lines.Add(line.ToString().Trim());
					lines.Add(string.Empty);
					line.Clear();
					spaceLeft = length;
					continue;
				}

				if (word[word.Length - 1] == '\u00AD')
				{
					if (next != null && spaceLeft - (word.Length - 1) - next.TrimEnd().Length <= 0)
						word = word.Remove(word.Length - 1) + '\u2010';
					else
						word = word.Remove(word.Length - 1);
				}

				line.Append(word);
				spaceLeft -= word.Length;
				if (next != null && spaceLeft - next.TrimEnd().Length <= 0)
				{
					if (!string.IsNullOrWhiteSpace(line.ToString().Trim()))
						lines.Add(line.ToString().Trim());
					line.Clear();
					spaceLeft = length;
				}
			}
			if (!string.IsNullOrWhiteSpace(line.ToString().Trim()))
				lines.Add(line.ToString());

			return string.Join("\n", lines.ToArray()) + '\n';
		}

		public static string SmartQuote(this string text, Func<string, string> filter = null)
		{
			var ret = new StringBuilder();
			var open = false;
			var quoted = new StringBuilder();
			foreach (var ch in text)
			{
				if (ch == '\"')
				{
					//ret.Append(open ? '\u201D' : '\u201C');
					if (!open)
					{
						quoted.Clear();
						open = true;
						ret.Append('\u201C');
					}
					else
					{
						var q = quoted.ToString();
						if (q.StartsWith("<nofilter>"))
							ret.Append(q.Substring(10));
						else if (filter == null)
							ret.Append(q);
						else
							ret.Append(filter(q));
						quoted.Clear();
						open = false;
						ret.Append('\u201D');
					}
				}
				else
				{
					if (open)
						quoted.Append(ch);
					else
						ret.Append(ch);
				}
			}
			return ret.ToString();
		}

		/// <summary>
		/// Use in a ForEach loop.
		/// </summary>
		public static IEnumerable<Point> Line(int x0, int y0, int x1, int y1, bool connectedDiagonals = false)
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
			int lastX = x0, lastY = y0;
			for (int x = x0; x <= x1; x++)
			{
				if (connectedDiagonals)
					yield return new Point((steep ? lastY : x), (steep ? x : lastY));
				lastX = x;
				lastY = y;
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
		public static string ToHtml(this string text)
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
						var col = Color.FromName(match.Groups["fore"].ToString());
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
		public static string ToNoxML(this XmlElement element)
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
						r += " <cWhite>";

					if (e.Name == "br")
						r += "\n";

					r += e.ToNoxML();

					if (e.Name == "b")
						r += "<c>";

					if (e.Name == "p")
						r += "\n\n";
				}
			}
			return r;
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
		/// Darkens a color to produce a nighttime palette.
		/// </summary>
		public static Color Night(this Color color)
		{
			return Color.FromArgb(color.R / 3, color.G / 4, color.B / 2);
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
			if (!subject.Equals("it", StringComparison.OrdinalIgnoreCase))
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
				var pick = rnd.Tokens[Random.Next(rnd.Tokens.Count)];
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

		public static string TranslateKey(KeyBinding binding, bool longhand = false, bool withColors = true)
		{
			if (Vista.GamepadAvailable)
			{
				if (binding == KeyBinding.Interact || binding == KeyBinding.Accept)
					return withColors ? "<cGreen>A<c>" : "A";
				if (binding == KeyBinding.Activate || binding == KeyBinding.Back)
					return withColors ? "<cRed>B<c>" : "B";
				if (binding == KeyBinding.Items)
					return withColors ? "<cBlue>X<c>" : "X";
				if (binding == KeyBinding.Fly)
					return withColors ? "<cYellow>Y<c>" : "Y";
				if (binding == KeyBinding.Rest)
					return withColors ? "<cSilver>\u2310<c>" : "Left";
				if (binding == KeyBinding.Travel || binding == KeyBinding.TabFocus)
					return withColors ? "<cSilver>\u00AC<c>" : "Right";
				if (binding == KeyBinding.Pause)
					return "Start";
			}
			return TranslateKey((System.Windows.Forms.Keys)NoxicoGame.KeyBindings[binding], longhand);
		}
		public static string TranslateKey(System.Windows.Forms.Keys key, bool longhand = false)
		{
			//BUG: OemSemicolon and OemQuotes are mistranslated as 1 and 7.
			var keyName = key.ToString();
			var specials = new Dictionary<string, string>()
			{
				{ "Left", "\u2190" },
				{ "Up", "\u2191" },
				{ "Right", "\u2192" },
				{ "Down", "\u2193" },
				{ "Return", "\u21B2" },
				{ "OemQuestion", "/" },
				{ "OemPeriod", "." },
				{ "Oemcomma", "," },
				{ "OemQuotes", "'" },
				{ "OemSemicolon", ";" },
				{ "Escape", "Esc." },
			};
			if (longhand)
			{
				specials = new Dictionary<string, string>()
				{
					{ "Return", "Enter" },
					{ "OemQuestion", "/" },
					{ "Oemcomma", "," },
					{ "Escape", "Escape" },
				};
			}
			if (specials.ContainsKey(keyName))
				return specials[keyName];
			if (keyName.StartsWith("Oem"))
				return keyName.Substring(3);
			return keyName;
		}

		public static string InitialCase(this string text)
		{
			var initial = text[0];
			if (char.IsLower(initial))
				return initial.ToString().ToUpperInvariant() + text.Substring(1);
			return text;
		}

		public static void SaveExpectation(BinaryWriter stream, string expectation)
		{
			stream.Write(expectation.ToCharArray());
		}
		public static void ExpectFromFile(BinaryReader stream, string expected, string friendly)
		{
			var found = stream.ReadChars(expected.Length);
			if ((new string(found)) != expected)
				throw new Exception("Expected to find " + friendly + " data.");
		}

		public static string ToID(this string name)
		{
			return Regex.Replace(name.ToLower(), "(^[A-Z])", "");
		}

		public static Direction Opposite(Direction current)
		{
			if (current == Direction.North)
				return Direction.South;
			else if (current == Direction.East)
				return Direction.West;
			else if (current == Direction.South)
				return Direction.North;
			else if (current == Direction.West)
				return Direction.East;
			return Direction.North;
		}

		#region PillowShout's additions
/// <summary>
        /// Checks the passed body plan to ensure that it contains all required components and throws an exception if a part is missing.
        /// Will not work with 'beast' bodyplans.
        /// </summary>
        /// <param name="bodyPlan">The xml bodyplan to be evaluated.</param>
        public static void VerifyBodyplan(XmlElement bodyPlan)
        {
			var plan = new Token();
			var name = bodyPlan.GetAttribute("id");
			plan.Tokenize(bodyPlan.ChildNodes[0].Value);
			VerifyBodyplan(plan, name);
		}

		public static void VerifyBodyplan(TokenCarrier bodyPlan, string name)
		{
			var plan = bodyPlan;
			var missing = new List<string>();

			//Rewrite by Kawa to allow ONE exception to list ALL missing tokens. It certainly reduced the amount of restarts when fixing all these missing tokens...
			if (plan.HasToken("beast"))
				return;

			foreach (var t in new[] { "culture", "namegen", "terms", "tallness", "hair", "skin", "eyes", "ears", "face", "teeth", "tongue" })
				if (!plan.HasToken(t))
					missing.Add(t);

			if (!(plan.HasToken("legs") || plan.HasToken("snaketail") || plan.HasToken("slimeblob")))
				missing.Add("legs, snaketail, or slimeblob");

			if (plan.HasToken("legs") & !(plan.HasToken("quadruped") || plan.HasToken("taur")))
			{
				if (!plan.HasToken("hips"))
					missing.Add("hips");
				if (!plan.HasToken("waist"))
					missing.Add("waist");
			}

			if (plan.HasToken("ass"))
			{
				foreach (var t in new[] { "ass/size", "ass/looseness" })
					if (plan.Path(t) == null)
						missing.Add(t);
			}
			else
				missing.Add("ass");

			if (!(plan.HasToken("maleonly") || plan.HasToken("neuteronly")))
			{
				if (!plan.HasToken("fertility"))
					missing.Add("fertility");
				
				if (plan.HasToken("vagina"))
				{
					var vaginas = plan.Tokens.FindAll(x => x.Name == "vagina");
					foreach (var v in vaginas)
					{
						foreach (var t in new[] { "clit", "looseness", "wetness" })
							if (!v.HasToken(t))
								missing.Add("vagina/" + t);
					}
				}
				else
					missing.Add("vagina");

				if (plan.HasToken("breastrow"))
				{
					var breastrows = plan.Tokens.FindAll(x => x.Name == "breastrow");
					foreach (var b in breastrows)
					{
						foreach (var t in new[] { "amount", "size", "nipples" })
							if (!b.HasToken(t))
								missing.Add("breastrow/" + t);
					}
				}
				else if (!plan.HasToken("quadruped"))
				{
					//Only consider missing breastrows a problem if the character is not a quadruped.
					missing.Add("breastrow");
				}
			}

			if (!(plan.HasToken("femaleonly") || plan.HasToken("neuteronly")))
			{
				if (plan.HasToken("penis"))
				{
					var penises = plan.Tokens.FindAll(x => x.Name == "penis");
					foreach (var p in penises)
					{
						foreach (var t in new[] { "thickness", "length" /* , "canfuck", "cumsource" */ })
							if (!p.HasToken(t))
								missing.Add("penis/" + t);
					}
				}
				else
					missing.Add("penis");

				if (plan.HasToken("balls"))
				{
					foreach (var t in new[] { "balls/size", "balls/amount" })
						if (plan.Path(t) == null)
							missing.Add(t);
				}
				else
					missing.Add("balls");
			}

			if (missing.Count > 0)
				throw new Exception("The \"" + name + "\" bodyplan is missing the following token(s):\r\n * " + string.Join("\r\n * ", missing));
        }
		#endregion

		#region Stolen from MSCorLib
		public static int Read7BitEncodedInt(this BinaryReader stream)
		{
			byte inputByte;
			int finalValue = 0, shifts = 0;
			do
			{
				if (shifts == 0x23)
					throw new FormatException("Bad 7-bit encoded integer.");
				inputByte = stream.ReadByte();
				finalValue |= (inputByte & 0x7f) << shifts;
				shifts += 7;
			}
			while ((inputByte & 0x80) != 0);
			return finalValue;
		}

		public static void Write7BitEncodedInt(this BinaryWriter stream, int value)
		{
			uint work = (uint)value;
			while (work >= 0x80)
			{
				stream.Write((byte)(work | 0x80));
				work = work >> 7;
			}
			stream.Write((byte)work);
		}
		#endregion

		/// <summary>
		/// Splits a string on spaces, but skips over doubled spaces and returns quoted strings as single items.
		/// </summary>
		/// <example>"a b \"c d\" e".Split() //returns { "a", "b", "c d", "e" }</example>
		/// <param name="input">The string to split.</param>
		/// <returns></returns>
		public static string[] SplitQ(this string input)
		{
			var ret = new List<string>();
			var item = new StringBuilder();
			for (var i = 0; i < input.Length; i++)
			{
				if (input[i] == '\"')
				{
					for (var j = i + 1; j < input.Length; j++)
					{
						if (input[j] == '\"')
						{
							i = j;
							break;
						}
						item.Append(input[j]);
					}
				}
				else if (input[i] == ' ')
				{
					if (item.Length > 0)
						ret.Add(item.ToString());
					item.Clear();
				}
				else
					item.Append(input[i]);
			}

			if (item.Length > 0)
				ret.Add(item.ToString());

			return ret.ToArray();
		}
	}
}

