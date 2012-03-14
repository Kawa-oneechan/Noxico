using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;

namespace Noxico
{
	//TODO: allow finer mouse control -- clicking individual list items, < > arrows...

	public abstract class UIElement
	{
		public abstract bool TabStop { get; }
		public EventHandler Enter { get; set; }
		public EventHandler UpArrow { get; set; }
		public EventHandler DownArrow { get; set; }
		public EventHandler LeftArrow { get; set; }
		public EventHandler RightArrow { get; set; }
		public EventHandler Change { get; set; }
		public bool Hidden { get; set; }
		public string Tag { get; set; }
		public string Text { get; set; }
		public int Left { get; set; }
		public int Top { get; set; }
		public int Width { get; set; }
		public int Height { get; set; }
		public Color Foreground { get; set; }
		public Color Background { get; set; }

		public void DoEnter()
		{
			if (Enter != null)
				Enter(this, null);
		}
		public virtual void DoUp()
		{
			if (UpArrow != null)
				UpArrow(this, null);
		}
		public virtual void DoDown()
		{
			if (DownArrow != null)
				DownArrow(this, null);
		}
		public virtual void DoLeft()
		{
			if (LeftArrow != null)
				LeftArrow(this, null);
		}
		public virtual void DoRight()
		{
			if (RightArrow != null)
				RightArrow(this, null);
		}
		public virtual void DoMouse(int left, int top)
		{
			DoEnter();
		}

		public abstract void Draw();
	}

	public class UIPNGBackground : UIElement
	{
		public Bitmap Bitmap { get; set; }

		public override bool TabStop
		{
			get { return false; }
		}

		public UIPNGBackground(Bitmap bitmap)
		{
			Bitmap = bitmap;
		}

		public override void Draw()
		{
			for (var row = 0; row < 25; row++)
			{
				for (var col = 0; col < 80; col++)
				{
					var top = Bitmap.GetPixel(col, row * 2);
					var bot = Bitmap.GetPixel(col, (row * 2) + 1);
					NoxicoGame.HostForm.SetCell(row, col, top == bot ? (char)0x20 : (char)0x2580, top, bot);
				}
			}
		}
	}

	public class UIWindow : UIElement
	{
		public override bool TabStop
		{
			get { return false; }
		}

		public UIWindow(string text)
		{
			Text = text;
			Foreground = Color.Black;
			Background = Color.Silver;
		}

		public override void Draw()
		{
			var top = (char)0x2554 + new string((char)0x2550, Width - 2) + (char)0x2557;
			var line = (char)0x2551 + new string(' ', Width - 2) + (char)0x2551;
			var bottom = (char)0x255A + new string((char)0x2550, Width - 2) + (char)0x255D;
			var caption = (char)0x2561 + " " + Text + ' ' + (char)0x255E;

			NoxicoGame.HostForm.Write(top, Foreground, Background, Left, Top);
			if (!string.IsNullOrWhiteSpace(Text))
				NoxicoGame.HostForm.Write(caption, Foreground, Background, Left + (Width / 2) - (caption.Length / 2), Top);
			for (var i = Top + 1; i < Top + Height; i++)
				NoxicoGame.HostForm.Write(line, Foreground, Background, Left, i);
			NoxicoGame.HostForm.Write(bottom, Foreground, Background, Left, Top + Height - 1);
		}
	}

	public class UILabel : UIElement
	{
		public override bool TabStop
		{
			get { return false; }
		}

