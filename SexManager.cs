using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public static class SexManager
	{
		//TODO: use XML file to create this.
		/* private static List<SexChoice> choices = new List<SexChoice>()
		{
			new SexChoice()
			{
				ID = "Wait",
				Message = "",
			},
			new SexChoice()
			{
				ID = "Walk away",
				Message = "<A> lets go and gets up.|You release <T> and get up.",
				Actions = new[]
				{
					new SexActionBreak(),
				},
				Limitations = new[]
				{
					"noRestrained",
				}
			},
			new SexChoice()
			{
				ID = "Cuddle",
				Message = "<A> [cuddles up to|rubs <A:HisHerIts> body against] <T>.|You [cuddle up to|rub your body against] <T>.",
				FromHere = new[] { "Kiss", "French kiss", "Pin down", "Take off top", "Take off bottom", "Take off top-u", "Take off top-b" },
				Actions = new[]
				{
					new SexActionTokenIncValue() { ActorNum = 0, Path = "climax", Delta = 1 },
					new SexActionTokenIncValue() { ActorNum = 1, Path = "climax", Delta = 1 },
				},
				Limitations = new[]
				{
					"consentual", "noRestrained", "noRestraining",
				},
			},
			new SexChoice()
			{
				ID = "Pin down",
				Message = "<A> grabs <T>'s arms and holds on tight.|You grab <T>'s arms and hold them tightly in place.",
				FromHere = new[] { "Kiss", "French kiss", "Release" },
				Actions = new[]
				{
					new SexActionTokenAdd() { ActorNum = 0, Path = "havingsex", Name = "restraining" },
					new SexActionTokenAdd() { ActorNum = 1, Path = "havingsex", Name = "restrained" },
				},
				Limitations = new[]
				{
					"noRestrained", "noRestraining",
				},
			},
			new SexChoice()
			{
				ID = "Release",
				Message = "letting go...",
				FromHere = new[] { "Cuddle", "Kiss", "French kiss", "Pin down", "Take off top", "Take off bottom", "Take off top-u", "Take off top-b" },
				Actions = new[]
				{
					new SexActionTokenRemove() { ActorNum = 0, Path = "havingsex", Name = "restraining" },
					new SexActionTokenRemove() { ActorNum = 1, Path = "havingsex", Name = "restrained" },
				},
				Limitations = new[]
				{
					"restraining", "noRestrained",
				}
			},
			new SexChoice()
			{
				ID = "Struggle",
				Message = "letting go...",
				FromHere = new[] { "Cuddle", "Kiss", "French kiss", "Pin down", "Take off top", "Take off bottom", "Take off top-u", "Take off top-b" },
				Actions = new SexAction[]
				{
					new SexActionCompareAgainst() { ActorNum = 0, Stat = Stat.Strength, AgainstNum = 1, AgainstStat = Stat.Strength, Failure = "failed!" },
					new SexActionTokenRemove() { ActorNum = 0, Path = "havingsex", Name = "restrained" },
					new SexActionTokenRemove() { ActorNum = 1, Path = "havingsex", Name = "restraining" },
				},
				Limitations = new[]
				{
					"restrained", "noRestraining",
				}
			},
			new SexChoice()
			{
				ID = "Kiss",
				Message = "kissu~",
				FromHere = new[] { "Kiss", "French kiss", "Pin down", "Take off top", "Take off bottom", "Take off top-u", "Take off top-b" },
				Actions = new[]
				{
					new SexActionTokenIncValue() { ActorNum = 0, Path = "climax", Delta = 2 },
					new SexActionTokenIncValue() { ActorNum = 1, Path = "climax", Delta = 2 },
				},
				Limitations = new[]
				{
					"consentual",
				},
			},
		}; */
		private static List<Token> choices;
		
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
			if (choices == null)
				LoadChoices();
			var actors = new[] { actor, target };
			var possibilities = new List<Token>();
			foreach (var choice in choices.Where(c => c.Name == "choice"))
			{
				if (LimitsOkay(actors, choice))
					possibilities.Add(choice);
			}
			//var possibilities = choices.(c => c.Name == "choice" && LimitsOkay(actors, c));
			var result = new Dictionary<object, string>();
			foreach (var possibility in possibilities)
			{
				if (result.ContainsKey(possibility.Text))
					continue;
				result.Add(possibility.Text, possibility.GetToken("_n").Text);
			}
			return result;
		}

		private static bool LimitsOkay(BoardChar[] actors, Token c)
		{
			var limitations = c.GetToken("limitations");
			if (limitations == null || limitations.Tokens.Count == 0)
				return true; //assume so
			foreach (var limit in limitations.Tokens)
			{
				if (limit.Name == "consentual")
				{
					//TODO
				}
				else if (limit.Name == "not")
				{
					var check = limit.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					var target = int.Parse(check[0]);
					var path = check[1];
					if (actors[target].Character.Path(path) != null)
						return false;
				}
				else if (limit.Name == "yes")
				{
					var check = limit.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
					var target = int.Parse(check[0]);
					var path = check[1];
					if (actors[target].Character.Path(path) == null)
						return false;
				}
				else if (limit.Name == "hastits")
				{
					var target = int.Parse(limit.Text);
					var tits = actors[target].Character.GetBreastSizes();
					if (tits.Length == 0)
						return false;
					if (tits.Average() < 0.2)
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
			var effects = (result.Name == "choice") ? result.GetToken("effects") : result;
			if (effects == null || effects.Tokens.Count == 0)
				return;
			var actors = new[] { actor, target };
			foreach (var effect in effects.Tokens)
			{
				var check = string.IsNullOrWhiteSpace(effect.Text) ? new string[] {} : effect.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				if (effect.Name == "break")
				{
					foreach (var act in actors)
						act.Character.RemoveAll("havingsex");
					return;
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
					var source = effect.GetToken((actor is Player) ? "first" : (target is Player) ? "second" : "third");
					if (source == null)
						continue;
					var message = source.Tokens[Random.Next(source.Tokens.Count)].Text;
					message = SceneSystem.ApplyTokens(message, actor.Character, target.Character);
					writer(message);
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
			}
		}
	}

	public partial class BoardChar
	{
		private BoardChar sexPartner;

		public bool UpdateSex()
		{
			if (!this.Character.HasToken("havingsex"))
				return false;
			var havingSex = this.Character.GetToken("havingsex");
			if (sexPartner == null)
				sexPartner = this.ParentBoard.Entities.OfType<BoardChar>().FirstOrDefault(b => b.ID == havingSex.Text);
			if (sexPartner == null || sexPartner.DistanceFrom(this) > 1)
			{
				Program.WriteLine("SEX: {0} is supposed to be having sex with {1} but that character isn't here.", this.ID, havingSex.Text);
				this.Character.RemoveToken("havingsex");
				return false;
			}

			var possibilities = SexManager.GetPossibilities(this, sexPartner);
			if (this is Player)
			{
				ActionList.Show("bow chicka bow wow", this.XPosition, this.YPosition, possibilities,
					() =>
					{
						var action = ActionList.Answer as string;
						if (action == null)
							action = "Wait";
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

	/*
	public class SexChoice
	{
		public string ID { get; set; }
		public string ListAs { get; set; }
		public string Message { get; set; }
		public string[] FromHere { get; set; }
		public SexAction[] Actions { get; set; }
		public string[] Limitations { get; set; }
		public int Time { get; set; }
		public bool LimitsOkay(BoardChar[] actors)
		{
			if (Limitations == null || Limitations.Length == 0)
				return true;

			if (Limitations.Contains("noRestrained") && actors[0].Character.Path("havingsex/restrained") != null)
				return false;
			if (Limitations.Contains("noRestraining") && actors[0].Character.Path("havingsex/restraining") != null)
				return false;
			if (Limitations.Contains("restrained") && actors[0].Character.Path("havingsex/restrained") == null)
				return false;
			if (Limitations.Contains("restraining") && actors[0].Character.Path("havingsex/restraining") == null)
				return false;

			if (Limitations.Contains("actorNeedsDick") && actors[0].Character.Path("penis") != null)
				return false; //TODO: check for -fitting- dick?
			if (Limitations.Contains("targetNeedsDick") && actors[1].Character.Path("penis") != null)
				return false;

			if (Limitations.Contains("actorNeedsPussy") && actors[0].Character.Path("vagina") != null)
				return false;
			if (Limitations.Contains("targetNeedsPussy") && actors[1].Character.Path("vagina") != null)
				return false;

			if (Limitations.Contains("actorNeedsTits") && actors[0].Character.Path("breastrow") != null)
				return false; //TODO: check for actually non-zero sized breast row!
			if (Limitations.Contains("targetNeedsTits") && actors[1].Character.Path("breastrow") != null)
				return false;

			return true;
		}
	}
	 */

	/*
	public class SexResult
	{
		public string ID { get; set; }
		public string Message { get; set; }
		public string[] FromHere { get; set; }
		public BoardChar[] Actors { get; set; }
		public SexAction[] Actions { get; set; }
		public int Time { get; set; }
		public bool Apply(Action<string> writer)
		{
			//writer(Message);
			writer(ID);
			if (Actions == null)
				return true;
			foreach (var action in Actions)
			{
				if (action is SexActionMessage)
				{
					((SexActionMessage)action).Apply(writer);
					continue;
				}
				if (!action.Apply(Actors))
				{
					if (action is SexActionFailable)
						writer(((SexActionFailable)action).Failure);
					return true;
				}
			}
			return true;
		}
	}

	public class SexAction
	{
		public int ActorNum { get; set; }
		public virtual bool Apply(BoardChar[] actors)
		{
			if (ActorNum >= actors.Length || ActorNum < 0)
				throw new IndexOutOfRangeException(string.Format("Actor index on SexAction out of range -- {0} actors, {1} requested.", actors.Length, ActorNum));
			return true;
		}
	}

	public class SexActionMessage : SexAction
	{
		public string Message { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			throw new Exception("Kawa! Call the other one!");
		}
		public void Apply(Action<string> writer)
		{
			writer(Message);
		}
	}

	public class SexActionBreak : SexAction
	{
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			foreach (var actor in actors)
				actor.Character.RemoveAll("havingsex");
			return true;
		}
	}

	public class SexActionFailable : SexAction
	{
		public string Failure { get; set; }
	}

	public class SexActionCompareAgainst : SexActionFailable
	{
		public int AgainstNum { get; set; }
		public Stat Stat { get; set; }
		public Stat AgainstStat { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			if (actors[ActorNum].Character.GetStat(Stat) > actors[AgainstNum].Character.GetStat(AgainstStat))
				return true;			
			return false;
		}
	}

	public class SexActionTokenIncValue : SexAction
	{
		public string Path { get; set; }
		public float Delta { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			var token = actors[ActorNum].Character.Path(Path);
			if (token == null)
				throw new Exception(string.Format("Tried to apply SexActionTokenIncValue but token {0} not found.", Path));
			token.Value += Delta;
			return true;
		}
	}

	public class SexActionTokenSetValue : SexAction
	{
		public string Path { get; set; }
		public float Value { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			var token = actors[ActorNum].Character.Path(Path);
			if (token == null)
				throw new Exception(string.Format("Tried to apply SexActionTokenSetValue but token {0} not found.", Path));
			token.Value = Value;
			return true;
		}
	}

	public class SexActionTokenSetText : SexAction
	{
		public string Path { get; set; }
		public string Text { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			var token = actors[ActorNum].Character.Path(Path);
			if (token == null)
				throw new Exception(string.Format("Tried to apply SexActionTokenSetText but token {0} not found.", Path));
			token.Text = Text;
			return true;
		}
	}

	public class SexActionTokenAdd : SexAction
	{
		public string Path { get; set; }
		public string Name { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			if (string.IsNullOrWhiteSpace(Path))
				actors[ActorNum].Character.AddToken(Name);
			else
			{
				var token = actors[ActorNum].Character.Path(Path);
				if (token == null)
					throw new Exception(string.Format("Tried to apply SexActionTokenAdd but token {0} not found.", Path));
				if (!token.HasToken(Name))
					token.AddToken(Name);
			}
			return true;
		}
	}

	public class SexActionTokenRemove : SexAction
	{
		public string Path { get; set; }
		public string Name { get; set; }
		public override bool Apply(BoardChar[] actors)
		{
			base.Apply(actors);
			if (string.IsNullOrWhiteSpace(Path))
				actors[ActorNum].Character.RemoveToken(Name);
			else
			{
				var token = actors[ActorNum].Character.Path(Path);
				if (token == null)
					throw new Exception(string.Format("Tried to apply SexActionTokenRemove but token {0} not found.", Path));
				token.RemoveToken(Name);
			}
			return true;
		}
	}
	*/
}
