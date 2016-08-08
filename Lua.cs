using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Noxico
{
	class Lua
	{
		public static Neo.IronLua.Lua IronLua;
		public static Neo.IronLua.LuaGlobal Environment;

		public static Neo.IronLua.Lua Create()
		{
			IronLua = new Neo.IronLua.Lua();
			Environment = IronLua.CreateEnvironment();
			Ascertain(Environment);
			return IronLua;
		}

		public static void Ascertain(Neo.IronLua.LuaGlobal env = null)
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
			env.SetValue("message", new Action<string>(x => NoxicoGame.AddMessage(x)));
		}
	}
}
