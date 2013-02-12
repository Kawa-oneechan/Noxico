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

		public Token Path(string path)
		{
			var parts = path.Split('/');
			var point = this;
			var final = parts.Last();
			if (Regex.IsMatch(final, @"\[(?<index>[0-9]+)\]"))
				final = final.Remove(final.IndexOf('['));
			foreach (var p in parts)
			{
				Token target = null;
				if (Regex.IsMatch(p, @"\[(?<index>[0-9]+)\]"))
				{
					var trueP = p.Remove(p.IndexOf('['));
					var index = int.Parse(Regex.Match(p, @"\[(?<index>[0-9]+)\]").Groups["index"].ToString());
					var targets = point.Tokens.FindAll(t => t.Name.Equals(trueP, StringComparison.InvariantCultureIgnoreCase));
					if (targets == null || index >= targets.Count)
						return null;
					target = targets[index];
				}
				else
					target = point.Tokens.Find(t => t.Name.Equals(p, StringComparison.InvariantCultureIgnoreCase));
				if (target == null)
					return null;
				if (target.Name.Equals(final, StringComparison.InvariantCultureIgnoreCase))
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

		public void Tokenize(string a)
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
					var choice = options[Random.Next(options.Length)].Trim();
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
						var choice = options[Random.Next(options.Length)].Trim();
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
						var roll = Random.Next(y) + z;
						newOne.Value = roll;
					}
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
			Tokens = t;
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
#else
		public string DumpTokens(List<Token> list, int tabs)
		{
			return string.Empty;
		}
#endif
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

		public Token(string name, float value, string text)
		{
			Name = name;
			Value = value;
			Text = text;
		}

		public void SaveToFile(BinaryWriter stream)
		{
			//No expectations here -- that'd be TOO damn much, unless we get to compress things.
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
			this.Tokens.AddRange(otherSet);
			/*
			foreach (var toAdd in otherSet)
			{
				this.AddToken(toAdd.Name, toAdd.Value, toAdd.Text);
				if (toAdd.Tokens.Count > 0)
					this.GetToken(toAdd.Name).AddSet(toAdd.Tokens);
			}
			*/
		}
	}
}
