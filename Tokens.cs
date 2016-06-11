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
	public class TokenCarrier
	{
		//TODO: put resolving the rolls for a tree in a separate method or sumth.
		//public static bool NoRolls { get; set; }
		public List<Token> Tokens { get; private set; }

		public TokenCarrier()
		{
			Tokens = new List<Token>();
		}

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

		public Token AddToken(string name, float value, string text)
		{
			var t = new Token(name, value, text);
			Tokens.Add(t);
			return t;
		}
		public Token AddToken(string name, string text)
		{
			var t = new Token(name, text);
			Tokens.Add(t);
			return t;
		}
		public Token AddToken(string name, float value)
		{
			var t = new Token(name, value);
			Tokens.Add(t);
			return t;
		}
		public Token AddToken(string name)
		{
			var t = new Token(name);
			Tokens.Add(t);
			return t;
		}

		public Token AddToken(Token t)
		{
			Tokens.Add(t);
			return t;
		}

		public Token RemoveToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			if (t != null)
				Tokens.Remove(t);
			return t;
		}

		public Token RemoveToken(string name, string text)
		{
			var t = Tokens.Find(x => x.Name == name && x.Text == text);
			if (t != null)
				Tokens.Remove(t);
			return t;
		}

		public Token RemoveToken(Token t)
		{
			Tokens.Remove(t);
			return t;
		}

		public Token RemoveToken(int i)
		{
			if (i < 0 || i >= Tokens.Count)
				throw new ArgumentOutOfRangeException("i");
			var t = Tokens[i];
			Tokens.Remove(t);
			return t;
		}

		public void RemoveAll(string name)
		{
			foreach (var t in Tokens.FindAll(x => x.Name == name))
				Tokens.Remove(t);
		}

		public Token Path(string pathSpec)
		{
			var parts = pathSpec.Split('/');
			var point = this;
			var final = parts.Last();
			var indexRE = new Regex(@"\[(?<index>[0-9]+)\]");
			var textRE = new Regex(@"\[=(?<text>\w+)\]");
			if (indexRE.IsMatch(final))
				final = final.Remove(final.IndexOf('['));
			else if (textRE.IsMatch(final))
				final = final.Remove(final.IndexOf('['));
			foreach (var p in parts)
			{
				Token target = null;
				if (indexRE.IsMatch(p))
				{
					var trueP = p.Remove(p.IndexOf('['));
					var index = int.Parse(indexRE.Match(p).Groups["index"].ToString());
					var targets = point.Tokens.FindAll(t => t.Name.Equals(trueP, StringComparison.OrdinalIgnoreCase));
					if (targets == null || index >= targets.Count)
						return null;
					target = targets[index];
				}
				else if (textRE.IsMatch(p))
				{
					var trueP = p.Remove(p.IndexOf('['));
					var text = textRE.Match(p).Groups["text"].ToString();
					var possibilities = point.Tokens.Where(t => t.Name.Equals(trueP, StringComparison.OrdinalIgnoreCase));
					target = possibilities.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.Text) && t.Text.Equals(text, StringComparison.OrdinalIgnoreCase));
					//target = point.Tokens.FirstOrDefault(t => t.Name.Equals(trueP, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(t.Text) && t.Text.Equals(text, StringComparison.OrdinalIgnoreCase));
				}
				else
					target = point.Tokens.Find(t => t.Name.Equals(p, StringComparison.OrdinalIgnoreCase));
				if (target == null)
					return null;
				if (target.Name.Equals(final, StringComparison.OrdinalIgnoreCase))
					return target;
				point = target;
			}
			return null;
		}

		public Token Item(int i)
		{
			if (i < 0 || i >= Tokens.Count)
				throw new ArgumentOutOfRangeException("i");
			return Tokens[i];
		}

		public int Count()
		{
			return Tokens.Count;
		}

		public void Tokenize(string a)
		{
			var t = new List<Token>();
			var lines = a.Split('\n');
			var nodes = new List<Token>();
			var prevTabs = 0;
			var cdata = false;
			var cdataText = new StringBuilder();
			foreach (var line in lines.Where(x => !string.IsNullOrWhiteSpace(x) && !x.TrimStart().StartsWith("--")))
			{
				var l = line.TrimEnd();
				if (cdata)
				{
					if (l.EndsWith("]]>"))
					{
						nodes.Last().AddToken("#text", 0, cdataText.ToString());
						cdata = false;
						continue;
					}
					cdataText.AppendLine(l);
					continue;
				}
				//count number of tabs in front
				var tabs = 0;
				for (; tabs < l.Length - 1; tabs++)
					if (l[tabs] != '\t')
						break;
				l = l.TrimStart();
				var newOne = new Token();
				var tokenName = l;
				if (tokenName == "<[[")
				{
					//Start of a CDATA-style text block! Switch to CDATA mode, keep parsing until ]]> and place it in the last node as "#text". 
					cdata = true;
					cdataText.Clear();
					continue;
				}
				/*
				if (!NoRolls && tokenName.StartsWith("oneof "))
				{
					var options = l.Substring(l.IndexOf(' ') + 1).Split(',');
					var choice = options[Random.Next(options.Length)].Trim();
					if (string.IsNullOrEmpty(choice))
						continue; //picked a blank token -- eat it.
					tokenName = choice;
				}
				else */ if (l.Contains(": "))
				{
					//Token has a value
					if (l.Contains(": \""))
					{
						var text = l.Substring(l.IndexOf('\"') + 1);
						newOne.Text = text.Remove(text.LastIndexOf('\"'));
					}
					/*
					else if (!NoRolls && l.Contains(": oneof "))
					{
						var options = l.Substring(l.IndexOf("of ") + 3).Split(',');
						var choice = options[Random.Next(options.Length)].Trim();
						newOne.Text = choice;
					}
					else if (!NoRolls && l.Contains(": roll "))
					{
						var xDyPz = l.Substring(l.LastIndexOf(' ') + 1);
						int y = 0, z = 0;
						ParseRoll(xDyPz, out y, out z);
						var roll = Random.Next(y) + z;
						newOne.Value = roll;
					}
					*/
					else if (l.Contains(": U+"))
					{
						var codePoint = l.Substring(l.LastIndexOf('+') + 1);
						newOne.Value = int.Parse(codePoint, NumberStyles.HexNumber);
					}
					else if (l.Contains(": 0x"))
					{
						var codePoint = l.Substring(l.LastIndexOf('x') + 1);
						newOne.Value = int.Parse(codePoint, NumberStyles.HexNumber);
					}
					else
					{
						float v;
						var value = l.Substring(l.IndexOf(' ') + 1);
						if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
							newOne.Value = v;
						else
							newOne.Text = value;
					}
					tokenName = tokenName.Remove(tokenName.IndexOf(':'));
					if (!string.IsNullOrWhiteSpace(newOne.Text))
					{
						var entity = new Regex("&#x([A-Za-z0-9]{2,4});");
						var matches = entity.Matches(newOne.Text);
						foreach (Match match in matches)
						{
							var replacement = new string((char)int.Parse(match.Value.Substring(3, match.Value.Length - 4), NumberStyles.HexNumber), 1);
							newOne.Text = newOne.Text.Replace(match.Value, replacement);
						}
					}
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
					throw new Exception(string.Format("Token tree contains a line that's indented too far. Line is \"{0}\", indenting {1} level(s), but the previous line is only {2} level(s).", l.Trim(), tabs, prevTabs));
				}
				prevTabs = tabs;
			}
			Tokens = t;
			//NoRolls = false;
		}

