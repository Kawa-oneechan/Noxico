using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint;

namespace Noxico
{
	public class JavaScript
	{
		public static JintEngine MainMachine { get; set; }

		public static JintEngine Create()
		{
			var jint = new JintEngine();
			Ascertain(jint);
			return jint;
		}

		public static void Ascertain(JintEngine jint)
		{
			jint.SetDebugMode(true);
			jint.SetFunction("eval", new Func<string, int>(x => 0));
			if (NoxicoGame.HostForm.Noxico.Player != null)
				jint.SetParameter("player", NoxicoGame.HostForm.Noxico.Player.Character);
			//TODO: predefine more stuff.
			jint.SetParameter("Gender", typeof(Gender));
			jint.SetParameter("MorphReport", typeof(MorphReportLevel));
			jint.SetParameter("Stat", typeof(Stat));
			jint.SetFunction("titlecase", new Func<string, string>(x => x.Titlecase()));
			jint.SetFunction("message", new Action<string>(x => NoxicoGame.AddMessage(x)));
		}
	}
}
