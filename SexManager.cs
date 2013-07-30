using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	public static class SexManager
	{
		//TODO: use XML file to create this.
		private static List<SexChoice> choices = new List<SexChoice>()
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
		};

		/// <summary>
		/// Returns a map of possible actions for a participant to pick from.
		/// </summary>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>Possible actions by ID to pass to GetResult</returns>
		public static Dictionary<object, string> GetPossibilities(BoardChar actor, BoardChar target)
		{
			var actors = new[] { actor, target };
			var possibilities = choices.Where(c => c.LimitsOkay(actors));
			var result = new Dictionary<object, string>();
			foreach (var possibility in possibilities)
			{
				if (result.ContainsKey(possibility.ID))
					continue;
				result.Add(possibility.ID, string.IsNullOrWhiteSpace(possibility.ListAs) ? possibility.ID : possibility.ListAs);
			}
			return result;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="id">A sex action</param>
		/// <param name="actor">The participating actor</param>
		/// <param name="target">The target of the participant's affection</param>
		/// <returns>A SexResult object encoding the results of the action</returns>
		public static SexResult GetResult(string id, BoardChar actor, BoardChar target)
		{
			var actors = new[] { actor, target };
			var possibilities = choices.Where(c => c.ID == id && c.LimitsOkay(actors)).ToArray();
			if (possibilities.Length == 0)
				throw new NullReferenceException(string.Format("Could not find a SexChoice named \"{0}\".", id));
			var choice = possibilities[Random.Next(possibilities.Length)];
			var result = new SexResult()
			{
				ID = choice.ID,
				Message = choice.Message,
				Actors = actors,
				Actions = choice.Actions,
				Time = choice.Time == 0 ? 1000 : choice.Time,
			};
			return result;
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
						result.Apply(new Action<string>(x => NoxicoGame.AddMessage(x)));
						this.Energy -= result.Time;
					}
				);
			}
			else
			{
				var keys = possibilities.Keys.Select(p => p as string).ToArray();
				var choice = Toolkit.PickOne(keys);
				var result = SexManager.GetResult(choice, this, sexPartner);
				result.Apply(new Action<string>(x => NoxicoGame.AddMessage(x)));
				this.Energy -= result.Time;
			}

			return true;
		}
	}

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
}
