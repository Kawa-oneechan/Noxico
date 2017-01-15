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
		
		private static void LoadChoices()
		{
			if (choices != null)
				return;
			choices = Mix.GetTokenTree("sex.tml");
			foreach (var choice in choices.Where(t => t.Name == "choice"))
			{
				var n = choice.GetToken("_n") ?? choice.AddToken("_n");
				if (string.IsNullOrWhiteSpace(n.Text))
					n.Text = choice.Text.Replace('_', ' ').Titlecase();
				if (!choice.HasToken("time"))
					choice.AddToken("time", 1000);
			}
		}

		/// <summary>
		/// Returns a map of possible actions for a participant to pick from.
		/// </summary>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>Possible actions by ID to pass to GetResult</returns>
		public static Dictionary<object, string> GetPossibilities(BoardChar actor, BoardChar target)
		{
			memory = new string[10];
			if (choices == null)
				LoadChoices();
			var actors = new[] { actor, target };
			var possibilities = new List<Token>();
			foreach (var choice in choices.Where(c => c.Name == "choice"))
			{
				if (choice.HasToken("meta"))
					continue;
				if (!(actor is Player) && choice.HasToken("ai_unlikely"))
					if (Random.NextDouble() < 0.75)
						continue;
				if (LimitsOkay(actors, choice))
					possibilities.Add(choice);
			}
			//var possibilities = choices.(c => c.Name == "choice" && LimitsOkay(actors, c));
			var result = new Dictionary<object, string>();
			foreach (var possibility in possibilities)
			{
				if (result.ContainsKey(possibility.Text))
					continue;
				result.Add(possibility.Text, i18n.Viewpoint(ApplyMemory(possibility.GetToken("_n").Text), actor.Character, target.Character));
			}
			return result;
		}

		private static bool LimitsOkay(BoardChar[] actors, Token c)
		{
			var filter = c.GetToken("filter");
			if (filter != null)
			{
				var env = Lua.Environment;
				env.SetValue("top", actors[0]);
				env.SetValue("bottom", actors[1]);
				env.SetValue("consentual", !actors[1].Character.HasToken("helpless"));
				env.SetValue("nonconsentual", actors[1].Character.HasToken("helpless"));
				env.SetValue("masturbating", actors[0] == actors[1]);
				return env.DoChunk("return " + filter.Text, "lol.lua").ToBoolean();
			}

			var limitations = c.GetToken("limitations");
			if (limitations == null || limitations.Tokens.Count == 0)
				return true; //assume so
			foreach (var limit in limitations.Tokens)
			{
				var check = string.IsNullOrWhiteSpace(limit.Text) ? new string[] {} : limit.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (limit.Name == "consentual")
				{
					if (actors[1].Character.HasToken("helpless"))
						return false;
				}
				else if (limit.Name == "nonconsentual")
				{
					if (!actors[1].Character.HasToken("helpless"))
						return false;
				}
				else if (limit.Name == "masturbating")
				{
					if (actors[0] != actors[1])
						return false;
				}
				else if (limit.Name == "item")
				{
					//TODO
				}
				else if (limit.Name == "clothing")
				{
					var t = actors[int.Parse(check[0])].Character;
					var clothClass = check[1];
					InventoryItem cloth = null;
					var haveSomething = false;
					if (clothClass == "top")
					{
						foreach (var slot in new[] { "cloak", "jacket", "shirt", "undershirt" })
						{
							var newCloth = t.GetEquippedItemBySlot(slot);
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
					else if (clothClass == "bottom")
					{
						foreach (var slot in new[] { "pants", "underpants" })
						{
							var newCloth = t.GetEquippedItemBySlot(slot);
							if (newCloth != null)
							{
								cloth = newCloth;
								haveSomething = true;
								break;
							}
						}
					}
					else
					{
						cloth = t.GetEquippedItemBySlot(clothClass);
						if (cloth != null)
							haveSomething = true;
					}
					if (haveSomething)
						memory[int.Parse(check[2])] = cloth.ToString(null, false, false);
					else
						return false;
				}
				else if (limit.Name == "not")
				{
					var target = int.Parse(check[0]);
					var path = check[1];
					if (actors[target].Character.Path(path) != null)
						return false;
				}
				else if (limit.Name == "yes")
				{
					var target = int.Parse(check[0]);
					var path = check[1];
					if (actors[target].Character.Path(path) == null)
						return false;
				}
				else if (limit.Name == "hastits")
				{
					var target = (int)limit.Value;
					var tits = actors[target].Character.GetBreastSizes();
					if (tits.Length == 0)
						return false;
					if (tits.Average() < 0.2)
						return false;
				}
				else if (limit.Name == "hasnipples")
				{
					var target = (int)limit.Value;
					foreach (var breastRow in actors[target].Character.Tokens.Where(t => t.Name == "breastrow"))
					{
						if (breastRow.HasToken("nipples") && breastRow.GetToken("nipples").Value >= 1)
							return true;
					}
					return false;
				}
				else if (limit.Name == "canreach")
				{
					var t = actors[int.Parse(check[0])].Character;
					var item = check[1];
					if (item == "breasts" && !t.CanReachBreasts())
						return false;
					if (item == "crotch" && !t.CanReachCrotch())
						return false;
				}
				//TODO: add capacity checks
			}

			return true;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id">A sex action</param>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>A SexResult object encoding the results of the action</returns>
		public static Token GetResult(string id, BoardChar actor, BoardChar target)
		{
			var actors = new[] { actor, target };
			if (choices == null)
				LoadChoices();
			var possibilities = choices.FindAll(c => c.Text == id && LimitsOkay(actors, c)).ToArray();
			if (possibilities.Length == 0)
				throw new NullReferenceException(string.Format("Could not find a sex choice named \"{0}\".", id));
			var choice = possibilities[Random.Next(possibilities.Length)];
			return choice;
		}

		public static void Engage(BoardChar actor, BoardChar target)
		{
			/*
			if (actor.Character.HasToken("havingsex"))
				throw new Exception(string.Format("Actor ({0}) already having sex.", actor.Character.ToString()));
			if (target.Character.HasToken("havingsex"))
				throw new Exception(string.Format("Target ({0}) already having sex.", target.Character.ToString()));
			*/
			actor.Character.RemoveAll("havingsex");
			target.Character.RemoveAll("havingsex");
			actor.Character.AddToken("havingsex", 0, target.ID);
			target.Character.AddToken("havingsex", 0, actor.ID);
		}

		public static void Apply(Token result, BoardChar actor, BoardChar target, Action<string> writer)
		{
			if (result.HasToken("effect"))
			{
				var f = result.GetToken("effect");
				var script = f.Tokens.Count == 1 ? f.Tokens[0].Text : f.Text;
				var env = Lua.Environment;
				env.SetValue("top", actor);
				env.SetValue("bottom", target);
				env.SetValue("consentual", !target.Character.HasToken("helpless"));
				env.SetValue("nonconsentual", target.Character.HasToken("helpless"));
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
						NoxicoGame.AddMessage(x.ToString().Viewpoint(actor.Character, target.Character), y);
					}));
				env.DoChunk(script, "lol.lua");
				return;
			}

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

	public partial class BoardChar
	{
		private BoardChar sexPartner;

		public bool Restrained()
		{
			return Character.Path("havingsex/restrained") != null;
		}
		public bool Restraining()
		{
			return Character.Path("havingsex/restraining") != null;
		}
		public bool HasNipples()
		{
			foreach (var breastRow in Character.Tokens.Where(t => t.Name == "breastrow"))
				if (breastRow.HasToken("nipples") && breastRow.GetToken("nipples").Value >= 1)
					return true;
			return false;
		}
		public bool CanReachBreasts()
		{
			return Character.CanReachBreasts();
		}
		public float Raise(Stat stat, float by)
		{
			return Character.ChangeStat(stat.ToString().ToLowerInvariant(), by);
		}
		public Token Path(string pathSpec)
		{
			return Character.Path(pathSpec);
		}

		public bool Fertilize()
		{
			if (!EnsureSexPartner())
				return false;
			return Character.Fertilize(sexPartner.Character);
		}

		public bool EnsureSexPartner()
		{
			if (!this.Character.HasToken("havingsex"))
				return false;
			var havingSex = this.Character.GetToken("havingsex");
			if (sexPartner != null && havingSex.Text != sexPartner.ID)
			{
				Program.WriteLine("SEX: {0} confuses {1} for {2}. What a slut.", this.ID, havingSex.Text, sexPartner.Character);
				sexPartner = null;
			}
			if (sexPartner == null)
				sexPartner = this.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(b => b.ID == havingSex.Text);
			if (sexPartner == null || sexPartner.DistanceFrom(this) > 1)
			{
				Program.WriteLine("SEX: {0} is supposed to be having sex with {1} but that character isn't here.", this.ID, sexPartner.Character.Name);
				this.Character.RemoveToken("havingsex");
			}
			return true;
		}

		public bool UpdateSex()
		{
			if (!EnsureSexPartner())
				return false;

			if (this.Character.GetStat(Stat.Climax) >= 100)
			{
				var result = SexManager.GetResult("climax", this, sexPartner);
				if (this.Character.HasItemEquipped("orgasm_denial_ring"))
					result = SexManager.GetResult("orgasm_denial_ring", this, sexPartner);
				SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.Energy -= (int)result.GetToken("time").Value;
				return true;
			}

			var possibilities = SexManager.GetPossibilities(this, sexPartner);
			if (this is Player)
			{
				ActionList.Show(string.Empty, this.XPosition, this.YPosition, possibilities,
					() =>
					{
						var action = ActionList.Answer as string;
						if (action == null)
							action = "wait";
						var result = SexManager.GetResult(action, this, sexPartner);
						SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
						this.Energy -= (int)result.GetToken("time").Value;
					}
				);
			}
			else
			{
				var keys = possibilities.Keys.Select(p => p as string).ToArray();
				var choice = Toolkit.PickOne(keys);
				var result = SexManager.GetResult(choice, this, sexPartner);
				SexManager.Apply(result, this, sexPartner, new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.Energy -= (int)result.GetToken("time").Value;
			}

			return true;
		}
	}
}
