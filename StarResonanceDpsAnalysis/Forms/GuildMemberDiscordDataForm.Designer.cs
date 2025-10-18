namespace StarResonanceDpsAnalysis.Forms
{
    partial class GuildMemberDiscordDataForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GuildMemberDiscordDataForm));
            pageHeader1 = new AntdUI.PageHeader();
            TitleText = new AntdUI.Label();
            panel1 = new AntdUI.Panel();
            label_Stub = new AntdUI.Label();
            panel2 = new AntdUI.Panel();
            button_Stub = new AntdUI.Button();
            button_Close = new AntdUI.Button();
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
            pageHeader1.Size = new Size(600, 40);
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
            TitleText.Size = new Size(600, 40);
            TitleText.TabIndex = 0;
            TitleText.Text = "Guild Member Discord Data";
            TitleText.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // panel1
            // 
            panel1.Controls.Add(label_Stub);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 40);
            panel1.Name = "panel1";
            panel1.Padding = new Padding(20);
            panel1.Size = new Size(600, 360);
            panel1.TabIndex = 1;
            // 
            // label_Stub
            // 
            label_Stub.Dock = DockStyle.Fill;
            label_Stub.Font = new Font("HarmonyOS Sans SC", 10F);
            label_Stub.Location = new Point(20, 20);
            label_Stub.Name = "label_Stub";
            label_Stub.Size = new Size(560, 320);
            label_Stub.TabIndex = 0;
            label_Stub.Text = "This form is currently a placeholder for guild member Discord data functionality.\r\n\r\nFuture features may include:\r\n• Discord username mapping\r\n• Activity status tracking\r\n• Message history analysis\r\n• Role and permission management\r\n\r\nUse the buttons below to interact with the form.";
            label_Stub.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            panel2.Controls.Add(button_Stub);
            panel2.Controls.Add(button_Close);
            panel2.Dock = DockStyle.Bottom;
            panel2.Location = new Point(0, 400);
            panel2.Name = "panel2";
            panel2.Padding = new Padding(10);
            panel2.Size = new Size(600, 60);
            panel2.TabIndex = 2;
            // 
            // button_Stub
            // 
            button_Stub.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_Stub.Font = new Font("HarmonyOS Sans SC", 9F);
            button_Stub.Location = new Point(370, 15);
            button_Stub.Name = "button_Stub";
            button_Stub.Radius = 3;
            button_Stub.Size = new Size(100, 30);
            button_Stub.TabIndex = 0;
            button_Stub.Text = "Stub Button";
            button_Stub.Click += button_Stub_Click;
            // 
            // button_Close
            // 
            button_Close.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_Close.Font = new Font("HarmonyOS Sans SC", 9F);
            button_Close.Location = new Point(480, 15);
            button_Close.Name = "button_Close";
            button_Close.Radius = 3;
            button_Close.Size = new Size(100, 30);
            button_Close.TabIndex = 1;
            button_Close.Text = "Close";
            button_Close.Click += button_Close_Click;
            // 
            // GuildMemberDiscordDataForm
            // 
            AutoScaleDimensions = new SizeF(120F, 120F);
            AutoScaleMode = AutoScaleMode.Dpi;
            BackColor = Color.White;
            BorderWidth = 0;
            ClientSize = new Size(600, 460);
            Controls.Add(panel1);
            Controls.Add(panel2);
            Controls.Add(pageHeader1);
            Font = new Font("HarmonyOS Sans SC", 8F);
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(2);
            Name = "GuildMemberDiscordDataForm";
            Radius = 3;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Guild Member Discord Data";
            FormClosing += GuildMemberDiscordDataForm_FormClosing;
            Load += GuildMemberDiscordDataForm_Load;
            pageHeader1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.PageHeader pageHeader1;
        private AntdUI.Label TitleText;
        private AntdUI.Panel panel1;
        private AntdUI.Label label_Stub;
        private AntdUI.Panel panel2;
        private AntdUI.Button button_Stub;
        private AntdUI.Button button_Close;
    }
}
