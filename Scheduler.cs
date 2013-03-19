using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

namespace Noxico
{
	public enum TaskType
	{
		Wait, Wander, FindAndGoto, AddToken, RemoveToken
	}

	public enum TaskStatus
	{
		Progressing, Complete, Failed
	}

	public class Task
	{
		public Task OnComplete { get; set; }
		public Task OnFail { get; set; }

		public TaskType type { get; protected set; }
		public TaskStatus status { get; set; }
		public Location moveTarget { get; set; }
		public int timer { get; protected set; }
		public Token token { get; protected set; }
		public string param { get; protected set; }

		public Task() { }

		public Task(TaskType type)
		{
			this.type = type;
		}

		public Task(TaskType type, int timer)
		{
			this.type = type;
			this.timer = timer;
		}

		public Task(TaskType type, string param)
		{
			this.type = type;
			this.param = param;
		}

		public Task(TaskType type, Location moveTarget)
		{
			this.type = type;
			this.moveTarget = moveTarget;
		}

		public Task(TaskType type, Token token)
		{
			this.type = type;
			this.token = token;
		}
	}

	public class AddTokenTask : Task
	{
		public AddTokenTask(Token token)
		{
			this.type = TaskType.AddToken;

			this.token = token;
		}
	}

	public class RemoveTokenTask : Task
	{
		public RemoveTokenTask(string param)
		{
			this.type = TaskType.RemoveToken;

			this.param = param;
		}
	}


	public class Scheduler
	{
		private List<Task> TaskList;
		private uint TaskTimer;
		private Token ScheduleToken;
		private Token CurrentActivity, NextActivity;
		private NoxicanDate NextActivityTime;
		private BoardChar entity;
		private string repeatScript, repeatScriptName;

		public static XmlDocument ScheduleDoc = Mix.GetXMLDocument("schedule.xml");

		public Scheduler(BoardChar entity)
		{
			this.entity = entity;
		}

		public Scheduler(BoardChar entity, string type)
		{
			this.entity = entity;
			AddSchedule(type, entity.Character);
		}

		public void RunSchedule()
		{
			if (TaskList == null)
				TaskList = new List<Task>();

			if (ScheduleToken == null)
				ScheduleToken = entity.Character.GetToken("schedule");

			if (CurrentActivity == null)
				InitSchedule();

			UpdateSchedule();

			if (TaskList.Count == 0)
				AddRepeatingTasks(CurrentActivity.Text);

			//Fallback: Check if queue is empty, wait for one tick if it is
			if (TaskList.Count == 0)
				TaskList.Add(new Task(TaskType.Wait, 1));

			// Perform current task
			PerformTask(TaskList[0]);

			// Resolve task status and react
			switch (TaskList[0].status)
			{
				case TaskStatus.Complete:
					TaskTimer = 0;

					if (TaskList[0].OnComplete != null)
						TaskList[0] = TaskList[0].OnComplete;
					else
						TaskList.RemoveAt(0);
					break;

				case TaskStatus.Failed:
					TaskTimer = 0;

					if (TaskList[0].OnFail != null)
						TaskList[0] = TaskList[0].OnFail;
					else
						TaskList.RemoveAt(0);
					break;

				case TaskStatus.Progressing:
				default:
					break;
			}
		}

		/// <summary>
		/// Adds a new task to the task queue.
		/// </summary>
		/// <param name="task">The new task to add to the queue.</param>
		public void AddTask(Task task)
		{
			if (task != null && TaskList != null)
				TaskList.Add(task);
		}

