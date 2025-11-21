namespace CASCExplorer
{
    partial class MainForm
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
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            splitContainer1 = new SplitContainer();
            folderTree = new TreeView();
            iconsList = new ImageList(components);
            fileList = new NoFlickerListView();
            columnHeader1 = new ColumnHeader();
            columnHeader2 = new ColumnHeader();
            columnHeader3 = new ColumnHeader();
            columnHeader5 = new ColumnHeader();
            columnHeader4 = new ColumnHeader();
            contextMenuStrip1 = new ContextMenuStrip(components);
            extractToolStripMenuItem = new ToolStripMenuItem();
            copyNameToolStripMenuItem = new ToolStripMenuItem();
            getSizeToolStripMenuItem = new ToolStripMenuItem();
            toolStripContainer1 = new ToolStripContainer();
            statusStrip1 = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            statusProgress = new ToolStripProgressBar();
            toolStrip1 = new ToolStrip();
            filterToolStripLabel = new ToolStripLabel();
            filterToolStripTextBox = new ToolStripTextBox();
            toolStrip2 = new ToolStrip();
            openStorageToolStripButton = new ToolStripButton();
            menuStrip1 = new MenuStrip();
            fileToolStripMenuItem = new ToolStripMenuItem();
            openStorageToolStripMenuItem = new ToolStripMenuItem();
            openOnlineStorageToolStripMenuItem = new ToolStripMenuItem();
            openRecentStorageToolStripMenuItem = new ToolStripMenuItem();
            closeStorageToolStripMenuItem = new ToolStripMenuItem();
            exitToolStripMenuItem = new ToolStripMenuItem();
            editToolStripMenuItem = new ToolStripMenuItem();
            findToolStripMenuItem = new ToolStripMenuItem();
            viewToolStripMenuItem = new ToolStripMenuItem();
            localeFlagsToolStripMenuItem = new ToolStripMenuItem();
            useLVToolStripMenuItem = new ToolStripMenuItem();
            toolsToolStripMenuItem = new ToolStripMenuItem();
            scanFilesToolStripMenuItem = new ToolStripMenuItem();
            analyseUnknownFilesToolStripMenuItem = new ToolStripMenuItem();
            extractInstallFilesToolStripMenuItem = new ToolStripMenuItem();
            CDNBuildsToolStripMenuItem = new ToolStripMenuItem();
            extractCASCSystemFilesToolStripMenuItem = new ToolStripMenuItem();
            bruteforceNamesToolStripMenuItem = new ToolStripMenuItem();
            exportListfileToolStripMenuItem = new ToolStripMenuItem();
            helpToolStripMenuItem = new ToolStripMenuItem();
            aboutToolStripMenuItem = new ToolStripMenuItem();
            storageFolderBrowserDialog = new FolderBrowserDialog();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            contextMenuStrip1.SuspendLayout();
            toolStripContainer1.BottomToolStripPanel.SuspendLayout();
            toolStripContainer1.ContentPanel.SuspendLayout();
            toolStripContainer1.TopToolStripPanel.SuspendLayout();
            toolStripContainer1.SuspendLayout();
            statusStrip1.SuspendLayout();
            toolStrip1.SuspendLayout();
            toolStrip2.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.FixedPanel = FixedPanel.Panel1;
            splitContainer1.Location = new Point(0, 0);
            splitContainer1.Margin = new Padding(4, 5, 4, 5);
            splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(folderTree);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(fileList);
            splitContainer1.Size = new Size(1117, 745);
            splitContainer1.SplitterDistance = 283;
            splitContainer1.SplitterWidth = 5;
            splitContainer1.TabIndex = 0;
            // 
            // folderTree
            // 
            folderTree.Dock = DockStyle.Fill;
            folderTree.HideSelection = false;
            folderTree.ImageIndex = 0;
            folderTree.ImageList = iconsList;
            folderTree.ItemHeight = 16;
            folderTree.Location = new Point(0, 0);
            folderTree.Margin = new Padding(4, 5, 4, 5);
            folderTree.Name = "folderTree";
            folderTree.SelectedImageIndex = 0;
            folderTree.Size = new Size(283, 745);
            folderTree.TabIndex = 0;
            folderTree.BeforeExpand += treeView1_BeforeExpand;
            folderTree.BeforeSelect += treeView1_BeforeSelect;
            // 
            // iconsList
            // 
            iconsList.ColorDepth = ColorDepth.Depth32Bit;
            iconsList.ImageSize = new Size(15, 15);
            iconsList.TransparentColor = Color.Transparent;
            // 
            // fileList
            // 
            fileList.Columns.AddRange(new ColumnHeader[] { columnHeader1, columnHeader2, columnHeader3, columnHeader5, columnHeader4 });
            fileList.ContextMenuStrip = contextMenuStrip1;
            fileList.Dock = DockStyle.Fill;
            fileList.FullRowSelect = true;
            fileList.Location = new Point(0, 0);
            fileList.Margin = new Padding(4, 5, 4, 5);
            fileList.Name = "fileList";
            fileList.Size = new Size(829, 745);
            fileList.SmallImageList = iconsList;
            fileList.Sorting = SortOrder.Ascending;
            fileList.TabIndex = 0;
            fileList.UseCompatibleStateImageBehavior = false;
            fileList.View = View.Details;
            fileList.VirtualMode = true;
            fileList.ColumnClick += listView1_ColumnClick;
            fileList.RetrieveVirtualItem += listView1_RetrieveVirtualItem;
            fileList.SearchForVirtualItem += fileList_SearchForVirtualItem;
            fileList.KeyDown += listView1_KeyDown;
            fileList.MouseDoubleClick += listView1_MouseDoubleClick;
            // 
            // columnHeader1
            // 
            columnHeader1.Text = "File Name";
            columnHeader1.Width = 250;
            // 
            // columnHeader2
            // 
            columnHeader2.Text = "Type";
            // 
            // columnHeader3
            // 
            columnHeader3.Text = "Locale Flags";
            columnHeader3.Width = 100;
            // 
            // columnHeader5
            // 
            columnHeader5.Text = "Content Flags";
            columnHeader5.Width = 100;
            // 
            // columnHeader4
            // 
            columnHeader4.Text = "Size";
            columnHeader4.TextAlign = HorizontalAlignment.Right;
            columnHeader4.Width = 80;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { extractToolStripMenuItem, copyNameToolStripMenuItem, getSizeToolStripMenuItem });
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(157, 76);
            contextMenuStrip1.Opening += contextMenuStrip1_Opening;
            // 
            // extractToolStripMenuItem
            // 
            extractToolStripMenuItem.Name = "extractToolStripMenuItem";
            extractToolStripMenuItem.Size = new Size(156, 24);
            extractToolStripMenuItem.Text = "Extract...";
            extractToolStripMenuItem.Click += extractToolStripMenuItem_Click;
            // 
            // copyNameToolStripMenuItem
            // 
            copyNameToolStripMenuItem.Name = "copyNameToolStripMenuItem";
            copyNameToolStripMenuItem.Size = new Size(156, 24);
            copyNameToolStripMenuItem.Text = "Copy Name";
            copyNameToolStripMenuItem.Click += copyNameToolStripMenuItem_Click;
            // 
            // getSizeToolStripMenuItem
            // 
            getSizeToolStripMenuItem.Name = "getSizeToolStripMenuItem";
            getSizeToolStripMenuItem.Size = new Size(156, 24);
            getSizeToolStripMenuItem.Text = "Get Size";
            getSizeToolStripMenuItem.Click += getSizeToolStripMenuItem_Click;
            // 
            // toolStripContainer1
            // 
            // 
            // toolStripContainer1.BottomToolStripPanel
            // 
            toolStripContainer1.BottomToolStripPanel.Controls.Add(statusStrip1);
            // 
            // toolStripContainer1.ContentPanel
            // 
            toolStripContainer1.ContentPanel.Controls.Add(splitContainer1);
            toolStripContainer1.ContentPanel.Margin = new Padding(4, 5, 4, 5);
            toolStripContainer1.ContentPanel.Size = new Size(1117, 745);
            toolStripContainer1.Dock = DockStyle.Fill;
            toolStripContainer1.Location = new Point(0, 0);
            toolStripContainer1.Margin = new Padding(4, 5, 4, 5);
            toolStripContainer1.Name = "toolStripContainer1";
            toolStripContainer1.Size = new Size(1117, 826);
            toolStripContainer1.TabIndex = 3;
            toolStripContainer1.Text = "toolStripContainer1";
            // 
            // toolStripContainer1.TopToolStripPanel
            // 
            toolStripContainer1.TopToolStripPanel.Controls.Add(toolStrip2);
            toolStripContainer1.TopToolStripPanel.Controls.Add(toolStrip1);
            toolStripContainer1.TopToolStripPanel.Controls.Add(menuStrip1);
            // 
            // statusStrip1
            // 
            statusStrip1.Dock = DockStyle.None;
            statusStrip1.ImageScalingSize = new Size(20, 20);
            statusStrip1.Items.AddRange(new ToolStripItem[] { statusLabel, statusProgress });
            statusStrip1.Location = new Point(0, 0);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1117, 26);
            statusStrip1.TabIndex = 0;
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(53, 20);
            statusLabel.Text = "Ready.";
            // 
            // statusProgress
            // 
            statusProgress.Name = "statusProgress";
            statusProgress.Size = new Size(100, 18);
            statusProgress.Visible = false;
            // 
            // toolStrip1
            // 
            toolStrip1.Dock = DockStyle.None;
            toolStrip1.ImageScalingSize = new Size(20, 20);
            toolStrip1.Items.AddRange(new ToolStripItem[] { filterToolStripLabel, filterToolStripTextBox });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new Size(262, 27);
            toolStrip1.TabIndex = 1;
            // 
            // filterToolStripLabel
            // 
            filterToolStripLabel.Name = "filterToolStripLabel";
            filterToolStripLabel.Size = new Size(83, 24);
            filterToolStripLabel.Text = "Files mask: ";
            // 
            // filterToolStripTextBox
            // 
            filterToolStripTextBox.Name = "filterToolStripTextBox";
            filterToolStripTextBox.Size = new Size(125, 27);
            filterToolStripTextBox.Text = "*";
            filterToolStripTextBox.TextChanged += filterToolStripTextBox_TextChanged;
            // 
            // toolStrip2
            // 
            toolStrip2.Dock = DockStyle.None;
            toolStrip2.ImageScalingSize = new Size(20, 20);
            toolStrip2.Items.AddRange(new ToolStripItem[] { openStorageToolStripButton });
            toolStrip2.Location = new Point(0, 0);
            toolStrip2.Name = "toolStrip2";
            toolStrip2.Size = new Size(42, 27);
            toolStrip2.TabIndex = 2;
            // 
            // openStorageToolStripButton
            // 
            openStorageToolStripButton.DisplayStyle = ToolStripItemDisplayStyle.Image;
            openStorageToolStripButton.Image = (Image)resources.GetObject("openStorageToolStripButton.Image");
            openStorageToolStripButton.ImageTransparentColor = Color.Magenta;
            openStorageToolStripButton.Name = "openStorageToolStripButton";
            openStorageToolStripButton.Size = new Size(29, 24);
            openStorageToolStripButton.Text = "&Open Storage";
            openStorageToolStripButton.Click += openStorageToolStripButton_Click;
            // 
            // menuStrip1
            // 
            menuStrip1.Dock = DockStyle.None;
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, editToolStripMenuItem, viewToolStripMenuItem, toolsToolStripMenuItem, helpToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1117, 28);
            menuStrip1.TabIndex = 0;
            menuStrip1.Text = "menuStrip1";
            // 
            // fileToolStripMenuItem
            // 
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openStorageToolStripMenuItem, openOnlineStorageToolStripMenuItem, openRecentStorageToolStripMenuItem, closeStorageToolStripMenuItem, exitToolStripMenuItem });
            fileToolStripMenuItem.Name = "fileToolStripMenuItem";
            fileToolStripMenuItem.Size = new Size(46, 24);
            fileToolStripMenuItem.Text = "File";
            // 
            // openStorageToolStripMenuItem
            // 
            openStorageToolStripMenuItem.Name = "openStorageToolStripMenuItem";
            openStorageToolStripMenuItem.Size = new Size(233, 26);
            openStorageToolStripMenuItem.Text = "Open Storage...";
            openStorageToolStripMenuItem.Click += openStorageToolStripMenuItem_Click;
            // 
            // openOnlineStorageToolStripMenuItem
            // 
            openOnlineStorageToolStripMenuItem.Enabled = false;
            openOnlineStorageToolStripMenuItem.Name = "openOnlineStorageToolStripMenuItem";
            openOnlineStorageToolStripMenuItem.Size = new Size(233, 26);
            openOnlineStorageToolStripMenuItem.Text = "Open Online Storage";
            openOnlineStorageToolStripMenuItem.DropDownItemClicked += openOnlineStorageToolStripMenuItem_DropDownItemClicked;
            // 
            // openRecentStorageToolStripMenuItem
            // 
            openRecentStorageToolStripMenuItem.Enabled = false;
            openRecentStorageToolStripMenuItem.Name = "openRecentStorageToolStripMenuItem";
            openRecentStorageToolStripMenuItem.Size = new Size(233, 26);
            openRecentStorageToolStripMenuItem.Text = "Open Recent Storage";
            openRecentStorageToolStripMenuItem.DropDownItemClicked += openRecentStorageToolStripMenuItem_DropDownItemClicked;
            // 
            // closeStorageToolStripMenuItem
            // 
            closeStorageToolStripMenuItem.Name = "closeStorageToolStripMenuItem";
            closeStorageToolStripMenuItem.Size = new Size(233, 26);
            closeStorageToolStripMenuItem.Text = "Close Storage";
            closeStorageToolStripMenuItem.Click += closeStorageToolStripMenuItem_Click;
            // 
            // exitToolStripMenuItem
            // 
            exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            exitToolStripMenuItem.Size = new Size(233, 26);
            exitToolStripMenuItem.Text = "Exit";
            exitToolStripMenuItem.Click += exitToolStripMenuItem_Click;
            // 
            // editToolStripMenuItem
            // 
            editToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { findToolStripMenuItem });
            editToolStripMenuItem.Name = "editToolStripMenuItem";
            editToolStripMenuItem.Size = new Size(49, 24);
            editToolStripMenuItem.Text = "Edit";
            // 
            // findToolStripMenuItem
            // 
            findToolStripMenuItem.Name = "findToolStripMenuItem";
            findToolStripMenuItem.Size = new Size(129, 26);
            findToolStripMenuItem.Text = "Find...";
            findToolStripMenuItem.Click += findToolStripMenuItem_Click;
            // 
            // viewToolStripMenuItem
            // 
            viewToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { localeFlagsToolStripMenuItem, useLVToolStripMenuItem });
            viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            viewToolStripMenuItem.Size = new Size(55, 24);
            viewToolStripMenuItem.Text = "View";
            // 
            // localeFlagsToolStripMenuItem
            // 
            localeFlagsToolStripMenuItem.Enabled = false;
            localeFlagsToolStripMenuItem.Name = "localeFlagsToolStripMenuItem";
            localeFlagsToolStripMenuItem.Size = new Size(135, 26);
            localeFlagsToolStripMenuItem.Text = "Locale";
            localeFlagsToolStripMenuItem.DropDownItemClicked += localeToolStripMenuItem_DropDownItemClicked;
            // 
            // useLVToolStripMenuItem
            // 
            useLVToolStripMenuItem.Enabled = false;
            useLVToolStripMenuItem.Name = "useLVToolStripMenuItem";
            useLVToolStripMenuItem.Size = new Size(135, 26);
            useLVToolStripMenuItem.Text = "Use LV";
            useLVToolStripMenuItem.Click += contentFlagsToolStripMenuItem_Click;
            // 
            // toolsToolStripMenuItem
            // 
            toolsToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { scanFilesToolStripMenuItem, analyseUnknownFilesToolStripMenuItem, extractInstallFilesToolStripMenuItem, CDNBuildsToolStripMenuItem, extractCASCSystemFilesToolStripMenuItem, bruteforceNamesToolStripMenuItem, exportListfileToolStripMenuItem });
            toolsToolStripMenuItem.Name = "toolsToolStripMenuItem";
            toolsToolStripMenuItem.Size = new Size(58, 24);
            toolsToolStripMenuItem.Text = "Tools";
            // 
            // scanFilesToolStripMenuItem
            // 
            scanFilesToolStripMenuItem.Enabled = false;
            scanFilesToolStripMenuItem.Name = "scanFilesToolStripMenuItem";
            scanFilesToolStripMenuItem.Size = new Size(261, 26);
            scanFilesToolStripMenuItem.Text = "Scan Files";
            scanFilesToolStripMenuItem.Click += scanFilesToolStripMenuItem_Click;
            // 
            // analyseUnknownFilesToolStripMenuItem
            // 
            analyseUnknownFilesToolStripMenuItem.Enabled = false;
            analyseUnknownFilesToolStripMenuItem.Name = "analyseUnknownFilesToolStripMenuItem";
            analyseUnknownFilesToolStripMenuItem.Size = new Size(261, 26);
            analyseUnknownFilesToolStripMenuItem.Text = "Analyse Unknown Files";
            analyseUnknownFilesToolStripMenuItem.Click += analyseUnknownFilesToolStripMenuItem_Click;
            // 
            // extractInstallFilesToolStripMenuItem
            // 
            extractInstallFilesToolStripMenuItem.Enabled = false;
            extractInstallFilesToolStripMenuItem.Name = "extractInstallFilesToolStripMenuItem";
            extractInstallFilesToolStripMenuItem.Size = new Size(261, 26);
            extractInstallFilesToolStripMenuItem.Text = "Extract Install Files";
            extractInstallFilesToolStripMenuItem.Click += extractInstallFilesToolStripMenuItem_Click;
            // 
            // CDNBuildsToolStripMenuItem
            // 
            CDNBuildsToolStripMenuItem.Enabled = false;
            CDNBuildsToolStripMenuItem.Name = "CDNBuildsToolStripMenuItem";
            CDNBuildsToolStripMenuItem.Size = new Size(261, 26);
            CDNBuildsToolStripMenuItem.Text = "CDN Builds";
            // 
            // extractCASCSystemFilesToolStripMenuItem
            // 
            extractCASCSystemFilesToolStripMenuItem.Enabled = false;
            extractCASCSystemFilesToolStripMenuItem.Name = "extractCASCSystemFilesToolStripMenuItem";
            extractCASCSystemFilesToolStripMenuItem.Size = new Size(261, 26);
            extractCASCSystemFilesToolStripMenuItem.Text = "Extract CASC System Files";
            extractCASCSystemFilesToolStripMenuItem.Click += extractCASCSystemFilesToolStripMenuItem_Click;
            // 
            // bruteforceNamesToolStripMenuItem
            // 
            bruteforceNamesToolStripMenuItem.Enabled = false;
            bruteforceNamesToolStripMenuItem.Name = "bruteforceNamesToolStripMenuItem";
            bruteforceNamesToolStripMenuItem.Size = new Size(261, 26);
            bruteforceNamesToolStripMenuItem.Text = "Bruteforce Names";
            bruteforceNamesToolStripMenuItem.Click += bruteforceNamesToolStripMenuItem_Click;
            // 
            // exportListfileToolStripMenuItem
            // 
            exportListfileToolStripMenuItem.Enabled = false;
            exportListfileToolStripMenuItem.Name = "exportListfileToolStripMenuItem";
            exportListfileToolStripMenuItem.Size = new Size(261, 26);
            exportListfileToolStripMenuItem.Text = "Export listfile";
            exportListfileToolStripMenuItem.Click += exportListfileToolStripMenuItem_Click;
            // 
            // helpToolStripMenuItem
            // 
            helpToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { aboutToolStripMenuItem });
            helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            helpToolStripMenuItem.Size = new Size(55, 24);
            helpToolStripMenuItem.Text = "Help";
            // 
            // aboutToolStripMenuItem
            // 
            aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            aboutToolStripMenuItem.Size = new Size(142, 26);
            aboutToolStripMenuItem.Text = "About...";
            aboutToolStripMenuItem.Click += aboutToolStripMenuItem_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1117, 826);
            Controls.Add(toolStripContainer1);
            MainMenuStrip = menuStrip1;
            Margin = new Padding(4, 5, 4, 5);
            Name = "MainForm";
            Text = "CASC Explorer";
            Load += Form1_Load;
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            contextMenuStrip1.ResumeLayout(false);
            toolStripContainer1.BottomToolStripPanel.ResumeLayout(false);
            toolStripContainer1.BottomToolStripPanel.PerformLayout();
            toolStripContainer1.ContentPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.ResumeLayout(false);
            toolStripContainer1.TopToolStripPanel.PerformLayout();
            toolStripContainer1.ResumeLayout(false);
            toolStripContainer1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            toolStrip2.ResumeLayout(false);
            toolStrip2.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.SplitContainer splitContainer1;
        private System.Windows.Forms.TreeView folderTree;
        private NoFlickerListView fileList;
        private System.Windows.Forms.ImageList iconsList;
        private System.Windows.Forms.ToolStripContainer toolStripContainer1;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.MenuStrip menuStrip1;
        private System.Windows.Forms.ToolStripMenuItem fileToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.ToolStripProgressBar statusProgress;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem extractToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ToolStripMenuItem copyNameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem toolsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem scanFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem analyseUnknownFilesToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem localeFlagsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem getSizeToolStripMenuItem;
        private System.Windows.Forms.ColumnHeader columnHeader5;
        private System.Windows.Forms.ToolStripMenuItem useLVToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openStorageToolStripMenuItem;
        private System.Windows.Forms.FolderBrowserDialog storageFolderBrowserDialog;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractInstallFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem CDNBuildsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem extractCASCSystemFilesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem bruteforceNamesToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openOnlineStorageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem closeStorageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openRecentStorageToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportListfileToolStripMenuItem;
        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel filterToolStripLabel;
        private System.Windows.Forms.ToolStripTextBox filterToolStripTextBox;
        private System.Windows.Forms.ToolStrip toolStrip2;
        private System.Windows.Forms.ToolStripButton openStorageToolStripButton;
    }
}

