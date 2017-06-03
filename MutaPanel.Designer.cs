namespace Noxico
{
	partial class MutaPanel
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.sourceBox = new System.Windows.Forms.RichTextBox();
			this.targetBox = new System.Windows.Forms.RichTextBox();
			this.resultBox = new System.Windows.Forms.RichTextBox();
			this.messagesBox = new System.Windows.Forms.RichTextBox();
			this.deltasBox = new System.Windows.Forms.RichTextBox();
			this.sourceMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.targetMenu = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip = new System.Windows.Forms.MenuStrip();
			this.menuStrip.SuspendLayout();
			this.SuspendLayout();
			// 
			// sourceBox
			// 
			this.sourceBox.Location = new System.Drawing.Point(11, 24);
			this.sourceBox.Name = "sourceBox";
			this.sourceBox.Size = new System.Drawing.Size(242, 488);
			this.sourceBox.TabIndex = 2;
			this.sourceBox.Text = "Source goes here.\n";
			// 
			// targetBox
			// 
			this.targetBox.Location = new System.Drawing.Point(259, 24);
			this.targetBox.Name = "targetBox";
			this.targetBox.Size = new System.Drawing.Size(242, 488);
			this.targetBox.TabIndex = 3;
			this.targetBox.Text = "Target goes here.";
			// 
			// resultBox
			// 
			this.resultBox.Location = new System.Drawing.Point(755, 24);
			this.resultBox.Name = "resultBox";
			this.resultBox.Size = new System.Drawing.Size(242, 488);
			this.resultBox.TabIndex = 4;
			this.resultBox.Text = "Result goes here.";
			// 
			// messagesBox
			// 
			this.messagesBox.Location = new System.Drawing.Point(1003, 24);
			this.messagesBox.Name = "messagesBox";
			this.messagesBox.Size = new System.Drawing.Size(242, 488);
			this.messagesBox.TabIndex = 5;
			this.messagesBox.Text = "Mesasges go here.";
			// 
			// deltasBox
			// 
			this.deltasBox.Location = new System.Drawing.Point(507, 24);
			this.deltasBox.Name = "deltasBox";
			this.deltasBox.Size = new System.Drawing.Size(242, 488);
			this.deltasBox.TabIndex = 8;
			this.deltasBox.Text = "Deltas go here.";
			// 
			// initialMenu
			// 
			this.sourceMenu.Name = "initialMenu";
			this.sourceMenu.Size = new System.Drawing.Size(145, 20);
			this.sourceMenu.Text = "Choose Initial Character";
			// 
			// targetMenu
			// 
			this.targetMenu.Name = "targetMenu";
			this.targetMenu.Size = new System.Drawing.Size(149, 20);
			this.targetMenu.Text = "Choose Target Bodyplan";			
			// 
			// menuStrip
			// 
			this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.sourceMenu,
            this.targetMenu});
			this.menuStrip.Location = new System.Drawing.Point(0, 0);
			this.menuStrip.Name = "menuStrip";
			this.menuStrip.Size = new System.Drawing.Size(1255, 24);
			this.menuStrip.TabIndex = 7;
			this.menuStrip.Text = "menuStrip2";
			// 
			// MutaPanel
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(1255, 521);
			this.Controls.Add(this.deltasBox);
			this.Controls.Add(this.messagesBox);
			this.Controls.Add(this.resultBox);
			this.Controls.Add(this.targetBox);
			this.Controls.Add(this.sourceBox);
			this.Controls.Add(this.menuStrip);
			this.Name = "MutaPanel";
			this.Text = "Mutamorph Test Panel";
			this.menuStrip.ResumeLayout(false);
			this.menuStrip.PerformLayout();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.RichTextBox sourceBox;
		private System.Windows.Forms.RichTextBox targetBox;
		private System.Windows.Forms.RichTextBox resultBox;
		private System.Windows.Forms.RichTextBox messagesBox;
		private System.Windows.Forms.RichTextBox deltasBox;
		private System.Windows.Forms.ToolStripMenuItem sourceMenu;
		private System.Windows.Forms.ToolStripMenuItem targetMenu;
		private System.Windows.Forms.MenuStrip menuStrip;
	}
}