#if DEBUG
		public string DumpTokens(List<Token> list, int tabs)
		{
			var ret = new StringBuilder();
			foreach (var item in list)
			{
				if (item.Name == "#text")
				{
					ret.AppendFormat("{0}<[[", new string('\t', tabs));
					ret.AppendLine();
					ret.Append(item.Text);
					ret.AppendFormat("{0}]]>", new string('\t', tabs));
					ret.AppendLine();
					continue;
				}

				ret.AppendFormat("{0}{1}", new string('\t', tabs), item.Name);
				if (item.Value != 0 || !string.IsNullOrWhiteSpace(item.Text))
				{
					ret.Append(": ");
					if (item.Value != 0)
						ret.Append(item.Value);
					else
						ret.AppendFormat("\"{0}\"", item.Text.Replace("\"", "&#x22;"));
					ret.AppendLine();
				}
				else
					ret.AppendLine();
				if (item.Tokens.Count > 0)
					ret.Append(DumpTokens(item.Tokens, tabs + 1));
			}
			return ret.ToString();
		}
#else
		public string DumpTokens(List<Token> list, int tabs)
		{
			return string.Empty;
		}
#endif

		/// <summary>
		/// Given a string of the format "1dX" or "1dX+Y", returns X and Y.
		/// </summary>
		/// <param name="text">A dice roll</param>
		/// <param name="range">The total amount of dots on the die</param>
		/// <param name="plus">The amount to be added to the die roll</param>
		/// <returns>True if the string could be parsed, false otherwise.</returns>
		public bool ParseRoll(string text, out int range, out int plus)
		{
			range = 0;
			plus = 0;
			var m = Regex.Match(text, @"1d(\d+)\+(\d+)");
			if (!m.Success)
			{
				m = Regex.Match(text, @"1d(\d+)");
				if (!m.Success)
					return false;
			}
			range = int.Parse(m.Groups[1].Value);
			if (m.Groups.Count == 3)
				plus = int.Parse(m.Groups[2].Value);
			return true;
		}

		/// <summary>
		/// Recursively resolves roll and oneof values.
		/// </summary>
		public void ResolveRolls()
		{
			foreach (var token in Tokens)
			{
				if (token.Tokens.Count > 0)
					token.ResolveRolls();
				if (string.IsNullOrWhiteSpace(token.Text))
					continue;
				if (token.Text.StartsWith("roll "))
				{
					var xDyPz = token.Text.Substring(token.Text.LastIndexOf(' ') + 1);
					int y = 0, z = 0;
					ParseRoll(xDyPz, out y, out z);
					var roll = Random.Next(y) + z;
					token.Value = roll;
					token.Text = null;
				}
				else if (token.Text.StartsWith("oneof "))
				{
					var options = token.Text.Substring(token.Text.IndexOf("of ") + 3).Split(',');
					var choice = options[Random.Next(options.Length)].Trim();
					token.Text = choice;
				}
			}
		}

		public void Patch(string p)
		{
			var patch = new Token();
			patch.Tokenize(p);
			foreach (var token in patch.Tokens)
			{
				var path = token.Text;
				if (token.Name == "add")
				{
					if (path == "-")
					{
						Tokens.AddRange(token.Tokens);
					}
					else if (path.EndsWith("/-"))
					{
						var target = Path(path.Substring(0, path.Length - 2));
						if (target != null)
						{
							target.Tokens.AddRange(token.Tokens);
						}
					}
					{
						var target = Path(path);
						if (target != null)
						{
							Tokens.InsertRange(Tokens.IndexOf(target) + 1, token.Tokens);
						}
					}
				}
				else if (token.Name == "remove")
				{
					var target = Path(path);
					if (target != null)
					{
						if (path.LastIndexOf('/') == -1)
						{
							Tokens.Remove(target);
						}
						else
						{
							var oneUp = Path(path.Substring(0, path.LastIndexOf('/')));
							oneUp.Tokens.Remove(target);
						}
					}
				}
				else if (token.Name == "replace")
				{
					var target = Path(path);
					if (target != null)
					{
						if (path.LastIndexOf('/') == -1)
						{
							Tokens[Tokens.IndexOf(target)] = token.Tokens[0];
						}
						else
						{
							var oneUp = Path(path.Substring(0, path.LastIndexOf('/')));
							oneUp.Tokens[oneUp.Tokens.IndexOf(target)] = token.Tokens[0];
						}
					}
				}
				else if (token.Name == "set")
				{
					var target = Path(path);
					if (target != null)
					{
						var val = token.GetToken("value");
						var txt = token.GetToken("text");
						if (val != null)
							target.Value = val.Value;
						if (txt != null)
							target.Text = txt.Text;
					}
				}
			}
		}

	}

	public class Token : TokenCarrier
	{
		public string Name { get; set; }
		public float Value { get; set; }
		public string Text { get; set; }

		public override string ToString()
		{
			if (string.IsNullOrWhiteSpace(Text))
				return string.Format("{0} ({1}, {2})", Name, Value, Tokens.Count);
			return string.Format("{0} ({1}, \"{2}\", {3})", Name, Value, Text, Tokens.Count);
		}

		public Token()
		{
		}

		public Token(string name)
		{
			Name = name;
		}

		public Token(string name, float value)
		{
			Name = name;
			Value = value;
		}

		public Token(string name, string text)
		{
			Name = name;
			Text = text;
		}

		public Token(string name, float value, string text)
		{
			Name = name;
			Value = value;
			Text = text;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Name ?? string.Empty);
			var isInt = (Value == (int)Value);
			var flags = 0;
			if (Value != 0)
				flags |= 1;
			if (isInt)
				flags |= 2;
			if (!string.IsNullOrWhiteSpace(Text))
				flags |= 4;
			flags |= Tokens.Count << 4;
			stream.Write7BitEncodedInt(flags);
			if (Value != 0)
			{
				if (isInt)
					stream.Write7BitEncodedInt((int)Value);
				else
					stream.Write((Single)Value);
			}
			if (!string.IsNullOrWhiteSpace(Text))
				stream.Write(Text);
			if (Tokens.Count > 0)
				Tokens.ForEach(x => x.SaveToFile(stream));
		}

		public static Token LoadFromFile(BinaryReader stream)
		{
			var newToken = new Token();
			newToken.Name = stream.ReadString();
			var flags = stream.Read7BitEncodedInt();
			var numTokens = flags >> 4;
			if ((flags & 1) == 1)
			{
				var isInt = ((flags & 2) == 2);
				if (isInt)
					newToken.Value = stream.Read7BitEncodedInt();
				else
					newToken.Value = stream.ReadSingle();
			}
			if ((flags & 4) == 4)
				newToken.Text = stream.ReadString();
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
			//this.Tokens.AddRange(otherSet);
			foreach (var toAdd in otherSet)
			{
				var newToken = new Token(toAdd.Name, toAdd.Value, toAdd.Text);
				if (toAdd.Tokens.Count > 0)
					newToken.AddSet(toAdd.Tokens);
				this.Tokens.Add(newToken);
				//this.AddToken(toAdd.Name, toAdd.Value, toAdd.Text);
				//if (toAdd.Tokens.Count > 0)
				//	this.GetToken(toAdd.Name).AddSet(toAdd.Tokens);
			}
		}

		public Token Clone(bool deep = true)
		{
			var t = new Token(Name, Value, Text);
			foreach (var child in Tokens)
				t.AddToken(deep ? child.Clone() : child);
			return t;
		}

		public bool Equals(Token t, bool deep = true)
		{
			if (t.Name == this.Name && t.Value == this.Value && t.Text == this.Text)
			{
				if (deep)
				{
					if (t.Tokens.Count != this.Tokens.Count)
						return false; //quick cut-off!
					for (var i = 0; i < this.Tokens.Count; i++)
						if (!this.Tokens[i].Equals(t.Tokens[i]))
							return false; //at least one of the child tokens doesn't match.
				}
				return true;
			}
			return false;
		}
	}
}
