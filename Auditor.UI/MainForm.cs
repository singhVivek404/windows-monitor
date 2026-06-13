using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using WorkstationAuditor.Models;

namespace WorkstationAuditor.UI
{
    public class MainForm : Form
    {
        private Label lblTitle = new Label();
        private Label lblScore = new Label();
        private Label lblMachine = new Label();
        private Button btnRefresh = new Button();
        private Button btnRunAudit = new Button();
        private Button btnSetup = new Button();
        private Button btnQuickFix = new Button();
        private Button btnToggleLogs = new Button();
        private TextBox txtWarnings = new TextBox();
        private Label lblLastRun = new Label();
        private Label lblStatus = new Label();
        private RichTextBox txtLog = new RichTextBox();
        private ProgressBar progressBar = new ProgressBar();
        private FlowLayoutPanel controlsPanel = new FlowLayoutPanel();
        private FlowLayoutPanel recommendationsPanel;
        private DataGridView dgvProcesses;
        private FileSystemWatcher? reportWatcher;

        public MainForm()
        {
            Text = "Developer Workstation Auditor";
            Width = 700;
            Height = 500;

            lblTitle.Text = "Developer Workstation Auditor";
            lblTitle.AutoSize = true;
            lblTitle.Top = 10;
            lblTitle.Left = 10;
            lblTitle.Font = new System.Drawing.Font(lblTitle.Font.FontFamily, 14);

            lblScore.Text = "Score: -";
            lblScore.AutoSize = false;
            lblScore.Width = 260;
            lblScore.Height = 80;
            lblScore.Font = new System.Drawing.Font(lblScore.Font.FontFamily, 36);
            lblScore.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            lblScore.Dock = DockStyle.Fill;
            lblScore.BackColor = System.Drawing.Color.FromArgb(255, 250, 240);
            lblScore.BorderStyle = BorderStyle.FixedSingle;
            lblScore.Padding = new Padding(6);

            lblMachine.Text = "";
            lblMachine.AutoSize = true;
            lblMachine.Top = 40;
            lblMachine.Left = 10;
            lblMachine.Font = new System.Drawing.Font(lblMachine.Font.FontFamily, 10);
            lblMachine.ForeColor = System.Drawing.Color.DimGray;

            btnRefresh.Text = "Refresh";
            btnRefresh.Size = new System.Drawing.Size(100, 36);
            btnRefresh.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnRefresh.Click += BtnRefresh_Click;

            btnRunAudit.Text = "Run Audit";
            btnRunAudit.Size = new System.Drawing.Size(100, 36);
            btnRunAudit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnRunAudit.Click += BtnRunAudit_Click;

            btnSetup.Text = "Setup";
            btnSetup.Size = new System.Drawing.Size(100, 36);
            btnSetup.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnSetup.Click += BtnSetup_Click;

            lblLastRun.Text = "Last run: -";
            lblLastRun.Top = 100;
            lblLastRun.Left = 240;
            lblLastRun.AutoSize = true;

            txtWarnings.Top = 10;
            txtWarnings.Left = 10;
            txtWarnings.Width = 640;
            txtWarnings.Height = 240;
            txtWarnings.Multiline = true;
            txtWarnings.ScrollBars = ScrollBars.Vertical;
            txtWarnings.ReadOnly = true;
            txtWarnings.Font = new System.Drawing.Font("Consolas", 10);

            txtLog.Top = 0;
            txtLog.Left = 0;
            txtLog.Width = 660;
            txtLog.Height = 150;
            txtLog.ReadOnly = true;
            txtLog.Font = new System.Drawing.Font("Consolas", 10);
            txtLog.BackColor = System.Drawing.Color.Black;
            txtLog.ForeColor = System.Drawing.Color.LightGreen;

            progressBar.Width = 660;
            progressBar.Height = 18;
            progressBar.Style = ProgressBarStyle.Blocks;


            // Top layout: left side (title + machine), right side (score)
            var topPanel = new TableLayoutPanel();
            topPanel.Dock = DockStyle.Top;
            topPanel.AutoSize = false;
            topPanel.Padding = new Padding(10);
            topPanel.Height = 120;
            topPanel.MinimumSize = new System.Drawing.Size(0, 120);
            topPanel.ColumnCount = 2;
            topPanel.RowCount = 1;
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            topPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));

