using System;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using WorkstationAuditor.Models;

namespace WorkstationAuditor.UI
{
    public class MainForm : Form
    {
        private Label lblTitle = new Label();
        private Label lblScore = new Label();
        private Button btnRefresh = new Button();
        private Button btnRunAudit = new Button();
        private TextBox txtWarnings = new TextBox();
        private Label lblLastRun = new Label();

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
            lblScore.Top = 50;
            lblScore.Left = 10;
            lblScore.Width = 200;
            lblScore.Font = new System.Drawing.Font(lblScore.Font.FontFamily, 24);

            btnRefresh.Text = "Refresh";
            btnRefresh.Top = 100;
            btnRefresh.Left = 10;
            btnRefresh.Size = new System.Drawing.Size(100, 36);
            btnRefresh.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnRefresh.Click += BtnRefresh_Click;

            btnRunAudit.Text = "Run Audit";
            btnRunAudit.Top = 100;
            btnRunAudit.Left = 120;
            btnRunAudit.Size = new System.Drawing.Size(100, 36);
            btnRunAudit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnRunAudit.Click += BtnRunAudit_Click;

            lblLastRun.Text = "Last run: -";
            lblLastRun.Top = 100;
            lblLastRun.Left = 240;
            lblLastRun.AutoSize = true;

            txtWarnings.Top = 150;
            txtWarnings.Left = 10;
            txtWarnings.Width = 660;
            txtWarnings.Height = 280;
            txtWarnings.Multiline = true;
            txtWarnings.ScrollBars = ScrollBars.Vertical;
            txtWarnings.ReadOnly = true;
            txtWarnings.Font = new System.Drawing.Font("Consolas", 10);


            Controls.Add(lblTitle);
            Controls.Add(lblScore);
            Controls.Add(btnRefresh);
            Controls.Add(btnRunAudit);
            Controls.Add(lblLastRun);
            Controls.Add(txtWarnings);

            Load += MainForm_Load;
        }

        private void MainForm_Load(object? sender, EventArgs e)
        {
            RefreshReport();
        }

        private void BtnRunAudit_Click(object? sender, EventArgs e)
        {
            var psPath = FindFileInParents("AuditCollector.ps1");
            if (psPath == null)
            {
                MessageBox.Show("Cannot find AuditCollector.ps1 in parent folders.");
                return;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{psPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(psPath) ?? AppDomain.CurrentDomain.BaseDirectory
            };
            try
            {
                WriteLog($"Starting audit: {psPath}");
                using var p = System.Diagnostics.Process.Start(psi);
                if (p != null)
                {
                    p.WaitForExit(120000);
                    WriteLog($"Audit finished with exit code {p.ExitCode}");
                }
                else
                {
                    WriteLog("Failed to start audit process (null)");
                }
                RefreshReport();
                MessageBox.Show("Audit finished.");
            }
            catch (Exception ex)
            {
                WriteLog("Failed to run audit: " + ex.ToString());
                MessageBox.Show("Failed to run audit: " + ex.Message);
            }
        }

        private void BtnRefresh_Click(object? sender, EventArgs e)
        {
            RefreshReport();
        }

        private void RefreshReport()
        {
            var reportPath = FindFileInParents(Path.Combine("Reports", "report.json"));
            if (reportPath == null)
            {
                lblScore.Text = "Score: -";
                txtWarnings.Text = "No report found. Run Audit to generate one.";
                return;
            }
            try
            {
                var txt = File.ReadAllText(reportPath);
                using var doc = JsonDocument.Parse(txt);
                if (!doc.RootElement.TryGetProperty("Analysis", out var analysis))
                {
                    lblScore.Text = "Score: -";
                    txtWarnings.Text = "Report missing Analysis section.";
                    return;
                }

                var score = analysis.GetProperty("HealthScore").GetInt32();
                lblScore.Text = $"Score: {score}/100";
                if (doc.RootElement.TryGetProperty("CollectedAt", out var collectedAt))
                {
                    lblLastRun.Text = "Last run: " + collectedAt.GetDateTime().ToLocalTime().ToString("g");
                }

                txtWarnings.Text = "";
                if (analysis.TryGetProperty("Warnings", out var warns))
                {
                    foreach (var w in warns.EnumerateArray())
                    {
                        var sev = w.GetProperty("Severity").GetString();
                        var msg = w.GetProperty("Message").GetString();
                        txtWarnings.AppendText($"[{sev}] {msg}{Environment.NewLine}");
                    }
                }
                if (analysis.TryGetProperty("Recommendations", out var recs))
                {
                    txtWarnings.AppendText(Environment.NewLine + "Recommendations:" + Environment.NewLine);
                    foreach (var r in recs.EnumerateArray()) txtWarnings.AppendText(" - " + r.GetString() + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                txtWarnings.Text = "Failed to load report: " + ex.Message + "\r\n" + ex.ToString();
                WriteLog("Failed to load report: " + ex.ToString());
            }
        }

        private string? FindFileInParents(string relativePath)
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
            return null;
        }

        private void WriteLog(string message)
        {
            try
            {
                var reportsDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Reports"));
                if (!Directory.Exists(reportsDir)) Directory.CreateDirectory(reportsDir);
                var logPath = Path.Combine(reportsDir, "ui.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch { }
        }
    }
}
