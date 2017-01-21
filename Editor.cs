using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;
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
			this.ClientSize = new Size(512, 512);
			//this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			Paint += new PaintEventHandler(GlyphSelectorForm_Paint);
			MouseUp += new MouseEventHandler(GlyphSelectorForm_MouseUp);
		}

		void GlyphSelectorForm_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawImage(GlyphSelector.Sheet, 0, 0, 512, 512);
			var val = Value - 32;
			e.Graphics.FillRectangle(highlight, (val % 32) * 16, (val / 32) * 16, 16, 16);
			e.Graphics.FillRectangle(highlight, 0, (val / 32) * 16, 512, 16);
			e.Graphics.FillRectangle(highlight, (val % 32) * 16, 0, 16, 512);
		}

		void GlyphSelectorForm_MouseUp(object sender, MouseEventArgs e)
		{
			var x = e.X / 16;
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
				Sheet = Mix.GetBitmap("fonts\\8x8-bold.png");
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
			var src = new System.Drawing.Rectangle((val % 32) * 8, (val / 32) * 8, 8, 8);
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
#endif
}
