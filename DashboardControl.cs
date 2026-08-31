using System;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace MeroDokan
{
    public class DashboardControl : UserControl
    {
        private Panel cardServicesRevenue;
        private Panel cardProductsRevenue;
        private Panel cardAppointments;
        private Panel cardStaffCount;

        private Label lblServicesVal, lblServicesSub;
        private Label lblProductsVal, lblProductsSub;
        private Label lblAppointmentsVal, lblAppointmentsSub;
        private Label lblStaffVal, lblStaffSub;

        private DataGridView gridAppointmentsToday;
        private DataGridView gridTopStylists;
        private Panel chartPanel;

        private decimal totalServiceSales = 0;
        private decimal totalProductSales = 0;
        private decimal totalGrossRevenue = 0;
        private int todayApptCount = 0;
        private int activeStaffCount = 0;
        private int todayBooked = 0;
        private int todayInChair = 0;
        private int todayDone = 0;
        private int todayBilled = 0;

        public DashboardControl()
        {
            InitializeComponent();
            LoadDashboardData();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1040, 700);
            this.AutoScroll = true;
            this.BackColor = Theme.Secondary;
            this.DoubleBuffered = true;

            // Welcome Header
            Label lblWelcome = new Label();
            lblWelcome.Text = $"Welcome Back, {Session.FullName}";
            lblWelcome.Location = new Point(20, 15);
            lblWelcome.AutoSize = true;
            Theme.StyleLabel(lblWelcome, Theme.TextLight, Theme.HeaderFont);
            this.Controls.Add(lblWelcome);

            Label lblRole = new Label();
            lblRole.Text = $"Saloon & Spa Operations Executive Intelligence Center • {DateTime.Now:dddd, MMMM dd, yyyy}";
            lblRole.Location = new Point(22, 45);
            lblRole.AutoSize = true;
            Theme.StyleLabel(lblRole, Theme.TextMuted, Theme.MainFont);
            this.Controls.Add(lblRole);

            // Responsive Layout Table for 4 Smart KPI Cards
            TableLayoutPanel kpiLayout = new TableLayoutPanel();
            kpiLayout.Location = new Point(20, 75);
            kpiLayout.Size = new Size(990, 105);
            kpiLayout.ColumnCount = 4;
            kpiLayout.RowCount = 1;
            kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            kpiLayout.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            kpiLayout.BackColor = Color.Transparent;
            this.Controls.Add(kpiLayout);

            // 1. Services Revenue Card
            cardServicesRevenue = Theme.CreateSmartKpiCard(235, 100, "✂️ SALON SERVICES", "Rs. 0.00", "Services revenue volume", Theme.Accent, out lblServicesVal, out lblServicesSub);
            cardServicesRevenue.Dock = DockStyle.Fill;
            cardServicesRevenue.Margin = new Padding(0, 0, 8, 0);
            kpiLayout.Controls.Add(cardServicesRevenue, 0, 0);

            // 2. Retail Beauty Products Card
            cardProductsRevenue = Theme.CreateSmartKpiCard(235, 100, "🛍️ RETAIL PRODUCTS", "Rs. 0.00", "Beauty merchandise sales", Theme.Success, out lblProductsVal, out lblProductsSub);
            cardProductsRevenue.Dock = DockStyle.Fill;
            cardProductsRevenue.Margin = new Padding(8, 0, 8, 0);
            kpiLayout.Controls.Add(cardProductsRevenue, 1, 0);

            // 3. Today's Appointments Card
            cardAppointments = Theme.CreateSmartKpiCard(235, 100, "📅 TODAY'S APPOINTMENTS", "0 Clients", "0 In-Chair • 0 Booked", Theme.Warning, out lblAppointmentsVal, out lblAppointmentsSub);
            cardAppointments.Dock = DockStyle.Fill;
            cardAppointments.Margin = new Padding(8, 0, 8, 0);
            kpiLayout.Controls.Add(cardAppointments, 2, 0);

            // 4. Active Stylists Card
            cardStaffCount = Theme.CreateSmartKpiCard(235, 100, "💈 ACTIVE STYLISTS", "0 Staff", "Specialists on duty", Theme.Info, out lblStaffVal, out lblStaffSub);
            cardStaffCount.Dock = DockStyle.Fill;
            cardStaffCount.Margin = new Padding(8, 0, 0, 0);
            kpiLayout.Controls.Add(cardStaffCount, 3, 0);

            // LEFT PANEL: Revenue Analytics Chart & Breakdowns
            Label lblChartTitle = new Label();
            lblChartTitle.Text = "📊 Revenue Analytics & Distribution";
            lblChartTitle.Location = new Point(20, 195);
            lblChartTitle.AutoSize = true;
            Theme.StyleLabel(lblChartTitle, Theme.TextLight, Theme.SubHeaderFont);
            this.Controls.Add(lblChartTitle);

            chartPanel = new Panel();
            chartPanel.Size = new Size(470, 480);
            chartPanel.Location = new Point(20, 225);
            chartPanel.BackColor = Theme.CardBg;
            chartPanel.Paint += ChartPanel_Paint;
            this.Controls.Add(chartPanel);

            // RIGHT TOP: Today's Appointments & Chair Queue
            Label lblQueueTitle = new Label();
            lblQueueTitle.Text = "🪑 Today's Client Chair Queue";
            lblQueueTitle.Location = new Point(510, 195);
            lblQueueTitle.AutoSize = true;
            Theme.StyleLabel(lblQueueTitle, Theme.TextLight, Theme.SubHeaderFont);
            this.Controls.Add(lblQueueTitle);

            gridAppointmentsToday = new DataGridView();
            gridAppointmentsToday.Size = new Size(500, 220);
            gridAppointmentsToday.Location = new Point(510, 225);
            gridAppointmentsToday.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Theme.StyleGrid(gridAppointmentsToday);
            gridAppointmentsToday.CellFormatting += GridAppointmentsToday_CellFormatting;
            this.Controls.Add(gridAppointmentsToday);

            // RIGHT BOTTOM: Top Performing Stylists Leaderboard
            Label lblStylistTitle = new Label();
            lblStylistTitle.Text = "🏆 Top Performing Specialists Leaderboard";
            lblStylistTitle.Location = new Point(510, 460);
            lblStylistTitle.AutoSize = true;
            Theme.StyleLabel(lblStylistTitle, Theme.TextLight, Theme.SubHeaderFont);
            this.Controls.Add(lblStylistTitle);

            gridTopStylists = new DataGridView();
            gridTopStylists.Size = new Size(500, 245);
            gridTopStylists.Location = new Point(510, 490);
            gridTopStylists.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Theme.StyleGrid(gridTopStylists);
            this.Controls.Add(gridTopStylists);
        }

        private void GridAppointmentsToday_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (gridAppointmentsToday.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string status = e.Value.ToString();
                if (status == "In-Chair")
                {
                    e.CellStyle.ForeColor = Theme.Info;
                    e.CellStyle.Font = Theme.BoldFont;
                }
                else if (status == "Booked")
                {
                    e.CellStyle.ForeColor = Theme.Warning;
                    e.CellStyle.Font = Theme.BoldFont;
                }
                else if (status == "Completed")
                {
                    e.CellStyle.ForeColor = Theme.Success;
                }
                else if (status == "Billed")
                {
                    e.CellStyle.ForeColor = Theme.Accent;
                    e.CellStyle.Font = Theme.BoldFont;
                }
                else if (status == "Cancelled")
                {
                    e.CellStyle.ForeColor = Theme.Danger;
                }
            }
        }

        private void LoadDashboardData()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Service Revenue (Realized net sales matching Report Daily Sales)
                    using (SqlCommand cmd = new SqlCommand(@"
                        WITH FilteredDetails AS (
                            SELECT 
                                sd.SaleId,
                                SUM(sd.Total) AS ItemSubTotal
                            FROM SaleDetails sd
                            WHERE sd.ItemType = 'Service'
                            GROUP BY sd.SaleId
                        )
                        SELECT ISNULL(SUM(CASE 
                            WHEN s.SubTotal > 0 THEN ROUND(s.GrandTotal * (fd.ItemSubTotal / s.SubTotal), 2)
                            ELSE 0.00
                        END), 0)
                        FROM FilteredDetails fd
                        INNER JOIN Sales s ON fd.SaleId = s.Id", conn))
                    {
                        totalServiceSales = Convert.ToDecimal(cmd.ExecuteScalar());
                        lblServicesVal.Text = $"Rs. {totalServiceSales:N0}";
                    }

                    // 2. Product Revenue (Realized net sales matching Report Daily Sales)
                    using (SqlCommand cmd = new SqlCommand(@"
                        WITH FilteredDetails AS (
                            SELECT 
                                sd.SaleId,
                                SUM(sd.Total) AS ItemSubTotal
                            FROM SaleDetails sd
                            WHERE (sd.ItemType = 'Product' OR sd.ItemType IS NULL OR sd.ItemType = '')
                            GROUP BY sd.SaleId
                        )
                        SELECT ISNULL(SUM(CASE 
                            WHEN s.SubTotal > 0 THEN ROUND(s.GrandTotal * (fd.ItemSubTotal / s.SubTotal), 2)
                            ELSE 0.00
                        END), 0)
                        FROM FilteredDetails fd
                        INNER JOIN Sales s ON fd.SaleId = s.Id", conn))
                    {
                        totalProductSales = Convert.ToDecimal(cmd.ExecuteScalar());
                        lblProductsVal.Text = $"Rs. {totalProductSales:N0}";
                    }

                    totalGrossRevenue = totalServiceSales + totalProductSales;
                    if (totalGrossRevenue > 0)
                    {
                        decimal srvPct = (totalServiceSales / totalGrossRevenue) * 100m;
                        lblServicesSub.Text = $"{srvPct:0.#}% of Gross Revenue Volume";
                        lblProductsSub.Text = $"{(100m - srvPct):0.#}% from Beauty Upsells";
                    }

                    // 3. Today's Appointments (Including Billed & In-Chair)
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT 
                            ISNULL(COUNT(*), 0) AS TotalCount,
                            ISNULL(SUM(CASE WHEN Status = 'Booked' THEN 1 ELSE 0 END), 0) AS BookedCount,
                            ISNULL(SUM(CASE WHEN Status = 'In-Chair' THEN 1 ELSE 0 END), 0) AS InChairCount,
                            ISNULL(SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END), 0) AS DoneCount,
                            ISNULL(SUM(CASE WHEN Status = 'Billed' THEN 1 ELSE 0 END), 0) AS BilledCount,
                            ISNULL(SUM(CASE WHEN Status = 'Cancelled' THEN 1 ELSE 0 END), 0) AS CancelledCount
                        FROM Appointments 
                        WHERE AppointmentDate = CAST(GETDATE() AS DATE)", conn))
                    {
                        using (SqlDataReader r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                todayApptCount = Convert.ToInt32(r["TotalCount"]);
                                todayBooked = Convert.ToInt32(r["BookedCount"]);
                                todayInChair = Convert.ToInt32(r["InChairCount"]);
                                todayDone = Convert.ToInt32(r["DoneCount"]);
                                todayBilled = Convert.ToInt32(r["BilledCount"]);
                                int todayCancelled = Convert.ToInt32(r["CancelledCount"]);

                                lblAppointmentsVal.Text = $"{todayApptCount} Scheduled";
                                lblAppointmentsSub.Text = $"{todayInChair} In-Chair • {todayBooked} Booked • {todayBilled + todayDone} Done / Billed • {todayCancelled} Canc";
                            }
                        }
                    }

                    // 4. Active Staff Count
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM Staff WHERE IsActive = 1", conn))
                    {
                        activeStaffCount = (int)cmd.ExecuteScalar();
                        lblStaffVal.Text = $"{activeStaffCount} Stylists";
                        lblStaffSub.Text = $"{activeStaffCount} Specialists On-Duty";
                    }

                    // 5. Today's Appointments Grid
                    using (SqlCommand chkCmd = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceIds')
                            ALTER TABLE Appointments ADD ServiceIds NVARCHAR(500) NULL;
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceNames')
                            ALTER TABLE Appointments ADD ServiceNames NVARCHAR(1000) NULL;
                    ", conn))
                    {
                        try { chkCmd.ExecuteNonQuery(); } catch { }
                    }

                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP 8 
                            a.AppointmentTime AS [Time],
                            c.Name AS [Client],
                            ISNULL(NULLIF(a.ServiceNames, ''), s.Name) AS [Service],
                            st.Name AS [Stylist],
                            a.Status
                        FROM Appointments a
                        LEFT JOIN Customers c ON a.CustomerId = c.Id
                        LEFT JOIN Services s ON a.ServiceId = s.Id
                        LEFT JOIN Staff st ON a.StaffId = st.Id
                        WHERE a.AppointmentDate = CAST(GETDATE() AS DATE)
                        ORDER BY 
                            CASE a.Status WHEN 'In-Chair' THEN 1 WHEN 'Booked' THEN 2 WHEN 'Completed' THEN 3 ELSE 4 END,
                            a.AppointmentTime ASC", conn))
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            gridAppointmentsToday.DataSource = dt;

                            if (gridAppointmentsToday.Columns["Time"] != null) gridAppointmentsToday.Columns["Time"].FillWeight = 55;
                            if (gridAppointmentsToday.Columns["Client"] != null) gridAppointmentsToday.Columns["Client"].FillWeight = 90;
                            if (gridAppointmentsToday.Columns["Service"] != null) gridAppointmentsToday.Columns["Service"].FillWeight = 110;
                            if (gridAppointmentsToday.Columns["Stylist"] != null) gridAppointmentsToday.Columns["Stylist"].FillWeight = 85;
                            if (gridAppointmentsToday.Columns["Status"] != null) gridAppointmentsToday.Columns["Status"].FillWeight = 65;
                        }
                    }

                    // 6. Top Stylists Grid Leaderboard
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT TOP 5
                            ROW_NUMBER() OVER (ORDER BY ISNULL(SUM(sd.Total), 0) DESC) AS [Rank],
                            st.Name AS [Specialist],
                            st.Role AS [Specialty],
                            COUNT(sd.Id) AS [Services Done],
                            ISNULL(SUM(sd.Total), 0) AS [Revenue (Rs.)]
                        FROM Staff st
                        LEFT JOIN SaleDetails sd ON sd.StaffId = st.Id AND sd.ItemType = 'Service'
                        GROUP BY st.Id, st.Name, st.Role
                        ORDER BY [Revenue (Rs.)] DESC", conn))
                    {
                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);
                            gridTopStylists.DataSource = dt;

                            if (gridTopStylists.Columns["Rank"] != null) gridTopStylists.Columns["Rank"].FillWeight = 35;
                            if (gridTopStylists.Columns["Specialist"] != null) gridTopStylists.Columns["Specialist"].FillWeight = 110;
                            if (gridTopStylists.Columns["Specialty"] != null) gridTopStylists.Columns["Specialty"].FillWeight = 95;
                            if (gridTopStylists.Columns["Services Done"] != null) gridTopStylists.Columns["Services Done"].FillWeight = 60;
                            if (gridTopStylists.Columns["Revenue (Rs.)"] != null)
                            {
                                gridTopStylists.Columns["Revenue (Rs.)"].DefaultCellStyle.Format = "N2";
                                gridTopStylists.Columns["Revenue (Rs.)"].FillWeight = 80;
                            }
                        }
                    }
                }

                chartPanel.Invalidate();
            }
            catch
            {
                // Graceful fallback
            }
        }

        private void ChartPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Font fBold = new Font("Segoe UI", 10, FontStyle.Bold);
            Font fTitle = new Font("Segoe UI", 12, FontStyle.Bold);
            Font fRegular = new Font("Segoe UI", 9.5F, FontStyle.Regular);

            // Title
            g.DrawString("Revenue Contribution Share (Services vs Products)", fTitle, Brushes.White, 20, 20);

            decimal total = totalServiceSales + totalProductSales;
            if (total <= 0)
            {
                g.DrawString("No sales data recorded yet.\nStart billing services and retail beauty products to view analytics.", fRegular, Brushes.Gray, 30, 80);
                return;
            }

            float srvPct = (float)(totalServiceSales / total);
            float prdPct = (float)(totalProductSales / total);

            int barY = 70;
            int maxBarW = 420;

            // Bar 1: Services
            g.DrawString($"Salon Services: Rs. {totalServiceSales:N2} ({srvPct * 100:0.0}%)", fBold, Brushes.LightCyan, 20, barY);
            using (GraphicsPath path = Theme.GetRoundedPath(new Rectangle(20, barY + 24, Math.Max(12, (int)(maxBarW * srvPct)), 24), 6))
            using (Brush b = new SolidBrush(Theme.Accent))
            {
                g.FillPath(b, path);
            }

            // Bar 2: Retail Products
            int bar2Y = 145;
            g.DrawString($"Retail Beauty Products: Rs. {totalProductSales:N2} ({prdPct * 100:0.0}%)", fBold, Brushes.LightGreen, 20, bar2Y);
            using (GraphicsPath path = Theme.GetRoundedPath(new Rectangle(20, bar2Y + 24, Math.Max(12, (int)(maxBarW * prdPct)), 24), 6))
            using (Brush b = new SolidBrush(Theme.Success))
            {
                g.FillPath(b, path);
            }

            // Summary Stats box with rounded corners
            int boxY = 230;
            Rectangle boxRect = new Rectangle(20, boxY, 430, 220);
            using (GraphicsPath boxPath = Theme.GetRoundedPath(boxRect, 8))
            using (Brush b = new SolidBrush(Color.FromArgb(24, 33, 47)))
            using (Pen p = new Pen(Theme.CardBorder, 1))
            {
                g.FillPath(b, boxPath);
                g.DrawPath(p, boxPath);
            }

            g.DrawString("SALON EXECUTIVE SUMMARY", fBold, Brushes.White, 35, boxY + 16);
            g.DrawString($"• Gross Realized Turnover: Rs. {total:N2}", fRegular, Brushes.LightGray, 35, boxY + 48);
            g.DrawString($"• Services Share: {srvPct * 100:0.0}% of total turnover", fRegular, Brushes.LightGray, 35, boxY + 75);
            g.DrawString($"• Retail Products Share: {prdPct * 100:0.0}% of total turnover", fRegular, Brushes.LightGray, 35, boxY + 102);
            g.DrawString($"• Active Staff Team: {activeStaffCount} Specialists on roster", fRegular, Brushes.LightGray, 35, boxY + 129);
            g.DrawString($"• Today's Appointments: {todayApptCount} Scheduled ({todayInChair} in-chair)", fRegular, Brushes.LightGray, 35, boxY + 156);
            g.DrawString($"• Completed & Billed Today: {todayDone + todayBilled} Client sessions finished", fRegular, Brushes.LightGray, 35, boxY + 183);
        }
    }
}
