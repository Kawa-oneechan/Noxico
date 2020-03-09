using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neo.IronLua;

namespace Noxico
{
	/// <summary>
	/// A custom wrapper around IronLua.
	/// </summary>
	public static class Lua
	{
		private static Dictionary<int, LuaChunk> LuaCache { get; set; }

		public static Neo.IronLua.Lua IronLua;

		/// <summary>
		/// The default environment. Any invocation of Run or RunFile without an environment will use this one.
		/// </summary>
		public static dynamic Environment;

		/// <summary>
		/// Sets up a new Lua engine and environment.
		/// </summary>
		/// <returns>Returns the new Lua engine.</returns>
		public static Neo.IronLua.Lua Create()
		{
			IronLua = new Neo.IronLua.Lua();
			Environment = IronLua.CreateEnvironment<LuaGlobal>();
			LuaCache = new Dictionary<int, LuaChunk>();
			Ascertain(Environment);
			return IronLua;
		}

		/// <summary>
		/// Ascertains that the Lua environment has various Noxico-specific types and functions.
		/// </summary>
		/// <param name="env"></param>
		public static void Ascertain(dynamic env = null)
		{
			if (env == null)
				env = Environment;

			if (env.player == null && NoxicoGame.HostForm != null && NoxicoGame.Me.Player != null)
				env.player = NoxicoGame.Me.Player;

			if (env.ascertained != null)
				return;

			env.RegisterVPTags = new Action<LuaTable>(t => i18n.RegisterVPTags(t));
			env.dofile = new Func<object, LuaResult>(f => RunFile(f.ToString()));

			//Replace IronLua's printer with our own. Why? Because fuck you.
			env.print = new Action<object[]>(x =>
			{
				if (x.Length == 1 && x[0] is LuaTable)
					Program.WriteLine("Table: {{ " + ((LuaTable)x[0]).Values.Select(v => string.Format("{0} = {1}", v.Key, v.Value is string ? '\"' + v.Value.ToString() + '\"' : v.Value)).Join() + " }}");
				else
					Program.WriteLine(string.Join("\t", x.Select(v => v ?? "nil")));
			});

			RunFile("init.lua");

			//TODO: predefine ALL THE THINGS.
			var env2 = (LuaGlobal)env;
			env2.RegisterPackage("Board", typeof(Board));
			env2.RegisterPackage("BoardChar", typeof(BoardChar));
			env2.RegisterPackage("BoardType", typeof(BoardType));
			env2.RegisterPackage("Character", typeof(Character));
			env2.RegisterPackage("Clutter", typeof(Clutter));
			env2.RegisterPackage("Color", typeof(Color));
			env2.RegisterPackage("Door", typeof(Door));
			env2.RegisterPackage("DroppedItem", typeof(DroppedItem));
			env2.RegisterPackage("Entity", typeof(Entity));
			env2.RegisterPackage("Gender", typeof(Gender));
			env2.RegisterPackage("InventoryItem", typeof(InventoryItem));
			env2.RegisterPackage("MorphReport", typeof(MorphReportLevel));
			env2.RegisterPackage("Mutations", typeof(Mutations));
			env2.RegisterPackage("Random", typeof(Random));
			env2.RegisterPackage("Realms", typeof(Realms));
			//env2.RegisterPackage("Stat", typeof(Stat));
			env2.RegisterPackage("SceneSystem", typeof(SceneSystem));
			env2.RegisterPackage("Tile", typeof(Tile));
			env2.RegisterPackage("Warp", typeof(Warp));
			//env2.RegisterPackage("Task", typeof(Task));
			//env2.RegisterPackage("TaskType", typeof(TaskType));
			env2.RegisterPackage("Token", typeof(Token));
			env2.RegisterPackage("Descriptions", typeof(Descriptions));
			env2.RegisterPackage("i18n", typeof(i18n));
			env2.RegisterPackage("Toolkit", typeof(Toolkit));

			env.PlaySound = new Action<string>(x => NoxicoGame.Sound.PlaySound(x));
			env.Message = new Action<object, object>((x, y) =>
				NoxicoGame.AddMessage(x, y));
			env.Titlecase = new Func<string, string>(x => x.Titlecase());

			env.GetBoard = new Func<int, Board>(x => NoxicoGame.Me.GetBoard(x));
			env.GetBiomeByName = new Func<string, int>(BiomeData.ByName);

			env.StartsWithVowel = new Func<string, bool>(x => x.StartsWithVowel());

			/*
			//Because apparently we can't use the Color type directly anymore?
			//Turns out this was because RegisterPackage is a thing you need for Types.
			env.colors = new LuaTable();
			for (var i = 0; i < 16; i++)
				env.colors[i] = Color.FromCGA(i);
			foreach (var c in new[] {
				"Black", "Silver", "Gray", "White", "Maroon", "Red",
				"Purple", "Fuchsia", "Green", "Lime", "Olive", "Yellow",
				"Navy", "Blue", "Teal", "Aqua", "Brown", "Orange", "DarkGray"
			})
				env.colors[c] = Color.FromName(c);
			env.colors.Get = new Func<object, Color>(x =>
			{
				if (x is string) return Color.FromName((string)x);
				if (x is int) return Color.FromArgb((int)x);
				return Color.Black;
			});
			*/


			env.ascertained = true;
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

		/// <summary>
		/// Runs a block of Lua code from a string.
		/// </summary>
		/// <param name="block">The code to run.</param>
		/// <param name="env">The Lua environment to run it in.</param>
		/// <returns>The script's return value, or False if an error occurred.</returns>
		public static LuaResult Run(string block, LuaGlobal env = null)
		{
			if (env == null)
				env = Environment;

			// Do we have this chunk cached? If so, use that version.
			var hash = block.GetHashCode();
			var useCache = false;
			LuaChunk compiledChunk = null;
			if (LuaCache.ContainsKey(hash))
			{
				compiledChunk = LuaCache[hash];
				useCache = true;
			}
			else
			{
				// Attempt to compile and add it to the cache.
				try
				{
					compiledChunk = IronLua.CompileChunk(block, "lol.lua" , null);
					useCache = true;
					LuaCache.Add(hash, compiledChunk);
				}
				catch (LuaException pax)
				{
					//Wrap it up in a normal Exception that our custom handler can then unwrap, so we can show the context in a nice way.
					throw new Exception("Lua parse error while trying to run this chunk:" + System.Environment.NewLine + PrepareParseError(block, pax.Line, pax.Column) + System.Environment.NewLine + pax.Message, pax);
				}
			}

			// TODO: It failed to compile? Run interpreted and hope for useful information
			LuaResult ret = null;
			try
			{
				if (useCache)
					ret = env.DoChunk(compiledChunk);
				else
					ret = env.DoChunk(block, "lol.lua");
			}
			catch (LuaException pax)
			{
				//Wrap it up in a normal Exception that our custom handler can then unwrap, so we can show the context in a nice way.
				throw new Exception("Lua parse error while trying to run this chunk:" + System.Environment.NewLine + PrepareParseError(block, pax.Line, pax.Column) + System.Environment.NewLine + pax.Message, pax);
			}
			return ret;
		}

		/// <summary>
		/// Runs a block of Lua code from a file, which may be archived.
		/// </summary>
		/// <param name="name">The name of the file with the code to run.</param>
		/// <param name="env">The Lua environment to run it in.</param>
		/// <returns>The script's return value, or False if an error occurred.</returns>
		public static LuaResult RunFile(string name, LuaGlobal env = null)
		{
			var ret = Run(Mix.GetString(name), env);
			var files = Mix.GetFilesWithPattern("*-" + name);
			if (files.Length > 0)
				foreach (var extra in files)
					ret = Run(Mix.GetString(extra), env);
			return ret;
		}
	}
}