            var leftPanel = new FlowLayoutPanel();
            leftPanel.FlowDirection = FlowDirection.TopDown;
            leftPanel.AutoSize = true;
            leftPanel.Controls.Add(lblTitle);
            leftPanel.Controls.Add(lblMachine);

            topPanel.Controls.Add(leftPanel, 0, 0);
            topPanel.Controls.Add(lblScore, 1, 0);

            controlsPanel.Dock = DockStyle.Top;
            controlsPanel.AutoSize = true;
            controlsPanel.Padding = new Padding(10);
            controlsPanel.FlowDirection = FlowDirection.LeftToRight;
            controlsPanel.Controls.Add(btnRefresh);
            controlsPanel.Controls.Add(btnRunAudit);
            controlsPanel.Controls.Add(btnSetup);
            controlsPanel.Controls.Add(btnQuickFix);
            controlsPanel.Controls.Add(btnToggleLogs);
            controlsPanel.Controls.Add(lblLastRun);
            controlsPanel.Controls.Add(lblStatus);
            controlsPanel.Controls.Add(progressBar);

            Controls.Add(topPanel);
            Controls.Add(controlsPanel);

            // Main split: warnings (top) and logs (bottom)
            var split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Horizontal;
            split.SplitterDistance = 260;

            // Build main panel in Panel1: warnings on top, processes + recommendations below
            var mainPanel = new Panel { Dock = DockStyle.Fill };
            txtWarnings.Dock = DockStyle.Top;
            txtWarnings.Height = 160;
            mainPanel.Controls.Add(txtWarnings);

            var lowerPanel = new Panel { Dock = DockStyle.Fill };

            dgvProcesses = new DataGridView();
            dgvProcesses.Dock = DockStyle.Fill;
            dgvProcesses.ReadOnly = true;
            dgvProcesses.AllowUserToAddRows = false;
            dgvProcesses.AllowUserToDeleteRows = false;
            dgvProcesses.RowHeadersVisible = false;
            dgvProcesses.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvProcesses.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dgvProcesses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Process" });
            dgvProcesses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid", HeaderText = "PID" });
            dgvProcesses.Columns.Add(new DataGridViewTextBoxColumn { Name = "Memory", HeaderText = "Memory (MB)" });
            dgvProcesses.Columns.Add(new DataGridViewButtonColumn { Name = "Kill", HeaderText = "Action", Text = "Kill", UseColumnTextForButtonValue = true });
            try
            {
                dgvProcesses.Columns["Name"].FillWeight = 60;
                dgvProcesses.Columns["Pid"].FillWeight = 10;
                dgvProcesses.Columns["Memory"].FillWeight = 20;
                dgvProcesses.Columns["Kill"].FillWeight = 10;
            }
            catch { }
            dgvProcesses.CellContentClick += DgvProcesses_CellContentClick;

            recommendationsPanel = new FlowLayoutPanel();
            recommendationsPanel.Dock = DockStyle.Right;
            recommendationsPanel.Width = 220;
            recommendationsPanel.FlowDirection = FlowDirection.TopDown;
            recommendationsPanel.AutoScroll = true;

            lowerPanel.Controls.Add(dgvProcesses);
            lowerPanel.Controls.Add(recommendationsPanel);

            mainPanel.Controls.Add(lowerPanel);
            split.Panel1.Controls.Add(mainPanel);

            txtLog.Dock = DockStyle.Fill;
            split.Panel2.Controls.Add(txtLog);
            // Hide logs by default (users prefer a clean dashboard)
            split.Panel2Collapsed = true;

