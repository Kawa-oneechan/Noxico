using System;
using System.Collections.Generic;
using System.Linq;
using Keys = System.Windows.Forms.Keys;
using Bitmap = System.Drawing.Bitmap;

namespace Noxico
{
	public static class UIColors
	{
		public static Color WindowBackground { get { return Color.FromArgb(0x282424); } }
		public static Color WindowBorder { get { return Color.FromArgb(0x8A8A8A); } }
		public static Color RegularText { get { return Color.FromArgb(0xDADCDA); } }
		public static Color HighlightText { get { return Color.FromArgb(0xA6A6FA); } }
		public static Color DarkBackground { get { return Color.FromArgb(0x131111); } }
		public static Color LightBackground { get { return Color.FromArgb(0x3C3737); } }
		public static Color SelectedBackground { get { return Color.FromArgb(0x4C4747); } }
		public static Color SelectedBackUnfocused { get { return Color.FromArgb(0x342E2E); } }
		public static Color SelectedText { get { return Color.FromArgb(0xC3C448); } }
		public static Color Unfocused { get { return Color.FromArgb(0xB29967); } }
		public static Color StatusBackground { get { return Color.FromArgb(0x131111); } }
		public static Color StatusForeground { get { return Color.FromArgb(0x6463D8); } }
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

		public virtual void DoEnter()
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

		private int relMode, relLeft, relTop;
		private UIElement relTo;

		public void Move(int left, int top, UIElement relativeTo)
		{
			if (relativeTo == null)
			{
				Left = left;
				Top = top;

				relMode = 0;
			}
			else
			{
				Left = relativeTo.Left + left;
				Top = relativeTo.Top + top;

				relMode = 1;
				relLeft = left;
				relTop = top;
				relTo = relativeTo;
			}
		}

		public void Move(int left, int top)
		{
			Move(left, top, null);
		}

		public void MoveBelow(int left, int top, UIElement relativeTo)
		{
			Left = relativeTo.Left + left;
			Top = relativeTo.Top + relativeTo.Height + top;

			relMode = 2;
			relLeft = left;
			relTop = top;
			relTo = relativeTo;
		}

		public void MoveBeside(int left, int top, UIElement relativeTo)
		{
			Left = relativeTo.Left + relativeTo.Width + left;
			Top = relativeTo.Top + top;

			relMode = 3;
			relLeft = left;
			relTop = top;
			relTo = relativeTo;
		}

		public void ReMove()
		{
			switch (relMode)
			{
				case 0: return;
				case 1: Move(relLeft, relTop, relTo); return;
				case 2: MoveBelow(relLeft, relTop, relTo); return;
				case 3: MoveBeside(relLeft, relTop, relTo); return;
			}
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
			Height = bitmap.Height;
		}

		public override void Draw()
		{
			if (!NoxicoGame.HostForm.IsSquare)
			{
				for (var row = 0; row < Height / 2; row++)
				{
					for (var col = 0; col < Width; col++)
					{
						var colorT = Bitmap.GetPixel(col, (row * 2) + 0);
						var colorB = Bitmap.GetPixel(col, (row * 2) + 1);
						NoxicoGame.HostForm.SetCell(Top + row, Left + col, '\xDF', colorT, colorB);
					}
				}
			}
			else
			{
				for (var row = 0; row < Height; row++)
				{
					for (var col = 0; col < Width; col++)
					{
						var color = Bitmap.GetPixel(col, row);
						NoxicoGame.HostForm.SetCell(Top + row, Left + col, ' ', Color.Black, color);
					}
				}
			}
		}
	}

	public class UIPNGBackground : UIPNG
	{
		public UIPNGBackground(Bitmap bitmap) : base(bitmap)
		{
			var actualBitmap = new Bitmap(Program.Cols, Program.Rows * (NoxicoGame.HostForm.IsSquare ? 1 : 2));
			using (var g = System.Drawing.Graphics.FromImage(actualBitmap))
			{
				g.DrawImage(bitmap, 0, 0, Program.Cols, Program.Rows * (NoxicoGame.HostForm.IsSquare ? 1 : 2));
			}
			Bitmap = actualBitmap;
			Width = Bitmap.Width;
			Height = Bitmap.Height;
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
			Gradient = false;
		}

