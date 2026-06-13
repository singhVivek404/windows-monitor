using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using WorkstationAuditor.Models;

namespace WorkstationAuditor.UI
{
    public class MainForm : Form
    {
        // ═══════════════════════════════════════════════════════════════════
        // COLOR PALETTE  (Catppuccin Mocha-inspired dark theme)
        // ═══════════════════════════════════════════════════════════════════
        private static readonly Color BgDark      = Color.FromArgb(20, 22, 40);
        private static readonly Color BgPanel     = Color.FromArgb(35, 38, 60);
        private static readonly Color BgCard      = Color.FromArgb(50, 54, 80);
        private static readonly Color Accent      = Color.FromArgb(100, 160, 245);
        private static readonly Color TxtPrimary  = Color.FromArgb(210, 218, 244);
        private static readonly Color TxtMuted    = Color.FromArgb(140, 150, 190);
        private static readonly Color ColorGood   = Color.FromArgb(140, 220, 140);
        private static readonly Color ColorWarn   = Color.FromArgb(245, 200, 90);
        private static readonly Color ColorBad    = Color.FromArgb(240, 110, 110);
        private static readonly Color BtnBg       = Color.FromArgb(55, 60, 95);

        // ═══════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════
        private int              _score      = -1;
        private List<DiskInfo>   _disks      = new();
        private bool             _logVisible;
        private bool             _isRunning;
        private FileSystemWatcher? _watcher;

        // ═══════════════════════════════════════════════════════════════════
        // CONTROLS
        // ═══════════════════════════════════════════════════════════════════
        private readonly Panel          pnlTop        = new();
        private readonly Panel          pnlScore      = new();
        private readonly Label          lblTitle      = new();
        private readonly Label          lblMachine    = new();
        private readonly Label          lblLastRun    = new();
        private readonly TabControl     tabMain       = new();
        private readonly TabPage        tabOverview   = new("  Overview  ");
        private readonly TabPage        tabProcesses  = new("  Processes  ");
        private readonly TabPage        tabStartup    = new("  Startup  ");
        private readonly TabPage        tabDisk       = new("  Disk  ");
        private readonly TabPage        tabNetwork    = new("  Network  ");
        private readonly TabPage        tabDevEnv     = new("  Dev Env  ");
        private readonly RichTextBox    rtbWarnings   = new();
        private readonly FlowLayoutPanel flpRecs      = new();
        private readonly DataGridView   dgvProcs      = new();
        private readonly DataGridView   dgvStartup    = new();
        private readonly Panel          pnlDiskViz    = new();
        private readonly DataGridView   dgvNetwork    = new();
        private readonly FlowLayoutPanel pnlDevCards  = new();
        private readonly FlowLayoutPanel pnlButtons   = new();
        private readonly Button         btnRunAudit   = new();
        private readonly Button         btnRefresh    = new();
        private readonly Button         btnQuickFix   = new();
        private readonly Button         btnToggleLogs = new();
        private readonly Label          lblStatus     = new();
        private readonly ProgressBar    progressBar   = new();
        private readonly RichTextBox    rtbLog        = new();
        private readonly Panel          pnlLog        = new();

        // ═══════════════════════════════════════════════════════════════════
        // CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════
        public MainForm()
        {
            SuspendLayout();
            EnsureCollectorsExtracted();

            Text          = "Developer Workstation Auditor";
            Size          = new Size(980, 700);
            MinimumSize   = new Size(920, 640);
            BackColor     = BgDark;
            ForeColor     = TxtPrimary;
            Font          = new Font("Segoe UI", 9.5f);
            StartPosition = FormStartPosition.CenterScreen;
            WindowState   = FormWindowState.Maximized;

            BuildTopPanel();
            BuildTabControl();
            BuildButtonBar();
            BuildLogPanel();

            // Docking order matters: last added → outermost dock priority
            Controls.Add(tabMain);       // Fill
            Controls.Add(pnlButtons);   // Bottom (inner)
            Controls.Add(pnlTop);       // Top
            Controls.Add(pnlLog);       // Bottom (outer)

            ResumeLayout(false);
            PerformLayout();

            Load        += OnLoad;
            FormClosing += OnFormClosing;
            TrySetupFileWatcher();
        }

        // ═══════════════════════════════════════════════════════════════════
        // LAYOUT BUILDERS
        // ═══════════════════════════════════════════════════════════════════

        private void BuildTopPanel()
        {
            pnlTop.Dock      = DockStyle.Top;
            pnlTop.Height    = 112;
            pnlTop.BackColor = BgPanel;
            pnlTop.Paint    += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(55, 65, 110), 1);
                e.Graphics.DrawLine(pen, 0, pnlTop.Height - 1, pnlTop.Width, pnlTop.Height - 1);
            };

