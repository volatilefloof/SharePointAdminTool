using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace EntraGroupsApp
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            btnSignIn = new Button();
            flowLayoutPanelButtons = new FlowLayoutPanel();
            btnSearchUser = new Button();
            btnSearchGroup = new Button();
            btnPIMElevate = new Button();
            btnSignOut = new Button();
            panelReviewChanges = new Panel();
            btnReviewChanges = new Button();
            lblStatus = new Label();
            imageList1 = new ImageList(components);
            flowLayoutPanelButtons.SuspendLayout();
            panelReviewChanges.SuspendLayout();
            SuspendLayout();

            btnSignIn.Font = new Font("Segoe UI", 10F);
            btnSignIn.Location = new Point(275, 200);
            btnSignIn.Name = "btnSignIn";
            btnSignIn.Size = new Size(150, 40);
            btnSignIn.TabIndex = 0;
            btnSignIn.Text = "Sign In";
            btnSignIn.TextAlign = ContentAlignment.MiddleCenter;
            btnSignIn.UseVisualStyleBackColor = true;

            flowLayoutPanelButtons.AutoSize = true;
            flowLayoutPanelButtons.AutoScroll = true;
            flowLayoutPanelButtons.WrapContents = true;
            flowLayoutPanelButtons.Controls.Add(btnSearchUser);
            flowLayoutPanelButtons.Controls.Add(btnSearchGroup);
            flowLayoutPanelButtons.Controls.Add(btnPIMElevate);
            flowLayoutPanelButtons.Controls.Add(btnSignOut);
            flowLayoutPanelButtons.FlowDirection = FlowDirection.LeftToRight;
            flowLayoutPanelButtons.Location = new Point(40, 160);
            flowLayoutPanelButtons.MaximumSize = new Size(750, 100);
            flowLayoutPanelButtons.Name = "flowLayoutPanelButtons";
            flowLayoutPanelButtons.Size = new Size(750, 45);
            flowLayoutPanelButtons.TabIndex = 1;
            flowLayoutPanelButtons.Visible = false;

            btnSearchUser.Font = new Font("Segoe UI", 9F);
            btnSearchUser.Location = new Point(5, 5);
            btnSearchUser.Name = "btnSearchUser";
            btnSearchUser.Size = new Size(140, 35);
            btnSearchUser.Margin = new Padding(5);
            btnSearchUser.TabIndex = 1;
            btnSearchUser.Text = "Manage Users";
            btnSearchUser.TextAlign = ContentAlignment.MiddleCenter;
            btnSearchUser.UseVisualStyleBackColor = true;

            btnSearchGroup.Font = new Font("Segoe UI", 9F);
            btnSearchGroup.Location = new Point(150, 5);
            btnSearchGroup.Name = "btnSearchGroup";
            btnSearchGroup.Size = new Size(140, 35);
            btnSearchGroup.Margin = new Padding(5);
            btnSearchGroup.TabIndex = 2;
            btnSearchGroup.Text = "Manage Groups";
            btnSearchGroup.TextAlign = ContentAlignment.MiddleCenter;
            btnSearchGroup.UseVisualStyleBackColor = true;

            btnPIMElevate.Font = new Font("Segoe UI", 9F);
            btnPIMElevate.Location = new Point(295, 5);
            btnPIMElevate.Name = "btnPIMElevate";
            btnPIMElevate.Size = new Size(140, 35);
            btnPIMElevate.Margin = new Padding(5);
            btnPIMElevate.TabIndex = 3;
            btnPIMElevate.Text = "PIM Elevate";
            btnPIMElevate.TextAlign = ContentAlignment.MiddleCenter;
            btnPIMElevate.UseVisualStyleBackColor = true;

            btnSignOut.Font = new Font("Segoe UI", 9F);
            btnSignOut.Location = new Point(440, 5);
            btnSignOut.Name = "btnSignOut";
            btnSignOut.Size = new Size(140, 35);
            btnSignOut.Margin = new Padding(5);
            btnSignOut.TabIndex = 4;
            btnSignOut.Text = "End Session";
            btnSignOut.TextAlign = ContentAlignment.MiddleCenter;
            btnSignOut.UseVisualStyleBackColor = true;

            panelReviewChanges.Controls.Add(btnReviewChanges);
            panelReviewChanges.Location = new Point(40, 215);
            panelReviewChanges.Name = "panelReviewChanges";
            panelReviewChanges.Size = new Size(620, 45);
            panelReviewChanges.TabIndex = 5;
            panelReviewChanges.Visible = false;

            btnReviewChanges.Font = new Font("Segoe UI", 9F);
            btnReviewChanges.Location = new Point(220, 5);
            btnReviewChanges.Name = "btnReviewChanges";
            btnReviewChanges.Size = new Size(180, 35);
            btnReviewChanges.TabIndex = 6;
            btnReviewChanges.Text = "Review my recent changes";
            btnReviewChanges.TextAlign = ContentAlignment.MiddleCenter;
            btnReviewChanges.UseVisualStyleBackColor = true;
            btnReviewChanges.Visible = false;

            lblStatus.AutoSize = true;
            lblStatus.Font = new Font("Segoe UI", 12F);
            lblStatus.Location = new Point(290, 80);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(140, 28);
            lblStatus.TabIndex = 2;
            lblStatus.Text = "Not signed in";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;

            imageList1.ColorDepth = ColorDepth.Depth32Bit;
            imageList1.ImageStream = (ImageListStreamer)resources.GetObject("imageList1.ImageStream");
            imageList1.TransparentColor = Color.Silver;
            imageList1.Images.SetKeyName(0, "download.png");

            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(700, 380);
            Controls.Add(lblStatus);
            Controls.Add(btnSignIn);
            Controls.Add(flowLayoutPanelButtons);
            Controls.Add(panelReviewChanges);
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            Icon = (Icon)resources.GetObject("$this.Icon");
            MaximizeBox = false;
            Name = "Form1";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "SharePoint Admin Tool";
            Load += Form1_Load;
            flowLayoutPanelButtons.ResumeLayout(false);
            panelReviewChanges.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanelButtons;
        private System.Windows.Forms.Panel panelReviewChanges;
        private System.Windows.Forms.Button btnSignIn;
        private System.Windows.Forms.Button btnSignOut;
        private System.Windows.Forms.Button btnSearchUser;
        private System.Windows.Forms.Button btnSearchGroup;
        private System.Windows.Forms.Button btnPIMElevate;
        private System.Windows.Forms.Button btnReviewChanges;
        private System.Windows.Forms.Label lblStatus;
        private ImageList imageList1;
    }
}