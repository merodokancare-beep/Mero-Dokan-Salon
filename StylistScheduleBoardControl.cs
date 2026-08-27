using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace MeroDokan
{
    public class StylistColumnModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public Color AccentColor { get; set; }
        public int BookedMinutes { get; set; }
        public double UtilizationPercent { get; set; }
        public bool IsOffDuty { get; set; }
    }

    public class AppointmentCardModel
    {
        public int Id { get; set; }
        public string AppointmentNumber { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhone { get; set; }
        public int StaffId { get; set; }
        public string StaffName { get; set; }
        public string ServiceNames { get; set; }
        public string ServiceStaffIds { get; set; }
        public string ServiceIds { get; set; }
        public int SpecificServiceId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int DurationMinutes { get; set; }
        public string Status { get; set; }
        public decimal TotalAmount { get; set; }
        public int SaleId { get; set; }
        public string InvoiceNumber { get; set; }
        public string Notes { get; set; }

        // Render cache
        internal Rectangle Bounds { get; set; }
        internal Rectangle BtnBookBounds { get; set; }
        internal Rectangle BtnChairBounds { get; set; }
        internal Rectangle BtnDoneBounds { get; set; }
        internal Rectangle BtnBillBounds { get; set; }
    }

    public class StylistScheduleBoardControl : UserControl
    {
        private readonly List<StylistColumnModel> stylists = new List<StylistColumnModel>();
        private readonly List<AppointmentCardModel> appointments = new List<AppointmentCardModel>();

        private int selectedApptId = 0;
        private int hoveredApptId = 0;
        private int hoveredResizeApptId = 0;
        private string hoveredButtonKey = "";
        private Point hoveredSlot = new Point(-1, -1); // X = Stylist Index, Y = Slot Index

        // Interactive Drag-to-Resize State
        private bool isResizing = false;
        private AppointmentCardModel resizingAppt = null;
        private int resizingNewMinutes = 0;
        private int resizeInitialMinutes = 0;

        // Interactive Drag-to-Move State
        private bool isMoving = false;
        private AppointmentCardModel movingAppt = null;
        private Point dragStartPoint = Point.Empty;
        private int targetStaffId = 0;
        private DateTime targetStartTime = DateTime.MinValue;
        private DateTime targetEndTime = DateTime.MinValue;
        private int targetColIndex = -1;
        private int targetSlotIndex = -1;

        // Layout constants (5-Minute Timeline Grid)
        private const int TimeColWidth = 70;
        private const int HeaderHeight = 62;
        private const int SlotIntervalMinutes = 5; // 5-minute slot gap
        private const int SlotHeight = 14;         // 14 pixels per 5-min slot (1 hour = 168px)
        private const int StartHour = 10;          // 10:00 AM
        private const int EndHour = 21;            // 09:00 PM (11 hours = 132 slots)
        private const int SlotsPerHour = 60 / SlotIntervalMinutes; // 12 slots per hour
        private const int TotalSlots = (EndHour - StartHour) * SlotsPerHour; // 132 slots
        private const int MinColumnWidth = 190;

        // Scrolling
        private readonly VScrollBar vScrollBar;
        private readonly HScrollBar hScrollBar;
        private string searchQuery = "";

        // Events
        public event Action<AppointmentCardModel> AppointmentSelected;
        public event Action<AppointmentCardModel> AppointmentDoubleClicked;
        public event Action<int, DateTime> EmptySlotClicked;
        public event Action<int, DateTime> EmptySlotDoubleClicked;
        public event Action<AppointmentCardModel, int, DateTime> AppointmentDurationChanged;
        public event Action<AppointmentCardModel, int, DateTime, DateTime> AppointmentMoved;
        public event Action<AppointmentCardModel, string> AppointmentStatusChangeRequested;
        public event Action<AppointmentCardModel> AppointmentDeleteRequested;
        public event Action<AppointmentCardModel> AppointmentCheckoutRequested;

        private static readonly Color[] StylistAccentPalette = new Color[] {
            Color.FromArgb(59, 130, 246),  // Royal Blue
            Color.FromArgb(236, 72, 153),  // Rose Pink
            Color.FromArgb(168, 85, 247),  // Purple
            Color.FromArgb(16, 185, 129),  // Emerald
            Color.FromArgb(245, 158, 11),  // Amber
            Color.FromArgb(14, 165, 233),  // Sky Blue
            Color.FromArgb(249, 115, 22),  // Orange
            Color.FromArgb(20, 184, 166)   // Teal
        };

        public StylistScheduleBoardControl()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.ResizeRedraw, true);

            this.BackColor = Color.FromArgb(13, 17, 23);
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

            vScrollBar = new VScrollBar();
            vScrollBar.Dock = DockStyle.Right;
            vScrollBar.SmallChange = SlotHeight * 3;
            vScrollBar.LargeChange = SlotHeight * 12;
            vScrollBar.Scroll += (s, e) => this.Invalidate();
            this.Controls.Add(vScrollBar);

            hScrollBar = new HScrollBar();
            hScrollBar.Dock = DockStyle.Bottom;
            hScrollBar.SmallChange = 50;
            hScrollBar.LargeChange = 200;
            hScrollBar.Scroll += (s, e) => this.Invalidate();
            this.Controls.Add(hScrollBar);

            this.MouseWheel += StylistScheduleBoardControl_MouseWheel;
            this.MouseMove += StylistScheduleBoardControl_MouseMove;
            this.MouseLeave += StylistScheduleBoardControl_MouseLeave;
            this.MouseDown += StylistScheduleBoardControl_MouseDown;
            this.MouseUp += StylistScheduleBoardControl_MouseUp;
            this.MouseDoubleClick += StylistScheduleBoardControl_MouseDoubleClick;
            this.Resize += (s, e) => UpdateScrollRanges();
        }

        public void SetData(List<StylistColumnModel> stylistList, List<AppointmentCardModel> apptList, int selectedId = 0)
        {
            stylists.Clear();
            if (stylistList != null)
            {
                int colorIdx = 0;
                foreach (var st in stylistList)
                {
                    if (st.AccentColor == Color.Empty)
                    {
                        st.AccentColor = StylistAccentPalette[colorIdx % StylistAccentPalette.Length];
                    }
                    colorIdx++;
                    stylists.Add(st);
                }
            }

            appointments.Clear();
            if (apptList != null)
            {
                appointments.AddRange(apptList);
            }

            // Compute utilization for each stylist
            // Based on standard 10-hour working day = 600 working minutes
            const double StandardDayMinutes = 600.0;
            foreach (var st in stylists)
            {
                int bookedMin = appointments.Where(a => a.StaffId == st.Id && a.Status != "Cancelled")
                                            .Sum(a => a.DurationMinutes > 0 ? a.DurationMinutes : 30);
                st.BookedMinutes = bookedMin;
                st.UtilizationPercent = Math.Round((bookedMin / StandardDayMinutes) * 100.0, 1);
            }

            selectedApptId = selectedId;
            UpdateScrollRanges();
            this.Invalidate();
        }

        public void SetSearchQuery(string query)
        {
            searchQuery = query?.Trim() ?? "";
            this.Invalidate();
        }

        public void SelectAppointment(int apptId)
        {
            selectedApptId = apptId;
            this.Invalidate();
        }

        private int GetColumnWidth()
        {
            int clientW = this.ClientSize.Width - TimeColWidth - (vScrollBar.Visible ? vScrollBar.Width : 0);
            if (stylists.Count == 0) return MinColumnWidth;
            int computed = clientW / stylists.Count;
            return Math.Max(MinColumnWidth, computed);
        }

        private void UpdateScrollRanges()
        {
            int colWidth = GetColumnWidth();
            int totalGridWidth = TimeColWidth + (stylists.Count * colWidth);
            int totalGridHeight = HeaderHeight + (TotalSlots * SlotHeight);

            int viewWidth = this.ClientSize.Width - (vScrollBar.Visible ? vScrollBar.Width : 0);
            int viewHeight = this.ClientSize.Height - (hScrollBar.Visible ? hScrollBar.Height : 0);

            // Horizontal scrollbar
            if (totalGridWidth > viewWidth)
            {
                hScrollBar.Visible = true;
                hScrollBar.Maximum = totalGridWidth - viewWidth + hScrollBar.LargeChange;
            }
            else
            {
                hScrollBar.Visible = false;
                hScrollBar.Value = 0;
            }

            // Vertical scrollbar
            if (totalGridHeight > viewHeight)
            {
                vScrollBar.Visible = true;
                vScrollBar.Maximum = totalGridHeight - viewHeight + vScrollBar.LargeChange;
            }
            else
            {
                vScrollBar.Visible = false;
                vScrollBar.Value = 0;
            }
        }

        private void StylistScheduleBoardControl_MouseWheel(object sender, MouseEventArgs e)
        {
            if (vScrollBar.Visible)
            {
                int delta = -Math.Sign(e.Delta) * SlotHeight * 3;
                int newVal = vScrollBar.Value + delta;
                newVal = Math.Max(vScrollBar.Minimum, Math.Min(vScrollBar.Maximum - vScrollBar.LargeChange + 1, newVal));
                vScrollBar.Value = newVal;
                this.Invalidate();
            }
        }

        private void StylistScheduleBoardControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (movingAppt != null && !isResizing)
            {
                int dx = Math.Abs(e.X - dragStartPoint.X);
                int dy = Math.Abs(e.Y - dragStartPoint.Y);
                if (dx > 4 || dy > 4 || isMoving)
                {
                    isMoving = true;
                    this.Cursor = Cursors.SizeAll;

                    int scrollX = hScrollBar.Visible ? hScrollBar.Value : 0;
                    int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;
                    int colWidth = GetColumnWidth();

                    int adjX = e.X - TimeColWidth + scrollX;
                    int adjY = e.Y - HeaderHeight + scrollY;

                    int colIdx = Math.Max(0, Math.Min(stylists.Count - 1, adjX / colWidth));
                    int slotIdx = Math.Max(0, Math.Min(TotalSlots - 1, adjY / SlotHeight));

                    if (colIdx >= 0 && colIdx < stylists.Count && slotIdx >= 0 && slotIdx < TotalSlots)
                    {
                        targetColIndex = colIdx;
                        targetSlotIndex = slotIdx;
                        targetStaffId = stylists[colIdx].Id;

                        int dur = movingAppt.DurationMinutes > 0 ? movingAppt.DurationMinutes : SlotIntervalMinutes;
                        DateTime baseDate = movingAppt.StartTime != DateTime.MinValue ? movingAppt.StartTime.Date : DateTime.Today;
                        targetStartTime = baseDate.AddHours(StartHour).AddMinutes(slotIdx * SlotIntervalMinutes);
                        targetEndTime = targetStartTime.AddMinutes(dur);

                        this.Invalidate();
                    }
                    return;
                }
            }

            if (!isResizing && !isMoving)
            {
                int oldHoverAppt = hoveredApptId;
                int oldHoverResize = hoveredResizeApptId;
                string oldHoverKey = hoveredButtonKey;
                Point oldHoverSlot = hoveredSlot;

                hoveredApptId = 0;
                hoveredResizeApptId = 0;
                hoveredButtonKey = "";
                hoveredSlot = new Point(-1, -1);

                // Check if hovering over any of the 4 card action buttons, resize handle, or card body
                foreach (var appt in appointments)
                {
                    if (e.Y > HeaderHeight)
                    {
                        if (appt.BtnBookBounds.Contains(e.Location))
                        {
                            hoveredApptId = appt.Id;
                            hoveredButtonKey = $"book_{appt.Id}";
                            this.Cursor = Cursors.Hand;
                            break;
                        }
                        if (appt.BtnChairBounds.Contains(e.Location))
                        {
                            hoveredApptId = appt.Id;
                            hoveredButtonKey = $"chair_{appt.Id}";
                            this.Cursor = Cursors.Hand;
                            break;
                        }
                        if (appt.BtnDoneBounds.Contains(e.Location))
                        {
                            hoveredApptId = appt.Id;
                            hoveredButtonKey = $"done_{appt.Id}";
                            this.Cursor = Cursors.Hand;
                            break;
                        }
                        if (appt.BtnBillBounds.Contains(e.Location))
                        {
                            hoveredApptId = appt.Id;
                            hoveredButtonKey = $"bill_{appt.Id}";
                            this.Cursor = Cursors.Hand;
                            break;
                        }
                        if (e.X >= appt.Bounds.Left && e.X <= appt.Bounds.Right)
                        {
                            if (e.Y >= appt.Bounds.Bottom - 10 && e.Y <= appt.Bounds.Bottom + 6)
                            {
                                hoveredApptId = appt.Id;
                                hoveredResizeApptId = appt.Id;
                                this.Cursor = Cursors.SizeNS;
                                break;
                            }
                            else if (appt.Bounds.Contains(e.Location))
                            {
                                hoveredApptId = appt.Id;
                                this.Cursor = Cursors.Hand;
                                break;
                            }
                        }
                    }
                }

                if (hoveredApptId == 0)
                {
                    // Check if hovering over grid slots
                    int scrollX = hScrollBar.Visible ? hScrollBar.Value : 0;
                    int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;

                    if (e.X > TimeColWidth && e.Y > HeaderHeight)
                    {
                        int colWidth = GetColumnWidth();
                        int adjX = e.X - TimeColWidth + scrollX;
                        int adjY = e.Y - HeaderHeight + scrollY;

                        int colIdx = adjX / colWidth;
                        int slotIdx = adjY / SlotHeight;

                        if (colIdx >= 0 && colIdx < stylists.Count && slotIdx >= 0 && slotIdx < TotalSlots)
                        {
                            hoveredSlot = new Point(colIdx, slotIdx);
                            this.Cursor = Cursors.Cross;
                        }
                        else
                        {
                            this.Cursor = Cursors.Default;
                        }
                    }
                    else
                    {
                        this.Cursor = Cursors.Default;
                    }
                }

                if (oldHoverAppt != hoveredApptId || oldHoverResize != hoveredResizeApptId || oldHoverKey != hoveredButtonKey || oldHoverSlot != hoveredSlot)
                {
                    this.Invalidate();
                }
            }
            else if (isResizing && resizingAppt != null)
            {
                // Live dragging resize calculations
                int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;
                int startSlot = GetSlotIndexFromTime(resizingAppt.StartTime);
                int cardTopY = HeaderHeight + (startSlot * SlotHeight) - scrollY;
                int draggedHeight = e.Y - cardTopY;

                // Minimum 1 slot (5 mins), round to nearest 5-min slot
                int rawSlots = (int)Math.Max(1, Math.Round((double)draggedHeight / SlotHeight));
                int newDuration = rawSlots * SlotIntervalMinutes;
                if (newDuration > 600) newDuration = 600; // max 10 hours

                if (newDuration != resizingNewMinutes)
                {
                    resizingNewMinutes = newDuration;
                    this.Invalidate();
                }
            }
        }

        private void StylistScheduleBoardControl_MouseLeave(object sender, EventArgs e)
        {
            if (!isResizing && !isMoving)
            {
                hoveredApptId = 0;
                hoveredResizeApptId = 0;
                hoveredButtonKey = "";
                hoveredSlot = new Point(-1, -1);
                this.Cursor = Cursors.Default;
                this.Invalidate();
            }
        }

        private void StylistScheduleBoardControl_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 1. Direct check: Did the user click one of the 4 on-card action buttons?
                foreach (var appt in appointments)
                {
                    if (e.Y > HeaderHeight)
                    {
                        if (appt.BtnBookBounds.Contains(e.Location))
                        {
                            selectedApptId = appt.Id;
                            AppointmentSelected?.Invoke(appt);
                            AppointmentStatusChangeRequested?.Invoke(appt, "Booked");
                            this.Invalidate();
                            return;
                        }
                        if (appt.BtnChairBounds.Contains(e.Location))
                        {
                            selectedApptId = appt.Id;
                            AppointmentSelected?.Invoke(appt);
                            AppointmentStatusChangeRequested?.Invoke(appt, "In-Chair");
                            this.Invalidate();
                            return;
                        }
                        if (appt.BtnDoneBounds.Contains(e.Location))
                        {
                            selectedApptId = appt.Id;
                            AppointmentSelected?.Invoke(appt);
                            AppointmentStatusChangeRequested?.Invoke(appt, "Completed");
                            this.Invalidate();
                            return;
                        }
                        if (appt.BtnBillBounds.Contains(e.Location))
                        {
                            selectedApptId = appt.Id;
                            AppointmentSelected?.Invoke(appt);
                            AppointmentCheckoutRequested?.Invoke(appt);
                            this.Invalidate();
                            return;
                        }
                    }
                }

                // 2. Direct check: Did the user click the bottom resize handle of ANY appointment?
                foreach (var appt in appointments)
                {
                    if (e.X >= appt.Bounds.Left && e.X <= appt.Bounds.Right && e.Y >= appt.Bounds.Bottom - 10 && e.Y <= appt.Bounds.Bottom + 6 && e.Y > HeaderHeight)
                    {
                        isResizing = true;
                        resizingAppt = appt;
                        resizeInitialMinutes = appt.DurationMinutes > 0 ? appt.DurationMinutes : SlotIntervalMinutes;
                        resizingNewMinutes = resizeInitialMinutes;
                        this.Capture = true;
                        this.Cursor = Cursors.SizeNS;
                        this.Invalidate();
                        return;
                    }
                }

                // 3. Check if clicked an appointment body (for Drag-to-Move or selection)
                foreach (var appt in appointments)
                {
                    if (appt.Bounds.Contains(e.Location) && e.Y > HeaderHeight)
                    {
                        selectedApptId = appt.Id;
                        movingAppt = appt;
                        dragStartPoint = e.Location;
                        isMoving = false;
                        targetStaffId = appt.StaffId;
                        targetStartTime = appt.StartTime;
                        targetEndTime = appt.EndTime;
                        targetColIndex = stylists.FindIndex(s => s.Id == appt.StaffId);
                        targetSlotIndex = GetSlotIndexFromTime(appt.StartTime);

                        this.Capture = true;
                        this.Invalidate();
                        return;
                    }
                }

                // Check if clicked an empty slot
                int scrollX = hScrollBar.Visible ? hScrollBar.Value : 0;
                int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;

                if (e.X > TimeColWidth && e.Y > HeaderHeight)
                {
                    int colWidth = GetColumnWidth();
                    int adjX = e.X - TimeColWidth + scrollX;
                    int adjY = e.Y - HeaderHeight + scrollY;

                    int colIdx = adjX / colWidth;
                    int slotIdx = adjY / SlotHeight;

                    if (colIdx >= 0 && colIdx < stylists.Count && slotIdx >= 0 && slotIdx < TotalSlots)
                    {
                        var stylist = stylists[colIdx];
                        DateTime slotTime = DateTime.Today.AddHours(StartHour).AddMinutes(slotIdx * SlotIntervalMinutes);
                        EmptySlotClicked?.Invoke(stylist.Id, slotTime);
                    }
                }
            }
            else if (e.Button == MouseButtons.Right)
            {
                foreach (var appt in appointments)
                {
                    if (appt.Bounds.Contains(e.Location) && e.Y > HeaderHeight)
                    {
                        selectedApptId = appt.Id;
                        this.Invalidate();
                        AppointmentSelected?.Invoke(appt);
                        ShowAppointmentContextMenu(appt, e.Location);
                        return;
                    }
                }
            }
        }

        private void StylistScheduleBoardControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (isMoving && movingAppt != null)
            {
                this.Capture = false;
                var appt = movingAppt;
                int newStaffId = targetStaffId;
                DateTime newStart = targetStartTime;
                DateTime newEnd = targetEndTime;

                isMoving = false;
                movingAppt = null;
                this.Cursor = Cursors.Default;

                if (newStaffId > 0 && (newStaffId != appt.StaffId || newStart != appt.StartTime))
                {
                    AppointmentMoved?.Invoke(appt, newStaffId, newStart, newEnd);
                }

                this.Invalidate();
                return;
            }
            else if (movingAppt != null)
            {
                this.Capture = false;
                var appt = movingAppt;
                movingAppt = null;
                isMoving = false;
                this.Cursor = Cursors.Default;
                this.Invalidate();
                AppointmentSelected?.Invoke(appt);
            }

            if (isResizing && resizingAppt != null)
            {
                this.Capture = false;
                isResizing = false;

                int finalDuration = resizingNewMinutes;
                var appt = resizingAppt;
                resizingAppt = null;
                this.Cursor = Cursors.Default;

                if (finalDuration > 0 && finalDuration != appt.DurationMinutes)
                {
                    DateTime newEndTime = appt.StartTime.AddMinutes(finalDuration);

                    // Check for overlapping conflict on board
                    bool hasConflict = appointments.Any(a => a.Id != appt.Id && a.StaffId == appt.StaffId && a.Status != "Cancelled" && appt.StartTime < a.EndTime && newEndTime > a.StartTime);
                    if (hasConflict)
                    {
                        MessageBox.Show("Cannot extend appointment duration!\n\nThe extended time overlaps with an existing appointment for this stylist.\n\nAppointments are not allowed to overlap.", "Schedule Overlap Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        this.Invalidate();
                        return;
                    }

                    AppointmentDurationChanged?.Invoke(appt, finalDuration, newEndTime);
                }

                this.Invalidate();
            }
        }

        private void StylistScheduleBoardControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;

            foreach (var appt in appointments)
            {
                if (appt.Bounds.Contains(e.Location) && e.Y > HeaderHeight)
                {
                    AppointmentDoubleClicked?.Invoke(appt);
                    return;
                }
            }

            int scrollX = hScrollBar.Visible ? hScrollBar.Value : 0;
            int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;

            if (e.X > TimeColWidth && e.Y > HeaderHeight)
            {
                int colWidth = GetColumnWidth();
                int adjX = e.X - TimeColWidth + scrollX;
                int adjY = e.Y - HeaderHeight + scrollY;

                int colIdx = adjX / colWidth;
                int slotIdx = adjY / SlotHeight;

                if (colIdx >= 0 && colIdx < stylists.Count && slotIdx >= 0 && slotIdx < TotalSlots)
                {
                    var stylist = stylists[colIdx];
                    DateTime slotTime = DateTime.Today.AddHours(StartHour).AddMinutes(slotIdx * SlotIntervalMinutes);
                    EmptySlotDoubleClicked?.Invoke(stylist.Id, slotTime);
                }
            }
        }

        private void ShowAppointmentContextMenu(AppointmentCardModel appt, Point pt)
        {
            ContextMenuStrip cms = new ContextMenuStrip();
            cms.Renderer = new DarkMenuRenderer();
            cms.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            cms.BackColor = Color.FromArgb(17, 24, 39);
            cms.ForeColor = Color.FromArgb(241, 245, 249);

            // Header info item
            var headerItem = new ToolStripMenuItem($"📌 {appt.CustomerName ?? "Client"} (#{appt.AppointmentNumber})") { Enabled = false };
            headerItem.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            headerItem.ForeColor = Color.FromArgb(148, 163, 184);
            cms.Items.Add(headerItem);
            cms.Items.Add(new ToolStripSeparator());

            // 1. Duration Menu
            var durationMenu = new ToolStripMenuItem($"⏱ Adjust Duration (Current: {appt.DurationMinutes}m)");
            durationMenu.DropDown.Renderer = new DarkMenuRenderer();
            durationMenu.DropDown.BackColor = Color.FromArgb(17, 24, 39);

            // Quick extensions
            durationMenu.DropDownItems.Add("➕ Extend +5 mins", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 5));
            durationMenu.DropDownItems.Add("➕ Extend +10 mins", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 10));
            durationMenu.DropDownItems.Add("➕ Extend +15 mins", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 15));
            durationMenu.DropDownItems.Add("➕ Extend +30 mins", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 30));
            durationMenu.DropDownItems.Add("➕ Extend +45 mins", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 45));
            durationMenu.DropDownItems.Add("➕ Extend +60 mins (1 hr)", null, (s, ev) => ApplyDurationChange(appt, appt.DurationMinutes + 60));
            durationMenu.DropDownItems.Add(new ToolStripSeparator());

            // Quick reductions
            var dec5 = new ToolStripMenuItem("➖ Decrease -5 mins", null, (s, ev) => ApplyDurationChange(appt, Math.Max(5, appt.DurationMinutes - 5))) { Enabled = appt.DurationMinutes > 5 };
            var dec10 = new ToolStripMenuItem("➖ Decrease -10 mins", null, (s, ev) => ApplyDurationChange(appt, Math.Max(5, appt.DurationMinutes - 10))) { Enabled = appt.DurationMinutes > 10 };
            var dec15 = new ToolStripMenuItem("➖ Decrease -15 mins", null, (s, ev) => ApplyDurationChange(appt, Math.Max(5, appt.DurationMinutes - 15))) { Enabled = appt.DurationMinutes > 15 };
            var dec30 = new ToolStripMenuItem("➖ Decrease -30 mins", null, (s, ev) => ApplyDurationChange(appt, Math.Max(5, appt.DurationMinutes - 30))) { Enabled = appt.DurationMinutes > 30 };
            durationMenu.DropDownItems.Add(dec5);
            durationMenu.DropDownItems.Add(dec10);
            durationMenu.DropDownItems.Add(dec15);
            durationMenu.DropDownItems.Add(dec30);
            durationMenu.DropDownItems.Add(new ToolStripSeparator());

            // Exact standard durations
            int[] standardDurations = new int[] { 5, 10, 15, 20, 25, 30, 45, 60, 75, 90, 120, 150, 180 };
            foreach (int mins in standardDurations)
            {
                string label = mins >= 60 ? (mins % 60 == 0 ? $"{mins / 60} hr" : $"{mins / 60} hr {mins % 60}m") : $"{mins} mins";
                bool isCurrent = (appt.DurationMinutes == mins);
                var item = new ToolStripMenuItem($"{(isCurrent ? "✓ " : "  ")}{label} ({mins}m)", null, (s, ev) => ApplyDurationChange(appt, mins));
                if (isCurrent) item.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                durationMenu.DropDownItems.Add(item);
            }
            durationMenu.DropDownItems.Add(new ToolStripSeparator());
            durationMenu.DropDownItems.Add("✏ Custom Duration (mins)...", null, (s, ev) => PromptCustomDuration(appt));

            cms.Items.Add(durationMenu);
            cms.Items.Add(new ToolStripSeparator());

            // Status actions
            var statusMenu = new ToolStripMenuItem("🔄 Change Status");
            statusMenu.DropDown.Renderer = new DarkMenuRenderer();
            statusMenu.DropDown.BackColor = Color.FromArgb(17, 24, 39);
            string[] statuses = new string[] { "Booked", "In-Chair", "Completed", "Cancelled" };
            foreach (string st in statuses)
            {
                bool isCurrent = string.Equals(appt.Status, st, StringComparison.OrdinalIgnoreCase);
                var item = new ToolStripMenuItem($"{(isCurrent ? "✓ " : "  ")}{st}", null, (s, ev) => AppointmentStatusChangeRequested?.Invoke(appt, st));
                if (isCurrent) item.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                statusMenu.DropDownItems.Add(item);
            }
            cms.Items.Add(statusMenu);

            // Bill / Checkout
            cms.Items.Add("💳 Proceed to Checkout", null, (s, ev) => AppointmentCheckoutRequested?.Invoke(appt));
            cms.Items.Add(new ToolStripSeparator());
            cms.Items.Add("🗑 Delete Appointment", null, (s, ev) => AppointmentDeleteRequested?.Invoke(appt));

            void StyleMenuItemsRecursively(ToolStripItemCollection items)
            {
                foreach (ToolStripItem item in items)
                {
                    item.ForeColor = Color.FromArgb(241, 245, 249);
                    item.BackColor = Color.FromArgb(17, 24, 39);
                    if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                    {
                        menuItem.DropDown.Renderer = new DarkMenuRenderer();
                        menuItem.DropDown.BackColor = Color.FromArgb(17, 24, 39);
                        StyleMenuItemsRecursively(menuItem.DropDownItems);
                    }
                }
            }
            StyleMenuItemsRecursively(cms.Items);

            cms.Show(this, pt);
        }

        private void ApplyDurationChange(AppointmentCardModel appt, int newMinutes)
        {
            if (newMinutes < 5) newMinutes = 5;
            if (newMinutes > 600) newMinutes = 600;
            if (newMinutes != appt.DurationMinutes)
            {
                DateTime newEndTime = appt.StartTime.AddMinutes(newMinutes);
                AppointmentDurationChanged?.Invoke(appt, newMinutes, newEndTime);
            }
        }

        private void PromptCustomDuration(AppointmentCardModel appt)
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 340;
                prompt.Height = 180;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.Text = "Adjust Appointment Duration";
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.BackColor = Color.FromArgb(17, 24, 39);
                prompt.ForeColor = Color.White;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;

                Label lbl = new Label() { Left = 20, Top = 16, Width = 280, Text = $"Enter duration in minutes for {appt.CustomerName}:", ForeColor = Color.FromArgb(226, 232, 240) };
                NumericUpDown num = new NumericUpDown() { Left = 20, Top = 46, Width = 280, Minimum = 5, Maximum = 600, Increment = 5, Value = appt.DurationMinutes > 0 ? appt.DurationMinutes : 30 };
                num.BackColor = Color.FromArgb(30, 41, 59);
                num.ForeColor = Color.White;
                num.Font = new Font("Segoe UI", 10F, FontStyle.Bold);

                Button btnOk = new Button() { Text = "Set Duration", Left = 80, Width = 110, Top = 90, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(249, 115, 22), ForeColor = Color.White };
                btnOk.FlatAppearance.BorderSize = 0;
                Button btnCancel = new Button() { Text = "Cancel", Left = 200, Width = 100, Top = 90, DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(51, 65, 85), ForeColor = Color.White };
                btnCancel.FlatAppearance.BorderSize = 0;

                prompt.Controls.Add(lbl);
                prompt.Controls.Add(num);
                prompt.Controls.Add(btnOk);
                prompt.Controls.Add(btnCancel);
                prompt.AcceptButton = btnOk;
                prompt.CancelButton = btnCancel;

                if (prompt.ShowDialog(this) == DialogResult.OK)
                {
                    ApplyDurationChange(appt, (int)num.Value);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int scrollX = hScrollBar.Visible ? hScrollBar.Value : 0;
            int scrollY = vScrollBar.Visible ? vScrollBar.Value : 0;
            int colWidth = GetColumnWidth();

            int viewWidth = this.ClientSize.Width - (vScrollBar.Visible ? vScrollBar.Width : 0);
            int viewHeight = this.ClientSize.Height - (hScrollBar.Visible ? hScrollBar.Height : 0);

            // 1. Draw Grid Canvas Background & Cells
            Rectangle gridArea = new Rectangle(TimeColWidth, HeaderHeight, viewWidth - TimeColWidth, viewHeight - HeaderHeight);
            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(13, 17, 23)))
            {
                g.FillRectangle(bgBrush, gridArea);
            }

            // Draw Stylist Column Backgrounds & Vertical Dividers
            using (Pen gridLinePen = new Pen(Color.FromArgb(30, 41, 59), 1))
            using (Pen hourLinePen = new Pen(Color.FromArgb(51, 65, 85), 1))
            using (Pen colLinePen = new Pen(Color.FromArgb(40, 50, 70), 1))
            using (SolidBrush offDutyBrush = new SolidBrush(Color.FromArgb(18, 22, 32)))
            {
                for (int c = 0; c < stylists.Count; c++)
                {
                    int colX = TimeColWidth + (c * colWidth) - scrollX;
                    if (colX + colWidth < TimeColWidth || colX > viewWidth) continue;

                    var stylist = stylists[c];
                    if (stylist.IsOffDuty || stylist.BookedMinutes == 0 && stylist.UtilizationPercent == 0)
                    {
                        // Slight contrast shading for inactive/empty columns
                        g.FillRectangle(offDutyBrush, colX, HeaderHeight, colWidth, viewHeight - HeaderHeight);
                    }

                    // Column right border
                    g.DrawLine(colLinePen, colX + colWidth, HeaderHeight, colX + colWidth, viewHeight);
                }

                // Draw Horizontal Time Slot Lines (Hour, 15-min quarter, and 5-min intervals)
                using (Pen subtleSlotPen = new Pen(Color.FromArgb(22, 28, 40), 1))
                {
                    for (int slot = 0; slot <= TotalSlots; slot++)
                    {
                        int slotY = HeaderHeight + (slot * SlotHeight) - scrollY;
                        if (slotY < HeaderHeight || slotY > viewHeight) continue;

                        bool isHour = (slot % SlotsPerHour == 0);
                        bool isQuarter = (slot % (15 / SlotIntervalMinutes) == 0);
                        Pen p = isHour ? hourLinePen : (isQuarter ? gridLinePen : subtleSlotPen);
                        g.DrawLine(p, TimeColWidth, slotY, viewWidth, slotY);
                    }
                }
            }

            // Draw Slot Hover Highlight
            if (!isResizing && hoveredSlot.X >= 0 && hoveredSlot.X < stylists.Count && hoveredSlot.Y >= 0 && hoveredSlot.Y < TotalSlots)
            {
                int hX = TimeColWidth + (hoveredSlot.X * colWidth) - scrollX;
                int hY = HeaderHeight + (hoveredSlot.Y * SlotHeight) - scrollY;
                if (hX + colWidth >= TimeColWidth && hX <= viewWidth && hY + SlotHeight >= HeaderHeight && hY <= viewHeight)
                {
                    using (SolidBrush hBrush = new SolidBrush(Color.FromArgb(35, 59, 130, 246)))
                    using (Pen hPen = new Pen(Color.FromArgb(120, 59, 130, 246), 1))
                    {
                        Rectangle hRect = new Rectangle(hX + 1, hY + 1, colWidth - 2, SlotHeight - 2);
                        g.FillRectangle(hBrush, hRect);
                        g.DrawRectangle(hPen, hRect);
                    }
                }
            }

            // 2. Draw Appointment Cards
            using (Font titleFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold))
            using (Font srvFont = new Font("Segoe UI", 8F, FontStyle.Bold))
            using (Font subFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
            {
                foreach (var appt in appointments)
                {
                    // Find stylist column
                    int colIdx = stylists.FindIndex(s => s.Id == appt.StaffId);
                    if (colIdx < 0)
                    {
                        // Fallback to first available column or skip
                        colIdx = 0;
                    }

                    if (stylists.Count == 0) continue;

                    int colX = TimeColWidth + (colIdx * colWidth) - scrollX;
                    int startSlot = GetSlotIndexFromTime(appt.StartTime);
                    int durMinutes = appt.DurationMinutes > 0 ? appt.DurationMinutes : SlotIntervalMinutes;
                    int slotSpan = Math.Max(1, (int)Math.Ceiling((double)durMinutes / SlotIntervalMinutes));

                    int cardY = HeaderHeight + (startSlot * SlotHeight) - scrollY;
                    int cardH = Math.Max(SlotHeight - 2, (slotSpan * SlotHeight) - 3);
                    int cardW = colWidth - 8;

                    Rectangle cardRect = new Rectangle(colX + 4, cardY + 2, cardW, cardH);
                    appt.Bounds = cardRect;

                    // Skip if out of viewport
                    if (cardRect.Right < TimeColWidth || cardRect.Left > viewWidth || cardRect.Bottom < HeaderHeight || cardRect.Top > viewHeight)
                    {
                        continue;
                    }

                    // Card colors based on status & Zenoti palette
                    Color cardBg;
                    Color cardBorder;
                    Color textColor;
                    Color subTextColor;

                    bool isHighlighted = !string.IsNullOrEmpty(searchQuery) &&
                        ((appt.CustomerName != null && appt.CustomerName.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (appt.CustomerPhone != null && appt.CustomerPhone.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0) ||
                         (appt.ServiceNames != null && appt.ServiceNames.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0));

                    GetAppointmentColors(appt.Status, out cardBg, out cardBorder, out textColor, out subTextColor);

                    if (appt.Id == selectedApptId)
                    {
                        cardBorder = Color.FromArgb(245, 158, 11); // Amber / Gold highlight border
                    }

                    // Draw rounded card
                    using (GraphicsPath path = GetRoundedRectangle(cardRect, 6))
                    using (SolidBrush bBrush = new SolidBrush(cardBg))
                    using (Pen bPen = new Pen(cardBorder, (appt.Id == selectedApptId || isHighlighted) ? 2.5F : 1.2F))
                    {
                        g.FillPath(bBrush, path);
                        g.DrawPath(bPen, path);

                        // Draw left status accent bar
                        Rectangle statusStrip = new Rectangle(cardRect.Left, cardRect.Top, 4, cardRect.Height);
                        using (GraphicsPath stripPath = GetLeftRoundedRectangle(statusStrip, 6))
                        using (SolidBrush stripBrush = new SolidBrush(cardBorder))
                        {
                            g.FillPath(stripBrush, stripPath);
                        }
                    }

                    // Content text
                    int textPadX = cardRect.Left + 8;
                    int textPadY = cardRect.Top + 4;
                    int textW = cardRect.Width - 14;

                    // Line 1: Client Name (+ Phone)
                    string clientStr = $"{appt.CustomerName ?? "Walk-in"}";
                    if (!string.IsNullOrEmpty(appt.CustomerPhone) && appt.CustomerPhone != "0000000000")
                    {
                        clientStr += $" ({appt.CustomerPhone})";
                    }

                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    using (SolidBrush subBrush = new SolidBrush(subTextColor))
                    {
                        RectangleF r1 = new RectangleF(textPadX, textPadY, textW, 16);
                        g.DrawString(clientStr, titleFont, textBrush, r1, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

                        // Line 2: Services
                        if (cardH >= 34)
                        {
                            string srvStr = appt.ServiceNames ?? "Service";
                            RectangleF r2 = new RectangleF(textPadX, textPadY + 14, textW, 15);
                            g.DrawString(srvStr, srvFont, subBrush, r2, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                        }

                        // Line 3: Time Slot + Price
                        if (cardH >= 54)
                        {
                            string timePriceStr = $"{appt.StartTime:hh:mm tt} ({durMinutes}m) • Rs. {appt.TotalAmount:N0}";
                            RectangleF r3 = new RectangleF(textPadX, textPadY + 28, textW, 14);
                            g.DrawString(timePriceStr, subFont, subBrush, r3, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
                        }
                    }

                    // Render 4 Quick Status Action Buttons Row [📅 Book] [🪑 Chair] [✅ Done] [🧾 Bill]
                    int rowY = (cardH >= 70) ? (cardRect.Top + 46) : ((cardH >= 50) ? (cardRect.Top + 30) : (cardRect.Top + 14));
                    int btnH = 20;
                    int totalAvailableW = cardRect.Width - 14;
                    int gap = 3;
                    int bW = Math.Max(26, (totalAvailableW - (gap * 3)) / 4);

                    int b1X = cardRect.Left + 7;
                    int b2X = b1X + bW + gap;
                    int b3X = b2X + bW + gap;
                    int b4X = b3X + bW + gap;

                    appt.BtnBookBounds = new Rectangle(b1X, rowY, bW, btnH);
                    appt.BtnChairBounds = new Rectangle(b2X, rowY, bW, btnH);
                    appt.BtnDoneBounds = new Rectangle(b3X, rowY, bW, btnH);
                    appt.BtnBillBounds = new Rectangle(b4X, rowY, bW, btnH);

                    if (cardH >= 34)
                    {
                        string billLabel = (appt.Status == "Billed") ? "Paid" : "Bill";

                        DrawCardActionButton(g, appt.BtnBookBounds, "Book", appt.Status == "Booked", hoveredButtonKey == $"book_{appt.Id}", Color.FromArgb(234, 88, 12), Color.FromArgb(251, 146, 60));
                        DrawCardActionButton(g, appt.BtnChairBounds, "Chair", appt.Status == "In-Chair", hoveredButtonKey == $"chair_{appt.Id}", Color.FromArgb(8, 145, 178), Color.FromArgb(34, 211, 238));
                        DrawCardActionButton(g, appt.BtnDoneBounds, "Done", appt.Status == "Completed", hoveredButtonKey == $"done_{appt.Id}", Color.FromArgb(5, 150, 105), Color.FromArgb(52, 211, 153));
                        DrawCardActionButton(g, appt.BtnBillBounds, billLabel, appt.Status == "Billed", hoveredButtonKey == $"bill_{appt.Id}", Color.FromArgb(79, 70, 229), Color.FromArgb(165, 180, 252));
                    }
                    else
                    {
                        appt.BtnBookBounds = Rectangle.Empty;
                        appt.BtnChairBounds = Rectangle.Empty;
                        appt.BtnDoneBounds = Rectangle.Empty;
                        appt.BtnBillBounds = Rectangle.Empty;
                    }

                    // Draw prominent bottom resize grip handle
                    int handleW = Math.Max(36, Math.Min(64, cardRect.Width / 3));
                    int handleH = 4;
                    int handleX = cardRect.Left + (cardRect.Width - handleW) / 2;
                    int handleY = cardRect.Bottom - 6;
                    bool isHandleHovered = (hoveredResizeApptId == appt.Id || (isResizing && resizingAppt?.Id == appt.Id));
                    Color handleColor = isHandleHovered ? Color.FromArgb(249, 115, 22) : Color.FromArgb(140, 255, 255, 255);

                    using (GraphicsPath handlePath = GetRoundedRectangle(new Rectangle(handleX, handleY, handleW, handleH), 2))
                    using (SolidBrush handleBrush = new SolidBrush(handleColor))
                    {
                        g.FillPath(handleBrush, handlePath);
                        if (isHandleHovered)
                        {
                            using (Pen glowPen = new Pen(Color.FromArgb(254, 215, 170), 1))
                            {
                                g.DrawPath(glowPen, handlePath);
                            }
                        }
                    }
                }
            }

            // Draw Live Ghost Resizing Preview & Floating Tooltip if dragging
            if (isResizing && resizingAppt != null)
            {
                int colIdx = stylists.FindIndex(s => s.Id == resizingAppt.StaffId);
                if (colIdx < 0) colIdx = 0;
                int colX = TimeColWidth + (colIdx * colWidth) - scrollX;
                int startSlot = GetSlotIndexFromTime(resizingAppt.StartTime);
                int targetSlotSpan = Math.Max(1, (int)Math.Ceiling((double)resizingNewMinutes / SlotIntervalMinutes));

                int ghostY = HeaderHeight + (startSlot * SlotHeight) - scrollY;
                int ghostH = Math.Max(SlotHeight - 2, (targetSlotSpan * SlotHeight) - 3);
                int ghostW = colWidth - 8;
                Rectangle ghostRect = new Rectangle(colX + 4, ghostY + 2, ghostW, ghostH);

                DateTime newEnd = resizingAppt.StartTime.AddMinutes(resizingNewMinutes);
                int delta = resizingNewMinutes - (resizingAppt.DurationMinutes > 0 ? resizingAppt.DurationMinutes : SlotIntervalMinutes);
                string deltaStr = delta > 0 ? $"+{delta}m" : (delta < 0 ? $"{delta}m" : "0m");

                var conflictAppt = appointments.FirstOrDefault(a => a.Id != resizingAppt.Id && a.StaffId == resizingAppt.StaffId && a.Status != "Cancelled" && resizingAppt.StartTime < a.EndTime && newEnd > a.StartTime);
                bool hasConflict = (conflictAppt != null);

                Color ghostBorderColor = hasConflict ? Color.FromArgb(239, 68, 68) : Color.FromArgb(249, 115, 22);
                Color ghostFillColor = hasConflict ? Color.FromArgb(60, 239, 68, 68) : Color.FromArgb(45, 249, 115, 22);
                Color tipBorderColor = hasConflict ? Color.FromArgb(239, 68, 68) : Color.FromArgb(249, 115, 22);
                Color tipTextColor = hasConflict ? Color.FromArgb(254, 202, 202) : Color.FromArgb(254, 215, 170);

                string tooltipText = hasConflict
                    ? $"⛔ OVERLAP CONFLICT with {conflictAppt.CustomerName} ({conflictAppt.StartTime:hh:mm tt})"
                    : $"🕒 {resizingAppt.StartTime:hh:mm tt} – {newEnd:hh:mm tt} ({resizingNewMinutes} mins) [{deltaStr}]";

                // Render semi-transparent glowing overlay
                using (GraphicsPath ghostPath = GetRoundedRectangle(ghostRect, 6))
                using (SolidBrush ghostBrush = new SolidBrush(ghostFillColor))
                using (Pen ghostPen = new Pen(ghostBorderColor, 2F) { DashStyle = DashStyle.Dash })
                {
                    g.FillPath(ghostBrush, ghostPath);
                    g.DrawPath(ghostPen, ghostPath);
                }

                using (Font tipFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold))
                {
                    SizeF tipSize = g.MeasureString(tooltipText, tipFont);
                    int tipW = (int)tipSize.Width + 18;
                    int tipH = (int)tipSize.Height + 10;
                    int tipX = Math.Min(viewWidth - tipW - 8, Math.Max(TimeColWidth + 8, ghostRect.Left));
                    int tipY = ghostRect.Bottom + 4;
                    if (tipY + tipH > viewHeight) tipY = ghostRect.Top - tipH - 4;

                    Rectangle tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                    using (GraphicsPath tipPath = GetRoundedRectangle(tipRect, 5))
                    using (SolidBrush tipBg = new SolidBrush(Color.FromArgb(245, 15, 23, 42)))
                    using (Pen tipBorder = new Pen(tipBorderColor, 1.5F))
                    using (SolidBrush tipTextBrush = new SolidBrush(tipTextColor))
                    {
                        g.FillPath(tipBg, tipPath);
                        g.DrawPath(tipBorder, tipPath);
                        g.DrawString(tooltipText, tipFont, tipTextBrush, tipRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                }
            }

            // Draw Live Ghost Move / Reschedule Preview if dragging
            if (isMoving && movingAppt != null && targetColIndex >= 0 && targetColIndex < stylists.Count && targetSlotIndex >= 0)
            {
                int colX = TimeColWidth + (targetColIndex * colWidth) - scrollX;
                int targetSlotSpan = Math.Max(1, (int)Math.Ceiling((double)(movingAppt.DurationMinutes > 0 ? movingAppt.DurationMinutes : SlotIntervalMinutes) / SlotIntervalMinutes));

                int ghostY = HeaderHeight + (targetSlotIndex * SlotHeight) - scrollY;
                int ghostH = Math.Max(SlotHeight - 2, (targetSlotSpan * SlotHeight) - 3);
                int ghostW = colWidth - 8;
                Rectangle ghostRect = new Rectangle(colX + 4, ghostY + 2, ghostW, ghostH);

                string targetStylistName = stylists[targetColIndex].Name;
                var conflictAppt = appointments.FirstOrDefault(a => a.Id != movingAppt.Id && a.StaffId == targetStaffId && a.Status != "Cancelled" && targetStartTime < a.EndTime && targetEndTime > a.StartTime);
                bool hasConflict = (conflictAppt != null);

                Color ghostBorderColor = hasConflict ? Color.FromArgb(239, 68, 68) : Color.FromArgb(59, 130, 246);
                Color ghostFillColor = hasConflict ? Color.FromArgb(60, 239, 68, 68) : Color.FromArgb(60, 59, 130, 246);
                Color tipBorderColor = hasConflict ? Color.FromArgb(239, 68, 68) : Color.FromArgb(59, 130, 246);
                Color tipTextColor = hasConflict ? Color.FromArgb(254, 202, 202) : Color.FromArgb(191, 219, 254);

                string tooltipText = hasConflict
                    ? $"⛔ OVERLAP CONFLICT: {targetStylistName} already booked ({conflictAppt.CustomerName} • {conflictAppt.StartTime:hh:mm tt})"
                    : $"📍 {targetStylistName} • 🕒 {targetStartTime:hh:mm tt} – {targetEndTime:hh:mm tt} (Release to Move)";

                // Render semi-transparent glowing overlay
                using (GraphicsPath ghostPath = GetRoundedRectangle(ghostRect, 6))
                using (SolidBrush ghostBrush = new SolidBrush(ghostFillColor))
                using (Pen ghostPen = new Pen(ghostBorderColor, 2.2F) { DashStyle = DashStyle.Dash })
                {
                    g.FillPath(ghostBrush, ghostPath);
                    g.DrawPath(ghostPen, ghostPath);
                }

                using (Font tipFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold))
                {
                    SizeF tipSize = g.MeasureString(tooltipText, tipFont);
                    int tipW = (int)tipSize.Width + 20;
                    int tipH = (int)tipSize.Height + 10;
                    int tipX = Math.Min(viewWidth - tipW - 8, Math.Max(TimeColWidth + 8, ghostRect.Left));
                    int tipY = ghostRect.Bottom + 4;
                    if (tipY + tipH > viewHeight) tipY = ghostRect.Top - tipH - 4;

                    Rectangle tipRect = new Rectangle(tipX, tipY, tipW, tipH);
                    using (GraphicsPath tipPath = GetRoundedRectangle(tipRect, 5))
                    using (SolidBrush tipBg = new SolidBrush(Color.FromArgb(245, 15, 23, 42)))
                    using (Pen tipBorder = new Pen(tipBorderColor, 1.5F))
                    using (SolidBrush tipTextBrush = new SolidBrush(tipTextColor))
                    {
                        g.FillPath(tipBg, tipPath);
                        g.DrawPath(tipBorder, tipPath);
                        g.DrawString(tooltipText, tipFont, tipTextBrush, tipRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    }
                }
            }

            // 3. Draw Sticky Left Time Column
            Rectangle timeColRect = new Rectangle(0, HeaderHeight, TimeColWidth, viewHeight - HeaderHeight);
            using (SolidBrush timeBgBrush = new SolidBrush(Color.FromArgb(17, 24, 39)))
            using (Pen timeBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1.5F))
            {
                g.FillRectangle(timeBgBrush, timeColRect);
                g.DrawLine(timeBorderPen, TimeColWidth, HeaderHeight, TimeColWidth, viewHeight);

                using (Font hourFont = new Font("Segoe UI", 8F, FontStyle.Bold))
                using (Font minFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
                using (SolidBrush hourBrush = new SolidBrush(Color.FromArgb(226, 232, 240)))
                using (SolidBrush minBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (Pen subLinePen = new Pen(Color.FromArgb(30, 41, 59), 1))
                using (Pen hrLinePen = new Pen(Color.FromArgb(71, 85, 105), 1))
                using (Pen tickPen = new Pen(Color.FromArgb(40, 53, 72), 1))
                {
                    for (int slot = 0; slot < TotalSlots; slot++)
                    {
                        int slotY = HeaderHeight + (slot * SlotHeight) - scrollY;
                        if (slotY + SlotHeight < HeaderHeight || slotY > viewHeight) continue;

                        int minutesFromStart = slot * SlotIntervalMinutes;
                        int hour = StartHour + (minutesFromStart / 60);
                        int minute = minutesFromStart % 60;

                        DateTime dt = DateTime.Today.AddHours(hour).AddMinutes(minute);

                        if (minute == 0)
                        {
                            // Hour block
                            g.DrawLine(hrLinePen, 0, slotY, TimeColWidth, slotY);
                            RectangleF hrRect = new RectangleF(4, slotY + 1, TimeColWidth - 8, Math.Max(12, SlotHeight * 2));
                            g.DrawString($"{dt:hh tt}", hourFont, hourBrush, hrRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                        }
                        else if (minute == 15 || minute == 30 || minute == 45)
                        {
                            g.DrawLine(subLinePen, 20, slotY, TimeColWidth, slotY);
                            RectangleF minRect = new RectangleF(10, slotY + 1, TimeColWidth - 14, Math.Max(11, SlotHeight * 2));
                            g.DrawString($"{dt:hh:mm}", minFont, minBrush, minRect, new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center });
                        }
                        else
                        {
                            // Subtle 5-minute tick mark
                            g.DrawLine(tickPen, TimeColWidth - 6, slotY, TimeColWidth, slotY);
                        }
                    }
                }
            }

            // 4. Draw Sticky Top Stylist Header
            Rectangle headerRect = new Rectangle(0, 0, viewWidth, HeaderHeight);
            using (SolidBrush headerBgBrush = new SolidBrush(Color.FromArgb(24, 30, 44)))
            using (Pen headerBorderPen = new Pen(Color.FromArgb(51, 65, 85), 1.5F))
            {
                g.FillRectangle(headerBgBrush, headerRect);
                g.DrawLine(headerBorderPen, 0, HeaderHeight, viewWidth, HeaderHeight);

                // Top Left Corner ("Time")
                Rectangle cornerRect = new Rectangle(0, 0, TimeColWidth, HeaderHeight);
                using (SolidBrush cornerBrush = new SolidBrush(Color.FromArgb(17, 24, 39)))
                using (Font cornerFont = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold))
                using (SolidBrush cornerTextBrush = new SolidBrush(Color.FromArgb(203, 213, 225)))
                {
                    g.FillRectangle(cornerBrush, cornerRect);
                    g.DrawString("Time", cornerFont, cornerTextBrush, cornerRect, new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
                    g.DrawLine(headerBorderPen, TimeColWidth, 0, TimeColWidth, HeaderHeight);
                }

                // Draw Column Headers
                using (Font nameFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                using (Font roleFont = new Font("Segoe UI", 7.5F, FontStyle.Regular))
                using (Font utilFont = new Font("Segoe UI", 7.5F, FontStyle.Bold))
                using (SolidBrush nameBrush = new SolidBrush(Color.FromArgb(241, 245, 249)))
                using (SolidBrush roleBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
                using (SolidBrush utilBrush = new SolidBrush(Color.FromArgb(56, 189, 248)))
                using (Pen colSepPen = new Pen(Color.FromArgb(45, 55, 72), 1))
                {
                    for (int c = 0; c < stylists.Count; c++)
                    {
                        int colX = TimeColWidth + (c * colWidth) - scrollX;
                        if (colX + colWidth < TimeColWidth || colX > viewWidth) continue;

                        var st = stylists[c];

                        // Top accent strip (Zenoti style colored indicator)
                        using (SolidBrush accentBrush = new SolidBrush(st.AccentColor))
                        {
                            g.FillRectangle(accentBrush, colX + 2, 0, colWidth - 4, 4);
                        }

                        // Line 1: Stylist Name
                        RectangleF nameRect = new RectangleF(colX + 8, 8, colWidth - 16, 16);
                        g.DrawString(st.Name, nameFont, nameBrush, nameRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

                        // Line 2: Role
                        RectangleF roleRect = new RectangleF(colX + 8, 25, colWidth - 16, 14);
                        g.DrawString(st.Role ?? "Stylist", roleFont, roleBrush, roleRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

                        // Line 3: Utilization Badge (Zenoti style Utilization: (41.67%))
                        string utilText = $"Utilization: ({st.UtilizationPercent:F1}%)";
                        RectangleF utilRect = new RectangleF(colX + 8, 40, colWidth - 16, 14);
                        g.DrawString(utilText, utilFont, utilBrush, utilRect, new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

                        // Column separator line
                        g.DrawLine(colSepPen, colX + colWidth, 0, colX + colWidth, HeaderHeight);
                    }
                }
            }
        }

        private static int GetSlotIndexFromTime(DateTime dt)
        {
            int hour = dt.Hour;
            int minute = dt.Minute;
            if (hour < StartHour) return 0;
            if (hour >= EndHour) return TotalSlots - 1;

            int minutesFromStart = ((hour - StartHour) * 60) + minute;
            return Math.Max(0, Math.Min(TotalSlots - 1, minutesFromStart / SlotIntervalMinutes));
        }

        private static void GetAppointmentColors(string status, out Color bg, out Color border, out Color text, out Color subText)
        {
            switch (status)
            {
                case "In-Chair":
                    bg = Color.FromArgb(14, 116, 144);       // Rich Cyan
                    border = Color.FromArgb(6, 182, 212);    // Aqua
                    text = Color.White;
                    subText = Color.FromArgb(207, 250, 254);
                    break;
                case "Completed":
                    bg = Color.FromArgb(6, 95, 70);          // Forest Green
                    border = Color.FromArgb(16, 185, 129);   // Emerald
                    text = Color.White;
                    subText = Color.FromArgb(209, 250, 229);
                    break;
                case "Billed":
                    bg = Color.FromArgb(67, 56, 202);        // Indigo
                    border = Color.FromArgb(129, 140, 248);  // Purple / Light Indigo
                    text = Color.White;
                    subText = Color.FromArgb(224, 231, 255);
                    break;
                case "Cancelled":
                    bg = Color.FromArgb(51, 65, 85);         // Slate Grey
                    border = Color.FromArgb(100, 116, 139);
                    text = Color.FromArgb(203, 213, 225);
                    subText = Color.FromArgb(148, 163, 184);
                    break;
                case "Booked":
                default:
                    // Zenoti Signature Warm Beige / Caramel
                    bg = Color.FromArgb(217, 180, 142);
                    border = Color.FromArgb(180, 130, 85);
                    text = Color.FromArgb(30, 20, 10);
                    subText = Color.FromArgb(60, 40, 20);
                    break;
            }
        }

        private static void DrawCardActionButton(Graphics g, Rectangle r, string text, bool isActive, bool isHovered, Color activeBg, Color activeBorder)
        {
            if (r.Width <= 0 || r.Height <= 0) return;

            Color bg;
            Color border;
            Color txtColor;
            float borderWidth = 1.0F;

            if (isActive)
            {
                // Full high-contrast solid active fill
                bg = activeBg;
                border = isHovered ? Color.White : activeBorder;
                txtColor = Color.White;
                borderWidth = 2.0F;
            }
            else if (isHovered)
            {
                bg = Color.FromArgb(220, 45, 55, 72);
                border = Color.White;
                txtColor = Color.White;
                borderWidth = 1.5F;
            }
            else
            {
                // Subtle dark translucent inactive state
                bg = Color.FromArgb(170, 20, 28, 40);
                border = Color.FromArgb(100, 100, 116, 139);
                txtColor = Color.FromArgb(180, 195, 210);
            }

            using (GraphicsPath p = GetRoundedRectangle(r, 3))
            using (SolidBrush b = new SolidBrush(bg))
            using (Pen pen = new Pen(border, borderWidth))
            using (SolidBrush tb = new SolidBrush(txtColor))
            using (Font f = new Font("Segoe UI", isActive ? 7.8F : 7.2F, FontStyle.Bold))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);

                StringFormat sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                g.DrawString(text, f, tb, r, sf);
            }
        }

        private static GraphicsPath GetRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;

            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            // Top left
            path.AddArc(arc, 180, 90);
            // Top right
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            // Bottom right
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            // Bottom left
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }

        private static GraphicsPath GetLeftRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0) return path;

            int diameter = radius * 2;
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

            path.AddArc(arc, 180, 90);
            path.AddLine(bounds.Right, bounds.Top, bounds.Right, bounds.Bottom);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private class DarkMenuRenderer : ToolStripProfessionalRenderer
        {
            public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }

            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                if (e.Item is ToolStripMenuItem mi)
                {
                    if (!mi.Enabled)
                    {
                        e.TextColor = Color.FromArgb(148, 163, 184); // Muted slate for disabled header
                    }
                    else if (mi.Selected)
                    {
                        e.TextColor = Color.FromArgb(255, 255, 255); // Crisp White for selected
                    }
                    else
                    {
                        e.TextColor = Color.FromArgb(241, 245, 249); // Clean Light text
                    }
                }
                base.OnRenderItemText(e);
            }

            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected && e.Item.Enabled)
                {
                    Rectangle rc = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
                    using (SolidBrush b = new SolidBrush(Color.FromArgb(51, 65, 85)))
                    using (Pen p = new Pen(Color.FromArgb(59, 130, 246), 1F))
                    {
                        e.Graphics.FillRectangle(b, rc);
                        e.Graphics.DrawRectangle(p, rc.X, rc.Y, rc.Width - 1, rc.Height - 1);
                    }
                }
                else
                {
                    base.OnRenderMenuItemBackground(e);
                }
            }

            protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
            {
                e.ArrowColor = Color.FromArgb(203, 213, 225);
                base.OnRenderArrow(e);
            }
        }

        private class DarkMenuColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(51, 65, 85);
            public override Color MenuItemSelectedGradientBegin => Color.FromArgb(51, 65, 85);
            public override Color MenuItemSelectedGradientEnd => Color.FromArgb(51, 65, 85);
            public override Color MenuItemBorder => Color.FromArgb(71, 85, 105);
            public override Color MenuBorder => Color.FromArgb(51, 65, 85);
            public override Color ToolStripDropDownBackground => Color.FromArgb(17, 24, 39);
            public override Color ImageMarginGradientBegin => Color.FromArgb(17, 24, 39);
            public override Color ImageMarginGradientMiddle => Color.FromArgb(17, 24, 39);
            public override Color ImageMarginGradientEnd => Color.FromArgb(17, 24, 39);
            public override Color SeparatorDark => Color.FromArgb(51, 65, 85);
            public override Color SeparatorLight => Color.FromArgb(30, 41, 59);
        }
    }
}
