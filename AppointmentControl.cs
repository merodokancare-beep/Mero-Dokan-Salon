using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace MeroDokan
{
    public class AppointmentControl : UserControl
    {
        private DateTimePicker dtpFilterDate;
        private ComboBox comboFilterStatus;
        private ComboBox comboFilterStaff;

        private TextBox txtCustomerPhone;
        private TextBox txtCustomerName;
        
        // Multi-Service Selector Controls
        private Button btnSelectServices;
        private Label lblServiceSummary;
        private ToolStripDropDown serviceDropDown;
        private CheckedListBox chkListServices;
        private TextBox txtSearchServices;
        private Label lblPopupTotal;
        private Button btnPopupDone;
        private readonly List<ServiceItem> allServicesList = new List<ServiceItem>();
        private readonly List<int> selectedServiceIds = new List<int>();
        private readonly Dictionary<int, int> selectedServiceStaffMap = new Dictionary<int, int>();
        private readonly Dictionary<int, string> selectedServiceTimeMap = new Dictionary<int, string>();

        private FlowLayoutPanel pnlPerServiceStaff;
        private Label lblStaff;
        private ComboBox comboStaff;
        private Label lblDate;
        private DateTimePicker dtpApptDate;
        private Label lblTime;
        private ComboBox comboFromHour;
        private ComboBox comboFromMin;
        private Label lblTo;
        private ComboBox comboToHour;
        private ComboBox comboToMin;
        private bool isInternalTimeUpdating = false;
        private Label lblStatus;
        private ComboBox comboStatus;
        private Label lblNotes;
        private TextBox txtNotes;

        private Panel pnlClientHistory;
        private Label lblHistoryTitle;
        private DataGridView gridClientHistory;

        private DataGridView gridAppointments;
        private StylistScheduleBoardControl scheduleBoard;
        private Button btnViewTimeline;
        private Button btnViewList;
        private TextBox txtSearchTimeline;
        private Panel pnlBoardSummary;
        private Label lblBoardSummary;
        private Label lblCardTitle;
        private Button btnNewAppt;
        private Button btnBook;
        private Button btnClear;
        private Button btnDelete;
        private Button btnStatusInChair;
        private Button btnStatusCompleted;
        private Button btnCheckoutNow;

        private Label lblTotalBooked;
        private Label lblInChair;
        private Label lblCompleted;
        private Label lblCancelled;

        private Panel kpiCard;
        private Panel entryPanel;
        private Panel rightPanel;
        private Panel filterCard;
        private Button btnToggleBookingPanel;
        private Button btnCollapseLeft;
        private bool isLeftPanelCollapsed = false;

        private int selectedApptId = 0;
        private bool isSuppressingSelection = false;
        private bool isExplicitUserSelection = false;
        private bool isPopulatingList = false;
        private bool isSettingCustomerProgrammatically = false;
        private NoFocusToolStripDropDown customerSuggestionDropDown;
        private ToolStripControlHost customerSuggestionHost;
        private Panel pnlCustomerSuggestions;
        private ListBox lstCustomerSuggestions;
        private TextBox activeSuggestionTarget;

        private class NoFocusToolStripDropDown : ToolStripDropDown
        {
            private const int WS_EX_NOACTIVATE = 0x08000000;

            public NoFocusToolStripDropDown()
            {
                AutoClose = false;
                DoubleBuffered = true;
                ResizeRedraw = true;
                Margin = Padding.Empty;
                Padding = Padding.Empty;
            }

            protected override CreateParams CreateParams
            {
                get
                {
                    CreateParams cp = base.CreateParams;
                    cp.ExStyle |= WS_EX_NOACTIVATE;
                    return cp;
                }
            }
        }

        private class CustomerPhoneSuggestion
        {
            public string Phone { get; set; }
            public string Name { get; set; }
            public override string ToString()
            {
                return $"📞 {Phone}  —  👤 {Name}";
            }
        }

        private static readonly string[] HourOptions = {
            "06 AM", "07 AM", "08 AM", "09 AM", "10 AM", "11 AM",
            "12 PM", "01 PM", "02 PM", "03 PM", "04 PM", "05 PM",
            "06 PM", "07 PM", "08 PM", "09 PM", "10 PM", "11 PM",
            "12 AM", "01 AM", "02 AM", "03 AM", "04 AM", "05 AM"
        };

        private static readonly string[] MinuteOptions = {
            "00", "05", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55"
        };

        private static readonly string[] BaseStartTimes = {
            "10:00 AM", "10:30 AM", "11:00 AM", "11:30 AM", "12:00 PM", "12:30 PM",
            "01:00 PM", "01:30 PM", "02:00 PM", "02:30 PM", "03:00 PM", "03:30 PM",
            "04:00 PM", "04:30 PM", "05:00 PM", "05:30 PM", "06:00 PM", "06:30 PM",
            "07:00 PM", "07:30 PM", "08:00 PM"
        };

        private static List<string> GetTimeSlotsForDuration(int durationMinutes, DateTime? baseDate = null)
        {
            List<string> slots = new List<string>();
            DateTime d = (baseDate ?? DateTime.Today).Date;
            int dur = durationMinutes > 0 ? durationMinutes : 30;

            foreach (string startStr in BaseStartTimes)
            {
                DateTime startDt = ParseTimeSlotStatic(d, startStr);
                DateTime endDt = startDt.AddMinutes(dur);
                slots.Add($"{startDt:hh:mm tt} – {endDt:hh:mm tt}");
            }
            return slots;
        }

        private static DateTime ParseTimeSlotStatic(DateTime baseDate, string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return baseDate.Date.AddHours(10);
            string cleaned = timeStr;
            if (cleaned.Contains("–")) cleaned = cleaned.Split('–')[0].Trim();
            else if (cleaned.Contains("-")) cleaned = cleaned.Split('-')[0].Trim();

            if (DateTime.TryParseExact(cleaned, "hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, dt.Hour, dt.Minute, 0);
            }
            if (DateTime.TryParse(cleaned, out DateTime dt2))
            {
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, dt2.Hour, dt2.Minute, 0);
            }
            return baseDate.Date.AddHours(10);
        }

        // Event for MainForm to switch to Sales Billing with apptId, customer & all selected services pre-selected with their respective stylists, plus existing saleId if billed
        public event Action<int, int, List<Tuple<int, int>>, int> OnCheckoutRequested;

        public AppointmentControl()
        {
            InitializeComponent();
            BuildServiceDropdownPopup();
            BuildCustomerSuggestionPanel();
            LoadDropdowns();
            RefreshTimeSlots();
            isExplicitUserSelection = false;
            LoadAppointments();
            ResetForm();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            isExplicitUserSelection = false;
            ResetForm();
            if (txtCustomerPhone != null)
            {
                txtCustomerPhone.Focus();
            }
        }

        private void InitializeComponent()
        {
            this.Size = new Size(1020, 680);
            this.AutoScroll = true;
            this.BackColor = Theme.Secondary;

            // Page Title
            Label lblHeader = new Label();
            lblHeader.Text = "📅 Saloon Appointments & Client Queue";
            lblHeader.Location = new Point(20, 15);
            lblHeader.AutoSize = true;
            Theme.StyleLabel(lblHeader, Theme.TextLight, Theme.HeaderFont);
            this.Controls.Add(lblHeader);

            Label lblSubtitle = new Label();
            lblSubtitle.Text = "Schedule multi-service visits, manage stylist chairs, and instantly convert appointments to bills";
            lblSubtitle.Location = new Point(22, 45);
            lblSubtitle.AutoSize = true;
            Theme.StyleLabel(lblSubtitle, Theme.TextDark, Theme.MainFont);
            this.Controls.Add(lblSubtitle);

            // KPI Badges Card (Left Column Top, matches 350 width)
            kpiCard = Theme.CreateCard(350, 50);
            kpiCard.Location = new Point(20, 75);

            Font badgeFont = new Font("Segoe UI", 8F, FontStyle.Bold);

            lblTotalBooked = new Label();
            lblTotalBooked.Text = "📌 Booked: 0";
            lblTotalBooked.Location = new Point(8, 15);
            lblTotalBooked.AutoSize = true;
            Theme.StyleLabel(lblTotalBooked, Theme.Warning, badgeFont);
            kpiCard.Controls.Add(lblTotalBooked);

            lblInChair = new Label();
            lblInChair.Text = "🪑 Chair: 0";
            lblInChair.Location = new Point(96, 15);
            lblInChair.AutoSize = true;
            Theme.StyleLabel(lblInChair, Theme.Accent, badgeFont);
            kpiCard.Controls.Add(lblInChair);

            lblCompleted = new Label();
            lblCompleted.Text = "✅ Done: 0";
            lblCompleted.Location = new Point(176, 15);
            lblCompleted.AutoSize = true;
            Theme.StyleLabel(lblCompleted, Theme.Success, badgeFont);
            kpiCard.Controls.Add(lblCompleted);

            lblCancelled = new Label();
            lblCancelled.Text = "❌ Canc: 0";
            lblCancelled.Location = new Point(256, 15);
            lblCancelled.AutoSize = true;
            Theme.StyleLabel(lblCancelled, Theme.Danger, badgeFont);
            kpiCard.Controls.Add(lblCancelled);

            this.Controls.Add(kpiCard);

            // LEFT PANEL: Booking Entry Card
            entryPanel = Theme.CreateCard(350, 530);
            entryPanel.Location = new Point(20, 135);
            entryPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom;

            lblCardTitle = new Label();
            lblCardTitle.Text = "+ New Booking";
            lblCardTitle.Location = new Point(12, 10);
            lblCardTitle.Size = new Size(130, 26);
            lblCardTitle.AutoEllipsis = true;
            lblCardTitle.AutoSize = false;
            Theme.StyleLabel(lblCardTitle, Theme.TextLight, Theme.SubHeaderFont);
            entryPanel.Controls.Add(lblCardTitle);

            btnNewAppt = new Button();
            btnNewAppt.Text = "➕ New";
            btnNewAppt.Size = new Size(95, 26);
            btnNewAppt.Location = new Point(146, 10);
            Theme.StyleSuccessButton(btnNewAppt);
            btnNewAppt.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnNewAppt.Click += (s, e) => { SetLeftPanelCollapsed(false); ResetForm(); };
            entryPanel.Controls.Add(btnNewAppt);

            btnCollapseLeft = new Button();
            btnCollapseLeft.Text = "◀ Hide";
            btnCollapseLeft.Size = new Size(92, 26);
            btnCollapseLeft.Location = new Point(245, 10);
            Theme.StyleSecondaryButton(btnCollapseLeft);
            btnCollapseLeft.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            btnCollapseLeft.Click += (s, e) => SetLeftPanelCollapsed(true);
            entryPanel.Controls.Add(btnCollapseLeft);

            // 1. Client Phone (Auto-lookup on type/enter)
            Label lblCustPhone = new Label();
            lblCustPhone.Text = "Client Phone Number *";
            lblCustPhone.Location = new Point(15, 42);
            lblCustPhone.AutoSize = true;
            Theme.StyleLabel(lblCustPhone, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblCustPhone);

            txtCustomerPhone = new TextBox();
            txtCustomerPhone.Size = new Size(320, 26);
            txtCustomerPhone.Location = new Point(15, 60);
            Theme.StyleTextBox(txtCustomerPhone);
            txtCustomerPhone.TextChanged += TxtCustomerPhone_TextChanged;
            txtCustomerPhone.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Down)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.Items.Count > 0)
                    {
                        int nextIdx = lstCustomerSuggestions.SelectedIndex + 1;
                        if (nextIdx >= lstCustomerSuggestions.Items.Count) nextIdx = 0;
                        lstCustomerSuggestions.SelectedIndex = nextIdx;
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.Items.Count > 0)
                    {
                        int prevIdx = lstCustomerSuggestions.SelectedIndex - 1;
                        if (prevIdx < 0) prevIdx = lstCustomerSuggestions.Items.Count - 1;
                        lstCustomerSuggestions.SelectedIndex = prevIdx;
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible)
                    {
                        HideCustomerSuggestions();
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.SelectedIndex >= 0)
                    {
                        e.SuppressKeyPress = true;
                        ApplySelectedCustomerSuggestion();
                        return;
                    }

                    e.SuppressKeyPress = true;
                    string phone = txtCustomerPhone.Text.Trim();
                    LookupCustomerAndHistory(phone, updateNameOnlyIfEmpty: true);

                    if (string.IsNullOrEmpty(txtCustomerName.Text))
                    {
                        txtCustomerName.Focus();
                    }
                    else
                    {
                        btnSelectServices.Focus();
                    }
                }
            };
            txtCustomerPhone.Leave += (s, e) => {
                this.BeginInvoke(new Action(() => {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && !lstCustomerSuggestions.Focused && !pnlCustomerSuggestions.Focused && activeSuggestionTarget == txtCustomerPhone)
                    {
                        HideCustomerSuggestions();
                    }
                }));
            };
            entryPanel.Controls.Add(txtCustomerPhone);

            // 2. Client Name
            Label lblCustName = new Label();
            lblCustName.Text = "Client Name *";
            lblCustName.Location = new Point(15, 92);
            lblCustName.AutoSize = true;
            Theme.StyleLabel(lblCustName, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblCustName);

            txtCustomerName = new TextBox();
            txtCustomerName.Size = new Size(320, 26);
            txtCustomerName.Location = new Point(15, 110);
            Theme.StyleTextBox(txtCustomerName);
            txtCustomerName.TextChanged += TxtCustomerName_TextChanged;
            txtCustomerName.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Down)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.Items.Count > 0)
                    {
                        int nextIdx = lstCustomerSuggestions.SelectedIndex + 1;
                        if (nextIdx >= lstCustomerSuggestions.Items.Count) nextIdx = 0;
                        lstCustomerSuggestions.SelectedIndex = nextIdx;
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Up)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.Items.Count > 0)
                    {
                        int prevIdx = lstCustomerSuggestions.SelectedIndex - 1;
                        if (prevIdx < 0) prevIdx = lstCustomerSuggestions.Items.Count - 1;
                        lstCustomerSuggestions.SelectedIndex = prevIdx;
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible)
                    {
                        HideCustomerSuggestions();
                        e.SuppressKeyPress = true;
                        return;
                    }
                }
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
                {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && lstCustomerSuggestions != null && lstCustomerSuggestions.SelectedIndex >= 0)
                    {
                        e.SuppressKeyPress = true;
                        ApplySelectedCustomerSuggestion();
                        return;
                    }

                    e.SuppressKeyPress = true;
                    btnSelectServices.Focus();
                }
            };
            txtCustomerName.Leave += (s, e) => {
                this.BeginInvoke(new Action(() => {
                    if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible && !lstCustomerSuggestions.Focused && !pnlCustomerSuggestions.Focused && activeSuggestionTarget == txtCustomerName)
                    {
                        HideCustomerSuggestions();
                    }
                }));
            };
            entryPanel.Controls.Add(txtCustomerName);

            // 3. Multi-Service Selection
            Label lblSrv = new Label();
            lblSrv.Text = "Requested Services * (Multi-Select)";
            lblSrv.Location = new Point(15, 142);
            lblSrv.AutoSize = true;
            Theme.StyleLabel(lblSrv, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblSrv);

            btnSelectServices = new Button();
            btnSelectServices.Size = new Size(320, 30);
            btnSelectServices.Location = new Point(15, 160);
            btnSelectServices.Text = "👉 Click to Select Services...";
            btnSelectServices.TextAlign = ContentAlignment.MiddleLeft;
            btnSelectServices.FlatStyle = FlatStyle.Flat;
            btnSelectServices.FlatAppearance.BorderColor = Theme.CardBorder;
            btnSelectServices.FlatAppearance.BorderSize = 1;
            btnSelectServices.BackColor = Theme.CardBg;
            btnSelectServices.ForeColor = Theme.TextLight;
            btnSelectServices.Font = Theme.MainFont;
            btnSelectServices.Cursor = Cursors.Hand;
            btnSelectServices.Click += BtnSelectServices_Click;
            entryPanel.Controls.Add(btnSelectServices);

            lblServiceSummary = new Label();
            lblServiceSummary.Text = "✨ 0 services selected (Rs. 0 • 0 mins)";
            lblServiceSummary.Location = new Point(15, 192);
            lblServiceSummary.Size = new Size(320, 18);
            lblServiceSummary.AutoEllipsis = true;
            Theme.StyleLabel(lblServiceSummary, Theme.Accent, new Font("Segoe UI", 8F, FontStyle.Italic));
            entryPanel.Controls.Add(lblServiceSummary);

            // 4. Stylist (Primary / Default)
            lblStaff = new Label();
            lblStaff.Text = "Default / Primary Stylist";
            lblStaff.Location = new Point(15, 214);
            lblStaff.AutoSize = true;
            Theme.StyleLabel(lblStaff, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblStaff);

            comboStaff = new ComboBox();
            comboStaff.Size = new Size(320, 26);
            comboStaff.Location = new Point(15, 232);
            comboStaff.DropDownStyle = ComboBoxStyle.DropDownList;
            Theme.StyleComboBox(comboStaff);
            comboStaff.SelectedIndexChanged += (s, e) => {
                if (comboStaff.SelectedItem is ComboBoxItem itm && itm.Id > 0)
                {
                    foreach (int sId in selectedServiceIds)
                    {
                        if (selectedServiceIds.Count == 1 || !selectedServiceStaffMap.ContainsKey(sId) || selectedServiceStaffMap[sId] <= 0)
                        {
                            selectedServiceStaffMap[sId] = itm.Id;
                        }
                    }
                    UpdateServiceSelectionUI();
                }
            };
            entryPanel.Controls.Add(comboStaff);

            // 4b. Dynamic Per-Service Stylist Assignment Panel
            pnlPerServiceStaff = new FlowLayoutPanel();
            pnlPerServiceStaff.Size = new Size(320, 10);
            pnlPerServiceStaff.Location = new Point(15, 264);
            pnlPerServiceStaff.AutoSize = true;
            pnlPerServiceStaff.FlowDirection = FlowDirection.TopDown;
            pnlPerServiceStaff.WrapContents = false;
            pnlPerServiceStaff.BackColor = Color.Transparent;
            pnlPerServiceStaff.Visible = false;
            entryPanel.Controls.Add(pnlPerServiceStaff);

            // 5. Date
            lblDate = new Label();
            lblDate.Text = "Date *";
            lblDate.Location = new Point(15, 280);
            lblDate.AutoSize = true;
            Theme.StyleLabel(lblDate, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblDate);

            dtpApptDate = new DateTimePicker();
            dtpApptDate.Format = DateTimePickerFormat.Short;
            dtpApptDate.Size = new Size(320, 26);
            dtpApptDate.Location = new Point(15, 298);
            dtpApptDate.Font = Theme.MainFont;
            dtpApptDate.MinDate = DateTime.Today;
            dtpApptDate.Value = DateTime.Today;
            dtpApptDate.ValueChanged += (s, e) => {
                UpdateStatusOptionsForDate(dtpApptDate.Value);
            };
            entryPanel.Controls.Add(dtpApptDate);

            // Visible Time Slot & Duration Controls
            lblTime = new Label();
            lblTime.Text = "Time Slot & Duration *";
            lblTime.AutoSize = true;
            Theme.StyleLabel(lblTime, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblTime);

            comboFromHour = new ComboBox();
            comboFromHour.Size = new Size(82, 26);
            comboFromHour.DropDownStyle = ComboBoxStyle.DropDownList;
            comboFromHour.Items.AddRange(HourOptions);
            comboFromHour.SelectedItem = "10 AM";
            Theme.StyleComboBox(comboFromHour);
            comboFromHour.SelectedIndexChanged += (s, e) => OnFromTimeChanged();
            entryPanel.Controls.Add(comboFromHour);

            comboFromMin = new ComboBox();
            comboFromMin.Size = new Size(54, 26);
            comboFromMin.DropDownStyle = ComboBoxStyle.DropDownList;
            comboFromMin.Items.AddRange(MinuteOptions);
            comboFromMin.SelectedItem = "00";
            Theme.StyleComboBox(comboFromMin);
            comboFromMin.SelectedIndexChanged += (s, e) => OnFromTimeChanged();
            entryPanel.Controls.Add(comboFromMin);

            lblTo = new Label();
            lblTo.Text = "to";
            lblTo.AutoSize = true;
            Theme.StyleLabel(lblTo, Theme.TextDark, Theme.MainFont);
            entryPanel.Controls.Add(lblTo);

            comboToHour = new ComboBox();
            comboToHour.Size = new Size(82, 26);
            comboToHour.DropDownStyle = ComboBoxStyle.DropDownList;
            comboToHour.Items.AddRange(HourOptions);
            comboToHour.SelectedItem = "10 AM";
            Theme.StyleComboBox(comboToHour);
            comboToHour.SelectedIndexChanged += (s, e) => OnToTimeChanged();
            entryPanel.Controls.Add(comboToHour);

            comboToMin = new ComboBox();
            comboToMin.Size = new Size(54, 26);
            comboToMin.DropDownStyle = ComboBoxStyle.DropDownList;
            comboToMin.Items.AddRange(MinuteOptions);
            comboToMin.SelectedItem = "30";
            Theme.StyleComboBox(comboToMin);
            comboToMin.SelectedIndexChanged += (s, e) => OnToTimeChanged();
            entryPanel.Controls.Add(comboToMin);

            // 6. Status
            lblStatus = new Label();
            lblStatus.Text = "Current Appointment Status";
            lblStatus.Location = new Point(15, 380);
            lblStatus.AutoSize = true;
            Theme.StyleLabel(lblStatus, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblStatus);

            comboStatus = new ComboBox();
            comboStatus.Size = new Size(320, 26);
            comboStatus.Location = new Point(15, 398);
            comboStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            comboStatus.Items.AddRange(new object[] { "Booked", "In-Chair", "Completed", "Billed", "Cancelled" });
            comboStatus.SelectedIndex = 0;
            Theme.StyleComboBox(comboStatus);
            entryPanel.Controls.Add(comboStatus);

            // 7. Notes
            lblNotes = new Label();
            lblNotes.Text = "Client Notes / Requests";
            lblNotes.Location = new Point(15, 430);
            lblNotes.AutoSize = true;
            Theme.StyleLabel(lblNotes, Theme.TextLight, Theme.BoldFont);
            entryPanel.Controls.Add(lblNotes);

            txtNotes = new TextBox();
            txtNotes.Size = new Size(320, 36);
            txtNotes.Location = new Point(15, 448);
            txtNotes.Multiline = true;
            Theme.StyleTextBox(txtNotes);
            entryPanel.Controls.Add(txtNotes);

            // Action Buttons
            btnBook = new Button();
            btnBook.Text = "+ Book Appointment";
            btnBook.Size = new Size(160, 38);
            btnBook.Location = new Point(15, 496);
            Theme.StyleSuccessButton(btnBook);
            btnBook.Click += BtnBook_Click;
            entryPanel.Controls.Add(btnBook);

            btnClear = new Button();
            btnClear.Text = "🔄 Clear / New";
            btnClear.Size = new Size(150, 38);
            btnClear.Location = new Point(185, 496);
            Theme.StyleSecondaryButton(btnClear);
            btnClear.Click += (s, e) => ResetForm();
            entryPanel.Controls.Add(btnClear);

            // 8. Client Past History Panel (Below Action Buttons)
            pnlClientHistory = new Panel();
            pnlClientHistory.Size = new Size(320, 185);
            pnlClientHistory.Location = new Point(15, 546);
            pnlClientHistory.BackColor = Theme.CardBg;
            pnlClientHistory.Visible = false;
            pnlClientHistory.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.CardBorder, 1))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, pnlClientHistory.Width - 1, pnlClientHistory.Height - 1);
                }
            };

            lblHistoryTitle = new Label();
            lblHistoryTitle.Text = "📜 Past Client History & Visits";
            lblHistoryTitle.Location = new Point(8, 6);
            lblHistoryTitle.Size = new Size(304, 18);
            Theme.StyleLabel(lblHistoryTitle, Theme.Accent, new Font("Segoe UI Semibold", 8F, FontStyle.Bold));
            pnlClientHistory.Controls.Add(lblHistoryTitle);

            gridClientHistory = new DataGridView();
            gridClientHistory.Location = new Point(6, 26);
            gridClientHistory.Size = new Size(308, 150);
            gridClientHistory.AllowUserToAddRows = false;
            gridClientHistory.AllowUserToDeleteRows = false;
            gridClientHistory.ReadOnly = true;
            gridClientHistory.RowHeadersVisible = false;
            gridClientHistory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridClientHistory.MultiSelect = false;
            gridClientHistory.BackgroundColor = Color.FromArgb(15, 23, 42);
            gridClientHistory.BorderStyle = BorderStyle.None;
            gridClientHistory.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            gridClientHistory.GridColor = Color.FromArgb(51, 65, 85);
            gridClientHistory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
            gridClientHistory.ColumnHeadersDefaultCellStyle.ForeColor = Theme.TextLight;
            gridClientHistory.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 7.5F, FontStyle.Bold);
            gridClientHistory.DefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42);
            gridClientHistory.DefaultCellStyle.ForeColor = Theme.TextLight;
            gridClientHistory.DefaultCellStyle.Font = new Font("Segoe UI", 7.5F);
            gridClientHistory.DefaultCellStyle.SelectionBackColor = Color.FromArgb(59, 130, 246);
            gridClientHistory.DefaultCellStyle.SelectionForeColor = Color.White;
            gridClientHistory.RowTemplate.Height = 26;
            gridClientHistory.ColumnHeadersHeight = 24;
            gridClientHistory.EnableHeadersVisualStyles = false;
            gridClientHistory.CellContentClick += GridClientHistory_CellContentClick;
            pnlClientHistory.Controls.Add(gridClientHistory);

            entryPanel.Controls.Add(pnlClientHistory);

            entryPanel.AutoScroll = true;

            this.Controls.Add(entryPanel);

            // RIGHT PANEL: Filter Controls and Appointments Grid / Timeline
            rightPanel = new Panel();
            rightPanel.Location = new Point(385, 75);
            rightPanel.Size = new Size(615, 575);
            rightPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            // Top Filter Bar Card
            filterCard = Theme.CreateCard(615, 50);
            filterCard.Location = new Point(0, 0);
            filterCard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            btnToggleBookingPanel = new Button();
            btnToggleBookingPanel.Text = "◀ Hide Form";
            btnToggleBookingPanel.Size = new Size(110, 30);
            btnToggleBookingPanel.Location = new Point(8, 10);
            btnToggleBookingPanel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnToggleBookingPanel.BackColor = Color.FromArgb(30, 41, 59);
            btnToggleBookingPanel.ForeColor = Theme.TextDark;
            btnToggleBookingPanel.FlatStyle = FlatStyle.Flat;
            btnToggleBookingPanel.FlatAppearance.BorderSize = 0;
            btnToggleBookingPanel.Cursor = Cursors.Hand;
            btnToggleBookingPanel.Padding = new Padding(2, 0, 2, 0);
            btnToggleBookingPanel.Click += (s, e) => ToggleLeftPanel();
            filterCard.Controls.Add(btnToggleBookingPanel);

            Button btnPrevDay = new Button();
            btnPrevDay.Text = "◀";
            btnPrevDay.Size = new Size(30, 30);
            btnPrevDay.Location = new Point(124, 10);
            btnPrevDay.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnPrevDay.Padding = new Padding(0);
            Theme.StylePrimaryButton(btnPrevDay);
            btnPrevDay.Click += (s, e) => { dtpFilterDate.Value = dtpFilterDate.Value.AddDays(-1); };
            filterCard.Controls.Add(btnPrevDay);

            dtpFilterDate = new DateTimePicker();
            dtpFilterDate.Format = DateTimePickerFormat.Short;
            dtpFilterDate.Size = new Size(105, 30);
            dtpFilterDate.Location = new Point(158, 10);
            dtpFilterDate.Font = Theme.MainFont;
            dtpFilterDate.ValueChanged += (s, e) => LoadAppointments();
            filterCard.Controls.Add(dtpFilterDate);

            Button btnNextDay = new Button();
            btnNextDay.Text = "▶";
            btnNextDay.Size = new Size(30, 30);
            btnNextDay.Location = new Point(267, 10);
            btnNextDay.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnNextDay.Padding = new Padding(0);
            Theme.StylePrimaryButton(btnNextDay);
            btnNextDay.Click += (s, e) => { dtpFilterDate.Value = dtpFilterDate.Value.AddDays(1); };
            filterCard.Controls.Add(btnNextDay);

            Button btnToday = new Button();
            btnToday.Text = "Today";
            btnToday.Size = new Size(76, 30);
            btnToday.Location = new Point(301, 10);
            btnToday.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnToday.Padding = new Padding(0);
            Theme.StylePrimaryButton(btnToday);
            btnToday.Click += (s, e) => { dtpFilterDate.Value = DateTime.Today; };
            filterCard.Controls.Add(btnToday);

            // View Switcher Buttons
            btnViewTimeline = new Button();
            btnViewTimeline.Text = "Timeline";
            btnViewTimeline.Size = new Size(84, 30);
            btnViewTimeline.Location = new Point(383, 10);
            btnViewTimeline.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnViewTimeline.FlatStyle = FlatStyle.Flat;
            btnViewTimeline.FlatAppearance.BorderSize = 0;
            btnViewTimeline.BackColor = Theme.Accent;
            btnViewTimeline.ForeColor = Color.White;
            btnViewTimeline.Padding = new Padding(0);
            btnViewTimeline.Click += (s, e) => SetViewMode(true);
            filterCard.Controls.Add(btnViewTimeline);

            btnViewList = new Button();
            btnViewList.Text = "List View";
            btnViewList.Size = new Size(82, 30);
            btnViewList.Location = new Point(471, 10);
            btnViewList.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            btnViewList.BackColor = Color.FromArgb(30, 41, 59);
            btnViewList.ForeColor = Theme.TextDark;
            btnViewList.FlatStyle = FlatStyle.Flat;
            btnViewList.FlatAppearance.BorderSize = 0;
            btnViewList.Padding = new Padding(0);
            btnViewList.Click += (s, e) => SetViewMode(false);
            filterCard.Controls.Add(btnViewList);

            // Search box
            txtSearchTimeline = new TextBox();
            txtSearchTimeline.Size = new Size(110, 30);
            txtSearchTimeline.Location = new Point(557, 10);
            txtSearchTimeline.Font = Theme.MainFont;
            txtSearchTimeline.Text = "Search...";
            txtSearchTimeline.ForeColor = Color.Gray;
            txtSearchTimeline.Enter += (s, e) => {
                if (txtSearchTimeline.Text == "Search...") { txtSearchTimeline.Text = ""; txtSearchTimeline.ForeColor = Theme.TextLight; }
            };
            txtSearchTimeline.Leave += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtSearchTimeline.Text)) { txtSearchTimeline.Text = "Search..."; txtSearchTimeline.ForeColor = Color.Gray; }
            };
            txtSearchTimeline.TextChanged += (s, e) => {
                string q = (txtSearchTimeline.Text == "Search...") ? "" : txtSearchTimeline.Text.Trim();
                if (scheduleBoard != null) scheduleBoard.SetSearchQuery(q);
            };
            Theme.StyleTextBox(txtSearchTimeline);
            filterCard.Controls.Add(txtSearchTimeline);

            comboFilterStatus = new ComboBox();
            comboFilterStatus.Size = new Size(95, 30);
            comboFilterStatus.Location = new Point(673, 10);
            comboFilterStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            comboFilterStatus.Items.AddRange(new object[] { "All", "Booked", "In-Chair", "Completed", "Billed", "Cancelled" });
            comboFilterStatus.SelectedIndex = 0;
            Theme.StyleComboBox(comboFilterStatus);
            comboFilterStatus.SelectedIndexChanged += (s, e) => LoadAppointments();
            filterCard.Controls.Add(comboFilterStatus);

            comboFilterStaff = new ComboBox();
            comboFilterStaff.Size = new Size(10, 26);
            comboFilterStaff.Visible = false;
            filterCard.Controls.Add(comboFilterStaff);

            rightPanel.Controls.Add(filterCard);

            // 1. Zenoti Stylist Schedule Board Control (Default View)
            scheduleBoard = new StylistScheduleBoardControl();
            scheduleBoard.Location = new Point(0, 56);
            scheduleBoard.Size = new Size(615, 416);
            scheduleBoard.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            scheduleBoard.AppointmentSelected += (appt) => { if (appt != null) SelectAppointmentById(appt.Id); };
            scheduleBoard.AppointmentDoubleClicked += (appt) => { 
                if (appt != null) { 
                    SelectAppointmentById(appt.Id); 
                    BtnCheckoutNow_Click(this, EventArgs.Empty); 
                } 
            };
            scheduleBoard.AppointmentDurationChanged += (appt, newDur, newEnd) => {
                UpdateAppointmentDuration(appt, newDur, newEnd);
            };
            scheduleBoard.AppointmentMoved += (appt, newStaffId, newStart, newEnd) => {
                UpdateAppointmentSlotAndStaff(appt, newStaffId, newStart, newEnd);
            };
            scheduleBoard.AppointmentStatusChangeRequested += (appt, newStatus) => {
                if (appt != null) {
                    SelectAppointmentById(appt.Id);
                    UpdateSelectedStatus(newStatus, appt.SpecificServiceId);
                }
            };
            scheduleBoard.AppointmentCheckoutRequested += (appt) => {
                if (appt != null) {
                    SelectAppointmentById(appt.Id);
                    BtnCheckoutNow_Click(this, EventArgs.Empty);
                }
            };
            scheduleBoard.AppointmentDeleteRequested += (appt) => {
                if (appt != null) {
                    SelectAppointmentById(appt.Id);
                    BtnDelete_Click(this, EventArgs.Empty);
                }
            };
            scheduleBoard.EmptySlotClicked += (staffId, slotTime) => { QuickBookSlot(staffId, slotTime); };
            scheduleBoard.EmptySlotDoubleClicked += (staffId, slotTime) => { QuickBookSlot(staffId, slotTime); };
            rightPanel.Controls.Add(scheduleBoard);

            // 2. DataGridView (Table List View)
            gridAppointments = new DataGridView();
            gridAppointments.Location = new Point(0, 56);
            gridAppointments.Size = new Size(615, 416);
            gridAppointments.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            gridAppointments.Visible = false;
            Theme.StyleGrid(gridAppointments);
            gridAppointments.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridAppointments.MultiSelect = false;
            gridAppointments.CellClick += (s, e) => { if (e.RowIndex >= 0) isExplicitUserSelection = true; };
            gridAppointments.CellMouseDown += (s, e) => { if (e.RowIndex >= 0) isExplicitUserSelection = true; };
            gridAppointments.SelectionChanged += GridAppointments_SelectionChanged;
            gridAppointments.CellFormatting += GridAppointments_CellFormatting;
            rightPanel.Controls.Add(gridAppointments);

            // 3. Summary Footer Bar
            pnlBoardSummary = new Panel();
            pnlBoardSummary.Location = new Point(0, 476);
            pnlBoardSummary.Size = new Size(615, 34);
            pnlBoardSummary.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            pnlBoardSummary.BackColor = Color.FromArgb(17, 24, 39);
            pnlBoardSummary.Padding = new Padding(8, 4, 8, 4);

            lblBoardSummary = new Label();
            lblBoardSummary.Dock = DockStyle.Fill;
            lblBoardSummary.TextAlign = ContentAlignment.MiddleLeft;
            lblBoardSummary.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            lblBoardSummary.ForeColor = Theme.Accent;
            lblBoardSummary.Text = "📊 Summary: Loading metrics...";
            pnlBoardSummary.Controls.Add(lblBoardSummary);
            rightPanel.Controls.Add(pnlBoardSummary);

            // 4. Bottom Actions Panel
            Panel actionsPanel = new Panel();
            actionsPanel.Location = new Point(0, 515);
            actionsPanel.Size = new Size(615, 50);
            actionsPanel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;

            btnStatusInChair = new Button();
            btnStatusInChair.Text = "🪑 In-Chair";
            btnStatusInChair.Size = new Size(130, 42);
            btnStatusInChair.Location = new Point(0, 4);
            Theme.StylePrimaryButton(btnStatusInChair);
            btnStatusInChair.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnStatusInChair.Padding = new Padding(2, 0, 2, 0);
            btnStatusInChair.Click += (s, e) => UpdateSelectedStatus("In-Chair");
            actionsPanel.Controls.Add(btnStatusInChair);

            btnStatusCompleted = new Button();
            btnStatusCompleted.Text = "✅ Completed";
            btnStatusCompleted.Size = new Size(140, 42);
            btnStatusCompleted.Location = new Point(138, 4);
            Theme.StyleSuccessButton(btnStatusCompleted);
            btnStatusCompleted.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnStatusCompleted.Padding = new Padding(2, 0, 2, 0);
            btnStatusCompleted.Click += (s, e) => UpdateSelectedStatus("Completed");
            actionsPanel.Controls.Add(btnStatusCompleted);

            btnCheckoutNow = new Button();
            btnCheckoutNow.Text = "🚀 🧾 Bill / Checkout";
            btnCheckoutNow.Size = new Size(220, 42);
            btnCheckoutNow.Location = new Point(286, 4);
            Theme.StyleSuccessButton(btnCheckoutNow);
            btnCheckoutNow.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnCheckoutNow.Padding = new Padding(2, 0, 2, 0);
            btnCheckoutNow.BackColor = Theme.Accent;
            btnCheckoutNow.Click += BtnCheckoutNow_Click;
            actionsPanel.Controls.Add(btnCheckoutNow);

            btnDelete = new Button();
            btnDelete.Text = "🗑️ Delete";
            btnDelete.Size = new Size(115, 42);
            btnDelete.Location = new Point(514, 4);
            Theme.StyleDangerButton(btnDelete);
            btnDelete.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            btnDelete.Padding = new Padding(2, 0, 2, 0);
            btnDelete.Click += BtnDelete_Click;
            actionsPanel.Controls.Add(btnDelete);

            Button btnReload = new Button();
            btnReload.Text = "🔄";
            btnReload.Size = new Size(55, 42);
            btnReload.Location = new Point(637, 4);
            Theme.StylePrimaryButton(btnReload);
            btnReload.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            btnReload.Padding = new Padding(0);
            btnReload.Click += (s, e) => { LoadDropdowns(); LoadAppointments(); };
            actionsPanel.Controls.Add(btnReload);

            rightPanel.Controls.Add(actionsPanel);

            this.Controls.Add(rightPanel);
            ApplyResponsiveLayout();
        }

        public void SetLeftPanelCollapsed(bool collapsed)
        {
            isLeftPanelCollapsed = collapsed;
            HideCustomerSuggestions();
            if (serviceDropDown != null && serviceDropDown.Visible)
            {
                serviceDropDown.Close();
            }
            this.SuspendLayout();
            if (collapsed)
            {
                if (kpiCard != null) kpiCard.Visible = false;
                if (entryPanel != null) entryPanel.Visible = false;
                if (btnToggleBookingPanel != null)
                {
                    btnToggleBookingPanel.Text = "➕ Show Form";
                    btnToggleBookingPanel.BackColor = Theme.Success;
                    btnToggleBookingPanel.ForeColor = Color.White;
                }
            }
            else
            {
                if (kpiCard != null) kpiCard.Visible = true;
                if (entryPanel != null) entryPanel.Visible = true;
                if (btnToggleBookingPanel != null)
                {
                    btnToggleBookingPanel.Text = "◀ Hide Form";
                    btnToggleBookingPanel.BackColor = Color.FromArgb(30, 41, 59);
                    btnToggleBookingPanel.ForeColor = Theme.TextDark;
                }
            }
            ApplyResponsiveLayout();
            this.ResumeLayout(true);
            if (scheduleBoard != null && scheduleBoard.Visible)
            {
                scheduleBoard.Invalidate();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!this.Visible)
            {
                HideCustomerSuggestions();
                if (serviceDropDown != null && serviceDropDown.Visible)
                    serviceDropDown.Close();
            }
        }

        public void ToggleLeftPanel()
        {
            SetLeftPanelCollapsed(!isLeftPanelCollapsed);
        }

        private void ApplyResponsiveLayout()
        {
            if (rightPanel == null) return;

            int topOffset = 75;
            int bottomMargin = 20;
            int totalWidth = this.ClientSize.Width;
            int totalHeight = this.ClientSize.Height;

            rightPanel.Anchor = AnchorStyles.None;

            if (isLeftPanelCollapsed)
            {
                rightPanel.Location = new Point(20, topOffset);
                rightPanel.Size = new Size(Math.Max(500, totalWidth - 40), Math.Max(400, totalHeight - topOffset - bottomMargin));
            }
            else
            {
                if (kpiCard != null)
                {
                    kpiCard.Location = new Point(20, topOffset);
                    kpiCard.Size = new Size(350, 50);
                }
                if (entryPanel != null)
                {
                    entryPanel.Location = new Point(20, 135);
                    entryPanel.Size = new Size(350, Math.Max(400, totalHeight - 135 - bottomMargin));
                }
                rightPanel.Location = new Point(385, topOffset);
                rightPanel.Size = new Size(Math.Max(500, totalWidth - 405), Math.Max(400, totalHeight - topOffset - bottomMargin));
            }

            rightPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyResponsiveLayout();
        }

        private class ServiceItem
        {
            public int Id { get; set; }
            public string Code { get; set; } = "";
            public string Name { get; set; }
            public decimal Price { get; set; }
            public int DurationMinutes { get; set; }
            public string SACCode { get; set; } = "999721";
            public decimal GSTRate { get; set; } = 18.00m;
            public override string ToString() => $"{Name} (Rs. {Price:N0}, {DurationMinutes}m)";
        }

        private class ComboBoxItem
        {
            public int Id { get; set; }
            public string Display { get; set; }
            public override string ToString() => Display;
        }

        private void BuildServiceDropdownPopup()
        {
            Panel popupPanel = new Panel();
            popupPanel.Size = new Size(340, 360);
            popupPanel.BackColor = Theme.Secondary;
            popupPanel.Padding = new Padding(8);
            popupPanel.BorderStyle = BorderStyle.FixedSingle;

            // Search Box
            txtSearchServices = new TextBox();
            txtSearchServices.Location = new Point(10, 10);
            txtSearchServices.Size = new Size(318, 26);
            txtSearchServices.Font = Theme.MainFont;
            txtSearchServices.ForeColor = Color.Gray;
            txtSearchServices.Text = "🔍 Search services...";
            txtSearchServices.Enter += (s, e) => {
                if (txtSearchServices.Text == "🔍 Search services...")
                {
                    txtSearchServices.Text = "";
                    txtSearchServices.ForeColor = Theme.TextLight;
                }
            };
            txtSearchServices.Leave += (s, e) => {
                if (string.IsNullOrWhiteSpace(txtSearchServices.Text))
                {
                    txtSearchServices.Text = "🔍 Search services...";
                    txtSearchServices.ForeColor = Color.Gray;
                }
            };
            txtSearchServices.TextChanged += (s, e) => {
                if (txtSearchServices.Text != "🔍 Search services...")
                {
                    FilterServiceCheckedList(txtSearchServices.Text.Trim());
                }
            };
            Theme.StyleTextBox(txtSearchServices);
            popupPanel.Controls.Add(txtSearchServices);

            // Quick Selection buttons
            Button btnSelectAll = new Button();
            btnSelectAll.Text = "Select All";
            btnSelectAll.Size = new Size(80, 24);
            btnSelectAll.Location = new Point(10, 42);
            btnSelectAll.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            Theme.StylePrimaryButton(btnSelectAll);
            btnSelectAll.Click += (s, e) => {
                for (int i = 0; i < chkListServices.Items.Count; i++)
                {
                    chkListServices.SetItemChecked(i, true);
                }
            };
            popupPanel.Controls.Add(btnSelectAll);

            Button btnClearAll = new Button();
            btnClearAll.Text = "Clear";
            btnClearAll.Size = new Size(60, 24);
            btnClearAll.Location = new Point(95, 42);
            btnClearAll.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            Theme.StyleSecondaryButton(btnClearAll);
            btnClearAll.Click += (s, e) => {
                for (int i = 0; i < chkListServices.Items.Count; i++)
                {
                    chkListServices.SetItemChecked(i, false);
                }
            };
            popupPanel.Controls.Add(btnClearAll);

            Label lblHint = new Label();
            lblHint.Text = "✓ Check multiple services";
            lblHint.Location = new Point(165, 46);
            lblHint.AutoSize = true;
            Theme.StyleLabel(lblHint, Theme.TextDark, new Font("Segoe UI", 7.5F, FontStyle.Italic));
            popupPanel.Controls.Add(lblHint);

            // CheckedListBox
            chkListServices = new CheckedListBox();
            chkListServices.Location = new Point(10, 72);
            chkListServices.Size = new Size(318, 220);
            chkListServices.BackColor = Theme.CardBg;
            chkListServices.ForeColor = Theme.TextLight;
            chkListServices.BorderStyle = BorderStyle.FixedSingle;
            chkListServices.Font = Theme.MainFont;
            chkListServices.CheckOnClick = true;
            chkListServices.ItemCheck += ChkListServices_ItemCheck;
            popupPanel.Controls.Add(chkListServices);

            // Bottom summary & Done button
            lblPopupTotal = new Label();
            lblPopupTotal.Text = "Selected: 0 | Rs. 0";
            lblPopupTotal.Location = new Point(10, 302);
            lblPopupTotal.Size = new Size(220, 48);
            Theme.StyleLabel(lblPopupTotal, Theme.Success, Theme.BoldFont);
            popupPanel.Controls.Add(lblPopupTotal);

            btnPopupDone = new Button();
            btnPopupDone.Text = "✔ Done";
            btnPopupDone.Size = new Size(90, 30);
            btnPopupDone.Location = new Point(238, 302);
            Theme.StyleSuccessButton(btnPopupDone);
            btnPopupDone.Click += (s, e) => serviceDropDown.Close();
            popupPanel.Controls.Add(btnPopupDone);

            ToolStripControlHost host = new ToolStripControlHost(popupPanel) {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = popupPanel.Size
            };

            serviceDropDown = new ToolStripDropDown();
            serviceDropDown.AutoClose = true;
            serviceDropDown.Margin = Padding.Empty;
            serviceDropDown.Padding = Padding.Empty;
            serviceDropDown.Items.Add(host);
        }

        private void BuildCustomerSuggestionPanel()
        {
            lstCustomerSuggestions = new ListBox();
            lstCustomerSuggestions.Dock = DockStyle.Fill;
            lstCustomerSuggestions.BackColor = Color.FromArgb(15, 23, 42);
            lstCustomerSuggestions.ForeColor = Color.White;
            lstCustomerSuggestions.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Regular);
            lstCustomerSuggestions.BorderStyle = BorderStyle.None;
            lstCustomerSuggestions.ItemHeight = 28;
            lstCustomerSuggestions.DrawMode = DrawMode.OwnerDrawFixed;
            lstCustomerSuggestions.DrawItem += (s, e) => {
                if (e.Index < 0 || e.Index >= lstCustomerSuggestions.Items.Count) return;
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                using (SolidBrush bgBrush = new SolidBrush(isSelected ? Theme.Accent : Color.FromArgb(15, 23, 42)))
                {
                    e.Graphics.FillRectangle(bgBrush, e.Bounds);
                }
                if (lstCustomerSuggestions.Items[e.Index] is CustomerPhoneSuggestion sug)
                {
                    using (SolidBrush phoneBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(56, 189, 248)))
                    using (SolidBrush nameBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(241, 245, 249)))
                    using (Font fBold = new Font("Segoe UI", 9F, FontStyle.Bold))
                    using (Font fReg = new Font("Segoe UI", 8.5F, FontStyle.Regular))
                    {
                        e.Graphics.DrawString(sug.Phone, fBold, phoneBrush, e.Bounds.X + 8, e.Bounds.Y + 5);
                        e.Graphics.DrawString(sug.Name, fReg, nameBrush, e.Bounds.X + 115, e.Bounds.Y + 6);
                    }
                }
                if (isSelected)
                {
                    e.DrawFocusRectangle();
                }
            };

            lstCustomerSuggestions.MouseDown += (s, e) => {
                int idx = lstCustomerSuggestions.IndexFromPoint(e.Location);
                if (idx >= 0 && idx < lstCustomerSuggestions.Items.Count)
                {
                    lstCustomerSuggestions.SelectedIndex = idx;
                    ApplySelectedCustomerSuggestion();
                }
            };
            lstCustomerSuggestions.Click += (s, e) => ApplySelectedCustomerSuggestion();
            lstCustomerSuggestions.DoubleClick += (s, e) => ApplySelectedCustomerSuggestion();
            lstCustomerSuggestions.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
                {
                    e.SuppressKeyPress = true;
                    ApplySelectedCustomerSuggestion();
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    HideCustomerSuggestions();
                    activeSuggestionTarget?.Focus();
                }
            };

            pnlCustomerSuggestions = new Panel();
            pnlCustomerSuggestions.Size = new Size(320, 140);
            pnlCustomerSuggestions.BackColor = Color.FromArgb(15, 23, 42);
            pnlCustomerSuggestions.Padding = new Padding(1);
            pnlCustomerSuggestions.Paint += (s, e) => {
                using (Pen p = new Pen(Theme.Accent, 1.5f))
                {
                    e.Graphics.DrawRectangle(p, 0, 0, pnlCustomerSuggestions.Width - 1, pnlCustomerSuggestions.Height - 1);
                }
            };
            pnlCustomerSuggestions.Controls.Add(lstCustomerSuggestions);

            customerSuggestionHost = new ToolStripControlHost(pnlCustomerSuggestions) {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = pnlCustomerSuggestions.Size
            };

            customerSuggestionDropDown = new NoFocusToolStripDropDown();
            customerSuggestionDropDown.Items.Add(customerSuggestionHost);
        }

        private void ApplySelectedCustomerSuggestion()
        {
            if (lstCustomerSuggestions != null && lstCustomerSuggestions.SelectedItem is CustomerPhoneSuggestion sug)
            {
                isSettingCustomerProgrammatically = true;
                txtCustomerPhone.Text = sug.Phone ?? "";
                txtCustomerName.Text = sug.Name ?? "";
                isSettingCustomerProgrammatically = false;

                HideCustomerSuggestions();

                if (!string.IsNullOrEmpty(sug.Phone) && sug.Phone != "0000000000")
                {
                    LoadCustomerPastHistory(sug.Phone);
                }

                if (string.IsNullOrEmpty(txtCustomerName.Text))
                {
                    txtCustomerName.Focus();
                }
                else
                {
                    btnSelectServices.Focus();
                }
            }
        }

        private void HideCustomerSuggestions()
        {
            if (customerSuggestionDropDown != null && customerSuggestionDropDown.Visible)
            {
                customerSuggestionDropDown.Close();
            }
        }

        private void FilterServiceCheckedList(string filter)
        {
            isPopulatingList = true;
            try
            {
                chkListServices.BeginUpdate();
                chkListServices.Items.Clear();

                var filtered = string.IsNullOrEmpty(filter)
                    ? allServicesList
                    : allServicesList.Where(s => s.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                foreach (var item in filtered)
                {
                    bool isChecked = selectedServiceIds.Contains(item.Id);
                    chkListServices.Items.Add(item, isChecked);
                }
                chkListServices.EndUpdate();
            }
            finally
            {
                isPopulatingList = false;
            }
        }

        private void ChkListServices_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (isPopulatingList) return;

            if (chkListServices.Items.Count > e.Index && chkListServices.Items[e.Index] is ServiceItem srv)
            {
                if (e.NewValue == CheckState.Checked)
                {
                    if (!selectedServiceIds.Contains(srv.Id)) selectedServiceIds.Add(srv.Id);
                }
                else
                {
                    selectedServiceIds.Remove(srv.Id);
                }
            }

            if (this.IsHandleCreated)
            {
                try
                {
                    this.BeginInvoke(new Action(() => {
                        UpdateServiceSelectionUI();
                    }));
                    return;
                }
                catch { }
            }

            UpdateServiceSelectionUI();
        }

        private void RelayoutLeftPanel()
        {
            if (lblStaff == null || comboStaff == null || pnlPerServiceStaff == null || lblDate == null) return;

            int curY = lblServiceSummary.Bottom + 6;

            // 1. Date
            lblDate.Location = new Point(15, curY);
            curY += 20;
            dtpApptDate.Location = new Point(15, curY);
            dtpApptDate.Size = new Size(320, 26);
            curY += 34;

            // 2. Time Slot & Duration
            if (lblTime != null) lblTime.Location = new Point(15, curY);
            curY += 20;
            if (comboFromHour != null) comboFromHour.Location = new Point(15, curY);
            if (comboFromMin != null) comboFromMin.Location = new Point(102, curY);
            if (lblTo != null) lblTo.Location = new Point(162, curY + 3);
            if (comboToHour != null) comboToHour.Location = new Point(183, curY);
            if (comboToMin != null) comboToMin.Location = new Point(270, curY);
            curY += 34;

            // 3. Primary / Default Stylist
            lblStaff.Location = new Point(15, curY);
            curY += 20;
            comboStaff.Location = new Point(15, curY);
            curY += comboStaff.Height + 8;

            // 4. Dynamic Sequential Service Timeline & Stylists
            if (pnlPerServiceStaff.Visible && pnlPerServiceStaff.Controls.Count > 0)
            {
                pnlPerServiceStaff.Location = new Point(15, curY);
                curY += pnlPerServiceStaff.Height + 8;
            }

            // 5. Status
            if (lblStatus != null) lblStatus.Location = new Point(15, curY);
            curY += 20;
            if (comboStatus != null) comboStatus.Location = new Point(15, curY);
            curY += 34;

            // 6. Notes
            if (lblNotes != null) lblNotes.Location = new Point(15, curY);
            curY += 20;
            if (txtNotes != null) txtNotes.Location = new Point(15, curY);
            curY += (txtNotes != null ? txtNotes.Height : 36) + 12;

            // 7. Action buttons
            if (btnBook != null) btnBook.Location = new Point(15, curY);
            if (btnClear != null) btnClear.Location = new Point(185, curY);
            curY += (btnBook != null ? btnBook.Height : 38) + 12;

            // 8. Client History Panel (Dynamic placement below buttons)
            if (pnlClientHistory != null)
            {
                pnlClientHistory.Location = new Point(15, curY);
                if (pnlClientHistory.Visible)
                {
                    curY += pnlClientHistory.Height + 12;
                }
            }
        }

        private void UpdateStatusOptionsForDate(DateTime date)
        {
            if (comboStatus == null) return;
            string current = comboStatus.SelectedItem?.ToString() ?? "Booked";
            comboStatus.Items.Clear();

            if (date.Date > DateTime.Today)
            {
                comboStatus.Items.AddRange(new object[] { "Booked", "Cancelled" });
                if (current != "Booked" && current != "Cancelled") current = "Booked";
            }
            else
            {
                comboStatus.Items.AddRange(new object[] { "Booked", "In-Chair", "Completed", "Billed", "Cancelled" });
            }

            comboStatus.SelectedItem = current;
        }

        private DateTime ParseTimeSlot(DateTime baseDate, string timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return baseDate.Date.AddHours(10);
            
            // If the time slot contains a range like "10:00 AM – 12:20 PM", take the first part
            string cleaned = timeStr;
            if (cleaned.Contains("–")) cleaned = cleaned.Split('–')[0].Trim();
            else if (cleaned.Contains("-")) cleaned = cleaned.Split('-')[0].Trim();

            if (DateTime.TryParseExact(cleaned, "hh:mm tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, dt.Hour, dt.Minute, 0);
            }
            if (DateTime.TryParse(cleaned, out DateTime dt2))
            {
                return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, dt2.Hour, dt2.Minute, 0);
            }
            return baseDate.Date.AddHours(10);
        }

        private void UpdateServiceSelectionUI()
        {
            var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
            decimal totalAmount = selectedItems.Sum(s => s.Price);
            int totalMinutes = selectedItems.Sum(s => s.DurationMinutes);
            if (totalMinutes <= 0) totalMinutes = 30;

            DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
            DateTime apptStart = GetSelectedFromTime(baseDate);
            DateTime runningStart = apptStart;

            // Sync End Time with total duration
            if (!isInternalTimeUpdating && comboToHour != null && comboToMin != null)
            {
                isInternalTimeUpdating = true;
                try
                {
                    DateTime apptEnd = apptStart.AddMinutes(totalMinutes);
                    SetComboTime(comboToHour, comboToMin, apptEnd);
                }
                finally
                {
                    isInternalTimeUpdating = false;
                }
            }

            lblPopupTotal.Text = $"Selected: {selectedItems.Count} items\nRs. {totalAmount:N0} • {totalMinutes}m";

            int currentPrimaryStaffId = 0;
            if (comboStaff != null && comboStaff.SelectedItem is ComboBoxItem stItm)
            {
                currentPrimaryStaffId = stItm.Id;
            }

            // Sync dictionary
            var keysToRemove = selectedServiceStaffMap.Keys.Where(k => !selectedServiceIds.Contains(k)).ToList();
            foreach (var k in keysToRemove) selectedServiceStaffMap.Remove(k);

            foreach (var item in selectedItems)
            {
                if (selectedItems.Count == 1 && currentPrimaryStaffId > 0)
                {
                    selectedServiceStaffMap[item.Id] = currentPrimaryStaffId;
                }
                else if (!selectedServiceStaffMap.ContainsKey(item.Id) || selectedServiceStaffMap[item.Id] <= 0)
                {
                    selectedServiceStaffMap[item.Id] = currentPrimaryStaffId;
                }
            }

            if (selectedItems.Count == 0)
            {
                btnSelectServices.Text = "👉 Click to Select Services...";
                btnSelectServices.ForeColor = Theme.TextDark;
                lblServiceSummary.Text = "✨ 0 services selected (Rs. 0 • 0 mins)";
                lblServiceSummary.ForeColor = Theme.Accent;
                if (pnlPerServiceStaff != null) pnlPerServiceStaff.Visible = false;
            }
            else if (selectedItems.Count == 1)
            {
                int dur = selectedItems[0].DurationMinutes > 0 ? selectedItems[0].DurationMinutes : 30;
                btnSelectServices.Text = $"💇 {selectedItems[0].Name} (Rs. {selectedItems[0].Price:N0})";
                btnSelectServices.ForeColor = Theme.TextLight;
                lblServiceSummary.Text = $"✨ 1 service selected (Rs. {totalAmount:N0} • {dur}m)";
                lblServiceSummary.ForeColor = Theme.Success;
                if (pnlPerServiceStaff != null) pnlPerServiceStaff.Visible = false;
            }
            else
            {
                btnSelectServices.Text = $"💇 {selectedItems[0].Name} (+{selectedItems.Count - 1} more) • Rs. {totalAmount:N0}";
                btnSelectServices.ForeColor = Theme.TextLight;
                lblServiceSummary.Text = $"✨ {selectedItems.Count} services selected (Rs. {totalAmount:N0} • {totalMinutes}m)";
                lblServiceSummary.ForeColor = Theme.Success;

                if (pnlPerServiceStaff != null)
                {
                    pnlPerServiceStaff.SuspendLayout();
                    pnlPerServiceStaff.Controls.Clear();

                    int seq = 1;
                    foreach (var srv in selectedItems)
                    {
                        int dur = srv.DurationMinutes > 0 ? srv.DurationMinutes : 30;

                        Panel card = new Panel();
                        card.Size = new Size(310, 56);
                        card.Margin = new Padding(0, 2, 0, 4);
                        card.BackColor = Theme.CardBg;
                        card.Paint += (s, e) => {
                            using (Pen p = new Pen(Theme.CardBorder, 1))
                            {
                                e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1);
                            }
                        };

                        // Row 1: Service Name (Full width)
                        Label lblSrvTitle = new Label();
                        lblSrvTitle.Text = $"💇 #{seq}. {srv.Name} ({dur}m • Rs. {srv.Price:N0})";
                        lblSrvTitle.Location = new Point(6, 4);
                        lblSrvTitle.Size = new Size(298, 18);
                        lblSrvTitle.AutoEllipsis = true;
                        Theme.StyleLabel(lblSrvTitle, Theme.TextLight, new Font("Segoe UI Semibold", 8.2F, FontStyle.Bold));
                        card.Controls.Add(lblSrvTitle);

                        // Row 2: Stylist Dropdown (Full width)
                        ComboBox cbStaff = new ComboBox();
                        cbStaff.Location = new Point(6, 25);
                        cbStaff.Size = new Size(298, 24);
                        cbStaff.DropDownStyle = ComboBoxStyle.DropDownList;
                        Theme.StyleComboBox(cbStaff);

                        int chosenStaffId = selectedServiceStaffMap.ContainsKey(srv.Id) && selectedServiceStaffMap[srv.Id] > 0
                            ? selectedServiceStaffMap[srv.Id]
                            : currentPrimaryStaffId;

                        int matchIdx = 0;
                        if (comboStaff != null)
                        {
                            for (int i = 0; i < comboStaff.Items.Count; i++)
                            {
                                cbStaff.Items.Add(comboStaff.Items[i]);
                                if (comboStaff.Items[i] is ComboBoxItem itm && itm.Id == chosenStaffId)
                                {
                                    matchIdx = i;
                                }
                            }
                        }

                        if (cbStaff.Items.Count > 0)
                        {
                            cbStaff.SelectedIndex = matchIdx;
                        }

                        int serviceId = srv.Id;
                        cbStaff.SelectedIndexChanged += (s, e) => {
                            if (cbStaff.SelectedItem is ComboBoxItem itm && itm.Id > 0)
                            {
                                selectedServiceStaffMap[serviceId] = itm.Id;
                            }
                        };
                        card.Controls.Add(cbStaff);

                        pnlPerServiceStaff.Controls.Add(card);
                        seq++;
                    }

                    pnlPerServiceStaff.Visible = true;
                    pnlPerServiceStaff.ResumeLayout();
                }
            }

            RelayoutLeftPanel();
        }

        private void BtnSelectServices_Click(object sender, EventArgs e)
        {
            if (btnSelectServices.Enabled)
            {
                txtSearchServices.Text = "🔍 Search services...";
                txtSearchServices.ForeColor = Color.Gray;
                FilterServiceCheckedList("");
                UpdateServiceSelectionUI();
                serviceDropDown.Show(btnSelectServices, new Point(0, btnSelectServices.Height + 2));
            }
        }

        private void TxtCustomerPhone_TextChanged(object sender, EventArgs e)
        {
            if (isSettingCustomerProgrammatically) return;

            string phone = txtCustomerPhone.Text.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                HideCustomerSuggestions();
                ClearCustomerPastHistory();
                return;
            }

            ShowCustomerSuggestions(txtCustomerPhone, phone, searchByName: false);
        }

        private void TxtCustomerName_TextChanged(object sender, EventArgs e)
        {
            if (isSettingCustomerProgrammatically) return;

            string name = txtCustomerName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                HideCustomerSuggestions();
                return;
            }

            ShowCustomerSuggestions(txtCustomerName, name, searchByName: true);
        }

        private void ShowCustomerSuggestions(TextBox targetBox, string query, bool searchByName)
        {
            if (isLeftPanelCollapsed || targetBox == null || !targetBox.Visible)
            {
                HideCustomerSuggestions();
                return;
            }

            if (customerSuggestionDropDown == null || lstCustomerSuggestions == null) return;

            query = query?.Trim() ?? "";
            if (string.IsNullOrEmpty(query) || query == "0000000000")
            {
                HideCustomerSuggestions();
                return;
            }

            try
            {
                List<CustomerPhoneSuggestion> suggestions = new List<CustomerPhoneSuggestion>();
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    string sql = searchByName ? @"
                        SELECT TOP 8 Phone, Name 
                        FROM (
                            SELECT DISTINCT Phone, Name 
                            FROM Customers 
                            WHERE (Name IS NOT NULL AND Name <> '' AND Name <> 'Walk-in Client' AND Name LIKE '%' + @q + '%') 
                               OR (Phone IS NOT NULL AND Phone <> '' AND Phone <> '0000000000' AND Phone LIKE '%' + @q + '%')
                        ) c 
                        ORDER BY 
                          CASE 
                            WHEN c.Name LIKE @q + '%' THEN 0 
                            WHEN c.Phone LIKE @q + '%' THEN 1 
                            ELSE 2 
                          END, 
                          c.Name ASC"
                    : @"
                        SELECT TOP 8 Phone, Name 
                        FROM (
                            SELECT DISTINCT Phone, Name 
                            FROM Customers 
                            WHERE (Phone IS NOT NULL AND Phone <> '' AND Phone <> '0000000000' AND Phone LIKE '%' + @q + '%')
                               OR (Name IS NOT NULL AND Name <> '' AND Name <> 'Walk-in Client' AND Name LIKE '%' + @q + '%') 
                        ) c 
                        ORDER BY 
                          CASE 
                            WHEN c.Phone LIKE @q + '%' THEN 0 
                            WHEN c.Name LIKE @q + '%' THEN 1 
                            ELSE 2 
                          END, 
                          c.Phone ASC";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@q", query);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                string p = rdr["Phone"]?.ToString() ?? "";
                                string n = rdr["Name"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(p) || !string.IsNullOrEmpty(n))
                                {
                                    suggestions.Add(new CustomerPhoneSuggestion { Phone = p, Name = n });
                                }
                            }
                        }
                    }
                }

                if (suggestions.Count > 0)
                {
                    activeSuggestionTarget = targetBox;
                    lstCustomerSuggestions.BeginUpdate();
                    lstCustomerSuggestions.Items.Clear();
                    foreach (var s in suggestions)
                    {
                        lstCustomerSuggestions.Items.Add(s);
                    }
                    lstCustomerSuggestions.SelectedIndex = 0;
                    lstCustomerSuggestions.EndUpdate();

                    int newH = Math.Min(180, suggestions.Count * 28 + 4);
                    pnlCustomerSuggestions.Size = new Size(targetBox.Width, newH);
                    if (customerSuggestionHost != null)
                    {
                        customerSuggestionHost.Size = pnlCustomerSuggestions.Size;
                    }

                    if (!customerSuggestionDropDown.Visible)
                    {
                        customerSuggestionDropDown.Show(targetBox, new Point(0, targetBox.Height + 2));
                    }
                }
                else
                {
                    HideCustomerSuggestions();
                }
            }
            catch
            {
                HideCustomerSuggestions();
            }
        }

        private void LookupCustomerAndHistory(string phone, bool updateNameOnlyIfEmpty = false)
        {
            if (!string.IsNullOrEmpty(phone) && phone != "0000000000" && phone.Length >= 3)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 Name FROM Customers WHERE Phone = @phone", conn))
                        {
                            cmd.Parameters.AddWithValue("@phone", phone);
                            object result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                string name = result.ToString();
                                if (!string.IsNullOrEmpty(name) && name != "Walk-in Client")
                                {
                                    if (!updateNameOnlyIfEmpty || string.IsNullOrEmpty(txtCustomerName.Text.Trim()))
                                    {
                                        isSettingCustomerProgrammatically = true;
                                        txtCustomerName.Text = name;
                                        isSettingCustomerProgrammatically = false;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                LoadCustomerPastHistory(phone);
            }
            else
            {
                ClearCustomerPastHistory();
            }
        }

        private void ClearCustomerPastHistory()
        {
            if (pnlClientHistory != null)
            {
                pnlClientHistory.Visible = false;
                if (gridClientHistory != null) gridClientHistory.DataSource = null;
                RelayoutLeftPanel();
            }
        }

        private void LoadCustomerPastHistory(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone == "0000000000" || phone.Length < 3)
            {
                ClearCustomerPastHistory();
                return;
            }

            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("Date", typeof(string));
                dt.Columns.Add("Client", typeof(string));
                dt.Columns.Add("Services", typeof(string));
                dt.Columns.Add("Amount", typeof(string));
                dt.Columns.Add("InvoiceNumber", typeof(string));

                HashSet<string> seenInvoices = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // 1. Fetch completed/billed sales for this customer phone (1 row per unique invoice)
                    string salesQuery = @"
                        SELECT TOP 10 
                            s.Id AS SaleId,
                            s.InvoiceNumber,
                            CONVERT(VARCHAR(10), s.SaleDate, 120) AS ServiceDate,
                            ISNULL(c.Name, 'Customer') AS CustomerName,
                            ISNULL(s.GrandTotal, 0) AS TotalAmount
                        FROM Sales s
                        LEFT JOIN Customers c ON s.CustomerId = c.Id
                        WHERE c.Phone = @phone
                        ORDER BY s.SaleDate DESC, s.Id DESC";

                    using (SqlCommand cmd = new SqlCommand(salesQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@phone", phone);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            var saleRows = new List<Tuple<int, string, string, string, decimal>>();
                            while (rdr.Read())
                            {
                                int sId = Convert.ToInt32(rdr["SaleId"]);
                                string inv = rdr["InvoiceNumber"].ToString();
                                string sDate = rdr["ServiceDate"].ToString();
                                string cName = rdr["CustomerName"].ToString();
                                decimal amt = Convert.ToDecimal(rdr["TotalAmount"]);
                                saleRows.Add(Tuple.Create(sId, inv, sDate, cName, amt));
                            }
                            rdr.Close();

                            foreach (var sRow in saleRows)
                            {
                                if (seenInvoices.Contains(sRow.Item2)) continue;
                                seenInvoices.Add(sRow.Item2);

                                List<string> itemNames = new List<string>();
                                using (SqlCommand itmCmd = new SqlCommand(@"
                                    SELECT ISNULL(s.Name, ISNULL(p.Name, sd.ItemType)) AS ItemName
                                    FROM SaleDetails sd
                                    LEFT JOIN Services s ON sd.ServiceId = s.Id
                                    LEFT JOIN Products p ON sd.ProductId = p.Id
                                    WHERE sd.SaleId = @saleId", conn))
                                {
                                    itmCmd.Parameters.AddWithValue("@saleId", sRow.Item1);
                                    using (SqlDataReader itmRdr = itmCmd.ExecuteReader())
                                    {
                                        while (itmRdr.Read())
                                        {
                                            itemNames.Add(itmRdr["ItemName"].ToString());
                                        }
                                    }
                                }

                                string srvStr = itemNames.Count > 0 ? string.Join(", ", itemNames) : "Salon Service";
                                dt.Rows.Add(sRow.Item3, sRow.Item4, srvStr, $"Rs. {sRow.Item5:N0}", sRow.Item2);
                            }
                        }
                    }

                    // 2. Fetch past unbilled appointments for this customer phone (if not already represented as an invoice)
                    string apptQuery = @"
                        SELECT TOP 10
                            a.AppointmentNumber,
                            CONVERT(VARCHAR(10), a.AppointmentDate, 120) AS ServiceDate,
                            ISNULL(c.Name, 'Customer') AS CustomerName,
                            ISNULL(NULLIF(a.ServiceNames, ''), srv.Name) AS ServiceNames,
                            a.Status
                        FROM Appointments a
                        LEFT JOIN Customers c ON a.CustomerId = c.Id
                        LEFT JOIN Services srv ON a.ServiceId = srv.Id
                        WHERE (c.Phone = @phone OR a.Notes LIKE '%' + @phone + '%')
                          AND a.Status <> 'Billed'
                        ORDER BY a.AppointmentDate DESC, a.Id DESC";

                    using (SqlCommand apptCmd = new SqlCommand(apptQuery, conn))
                    {
                        apptCmd.Parameters.AddWithValue("@phone", phone);
                        using (SqlDataReader apptRdr = apptCmd.ExecuteReader())
                        {
                            while (apptRdr.Read())
                            {
                                string apptNum = apptRdr["AppointmentNumber"] != DBNull.Value ? apptRdr["AppointmentNumber"].ToString() : "";
                                string sDate = apptRdr["ServiceDate"] != DBNull.Value ? apptRdr["ServiceDate"].ToString() : "";
                                string cName = apptRdr["CustomerName"] != DBNull.Value ? apptRdr["CustomerName"].ToString() : "";
                                string sNames = apptRdr["ServiceNames"] != DBNull.Value ? apptRdr["ServiceNames"].ToString() : "Salon Service";

                                if (!string.IsNullOrEmpty(apptNum) && !seenInvoices.Contains(apptNum))
                                {
                                    seenInvoices.Add(apptNum);
                                    dt.Rows.Add(sDate, cName, sNames, "-", apptNum);
                                }
                            }
                        }
                    }
                }

                if (dt.Rows.Count > 0)
                {
                    lblHistoryTitle.Text = $"📜 Past Client History ({dt.Rows.Count} visit{(dt.Rows.Count > 1 ? "s" : "")})";
                    lblHistoryTitle.ForeColor = Theme.Accent;

                    gridClientHistory.AutoGenerateColumns = false;
                    gridClientHistory.Columns.Clear();

                    DataGridViewTextBoxColumn colDate = new DataGridViewTextBoxColumn();
                    colDate.Name = "Date";
                    colDate.DataPropertyName = "Date";
                    colDate.HeaderText = "Date";
                    colDate.Width = 72;
                    gridClientHistory.Columns.Add(colDate);

                    DataGridViewTextBoxColumn colClient = new DataGridViewTextBoxColumn();
                    colClient.Name = "Client";
                    colClient.DataPropertyName = "Client";
                    colClient.HeaderText = "Client";
                    colClient.Width = 62;
                    gridClientHistory.Columns.Add(colClient);

                    DataGridViewTextBoxColumn colSrv = new DataGridViewTextBoxColumn();
                    colSrv.Name = "Services";
                    colSrv.DataPropertyName = "Services";
                    colSrv.HeaderText = "Services Availed";
                    colSrv.Width = 96;
                    gridClientHistory.Columns.Add(colSrv);

                    DataGridViewTextBoxColumn colAmt = new DataGridViewTextBoxColumn();
                    colAmt.Name = "Amount";
                    colAmt.DataPropertyName = "Amount";
                    colAmt.HeaderText = "Amount";
                    colAmt.Width = 48;
                    gridClientHistory.Columns.Add(colAmt);

                    DataGridViewTextBoxColumn colInv = new DataGridViewTextBoxColumn();
                    colInv.Name = "InvoiceNumber";
                    colInv.DataPropertyName = "InvoiceNumber";
                    colInv.HeaderText = "InvoiceNumber";
                    colInv.Visible = false;
                    gridClientHistory.Columns.Add(colInv);

                    DataGridViewButtonColumn colBtn = new DataGridViewButtonColumn();
                    colBtn.Name = "Action";
                    colBtn.HeaderText = "View";
                    colBtn.Text = "👁️";
                    colBtn.UseColumnTextForButtonValue = true;
                    colBtn.Width = 38;
                    colBtn.FlatStyle = FlatStyle.Flat;
                    colBtn.DefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59);
                    colBtn.DefaultCellStyle.ForeColor = Theme.Accent;
                    gridClientHistory.Columns.Add(colBtn);

                    gridClientHistory.DataSource = dt;
                    pnlClientHistory.Visible = true;
                }
                else
                {
                    ClearCustomerPastHistory();
                }

                RelayoutLeftPanel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("History load error: " + ex.Message);
                ClearCustomerPastHistory();
            }
        }

        private void GridClientHistory_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && gridClientHistory.Columns[e.ColumnIndex].Name == "Action")
            {
                OpenDetailsForHistoryRow(e.RowIndex);
            }
        }

        private void OpenDetailsForHistoryRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= gridClientHistory.Rows.Count) return;
            string invNum = null;

            if (gridClientHistory.Columns.Contains("InvoiceNumber") && gridClientHistory.Rows[rowIndex].Cells["InvoiceNumber"] != null)
            {
                invNum = gridClientHistory.Rows[rowIndex].Cells["InvoiceNumber"].Value?.ToString();
            }
            else if (gridClientHistory.DataSource is DataTable dt && rowIndex < dt.Rows.Count && dt.Columns.Contains("InvoiceNumber"))
            {
                invNum = dt.Rows[rowIndex]["InvoiceNumber"]?.ToString();
            }

            if (!string.IsNullOrEmpty(invNum))
            {
                if (invNum.StartsWith("INV-", StringComparison.OrdinalIgnoreCase))
                {
                    using (var dlg = new InvoiceDetailsForm(invNum))
                    {
                        dlg.ShowDialog(this);
                    }
                }
                else
                {
                    ShowPastAppointmentDetailsModal(invNum);
                }
            }
        }

        private void ShowPastAppointmentDetailsModal(string apptNum)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT a.AppointmentNumber, a.AppointmentDate, a.AppointmentTime, c.Name AS CustomerName, c.Phone AS CustomerPhone,
                               ISNULL(NULLIF(a.ServiceNames, ''), srv.Name) AS Services, st.Name AS Stylist, a.Status, a.Notes
                        FROM Appointments a
                        LEFT JOIN Customers c ON a.CustomerId = c.Id
                        LEFT JOIN Services srv ON a.ServiceId = srv.Id
                        LEFT JOIN Staff st ON a.StaffId = st.Id
                        WHERE a.AppointmentNumber = @num", conn))
                    {
                        cmd.Parameters.AddWithValue("@num", apptNum);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                string info = $"📅 Appointment: {rdr["AppointmentNumber"]}\n" +
                                              $"📆 Date: {Convert.ToDateTime(rdr["AppointmentDate"]):dd-MMM-yyyy}\n" +
                                              $"⏰ Time: {rdr["AppointmentTime"]}\n" +
                                              $"👤 Client Name: {rdr["CustomerName"]}\n" +
                                              $"📞 Client Phone: {rdr["CustomerPhone"]}\n\n" +
                                              $"💇 Services Availed: {rdr["Services"]}\n" +
                                              $"✂️ Stylist: {rdr["Stylist"]}\n" +
                                              $"📌 Status: {rdr["Status"]}\n" +
                                              $"📝 Notes: {rdr["Notes"]}";

                                MessageBox.Show(info, "Past Appointment Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading appointment details: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadDropdowns()
        {
            try
            {
                // Services
                allServicesList.Clear();
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Code, Name, Price, DurationMinutes, ISNULL(SACCode, '999721') AS SACCode, ISNULL(GSTRate, 18.00) AS GSTRate FROM Services WHERE IsActive = 1 ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                allServicesList.Add(new ServiceItem {
                                    Id = Convert.ToInt32(rdr["Id"]),
                                    Code = rdr["Code"]?.ToString() ?? "",
                                    Name = rdr["Name"].ToString(),
                                    Price = Convert.ToDecimal(rdr["Price"]),
                                    DurationMinutes = rdr["DurationMinutes"] != DBNull.Value ? Convert.ToInt32(rdr["DurationMinutes"]) : 30,
                                    SACCode = rdr["SACCode"].ToString(),
                                    GSTRate = Convert.ToDecimal(rdr["GSTRate"])
                                });
                            }
                        }
                    }
                }

                FilterServiceCheckedList("");
                UpdateServiceSelectionUI();

                comboStaff.Items.Clear();
                comboStaff.Items.Add(new ComboBoxItem { Id = 0, Display = "-- Select Stylist --" });
                comboFilterStaff.Items.Clear();
                comboFilterStaff.Items.Add(new ComboBoxItem { Id = 0, Display = "All Stylists" });

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT Id, Name, Role FROM Staff WHERE IsActive = 1 ORDER BY Name ASC", conn))
                    {
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                int id = Convert.ToInt32(rdr["Id"]);
                                string name = rdr["Name"].ToString();
                                string role = rdr["Role"].ToString();

                                var item = new ComboBoxItem {
                                    Id = id,
                                    Display = $"{name} ({role})"
                                };
                                comboStaff.Items.Add(item);
                                comboFilterStaff.Items.Add(new ComboBoxItem {
                                    Id = id,
                                    Display = $"{name} ({role})"
                                });
                            }
                        }
                    }
                }
                if (comboStaff.Items.Count > 0) comboStaff.SelectedIndex = 0;
                if (comboFilterStaff.Items.Count > 0) comboFilterStaff.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading appointment dropdowns: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnFromTimeChanged()
        {
            if (isInternalTimeUpdating) return;

            // Recalculate End Time based on selected services' total duration
            var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
            int totalMinutes = selectedItems.Sum(s => s.DurationMinutes);
            if (totalMinutes <= 0) totalMinutes = 30;

            DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
            DateTime fromDt = GetSelectedFromTime(baseDate);
            DateTime toDt = fromDt.AddMinutes(totalMinutes);

            isInternalTimeUpdating = true;
            try
            {
                SetComboTime(comboToHour, comboToMin, toDt);
            }
            finally
            {
                isInternalTimeUpdating = false;
            }

            UpdateServiceSelectionUI();
        }

        private void OnToTimeChanged()
        {
            if (isInternalTimeUpdating) return;

            DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
            DateTime fromDt = GetSelectedFromTime(baseDate);
            DateTime toDt = GetSelectedToTime(baseDate);
            int chosenMinutes = (int)(toDt - fromDt).TotalMinutes;
            if (chosenMinutes < 5) chosenMinutes = 5;

            var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
            decimal totalAmount = selectedItems.Sum(s => s.Price);
            if (selectedItems.Count > 0 && lblServiceSummary != null)
            {
                lblServiceSummary.Text = $"✨ {selectedItems.Count} service(s) selected (Rs. {totalAmount:N0} • {chosenMinutes}m)";
            }
        }

        private DateTime GetSelectedFromTime(DateTime baseDate)
        {
            string hourStr = comboFromHour?.SelectedItem?.ToString() ?? "10 AM";
            string minStr = comboFromMin?.SelectedItem?.ToString() ?? "00";
            return ParseHourMinuteString(baseDate, hourStr, minStr);
        }

        private DateTime GetSelectedToTime(DateTime baseDate)
        {
            string hourStr = comboToHour?.SelectedItem?.ToString() ?? "10 AM";
            string minStr = comboToMin?.SelectedItem?.ToString() ?? "30";
            return ParseHourMinuteString(baseDate, hourStr, minStr);
        }

        private static DateTime ParseHourMinuteString(DateTime baseDate, string hourStr, string minStr)
        {
            int hour = 10;
            bool isPm = false;
            if (!string.IsNullOrEmpty(hourStr))
            {
                string cleanH = hourStr.Trim();
                if (cleanH.EndsWith("PM", StringComparison.OrdinalIgnoreCase))
                {
                    isPm = true;
                    cleanH = cleanH.Replace("PM", "").Trim();
                }
                else if (cleanH.EndsWith("AM", StringComparison.OrdinalIgnoreCase))
                {
                    cleanH = cleanH.Replace("AM", "").Trim();
                }
                if (int.TryParse(cleanH, out int h))
                {
                    if (isPm && h < 12) hour = h + 12;
                    else if (!isPm && h == 12) hour = 0;
                    else hour = h;
                }
            }

            int min = 0;
            if (!string.IsNullOrEmpty(minStr) && int.TryParse(minStr.Trim(), out int m))
            {
                min = m;
            }

            return new DateTime(baseDate.Year, baseDate.Month, baseDate.Day, Math.Min(23, Math.Max(0, hour)), Math.Min(59, Math.Max(0, min)), 0);
        }

        private string GetSelectedStartTime()
        {
            DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
            DateTime fromDt = GetSelectedFromTime(baseDate);
            return $"{fromDt:hh:mm tt}";
        }

        private string GetSelectedTimeSlot()
        {
            DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
            DateTime fromDt = GetSelectedFromTime(baseDate);
            DateTime toDt = GetSelectedToTime(baseDate);
            return $"{fromDt:hh:mm tt} – {toDt:hh:mm tt}";
        }

        private void SetComboTime(ComboBox cbHour, ComboBox cbMin, DateTime dt)
        {
            if (cbHour == null || cbMin == null) return;

            string hourStr = $"{dt:hh tt}"; // e.g. "10 AM", "01 PM"
            string minStr = $"{dt:mm}";    // e.g. "00", "30"

            if (!cbHour.Items.Contains(hourStr))
            {
                cbHour.Items.Add(hourStr);
            }
            cbHour.SelectedItem = hourStr;

            if (!cbMin.Items.Contains(minStr))
            {
                cbMin.Items.Add(minStr);
            }
            cbMin.SelectedItem = minStr;
        }

        private void SetTimeSlot(string timeSlotStr, int defaultDurationMinutes = 30)
        {
            isInternalTimeUpdating = true;
            try
            {
                DateTime baseDate = dtpApptDate != null ? dtpApptDate.Value.Date : DateTime.Today;
                DateTime fromDt = baseDate.Date.AddHours(10);
                DateTime toDt = fromDt.AddMinutes(defaultDurationMinutes > 0 ? defaultDurationMinutes : 30);

                if (!string.IsNullOrWhiteSpace(timeSlotStr))
                {
                    string fromPart = timeSlotStr;
                    string toPart = "";

                    if (timeSlotStr.Contains("–"))
                    {
                        string[] parts = timeSlotStr.Split('–');
                        fromPart = parts[0].Trim();
                        if (parts.Length > 1) toPart = parts[1].Trim();
                    }
                    else if (timeSlotStr.Contains("-"))
                    {
                        string[] parts = timeSlotStr.Split('-');
                        fromPart = parts[0].Trim();
                        if (parts.Length > 1) toPart = parts[1].Trim();
                    }
                    else if (timeSlotStr.IndexOf(" TO ", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] parts = timeSlotStr.Split(new[] { " TO ", " to " }, StringSplitOptions.None);
                        fromPart = parts[0].Trim();
                        if (parts.Length > 1) toPart = parts[1].Trim();
                    }

                    fromDt = ParseTimeSlotStatic(baseDate, fromPart);
                    if (!string.IsNullOrWhiteSpace(toPart))
                    {
                        toDt = ParseTimeSlotStatic(baseDate, toPart);
                    }
                    else
                    {
                        toDt = fromDt.AddMinutes(defaultDurationMinutes > 0 ? defaultDurationMinutes : 30);
                    }
                }

                SetComboTime(comboFromHour, comboFromMin, fromDt);
                SetComboTime(comboToHour, comboToMin, toDt);
            }
            finally
            {
                isInternalTimeUpdating = false;
            }
        }

        private void RefreshTimeSlots(string preserveSlot = null, int durationMinutes = 30)
        {
            if (!string.IsNullOrEmpty(preserveSlot))
            {
                SetTimeSlot(preserveSlot, durationMinutes);
            }
        }

        private static bool _schemaEnsured = false;
        private static void EnsureSchema(SqlConnection conn)
        {
            if (_schemaEnsured) return;
            try
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceIds')
                        ALTER TABLE Appointments ADD ServiceIds NVARCHAR(500) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceNames')
                        ALTER TABLE Appointments ADD ServiceNames NVARCHAR(1000) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'ServiceStaffIds')
                        ALTER TABLE Appointments ADD ServiceStaffIds NVARCHAR(1000) NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Appointments') AND name = 'SaleId')
                        ALTER TABLE Appointments ADD SaleId INT NULL;
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Sales') AND name = 'AppointmentId')
                        ALTER TABLE Sales ADD AppointmentId INT NULL;
                ", conn))
                {
                    cmd.ExecuteNonQuery();
                }
                _schemaEnsured = true;
            }
            catch { }
        }

        private void LoadAppointments()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    EnsureSchema(conn);
                    string query = @"
                        SELECT 
                            a.Id,
                            a.AppointmentNumber AS [Appt #],
                            CONVERT(VARCHAR(10), a.AppointmentDate, 120) AS [Date],
                            a.AppointmentTime AS [Time Slot],
                            c.Name AS [Client Name],
                            c.Phone AS [Client Phone],
                            ISNULL(NULLIF(a.ServiceNames, ''), s.Name) AS [Services],
                            st.Name AS [Stylist],
                            a.Status,
                            a.Notes,
                            a.CustomerId,
                            a.ServiceId,
                            a.ServiceIds,
                            a.ServiceStaffIds,
                            a.StaffId,
                            a.SaleId,
                            ISNULL(sal.InvoiceNumber, '') AS [InvoiceNumber],
                            ISNULL(sal.GrandTotal, ISNULL(sal.SubTotal, 0)) AS [InvoiceTotal]
                        FROM Appointments a
                        LEFT JOIN Customers c ON a.CustomerId = c.Id
                        LEFT JOIN Services s ON a.ServiceId = s.Id
                        LEFT JOIN Staff st ON a.StaffId = st.Id
                        LEFT JOIN Sales sal ON a.SaleId = sal.Id OR (a.SaleId IS NULL AND sal.AppointmentId = a.Id)
                        WHERE a.AppointmentDate = @apptDate";

                    if (comboFilterStatus.SelectedIndex > 0)
                    {
                        query += " AND a.Status = @status";
                    }

                    if (comboFilterStaff.SelectedItem is ComboBoxItem selectedStaff && selectedStaff.Id > 0)
                    {
                        query += " AND a.StaffId = @staffId";
                    }

                    query += " ORDER BY a.AppointmentTime ASC, a.Id ASC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@apptDate", dtpFilterDate.Value.Date);

                        if (comboFilterStatus.SelectedIndex > 0)
                        {
                            cmd.Parameters.AddWithValue("@status", comboFilterStatus.SelectedItem.ToString());
                        }

                        if (comboFilterStaff.SelectedItem is ComboBoxItem filterStaff && filterStaff.Id > 0)
                        {
                            cmd.Parameters.AddWithValue("@staffId", filterStaff.Id);
                        }

                        using (SqlDataAdapter da = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            da.Fill(dt);

                            isSuppressingSelection = true;
                            gridAppointments.DataSource = dt;
                            gridAppointments.ClearSelection();
                            isSuppressingSelection = false;

                            // Populate Zenoti Visual Schedule Board
                            List<StylistColumnModel> stylistColumns = new List<StylistColumnModel>();
                            using (SqlCommand cmdStaff = new SqlCommand("SELECT Id, Name, Role FROM Staff WHERE IsActive = 1 ORDER BY Name ASC", conn))
                            {
                                using (SqlDataReader rdr = cmdStaff.ExecuteReader())
                                {
                                    while (rdr.Read())
                                    {
                                        stylistColumns.Add(new StylistColumnModel
                                        {
                                            Id = Convert.ToInt32(rdr["Id"]),
                                            Name = rdr["Name"].ToString(),
                                            Role = rdr["Role"]?.ToString() ?? "Stylist"
                                        });
                                    }
                                }
                            }

                            List<AppointmentCardModel> apptCardList = new List<AppointmentCardModel>();
                            decimal totalServicesValue = 0m;
                            HashSet<string> uniqueClients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            int booked = 0, inChair = 0, done = 0, cancelled = 0, billed = 0;
                            foreach (DataRow r in dt.Rows)
                            {
                                int aId = Convert.ToInt32(r["Id"]);
                                string apptNum = r["Appt #"]?.ToString() ?? "";
                                string cName = r["Client Name"]?.ToString() ?? "Walk-in";
                                string cPhone = r["Client Phone"]?.ToString() ?? "";
                                int staffId = (r["StaffId"] != DBNull.Value) ? Convert.ToInt32(r["StaffId"]) : 0;
                                string staffName = r["Stylist"]?.ToString() ?? "";
                                string srvNames = r["Services"]?.ToString() ?? "";
                                string srvStaffIds = r["ServiceStaffIds"]?.ToString() ?? "";
                                string srvIds = r["ServiceIds"]?.ToString() ?? "";
                                string st = r["Status"]?.ToString() ?? "Booked";
                                string timeSlot = r["Time Slot"]?.ToString() ?? "10:00 AM";
                                int saleId = (r["SaleId"] != DBNull.Value) ? Convert.ToInt32(r["SaleId"]) : 0;
                                string invNum = r["InvoiceNumber"]?.ToString() ?? "";
                                string notes = r["Notes"]?.ToString() ?? "";
                                int custId = (r["CustomerId"] != DBNull.Value) ? Convert.ToInt32(r["CustomerId"]) : 0;
                                decimal invTotal = (r.Table.Columns.Contains("InvoiceTotal") && r["InvoiceTotal"] != DBNull.Value) ? Convert.ToDecimal(r["InvoiceTotal"]) : 0m;

                                if (st == "Booked") booked++;
                                else if (st == "In-Chair") inChair++;
                                else if (st == "Completed") done++;
                                else if (st == "Billed") billed++;
                                else if (st == "Cancelled") cancelled++;

                                DateTime apptDate = dtpFilterDate.Value.Date;
                                DateTime startTime = ParseTimeSlotStatic(apptDate, timeSlot);

                                int durationMin = 30;
                                if (timeSlot.Contains("–") || timeSlot.Contains("-"))
                                {
                                    string[] parts = timeSlot.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2)
                                    {
                                        DateTime t1 = ParseTimeSlotStatic(apptDate, parts[0].Trim());
                                        DateTime t2 = ParseTimeSlotStatic(apptDate, parts[1].Trim());
                                        if (t2 > t1) durationMin = (int)(t2 - t1).TotalMinutes;
                                    }
                                }

                                decimal totalAmt = 0m;
                                if (invTotal > 0)
                                {
                                    totalAmt = invTotal;
                                }
                                else if (!string.IsNullOrEmpty(srvIds))
                                {
                                    foreach (string p in srvIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                                    {
                                        if (int.TryParse(p.Trim(), out int sid))
                                        {
                                            var sMatch = allServicesList.FirstOrDefault(s => s.Id == sid);
                                            if (sMatch != null) totalAmt += sMatch.Price;
                                        }
                                    }
                                }
                                else if (r["ServiceId"] != DBNull.Value)
                                {
                                    int sid = Convert.ToInt32(r["ServiceId"]);
                                    var sMatch = allServicesList.FirstOrDefault(s => s.Id == sid);
                                    if (sMatch != null) totalAmt += sMatch.Price;
                                }

                                totalServicesValue += totalAmt;
                                if (!string.IsNullOrEmpty(cName)) uniqueClients.Add(cName);

                                bool hasDistinctStylists = false;
                                List<AppointmentCardModel> multiServiceCards = new List<AppointmentCardModel>();

                                if (!string.IsNullOrEmpty(srvStaffIds) && srvStaffIds.Contains(":"))
                                {
                                    string[] entries = srvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    DateTime runningSlotStart = startTime;

                                    foreach (string entry in entries)
                                    {
                                        // Format: "{serviceId}:{staffId}|{timeSlot}|{status}"
                                        string staffPart = entry.Trim();
                                        string timePart = "";
                                        string serviceStatus = "";
                                        if (staffPart.Contains("|"))
                                        {
                                            var tParts = staffPart.Split('|');
                                            staffPart = tParts[0].Trim();
                                            if (tParts.Length > 1) timePart = tParts[1].Trim();
                                            if (tParts.Length > 2) serviceStatus = tParts[2].Trim();
                                        }

                                        var sParts = staffPart.Split(':');
                                        if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                                        {
                                            int resolvedSid = allServicesList.Any(s => s.Id == rawSid) 
                                                ? rawSid 
                                                : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid))?.Id ?? rawSid);

                                            var sMatch = allServicesList.FirstOrDefault(s => s.Id == resolvedSid);
                                            int sDur = sMatch?.DurationMinutes > 0 ? sMatch.DurationMinutes : 30;
                                            decimal sPrice = sMatch != null ? sMatch.Price : 0m;
                                            string sName = sMatch != null ? sMatch.Name : "Service";

                                            DateTime sStart = runningSlotStart;
                                            if (!string.IsNullOrEmpty(timePart))
                                            {
                                                sStart = ParseTimeSlotStatic(apptDate, timePart);
                                                if (timePart.Contains("-") || timePart.Contains("–"))
                                                {
                                                    string[] tp = timePart.Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                                                    if (tp.Length == 2)
                                                    {
                                                        DateTime t1 = ParseTimeSlotStatic(apptDate, tp[0].Trim());
                                                        DateTime t2 = ParseTimeSlotStatic(apptDate, tp[1].Trim());
                                                        if (t2 > t1)
                                                        {
                                                            sStart = t1;
                                                            sDur = (int)(t2 - t1).TotalMinutes;
                                                        }
                                                    }
                                                }
                                            }
                                            else if (durationMin > 0 && entries.Length == 1)
                                            {
                                                sDur = durationMin;
                                            }
                                            DateTime sEnd = sStart.AddMinutes(sDur);
                                            runningSlotStart = sEnd;

                                            string sStaffName = staffName;
                                            var colMatch = stylistColumns.FirstOrDefault(sc => sc.Id == sStaffId);
                                            if (colMatch != null) sStaffName = colMatch.Name;

                                            string cardStatus = "Booked";
                                            if (st == "Billed" || st == "Cancelled" || saleId > 0 || !string.IsNullOrEmpty(invNum))
                                            {
                                                cardStatus = (st == "Cancelled") ? "Cancelled" : "Billed";
                                            }
                                            else if (!string.IsNullOrEmpty(serviceStatus))
                                            {
                                                cardStatus = serviceStatus;
                                            }
                                            else
                                            {
                                                if (st == "In-Chair")
                                                {
                                                    // Only the first service is In-Chair; subsequent upcoming services are Booked
                                                    cardStatus = (multiServiceCards.Count == 0) ? "In-Chair" : "Booked";
                                                }
                                                else if (st == "Completed")
                                                {
                                                    cardStatus = "Completed";
                                                }
                                                else
                                                {
                                                    cardStatus = "Booked";
                                                }
                                            }

                                            multiServiceCards.Add(new AppointmentCardModel
                                            {
                                                Id = aId,
                                                AppointmentNumber = apptNum,
                                                CustomerId = custId,
                                                CustomerName = cName,
                                                CustomerPhone = cPhone,
                                                StaffId = sStaffId > 0 ? sStaffId : staffId,
                                                StaffName = sStaffName,
                                                ServiceNames = sName,
                                                ServiceStaffIds = srvStaffIds,
                                                ServiceIds = srvIds,
                                                SpecificServiceId = resolvedSid,
                                                StartTime = sStart,
                                                EndTime = sEnd,
                                                DurationMinutes = sDur,
                                                Status = cardStatus,
                                                TotalAmount = sPrice > 0 ? sPrice : totalAmt,
                                                SaleId = saleId,
                                                InvoiceNumber = invNum,
                                                Notes = notes
                                            });
                                        }
                                    }

                                    if (multiServiceCards.Count > 1 && multiServiceCards.Select(c => c.StaffId).Distinct().Count() > 1)
                                    {
                                        hasDistinctStylists = true;
                                    }
                                }

                                if (hasDistinctStylists && multiServiceCards.Count > 0)
                                {
                                    apptCardList.AddRange(multiServiceCards);
                                }
                                else if (multiServiceCards.Count == 1)
                                {
                                    var singleCard = multiServiceCards[0];
                                    singleCard.TotalAmount = invTotal > 0 ? invTotal : totalAmt;
                                    singleCard.ServiceNames = !string.IsNullOrEmpty(srvNames) ? srvNames : singleCard.ServiceNames;
                                    apptCardList.Add(singleCard);
                                }
                                else
                                {
                                    apptCardList.Add(new AppointmentCardModel
                                    {
                                        Id = aId,
                                        AppointmentNumber = apptNum,
                                        CustomerId = custId,
                                        CustomerName = cName,
                                        CustomerPhone = cPhone,
                                        StaffId = staffId,
                                        StaffName = staffName,
                                        ServiceNames = (multiServiceCards.Count > 0 ? string.Join(" ➜ ", multiServiceCards.Select(m => m.ServiceNames)) : (!string.IsNullOrEmpty(srvNames) ? srvNames : "Services")),
                                        ServiceStaffIds = srvStaffIds,
                                        ServiceIds = srvIds,
                                        SpecificServiceId = (r["ServiceId"] != DBNull.Value ? Convert.ToInt32(r["ServiceId"]) : 0),
                                        StartTime = startTime,
                                        EndTime = startTime.AddMinutes(durationMin),
                                        DurationMinutes = durationMin,
                                        Status = st,
                                        TotalAmount = totalAmt,
                                        SaleId = saleId,
                                        InvoiceNumber = invNum,
                                        Notes = notes
                                    });
                                }
                            }

                            lblTotalBooked.Text = $"📌 Booked: {booked}";
                            lblInChair.Text = $"🪑 Chair: {inChair}";
                            lblCompleted.Text = $"✅ Done: {done}";
                            lblCancelled.Text = $"❌ Canc: {cancelled}";

                            if (scheduleBoard != null)
                            {
                                scheduleBoard.SetData(stylistColumns, apptCardList, selectedApptId);
                            }

                            if (lblBoardSummary != null)
                            {
                                int openAppts = booked + inChair;
                                lblBoardSummary.Text = $"👥 Guests: {uniqueClients.Count}  |  📅 Appts: {dt.Rows.Count}  |  🕒 Open: {openAppts}  |  🪑 Chair: {inChair}  |  ✅ Done: {done}  |  🔒 Billed: {billed}  |  💰 Services: Rs. {totalServicesValue:N0}";
                            }
                        }
                    }
                }

                if (gridAppointments.Columns["Id"] != null) gridAppointments.Columns["Id"].Visible = false;
                if (gridAppointments.Columns["CustomerId"] != null) gridAppointments.Columns["CustomerId"].Visible = false;
                if (gridAppointments.Columns["ServiceId"] != null) gridAppointments.Columns["ServiceId"].Visible = false;
                if (gridAppointments.Columns["ServiceIds"] != null) gridAppointments.Columns["ServiceIds"].Visible = false;
                if (gridAppointments.Columns["ServiceStaffIds"] != null) gridAppointments.Columns["ServiceStaffIds"].Visible = false;
                if (gridAppointments.Columns["StaffId"] != null) gridAppointments.Columns["StaffId"].Visible = false;
                if (gridAppointments.Columns["SaleId"] != null) gridAppointments.Columns["SaleId"].Visible = false;
                if (gridAppointments.Columns["InvoiceNumber"] != null) gridAppointments.Columns["InvoiceNumber"].Visible = false;
                if (gridAppointments.Columns["InvoiceTotal"] != null) gridAppointments.Columns["InvoiceTotal"].Visible = false;

                if (selectedApptId == 0)
                {
                    isSuppressingSelection = true;
                    gridAppointments.ClearSelection();
                    isSuppressingSelection = false;
                    ResetForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading appointments: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetViewMode(bool isTimeline)
        {
            if (scheduleBoard != null) scheduleBoard.Visible = isTimeline;
            if (gridAppointments != null) gridAppointments.Visible = !isTimeline;

            if (isTimeline)
            {
                btnViewTimeline.BackColor = Theme.Accent;
                btnViewTimeline.ForeColor = Color.White;
                btnViewList.BackColor = Color.FromArgb(30, 41, 59);
                btnViewList.ForeColor = Theme.TextDark;
            }
            else
            {
                btnViewList.BackColor = Theme.Accent;
                btnViewList.ForeColor = Color.White;
                btnViewTimeline.BackColor = Color.FromArgb(30, 41, 59);
                btnViewTimeline.ForeColor = Theme.TextDark;
            }
        }

        private void QuickBookSlot(int staffId, DateTime slotTime)
        {
            if (isLeftPanelCollapsed)
            {
                SetLeftPanelCollapsed(false);
            }

            // If the user already entered customer info or selected services, preserve them!
            bool hasEnteredData = (txtCustomerPhone != null && !string.IsNullOrEmpty(txtCustomerPhone.Text.Trim())) ||
                                  (txtCustomerName != null && !string.IsNullOrEmpty(txtCustomerName.Text.Trim())) ||
                                  selectedServiceIds.Count > 0;

            if (!hasEnteredData && selectedApptId == 0)
            {
                ResetForm();
            }

            if (comboStaff != null && staffId > 0)
            {
                SelectComboById(comboStaff, staffId);
            }
            if (dtpApptDate != null && dtpFilterDate != null)
            {
                dtpApptDate.Value = dtpFilterDate.Value.Date;
            }

            // Calculate duration from currently selected services if any
            int totalMinutes = 30;
            if (selectedServiceIds.Count > 0)
            {
                var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
                totalMinutes = selectedItems.Sum(s => s.DurationMinutes);
                if (totalMinutes <= 0) totalMinutes = 30;
            }

            DateTime endSlotTime = slotTime.AddMinutes(totalMinutes);
            SetTimeSlot($"{slotTime:hh:mm tt} - {endSlotTime:hh:mm tt}", totalMinutes);

            if (txtCustomerPhone != null && string.IsNullOrEmpty(txtCustomerPhone.Text.Trim()))
            {
                txtCustomerPhone.Focus();
            }
        }

        private void SelectAppointmentById(int apptId)
        {
            isExplicitUserSelection = true;
            selectedApptId = apptId;
            for (int i = 0; i < gridAppointments.Rows.Count; i++)
            {
                if (gridAppointments.Rows[i].Cells["Id"].Value != null &&
                    Convert.ToInt32(gridAppointments.Rows[i].Cells["Id"].Value) == apptId)
                {
                    gridAppointments.ClearSelection();
                    gridAppointments.Rows[i].Selected = true;
                    if (gridAppointments.Visible && gridAppointments.Columns["Client Name"] != null && gridAppointments.Columns["Client Name"].Visible)
                        gridAppointments.CurrentCell = gridAppointments.Rows[i].Cells["Client Name"];
                    GridAppointments_SelectionChanged(gridAppointments, EventArgs.Empty);
                    break;
                }
            }
            if (scheduleBoard != null)
            {
                scheduleBoard.SelectAppointment(apptId);
            }
        }

        private void GridAppointments_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= gridAppointments.Rows.Count) return;

            DataGridViewRow row = gridAppointments.Rows[e.RowIndex];
            string status = row.Cells["Status"]?.Value?.ToString() ?? "";

            if (status == "Billed")
            {
                // Billed row styling (clean dark blue-gray with orange accent status)
                e.CellStyle.BackColor = Color.FromArgb(24, 32, 47);
                e.CellStyle.SelectionBackColor = Color.FromArgb(40, 53, 75);
                e.CellStyle.SelectionForeColor = Color.FromArgb(254, 215, 170);

                if (gridAppointments.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
                {
                    e.Value = "🔒 🧾 Billed";
                    e.CellStyle.ForeColor = Color.FromArgb(251, 146, 60);
                    e.CellStyle.Font = new Font(gridAppointments.Font, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
                else
                {
                    e.CellStyle.ForeColor = Color.FromArgb(203, 213, 225);
                }
                return;
            }

            if (gridAppointments.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string val = e.Value.ToString();
                if (val == "Completed")
                {
                    e.Value = "✅ Completed (Pay Due)";
                    e.CellStyle.ForeColor = Theme.Success;
                    e.CellStyle.SelectionForeColor = Color.FromArgb(187, 247, 208);
                    e.CellStyle.Font = new Font(gridAppointments.Font, FontStyle.Bold);
                    e.FormattingApplied = true;
                }
                else if (val == "In-Chair")
                {
                    e.CellStyle.ForeColor = Color.FromArgb(56, 189, 248); // Sky blue
                    e.CellStyle.SelectionForeColor = Color.FromArgb(186, 230, 253);
                    e.CellStyle.Font = new Font(gridAppointments.Font, FontStyle.Bold);
                }
                else if (val == "Booked")
                {
                    e.CellStyle.ForeColor = Theme.Warning;
                    e.CellStyle.SelectionForeColor = Color.FromArgb(254, 240, 138);
                    e.CellStyle.Font = new Font(gridAppointments.Font, FontStyle.Bold);
                }
                else if (val == "Cancelled")
                {
                    e.CellStyle.ForeColor = Theme.Danger;
                    e.CellStyle.SelectionForeColor = Color.FromArgb(254, 202, 202);
                }
            }
        }

        private void GridAppointments_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            // Allow selection of all rows including Billed appointments
        }

        private void GridAppointments_SelectionChanged(object sender, EventArgs e)
        {
            if (isSuppressingSelection || !isExplicitUserSelection)
            {
                if (!isExplicitUserSelection && gridAppointments.SelectedRows.Count > 0)
                {
                    isSuppressingSelection = true;
                    gridAppointments.ClearSelection();
                    isSuppressingSelection = false;
                }
                return;
            }

            if (gridAppointments.SelectedRows.Count > 0)
            {
                DataGridViewRow row = gridAppointments.SelectedRows[0];
                string status = row.Cells["Status"]?.Value?.ToString() ?? "";

                if (row.Cells["Id"].Value != null && row.Cells["Id"].Value != DBNull.Value)
                {
                    selectedApptId = Convert.ToInt32(row.Cells["Id"].Value);
                    string apptNum = row.Cells["Appt #"]?.Value?.ToString() ?? $"#{selectedApptId}";
                    string invNum = row.Cells["InvoiceNumber"]?.Value?.ToString() ?? "";

                    int custId = row.Cells["CustomerId"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["CustomerId"].Value) : 0;
                    int staffId = row.Cells["StaffId"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["StaffId"].Value) : 0;

                    isSettingCustomerProgrammatically = true;
                    if (row.Cells["Client Name"] != null && row.Cells["Client Name"].Value != DBNull.Value)
                    {
                        txtCustomerName.Text = row.Cells["Client Name"].Value.ToString();
                    }
                    if (row.Cells["Client Phone"] != null && row.Cells["Client Phone"].Value != DBNull.Value)
                    {
                        txtCustomerPhone.Text = row.Cells["Client Phone"].Value.ToString();
                    }
                    isSettingCustomerProgrammatically = false;
                    HideCustomerSuggestions();

                    // 1. Date
                    if (DateTime.TryParse(row.Cells["Date"].Value?.ToString(), out DateTime d))
                    {
                        if (d.Date < DateTime.Today)
                        {
                            dtpApptDate.MinDate = d.Date;
                        }
                        else
                        {
                            dtpApptDate.MinDate = DateTime.Today;
                        }
                        dtpApptDate.Value = d.Date;
                    }

                    // 2. Primary Start Time Slot
                    string rawTimeSlot = row.Cells["Time Slot"].Value?.ToString() ?? "10:00 AM";
                    string startSlot = rawTimeSlot;
                    if (startSlot.Contains("–")) startSlot = startSlot.Split('–')[0].Trim();
                    else if (startSlot.Contains("-")) startSlot = startSlot.Split('-')[0].Trim();
                    SetTimeSlot(rawTimeSlot);

                    // 3. Primary Stylist
                    SelectComboById(comboStaff, staffId);

                    // 4. Load Selected Services & Per-Service Staff & Time Mappings
                    selectedServiceIds.Clear();
                    selectedServiceStaffMap.Clear();
                    selectedServiceTimeMap.Clear();

                    string serviceStaffIdsStr = row.Cells["ServiceStaffIds"]?.Value?.ToString();
                    if (!string.IsNullOrEmpty(serviceStaffIdsStr))
                    {
                        foreach (string part in serviceStaffIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                        {
                            string p = part.Trim();
                            if (p.Contains("|"))
                            {
                                string[] mainSub = p.Split('|');
                                string[] idSub = mainSub[0].Split(':');
                                if (idSub.Length >= 2 && int.TryParse(idSub[0].Trim(), out int rawSrvId) && int.TryParse(idSub[1].Trim(), out int stId))
                                {
                                    int resolvedSrvId = allServicesList.Any(s => s.Id == rawSrvId) 
                                        ? rawSrvId 
                                        : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSrvId || s.Code.EndsWith("-" + rawSrvId))?.Id ?? 0);

                                    if (resolvedSrvId > 0)
                                    {
                                        if (!selectedServiceIds.Contains(resolvedSrvId)) selectedServiceIds.Add(resolvedSrvId);
                                        selectedServiceStaffMap[resolvedSrvId] = stId;
                                        if (mainSub.Length >= 2 && !string.IsNullOrEmpty(mainSub[1].Trim()))
                                        {
                                            selectedServiceTimeMap[resolvedSrvId] = mainSub[1].Trim();
                                        }
                                    }
                                }
                            }
                            else
                            {
                                string[] sub = p.Split(':');
                                if (sub.Length >= 2 && int.TryParse(sub[0].Trim(), out int rawSrvId) && int.TryParse(sub[1].Trim(), out int stId))
                                {
                                    int resolvedSrvId = allServicesList.Any(s => s.Id == rawSrvId) 
                                        ? rawSrvId 
                                        : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSrvId || s.Code.EndsWith("-" + rawSrvId))?.Id ?? 0);

                                    if (resolvedSrvId > 0)
                                    {
                                        if (!selectedServiceIds.Contains(resolvedSrvId)) selectedServiceIds.Add(resolvedSrvId);
                                        selectedServiceStaffMap[resolvedSrvId] = stId;
                                        if (sub.Length == 4)
                                        {
                                            selectedServiceTimeMap[resolvedSrvId] = $"{sub[2].Trim()}:{sub[3].Trim()}";
                                        }
                                        else if (sub.Length == 3)
                                        {
                                            selectedServiceTimeMap[resolvedSrvId] = sub[2].Trim();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (selectedServiceIds.Count == 0)
                    {
                        string serviceIdsStr = row.Cells["ServiceIds"]?.Value?.ToString();
                        if (!string.IsNullOrEmpty(serviceIdsStr))
                        {
                            foreach (string part in serviceIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (int.TryParse(part.Trim(), out int rawId))
                                {
                                    int resolvedId = allServicesList.Any(s => s.Id == rawId) 
                                        ? rawId 
                                        : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawId || s.Code.EndsWith("-" + rawId))?.Id ?? 0);

                                    if (resolvedId > 0 && !selectedServiceIds.Contains(resolvedId))
                                    {
                                        selectedServiceIds.Add(resolvedId);
                                        selectedServiceStaffMap[resolvedId] = staffId;
                                        selectedServiceTimeMap[resolvedId] = startSlot;
                                    }
                                }
                            }
                        }
                        else if (row.Cells["ServiceId"].Value != DBNull.Value)
                        {
                            int rawSrvId = Convert.ToInt32(row.Cells["ServiceId"].Value);
                            int resolvedId = allServicesList.Any(s => s.Id == rawSrvId) 
                                ? rawSrvId 
                                : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSrvId || s.Code.EndsWith("-" + rawSrvId))?.Id ?? 0);

                            if (resolvedId > 0 && !selectedServiceIds.Contains(resolvedId))
                            {
                                selectedServiceIds.Add(resolvedId);
                                selectedServiceStaffMap[resolvedId] = staffId;
                                selectedServiceTimeMap[resolvedId] = startSlot;
                            }
                        }
                    }

                    // 5. Update Selection UI with fully restored time slots & stylists
                    FilterServiceCheckedList("");
                    UpdateServiceSelectionUI();

                    // 6. Status & Notes (enforce future date status options)
                    UpdateStatusOptionsForDate(d.Date);
                    comboStatus.SelectedItem = row.Cells["Status"].Value?.ToString() ?? "Booked";
                    txtNotes.Text = row.Cells["Notes"].Value?.ToString() ?? "";

                    decimal invTotal = (row.Cells["InvoiceTotal"] != null && row.Cells["InvoiceTotal"].Value != DBNull.Value) ? Convert.ToDecimal(row.Cells["InvoiceTotal"].Value) : 0m;

                    if (status == "Billed")
                    {
                        if (invTotal > 0)
                        {
                            lblCardTitle.Text = string.IsNullOrEmpty(invNum) ? $"🔒 🧾 Appt #{selectedApptId} (Billed - Rs. {invTotal:N0})" : $"🔒 🧾 Appt #{selectedApptId} (Billed - {invNum} • Rs. {invTotal:N0})";
                            lblServiceSummary.Text = $"✨ {selectedServiceIds.Count} service(s) (Billed Total: Rs. {invTotal:N0})";
                        }
                        else
                        {
                            lblCardTitle.Text = string.IsNullOrEmpty(invNum) ? $"🔒 🧾 Appt #{selectedApptId} (Billed)" : $"🔒 🧾 Appt #{selectedApptId} (Billed - {invNum})";
                        }
                        lblCardTitle.ForeColor = Theme.Accent;
                        btnBook.Text = "✏️ Update Appt";
                        btnCheckoutNow.Text = "✏️ 🧾 Adjust / Edit Bill";
                        btnCheckoutNow.BackColor = Color.FromArgb(245, 158, 11);
                    }
                    else
                    {
                        lblCardTitle.Text = $"✏️ Edit Appt #{selectedApptId}";
                        lblCardTitle.ForeColor = Theme.TextLight;
                        btnBook.Text = "✏️ Update Appt";
                        btnCheckoutNow.Text = "🚀 🧾 Bill / Checkout";
                        btnCheckoutNow.BackColor = Theme.Accent;
                    }
                    Theme.StylePrimaryButton(btnBook);
                }
            }
        }

        private void SelectComboById(ComboBox combo, int id)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is ComboBoxItem item && item.Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private DateTime FindNextAvailableSlot(SqlConnection conn, DateTime apptDate, int staffId, DateTime desiredStartTime, int durationMinutes, int excludeApptId)
        {
            if (staffId <= 0 || durationMinutes <= 0) return desiredStartTime;

            List<Tuple<DateTime, DateTime>> busyIntervals = new List<Tuple<DateTime, DateTime>>();

            try
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT Id, AppointmentTime, ServiceStaffIds, StaffId, Status 
                    FROM Appointments 
                    WHERE CAST(AppointmentDate AS DATE) = CAST(@dt AS DATE)
                      AND Status != 'Cancelled' 
                      AND (@exId = 0 OR Id != @exId)", conn))
                {
                    cmd.Parameters.AddWithValue("@dt", apptDate.Date);
                    cmd.Parameters.AddWithValue("@exId", excludeApptId);

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int aId = Convert.ToInt32(rdr["Id"]);
                            int mainStaffId = rdr["StaffId"] != DBNull.Value ? Convert.ToInt32(rdr["StaffId"]) : 0;
                            string rawSlot = rdr["AppointmentTime"]?.ToString() ?? "";
                            string srvStaffIds = rdr["ServiceStaffIds"]?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(srvStaffIds) && srvStaffIds.Contains(":"))
                            {
                                string[] entries = srvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                DateTime runningStart = ParseTimeSlotStatic(apptDate, rawSlot);

                                foreach (string entry in entries)
                                {
                                    string staffPart = entry.Trim();
                                    string timePart = "";
                                    if (staffPart.Contains("|"))
                                    {
                                        var tParts = staffPart.Split('|');
                                        staffPart = tParts[0].Trim();
                                        if (tParts.Length > 1) timePart = tParts[1].Trim();
                                    }

                                    var sParts = staffPart.Split(':');
                                    if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                                    {
                                        int resolvedSid = allServicesList.Any(s => s.Id == rawSid) 
                                            ? rawSid 
                                            : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid))?.Id ?? rawSid);

                                        var sMatch = allServicesList.FirstOrDefault(s => s.Id == resolvedSid);
                                        int sDur = sMatch?.DurationMinutes > 0 ? sMatch.DurationMinutes : 30;

                                        DateTime sStart = runningStart;
                                        DateTime sEnd = sStart.AddMinutes(sDur);
                                        if (!string.IsNullOrEmpty(timePart))
                                        {
                                            sStart = ParseTimeSlotStatic(apptDate, timePart);
                                            if (timePart.Contains("-") || timePart.Contains("–"))
                                            {
                                                string[] tp = timePart.Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                                                if (tp.Length == 2)
                                                {
                                                    DateTime t1 = ParseTimeSlotStatic(apptDate, tp[0].Trim());
                                                    DateTime t2 = ParseTimeSlotStatic(apptDate, tp[1].Trim());
                                                    if (t2 > t1)
                                                    {
                                                        sStart = t1;
                                                        sDur = (int)(t2 - t1).TotalMinutes;
                                                    }
                                                }
                                            }
                                            sEnd = sStart.AddMinutes(sDur);
                                        }
                                        runningStart = sEnd;

                                        if (sStaffId == staffId)
                                        {
                                            busyIntervals.Add(new Tuple<DateTime, DateTime>(sStart, sEnd));
                                        }
                                    }
                                }
                            }
                            else if (mainStaffId == staffId)
                            {
                                DateTime sStart = ParseTimeSlotStatic(apptDate, rawSlot);
                                int dur = 30;
                                if (rawSlot.Contains("–") || rawSlot.Contains("-"))
                                {
                                    string[] parts = rawSlot.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2)
                                    {
                                        DateTime t1 = ParseTimeSlotStatic(apptDate, parts[0].Trim());
                                        DateTime t2 = ParseTimeSlotStatic(apptDate, parts[1].Trim());
                                        if (t2 > t1) dur = (int)(t2 - t1).TotalMinutes;
                                    }
                                }
                                busyIntervals.Add(new Tuple<DateTime, DateTime>(sStart, sStart.AddMinutes(dur)));
                            }
                        }
                    }
                }
            }
            catch { }

            busyIntervals = busyIntervals.OrderBy(i => i.Item1).ToList();

            DateTime candidate = desiredStartTime;
            DateTime maxSalonClose = apptDate.Date.AddHours(21); // 9:00 PM closing

            while (candidate.AddMinutes(durationMinutes) <= maxSalonClose)
            {
                DateTime candEnd = candidate.AddMinutes(durationMinutes);
                var overlap = busyIntervals.FirstOrDefault(b => candidate < b.Item2 && candEnd > b.Item1);
                if (overlap == null)
                {
                    return candidate;
                }

                candidate = overlap.Item2;
                int remMin = candidate.Minute % 5;
                if (remMin != 0)
                {
                    candidate = candidate.AddMinutes(5 - remMin);
                }
            }

            return desiredStartTime;
        }

        private bool CheckStylistConflict(SqlConnection conn, DateTime apptDate, int staffId, DateTime startTime, DateTime endTime, int excludeApptId, out string conflictReason)
        {
            conflictReason = "";
            if (staffId <= 0 || startTime >= endTime) return false;

            try
            {
                using (SqlCommand cmd = new SqlCommand(@"
                    SELECT a.Id, a.AppointmentTime, a.ServiceStaffIds, a.StaffId, a.Status,
                           ISNULL(c.Name, 'Walk-in Client') AS CustomerName,
                           ISNULL(c.Phone, '') AS CustomerPhone
                    FROM Appointments a
                    LEFT JOIN Customers c ON a.CustomerId = c.Id
                    WHERE CAST(a.AppointmentDate AS DATE) = CAST(@dt AS DATE)
                      AND a.Status != 'Cancelled'
                      AND (@exId = 0 OR a.Id != @exId)", conn))
                {
                    cmd.Parameters.AddWithValue("@dt", apptDate.Date);
                    cmd.Parameters.AddWithValue("@exId", excludeApptId);

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            int aId = Convert.ToInt32(rdr["Id"]);
                            int mainStaffId = rdr["StaffId"] != DBNull.Value ? Convert.ToInt32(rdr["StaffId"]) : 0;
                            string rawSlot = rdr["AppointmentTime"]?.ToString() ?? "";
                            string srvStaffIds = rdr["ServiceStaffIds"]?.ToString() ?? "";
                            string custName = rdr["CustomerName"].ToString();

                            if (!string.IsNullOrEmpty(srvStaffIds) && srvStaffIds.Contains(":"))
                            {
                                string[] entries = srvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                DateTime runningStart = ParseTimeSlotStatic(apptDate, rawSlot);

                                foreach (string entry in entries)
                                {
                                    string staffPart = entry.Trim();
                                    string timePart = "";
                                    if (staffPart.Contains("|"))
                                    {
                                        var tParts = staffPart.Split('|');
                                        staffPart = tParts[0].Trim();
                                        if (tParts.Length > 1) timePart = tParts[1].Trim();
                                    }

                                    var sParts = staffPart.Split(':');
                                    if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                                    {
                                        int resolvedSid = allServicesList.Any(s => s.Id == rawSid)
                                            ? rawSid
                                            : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid))?.Id ?? rawSid);

                                        var sMatch = allServicesList.FirstOrDefault(s => s.Id == resolvedSid);
                                        int sDur = sMatch?.DurationMinutes > 0 ? sMatch.DurationMinutes : 30;

                                        DateTime sStart = runningStart;
                                        if (!string.IsNullOrEmpty(timePart))
                                        {
                                            sStart = ParseTimeSlotStatic(apptDate, timePart);
                                            if (timePart.Contains("-") || timePart.Contains("–"))
                                            {
                                                string[] subParts = timePart.Split(new[] { '-', '–' }, StringSplitOptions.RemoveEmptyEntries);
                                                if (subParts.Length == 2)
                                                {
                                                    DateTime t1 = ParseTimeSlotStatic(apptDate, subParts[0].Trim());
                                                    DateTime t2 = ParseTimeSlotStatic(apptDate, subParts[1].Trim());
                                                    if (t2 > t1) sDur = (int)(t2 - t1).TotalMinutes;
                                                }
                                            }
                                        }
                                        DateTime sEnd = sStart.AddMinutes(sDur);
                                        runningStart = sEnd;

                                        if (sStaffId == staffId)
                                        {
                                            if (startTime < sEnd && endTime > sStart)
                                            {
                                                conflictReason = $"Appt #{aId} for {custName} ({sStart:hh:mm tt} – {sEnd:hh:mm tt})";
                                                return true;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (mainStaffId == staffId)
                            {
                                DateTime sStart = ParseTimeSlotStatic(apptDate, rawSlot);
                                int dur = 30;
                                if (rawSlot.Contains("–") || rawSlot.Contains("-"))
                                {
                                    string[] parts = rawSlot.Split(new[] { '–', '-' }, StringSplitOptions.RemoveEmptyEntries);
                                    if (parts.Length == 2)
                                    {
                                        DateTime t1 = ParseTimeSlotStatic(apptDate, parts[0].Trim());
                                        DateTime t2 = ParseTimeSlotStatic(apptDate, parts[1].Trim());
                                        if (t2 > t1) dur = (int)(t2 - t1).TotalMinutes;
                                    }
                                }
                                DateTime sEnd = sStart.AddMinutes(dur);

                                if (startTime < sEnd && endTime > sStart)
                                {
                                    conflictReason = $"Appt #{aId} for {custName} ({sStart:hh:mm tt} – {sEnd:hh:mm tt})";
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Conflict check error: " + ex.Message);
            }

            return false;
        }

        private void BtnBook_Click(object sender, EventArgs e)
        {
            string custName = txtCustomerName.Text.Trim();
            string custPhone = txtCustomerPhone.Text.Trim();

            if (string.IsNullOrWhiteSpace(custName))
            {
                MessageBox.Show("Please enter the Client / Customer Name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCustomerName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(custPhone))
            {
                custPhone = "0000000000";
            }

            if (selectedServiceIds.Count == 0)
            {
                MessageBox.Show("Please select at least one requested Service for this appointment.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                BtnSelectServices_Click(btnSelectServices, EventArgs.Empty);
                return;
            }

            int staffId = (comboStaff.SelectedItem is ComboBoxItem stItem) ? stItem.Id : 0;
            if (staffId <= 0 && selectedServiceStaffMap.Count > 0)
            {
                foreach (int sId in selectedServiceIds)
                {
                    if (selectedServiceStaffMap.ContainsKey(sId) && selectedServiceStaffMap[sId] > 0)
                    {
                        staffId = selectedServiceStaffMap[sId];
                        break;
                    }
                }
            }

            if (staffId <= 0)
            {
                MessageBox.Show("Please select an assigned Stylist/Specialist for this appointment.", "Stylist Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                comboStaff.Focus();
                return;
            }

            DateTime apptDate = dtpApptDate.Value.Date;

            if (selectedApptId == 0 && apptDate < DateTime.Today)
            {
                MessageBox.Show("Appointments can only be scheduled for today or future dates.", "Invalid Date", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                dtpApptDate.Focus();
                return;
            }

            string status = comboStatus.SelectedItem?.ToString() ?? "Booked";

            // BUSINESS RULE: Future date appointments can ONLY be Booked or Cancelled
            if (apptDate > DateTime.Today && status != "Booked" && status != "Cancelled")
            {
                MessageBox.Show($"Future date appointments ({apptDate:dd-MM-yyyy}) can only have status 'Booked' or 'Cancelled'.\n\n'In-Chair', 'Completed', and 'Billed' statuses can only be activated on the appointment date when the client arrives.", "Future Date Restriction", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                UpdateStatusOptionsForDate(apptDate);
                comboStatus.SelectedItem = "Booked";
                return;
            }

            string apptTime = GetSelectedTimeSlot();
            string notes = txtNotes.Text.Trim();

            // Prepare Service IDs and concatenated Service Names with assigned Stylists & Sequential Timeline
            var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
            int primaryServiceId = selectedItems.Count > 0 ? selectedItems[0].Id : 0;
            string serviceIdsCsv = string.Join(",", selectedItems.Select(s => s.Id));

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    DateTime baseDate = dtpApptDate.Value.Date;
                    DateTime apptStart = GetSelectedFromTime(baseDate);
                    DateTime apptEnd = GetSelectedToTime(baseDate);
                    int customDuration = (int)(apptEnd - apptStart).TotalMinutes;
                    if (customDuration < 5) customDuration = 5;

                    DateTime runningTime = apptStart;
                    DateTime overallStartTime = apptStart;
                    DateTime overallEndTime = apptStart;

                    List<string> serviceStaffIdPairs = new List<string>();
                    List<string> serviceNamesWithTimeline = new List<string>();
                    List<string> stylistChainList = new List<string>();
                    List<string> notificationLines = new List<string>();

                    int seq = 1;
                    foreach (var srv in selectedItems)
                    {
                        int dur = (selectedItems.Count == 1)
                            ? customDuration
                            : (srv.DurationMinutes > 0 ? srv.DurationMinutes : 30);

                        int itemStaffId = (selectedItems.Count > 1 && selectedServiceStaffMap.ContainsKey(srv.Id) && selectedServiceStaffMap[srv.Id] > 0)
                            ? selectedServiceStaffMap[srv.Id]
                            : (staffId > 0 ? staffId : 0);

                        // Find next immediate available slot for this stylist starting from runningTime
                        DateTime srvStartTime = FindNextAvailableSlot(conn, apptDate, itemStaffId, runningTime, dur, selectedApptId);
                        DateTime srvEndTime = srvStartTime.AddMinutes(dur);

                        if (seq == 1)
                        {
                            overallStartTime = srvStartTime;
                        }
                        if (srvEndTime > overallEndTime)
                        {
                            overallEndTime = srvEndTime;
                        }

                        // Next service in the sequence starts after this service completes
                        runningTime = srvEndTime;

                        string itemTimeSlot = $"{srvStartTime:hh:mm tt} - {srvEndTime:hh:mm tt}";

                        string staffName = "Stylist";
                        if (comboStaff != null)
                        {
                            foreach (var itm in comboStaff.Items)
                            {
                                if (itm is ComboBoxItem cbItm && cbItm.Id == itemStaffId)
                                {
                                    string raw = cbItm.Display;
                                    int pIdx = raw.IndexOf('(');
                                    staffName = pIdx > 0 ? raw.Substring(0, pIdx).Trim() : raw.Trim();
                                    break;
                                }
                            }
                        }

                        serviceStaffIdPairs.Add($"{srv.Id}:{itemStaffId}|{itemTimeSlot}");
                        serviceNamesWithTimeline.Add($"#{seq} {srv.Name} [{itemTimeSlot} • {staffName}]");
                        if (!stylistChainList.Contains(staffName)) stylistChainList.Add(staffName);
                        notificationLines.Add($"  • #{seq} {srv.Name} ({dur}m) → Stylist: {staffName} | Time: {itemTimeSlot}");
                        seq++;
                    }

                    string fullSpanTimeSlot = $"{overallStartTime:hh:mm tt} – {overallEndTime:hh:mm tt}";
                    string serviceStaffIdsCsv = string.Join(",", serviceStaffIdPairs);
                    string serviceNamesCsv = string.Join(" ➜ ", serviceNamesWithTimeline);

                    int custId = 0;
                    if (!string.IsNullOrEmpty(custPhone) && custPhone != "0000000000")
                    {
                        using (SqlCommand findCmd = new SqlCommand("SELECT TOP 1 Id FROM Customers WHERE Phone = @phone", conn))
                        {
                            findCmd.Parameters.AddWithValue("@phone", custPhone);
                            object obj = findCmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                custId = Convert.ToInt32(obj);
                                using (SqlCommand updCmd = new SqlCommand("UPDATE Customers SET Name = @name WHERE Id = @id", conn))
                                {
                                    updCmd.Parameters.AddWithValue("@name", custName);
                                    updCmd.Parameters.AddWithValue("@id", custId);
                                    updCmd.ExecuteNonQuery();
                                }
                            }
                        }
                    }

                    if (custId == 0)
                    {
                        using (SqlCommand findNameCmd = new SqlCommand("SELECT TOP 1 Id FROM Customers WHERE Name = @name", conn))
                        {
                            findNameCmd.Parameters.AddWithValue("@name", custName);
                            object obj = findNameCmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value)
                            {
                                custId = Convert.ToInt32(obj);
                                if (!string.IsNullOrEmpty(custPhone) && custPhone != "0000000000")
                                {
                                    using (SqlCommand updCmd = new SqlCommand("UPDATE Customers SET Phone = @phone WHERE Id = @id", conn))
                                    {
                                        updCmd.Parameters.AddWithValue("@phone", custPhone);
                                        updCmd.Parameters.AddWithValue("@id", custId);
                                        updCmd.ExecuteNonQuery();
                                    }
                                }
                            }
                        }
                    }

                    if (custId == 0)
                    {
                        using (SqlCommand insCmd = new SqlCommand(@"
                            INSERT INTO Customers (Name, Phone, Email, Address)
                            OUTPUT INSERTED.Id
                            VALUES (@name, @phone, '', 'Local')", conn))
                        {
                            insCmd.Parameters.AddWithValue("@name", custName);
                            insCmd.Parameters.AddWithValue("@phone", custPhone);
                            custId = (int)insCmd.ExecuteScalar();
                        }
                    }

                    if (selectedApptId == 0)
                    {
                        string apptNum = "APT-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                        using (SqlCommand cmd = new SqlCommand(@"
                            INSERT INTO Appointments (AppointmentNumber, CustomerId, StaffId, ServiceId, ServiceIds, ServiceNames, ServiceStaffIds, AppointmentDate, AppointmentTime, Status, Notes)
                            VALUES (@num, @custId, @staffId, @srvId, @srvIds, @srvNames, @srvStaffIds, @date, @time, @status, @notes)", conn))
                        {
                            cmd.Parameters.AddWithValue("@num", apptNum);
                            cmd.Parameters.AddWithValue("@custId", custId);
                            cmd.Parameters.AddWithValue("@staffId", staffId > 0 ? (object)staffId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@srvId", (primaryServiceId > 0 && allServicesList.Any(s => s.Id == primaryServiceId)) ? (object)primaryServiceId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@srvIds", serviceIdsCsv);
                            cmd.Parameters.AddWithValue("@srvNames", serviceNamesCsv);
                            cmd.Parameters.AddWithValue("@srvStaffIds", serviceStaffIdsCsv);
                            cmd.Parameters.AddWithValue("@date", apptDate);
                            cmd.Parameters.AddWithValue("@time", fullSpanTimeSlot);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@notes", notes);
                            cmd.ExecuteNonQuery();
                        }
                        MessageBox.Show($"Appointment scheduled successfully for {custName}!\n\n🕒 Sequential Service Schedule:\n{string.Join("\n", notificationLines)}\n\nTotal Window: {fullSpanTimeSlot}", "Booked", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        // Security check: If editing an appointment that is currently Billed, require Admin Authentication
                        string currentGridStatus = "";
                        foreach (DataGridViewRow r in gridAppointments.SelectedRows)
                        {
                            if (r.Cells["Id"]?.Value != null && Convert.ToInt32(r.Cells["Id"].Value) == selectedApptId)
                            {
                                currentGridStatus = r.Cells["Status"]?.Value?.ToString() ?? "";
                                break;
                            }
                        }

                        if (string.Equals(currentGridStatus, "Billed", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!AdminAuthDialog.VerifyAdmin(this, "modify details of this billed appointment"))
                            {
                                return;
                            }
                        }

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE Appointments
                            SET CustomerId = @custId, StaffId = @staffId, ServiceId = @srvId, ServiceIds = @srvIds, ServiceNames = @srvNames, ServiceStaffIds = @srvStaffIds,
                                AppointmentDate = @date, AppointmentTime = @time, Status = @status, Notes = @notes
                            WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@custId", custId);
                            cmd.Parameters.AddWithValue("@staffId", staffId > 0 ? (object)staffId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@srvId", (primaryServiceId > 0 && allServicesList.Any(s => s.Id == primaryServiceId)) ? (object)primaryServiceId : DBNull.Value);
                            cmd.Parameters.AddWithValue("@srvIds", serviceIdsCsv);
                            cmd.Parameters.AddWithValue("@srvNames", serviceNamesCsv);
                            cmd.Parameters.AddWithValue("@srvStaffIds", serviceStaffIdsCsv);
                            cmd.Parameters.AddWithValue("@date", apptDate);
                            cmd.Parameters.AddWithValue("@time", fullSpanTimeSlot);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@notes", notes);
                            cmd.Parameters.AddWithValue("@id", selectedApptId);
                            cmd.ExecuteNonQuery();
                        }

                        // Synchronize linked Sales and SaleDetails invoice records if this appointment was billed
                        int linkedSaleId = 0;
                        using (SqlCommand cmdFindSale = new SqlCommand("SELECT ISNULL(SaleId, 0) FROM Appointments WHERE Id = @id", conn))
                        {
                            cmdFindSale.Parameters.AddWithValue("@id", selectedApptId);
                            object obj = cmdFindSale.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value && Convert.ToInt32(obj) > 0)
                            {
                                linkedSaleId = Convert.ToInt32(obj);
                            }
                        }
                        if (linkedSaleId == 0)
                        {
                            using (SqlCommand cmdFindSale2 = new SqlCommand("SELECT TOP 1 Id FROM Sales WHERE AppointmentId = @id ORDER BY Id DESC", conn))
                            {
                                cmdFindSale2.Parameters.AddWithValue("@id", selectedApptId);
                                object obj = cmdFindSale2.ExecuteScalar();
                                if (obj != null && obj != DBNull.Value && Convert.ToInt32(obj) > 0)
                                {
                                    linkedSaleId = Convert.ToInt32(obj);
                                }
                            }
                        }

                        if (linkedSaleId > 0)
                        {
                            try
                            {
                                bool isGst = true;
                                decimal discountVal = 0m;
                                string paymentMethod = "Cash";

                                using (SqlCommand cmdGetSale = new SqlCommand("SELECT CustomerId, IsGSTBill, Discount, PaymentMethod FROM Sales WHERE Id = @sId", conn))
                                {
                                    cmdGetSale.Parameters.AddWithValue("@sId", linkedSaleId);
                                    using (SqlDataReader rdr = cmdGetSale.ExecuteReader())
                                    {
                                        if (rdr.Read())
                                        {
                                            isGst = rdr["IsGSTBill"] != DBNull.Value && Convert.ToBoolean(rdr["IsGSTBill"]);
                                            discountVal = rdr["Discount"] != DBNull.Value ? Convert.ToDecimal(rdr["Discount"]) : 0m;
                                            paymentMethod = rdr["PaymentMethod"]?.ToString() ?? "Cash";
                                        }
                                    }
                                }

                                if (selectedItems.Count > 0)
                                {
                                    // Remove old service lines and re-insert with updated service pricing and assigned stylist
                                    using (SqlCommand cmdDel = new SqlCommand("DELETE FROM SaleDetails WHERE SaleId = @sId AND ItemType = 'Service'", conn))
                                    {
                                        cmdDel.Parameters.AddWithValue("@sId", linkedSaleId);
                                        cmdDel.ExecuteNonQuery();
                                    }

                                    decimal totalTaxable = 0;
                                    decimal totalCGST = 0;
                                    decimal totalSGST = 0;
                                    decimal totalTax = 0;
                                    decimal subTotal = 0;

                                    foreach (var srv in selectedItems)
                                    {
                                        int itemStaffId = (selectedItems.Count > 1 && selectedServiceStaffMap.ContainsKey(srv.Id) && selectedServiceStaffMap[srv.Id] > 0)
                                            ? selectedServiceStaffMap[srv.Id]
                                            : (staffId > 0 ? staffId : 0);

                                        decimal price = srv.Price;
                                        decimal gstRate = srv.GSTRate > 0 ? srv.GSTRate : 18.00m;
                                        string sacCode = !string.IsNullOrEmpty(srv.SACCode) ? srv.SACCode : "999721";

                                        decimal taxable = 0;
                                        decimal cgst = 0;
                                        decimal sgst = 0;
                                        decimal tax = 0;

                                        if (isGst)
                                        {
                                            taxable = Math.Round(price / (1.00m + (gstRate / 100.00m)), 2);
                                            tax = price - taxable;
                                            cgst = Math.Round(tax / 2.00m, 2);
                                            sgst = tax - cgst;
                                        }
                                        else
                                        {
                                            taxable = price;
                                            gstRate = 0.00m;
                                        }

                                        totalTaxable += taxable;
                                        totalCGST += cgst;
                                        totalSGST += sgst;
                                        totalTax += tax;
                                        subTotal += price;

                                        using (SqlCommand cmdInsSrv = new SqlCommand(@"
                                            INSERT INTO SaleDetails (
                                                SaleId, ItemType, ProductId, ServiceId, StaffId, Quantity, UnitPrice, Total, PurchaseCostAtSale,
                                                HSNSAC, GSTRate, TaxableAmount, CGSTAmount, SGSTAmount, IGSTAmount
                                            )
                                            VALUES (
                                                @saleId, 'Service', NULL, @srvId, @stId, 1, @price, @tot, 0.00,
                                                @hsn, @gstRate, @taxable, @cgst, @sgst, 0.00
                                            )", conn))
                                        {
                                            cmdInsSrv.Parameters.AddWithValue("@saleId", linkedSaleId);
                                            cmdInsSrv.Parameters.AddWithValue("@srvId", srv.Id);
                                            cmdInsSrv.Parameters.AddWithValue("@stId", itemStaffId > 0 ? (object)itemStaffId : DBNull.Value);
                                            cmdInsSrv.Parameters.AddWithValue("@price", price);
                                            cmdInsSrv.Parameters.AddWithValue("@tot", price);
                                            cmdInsSrv.Parameters.AddWithValue("@hsn", sacCode);
                                            cmdInsSrv.Parameters.AddWithValue("@gstRate", gstRate);
                                            cmdInsSrv.Parameters.AddWithValue("@taxable", taxable);
                                            cmdInsSrv.Parameters.AddWithValue("@cgst", cgst);
                                            cmdInsSrv.Parameters.AddWithValue("@sgst", sgst);
                                            cmdInsSrv.ExecuteNonQuery();
                                        }
                                    }

                                    // Add any product subtotals if present
                                    using (SqlCommand cmdProdSum = new SqlCommand(@"
                                        SELECT ISNULL(SUM(Total), 0), ISNULL(SUM(TaxableAmount), 0), ISNULL(SUM(CGSTAmount), 0), ISNULL(SUM(SGSTAmount), 0)
                                        FROM SaleDetails WHERE SaleId = @sId AND ItemType = 'Product'", conn))
                                    {
                                        cmdProdSum.Parameters.AddWithValue("@sId", linkedSaleId);
                                        using (SqlDataReader rdrProd = cmdProdSum.ExecuteReader())
                                        {
                                            if (rdrProd.Read())
                                            {
                                                subTotal += Convert.ToDecimal(rdrProd[0]);
                                                totalTaxable += Convert.ToDecimal(rdrProd[1]);
                                                totalCGST += Convert.ToDecimal(rdrProd[2]);
                                                totalSGST += Convert.ToDecimal(rdrProd[3]);
                                                totalTax += (Convert.ToDecimal(rdrProd[2]) + Convert.ToDecimal(rdrProd[3]));
                                            }
                                        }
                                    }

                                    decimal grandTotal = Math.Max(0, subTotal - discountVal);

                                    // Update Sales Header
                                    using (SqlCommand cmdUpdSale = new SqlCommand(@"
                                        UPDATE Sales SET
                                            CustomerId = @cust,
                                            SaleDate = @sDate,
                                            SubTotal = @sub,
                                            Tax = @tx,
                                            GrandTotal = @grand,
                                            AmountPaid = @paid,
                                            TaxableAmount = @taxable,
                                            CGSTAmount = @cgst,
                                            SGSTAmount = @sgst,
                                            CashAmount = CASE WHEN PaymentMethod = 'Cash' THEN @grand ELSE CashAmount END,
                                            OnlineAmount = CASE WHEN PaymentMethod != 'Cash' AND PaymentMethod != 'Split' THEN @grand ELSE OnlineAmount END
                                        WHERE Id = @sId", conn))
                                    {
                                        cmdUpdSale.Parameters.AddWithValue("@cust", custId);
                                        cmdUpdSale.Parameters.AddWithValue("@sDate", apptDate);
                                        cmdUpdSale.Parameters.AddWithValue("@sub", subTotal);
                                        cmdUpdSale.Parameters.AddWithValue("@tx", isGst ? totalTax : 0.00m);
                                        cmdUpdSale.Parameters.AddWithValue("@grand", grandTotal);
                                        cmdUpdSale.Parameters.AddWithValue("@paid", grandTotal);
                                        cmdUpdSale.Parameters.AddWithValue("@taxable", totalTaxable);
                                        cmdUpdSale.Parameters.AddWithValue("@cgst", isGst ? totalCGST : 0.00m);
                                        cmdUpdSale.Parameters.AddWithValue("@sgst", isGst ? totalSGST : 0.00m);
                                        cmdUpdSale.Parameters.AddWithValue("@sId", linkedSaleId);
                                        cmdUpdSale.ExecuteNonQuery();
                                    }
                                }
                                else if (staffId > 0)
                                {
                                    using (SqlCommand cmdUpdStaff = new SqlCommand(@"
                                        UPDATE SaleDetails SET StaffId = @stId WHERE SaleId = @sId AND ItemType = 'Service'", conn))
                                    {
                                        cmdUpdStaff.Parameters.AddWithValue("@stId", staffId);
                                        cmdUpdStaff.Parameters.AddWithValue("@sId", linkedSaleId);
                                        cmdUpdStaff.ExecuteNonQuery();
                                    }
                                    using (SqlCommand cmdUpdCust = new SqlCommand(@"
                                        UPDATE Sales SET CustomerId = @cust, SaleDate = @sDate WHERE Id = @sId", conn))
                                    {
                                        cmdUpdCust.Parameters.AddWithValue("@cust", custId);
                                        cmdUpdCust.Parameters.AddWithValue("@sDate", apptDate);
                                        cmdUpdCust.Parameters.AddWithValue("@sId", linkedSaleId);
                                        cmdUpdCust.ExecuteNonQuery();
                                    }
                                }
                            }
                            catch { }
                        }

                        MessageBox.Show($"Appointment updated successfully for {custName}!\n\n🕒 Sequential Service Schedule:\n{string.Join("\n", notificationLines)}\n\nTotal Window: {fullSpanTimeSlot}", "Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                LoadDropdowns();
                ResetForm();
                LoadAppointments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving appointment: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSelectedStatus(string newStatus, int specificServiceId = 0)
        {
            if (gridAppointments.SelectedRows.Count == 0 && selectedApptId == 0)
            {
                MessageBox.Show("Please select an active appointment from the queue list.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int currentId = selectedApptId;
            if (currentId <= 0 && gridAppointments.SelectedRows.Count > 0)
            {
                currentId = Convert.ToInt32(gridAppointments.SelectedRows[0].Cells["Id"].Value);
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    string existingSrvStaffIds = "";
                    string currentStatus = "";
                    DateTime apptDate = DateTime.Today;

                    using (SqlCommand cmdGet = new SqlCommand("SELECT ServiceStaffIds, Status, AppointmentDate FROM Appointments WHERE Id = @id", conn))
                    {
                        cmdGet.Parameters.AddWithValue("@id", currentId);
                        using (SqlDataReader rdr = cmdGet.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                existingSrvStaffIds = rdr["ServiceStaffIds"]?.ToString() ?? "";
                                currentStatus = rdr["Status"]?.ToString() ?? "";
                                if (rdr["AppointmentDate"] != DBNull.Value) apptDate = Convert.ToDateTime(rdr["AppointmentDate"]);
                            }
                        }
                    }

                    if (currentStatus == "Billed")
                    {
                        if (!AdminAuthDialog.VerifyAdmin(this, "change status of this finalized billed appointment"))
                        {
                            return;
                        }
                    }

                    if (apptDate.Date > DateTime.Today && newStatus != "Booked" && newStatus != "Cancelled")
                    {
                        MessageBox.Show($"Cannot change status to '{newStatus}' for future date appointments ({apptDate:dd-MM-yyyy}).\n\nServices can only be moved to 'In-Chair' or 'Completed' on the scheduled appointment date when the client arrives.", "Future Appointment Restriction", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!string.IsNullOrEmpty(existingSrvStaffIds) && existingSrvStaffIds.Contains(":"))
                    {
                        string[] entries = existingSrvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> updatedEntries = new List<string>();

                        bool anyInChair = false;
                        bool allCompleted = true;

                        for (int i = 0; i < entries.Length; i++)
                        {
                            string entry = entries[i].Trim();
                            string staffPart = entry;
                            string timePart = "";
                            string itemStatus = "Booked";

                            if (staffPart.Contains("|"))
                            {
                                var tParts = staffPart.Split('|');
                                staffPart = tParts[0].Trim();
                                if (tParts.Length > 1) timePart = tParts[1].Trim();
                                if (tParts.Length > 2) itemStatus = tParts[2].Trim();
                            }

                            var sParts = staffPart.Split(':');
                            if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                            {
                                int resolvedSid = allServicesList.Any(s => s.Id == rawSid)
                                    ? rawSid
                                    : (allServicesList.FirstOrDefault(s => s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid))?.Id ?? rawSid);

                                bool isTarget = (specificServiceId > 0 && resolvedSid == specificServiceId) || (specificServiceId <= 0 && entries.Length == 1);

                                if (isTarget)
                                {
                                    itemStatus = newStatus;
                                }
                                else if (specificServiceId <= 0)
                                {
                                    // Global button click (e.g. Move to Chair)
                                    if (newStatus == "In-Chair")
                                    {
                                        // ONLY move the first active / unfinished service to In-Chair, others remain Booked!
                                        if (!anyInChair && itemStatus != "Completed")
                                        {
                                            itemStatus = "In-Chair";
                                            anyInChair = true;
                                        }
                                        else if (itemStatus == "In-Chair")
                                        {
                                            itemStatus = "Completed";
                                        }
                                    }
                                    else
                                    {
                                        itemStatus = newStatus;
                                    }
                                }
                                else if (newStatus == "In-Chair" && itemStatus == "In-Chair")
                                {
                                    // A client cannot be in two chairs simultaneously! If this service is now In-Chair, previous In-Chair service becomes Completed!
                                    itemStatus = "Completed";
                                }

                                if (itemStatus == "In-Chair") anyInChair = true;
                                if (itemStatus != "Completed" && itemStatus != "Billed") allCompleted = false;

                                string formattedEntry = $"{rawSid}:{sStaffId}|{timePart}|{itemStatus}";
                                updatedEntries.Add(formattedEntry);
                            }
                            else
                            {
                                updatedEntries.Add(entry);
                            }
                        }

                        string overallStatus = newStatus;
                        if (newStatus == "In-Chair") overallStatus = "In-Chair";
                        else if (allCompleted) overallStatus = "Completed";
                        else if (anyInChair) overallStatus = "In-Chair";
                        else if (newStatus != "Cancelled" && newStatus != "Billed") overallStatus = "Booked";

                        string updatedSrvStaffCsv = string.Join(",", updatedEntries);

                        using (SqlCommand cmd = new SqlCommand("UPDATE Appointments SET Status = @status, ServiceStaffIds = @srvStaffIds WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@status", overallStatus);
                            cmd.Parameters.AddWithValue("@srvStaffIds", updatedSrvStaffCsv);
                            cmd.Parameters.AddWithValue("@id", currentId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        using (SqlCommand cmd = new SqlCommand("UPDATE Appointments SET Status = @status WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@status", newStatus);
                            cmd.Parameters.AddWithValue("@id", currentId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                LoadAppointments();
                SelectAppointmentById(currentId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating status: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void UpdateAppointmentSlotAndStaff(AppointmentCardModel appt, int newStaffId, DateTime newStartTime, DateTime newEndTime)
        {
            if (appt == null || appt.Id <= 0) return;

            try
            {
                // Security check: If editing an appointment that is currently Billed, require Admin Authentication
                if (string.Equals(appt.Status, "Billed", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AdminAuthDialog.VerifyAdmin(this, "reschedule this finalized billed appointment"))
                    {
                        return;
                    }
                }

                int dur = appt.DurationMinutes > 0 ? appt.DurationMinutes : (int)(newEndTime - newStartTime).TotalMinutes;
                if (dur <= 0) dur = 30;

                string newTimeSlot = $"{newStartTime:hh:mm tt} - {newEndTime:hh:mm tt}";

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Automatically resolve to nearby next available slot if target time has overlap
                    DateTime resolvedStart = FindNextAvailableSlot(conn, appt.StartTime.Date, newStaffId, newStartTime, dur, appt.Id);
                    DateTime resolvedEnd = resolvedStart.AddMinutes(dur);
                    newStartTime = resolvedStart;
                    newEndTime = resolvedEnd;
                    newTimeSlot = $"{newStartTime:hh:mm tt} - {newEndTime:hh:mm tt}";

                    // Check if this appointment has multiple services in ServiceStaffIds
                    string existingSrvStaffIds = "";
                    string currentServiceIds = "";
                    using (SqlCommand cmdGet = new SqlCommand("SELECT ServiceStaffIds, ServiceIds, StaffId, AppointmentTime FROM Appointments WHERE Id = @id", conn))
                    {
                        cmdGet.Parameters.AddWithValue("@id", appt.Id);
                        using (SqlDataReader rdr = cmdGet.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                existingSrvStaffIds = rdr["ServiceStaffIds"]?.ToString() ?? "";
                                currentServiceIds = rdr["ServiceIds"]?.ToString() ?? "";
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(existingSrvStaffIds) && existingSrvStaffIds.Contains(":"))
                    {
                        // Multi-service appointment: update the moved service segment
                        string[] entries = existingSrvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> updatedEntries = new List<string>();
                        int targetServiceIdToUpdate = appt.SpecificServiceId;
                        DateTime earliestStart = newStartTime;
                        DateTime latestEnd = newEndTime;

                        foreach (string entry in entries)
                        {
                            string staffPart = entry.Trim();
                            string timePart = "";
                            if (staffPart.Contains("|"))
                            {
                                var tParts = staffPart.Split('|');
                                staffPart = tParts[0].Trim();
                                if (tParts.Length > 1) timePart = tParts[1].Trim();
                            }

                            var sParts = staffPart.Split(':');
                            if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                            {
                                var sMatch = allServicesList.FirstOrDefault(s => s.Id == rawSid || s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid));
                                int resolvedSid = sMatch != null ? sMatch.Id : rawSid;
                                string sName = sMatch != null ? sMatch.Name : "";

                                bool isMatch = (targetServiceIdToUpdate > 0 && resolvedSid == targetServiceIdToUpdate)
                                               || (entries.Length == 1)
                                               || (sMatch != null && appt.ServiceNames != null && appt.ServiceNames.Contains(sName));

                                if (isMatch)
                                {
                                    targetServiceIdToUpdate = resolvedSid;
                                    string updatedSlot = $"{newStartTime:hh:mm tt} - {newEndTime:hh:mm tt}";
                                    updatedEntries.Add($"{rawSid}:{newStaffId}|{updatedSlot}");
                                }
                                else
                                {
                                    updatedEntries.Add(entry.Trim());
                                    if (!string.IsNullOrEmpty(timePart))
                                    {
                                        DateTime otherStart = ParseTimeSlotStatic(appt.StartTime.Date, timePart);
                                        if (otherStart < earliestStart) earliestStart = otherStart;
                                        var otherEndPart = timePart.Contains("-") ? timePart.Split('-')[1].Trim() : (timePart.Contains("–") ? timePart.Split('–')[1].Trim() : "");
                                        if (!string.IsNullOrEmpty(otherEndPart))
                                        {
                                            DateTime otherEnd = ParseTimeSlotStatic(appt.StartTime.Date, otherEndPart);
                                            if (otherEnd > latestEnd) latestEnd = otherEnd;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                updatedEntries.Add(entry.Trim());
                            }
                        }

                        string updatedSrvStaffCsv = string.Join(",", updatedEntries);
                        string overallTimeSlot = (entries.Length == 1) ? newTimeSlot : $"{earliestStart:hh:mm tt} - {latestEnd:hh:mm tt}";
                        int overallStaffId = (entries.Length == 1) ? newStaffId : newStaffId;

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE Appointments 
                            SET AppointmentTime = @time,
                                StaffId = @staffId,
                                ServiceStaffIds = @srvStaffIds 
                            WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@time", overallTimeSlot);
                            cmd.Parameters.AddWithValue("@staffId", overallStaffId);
                            cmd.Parameters.AddWithValue("@srvStaffIds", updatedSrvStaffCsv);
                            cmd.Parameters.AddWithValue("@id", appt.Id);
                            cmd.ExecuteNonQuery();
                        }

                        // Update ONLY this specific service row in SaleDetails if linked to an active sale
                        if (appt.SaleId > 0 && targetServiceIdToUpdate > 0)
                        {
                            using (SqlCommand cmdSaleStaff = new SqlCommand(@"
                                UPDATE SaleDetails 
                                SET StaffId = @stId 
                                WHERE SaleId = @sId AND ServiceId = @srvId AND ItemType = 'Service'", conn))
                            {
                                cmdSaleStaff.Parameters.AddWithValue("@stId", newStaffId);
                                cmdSaleStaff.Parameters.AddWithValue("@sId", appt.SaleId);
                                cmdSaleStaff.Parameters.AddWithValue("@srvId", targetServiceIdToUpdate);
                                cmdSaleStaff.ExecuteNonQuery();
                            }
                        }
                    }
                    else
                    {
                        // Single service appointment
                        string srvIdToUse = !string.IsNullOrEmpty(currentServiceIds) ? currentServiceIds : (appt.ServiceIds ?? appt.SpecificServiceId.ToString());
                        string updatedSrvStaffCsv = $"{srvIdToUse}:{newStaffId}|{newTimeSlot}";

                        using (SqlCommand cmd = new SqlCommand(@"
                            UPDATE Appointments 
                            SET AppointmentTime = @time, 
                                StaffId = @staffId, 
                                ServiceStaffIds = @srvStaffIds 
                            WHERE Id = @id", conn))
                        {
                            cmd.Parameters.AddWithValue("@time", newTimeSlot);
                            cmd.Parameters.AddWithValue("@staffId", newStaffId);
                            cmd.Parameters.AddWithValue("@srvStaffIds", updatedSrvStaffCsv);
                            cmd.Parameters.AddWithValue("@id", appt.Id);
                            cmd.ExecuteNonQuery();
                        }

                        if (appt.SaleId > 0)
                        {
                            using (SqlCommand cmdSaleStaff = new SqlCommand("UPDATE SaleDetails SET StaffId = @stId WHERE SaleId = @sId AND ItemType = 'Service'", conn))
                            {
                                cmdSaleStaff.Parameters.AddWithValue("@stId", newStaffId);
                                cmdSaleStaff.Parameters.AddWithValue("@sId", appt.SaleId);
                                cmdSaleStaff.ExecuteNonQuery();
                            }
                        }
                    }
                }

                // Refresh timeline & grid
                LoadAppointments();
                SelectAppointmentById(appt.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error moving appointment: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateAppointmentDuration(AppointmentCardModel appt, int newDurationMinutes, DateTime newEndTime)
        {
            if (appt == null || appt.Id <= 0) return;

            try
            {
                // Security check: If editing an appointment that is currently Billed, require Admin Authentication
                if (string.Equals(appt.Status, "Billed", StringComparison.OrdinalIgnoreCase))
                {
                    if (!AdminAuthDialog.VerifyAdmin(this, "modify duration of this finalized billed appointment"))
                    {
                        return;
                    }
                }

                string newTimeSlot = $"{appt.StartTime:hh:mm tt} - {newEndTime:hh:mm tt}";

                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();

                    // Check for overlap conflict before extending duration
                    if (CheckStylistConflict(conn, appt.StartTime.Date, appt.StaffId, appt.StartTime, newEndTime, appt.Id, out string conflictReason))
                    {
                        MessageBox.Show($"Cannot extend appointment duration!\n\nThe new end time ({newEndTime:hh:mm tt}) overlaps with an existing appointment:\n\n• {conflictReason}\n\nAppointments are not allowed to overlap.", "Schedule Overlap Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        LoadAppointments();
                        return;
                    }

                    string existingSrvStaffIds = "";
                    using (SqlCommand cmdGet = new SqlCommand("SELECT ServiceStaffIds FROM Appointments WHERE Id = @id", conn))
                    {
                        cmdGet.Parameters.AddWithValue("@id", appt.Id);
                        existingSrvStaffIds = cmdGet.ExecuteScalar()?.ToString() ?? "";
                    }

                    string updatedSrvStaffIds = "";
                    if (!string.IsNullOrEmpty(existingSrvStaffIds) && existingSrvStaffIds.Contains(":"))
                    {
                        string[] entries = existingSrvStaffIds.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> updatedEntries = new List<string>();
                        int targetServiceIdToUpdate = appt.SpecificServiceId;

                        foreach (string entry in entries)
                        {
                            string staffPart = entry.Trim();
                            if (staffPart.Contains("|"))
                            {
                                var tParts = staffPart.Split('|');
                                staffPart = tParts[0].Trim();
                            }

                            var sParts = staffPart.Split(':');
                            if (sParts.Length >= 2 && int.TryParse(sParts[0].Trim(), out int rawSid) && int.TryParse(sParts[1].Trim(), out int sStaffId))
                            {
                                var sMatch = allServicesList.FirstOrDefault(s => s.Id == rawSid || s.Code == "SRV-" + rawSid || s.Code.EndsWith("-" + rawSid));
                                int resolvedSid = sMatch != null ? sMatch.Id : rawSid;

                                if (targetServiceIdToUpdate <= 0 || resolvedSid == targetServiceIdToUpdate || entries.Length == 1)
                                {
                                    updatedEntries.Add($"{rawSid}:{sStaffId}|{newTimeSlot}");
                                }
                                else
                                {
                                    updatedEntries.Add(entry.Trim());
                                }
                            }
                            else
                            {
                                updatedEntries.Add(entry.Trim());
                            }
                        }
                        updatedSrvStaffIds = string.Join(",", updatedEntries);
                    }

                    using (SqlCommand cmd = new SqlCommand(@"
                        UPDATE Appointments 
                        SET AppointmentTime = @time,
                            ServiceStaffIds = CASE WHEN @srvStaffIds != '' THEN @srvStaffIds ELSE ServiceStaffIds END
                        WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@time", newTimeSlot);
                        cmd.Parameters.AddWithValue("@srvStaffIds", updatedSrvStaffIds);
                        cmd.Parameters.AddWithValue("@id", appt.Id);
                        cmd.ExecuteNonQuery();
                    }
                }

                // If the appointment is currently loaded in the left booking form, sync end time dropdowns & summary
                if (selectedApptId == appt.Id)
                {
                    SetComboTime(comboToHour, comboToMin, newEndTime);
                    if (lblServiceSummary != null && selectedServiceIds.Count > 0)
                    {
                        var selectedItems = allServicesList.Where(s => selectedServiceIds.Contains(s.Id)).ToList();
                        decimal totalAmount = selectedItems.Sum(s => s.Price);
                        lblServiceSummary.Text = $"✔ {selectedItems.Count} service(s): Rs. {totalAmount:N0}   {newDurationMinutes}m ({appt.StartTime:hh:mm tt} - {newEndTime:hh:mm tt})";
                    }
                }

                LoadAppointments();
                SelectAppointmentById(appt.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating appointment duration: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnCheckoutNow_Click(object sender, EventArgs e)
        {
            if (gridAppointments.SelectedRows.Count == 0 || selectedApptId == 0)
            {
                MessageBox.Show("Please select an appointment from the queue to proceed to checkout.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DataGridViewRow row = gridAppointments.SelectedRows[0];
            string status = row.Cells["Status"]?.Value?.ToString() ?? "";

            if (DateTime.TryParse(row.Cells["Date"]?.Value?.ToString(), out DateTime apptDate) && apptDate.Date > DateTime.Today)
            {
                MessageBox.Show($"Cannot checkout or bill future date appointments ({apptDate:dd-MM-yyyy}).\n\nCheckout and billing can only be processed on the scheduled appointment date when the client arrives.", "Future Date Appointment", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("This appointment is marked as Cancelled and cannot proceed to billing.\n\nIf the client has arrived, please change its status to 'In-Chair', 'Completed', or 'Booked' first.", "Appointment Cancelled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int saleId = 0;
            if (row.Cells["SaleId"] != null && row.Cells["SaleId"].Value != DBNull.Value)
            {
                saleId = Convert.ToInt32(row.Cells["SaleId"].Value);
            }

            int custId = row.Cells["CustomerId"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["CustomerId"].Value) : 0;
            int staffId = row.Cells["StaffId"].Value != DBNull.Value ? Convert.ToInt32(row.Cells["StaffId"].Value) : 0;

            // If SaleId wasn't populated in column (e.g. legacy), try finding it from Sales
            if (saleId == 0 && string.Equals(status, "Billed", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 Id FROM Sales WHERE AppointmentId = @aId OR (CustomerId = @cId AND CAST(SaleDate AS DATE) = @dt) ORDER BY Id DESC", conn))
                        {
                            cmd.Parameters.AddWithValue("@aId", selectedApptId);
                            cmd.Parameters.AddWithValue("@cId", custId);
                            cmd.Parameters.AddWithValue("@dt", apptDate.Date);
                            object obj = cmd.ExecuteScalar();
                            if (obj != null && obj != DBNull.Value) saleId = Convert.ToInt32(obj);
                        }
                    }
                }
                catch { }
            }

            // Security Prompt: If appointment is already Billed, require Admin Authentication before adjusting invoice
            if (string.Equals(status, "Billed", StringComparison.OrdinalIgnoreCase))
            {
                if (!AdminAuthDialog.VerifyAdmin(this, "adjust the saved bill and invoice for this appointment"))
                {
                    return;
                }
            }

            // Extract all (serviceId, staffId) pairs
            List<Tuple<int, int>> serviceStaffPairs = new List<Tuple<int, int>>();
            string serviceStaffIdsStr = row.Cells["ServiceStaffIds"]?.Value?.ToString();

            if (!string.IsNullOrEmpty(serviceStaffIdsStr))
            {
                foreach (string part in serviceStaffIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string p = part.Trim();
                    if (p.Contains("|"))
                    {
                        string[] mainSub = p.Split('|');
                        string[] idSub = mainSub[0].Split(':');
                        if (idSub.Length >= 2 && int.TryParse(idSub[0].Trim(), out int srvId) && int.TryParse(idSub[1].Trim(), out int stId))
                        {
                            serviceStaffPairs.Add(Tuple.Create(srvId, stId));
                        }
                    }
                    else
                    {
                        string[] sub = p.Split(':');
                        if (sub.Length >= 2 && int.TryParse(sub[0].Trim(), out int srvId) && int.TryParse(sub[1].Trim(), out int stId))
                        {
                            serviceStaffPairs.Add(Tuple.Create(srvId, stId));
                        }
                    }
                }
            }

            if (serviceStaffPairs.Count == 0)
            {
                string serviceIdsStr = row.Cells["ServiceIds"]?.Value?.ToString();
                if (!string.IsNullOrEmpty(serviceIdsStr))
                {
                    foreach (string part in serviceIdsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (int.TryParse(part.Trim(), out int id))
                        {
                            serviceStaffPairs.Add(Tuple.Create(id, staffId));
                        }
                    }
                }
                else if (row.Cells["ServiceId"].Value != DBNull.Value)
                {
                    int srvId = Convert.ToInt32(row.Cells["ServiceId"].Value);
                    if (srvId > 0) serviceStaffPairs.Add(Tuple.Create(srvId, staffId));
                }
            }

            // Trigger event for MainForm to switch to Sales Billing tab with apptId, client, services with assigned stylists, and existing saleId (if adjusting)
            OnCheckoutRequested?.Invoke(selectedApptId, custId, serviceStaffPairs, saleId);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (gridAppointments.SelectedRows.Count == 0 || selectedApptId == 0)
            {
                MessageBox.Show("Please select an appointment to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DataGridViewRow row = gridAppointments.SelectedRows[0];
            string currentStatus = row.Cells["Status"]?.Value?.ToString() ?? "";
            if (currentStatus == "Billed")
            {
                if (!AdminAuthDialog.VerifyAdmin(this, "delete this finalized billed appointment record"))
                {
                    return;
                }
            }

            var confirm = MessageBox.Show("Are you sure you want to delete this appointment?", "Confirm Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(DatabaseHelper.ConnectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM Appointments WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", selectedApptId);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Appointment deleted.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ResetForm();
                LoadAppointments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting appointment: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ResetForm()
        {
            isExplicitUserSelection = false;
            selectedApptId = 0;
            if (gridAppointments != null)
            {
                isSuppressingSelection = true;
                gridAppointments.ClearSelection();
                isSuppressingSelection = false;
            }
            if (scheduleBoard != null)
            {
                scheduleBoard.SelectAppointment(0);
            }
            isSettingCustomerProgrammatically = true;
            if (txtCustomerPhone != null) txtCustomerPhone.Clear();
            if (txtCustomerName != null) txtCustomerName.Clear();
            isSettingCustomerProgrammatically = false;
            HideCustomerSuggestions();
            
            selectedServiceIds.Clear();
            selectedServiceStaffMap.Clear();
            selectedServiceTimeMap.Clear();
            ClearCustomerPastHistory();
            FilterServiceCheckedList("");
            UpdateServiceSelectionUI();

            if (comboStaff != null && comboStaff.Items.Count > 0) comboStaff.SelectedIndex = 0;
            if (dtpApptDate != null)
            {
                dtpApptDate.MinDate = DateTime.Today;
                dtpApptDate.Value = dtpFilterDate != null ? dtpFilterDate.Value.Date : DateTime.Today;
            }
            SetTimeSlot("10:00 AM", 30);
            if (comboStatus != null && comboStatus.Items.Count > 0) comboStatus.SelectedIndex = 0; // Booked
            if (txtNotes != null) txtNotes.Clear();
            
            if (lblCardTitle != null)
            {
                lblCardTitle.Text = "+ New Booking";
                lblCardTitle.ForeColor = Theme.TextLight;
            }
            if (btnBook != null)
            {
                btnBook.Text = "+ Book Appointment";
                Theme.StyleSuccessButton(btnBook);
            }
            if (btnCheckoutNow != null)
            {
                btnCheckoutNow.Text = "🚀 🧾 Bill / Checkout";
                btnCheckoutNow.BackColor = Theme.Accent;
            }
        }
    }
}
