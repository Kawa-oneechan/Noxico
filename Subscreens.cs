//This file holds UNSORTED subscreens that need to be filtered out into the /subscreens folder.
using System;
using System.Collections.Generic;

namespace Noxico
{
	public static class Subscreens
	{
		public static Stack<SubscreenFunc> PreviousScreen = new Stack<SubscreenFunc>();

		public static bool FirstDraw = true;
		public static bool Redraw = true;

		public static bool UsingMouse = false;
		public static bool Mouse = false;
		public static int MouseX = -1;
		public static int MouseY = -1;
	}
}
