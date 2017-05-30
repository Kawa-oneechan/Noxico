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
		public static LuaGlobal Environment;

		/// <summary>
		/// Sets up a new Lua engine and environment.
		/// </summary>
		/// <returns>Returns the new Lua engine.</returns>
		public static Neo.IronLua.Lua Create()
		{
			IronLua = new Neo.IronLua.Lua();
			Environment = IronLua.CreateEnvironment();
			Ascertain(Environment);
			LuaCache = new Dictionary<int, LuaChunk>();
			return IronLua;
		}

		/// <summary>
		/// Ascertains that the Lua environment has various Noxico-specific types and functions.
		/// </summary>
		/// <param name="env"></param>
		public static void Ascertain(LuaGlobal env = null)
		{
			if (env == null)
				env = Environment;
			if (NoxicoGame.HostForm.Noxico.Player != null)
				env.SetValue("player", NoxicoGame.HostForm.Noxico.Player);
			//TODO: predefine ALL THE THINGS.
			env.RegisterPackage("Board", typeof(Board));
			env.RegisterPackage("BoardChar", typeof(BoardChar));
			env.RegisterPackage("BoardType", typeof(BoardType));
			env.RegisterPackage("Character", typeof(Character));
			env.RegisterPackage("Clutter", typeof(Clutter));
			env.RegisterPackage("Color", typeof(Color));
			env.RegisterPackage("Door", typeof(Door));
			env.RegisterPackage("DroppedItem", typeof(DroppedItem));
			env.RegisterPackage("Entity", typeof(Entity));
			env.RegisterPackage("Gender", typeof(Gender));
			env.RegisterPackage("InventoryItem", typeof(InventoryItem));
			env.RegisterPackage("MorphReport", typeof(MorphReportLevel));
			env.RegisterPackage("Mutations", typeof(Mutations));
			env.RegisterPackage("Random", typeof(Random));
			env.RegisterPackage("Realms", typeof(Realms));
			env.RegisterPackage("Stat", typeof(Stat));
			env.RegisterPackage("SceneSystem", typeof(SceneSystem));
			env.RegisterPackage("Tile", typeof(Tile));
			env.RegisterPackage("Warp", typeof(Warp));
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

			// todo Do we have this chunk cached? If so, use that version.
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

			// todo It failed to compile? Run interpreted and hope for useful information
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
		/// <param name="block">The name of the file with the code to run.</param>
		/// <param name="env">The Lua environment to run it in.</param>
		/// <returns>The script's return value, or False if an error occurred.</returns>
		public static LuaResult RunFile(string name, LuaGlobal env = null)
		{
			return Run(Mix.GetString(name), env);
		}
	}
}
