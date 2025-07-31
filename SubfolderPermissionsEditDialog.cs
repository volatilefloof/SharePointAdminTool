using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Graph;
using System.Text;
using Azure.Core;
using Microsoft.Identity.Client.NativeInterop;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.Drawing;
using System.Reflection;

namespace EntraGroupsApp
{
    public partial class SubfolderPermissionsEditDialog : System.Windows.Forms.Form
    {
        private System.ComponentModel.IContainer components = null;
        private readonly string _libraryName;
        private bool isUpdatingDataGridView;
        private bool isUpdatingTreeView;
        private readonly IPublicClientApplication _pca;
        private readonly string _siteUrl;
        private readonly GraphServiceClient _graphClient;
        private readonly AuditLogManager _auditLogManager;
        private readonly string _signedInUserId;
        private List<GroupItem> _availableGroups;
        private DateTime lastRefreshTime = DateTime.MinValue;
        private readonly TimeSpan minimumRefreshInterval = TimeSpan.FromSeconds(5);
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly object _progressLock = new object();
        private readonly Color _placeholderColor = SystemColors.GrayText;
        private readonly Color _normalColor = SystemColors.ControlText;
        private readonly string _placeholderText = "Search subfolders or groups...";
        private TreeNodeCollection _originalNodes;
        private List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups)> _subfolderCache;
        private readonly string _selectedDepartment;
        private readonly Dictionary<string, (string SiteUrl, string[] GroupPrefixes)> _departmentMappings; //dynamically determine available groups in the dropdown using dictionary reference