            // Two-column TableLayoutPanel keeps score badge in its own fixed cell,
            // so it can NEVER overlap the text labels regardless of window width.
            var tbl = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = Color.Transparent,
                Padding     = new Padding(16, 8, 12, 8)
            };
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));  // left: fills remaining
            tbl.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 148f)); // right: fixed score badge
            tbl.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Left cell: stacked labels using TableLayoutPanel to prevent overlap ──
            var pnlLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            
            var tblLeft = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                BackColor   = Color.Transparent,
                Padding     = new Padding(0, 4, 0, 4)
            };
            tblLeft.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            tblLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tblLeft.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            lblTitle.Text        = "Developer Workstation Auditor";
            lblTitle.Font        = new Font("Segoe UI", 16, FontStyle.Bold);
            lblTitle.ForeColor   = TxtPrimary;
            lblTitle.AutoSize    = true;
            lblTitle.Dock        = DockStyle.Fill;
            lblTitle.Margin      = new Padding(0, 0, 0, 4);

            lblMachine.Text        = "Initializing…";
            lblMachine.Font        = new Font("Segoe UI", 9f);
            lblMachine.ForeColor   = TxtMuted;
            lblMachine.AutoSize    = true;
            lblMachine.Dock        = DockStyle.Fill;
            lblMachine.Margin      = new Padding(0, 0, 0, 4);

            lblLastRun.Text      = "";
            lblLastRun.Font      = new Font("Segoe UI", 8.5f);
            lblLastRun.ForeColor = TxtMuted;
            lblLastRun.AutoSize  = true;
            lblLastRun.Dock        = DockStyle.Fill;
            lblLastRun.Margin      = new Padding(0, 0, 0, 0);

            tblLeft.Controls.Add(lblTitle,   0, 0);
            tblLeft.Controls.Add(lblMachine, 0, 1);
            tblLeft.Controls.Add(lblLastRun, 0, 2);
            pnlLeft.Controls.Add(tblLeft);

            // ── Right cell: score badge ────────────────────────────────────
            pnlScore.Dock      = DockStyle.Fill;
            pnlScore.BackColor = Color.Transparent;
            pnlScore.Paint    += PaintScorePanel;

            tbl.Controls.Add(pnlLeft,    0, 0);
            tbl.Controls.Add(pnlScore,   1, 0);
            pnlTop.Controls.Add(tbl);
        }

        private void BuildTabControl()
        {
            tabMain.Dock      = DockStyle.Fill;
            tabMain.BackColor = BgDark;
            tabMain.DrawMode  = TabDrawMode.OwnerDrawFixed;
            tabMain.ItemSize  = new Size(118, 34);
            tabMain.SizeMode  = TabSizeMode.Fixed;
            tabMain.Padding   = new Point(0, 7);
            tabMain.DrawItem += DrawTab;
            tabMain.Font      = new Font("Segoe UI", 9.5f);

            BuildOverviewTab();
            BuildProcessesTab();
            BuildStartupTab();
            BuildDiskTab();
            BuildNetworkTab();
            BuildDevEnvTab();

            tabMain.TabPages.AddRange(new[]
            {
                tabOverview, tabProcesses, tabStartup, tabDisk, tabNetwork, tabDevEnv
            });
        }

        private void BuildOverviewTab()
        {
            tabOverview.BackColor = BgDark;

            var tblOverview = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 1,
                BackColor   = BgDark,
            };
            tblOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f)); // Warnings take 60%
            tblOverview.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f)); // Recommendations take 40%
            tblOverview.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            // ── Left: Warnings ─────────────────────────────────────────────
            var pnlW = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 6, 8) };
            var lblW = new Label
            {
                Text = "⚠  Warnings & Alerts",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TxtPrimary,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 6)
            };
            rtbWarnings.Dock        = DockStyle.Fill;
            rtbWarnings.ReadOnly    = true;
            rtbWarnings.BackColor   = BgPanel;
            rtbWarnings.ForeColor   = TxtPrimary;
            rtbWarnings.Font        = new Font("Consolas", 9.5f);
            rtbWarnings.BorderStyle = BorderStyle.None;
            rtbWarnings.ScrollBars  = RichTextBoxScrollBars.Vertical;
            pnlW.Controls.Add(rtbWarnings);
            pnlW.Controls.Add(lblW);
            tblOverview.Controls.Add(pnlW, 0, 0);

            // ── Right: Recommendations ─────────────────────────────────────
            var pnlR = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6, 8, 10, 8) };
            var lblR = new Label
            {
                Text = "💡  Recommendations",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TxtPrimary,
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(0, 0, 0, 6)
            };
            flpRecs.Dock          = DockStyle.Fill;
            flpRecs.AutoScroll    = true;
            flpRecs.BackColor     = BgDark;
            flpRecs.FlowDirection = FlowDirection.TopDown;
            flpRecs.WrapContents  = false;
            pnlR.Controls.Add(flpRecs);
            pnlR.Controls.Add(lblR);
            tblOverview.Controls.Add(pnlR, 1, 0);

            // Dynamically resize cards to fit the recommendation panel width
            flpRecs.Resize += (s, e) =>
            {
                int w = flpRecs.ClientSize.Width - 20;
                if (w < 180) w = 180;
                foreach (Control c in flpRecs.Controls)
                {
                    if (c is Panel card)
                    {
                        card.Width = w;
                        foreach (Control sub in card.Controls)
                        {
                            if (sub is Label) sub.Width = card.Width - 108;
                            else if (sub is Button) sub.Left = card.Width - 92;
                        }
                    }
                }
            };

            tabOverview.Controls.Add(tblOverview);
        }

        private void BuildProcessesTab()
        {
            tabProcesses.BackColor = BgDark;
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };

            StyleGrid(dgvProcs);
            dgvProcs.Dock = DockStyle.Fill;
            dgvProcs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",   HeaderText = "Process",       FillWeight = 42 });
            dgvProcs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Pid",    HeaderText = "PID",           FillWeight = 10 });
            dgvProcs.Columns.Add(new DataGridViewTextBoxColumn { Name = "CPU",    HeaderText = "CPU (s)",        FillWeight = 13 });
            dgvProcs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Memory", HeaderText = "Memory (MB)",   FillWeight = 18 });
            dgvProcs.Columns.Add(new DataGridViewButtonColumn
            {
                Name  = "Kill", HeaderText = "Action", FillWeight = 12,
                Text  = "Kill ✕", UseColumnTextForButtonValue = true
            });
            dgvProcs.CellContentClick += OnKillProcessClick;

            var info = new Label
            {
                Text      = "Top 50 processes by memory — click 'Kill ✕' to terminate a process",
                ForeColor = TxtMuted,
                Font      = new Font("Segoe UI", 8.5f),
                AutoSize  = true,
                Dock      = DockStyle.Bottom,
                Padding   = new Padding(2, 4, 0, 0)
            };

            pnl.Controls.Add(dgvProcs);
            pnl.Controls.Add(info);
            tabProcesses.Controls.Add(pnl);
        }

        private void BuildStartupTab()
        {
            tabStartup.BackColor = BgDark;
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };

            StyleGrid(dgvStartup);
            dgvStartup.Dock = DockStyle.Fill;
            dgvStartup.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",     HeaderText = "Name",     FillWeight = 22 });
            dgvStartup.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Location", FillWeight = 32 });
            dgvStartup.Columns.Add(new DataGridViewTextBoxColumn { Name = "Command",  HeaderText = "Command",  FillWeight = 46 });

            var info = new Label
            {
                Text      = "Disable startup items via Task Manager › Startup Apps (Win + X → Task Manager)",
                ForeColor = TxtMuted,
                Font      = new Font("Segoe UI", 8.5f),
                AutoSize  = true,
                Dock      = DockStyle.Bottom,
                Padding   = new Padding(2, 4, 0, 0)
            };

            pnl.Controls.Add(dgvStartup);
            pnl.Controls.Add(info);
            tabStartup.Controls.Add(pnl);
        }

        private void BuildDiskTab()
        {
            tabDisk.BackColor = BgDark;
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 12, 16, 12), AutoScroll = true };

            var hdr = new Label
            {
                Text      = "Disk Usage — Fixed Local Drives",
                Font      = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = TxtPrimary,
                AutoSize  = true,
                Dock      = DockStyle.Top,
                Padding   = new Padding(0, 0, 0, 10)
            };

            pnlDiskViz.Dock      = DockStyle.Top;
            pnlDiskViz.Height    = 100; // Resized dynamically in PopulateDisks
            pnlDiskViz.BackColor = BgDark;
            pnlDiskViz.Paint    += PaintDiskBars;

            pnl.Controls.Add(pnlDiskViz);
            pnl.Controls.Add(hdr);
            tabDisk.Controls.Add(pnl);
        }

        private void BuildNetworkTab()
        {
            tabNetwork.BackColor = BgDark;
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 8, 10, 8) };

            StyleGrid(dgvNetwork);
            dgvNetwork.Dock = DockStyle.Fill;
            dgvNetwork.Columns.Add(new DataGridViewTextBoxColumn { Name = "Local",   HeaderText = "Local Address",  FillWeight = 28 });
            dgvNetwork.Columns.Add(new DataGridViewTextBoxColumn { Name = "Remote",  HeaderText = "Remote Address", FillWeight = 28 });
            dgvNetwork.Columns.Add(new DataGridViewTextBoxColumn { Name = "State",   HeaderText = "State",          FillWeight = 18 });
            dgvNetwork.Columns.Add(new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "Process",        FillWeight = 26 });

            var info = new Label
            {
                Text      = "Showing Established and Listening connections only",
                ForeColor = TxtMuted,
                Font      = new Font("Segoe UI", 8.5f),
                AutoSize  = true,
                Dock      = DockStyle.Bottom,
                Padding   = new Padding(2, 4, 0, 0)
            };

            pnl.Controls.Add(dgvNetwork);
            pnl.Controls.Add(info);
            tabNetwork.Controls.Add(pnl);
        }

        private void BuildDevEnvTab()
        {
            tabDevEnv.BackColor = BgDark;

            pnlDevCards.Dock          = DockStyle.Fill;
            pnlDevCards.AutoScroll    = true;
            pnlDevCards.BackColor     = BgDark;
            pnlDevCards.FlowDirection = FlowDirection.LeftToRight;
            pnlDevCards.WrapContents  = true;
            pnlDevCards.Padding       = new Padding(10, 10, 10, 10);

            tabDevEnv.Controls.Add(pnlDevCards);
        }

        private void BuildButtonBar()
        {
            pnlButtons.Dock          = DockStyle.Bottom;
            pnlButtons.AutoSize      = true;
            pnlButtons.AutoSizeMode  = AutoSizeMode.GrowAndShrink;
            pnlButtons.BackColor     = BgPanel;
            pnlButtons.Padding       = new Padding(10, 6, 10, 6);
            pnlButtons.FlowDirection = FlowDirection.LeftToRight;
            pnlButtons.WrapContents  = true;
            pnlButtons.Paint        += (_, e) =>
            {
                using var pen = new Pen(Color.FromArgb(55, 65, 110), 1);
                e.Graphics.DrawLine(pen, 0, 0, pnlButtons.Width, 0);
            };

            StyleButton(btnRunAudit,  "▶  Run Audit",  Accent);
            StyleButton(btnRefresh,   "↺  Refresh",    BtnBg);
            StyleButton(btnQuickFix,  "⚡ Quick Fix",  BtnBg);
            StyleButton(btnToggleLogs,"📋 Show Logs",  BtnBg);

            btnRunAudit.Click   += async (_, __) => await RunFullAuditAsync();
            btnRefresh.Click    += async (_, __) => await RefreshReportAsync();
            btnQuickFix.Click   += (_, __) => Task.Run(QuickFix);
            btnToggleLogs.Click += ToggleLogs;

            progressBar.Width   = 130;
            progressBar.Height  = 18;
            progressBar.Style   = ProgressBarStyle.Blocks;
            progressBar.Visible = false;
            progressBar.Margin  = new Padding(0, 8, 8, 8);

            lblStatus.AutoSize  = true;
            lblStatus.ForeColor = TxtMuted;
            lblStatus.Font      = new Font("Segoe UI", 9f);
            lblStatus.Margin    = new Padding(0, 8, 8, 8);

            pnlButtons.Controls.AddRange(new Control[]
            {
                btnRunAudit, btnRefresh, btnQuickFix, btnToggleLogs,
                progressBar, lblStatus
            });
        }

        private void BuildLogPanel()
        {
            pnlLog.Dock      = DockStyle.Bottom;
            pnlLog.Height    = 145;
            pnlLog.BackColor = Color.Black;
            pnlLog.Visible   = false;

            rtbLog.Dock        = DockStyle.Fill;
            rtbLog.ReadOnly    = true;
            rtbLog.BackColor   = Color.FromArgb(12, 12, 22);
            rtbLog.ForeColor   = Color.FromArgb(100, 230, 100);
            rtbLog.Font        = new Font("Consolas", 9f);
            rtbLog.BorderStyle = BorderStyle.None;
            pnlLog.Controls.Add(rtbLog);
        }

        // ═══════════════════════════════════════════════════════════════════
        // CUSTOM PAINTING
        // ═══════════════════════════════════════════════════════════════════

        private void PaintScorePanel(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = pnlScore.Width;
            int h = pnlScore.Height;

            using var bgPath  = RoundedRect(new Rectangle(0, 0, w - 1, h - 1), 10);
            using var bgBrush = new SolidBrush(BgCard);
            g.FillPath(bgBrush, bgPath);
            using var borderPen = new Pen(Color.FromArgb(65, 75, 120), 1);
            g.DrawPath(borderPen, bgPath);

            var centerFmt = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            if (_score < 0)
            {
                using var nb = new SolidBrush(TxtMuted);
                g.DrawString("—", new Font("Segoe UI", 26, FontStyle.Bold), nb,
                    new RectangleF(0, -4, w, h), centerFmt);
                g.DrawString("NO DATA", new Font("Segoe UI", 6.5f, FontStyle.Bold), nb,
                    new RectangleF(0, h - 18, w, 14), centerFmt);
                return;
            }

            var scoreColor = _score >= 80 ? ColorGood : _score >= 50 ? ColorWarn : ColorBad;

            // Arc gauge
            const int margin = 9;
            int arcSize = Math.Min(w - margin * 2, h - margin - 22);
            int arcX    = margin + (w - margin * 2 - arcSize) / 2;
            var arcRect = new Rectangle(arcX, margin, arcSize, arcSize);

            using var bgArcPen = new Pen(Color.FromArgb(45, 55, 85), 7)
            {
                StartCap = LineCap.Round,
                EndCap   = LineCap.Round
            };
            g.DrawArc(bgArcPen, arcRect, 150, 240);

            float filled = (float)(_score / 100.0 * 240.0);
            if (filled > 0)
            {
                using var scorePen = new Pen(scoreColor, 7)
                {
                    StartCap = LineCap.Round,
                    EndCap   = LineCap.Round
                };
                g.DrawArc(scorePen, arcRect, 150, filled);
            }

            // Score number centred inside the arc
            using var scoreBrush = new SolidBrush(scoreColor);
            g.DrawString(_score.ToString(),
                new Font("Segoe UI", 21, FontStyle.Bold),
                scoreBrush,
                new RectangleF(0, 2, w, h - 20),
                centerFmt);

            // Footer labels
            using var lb = new SolidBrush(TxtMuted);
            var botFmt = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("HEALTH SCORE",
                new Font("Segoe UI", 6f, FontStyle.Bold),
                lb,
                new RectangleF(0, h - 17, w, 14),
                botFmt);
        }

        private void PaintDiskBars(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (_disks.Count == 0)
            {
                using var nb = new SolidBrush(TxtMuted);
                g.DrawString("No disk data — run an audit first.",
                    new Font("Segoe UI", 10f), nb, new PointF(12, 12));
                return;
            }

            int panelW = pnlDiskViz.ClientSize.Width;
            int barW   = Math.Max(80, panelW - 290);
            const int barH   = 32;
            const int rowH   = barH + 20;
            const int labelW = 52;
            const int xBar   = labelW + 8;

            using var lblFont  = new Font("Segoe UI", 12f, FontStyle.Bold);
            using var infoFont = new Font("Segoe UI", 9f);
            using var pctFont  = new Font("Segoe UI", 8.5f, FontStyle.Bold);

            int y = 8;
            foreach (var disk in _disks)
            {
                // Drive letter
                using var driveBrush = new SolidBrush(TxtPrimary);
                g.DrawString(disk.Drive, lblFont, driveBrush, new PointF(8, y + 5));

                // Bar background
                var barRect = new Rectangle(xBar, y, barW, barH);
                using var bgPath  = RoundedRect(barRect, 6);
                using var bgBrush = new SolidBrush(BgCard);
                g.FillPath(bgBrush, bgPath);

                // Filled portion
                double pct = Math.Clamp(disk.UsedPercentage, 0, 100);
                if (pct > 0)
                {
                    int usedW      = Math.Max(8, (int)(barW * pct / 100.0));
                    var fillColor  = pct >= 90 ? ColorBad : pct >= 75 ? ColorWarn : Accent;
                    var fillRect   = new Rectangle(barRect.X, barRect.Y, usedW, barH);
                    using var fillPath  = RoundedRect(fillRect, 6);
                    using var fillBrush = new LinearGradientBrush(
                        fillRect,
                        Color.FromArgb(200, fillColor),
                        fillColor,
                        LinearGradientMode.Horizontal);
                    g.FillPath(fillBrush, fillPath);
                }

                // Border
                using var borderPen = new Pen(Color.FromArgb(65, 75, 120), 1);
                using var borderPath = RoundedRect(barRect, 6);
                g.DrawPath(borderPen, borderPath);

                // Percentage inside bar
                using var pctBrush = new SolidBrush(Color.White);
                var cFmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString($"{pct:F0}%", pctFont, pctBrush, barRect, cFmt);

                // Info text to the right
                double usedGB  = disk.TotalSizeGB - disk.FreeSpaceGB;
                using var infoBrush = new SolidBrush(TxtMuted);
                g.DrawString(
                    $"{usedGB:F1} / {disk.TotalSizeGB:F1} GB    ({disk.FreeSpaceGB:F1} GB free)",
                    infoFont, infoBrush,
                    new PointF(barRect.Right + 14, y + 8));

                y += rowH;
            }
        }

        private void DrawTab(object? sender, DrawItemEventArgs e)
        {
            if (sender is not TabControl tab) return;
            var page = tab.TabPages[e.Index];
            var rect = tab.GetTabRect(e.Index);
            bool sel = tab.SelectedIndex == e.Index;

            using var bgBrush = new SolidBrush(sel ? BgPanel : BgDark);
            e.Graphics.FillRectangle(bgBrush, rect);

            using var textBrush = new SolidBrush(sel ? TxtPrimary : TxtMuted);
            var fmt = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            var font = sel
                ? new Font("Segoe UI", 9.5f, FontStyle.Bold)
                : new Font("Segoe UI", 9.5f);
            e.Graphics.DrawString(page.Text.Trim(), font, textBrush, rect, fmt);

            if (sel)
            {
                using var accentPen = new Pen(Accent, 3);
                e.Graphics.DrawLine(accentPen,
                    rect.X + 3, rect.Bottom - 2,
                    rect.Right - 3, rect.Bottom - 2);
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // REPORT LOADING  (fully async — no Thread.Sleep on UI thread)
        // ═══════════════════════════════════════════════════════════════════

        private async Task RefreshReportAsync()
        {
            var reportPath = FindReportPath();
            if (reportPath == null)
            {
                rtbWarnings.Text = "No report found. Click ▶ Run Audit to generate one.";
                _score = -1;
                pnlScore.Invalidate();
                AppendLog("Refresh: no report.json found");
                return;
            }

            AppendLog($"Loading: {reportPath}");

            // File read + retry on background thread — no Thread.Sleep on UI thread
            string? content = await Task.Run(async () =>
            {
                for (int i = 0; i < 6; i++)
                {
                    try { return File.ReadAllText(reportPath); }
                    catch (IOException)               { await Task.Delay(150); }
                    catch (UnauthorizedAccessException) { await Task.Delay(150); }
                }
                return null;
            });

            if (content == null) { AppendLog("Could not read report.json after retries"); return; }

            JsonDocument doc;
            try   { doc = JsonDocument.Parse(content); }
            catch (JsonException jex) { AppendLog("JSON parse error: " + jex.Message); return; }

            try   { ApplyReport(doc); }
            catch (Exception ex)      { AppendLog("ApplyReport error: " + ex.Message); }
        }

        private void ApplyReport(JsonDocument doc)
        {
            var root = doc.RootElement;

            // ── Timestamp ──────────────────────────────────────────────────
            if (root.TryGetProperty("CollectedAt", out var ts))
                lblLastRun.Text = "Last run: " + ts.GetDateTime().ToLocalTime().ToString("g");

            // ── Machine header ─────────────────────────────────────────────
            if (root.TryGetProperty("Machine", out var machine))
            {
                try
                {
                    var name    = GetStr(machine, "ComputerName");
                    var os      = GetStr(machine, "OSVersion");
                    var totGB   = GetDbl(machine, "TotalMemoryGB");
                    var freeGB  = GetDbl(machine, "FreeMemoryGB");
                    var usedGB  = totGB - freeGB;
                    var ramPct  = totGB > 0 ? usedGB / totGB * 100 : 0;
                    var cpu     = GetStr(machine, "CPUName");
                    var cores   = machine.TryGetProperty("CPUCores", out var cc) ? cc.GetInt32() : 0;

                    var sb = new StringBuilder($"{name}  ·  {os}");
                    if (totGB > 0)
                        sb.Append($"  ·  RAM: {usedGB:F1}/{totGB:F1} GB ({ramPct:F0}%)");
                    if (!string.IsNullOrEmpty(cpu))
                        sb.Append($"  ·  {cpu}{(cores > 0 ? $" ({cores}C)" : "")}");
                    lblMachine.Text = sb.ToString();
                }
                catch { lblMachine.Text = ""; }
            }

            // ── Score ──────────────────────────────────────────────────────
            if (root.TryGetProperty("Analysis", out var analysis) &&
                analysis.TryGetProperty("HealthScore", out var hs))
            {
                _score = hs.GetInt32();
                pnlScore.Invalidate();
                AppendLog($"Score: {_score}/100");
            }

            PopulateWarnings(root);
            PopulateRecommendations(root);
            PopulateProcesses(root);
            PopulateStartup(root);
            PopulateDisks(root);
            PopulateNetwork(root);
            PopulateDevEnv(root);
        }

        // ─── Tab populators ────────────────────────────────────────────────

        private void PopulateWarnings(JsonElement root)
        {
            rtbWarnings.Clear();
            bool any = false;

            if (root.TryGetProperty("Analysis", out var analysis) &&
                analysis.TryGetProperty("Warnings", out var warns))
            {
                foreach (var w in warns.EnumerateArray())
                {
                    any = true;
                    var sev = GetStr(w, "Severity") ?? "INFO";
                    var msg = GetStr(w, "Message")  ?? "";
                    var col = sev switch
                    {
                        "HIGH"   => ColorBad,
                        "MEDIUM" => ColorWarn,
                        _        => Color.FromArgb(140, 175, 235)
                    };
                    rtbWarnings.SelectionColor = col;
                    rtbWarnings.AppendText($"[{sev,-6}]  {msg}\n");
                }
            }

            if (!any)
            {
                rtbWarnings.SelectionColor = ColorGood;
                rtbWarnings.AppendText("✓  All clear — no warnings detected!\n");
            }

            if (root.TryGetProperty("Analysis", out var anal2) &&
                anal2.TryGetProperty("DevFindings", out var df) &&
                df.ValueKind == JsonValueKind.Array && df.GetArrayLength() > 0)
            {
                rtbWarnings.SelectionColor = TxtMuted;
                rtbWarnings.AppendText("\n── Developer Environment Findings ──────────\n");
                foreach (var f in df.EnumerateArray())
                {
                    rtbWarnings.SelectionColor = TxtMuted;
                    rtbWarnings.AppendText($"  ▸  {f.GetString()}\n");
                }
            }

            rtbWarnings.SelectionStart = 0;
            rtbWarnings.ScrollToCaret();
        }

        private void PopulateRecommendations(JsonElement root)
        {
            flpRecs.Controls.Clear();
            if (!root.TryGetProperty("Analysis",       out var analysis)) return;
            if (!analysis.TryGetProperty("Recommendations", out var recs)) return;
            if (recs.ValueKind != JsonValueKind.Array) return;

            int targetW = Math.Max(180, flpRecs.ClientSize.Width - 20);

            foreach (var r in recs.EnumerateArray())
            {
                var text = r.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(text)) continue;

                var card = new Panel
                {
                    Width     = targetW,
                    Height    = 72,
                    BackColor = BgCard,
                    Margin    = new Padding(0, 0, 0, 8)
                };
                card.Paint += (_, pe) =>
                {
                    pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using var p  = new Pen(Color.FromArgb(65, 80, 130), 1);
                    using var bp = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
                    pe.Graphics.DrawPath(p, bp);
                    using var lb = new SolidBrush(Accent);
                    pe.Graphics.FillRectangle(lb, new Rectangle(0, 0, 3, card.Height));
                };

                var lbl = new Label
                {
                    Text         = text,
                    ForeColor    = TxtPrimary,
                    Font         = new Font("Segoe UI", 8.5f),
                    Left         = 10, Top    = 8,
                    Width        = card.Width - 108,
                    Height       = 52,
                    AutoEllipsis = true
                };

                var btn = new Button
                {
                    Text      = "Action",
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Accent,
                    ForeColor = BgDark,
                    Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
                    Width     = 82,
                    Height    = 30,
                    Left      = card.Width - 92,
                    Top       = 20
                };
                btn.FlatAppearance.BorderSize = 0;
                var capturedText = text;
                btn.Click += (_, __) => PerformRecommendationAction(capturedText);

                card.Controls.AddRange(new Control[] { lbl, btn });
                flpRecs.Controls.Add(card);
            }
        }

        private void PopulateProcesses(JsonElement root)
        {
            dgvProcs.Rows.Clear();
            if (!root.TryGetProperty("Processes", out var procs)) return;
            if (procs.ValueKind != JsonValueKind.Array) return;

            var sorted = procs.EnumerateArray()
                .Select(p =>
                {
                    string name = GetStr(p, "Name") ?? GetStr(p, "ProcessName") ?? "";
                    int    pid  = p.TryGetProperty("Pid", out var pp) ? pp.GetInt32()
                                : p.TryGetProperty("Id",  out var id) ? id.GetInt32() : 0;
                    double cpu  = GetDbl(p, "CPU");
                    double mem  = GetDbl(p, "MemoryMb");
                    return (name, pid, cpu, mem);
                })
                .OrderByDescending(x => x.mem)
                .Take(50);

            foreach (var (name, pid, cpu, mem) in sorted)
                dgvProcs.Rows.Add(name, pid, cpu.ToString("F2"), mem.ToString("F1"));
        }

        private void PopulateStartup(JsonElement root)
        {
            dgvStartup.Rows.Clear();
            if (!root.TryGetProperty("Startup", out var startup)) return;
            if (startup.ValueKind != JsonValueKind.Array) return;

            foreach (var s in startup.EnumerateArray())
                dgvStartup.Rows.Add(
                    GetStr(s, "Name")     ?? "",
                    GetStr(s, "Location") ?? "",
                    GetStr(s, "Command")  ?? "");
        }

        private void PopulateDisks(JsonElement root)
        {
            _disks.Clear();
            if (!root.TryGetProperty("Disks", out var disks)) { pnlDiskViz.Invalidate(); return; }
            if (disks.ValueKind != JsonValueKind.Array)       { pnlDiskViz.Invalidate(); return; }

            foreach (var d in disks.EnumerateArray())
            {
                try
                {
                    _disks.Add(new DiskInfo
                    {
                        Drive          = GetStr(d, "Drive") ?? "?",
                        TotalSizeGB    = GetDbl(d, "TotalSizeGB"),
                        FreeSpaceGB    = GetDbl(d, "FreeSpaceGB"),
                        UsedPercentage = GetDbl(d, "UsedPercentage")
                    });
                }
                catch { /* skip malformed entry */ }
            }

            // Dynamically size pnlDiskViz height to accommodate all disks and trigger vertical scrollbar on parent panel
            const int barH = 32;
            const int rowH = barH + 20;
            pnlDiskViz.Height = _disks.Count * rowH + 20;

            pnlDiskViz.Invalidate();
        }

        private void PopulateNetwork(JsonElement root)
        {
            dgvNetwork.Rows.Clear();
            if (!root.TryGetProperty("Network", out var net)) return;
            if (net.ValueKind != JsonValueKind.Array) return;

            foreach (var c in net.EnumerateArray())
            {
                var state = GetStr(c, "State") ?? "";
                if (!state.Equals("Established", StringComparison.OrdinalIgnoreCase) &&
                    !state.Equals("Listen",       StringComparison.OrdinalIgnoreCase))
                    continue;

                dgvNetwork.Rows.Add(
                    GetStr(c, "LocalAddress")  ?? "",
                    GetStr(c, "RemoteAddress") ?? "",
                    state,
                    GetStr(c, "Process")       ?? "");
            }
        }

        private void PopulateDevEnv(JsonElement root)
        {
            pnlDevCards.Controls.Clear();

            if (!root.TryGetProperty("DevEnvironment", out var dev) ||
                dev.ValueKind == JsonValueKind.Null)
            {
                var lbl = new Label
                {
                    Text      = "Developer environment data not yet available.\n\nClick ▶ Run Audit to collect it.",
                    ForeColor = TxtMuted,
                    Font      = new Font("Segoe UI", 10f),
                    AutoSize  = true,
                    Location  = new Point(14, 14)
                };
                pnlDevCards.Controls.Add(lbl);
                return;
            }

            // ── WSL2 ──────────────────────────────────────────────────────
            bool wslFound = dev.TryGetProperty("WslDetected", out var wd) && wd.GetBoolean();
            if (wslFound && dev.TryGetProperty("WslDisks", out var wslD) && wslD.GetArrayLength() > 0)
            {
                double tot = 0;
                var sb = new StringBuilder();
                foreach (var d in wslD.EnumerateArray())
                {
                    var dname = GetStr(d, "Distribution") ?? "?";
                    var sz    = GetDbl(d, "SizeGB");
                    tot += sz;
                    sb.AppendLine($"  {dname}: {sz:F1} GB");
                }
                sb.Insert(0, $"Total VHD: {tot:F1} GB\n");
                AddDevCard("🐧  WSL2 Virtual Disks", sb.ToString().TrimEnd(),
                    tot > 30 ? ColorWarn : Accent,
                    "Shutdown WSL", () =>
                    {
                        Process.Start(new ProcessStartInfo("powershell",
                            "-NoProfile -Command \"wsl --shutdown; Write-Host 'WSL stopped. To compact: Optimize-VHD'\"")
                        { UseShellExecute = true });
                    });
            }
            else
            {
                AddDevCard("🐧  WSL2", "Not detected on this machine", TxtMuted, null, null);
            }

            // ── Docker ────────────────────────────────────────────────────
            bool dockerFound = dev.TryGetProperty("DockerDetected", out var dd) && dd.GetBoolean();
            if (dockerFound)
            {
                var sb = new StringBuilder();
                if (dev.TryGetProperty("DockerContainers", out var ctrs)) sb.AppendLine($"Running containers: {ctrs.GetArrayLength()}");
                if (dev.TryGetProperty("DockerImages",     out var imgs)) sb.AppendLine($"Pulled images: {imgs.GetArrayLength()}");
                AddDevCard("🐳  Docker", sb.ToString().TrimEnd(), Accent,
                    "docker system prune", () =>
                    {
                        if (MessageBox.Show("Remove all unused containers, images and networks?",
                            "Docker Cleanup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                            Process.Start(new ProcessStartInfo("cmd",
                                "/c docker system prune -f && pause")
                            { UseShellExecute = true });
                    });
            }
            else
            {
                AddDevCard("🐳  Docker", "Docker CLI not found in PATH", TxtMuted, null, null);
            }

            // ── Dev Caches ────────────────────────────────────────────────
            if (dev.TryGetProperty("DevCaches", out var cachesEl) && cachesEl.ValueKind == JsonValueKind.Array)
            {
                double total = GetDbl(dev, "TotalDevCacheSizeGB");
                var sb = new StringBuilder($"Total cache: {total:F1} GB\n\n");
                foreach (var c in cachesEl.EnumerateArray())
                {
                    double sz = GetDbl(c, "SizeGB");
                    if (sz >= 0.05)
                        sb.AppendLine($"  {GetStr(c, "Name")}: {sz:F2} GB");
                }
                AddDevCard("📦  Package Caches", sb.ToString().TrimEnd(),
                    total > 10 ? ColorWarn : TxtMuted, null, null);
            }

            // ── Zombie Processes ──────────────────────────────────────────
            if (dev.TryGetProperty("ZombieProcesses", out var zombies) && zombies.ValueKind == JsonValueKind.Array)
            {
                int cnt = zombies.GetArrayLength();
                if (cnt > 0)
                {
                    var sb = new StringBuilder($"Found {cnt} orphaned process(es):\n\n");
                    var groups = zombies.EnumerateArray()
                        .GroupBy(z => GetStr(z, "ProcessName") ?? "?")
                        .Select(g => (g.Key, Count: g.Count(),
                                     Mem: g.Sum(z => GetDbl(z, "MemoryMb"))));
                    foreach (var (pname, count, mem) in groups)
                        sb.AppendLine($"  {pname} ×{count}  ({mem:F0} MB)");

                    AddDevCard("👻  Orphaned Dev Processes", sb.ToString().TrimEnd(), ColorWarn,
                        "Kill all", () =>
                        {
                            foreach (var z in zombies.EnumerateArray())
                                if (z.TryGetProperty("Pid", out var pidEl))
                                    try { Process.GetProcessById(pidEl.GetInt32()).Kill(); } catch { }
                            AppendLog("Killed orphaned dev processes");
                            _ = RefreshReportAsync();
                        });
                }
                else
                {
                    AddDevCard("👻  Orphaned Dev Processes", "None detected  ✓", ColorGood, null, null);
                }
            }

            // ── PATH & Tools ──────────────────────────────────────────────
            if (dev.TryGetProperty("PathTools", out var tools) && tools.ValueKind == JsonValueKind.Array)
            {
                var sb    = new StringBuilder();
                int found = 0, missing = 0;
                foreach (var t in tools.EnumerateArray())
                {
                    var tname   = GetStr(t, "ToolName") ?? "?";
                    bool isFound = t.TryGetProperty("Found", out var tf) && tf.GetBoolean();
                    var ver     = GetStr(t, "Version");
                    if (isFound) found++; else missing++;
                    sb.AppendLine($"  {(isFound ? "✓" : "✗")} {tname}{(!string.IsNullOrEmpty(ver) ? $"  v{ver}" : "")}");
                }

                bool longPaths = dev.TryGetProperty("LongPathsEnabled", out var lp) && lp.GetBoolean();
                int pathLen    = dev.TryGetProperty("PathLength", out var pl) ? pl.GetInt32() : 0;
                sb.AppendLine();
                sb.AppendLine($"  PATH: {pathLen} chars{(pathLen > 2048 ? " ⚠ very long" : "")}");
                sb.Append($"  LongPaths: {(longPaths ? "✓ Enabled" : "✗ Disabled")}");

                var cardColor = missing > 2 ? ColorWarn : Accent;
                AddDevCard("🛠  PATH & Tools", sb.ToString().TrimEnd(), cardColor,
                    longPaths ? null : "Enable LongPaths",
                    longPaths ? null : () =>
                    {
                        if (MessageBox.Show(
                            "Enable LongPathsEnabled registry key? Requires UAC elevation.",
                            "Long Paths", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            Process.Start(new ProcessStartInfo("powershell",
                                "-Command \"Set-ItemProperty " +
                                "'HKLM:\\SYSTEM\\CurrentControlSet\\Control\\FileSystem' LongPathsEnabled 1\"")
                            { UseShellExecute = true, Verb = "runas" });
                        }
                    });
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        // EVENT HANDLERS & ACTIONS
        // ═══════════════════════════════════════════════════════════════════

        private async void OnLoad(object? sender, EventArgs e)
        {
            await RefreshReportAsync();
            if (_score < 0)
            {
                SetStatus("No report — running initial audit…");
                await Task.Delay(200);
                await RunFullAuditAsync();
            }
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            _watcher?.Dispose();
        }

        private async Task RunFullAuditAsync()
        {
            if (_isRunning) return;
            _isRunning = true;
            SetRunning(true, "Collecting data…");

            try
            {
                var psPath = FindFileInParents("AuditCollector.ps1");
                if (psPath == null)
                {
                    var appDataPath = Path.Combine(GetAppDataBase(), "AuditCollector.ps1");
                    if (File.Exists(appDataPath))
                    {
                        psPath = appDataPath;
                    }
                }

                if (psPath == null)
                {
                    AppendLog("AuditCollector.ps1 not found — will analyze existing Data/ if present.");
                }
                else
                {
                    // Pass -OutputDir explicitly so data always lands in the
                    // writable AppData folder, not next to the EXE (which is
                    // read-only when installed to Program Files).
                    var dataDir = GetDataDirectory();
                    var psi = new ProcessStartInfo
                    {
                        FileName               = LocatePowerShell(),
                        Arguments              = $"-NoProfile -ExecutionPolicy Bypass -File \"{psPath}\" -OutputDir \"{dataDir}\"",
                        UseShellExecute        = false,
                        CreateNoWindow         = true,
                        WorkingDirectory       = Path.GetDirectoryName(psPath)!,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true
                    };
                    AppendLog("Starting collectors…");
                    int code = await RunProcessAsync(psi, "Collector", 360_000);
                    AppendLog($"Collectors finished (exit {code})");
                }

                SetStatus("Analyzing…");
                AppendLog("Running analyzer in-process…");
                int aCode = await Task.Run(() =>
                    AuditorRunner.Run(
                        GetDataDirectory(),
                        GetReportsDirectory(),
                        s => AppendLog("[Analyzer] " + s)));
                AppendLog($"Analyzer finished (exit {aCode})");
                await RefreshReportAsync();
                AppendLog("Audit complete ✓");
            }
            catch (Exception ex)
            {
                AppendLog("Audit error: " + ex.Message);
            }
            finally
            {
                _isRunning = false;
                SetRunning(false, "");
            }
        }

        private async void OnKillProcessClick(object? sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0) return;
                if (dgvProcs.Columns[e.ColumnIndex].Name != "Kill") return;

                var pidVal  = dgvProcs.Rows[e.RowIndex].Cells["Pid"].Value;
                var nameVal = dgvProcs.Rows[e.RowIndex].Cells["Name"].Value;
                if (pidVal == null || !int.TryParse(pidVal.ToString(), out var pid)) return;

                var name = nameVal?.ToString() ?? pid.ToString();
                if (MessageBox.Show($"Kill process '{name}' (PID {pid})?",
                    "Confirm Kill", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                await Task.Run(() =>
                {
                    try   { Process.GetProcessById(pid).Kill(true); AppendLog($"Killed {name} (PID {pid})"); }
                    catch (Exception ex)                            { AppendLog($"Failed to kill {pid}: {ex.Message}"); }
                });

                await Task.Delay(600);
                await RefreshReportAsync();
            }
            catch (Exception ex) { AppendLog("Kill error: " + ex.Message); }
        }

        private void PerformRecommendationAction(string rec)
        {
            var r = rec.ToLowerInvariant();
            try
            {
                if      (r.Contains("disk") || r.Contains("storage"))
                {
                    try { Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true }); }
                    catch { Process.Start(new ProcessStartInfo("cmd", "/c start ms-settings:storage")
                            { UseShellExecute = true }); }
                }
                else if (r.Contains("ram") || r.Contains("close") || r.Contains("chrome"))
                {
                    Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true });
                }
                else if (r.Contains("startup"))
                {
                    tabMain.SelectedTab = tabStartup;
                    Process.Start(new ProcessStartInfo("taskmgr") { UseShellExecute = true });
                }
                else if (r.Contains("reboot"))
                {
                    if (MessageBox.Show("Reboot now? Unsaved work will be lost.",
                        "Reboot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo("shutdown", "/r /t 5")
                        { CreateNoWindow = true, UseShellExecute = false });
                    }
                }
                else if (r.Contains("wsl") || r.Contains("vhd"))
                {
                    tabMain.SelectedTab = tabDevEnv;
                }
                else if (r.Contains("cache"))
                {
                    tabMain.SelectedTab = tabDevEnv;
                }
                else if (r.Contains("orphaned"))
                {
                    tabMain.SelectedTab = tabDevEnv;
                }
                else
                {
                    AppendLog("No automated action for: " + rec);
                }
            }
            catch (Exception ex) { AppendLog("Action error: " + ex.Message); }
        }

        private void QuickFix()
        {
            AppendLog("Quick Fix: opening Disk Cleanup + Task Manager…");
            try { Process.Start(new ProcessStartInfo("cleanmgr.exe") { UseShellExecute = true }); } catch { }
            try { Process.Start(new ProcessStartInfo("taskmgr")      { UseShellExecute = true }); } catch { }
        }

        private void ToggleLogs(object? sender, EventArgs e)
        {
            _logVisible         = !_logVisible;
            pnlLog.Visible      = _logVisible;
            btnToggleLogs.Text  = _logVisible ? "📋 Hide Logs" : "📋 Show Logs";
        }

        // ═══════════════════════════════════════════════════════════════════
        // PATH RESOLUTION
        // ═══════════════════════════════════════════════════════════════════

        private string GetAppDataBase()
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WorkstationAuditor");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        private void EnsureCollectorsExtracted()
        {
            try
            {
                var targetDir = GetAppDataBase();
                var assembly = typeof(MainForm).Assembly;
                var resourceNames = assembly.GetManifestResourceNames();

                foreach (var resName in resourceNames)
                {
                    if (resName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase))
                    {
                        string fileName = resName;
                        int resIndex = resName.IndexOf("Resources.", StringComparison.OrdinalIgnoreCase);
                        if (resIndex >= 0)
                        {
                            fileName = resName.Substring(resIndex + "Resources.".Length);
                        }
                        else
                        {
                            int lastDot = resName.LastIndexOf('.', resName.Length - 5);
                            if (lastDot >= 0) fileName = resName.Substring(lastDot + 1);
                        }

                        var destPath = Path.Combine(targetDir, fileName);

                        using var stream = assembly.GetManifestResourceStream(resName);
                        if (stream != null)
                        {
                            using var destStream = File.Create(destPath);
                            stream.CopyTo(destStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLog("Failed to extract embedded collectors: " + ex.Message);
            }
        }

        private string? FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "readme.md")) ||
                    Directory.Exists(Path.Combine(dir.FullName, ".git")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }

        private string GetReportsDirectory()
        {
            var repo = FindRepositoryRoot();
            if (repo != null)
            {
                var d = Path.Combine(repo, "Reports");
                Directory.CreateDirectory(d);
                return d;
            }
            var app = Path.Combine(GetAppDataBase(), "Reports");
            Directory.CreateDirectory(app);
            return app;
        }

        private string GetDataDirectory()
        {
            var repo = FindRepositoryRoot();
            if (repo != null)
            {
                var d = Path.Combine(repo, "Data");
                Directory.CreateDirectory(d);
                return d;
            }
            var app = Path.Combine(GetAppDataBase(), "Data");
            Directory.CreateDirectory(app);
            return app;
        }

        private string? FindReportPath()
        {
            var p = Path.Combine(GetReportsDirectory(), "report.json");
            return File.Exists(p) ? p : null;
        }

        /// <summary>
        /// Walks parent directories from the EXE base to find a file by relative path.
        /// Bug fix: removed the stray lblScore.BringToFront() call that was originally
        /// embedded inside this helper method.
        /// </summary>
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

        // ═══════════════════════════════════════════════════════════════════
        // FILE WATCHER
        // ═══════════════════════════════════════════════════════════════════

        private void TrySetupFileWatcher()
        {
            try
            {
                var dir = GetReportsDirectory();
                if (!Directory.Exists(dir)) return;
                _watcher = new FileSystemWatcher(dir, "report.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
                };
                _watcher.Changed += OnReportChanged;
                _watcher.Created += OnReportChanged;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex) { AppendLog("Watcher setup failed: " + ex.Message); }
        }

        private void OnReportChanged(object sender, FileSystemEventArgs e)
        {
            if (IsDisposed) return;
            // Marshal to UI thread; use Task.Delay for debounce (no Thread.Sleep on UI thread)
            BeginInvoke(new Action(async () =>
            {
                await Task.Delay(350);
                if (!IsDisposed) await RefreshReportAsync();
            }));
        }

        // ═══════════════════════════════════════════════════════════════════
        // PROCESS HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private static string LocatePowerShell()
        {
            var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            foreach (var ver in new[] { "7", "6" })
            {
                var c = Path.Combine(pf, "PowerShell", ver, "pwsh.exe");
                if (File.Exists(c)) return c;
            }
            return "powershell";
        }

        private async Task<int> RunProcessAsync(ProcessStartInfo psi, string tag, int timeoutMs)
        {
            using var proc = new Process { StartInfo = psi };
            proc.OutputDataReceived += (_, e) => { if (e.Data != null) AppendLog($"[{tag}] {e.Data}"); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) AppendLog($"[{tag} ERR] {e.Data}"); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            bool done = await Task.Run(() => proc.WaitForExit(timeoutMs));
            if (!done) { try { proc.Kill(true); } catch { } AppendLog($"[{tag}] timed out"); return -1; }
            return proc.ExitCode;
        }

        // ═══════════════════════════════════════════════════════════════════
        // UI HELPERS
        // ═══════════════════════════════════════════════════════════════════

        private void SetStatus(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
            lblStatus.Text = text;
        }

        private void SetRunning(bool running, string status)
        {
            if (InvokeRequired) { BeginInvoke(() => SetRunning(running, status)); return; }
            btnRunAudit.Enabled = !running;
            btnRefresh.Enabled  = !running;
            progressBar.Visible = running;
            progressBar.Style   = running ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            lblStatus.Text      = status;
        }

        private void AppendLog(string message)
        {
            if (rtbLog.InvokeRequired) { rtbLog.BeginInvoke(() => AppendLog(message)); return; }
            rtbLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            try
            {
                File.AppendAllText(
                    Path.Combine(GetReportsDirectory(), "ui.log"),
                    DateTime.Now.ToString("o") + " " + message + Environment.NewLine);
            }
            catch { }
        }

        private void AddDevCard(string title, string body, Color accentColor,
                                string? actionLabel, Action? action)
        {
            int cardH = actionLabel != null ? 210 : 180;
            var card  = new Panel
            {
                Width     = 290,
                Height    = cardH,
                BackColor = BgCard,
                Margin    = new Padding(0, 0, 12, 12)
            };
            card.Paint += (_, pe) =>
            {
                pe.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var lb = new SolidBrush(accentColor);
                pe.Graphics.FillRectangle(lb, new Rectangle(0, 0, 4, card.Height));
                using var bp = RoundedRect(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 6);
                using var bp2 = new Pen(Color.FromArgb(65, 80, 130), 1);
                pe.Graphics.DrawPath(bp2, bp);
            };

            var lblT = new Label
            {
                Text      = title,
                Font      = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize  = false,
                Left = 12, Top = 10,
                Width = card.Width - 18, Height = 22
            };

            var lblB = new RichTextBox
            {
                Text            = body,
                Font            = new Font("Segoe UI", 8.5f),
                ForeColor       = TxtPrimary,
                BackColor       = BgCard,
                BorderStyle     = BorderStyle.None,
                ReadOnly        = true,
                ScrollBars      = RichTextBoxScrollBars.Vertical,
                Left            = 12,
                Top             = 36,
                Width           = card.Width - 24,
                Height          = cardH - (actionLabel != null ? 84 : 50),
                Cursor          = Cursors.Arrow,
                DetectUrls      = false
            };

            card.Controls.AddRange(new Control[] { lblT, lblB });

            if (action != null && actionLabel != null)
            {
                var btn = new Button
                {
                    Text      = actionLabel,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(58, 68, 108),
                    ForeColor = TxtPrimary,
                    Font      = new Font("Segoe UI", 8.5f),
                    Left      = 12,
                    Width     = card.Width - 24,
                    Height    = 28,
                    Top       = cardH - 38
                };
                btn.FlatAppearance.BorderColor = Color.FromArgb(78, 95, 155);
                btn.Click += (_, __) => action();
                card.Controls.Add(btn);
            }
            pnlDevCards.Controls.Add(card);
        }

        private static void StyleButton(Button btn, string text, Color backColor)
        {
            btn.Text      = text;
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = backColor;
            btn.ForeColor = backColor == Accent ? BgDark : TxtPrimary;
            btn.Font      = new Font("Segoe UI", 9f,
                backColor == Accent ? FontStyle.Bold : FontStyle.Regular);
            btn.Size      = new Size(114, 34);
            btn.Margin    = new Padding(0, 4, 8, 4);
            btn.FlatAppearance.BorderSize         = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(backColor, 0.15f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(backColor,  0.1f);
        }

        private static void StyleGrid(DataGridView dgv)
        {
            dgv.BackgroundColor            = BgPanel;
            dgv.GridColor                  = Color.FromArgb(48, 54, 84);
            dgv.BorderStyle                = BorderStyle.None;
            dgv.ColumnHeadersBorderStyle   = DataGridViewHeaderBorderStyle.Single;
            dgv.ReadOnly                   = true;
            dgv.AllowUserToAddRows         = false;
            dgv.AllowUserToDeleteRows      = false;
            dgv.RowHeadersVisible          = false;
            dgv.SelectionMode              = DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode        = DataGridViewAutoSizeColumnsMode.Fill;
            dgv.EnableHeadersVisualStyles  = false;
            dgv.RowTemplate.Height         = 24;

            dgv.DefaultCellStyle.BackColor          = BgPanel;
            dgv.DefaultCellStyle.ForeColor          = TxtPrimary;
            dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(65, 90, 155);
            dgv.DefaultCellStyle.SelectionForeColor = Color.White;
            dgv.DefaultCellStyle.Font               = new Font("Segoe UI", 9f);

            dgv.AlternatingRowsDefaultCellStyle.BackColor = BgDark;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = TxtPrimary;

            dgv.ColumnHeadersDefaultCellStyle.BackColor  = Color.FromArgb(42, 46, 74);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor  = TxtPrimary;
            dgv.ColumnHeadersDefaultCellStyle.Font       = new Font("Segoe UI", 9f, FontStyle.Bold);
            dgv.ColumnHeadersHeight = 30;
        }

        // ─── Graphics utility ──────────────────────────────────────────────
        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d    = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X,           bounds.Y,            d, d, 180, 90);
            path.AddArc(bounds.Right - d,   bounds.Y,            d, d, 270, 90);
            path.AddArc(bounds.Right - d,   bounds.Bottom - d,   d, d,   0, 90);
            path.AddArc(bounds.X,           bounds.Bottom - d,   d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        // ─── JSON extraction helpers ───────────────────────────────────────
        private static string? GetStr(JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

        private static double GetDbl(JsonElement el, string key) =>
            el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetDouble() : 0.0;
    }
}
