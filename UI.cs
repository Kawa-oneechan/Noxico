using System;
using System.Collections.Generic;
using System.Linq;
using Keys = System.Windows.Forms.Keys;
using Bitmap = System.Drawing.Bitmap;

namespace Noxico
{
	//TODO: allow finer mouse control -- clicking individual list items, < > arrows...

	public static class UIColors
	{
		public static Color WindowBackground { get { return Color.FromArgb(42, 42, 42); } }
		public static Color WindowBorder { get { return Color.FromArgb(109, 109, 109); } }
		public static Color RegularText { get { return Color.FromArgb(220, 220, 204); } }
		public static Color HighlightText { get { return Color.FromArgb(153, 180, 209); } }
		public static Color DarkBackground { get { return Color.FromArgb(22, 22, 22); } }
		public static Color LightBackground { get { return Color.FromArgb(82, 82, 82); } }
		public static Color SelectedBackground { get { return Color.FromArgb(61, 90, 85); } }
		public static Color SelectedBackUnfocused { get { return Color.FromArgb(62, 70, 74); } }
		public static Color SelectedText { get { return Color.FromArgb(204, 220, 144); } }
		public static Color Unfocused { get { return Color.Gray; } }
		public static Color StatusBackground { get { return Color.Black; } }
		public static Color StatusForeground { get { return Color.Silver; } }
	}

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
		public bool Enabled { get; set; }

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

	public class UIPNG : UIElement
	{
		public Bitmap Bitmap { get; set; }

		public override bool TabStop
		{
			get { return false; }
		}

		public UIPNG()
		{
			throw new Exception("Use the other constructor!");
		}

		public UIPNG(Bitmap bitmap)
		{
			Bitmap = bitmap;
			Width = bitmap.Width;
			Height = bitmap.Height / 2;
		}

		public override void Draw()
		{
			for (var row = 0; row < Height; row++)
			{
				for (var col = 0; col < Width; col++)
				{
					var top = Bitmap.GetPixel(col, row * 2);
					var bot = Bitmap.GetPixel(col, (row * 2) + 1);
					NoxicoGame.HostForm.SetCell(Top + row, Left + col, top == bot ? (char)0x20 : (char)0x2580, top, bot);
				}
			}
		}
	}

	public class UIPNGBackground : UIPNG
	{
		public UIPNGBackground(Bitmap bitmap)
			: base(bitmap)
		{
		}

		public override void Draw()
		{
			Left = 0;
			Top = 0;
			base.Draw();
		}
	}

	public class UIWindow : UIElement
	{
		public Color Title { get; set; }
		public bool Gradient { get; set; }

		public override bool TabStop
		{
			get { return false; }
		}

		public UIWindow(string text)
		{
			Text = text;
			Foreground = UIColors.WindowBorder;
			Background = UIColors.WindowBackground;
			Gradient = true;
		}