		public UILabel(string text)
		{
			Text = text;
			Foreground = Color.Gray;
			Background = Color.Transparent;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text, Foreground, Background, Left, Top);
		}
	}

	public class UIButton : UIElement
	{
		public override bool TabStop
		{
			get { return true; }
		}

		public UIButton(string text, EventHandler enter)
		{
			Text = text;
			Enter = enter;
			Width = text.Length;
			Height = 1;
			Foreground = Color.Black;
			Background = Color.Gray;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(new string(' ', Width), Foreground, UIManager.Highlight == this ? Color.Silver : Color.Gray, Left, Top);
			NoxicoGame.HostForm.Write(Text, Foreground, UIManager.Highlight == this ? Color.Silver : Color.Gray, Left + (Width / 2) - (Text.Length / 2), Top);
		}
	}

	public class UIList : UIElement
	{
		public List<string> Items;
		protected int index, scroll;
		public int Index
		{
			get
			{
				return index;
			}
			set
			{
				index = value < Items.Count ? value : 0;
				Text = Items[index];
				if (Change != null)
					Change(this, null);
			}
		}

		public override bool TabStop
		{
			get { return true; }
		}

		public UIList()
		{
			Text = "";
			Items = new List<string>();
			index = 0;
			Width = 32;
			Foreground = Color.Black;
			Background = Color.White;
		}

		public UIList(string text, EventHandler enter, IEnumerable<string> items, int index = 0)
		{
			Text = text;
			Items = new List<string>();
			Items.AddRange(items);
			Index = index;
		}

		public override void Draw()
		{
			var l = Left;
			var t = Top;
			for (var i = 0; i < Items.Count && i < Height; i++, t++)
				NoxicoGame.HostForm.Write(' ' + Items[i + scroll].PadRight(Width - 2) + ' ',
					index == i + scroll ? UIManager.Highlight == this ? Color.White : Color.White : Foreground,
					index == i + scroll ? UIManager.Highlight == this ? Color.Navy : Color.Gray : Background,
					 l, t);
		}

		public override void DoUp()
		{
			if (index == 0)
				return;
			index--;
			if (index < scroll)
				scroll--;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoDown()
		{
			if (index == Items.Count - 1)
				return;
			index++;
			if (index - scroll >= Height)
				scroll++;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoMouse(int left, int top)
		{
			Index = top + scroll;
			Draw();
		}
	}

	public class UITextBox : UIElement
	{
		private int caret;

		public override bool TabStop
		{
			get { return true; }
		}

		public UITextBox(string text)
		{
			Text = text;
			caret = text.Length;
			Width = text.Length + 2;
			Height = 1;
			Foreground = Color.Black;
			Background = Color.White;
		}

		public void DoKey(Keys key, bool shift)
		{
			if (key == Keys.Escape) //really Backspace
			{
				if (Text.Length > 0 && caret > 0)
				{
					Text = Text.Remove(--caret, 1);
					Draw();
					if (Change != null)
						Change(this, null);
				}
				else
					NoxicoGame.Sound.PlaySound("Push");
				return;
			}
			var skippers = new[] { Keys.ShiftKey, Keys.ControlKey, Keys.Alt };
			if (skippers.Contains(key))
				return;
			var c = NoxicoGame.LastPress;
			if (Text.Length < Width - 1 && !char.IsControl(c))
			{
				Text = Text.Insert(caret++, c.ToString());
				Draw();
				if (Change != null)
					Change(this, null);
			}
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text.PadRight(Width), UIManager.Highlight == this ? Foreground : Color.Gray, Background, Left, Top);
			if (UIManager.Highlight == this)
				NoxicoGame.HostForm.SetCell(Top, Left + caret, ' ', Color.Gray, Color.Silver);
		}
	}

	public class UISingleList : UIList
	{
		public override bool TabStop
		{
			get { return true; }
		}

		public UISingleList() : base()
		{
		}

		public UISingleList(string text, EventHandler enter, IEnumerable<string> items, int index = 0)
		{
			Text = text;
			Items = new List<string>();
			Items.AddRange(items);
			Index = index;
			Width = 32;
			Height = 1;
			Foreground = Color.Black;
			Background = Color.White;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text.PadRight(Width - 2) + "<cBlack,Gray>\u25C4\u25BA", UIManager.Highlight == this ? Foreground : Color.Gray, Background, Left, Top);
		}

		public override void DoUp()
		{
			DoLeft();
		}

		public override void DoDown()
		{
			DoRight();
		}

		public override void DoLeft()
		{
			if (index == 0)
				index = Items.Count;
			Index--;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoRight()
		{
			if (index == Items.Count - 1)
				index = -1;
			Index++;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoMouse(int left, int top)
		{
			if (left == Width - 2)
				DoLeft();
			if (left == Width - 1)
				DoRight();
		}
	}

	public class UIColorList : UISingleList
	{
		public override void Draw()
		{
			NoxicoGame.HostForm.Write("\u2588\u258C", Toolkit.GetColor(Text), Background, Left, Top);
			NoxicoGame.HostForm.Write(Text.PadRight(Width - 4) + "<cBlack,Gray>\u25C4\u25BA", UIManager.Highlight == this ? Foreground : Color.Gray, Background, Left + 2, Top);
		}
	
		public UIColorList() : base()
		{
		}

		public UIColorList(string text, EventHandler enter, IEnumerable<string> items, int index = 0) : base(text, enter, items, index)
		{
		}
}

	public class UIBinary : UIElement
	{
		private string[] choices;
		private int val;
		public int Value
		{
			get
			{
				return val;
			}
			set
			{
				val = value > 0 ? 1 : 0;
				Draw();
				if (Change != null)
					Change(this, null);				
			}
		}

		public UIBinary() : base()
		{
		}

		public UIBinary(string a, string b)
		{
			choices = new[] { a, b };
			Height = 1;
			Foreground = Color.Black;
			Background = Color.Transparent;
		}

		public override bool TabStop
		{
			get { return true; }
		}

		public override void Draw()
		{
			var off = "\u25CC"; // ( )  or []
			var on = "\u25CF"; // (*)  or [#
			var a = (val == 0 ? on : off) + ' ' + choices[0];
			var b = (val == 1 ? on : off) + ' ' + choices[1];
			var c = a.PadRight(Width / 2) + b.PadLeft(Width / 2);
			NoxicoGame.HostForm.Write(c, UIManager.Highlight == this ? Foreground : Color.Gray, Background, Left, Top);
		}

		public override void DoLeft()
		{
			Value = 0;
		}

		public override void DoRight()
		{
			Value = 1;
		}

		public override void DoMouse(int left, int top)
		{
			if (left < Width / 2)
				DoLeft();
			else
				DoRight();
		}
	}

	static class UIManager
	{
		private static UIElement highlight;

		public static List<UIElement> Elements { get; set; }
		public static UIElement Highlight
		{
			get
			{
				return highlight;
			}
			set
			{
				if (Elements.Contains(value))
					highlight = value;
				else
					highlight = Elements[0];
			}
		}

		public static void Initialize()
		{
			Elements = new List<UIElement>();
		}

		public static void CheckKeys()
		{
			if (Subscreens.Mouse)
			{
				Subscreens.Mouse = false;
				var item = Elements.FindLast(x => x.TabStop && !x.Hidden && Subscreens.MouseX >= x.Left && Subscreens.MouseY >= x.Top && Subscreens.MouseX < x.Left + x.Width && Subscreens.MouseY < x.Top + (x.Height == 0 ? 1 : x.Height));
				if (item != null)
				{
					if (item != highlight)
					{
						var h = highlight;
						highlight = item;
						h.Draw();
						item.Draw();
					}
					item.DoMouse(Subscreens.MouseX - item.Left, Subscreens.MouseY - item.Top);
				}
			}

			for (var i = 0; i < 255; i++)
			{
				if (NoxicoGame.KeyMap[i])
				{
					NoxicoGame.KeyMap[i] = false;
					UIManager.ProcessKey((Keys)i, NoxicoGame.Modifiers[0]);
					break;
				}
			}
		}

		public static void ProcessKey(Keys key, bool shift = false)
		{
			switch (key)
			{
				case Keys.Enter:
					highlight.DoEnter();
					break;
				case Keys.Up:
					highlight.DoUp();
					break;
				case Keys.Down:
					highlight.DoDown();
					break;
				case Keys.Left:
					highlight.DoLeft();
					break;
				case Keys.Right:
					highlight.DoRight();
					break;
				case Keys.Tab:
					ProcessTab(shift);
					NoxicoGame.HostForm.Text = highlight.ToString() + ":\"" + highlight.Text + "\"";
					break;
				default:
					if (highlight is UITextBox)
						((UITextBox)highlight).DoKey(key, shift);
					break;
			}
		}

		private static void ProcessTab(bool shift = false)
		{
			var hiIndex = Elements.IndexOf(highlight);
			if (!shift)
			{
				for (var i = hiIndex + 1; i < Elements.Count; i++)
				{
					if (Elements[i].TabStop && !Elements[i].Hidden)
					{
						var oH = highlight;
						highlight = Elements[i];
						oH.Draw();
						highlight.Draw();
						return;
					}
				}
				//Try again from the start
				for (var i = 0; i < hiIndex; i++)
				{
					if (Elements[i].TabStop && !Elements[i].Hidden)
					{
						var oH = highlight;
						highlight = Elements[i];
						oH.Draw();
						highlight.Draw();
						return;
					}
				}
			}
			else
			{
				for (var i = hiIndex - 1; i >= 0; i--)
				{
					if (Elements[i].TabStop && !Elements[i].Hidden)
					{
						var oH = highlight;
						highlight = Elements[i];
						oH.Draw();
						highlight.Draw();
						return;
					}
				}
				//Try again from the <del>start</del> end
				for (var i = Elements.Count - 1; i > hiIndex; i--)
				{
					if (Elements[i].TabStop && !Elements[i].Hidden)
					{
						var oH = highlight;
						highlight = Elements[i];
						oH.Draw();
						highlight.Draw();
						return;
					}
				}
			}
			//Didn't find any? Doesn't matter, had loops.
		}

		public static void Draw()
		{
			//Take this moment to highlight the first element, if needed.
			if (highlight == null || !highlight.TabStop)
				highlight = Elements.FirstOrDefault(x => x.TabStop && !x.Hidden);
			if (highlight == null) //Still?
				highlight = Elements[0]; //Fuck it.

			Elements.ForEach(x => { if (!x.Hidden) x.Draw(); });
		}
	}
}
