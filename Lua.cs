﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neo.IronLua;

namespace Noxico
{
	class Lua
	{
		public static Neo.IronLua.Lua IronLua;
		public static LuaGlobal Environment;

		public static Neo.IronLua.Lua Create()
		{
			IronLua = new Neo.IronLua.Lua();
			Environment = IronLua.CreateEnvironment();
			Ascertain(Environment);
			return IronLua;
		}

		public static void Ascertain(LuaGlobal env = null)
		{
			if (env == null)
				env = Environment;
			if (NoxicoGame.HostForm.Noxico.Player != null)
				env.SetValue("player", NoxicoGame.HostForm.Noxico.Player);
			//TODO: predefine more stuff.
			env.RegisterPackage("Gender", typeof(Gender));
			env.RegisterPackage("MorphReport", typeof(MorphReportLevel));
			env.RegisterPackage("Stat", typeof(Stat));
			env.RegisterPackage("Realms", typeof(Realms));
			env.RegisterPackage("Random", typeof(Random));
			env.RegisterPackage("BoardType", typeof(BoardType));
			env.RegisterPackage("Mutations", typeof(Mutations));
			env.RegisterPackage("Character", typeof(Character));
			env.RegisterPackage("BoardChar", typeof(BoardChar));
			env.RegisterPackage("DroppedItem", typeof(DroppedItem));
			env.RegisterPackage("Clutter", typeof(Clutter));
			env.RegisterPackage("Door", typeof(Door));
			env.RegisterPackage("InventoryItem", typeof(InventoryItem));
			env.RegisterPackage("Warp", typeof(Warp));
			env.RegisterPackage("Tile", typeof(Tile));
			env.RegisterPackage("Color", typeof(Color));
			env.SetValue("titlecase", new Func<string, string>(x => x.Titlecase()));
			env.SetValue("message", new Action<object, Color>((x, y) => NoxicoGame.AddMessage(x, y)));
		}

		private static string PrepareParseError(string block, int line, int column)
		{
			var lines = block.Split('\r');
			if (lines.Length == 1)
				return lines[0].Trim() + " <---";
			var first = line - 4;
			var last = line + 4;
			if (first < 0) first = 0;
			if (last > lines.Length) last = lines.Length;
			var ret = new StringBuilder();
			for (var i = first; i < last; i++)
			{
				ret.Append(lines[i].Trim());
				if (i == line - 1)
					ret.Append(" <---");
				ret.AppendLine();
			}
			return ret.ToString();
		}

		public static LuaResult Run(string block, LuaGlobal env = null)
		{
			if (env == null)
				env = Environment;
			LuaResult ret = null;
			try
			{
				ret = env.DoChunk(block, "lol.lua");
				//ret = Lua.Run(block, env); // no kawa don't do that here!
			}
			catch (LuaParseException pax)
			{
				System.Windows.Forms.MessageBox.Show((System.Windows.Forms.Form)NoxicoGame.HostForm, "Parse error while trying to run this chunk:\r\n" + PrepareParseError(block, pax.Line, pax.Column) + "\r\n" + pax.Message, "Lua error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
				return new LuaResult(false);
			}
			return ret;
		}

		public static LuaResult RunFile(string name, LuaGlobal env = null)
		{
			return Run(Mix.GetString(name), env);
		}
	}
}
