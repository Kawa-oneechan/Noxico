using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Noxico
{
	/// <summary>
	/// Base class for all things Token.
	/// </summary>
	public class TokenCarrier
	{
		/// <summary>
		/// The child Tokens for this Token.
		/// </summary>
#if DEBUG
		//Doesn't work quite right yet.
		[System.ComponentModel.Editor(typeof(TokenEditor), typeof(System.Drawing.Design.UITypeEditor))]
#endif
		public List<Token> Tokens { get; private set; }

		/// <summary>
		/// Initializes a new TokenCarrier.
		/// </summary>
		public TokenCarrier()
		{
			Tokens = new List<Token>();
		}

		/// <summary>
		/// Checks to see if this Token has a child Token with the specified name.
		/// </summary>
		/// <param name="name">The Token to check for.</param>
		/// <returns>Returns true if the Token exists, false otherwise.</returns>
		public bool HasToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t != null;
		}

		/// <summary>
		/// Returns the first child Token with the specified name.
		/// </summary>
		/// <param name="name">The Token to return.</param>
		/// <returns>Returns the Token or null.</returns>
		public Token GetToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			return t;
		}

		public IEnumerable<Token> GetAll(string name)
		{
			return Tokens.Where(t => t.Name == name);
		}

		/// <summary>
		/// Adds a new child Token with the specified name, value, and text.
		/// </summary>
		/// <param name="name">The name of the Token to add.</param>
		/// <param name="value">The value to assign to the new Token.</param>
		/// <param name="text">The text to assign to the new Token.</param>
		/// <returns>Returns the Token that was added.</returns>
		public Token AddToken(string name, float value, string text)
		{
			var t = new Token(name, value, text);
			Tokens.Add(t);
			return t;
		}
		/// <summary>
		/// Adds a new child Token with the specified name and text.
		/// </summary>
		/// <param name="name">The name of the Token to add.</param>
		/// <param name="text">The text to assign to the new Token.</param>
		/// <returns>Returns the Token that was added.</returns>
		public Token AddToken(string name, string text)
		{
			var t = new Token(name, text);
			Tokens.Add(t);
			return t;
		}
		/// <summary>
		/// Adds a new child Token with the specified name and value.
		/// </summary>
		/// <param name="name">The name of the Token to add.</param>
		/// <param name="value">The value to assign to the new Token.</param>
		/// <returns>Returns the Token that was added.</returns>
		public Token AddToken(string name, float value)
		{
			var t = new Token(name, value);
			Tokens.Add(t);
			return t;
		}
		/// <summary>
		/// Adds a new child Token with the specified name.
		/// </summary>
		/// <param name="name">The name of the Token to add.</param>
		/// <returns>Returns the Token that was added.</returns>
		public Token AddToken(string name)
		{
			var t = new Token(name);
			Tokens.Add(t);
			return t;
		}

		/// <summary>
		/// Adds a Token to this Token's children.
		/// </summary>
		/// <param name="name">The Token to add.</param>
		/// <returns>Returns the Token that was added.</returns>
		public Token AddToken(Token t)
		{
			Tokens.Add(t);
			return t;
		}

		/// <summary>
		/// Finds the first token with the specified name and removes it.
		/// </summary>
		/// <param name="name">The name of the Token to remove.</param>
		/// <returns>Returns the Token that was removed, or null if there was none.</returns>
		public Token RemoveToken(string name)
		{
			var t = Tokens.Find(x => x.Name == name);
			if (t != null)
				Tokens.Remove(t);
			return t;
		}

		/// <summary>
		/// Finds the first token with the specified name and text and removes it.
		/// </summary>
		/// <param name="name">The name of the Token to remove.</param>
		/// <param name="text">The text of the Token to remove.</param>
		/// <returns>Returns the Token that was removed, or null if there was none.</returns>
		public Token RemoveToken(string name, string text)
		{
			var t = Tokens.Find(x => x.Name == name && x.Text == text);
			if (t != null)
				Tokens.Remove(t);
			return t;
		}

		/// <summary>
		/// Removes the specified Token from this Token's children.
		/// </summary>
		/// <param name="token">The Token to remove.</param>
		/// <returns>Returns the Token that was removed.</returns>
		public Token RemoveToken(Token token)
		{
			Tokens.Remove(token);
			return token;
		}

		/// <summary>
		/// Removes the child Token at the specified index.
		/// </summary>
		/// <param name="index">The index of the Token to remove.</param>
		/// <returns>Returns the Token that was removed.</returns>
		/// <exception cref="System.ArgumentOutOfRangeException">The requested index was out of range.</exception>
		public Token RemoveToken(int index)
		{
			if (index < 0 || index >= Tokens.Count)
				throw new ArgumentOutOfRangeException("i");
			var t = Tokens[index];
			Tokens.Remove(t);
			return t;
		}

		/// <summary>
		/// Removes all child Tokens by the specified name.
		/// </summary>
		/// <param name="name">The name of the Tokens to remove.</param>
		public void RemoveAll(string name)
		{
			foreach (var t in Tokens.FindAll(x => x.Name == name))
				Tokens.Remove(t);
		}

		/// <summary>
		/// Finds a Token by a given path and returns it.
		/// </summary>
		/// <param name="pathSpec">The path to the Token to find.</param>
		/// <returns>Returns the Token. If not found, returns null.</returns>
		/// <example>"foo/bar" returns, the foo token's bar child. "bar[4]" returns the 4th bar token. "foo[=cake]" returns a foo token with text "cake".</example>
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

		public Token Item(int index)
		{
			if (index < 0 || index >= Tokens.Count)
				throw new ArgumentOutOfRangeException("i");
			return Tokens[index];
		}

		public int Count()
		{
			return Tokens.Count;
		}

		/// <summary>
		/// Converts a TML string to a Token tree.
		/// </summary>
		/// <param name="source">The TML string to convert.</param>
		public void Tokenize(string source)
		{
			var t = new List<Token>();
			var lines = source.Split('\n');
			var nodes = new List<Token>();
			var prevTabs = 0;
			var cdata = false;
			var cdataText = new StringBuilder();
			foreach (var line in lines.Where(x => !x.TrimStart().StartsWith("--")))
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
				else
					if (string.IsNullOrWhiteSpace(line))
						continue;
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
				if (tokenName.StartsWith("- "))
				{
					//Token is an anonymous value -- transform from "- X" to "#a: X".
					tokenName = string.Format("#a: {0}", tokenName.Substring(2));
					l = tokenName;
				}
				if (l.Contains(": "))
				{
					//Token has a value
					if (l.Contains(": \""))
					{
						var text = l.Substring(l.IndexOf('\"') + 1);
						newOne.Text = text.Remove(text.LastIndexOf('\"'));
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

		/// <summary>
		/// Recursively converts a Token list to a TML string. Available in debug builds only.
		/// </summary>
		/// <param name="list">The list of Tokens to convert.</param>
		/// <param name="tabs">The amount of tab characters to indent.</param>
		/// <returns>Returns a TML string representation of the specified Tokens. Returns the empty string in release builds.</returns>
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

		/// <summary>
		/// Applies a patch, given in TML format, to this Token tree.
		/// </summary>
		/// <param name="source">A TML representation of the patch to apply.</param>
		/// <remarks>Patch syntax is somewhat based on JSON PATCH, RFC 6902.</remarks>
		public void Patch(string source)
		{
			var patch = new Token();
			patch.Tokenize(source);
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

	/// <summary>
	/// The backbone of Noxico data.
	/// </summary>
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

		/// <summary>
		/// Initializes a new Token. Should not be used as-is unless directly followed by something to set the Name.
		/// </summary>
		public Token()
		{
		}

		/// <summary>
		/// Initializes a new Token.
		/// </summary>
		/// <param name="name">The name to give the new Token.</param>
		public Token(string name)
		{
			Name = name;
		}

		/// <summary>
		/// Initializes a new Token.
		/// </summary>
		/// <param name="name">The name to give the new Token.</param>
		/// <param name="value">The value to give the new Token.</param>
		public Token(string name, float value)
		{
			Name = name;
			Value = value;
		}

		/// <summary>
		/// Initializes a new Token.
		/// </summary>
		/// <param name="name">The name to give the new Token.</param>
		/// <param name="text">The text to give the new Token.</param>
		public Token(string name, string text)
		{
			Name = name;
			Text = text;
		}

		/// <summary>
		/// Initializes a new Token.
		/// </summary>
		/// <param name="name">The name to give the new Token.</param>
		/// <param name="value">The value to give the new Token.</param>
		/// <param name="text">The text to give the new Token.</param>
		public Token(string name, float value, string text)
		{
			Name = name;
			Value = value;
			Text = text;
		}

		/// <summary>
		/// Recursively serializes a Token to a stream for storage, along with its children.
		/// </summary>
		/// <param name="stream">The stream to write to.</param>
		public void SaveToFile(BinaryWriter stream)
		{
			stream.Write(Name ?? string.Empty);
			var isInt = (Value == (int)Value);
			//Format: child count, reserved, has text, value is integral, has value.
			var flags = 0;
			if (Value != 0)
				flags |= 1;
			if (isInt)
				flags |= 2;
			if (!string.IsNullOrWhiteSpace(Text))
				flags |= 4;
			flags |= Tokens.Count << 4;
			//We store this as an encoded value to allow any amount of child tokens.
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

		/// <summary>
		/// Recursively deserializes a Token and its children from a stream.
		/// </summary>
		/// <param name="stream">The stream to read from.</param>
		/// <returns>Returns the Token that was deserialized.</returns>
		public static Token LoadFromFile(BinaryReader stream)
		{
			var newToken = new Token();
			newToken.Name = stream.ReadString();
			var flags = stream.Read7BitEncodedInt();
			//Format: child count, reserved, has text, value is integral, has value.
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

		/// <summary>
		/// Recursively checks if this Token's children matches another list of Tokens, by name.
		/// </summary>
		/// <param name="otherSet">The list of Tokens to compare against.</param>
		/// <returns>Returns true if the sets match.</returns>
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

		/// <summary>
		/// Removes each Token in the specified set from this Token's children, by reference.
		/// </summary>
		/// <param name="otherSet">A list of Tokens to remove.</param>
		public void RemoveSet(List<Token> otherSet)
		{
			//throw new NotImplementedException();
			foreach (var t in otherSet)
				this.Tokens.Remove(t);
		}

		/// <summary>
		/// Recursively adds a list of Tokens to this Token's children, by cloning.
		/// </summary>
		/// <param name="otherSet">The list of Tokens to add.</param>
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

		/// <summary>
		/// Creates a new Token with the same name, value, text, and (recursively) children as this one.
		/// </summary>
		/// <param name="deep">If true, children are also cloned. If false, they are passed as references to the originals.</param>
		/// <returns>Returns a copy of this Token.</returns>
		public Token Clone(bool deep = true)
		{
			var t = new Token(Name, Value, Text);
			foreach (var child in Tokens)
				t.AddToken(deep ? child.Clone() : child);
			return t;
		}

		/// <summary>
		/// Compares this Token's name, value, and text to another Token's.
		/// </summary>
		/// <param name="other">The Token to compare to.</param>
		/// <param name="deep">If true, also checks the Tokens' children.</param>
		/// <returns>Returns true if the tokens match.</returns>
		public bool Equals(Token other, bool deep = true)
		{
			if (other.Name == this.Name && other.Value == this.Value && other.Text == this.Text)
			{
				if (deep)
				{
					if (other.Tokens.Count != this.Tokens.Count)
						return false; //quick cut-off!
					for (var i = 0; i < this.Tokens.Count; i++)
						if (!this.Tokens[i].Equals(other.Tokens[i]))
							return false; //at least one of the child tokens doesn't match.
				}
				return true;
			}
			return false;
		}
	}
}
