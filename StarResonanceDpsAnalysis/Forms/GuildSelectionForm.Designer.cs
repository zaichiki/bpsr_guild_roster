namespace StarResonanceDpsAnalysis.Forms
{
    partial class GuildSelectionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GuildSelectionForm));
            pageHeader1 = new AntdUI.PageHeader();
            TitleText = new AntdUI.Label();
            panel1 = new AntdUI.Panel();
            label_Instructions = new AntdUI.Label();
            comboBox_Guilds = new AntdUI.Select();
            panel2 = new AntdUI.Panel();
            button_Select = new AntdUI.Button();
            button_Cancel = new AntdUI.Button();
            pageHeader1.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // pageHeader1
            // 
            pageHeader1.BackColor = Color.FromArgb(178, 178, 178);
            pageHeader1.ColorScheme = AntdUI.TAMode.Dark;
            pageHeader1.Controls.Add(TitleText);
            pageHeader1.DividerShow = true;
            pageHeader1.DividerThickness = 2F;
            pageHeader1.Dock = DockStyle.Top;
            pageHeader1.Location = new Point(0, 0);
            pageHeader1.Margin = new Padding(2);
            pageHeader1.MaximizeBox = false;
            pageHeader1.Mode = AntdUI.TAMode.Dark;
            pageHeader1.Name = "pageHeader1";
            pageHeader1.Size = new Size(500, 40);
            pageHeader1.TabIndex = 0;
            pageHeader1.Text = "";
            // 
            // TitleText
            // 
            TitleText.BackColor = Color.Transparent;
            TitleText.ColorScheme = AntdUI.TAMode.Dark;
            TitleText.Dock = DockStyle.Fill;
            TitleText.Font = new Font("SAO Welcome TT", 12F, FontStyle.Bold);
            TitleText.ForeColor = Color.White;
            TitleText.Location = new Point(0, 0);
            TitleText.Name = "TitleText";
            TitleText.Size = new Size(500, 40);
            TitleText.TabIndex = 0;
            TitleText.Text = "Select Discord Guild";
            TitleText.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // panel1
            // 
            panel1.Controls.Add(label_Instructions);
            panel1.Controls.Add(comboBox_Guilds);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 40);
            panel1.Name = "panel1";
            panel1.Padding = new Padding(20);
            panel1.Size = new Size(500, 300);
            panel1.TabIndex = 1;
            // 
            // label_Instructions
            // 
            label_Instructions.Dock = DockStyle.Top;
            label_Instructions.Font = new Font("HarmonyOS Sans SC", 10F);
            label_Instructions.Location = new Point(20, 20);
            label_Instructions.Name = "label_Instructions";
            label_Instructions.Size = new Size(460, 40);
            label_Instructions.TabIndex = 1;
            label_Instructions.Text = "Select the Discord guild you want to use for member verification:";
            // 
            // comboBox_Guilds
            // 
            comboBox_Guilds.Dock = DockStyle.Top;
            comboBox_Guilds.Font = new Font("HarmonyOS Sans SC", 9F);
            comboBox_Guilds.Location = new Point(20, 60);
            comboBox_Guilds.Name = "comboBox_Guilds";
            comboBox_Guilds.Size = new Size(460, 40);
            comboBox_Guilds.TabIndex = 0;
            comboBox_Guilds.SelectedIndexChanged += comboBox_Guilds_SelectedIndexChanged;
            // 
            // panel2
            // 
            panel2.Controls.Add(button_Select);
            panel2.Controls.Add(button_Cancel);
            panel2.Dock = DockStyle.Bottom;
            panel2.Location = new Point(0, 340);
            panel2.Name = "panel2";
            panel2.Padding = new Padding(10);
            panel2.Size = new Size(500, 60);
            panel2.TabIndex = 2;
            // 
            // button_Select
            // 
            button_Select.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_Select.Enabled = false;
            button_Select.Font = new Font("HarmonyOS Sans SC", 9F);
            button_Select.Location = new Point(300, 15);
            button_Select.Name = "button_Select";
            button_Select.Radius = 3;
            button_Select.Size = new Size(90, 30);
            button_Select.TabIndex = 0;
            button_Select.Text = "Select";
            button_Select.Click += button_Select_Click;
            // 
            // button_Cancel
            // 
            button_Cancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_Cancel.Font = new Font("HarmonyOS Sans SC", 9F);
            button_Cancel.Location = new Point(400, 15);
            button_Cancel.Name = "button_Cancel";
            button_Cancel.Radius = 3;
            button_Cancel.Size = new Size(90, 30);
            button_Cancel.TabIndex = 1;
            button_Cancel.Text = "Cancel";
            button_Cancel.Click += button_Cancel_Click;
            // 
            // GuildSelectionForm
            // 
            AutoScaleDimensions = new SizeF(120F, 120F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            BorderWidth = 0;
            ClientSize = new Size(500, 400);
            Controls.Add(panel1);
            Controls.Add(panel2);
            Controls.Add(pageHeader1);
            Font = new Font("HarmonyOS Sans SC", 8F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            Name = "GuildSelectionForm";
            Radius = 3;
            StartPosition = FormStartPosition.CenterParent;
            Text = "Select Discord Guild";
            FormClosing += GuildSelectionForm_FormClosing;
            Load += GuildSelectionForm_Load;
            pageHeader1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.PageHeader pageHeader1;
        private AntdUI.Label TitleText;
        private AntdUI.Panel panel1;
        private AntdUI.Label label_Instructions;
        private AntdUI.Select comboBox_Guilds;
        private AntdUI.Panel panel2;
        private AntdUI.Button button_Select;
        private AntdUI.Button button_Cancel;
    }
}