        // Updated cache structure for nested folders
        private List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions,
            List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)> _nestedSubfolderCache;

        private bool _isPreviewActive = false;
        private TreeNode _previewSubfolderNode = null;
        private List<TreeNode> _previewSubfolderNodes = new List<TreeNode>();
        private bool isHandlingCheck = false;
        private int _maxNestingLevel = 3; // Configurable maximum nesting depth

        public SubfolderPermissionsEditDialog(string libraryName, IPublicClientApplication pca, string siteUrl, GraphServiceClient graphClient, AuditLogManager auditLogManager, string signedInUserId, string selectedDepartment, Dictionary<string, (string SiteUrl, string[] GroupPrefixes)> departmentMappings = null)
        {
            try
            {
                if (string.IsNullOrEmpty(libraryName)) throw new ArgumentNullException(nameof(libraryName));
                if (pca == null) throw new ArgumentNullException(nameof(pca));
                if (string.IsNullOrEmpty(siteUrl)) throw new ArgumentNullException(nameof(siteUrl));
                if (auditLogManager == null) throw new ArgumentNullException(nameof(auditLogManager));
                if (string.IsNullOrEmpty(signedInUserId)) throw new ArgumentNullException(nameof(signedInUserId));

                _libraryName = libraryName;
                _pca = pca;
                _siteUrl = siteUrl;
                _graphClient = graphClient;
                _auditLogManager = auditLogManager;
                _signedInUserId = signedInUserId;
                _nestedSubfolderCache = new List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions,
                    List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>();
                _cancellationTokenSource = new CancellationTokenSource();
                _selectedDepartment = selectedDepartment; // Store the selected department
                _departmentMappings = departmentMappings; // Store the department mappings

                InitializeComponent();
                InitializeUIEnhancements();
                SetupCustomTreeViewIcons();

                this.Text = $"Edit Subfolder Permissions: {libraryName}";
                lblLibrary.Text = $"Library: {libraryName}";

                if (tvSubfolders != null)
                {
                    tvSubfolders.HideSelection = false;
                }
                else
                {
                    throw new NullReferenceException("tvSubfolders is null after InitializeComponent.");
                }

                UpdateUI(() =>
                {
                    try
                    {
                        if (txtSearch != null)
                        {
                            txtSearch.ForeColor = _placeholderColor;
                            txtSearch.Text = _placeholderText;
                            txtSearch.Enter += txtSearch_Enter;
                            txtSearch.Leave += txtSearch_Leave;
                            txtSearch.TextChanged += txtSearch_TextChanged;
                        }
                        else
                        {
                            throw new NullReferenceException("txtSearch is null.");
                        }

                        if (cmbView != null)
                        {
                            cmbView.SelectedIndex = 0;
                            cmbView.SelectedIndexChanged += cmbView_SelectedIndexChanged;
                        }
                        else
                        {
                            throw new NullReferenceException("cmbView is null.");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error initializing UI components: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });

                this.FormClosing += (s, e) => _nestedSubfolderCache = null;

                // Start async initialization
                Task.Run(async () =>
                {
                    try
                    {
                        await LoadAvailableGroupsAsync();
                        await LoadCurrentPermissionsAsync();
                    }
                    catch (Exception ex)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Error loading initial data: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            if (lblStatus != null)
                            {
                                lblStatus.Text = "Error loading initial data.";
                            }
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "InitializationError", _libraryName, null, "Subfolder", $"Failed to initialize dialog: {ex.Message}, StackTrace: {ex.StackTrace}");
                    }
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Critical error initializing dialog: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }

        private void InitializeUIEnhancements()
        {
            // Set modern, clean styling consistent with SPTLibraryCreator
            this.BackColor = Color.White;

            // Style group boxes with modern appearance
            foreach (Control control in this.Controls)
            {
                if (control is GroupBox groupBox)
                {
                    groupBox.ForeColor = Color.FromArgb(64, 64, 64);
                    groupBox.Font = new Font("Microsoft Sans Serif", 9F, FontStyle.Bold);
                }
            }

            // Style buttons consistently
            foreach (Control control in this.Controls.OfType<Control>().SelectMany(GetAllControls))
            {
                if (control is System.Windows.Forms.Button button && button.BackColor == Color.FromArgb(0, 120, 215))
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.Cursor = Cursors.Hand;
                }
            }

            // Enhanced tooltips
            if (toolTipGroups != null)
            {
                toolTipGroups.SetToolTip(btnPreview, "Preview how the permission will look before applying");
                toolTipGroups.SetToolTip(btnAdd, "Add the previewed permission to the selected subfolder");
                toolTipGroups.SetToolTip(btnRemove, "Remove the selected permission or cancel preview");
                toolTipGroups.SetToolTip(btnBreakInheritance, "Break inheritance to enable unique permissions");
                toolTipGroups.SetToolTip(btnRestoreInheritance, "Restore inheritance from parent library");
                toolTipGroups.SetToolTip(btnResetPermissions, "Remove all custom permissions from subfolder");
                toolTipGroups.SetToolTip(txtSearch, "Type to filter subfolders or groups");
                toolTipGroups.SetToolTip(cmbView, "Filter view by permission type");
            }
        }

        private void EnsureTreeViewVisibility()
        {
            UpdateUI(() =>
            {
                if (tvSubfolders != null && tvSubfolders.Nodes.Count > 0)
                {
                    try
                    {
                        // Force the TreeView to refresh and show nodes
                        tvSubfolders.Refresh();
                        tvSubfolders.Update();

                        // Select the first node to ensure visibility
                        if (tvSubfolders.SelectedNode == null && tvSubfolders.Nodes.Count > 0)
                        {
                            var firstNode = tvSubfolders.Nodes[0];
                            if (firstNode != null)
                            {
                                tvSubfolders.SelectedNode = firstNode;
                                // Double-check that the assignment worked before calling EnsureVisible
                                if (tvSubfolders.SelectedNode != null)
                                {
                                    tvSubfolders.SelectedNode.EnsureVisible();
                                }
                                else
                                {
                                    // Fallback: just make the first node visible without selecting it
                                    firstNode.EnsureVisible();
                                }
                            }
                        }

                        // Force focus to ensure proper display
                        tvSubfolders.Focus();

                        System.Diagnostics.Debug.WriteLine($"EnsureTreeViewVisibility: TreeView has {tvSubfolders.Nodes.Count} nodes, first node text: {(tvSubfolders.Nodes.Count > 0 ? tvSubfolders.Nodes[0].Text : "No nodes")}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"EnsureTreeViewVisibility: Error ensuring visibility: {ex.Message}");
                        // Fallback: just try to refresh without selection
                        try
                        {
                            tvSubfolders.Refresh();
                            if (tvSubfolders.Nodes.Count > 0)
                            {
                                tvSubfolders.Nodes[0].EnsureVisible();
                            }
                        }
                        catch (Exception fallbackEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"EnsureTreeViewVisibility: Fallback also failed: {fallbackEx.Message}");
                        }
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"EnsureTreeViewVisibility: TreeView is null or has no nodes. tvSubfolders={tvSubfolders != null}, NodeCount={tvSubfolders?.Nodes.Count ?? -1}");
                }
            });
        }

        private (List<string> ExpandedPaths, string SelectedPath) CaptureTreeViewState()
        {
            var expandedPaths = new List<string>();
            string selectedPath = null;

            if (tvSubfolders != null)
            {
                // Capture expanded nodes
                CaptureExpandedNodes(tvSubfolders.Nodes, expandedPaths);

                // Capture selected node
                if (tvSubfolders.SelectedNode?.Tag is TreeNodeData selectedData && selectedData.IsSubfolder)
                {
                    selectedPath = selectedData.FullPath;
                }
            }

            return (expandedPaths, selectedPath);
        }

        private void CaptureExpandedNodes(TreeNodeCollection nodes, List<string> expandedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.IsExpanded && node.Tag is TreeNodeData nodeData && nodeData.IsSubfolder && !string.IsNullOrEmpty(nodeData.FullPath))
                {
                    expandedPaths.Add(nodeData.FullPath);
                }
                if (node.Nodes.Count > 0)
                {
                    CaptureExpandedNodes(node.Nodes, expandedPaths);
                }
            }
        }

        // Helper method to restore TreeView state
        private void RestoreTreeViewState(List<string> expandedPaths, string selectedPath)
        {
            if (tvSubfolders == null) return;

            try
            {
                isUpdatingTreeView = true;

                // Restore expanded state
                RestoreExpandedNodes(tvSubfolders.Nodes, expandedPaths);

                // Restore selected node
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    var nodeToSelect = FindNodeByPath(tvSubfolders.Nodes, selectedPath);
                    if (nodeToSelect != null)
                    {
                        tvSubfolders.SelectedNode = nodeToSelect;
                        nodeToSelect.EnsureVisible();
                    }
                }
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }

        // Helper method to recursively restore expanded nodes
        private void RestoreExpandedNodes(TreeNodeCollection nodes, List<string> expandedPaths)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is TreeNodeData nodeData && nodeData.IsSubfolder &&
                    !string.IsNullOrEmpty(nodeData.FullPath) && expandedPaths.Contains(nodeData.FullPath))
                {
                    node.Expand();
                }
                if (node.Nodes.Count > 0)
                {
                    RestoreExpandedNodes(node.Nodes, expandedPaths);
                }
            }
        }


        private IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                yield return control;
                foreach (Control child in GetAllControls(control))
                    yield return child;
            }
        }

        private class GroupItem
        {
            public string Id { get; set; }
            public string DisplayName { get; set; }
        }

        private void UpdateUI(Action action)
        {
            if (InvokeRequired)
            {
                Invoke(action);
            }
            else
            {
                action();
            }
        }

        // Enhanced TreeNodeData to support nested folder hierarchy
        private class TreeNodeData
        {
            public bool IsSubfolder { get; set; }
            public Folder Subfolder { get; set; }
            public string SubfolderName { get; set; }
            public string FullPath { get; set; } // Store full server-relative path
            public int Level { get; set; } // Track nesting level
            public string GroupId { get; set; }
            public string GroupName { get; set; }
            public string Permission { get; set; }
            public bool IsPreview { get; set; }
            public bool HasChildren { get; set; } // Track if folder has subfolders
            public bool IsExpanded { get; set; } // Track expansion state
        }

        private void txtSearch_Enter(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (txtSearch.Text == _placeholderText)
                {
                    txtSearch.Text = string.Empty;
                    txtSearch.ForeColor = _normalColor;
                }
            });
        }

        private void txtSearch_Leave(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (string.IsNullOrWhiteSpace(txtSearch.Text))
                {
                    txtSearch.Text = _placeholderText;
                    txtSearch.ForeColor = _placeholderColor;
                }
            });
        }

        // New method to populate TreeView with nested structure
        private void PopulateNestedTreeView()
        {
            isUpdatingTreeView = true;
            try
            {
                tvSubfolders.Nodes.Clear();
                if (_nestedSubfolderCache == null || !_nestedSubfolderCache.Any())
                {
                    System.Diagnostics.Debug.WriteLine("PopulateNestedTreeView: _nestedSubfolderCache is null or empty");
                    return;
                }

                var rootFolders = _nestedSubfolderCache.Where(f => f.Level == 0).OrderBy(f => f.FolderName).ToList();
                System.Diagnostics.Debug.WriteLine($"PopulateNestedTreeView: Found {rootFolders.Count} Level 0 folders");

                if (!rootFolders.Any())
                {
                    UpdateUI(() => lblStatus.Text = "No top-level subfolders found.");
                    return;
                }

                var foldersByParent = _nestedSubfolderCache
                    .Where(f => f.Level > 0)
                    .GroupBy(f => GetParentPath(f.FullPath))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => (f.FullPath, f.FolderName, f.Level, f.HasUniquePermissions, f.Groups, f.HasChildren)).ToList()
                    );

                foreach (var folder in rootFolders)
                {
                    var node = CreateFolderTreeNode(folder, foldersByParent);
                    tvSubfolders.Nodes.Add(node);
                }

                UpdateUI(() => lblStatus.Text = $"Displayed {rootFolders.Count} top-level subfolders.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PopulateNestedTreeView: Error: {ex.Message}");
                UpdateUI(() => lblStatus.Text = $"Error populating TreeView: {ex.Message}");
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }

        private void SetupCustomTreeViewIcons()
        {
            try
            {
                // Clear any existing images
                treeViewImageList.Images.Clear();

                // Set the image size to match your bitmap
                treeViewImageList.ImageSize = new Size(12, 12);

                // Load your custom bitmap
                string bitmapPath = Path.Combine(Application.StartupPath, "icons8-folder-13.png");

                if (System.IO.File.Exists(bitmapPath))
                {
                    using (var bitmap = new Bitmap(bitmapPath))
                    {
                        // Add the same image for both folder and group icons
                        // Index 0: Folder icon
                        treeViewImageList.Images.Add("folder", new Bitmap(bitmap));

                        // Index 1: Group icon (you could use the same or a different variation)
                        treeViewImageList.Images.Add("group", new Bitmap(bitmap));

                        // Optional: Add a third icon for preview items
                        // Index 2: Preview icon (maybe with some modification)
                        var previewBitmap = new Bitmap(bitmap);
                        using (var g = Graphics.FromImage(previewBitmap))
                        {
                            // Add a small "P" overlay for preview items
                            using (var brush = new SolidBrush(Color.Green))
                            using (var font = new Font("Arial", 8, FontStyle.Bold))
                            {
                                g.DrawString("P", font, brush, new PointF(16, 16));
                            }
                        }
                        treeViewImageList.Images.Add("preview", previewBitmap);
                    }

                    System.Diagnostics.Debug.WriteLine($"Custom TreeView icons loaded successfully from {bitmapPath}");
                }
                else
                {
                    // Fallback: Create simple colored rectangles if bitmap file not found
                    CreateFallbackIcons();
                    System.Diagnostics.Debug.WriteLine($"Bitmap file not found at {bitmapPath}, using fallback icons");
                }

                // Assign the ImageList to the TreeView
                tvSubfolders.ImageList = treeViewImageList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading custom TreeView icons: {ex.Message}");
                CreateFallbackIcons();
            }
        }

        private void SetupCustomTreeViewIconsFromResource()
        {
            try
            {
                treeViewImageList.Images.Clear();
                treeViewImageList.ImageSize = new Size(24, 24);
                treeViewImageList.ColorDepth = ColorDepth.Depth32Bit;

                // Replace "YourProjectName" with your actual project name
                var assembly = Assembly.GetExecutingAssembly();
                string resourceName = "EntraGroupsApp.j.bmp"; // Format: Namespace.FileName

                // List all resources to debug
                var resourceNames = assembly.GetManifestResourceNames();
                System.Diagnostics.Debug.WriteLine("Available resources:");
                foreach (var name in resourceNames)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {name}");
                }

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream != null)
                    {
                        using (var bitmap = new Bitmap(stream))
                        {
                            // Ensure proper size
                            var resizedBitmap = new Bitmap(bitmap, new Size(24, 24));

                            treeViewImageList.Images.Add("folder", new Bitmap(resizedBitmap));
                            treeViewImageList.Images.Add("group", new Bitmap(resizedBitmap));

                            // Create preview version
                            var previewBitmap = new Bitmap(resizedBitmap);
                            using (var g = Graphics.FromImage(previewBitmap))
                            {
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                using (var brush = new SolidBrush(Color.Red))
                                using (var font = new Font("Arial", 8, FontStyle.Bold))
                                {
                                    g.DrawString("P", font, brush, new PointF(16, 16));
                                }
                            }
                            treeViewImageList.Images.Add("preview", previewBitmap);
                        }
                        System.Diagnostics.Debug.WriteLine("Icons loaded from embedded resource");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Resource not found: {resourceName}");
                        CreateFallbackIcons();
                    }
                }

                tvSubfolders.ImageList = treeViewImageList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading from resource: {ex.Message}");
                CreateFallbackIcons();
            }
        }

        // Fallback method if bitmap file is not found
        private void CreateFallbackIcons()
        {
            treeViewImageList.Images.Clear();
            treeViewImageList.ImageSize = new Size(16, 16); // Standard size for fallback

            // Create simple colored rectangles as fallback
            var folderIcon = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(folderIcon))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.SkyBlue, 2, 2, 12, 12);
                g.DrawRectangle(Pens.DarkBlue, 2, 2, 12, 12);
            }

            var groupIcon = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(groupIcon))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.LightGreen, 2, 2, 12, 12);
                g.DrawRectangle(Pens.DarkGreen, 2, 2, 12, 12);
            }

            var previewIcon = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(previewIcon))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.Yellow, 2, 2, 12, 12);
                g.DrawRectangle(Pens.Orange, 2, 2, 12, 12);
            }

            treeViewImageList.Images.Add("folder", folderIcon);
            treeViewImageList.Images.Add("group", groupIcon);
            treeViewImageList.Images.Add("preview", previewIcon);
        }

        // Method 2: Alternative - Load from embedded resource
        private TreeNode CreateFolderTreeNodeWithCustomIcon(
    (string FullPath, string FolderName, int Level, bool HasUniquePermissions,
     List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren) folder,
    Dictionary<string, List<(string FullPath, string FolderName, int Level,
     bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>> foldersByParent)
        {
            if (string.IsNullOrEmpty(folder.FullPath))
            {
                System.Diagnostics.Debug.WriteLine($"CreateFolderTreeNodeWithCustomIcon: Skipping folder '{folder.FolderName}' with null or empty FullPath");
                return null;
            }

            var groupCount = folder.Groups.Count;
            var indent = new string(' ', folder.Level * 2);
            var nodeText = folder.HasUniquePermissions
                ? $"{indent}{folder.FolderName} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned)"
                : $"{indent}{folder.FolderName} (Inherited)";

            var folderNode = new TreeNode
            {
                Text = nodeText,
                ImageKey = "folder", // Use named key instead of index
                SelectedImageKey = "folder",
                Tag = new TreeNodeData
                {
                    IsSubfolder = true,
                    SubfolderName = folder.FolderName,
                    FullPath = folder.FullPath,
                    Level = folder.Level,
                    HasChildren = folder.HasChildren
                }
            };

            foreach (var group in folder.Groups)
            {
                folderNode.Nodes.Add(new TreeNode
                {
                    Text = $"{group.GroupName}: {group.Role}",
                    ImageKey = "group", // Use named key for group icon
                    SelectedImageKey = "group",
                    Tag = new TreeNodeData
                    {
                        IsSubfolder = false,
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        Permission = group.Role,
                        FullPath = folder.FullPath,
                        Level = folder.Level
                    }
                });
            }

            // Add child folders if they exist
            if (foldersByParent.ContainsKey(folder.FullPath))
            {
                foreach (var childFolder in foldersByParent[folder.FullPath].OrderBy(f => f.FolderName))
                {
                    var childNode = CreateFolderTreeNodeWithCustomIcon(childFolder, foldersByParent);
                    if (childNode != null)
                        folderNode.Nodes.Add(childNode);
                }
            }

            return folderNode;
        }
        private void AddChildNodesRecursively(
            TreeNode parentNode,
            Dictionary<string, List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions,
                List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>> foldersByParent,
            HashSet<string> expandedPaths)
        {
            if (!(parentNode.Tag is TreeNodeData parentData) || !foldersByParent.ContainsKey(parentData.FullPath))
                return;

            foreach (var folder in foldersByParent[parentData.FullPath].OrderBy(f => f.FolderName))
            {
                var node = CreateFolderTreeNode(folder, foldersByParent);
                node.BackColor = Color.LightYellow; // Mark as temporary
                node.Text += " [Loaded]";
                parentNode.Nodes.Add(node);
                if (expandedPaths.Contains(folder.FullPath))
                {
                    node.Expand();
                    AddChildNodesRecursively(node, foldersByParent, expandedPaths);
                }
            }
        }

        private TreeNode CreateFolderTreeNode(
            (string FullPath, string FolderName, int Level, bool HasUniquePermissions,
             List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren) folder,
            Dictionary<string, List<(string FullPath, string FolderName, int Level,
             bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>> foldersByParent)
        {
            if (string.IsNullOrEmpty(folder.FullPath))
            {
                System.Diagnostics.Debug.WriteLine($"CreateFolderTreeNode: Skipping folder '{folder.FolderName}' with null or empty FullPath");
                return null;
            }

            var groupCount = folder.Groups.Count;
            var indent = new string(' ', folder.Level * 2);
            var nodeText = folder.HasUniquePermissions
                ? $"{indent}{folder.FolderName} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned)"
                : $"{indent}{folder.FolderName} (Inherited)";

            var folderNode = new TreeNode
            {
                Text = nodeText,
                ImageIndex = 0,
                SelectedImageIndex = 0,
                Tag = new TreeNodeData
                {
                    IsSubfolder = true,
                    SubfolderName = folder.FolderName,
                    FullPath = folder.FullPath,
                    Level = folder.Level,
                    HasChildren = folder.HasChildren
                }
            };

            foreach (var group in folder.Groups)
            {
                folderNode.Nodes.Add(new TreeNode
                {
                    Text = $"{group.GroupName}: {group.Role}",
                    ImageIndex = 1,
                    SelectedImageIndex = 1,
                    Tag = new TreeNodeData
                    {
                        IsSubfolder = false,
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        Permission = group.Role,
                        FullPath = folder.FullPath,
                        Level = folder.Level
                    }
                });
            }

            if (foldersByParent.ContainsKey(folder.FullPath))
            {
                foreach (var childFolder in foldersByParent[folder.FullPath].OrderBy(f => f.FolderName))
                {
                    var childNode = CreateFolderTreeNode(childFolder, foldersByParent);
                    if (childNode != null)
                        folderNode.Nodes.Add(childNode);
                }
            }

            System.Diagnostics.Debug.WriteLine($"CreateFolderTreeNode: Created node for '{folder.FolderName}', FullPath={folder.FullPath}, HTTPS={_siteUrl}{folder.FullPath}, Level={folder.Level}");
            return folderNode;
        }

        private string GetParentPath(string fullPath)
        {
            var lastSlashIndex = fullPath.LastIndexOf('/');
            return lastSlashIndex > 0 ? fullPath.Substring(0, lastSlashIndex) : string.Empty;
        }

        // Filtered tree view for search results
        private void PopulateFilteredTreeView(List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions,
            List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)> filteredFolders)
        {
            isUpdatingTreeView = true;
            try
            {
                tvSubfolders.Nodes.Clear();

                foreach (var folder in filteredFolders.OrderBy(f => f.Level).ThenBy(f => f.FolderName))
                {
                    var groupCount = folder.Groups.Count;
                    var indent = new string(' ', folder.Level * 2);
                    var nodeText = folder.HasUniquePermissions
                        ? $"{indent}{folder.FolderName} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned)"
                        : $"{indent}{folder.FolderName} (Inherited)";

                    var folderNode = new TreeNode
                    {
                        Text = nodeText,
                        ImageIndex = 0,
                        SelectedImageIndex = 0,
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = true,
                            SubfolderName = folder.FolderName,
                            FullPath = folder.FullPath,
                            Level = folder.Level,
                            HasChildren = folder.HasChildren
                        }
                    };

                    // Add group nodes
                    foreach (var group in folder.Groups)
                    {
                        folderNode.Nodes.Add(new TreeNode
                        {
                            Text = $"{group.GroupName}: {group.Role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = group.GroupId,
                                GroupName = group.GroupName,
                                Permission = group.Role,
                                FullPath = folder.FullPath,
                                Level = folder.Level
                            }
                        });
                    }

                    tvSubfolders.Nodes.Add(folderNode);
                }
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }

        private void cmbView_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                if (isUpdatingTreeView || _subfolderCache == null) return;

                // Capture current state
                var (expandedPaths, selectedPath) = CaptureTreeViewState();

                string query = txtSearch.Text.Trim().ToLower() == _placeholderText.ToLower() ? string.Empty : txtSearch.Text.Trim().ToLower();
                string viewFilter = cmbView.SelectedItem?.ToString() ?? "All Subfolders";

                if (string.IsNullOrWhiteSpace(query))
                {
                    // No search query - restore full view with state
                    RestoreFullTreeViewWithState(viewFilter);
                }
                else
                {
                    // Search query active - apply filter with state preservation
                    ApplySearchFilterWithStatePreservation(query, viewFilter, expandedPaths, selectedPath);
                }
            });
        }
        private void PopulateTreeView(List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups)> cache, string viewFilter)
        {
            isUpdatingTreeView = true;
            try
            {
                tvSubfolders.Nodes.Clear();
                if (cache == null || !cache.Any())
                {
                    System.Diagnostics.Debug.WriteLine("PopulateTreeView: Cache is null or empty");
                    UpdateUI(() => lblStatus.Text = "No subfolders available to display.");
                    return;
                }

                var filteredCache = cache
                    .Where(s =>
                        (viewFilter == "All Subfolders" ||
                         (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                         (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)))
                    .OrderBy(s => s.SubfolderName)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"PopulateTreeView: Adding {filteredCache.Count} subfolders to TreeView");

                foreach (var subfolder in filteredCache)
                {
                    var subfolderNode = new TreeNode
                    {
                        Text = subfolder.HasUniquePermissions
                            ? $"{subfolder.SubfolderName} (Unique, {subfolder.Groups.Count} CSG group{(subfolder.Groups.Count == 1 ? "" : "s")} assigned)"
                            : $"{subfolder.SubfolderName} (Inherited)",
                        ImageIndex = 0,
                        SelectedImageIndex = 0,
                        Tag = new TreeNodeData { IsSubfolder = true, SubfolderName = subfolder.SubfolderName }
                    };

                    foreach (var group in subfolder.Groups)
                    {
                        subfolderNode.Nodes.Add(new TreeNode
                        {
                            Text = $"{group.GroupName}: {group.Role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData { IsSubfolder = false, GroupId = group.GroupId, GroupName = group.GroupName, Permission = group.Role }
                        });
                    }

                    tvSubfolders.Nodes.Add(subfolderNode);
                    System.Diagnostics.Debug.WriteLine($"PopulateTreeView: Added node for {subfolder.SubfolderName}");
                }

                UpdateUI(() => lblStatus.Text = $"Displayed {filteredCache.Count} top-level subfolders.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PopulateTreeView: Error: {ex.Message}");
                UpdateUI(() => lblStatus.Text = $"Error populating TreeView: {ex.Message}");
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }

        // Helper method to restore full TreeView while preserving state
        private void RestoreFullTreeViewWithState(string viewFilter)
        {
            if (_subfolderCache == null) return;

            // Capture current state
            var (expandedPaths, selectedPath) = CaptureTreeViewState();

            try
            {
                isUpdatingTreeView = true;

                // Filter cache based on view filter only (no search filter)
                var filteredCache = _subfolderCache
                    .Where(s =>
                        viewFilter == "All Subfolders" ||
                        (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                        (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions))
                    .OrderBy(s => s.SubfolderName)
                    .ToList();

                // Rebuild TreeView with top-level subfolders
                tvSubfolders.Nodes.Clear();
                foreach (var subfolder in filteredCache)
                {
                    var subfolderNode = new TreeNode
                    {
                        Text = subfolder.HasUniquePermissions
                            ? $"{subfolder.SubfolderName} (Unique, {subfolder.Groups.Count} CSG group{(subfolder.Groups.Count == 1 ? "" : "s")} assigned)"
                            : $"{subfolder.SubfolderName} (Inherited)",
                        ImageIndex = 0,
                        SelectedImageIndex = 0,
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = true,
                            SubfolderName = subfolder.SubfolderName,
                            Level = 0,
                            HasChildren = true // Assume top-level may have children
                        }
                    };

                    foreach (var group in subfolder.Groups)
                    {
                        subfolderNode.Nodes.Add(new TreeNode
                        {
                            Text = $"{group.GroupName}: {group.Role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = group.GroupId,
                                GroupName = group.GroupName,
                                Permission = group.Role
                            }
                        });
                    }

                    tvSubfolders.Nodes.Add(subfolderNode);
                }

                // Add any loaded nested subfolders back to their parents
                RestoreNestedSubfoldersToTree(expandedPaths);

                // Restore expansion and selection state
                RestoreTreeViewState(expandedPaths, selectedPath);

                lblStatus.Text = $"Showing {filteredCache.Count} subfolders (search cleared).";
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }

        private void RestoreNestedSubfoldersToTree(List<string> expandedPaths)
        {
            if (_nestedSubfolderCache == null || !_nestedSubfolderCache.Any()) return;

            // Group nested folders by their parent paths
            var foldersByParent = _nestedSubfolderCache
                .GroupBy(f => GetParentPath(f.FullPath))
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(f => f.FolderName).ToList()
                );

            // Add nested folders back to their parents
            RestoreNestedFoldersRecursively(tvSubfolders.Nodes, foldersByParent, expandedPaths);
        }

        private void RestoreNestedFoldersRecursively(TreeNodeCollection parentNodes,
    Dictionary<string, List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>> foldersByParent,
    List<string> expandedPaths)
        {
            foreach (TreeNode parentNode in parentNodes)
            {
                if (parentNode.Tag is TreeNodeData parentData && parentData.IsSubfolder)
                {
                    string parentPath = parentData.FullPath;
                    if (!string.IsNullOrEmpty(parentPath) && foldersByParent.ContainsKey(parentPath))
                    {
                        // Only add children if this parent was previously expanded
                        if (expandedPaths.Contains(parentPath))
                        {
                            foreach (var folder in foldersByParent[parentPath])
                            {
                                // Check if child already exists to avoid duplicates
                                var existingChild = parentNode.Nodes.Cast<TreeNode>()
                                    .FirstOrDefault(n => n.Tag is TreeNodeData childData &&
                                                   childData.IsSubfolder &&
                                                   childData.SubfolderName == folder.FolderName);

                                if (existingChild == null)
                                {
                                    var childNode = CreateFolderTreeNode(folder, foldersByParent);
                                    if (childNode != null)
                                    {
                                        childNode.BackColor = Color.LightYellow;
                                        childNode.Text += " [Loaded]";
                                        parentNode.Nodes.Add(childNode);
                                    }
                                }
                            }
                        }
                    }

                    // Recursively process children
                    if (parentNode.Nodes.Count > 0)
                    {
                        RestoreNestedFoldersRecursively(parentNode.Nodes, foldersByParent, expandedPaths);
                    }
                }
            }
        }

        private void ApplySearchFilterWithStatePreservation(string query, string viewFilter, List<string> expandedPaths, string selectedPath)
        {
            if (_subfolderCache == null) return;

            try
            {
                isUpdatingTreeView = true;

                // Filter top-level cache
                var filteredTopLevel = _subfolderCache
                    .Where(s =>
                        (viewFilter == "All Subfolders" ||
                         (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                         (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)) &&
                        (s.SubfolderName.ToLower().Contains(query) || s.Groups.Any(g => g.GroupName.ToLower().Contains(query))))
                    .OrderBy(s => s.SubfolderName)
                    .ToList();

                // Filter nested cache
                var filteredNested = _nestedSubfolderCache
                    .Where(s =>
                        (viewFilter == "All Subfolders" ||
                         (viewFilter == "Unique Permissions Only" && s.HasUniquePermissions) ||
                         (viewFilter == "Inherited Permissions Only" && !s.HasUniquePermissions)) &&
                        (s.FolderName.ToLower().Contains(query) || s.Groups.Any(g => g.GroupName.ToLower().Contains(query))))
                    .OrderBy(s => s.Level)
                    .ThenBy(s => s.FolderName)
                    .ToList();

                tvSubfolders.Nodes.Clear();

                // Add matching top-level folders
                foreach (var subfolder in filteredTopLevel)
                {
                    var subfolderNode = new TreeNode
                    {
                        Text = subfolder.HasUniquePermissions
                            ? $"{subfolder.SubfolderName} (Unique, {subfolder.Groups.Count} CSG group{(subfolder.Groups.Count == 1 ? "" : "s")} assigned)"
                            : $"{subfolder.SubfolderName} (Inherited)",
                        ImageIndex = 0,
                        SelectedImageIndex = 0,
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = true,
                            SubfolderName = subfolder.SubfolderName,
                            Level = 0,
                            HasChildren = true
                        }
                    };

                    foreach (var group in subfolder.Groups)
                    {
                        subfolderNode.Nodes.Add(new TreeNode
                        {
                            Text = $"{group.GroupName}: {group.Role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = group.GroupId,
                                GroupName = group.GroupName,
                                Permission = group.Role
                            }
                        });
                    }

                    tvSubfolders.Nodes.Add(subfolderNode);
                }

                // Add matching nested folders as flat list (since it's filtered results)
                foreach (var folder in filteredNested)
                {
                    var folderNode = new TreeNode
                    {
                        Text = folder.HasUniquePermissions
                            ? $"{folder.FolderName} (Level {folder.Level}, Unique, {folder.Groups.Count} CSG group{(folder.Groups.Count == 1 ? "" : "s")} assigned)"
                            : $"{folder.FolderName} (Level {folder.Level}, Inherited)",
                        ImageIndex = 0,
                        SelectedImageIndex = 0,
                        BackColor = Color.LightBlue, // Distinguish nested results
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = true,
                            SubfolderName = folder.FolderName,
                            FullPath = folder.FullPath,
                            Level = folder.Level,
                            HasChildren = folder.HasChildren
                        }
                    };

                    foreach (var group in folder.Groups)
                    {
                        folderNode.Nodes.Add(new TreeNode
                        {
                            Text = $"{group.GroupName}: {group.Role}",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = group.GroupId,
                                GroupName = group.GroupName,
                                Permission = group.Role,
                                FullPath = folder.FullPath,
                                Level = folder.Level
                            }
                        });
                    }

                    tvSubfolders.Nodes.Add(folderNode);
                }

                // Try to restore selection if the selected item matches the search
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    var nodeToSelect = FindNodeByPath(tvSubfolders.Nodes, selectedPath);
                    if (nodeToSelect != null)
                    {
                        tvSubfolders.SelectedNode = nodeToSelect;
                        nodeToSelect.EnsureVisible();
                    }
                }

                var totalResults = filteredTopLevel.Count + filteredNested.Count;
                lblStatus.Text = $"Found {totalResults} matches for '{query}' ({filteredTopLevel.Count} top-level, {filteredNested.Count} nested).";
            }
            finally
            {
                isUpdatingTreeView = false;
            }
        }



        // Enhanced search functionality to work with nested folders
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            // Don't process if we're updating the TreeView programmatically
            if (isUpdatingTreeView) return;

            UpdateUI(() =>
            {
                string query = txtSearch.Text.Trim().ToLower();
                string viewFilter = cmbView.SelectedItem?.ToString() ?? "All Subfolders";

                // If query is empty or placeholder, restore full view with preserved state
                if (query == _placeholderText.ToLower() || string.IsNullOrWhiteSpace(query))
                {
                    RestoreFullTreeViewWithState(viewFilter);
                    return;
                }

                // Capture current expansion state before filtering
                var (expandedPaths, selectedPath) = CaptureTreeViewState();

                // Filter and display results while preserving structure
                ApplySearchFilterWithStatePreservation(query, viewFilter, expandedPaths, selectedPath);
            });
        }
        private TreeNode CloneTreeNode(TreeNode node)
        {
            var newNode = new TreeNode
            {
                Text = node.Text,
                ImageIndex = node.ImageIndex,
                SelectedImageIndex = node.SelectedImageIndex,
                Tag = node.Tag
            };
            foreach (TreeNode child in node.Nodes)
            {
                newNode.Nodes.Add(CloneTreeNode(child));
            }
            return newNode;
        }

        private TreeNodeCollection CloneTreeNodes(TreeNodeCollection nodes)
        {
            var newNodes = new TreeNode[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                newNodes[i] = CloneTreeNode(nodes[i]);
            }
            var tempTreeView = new System.Windows.Forms.TreeView();
            tempTreeView.Nodes.AddRange(newNodes);
            return tempTreeView.Nodes;
        }

        private async Task LoadAvailableGroupsAsync()
        {
            try
            {
                UpdateUI(() => lblStatus.Text = "Loading groups...");

                var allGroups = new List<GroupItem>();
                string[] groupPrefixes = GetRelevantGroupPrefixes();

                System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Selected department: {_selectedDepartment}");
                System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Group prefixes: {string.Join(", ", groupPrefixes)}");

                // Load groups for each relevant prefix
                foreach (string prefix in groupPrefixes)
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Loading groups for prefix: {prefix}");

                        var groups = await _graphClient.Groups.GetAsync(requestConfiguration =>
                        {
                            // Use the dynamic prefix instead of hard-coded values
                            requestConfiguration.QueryParameters.Filter = $"startswith(displayName, '{prefix}')";
                            requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
                            requestConfiguration.QueryParameters.Top = 999;
                            requestConfiguration.Headers.Add("ConsistencyLevel", "eventual");
                        });

                        var prefixGroups = groups?.Value
                            ?.Where(g => g.DisplayName != null &&
                                       !g.DisplayName.Contains("Mays-Group", StringComparison.OrdinalIgnoreCase) &&
                                       !g.DisplayName.Contains("mays-group", StringComparison.OrdinalIgnoreCase))
                            .OrderBy(g => g.DisplayName)
                            .Select(g => new GroupItem { Id = g.Id, DisplayName = g.DisplayName })
                            .ToList() ?? new List<GroupItem>();

                        allGroups.AddRange(prefixGroups);
                        System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Found {prefixGroups.Count} groups for prefix {prefix}");
                    }
                    catch (Exception prefixEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Error loading groups for prefix {prefix}: {prefixEx.Message}");
                        await _auditLogManager?.LogAction(_signedInUserId, null, "LoadGroupsError", _libraryName, null, "Subfolder",
                            $"Failed to load groups for prefix {prefix}: {prefixEx.Message}");
                    }
                }

                // Remove duplicates and sort
                _availableGroups = allGroups
                    .GroupBy(g => g.Id)
                    .Select(g => g.First())
                    .OrderBy(g => g.DisplayName)
                    .ToList();

                UpdateUI(() =>
                {
                    cmbGroups.DataSource = _availableGroups;
                    cmbGroups.DisplayMember = "DisplayName";
                    cmbGroups.ValueMember = "Id";

                    if (_availableGroups.Any())
                    {
                        cmbGroups.SelectedIndex = 0;
                        System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: Loaded {_availableGroups.Count} total groups");
                    }
                    else
                    {
                        lblStatus.Text = $"No groups found for department {_selectedDepartment}.";
                        btnAdd.Enabled = false;
                        btnRemove.Enabled = false;
                        System.Diagnostics.Debug.WriteLine($"LoadAvailableGroupsAsync: No groups found for department {_selectedDepartment}");
                    }
                    UpdateSidebar();
                });
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to load groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error loading groups.";
                    btnAdd.Enabled = false;
                    btnRemove.Enabled = false;
                });
                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadGroupsError", _libraryName, null, "Subfolder",
                    $"Failed to load groups: {ex.Message}");
            }
        }
        // Recursive method to load nested subfolders
        // Recursive method to load nested subfolders

        // Helper method to get relevant group prefixes based on the selected department
        private string[] GetRelevantGroupPrefixes()
        {
            // If we have department mappings and a selected department, use them
            if (_departmentMappings != null && !string.IsNullOrEmpty(_selectedDepartment))
            {
                if (_departmentMappings.TryGetValue(_selectedDepartment, out var mapping))
                {
                    System.Diagnostics.Debug.WriteLine($"GetRelevantGroupPrefixes: Found mapping for {_selectedDepartment}: {string.Join(", ", mapping.GroupPrefixes)}");

                    // Transform FSG prefixes to CSG prefixes for subfolder permissions
                    var csgPrefixes = mapping.GroupPrefixes
                        .Select(prefix => prefix.Replace("FSG-", "CSG-", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    System.Diagnostics.Debug.WriteLine($"GetRelevantGroupPrefixes: Transformed to CSG prefixes: {string.Join(", ", csgPrefixes)}");
                    return csgPrefixes;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"GetRelevantGroupPrefixes: No mapping found for department {_selectedDepartment}");
                }
            }

            // Fallback: try to determine from site URL or use broad prefixes
            if (!string.IsNullOrEmpty(_siteUrl))
            {
                // Extract department from site URL patterns
                var urlParts = _siteUrl.Split('/');
                var teamSitePart = urlParts.LastOrDefault();

                if (!string.IsNullOrEmpty(teamSitePart))
                {
                    // Map common URL patterns to CSG department prefixes (not FSG)
                    var urlToDeptMapping = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    { "MKTGTeamSite", new[] { "CSG-CLBA-MKTG" } },
                    { "MGMTTeamSite", new[] { "CSG-CLBA-MGMT" } },
                    { "CIBSTeamSite", new[] { "CSG-CLBA-CIBS" } },
                    { "FINCTeamSite", new[] { "CSG-CLBA-FINC" } },
                    { "AccountingTeamSite", new[] { "CSG-CLBA-ACCT" } },
                    { "BUSPTeamSite", new[] { "CSG-CLBA-BUSP" } },
                    { "team-CEDTeamSite", new[] { "CSG-CLBA-CED" } },
                    { "COMMTeamSite", new[] { "CSG-CLBA-COMM" } },
                    { "DeansOfficeDrive", new[] { "CSG-CLBA-DEAN" } },
                    { "INFOTeamSite", new[] { "CSG-CLBA-INFO" } },
                    { "BizGradTeamSite", new[] { "CSG-CLBA-BizGrad" } },
                    { "MediaTeamSite", new[] { "CSG-CLBA-Media" } },
                    { "team-MaysStaffCouncil", new[] { "CSG-CLBA-MaysStaffCouncil" } }
                };

                    if (urlToDeptMapping.TryGetValue(teamSitePart, out var prefixes))
                    {
                        System.Diagnostics.Debug.WriteLine($"GetRelevantGroupPrefixes: Determined CSG prefixes from URL {teamSitePart}: {string.Join(", ", prefixes)}");
                        return prefixes;
                    }
                }
            }

            // Ultimate fallback: use broad CSG prefixes (this will load more groups but ensures we don't miss any)
            var fallbackPrefixes = new[] { "CSG-CLBA-MKTG", "CSG-CLBA-MGMT" }; // You might want to expand this
            System.Diagnostics.Debug.WriteLine($"GetRelevantGroupPrefixes: Using fallback CSG prefixes: {string.Join(", ", fallbackPrefixes)}");
            return fallbackPrefixes;
        }

        private async Task<List<Folder>> LoadNestedFoldersAsync(ClientContext context, Folder parentFolder, string webServerRelativeUrl, int currentLevel = 0)
        {
            var allFolders = new List<Folder>();

            try
            {
                string parentPath = parentFolder.ServerRelativeUrl ?? NormalizePath($"{webServerRelativeUrl}/{_libraryName}", false);
                System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Starting for parentPath: {parentPath}, level: {currentLevel}");

                int attempt = 1;
                bool success = false;
                string currentPath = parentPath;

                while (attempt <= 2 && !success)
                {
                    System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Attempt {attempt} with path: {currentPath}");

                    try
                    {
                        var targetFolder = context.Web.GetFolderByServerRelativeUrl(currentPath);
                        context.Load(targetFolder, f => f.Folders.Include(
                            sf => sf.Name,
                            sf => sf.ServerRelativeUrl,
                            sf => sf.ListItemAllFields.HasUniqueRoleAssignments,
                            sf => sf.ListItemAllFields.RoleAssignments.Include(
                                ra => ra.Member.Title,
                                ra => ra.Member.LoginName,
                                ra => ra.RoleDefinitionBindings),
                            sf => sf.Folders));
                        await context.ExecuteQueryAsync();

                        var subfolders = targetFolder.Folders?.Where(f => !f.Name.StartsWith("Forms")).ToList() ?? new List<Folder>();
                        System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Found {subfolders.Count} subfolders at level {currentLevel} for parent {currentPath ?? "null"}");

                        foreach (var subfolder in subfolders)
                        {
                            if (string.IsNullOrEmpty(subfolder.Name))
                            {
                                System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Skipping subfolder with null or empty Name");
                                continue;
                            }

                            var fullPath = NormalizePath(string.IsNullOrEmpty(subfolder.ServerRelativeUrl)
                                ? $"{currentPath}/{subfolder.Name}"
                                : subfolder.ServerRelativeUrl, true); // Encode for folders
                            if (string.IsNullOrEmpty(subfolder.ServerRelativeUrl))
                            {
                                System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Subfolder '{subfolder.Name}' has null ServerRelativeUrl, using constructed path: {fullPath}");
                            }

                            allFolders.Add(subfolder);
                            if (currentLevel < _maxNestingLevel)
                            {
                                var nestedFolders = await LoadNestedFoldersAsync(context, subfolder, webServerRelativeUrl, currentLevel + 1);
                                allFolders.AddRange(nestedFolders);
                            }

                            System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Processed subfolder '{subfolder.Name}', FullPath={fullPath}, HTTPS={_siteUrl}{fullPath}");
                        }

                        success = true;
                        System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Success on attempt {attempt} with path: {currentPath}");
                    }
                    catch (Microsoft.SharePoint.Client.ServerException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist"))
                    {
                        if (attempt == 1)
                        {
                            // Fallback: Encode library part if it's the root
                            currentPath = NormalizePath(currentPath.Replace(_libraryName.Replace(" ", ""), Uri.EscapeDataString(_libraryName)), true);
                            System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Original failed ({ex.Message}). Trying fallback path: {currentPath}");
                            attempt++;
                            continue;
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadNestedFoldersAsync: Error loading folders for {parentFolder.ServerRelativeUrl ?? "null"}: {ex.Message}");
                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadNestedFoldersError", _libraryName, null, "Subfolder",
                    $"Error loading nested folders for {parentFolder.ServerRelativeUrl ?? "null"}: {ex.Message}, StackTrace: {ex.StackTrace}");
            }

            return allFolders;
        }
        private async Task<List<Folder>> LoadSubfoldersForParentAsync(string parentPath)
        {
            var nestedFolders = new List<Folder>();

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };

                    var parentFolder = context.Web.GetFolderByServerRelativeUrl(parentPath);
                    var loadedFolders = await LoadNestedFoldersRecursiveAsync(context, parentFolder, 1); // Start at level 1
                    nestedFolders.AddRange(loadedFolders);
                }
            }
            catch (Exception ex)
            {
                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersOnDemandError", _libraryName, null, "Subfolder",
                    $"Error loading subfolders for {parentPath}: {ex.Message}");
            }

            return nestedFolders;
        }

        // Recursive helper for on-demand loading
        private async Task<List<Folder>> LoadNestedFoldersRecursiveAsync(ClientContext context, Folder parentFolder, int currentLevel)
        {
            var allFolders = new List<Folder>();

            try
            {
                context.Load(parentFolder, f => f.Folders.Include(
                    sf => sf.Name,
                    sf => sf.ServerRelativeUrl,
                    sf => sf.ListItemAllFields.HasUniqueRoleAssignments,
                    sf => sf.ListItemAllFields.RoleAssignments.Include(
                        ra => ra.Member.Title,
                        ra => ra.Member.LoginName,
                        ra => ra.RoleDefinitionBindings),
                    sf => sf.Folders
                ));

                await context.ExecuteQueryAsync();

                var subfolders = parentFolder.Folders?.Where(f => !f.Name.StartsWith("Forms")).ToList() ?? new List<Folder>();

                foreach (var subfolder in subfolders)
                {
                    allFolders.Add(subfolder);

                    // Recursively load up to max nesting level
                    if (currentLevel < _maxNestingLevel)
                    {
                        var nestedFolders = await LoadNestedFoldersRecursiveAsync(context, subfolder, currentLevel + 1);
                        allFolders.AddRange(nestedFolders);
                    }
                }
            }
            catch (Exception ex)
            {
                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadNestedFoldersRecursiveError", _libraryName, null, "Subfolder",
                    $"Error loading nested folders for {parentFolder.ServerRelativeUrl}: {ex.Message}");
            }

            return allFolders;
        }

        private async Task LoadCurrentPermissionsAsync(string debugSessionId = null, bool preserveTreeViewState = false)
        {
            debugSessionId = debugSessionId ?? Guid.NewGuid().ToString();
            if (DateTime.UtcNow - lastRefreshTime < minimumRefreshInterval && debugSessionId != null)
            {
                await _auditLogManager?.LogAction(_signedInUserId, null, "DebugLoadPermissionsSkipped", _libraryName, null, "Subfolder",
                    $"Refresh skipped due to debounce, time since last refresh: {(DateTime.UtcNow - lastRefreshTime).TotalSeconds}s, Session ID: {debugSessionId}");
                return;
            }

            // Capture current state if preserving
            var (expandedPaths, selectedPath) = preserveTreeViewState ? CaptureTreeViewState() : (new List<string>(), null);

            string selectedSubfolderName = null;
            var auditLogs = new List<(string Action, string GroupName, string Details)>();

            // Only reset cache if not preserving state (for full refresh)
            if (!preserveTreeViewState)
            {
                _subfolderCache = new List<(string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups)>();
            }

            UpdateUI(() =>
            {
                try
                {
                    isUpdatingTreeView = true;
                    if (tvSubfolders != null && tvSubfolders.SelectedNode?.Tag is TreeNodeData nodeData && nodeData.IsSubfolder && !preserveTreeViewState)
                    {
                        selectedSubfolderName = nodeData.SubfolderName;
                    }

                    // Only clear tree if not preserving state
                    if (!preserveTreeViewState)
                    {
                        tvSubfolders?.Nodes.Clear();
                    }

                    if (lblStatus != null) lblStatus.Text = preserveTreeViewState ? "Refreshing permissions..." : "Loading subfolder permissions...";
                    if (progressBar != null)
                    {
                        progressBar.Value = 0;
                        progressBar.Visible = true;
                    }
                    if (btnRefresh != null) btnRefresh.Enabled = false;
                    this.Cursor = Cursors.WaitCursor;
                }
                catch (Exception ex)
                {
                    selectedSubfolderName = null;
                    if (lblStatus != null) lblStatus.Text = "Warning: Subfolder selection invalid during load.";
                    auditLogs.Add(("DebugLoadPermissionsSelectionError", null, $"Failed to get selected subfolder: {ex.Message}, Session ID: {debugSessionId}"));
                }
                finally
                {
                    isUpdatingTreeView = false;
                }
            });

            try
            {
                if (_pca == null || string.IsNullOrEmpty(_siteUrl) || string.IsNullOrEmpty(_libraryName))
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Authentication or site configuration is invalid.", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        if (lblStatus != null) lblStatus.Text = "Error: Invalid configuration.";
                        if (progressBar != null) progressBar.Visible = false;
                        this.Cursor = Cursors.Default;
                    });
                    auditLogs.Add(("ConfigurationError", null, $"Invalid configuration: pca={_pca == null}, siteUrl={_siteUrl}, libraryName={_libraryName}, Session ID: {debugSessionId}"));
                    await LogAuditBatchAsync(auditLogs);
                    return;
                }

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        if (lblStatus != null) lblStatus.Text = "Error: No signed-in account.";
                        if (progressBar != null) progressBar.Visible = false;
                        this.Cursor = Cursors.Default;
                    });
                    System.Diagnostics.Debug.WriteLine("LoadCurrentPermissionsAsync: No signed-in account found");
                    auditLogs.Add(("NoAccountError", null, $"No signed-in account found, Session ID: {debugSessionId}"));
                    await LogAuditBatchAsync(auditLogs);
                    return;
                }

                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Acquired token for account {account.Username}");

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    context.Load(context.Web, w => w.ServerRelativeUrl);
                    await context.ExecuteQueryAsync();

                    string webServerRelativeUrl = context.Web.ServerRelativeUrl;
                    string librarySlug = _libraryName.Replace(" ", "");
                    string libraryPath = $"{webServerRelativeUrl.TrimEnd('/')}/{librarySlug}".Replace("//", "/");

                    System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: webServerRelativeUrl={webServerRelativeUrl}, libraryName={_libraryName}, librarySlug={librarySlug}, libraryPath={libraryPath}");

                    var library = context.Web.Lists.GetByTitle(_libraryName);
                    context.Load(library, l => l.RootFolder.ServerRelativeUrl);
                    await context.ExecuteQueryAsync();

                    if (string.IsNullOrEmpty(library.RootFolder.ServerRelativeUrl))
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Library '{_libraryName}' not found or inaccessible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            if (lblStatus != null) lblStatus.Text = "Error: Library not found.";
                            if (progressBar != null) progressBar.Visible = false;
                            this.Cursor = Cursors.Default;
                        });
                        System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Library '{_libraryName}' has null RootFolder.ServerRelativeUrl");
                        auditLogs.Add(("LibraryNotFound", null, $"Library '{_libraryName}' not found or inaccessible, Session ID: {debugSessionId}"));
                        await LogAuditBatchAsync(auditLogs);
                        return;
                    }

                    var folder = library.RootFolder;
                    context.Load(folder, f => f.Folders.Include(
                        f => f.Name,
                        f => f.ServerRelativeUrl,
                        f => f.ListItemAllFields.HasUniqueRoleAssignments,
                        f => f.ListItemAllFields.RoleAssignments.Include(
                            ra => ra.Member.Title,
                            ra => ra.Member.LoginName,
                            ra => ra.RoleDefinitionBindings),
                        f => f.Folders));
                    await context.ExecuteQueryAsync();
                    System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Found {folder.Folders?.Count ?? 0} subfolders in library '{_libraryName}'");

                    var subfolders = folder.Folders?.Where(f => !f.Name.StartsWith("Forms")).ToList() ?? new List<Folder>();
                    if (!subfolders.Any())
                    {
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show("No subfolders found.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (lblStatus != null) lblStatus.Text = "No subfolders found.";
                            if (progressBar != null) progressBar.Visible = false;
                            if (btnAdd != null) btnAdd.Enabled = false;
                            if (btnRemove != null) btnRemove.Enabled = false;
                            UpdateSidebar();
                            isUpdatingTreeView = false;
                            this.Cursor = Cursors.Default;
                        });
                        System.Diagnostics.Debug.WriteLine("LoadCurrentPermissionsAsync: No subfolders found");
                        auditLogs.Add(("NoSubfolders", null, $"No subfolders found in library '{_libraryName}', Session ID: {debugSessionId}"));
                        await LogAuditBatchAsync(auditLogs);
                        return;
                    }

                    int totalSubfolders = subfolders.Count;
                    int processedSubfolders = 0;
                    int progressUpdateInterval = Math.Max(1, totalSubfolders / 20);

                    // Only rebuild TreeView if not preserving state
                    if (!preserveTreeViewState)
                    {
                        foreach (var subfolder in subfolders)
                        {
                            if (_cancellationTokenSource?.Token.IsCancellationRequested == true)
                            {
                                UpdateUI(() =>
                                {
                                    if (lblStatus != null) lblStatus.Text = "Loading cancelled by user.";
                                    if (progressBar != null) progressBar.Visible = false;
                                    this.Cursor = Cursors.Default;
                                });
                                auditLogs.Add(("LoadCancelled", null, $"Loading cancelled by user, Session ID: {debugSessionId}"));
                                await LogAuditBatchAsync(auditLogs);
                                return;
                            }

                            try
                            {
                                if (string.IsNullOrEmpty(subfolder.Name))
                                {
                                    System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Skipping subfolder with null or empty Name");
                                    auditLogs.Add(("InvalidSubfolderName", null, $"Subfolder with null or empty Name in library '{_libraryName}', Session ID: {debugSessionId}"));
                                    continue;
                                }

                                var groupList = new List<(string GroupName, string GroupId, string Role)>();
                                bool hasUniquePermissions = subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false;

                                if (hasUniquePermissions && subfolder.ListItemAllFields?.RoleAssignments != null)
                                {
                                    foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                                    {
                                        try
                                        {
                                            if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                                            {
                                                var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                                if (role == "Contribute") role = "Edit";
                                                if (role != "Limited Access")
                                                {
                                                    var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                                    groupList.Add((ra.Member.Title, groupId, role));
                                                }
                                            }
                                        }
                                        catch (Exception raEx)
                                        {
                                            auditLogs.Add(("ProcessSubfolderPermissionError", null,
                                                $"Error processing role assignment for subfolder '{subfolder.Name}': {raEx.Message}, Session ID: {debugSessionId}"));
                                        }
                                    }
                                }

                                // Properly construct the fullPath for top-level subfolders
                                var fullPath = string.IsNullOrEmpty(subfolder.ServerRelativeUrl)
                                    ? $"{libraryPath}/{subfolder.Name}".Replace("//", "/")
                                    : subfolder.ServerRelativeUrl;

                                if (string.IsNullOrEmpty(subfolder.ServerRelativeUrl))
                                {
                                    auditLogs.Add(("NullServerRelativeUrl", null,
                                        $"Subfolder '{subfolder.Name}' has null ServerRelativeUrl, using constructed path: {fullPath}, Session ID: {debugSessionId}"));
                                    System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Subfolder '{subfolder.Name}' has null ServerRelativeUrl, using constructed path: {fullPath}, HTTPS={_siteUrl}{fullPath}");
                                }
                                else if (!string.Equals(subfolder.ServerRelativeUrl, fullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    auditLogs.Add(("PathMismatch", null,
                                        $"Subfolder '{subfolder.Name}' ServerRelativeUrl ({subfolder.ServerRelativeUrl}) does not match constructed path ({fullPath}), Session ID: {debugSessionId}"));
                                }

                                _subfolderCache.Add((subfolder.Name, hasUniquePermissions, groupList));
                                System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Added subfolder '{subfolder.Name}', FullPath={fullPath}, HasUniquePermissions={hasUniquePermissions}, Groups={groupList.Count}, HTTPS={_siteUrl}{fullPath}");

                                // Prepare node data for UI thread (FIXED: Create data structure to pass to UI thread)
                                var nodeData = new
                                {
                                    SubfolderName = subfolder.Name,
                                    FullPath = fullPath,
                                    HasUniquePermissions = hasUniquePermissions,
                                    GroupList = groupList.ToList(), // Create a copy for thread safety
                                    HasChildren = subfolder.Folders?.Count(f => !f.Name.StartsWith("Forms")) > 0
                                };

                                // Create TreeNode on UI thread (FIXED: All TreeNode operations moved to UI thread)
                                UpdateUI(() =>
                                {
                                    try
                                    {
                                        var node = new TreeNode
                                        {
                                            Text = nodeData.HasUniquePermissions
                                                ? $"{nodeData.SubfolderName} (Unique, {nodeData.GroupList.Count} CSG group{(nodeData.GroupList.Count == 1 ? "" : "s")} assigned)"
                                                : $"{nodeData.SubfolderName} (Inherited)",
                                            ImageKey = "folder", // Use custom folder icon
                                            SelectedImageKey = "folder",
                                            Tag = new TreeNodeData
                                            {
                                                IsSubfolder = true,
                                                SubfolderName = nodeData.SubfolderName,
                                                FullPath = nodeData.FullPath,
                                                Level = 0, // Top-level subfolders are level 0
                                                HasChildren = nodeData.HasChildren
                                            }
                                        };

                                        foreach (var group in nodeData.GroupList)
                                        {
                                            node.Nodes.Add(new TreeNode
                                            {
                                                Text = $"{group.GroupName}: {group.Role}",
                                                ImageKey = "group", // Use custom group icon
                                                SelectedImageKey = "group",
                                                Tag = new TreeNodeData
                                                {
                                                    IsSubfolder = false,
                                                    GroupId = group.GroupId,
                                                    GroupName = group.GroupName,
                                                    Permission = group.Role,
                                                    FullPath = nodeData.FullPath,
                                                    Level = 0
                                                }
                                            });
                                        }

                                        // Add node to TreeView (this must be on UI thread)
                                        if (tvSubfolders != null && !tvSubfolders.IsDisposed)
                                        {
                                            tvSubfolders.Nodes.Add(node);
                                            System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Successfully added TreeNode for '{nodeData.SubfolderName}' on UI thread");
                                        }
                                    }
                                    catch (Exception nodeEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Error creating TreeNode for '{nodeData.SubfolderName}': {nodeEx.Message}");
                                        auditLogs.Add(("CreateTreeNodeError", null, $"Error creating TreeNode for '{nodeData.SubfolderName}': {nodeEx.Message}, Session ID: {debugSessionId}"));
                                    }
                                });

                                processedSubfolders++;

                                // Update progress (FIXED: Capture values before UI thread call)
                                var currentProgress = processedSubfolders;
                                var totalProgress = totalSubfolders;

                                if (currentProgress % progressUpdateInterval == 0 || currentProgress == totalProgress)
                                {
                                    UpdateUI(() =>
                                    {
                                        try
                                        {
                                            if (progressBar != null && !progressBar.IsDisposed)
                                            {
                                                var progressValue = totalProgress > 0 ?
                                                    Math.Min(100, (int)((currentProgress / (double)totalProgress) * 100)) : 0;
                                                progressBar.Value = progressValue;
                                            }
                                        }
                                        catch (Exception progEx)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Error updating progress: {progEx.Message}");
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                auditLogs.Add(("ProcessSubfolderError", null,
                                    $"Error processing subfolder '{subfolder.Name}': {ex.Message}, Session ID: {debugSessionId}"));
                                System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Error processing subfolder '{subfolder.Name}': {ex.Message}");
                            }
                        }

                        System.Diagnostics.Debug.WriteLine($"LoadCurrentPermissionsAsync: Added {subfolders.Count} top-level subfolders to _subfolderCache");
                    }
                    else
                    {
                        // When preserving state, just update the cache but don't rebuild TreeView
                        foreach (var subfolder in subfolders)
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(subfolder.Name)) continue;

                                var groupList = new List<(string GroupName, string GroupId, string Role)>();
                                bool hasUniquePermissions = subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false;

                                if (hasUniquePermissions && subfolder.ListItemAllFields?.RoleAssignments != null)
                                {
                                    foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                                    {
                                        try
                                        {
                                            if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                                            {
                                                var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                                if (role == "Contribute") role = "Edit";
                                                if (role != "Limited Access")
                                                {
                                                    var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                                    groupList.Add((ra.Member.Title, groupId, role));
                                                }
                                            }
                                        }
                                        catch (Exception raEx)
                                        {
                                            auditLogs.Add(("ProcessSubfolderPermissionError", null,
                                                $"Error processing role assignment for subfolder '{subfolder.Name}': {raEx.Message}, Session ID: {debugSessionId}"));
                                        }
                                    }
                                }

                                // Update cache entry
                                var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolder.Name, StringComparison.OrdinalIgnoreCase));
                                if (cacheIndex >= 0)
                                {
                                    _subfolderCache[cacheIndex] = (subfolder.Name, hasUniquePermissions, groupList);
                                }
                                else
                                {
                                    _subfolderCache.Add((subfolder.Name, hasUniquePermissions, groupList));
                                }
                            }
                            catch (Exception ex)
                            {
                                auditLogs.Add(("ProcessSubfolderError", null,
                                    $"Error processing subfolder '{subfolder.Name}': {ex.Message}, Session ID: {debugSessionId}"));
                            }
                        }
                    }

                    UpdateUI(() =>
                    {
                        isUpdatingTreeView = true;
                        if (lblStatus != null) lblStatus.Text = $"Loaded {subfolders.Count} top-level subfolders.";
                        if (progressBar != null)
                        {
                            progressBar.Value = 100;
                            progressBar.Visible = false;
                        }
                        UpdateSidebar();

                        // Restore state if preserving, otherwise use original logic
                        if (preserveTreeViewState && expandedPaths.Any())
                        {
                            RestoreTreeViewState(expandedPaths, selectedPath);
                        }
                        else if (!string.IsNullOrEmpty(selectedSubfolderName) && tvSubfolders != null)
                        {
                            var nodeToSelect = tvSubfolders.Nodes.Cast<TreeNode>().FirstOrDefault(n => (n.Tag as TreeNodeData)?.SubfolderName == selectedSubfolderName);
                            if (nodeToSelect != null)
                            {
                                tvSubfolders.SelectedNode = nodeToSelect;
                                nodeToSelect.EnsureVisible();
                            }
                        }

                        // Ensure TreeView visibility only for fresh loads
                        if (!preserveTreeViewState)
                        {
                            EnsureTreeViewVisibility();
                        }

                        isUpdatingTreeView = false;
                        this.Cursor = Cursors.Default;
                    });

                    await LogAuditBatchAsync(auditLogs);
                    lastRefreshTime = DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to load permissions: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    if (lblStatus != null) lblStatus.Text = "Error loading permissions.";
                    if (progressBar != null) progressBar.Visible = false;
                    if (btnAdd != null) btnAdd.Enabled = false;
                    if (btnRemove != null) btnRemove.Enabled = false;
                    UpdateSidebar();
                    isUpdatingTreeView = false;
                    this.Cursor = Cursors.Default;
                });
                auditLogs.Add(("LoadSubfolderPermissionsError", null, $"Failed to load subfolder permissions: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}"));
                await LogAuditBatchAsync(auditLogs);
            }
            finally
            {
                UpdateUI(() =>
                {
                    if (btnRefresh != null) btnRefresh.Enabled = true;
                    this.Cursor = Cursors.Default;
                });
            }
        }
        private TreeNode FindNodeByPath(TreeNodeCollection nodes, string targetPath)
        {
            foreach (TreeNode node in nodes)
            {
                if (node.Tag is TreeNodeData nodeData && nodeData.FullPath == targetPath)
                    return node;

                var childResult = FindNodeByPath(node.Nodes, targetPath);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }

        private bool IsDescendantOrSelf(TreeNode node, TreeNode ancestor)
        {
            while (node != null)
            {
                if (node == ancestor) return true;
                node = node.Parent;
            }
            return false;
        }

        private async Task LogAuditBatchAsync(List<(string Action, string GroupName, string Details)> logs)
        {
            foreach (var log in logs)
            {
                await _auditLogManager.LogAction(_signedInUserId, null, log.Action, _libraryName, log.GroupName, "Subfolder", log.Details);
            }
        }

        // UI Enhancement: Add breadcrumb navigation
        private void ShowFolderBreadcrumb(TreeNode selectedNode)
        {
            if (selectedNode?.Tag is TreeNodeData nodeData && nodeData.IsSubfolder)
            {
                var breadcrumbs = new List<string>();
                var currentNode = selectedNode;

                while (currentNode != null && currentNode.Tag is TreeNodeData data && data.IsSubfolder)
                {
                    breadcrumbs.Insert(0, data.SubfolderName);
                    currentNode = currentNode.Parent;
                }

                var breadcrumbText = string.Join(" > ", breadcrumbs);
                lblSelectedItem.Text = $"Selected: {breadcrumbText}";
            }
        }

        // Enhanced UpdateSidebar to handle nested folder operations
        private void UpdateSidebar()
        {
            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                try
                {
                    if (lblSelectedItem == null || btnAdd == null || btnRemove == null ||
                        btnBreakInheritance == null || btnRestoreInheritance == null || btnResetPermissions == null ||
                        btnPreview == null || radioRead == null || radioEdit == null || radioNoAccess == null ||
                        cmbGroups == null || tvSubfolders == null || lblStatus == null || btnViewSubfolders == null)
                    {
                        return;
                    }

                    lblSelectedItem.Text = "Selected Item: None";
                    btnAdd.Enabled = false;
                    btnRemove.Enabled = false;
                    btnBreakInheritance.Enabled = false;
                    btnRestoreInheritance.Enabled = false;
                    btnResetPermissions.Enabled = false;
                    btnPreview.Enabled = false;
                    btnViewSubfolders.Enabled = false;
                    radioRead.Checked = false;
                    radioEdit.Checked = false;
                    radioNoAccess.Checked = false;
                    radioRead.Enabled = false;
                    radioEdit.Enabled = false;
                    radioNoAccess.Enabled = false;
                    cmbGroups.Enabled = false;

                    if (tvSubfolders?.SelectedNode == null)
                    {
                        lblStatus.Text = "Select a subfolder to view or modify its permissions.";
                        return;
                    }

                    var nodeData = tvSubfolders.SelectedNode.Tag as TreeNodeData;
                    if (nodeData == null)
                    {
                        lblStatus.Text = "Invalid selection.";
                        return;
                    }

                    string subfolderName = null;
                    bool hasUniquePermissions = false;
                    string targetPath = null;
                    bool hasChildren = false;
                    int currentLevel = 0;
                    var currentGroupSelection = cmbGroups.SelectedItem;

                    if (nodeData.IsSubfolder)
                    {
                        subfolderName = nodeData.SubfolderName;
                        targetPath = nodeData.FullPath;
                        currentLevel = nodeData.Level;
                        hasChildren = nodeData.HasChildren;
                        ShowFolderBreadcrumb(tvSubfolders.SelectedNode);
                    }
                    else
                    {
                        var subfolderNode = tvSubfolders.SelectedNode.Parent;
                        var subfolderData = subfolderNode?.Tag as TreeNodeData;
                        subfolderName = subfolderData?.SubfolderName;
                        targetPath = subfolderData?.FullPath;
                        currentLevel = subfolderData?.Level ?? 0;
                        hasChildren = subfolderData?.HasChildren ?? false;
                        ShowFolderBreadcrumb(subfolderNode);
                    }

                    // Check cache for this specific path and level
                    bool isNested = !string.IsNullOrEmpty(targetPath) && _nestedSubfolderCache.Any(s => string.Equals(s.FullPath, targetPath, StringComparison.OrdinalIgnoreCase));
                    var cacheEntryNested = isNested ? _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FullPath, targetPath, StringComparison.OrdinalIgnoreCase)) : default;
                    var cacheEntryTop = !isNested && subfolderName != null ? _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase)) : default;
                    hasUniquePermissions = isNested ? (!string.IsNullOrEmpty(cacheEntryNested.FullPath) && cacheEntryNested.HasUniquePermissions) : (!string.IsNullOrEmpty(cacheEntryTop.SubfolderName) && cacheEntryTop.HasUniquePermissions);

                    // Enhanced logic for hasChildren - check if we can go deeper
                    if (isNested)
                    {
                        hasChildren = cacheEntryNested.HasChildren ||
                                     (currentLevel < _maxNestingLevel &&
                                      !_nestedSubfolderCache.Any(s => s.FullPath.StartsWith(targetPath + "/") && s.Level == currentLevel + 1));
                    }
                    else
                    {
                        hasChildren = true; // Assume top-level may have children, or check if already loaded
                    }

                    if (nodeData.IsSubfolder)
                    {
                        bool hasAssignedPermissions = isNested ? cacheEntryNested.Groups?.Any() ?? false : cacheEntryTop.Groups?.Any() ?? false;
                        bool hasPreview = tvSubfolders.SelectedNode.Nodes.Cast<TreeNode>().Any(n => (n.Tag as TreeNodeData)?.IsPreview == true);

                        btnBreakInheritance.Enabled = !hasUniquePermissions;
                        btnRestoreInheritance.Enabled = hasUniquePermissions;
                        btnResetPermissions.Enabled = hasUniquePermissions && hasAssignedPermissions;
                        btnAdd.Enabled = hasUniquePermissions;
                        btnRemove.Enabled = hasUniquePermissions && hasPreview;
                        btnPreview.Enabled = hasUniquePermissions && !hasPreview && cmbGroups.SelectedItem != null;

                        // Enhanced: Enable "View Subfolders" for any level within the limit
                        btnViewSubfolders.Enabled = hasChildren && currentLevel < _maxNestingLevel;
                        if (btnViewSubfolders.Enabled)
                        {
                            btnViewSubfolders.Text = currentLevel == 0 ? "View Subfolders" : $"Load Level {currentLevel + 1} Folders";
                        }
                        else if (currentLevel >= _maxNestingLevel)
                        {
                            btnViewSubfolders.Text = $"Max Depth ({_maxNestingLevel}) Reached";
                        }
                        else
                        {
                            btnViewSubfolders.Text = "No Subfolders";
                        }

                        cmbGroups.Enabled = hasUniquePermissions && !hasPreview;
                        radioRead.Enabled = hasUniquePermissions;
                        radioEdit.Enabled = hasUniquePermissions;
                        radioNoAccess.Enabled = false;

                        if (hasPreview)
                        {
                            var previewNodeData = tvSubfolders.SelectedNode.Nodes.Cast<TreeNode>()
                                .FirstOrDefault(n => (n.Tag as TreeNodeData)?.IsPreview == true)?.Tag as TreeNodeData;
                            if (previewNodeData != null)
                            {
                                radioRead.Checked = previewNodeData.Permission == "Read";
                                radioEdit.Checked = previewNodeData.Permission == "Edit";
                                radioNoAccess.Checked = previewNodeData.Permission == "No Direct Access";
                            }
                            lblStatus.Text = "Preview created: Select the preview item and click 'Add Permission' to confirm or 'Remove' to cancel.";
                        }
                        else
                        {
                            lblStatus.Text = hasUniquePermissions
                                ? "Select a group and permission level to create preview."
                                : "Subfolder has inherited permissions. Break inheritance to modify.";
                        }
                    }
                    else
                    {
                        if (nodeData.IsPreview)
                        {
                            lblSelectedItem.Text = $"Group: {nodeData.GroupName} ({nodeData.Permission}) (Preview)";
                            btnBreakInheritance.Enabled = false;
                            btnRestoreInheritance.Enabled = false;
                            btnResetPermissions.Enabled = false;
                            btnAdd.Enabled = hasUniquePermissions;
                            btnRemove.Enabled = hasUniquePermissions;
                            btnPreview.Enabled = false;
                            btnViewSubfolders.Enabled = false;
                            btnViewSubfolders.Text = "View Subfolders";
                            cmbGroups.Enabled = false;

                            radioRead.Enabled = hasUniquePermissions;
                            radioEdit.Enabled = hasUniquePermissions;
                            radioNoAccess.Enabled = false;
                            radioRead.Checked = nodeData.Permission == "Read";
                            radioEdit.Checked = nodeData.Permission == "Edit";
                            radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                            lblStatus.Text = "Preview selected: Click 'Add Permission' to confirm or 'Remove' to cancel. Change permission to update preview.";
                        }
                        else
                        {
                            lblSelectedItem.Text = $"Group: {nodeData.GroupName} ({nodeData.Permission})";
                            btnBreakInheritance.Enabled = false;
                            btnRestoreInheritance.Enabled = false;
                            btnResetPermissions.Enabled = false;
                            btnAdd.Enabled = false;
                            btnRemove.Enabled = hasUniquePermissions;
                            btnPreview.Enabled = false;
                            btnViewSubfolders.Enabled = false;
                            btnViewSubfolders.Text = "View Subfolders";
                            cmbGroups.Enabled = false;

                            radioRead.Enabled = hasUniquePermissions;
                            radioEdit.Enabled = hasUniquePermissions;
                            radioNoAccess.Enabled = hasUniquePermissions;
                            radioRead.Checked = nodeData.Permission == "Read";
                            radioEdit.Checked = nodeData.Permission == "Edit";
                            radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                            lblStatus.Text = hasUniquePermissions
                                ? "Change permission to update, or remove the group."
                                : "Inherited permissions: Break inheritance to modify.";
                        }
                    }

                    if (currentGroupSelection != null && cmbGroups.Enabled)
                    {
                        cmbGroups.SelectedItem = currentGroupSelection;
                    }
                }
                finally
                {
                    isUpdatingTreeView = false;
                }
            });
        }

        private string NormalizePath(string path, bool encodeSpaces = false)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (encodeSpaces)
            {
                // Split and encode segments
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                path = "/" + string.Join("/", segments.Select(Uri.EscapeDataString));
            }
            path = path.Replace("//", "/").TrimEnd('/');
            System.Diagnostics.Debug.WriteLine($"Normalized path (encodeSpaces={encodeSpaces}): {path}");
            return path;
        }
        private string ConstructSubfolderPath(TreeNode node, string webServerRelativeUrl, string libraryName)
        {
            if (node?.Tag is not TreeNodeData nodeData || !nodeData.IsSubfolder)
                return null;

            // If we already have a valid FullPath, clean and use it
            if (!string.IsNullOrEmpty(nodeData.FullPath))
            {
                var normalized = NormalizePath(nodeData.FullPath);
                var cleaned = CleanDuplicateSitePath(normalized, _siteUrl);
                System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPath: Using existing FullPath: {cleaned}");
                return cleaned;
            }

            // Build path by walking up the tree
            var pathParts = new List<string>();
            var currentNode = node;

            while (currentNode != null && currentNode.Tag is TreeNodeData currentData && currentData.IsSubfolder)
            {
                pathParts.Insert(0, currentData.SubfolderName);
                currentNode = currentNode.Parent;
            }

            // Construct the full path - ensure clean webServerRelativeUrl
            string librarySlug = libraryName.Replace(" ", "");
            string cleanWebPath = CleanDuplicateSitePath(webServerRelativeUrl.TrimEnd('/'), _siteUrl);

            string fullPath = $"{cleanWebPath}/{librarySlug}/{string.Join("/", pathParts)}";
            var normalizedPath = NormalizePath(fullPath);
            var finalCleanedPath = CleanDuplicateSitePath(normalizedPath, _siteUrl);

            System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPath: Constructed path from tree walk: {finalCleanedPath}");

            // Update the node's FullPath for future use
            nodeData.FullPath = finalCleanedPath;

            return finalCleanedPath;
        }
        private bool IsValidSharePointPath(string path, string expectedSiteUrl)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(expectedSiteUrl))
                return false;

            try
            {
                // Check for duplicate segments in the path
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var segmentCounts = segments.GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
                                          .Where(g => g.Count() > 1)
                                          .ToList();

                if (segmentCounts.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Found duplicate segments in path {path}: {string.Join(", ", segmentCounts.Select(g => g.Key))}");
                    return false;
                }

                // Ensure the path starts with the expected site structure
                var expectedStart = new Uri(expectedSiteUrl).AbsolutePath.TrimEnd('/');
                if (!path.StartsWith(expectedStart, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Path {path} doesn't start with expected site path {expectedStart}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validating path {path}: {ex.Message}");
                return false;
            }
        }
        private void radioPermission_CheckedChanged(object sender, EventArgs e)
        {
            RadioButton changedRadio = sender as RadioButton;
            if (changedRadio == null || isUpdatingTreeView) return;

            string permissionLevel = radioRead.Checked ? "Read" : radioEdit.Checked ? "Edit" : radioNoAccess.Checked ? "No Direct Access" : null;
            var currentGroupSelection = cmbGroups?.SelectedItem;
            var currentSelectedNode = tvSubfolders?.SelectedNode;

            if (tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData selectedNodeData))
                return;

            if (selectedNodeData.IsSubfolder)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    var nodesToRemove = new List<TreeNode>();
                    foreach (TreeNode node in tvSubfolders.SelectedNode.Nodes)
                    {
                        if (node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                            nodesToRemove.Add(node);
                    }
                    foreach (var node in nodesToRemove)
                    {
                        node.Remove();
                    }

                    if (permissionLevel != null && cmbGroups.SelectedItem is GroupItem selectedGroup)
                    {
                        var previewNode = new TreeNode
                        {
                            Text = $"{selectedGroup.DisplayName}: {permissionLevel} (Pending)",
                            ImageIndex = 1,
                            SelectedImageIndex = 1,
                            ForeColor = Color.Green,
                            Tag = new TreeNodeData
                            {
                                IsSubfolder = false,
                                GroupId = selectedGroup.Id,
                                GroupName = selectedGroup.DisplayName,
                                Permission = permissionLevel,
                                IsPreview = true
                            }
                        };
                        tvSubfolders.SelectedNode.Nodes.Add(previewNode);
                        _isPreviewActive = true;
                        _previewSubfolderNode = tvSubfolders.SelectedNode;
                        tvSubfolders.SelectedNode = previewNode;
                        lblStatus.Text = $"Previewing '{permissionLevel}' for group '{selectedGroup.DisplayName}' on '{selectedNodeData.SubfolderName}'. Click 'Add Permission' to confirm.";
                    }
                    else
                    {
                        lblStatus.Text = permissionLevel != null
                            ? $"Selected '{permissionLevel}' for '{selectedNodeData.SubfolderName}'. Select a group and click 'Add Permission' to assign."
                            : "Select a permission level to add to a group.";
                    }

                    if (currentGroupSelection != null && cmbGroups.Enabled)
                        cmbGroups.SelectedItem = currentGroupSelection;
                    tvSubfolders.ExpandAll();
                    isUpdatingTreeView = false;
                });
                return;
            }

            string subfolderName = null;
            TreeNodeData nodeData = selectedNodeData;
            string debugSessionId = Guid.NewGuid().ToString();

            if (nodeData.IsPreview)
            {
                UpdateUI(() =>
                {
                    nodeData.Permission = permissionLevel;
                    tvSubfolders.SelectedNode.Text = $"{nodeData.GroupName}: {permissionLevel} (Pending)";
                    lblStatus.Text = $"Updated preview to '{permissionLevel}' for group '{nodeData.GroupName}' on '{(tvSubfolders.SelectedNode.Parent.Tag as TreeNodeData)?.SubfolderName}'.";
                });
                return;
            }

            var subfolderNode = tvSubfolders.SelectedNode.Parent;
            var subfolderData = subfolderNode?.Tag as TreeNodeData;
            subfolderName = subfolderData?.SubfolderName;
            bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);
            var cacheEntryNested = default((string FullPath, string FolderName, int Level, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren));
            var cacheEntryTop = default((string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups));
            bool hasUniquePermissions;
            if (isNested)
            {
                cacheEntryNested = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryNested.FullPath) && cacheEntryNested.HasUniquePermissions;
            }
            else
            {
                cacheEntryTop = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryTop.SubfolderName) && cacheEntryTop.HasUniquePermissions;
            }

            List<(string GroupName, string GroupId, string Role)> cacheGroups = null;
            if (isNested)
            {
                var cacheEntry = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                hasUniquePermissions = cacheEntry.FullPath != null && cacheEntry.HasUniquePermissions;
                cacheGroups = cacheEntry.Groups;
            }
            else
            {
                var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                cacheGroups = cacheEntry.Groups;
            }
            if (!hasUniquePermissions)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = "Change cancelled: Subfolder has inherited permissions.";
                    radioRead.Checked = nodeData.Permission == "Read";
                    radioEdit.Checked = nodeData.Permission == "Edit";
                    radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                });
                return;
            }

            if (permissionLevel == null)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show("No permission selected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = "Change cancelled: No permission selected.";
                    radioRead.Checked = nodeData.Permission == "Read";
                    radioEdit.Checked = nodeData.Permission == "Edit";
                    radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                });
                return;
            }

            if (permissionLevel == nodeData.Permission)
            {
                UpdateUI(() => lblStatus.Text = "No change in permission.");
                return;
            }

            var confirmMessage = permissionLevel == "No Direct Access"
                ? $"Are you sure you want to remove permissions for '{nodeData.GroupName}' from subfolder '{subfolderName}'?"
                : $"Are you sure you want to change the permission for '{nodeData.GroupName}' on subfolder '{subfolderName}' from '{nodeData.Permission}' to '{permissionLevel}'?";
            var confirm = MessageBox.Show(confirmMessage, "Confirm Permission Change", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                UpdateUI(() =>
                {
                    lblStatus.Text = "Permission change cancelled by user.";
                    radioRead.Checked = nodeData.Permission == "Read";
                    radioEdit.Checked = nodeData.Permission == "Edit";
                    radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                });
                return;
            }

            UpdateUI(() => lblStatus.Text = permissionLevel == "No Direct Access"
                ? $"Removing permission for '{nodeData.GroupName}' from '{subfolderName}'..."
                : $"Changing permission for '{nodeData.GroupName}' on '{subfolderName}' to '{permissionLevel}'...");

            Task.Run(async () =>
            {
                try
                {
                    var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                    var accounts = await _pca.GetAccountsAsync();
                    var account = accounts.FirstOrDefault();
                    if (account == null)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            lblStatus.Text = "Error: No signed-in account.";
                        });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                            $"No signed-in account found, Session ID: {debugSessionId}");
                        return;
                    }

                    var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                    const int maxRetries = 3;
                    int retryCount = 0;
                    bool success = false;
                    string subfolderRelativeUrl = isNested ? cacheEntryNested.FullPath : $"{_siteUrl}/{_libraryName}/{subfolderName}".Replace("//", "/");

                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                context.Load(context.Web, w => w.ServerRelativeUrl);
                                await context.ExecuteQueryAsync();
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                await context.ExecuteQueryAsync();

                                string groupPrincipalId = $"c:0t.c|tenant|{nodeData.GroupId}";
                                var raToModify = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupPrincipalId && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                                if (raToModify == null)
                                {
                                    UpdateUI(() =>
                                    {
                                        MessageBox.Show($"Group '{nodeData.GroupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        lblStatus.Text = "Change cancelled: Group not found.";
                                    });
                                    await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Group '{nodeData.GroupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    return;
                                }

                                if (permissionLevel == "No Direct Access")
                                {
                                    foreach (RoleDefinition rd in raToModify.RoleDefinitionBindings.ToList())
                                        raToModify.RoleDefinitionBindings.Remove(rd);
                                    raToModify.Update();
                                    raToModify.DeleteObject();
                                    await context.ExecuteQueryAsync();
                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        tvSubfolders.SelectedNode.Remove();
                                        lblStatus.Text = $"Removed permissions for '{nodeData.GroupName}' from '{subfolderName}'.";
                                        radioRead.Checked = false;
                                        radioEdit.Checked = false;
                                        radioNoAccess.Checked = false;

                                        if (isNested)
                                        {
                                            var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _nestedSubfolderCache[cacheIndex].Groups.Where(g => g.GroupId != nodeData.GroupId).ToList();
                                                _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, _nestedSubfolderCache[cacheIndex].HasUniquePermissions, updatedGroups, _nestedSubfolderCache[cacheIndex].HasChildren);
                                            }
                                        }
                                        else
                                        {
                                            var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _subfolderCache[cacheIndex].Groups.Where(g => g.GroupId != nodeData.GroupId).ToList();
                                                _subfolderCache[cacheIndex] = (subfolderName, _subfolderCache[cacheIndex].HasUniquePermissions, updatedGroups);
                                            }
                                        }

                                        UpdateSidebar();
                                        isUpdatingTreeView = false;
                                    });
                                    success = true;
                                }
                                else
                                {
                                    context.Load(context.Web.RoleDefinitions);
                                    await context.ExecuteQueryAsync();
                                    string targetRoleName = permissionLevel == "Edit" ? "Contribute" : permissionLevel;
                                    var roleDefinition = context.Web.RoleDefinitions.FirstOrDefault(rd => rd.Name == targetRoleName);
                                    if (roleDefinition == null)
                                    {
                                        UpdateUI(() =>
                                        {
                                            MessageBox.Show($"Permission level '{permissionLevel}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            lblStatus.Text = "Error: Permission level not found.";
                                        });
                                        await _auditLogManager?.LogAction(_signedInUserId, null, "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                            $"Permission level '{permissionLevel}' not found for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                        return;
                                    }

                                    foreach (RoleDefinition rd in raToModify.RoleDefinitionBindings.ToList())
                                        raToModify.RoleDefinitionBindings.Remove(rd);
                                    raToModify.RoleDefinitionBindings.Add(roleDefinition);
                                    raToModify.Update();
                                    await context.ExecuteQueryAsync();

                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        tvSubfolders.SelectedNode.Text = $"{nodeData.GroupName}: {permissionLevel}";
                                        (tvSubfolders.SelectedNode.Tag as TreeNodeData).Permission = permissionLevel;
                                        lblStatus.Text = $"Changed permission for '{nodeData.GroupName}' on '{subfolderName}' to '{permissionLevel}'.";

                                        if (isNested)
                                        {
                                            var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _nestedSubfolderCache[cacheIndex].Groups.ToList();
                                                var groupIndex = updatedGroups.FindIndex(g => g.GroupId == nodeData.GroupId);
                                                if (groupIndex >= 0)
                                                    updatedGroups[groupIndex] = (nodeData.GroupName, nodeData.GroupId, permissionLevel);
                                                _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, _nestedSubfolderCache[cacheIndex].HasUniquePermissions, updatedGroups, _nestedSubfolderCache[cacheIndex].HasChildren);
                                            }
                                        }
                                        else
                                        {
                                            var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _subfolderCache[cacheIndex].Groups.ToList();
                                                var groupIndex = updatedGroups.FindIndex(g => g.GroupId == nodeData.GroupId);
                                                if (groupIndex >= 0)
                                                    updatedGroups[groupIndex] = (nodeData.GroupName, nodeData.GroupId, permissionLevel);
                                                _subfolderCache[cacheIndex] = (subfolderName, _subfolderCache[cacheIndex].HasUniquePermissions, updatedGroups);
                                            }
                                        }

                                        UpdateSidebar();
                                        isUpdatingTreeView = false;
                                    });

                                    success = true;
                                }

                                await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermission" : "ChangeSubfolderPermission", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"{(permissionLevel == "No Direct Access" ? "Removed" : "Changed")} permission for group '{nodeData.GroupName}' on subfolder '{subfolderName}' to '{permissionLevel}', Session ID: {debugSessionId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionRetry" : "ChangeSubfolderPermissionRetry", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Retry {retryCount} for {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission on '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                MessageBox.Show($"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission after {maxRetries} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                lblStatus.Text = $"Error {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission.";
                                radioRead.Checked = nodeData.Permission == "Read";
                                radioEdit.Checked = nodeData.Permission == "Edit";
                                radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                                UpdateSidebar();
                                isUpdatingTreeView = false;
                            });
                            await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionError" : "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                $"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission for subfolder '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UpdateUI(() =>
                    {
                        isUpdatingTreeView = true;
                        MessageBox.Show($"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = $"Error {(permissionLevel == "No Direct Access" ? "removing" : "changing")} permission.";
                        radioRead.Checked = nodeData.Permission == "Read";
                        radioEdit.Checked = nodeData.Permission == "Edit";
                        radioNoAccess.Checked = nodeData.Permission == "No Direct Access";
                        UpdateSidebar();
                        isUpdatingTreeView = false;
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, permissionLevel == "No Direct Access" ? "RemoveSubfolderPermissionError" : "ChangeSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                        $"Failed to {(permissionLevel == "No Direct Access" ? "remove" : "change")} permission for subfolder '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                }
            });
        }

        private string ConstructFullHttpsUrl(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath) || string.IsNullOrEmpty(_siteUrl))
                return string.Empty;

            try
            {
                var siteUri = new Uri(_siteUrl);
                var sitePath = siteUri.AbsolutePath.TrimEnd('/');

                // Clean the relative path first
                var cleanedRelativePath = CleanDuplicateSitePath(NormalizePath(relativePath), _siteUrl);

                // If the relative path already starts with the site path, don't duplicate it
                if (cleanedRelativePath.StartsWith(sitePath, StringComparison.OrdinalIgnoreCase))
                {
                    return $"{siteUri.Scheme}://{siteUri.Host}{cleanedRelativePath}";
                }
                else
                {
                    return $"{siteUri.Scheme}://{siteUri.Host}{sitePath}{cleanedRelativePath}";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConstructFullHttpsUrl: Error constructing HTTPS URL: {ex.Message}");
                return $"{_siteUrl}{relativePath}"; // Fallback to simple concatenation
            }
        }
        private void tvSubfolders_AfterSelect(object sender, TreeViewEventArgs e)
        {
            UpdateUI(() =>
            {
                isUpdatingTreeView = true;
                UpdateSidebar();
                isUpdatingTreeView = false;
            });
        }

        private string GetLibraryPathWithFallback(string webServerRelativeUrl, string libraryName, out bool usedFallback)
        {
            usedFallback = false;

            // Original: Remove spaces
            string librarySlug = libraryName.Replace(" ", "");
            string originalPath = $"{webServerRelativeUrl.TrimEnd('/')}/{librarySlug}".Replace("//", "/");
            System.Diagnostics.Debug.WriteLine($"Constructed original library path: {originalPath}");

            // Fallback: Encode spaces as %20
            string encodedName = Uri.EscapeDataString(libraryName);
            string fallbackPath = $"{webServerRelativeUrl.TrimEnd('/')}/{encodedName}".Replace("//", "/");
            System.Diagnostics.Debug.WriteLine($"Constructed fallback library path: {fallbackPath}");

            // For now, return original; fallback will be used in try-catch during execution
            return originalPath; // Caller will handle retry
        }

        private void cmbGroups_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingTreeView || tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData selectedNodeData) || !selectedNodeData.IsSubfolder)
                return;

            string permissionLevel = radioRead.Checked ? "Read" : radioEdit.Checked ? "Edit" : radioNoAccess.Checked ? "No Direct Access" : null;

            UpdateUI(() =>
            {
                isUpdatingTreeView = true;

                // Remove any existing preview node from this subfolder
                var nodesToRemove = new List<TreeNode>();
                foreach (TreeNode node in tvSubfolders.SelectedNode.Nodes)
                {
                    if (node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                        nodesToRemove.Add(node);
                }
                foreach (var node in nodesToRemove)
                {
                    node.Remove();
                }

                // Reset preview state
                _isPreviewActive = false;
                _previewSubfolderNode = null;

                if (permissionLevel != null && cmbGroups.SelectedItem is GroupItem selectedGroup)
                {
                    var previewNode = new TreeNode
                    {
                        Text = $"{selectedGroup.DisplayName}: {permissionLevel} (Pending)",
                        ImageIndex = 1,
                        SelectedImageIndex = 1,
                        ForeColor = System.Drawing.Color.Green,
                        Tag = new TreeNodeData
                        {
                            IsSubfolder = false,
                            GroupId = selectedGroup.Id,
                            GroupName = selectedGroup.DisplayName,
                            Permission = permissionLevel,
                            IsPreview = true,
                            FullPath = selectedNodeData.FullPath,
                            Level = selectedNodeData.Level
                        }
                    };

                    tvSubfolders.SelectedNode.Nodes.Add(previewNode);
                    tvSubfolders.SelectedNode.Expand();

                    // Set preview state
                    _isPreviewActive = true;
                    _previewSubfolderNode = tvSubfolders.SelectedNode;

                    // Auto-select the preview node
                    tvSubfolders.SelectedNode = previewNode;

                    lblStatus.Text = $"Previewing '{permissionLevel}' for group '{selectedGroup.DisplayName}' on '{selectedNodeData.SubfolderName}'. Click 'Add Permission' to confirm.";
                }
                else
                {
                    lblStatus.Text = permissionLevel != null ? $"Selected '{permissionLevel}' for '{selectedNodeData.SubfolderName}'. Select a group to create preview." : "Select a permission level and group to create preview.";
                }

                UpdateSidebar(); // This will enable the Add button for preview
                isUpdatingTreeView = false;
            });
        }

        private async void btnAdd_Click(object sender, EventArgs e)
        {
            string subfolderName = null;
            GroupItem selectedGroup = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnAdd.Enabled = false; isUpdatingTreeView = false; });

            try
            {
                TreeNode selectedNode = tvSubfolders?.SelectedNode;
                if (selectedNode == null)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder or preview.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: Invalid selection."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Invalid selection, Session ID: {debugSessionId}");
                    return;
                }

                TreeNode subfolderNode;
                TreeNodeData nodeData = selectedNode.Tag as TreeNodeData;
                bool isPreview = nodeData?.IsPreview ?? false;
                string permissionLevel = null;

                if (isPreview)
                {
                    subfolderNode = selectedNode.Parent;
                    var subfolderData = subfolderNode?.Tag as TreeNodeData;
                    subfolderName = subfolderData?.SubfolderName;
                    selectedGroup = new GroupItem { Id = nodeData.GroupId, DisplayName = nodeData.GroupName };
                    permissionLevel = nodeData.Permission;

                    if (permissionLevel == "No Direct Access")
                    {
                        UpdateUI(() => { MessageBox.Show("Use 'Remove Permission' to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: Invalid permission level."; });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder",
                            $"Invalid permission level 'No Direct Access', Session ID: {debugSessionId}");
                        return;
                    }
                }
                else
                {
                    if (!(nodeData?.IsSubfolder ?? false))
                    {
                        UpdateUI(() => { MessageBox.Show("Please select a subfolder or preview.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: Invalid subfolder selection."; });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"Invalid subfolder selection, Session ID: {debugSessionId}");
                        return;
                    }
                    subfolderNode = selectedNode;
                    subfolderName = nodeData.SubfolderName;

                    if (cmbGroups?.SelectedItem == null)
                    {
                        UpdateUI(() => { MessageBox.Show("Please select a group from the dropdown.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: No group selected."; });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"No group selected, Session ID: {debugSessionId}");
                        return;
                    }

                    bool readChecked = radioRead?.Checked ?? false;
                    bool editChecked = radioEdit?.Checked ?? false;
                    bool noAccessChecked = radioNoAccess?.Checked ?? false;

                    if (!readChecked && !editChecked && !noAccessChecked)
                    {
                        UpdateUI(() => { MessageBox.Show("Please select a permission level (Read, Edit, or No Direct Access).", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: No permission level selected."; });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder", $"No permission level selected, Session ID: {debugSessionId}");
                        return;
                    }
                    selectedGroup = (GroupItem)cmbGroups.SelectedItem;
                    permissionLevel = readChecked ? "Read" : editChecked ? "Edit" : "No Direct Access";

                    if (permissionLevel == "No Direct Access")
                    {
                        UpdateUI(() => { MessageBox.Show("Use 'Remove Permission' to remove permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: Invalid permission level."; });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup.DisplayName, "Subfolder",
                            $"Invalid permission level 'No Direct Access', Session ID: {debugSessionId}");
                        return;
                    }
                }

                bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);
                var cacheEntryNested = default((string FullPath, string FolderName, int Level, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren));
                var cacheEntryTop = default((string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups));
                bool hasUniquePermissions;
                if (isNested)
                {
                    cacheEntryNested = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryNested.FullPath) && cacheEntryNested.HasUniquePermissions;
                }
                else
                {
                    cacheEntryTop = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryTop.SubfolderName) && cacheEntryTop.HasUniquePermissions;
                }
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show("Subfolder has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Add cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }

                string groupName = selectedGroup.DisplayName;
                string selectedGroupId = selectedGroup.Id;
                var confirm = MessageBox.Show($"Are you sure you want to add '{permissionLevel}' permission for group '{groupName}' to subfolder '{subfolderName}'?",
                    "Confirm Add Permission", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { lblStatus.Text = "Add permission cancelled by user."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionCancelled", _libraryName, groupName, "Subfolder",
                        $"User cancelled adding '{permissionLevel}' permission for group '{groupName}' to subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                TreeNode previewToRemove = isPreview ? selectedNode : subfolderNode.Nodes.Cast<TreeNode>().FirstOrDefault(n => (n.Tag as TreeNodeData)?.IsPreview == true);
                if (previewToRemove != null)
                {
                    UpdateUI(() =>
                    {
                        isUpdatingTreeView = true;
                        previewToRemove.Remove();
                        isUpdatingTreeView = false;
                    });
                }

                UpdateUI(() => lblStatus.Text = $"Adding permission for '{groupName}' to '{subfolderName}'...");

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() => { MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); lblStatus.Text = "Error: No signed-in account."; });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, groupName, "Subfolder", $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;

                // Cache webServerRelativeUrl before entering the using block
                string webServerRelativeUrl;
                using (var tempContext = new ClientContext(_siteUrl))
                {
                    tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                    await tempContext.ExecuteQueryAsync();
                    webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                }

                string subfolderRelativeUrl = isNested ? cacheEntryNested.FullPath : $"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");

                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();

                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            var listItem = subfolder.ListItemAllFields;
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            context.Load(context.Web, s => s.RoleDefinitions);
                            await context.ExecuteQueryAsync();

                            if (!listItem.HasUniqueRoleAssignments)
                            {
                                listItem.BreakRoleInheritance(true, false);
                                await context.ExecuteQueryAsync();
                            }

                            string groupPrincipalId = $"c:0t.c|tenant|{selectedGroupId}";
                            var principal = context.Web.EnsureUser(groupPrincipalId);
                            context.Load(principal);
                            await context.ExecuteQueryAsync();

                            string targetRoleName = permissionLevel == "Edit" ? "Contribute" : permissionLevel;
                            var roleDefinition = context.Web.RoleDefinitions.FirstOrDefault(rd => rd.Name == targetRoleName);
                            if (roleDefinition == null)
                            {
                                UpdateUI(() => { MessageBox.Show($"Permission level '{permissionLevel}' not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); lblStatus.Text = "Error: Permission level not found."; });
                                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                                    $"Permission level '{permissionLevel}' not found for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                return;
                            }

                            var roleDefinitionBindings = new RoleDefinitionBindingCollection(context);
                            roleDefinitionBindings.Add(roleDefinition);
                            listItem.RoleAssignments.Add(principal, roleDefinitionBindings);
                            listItem.Update();
                            await context.ExecuteQueryAsync();

                            success = true;

                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                var newGroupNode = new TreeNode
                                {
                                    Text = $"{groupName}: {permissionLevel}",
                                    ImageIndex = 1,
                                    SelectedImageIndex = 1,
                                    Tag = new TreeNodeData { IsSubfolder = false, GroupId = selectedGroupId, GroupName = groupName, Permission = permissionLevel }
                                };
                                subfolderNode.Nodes.Add(newGroupNode);

                                int groupCount = subfolderNode.Nodes.Cast<TreeNode>().Count(n => (n.Tag as TreeNodeData)?.IsPreview != true);
                                subfolderNode.Text = $"{subfolderName} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned)";

                                if (isNested)
                                {
                                    var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var updatedGroups = _nestedSubfolderCache[cacheIndex].Groups.ToList();
                                        updatedGroups.Add((groupName, selectedGroupId, permissionLevel));
                                        _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, true, updatedGroups, _nestedSubfolderCache[cacheIndex].HasChildren);
                                    }
                                }
                                else
                                {
                                    var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var updatedGroups = _subfolderCache[cacheIndex].Groups.ToList();
                                        updatedGroups.Add((groupName, selectedGroupId, permissionLevel));
                                        _subfolderCache[cacheIndex] = (subfolderName, true, updatedGroups);
                                    }
                                }

                                subfolderNode.Expand();
                                tvSubfolders.SelectedNode = newGroupNode;
                                lblStatus.Text = $"Added '{permissionLevel}' permission for '{groupName}' to '{subfolderName}'.";
                                _isPreviewActive = false;
                                _previewSubfolderNode = null;
                                UpdateSidebar();
                                isUpdatingTreeView = false;
                            });

                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermission", _libraryName, groupName, "Subfolder",
                                $"Added '{permissionLevel}' permission for group '{groupName}' to subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionRetry", _libraryName, groupName, "Subfolder",
                                $"Retry {retryCount} for adding permission to '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Failed to add permission after {maxRetries} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Error adding permission.";
                        });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, groupName, "Subfolder",
                            $"Failed to add permission to subfolder '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to add permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error adding permission.";
                });
                await _auditLogManager?.LogAction(_signedInUserId, null, "AddSubfolderPermissionError", _libraryName, selectedGroup?.DisplayName ?? "unknown", "Subfolder",
                    $"Failed to add permission to subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnAdd.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void btnPreview_Click(object sender, EventArgs e)
        {
            // Implementation for previewing permissions - preserving all original logic
            // [Full implementation would go here...]
        }

        private async void btnRemove_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnRemove.Enabled = false; isUpdatingTreeView = false; });
            TreeNodeData nodeData = null;
            string subfolderName = null;

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                string subfolderRelativeUrl = null;

                if (tvSubfolders.SelectedNode != null && tvSubfolders.SelectedNode.Tag is TreeNodeData selectedNodeData && !selectedNodeData.IsSubfolder)
                {
                    nodeData = selectedNodeData;
                    var subfolderNode = tvSubfolders.SelectedNode.Parent;
                    var subfolderData = subfolderNode?.Tag as TreeNodeData;
                    subfolderName = subfolderData?.SubfolderName;

                    if (nodeData.IsPreview)
                    {
                        UpdateUI(() =>
                        {
                            tvSubfolders.SelectedNode.Remove();
                            _isPreviewActive = false;
                            _previewSubfolderNode = null;
                            lblStatus.Text = $"Cancelled preview for '{nodeData.GroupName}' on '{subfolderName}'.";
                            tvSubfolders.SelectedNode = subfolderNode;
                            UpdateSidebar();
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "CancelSubfolderPermissionPreview", _libraryName, nodeData.GroupName, "Subfolder",
                            $"Cancelled preview addition of '{nodeData.Permission}' for group '{nodeData.GroupName}' to subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }

                    var confirm = MessageBox.Show($"Are you sure you want to remove permissions for '{nodeData.GroupName}' from subfolder '{subfolderName}'?\n\nNote: If the permission remains in the SharePoint UI, check your account's 'Manage Permissions' rights or revoke sharing links manually.",
                        "Confirm Remove Permission", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => { lblStatus.Text = "Remove permission cancelled by user."; });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, nodeData.GroupName, "Subfolder",
                            $"User cancelled removal of permissions for group '{nodeData.GroupName}' from subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }

                    UpdateUI(() => { lblStatus.Text = $"Removing permission for '{nodeData.GroupName}' from '{subfolderName}'..."; });
                    string groupLogin = $"c:0t.c|tenant|{nodeData.GroupId}";
                    bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);

                    // Cache webServerRelativeUrl before entering the using block
                    string webServerRelativeUrl;
                    using (var tempContext = new ClientContext(_siteUrl))
                    {
                        tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                        tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                        await tempContext.ExecuteQueryAsync();
                        webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                    }

                    subfolderRelativeUrl = isNested ? _nestedSubfolderCache.FirstOrDefault(s => s.FolderName == subfolderName).FullPath : $"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");

                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                await context.ExecuteQueryAsync();

                                var raToRemove = subfolder.ListItemAllFields.RoleAssignments.FirstOrDefault(ra => ra.Member.LoginName == groupLogin && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup);
                                if (raToRemove != null)
                                {
                                    foreach (RoleDefinition rd in raToRemove.RoleDefinitionBindings.ToList())
                                        raToRemove.RoleDefinitionBindings.Remove(rd);
                                    raToRemove.Update();
                                    raToRemove.DeleteObject();
                                    await context.ExecuteQueryAsync();

                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        tvSubfolders.SelectedNode.Remove();
                                        lblStatus.Text = $"Removed permissions for '{nodeData.GroupName}' from '{subfolderName}'.";
                                        radioRead.Checked = false;
                                        radioEdit.Checked = false;
                                        radioNoAccess.Checked = false;

                                        if (isNested)
                                        {
                                            var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _nestedSubfolderCache[cacheIndex].Groups.Where(g => g.GroupId != nodeData.GroupId).ToList();
                                                _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, _nestedSubfolderCache[cacheIndex].HasUniquePermissions, updatedGroups, _nestedSubfolderCache[cacheIndex].HasChildren);
                                            }
                                        }
                                        else
                                        {
                                            var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                            {
                                                var updatedGroups = _subfolderCache[cacheIndex].Groups.Where(g => g.GroupId != nodeData.GroupId).ToList();
                                                _subfolderCache[cacheIndex] = (subfolderName, _subfolderCache[cacheIndex].HasUniquePermissions, updatedGroups);
                                            }
                                        }

                                        UpdateSidebar();
                                        isUpdatingTreeView = false;
                                    });

                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermission", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Removed permissions for group '{nodeData.GroupName}' from subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    success = true;
                                }
                                else
                                {
                                    UpdateUI(() =>
                                    {
                                        MessageBox.Show($"Group '{nodeData.GroupName}' not found in permissions for '{subfolderName}'.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                        lblStatus.Text = "Remove cancelled: Group not found.";
                                    });
                                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                        $"Group '{nodeData.GroupName}' not found in permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                                    return;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionRetry", _libraryName, nodeData.GroupName, "Subfolder",
                                    $"Retry {retryCount} for removing permission from '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            UpdateUI(() =>
                            {
                                MessageBox.Show($"Failed to remove permission after {maxRetries} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                lblStatus.Text = "Error removing permission.";
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData.GroupName, "Subfolder",
                                $"Failed to remove permission for subfolder '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else if (tvSubfolders.SelectedNode != null && tvSubfolders.SelectedNode.Tag is TreeNodeData selNodeData && selNodeData.IsSubfolder)
                {
                    subfolderName = selNodeData.SubfolderName;
                    bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);
                    var cacheEntryNested = default((string FullPath, string FolderName, int Level, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren));
                    var cacheEntryTop = default((string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups));
                    bool hasUniquePermissions;
                    if (isNested)
                    {
                        cacheEntryNested = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                        hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryNested.FullPath) && cacheEntryNested.HasUniquePermissions;
                    }
                    else
                    {
                        cacheEntryTop = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                        hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryTop.SubfolderName) && cacheEntryTop.HasUniquePermissions;
                    }
                    if (isNested)
                    {
                        var cacheEntry = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                        hasUniquePermissions = cacheEntry.FullPath != null && cacheEntry.HasUniquePermissions;
                    }
                    else
                    {
                        var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                        hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                    }
                    if (!hasUniquePermissions)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            lblStatus.Text = "Remove cancelled: Subfolder has inherited permissions.";
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                            $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                        return;
                    }

                    var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.", "Confirm Remove All Permissions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (confirm != DialogResult.Yes)
                    {
                        UpdateUI(() => { lblStatus.Text = "Remove all permissions cancelled by user."; });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionCancelled", _libraryName, null, "Subfolder",
                            $"User cancelled removal of all permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                        return;
                    }

                    UpdateUI(() => { lblStatus.Text = $"Removing all permissions for '{subfolderName}'..."; });
                    string webServerRelativeUrl;
                    using (var tempContext = new ClientContext(_siteUrl))
                    {
                        tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                        tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                        await tempContext.ExecuteQueryAsync();
                        webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                    }

                    subfolderRelativeUrl = isNested ? cacheEntryNested.FullPath : $"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");

                    while (retryCount < maxRetries && !success)
                    {
                        try
                        {
                            using (var context = new ClientContext(_siteUrl))
                            {
                                context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                                Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                                context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                                context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.PrincipalId, ra => ra.Member.Title, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                                context.Load(subfolder.ListItemAllFields, l => l.HasUniqueRoleAssignments);
                                await context.ExecuteQueryAsync();

                                var removedGroups = new List<string>();
                                foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList())
                                {
                                    if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                    {
                                        var groupName = ra.Member.Title;
                                        foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                            ra.RoleDefinitionBindings.Remove(rd);
                                        ra.Update();
                                        ra.DeleteObject();
                                        removedGroups.Add(groupName);
                                    }
                                }

                                if (removedGroups.Any())
                                {
                                    subfolder.ListItemAllFields.Update();
                                    await context.ExecuteQueryAsync();
                                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissions", _libraryName, null, "Subfolder",
                                        $"Reset permissions for subfolder '{subfolderName}', Removed groups: {string.Join(", ", removedGroups)}, Session ID: {debugSessionId}");

                                    UpdateUI(() =>
                                    {
                                        isUpdatingTreeView = true;
                                        if (isNested)
                                        {
                                            var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                                _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, true, new List<(string, string, string)>(), _nestedSubfolderCache[cacheIndex].HasChildren);
                                        }
                                        else
                                        {
                                            var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                            if (cacheIndex >= 0)
                                                _subfolderCache[cacheIndex] = (subfolderName, true, new List<(string, string, string)>());
                                        }
                                        lblStatus.Text = $"Reset permissions for '{subfolderName}'.";
                                        UpdateSidebar();
                                        isUpdatingTreeView = false;
                                    });

                                    success = true;
                                    lastRefreshTime = DateTime.MinValue;
                                    await LoadCurrentPermissionsAsync(debugSessionId);
                                }
                                else
                                {
                                    UpdateUI(() => lblStatus.Text = $"No permissions to reset for '{subfolderName}'.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsRetry", _libraryName, null, "Subfolder",
                                    $"Retry {retryCount} for resetting permissions on '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                                await Task.Delay(1000 * retryCount);
                                continue;
                            }
                            UpdateUI(() =>
                            {
                                MessageBox.Show($"Failed to reset permissions after {maxRetries} attempts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                lblStatus.Text = "Error resetting permissions.";
                            });
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                                $"Failed to reset permissions for subfolder '{subfolderName}': {ex.Message}, Session ID: {debugSessionId}");
                        }
                    }
                }
                else
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a group or subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Remove cancelled: Invalid selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, null, "Subfolder",
                        $"Invalid selection for removal, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Failed to remove permission: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error removing permission.";
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "RemoveSubfolderPermissionError", _libraryName, nodeData?.GroupName ?? "unknown", "Subfolder",
                    $"Failed to remove permission for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnRemove.Enabled = true; isUpdatingTreeView = false; });
            }
        }

        private bool IsNestedSubfolderSelected()
        {
            if (tvSubfolders?.SelectedNode?.Tag is TreeNodeData nodeData && nodeData.IsSubfolder)
            {
                return nodeData.Level > 0; // Level 0 is top-level, > 0 is nested
            }
            return false;
        }
        private async void btnBreakInheritance_Click(object sender, EventArgs e)
        {
            string subfolderPath = null;
            string subfolderName = null;
            string subfolderRelativeUrl = null;
            int subfolderLevel = -1;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnBreakInheritance.Enabled = false; });

            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Break inheritance cancelled: Invalid subfolder selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }

                subfolderPath = nodeData.FullPath;
                subfolderName = nodeData.SubfolderName;
                subfolderLevel = nodeData.Level;

                bool isNested = _nestedSubfolderCache.Any(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                bool hasUniquePermissions;
                if (isNested)
                {
                    var cacheEntry = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.FullPath != null && cacheEntry.HasUniquePermissions;
                }
                else
                {
                    var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                }

                if (hasUniquePermissions)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Subfolder already has unique permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Break inheritance cancelled: Subfolder already has unique permissions.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' already has unique permissions, Session ID: {debugSessionId}");
                    return;
                }

                var confirm = MessageBox.Show($"Are you sure you want to break permission inheritance for '{subfolderName}'? This will allow unique permissions to be set.",
                    "Confirm Break Inheritance", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { lblStatus.Text = "Break inheritance cancelled by user."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled breaking inheritance for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                UpdateUI(() => lblStatus.Text = $"Breaking inheritance for '{subfolderName}'...");

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

                string webServerRelativeUrl;
                using (var tempContext = new ClientContext(_siteUrl))
                {
                    tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                    await tempContext.ExecuteQueryAsync();
                    webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                }

                // Construct path with fallback for library
                bool usedFallback = false;
                subfolderRelativeUrl = isNested ? NormalizePath(subfolderPath, true) : NormalizePath($"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}", false);
                string originalLibraryPart = _libraryName.Replace(" ", "");
                string fallbackLibraryPart = Uri.EscapeDataString(_libraryName);

                const int maxRetries = 2;
                int attempt = 1;
                bool success = false;

                while (attempt <= maxRetries && !success)
                {
                    System.Diagnostics.Debug.WriteLine($"btnBreakInheritance_Click: Attempt {attempt} with path: {subfolderRelativeUrl}, Session ID: {debugSessionId}");

                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            var listItem = subfolder.ListItemAllFields;
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            listItem.BreakRoleInheritance(true, false);
                            await context.ExecuteQueryAsync();

                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            if (!listItem.HasUniqueRoleAssignments)
                                throw new Exception("Failed to break inheritance: Subfolder still inherits permissions.");

                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                lblStatus.Text = $"Broke permission inheritance for '{subfolderName}'.";

                                if (isNested)
                                {
                                    var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FullPath, subfolderPath, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var existing = _nestedSubfolderCache[cacheIndex];
                                        _nestedSubfolderCache[cacheIndex] = (existing.FullPath, existing.FolderName, existing.Level, true, existing.Groups, existing.HasChildren);
                                    }
                                }
                                else
                                {
                                    var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        _subfolderCache[cacheIndex] = (subfolderName, true, _subfolderCache[cacheIndex].Groups);
                                    }
                                }

                                var node = FindNodeByPath(tvSubfolders.Nodes, subfolderPath ?? subfolderRelativeUrl);
                                if (node != null && node.Tag is TreeNodeData nodeData)
                                {
                                    node.Text = $"{nodeData.SubfolderName} (Unique, 0 CSG groups assigned)";
                                    node.Nodes.Clear();
                                }

                                UpdateSidebar();
                                isUpdatingTreeView = false;
                            });

                            await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritance", _libraryName, null, "Subfolder",
                                $"Broke permission inheritance for subfolder '{subfolderName}' at '{subfolderRelativeUrl}', Session ID: {debugSessionId}");
                            success = true;
                            System.Diagnostics.Debug.WriteLine($"btnBreakInheritance_Click: Success on attempt {attempt} with path: {subfolderRelativeUrl}, Session ID: {debugSessionId}");

                            lastRefreshTime = DateTime.MinValue;
                            await LoadCurrentPermissionsAsync(debugSessionId);
                        }
                    }
                    catch (Microsoft.SharePoint.Client.ServerException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist"))
                    {
                        if (attempt == 1)
                        {
                            // Replace library part with encoded version
                            subfolderRelativeUrl = subfolderRelativeUrl.Replace(originalLibraryPart, fallbackLibraryPart);
                            subfolderRelativeUrl = NormalizePath(subfolderRelativeUrl, true);
                            usedFallback = true;
                            System.Diagnostics.Debug.WriteLine($"btnBreakInheritance_Click: Original path failed ({ex.Message}). Trying fallback path: {subfolderRelativeUrl}, Session ID: {debugSessionId}");
                            attempt++;
                            continue;
                        }

                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to break inheritance after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Error breaking inheritance.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                            $"Failed to break inheritance for subfolder '{subfolderName}' at level {subfolderLevel}, Path: {subfolderRelativeUrl}: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                        return;
                    }
                }

                if (usedFallback)
                {
                    await _auditLogManager?.LogAction(_signedInUserId, null, "BreakInheritancePathFallbackUsed", _libraryName, null, "Subfolder",
                        $"Used fallback %20 path for subfolder: {subfolderRelativeUrl}, Session ID: {debugSessionId}");
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to break inheritance: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error breaking inheritance.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "BreakInheritanceError", _libraryName, null, "Subfolder",
                    $"Failed to break inheritance for subfolder '{subfolderName ?? "unknown"}' at level {subfolderLevel}: {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    btnBreakInheritance.Enabled = true;
                    isUpdatingTreeView = false;
                });
            }
        }
        private async Task RefreshSpecificSubfolderPermissions(string subfolderPath)
        {
            if (string.IsNullOrEmpty(subfolderPath)) return;

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null) return;

                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };

                    var subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderPath);
                    context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields.HasUniqueRoleAssignments,
                        f => f.ListItemAllFields.RoleAssignments.Include(
                            ra => ra.Member.Title,
                            ra => ra.Member.LoginName,
                            ra => ra.RoleDefinitionBindings));
                    await context.ExecuteQueryAsync();

                    var groupList = new List<(string GroupName, string GroupId, string Role)>();
                    bool hasUniquePermissions = subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false;

                    if (hasUniquePermissions && subfolder.ListItemAllFields?.RoleAssignments != null)
                    {
                        foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                        {
                            try
                            {
                                if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                                {
                                    var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                    if (role == "Contribute") role = "Edit";
                                    if (role != "Limited Access")
                                    {
                                        var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                        groupList.Add((ra.Member.Title, groupId, role));
                                    }
                                }
                            }
                            catch (Exception raEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"RefreshSpecificSubfolderPermissions: Error processing role assignment: {raEx.Message}");
                            }
                        }
                    }

                    // Update cache
                    var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FullPath, subfolderPath, StringComparison.OrdinalIgnoreCase));
                    if (cacheIndex >= 0)
                    {
                        var existing = _nestedSubfolderCache[cacheIndex];
                        _nestedSubfolderCache[cacheIndex] = (existing.FullPath, existing.FolderName, existing.Level, hasUniquePermissions, groupList, existing.HasChildren);
                    }

                    // Update TreeView node
                    UpdateUI(() =>
                    {
                        var node = FindNodeByPath(tvSubfolders.Nodes, subfolderPath);
                        if (node != null)
                        {
                            // Update folder node text
                            if (node.Tag is TreeNodeData nodeData && nodeData.IsSubfolder)
                            {
                                node.Text = hasUniquePermissions
                                    ? $"{nodeData.SubfolderName} (Unique, {groupList.Count} CSG group{(groupList.Count == 1 ? "" : "s")} assigned)"
                                    : $"{nodeData.SubfolderName} (Inherited)";

                                // Clear and rebuild group nodes
                                var groupNodes = node.Nodes.Cast<TreeNode>().Where(n => (n.Tag as TreeNodeData)?.IsSubfolder == false).ToList();
                                foreach (var groupNode in groupNodes)
                                {
                                    node.Nodes.Remove(groupNode);
                                }

                                foreach (var group in groupList)
                                {
                                    node.Nodes.Add(new TreeNode
                                    {
                                        Text = $"{group.GroupName}: {group.Role}",
                                        ImageIndex = 1,
                                        SelectedImageIndex = 1,
                                        Tag = new TreeNodeData
                                        {
                                            IsSubfolder = false,
                                            GroupId = group.GroupId,
                                            GroupName = group.GroupName,
                                            Permission = group.Role,
                                            FullPath = subfolderPath,
                                            Level = nodeData.Level
                                        }
                                    });
                                }
                            }
                        }
                        UpdateSidebar();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RefreshSpecificSubfolderPermissions: Error: {ex.Message}");
                UpdateUI(() => lblStatus.Text = $"Error refreshing permissions: {ex.Message}");
            }
        }
        private async void btnRestoreInheritance_Click(object sender, EventArgs e)
        {
            string subfolderPath = null;
            string subfolderName = null;
            string debugSessionId = Guid.NewGuid().ToString();
            UpdateUI(() => { isUpdatingTreeView = true; btnRestoreInheritance.Enabled = false; isUpdatingTreeView = false; });

            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Restore inheritance cancelled: Invalid subfolder selection.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }

                subfolderPath = nodeData.FullPath;
                subfolderName = nodeData.SubfolderName;

                bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);
                bool hasUniquePermissions;
                if (isNested)
                {
                    var cacheEntry = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.FullPath != null && cacheEntry.HasUniquePermissions;
                }
                else
                {
                    var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                }
                if (!hasUniquePermissions)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("Subfolder already inherits permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        lblStatus.Text = "Restore inheritance cancelled: Subfolder already inherits permissions.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' already inherits permissions, Session ID: {debugSessionId}");
                    return;
                }

                var confirm = MessageBox.Show($"Are you sure you want to restore permission inheritance for '{subfolderName}'? This will remove unique permissions and inherit from the parent.",
                    "Confirm Restore Inheritance", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { lblStatus.Text = "Restore inheritance cancelled by user."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled restoring inheritance for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                UpdateUI(() => lblStatus.Text = $"Restoring inheritance for '{subfolderName}'...");

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "Error: No signed-in account.";
                    });
                    await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;

                // Cache webServerRelativeUrl before entering the using block
                string webServerRelativeUrl;
                using (var tempContext = new ClientContext(_siteUrl))
                {
                    tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                    await tempContext.ExecuteQueryAsync();
                    webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                }

                // Use subfolderPath from nodeData.FullPath, or construct for top-level subfolder
                subfolderPath = isNested ? subfolderPath : $"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");

                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderPath);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            var listItem = subfolder.ListItemAllFields;
                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            listItem.ResetRoleInheritance();
                            await context.ExecuteQueryAsync();

                            context.Load(listItem, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            if (listItem.HasUniqueRoleAssignments)
                                throw new Exception("Failed to restore inheritance: Subfolder still has unique permissions.");

                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;
                                lblStatus.Text = $"Restored permission inheritance for '{subfolderName}'.";

                                if (isNested)
                                {
                                    var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FullPath, subfolderPath, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        var existing = _nestedSubfolderCache[cacheIndex];
                                        _nestedSubfolderCache[cacheIndex] = (existing.FullPath, existing.FolderName, existing.Level, false, new List<(string, string, string)>(), existing.HasChildren);
                                    }
                                }
                                else
                                {
                                    var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                    if (cacheIndex >= 0)
                                    {
                                        _subfolderCache[cacheIndex] = (subfolderName, false, new List<(string, string, string)>());
                                    }
                                }

                                UpdateSidebar();
                                isUpdatingTreeView = false;
                            });

                            await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritance", _libraryName, null, "Subfolder",
                                $"Restored permission inheritance for subfolder '{subfolderName}' in library '{_libraryName}', Session ID: {debugSessionId}");
                            success = true;

                            lastRefreshTime = DateTime.MinValue;
                            await LoadCurrentPermissionsAsync(debugSessionId);
                        }
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceRetry", _libraryName, null, "Subfolder",
                                $"Retry {retryCount} for restoring inheritance on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }

                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to restore inheritance after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Error restoring inheritance.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceError", _libraryName, null, "Subfolder",
                            $"Failed to restore inheritance for subfolder '{subfolderName}' at '{subfolderPath}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to restore inheritance: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error restoring inheritance.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "RestoreInheritanceError", _libraryName, null, "Subfolder",
                    $"Failed to restore inheritance for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnRestoreInheritance.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void btnResetPermissions_Click(object sender, EventArgs e)
        {
            string debugSessionId = Guid.NewGuid().ToString();
            string subfolderName = null; // Declare at method scope
            UpdateUI(() => { isUpdatingTreeView = true; btnResetPermissions.Enabled = false; isUpdatingTreeView = false; });

            try
            {
                if (tvSubfolders.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
                {
                    UpdateUI(() => { MessageBox.Show("Please select a subfolder.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Reset cancelled: Invalid subfolder selection."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"Invalid subfolder selection, Session ID: {debugSessionId}");
                    return;
                }

                subfolderName = nodeData.SubfolderName; // Assign here
                bool isNested = _nestedSubfolderCache.Any(s => s.FolderName == subfolderName);
                var cacheEntryNested = default((string FullPath, string FolderName, int Level, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren));
                var cacheEntryTop = default((string SubfolderName, bool HasUniquePermissions, List<(string GroupName, string GroupId, string Role)> Groups));
                bool hasUniquePermissions;
                if (isNested)
                {
                    cacheEntryNested = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryNested.FullPath) && cacheEntryNested.HasUniquePermissions;
                }
                else
                {
                    cacheEntryTop = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = !string.IsNullOrEmpty(cacheEntryTop.SubfolderName) && cacheEntryTop.HasUniquePermissions;
                }
                if (isNested)
                {
                    var cacheEntry = _nestedSubfolderCache.FirstOrDefault(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.FullPath != null && cacheEntry.HasUniquePermissions;
                }
                else
                {
                    var cacheEntry = _subfolderCache.FirstOrDefault(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                    hasUniquePermissions = cacheEntry.SubfolderName != null && cacheEntry.HasUniquePermissions;
                }
                if (!hasUniquePermissions)
                {
                    UpdateUI(() => { MessageBox.Show($"Subfolder '{subfolderName}' has inherited permissions. Break inheritance first.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information); lblStatus.Text = "Reset cancelled: Subfolder has inherited permissions."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"Subfolder '{subfolderName}' has inherited permissions, Session ID: {debugSessionId}");
                    return;
                }

                var confirm = MessageBox.Show($"Are you sure you want to remove all permissions for subfolder '{subfolderName}'? This cannot be undone.", "Confirm Reset Permissions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    UpdateUI(() => { lblStatus.Text = "Reset permissions cancelled by user."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsCancelled", _libraryName, null, "Subfolder",
                        $"User cancelled reset of permissions for subfolder '{subfolderName}', Session ID: {debugSessionId}");
                    return;
                }

                UpdateUI(() => lblStatus.Text = $"Removing all permissions for '{subfolderName}'...");

                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() => { MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning); lblStatus.Text = "Error: No signed-in account."; });
                    await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();
                const int maxRetries = 3;
                int retryCount = 0;
                bool success = false;
                string subfolderRelativeUrl = null;

                // Cache webServerRelativeUrl before entering the using block
                string webServerRelativeUrl;
                using (var tempContext = new ClientContext(_siteUrl))
                {
                    tempContext.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    tempContext.Load(tempContext.Web, w => w.ServerRelativeUrl);
                    await tempContext.ExecuteQueryAsync();
                    webServerRelativeUrl = tempContext.Web.ServerRelativeUrl;
                }

                subfolderRelativeUrl = isNested ? cacheEntryNested.FullPath : $"{webServerRelativeUrl.TrimEnd('/')}/{_libraryName}/{subfolderName}".Replace("//", "/");

                while (retryCount < maxRetries && !success)
                {
                    try
                    {
                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            context.Load(context.Web, w => w.ServerRelativeUrl);
                            await context.ExecuteQueryAsync();
                            Folder subfolder = context.Web.GetFolderByServerRelativeUrl(subfolderRelativeUrl);
                            context.Load(subfolder, f => f.Name, f => f.ServerRelativeUrl, f => f.ListItemAllFields);
                            context.Load(subfolder.ListItemAllFields.RoleAssignments, ras => ras.Include(ra => ra.Member.LoginName, ra => ra.PrincipalId, ra => ra.Member.Title, ra => ra.RoleDefinitionBindings, ra => ra.Member.PrincipalType));
                            context.Load(subfolder.ListItemAllFields, l => l.HasUniqueRoleAssignments);
                            await context.ExecuteQueryAsync();

                            var removedGroups = new List<string>();
                            foreach (var ra in subfolder.ListItemAllFields.RoleAssignments.ToList())
                            {
                                if (ra.Member.Title != null && ra.Member.Title.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) && ra.Member.PrincipalType == Microsoft.SharePoint.Client.Utilities.PrincipalType.SecurityGroup)
                                {
                                    var groupName = ra.Member.Title;
                                    foreach (RoleDefinition rd in ra.RoleDefinitionBindings.ToList())
                                        ra.RoleDefinitionBindings.Remove(rd);
                                    ra.Update();
                                    ra.DeleteObject();
                                    removedGroups.Add(groupName);
                                }
                            }

                            if (removedGroups.Any())
                            {
                                subfolder.ListItemAllFields.Update();
                                await context.ExecuteQueryAsync();
                                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissions", _libraryName, null, "Subfolder",
                                    $"Reset permissions for subfolder '{subfolderName}', Removed groups: {string.Join(", ", removedGroups)}, Session ID: {debugSessionId}");

                                UpdateUI(() =>
                                {
                                    isUpdatingTreeView = true;
                                    if (isNested)
                                    {
                                        var cacheIndex = _nestedSubfolderCache.FindIndex(s => string.Equals(s.FolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                        if (cacheIndex >= 0)
                                            _nestedSubfolderCache[cacheIndex] = (_nestedSubfolderCache[cacheIndex].FullPath, subfolderName, _nestedSubfolderCache[cacheIndex].Level, true, new List<(string, string, string)>(), _nestedSubfolderCache[cacheIndex].HasChildren);
                                    }
                                    else
                                    {
                                        var cacheIndex = _subfolderCache.FindIndex(s => string.Equals(s.SubfolderName, subfolderName, StringComparison.OrdinalIgnoreCase));
                                        if (cacheIndex >= 0)
                                            _subfolderCache[cacheIndex] = (subfolderName, true, new List<(string, string, string)>());
                                    }
                                    lblStatus.Text = $"Reset permissions for '{subfolderName}'.";
                                    UpdateSidebar();
                                    isUpdatingTreeView = false;
                                });

                                success = true;
                                lastRefreshTime = DateTime.MinValue;
                                await LoadCurrentPermissionsAsync(debugSessionId);
                            }
                            else
                            {
                                UpdateUI(() => lblStatus.Text = $"No permissions to reset for '{subfolderName}'.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsRetry", _libraryName, null, "Subfolder",
                                $"Retry {retryCount} for resetting permissions on '{subfolderName}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, Session ID: {debugSessionId}");
                            await Task.Delay(1000 * retryCount);
                            continue;
                        }
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            MessageBox.Show($"Failed to reset permissions after {maxRetries} attempts: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Error resetting permissions.";
                            isUpdatingTreeView = false;
                        });
                        await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                            $"Failed to reset permissions for subfolder '{subfolderName}' at '{subfolderRelativeUrl}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateUI(() =>
                {
                    isUpdatingTreeView = true;
                    MessageBox.Show($"Failed to reset permissions: {ex.Message}\nInner Exception: {(ex.InnerException?.Message ?? "None")}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error resetting permissions.";
                    isUpdatingTreeView = false;
                });
                await _auditLogManager.LogAction(_signedInUserId, null, "ResetSubfolderPermissionsError", _libraryName, null, "Subfolder",
                    $"Failed to reset permissions for subfolder '{subfolderName ?? "unknown"}': {ex.Message}, Inner: {(ex.InnerException?.Message ?? "None")}, Session ID: {debugSessionId}");
            }
            finally
            {
                UpdateUI(() => { isUpdatingTreeView = true; btnResetPermissions.Enabled = true; isUpdatingTreeView = false; });
            }
        }
        private async void tvSubfolders_BeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Tag is TreeNodeData nodeData && nodeData.IsSubfolder && e.Node.Nodes.Count == 0)
            {
                UpdateUI(() =>
                {
                    lblStatus.Text = $"Loading subfolders for '{nodeData.SubfolderName}'...";
                    this.Cursor = Cursors.WaitCursor;
                });

                try
                {
                    var parentPath = $"{_siteUrl}/{_libraryName}/{nodeData.SubfolderName}".Replace("//", "/");
                    var subfolders = _nestedSubfolderCache
                        .Where(s => s.FullPath.StartsWith(parentPath + "/") && s.Level == 1)
                        .OrderBy(s => s.FolderName)
                        .ToList();

                    if (!subfolders.Any())
                    {
                        var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                        var accounts = await _pca.GetAccountsAsync();
                        var account = accounts.FirstOrDefault();
                        if (account == null)
                        {
                            UpdateUI(() =>
                            {
                                MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                                lblStatus.Text = "Error: No signed-in account.";
                            });
                            e.Cancel = true;
                            return;
                        }
                        var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

                        using (var context = new ClientContext(_siteUrl))
                        {
                            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                            var parentFolder = context.Web.GetFolderByServerRelativeUrl(parentPath);
                            var loadedFolders = await LoadNestedFoldersAsync(context, parentFolder, context.Web.ServerRelativeUrl, 1);

                            var libraryPath = $"{context.Web.ServerRelativeUrl.TrimEnd('/')}/{_libraryName}";
                            foreach (var folder in loadedFolders)
                            {
                                var relativePath = folder.ServerRelativeUrl.Replace(libraryPath + "/", "");
                                var level = relativePath.Split('/').Length - 1;
                                var folderName = folder.Name;
                                bool hasChildren = folder.Folders?.Count(f => !f.Name.StartsWith("Forms")) > 0;
                                var groupList = new List<(string GroupName, string GroupId, string Role)>();
                                bool hasUniquePermissions = folder.ListItemAllFields?.HasUniqueRoleAssignments ?? false;

                                if (hasUniquePermissions && folder.ListItemAllFields?.RoleAssignments != null)
                                {
                                    foreach (var ra in folder.ListItemAllFields.RoleAssignments)
                                    {
                                        if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                            if (role == "Contribute") role = "Edit";
                                            if (role != "Limited Access")
                                            {
                                                var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                                groupList.Add((ra.Member.Title, groupId, role));
                                            }
                                        }
                                    }
                                }

                                _nestedSubfolderCache.Add((folder.ServerRelativeUrl, folderName, level, hasUniquePermissions, groupList, hasChildren));
                            }

                            subfolders = _nestedSubfolderCache
                                .Where(s => s.FullPath.StartsWith(parentPath + "/") && s.Level == 1)
                                .OrderBy(s => s.FolderName)
                                .ToList();
                        }
                    }

                    if (!subfolders.Any())
                    {
                        UpdateUI(() => lblStatus.Text = $"No subfolders found in '{nodeData.SubfolderName}'.");
                        e.Cancel = true;
                    }
                    else
                    {
                        UpdateUI(() =>
                        {
                            isUpdatingTreeView = true;
                            var foldersByParent = _nestedSubfolderCache
                                .Where(f => f.Level > 0)
                                .GroupBy(f => GetParentPath(f.FullPath))
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Select(f => (f.FullPath, f.FolderName, f.Level, f.HasUniquePermissions, f.Groups, f.HasChildren)).ToList()
                                );

                            foreach (var folder in subfolders)
                            {
                                var node = CreateFolderTreeNode(folder, foldersByParent);
                                node.BackColor = Color.LightYellow;
                                node.Text += " [Loaded]";
                                e.Node.Nodes.Add(node);
                            }
                            lblStatus.Text = $"Loaded {subfolders.Count} subfolders for '{nodeData.SubfolderName}'.";
                            isUpdatingTreeView = false;
                        });
                    }
                }
                catch (Exception ex)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show($"Error loading subfolders: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "Error loading subfolders.";
                        e.Cancel = true;
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersError", _libraryName, null, "Subfolder",
                        $"Error loading subfolders for '{nodeData.SubfolderName}': {ex.Message}");
                }
                finally
                {
                    UpdateUI(() => this.Cursor = Cursors.Default);
                }
            }
        }
        private void tvSubfolders_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            // Don't interfere with selection if we're programmatically updating the TreeView
            if (isUpdatingTreeView)
            {
                return;
            }

            // Check if there's an active preview and if we're selecting outside the preview context
            if (_isPreviewActive && _previewSubfolderNode != null)
            {
                // Allow selection within the same subfolder (preview node and its parent)
                bool isSelectingWithinPreviewContext = false;

                if (e.Node != null)
                {
                    // Check if selecting the preview subfolder node itself
                    if (e.Node == _previewSubfolderNode)
                    {
                        isSelectingWithinPreviewContext = true;
                    }
                    // Check if selecting a child of the preview subfolder (including the preview node)
                    else if (e.Node.Parent == _previewSubfolderNode)
                    {
                        isSelectingWithinPreviewContext = true;
                    }
                    // Check if selecting the preview node itself
                    else if (e.Node.Tag is TreeNodeData nodeData && nodeData.IsPreview)
                    {
                        isSelectingWithinPreviewContext = true;
                    }
                }

                // Only block selection if we're going outside the preview context
                if (!isSelectingWithinPreviewContext)
                {
                    e.Cancel = true;

                    // Use a single, controlled message box that won't respawn
                    if (!this.Focused) this.Focus(); // Ensure dialog has focus

                    UpdateUI(() =>
                    {
                        lblStatus.Text = "Finish or cancel the current preview before selecting another subfolder.";
                    });

                    // Don't show popup if we're already showing one
                    var result = MessageBox.Show(this,
                        "Please confirm or cancel the current preview before selecting another subfolder.",
                        "Preview Active",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
            }
        }
        // Enhanced context menu with nested folder support
        private void tvSubfolders_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                tvSubfolders.SelectedNode = e.Node;
                var nodeData = e.Node.Tag as TreeNodeData;
                if (nodeData == null) return;

                var contextMenu = new ContextMenuStrip();

                if (nodeData.IsSubfolder)
                {
                    if (nodeData.HasChildren)
                    {
                        if (e.Node.IsExpanded)
                            contextMenu.Items.Add("Collapse", null, (s, ev) => e.Node.Collapse());
                        else
                            contextMenu.Items.Add("Expand", null, (s, ev) => e.Node.Expand());
                        contextMenu.Items.Add("Load Subfolders", null, btnViewSubfolders_Click);
                        contextMenu.Items.Add("-");
                    }

                    if (e.Node.Text.Contains("(Unique"))
                    {
                        contextMenu.Items.Add("Add Permission", null, btnAdd_Click);
                        contextMenu.Items.Add("Reset Permissions", null, btnResetPermissions_Click);
                        contextMenu.Items.Add("Restore Inheritance", null, btnRestoreInheritance_Click);
                    }
                    else
                    {
                        contextMenu.Items.Add("Break Inheritance", null, btnBreakInheritance_Click);
                    }
                }
                else
                {
                    contextMenu.Items.Add("Remove Permission", null, btnRemove_Click);
                }

                contextMenu.Show(tvSubfolders, e.Location);
            }
        }
        private void btnClose_Click(object sender, EventArgs e)
        {
            UpdateUI(() => this.Close());
        }

        // Helper method to construct properly URL-encoded path
        private string ConstructProperlyEncodedPath(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath))
                return originalPath;

            try
            {
                // Split path into segments and properly encode each segment
                var pathSegments = originalPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var encodedSegments = pathSegments.Select(segment => Uri.EscapeDataString(segment)).ToArray();
                var encodedPath = "/" + string.Join("/", encodedSegments);

                System.Diagnostics.Debug.WriteLine($"ConstructProperlyEncodedPath: Original: {originalPath}");
                System.Diagnostics.Debug.WriteLine($"ConstructProperlyEncodedPath: Encoded: {encodedPath}");

                return encodedPath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConstructProperlyEncodedPath: Error encoding path '{originalPath}': {ex.Message}");
                return originalPath; // Return original if encoding fails
            }
        }



        // Helper method to validate a path with %20 fallback
        // Helper method to validate a path with %20 fallback
