using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace Noxico
{
	[Obsolete("Use the Motor.Sexytimes enum value instead.", true)]
	public class Sexytimes : Entity
	{
		public SexyTimes.ISexState State;
		public BoardChar[] Participants;
		public List<string> Log;

		private int animationTimer = -1;
		private int participantShown = -1;

		public Sexytimes()
		{
			this.AsciiChar = (char)3;
			this.ForegroundColor = Color.Red;
			this.BackgroundColor = Color.Brown;
			this.Blocking = true;
			Log = new List<string>();
		}

		public string GetParticipants(bool fullName = false)
		{
			if (fullName)
				return Participants[0].Character.GetName() + " and " + Participants[1].Character.GetName();
			else
				return Participants[0].Character.Name + " and " + Participants[1].Character.Name;
		}

		public void Describe(string occurance)
		{
			Log.Add(occurance);
			var player = NoxicoGame.HostForm.Noxico.Player;
			if (player != null && player.DistanceFrom(this) < 3)
			{
				//player.ParentBoard.Message = occurance;
				//player.ParentBoard.MessageTimer = 50;
			}
		}

		public override void Update()
		{
			//var player = NoxicoGame.HostForm.Noxico.CurrentBoard.Entities.OfType<Player>().First();
			//if (player != null)
			//	NoxicoGame.HostForm.Write(player.DistanceFrom(this).ToString(), 7, 0, 1, 1);
			animationTimer++;
			if (animationTimer % 20 == 10)
			{
				participantShown++;
				if (participantShown >= Participants.Length)
					participantShown = 0;
				ShowParticipant(participantShown);
			}
			else if (animationTimer % 20 == 0)
			{
				ShowParticipant(-1);
				State.Update();
				var branches = State.GetBranches();
				if (branches.Count > 0)
				{
					var branch = branches[Toolkit.Rand.Next(branches.Count)];
					if (branch != State.GetType())
					{
						var newState = branch.GetConstructor(new[] { typeof(Sexytimes) }).Invoke(new[] { this });
						State = (SexyTimes.ISexState)newState;
					}
				}
			}
		}

		private void ShowParticipant(int i)
		{
			if (i == -1)
			{
				this.AsciiChar = (char)3;
				this.ForegroundColor = Color.Red;
				this.BackgroundColor = Color.Brown;
				return;
			}
			var shown = Participants[i];
			this.AsciiChar = shown.AsciiChar;
			this.ForegroundColor = shown.ForegroundColor;
			this.BackgroundColor = shown.BackgroundColor;
		}
	}
}

namespace SexyTimes
{
	public interface ISexState
	{
		void Update();
		List<Type> GetBranches();
	}

	[Obsolete("Use the Motor.Sexytimes enum value instead.", true)]
	public class InitialMakeouts : ISexState
	{
		private Noxico.Sexytimes host;
		private int progress = 0;
		private Noxico.Character one, two;

		public InitialMakeouts()
		{
		}

		public InitialMakeouts(Noxico.Sexytimes host)
		{
			this.host = host;
			one = host.Participants[0].Character;
			two = host.Participants[1].Character;
		}

		void ISexState.Update()
		{
			progress++;
			if (progress == 1)
				host.Describe("<c12>" + host.GetParticipants() + " have started making out.");
			if (one.GetToken("stimulation").Value < 20 || two.GetToken("stimulation").Value < 20)
			{
				one.GetToken("stimulation").Value += Noxico.Toolkit.Rand.Next(1, 5);
				two.GetToken("stimulation").Value += Noxico.Toolkit.Rand.Next(1, 5);
			}
		}

		List<Type> ISexState.GetBranches()
		{
			var branches = new List<Type>();
			branches.Add(typeof(InitialMakeouts));
			if (one.GetToken("stimulation").Value >= 20 && two.GetToken("stimulation").Value >= 20)
				branches.Add(typeof(HeavyPetting));
			return branches;
		}
	}

	[Obsolete("Use the Motor.Sexytimes enum value instead.", true)]
	public class HeavyPetting : ISexState
	{
		private Noxico.Sexytimes host;
		private int progress = 0;

		public HeavyPetting()
		{
		}

		public HeavyPetting(Noxico.Sexytimes host)
		{
			this.host = host;
		}

		void ISexState.Update()
		{
			progress++;
			if (progress == 1)
				host.Describe("<c11>" + host.GetParticipants() + " have started touching eachother.");
		}

		List<Type> ISexState.GetBranches()
		{
			return new List<Type>() { };
		}
	}
}
