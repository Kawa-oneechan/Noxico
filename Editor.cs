using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Design;
using System.Windows.Forms.Design;

namespace Noxico
{
	public partial class Editor : Form
	{
		public Editor()
		{
			InitializeComponent();
		}

		public void LoadBoard(Board board)
		{
			comboBox1.Items.Clear();
			comboBox1.Items.AddRange(board.Entities.ToArray());
			comboBox1.SelectedIndex = 0;
			var host = (Form)NoxicoGame.HostForm;
			ShowDialog();
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
			tabControl1.TabPages.Clear();
			AddTab(comboBox1.SelectedItem);
			comboBox1.Select();
		}

		private void AddTab(object o)
		{
			var page = new TabPage(o.GetType().ToString());
			var prop = new PropertyGrid();
			page.Controls.Add(prop);
			prop.Dock = DockStyle.Fill;
			prop.SelectedObject = o;
			tabControl1.TabPages.Add(page);
			if (o is BoardChar)
				AddTab(((BoardChar)o).Character);
			else if (o is Container)
				AddTab(((Container)o).Token);
			else if (o is DroppedItem)
				AddTab(((DroppedItem)o).Token);
		}
	}

	public class GlyphSelectorForm : Form
	{
		private Bitmap sheet;
		private Brush highlight;
		public int Value { get; set; }


		public GlyphSelectorForm()
		{
			sheet = Mix.GetBitmap("fonts\\8x8-bold.png");
			highlight = new SolidBrush(Color.FromArgb(128, 64, 64, 255));
			this.ClientSize = new Size(512, 512);
			this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
			Paint += new PaintEventHandler(GlyphSelectorForm_Paint);
			MouseUp += new MouseEventHandler(GlyphSelectorForm_MouseUp);
		}

		void GlyphSelectorForm_Paint(object sender, PaintEventArgs e)
		{
			e.Graphics.DrawImage(sheet, 0, 0, 512, 512);
			e.Graphics.FillRectangle(highlight, ((Value - 32) % 0x40) * 16, ((Value - 32) / 0x1F) * 16, 16, 16);
		}

		void GlyphSelectorForm_MouseUp(object sender, MouseEventArgs e)
		{
			var x = e.X / 16;
			var y = e.Y / 16;
			if (x < 0 || y < 0 || x >= 32 || y >= 32)
				return;
			Value = ((y * 32) + x) + 32;
			Close();
		}
	}

	public class GlyphSelector : UITypeEditor
	{


		public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
		{
			if (context != null && context.Instance != null && provider != null)
			{
				var edSvc = (IWindowsFormsEditorService)provider.GetService(typeof(IWindowsFormsEditorService));
				if (edSvc != null)
				{
					var editor = new GlyphSelectorForm();
					editor.Value = (int)Convert.ChangeType(value, context.PropertyDescriptor.PropertyType);
					edSvc.ShowDialog(editor);
					return editor.Value;
				}
			}
			return value;
		}

		public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
		{
			return UITypeEditorEditStyle.Modal;
		}
	}
}
