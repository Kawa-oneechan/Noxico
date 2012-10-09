using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint;

namespace Noxico
{
	public class Javascript
	{
		public static JintEngine MainMachine { get; set; }

		public static JintEngine Create()
		{
			var jint = new JintEngine();
			Ascertain(jint);
			return jint;
		}

		public static void Ascertain(JintEngine jint, bool asBoardChars = false)
		{
			jint.SetDebugMode(true);
			jint.SetFunction("eval", new Func<string, int>(x => 0));
			if (NoxicoGame.HostForm.Noxico.Player != null)
			{
				if (asBoardChars)
					jint.SetParameter("player", NoxicoGame.HostForm.Noxico.Player);
				else
					jint.SetParameter("player", NoxicoGame.HostForm.Noxico.Player.Character);
			}
			//TODO: predefine more stuff.
			jint.SetParameter("Gender", typeof(Gender));
			jint.SetParameter("MorphReport", typeof(MorphReportLevel));
		}

		public static void Assert()
		{
			var stack = new System.Diagnostics.StackTrace().GetFrames();
			var isFromJint = false;
			var shouldBeFromJint = false;
			var caller = stack[1].GetMethod();
			var callerAttributes = caller.GetCustomAttributes(false);
			var forJS = callerAttributes.FirstOrDefault(x => x is ForJSAttribute) as ForJSAttribute;
			if (forJS == null)
				return;
			if (forJS.Usage == ForJSUsage.Only)
				shouldBeFromJint = true;
			foreach (var frame in stack)
			{
				var m = frame.GetMethod();
				if (m.Name == "jsWrapper")
				{
					isFromJint = true;
					break;
				}
			}
			if (isFromJint && !shouldBeFromJint)
				throw new System.Security.SecurityException("Tried to call " + caller.Name + " from Javascript, but is not allowed.");
			else if (!isFromJint && shouldBeFromJint)
				throw new System.Security.SecurityException("Tried to call " + caller.Name + " from hard code, but is only meant for Javascript use.");
		}
	}

	public enum ForJSUsage
	{
		Only,
		Never,
		Either,
	}

	public class ForJSAttribute : Attribute
	{
		public ForJSUsage Usage { get; private set; }
		public ForJSAttribute()
		{
			Usage = ForJSUsage.Either;
		}
		public ForJSAttribute(ForJSUsage usage)
		{
			Usage = usage;
		}
	}
}
