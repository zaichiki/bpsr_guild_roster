namespace StarResonanceDpsAnalysis.Forms
{
    partial class DebugWindowForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(DebugWindowForm));
            pageHeader1 = new AntdUI.PageHeader();
            TitleText = new AntdUI.Label();
            panel1 = new AntdUI.Panel();
            input_Output = new AntdUI.Input();
            panel2 = new AntdUI.Panel();
            label_PlayerID = new AntdUI.Label();
            input_PlayerID = new AntdUI.Input();
            button_Test1 = new AntdUI.Button();
            button_Test2 = new AntdUI.Button();
            button_Clear = new AntdUI.Button();
            button_Close = new AntdUI.Button();
            pageHeader1.SuspendLayout();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // pageHeader1
            // 
            pageHeader1.BackColor = System.Drawing.Color.FromArgb(178, 178, 178);
            pageHeader1.ColorScheme = AntdUI.TAMode.Dark;
            pageHeader1.Controls.Add(TitleText);
            pageHeader1.DividerShow = true;
            pageHeader1.DividerThickness = 2F;
            pageHeader1.Dock = System.Windows.Forms.DockStyle.Top;
            pageHeader1.Location = new System.Drawing.Point(0, 0);
            pageHeader1.Margin = new System.Windows.Forms.Padding(2);
            pageHeader1.MaximizeBox = false;
            pageHeader1.Mode = AntdUI.TAMode.Dark;
            pageHeader1.Name = "pageHeader1";
            pageHeader1.Size = new System.Drawing.Size(800, 40);
            pageHeader1.TabIndex = 0;
            pageHeader1.Text = "";
            // 
            // TitleText
            // 
            TitleText.BackColor = System.Drawing.Color.Transparent;
            TitleText.ColorScheme = AntdUI.TAMode.Dark;
            TitleText.Dock = System.Windows.Forms.DockStyle.Fill;
            TitleText.Font = new System.Drawing.Font("SAO Welcome TT", 12F, System.Drawing.FontStyle.Bold);
            TitleText.ForeColor = System.Drawing.Color.White;
            TitleText.Location = new System.Drawing.Point(0, 0);
            TitleText.Name = "TitleText";
            TitleText.Size = new System.Drawing.Size(800, 40);
            TitleText.TabIndex = 0;
            TitleText.Text = "Master Score Collector";
            TitleText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel1
            // 
            panel1.Controls.Add(input_Output);
            panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            panel1.Location = new System.Drawing.Point(0, 40);
            panel1.Name = "panel1";
            panel1.Padding = new System.Windows.Forms.Padding(10);
            panel1.Size = new System.Drawing.Size(800, 460);
            panel1.TabIndex = 1;
            // 
            // input_Output
            // 
            input_Output.Dock = System.Windows.Forms.DockStyle.Fill;
            input_Output.Font = new System.Drawing.Font("Consolas", 9F);
            input_Output.Location = new System.Drawing.Point(10, 10);
            input_Output.Multiline = true;
            input_Output.Name = "input_Output";
            input_Output.ReadOnly = true;
            input_Output.Size = new System.Drawing.Size(780, 440);
            input_Output.TabIndex = 0;
            // 
            // panel2
            // 
            panel2.Controls.Add(label_PlayerID);
            panel2.Controls.Add(input_PlayerID);
            panel2.Controls.Add(button_Test1);
            panel2.Controls.Add(button_Test2);
            panel2.Controls.Add(button_Clear);
            panel2.Controls.Add(button_Close);
            panel2.Dock = System.Windows.Forms.DockStyle.Bottom;
            panel2.Location = new System.Drawing.Point(0, 500);
            panel2.Name = "panel2";
            panel2.Padding = new System.Windows.Forms.Padding(10);
            panel2.Size = new System.Drawing.Size(800, 60);
            panel2.TabIndex = 2;
            // 
            // label_PlayerID
            // 
            label_PlayerID.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            label_PlayerID.Font = new System.Drawing.Font("HarmonyOS Sans SC", 9F);
            label_PlayerID.Location = new System.Drawing.Point(10, 17);
            label_PlayerID.Name = "label_PlayerID";
            label_PlayerID.Size = new System.Drawing.Size(80, 25);
            label_PlayerID.TabIndex = 4;
            label_PlayerID.Text = "Player ID:";
            label_PlayerID.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            label_PlayerID.Visible = false;
            // 
            // input_PlayerID
            // 
            input_PlayerID.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            input_PlayerID.Font = new System.Drawing.Font("Consolas", 9F);
            input_PlayerID.Location = new System.Drawing.Point(95, 15);
            input_PlayerID.Name = "input_PlayerID";
            input_PlayerID.PlaceholderText = "Enter player ID...";
            input_PlayerID.Size = new System.Drawing.Size(120, 30);
            input_PlayerID.TabIndex = 5;
            input_PlayerID.Text = "";
            input_PlayerID.Visible = false;
            // 
            // button_Test1
            // 
            button_Test1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            button_Test1.Font = new System.Drawing.Font("HarmonyOS Sans SC", 9F);
            button_Test1.Location = new System.Drawing.Point(225, 15);
            button_Test1.Name = "button_Test1";
            button_Test1.Radius = 3;
            button_Test1.Size = new System.Drawing.Size(130, 30);
            button_Test1.TabIndex = 0;
            button_Test1.Text = "Send to API";
            button_Test1.Click += button_Test1_Click;
            // 
            // button_Test2
            // 
            button_Test2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            button_Test2.Font = new System.Drawing.Font("HarmonyOS Sans SC", 9F);
            button_Test2.Location = new System.Drawing.Point(365, 15);
            button_Test2.Name = "button_Test2";
            button_Test2.Radius = 3;
            button_Test2.Size = new System.Drawing.Size(180, 30);
            button_Test2.TabIndex = 1;
            button_Test2.Text = "Auto-Click Guild Roster";
            button_Test2.Click += button_Test2_Click;
            // 
            // button_Clear
            // 
            button_Clear.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            button_Clear.Font = new System.Drawing.Font("HarmonyOS Sans SC", 9F);
            button_Clear.Location = new System.Drawing.Point(580, 15);
            button_Clear.Name = "button_Clear";
            button_Clear.Radius = 3;
            button_Clear.Size = new System.Drawing.Size(100, 30);
            button_Clear.TabIndex = 2;
            button_Clear.Text = "Clear Data";
            button_Clear.Click += button_Clear_Click;
            // 
            // button_Close
            // 
            button_Close.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            button_Close.Font = new System.Drawing.Font("HarmonyOS Sans SC", 9F);
            button_Close.Location = new System.Drawing.Point(690, 15);
            button_Close.Name = "button_Close";
            button_Close.Radius = 3;
            button_Close.Size = new System.Drawing.Size(100, 30);
            button_Close.TabIndex = 3;
            button_Close.Text = "Close";
            button_Close.Click += button_Close_Click;
            // 
            // DebugWindowForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            BackColor = System.Drawing.Color.White;
            BorderWidth = 0;
            ClientSize = new System.Drawing.Size(800, 560);
            Controls.Add(panel1);
            Controls.Add(panel2);
            Controls.Add(pageHeader1);
            Font = new System.Drawing.Font("HarmonyOS Sans SC", 8F);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(2);
            Name = "DebugWindowForm";
            Radius = 3;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Master Score Collector";
            FormClosing += DebugWindowForm_FormClosing;
            Load += DebugWindowForm_Load;
            pageHeader1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private AntdUI.PageHeader pageHeader1;
        private AntdUI.Label TitleText;
        private AntdUI.Panel panel1;
        private AntdUI.Input input_Output;
        private AntdUI.Panel panel2;
        private AntdUI.Label label_PlayerID;
        private AntdUI.Input input_PlayerID;
        private AntdUI.Button button_Test1;
        private AntdUI.Button button_Test2;
        private AntdUI.Button button_Clear;
        private AntdUI.Button button_Close;
    }
}