		public override void Draw()
		{
			var top = (char)0x306 + new string((char)0x2E1, Width - 2) + (char)0x307;
			var line = (char)0x300 + new string(' ', Width - 2) + (char)0x302;
			var bottom = new string((char)0x321, Width - 2); //(char)0x326 + new string((char)0x321, Width - 2) + (char)0x327;
			var caption = Text.Length() > Width - 8 ? Text.Remove(Width - 8) + (char)0x137 : Text;

			if (!Text.IsBlank())
			{
				NoxicoGame.HostForm.Write(new string(' ', Width), Background, Foreground, Top, Left);
				NoxicoGame.HostForm.Write(caption, Title.A > 0 ? Title : Background, Foreground, Top, Left + (Width / 2) - (caption.Length() / 2));
			}
			else
			{
				NoxicoGame.HostForm.Write(top, Foreground, Background, Top, Left);
			}

			var bg = Background;
			if (Gradient)
				bg = bg.Darken(2 * Height);
			for (var i = Top + 1; i < Top + Height - 1; i++)
			{
				NoxicoGame.HostForm.Write(line, Foreground, bg, i, Left);
				if (i == Top + 1 && !Text.IsBlank())
				{
					NoxicoGame.HostForm.SetCell(i, Left, 0x306, Foreground, bg);
					NoxicoGame.HostForm.SetCell(i, Left + Width - 1, 0x307, Foreground, bg);
				}
				if (Gradient)
					bg = bg.Darken(2 * Height);
			}
			//NoxicoGame.HostForm.Write(bottom, Foreground, bg, Top + Height - 1, Left);
			//weird bug causes fancy bottom to render as ____\/ instead of \____/
			NoxicoGame.HostForm.Write(bottom, Foreground, bg, Top + Height - 1, Left + 1);
			NoxicoGame.HostForm.SetCell(Top + Height - 1, Left, 0x326, Foreground, bg);
			NoxicoGame.HostForm.SetCell(Top + Height - 1, Left + Width - 1, 0x327, Foreground, bg);
		}

		public void Center()
		{
			Left = (Program.Cols / 2) - (Width / 2);
			Top = (Program.Rows / 2) - (Height / 2);
		}
	}

	public class UILabel : UIElement
	{
		public override bool TabStop
		{
			get { return false; }
		}
		public bool Darken { get; set; }

		public UILabel(string text)
		{
			Text = text;
			Foreground = UIColors.RegularText;
			Background = Color.Transparent;
			Width = Text.Length;
		}

		public override void Draw()
		{
			NoxicoGame.HostForm.Write(Text, Foreground, Background, Top, Left, Darken);
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
			Width = text.Length();
			Height = 1;
			Foreground = UIColors.SelectedText;
			Background = UIColors.SelectedBackUnfocused;
			Enabled = true;
		}

		public override void Draw()
		{
			for (var i = 0; i < Height; i++)
				NoxicoGame.HostForm.Write(new string(' ', Width), Foreground, UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused, Top + i, Left);
			NoxicoGame.HostForm.Write(Text, UIManager.Highlight == this ? UIColors.SelectedText : UIColors.Unfocused, UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused, Top + (Height / 2), Left + (Width / 2) - (Text.Length / 2));
		}
	}

