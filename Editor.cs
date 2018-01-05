using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Linq;
using System.Collections.Generic;

namespace Noxico
{
#if DEBUG
	public class Editor : Form
	{
		private ComboBox combo;
		private TabControl tabs;

		public Editor()
		{
			Text = "Noxico Debug Editor 0.5";
			ClientSize = new Size(388, 442);
			tabs = new TabControl();
			tabs.Dock = DockStyle.Fill;
			Controls.Add(tabs);
			combo = new ComboBox();
			combo.DropDownStyle = ComboBoxStyle.DropDownList;
			combo.Dock = DockStyle.Top;
			combo.SelectedIndexChanged += new EventHandler(combo_SelectedIndexChanged);
			Controls.Add(combo);
		}

		public void LoadBoard(Board board)
		{
			combo.Items.Clear();
			combo.Items.AddRange(board.Entities.ToArray());
			combo.Items.Add(board);
			combo.SelectedIndex = 0;
			ShowDialog();
		}

		private void combo_SelectedIndexChanged(object sender, EventArgs e)
		{
			tabs.TabPages.Clear();
			AddTab(combo.SelectedItem);
			combo.Select();
		}

		private void AddTab(object o)
		{
			var page = new TabPage(o.GetType().ToString());
			var prop = new PropertyGrid();
			prop.Dock = DockStyle.Fill;
			prop.SelectedObject = o;
			prop.ToolbarVisible = false;
			prop.HelpVisible = false;
			prop.PropertySort = PropertySort.CategorizedAlphabetical;
			page.Controls.Add(prop);
			tabs.TabPages.Add(page);
			if (o is BoardChar)
				AddTab(((BoardChar)o).Character);
			else if (o is Container)
				AddTab(((Container)o).Token);
			else if (o is DroppedItem)
				AddTab(((DroppedItem)o).Token);
		}
	}

	public class GlyphSelectorControl : UserControl
	{
		private Brush highlight;
		public int Value { get; set; }
		public IWindowsFormsEditorService EdSvc { get; set; }

		public GlyphSelectorControl()
		{
			GlyphSelector.Init();
			highlight = new SolidBrush(Color.FromArgb(128, 64, 64, 255));
			this.ClientSize = new Size(256, 512);
			//this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			Paint += new PaintEventHandler(GlyphSelectorForm_Paint);
			MouseUp += new MouseEventHandler(GlyphSelectorForm_MouseUp);
		}

		void GlyphSelectorForm_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawImage(GlyphSelector.Sheet, 0, 0, 256, 512);
			var val = Value - 32;
			e.Graphics.FillRectangle(highlight, (val % 32) * 8, (val / 32) * 16, 8, 16);
			e.Graphics.FillRectangle(highlight, 0, (val / 32) * 16, 256, 16);
			e.Graphics.FillRectangle(highlight, (val % 32) * 8, 0, 8, 512);
		}

