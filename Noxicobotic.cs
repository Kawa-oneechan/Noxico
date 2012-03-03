using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Noxico
{
	public static class Noxicobotic
	{
		enum MessageTypes
		{
			Box, Question, Scroller
		}

		public static void Run(Entity subject, string[] Script)
		{
			var msgBuffer = new StringBuilder();
			var msgType = MessageTypes.Box;

			var labelRE = @"^(?:(?<word>\w+) \:)";
			var commandRE = @"^(?:(?:(?<id>\w+?) \.)?) (?<command>\w+) (?:(?:\s?) (?<parms>.+)?)";
			var messageRE = @"^(?<type>[|!?])<< .+";
			var assignmentRE = @"^(?<variable>\w+) (?:\s?) = (?:\s?) (?<expression>.+)";
			var ifVarIsValRE = @"^if \s (?<variable>\w+) \s (?<compare>[!=\<\>]{1,2}) \s (?<value>\w+)";
			var ifFlagRE = @"^if \s (?<flag>set|exists|not\sset|not\sexists)$";
			var blockEndRE = @"^(?:endif|else|next)"; //so we don't try to execute these like commands
			var ro = RegexOptions.IgnorePatternWhitespace;

			//TODO: IF and FOR

			while (subject.ScriptRunning && subject.ScriptPointer < Script.Length)
			{
				var breakTime = false;
				var showMessage = false;

				while (!breakTime && subject.ScriptPointer < Script.Length)
				{
					var line = Script[subject.ScriptPointer].Trim();
					//if (!string.IsNullOrWhiteSpace(line))
					//	Console.WriteLine(line);
					subject.ScriptPointer++;
					if (subject.ScriptPointer == Script.Length)
						showMessage = true;

					#region Comments
					if (line.StartsWith("-- "))
						continue;
					#endregion

					#region Convert flag checks to variable checks
					if (Regex.IsMatch(line, ifFlagRE, ro))
					{
						//Rewrite to x=y notation
						var match = Regex.Match(line, ifFlagRE, ro);
						var flag = match.Groups["flag"].ToString();
						var convertions = new Dictionary<string, string>()
						{
							{ "set", "if set > 0" },
							{ "exists", "if exists > 0" },
							{ "not set", "if set <= 0" },
							{ "not exists", "if exists <= 0" },
						};
						line = convertions[flag];
					}
					#endregion

					#region Empty lines
					if (string.IsNullOrWhiteSpace(line))
					{
						if (!showMessage)
							continue;
					}
					#endregion

					#region Labels
					else if (Regex.IsMatch(line, labelRE, ro))
					{
						var match = Regex.Match(line, labelRE, ro);
						var label = match.Groups["word"].ToString();
						//Console.WriteLine("Label \"{0}\", irrelevant here", label);
						continue;
					}
					#endregion

					#region Block ends -- else/endif
					else if (Regex.IsMatch(line, blockEndRE, ro))
					{
						if (line == "else")
						{
							var level = 0;
							for (var i = subject.ScriptPointer; i < Script.Length; i++)
							{
								var scan = Script[i].Trim();
								if (Regex.IsMatch(scan, ifVarIsValRE, ro) || Regex.IsMatch(scan, ifFlagRE, ro))
									level++;
								if (scan == "endif")
								{
									level--;
									if (level == -1)
									{
										subject.ScriptPointer = i + 1;
										break;
									}
								}
							}
							//Console.WriteLine("Else to skip.");
							continue;
						}
						//Console.WriteLine("Block end, irrelevant here");
						continue;
					}
					#endregion

					#region Messages
					else if (Regex.IsMatch(line, messageRE, ro))
					{
						var match = Regex.Match(line, messageRE, ro);
						var type = match.Groups["type"].ToString();
						switch (type)
						{
							case "!":
								msgType = MessageTypes.Box;
								break;
							case "?":
								msgType = MessageTypes.Question;
								break;
							case "|":
								msgType = MessageTypes.Scroller;
								break;
						}
						var msg = "";
						for (var i = subject.ScriptPointer - 1; i < Script.Length; i++)
						{
							line = Script[i].Trim();
							if (i == subject.ScriptPointer - 1) //gotta cut out the start
								msg += line.Substring(3);
							else
								msg += line;
							if (line.EndsWith(">>"))
							{
								msg = msg.Remove(msg.Length - 2, 2);
								subject.ScriptPointer = i + 1;
								break;
							}
							msg += ' ';
						}
						msg = msg.Trim();
						//Note that the message should not be displayed until one of the split points is found, as described in noxicobotic.txt.
						//Console.WriteLine("Buffering \"{0}\"", msg);
						if (msg.Contains("[end]"))
							showMessage = true;
						msgBuffer.Append(msg);
						msgBuffer.Append(' ');
					}
					#endregion

					#region Assigment -- x = y
					else if (Regex.IsMatch(line, assignmentRE, ro))
					{
						var match = Regex.Match(line, assignmentRE, ro);
						var variable = match.Groups["variable"].ToString();
						var expression = match.Groups["expression"].ToString();
						//Console.Write("Assignment: {0} = {1}", variable, expression);
						var result = 0.0;
						try
						{
							result = Shunt(expression, ref NoxicoGame.ScriptVariables);
						}
						catch (Exception x)
						{
							System.Windows.Forms.MessageBox.Show("While shunting \"" + expression + ":\r\n\r\n" + x.ToString() + "\r\n" + x.Message, "Something bad happened");
							//Console.WriteLine();
							//Console.WriteLine("EXCEPTION");
							//Console.WriteLine("---------");
							//Console.WriteLine(x.Message);
							return;
						}
						//Console.WriteLine(" => {0}", result);
						if (!NoxicoGame.ScriptVariables.ContainsKey(variable))
							NoxicoGame.ScriptVariables.Add(variable, result);
						else
							NoxicoGame.ScriptVariables[variable] = result;
					}
					#endregion

					#region Variable checks -- if x < 42
					else if (Regex.IsMatch(line, ifVarIsValRE, ro))
					{
						var match = Regex.Match(line, ifVarIsValRE, ro);
						var variable = match.Groups["variable"].ToString();
						var compare = match.Groups["compare"].ToString();
						var value = match.Groups["value"].ToString();
						if (NoxicoGame.ScriptVariables.ContainsKey(variable))
							variable = NoxicoGame.ScriptVariables[variable].ToString();
						if (NoxicoGame.ScriptVariables.ContainsKey(value))
							value = NoxicoGame.ScriptVariables[value].ToString();
						var varV = float.Parse(variable);
						var valV = float.Parse(value);
						var truth = false;
						switch (compare)
						{
							case "=":
							case "==":
								truth = varV == valV;
								break;
							case "!=":
							case "<>":
								truth = varV != valV;
								break;
							case "<":
								truth = varV < valV;
								break;
							case ">":
								truth = varV > valV;
								break;
							case "<=":
								truth = varV <= valV;
								break;
							case ">=":
								truth = varV >= valV;
								break;
						}
						if (!truth)
						{
							var level = 0;
							for (var i = subject.ScriptPointer; i < Script.Length; i++)
							{
								var scan = Script[i].Trim();
								if (Regex.IsMatch(scan, ifVarIsValRE, ro) || Regex.IsMatch(scan, ifFlagRE, ro))
									level++;
								if (scan == "endif" || scan == "else")
								{
									if (scan == "endif")
										level--;
									if (level == (scan == "endif" ? -1 : 0))
									{
										subject.ScriptPointer = i + 1;
										break;
									}
								}
							}
						}
					}
					#endregion

					#region Commands!
					else if (Regex.IsMatch(line, commandRE, ro))
					{
						var match = Regex.Match(line, commandRE, ro);
						var id = match.Groups["id"].ToString();
						var command = match.Groups["command"].ToString().ToLowerInvariant();
						var parameters = match.Groups["parms"].ToString();
						if (string.IsNullOrWhiteSpace(id))
							id = "me";
						var parms = new[] { "" };
						if (!string.IsNullOrWhiteSpace(parameters))
							parms = parameters.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
						for (var i = 0; i < parms.Length; i++)
						{
							if (parms[i].StartsWith("\"") && parms[i].EndsWith("\""))
								parms[i] = parms[i].Substring(1, parms[i].Length - 2);
							if (NoxicoGame.ScriptVariables.ContainsKey(parms[i]))
								parms[i] = NoxicoGame.ScriptVariables[parms[i]].ToString();
						}
						//Console.WriteLine("Command -> {0} . {1} ({2})", id, command, string.Join(", ", parms));

						if (id == "me")
							id = subject.ID;
						else if (id == "player")
							id = NoxicoGame.HostForm.Noxico.Player.ID;
						var target = subject.ParentBoard.Entities.Find(x => x.ID.Equals(id, StringComparison.InvariantCultureIgnoreCase));
						if (target == null)
							target = subject;
						breakTime = Execute(target, command, parms);
						if (breakTime)
						{
							if (msgBuffer.Length > 0)
								showMessage = true;
							goto SkipABitBrother;
						}
						continue;
					}
					#endregion



					#region Rounding up
				SkipABitBrother:
					if (showMessage && msgBuffer.Length > 0)
					{
						showMessage = false;
						msgBuffer.Replace("[end]", "");
						msgBuffer.Replace("[break]", "\r\n");
						foreach (var variable in NoxicoGame.ScriptVariables)
							msgBuffer.Replace("<$" + variable.Key + ">", variable.Value.ToString());
						switch (msgType)
						{
							case MessageTypes.Box:
								MessageBox.Message(msgBuffer.ToString(), true);
								break;
							case MessageTypes.Question:
								MessageBox.Ask(msgBuffer.ToString(),
									() =>
									{
										NoxicoGame.ScriptVariables["set"] = 1;
									},
									() =>
									{
										NoxicoGame.ScriptVariables["set"] = 0;
									},
									true);
								break;
							case MessageTypes.Scroller:
								TextScroller.Noxicobotic(subject, msgBuffer.ToString());
								break;
						}
						msgBuffer.Clear();
						return;
					}
					#endregion
				}
				if (breakTime)
					break;
			}
		}

		public static bool Execute(Entity subject, string command, params string[] parms)
		{
			switch (command)
			{
				#region Visual presentation
				case "char":
					if (parms.Length < 1)
						throw new ParameterMismatchException("char", 1);
					subject.AsciiChar = ParseCharacter(parms[0]);
					return false;
				case "color":
					if (parms.Length < 1)
						throw new ParameterMismatchException("color", 1);
					if (parms.Length == 1)
						subject.ForegroundColor = Toolkit.GetColor(parms[0]);
					else if (parms.Length == 2)
					{
						subject.ForegroundColor = Toolkit.GetColor(parms[0]);
						subject.BackgroundColor = Toolkit.GetColor(parms[1]);
					}
					else if (parms.Length == 3)
						subject.ForegroundColor = System.Drawing.Color.FromArgb((int)ParseNumber(parms[0]), (int)ParseNumber(parms[1]), (int)ParseNumber(parms[2]));
					else if (parms.Length == 6)
					{
						subject.ForegroundColor = System.Drawing.Color.FromArgb((int)ParseNumber(parms[0]), (int)ParseNumber(parms[1]), (int)ParseNumber(parms[2]));
						subject.BackgroundColor = System.Drawing.Color.FromArgb((int)ParseNumber(parms[3]), (int)ParseNumber(parms[4]), (int)ParseNumber(parms[5]));
					}
					return false;
				case "adjustview":
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					((BoardChar)subject).AdjustView();
					return false;
				#endregion
				#region Timing and movement
				case "wait":
					if (parms.Length < 1)
						throw new ParameterMismatchException("wait", 1);
					subject.ScriptDelay = (int)ParseNumber(parms[0]);
					return true;
				case "go":
					if (parms.Length < 1)
						throw new ParameterMismatchException("go", 1);
					var directionMap = new Dictionary<string, Direction>()
					{
						{ "north", Direction.North },
						{ "south", Direction.South },
						{ "west", Direction.West },
						{ "east", Direction.East },
						{ "up", Direction.North },
						{ "down", Direction.South },
						{ "left", Direction.West },
						{ "right", Direction.East },
						{ "flow", subject.Flow }, //haha
					};
					if (directionMap.ContainsKey(parms[0]))
					{
						subject.Move(directionMap[parms[0]]);
						subject.ScriptDelay = 0; //Needed?
					}
					return true;
				case "find":
					//Should use the Dijkstra mapper.
					throw new NotImplementedException();
				case "transport":
					if (parms.Length < 2)
						throw new ParameterMismatchException("transport", 2);
					subject.XPosition = (int)ParseNumber(parms[0]);
					subject.YPosition = (int)ParseNumber(parms[1]);
					return true;
				#endregion
				#region Death
				case "immolate":
					subject.ParentBoard.Immolate(subject.YPosition, subject.XPosition);
					Execute(subject, "die");
					return true;
				case "die":
					subject.ParentBoard.EntitiesToRemove.Add(subject);
					subject.ScriptRunning = false;
					return true;
				case "gib":
					throw new NotImplementedException("Not yet.");
				#endregion
				#region Flow control
				case "repeat":
					subject.ScriptPointer = 0;
					return false;
				case "end":
					subject.ScriptRunning = false;
					return true;
				case "goto":
					if (parms.Length < 1)
						throw new ParameterMismatchException("goto", 1);
					subject.CallScript(parms[0]);
					return false;
				case "clearsubs":
					NoxicoGame.Subscreen = null;
					Subscreens.PreviousScreen.Clear();
					return false;
				#endregion
				#region Tokens
				case "checktag":
					if (parms.Length < 1)
						throw new ParameterMismatchException("checktag", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					var token = ((BoardChar)subject).Character.Path(parms[0]);
					NoxicoGame.ScriptVariables["exists"] = token == null ? 0 : 1;
					return false;
				case "addtag":
					if (parms.Length < 1)
						throw new ParameterMismatchException("addtag", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					if (parms.Length == 2)
					{
						token = ((BoardChar)subject).Character.Path(parms[0]);
						if (token == null)
							throw new Exception("No such token path.");
						if (!token.HasToken(parms[1]))
							token.Tokens.Add(new Token() { Name = parms[1] });
					}
					else if (!((BoardChar)subject).Character.HasToken(parms[0]))
						((BoardChar)subject).Character.Tokens.Add(new Token() { Name = parms[0] });
					return false;
				case "replacetag":
					if (parms.Length < 2)
						throw new ParameterMismatchException("replacetag", 2);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					token.Name = parms[1];
					token.Value = 0;
					token.Tokens.Clear();
					return false;
				case "renametag":
					if (parms.Length < 2)
						throw new ParameterMismatchException("renametag", 2);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					token.Name = parms[1];
					return false;
				case "removetag":
					if (parms.Length < 1)
						throw new ParameterMismatchException("removetag", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					if (parms.Length == 2)
					{
						token = ((BoardChar)subject).Character.Path(parms[0]);
						if (token == null)
							throw new Exception("No such token path.");
						if (!token.HasToken(parms[1]))
							token.RemoveToken(parms[1]);
					}
					else if (((BoardChar)subject).Character.HasToken(parms[0]))
						((BoardChar)subject).Character.RemoveToken(parms[0]);
					return false;
				case "settagval":
					if (parms.Length < 2)
						throw new ParameterMismatchException("settagval", 2);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					token.Value = ParseNumber(parms[1]);
					return false;
				case "gettagval":
					if (parms.Length < 1)
						throw new ParameterMismatchException("gettagval", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						NoxicoGame.ScriptVariables["it"] = 0;
					else
						NoxicoGame.ScriptVariables["it"] = token.Value;
					return false;
				case "inctagval":
					if (parms.Length < 1)
						throw new ParameterMismatchException("inctagval", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					if (parms.Length == 1)
						token.Value += 1;
					else
						token.Value += ParseNumber(parms[1]);
					return false;
				case "dectagval":
					if (parms.Length < 1)
						throw new ParameterMismatchException("dectagval", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					if (parms.Length == 1)
						token.Value -= 1;
					else
						token.Value -= ParseNumber(parms[1]);
					return false;
				case "roundtagval":
					if (parms.Length < 1)
						throw new ParameterMismatchException("roundtagval", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					token = ((BoardChar)subject).Character.Path(parms[0]);
					if (token == null)
						throw new Exception("No such token path.");
					token.Value = (float)((int)token.Value);
					return false;
				#endregion
				#region Plot flags
				#endregion
				#region Body horror
				case "morph":
					if (parms.Length < 1)
						throw new ParameterMismatchException("morph", 1);
					var targetPlan = parms[0];
					var chance = parms.Length > 1 ? (int)ParseNumber(parms[1]) : 0;
					var reportLevel = parms.Contains("silent") ? MorphReportLevel.NoReports : parms.Contains("any") ? MorphReportLevel.Anyone : MorphReportLevel.PlayerOnly;
					var asMessages = !parms.Contains("window");
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					((BoardChar)subject).Character.Morph(targetPlan, reportLevel, asMessages, chance);
					((BoardChar)subject).AdjustView();
					if (!asMessages)
					{
						MessageBox.Message(Character.MorphBuffer.ToString(), true);
						Character.MorphBuffer.Clear();
						return true;
					}
					return false;
				case "setspecies":
					if (parms.Length < 1)
						throw new ParameterMismatchException("setspecies", 1);
					if (!(subject is BoardChar))
						throw new Exception("Noxicobotic subject is not a BoardChar.");
					var species = parms[0];
					var male = "male " + species;
					var female = "female " + species;
					if (parms.Length == 3)
					{
						male = parms[1];
						female = parms[2];
					}
					if (((BoardChar)subject).Character.HasToken("malename"))
						((BoardChar)subject).Character.GetToken("malename").Tokens[0].Name = male;
					if (((BoardChar)subject).Character.HasToken("femalename"))
						((BoardChar)subject).Character.GetToken("femalename").Tokens[0].Name = female;
					((BoardChar)subject).Character.Species = species;
					((BoardChar)subject).Character.UpdateTitle();
					return false;
				#endregion
			}
			return false;
		}

		private static char ParseCharacter(string parm)
		{
			if (parm.Length == 3 && parm[0] == '\'' && parm[2] == '\'')
				return parm[1];
			else
				return (char)ParseNumber(parm);
		}

		private static float ParseNumber(string parm)
		{
			if (parm.StartsWith("0x"))
				return (float)int.Parse(parm.Substring(2), NumberStyles.HexNumber);
			return float.Parse(parm, CultureInfo.InvariantCulture);
		}

		private static double Shunt(string expression, ref Dictionary<string, double> variables)
		{
			//Phase 1 - Tokenize
			var tokens = new List<string>();
			var token = "";
			foreach (var ch in expression)
			{
				if (char.IsWhiteSpace(ch))
				{
					if (token != "")
						tokens.Add(token);
					token = "";
					continue;
				}
				if (char.IsLetterOrDigit(ch) || ch == '$' || ch == '.' || ch == '\'' || (ch == '-' && token == ""))
					token += ch;
				else if (ch == ',')
				{
					//token += '.'; //Consider 2,5 to mean 2½

					//Consider 2,5 to mean a two and a 5
					if (token != "")
						tokens.Add(token);
					tokens.Add(ch.ToString());
					token = "";
				}
				else if (ch == '<' || ch == '>')
				{
					if (token.Length == 1 && token[0] == ch)
					{
						token += ch;
						tokens.Add(token);
						token = "";
					}
					else
					{
						if (token != "")
							tokens.Add(token);
						token = ch.ToString();
					}
				}
				else
				{
					if (token != "")
						tokens.Add(token);
					tokens.Add(ch.ToString());
					token = "";
				}
			}
			if (token != "")
				tokens.Add(token);

			//Phase 2 - Shunting Yard
			var functions = new List<string>() { "floor", "ceil", "abs", "neg", "sin", "cos", "int", "pow" };
			var precedence = new Dictionary<char, int>() { { '^', 4 }, { '*', 3 }, { '/', 3 }, { '+', 2 }, { '-', 2 }, { '<', 1 }, { '>', 1 } };
			var rightAssocs = new List<char>() { '^' };
			var output = new List<string>();
			var opStack = new Stack<string>();
			foreach (var tok in tokens)
			{
				if (functions.Contains(tok))
				{
					opStack.Push(tok);
				}
				else if (precedence.ContainsKey(tok[0]) && !(tok[0] == '-' && tok.Length > 1))
				{
					var o1 = tok[0];
					while (opStack.Count > 0)
					{
						var o2 = opStack.Peek()[0];
						if (precedence.ContainsKey(o2))
						{
							var rightAssociative = rightAssocs.Contains(o1);
							if (rightAssociative && precedence[o1] < precedence[o2])
								output.Add(opStack.Pop());
							else if (precedence[o1] <= precedence[o2])
								output.Add(opStack.Pop());
							break;
						}
						else
							break;
					}
					opStack.Push(tok);
				}
				else if (tok == ",")
				{
					while (opStack.Count > 0)
					{
						if (opStack.Peek() == "(")
							break;
						else
							output.Add(opStack.Pop());
					}
				}
				else if (tok == "(")
				{
					opStack.Push(tok);
				}
				else if (tok == ")")
				{
					while (opStack.Peek() != "(")
						output.Add(opStack.Pop());
					opStack.Pop(); //throw away the (
					if (opStack.Count > 0 && functions.Contains(opStack.Peek()))
						output.Add(opStack.Pop());
				}
				else
				{
					output.Add(tok);
				}
			}
			foreach (var op in opStack)
				output.Add(op);

			//Phase 3 - RPN
			var rpnStack = new Stack<double>();
			var op1 = 0.0;
			var op2 = 0.0;
			foreach (var tok in output)
			{
				if (tok == "floor")
				{
					rpnStack.Push(Math.Floor(rpnStack.Pop()));
				}
				else if (tok == "ceil")
				{
					rpnStack.Push(Math.Ceiling(rpnStack.Pop()));
				}
				else if (tok == "abs")
				{
					rpnStack.Push(Math.Abs(rpnStack.Pop()));
				}
				else if (tok == "neg")
				{
					rpnStack.Push(-(rpnStack.Pop()));
				}
				else if (tok == "sin")
				{
					rpnStack.Push(Math.Sin(rpnStack.Pop()));
				}
				else if (tok == "cos")
				{
					rpnStack.Push(Math.Cos(rpnStack.Pop()));
				}
				else if (tok == "int")
				{
					rpnStack.Push((int)rpnStack.Pop());
				}
				else if (tok == "^" || tok == "pow")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
					rpnStack.Push(Math.Pow(op2, op1));
				}
				else if (tok == "*")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
					rpnStack.Push(op2 * op1);
				}
				else if (tok == "/")
				{
					op1 = rpnStack.Pop();
					if (op1 == 0)
						throw new DivideByZeroException();
					op2 = rpnStack.Pop();
					rpnStack.Push(op2 / op1);
				}
				else if (tok == "+")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
					rpnStack.Push(op1 + op2);
				}
				else if (tok == "-")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
					rpnStack.Push(op2 - op1);
				}
				else if (tok == "<<")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
#if STRICT_ON_SHIFT
					if ((int)op1 != op1 || (int)op2 != op2)
						throw new InvalidCastException("Shifting floats is considered stupid.");
#endif
					rpnStack.Push((int)op2 << (int)op1);
				}
				else if (tok == ">>")
				{
					op1 = rpnStack.Pop();
					op2 = rpnStack.Pop();
#if STRICT_ON_SHIFT
					if ((int)op1 != op1 || (int)op2 != op2)
						throw new InvalidCastException("Shifting floats is considered stupid.");
#endif
					rpnStack.Push((int)op2 >> (int)op1);
				}
				else if (variables.ContainsKey(tok))
				{
					rpnStack.Push(variables[tok]);
				}
				else
				{
					var asNum = 0.0;
					if (tok.StartsWith("0x"))
						asNum = int.Parse(tok.Substring(2), System.Globalization.NumberStyles.HexNumber);
					else if (tok.StartsWith("$"))
						asNum = int.Parse(tok.Substring(1), System.Globalization.NumberStyles.HexNumber);
					else if (tok.Length == 3 && tok[0] == '\'' && tok[2] == '\'')
						asNum = tok[1];
					else
						if (!double.TryParse(tok, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out asNum))
							throw new Exception("Unknown token \"" + tok + "\".");
					rpnStack.Push(asNum);
				}
			}

			return rpnStack.Pop();
		}
	}

	public partial class Entity
	{
		partial void RunCycle()
		{
			if (!ScriptRunning)
				return;

			if (ScriptDelay > 0)
			{
				ScriptDelay--;
				return;
			}

			var sp = ScriptPointer;
			var sr = ScriptRunning;
			Noxicobotic.Run(this, Script);
			//ScriptPointer = sp;
			//ScriptRunning = sr;
		}
	}

	public class ParameterMismatchException : Exception
	{
		public ParameterMismatchException(string command, int parms) : base(string.Format("The {0} command expects {1} parameter(s).", command, parms))
		{
		}
	}
}