		/// <summary>
		/// Inititalizes CurrentActivity and NextActivity either by retreiving the previous state of the scheduler from Entity.Character's token tree,
		/// or if the entity has no previous information stored, then by determining the current time of day and from there what the current and
		/// next activities should be.
		/// </summary>
		private void InitSchedule()
		{
			var hour = NoxicoGame.InGameTime.Hour;
			var minute = NoxicoGame.InGameTime.Minute;
			var hourC = 0;
			var minC = 0;
			var hourCprev = 0;
			var minCprev = 0;

			if (entity.Character.GetToken("currentActivity").Tokens.Count > 0)
			{
				ExtractCurrentActivity();

				for (var i = 0; i < ScheduleToken.Tokens.Count; i++)
				{
					if (ScheduleToken.Tokens[i].Name == CurrentActivity.Name)
					{
						if (i == ScheduleToken.Tokens.Count - 1)
							NextActivity = ScheduleToken.Tokens[0];
						else
							NextActivity = ScheduleToken.Tokens[i + 1];

						break;
					}
				}
			}
			else
			{
				for (var i = 0; i < ScheduleToken.Tokens.Count; i++)
				{
					hourC = int.Parse(ScheduleToken.Tokens[i].Name.Substring(0, 2));
					minC = int.Parse(ScheduleToken.Tokens[i].Name.Substring(2, 2));


					// Check if the current time is between any sequential activities
					if (60 * hour + minute <= 60 * hourC + minC && 60 * hour + minute >= 60 * hourCprev + minCprev)
					{
						if (i == 0)
							CurrentActivity = ScheduleToken.Tokens[ScheduleToken.Tokens.Count - 1];
						else
							CurrentActivity = ScheduleToken.Tokens[i - 1];

						NextActivity = ScheduleToken.Tokens[i];
						break;
					}
					// Check if the current time is between the last scheduled activity and the first
					else if (i == ScheduleToken.Tokens.Count - 1 && 60 * hour + minute >= 60 * hourC + minC)
					{
						if (i == 0)
							CurrentActivity = ScheduleToken.Tokens[0];
						else
							CurrentActivity = ScheduleToken.Tokens[i];

						if (i == ScheduleToken.Tokens.Count - 1)
							NextActivity = ScheduleToken.Tokens[0];
						else
							NextActivity = ScheduleToken.Tokens[i];
					}

					hourCprev = hourC;
					minCprev = minC;
				}

				SetNextActivityTime(NextActivity);
				RecordCurrentActivity();
			}

			AddScheduledTasks(CurrentActivity.Text, "init");
		}

		/// <summary>
		/// Checks the current time against NextActivityTime to see if the scheduler should change the current activity to the next one.
		/// 
		/// </summary>
		private void UpdateSchedule()
		{
			// Check if it's time to switch to the next task
			if (NoxicoGame.InGameTime >= NextActivityTime)
			{
				// Next task setup
				TaskList.Clear();
				TaskTimer = 0;

				AddScheduledTasks(CurrentActivity.Text, "end");
				AddScheduledTasks(NextActivity.Text, "init");

				CurrentActivity = NextActivity;

				for (var i = 0; i < ScheduleToken.Tokens.Count; i++)
				{
					if (ScheduleToken.Tokens[i].Name == CurrentActivity.Name)
					{
						if (i == ScheduleToken.Tokens.Count - 1)
							NextActivity = ScheduleToken.Tokens[0];
						else
							NextActivity = ScheduleToken.Tokens[i + 1];

						SetNextActivityTime(NextActivity);
						break;
					}
				}

				RecordCurrentActivity();
			}
		}

		private void SetNextActivityTime(Token nextActivity)
		{
			var range = nextActivity.GetToken("range").Text;

			var time = new TimeSpan(int.Parse(nextActivity.Name.Substring(0, 2)),
									int.Parse(nextActivity.Name.Substring(2, 2)),
									0);

			// If the scheduled activity is on the following day, change time to reflect that.
			if (60 * time.Hours + time.Minutes < 60 * NoxicoGame.InGameTime.Hour + NoxicoGame.InGameTime.Minute)
				time += new TimeSpan(1, 0, 0, 0);

			var plus = range.Contains('+');
			var minus = range.Contains('-');

			range = range.Replace("+", "");
			range = range.Replace("-", "");

			var amount = int.Parse(range);
			TimeSpan timediff;

			if (plus && minus)
				timediff = new TimeSpan(0, Random.Next(-amount, amount), 0);
			else if (minus)
				timediff = new TimeSpan(0, Random.Next(-amount, 0), 0);
			else
				timediff = new TimeSpan(0, Random.Next(0, amount), 0);

			time += timediff;

			NextActivityTime = new NoxicanDate(NoxicoGame.InGameTime.Year, NoxicoGame.InGameTime.Month + 1, NoxicoGame.InGameTime.Day + 1);
			NextActivityTime.Add(time);
		}

		private void PerformTask(Task task)
		{
			if (task == null)
				return;

			switch (task.type)
			{
				case TaskType.Wait:
					WaitInPlace(task);
					break;

				case TaskType.Wander:
					WanderInSector(task);
					break;

				case TaskType.AddToken:
					AddTokenToChar(task);
					break;

				case TaskType.RemoveToken:
					RemoveTokenFromChar(task);
					break;

				case TaskType.FindAndGoto:
					GotoAndFind(task);
					break;

				default:

					break;
			}
		}

		private TaskStatus WanderInSector(Task task)
		{
			entity.Movement = Motor.WanderSector;

			if (task.timer != -1)
			{
				TaskTimer++;

				if (TaskTimer >= task.timer)
					task.status = TaskStatus.Complete;
			}

			return task.status;
		}

