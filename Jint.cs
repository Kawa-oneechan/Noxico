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
