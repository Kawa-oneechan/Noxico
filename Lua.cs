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
			Environment = IronLua.CreateEnvironment();
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

			Lua.RunFile("i18n.lua");
			Lua.RunFile("defense.lua");

			//TODO: predefine ALL THE THINGS.
			env.Board = typeof(Board);
			env.BoardChar = typeof(BoardChar);
			env.BoardType = typeof(BoardType);
			env.Character = typeof(Character);
			env.Clutter = typeof(Clutter);
			env.Color = typeof(Color);
			env.Door = typeof(Door);
			env.DroppedItem = typeof(DroppedItem);
			env.Entity = typeof(Entity);
			env.Gender = typeof(Gender);
			env.InventoryItem = typeof(InventoryItem);
			env.MorphReport = typeof(MorphReportLevel);
			env.Mutations = typeof(Mutations);
			env.Random = typeof(Random);
			env.Realms = typeof(Realms);
			env.Stat = typeof(Stat);
			env.SceneSystem = typeof(SceneSystem);
			env.Tile = typeof(Tile);
			env.Warp = typeof(Warp);

			env.PlaySound = new Action<string>(x => NoxicoGame.Sound.PlaySound(x));
			env.Message = new Action<string>(x => NoxicoGame.AddMessage(x));
			env.MessageC = new Action<object, Color>((x, y) => NoxicoGame.AddMessage(x, y));
			env.Titlecase = new Func<string, string>(x => x.Titlecase());

			env.FindTargetBoardByName = new Func<string, int>(x =>
			{
				if (!NoxicoGame.TravelTargets.ContainsValue(x))
					return -1;
				var i = NoxicoGame.TravelTargets.First(b => b.Value == x);
				return i.Key;
			});

			env.MakeBoardTarget = new Action<Board>(board =>
			{
				if (string.IsNullOrWhiteSpace(board.Name))
					throw new Exception("Board must have a name before it can be added to the target list.");
				if (NoxicoGame.TravelTargets.ContainsKey(board.BoardNum))
					return; //throw new Exception("Board is already a travel target.");
				NoxicoGame.TravelTargets.Add(board.BoardNum, board.Name);
			});

			env.GetBoard = new Func<int, Board>(x => NoxicoGame.Me.GetBoard(x));
			env.GetBiomeByName = new Func<string, int>(BiomeData.ByName);

			env.Task = typeof(Task);
			env.TaskType = typeof(TaskType);
			env.Token = typeof(Token);

			env.StartsWithVowel = new Func<string, bool>(x => x.StartsWithVowel());

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
			return Run(Mix.GetString(name), env);
		}
	}
}
