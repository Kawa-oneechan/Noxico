using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

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
}