	public class UIList : UIElement
	{
		public List<string> Items { get; set; }
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
				_index = (value < Items.Count ? (value < 0 ? Items.Count - 1 : value) : 0);
				Text = Items[_index];
				EnsureVisible();
				if (Change != null)
					Change(this, null);
			}
		}
		public int Scroll { get { return scroll; } }

		public override bool TabStop
		{
			get { return true; }
		}

		public UIList()
		{
			Text = string.Empty;
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
					NoxicoGame.HostForm.Write(' ' + Items[i + scroll].PadEffective(Width - 2) + ' ',
						_index == i + scroll ? UIManager.Highlight == this ? UIColors.SelectedText : UIColors.Unfocused : Foreground,
						_index == i + scroll ? UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused : Background,
						 t, l);

			DrawScrollArrows();
		}

		public void DrawQuick()
		{
			if (Items == null || Items.Count == 0)
				return;
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadEffective(Width - 2) + ' ',
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
			var pi = _index;
			_index--;
			if (_index < scroll)
			{
				scroll--;
				NoxicoGame.HostForm.ScrollDown(Top + 1, Top + Height - 1, Left, Left + Width - 1, Background);
			}
			NoxicoGame.HostForm.Write(' ' + Items[pi].PadEffective(Width - 2) + ' ', Foreground, Background, Top + pi - scroll, Left);
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadEffective(Width - 2) + ' ',
				UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText,
				UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused,
				Top + _index - scroll, Left);

			DrawScrollArrows();

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
			var pi = _index;
			_index++;
			if (_index - scroll >= Height)
			{
				scroll++;
				NoxicoGame.HostForm.ScrollUp(Top, Top + Height - 1, Left, Left + Width - 1, Background);
			}
			NoxicoGame.HostForm.Write(' ' + Items[pi].PadEffective(Width - 2) + ' ', Foreground, Background, Top + pi - scroll, Left);
			NoxicoGame.HostForm.Write(' ' + Items[_index].PadEffective(Width - 2) + ' ',
								UIManager.Highlight == this ? UIColors.SelectedText : UIColors.HighlightText,
				UIManager.Highlight == this ? UIColors.SelectedBackground : UIColors.SelectedBackUnfocused,
				Top + _index - scroll, Left);

			DrawScrollArrows();

			Text = Items[_index];
			if (Change != null)
				Change(this, null);
		}

		private void DrawScrollArrows()
		{
			if (scroll > 0)
				NoxicoGame.HostForm.Write("\x1E", UIColors.RegularText, Color.Transparent, Top, Left + Width - 1);
			if (scroll + Height < Items.Count)
				NoxicoGame.HostForm.Write("\x1F", UIColors.RegularText, Color.Transparent, Top + Height - 1, Left + Width - 1);
		}

		public override void DoMouse(int left, int top)
		{
			Index = top + scroll;
			//Draw(); //not needed, broke rendering in inventory and containers.
		}

		public void EnsureVisible()
		{
			if (Height == 0)
				return;
			var top = scroll;
			var bottom = scroll + Height;
			if (_index >= bottom)
				scroll = _index - Height + 1;
			else if (_index < top)
				scroll = _index;
		}
	}

	public class UITextBox : UIElement
	{
		private int caret;
		public bool Numeric = false;

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
				return;
			}
			var skippers = new[] { Keys.ShiftKey, Keys.ControlKey, Keys.Alt };
			if (skippers.Contains(key))
				return;
			var c = NoxicoGame.LastPress;
			if (Numeric && !char.IsDigit(c))
			{
				return;
			}
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
			NoxicoGame.HostForm.Write(Text.PadEffective(Width), UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left);
			if (UIManager.Highlight == this)
				NoxicoGame.HostForm.Cursor = new Point(Left + caret, Top);
			else if (!(UIManager.Highlight is UITextBox))
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
			NoxicoGame.HostForm.Write(Text.PadEffective(Width - 2) + "<cBlack,Gray>\x11\x10", UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left);
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
			Index--;
			Draw();
			if (Change != null)
				Change(this, null);
		}

		public override void DoRight()
		{
			if (_index == Items.Count - 1)
				_index = -1;
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
			NoxicoGame.HostForm.Write("\xDB\xDD", Color.FromName(Text), Background, Top, Left);
			NoxicoGame.HostForm.Write(Text.PadEffective(Width - 4) + "<cBlack,Gray>\x11\x10", UIManager.Highlight == this ? Foreground : UIColors.Unfocused, Background, Top, Left + 2);
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
			var off = "\x13C";
			var on = "\x13D";
			var a = (val == 0 ? on : off) + ' ' + choices[0];
			var b = (val == 1 ? on : off) + ' ' + choices[1];
			var c = a.PadEffective(Width / 2) + b.PadLeft(Width / 2);
			NoxicoGame.HostForm.Write(c, UIManager.Highlight == this ? Foreground : Color.Gray, Background, Top, Left);
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

	public class UIRadioList : UIElement
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
				val = value;
				Draw();
				if (Change != null)
					Change(this, null);
			}
		}
		public bool[] ItemsEnabled { get; set; }

		public UIRadioList() : base()
		{
		}

		public UIRadioList(string[] options)
		{
			choices = options;
			Height = options.Length;
			Foreground = UIColors.HighlightText;
			Background = UIColors.WindowBackground;
			ItemsEnabled = new bool[options.Length];
			for (var i = 0; i < options.Length; i++)
				ItemsEnabled[i] = true;
			Enabled = true;
		}

		public override bool TabStop
		{
			get { return true; }
		}

		public override void Draw()
		{
			var off = "\x13C";
			var on = "\x13D";
			for (var i = 0; i < choices.Length; i++)
				NoxicoGame.HostForm.Write((val == i ? on : off) + ' ' + choices[i], UIManager.Highlight == this ? (ItemsEnabled[i] ? Foreground : Color.Gray) : (ItemsEnabled[i] ? Color.Gray : Color.Silver), Background, Top + i, Left);
		}

		public override void DoUp()
		{
			if (ItemsEnabled.Count(x => x == true) == 1)
				return;
			Value = Math.Max(0, Value - 1);
			if (!ItemsEnabled[Value])
			{
				if (Value == 0)
					DoDown();
				else
					DoUp();
			}
		}

		public override void DoDown()
		{
			if (ItemsEnabled.Count(x => x == true) == 1)
				return;
			Value = Math.Min(Value + 1, choices.Length - 1);
			if (!ItemsEnabled[Value])
			{
				if (Value == choices.Length - 1)
					DoUp();
				else
					DoDown();
			}
		}

		public override void DoMouse(int left, int top)
		{
			if (!ItemsEnabled[top])
				return;
			Value = top;
			Draw();
		}
	}

	public class UIToggle : UIElement
	{
		private bool val;
		public bool Checked
		{
			get
			{
				return val;
			}
			set
			{
				val = value;
				Draw();
				if (Change != null)
					Change(this, null);
			}
		}

		public void Toggle()
		{
			Checked = !Checked;
		}

		public UIToggle(string text) : base()
		{
			Text = text;
			Width = text.Length() + 3;
			Height = 1;
			Left = -1; //prevent checking before positioning from drawing in the corner
			Foreground = UIColors.RegularText;
			Background = UIColors.DarkBackground;
			Enabled = true;
		}

		public override bool TabStop
		{
			get { return true; }
		}

		public override void Draw()
		{
			if (Left == -1) return; //prevent checking before positioning from drawing in the corner
			var off = "\x13C";
			var on = "\x13D";
			var c = (val ? on : off) + ' ' + Text;
			NoxicoGame.HostForm.Write(c, UIManager.Highlight == this ? Foreground : Color.Gray, Background, Top, Left);
		}

		public override void DoEnter()
		{
			Toggle();
			base.DoEnter();
		}

		public override void DoMouse(int left, int top)
		{
			Toggle();
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
				if (NoxicoGame.KeyMap[(Keys)i])
				{
					NoxicoGame.KeyMap[(Keys)i] = false;
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
			if (Elements.Count == 0)
			{
				Program.WriteLine("Warning: UIManager.Draw() called with an empty elements list.");
				return;
			}
			//Take this moment to highlight the first element, if needed.
			if (highlight == null || !highlight.TabStop)
				highlight = Elements.FirstOrDefault(x => x.TabStop && !x.Hidden);
			if (highlight == null) //Still?
				highlight = Elements[0]; //Fuck it.

			Elements.ForEach(x => { if (!x.Hidden) x.Draw(); });
		}

		public static void ReMove()
		{
			foreach (var e in Elements)
				e.ReMove();
		}
	}
}
