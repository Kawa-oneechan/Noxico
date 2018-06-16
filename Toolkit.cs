using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Noxico
{
	public static class Toolkit
	{
		public static TextInfo ti = CultureInfo.InvariantCulture.TextInfo;
		private static List<Tuple<Regex, int>> hyphenationRules;

		/// <summary>
		/// Returns the amount of change between two strings according to the Levenshtein method.
		/// </summary>
		public static int GetLevenshteinDistance(this string s, string t)
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
		public static int GetHammingDistance(this string s, string t)
		{
			if (s.Length != t.Length)
				throw new ArgumentException("Subject strings in a Hamming distance calculation should be of equal length.");
			return s.Zip(t, (c1, c2) => c1 == c2 ? 0 : 1).Sum();
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
		/// Picks a single item from an array, at random.
		/// </summary>
		public static T PickOne<T>(this T[] options)
		{
			return options[Random.Next(options.Length)];
		}

		/// <summary>
		/// Picks a single item from a List, at random.
		/// </summary>
		public static T PickOne<T>(this List<T> options)
		{
			return options[Random.Next(options.Count)];
		}

		/// <summary>
		/// Picks a single item from a token list, weighted.
		/// </summary>
		public static Token PickWeighted(this List<Token> tokens)
		{
			if (tokens.Count == 0) return null;
			if (tokens.Count == 1) return tokens[0];
			var useWeight = false;
			var weights = new float[tokens.Count];
			for (var i = 0; i < tokens.Count; i++)
			{
				weights[i] = -1;
				if (tokens[i].HasToken("weight"))
				{
					weights[i] = tokens[i].GetToken("weight").Value;
					useWeight = true;
				}
			}
			if (!useWeight)
				return tokens.PickOne();
			var numWithWeigths = weights.Count(x => x > -1);
			var totalWithWeights = weights.Where(x => x > -1).Sum();
			var defaultWeight = (1.0f - totalWithWeights) / numWithWeigths;
			for (var i = 0; i < weights.Length; i++)
				if (weights[i] == -1)
					weights[i] = defaultWeight;
			var w = weights.Sum();
			var r = (float)(Random.NextDouble() * w);
			for (var i = 0; i < weights.Length; i++)
			{
				if (r < weights[i])
					return tokens[i];
				r -= weights[i];
			}
			//fuck it, go unweighted.
			return tokens.PickOne();
		}


		/// <summary>
		/// Returns the given number as a word, from "one" up to "twelve". 13 and higher are returned as-is.
		/// </summary>
		public static string Count(this float num)
		{
			var words = i18n.GetArray("counts");
			var i = (int)Math.Floor(num);
			if (i < words.Length)
				return words[i];
			return i.ToString();
		}
		/// <summary>
		/// Returns the given number as a word, from "first" up to "twelfth". 13 and higher are passed to Ordinal.
		/// </summary>
		public static string CountOrdinal(this float num)
		{
			var words = i18n.GetArray("countsordinal");
			var i = (int)Math.Floor(num);
			if (i < words.Length)
				return words[i];
			return i18n.Ordinal(num);
		}
		/// <summary>
		/// Returns the given number as a word, from "first" up to "twelfth". 13 and higher are passed to Ordinal.
		/// </summary>
		public static string CountOrdinal(this int num)
		{
			var words = i18n.GetArray("countsordinal");
			if (num < words.Length)
				return words[num];
			return i18n.Ordinal(num);
		}

		public static bool IsBlank(this string text)
		{
			return string.IsNullOrWhiteSpace(text);
		}

		public static string IsBlank(this string text, string ifItIs, string ifNot)
		{
			return string.IsNullOrWhiteSpace(text) ? ifItIs : ifNot;
		}

		public static string OrEmpty(this string text)
		{
			return (text == null) ? string.Empty : text;
		}

		public static string Join<T>(this T[] values)
		{
			return string.Join(", ", values);
		}
		public static string Join<T>(this IEnumerable<T> values)
		{
			return string.Join(", ", values);
		}

		public static bool StartsWith(this string text, char ch)
		{
			return (text.Length > 0 && text[0] == ch);
		}
		public static bool EndsWith(this string text, char ch)
		{
			return (text.Length > 0 && text[text.Length - 1] == ch);
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
			return ti.ToTitleCase(text.ToLowerInvariant());
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
			var words = new List<Word>();
			var lines = new List<string>();

			text = text.Normalize();

			#region Hyphenator
			if (hyphenationRules == null)
			{
				var rulesRoot = Mix.GetTokenTree("i18n.tml").First(t => t.Name == "hyphenation");
				hyphenationRules = new List<Tuple<Regex, int>>();
				foreach (var rule in rulesRoot.Tokens)
				{
					var newTuple = Tuple.Create(new Regex(rule.GetToken("pattern").Text), (int)rule.GetToken("cutoff").Value);
					hyphenationRules.Add(newTuple);
				}
			}

			var newText = new StringBuilder();
			foreach (var inWord in text.Split(' '))
			{
				if (inWord.Contains('\u00AD') || inWord.Length < 6)
				{
					newText.Append(inWord);
					newText.Append(' ');
					continue;
				}
				var word = inWord;
				foreach (var rule in hyphenationRules)
				{
					if (rule.Item2 == -1 && rule.Item1.IsMatch(word))
						break;
					while (rule.Item1.IsMatch(word))
					{
						var match = rule.Item1.Match(word);
						word = word.Substring(0, match.Index + rule.Item2) + '\u00AD' + word.Substring(match.Index + rule.Item2);
					}
				}
				newText.Append(word);
				newText.Append(' ');
			}
			text = newText.ToString();
			text = text.Replace("\u00AD\u00AD", "\u00AD");
			text = Regex.Replace(text, "<(?:[\\w^­]+)(\u00AD)(?:[\\w^­]+)>", (m => m.Captures[0].Value.Replace("\u00AD", string.Empty)));
			#endregion

			var currentWord = new StringBuilder();
			var breakIt = false;
			var spaceAfter = false;
			var mandatory = false;
			var softHyphen = false;
			var color = Color.Transparent;
			for (var i = 0; i < text.Length; i++)
			{
				var ch = text[i];
				var nextCh = (i < text.Length - 1) ? text[i + 1] : '\0';
				if (ch == '<' && nextCh == 'c')
				{
					if (text[i + 2] == '>')
					{
						color = Color.Transparent;
						i += 2;
						continue;
					}
					var colorToken = new StringBuilder();
					for (var j = i + 2; j < text.Length; j++)
					{
						if (text[j] == '>')
						{
							color = Color.FromName(colorToken.ToString());
							i = j;
							break;
						}
						colorToken.Append(text[j]);
					}
					continue;
				}

				if ((ch == '\r' && nextCh != '\n') || ch == '\n')
				{
					breakIt = true;
					mandatory = true;
				}
				else if (char.IsWhiteSpace(ch) && ch != '\u00A0')
				{
					breakIt = true;
					spaceAfter = true;
				}
				else if (ch == '\u00AD')
				{
					breakIt = true;
					softHyphen = true;
				}
				else if (char.IsPunctuation(ch) && !(ch == '(' || ch == ')' || ch == '\''))
				{
					currentWord.Append(ch);
					breakIt = true;
				}
				else
					currentWord.Append(ch);


				if (breakIt)
				{
					var newWord = new Word()
					{
						Content = currentWord.ToString().Trim(),
						SpaceAfter = spaceAfter,
						MandatoryBreak = mandatory,
						SoftHyphen = softHyphen,
						Color = color,
					};
					breakIt = false;
					spaceAfter = false;
					mandatory = false;
					softHyphen = false;
					words.Add(newWord);
					currentWord.Clear();
				}
			}
			if (currentWord.ToString() != string.Empty)
			{
				var newWord = new Word()
				{
					Content = currentWord.ToString().Trim(),
					SpaceAfter = currentWord.ToString().EndsWith(' '),
					MandatoryBreak = false,
					SoftHyphen = softHyphen,
					Color = color,
				};
				words.Add(newWord);
			}

			var line = new StringBuilder();
			var spaceLeft = length;
			color = Color.Transparent;
			for (var i = 0; i < words.Count; i++)
			{
				var word = words[i];
				var next = (i < words.Count - 1) ? words[i + 1] : null;

				//Check for words longer than length? Should not happen with autohyphenator.

				//Reinsert color change without changing line length.
				if (word.Color != color)
				{
					color = word.Color;
					line.AppendFormat("<c{0}>", color == Color.Transparent ? string.Empty : color.Name);
				}

				if (word.Content == "\u2029")
				{
					lines.Add(line.ToString().Trim());
					lines.Add(string.Empty);
					line.Clear();
					spaceLeft = length;
					continue;
				}

				if (word.SoftHyphen)
				{
					if (next != null && spaceLeft - word.Length - next.Content.TrimEnd().Length <= 0)
					{
						word.Content += '-';
						word.SoftHyphen = false;
					}
				}

				line.Append(word.Content);
				spaceLeft -= word.Length;
				if (next != null && spaceLeft - next.Content.TrimEnd().Length <= 0)
				{
					if (!line.ToString().Trim().IsBlank())
						lines.Add(line.ToString().Trim());
					line.Clear();
					spaceLeft = length;
				}
				else
				{
					if (word.SpaceAfter)
					{
						line.Append(' ');
						spaceLeft--;
					}
				}

				if (word.MandatoryBreak)
				{
					lines.Add(line.ToString().Trim());
					line.Clear();
					spaceLeft = length;
					continue;
				}

			}
			if (!line.ToString().Trim().IsBlank())
				lines.Add(line.ToString());

			return string.Join("\n", lines.ToArray()) + '\n';
		}

		public static string SmartQuote(this string text, SpeechFilter filter = null)
		{
			var ret = new StringBuilder();
			var open = false;
			var quoted = new StringBuilder();
			foreach (var ch in text)
			{
				if (ch == '\"')
				{
					//ret.Append(open ? '\x139' : '\x138');
					if (!open)
					{
						quoted.Clear();
						open = true;
						ret.Append('\x138');
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
						ret.Append('\x139');
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

		public static string FoldEntities(this string text)
		{
			var tag = new Regex(@"\<g(?<chr>\w{1,4})>");
			var entity = new Regex(@"&#x(?<chr>\w{1,4});");
			while (tag.IsMatch(text))
			{
				var match = tag.Match(text);
				text = text.Replace(match.Value, ((char)int.Parse(match.Groups["chr"].Value, NumberStyles.HexNumber)).ToString());
			}
			while (entity.IsMatch(text))
			{
				var match = entity.Match(text);
				text = text.Replace(match.Value, ((char)int.Parse(match.Groups["chr"].Value, NumberStyles.HexNumber)).ToString());
			}
			return text;
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
			text = text.Replace("\n", "\r\n").Replace("\r\r", "\r");
			var lines = text.Split('\n');
			var glyph = @"\<g([0-9a-fA-F]{4})\>";
			var color = @"<c(?:(?:(?<fore>\w+)(?:(?:,(?<back>\w+))?))?)>";
			html.Append("<pre>");
			foreach (var line in lines)
			{
				var s = line;
				//if (s.Equals(lines[0]))
				//	s = "<h3>" + s.Trim() + "</h3>";
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

		public static string ToUnicode(this string text)
		{
			var sb = new StringBuilder(text.Length);
			var table = NoxicoGame.IngameToUnicode;
			foreach (var ch in text)
			{
				if (ch < ' ' || ch > table.Length)
					sb.Append(ch);
				else
					sb.Append(table[ch]);
			}
			return sb.ToString();
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

		public static void FoldCostumeRandoms(Token token)
		{
			if (token == null)
				return;
			while (token.HasToken("random"))
			{
				var rnd = token.GetToken("random");
				var pick = rnd.Tokens.PickOne();
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
				if (vars[id].IsBlank())
					token.RemoveToken("var");
				else
					getvar.Name = vars[id];
			}
			if (!token.Text.IsBlank() && token.Text.Trim().StartsWith("var "))
			{
				var id = int.Parse(token.Text.Trim().Substring(4));
				token.Text = vars[id].IsBlank("<invalid token>", vars[id]);
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
					return withColors ? "<cSilver>\xA9<c>" : "Left";
				if (binding == KeyBinding.Travel || binding == KeyBinding.TabFocus)
					return withColors ? "<cSilver>\xAB<c>" : "Right";
				if (binding == KeyBinding.Pause)
					return "Start";
			}
			return TranslateKey(NoxicoGame.RawBindings[binding], longhand);
		}
		public static string TranslateKey(System.Windows.Forms.Keys key, bool longhand = false)
		{
			return TranslateKey(key.ToString());
		}
		public static string TranslateKey(string key, bool longhand = false)
		{
			key = key.ToUpperInvariant();
			if (key.StartsWith("OEM"))
				key = key.Substring(3);
			var specials = new Dictionary<string, string>()
			{
				{ "LEFT", "\x1B" },
				{ "UP", "\x18" },
				{ "RIGHT", "\x1A" },
				{ "DOWN", "\x19" },
				{ "RETURN", "Ret." },
				{ "ENTER", "Ret." },
				{ "QUESTION", "/" },
				{ "PERIOD", "." },
				{ "COMMA", "," },
				{ "QUOTES", "'" },
				{ "SEMICOLON", ";" },
				{ "ESCAPE", "Esc." },
			};
			if (longhand)
			{
				specials = new Dictionary<string, string>()
				{
					{ "QUESTION", "/" },
					{ "COMMA", "," },
					{ "QUOTES", "'" },
					{ "SEMICOLON", ";" },
				};
			}
			if (specials.ContainsKey(key))
				return specials[key].Titlecase();
			return key.Titlecase();
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
			return Regex.Replace(name.ToLower(), "(^[A-Z])", string.Empty);
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

				if (plan.HasToken("breasts"))
				{
					var boobs = plan.GetToken("breasts");
					foreach (var t in new[] { "amount", "size" })
						if (!boobs.HasToken(t))
							missing.Add("breasts/" + t);
				}
				else if (!plan.HasToken("quadruped"))
				{
					//Only consider missing breasts a problem if the character is not a quadruped.
					missing.Add("breasts");
				}
			}

			if (!(plan.HasToken("femaleonly") || plan.HasToken("neuteronly")))
			{
				if (plan.HasToken("penis"))
				{
					var penises = plan.Tokens.FindAll(x => x.Name == "penis");
					foreach (var p in penises)
					{
						foreach (var t in new[] { "thickness", "length" })
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
				throw new Exception(string.Format("The \"\" bodyplan is missing the following token(s):\r\n * {1}", name, string.Join("\r\n * ", missing)));
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

		//Used internally by Wordwrap.
		private class Word
		{
			public string Content { get; set; }
			public bool SpaceAfter { get; set; }
			public bool SoftHyphen { get; set; }
			public bool MandatoryBreak { get; set; }
			public int Length { get { return Content.Length; } }
			public Color Color { get; set; }
			public override string ToString()
			{
				return string.Format("[\"{0}\", {1}, {2}]", Content, SpaceAfter, MandatoryBreak);
			}
		}
	}
}

