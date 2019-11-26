using Neo.IronLua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public static class SexManager
	{
		private static List<Token> choices;
		private static string[] memory;

		private static void FixChoices(List<Token> list)
		{
			foreach (var group in list.Where(t => t.Name == "group"))
			{
				FixChoices(group.Tokens);
			}
			foreach (var choice in list.Where(t => t.Name == "choice"))
			{
				var n = choice.GetToken("_n") ?? choice.AddToken("_n");
				if (n.Text.IsBlank())
					n.Text = choice.Text.Replace('_', ' ').Titlecase();
				if (!choice.HasToken("time"))
					choice.AddToken("time", 1000);
			}
		}

		private static void LoadChoices()
		{
			if (choices != null)
				return;
			choices = Mix.GetTokenTree("sex.tml");
			FixChoices(choices);
		}

		private static List<Token> GetPossibilitiesHelper(Character[] actors, List<Token> list)
		{
			var possibilities = new List<Token>();
			foreach (var choice in list)
			{
				if (choice.Name == "group")
				{
					if (LimitsOkay(actors, choice))
						possibilities.AddRange(GetPossibilitiesHelper(actors, choice.Tokens));
					continue;
				}
				if (choice.HasToken("meta"))
					continue;
				/*
				if (!(actors[0].BoardChar is Player) && choice.HasToken("ai_unlikely") && Random.NextDouble() < 0.85)
					continue;
                if (!(actors[0].BoardChar is Player) && choice.HasToken("ai_cant"))
                    continue;
				*/
                if (LimitsOkay(actors, choice))
					possibilities.Add(choice);
			}
			return possibilities;
		}

		/// <summary>
		/// Returns a map of possible actions for a participant to pick from.
		/// </summary>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>Possible actions by ID to pass to GetResult</returns>
		public static Dictionary<object, string> GetPossibilities(Character actor, Character target)
		{
			memory = new string[10];
			if (choices == null)
				LoadChoices();
			var actors = new[] { actor, target };
			var possibilities = GetPossibilitiesHelper(actors, choices);
			//var possibilities = choices.(c => c.Name == "choice" && LimitsOkay(actors, c));
			var result = new Dictionary<object, string>();
			foreach (var possibility in possibilities)
			{
				if (result.ContainsKey(possibility.Text))
					continue;
				result.Add(possibility, i18n.Viewpoint(ApplyMemory(possibility.GetToken("_n").Text), actor, target));
			}
			return result;
		}

		public static bool LimitsOkay(Character[] actors, Token c)
		{
			var filter = c.GetToken("filter");
			if (c.Name == "group")
				filter = c;
			if (filter == null)
				return true;
			var env = Lua.Environment;
#if DEBUG
			env.debug = true;
#else
			env.debug = false;
#endif
			env.top = actors[0];
			env.bottom = actors[1];
			env.consentual = !actors[1].HasToken("helpless");
			env.nonconsentual = actors[1].HasToken("helpless");
			env.masturbating = actors[0] == actors[1];
			if (env.GetClothing == null) env.GetClothing = new Func<Character, string, int, bool>((a, clothClass, s) =>
			{
				InventoryItem cloth = null;
				var haveSomething = false;
				var slots = clothClass == "top" ? new[] { "cloak", "jacket", "shirt", "undershirt" } : clothClass == "bottom" ? new[] { "pants", "underpants" } : null;
				if (slots == null)
				{
					cloth = a.GetEquippedItemBySlot(clothClass);
					if (cloth != null)
						haveSomething = true;
				}
				else
				{
					foreach (var slot in slots)
					{
						var newCloth = a.GetEquippedItemBySlot(slot);
						if (newCloth != null)
						{
							if (!(a.BoardChar is Player) && newCloth.HasToken("sextoy"))
								continue; //Let's not take off that strap-on lol...
							if (newCloth.CarriedToken[a.ID].HasToken("torn"))
								continue; //Ignore torn stuff.
							cloth = newCloth;
							haveSomething = true;
							break;
						}
					}
					if (!haveSomething)
						return false;
				}
				if (haveSomething)
				{
					memory[s] = cloth.ToString(null, false, false);
					return true;
				}
				return false;
			});
			//return env.DoChunk("return " + filter.Text, "lol.lua").ToBoolean();
			return Lua.Run("return " + filter.Text, env);
		}

		public static List<Token> GetResultHelper(string id, Character[] actors, List<Token> list)
		{
			//choices.FindAll(c => c.Text == id && LimitsOkay(actors, c)).ToArray();
			var possibilities = new List<Token>();
			foreach (var choice in list)
			{
				if (choice.Name == "group" && LimitsOkay(actors, choice))
				{
					possibilities.AddRange(GetResultHelper(id, actors, choice.Tokens));
					continue;
				}
				if (choice.Text == id && LimitsOkay(actors, choice))
					possibilities.Add(choice);
			}
			return possibilities;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id">A sex action</param>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>A SexResult object encoding the results of the action</returns>
		public static Token GetResult(string id, Character actor, Character target)
		{
			var actors = new[] { actor, target };
			if (choices == null)
				LoadChoices();
			var possibilities = GetResultHelper(id, actors, choices);
			if (possibilities.Count == 0)
				throw new NullReferenceException(string.Format("Could not find a sex choice named \"{0}\".", id));
			var choice = possibilities.PickOne();
			return choice;
		}

		public static void Engage(Character actor, Character target)
		{
			/*
			if (actor.Character.HasToken("havingsex"))
				throw new Exception(string.Format("Actor ({0}) already having sex.", actor.Character.ToString()));
			if (target.Character.HasToken("havingsex"))
				throw new Exception(string.Format("Target ({0}) already having sex.", target.Character.ToString()));
			*/
			if (actor != target)
			{
				actor.RemoveAll("havingsex");
				target.RemoveAll("havingsex");
				actor.AddToken("havingsex", 0, target.ID);
				target.AddToken("havingsex", 0, actor.ID);
				actor.AddToken("havingsex_initsex", 0, target.ID);
				target.AddToken("havingsex_initsex", 0, actor.ID);
			}
			else
			{
				actor.RemoveAll("havingsex");
				actor.AddToken("havingsex", 0, target.ID);
				actor.AddToken("havingsex_initsex", 0, target.ID);
			}
		}

		public static void Apply(Token result, Character actor, Character target, Action<string> writer)
		{
			if (!result.HasToken("effect"))
				return;
			var f = result.GetToken("effect");
			var script = f.Tokens.Count == 1 ? f.Tokens[0].Text : f.Text;
			var env = Lua.Environment;
			env.top = actor;
			env.bottom = target;
			env.consentual = !target.HasToken("helpless");
			env.nonconsentual = target.HasToken("helpless");
			env.masturbating = actor == target;
			env.MessageR = new Action<object, Color>((x, y) =>
			{
				if (x is Neo.IronLua.LuaTable)
					x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				while (x is object[])
				{
					var options = (object[])x;
					x = options.PickOne();
					if (x is Neo.IronLua.LuaTable)
						x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				}
				NoxicoGame.AddMessage(ApplyMemory(x.ToString()).Viewpoint(actor, target), y);
			});
			env.Stop = new Action(() =>
			{ 
				actor.RemoveAll("havingsex");
				target.RemoveAll("havingsex");
			});
			env.Roll = new Func<object, object, bool>((x, y) =>
			{
				float a, b;
				if (!float.TryParse(x.ToString(), out a))
				{
					if (Character.StatNames.Contains(x.ToString().ToLowerInvariant()))
						a = actor.GetStat(x.ToString());
					else
						a = actor.GetSkillLevel(x.ToString());
				}
				if (!float.TryParse(y.ToString(), out b))
				{
					if (Character.StatNames.Contains(x.ToString().ToLowerInvariant()))
						b = actor.GetStat(x.ToString());
					else
						b = target.GetSkillLevel(y.ToString());
				}
				return (a >= b);
			});

			// Okay, Sparky. What I did was, I put all the error handling in Lua.cs, with a Run method.
			// Instead of worrying about presentation, it just uses a standard WinForms MessageBox.
			// After all, the game's already in a broken state by now.

			var msg = env.Message;
			env.Message = new Action<object, object>((x, y) =>
			{
				if (x is Neo.IronLua.LuaTable)
					x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				while (x is object[])
				{
					var options = (object[])x;
					x = options.PickOne();
					if (x is Neo.IronLua.LuaTable)
						x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				}
				NoxicoGame.AddMessage(ApplyMemory(x.ToString()).Viewpoint(actor, target), y);
			});

			Lua.Run(script, env);

			env.Message = msg;
			/*
			try
			{
				// really should just compile once at startup but we're just testing the debugger trace
				// anyway here's how you'd do it.
				//LuaChunk chunk = env.Lua.CompileChunk(script, "lol.lua", new LuaStackTraceDebugger());
				//env.DoChunk(chunk, "lol.lua");
				env.DoChunk(script, "lol.lua");
			}
			catch (Neo.IronLua.LuaParseException lpe)
			{
				string complain = String.Format("Exception: {0} line {1} col {2},\r\n",
					lpe.Message, lpe.Line, lpe.Column);

				LuaExceptionData lex = LuaExceptionData.GetData(lpe);
				foreach (LuaStackFrame lsf in lex)
				{
					complain += String.Format("StackTrace: {0} line {1} col {2},\r\n",
						 lsf.MethodName, lsf.LineNumber, lsf.ColumnNumber);
				}

				var paused = true;
				MessageBox.ScriptPauseHandler = () => paused = false;
				MessageBox.Notice(complain);
				while (paused)
				{
					NoxicoGame.Me.Update();
					System.Windows.Forms.Application.DoEvents();
				}
				// kawa! things get REALLY BROKEN at this point but at least you got a MessageBox -- sparks
			}
			*/
		}

		private static string ApplyMemory(string text)
		{
			if (text.IsBlank())
				return string.Empty;
			for (var i = 0; i < memory.Length; i++)
				text = text.Replace(string.Format("[{0}]", i), memory[i].OrEmpty());
			return text;
		}
	}

	public partial class Character
	{
		private Character sexPartner;

		public bool Restrained()
		{
			return HasSexFlag("restrained");
		}
		
		public bool Restraining()
		{
			return HasSexFlag("restraining");
		}
		
		public bool HasNipples()
		{
			var boobs = this.GetToken("breasts");
			if (boobs == null)
				return false;
			if (boobs.HasToken("nipples") && boobs.GetToken("nipples").Value >= 1)
					return true;
			return false;
		}
		
		public bool HasBreasts()
		{
			if (this.GetBreastSize() < 0.2)
				return false;
			return true;
		}
		
		public float Raise(string stat, float by)
		{
			return ChangeStat(stat, by);
		}
		
		public Token AddToken(string name, object value)
		{
			var t = new Token(name);
			if (value != null)
			{
				if (value is double || value is float)
					t.Value = (float)value;
				else if (value is int)
					t.Value = (float)((int)value);
				else
					t.Text = value.ToString();
			}
			AddToken(t);
			return t;
		}
		
		public Token AddSexFlag(string name)
		{
			var havingSex = GetToken("havingsex");
			return havingSex.AddToken(name);
		}
		
		public Token RemoveSexFlag(string name)
		{
			var havingSex = GetToken("havingsex");
			return havingSex.RemoveToken(name);
		}
		
		public bool HasSexFlag(string name)
		{
			return Path("havingsex/" + name) != null;
		}
	
		public bool Disrobe(string clothClass, bool tear)
		{
			InventoryItem cloth = null;
			var slots = clothClass == "top" ? new[] { "cloak", "jacket", "shirt", "undershirt" } : clothClass == "bottom" ? new[] { "pants", "underpants" } : null;
			if (slots == null)
			{
				cloth = GetEquippedItemBySlot(clothClass);
			}
			else
			{
				foreach (var slot in slots)
				{
					cloth = GetEquippedItemBySlot(slot);
					if (cloth != null && cloth.CarriedToken.ContainsKey(this.ID) && cloth.CarriedToken[this.ID].HasToken("torn"))
						continue; //scan along
					if (cloth != null)
						break;
				}
			}

			if (cloth != null)
			{
				if (tear)
					return InventoryItem.TearApart(cloth, this, false);
				else
					return cloth.Unequip(this);
			}
			return false;
		}

		public bool TakeVirginity(string vaginaOrAss)
		{
			var vagina = Tokens.FirstOrDefault(x => x.Name == vaginaOrAss && x.HasToken("virgin"));
			if (vagina != null)
			{
				vagina.RemoveToken("virgin");
				return true;
			}
			return false;
		}

		public bool EnsureSexPartner()
		{
			if (!this.HasToken("havingsex"))
				return false;
			var havingSex = this.GetToken("havingsex");
			if (sexPartner != null && havingSex.Text != sexPartner.ID)
			{
				Program.WriteLine("SEX: {0} confuses {1} for {2}. What a slut.", this.ID, havingSex.Text, sexPartner);
				sexPartner = null;
			}
			if (sexPartner == null)
			{
				var s = this.BoardChar.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(b => b.Character.ID == havingSex.Text);
				if (s != null)
					sexPartner = ((BoardChar)s).Character;
			}
			if (sexPartner == null || sexPartner.BoardChar.DistanceFrom(this.BoardChar) > 1)
			{
				Program.WriteLine("SEX: {0} is supposed to be having sex with {1} but that character isn't here.", this.ID, sexPartner.Name);
				this.RemoveToken("havingsex");
			}
			return true;
		}

		public bool UpdateSex()
		{
			if (!EnsureSexPartner())
				return false;

			if (HasToken("havingsex_initsex"))
			{
				var runinit = SexManager.GetResult("initsex", this, sexPartner);
				SexManager.Apply(runinit, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				RemoveToken("havingsex_initsex");
			}
			
			var everysexturn = SexManager.GetResult("everysexturn", this, sexPartner);
			SexManager.Apply(everysexturn, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));

			if (this.GetStat("pleasure") >= 100)
			{
				var result = SexManager.GetResult("climax", this, sexPartner);
				if (this.HasItemEquipped("orgasm_denial_ring"))
					result = SexManager.GetResult("orgasm_denial_ring", this, sexPartner);
				SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.BoardChar.Energy -= (int)result.GetToken("time").Value;
				return true;
			}

			var possibilities = SexManager.GetPossibilities(this, sexPartner);
			if (this.BoardChar is Player)
			{
				ActionList.Show(string.Empty, this.BoardChar.XPosition, this.BoardChar.YPosition, possibilities,
					() =>
					{
						var answer = ActionList.Answer as Token;
						var action = (answer == null) ? "wait" : answer.Text;
						var result = SexManager.GetResult(action, this, sexPartner);
						SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
						this.BoardChar.Energy -= (int)result.GetToken("time").Value;
					}
				);
			}
			else
			{
				//var keys = possibilities.Keys.Select(p => p as string).ToArray();
				//var choice = Toolkit.PickOne(keys);
				var choice = possibilities.Keys.OfType<Token>().ToList().PickWeighted().Text;
				var result = SexManager.GetResult(choice, this, sexPartner);
				SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.BoardChar.Energy -= (int)result.GetToken("time").Value;
			}

			return true;
		}
	}
}