		private TaskStatus WaitInPlace(Task task)
		{
			entity.Movement = Motor.Stand;

			if (task.timer != -1)
			{
				TaskTimer++;

				if (TaskTimer >= task.timer)
					task.status = TaskStatus.Complete;
			}

			return task.status;
		}

		private TaskStatus AddTokenToChar(Task task)
		{
			if (task.token == null)
				return task.status = TaskStatus.Failed;

			entity.Character.AddToken(task.token);

			return task.status = TaskStatus.Complete;
		}

		private TaskStatus RemoveTokenFromChar(Task task)
		{
			if (String.IsNullOrWhiteSpace(task.param))
				return task.status = TaskStatus.Failed;

			entity.Character.RemoveToken(task.param);

			return task.status = TaskStatus.Complete;
		}

		private TaskStatus GotoAndFind(Task task)
		{
			var target = entity.ParentBoard.Entities.Find(x => x.ID == task.param);

			if (target == null)
				return task.status = TaskStatus.Failed;

			// Assign the target in script if it hasn't been
			if (entity.ScriptPathID != task.param)
				entity.MoveTo(target.XPosition, target.YPosition, target.ID);

			//Check if target has been reached
			if (target.XPosition == entity.XPosition && target.YPosition == entity.YPosition)
			{
				entity.ScriptPathID = null;

				task.status = TaskStatus.Complete;
			}
			else if (entity.ScriptPathTarget.IsLocalMinimum(entity.XPosition, entity.YPosition))
			{
				entity.ScriptPathID = null;

				task.status = TaskStatus.Failed;
			}

			return task.status;
		}

		/// <summary>
		/// Adds tasks from the XML activity definitions by running the javascript code contained in the section.
		/// </summary>
		/// <param name="name">The name of the activity to get the tasks from.</param>
		/// <param name="section">The section of the activity to get tasks from. Valid options are 'init', 'repeat' and 'end'.</param>
		private void AddScheduledTasks(string name, string section)
		{
			var script = ScheduleDoc.DocumentElement.SelectSingleNode("//schedules/activity[@name='" + name + "']/" + section + "/script");

			var text = "";
			if (script != null)
				text = script.ChildNodes.OfType<XmlCDataSection>().FirstOrDefault().Value;

			if (!string.IsNullOrWhiteSpace(text))
				entity.RunScript(text);
		}

		/// <summary>
		/// Works the same as AddScheduledTasks but is modified to work with the 'repeat' section of activities, 
		/// and only retrieves the repeated scripts from XML once. The scripts will be reloaded any time
		/// a new activity name is passed to the function.
		/// </summary>
		/// <param name="name">The name of the activity to run the scripts of.</param>
		private void AddRepeatingTasks(string name)
		{
			if (repeatScriptName != name)
			{
				repeatScriptName = name;

				var script = ScheduleDoc.DocumentElement.SelectSingleNode("//schedules/activity[@name='" + name + "']/repeat/script");

				if (script != null)
					repeatScript = script.ChildNodes.OfType<XmlCDataSection>().FirstOrDefault().Value;
			}

			if (!string.IsNullOrWhiteSpace(repeatScript))
				entity.RunScript(repeatScript);
		}

		private void RecordCurrentActivity()
		{
			var ct = entity.Character.GetToken("currentActivity");
			ct.Tokens.Clear();
			ct.AddToken(CurrentActivity);

			ct.AddToken("nexttime", 0, NextActivityTime.ToBinary().ToString());
		}

		private void ExtractCurrentActivity()
		{
			var ct = entity.Character.GetToken("currentActivity");

			if (ct != null && ct.Tokens.Count == 2)
			{
				CurrentActivity = ct.Tokens[0];
				NextActivityTime = NoxicanDate.FromBinary(long.Parse(ct.GetToken("nexttime").Text));
			}
		}

		public static void AddSchedule(string name, Character character)
		{
			if (character.HasToken("schedule"))
				character.RemoveToken("schedule");

			foreach (var entry in ScheduleDoc.DocumentElement.SelectNodes("//schedules/schedule").OfType<XmlElement>())
			{
				if (entry.GetAttribute("name").Equals(name, StringComparison.OrdinalIgnoreCase))
				{
					var text = entry.ChildNodes.OfType<XmlCDataSection>().FirstOrDefault().Value;

					var schedule = character.AddToken("schedule");

					if (!character.HasToken("currentActivity"))
						character.AddToken("currentActivity");

					schedule.Tokenize(text);

					break;
				}
			}
		}
	}
}