		public override void Draw()
		{
			var top = (char)0x2554 + new string((char)0x2550, Width - 2) + (char)0x2557;
			var line = (char)0x2551 + new string(' ', Width - 2) + (char)0x2551;
			var bottom = (char)0x255A + new string((char)0x2550, Width - 2) + (char)0x255D;
			var caption = ' ' + (Text.Length > Width - 8 ? Text.Remove(Width - 8) + '\x2026' : Text) + "  ";

			NoxicoGame.HostForm.Write(top, Foreground, Background, Top, Left);
			if (!string.IsNullOrWhiteSpace(Text))
				NoxicoGame.HostForm.Write(caption, Title.A > 0 ? Title : Foreground, Background, Top, Left + (Width / 2) - (caption.Length / 2));

			var bg = Background;
			if (Gradient)
				bg = bg.Darken(2 * Height);
			for (var i = Top + 1; i < Top + Height; i++)
			{
				NoxicoGame.HostForm.Write(line, Foreground, bg, i, Left);
				if (Gradient)
					bg = bg.Darken(2 * Height);
			}
			NoxicoGame.HostForm.Write(bottom, Foreground, bg, Top + Height - 1, Left);
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
			Foreground = UIColors.RegularText;
			Background = Color.Transparent;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text, Foreground, Background, Top, Left);
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
			Enabled = true;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(new string(' ', Width), Foreground, UIManager.Highlight == this ? Color.Silver : Color.Gray, Top, Left);
			NoxicoGame.HostForm.Write(Text, Foreground, UIManager.Highlight == this ? Color.Silver : Color.Gray, Top, Left + (Width / 2) - (Text.Length / 2));
		}
	}

	public class UIList : UIElement
	{
		public List<string> Items { get; private set; }
		private int _index, scroll;
		public int Index
		{
			get
			{
				return _index;
			}
			set
			{
				if (Items == null || Items.Count == 0)
					return;
				_index = value < Items.Count ? value < 0 ? Items.Count - 1 : value : 0;
				Text = Items[_index];
				EnsureVisible();
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
			_index = 0;
			Width = 32;
			Foreground = UIColors.RegularText;
			Background = UIColors.DarkBackground;
			Enabled = true;
		}

		public UIList(string text, EventHandler enter, IEnumerable<string> items, int index = 0)
		{
			Text = text;
			Items = new List<string>();
			Items.AddRange(items);
			Index = index;
			Width = 32;
			Foreground = UIColors.RegularText;
			Background = UIColors.DarkBackground;
			Enabled = true;
		}

		public override void Draw()
		{
			var l = Left;
			var t = Top;
			for (var i = 0; i < Items.Count && i < Height; i++, t++)
				if (i + scroll >= Items.Count)
					NoxicoGame.HostForm.Write("???", Color.Black, Color.Black, t, l);
				else
					NoxicoGame.HostForm.Write(' ' + Items[i + scroll].PadRight(Width - 2) + ' ',
						_index == i + scroll ? UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText : Foreground,
						_index == i + scroll ? UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused : Background,
						 t, l);

		}

		public void DrawQuick()
		{
			if (Items == null || Items.Count == 0)
				return;
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadRight(Width - 2) + ' ',
				UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText,
				UIManager.Highlight == this ? UIColors.WindowBackground : UIColors.SelectedBackUnfocused,
				Top + _index - scroll, Left);
		}

		public override void DoUp()
		{
			if (Items.Count == 0)
				return;
			if (_index == 0)
				return;
			NoxicoGame.Sound.PlaySound("Cursor");
			var pi = _index;
			_index--;
			if (_index < scroll)
			{
				scroll--;
				NoxicoGame.HostForm.ScrollDown(Top, Top + Height, Left, Left + Width - 1, Background);
			}
			NoxicoGame.HostForm.Write(' ' + Items[pi].PadRight(Width - 2) + ' ', Foreground, Background, Top + pi - scroll, Left);
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadRight(Width - 2) + ' ',
				UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText,
				UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused,
				Top + _index - scroll, Left);
			Text = Items[_index];
			if (Change != null)
				Change(this, null);
		}

		public override void DoDown()
		{
			if (Items.Count == 0)
				return;
			if (_index == Items.Count - 1)
				return;
			NoxicoGame.Sound.PlaySound("Cursor");
			var pi = _index;
			_index++;
			if (_index - scroll >= Height)
			{
				scroll++;
				NoxicoGame.HostForm.ScrollUp(Top - 1, Top + Height - 1, Left, Left + Width - 1, Background);
			}
			NoxicoGame.HostForm.Write(' ' + Items[pi].PadRight(Width - 2) + ' ', Foreground, Background, Top + pi - scroll, Left);
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadRight(Width - 2) + ' ',
								UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText,
				UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused,
				Top + _index - scroll, Left);
			Text = Items[_index];
			if (Change != null)
				Change(this, null);
		}

		public override void DoMouse(int left, int top)
		{
			Index = top + scroll;
			Draw();
		}

		public void EnsureVisible()
		{
			if (Height == 0)
				return;
			var top = scroll;
			var bottom = scroll + Height;
			if (_index > bottom)
				scroll = _index - Height + 1;
			else if (_index < top)
				scroll = _index;
		}
	}

	public class UITextBox : UIElement
	{
		public List<string> Items { get; private set; }
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
			Foreground = UIColors.HighlightText;
			Background = UIColors.DarkBackground;
			Enabled = true;
		}

		public void DoKey(Keys key, bool shift)
		{
			if (key == Keys.Back)
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
			NoxicoGame.HostForm.Write(Text.PadRight(Width), UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left);
			if (UIManager.Highlight == this)
				NoxicoGame.HostForm.Cursor = new Point(Left + caret, Top);
			else
				NoxicoGame.HostForm.Cursor = new Point(-1, -1);
				//NoxicoGame.HostForm.SetCell(Top, Left + caret, ' ', UIColors.RegularText, UIColors.SelectedBackground);
		}
	}

	public class UISingleList : UIList
	{
		private int _index;

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
			Items.AddRange(items);
			Index = index;
			Width = 32;
			Height = 1;
			Foreground = UIColors.RegularText;
			Background = UIColors.DarkBackground;
			Enabled = true;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text.PadRight(Width - 2) + "<cBlack,Gray>\u25C4\u25BA", UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left);
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
			if (_index == 0)
				_index = Items.Count;
			NoxicoGame.Sound.PlaySound("Cursor");
			Index--;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoRight()
		{
			if (_index == Items.Count - 1)
				_index = -1;
			NoxicoGame.Sound.PlaySound("Cursor");
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
			NoxicoGame.HostForm.Write("\u2588\u258C", Color.FromName(Text), Background, Top, Left);
			NoxicoGame.HostForm.Write(Text.PadRight(Width - 4) + "<cBlack,Gray>\u25C4\u25BA", UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left + 2);
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
			Foreground = UIColors.HighlightText;
			Background = UIColors.WindowBackground;
			Enabled = true;
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
			NoxicoGame.HostForm.Write(c, UIManager.Highlight == this ? Foreground : Color.Gray, Background, Top, Left);
		}

		public override void DoLeft()
		{
			Value = 0;
			NoxicoGame.Sound.PlaySound("Cursor");
		}

		public override void DoRight()
		{
			Value = 1;
			NoxicoGame.Sound.PlaySound("Cursor");
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

		public static EventHandler HighlightChanged { get; set; }
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
				if (HighlightChanged != null)
					HighlightChanged(null, null);
			}
		}

		public static void Initialize()
		{
			Elements = new List<UIElement>();
			HighlightChanged = null;
			highlight = null;
			NoxicoGame.HostForm.Cursor = new Point(-1, -1);
		}

		public static void CheckKeys()
		{
			if (Subscreens.Mouse)
			{
				Subscreens.Mouse = false;
				var item = Elements.FindLast(x => x.TabStop && x.Enabled && !x.Hidden && Subscreens.MouseX >= x.Left && Subscreens.MouseY >= x.Top && Subscreens.MouseX < x.Left + x.Width && Subscreens.MouseY < x.Top + (x.Height == 0 ? 1 : x.Height));
				if (item != null)
				{
					if (item != highlight)
					{
						var h = highlight;
						highlight = item;
						h.Draw();
						item.Draw();
						if (HighlightChanged != null)
							HighlightChanged(null, null);
					}
					item.DoMouse(Subscreens.MouseX - item.Left, Subscreens.MouseY - item.Top);
					return;
				}
			}

			if (Vista.Triggers != 0)
			{
				var triggers = Vista.Triggers;
				Vista.ReleaseTriggers();
				switch (triggers)
				{
					case XInputButtons.A:
						ProcessKey(Keys.Enter);
						break;
					case XInputButtons.Up:
						ProcessKey(Keys.Up);
						break;
					case XInputButtons.Down:
						ProcessKey(Keys.Down);
						break;
					case XInputButtons.Left:
						ProcessKey(Keys.Left);
						break;
					case XInputButtons.Right:
						ProcessKey(Keys.Right);
						break;
					case XInputButtons.LeftShoulder:
						ProcessKey(Keys.Tab, true);
						break;
					case XInputButtons.RightShoulder:
						ProcessKey(Keys.Tab, false);
						break;
				}
				return;
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
			if (!highlight.Enabled)
				return;
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
#if DEBUG
					NoxicoGame.HostForm.Text = highlight.ToString() + ":\"" + highlight.Text + "\"";
#endif
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
			NoxicoGame.Sound.PlaySound("Cursor");
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
						if (HighlightChanged != null)
							HighlightChanged(null, null);
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
						if (HighlightChanged != null)
							HighlightChanged(null, null);
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
						if (HighlightChanged != null)
							HighlightChanged(null, null);
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
						if (HighlightChanged != null)
							HighlightChanged(null, null);
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