		void GlyphSelectorForm_MouseUp(object sender, MouseEventArgs e)
		{
			var x = e.X / 8;
			var y = e.Y / 16;
			if (x < 0 || y < 0 || x >= 32 || y >= 32)
				return;
			Value = ((y * 32) + x) + 32;
			EdSvc.CloseDropDown(); //Close();
		}
	}

	public class GlyphSelector : UITypeEditor
	{
		public static Bitmap Sheet;
		public static void Init()
		{
			if (Sheet == null)
				Sheet = Mix.GetBitmap("fonts\\8x16-bold.png");
		}

		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (context != null && context.Instance != null && provider != null)
			{
				var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (edSvc != null)
				{
					var editor = new GlyphSelectorControl();
					editor.Value = (int)Convert.ChangeType(value, context.PropertyDescriptor.PropertyType);
					editor.EdSvc = edSvc;
					//edSvc.ShowDialog(editor);
					edSvc.DropDownControl(editor);
					return editor.Value;
				}
			}
			return value;
		}

		public override void PaintValue(PaintValueEventArgs e)
		{
			Init();
			var val = (int)e.Value - 32;
			var dest = e.Bounds;
			var src = new System.Drawing.Rectangle((val % 32) * 8, (val / 32) * 16, 8, 16);
			e.Graphics.DrawImage(GlyphSelector.Sheet, dest, src, GraphicsUnit.Pixel);
		}

		public override bool GetPaintValueSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.DropDown;
		}
	}

	public class TokenEditorForm : Form
	{
		public List<Token> Value { get; set; }
		private TokenCarrier carrier;
		private TextBox textBox;

		public TokenEditorForm()
		{
			this.Text = "Token editor";
			this.ClientSize = new Size(512, 512);

			textBox = new TextBox();
			textBox.Multiline = true;
			textBox.Dock = DockStyle.Fill;
			textBox.AcceptsTab = true;
			textBox.ScrollBars = ScrollBars.Both;
			Controls.Add(textBox);

			Load += new EventHandler(TokenEditorForm_Load);
			FormClosed += new FormClosedEventHandler(TokenEditorForm_FormClosed);
		}

		void TokenEditorForm_Load(object sender, EventArgs e)
		{
			carrier = new TokenCarrier();
			textBox.Text = carrier.DumpTokens(Value, 0);
			textBox.SelectionStart = textBox.SelectionLength = 0;
		}

		void TokenEditorForm_FormClosed(object sender, FormClosedEventArgs e)
		{
			carrier.Tokenize(textBox.Text);
			Value = carrier.Tokens;
		}
	}

	public class TokenEditor : UITypeEditor
	{
		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (context != null && context.Instance != null && provider != null)
			{
				var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (edSvc != null)
				{
					var editor = new TokenEditorForm();
					var tokens = value as List<Token>;
					editor.Value = tokens;
					edSvc.ShowDialog(editor);
					//return editor.Value;

					tokens.Clear();
					tokens.AddRange(editor.Value);

					value = tokens;
				}
			}
			return value;
		}

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}
	}

	public class ColorEditorControl : UserControl
	{
		public Color Value { get; set; }
		public IWindowsFormsEditorService EdSvc { get; set; }
		private static List<Tuple<string, SolidBrush>> colors;
		private TextBox hexField;
		private ListBox knownList;

		public ColorEditorControl(Color value)
		{
			Value = value;

			hexField = new TextBox();
			knownList = new ListBox();

			hexField.Text = value.ToHex();
			hexField.Dock = DockStyle.Top;

			if (colors == null)
			{
				colors = new List<Tuple<string, SolidBrush>>();
				var knownColors = Mix.GetTokenTree("knowncolors.tml", true);
				foreach (var knownColor in knownColors)
				{
					var name = knownColor.Name;
					var brush = new SolidBrush(System.Drawing.Color.FromArgb((int)((long)knownColor.Value | 0xFF000000)));
					colors.Add(new Tuple<string, SolidBrush>(name, brush));
				}
			}
			knownList.Items.AddRange(colors.ToArray());

			for (var i = 0; i < colors.Count; i++)
			{
				var colorHere = colors[i].Item2.Color.ToArgb();
				if ((colorHere & 0xFFFFFF) == (value.ArgbValue & 0xFFFFFF))
				{
					knownList.SelectedIndex = i;
					break;
				}
			}

			knownList.DrawMode = DrawMode.OwnerDrawFixed;
			knownList.DrawItem += new DrawItemEventHandler(knownList_DrawItem);
			knownList.SelectedIndexChanged += new EventHandler(knownList_SelectedIndexChanged);
			knownList.Dock = DockStyle.Fill;

			Controls.Add(knownList);
			Controls.Add(hexField);
		}

		void knownList_SelectedIndexChanged(object sender, EventArgs e)
		{
			var color = ((Tuple<string, SolidBrush>)knownList.SelectedItem).Item2.Color.ToArgb();
			var foo = color.ToString("X");
			hexField.Text = "#" + foo.Substring(foo.Length - 8);
			Value = Color.FromArgb(color);
		}

		void knownList_DrawItem(object sender, DrawItemEventArgs e)
		{
			var item = (Tuple<string, SolidBrush>)knownList.Items[e.Index];
			var name = item.Item1;
			var brush = item.Item2;
			var rect = new System.Drawing.Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Height - 2, e.Bounds.Height - 2);
			e.DrawBackground();
			e.Graphics.FillRectangle(brush, rect);
			e.Graphics.DrawRectangle(Pens.Black, rect);
			e.DrawFocusRectangle();
		}
	}

	public class ColorEditor : UITypeEditor
	{
		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (context != null && context.Instance != null && provider != null)
			{
				var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (edSvc != null)
				{
					var editor = new ColorEditorControl((Color)Convert.ChangeType(value, context.PropertyDescriptor.PropertyType));
					editor.EdSvc = edSvc;
					edSvc.DropDownControl(editor);
					return editor.Value;
				}
			}
			return value;
		}

		public override void PaintValue(PaintValueEventArgs e)
		{
			var val = (Color)e.Value;
			e.Graphics.FillRectangle(new SolidBrush(val), e.Bounds);
		}

		public override bool GetPaintValueSupported(ITypeDescriptorContext context)
		{
			return true;
		}

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.DropDown;
		}
	}
#endif
}
