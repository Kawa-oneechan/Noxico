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
				if (string.IsNullOrWhiteSpace(n.Text))
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
				if (!(actors[0].BoardChar is Player) && choice.HasToken("ai_unlikely"))
					if (Random.NextDouble() < 0.85)
						continue;
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
				result.Add(possibility.Text, i18n.Viewpoint(ApplyMemory(possibility.GetToken("_n").Text), actor, target));
			}
			return result;
		}

		private static bool LimitsOkay(Character[] actors, Token c)
		{
			var filter = c.GetToken("filter");
			if (c.Name == "group")
				filter = c;
			if (filter == null)
				return true;
			var env = Lua.Environment;
			env.SetValue("top", actors[0]);
			env.SetValue("bottom", actors[1]);
			env.SetValue("consentual", !actors[1].HasToken("helpless"));
			env.SetValue("nonconsentual", actors[1].HasToken("helpless"));
			env.SetValue("masturbating", actors[0] == actors[1]);
			env.SetValue("clothing", new Func<Character, string, int, bool>((a, clothClass, s) =>
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
			}));
			return env.DoChunk("return " + filter.Text, "lol.lua").ToBoolean();
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
			var choice = possibilities[Random.Next(possibilities.Count)];
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
			}
			else
			{
				actor.RemoveAll("havingsex");
				actor.AddToken("havingsex", 0, target.ID);
			}
		}

		public static void Apply(Token result, Character actor, Character target, Action<string> writer)
		{
			if (!result.HasToken("effect"))
				return;
			var f = result.GetToken("effect");
			var script = f.Tokens.Count == 1 ? f.Tokens[0].Text : f.Text;
			var env = Lua.Environment;
			env.SetValue("top", actor);
			env.SetValue("bottom", target);
			env.SetValue("consentual", !target.HasToken("helpless"));
			env.SetValue("nonconsentual", target.HasToken("helpless"));
			env.SetValue("masturbating", actor == target);
			env.SetValue("message", new Action<object, Color>((x, y) =>
			{
				if (x is Neo.IronLua.LuaTable)
					x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				while (x is object[])
				{
					var options = (object[])x;
					x = options[Random.Next(options.Length)];
					if (x is Neo.IronLua.LuaTable)
						x = ((Neo.IronLua.LuaTable)x).ArrayList.ToArray();
				}
				NoxicoGame.AddMessage(ApplyMemory(x.ToString()).Viewpoint(actor, target), y);
			}));
			env.SetValue("stop", new Action(() =>
			{ 
				actor.RemoveAll("havingsex");
				target.RemoveAll("havingsex");
			}));
			env.SetValue("roll", new Func<object, object, bool>((x, y) =>
			{
				float a, b;
				if (!float.TryParse(x.ToString(), out a))
				{
					Stat stat;
					if (x is Stat)
						stat = (Stat)x;
					if (Enum.TryParse<Stat>(x.ToString(), true, out stat))
						a = actor.GetStat(stat);
					else
						a = actor.GetSkillLevel(x.ToString());
				}
				if (!float.TryParse(y.ToString(), out b))
				{
					Stat stat;
					if (y is Stat)
						stat = (Stat)y;
					if (Enum.TryParse<Stat>(y.ToString(), true, out stat))
						b = target.GetStat(stat);
					else
						b = target.GetSkillLevel(y.ToString());
				}
				return (a >= b);
			}));
			env.DoChunk(script, "lol.lua");
			/*
			var effects = (result.Name == "choice") ? result.GetToken("effects") : result;
			if (effects == null || effects.Tokens.Count == 0)
				return;
			var actors = new[] { actor, target };
			foreach (var effect in effects.Tokens)
			{
				var check = string.IsNullOrWhiteSpace(effect.Text) ? new string[] { effect.Value.ToString() } : effect.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (effect.Name == "breakitoff")
				{
					foreach (var act in actors)
						act.Character.RemoveAll("havingsex");
					return;
				}
				else if (effect.Name == "end")
				{
					return;
				}
				else if (effect.Name == "stat")
				{
					actors[int.Parse(check[0])].Character.ChangeStat(check[1], float.Parse(check[2]));
				}
				else if (effect.Name == "increase")
				{
					var t = actors[int.Parse(check[0])].Character;
					var p = t.Path(check[1]);
					if (p != null)
						p.Value += float.Parse(check[2]);
				}
				else if (effect.Name == "decrease")
				{
					var t = actors[int.Parse(check[0])].Character;
					var p = t.Path(check[1]);
					if (p != null)
						p.Value -= float.Parse(check[2]);
				}
				else if (effect.Name == "add")
				{
					var t = actors[int.Parse(check[0])].Character.GetToken("havingsex");
					if (!t.HasToken(check[1]))
						t.AddToken(check[1], check.Length > 2 ? float.Parse(check[2]) : 0);
					else if (check.Length > 2)
						t.GetToken(check[1]).Value = float.Parse(check[2]);
				}
				else if (effect.Name == "remove")
				{
					var t = actors[int.Parse(check[0])].Character.GetToken("havingsex");
					if (t.HasToken(check[1]))
						t.RemoveToken(check[1]);
				}
				else if (effect.Name == "add!")
				{
					var t = actors[int.Parse(check[0])].Character;
					if (!t.HasToken(check[1]))
						t.AddToken(check[1], check.Length > 2 ? float.Parse(check[2]) : 0);
					else if (check.Length > 2)
						t.GetToken(check[1]).Value = float.Parse(check[2]);
				}
				else if (effect.Name == "remove!")
				{
					var t = actors[int.Parse(check[0])].Character;
					if (t.HasToken(check[1]))
						t.RemoveToken(check[1]);
				}
				else if (effect.Name == "message")
				{
					var message = effect.Tokens[Random.Next(effect.Tokens.Count)].Text;
					message = i18n.Viewpoint(ApplyMemory(message), actor.Character, target.Character);
					writer(message);
				}
				else if (effect.Name == "has")
				{
					var t = actors[int.Parse(check[0])].Character;
					if (t.Path(check[1]) != null)
						Apply(effect.GetToken("true"), actor, target, writer);
					else if (effect.HasToken("false"))
						Apply(effect.GetToken("false"), actor, target, writer);
				}
				else if (effect.Name == "roll")
				{
					float a, b;
					if (!float.TryParse(check[0], out a))
					{
						Stat stat;
						if (Enum.TryParse<Stat>(check[0], true, out stat))
							a = actors[0].Character.GetStat(stat);
						else
							a = actors[0].Character.GetSkillLevel(check[0]);
					}
					if (!float.TryParse(check[1], out b))
					{
						Stat stat;
						if (Enum.TryParse<Stat>(check[1], true, out stat))
							b = actors[1].Character.GetStat(stat);
						else
							b = actors[1].Character.GetSkillLevel(check[0]);
					}
					if (a >= b && effect.HasToken("win"))
						Apply(effect.GetToken("win"), actor, target, writer);
					else if (effect.HasToken("lose"))
						Apply(effect.GetToken("lose"), actor, target, writer);
				}
				else if (effect.Name == "disrobe")
				{
					var t = actors[int.Parse(check[0])].Character;
					var clothClass = check[1];
					InventoryItem cloth = null;
					if (clothClass == "top")
					{
						foreach (var slot in new[] { "cloak", "jacket", "shirt", "undershirt" })
						{
							cloth = t.GetEquippedItemBySlot(slot);
							if (cloth != null)
								break;
						}
					}
					else if (clothClass == "bottom")
					{
						foreach (var slot in new[] { "pants", "underpants" })
						{
							cloth = t.GetEquippedItemBySlot(slot);
							if (cloth != null)
								break;
						}
					}
					else
					{
						cloth = t.GetEquippedItemBySlot(clothClass);
					}
					if (cloth != null)
					{
						if (check.Length > 2 && check[2] == "tear")
						{
							InventoryItem.TearApart(cloth, t.GetToken("items").Tokens.First(x => x.Name == cloth.ID && x.HasToken("equipped")));
							Apply(effect.GetToken("success"), actor, target, writer);
						}
						else
						{
							var success = cloth.Unequip(t, cloth.tempToken);
							if (success && effect.HasToken("success"))
								Apply(effect.GetToken("success"), actor, target, writer);
							else if (effect.HasToken("failure"))
								Apply(effect.GetToken("failure"), actor, target, writer);
						}
					}
				}
				else if (effect.Name == "takevirginity")
				{
					var t = actors[int.Parse(check[0])].Character;
					var vagina = t.Tokens.FirstOrDefault(x => x.Name == "vagina" && x.HasToken("virgin"));
					if (vagina != null)
					{
						vagina.RemoveToken("virgin");
						Apply(effect.GetToken("success"), actor, target, writer);
					}
				}
				else if (effect.Name == "fertilize")
				{
					var t = actors[int.Parse(check[0])].Character;
					var t2 = actors[int.Parse(check[1])].Character;
					t.Fertilize(t2);
				}
				else
				{
					Program.WriteLine("** Unknown sex effect {0}.", effect.Name);
				}
			}
			*/
		}

		private static string ApplyMemory(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return string.Empty;
			for (var i = 0; i < memory.Length; i++)
				text = text.Replace("[" + i + "]", memory[i] ?? string.Empty);
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
			foreach (var breastRow in Tokens.Where(t => t.Name == "breastrow"))
				if (breastRow.HasToken("nipples") && breastRow.GetToken("nipples").Value >= 1)
					return true;
			return false;
		}
		public bool HasBreasts()
		{
			var tits = GetBreastSizes();
			if (tits.Length == 0)
				return false;
			if (tits.Average() < 0.2)
				return false;
			return true;
		}
		public float Raise(Stat stat, float by)
		{
			return ChangeStat(stat.ToString().ToLowerInvariant(), by);
		}
		public Token AddToken(string name, object value)
		{
			var t = new Token(name);
			if (value != null)
			{
				if (value is double)
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
					if (cloth != null)
						break;
				}
			}

			if (cloth != null)
			{
				if (tear)
				{
					InventoryItem.TearApart(cloth, GetToken("items").Tokens.First(x => x.Name == cloth.ID && x.HasToken("equipped")));
					return true;
				}
				else
				{
					return cloth.Unequip(this, cloth.tempToken);
				}
			}
			return false;
		}
		public bool Fertilize()
		{
			if (!EnsureSexPartner())
				return false;
			return Fertilize(sexPartner);
		}
		public bool TakeVirginity()
		{
			var vagina = Tokens.FirstOrDefault(x => x.Name == "vagina" && x.HasToken("virgin"));
			if (vagina != null)
			{
				vagina.RemoveToken("virgin");
				return true;
			}
			return false;
		}
		public IEnumerable<Token> GetPenises()
		{
			return GetAll("penis");
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

			if (this.GetStat(Stat.Climax) >= 100)
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
						var action = ActionList.Answer as string;
						if (action == null)
							action = "wait";
						var result = SexManager.GetResult(action, this, sexPartner);
						SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
						this.BoardChar.Energy -= (int)result.GetToken("time").Value;
					}
				);
			}
			else
			{
				var keys = possibilities.Keys.Select(p => p as string).ToArray();
				var choice = Toolkit.PickOne(keys);
				var result = SexManager.GetResult(choice, this, sexPartner);
				SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.BoardChar.Energy -= (int)result.GetToken("time").Value;
			}

			return true;
		}
	}
}