            // Wire up the quick-fix and logs toggle (capture split via closure)
            btnToggleLogs.Text = "Show Logs";
            btnToggleLogs.Size = new System.Drawing.Size(100, 36);
            btnToggleLogs.Click += (s, e) => {
                split.Panel2Collapsed = !split.Panel2Collapsed;
                btnToggleLogs.Text = split.Panel2Collapsed ? "Show Logs" : "Hide Logs";
            };

            btnQuickFix.Text = "Quick Fix";
            btnQuickFix.Size = new System.Drawing.Size(100, 36);
            btnQuickFix.Click += (s, e) => {
                Task.Run(() => QuickFixActions());
            };
            Controls.Add(split);

            Load += MainForm_Load;
            Resize += MainForm_Resize;
            Shown += (s,e)=> PositionScoreLabel();

            // Setup a watcher to refresh UI when report.json is updated by analyzer
            try
            {
                var reportsDir = GetReportsDirectory();
                SetupReportWatcher(reportsDir);
            }
            catch { }
        }

        private string GetAppDataBase()
        {
            try
            {
                var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkstationAuditor");
                if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                return baseDir;
            }
            catch { return Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory); }
        }

        private string GetReportsDirectory()
        {
            // Prefer repo Reports when running from source tree, otherwise use AppData
            try
            {
                var psPath = FindFileInParents("AuditCollector.ps1");
                if (psPath != null)
                {
                    var repoRoot = FindRepositoryRoot();
                    var reportsDir = Path.Combine(repoRoot, "Reports");
                    if (!Directory.Exists(reportsDir)) Directory.CreateDirectory(reportsDir);
                    return reportsDir;
                }
            }
            catch { }
            var app = Path.Combine(GetAppDataBase(), "Reports");
            if (!Directory.Exists(app)) Directory.CreateDirectory(app);
            return app;
        }

        private string GetDataDirectory()
        {
            try
            {
                var psPath = FindFileInParents("AuditCollector.ps1");
                if (psPath != null)
                {
                    var repoRoot = FindRepositoryRoot();
                    var dataDir = Path.Combine(repoRoot, "Data");
                    if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                    return dataDir;
                }
            }
            catch { }
            var app = Path.Combine(GetAppDataBase(), "Data");
            if (!Directory.Exists(app)) Directory.CreateDirectory(app);
            return app;
        }

        private void SetupReportWatcher(string reportsDir)
        {
            try
            {
                reportWatcher = new FileSystemWatcher(reportsDir, "report.json");
                reportWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime;
                reportWatcher.Changed += (s, e) => { if (!IsDisposed) BeginInvoke(new Action(() => { AppendLog("Report changed, refreshing..."); RefreshReport(); })); };
                reportWatcher.Created += (s, e) => { if (!IsDisposed) BeginInvoke(new Action(() => { AppendLog("Report created, refreshing..."); RefreshReport(); })); };
                reportWatcher.Renamed += (s, e) => { if (!IsDisposed) BeginInvoke(new Action(() => { AppendLog("Report renamed, refreshing..."); RefreshReport(); })); };
                reportWatcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                AppendLog("Failed to initialize report watcher: " + ex.Message);
            }
        }

        private async void DgvProcesses_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0) return;
                var col = dgvProcesses.Columns[e.ColumnIndex];
                if (col.Name == "Kill")
                {
                    var pidCell = dgvProcesses.Rows[e.RowIndex].Cells["Pid"].Value;
                    if (pidCell == null) return;
                    if (!int.TryParse(pidCell.ToString(), out var pid)) return;
                    var procName = dgvProcesses.Rows[e.RowIndex].Cells["Name"].Value?.ToString() ?? pid.ToString();
                    var res = MessageBox.Show($"Kill process {procName} (PID {pid})?","Confirm",MessageBoxButtons.YesNo,MessageBoxIcon.Warning);
                    if (res == DialogResult.Yes)
                    {
                        await Task.Run(() => {
                            try { var p = Process.GetProcessById(pid); p.Kill(); AppendLog($"Killed process {pid} ({procName})"); } catch (Exception ex) { AppendLog($"Failed to kill {pid}: {ex.Message}"); }
                        });
                        await Task.Delay(500);
                        RefreshReport();
                    }
                }
            }
            catch (Exception ex) { AppendLog("Process grid click error: " + ex.Message); }
        }

        private void PerformRecommendationAction(string rec)
        {
            try
            {
                var r = (rec ?? string.Empty).ToLowerInvariant();
                if (r.Contains("disk"))
                {
                    AppendLog("Opening Disk Cleanup...");
                    try { Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true }); }
                    catch { Process.Start(new ProcessStartInfo("cmd", "/c start ms-settings:storage") { CreateNoWindow = true, UseShellExecute = true }); }
                }
                else if (r.Contains("ram") || r.Contains("close") || r.Contains("chrome"))
                {
                    AppendLog("Opening Task Manager...");
                    Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true });
                }
                else if (r.Contains("startup"))
                {
                    AppendLog("Opening Task Manager (Startup)...");
                    Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true });
                }
                else if (r.Contains("reboot"))
                {
                    var res = MessageBox.Show("Reboot now? Your work will be lost.", "Reboot", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res == DialogResult.Yes)
                    {
                        AppendLog("Rebooting system in 5 seconds...");
                        Process.Start(new ProcessStartInfo("shutdown", "/r /t 5") { CreateNoWindow = true, UseShellExecute = false });
                    }
                }
                else
                {
                    AppendLog("No automated action for recommendation: " + rec);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Action failed: " + ex.Message);
            }
        }

        private async void MainForm_Load(object? sender, EventArgs e)
        {
            RefreshReport();
            var reportPath = FindFileInParents(Path.Combine("Reports", "report.json"));
            if (reportPath == null)
            {
                lblStatus.Text = "No report found. Running initial setup...";
                await Task.Delay(250);
                await RunFullSetupAsync();
            }
        }

        private async void BtnRunAudit_Click(object? sender, EventArgs e)
        {
            await RunCollectorOnlyAsync();
        }

        private async Task RunCollectorOnlyAsync()
        {
            var psPath = FindFileInParents("AuditCollector.ps1");
            if (psPath == null)
            {
                AppendLog("AuditCollector.ps1 not found. Skipping collectors; run analyzer on existing Data if present.");
                // Try to refresh report from data in AppData or repo
                RefreshReport();
                return;
            }

            var shell = LocatePowerShellExecutable();
            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(psPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            DisableControlsForRun();
            lblStatus.Text = "Collecting data...";
            progressBar.Style = ProgressBarStyle.Marquee;
            try
            {
                var code = await RunProcessAsync(psi, "Collector", 180000);
                WriteLog($"Collector exit code: {code}");
                AppendLog($"Collector finished with code {code}");
                // After collectors complete, analyzer may write the report; RefreshReport will be triggered by watcher.
                RefreshReport();
            }
            catch (Exception ex)
            {
                WriteLog("Failed to run collector: " + ex.ToString());
                AppendLog("Failed to run collector: " + ex.Message);
            }
            finally
            {
                EnableControlsAfterRun();
                lblStatus.Text = "";
                progressBar.Style = ProgressBarStyle.Blocks;
            }
        }

        private async void BtnSetup_Click(object? sender, EventArgs e)
        {
            await RunFullSetupAsync();
        }

        private async Task RunFullSetupAsync()
        {
            DisableControlsForRun();
            lblStatus.Text = "Running setup...";
            progressBar.Style = ProgressBarStyle.Marquee;
            try
            {
                // 1) Collect
                var psPath = FindFileInParents("AuditCollector.ps1");
                if (psPath == null)
                {
                    AppendLog("Cannot find AuditCollector.ps1 in parent folders.");
                    return;
                }
                var shell = LocatePowerShellExecutable();
                var psiCollector = new ProcessStartInfo
                {
                    FileName = shell,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(psPath) ?? AppDomain.CurrentDomain.BaseDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                lblStatus.Text = "Collecting data...";
                AppendLog("Starting collectors...");
                var colCode = await RunProcessAsync(psiCollector, "Collector", 300000);
                AppendLog($"Collector finished with code {colCode}");

                // 2) Run analyzer in-process
                lblStatus.Text = "Running analyzer...";
                AppendLog("Starting analyzer in-process...");
                var psPresence = FindFileInParents("AuditCollector.ps1") != null;
                var dataDir = GetDataDirectory();
                var reportsDir = GetReportsDirectory();
                AppendLog($"Analyzer dataDir={dataDir} reportsDir={reportsDir} (collector present={psPresence})");
                var analyzerCode = await Task.Run(() => WorkstationAuditor.AuditorRunner.Run(dataDir, reportsDir, s => AppendLog("[Analyzer] " + s)));
                AppendLog($"Analyzer finished with code {analyzerCode}");

                RefreshReport();
                AppendLog("Setup complete.");
            }
            catch (Exception ex)
            {
                WriteLog("Setup failed: " + ex.ToString());
                AppendLog("Setup failed: " + ex.Message);
            }
            finally
            {
                EnableControlsAfterRun();
                lblStatus.Text = "";
                progressBar.Style = ProgressBarStyle.Blocks;
            }
        }

        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            RefreshReport();
        }

        private void RefreshReport()
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => RefreshReport()));
                return;
            }

            var reportPath = FindFileInParents(Path.Combine("Reports", "report.json"));
            if (reportPath == null)
            {
                lblScore.Text = "Score: -";
                txtWarnings.Text = "No report found. Run Audit to generate one.";
                AppendLog("Refresh: no report found");
                return;
            }

            try
            {
                AppendLog($"Reading report from: {reportPath}");

                // Try parsing with a few retries to avoid transient write/read races.
                JsonDocument doc = null!;
                var parsed = false;
                var attempts = 0;
                while (!parsed && attempts < 6)
                {
                    attempts++;
                    try
                    {
                        var content = File.ReadAllText(reportPath);
                        doc = JsonDocument.Parse(content);
                        parsed = true;
                        break;
                    }
                    catch (System.IO.IOException ioex)
                    {
                        AppendLog($"IO read attempt {attempts} failed: {ioex.Message}");
                        System.Threading.Thread.Sleep(120);
                    }
                    catch (System.Text.Json.JsonException jex)
                    {
                        AppendLog($"JSON parse attempt {attempts} failed: {jex.Message}");
                        System.Threading.Thread.Sleep(120);
                    }
                }

                if (!parsed)
                {
                    AppendLog("Failed to parse report.json after retries.");
                    txtWarnings.Text = "Failed to load report (parse error). Check Reports/ui.log for details.";
                    return;
                }

                if (!doc.RootElement.TryGetProperty("Analysis", out var analysis))
                {
                    lblScore.Text = "Score: -";
                    txtWarnings.Text = "Report missing Analysis section.";
                    AppendLog("Refresh: report missing Analysis section");
                    return;
                }

                var score = analysis.GetProperty("HealthScore").GetInt32();
                lblScore.Text = $"Score: {score}/100";
                lblScore.ForeColor = System.Drawing.Color.DarkBlue;
                PositionScoreLabel();
                lblScore.BringToFront();

                if (doc.RootElement.TryGetProperty("CollectedAt", out var collectedAt))
                {
                    lblLastRun.Text = "Last run: " + collectedAt.GetDateTime().ToLocalTime().ToString("g");
                }

                // show machine summary
                if (doc.RootElement.TryGetProperty("Machine", out var machine))
                {
                    try
                    {
                        var name = machine.GetProperty("ComputerName").GetString();
                        var os = machine.GetProperty("OSVersion").GetString();
                        lblMachine.Text = name + " — " + (os ?? "");
                    }
                    catch { lblMachine.Text = ""; }
                }

                AppendLog($"Refreshed report: score {score}");

                txtWarnings.Text = "";
                var hasWarnings = false;
                if (analysis.TryGetProperty("Warnings", out var warns))
                {
                    foreach (var w in warns.EnumerateArray())
                    {
                        hasWarnings = true;
                        var sev = w.GetProperty("Severity").GetString();
                        var msg = w.GetProperty("Message").GetString();
                        txtWarnings.AppendText($"[{sev}] {msg}{Environment.NewLine}");
                    }
                }
                if (analysis.TryGetProperty("Recommendations", out var recs))
                {
                    if (recs.ValueKind == System.Text.Json.JsonValueKind.Array && recs.EnumerateArray().Any())
                    {
                        txtWarnings.AppendText(Environment.NewLine + "Recommendations:" + Environment.NewLine);
                        foreach (var r in recs.EnumerateArray()) txtWarnings.AppendText(" - " + r.GetString() + Environment.NewLine);
                    }
                }

                if (!hasWarnings)
                {
                    txtWarnings.Text = "No warnings detected.";
                }

                txtWarnings.SelectionStart = 0;
                txtWarnings.ScrollToCaret();
                txtWarnings.BringToFront();
                txtWarnings.Invalidate();

                // Populate processes grid
                try
                {
                    dgvProcesses.Rows.Clear();
                    if (doc.RootElement.TryGetProperty("Processes", out var procs) && procs.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var list = procs.EnumerateArray()
                            .Select(p => {
                                string name = "";
                                int pid = 0;
                                double mem = 0.0;
                                if (p.TryGetProperty("Name", out var np) && np.ValueKind == System.Text.Json.JsonValueKind.String) name = np.GetString() ?? "";
                                else if (p.TryGetProperty("ProcessName", out var pnp) && pnp.ValueKind == System.Text.Json.JsonValueKind.String) name = pnp.GetString() ?? "";
                                if (p.TryGetProperty("Pid", out var pidp) && pidp.ValueKind == System.Text.Json.JsonValueKind.Number) pid = pidp.GetInt32();
                                else if (p.TryGetProperty("Id", out var idp) && idp.ValueKind == System.Text.Json.JsonValueKind.Number) pid = idp.GetInt32();
                                if (p.TryGetProperty("MemoryMb", out var mp) && mp.ValueKind == System.Text.Json.JsonValueKind.Number) mem = mp.GetDouble();
                                else if (p.TryGetProperty("Memory", out var mp2) && mp2.ValueKind == System.Text.Json.JsonValueKind.Number) mem = mp2.GetDouble();
                                return new { Name = name, Pid = pid, Mem = mem };
                            })
                            .OrderByDescending(x => x.Mem)
                            .Take(25)
                            .ToList();
                        foreach (var pr in list)
                        {
                            dgvProcesses.Rows.Add(pr.Name, pr.Pid, pr.Mem.ToString("F1"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Failed to populate processes: " + ex.Message);
                }

                // Populate recommendations UI as actionable buttons
                try
                {
                    recommendationsPanel.Controls.Clear();
                    if (analysis.TryGetProperty("Recommendations", out var recs2) && recs2.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var r in recs2.EnumerateArray())
                        {
                            var text = r.GetString() ?? "";
                            var p = new Panel();
                            p.Width = Math.Max(200, recommendationsPanel.Width - 25);
                            p.Height = 60;
                            p.BorderStyle = BorderStyle.FixedSingle;
                            var lbl = new Label();
                            lbl.Text = text;
                            lbl.Left = 6;
                            lbl.Top = 6;
                            lbl.Width = p.Width - 110;
                            lbl.Height = 40;
                            lbl.AutoEllipsis = true;
                            var btn = new Button();
                            btn.Text = "Action";
                            btn.Width = 90;
                            btn.Height = 30;
                            btn.Left = p.Width - 95;
                            btn.Top = 14;
                            btn.Click += (s, e) => PerformRecommendationAction(text);
                            p.Controls.Add(lbl);
                            p.Controls.Add(btn);
                            recommendationsPanel.Controls.Add(p);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Failed to build recommendations UI: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                txtWarnings.Text = "Failed to load report: " + ex.Message + "\r\n" + ex.ToString();
                WriteLog("Failed to load report: " + ex.ToString());
                AppendLog("Refresh failed: " + ex.Message);
            }
        }

        private void PositionScoreLabel()
        {
            try
            {
                lblScore.BringToFront();
                lblLastRun.BringToFront();
            }
            catch { }
        }

        private void QuickFixActions()
        {
            try
            {
                AppendLog("Quick Fix: opening Disk Cleanup and Task Manager...");
                try { Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true }); }
                catch { try { Process.Start(new ProcessStartInfo("cmd", "/c start ms-settings:storage") { CreateNoWindow = true, UseShellExecute = true }); } catch { } }
                try { Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true }); } catch { }
            }
            catch (Exception ex)
            {
                AppendLog("QuickFix failed: " + ex.Message);
            }
        }

        private void MainForm_Resize(object? sender, EventArgs e)
        {
            PositionScoreLabel();
        }

        private string? FindFileInParents(string relativePath)
        {
                lblScore.BringToFront();
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
            return null;
        }

        private string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "readme.md")) || Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private string? FindBuiltAuditorDll(string repoRoot)
        {
            try
            {
                var binDir = Path.Combine(repoRoot, "Auditor", "bin");
                if (Directory.Exists(binDir))
                {
                    var found = Directory.EnumerateFiles(binDir, "Auditor.dll", SearchOption.AllDirectories).FirstOrDefault();
                    if (found != null) return found;
                }
            }
            catch { }
            return null;
        }

        private string LocatePowerShellExecutable()
        {
            try
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var candidate = Path.Combine(pf, "PowerShell", "7", "pwsh.exe");
                if (File.Exists(candidate)) return candidate;
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (!string.IsNullOrEmpty(pf86))
                {
                    candidate = Path.Combine(pf86, "PowerShell", "7", "pwsh.exe");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch { }
            return "powershell";
        }

        private void DisableControlsForRun()
        {
            btnRefresh.Enabled = false;
            btnRunAudit.Enabled = false;
            btnSetup.Enabled = false;
        }

        private void EnableControlsAfterRun()
        {
            btnRefresh.Enabled = true;
            btnRunAudit.Enabled = true;
            btnSetup.Enabled = true;
        }

        private async Task<int> RunProcessAsync(ProcessStartInfo psi, string tag, int timeoutMs = 120000)
        {
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            using var proc = new Process();
            proc.StartInfo = psi;
            var output = new StringBuilder();
            proc.OutputDataReceived += (s, e) => { if (e.Data != null) { output.AppendLine(e.Data); WriteLog($"[{tag}] {e.Data}"); AppendLog($"[{tag}] {e.Data}"); } };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) { output.AppendLine(e.Data); WriteLog($"[{tag} ERR] {e.Data}"); AppendLog($"[{tag} ERR] {e.Data}"); } };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            var exited = await Task.Run(() => proc.WaitForExit(timeoutMs));
            if (!exited)
            {
                try { proc.Kill(true); } catch { }
                WriteLog($"[{tag}] timed out after {timeoutMs}ms");
                return -1;
            }
            return proc.ExitCode;
        }

        private void WriteLog(string message)
        {
            try
            {
                var reportsDir = GetReportsDirectory();
                var logPath = Path.Combine(reportsDir, "ui.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        private void AppendLog(string message)
        {
            try
            {
                WriteLog(message);
                var line = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
                if (txtLog.InvokeRequired)
                {
                    txtLog.BeginInvoke(new Action(() => txtLog.AppendText(line)));
                }
                else
                {
                    txtLog.AppendText(line);
                }
            }
            catch { }
        }
    }
}
