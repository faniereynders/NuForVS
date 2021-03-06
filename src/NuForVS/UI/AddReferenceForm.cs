﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using NuForVS.Core;

namespace NuForVS.UI
{
    public partial class AddReferenceForm : Form
    {
        private PackageManager _pkgManager;
        private IConfigurationManager _configManager;
        private ViewEnum _activeView;
        private Label _lastView;
        private Label[] _views;

        public AddReferenceForm(string solutionPath, int targetFramework, IProject project, ICommandRunner runner, IFileSystem fs, IConfigurationManager configManager)
        {
            InitializeComponent();
            Win32.NearMargin(searchGems.Handle, 4);
            Win32.FarMargin(searchGems.Handle, 16);
            Win32.SetCueBanner(searchGems.Handle, "Search Gems");
            labelVersion.Text = "Version: " + getAssemblyVersion();
            labelTargetFramework.Text = string.Format("Target Framework: .NET Framework {0}.{1}", targetFramework >> 16,
                                                      targetFramework & 0xFFFF);
            _views = new Label[] { view0, view1, view2, view3 };

            var projectPath = Path.Combine(Path.GetFileName(Path.GetDirectoryName(project.ProjectPath)), Path.GetFileName(project.ProjectPath));
            this.Text = "Add Nu Reference to " + projectPath;

            _configManager = configManager;
            _pkgManager = new PackageManager(solutionPath, targetFramework, project, runner, fs, _configManager);
            buildGemList("");
            gemToInstall.Focus();
        }

        private string getAssemblyVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        }

        private void showView(ViewEnum view)
        {
            var currentView = _views[(int)view];

            copyDimensions(panelList, panelOptions);
            copyDimensions(panelList, panelAbout);
            copyDimensions(gemList, searchResultsList);

            panelList.Visible = view == ViewEnum.AvailableGems || view == ViewEnum.SearchResults;
            gemList.Visible = view == ViewEnum.AvailableGems;
            searchResultsList.Visible = view == ViewEnum.SearchResults;
            
            panelOptions.Visible = view == ViewEnum.Options;
            panelAbout.Visible = view == ViewEnum.About;

            switch (view)
            {
                case ViewEnum.AvailableGems:
                    break;
                case ViewEnum.SearchResults:
                    break;
                case ViewEnum.Options:
                    showConfig();
                    break;
                case ViewEnum.About:
                    break;
            }
            if (_lastView != null)
            {
                _lastView.ForeColor = SystemColors.WindowText;
                _lastView.BackColor = SystemColors.Window;
            }
            currentView.ForeColor = SystemColors.HighlightText;
            currentView.BackColor = SystemColors.Highlight;
            _lastView = currentView;
            _activeView = view;
        }

        private void showConfig()
        {
            configText.Text = _configManager.ConfigurationAsText;
            configText.Focus();
            configText.SelectionStart = 0;
            configText.SelectionLength = 0;
        }

        private void copyDimensions(Control source, Control destination)
        {
            destination.Left = source.Left;
            destination.Top = source.Top;
            destination.Height = source.Height;
            destination.Width = source.Width;
        }

        private void buildGemList(string currentGemName)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                var gems = _pkgManager.ListGems().ToList();

                gemList.Items.Clear();
                gems.ForEach(gem =>
                {
                    var item = new ListViewItem();
                    item.ImageIndex = gem.IsReferenced ? 0 : -1;
                    item.SubItems.Add(gem.Name);
                    item.SubItems.Add(gem.Version);
                    gemList.Items.Add(item);
                    if (gem.Name == currentGemName) item.EnsureVisible();
                });
                showView(ViewEnum.AvailableGems);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void buildSearchResults(string query)
        {
            if (query.Trim().Length == 0) return;

            try
            {
                this.Cursor = Cursors.WaitCursor;
                var gems = _pkgManager.SearchGems(query, outputConsole).ToList();
                
                searchResultsList.Items.Clear();
                gems.ForEach(gem =>
                {
                    var item = new ListViewItem();
                    item.SubItems.Add(gem.Name);
                    item.SubItems.Add(gem.Version);
                    if (gem.IsRemote) item.SubItems.Add("Yes");
                    searchResultsList.Items.Add(item);
                });
                showView(ViewEnum.SearchResults);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void installGem_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                clearConsole();
                var gemName = "";
                if (gemToInstall.Text.Trim() != "")
                {
                    gemName = gemToInstall.Text;
                    doInstallGem(gemName);
                }
                else
                {
                    var list = _activeView == ViewEnum.AvailableGems ? gemList : searchResultsList;
                    foreach (ListViewItem item in list.SelectedItems)
                    {
                        gemName = item.SubItems[1].Text;
                        doInstallGem(gemName);
                    }
                }
                buildGemList(gemName);
                gemToInstall.Text = "";
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void doInstallGem(string gemname)
        {
            if (gemname.Trim().Length == 0) return;

            var gems = _pkgManager.InstallGem(gemname, outputConsole);

            var unRefCount = gems.Count(g => g.IsReferenced == false);
            if (unRefCount > 0)
            {
                var form = new SelectReferencesForm(_pkgManager.Project, gems);
                form.ShowDialog();
            }
        }

        private void clearConsole()
        {
            consoleOutput.Text = "";
        }

        private void outputConsole(string line)
        {
            consoleOutput.Text += line + "\r\n";
            consoleOutput.SelectionStart = consoleOutput.Text.Length;
            consoleOutput.ScrollToCaret();
        }

        private void gemList_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var list = sender as ListView;
            try
            {
                this.Cursor = Cursors.WaitCursor;
                clearConsole();
                var gemName = "";
                foreach (ListViewItem item in list.SelectedItems)
                {
                    gemName = item.SubItems[1].Text;
                    doInstallGem(gemName);
                }
                buildGemList(gemName);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void changeView_Click(object sender, EventArgs e)
        {
            var newView = sender as Label;
            showView((ViewEnum)Enum.Parse(typeof(ViewEnum), newView.Tag.ToString(), true));
        }

        private void searchGems_KeyUp(object sender, KeyEventArgs e)
        {

            if (e.KeyCode == Keys.Enter)
            {
                buildSearchResults(searchGems.Text);
            }
        }

        private void gemToInstall_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                installGem.PerformClick();
            }

        }

        private void linkWebSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var psi = new ProcessStartInfo(linkWebSite.Text);
            Process.Start(psi);
        }

        private void saveConfig_Click(object sender, EventArgs e)
        {
            _configManager.SaveConfig(configText.Text);
            MessageBox.Show("Configuration saved");
        }

        private void cancelConfig_Click(object sender, EventArgs e)
        {
            configText.Text = _configManager.ConfigurationAsText;
        }

        

    }

    enum ViewEnum
    {
        AvailableGems = 0,
        SearchResults = 1,
        Options = 2,
        About = 3
    }
}