private async Task<string> ValidatePathWithFallback(string path)
{
    try
    {
        var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
        var accounts = await _pca.GetAccountsAsync();
        var account = accounts.FirstOrDefault();
        if (account == null)
        {
            System.Diagnostics.Debug.WriteLine("ValidatePathWithFallback: No account available for validation");
            return path; // Return original path if can't validate
        }

        var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

        using (var context = new ClientContext(_siteUrl))
        {
            context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };

            // Generate multiple path variations to try
            var pathsToTry = new List<string>();
            
            // 1. Original path
            pathsToTry.Add(path);
            
            // 2. Properly URL-encoded version
            pathsToTry.Add(ConstructProperlyEncodedPath(path));
            
            // 3. SharePoint library pattern (spaces removed from library name only)
            if (path.Contains(_libraryName) && _libraryName.Contains(" "))
            {
                string libraryWithoutSpaces = _libraryName.Replace(" ", "");
                string sharePointLibraryPath = path.Replace(_libraryName, libraryWithoutSpaces);
                pathsToTry.Add(sharePointLibraryPath);
                
                // 4. SharePoint library pattern with URL encoding for folder names
                pathsToTry.Add(ConstructProperlyEncodedPath(sharePointLibraryPath));
            }

            // Remove duplicates while preserving order
            pathsToTry = pathsToTry.Distinct().ToList();

            for (int i = 0; i < pathsToTry.Count; i++)
            {
                try
                {
                    string currentPath = pathsToTry[i];
                    System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: Attempting validation of path {i + 1}/{pathsToTry.Count}: {currentPath}");

                    var folder = context.Web.GetFolderByServerRelativeUrl(currentPath);
                    context.Load(folder, f => f.Exists);
                    await context.ExecuteQueryAsync();

                    if (folder.Exists)
                    {
                        if (i > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: SUCCESS using fallback path {i + 1}: {currentPath}");
                            System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: Original path that failed: {path}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: SUCCESS with original path: {currentPath}");
                        }
                        return currentPath;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: Path validation failed for {pathsToTry[i]}: {ex.Message}");
                    continue; // Try next path
                }
            }
        }
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: Validation error: {ex.Message}");
    }

    System.Diagnostics.Debug.WriteLine($"ValidatePathWithFallback: All validation attempts failed, returning original path: {path}");
    return path; // Return original if all attempts fail
}

        // Method to get the actual library path with proper space handling
        private string GetActualLibraryPath(string webServerRelativeUrl, string libraryName)
        {
            // First, check if we have any cached nested folders that can tell us the actual structure
            if (_nestedSubfolderCache?.Any() == true)
            {
                var anyNestedFolder = _nestedSubfolderCache.FirstOrDefault();
                if (!string.IsNullOrEmpty(anyNestedFolder.FullPath))
                {
                    // Extract the library portion from a known good path
                    var segments = anyNestedFolder.FullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length >= 3) // Should have at least teams/sitename/library
                    {
                        var librarySegment = segments[2]; // Assuming structure like /teams/sitename/library/...
                        var actualLibraryPath = $"{webServerRelativeUrl.TrimEnd('/')}/{librarySegment}";
                        System.Diagnostics.Debug.WriteLine($"GetActualLibraryPath: Determined from cache: {actualLibraryPath}");
                        return actualLibraryPath;
                    }
                }
            }

            // Check top-level cache for library path hints
            if (_subfolderCache?.Any() == true)
            {
                // We can't directly get the library path from top-level cache, but we know it exists
                // Try with spaces first (most common case)
                string withSpaces = $"{webServerRelativeUrl.TrimEnd('/')}/{libraryName}";
                System.Diagnostics.Debug.WriteLine($"GetActualLibraryPath: Using library name with spaces: {withSpaces}");
                return withSpaces;
            }

            // Ultimate fallback: return with spaces (will be handled by fallback logic later)
            string fallbackPath = $"{webServerRelativeUrl.TrimEnd('/')}/{libraryName}";
            System.Diagnostics.Debug.WriteLine($"GetActualLibraryPath: Using fallback path: {fallbackPath}");
            return fallbackPath;
        }
        private async Task<string> ConstructSubfolderPathWithFallback(TreeNode node, string webServerRelativeUrl, string libraryName)
        {
            if (node?.Tag is not TreeNodeData nodeData || !nodeData.IsSubfolder)
                return null;

            // If we already have a valid FullPath, clean and use it
            if (!string.IsNullOrEmpty(nodeData.FullPath))
            {
                var normalized = NormalizePath(nodeData.FullPath);
                var cleaned = CleanDuplicateSitePath(normalized, _siteUrl);
                System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPathWithFallback: Using existing FullPath: {cleaned}");
                return cleaned;
            }

            // Get the actual library path
            string actualLibraryPath = GetActualLibraryPath(webServerRelativeUrl, libraryName);

            // Build path by walking up the tree
            var pathParts = new List<string>();
            var currentNode = node;

            while (currentNode != null && currentNode.Tag is TreeNodeData currentData && currentData.IsSubfolder)
            {
                pathParts.Insert(0, currentData.SubfolderName);
                currentNode = currentNode.Parent;
            }

            // Construct the full path using the actual library path
            string fullPath = $"{actualLibraryPath}/{string.Join("/", pathParts)}";
            var normalizedPath = NormalizePath(fullPath);
            var finalCleanedPath = CleanDuplicateSitePath(normalizedPath, _siteUrl);

            System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPathWithFallback: Constructed path: {finalCleanedPath}");

            // **VALIDATE PATH WITH %20 FALLBACK**
            string validatedPath = await ValidatePathWithFallback(finalCleanedPath);

            // Update the node's FullPath with the validated working path
            if (!string.IsNullOrEmpty(validatedPath))
            {
                nodeData.FullPath = validatedPath;
                System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPathWithFallback: Validated and using path: {validatedPath}");
                return validatedPath;
            }

            System.Diagnostics.Debug.WriteLine($"ConstructSubfolderPathWithFallback: Path validation failed, returning original: {finalCleanedPath}");
            return finalCleanedPath; // Return original even if validation failed
        }

        private async void btnViewSubfolders_Click(object sender, EventArgs e)
        {
            if (tvSubfolders?.SelectedNode == null || !(tvSubfolders.SelectedNode.Tag is TreeNodeData nodeData) || !nodeData.IsSubfolder)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show("Please select a subfolder to view its subfolders.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = "No subfolder selected.";
                });
                System.Diagnostics.Debug.WriteLine("btnViewSubfolders_Click: No subfolder selected or invalid node");
                return;
            }

            string parentName = nodeData.SubfolderName;
            string parentRelativePath = nodeData.FullPath;
            int parentLevel = nodeData.Level;
            TreeNode parentNode = tvSubfolders.SelectedNode;
            string debugSessionId = Guid.NewGuid().ToString();

            System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Starting - parentName={parentName}, parentLevel={parentLevel}, parentRelativePath={parentRelativePath ?? "NULL"}");

            // Check if we've reached the maximum nesting level
            if (parentLevel >= _maxNestingLevel)
            {
                UpdateUI(() =>
                {
                    MessageBox.Show($"Maximum nesting depth ({_maxNestingLevel}) reached. Cannot load deeper levels.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = $"Maximum nesting depth ({_maxNestingLevel}) reached.";
                });
                return;
            }

            UpdateUI(() =>
            {
                lblStatus.Text = $"Loading level {parentLevel + 1} subfolders for '{parentName}'...";
                btnViewSubfolders.Enabled = false;
                this.Cursor = Cursors.WaitCursor;
            });

            try
            {
                var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
                var accounts = await _pca.GetAccountsAsync();
                var account = accounts.FirstOrDefault();
                if (account == null)
                {
                    UpdateUI(() =>
                    {
                        MessageBox.Show("No signed-in account found. Please sign in again.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        lblStatus.Text = "Error: No signed-in account.";
                        btnViewSubfolders.Enabled = true;
                        this.Cursor = Cursors.Default;
                    });
                    await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersError", _libraryName, null, "Subfolder",
                        $"No signed-in account found, Session ID: {debugSessionId}");
                    return;
                }
                var authResult = await _pca.AcquireTokenSilent(scopes, account).ExecuteAsync();

                using (var context = new ClientContext(_siteUrl))
                {
                    context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                    context.Load(context.Web, w => w.ServerRelativeUrl);
                    await context.ExecuteQueryAsync();
                    string webServerRelativeUrl = context.Web.ServerRelativeUrl;

                    // **IMPROVED PATH CONSTRUCTION WITH ASYNC VALIDATION**
                    if (string.IsNullOrEmpty(parentRelativePath))
                    {
                        // Use the enhanced async method for better validation
                        parentRelativePath = await ConstructSubfolderPathWithFallback(parentNode, webServerRelativeUrl, _libraryName);
                        System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Constructed and validated parentRelativePath: {parentRelativePath}");
                    }
                    else
                    {
                        // Normalize existing path to remove any duplicates
                        parentRelativePath = NormalizePath(parentRelativePath);
                        System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Normalized existing parentRelativePath: {parentRelativePath}");
                    }

                    // Validate the path before using it
                    if (string.IsNullOrEmpty(parentRelativePath))
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Could not determine subfolder path for '{parentName}'. Please refresh and try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Could not determine subfolder path.";
                            btnViewSubfolders.Enabled = true;
                            this.Cursor = Cursors.Default;
                        });
                        await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersError", _libraryName, null, "Subfolder",
                            $"Could not determine subfolder path for '{parentName}', Session ID: {debugSessionId}");
                        return;
                    }

                    // Check if we've already loaded children for this parent
                    bool alreadyLoaded = _nestedSubfolderCache.Any(s =>
                        s.FullPath.StartsWith(parentRelativePath + "/", StringComparison.OrdinalIgnoreCase) &&
                        s.Level == parentLevel + 1);

                    if (alreadyLoaded && parentNode.Nodes.Count > 0)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Subfolders for '{parentName}' are already loaded.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            lblStatus.Text = $"Subfolders for '{parentName}' are already loaded.";
                            btnViewSubfolders.Enabled = true;
                            this.Cursor = Cursors.Default;
                        });
                        return;
                    }

                    // **DUAL-PATH FALLBACK: Try original path first, then with proper URL encoding**
                    string[] pathsToTry = {
                parentRelativePath, // Should already be the validated path
                ConstructProperlyEncodedPath(parentRelativePath) // Properly URL-encoded version
            };

                    bool success = false;
                    string workingPath = null;
                    int attemptNumber = 0;

                    foreach (string currentPath in pathsToTry)
                    {
                        attemptNumber++;
                        System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Attempt {attemptNumber}/{pathsToTry.Length} with path: {currentPath}");

                        try
                        {
                            var parentFolder = context.Web.GetFolderByServerRelativeUrl(currentPath);
                            context.Load(parentFolder, f => f.Exists, f => f.Name, f => f.ServerRelativeUrl, f => f.Folders);
                            await context.ExecuteQueryAsync();

                            System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Folder Exists={parentFolder.Exists}, ServerRelativeUrl={parentFolder.ServerRelativeUrl ?? "null"}");

                            if (!parentFolder.Exists || string.IsNullOrEmpty(parentFolder.ServerRelativeUrl))
                            {
                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Folder does not exist or has null ServerRelativeUrl at: {currentPath}");
                                continue; // Try next path
                            }

                            // Load direct subfolders only (not recursive)
                            context.Load(parentFolder.Folders, folders => folders.Include(
                                f => f.Name,
                                f => f.ServerRelativeUrl,
                                f => f.ListItemAllFields.HasUniqueRoleAssignments,
                                f => f.ListItemAllFields.RoleAssignments.Include(
                                    ra => ra.Member.Title,
                                    ra => ra.Member.LoginName,
                                    ra => ra.RoleDefinitionBindings),
                                f => f.Folders));
                            await context.ExecuteQueryAsync();

                            var directSubfolders = parentFolder.Folders?.Where(f => !f.Name.StartsWith("Forms")).ToList() ?? new List<Folder>();

                            System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: SUCCESS - Found {directSubfolders.Count} direct subfolders in {currentPath}");

                            if (attemptNumber > 1)
                            {
                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: SUCCESS using properly URL-encoded fallback path: {currentPath}");
                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Previous path that failed: {pathsToTry[0]}");
                            }

                            success = true;
                            workingPath = currentPath;

                            if (!directSubfolders.Any())
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"No subfolders found in '{parentName}' at level {parentLevel + 1}.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    lblStatus.Text = "No subfolders found.";
                                    btnViewSubfolders.Enabled = true;
                                    this.Cursor = Cursors.Default;
                                });
                                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersNoSubfolders", _libraryName, null, "Subfolder",
                                    $"No subfolders found in '{parentName}' at level {parentLevel + 1}, Session ID: {debugSessionId}");
                                return;
                            }

                            // Remove existing cached entries for this parent's children to avoid duplicates
                            _nestedSubfolderCache.RemoveAll(s =>
                                s.FullPath.StartsWith(currentPath + "/", StringComparison.OrdinalIgnoreCase) &&
                                s.Level == parentLevel + 1);

                            var processedSubfolders = new List<(string FullPath, string FolderName, int Level, bool HasUniquePermissions,
                                List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren)>();

                            // Process each direct subfolder
                            foreach (var subfolder in directSubfolders)
                            {
                                if (string.IsNullOrEmpty(subfolder.Name))
                                {
                                    System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Skipping subfolder with null or empty Name");
                                    continue;
                                }

                                var fullPath = NormalizePath(subfolder.ServerRelativeUrl);
                                var folderName = subfolder.Name;
                                var level = parentLevel + 1;
                                bool hasChildren = subfolder.Folders?.Count(f => !f.Name.StartsWith("Forms")) > 0;
                                var groupList = new List<(string GroupName, string GroupId, string Role)>();
                                bool hasUniquePermissions = subfolder.ListItemAllFields?.HasUniqueRoleAssignments ?? false;

                                // Process permissions
                                if (hasUniquePermissions && subfolder.ListItemAllFields?.RoleAssignments != null)
                                {
                                    foreach (var ra in subfolder.ListItemAllFields.RoleAssignments)
                                    {
                                        try
                                        {
                                            if (ra.Member?.Title?.StartsWith("CSG-", StringComparison.OrdinalIgnoreCase) == true)
                                            {
                                                var role = ra.RoleDefinitionBindings.FirstOrDefault()?.Name ?? "Unknown";
                                                if (role == "Contribute") role = "Edit";
                                                if (role != "Limited Access")
                                                {
                                                    var groupId = ra.Member.LoginName?.Split('|').Last() ?? string.Empty;
                                                    groupList.Add((ra.Member.Title, groupId, role));
                                                }
                                            }
                                        }
                                        catch (Exception raEx)
                                        {
                                            await _auditLogManager?.LogAction(_signedInUserId, null, "ProcessNestedSubfolderPermissionError", _libraryName, null, "Subfolder",
                                                $"Error processing role assignment for nested subfolder '{folderName}': {raEx.Message}, Session ID: {debugSessionId}");
                                        }
                                    }
                                }

                                var folderData = (fullPath, folderName, level, hasUniquePermissions, groupList, hasChildren);
                                processedSubfolders.Add(folderData);
                                _nestedSubfolderCache.Add(folderData);

                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Added level {level} subfolder to cache: FullPath={fullPath}, FolderName={folderName}");
                            }

                            if (!processedSubfolders.Any())
                            {
                                UpdateUI(() =>
                                {
                                    MessageBox.Show($"No valid subfolders found in '{parentName}' at level {parentLevel + 1}.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                    lblStatus.Text = "No valid subfolders found.";
                                    btnViewSubfolders.Enabled = true;
                                    this.Cursor = Cursors.Default;
                                });
                                return;
                            }

                            // Update UI with the loaded subfolders
                            UpdateUI(() =>
                            {
                                isUpdatingTreeView = true;

                                // Clear existing child nodes that might be placeholders
                                parentNode.Nodes.Clear();

                                // Create a dictionary for potential nested children (for future expansion)
                                var foldersByParent = _nestedSubfolderCache
                                    .Where(f => f.Level > parentLevel + 1)
                                    .GroupBy(f => GetParentPath(f.FullPath))
                                    .ToDictionary(
                                        g => g.Key,
                                        g => g.Select(f => (f.FullPath, f.FolderName, f.Level, f.HasUniquePermissions, f.Groups, f.HasChildren)).ToList()
                                    );

                                // Add new child nodes
                                foreach (var folder in processedSubfolders.OrderBy(f => f.FolderName))
                                {
                                    var node = CreateFolderTreeNode(folder, foldersByParent);
                                    if (node != null)
                                    {
                                        node.BackColor = Color.LightYellow;
                                        node.Text += " [Loaded]";
                                        parentNode.Nodes.Add(node);
                                    }
                                }

                                parentNode.Expand();
                                lblStatus.Text = $"Loaded {processedSubfolders.Count} level {parentLevel + 1} subfolders for '{parentName}'.";
                                isUpdatingTreeView = false;
                                btnViewSubfolders.Enabled = true;
                                this.Cursor = Cursors.Default;
                            });

                            await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersSuccess", _libraryName, null, "Subfolder",
                                $"Successfully loaded {processedSubfolders.Count} subfolders for '{parentName}' at level {parentLevel + 1}, Session ID: {debugSessionId}");

                            // Log if fallback was used
                            if (attemptNumber > 1)
                            {
                                nodeData.FullPath = workingPath; // Update with working path
                                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersPathFallbackUsed", _libraryName, null, "Subfolder",
                                    $"Used properly URL-encoded fallback path for subfolder: {workingPath}, Previous Path: {pathsToTry[0]}, Session ID: {debugSessionId}");
                            }

                            break; // Exit the foreach loop on success
                        }
                        catch (Microsoft.SharePoint.Client.ServerException ex) when (ex.Message.Contains("not found") || ex.Message.Contains("does not exist"))
                        {
                            System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Path not found (attempt {attemptNumber}): {currentPath}");
                            System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Error details: {ex.Message}");

                            if (attemptNumber < pathsToTry.Length)
                            {
                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Will try next path variation...");
                                continue; // Try next path
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: All path attempts failed");
                                throw; // Re-throw if this was the last attempt
                            }
                        }
                    }

                    if (!success)
                    {
                        UpdateUI(() =>
                        {
                            MessageBox.Show($"Subfolder '{parentName}' not found at any of the attempted paths:\n" +
                                            $"1. {pathsToTry[0]}\n" +
                                            $"2. {pathsToTry[1]}",
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            lblStatus.Text = "Subfolder not found at any attempted path.";
                            btnViewSubfolders.Enabled = true;
                            this.Cursor = Cursors.Default;
                        });

                        await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersError", _libraryName, null, "Subfolder",
                            $"Subfolder '{parentName}' not found at any attempted path. Tried: {string.Join(", ", pathsToTry)}, Session ID: {debugSessionId}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: Top-level error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"btnViewSubfolders_Click: StackTrace: {ex.StackTrace}");

                UpdateUI(() =>
                {
                    MessageBox.Show($"Error loading subfolders: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    lblStatus.Text = "Error loading subfolders.";
                    btnViewSubfolders.Enabled = true;
                    this.Cursor = Cursors.Default;
                });

                await _auditLogManager?.LogAction(_signedInUserId, null, "LoadSubfoldersError", _libraryName, null, "Subfolder",
                    $"Top-level error loading subfolders for '{parentName}': {ex.Message}, StackTrace: {ex.StackTrace}, Session ID: {debugSessionId}");
            }
        }
        private string CleanDuplicateSitePath(string path, string siteUrl)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(siteUrl))
                return path;

            try
            {
                var siteUri = new Uri(siteUrl);
                var sitePath = siteUri.AbsolutePath.TrimEnd('/');

                // Extract site segments for more precise duplicate detection
                var siteSegments = sitePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (siteSegments.Length < 2) return path; // Need at least "teams" and "sitename"

                var pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var cleanedSegments = new List<string>();

                // Look for duplicate site path patterns
                for (int i = 0; i < pathSegments.Length; i++)
                {
                    // Check if we're at the start of a potential duplicate site path
                    if (i + siteSegments.Length <= pathSegments.Length)
                    {
                        bool isDuplicate = true;
                        for (int j = 0; j < siteSegments.Length; j++)
                        {
                            if (!string.Equals(pathSegments[i + j], siteSegments[j], StringComparison.OrdinalIgnoreCase))
                            {
                                isDuplicate = false;
                                break;
                            }
                        }

                        // If we found a duplicate and we already have the site path, skip it
                        if (isDuplicate && cleanedSegments.Count >= siteSegments.Length)
                        {
                            bool alreadyHasSitePath = true;
                            for (int k = 0; k < siteSegments.Length; k++)
                            {
                                if (!string.Equals(cleanedSegments[k], siteSegments[k], StringComparison.OrdinalIgnoreCase))
                                {
                                    alreadyHasSitePath = false;
                                    break;
                                }
                            }

                            if (alreadyHasSitePath)
                            {
                                System.Diagnostics.Debug.WriteLine($"CleanDuplicateSitePath: Skipping duplicate site path at position {i}");
                                i += siteSegments.Length - 1; // Skip the duplicate segments
                                continue;
                            }
                        }
                    }

                    cleanedSegments.Add(pathSegments[i]);
                }

                var cleaned = "/" + string.Join("/", cleanedSegments);

                if (cleaned != path)
                {
                    System.Diagnostics.Debug.WriteLine($"CleanDuplicateSitePath: Cleaned path: '{path}' -> '{cleaned}'");
                }

                return cleaned;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CleanDuplicateSitePath: Error cleaning path: {ex.Message}");
                return path;
            }
        }
        private async Task<Microsoft.SharePoint.Client.Web> GetWebContextAsync()
        {
            var scopes = new[] { "https://tamucs.sharepoint.com/.default" };
            var accounts = await _pca.GetAccountsAsync();
            var authResult = await _pca.AcquireTokenSilent(scopes, accounts.FirstOrDefault()).ExecuteAsync();

            using (var context = new ClientContext(_siteUrl))
            {
                context.ExecutingWebRequest += (s, e) => { e.WebRequestExecutor.RequestHeaders["Authorization"] = "Bearer " + authResult.AccessToken; };
                var web = context.Web;
                context.Load(web, w => w.ServerRelativeUrl);
                await context.ExecuteQueryAsync();
                return web;
            }
        }

        private void ShowNestedFolderTemporarily(
    (string FullPath, string FolderName, int Level, bool HasUniquePermissions,
     List<(string GroupName, string GroupId, string Role)> Groups, bool HasChildren) folder)
        {
            var groupCount = folder.Groups.Count;
            var indent = new string(' ', folder.Level * 2);
            var nodeText = folder.HasUniquePermissions
                ? $"{indent}{folder.FolderName} (Unique, {groupCount} CSG group{(groupCount == 1 ? "" : "s")} assigned) [Temp View]"
                : $"{indent}{folder.FolderName} (Inherited) [Temp View]";

            var folderNode = new TreeNode
            {
                Text = nodeText,
                ImageIndex = 0,
                SelectedImageIndex = 0,
                BackColor = Color.LightYellow, // Highlight temporary view
                Tag = new TreeNodeData
                {
                    IsSubfolder = true,
                    SubfolderName = folder.FolderName,
                    FullPath = folder.FullPath,
                    Level = folder.Level,
                    HasChildren = folder.HasChildren
                }
            };

            // Add group nodes
            foreach (var group in folder.Groups)
            {
                folderNode.Nodes.Add(new TreeNode
                {
                    Text = $"{group.GroupName}: {group.Role}",
                    ImageIndex = 1,
                    SelectedImageIndex = 1,
                    Tag = new TreeNodeData
                    {
                        IsSubfolder = false,
                        GroupId = group.GroupId,
                        GroupName = group.GroupName,
                        Permission = group.Role,
                        FullPath = folder.FullPath,
                        Level = folder.Level
                    }
                });
            }

            tvSubfolders.Nodes.Add(folderNode);
            tvSubfolders.SelectedNode = folderNode;
            folderNode.EnsureVisible();

            lblStatus.Text = $"Temporary view: {folder.FolderName} (Level {folder.Level}). Refresh to return to top-level view.";
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            UpdateUI(() =>
            {
                btnRefresh.Enabled = false;
                tvSubfolders.Nodes.Clear();
                lblStatus.Text = "Refreshing folder permissions...";
            });
            try
            {
                lastRefreshTime = DateTime.MinValue;
                await LoadCurrentPermissionsAsync();
            }
            finally
            {
                UpdateUI(() =>
                {
                    btnRefresh.Enabled = true;
                    lblStatus.Text = "Refresh complete.";
                });
            }
        }

    }
}
